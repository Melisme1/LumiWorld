using System.Globalization;
using System.Security.Claims;
using Lumi.Api.Features.Config;
using Lumi.Api.Infrastructure;
using Npgsql;

namespace Lumi.Api.Features.Admin;

public sealed record ConfigUpdateRequest(string? Key, string? Value);

/// <summary>
/// Công cụ admin (giai đoạn 5): xem và đổi game_config mà không cần build lại client,
/// xem số liệu cho dashboard, và bảng xếp hạng theo điểm world.
/// Quyền admin đọc từ bảng players mỗi lần gọi, nên hạ quyền có hiệu lực ngay (không đợi token hết hạn).
/// Tạo admin: update players set role = 'admin' where username = '...'.
/// </summary>
public static class AdminEndpoints
{
    // Tham số có giá trị số: server phải đọc được thành số, nếu không GameConfig sẽ lặng lẽ dùng mặc định
    private static readonly string[] NumericPrefixes = ["market.", "obstacle.", "sync.", "save.", "world.", "trade."];

    // Phải từ 1 trở lên; trade.maxPendingOutgoing và market.maxActiveCardListings = 0 thì được (tắt tính năng)
    private static readonly HashSet<string> MinOneKeys =
        ["market.listingHours", "market.maxCardsPerListing", "trade.offerHours", "trade.maxCardsPerSide"];
    private const int MaxTradeOfferHours = 720;
    private const string AllowedKindsKey = "obstacle.allowedKinds";
    private const string MinPriceFactorKey = "market.minPriceFactor";
    private const string MaxPriceFactorKey = "market.maxPriceFactor";

    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin").RequireAuthorization();

