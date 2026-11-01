using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Lumi.Api.Tests;

[Collection("api")]
public sealed class AdminTests(LumiApiFactory factory)
{
    private static object Snapshot(int score) => new
    {
        version = 1,
        score,
        tiles = Enumerable.Range(0, 19).Select(i => new { q = i, r = 0, model = "Tile_Bloomfield_Lush" }),
        placements = new[] { new { cardID = "Leafwood", q = 0, r = 0, clusterId = 1 } }
    };

    // Đổi dữ liệu thẳng trong database (chỉ dùng cho test: tạo admin, khóa tài khoản)
    private async Task ExecAsync(string sql, string playerId)
    {
        await using var cmd = factory.Services.GetRequiredService<NpgsqlDataSource>().CreateCommand(sql);
        cmd.Parameters.AddWithValue("p", Guid.Parse(playerId));
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<ApiHelpers.Auth> AdminAsync()
    {
        var admin = await factory.CreateClient().RegisterAsync();
        await ExecAsync("update players set role = 'admin' where id = @p", admin.PlayerId);
        return admin;
    }

    private static async Task<HttpResponseMessage> PostWithKeyAsync(HttpClient client, string url, object? body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return await client.SendAsync(request);
    }

    private async Task<JsonElement> SetFeeAsync(ApiHelpers.Auth admin, string value) =>
        await (await factory.As(admin).PutAsJsonAsync("/api/v1/admin/config",
            new { key = "market.feePercent", value })).JsonAsync();

    [Fact]
    public async Task Players_cannot_read_or_change_config()
    {
        var player = await factory.CreateClient().RegisterAsync();
        var client = factory.As(player);

        var read = await client.GetAsync("/api/v1/admin/config");
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal("FORBIDDEN", await read.CodeAsync());

        var write = await client.PutAsJsonAsync("/api/v1/admin/config", new { key = "market.feePercent", value = "0" });
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Fact]
    public async Task Admin_rights_follow_the_database_not_the_token()
    {
        // Token lấy lúc còn là người chơi, nhưng quyền đọc từ bảng players mỗi lần gọi
        var player = await factory.CreateClient().RegisterAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.As(player).GetAsync("/api/v1/admin/config")).StatusCode);

        await ExecAsync("update players set role = 'admin' where id = @p", player.PlayerId);
        Assert.Equal(HttpStatusCode.OK, (await factory.As(player).GetAsync("/api/v1/admin/config")).StatusCode);

