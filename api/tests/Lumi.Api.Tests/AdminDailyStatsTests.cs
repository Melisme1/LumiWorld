using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Lumi.Api.Tests;

/// <summary>GET /api/v1/admin/stats/daily: mỗi ngày một dòng (giờ Việt Nam), ngày trống có số 0.</summary>
[Collection("api")]
public sealed class AdminDailyStatsTests(LumiApiFactory factory)
{
    private static object Snapshot(int score) => new
    {
        version = 1,
        score,
        tiles = Enumerable.Range(0, 19).Select(i => new { q = i, r = 0, model = "Tile_Bloomfield_Lush" }),
        placements = new[] { new { cardID = "Leafwood", q = 0, r = 0, clusterId = 1 } }
    };

    private async Task<ApiHelpers.Auth> AdminAsync()
    {
        var admin = await factory.CreateClient().RegisterAsync();
        await using var cmd = factory.Services.GetRequiredService<NpgsqlDataSource>()
            .CreateCommand("update players set role = 'admin' where id = @p");
        cmd.Parameters.AddWithValue("p", Guid.Parse(admin.PlayerId));
        await cmd.ExecuteNonQueryAsync();
        return admin;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object? body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return await client.SendAsync(request);
    }

    private async Task<JsonElement> TodayAsync(ApiHelpers.Auth admin)
    {
        var stats = await (await factory.As(admin).GetAsync("/api/v1/admin/stats/daily")).JsonAsync();
        return stats.GetProperty("days").EnumerateArray().Last();
    }

    private static long At(JsonElement day, string group, string field) => day.GetProperty(group).GetProperty(field).GetInt64();

    [Fact]
    public async Task Default_range_is_seven_days_ending_today_with_zero_days_filled()
    {
        var admin = await AdminAsync();
        var stats = await (await factory.As(admin).GetAsync("/api/v1/admin/stats/daily")).JsonAsync();

        var days = stats.GetProperty("days").EnumerateArray().ToList();
        Assert.Equal(7, days.Count);
        Assert.Equal(stats.GetProperty("to").GetString(), days[^1].GetProperty("day").GetString());
        Assert.Equal("Asia/Ho_Chi_Minh", stats.GetProperty("timeZone").GetString());

        // Một khoảng chắc chắn không có số liệu: đủ dòng, mọi số là 0
        var old = await (await factory.As(admin).GetAsync("/api/v1/admin/stats/daily?from=2001-01-01&to=2001-01-03")).JsonAsync();
        var oldDays = old.GetProperty("days").EnumerateArray().ToList();
        Assert.Equal(new[] { "2001-01-01", "2001-01-02", "2001-01-03" }, oldDays.Select(d => d.GetProperty("day").GetString()).ToArray());
        Assert.All(oldDays, d =>
        {
            Assert.Equal(0, At(d, "players", "new"));
            Assert.Equal(0, At(d, "market", "sold"));
            Assert.Empty(d.GetProperty("market").GetProperty("topCards").EnumerateArray());
        });

        Assert.True(stats.GetProperty("now").GetProperty("economy").TryGetProperty("totalCoins", out _));
        Assert.True(stats.GetProperty("now").GetProperty("world").TryGetProperty("sharedWorlds", out _));
    }

    [Fact]
    public async Task Todays_row_counts_players_market_trades_and_world()
    {
        var admin = await AdminAsync();
        var before = await TodayAsync(admin);

        var seller = await factory.CreateClient().RegisterAsync();
        var buyer = await factory.CreateClient().RegisterAsync();

        // Một lô tài nguyên và một lô thẻ thú được bán
        foreach (var (resourceId, amount, price) in new[] { ("wood", 10, 50), ("card.creature_fox", 1, 60) })
        {
            var lot = await (await PostAsync(factory.As(seller), "/api/v1/market/listings",
                new { resourceId, amount, priceCoins = price })).JsonAsync();
            var buy = await PostAsync(factory.As(buyer), $"/api/v1/market/listings/{lot.GetProperty("id").GetString()}/buy", null);
            Assert.Equal(HttpStatusCode.OK, buy.StatusCode);
        }

        // Một đề nghị đổi thẻ được đồng ý
        var trade = await (await PostAsync(factory.As(seller), "/api/v1/trades", new
        {
            toPlayerId = buyer.PlayerId,
            offerCardId = "card.creature_fox",
            offerCount = 1,
            requestCardId = "card.creature_deer",
            requestCount = 1
        })).JsonAsync();
        Assert.Equal(HttpStatusCode.OK,
            (await PostAsync(factory.As(buyer), $"/api/v1/trades/{trade.GetProperty("id").GetString()}/accept", null)).StatusCode);

        // Share đảo và một lượt thăm
        await factory.As(seller).PutAsJsonAsync("/api/v1/worlds/me", Snapshot(40));
        Assert.Equal(HttpStatusCode.OK, (await factory.As(buyer).GetAsync($"/api/v1/worlds/{seller.PlayerId}")).StatusCode);

        var after = await TodayAsync(admin);

        Assert.True(At(after, "players", "new") >= At(before, "players", "new") + 2);
        Assert.True(At(after, "players", "active") >= At(before, "players", "active") + 2);
        Assert.Equal(At(before, "market", "listed") + 2, At(after, "market", "listed"));
        Assert.Equal(At(before, "market", "sold") + 2, At(after, "market", "sold"));
        Assert.Equal(At(before, "market", "coinsTraded") + 110, At(after, "market", "coinsTraded"));
        // Phí 5%: 50 -> 3, 60 -> 3
        Assert.Equal(At(before, "market", "fees") + 6, At(after, "market", "fees"));
        Assert.Equal(At(before, "trades", "offered") + 1, At(after, "trades", "offered"));
        Assert.Equal(At(before, "trades", "accepted") + 1, At(after, "trades", "accepted"));
        Assert.Equal(At(before, "world", "shares") + 1, At(after, "world", "shares"));
        Assert.Equal(At(before, "world", "visits") + 1, At(after, "world", "visits"));

        var topCards = after.GetProperty("market").GetProperty("topCards").EnumerateArray().ToList();
        Assert.Contains(topCards, c => c.GetProperty("id").GetString() == "card.creature_fox");
        Assert.True(topCards.Count <= 3);
        Assert.DoesNotContain(after.GetProperty("market").GetProperty("topResources").EnumerateArray(),
            r => r.GetProperty("id").GetString()!.StartsWith("card."));
    }

    [Fact]
    public async Task Bad_ranges_and_players_are_refused()
    {
        var admin = await AdminAsync();
        var player = await factory.CreateClient().RegisterAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await factory.As(player).GetAsync("/api/v1/admin/stats/daily")).StatusCode);
        Assert.Equal("BAD_REQUEST",
            await (await factory.As(admin).GetAsync("/api/v1/admin/stats/daily?from=2026-10-10&to=2026-10-01")).CodeAsync());
        Assert.Equal("BAD_REQUEST",
            await (await factory.As(admin).GetAsync("/api/v1/admin/stats/daily?from=2026-01-01&to=2026-12-31")).CodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest,
            (await factory.As(admin).GetAsync("/api/v1/admin/stats/daily?from=not-a-date")).StatusCode);

        // /admin/stats cũ vẫn chạy
        Assert.Equal(HttpStatusCode.OK, (await factory.As(admin).GetAsync("/api/v1/admin/stats?days=7")).StatusCode);
    }
}