        group.MapGet("/config", ListConfig);
        group.MapPut("/config", UpdateConfig);
        group.MapGet("/stats", Stats);
    }

    public static void MapLeaderboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/leaderboard", Leaderboard).RequireAuthorization();
    }

    internal static async Task<IResult?> RequireAdminAsync(ClaimsPrincipal user, NpgsqlDataSource db, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "select role from players where id = @p and not is_suspended", conn);
        cmd.Parameters.AddWithValue("p", user.PlayerId());

        string? role = (string?)await cmd.ExecuteScalarAsync(ct);
        return role == "admin"
            ? null
            : ApiProblem.Create(403, ErrorCodes.Forbidden, "Only admins can do this.");
    }

    private static async Task<IResult> ListConfig(ClaimsPrincipal user, NpgsqlDataSource db, CancellationToken ct)
    {
        if (await RequireAdminAsync(user, db, ct) is { } denied) return denied;

        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "select key, value, updated_at from game_config order by key", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var items = new List<object>();
        while (await reader.ReadAsync(ct))
        {
            items.Add(new
            {
                key = reader.GetString(0),
                value = reader.GetString(1),
                updatedAt = reader.GetFieldValue<DateTimeOffset>(2)
            });
        }

        return Results.Ok(new { items });
    }

    private static async Task<IResult> UpdateConfig(
        ClaimsPrincipal user, ConfigUpdateRequest? body, NpgsqlDataSource db, GameConfigService config, CancellationToken ct)
    {
        if (await RequireAdminAsync(user, db, ct) is { } denied) return denied;

        string key = (body?.Key ?? string.Empty).Trim();
        string value = (body?.Value ?? string.Empty).Trim();
        if (key.Length == 0 || key.Length > 100 || value.Length == 0 || value.Length > 200)
            return ApiProblem.Create(400, ErrorCodes.BadRequest, "Key and value are required (value at most 200 characters).");

        if (!IsValid(key, value, out string? problem))
            return ApiProblem.Create(400, ErrorCodes.ConfigValueInvalid, problem!);

        // Giá sàn lớn hơn giá trần thì mọi lô đều bị từ chối
        if (key is MinPriceFactorKey or MaxPriceFactorKey)
        {
            var settings = await config.GetAsync(ct);
            double number = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
            double min = key == MinPriceFactorKey ? number : settings.MarketMinPriceFactor;
            double max = key == MaxPriceFactorKey ? number : settings.MarketMaxPriceFactor;
            if (min > max)
                return ApiProblem.Create(400, ErrorCodes.ConfigValueInvalid, "market.minPriceFactor cannot be more than market.maxPriceFactor.");
        }

        Guid adminId = user.PlayerId();
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        string? oldValue;
        await using (var read = new NpgsqlCommand("select value from game_config where key = @k for update", conn, tx))
        {
            read.Parameters.AddWithValue("k", key);
            oldValue = (string?)await read.ExecuteScalarAsync(ct);
        }

        if (oldValue == null)
            return ApiProblem.Create(400, ErrorCodes.ConfigKeyUnknown, $"Unknown config key '{key}'. Only existing keys can be changed.");

        await using (var write = new NpgsqlCommand(
            "update game_config set value = @v, updated_at = now(), updated_by = @a where key = @k", conn, tx))
        {
            write.Parameters.AddWithValue("v", value);
            write.Parameters.AddWithValue("a", adminId);
            write.Parameters.AddWithValue("k", key);
            await write.ExecuteNonQueryAsync(ct);
        }

        await ActionLog.WriteAsync(conn, adminId, "admin.config.update",
            new { key, oldValue, newValue = value }, tx, ct);
        await tx.CommitAsync(ct);

        // Lần bán kế tiếp dùng giá trị mới ngay, không đợi cache 30 giây
        config.Invalidate();

        return Results.Ok(new { key, value, previousValue = oldValue });
    }

    /// <summary>Kiểm tra giá trị trước khi ghi: tham số số phải là số không âm, phí không quá 50%.</summary>
    private static bool IsValid(string key, string value, out string? problem)
    {
        problem = null;

        if (key == AllowedKindsKey)
            return true;

        if (!NumericPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal)))
        {
            problem = $"Config key '{key}' is not a value admins can change here.";
            return false;
        }

        // Chỉ hai hệ số giá là số thập phân; còn lại GameConfigService đọc bằng int.TryParse,
        // nên "0.5" hay "1e3" sẽ bị bỏ qua lặng lẽ nếu cho ghi
        decimal number;
        if (key is MinPriceFactorKey or MaxPriceFactorKey)
        {
            if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number) || number <= 0)
            {
                problem = $"'{key}' needs a number greater than zero, like 0.5.";
                return false;
            }
        }
        else if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int whole))
        {
            number = whole;
        }
        else
        {
            problem = $"'{key}' needs a whole number that is zero or more.";
            return false;
        }

        if (key == "market.feePercent" && number > 50)
        {
            problem = "market.feePercent cannot be more than 50.";
            return false;
        }

        // 0 ở các khóa này làm mọi lô / đề nghị hết hạn ngay hoặc không ai gửi được lá nào
        if (MinOneKeys.Contains(key) && number < 1)
        {
            problem = $"'{key}' must be at least 1.";
            return false;
        }

        if (key == "trade.offerHours" && number > MaxTradeOfferHours)
        {
            problem = $"trade.offerHours cannot be more than {MaxTradeOfferHours} (30 days).";
            return false;
        }

        return true;
    }

    private static async Task<IResult> Stats(int? days, ClaimsPrincipal user, NpgsqlDataSource db, CancellationToken ct)
    {
        if (await RequireAdminAsync(user, db, ct) is { } denied) return denied;

        int window = Math.Clamp(days ?? 7, 1, 90);
        await using var conn = await db.OpenConnectionAsync(ct);

        long totalPlayers, activePlayers, newPlayers, worlds;
        await using (var cmd = new NpgsqlCommand(
            "select count(*), " +
            "count(*) filter (where last_login_at >= now() - make_interval(days => @d)), " +
            "count(*) filter (where created_at >= now() - make_interval(days => @d)) " +
            "from players", conn))
        {
            cmd.Parameters.AddWithValue("d", window);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            totalPlayers = reader.GetInt64(0);
            activePlayers = reader.GetInt64(1);
            newPlayers = reader.GetInt64(2);
        }

        await using (var cmd = new NpgsqlCommand("select count(*) from world_snapshots", conn))
            worlds = (long)(await cmd.ExecuteScalarAsync(ct))!;

        long sales, coinsTraded, activeListings;
        await using (var cmd = new NpgsqlCommand(
            "select count(*) filter (where status = 'Sold' and sold_at >= now() - make_interval(days => @d)), " +
            "coalesce(sum(price_coins) filter (where status = 'Sold' and sold_at >= now() - make_interval(days => @d)), 0), " +
            "count(*) filter (where status = 'Active' and expires_at > now()) " +
            "from listings", conn))
        {
            cmd.Parameters.AddWithValue("d", window);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            sales = reader.GetInt64(0);
            coinsTraded = Convert.ToInt64(reader.GetValue(1));
            activeListings = reader.GetInt64(2);
        }

        // Số lần mỗi hành động xảy ra trong khoảng ngày; hành động chưa có (thăm, obstacle) vẫn hiện số 0
        var counts = new Dictionary<string, long>();
        await using (var cmd = new NpgsqlCommand(
            "select action, count(*) from action_log where at >= now() - make_interval(days => @d) group by action", conn))
        {
            cmd.Parameters.AddWithValue("d", window);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                counts[reader.GetString(0)] = reader.GetInt64(1);
        }

        long Count(string action) => counts.TryGetValue(action, out long n) ? n : 0;

        return Results.Ok(new
        {
            days = window,
            players = new { total = totalPlayers, active = activePlayers, @new = newPlayers },
            worldsPublished = worlds,
            market = new { sales, coinsTraded, activeListings, listed = Count("market.list"), cancelled = Count("market.cancel") },
            visits = Count("world.visit"),
            obstacles = Count("obstacle.sent"),
            actions = counts.Select(pair => new { action = pair.Key, count = pair.Value }).OrderBy(a => a.action).ToList()
        });
    }

    /// <summary>Bảng xếp hạng world theo điểm. Người bị khóa không hiện.</summary>
    private static async Task<IResult> Leaderboard(int? limit, NpgsqlDataSource db, CancellationToken ct)
    {
        int size = Math.Clamp(limit ?? 10, 1, 50);

        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "select w.player_id, p.display_name, w.score from world_snapshots w " +
            "join players p on p.id = w.player_id where not p.is_suspended " +
            "order by w.score desc, w.updated_at limit @n", conn);
        cmd.Parameters.AddWithValue("n", size);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var items = new List<object>();
        int rank = 0;
        while (await reader.ReadAsync(ct))
        {
            rank++;
            items.Add(new { rank, playerId = reader.GetGuid(0).ToString(), displayName = reader.GetString(1), score = reader.GetInt32(2) });
        }

        return Results.Ok(new { items });
    }
}
