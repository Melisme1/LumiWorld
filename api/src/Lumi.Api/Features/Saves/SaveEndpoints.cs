using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Lumi.Api.Features.Rivers;
using Lumi.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

namespace Lumi.Api.Features.Saves;

/// <summary>
/// Save duy nhất của mỗi người chơi: world (WorldSaveData) và kinh tế (EconomySaveData)
/// gửi lên cùng nhau. Server chỉ kiểm tra khung và kích thước, không đọc nội dung game,
/// nên client đổi WorldSaveData (tăng version) không cần sửa server.
/// </summary>
public static class SaveEndpoints
{
    /// <summary>Giới hạn mỗi phần (world hoặc economy) sau khi bỏ khoảng trắng.</summary>
    public const int MaxPartBytes = 1024 * 1024;

    public static void MapSaveEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/saves").RequireAuthorization();
        group.MapGet("/me", GetMine);
        group.MapPut("/me", PutMine);
    }

    private static async Task<IResult> GetMine(ClaimsPrincipal user, NpgsqlDataSource db, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "select revision, world::text, economy::text, updated_at from saves where player_id = @p", conn);
        cmd.Parameters.AddWithValue("p", user.PlayerId());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return ApiProblem.Create(404, ErrorCodes.SaveNotFound, "This account has no saved island yet.");

        using var world = JsonDocument.Parse(reader.GetString(1));
        using var economy = JsonDocument.Parse(reader.GetString(2));

        return Results.Ok(new
        {
            revision = reader.GetInt64(0),
            updatedAt = reader.GetFieldValue<DateTimeOffset>(3).ToString("o", CultureInfo.InvariantCulture),
            world = world.RootElement.Clone(),
            economy = economy.RootElement.Clone()
        });
    }

    /// <summary>
    /// Body: { baseRevision, world: {...}, economy: {...} }.
    /// baseRevision là revision client nhận được lần trước (0 khi chưa từng lưu lên server).
    /// Lệch với revision trên server nghĩa là máy khác đã lưu sau đó: trả 409 kèm currentRevision,
    /// client tải bản trên server về thay vì ghi đè.
    /// </summary>
    private static async Task<IResult> PutMine(
        ClaimsPrincipal user,
        [FromBody] JsonElement body,
        NpgsqlDataSource db,
        CancellationToken ct)
    {
        if (body.ValueKind != JsonValueKind.Object
            || !body.TryGetProperty("baseRevision", out var baseElement)
            || !baseElement.TryGetInt64(out long baseRevision)
            || baseRevision < 0
            || !body.TryGetProperty("world", out var world) || world.ValueKind != JsonValueKind.Object
            || !body.TryGetProperty("economy", out var economy) || economy.ValueKind != JsonValueKind.Object)
        {
            return ApiProblem.Create(400, ErrorCodes.SaveInvalid,
                "The save must contain baseRevision, world and economy.");
        }

        if (!world.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number)
            return ApiProblem.Create(400, ErrorCodes.SaveInvalid, "The world save has no version.");

        if (RiverEndpoints.Validate(world) is string riverError)
            return ApiProblem.Create(400, ErrorCodes.SaveInvalid, riverError);

        string worldJson = world.GetRawText();
        string economyJson = economy.GetRawText();

        if (Encoding.UTF8.GetByteCount(worldJson) > MaxPartBytes || Encoding.UTF8.GetByteCount(economyJson) > MaxPartBytes)
            return ApiProblem.Create(413, ErrorCodes.SaveTooLarge, "The save is too large.");

        Guid playerId = user.PlayerId();

        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        long? current = null;
        await using (var read = new NpgsqlCommand(
            "select revision from saves where player_id = @p for update", conn, tx))
        {
            read.Parameters.AddWithValue("p", playerId);
            if (await read.ExecuteScalarAsync(ct) is long value)
                current = value;
        }

        if ((current ?? 0) != baseRevision)
            return Conflict(current ?? 0);

        long next = baseRevision + 1;

        // Lần đầu: insert ... on conflict do nothing để hai máy cùng lưu lần đầu thì một máy nhận 409
        string sql = current == null
            ? """
              insert into saves (player_id, revision, world, economy) values (@p, @r, @w, @e)
              on conflict (player_id) do nothing
              """
            : "update saves set revision = @r, world = @w, economy = @e, updated_at = now() where player_id = @p";

        await using (var write = new NpgsqlCommand(sql, conn, tx))
        {
            write.Parameters.AddWithValue("p", playerId);
            write.Parameters.AddWithValue("r", next);
            write.Parameters.Add(new NpgsqlParameter("w", NpgsqlDbType.Jsonb) { Value = worldJson });
            write.Parameters.Add(new NpgsqlParameter("e", NpgsqlDbType.Jsonb) { Value = economyJson });

            if (await write.ExecuteNonQueryAsync(ct) == 0)
                return Conflict(1);
        }

        await ActionLog.WriteAsync(conn, playerId, "save", new { revision = next }, tx, ct);
        await tx.CommitAsync(ct);

        return Results.Ok(new
        {
            revision = next,
            updatedAt = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture)
        });
    }

    private static IResult Conflict(long currentRevision) =>
        ApiProblem.Create(409, ErrorCodes.SaveConflict,
            "Your island was saved from another device. Load the newer save first.",
            new { currentRevision });
}
