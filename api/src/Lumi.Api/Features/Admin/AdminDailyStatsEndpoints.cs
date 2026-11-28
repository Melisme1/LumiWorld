using System.Security.Claims;
using Lumi.Api.Infrastructure;
using Npgsql;

namespace Lumi.Api.Features.Admin;

/// <summary>
/// Số liệu theo ngày cho dashboard: GET /api/v1/admin/stats/daily?from=yyyy-MM-dd&amp;to=yyyy-MM-dd.
/// Đọc các view report_* (migration 009_report_views). Ngày tính theo giờ Việt Nam; ngày không có số liệu vẫn có dòng với số 0.
/// Mặc định 7 ngày gần nhất, tối đa 92 ngày một lần. /admin/stats (tổng N ngày) giữ nguyên.
/// </summary>
public static class AdminDailyStatsEndpoints
{
    public const string TimeZone = "Asia/Ho_Chi_Minh";
    private const int DefaultDays = 7;
    private const int MaxDays = 92;
    private const int TopItems = 3;

    public static void MapAdminDailyStatsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/stats/daily", Daily).RequireAuthorization();
    }

    private sealed class Day
    {
        public long NewPlayers, ActivePlayers;
        public long Listed, Sold, CoinsTraded, Fees, ListingsCancelled, ListingsExpired;
        public readonly List<object> TopResources = new();
        public readonly List<object> TopCards = new();
        public long Offered, Accepted, Declined, TradesCancelled, TradesExpired;
        public long Shares, SharingPlayers, Visits, Obstacles;
    }

    private static async Task<IResult> Daily(
        DateOnly? from, DateOnly? to, ClaimsPrincipal user, NpgsqlDataSource db, CancellationToken ct)
    {
        if (await AdminEndpoints.RequireAdminAsync(user, db, ct) is { } denied) return denied;

        await using var conn = await db.OpenConnectionAsync(ct);

        DateOnly today;
        await using (var cmd = new NpgsqlCommand($"select (now() at time zone '{TimeZone}')::date", conn))
            today = DateOnly.FromDateTime((DateTime)(await cmd.ExecuteScalarAsync(ct))!);

        DateOnly last = to ?? today;
        DateOnly first = from ?? last.AddDays(-(DefaultDays - 1));
        if (first > last)
            return ApiProblem.Create(400, ErrorCodes.BadRequest, "from must be on or before to.");
        if (last.DayNumber - first.DayNumber + 1 > MaxDays)
            return ApiProblem.Create(400, ErrorCodes.BadRequest, $"At most {MaxDays} days at a time.");

        var days = new SortedDictionary<DateOnly, Day>();
        for (DateOnly d = first; d <= last; d = d.AddDays(1))
            days[d] = new Day();

        // sql đọc một view; ownFilter = true khi câu đã tự lọc theo @f, @t
        async Task ReadAsync(string sql, Action<NpgsqlDataReader, Day> fill, bool ownFilter = false)
        {
            await using var cmd = new NpgsqlCommand(ownFilter ? sql : sql + " where day between @f and @t", conn);
            cmd.Parameters.AddWithValue("f", first);
            cmd.Parameters.AddWithValue("t", last);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                if (days.TryGetValue(reader.GetFieldValue<DateOnly>(0), out Day? day))
                    fill(reader, day);
        }

        await ReadAsync("select day, new_players, active_players from report_players_daily", (r, d) =>
        {
            d.NewPlayers = r.GetInt64(1);
            d.ActivePlayers = r.GetInt64(2);
        });

        await ReadAsync("select day, listed, sold, coins_traded, fees, cancelled, expired from report_market_daily", (r, d) =>
        {
            d.Listed = r.GetInt64(1);
            d.Sold = r.GetInt64(2);
            d.CoinsTraded = Convert.ToInt64(r.GetValue(3));
            d.Fees = Convert.ToInt64(r.GetValue(4));
            d.ListingsCancelled = r.GetInt64(5);
            d.ListingsExpired = r.GetInt64(6);
        });

        // Top món bán chạy mỗi ngày theo Coins, tách tài nguyên và thẻ thú
        await ReadAsync(
            "select day, resource_id, is_card, sold_lots, sold_amount, coins from (" +
            "select *, row_number() over (partition by day, is_card order by coins desc, sold_amount desc, resource_id) as rank " +
            "from report_market_items_daily where day between @f and @t) ranked" +
            $" where rank <= {TopItems} order by day, is_card, rank",
            (r, d) =>
            {
                var item = new
                {
                    id = r.GetString(1),
                    soldLots = r.GetInt64(3),
                    soldAmount = Convert.ToInt64(r.GetValue(4)),
                    coins = Convert.ToInt64(r.GetValue(5))
                };
                (r.GetBoolean(2) ? d.TopCards : d.TopResources).Add(item);
            },
            ownFilter: true);

        await ReadAsync("select day, offered, accepted, declined, cancelled, expired from report_trades_daily", (r, d) =>
        {
            d.Offered = r.GetInt64(1);
            d.Accepted = r.GetInt64(2);
            d.Declined = r.GetInt64(3);
            d.TradesCancelled = r.GetInt64(4);
            d.TradesExpired = r.GetInt64(5);
        });

        await ReadAsync("select day, shares, sharing_players, visits, obstacles from report_world_daily", (r, d) =>
        {
            d.Shares = r.GetInt64(1);
            d.SharingPlayers = r.GetInt64(2);
            d.Visits = r.GetInt64(3);
            d.Obstacles = r.GetInt64(4);
        });

        object worldNow, economyNow;
        await using (var cmd = new NpgsqlCommand("select shared_worlds, avg_score, max_score from report_world_now", conn))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            await r.ReadAsync(ct);
            worldNow = new { sharedWorlds = r.GetInt64(0), avgScore = Convert.ToDouble(r.GetValue(1)), maxScore = Convert.ToInt64(r.GetValue(2)) };
        }
        await using (var cmd = new NpgsqlCommand("select saves, total_coins, avg_coins, max_coins from report_economy_now", conn))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            await r.ReadAsync(ct);
            economyNow = new
            {
                saves = r.GetInt64(0),
                totalCoins = Convert.ToInt64(r.GetValue(1)),
                avgCoins = Convert.ToDouble(r.GetValue(2)),
                maxCoins = Convert.ToInt64(r.GetValue(3))
            };
        }

        return Results.Ok(new
        {
            from = first.ToString("yyyy-MM-dd"),
            to = last.ToString("yyyy-MM-dd"),
            timeZone = TimeZone,
            days = days.Select(pair => new
            {
                day = pair.Key.ToString("yyyy-MM-dd"),
                players = new { @new = pair.Value.NewPlayers, active = pair.Value.ActivePlayers },
                market = new
                {
                    listed = pair.Value.Listed,
                    sold = pair.Value.Sold,
                    coinsTraded = pair.Value.CoinsTraded,
                    fees = pair.Value.Fees,
                    cancelled = pair.Value.ListingsCancelled,
                    expired = pair.Value.ListingsExpired,
                    topResources = pair.Value.TopResources,
                    topCards = pair.Value.TopCards
                },
                trades = new
                {
                    offered = pair.Value.Offered,
                    accepted = pair.Value.Accepted,
                    declined = pair.Value.Declined,
                    cancelled = pair.Value.TradesCancelled,
                    expired = pair.Value.TradesExpired
                },
                world = new
                {
                    shares = pair.Value.Shares,
                    sharingPlayers = pair.Value.SharingPlayers,
                    visits = pair.Value.Visits,
                    obstacles = pair.Value.Obstacles
                }
            }).ToList(),
            now = new { world = worldNow, economy = economyNow }
        });
    }
}
