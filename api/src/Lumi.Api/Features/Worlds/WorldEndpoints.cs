using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Lumi.Api.Features.Config;
using Lumi.Api.Features.Rivers;
using Lumi.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

namespace Lumi.Api.Features.Worlds;

/// <summary>
/// Snapshot đảo cho người khác thăm (giai đoạn 3). Snapshot là bản rút gọn của WorldSaveData:
/// { version, score, tiles: [...], placements: [...] }.
/// </summary>
public static class WorldEndpoints
{
    public const int MaxSnapshotBytes = 300 * 1024;
    public const int SupportedVersion = 1;
    private const int MaxPageSize = 50;
    /// <summary>Lưới bản đồ rộng 10 cột (khớp migration 003_world_map.sql).</summary>
    public const int MapColumns = 10;
    private const int MaxMapSpan = 20;
    private const int MaxSearchResults = 10;
    private const int MaxSearchLength = 32;

    public static void MapWorldEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/worlds").RequireAuthorization();
        group.MapPut("/me", Publish);
        group.MapDelete("/me", Unpublish);
        group.MapGet("/", Gallery);
        group.MapGet("/map", Map);
        group.MapGet("/search", Search);
        group.MapGet("/{playerId:guid}", GetOne);
    }

    private static async Task<IResult> Publish(
        ClaimsPrincipal user,
        [FromBody] JsonElement snapshot,
        NpgsqlDataSource db,
        GameConfigService config,
        CancellationToken ct)
    {
        string json = snapshot.GetRawText();
        if (Encoding.UTF8.GetByteCount(json) > MaxSnapshotBytes)
            return ApiProblem.Create(413, ErrorCodes.SnapshotTooLarge, "The island snapshot is larger than 300 KB.");

        if (snapshot.ValueKind != JsonValueKind.Object
            || !snapshot.TryGetProperty("version", out var versionElement)
            || !versionElement.TryGetInt32(out int version))
        {
            return ApiProblem.Create(400, ErrorCodes.SnapshotInvalid, "The snapshot has no version.");
        }

        if (version != SupportedVersion)
            return ApiProblem.Create(400, ErrorCodes.SnapshotVersionUnsupported, "This snapshot version is not supported.");

        if (!snapshot.TryGetProperty("tiles", out var tiles) || tiles.ValueKind != JsonValueKind.Array
            || !snapshot.TryGetProperty("placements", out var placements) || placements.ValueKind != JsonValueKind.Array)
        {
            return ApiProblem.Create(400, ErrorCodes.SnapshotInvalid, "The snapshot needs tiles and placements.");
        }

        var settings = await config.GetAsync(ct);
        if (tiles.GetArrayLength() == 0 || tiles.GetArrayLength() > settings.WorldMaxTiles)
            return ApiProblem.Create(400, ErrorCodes.SnapshotInvalid, "The snapshot has too many or too few tiles.");

        foreach (var placement in placements.EnumerateArray())
        {
            if (placement.ValueKind != JsonValueKind.Object
                || !placement.TryGetProperty("cardID", out var cardId)
                || cardId.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(cardId.GetString())
                || cardId.GetString()!.Length > 64)
            {
                return ApiProblem.Create(400, ErrorCodes.SnapshotInvalid, "Every placement needs a cardID.");
            }
        }

        if (RiverEndpoints.Validate(snapshot) is string riverError)
            return ApiProblem.Create(400, ErrorCodes.SnapshotInvalid, riverError);

        int score = snapshot.TryGetProperty("score", out var scoreElement) && scoreElement.TryGetInt32(out int s)
            ? Math.Max(0, s)
            : 0;

        Guid playerId = user.PlayerId();
        await using var conn = await db.OpenConnectionAsync(ct);

        await using (var upsert = new NpgsqlCommand(
            """
            insert into world_snapshots (player_id, version, data, score) values (@p, @v, @d, @s)
            on conflict (player_id) do update
            set version = excluded.version, data = excluded.data, score = excluded.score, updated_at = now()
            """, conn))
        {
            upsert.Parameters.AddWithValue("p", playerId);
            upsert.Parameters.AddWithValue("v", version);
            upsert.Parameters.Add(new NpgsqlParameter("d", NpgsqlDbType.Jsonb) { Value = json });
            upsert.Parameters.AddWithValue("s", score);
            await upsert.ExecuteNonQueryAsync(ct);
        }

        var (x, y) = await EnsurePositionAsync(conn, playerId, ct);

        await ActionLog.WriteAsync(conn, playerId, "world.publish", new { score }, ct: ct);
        return Results.Ok(new { published = true, score, x, y });
    }

    /// <summary>
    /// Cấp ô bản đồ cho lần chia sẻ đầu tiên; đã có thì giữ nguyên.
    /// Kiểm tra trước khi lấy số mới để không đốt slot (lỗ trên lưới) mỗi lần chia sẻ lại.
    /// </summary>
    private static async Task<(int X, int Y)> EnsurePositionAsync(NpgsqlConnection conn, Guid playerId, CancellationToken ct)
    {
        const string read = "select x, y from world_positions where player_id = @p";

        await using (var find = new NpgsqlCommand(read, conn))
        {
            find.Parameters.AddWithValue("p", playerId);
            await using var reader = await find.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
                return (reader.GetInt32(0), reader.GetInt32(1));
        }

        await using (var insert = new NpgsqlCommand(
            """
            insert into world_positions (player_id, slot, x, y)
            select @p, s, s % @cols + 1, s / @cols + 1 from (select nextval('world_slot_seq')::int as s) next_slot
            on conflict (player_id) do nothing
            """, conn))
        {
            insert.Parameters.AddWithValue("p", playerId);
            insert.Parameters.AddWithValue("cols", MapColumns);
            await insert.ExecuteNonQueryAsync(ct);
        }

        // Hai lần chia sẻ cùng lúc: một bên thắng, đọc lại ô đã lưu
        await using var again = new NpgsqlCommand(read, conn);
        again.Parameters.AddWithValue("p", playerId);
        await using var row = await again.ExecuteReaderAsync(ct);
        await row.ReadAsync(ct);
        return (row.GetInt32(0), row.GetInt32(1));
    }

    private static async Task<IResult> Unpublish(ClaimsPrincipal user, NpgsqlDataSource db, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("delete from world_snapshots where player_id = @p", conn);
        cmd.Parameters.AddWithValue("p", user.PlayerId());
        await cmd.ExecuteNonQueryAsync(ct);
        return Results.NoContent();
    }

    /// <summary>GET /api/v1/worlds?sort=recent|score&amp;page=1&amp;pageSize=20</summary>
    private static async Task<IResult> Gallery(
        NpgsqlDataSource db,
        string? sort,
        int? page,
        int? pageSize,
        CancellationToken ct)
    {
        int size = Math.Clamp(pageSize ?? 20, 1, MaxPageSize);
        int pageNumber = Math.Max(page ?? 1, 1);
        string orderBy = sort == "score" ? "w.score desc, w.updated_at desc" : "w.updated_at desc";

        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            $"""
            select w.player_id, p.display_name, w.score, w.updated_at, pos.x, pos.y
            from world_snapshots w
            join players p on p.id = w.player_id
            join world_positions pos on pos.player_id = w.player_id
            where not p.is_suspended
            order by {orderBy}
            limit @take offset @skip
            """, conn);
        // Lấy dư một dòng để biết còn trang sau
        cmd.Parameters.AddWithValue("take", size + 1);
        cmd.Parameters.AddWithValue("skip", (pageNumber - 1) * size);

        var items = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new
            {
                playerId = reader.GetGuid(0).ToString(),
                displayName = reader.GetString(1),
                score = reader.GetInt32(2),
                updatedAt = reader.GetFieldValue<DateTimeOffset>(3).ToString("o", CultureInfo.InvariantCulture),
                x = reader.GetInt32(4),
                y = reader.GetInt32(5)
            });
        }

        bool hasMore = items.Count > size;
        if (hasMore)
            items.RemoveAt(items.Count - 1);

        return Results.Ok(new { items, page = pageNumber, hasMore });
    }

    private static async Task<IResult> GetOne(Guid playerId, ClaimsPrincipal user, NpgsqlDataSource db, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            select p.display_name, w.score, w.updated_at, w.data::text, pos.x, pos.y
            from world_snapshots w
            join players p on p.id = w.player_id
            join world_positions pos on pos.player_id = w.player_id
            where w.player_id = @p and not p.is_suspended
            """, conn);
        cmd.Parameters.AddWithValue("p", playerId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return ApiProblem.Create(404, ErrorCodes.WorldNotFound, "This island isn't shared.");

        string displayName = reader.GetString(0);
        int score = reader.GetInt32(1);
        DateTimeOffset updatedAt = reader.GetFieldValue<DateTimeOffset>(2);
        using var data = JsonDocument.Parse(reader.GetString(3));
        int x = reader.GetInt32(4);
        int y = reader.GetInt32(5);
        await reader.CloseAsync();

        // Lượt thăm để số liệu admin đếm được; xem đảo của chính mình thì không tính
        Guid visitorId = user.PlayerId();
        if (visitorId != playerId)
            await ActionLog.WriteAsync(conn, visitorId, "world.visit", new { ownerId = playerId }, ct: ct);

        return Results.Ok(new
        {
            playerId = playerId.ToString(),
            displayName,
            score,
            updatedAt = updatedAt.ToString("o", CultureInfo.InvariantCulture),
            x,
            y,
            snapshot = data.RootElement.Clone()
        });
    }

    /// <summary>
    /// GET /api/v1/worlds/map?x=1&amp;y=1&amp;w=10&amp;h=10: các đảo đang chia sẻ trong vùng [x, x+w) × [y, y+h).
    /// Trả thêm columns và rows (kích thước lưới hiện có) để client biết giới hạn khi kéo bản đồ.
    /// </summary>
    private static async Task<IResult> Map(NpgsqlDataSource db, int? x, int? y, int? w, int? h, CancellationToken ct)
    {
        int left = Math.Max(x ?? 1, 1);
        int top = Math.Max(y ?? 1, 1);
        int width = Math.Clamp(w ?? MapColumns, 1, MaxMapSpan);
        int height = Math.Clamp(h ?? MapColumns, 1, MaxMapSpan);

        await using var conn = await db.OpenConnectionAsync(ct);

        int rows;
        await using (var size = new NpgsqlCommand("select coalesce(max(y), 0) from world_positions", conn))
            rows = Convert.ToInt32(await size.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);

        await using var cmd = new NpgsqlCommand(
            """
            select w.player_id, p.display_name, w.score, pos.x, pos.y
            from world_positions pos
            join world_snapshots w on w.player_id = pos.player_id
            join players p on p.id = pos.player_id
            where not p.is_suspended
              and pos.x >= @x and pos.x < @x + @w
              and pos.y >= @y and pos.y < @y + @h
            order by pos.y, pos.x
            """, conn);
        cmd.Parameters.AddWithValue("x", left);
        cmd.Parameters.AddWithValue("y", top);
        cmd.Parameters.AddWithValue("w", width);
        cmd.Parameters.AddWithValue("h", height);

        var islands = await ReadIslandsAsync(cmd, ct);
        return Results.Ok(new { x = left, y = top, w = width, h = height, columns = MapColumns, rows, islands });
    }

    /// <summary>
    /// GET /api/v1/worlds/search?q=: tìm đảo đang chia sẻ theo tên chủ đảo (không phân biệt hoa thường),
    /// hoặc theo toạ độ dạng "3:2" / "[3:2]". Tên bắt đầu bằng q xếp trước, rồi tới điểm cao.
    /// </summary>
    private static async Task<IResult> Search(NpgsqlDataSource db, string? q, CancellationToken ct)
    {
        string query = (q ?? "").Trim();
        if (query.Length == 0 || query.Length > MaxSearchLength)
            return ApiProblem.Create(400, ErrorCodes.SearchInvalid, "Type 1 to 32 characters to search.");

        await using var conn = await db.OpenConnectionAsync(ct);

        var coords = System.Text.RegularExpressions.Regex.Match(query, @"^\[?\s*(\d{1,4})\s*[:,]\s*(\d{1,4})\s*\]?$");
        NpgsqlCommand cmd;
        if (coords.Success)
        {
            cmd = new NpgsqlCommand(
                """
                select w.player_id, p.display_name, w.score, pos.x, pos.y
                from world_positions pos
                join world_snapshots w on w.player_id = pos.player_id
                join players p on p.id = pos.player_id
                where not p.is_suspended and pos.x = @x and pos.y = @y
                """, conn);
            cmd.Parameters.AddWithValue("x", int.Parse(coords.Groups[1].Value, CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("y", int.Parse(coords.Groups[2].Value, CultureInfo.InvariantCulture));
        }
        else
        {
            // Thoát ký tự đại diện của LIKE để "%" hay "_" trong tên được tìm đúng nghĩa đen
            string pattern = query.ToLowerInvariant().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            cmd = new NpgsqlCommand(
                """
                select w.player_id, p.display_name, w.score, pos.x, pos.y
                from world_positions pos
                join world_snapshots w on w.player_id = pos.player_id
                join players p on p.id = pos.player_id
                where not p.is_suspended and lower(p.display_name) like '%' || @q || '%'
                order by (lower(p.display_name) like @q || '%') desc, w.score desc, p.display_name
                limit @take
                """, conn);
            cmd.Parameters.AddWithValue("q", pattern);
            cmd.Parameters.AddWithValue("take", MaxSearchResults);
        }

        await using (cmd)
        {
            var items = await ReadIslandsAsync(cmd, ct);
            return Results.Ok(new { items });
        }
    }

    private static async Task<List<object>> ReadIslandsAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        var islands = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            islands.Add(new
            {
                playerId = reader.GetGuid(0).ToString(),
                displayName = reader.GetString(1),
                score = reader.GetInt32(2),
                x = reader.GetInt32(3),
                y = reader.GetInt32(4)
            });
        }
        return islands;
    }
}