        await ExecAsync("update players set role = 'player' where id = @p", player.PlayerId);
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.As(player).GetAsync("/api/v1/admin/config")).StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_config()
    {
        var admin = await AdminAsync();
        var response = await factory.As(admin).GetAsync("/api/v1/admin/config");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var keys = (await response.JsonAsync()).GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("key").GetString()).ToList();
        Assert.Contains("market.feePercent", keys);
        Assert.Contains("market.maxActiveListings", keys);
    }

    [Fact]
    public async Task Fee_change_applies_to_the_next_sale()
    {
        var admin = await AdminAsync();
        var seller = await factory.CreateClient().RegisterAsync();
        var buyer = await factory.CreateClient().RegisterAsync();
        try
        {
            var changed = await SetFeeAsync(admin, "10");
            Assert.Equal("10", changed.GetProperty("value").GetString());
            Assert.Equal("5", changed.GetProperty("previousValue").GetString());

            // Phí 10% của 50 Coins là 5, người bán nhận 45
            var lot = await (await PostWithKeyAsync(factory.As(seller), "/api/v1/market/listings",
                new { resourceId = "wood", amount = 10, priceCoins = 50 })).JsonAsync();
            string id = lot.GetProperty("id").GetString()!;

            var buy = await PostWithKeyAsync(factory.As(buyer), $"/api/v1/market/listings/{id}/buy", null);
            Assert.Equal(HttpStatusCode.OK, buy.StatusCode);

            var sale = (await (await factory.As(seller).GetAsync("/api/v1/inbox")).JsonAsync())
                .GetProperty("items").EnumerateArray()
                .Single(i => i.GetProperty("kind").GetString() == "CoinsFromSale"
                    && i.GetProperty("payload").GetString()!.Contains(id));
            var coins = JsonDocument.Parse(sale.GetProperty("payload").GetString()!).RootElement;
            Assert.Equal(45, coins.GetProperty("coins").GetInt32());
            Assert.Equal(5, coins.GetProperty("fee").GetInt32());
        }
        finally
        {
            // Trả phí về 5% để các test khác không bị ảnh hưởng
            await SetFeeAsync(admin, "5");
        }
    }

    [Fact]
    public async Task Unknown_keys_and_bad_values_are_refused()
    {
        var admin = await AdminAsync();
        var client = factory.As(admin);

        var unknown = await client.PutAsJsonAsync("/api/v1/admin/config", new { key = "market.nope", value = "1" });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal("CONFIG_KEY_UNKNOWN", await unknown.CodeAsync());

        var notNumber = await client.PutAsJsonAsync("/api/v1/admin/config", new { key = "market.feePercent", value = "five" });
        Assert.Equal(HttpStatusCode.BadRequest, notNumber.StatusCode);
        Assert.Equal("CONFIG_VALUE_INVALID", await notNumber.CodeAsync());

        var tooHigh = await client.PutAsJsonAsync("/api/v1/admin/config", new { key = "market.feePercent", value = "60" });
        Assert.Equal("CONFIG_VALUE_INVALID", await tooHigh.CodeAsync());

        var notAllowed = await client.PutAsJsonAsync("/api/v1/admin/config", new { key = "admin.secret", value = "1" });
        Assert.Equal("CONFIG_VALUE_INVALID", await notAllowed.CodeAsync());

        // Tham số số nguyên không nhận số lẻ (server sẽ bỏ qua lặng lẽ nếu cho ghi)
        var fraction = await client.PutAsJsonAsync("/api/v1/admin/config", new { key = "obstacle.dailyLimit", value = "0.5" });
        Assert.Equal("CONFIG_VALUE_INVALID", await fraction.CodeAsync());
        var exponent = await client.PutAsJsonAsync("/api/v1/admin/config", new { key = "obstacle.dailyLimit", value = "1e3" });
        Assert.Equal("CONFIG_VALUE_INVALID", await exponent.CodeAsync());

        // Giá sàn không được lớn hơn giá trần
        var minAboveMax = await client.PutAsJsonAsync("/api/v1/admin/config", new { key = "market.minPriceFactor", value = "999" });
        Assert.Equal("CONFIG_VALUE_INVALID", await minAboveMax.CodeAsync());
    }

    [Fact]
    public async Task Admin_can_tune_card_trades()
    {
        var admin = await AdminAsync();
        var client = factory.As(admin);
        var a = await factory.CreateClient().RegisterAsync();
        var b = await factory.CreateClient().RegisterAsync();

        Task<HttpResponseMessage> Set(string key, string value) =>
            client.PutAsJsonAsync("/api/v1/admin/config", new { key, value });
        Task<HttpResponseMessage> Offer(int count) => PostWithKeyAsync(factory.As(a), "/api/v1/trades", new
        {
            toPlayerId = b.PlayerId,
            offerCardId = "card.creature_fox",
            offerCount = count,
            requestCardId = "card.creature_deer",
            requestCount = 1
        });

        try
        {
            var changed = await (await Set("trade.maxCardsPerSide", "3")).JsonAsync();
            Assert.Equal("10", changed.GetProperty("previousValue").GetString());

            // Giới hạn mới có hiệu lực ngay ở đề nghị kế tiếp
            Assert.Equal("INVALID_AMOUNT", await (await Offer(4)).CodeAsync());
            Assert.Equal(HttpStatusCode.Created, (await Offer(3)).StatusCode);

            Assert.Equal(HttpStatusCode.OK, (await Set("trade.offerHours", "24")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Set("trade.maxPendingOutgoing", "0")).StatusCode);

            Assert.Equal("CONFIG_VALUE_INVALID", await (await Set("trade.maxCardsPerSide", "0")).CodeAsync());
            Assert.Equal("CONFIG_VALUE_INVALID", await (await Set("trade.offerHours", "0")).CodeAsync());
            Assert.Equal("CONFIG_VALUE_INVALID", await (await Set("trade.offerHours", "721")).CodeAsync());
            Assert.Equal("CONFIG_VALUE_INVALID", await (await Set("market.maxCardsPerListing", "0")).CodeAsync());
            Assert.Equal("CONFIG_KEY_UNKNOWN", await (await Set("trade.nope", "1")).CodeAsync());
        }
        finally
        {
            // Trả về mặc định để các test khác không bị ảnh hưởng
            await Set("trade.maxCardsPerSide", "10");
            await Set("trade.offerHours", "72");
            await Set("trade.maxPendingOutgoing", "5");
        }
    }

    [Fact]
    public async Task Stats_count_new_players_and_sales()
    {
        var admin = await AdminAsync();
        var before = await (await factory.As(admin).GetAsync("/api/v1/admin/stats?days=7")).JsonAsync();

        var seller = await factory.CreateClient().RegisterAsync();
        var buyer = await factory.CreateClient().RegisterAsync();
        var lot = await (await PostWithKeyAsync(factory.As(seller), "/api/v1/market/listings",
            new { resourceId = "wood", amount = 10, priceCoins = 50 })).JsonAsync();
        var buy = await PostWithKeyAsync(factory.As(buyer), $"/api/v1/market/listings/{lot.GetProperty("id").GetString()}/buy", null);
        Assert.Equal(HttpStatusCode.OK, buy.StatusCode);

        var after = await (await factory.As(admin).GetAsync("/api/v1/admin/stats?days=7")).JsonAsync();

        Assert.True(after.GetProperty("players").GetProperty("total").GetInt64()
            >= before.GetProperty("players").GetProperty("total").GetInt64() + 2);
        Assert.Equal(
            before.GetProperty("market").GetProperty("sales").GetInt64() + 1,
            after.GetProperty("market").GetProperty("sales").GetInt64());
        Assert.Equal(
            before.GetProperty("market").GetProperty("coinsTraded").GetInt64() + 50,
            after.GetProperty("market").GetProperty("coinsTraded").GetInt64());
        Assert.True(after.TryGetProperty("visits", out _));
        Assert.True(after.TryGetProperty("obstacles", out _));
    }

    [Fact]
    public async Task Leaderboard_sorts_by_score_and_hides_suspended_players()
    {
        var low = await factory.CreateClient().RegisterAsync();
        var high = await factory.CreateClient().RegisterAsync();
        var hidden = await factory.CreateClient().RegisterAsync();
        await factory.As(low).PutAsJsonAsync("/api/v1/worlds/me", Snapshot(30));
        await factory.As(high).PutAsJsonAsync("/api/v1/worlds/me", Snapshot(90));
        await factory.As(hidden).PutAsJsonAsync("/api/v1/worlds/me", Snapshot(999));
        await ExecAsync("update players set is_suspended = true where id = @p", hidden.PlayerId);

        var board = await (await factory.As(low).GetAsync("/api/v1/leaderboard?limit=50")).JsonAsync();
        var ids = board.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("playerId").GetString()).ToList();

        Assert.DoesNotContain(hidden.PlayerId, ids);
        Assert.Contains(high.PlayerId, ids);
        Assert.Contains(low.PlayerId, ids);
        Assert.True(ids.IndexOf(high.PlayerId) < ids.IndexOf(low.PlayerId));
    }

    [Fact]
    public async Task Visits_to_other_islands_are_counted()
    {
        var admin = await AdminAsync();
        var owner = await factory.CreateClient().RegisterAsync();
        await factory.As(owner).PutAsJsonAsync("/api/v1/worlds/me", Snapshot(20));
        var visitor = await factory.CreateClient().RegisterAsync();

        long before = (await (await factory.As(admin).GetAsync("/api/v1/admin/stats?days=7")).JsonAsync())
            .GetProperty("visits").GetInt64();

        Assert.Equal(HttpStatusCode.OK, (await factory.As(visitor).GetAsync($"/api/v1/worlds/{owner.PlayerId}")).StatusCode);
        // Xem đảo của chính mình không tính là lượt thăm
        Assert.Equal(HttpStatusCode.OK, (await factory.As(owner).GetAsync($"/api/v1/worlds/{owner.PlayerId}")).StatusCode);

        long after = (await (await factory.As(admin).GetAsync("/api/v1/admin/stats?days=7")).JsonAsync())
            .GetProperty("visits").GetInt64();
        Assert.Equal(before + 1, after);
    }
}
