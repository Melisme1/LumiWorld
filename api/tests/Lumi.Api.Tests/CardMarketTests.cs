using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lumi.Api.Features.Market;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Lumi.Api.Tests;

/// <summary>Thẻ thú bán trên chợ: resourceId "card.&lt;cardID&gt;", giá gốc 50, tối đa 10 lá mỗi lô, 3 lô thẻ mỗi người.</summary>
[Collection("api")]
public sealed class CardMarketTests(LumiApiFactory factory)
{
    private const string Fox = "card.creature_fox";

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object? body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> ListAsync(HttpClient seller, string resourceId, int amount, int price) =>
        PostAsync(seller, "/api/v1/market/listings", new { resourceId, amount, priceCoins = price });

    private static async Task<List<JsonElement>> InboxAsync(HttpClient client, string kind) =>
        (await (await client.GetAsync("/api/v1/inbox")).JsonAsync())
        .GetProperty("items").EnumerateArray()
        .Where(i => i.GetProperty("kind").GetString() == kind).ToList();

    private static JsonElement Payload(JsonElement item) =>
        JsonDocument.Parse(item.GetProperty("payload").GetString()!).RootElement.Clone();

    [Fact]
    public async Task Card_lot_is_sold_like_goods_and_found_by_kind()
    {
        var seller = await factory.CreateClient().RegisterAsync();
        var buyer = await factory.CreateClient().RegisterAsync();

        var created = await ListAsync(factory.As(seller), Fox, 1, 50);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        string id = (await created.JsonAsync()).GetProperty("id").GetString()!;

        var cards = await (await factory.As(buyer).GetAsync("/api/v1/market/listings?kind=card&pageSize=50")).JsonAsync();
        Assert.Contains(cards.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetString() == id);
        var goods = await (await factory.As(buyer).GetAsync("/api/v1/market/listings?kind=resource&pageSize=50")).JsonAsync();
        Assert.DoesNotContain(goods.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetString() == id);

        var buy = await PostAsync(factory.As(buyer), $"/api/v1/market/listings/{id}/buy", null);
        Assert.Equal(HttpStatusCode.OK, buy.StatusCode);

        var bought = Assert.Single(await InboxAsync(factory.As(buyer), "ItemsBought"));
        Assert.Equal(Fox, Payload(bought).GetProperty("resourceId").GetString());
        Assert.Equal(1, Payload(bought).GetProperty("amount").GetInt32());

        // Phí chợ 5% vẫn áp dụng: 50 Coins -> người bán nhận 47
        var paid = Assert.Single(await InboxAsync(factory.As(seller), "CoinsFromSale"));
        Assert.Equal(47, Payload(paid).GetProperty("coins").GetInt32());
    }

    [Fact]
    public async Task Cancelled_and_expired_card_lots_return_the_cards()
    {
        var seller = await factory.CreateClient().RegisterAsync();
        var client = factory.As(seller);

        string cancelId = (await (await ListAsync(client, Fox, 2, 100)).JsonAsync()).GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(client, $"/api/v1/market/listings/{cancelId}/cancel", null)).StatusCode);

        string expireId = (await (await ListAsync(client, Fox, 1, 50)).JsonAsync()).GetProperty("id").GetString()!;
        var db = factory.Services.GetRequiredService<NpgsqlDataSource>();
        await using (var conn = await db.OpenConnectionAsync())
        await using (var cmd = new NpgsqlCommand("update listings set expires_at = now() - interval '1 minute' where id = @id", conn))
        {
            cmd.Parameters.AddWithValue("id", Guid.Parse(expireId));
            await cmd.ExecuteNonQueryAsync();
        }
        await ListingExpiry.RunOnceAsync(db);

        var returned = await InboxAsync(client, "ItemsReturned");
        Assert.Equal(2, returned.Count);
        Assert.All(returned, r => Assert.Equal(Fox, Payload(r).GetProperty("resourceId").GetString()));
        Assert.Equal(3, returned.Sum(r => Payload(r).GetProperty("amount").GetInt32()));
    }

    [Fact]
    public async Task Card_limits_are_enforced()
    {
        var client = factory.As(await factory.CreateClient().RegisterAsync());

        var unknown = await ListAsync(client, "card.not_a_creature", 1, 50);
        Assert.Equal("UNKNOWN_RESOURCE", await unknown.CodeAsync());

        var tooMany = await ListAsync(client, Fox, 11, 550);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal("INVALID_AMOUNT", await tooMany.CodeAsync());

        for (int i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Created, (await ListAsync(client, Fox, 1, 50)).StatusCode);

        var fourth = await ListAsync(client, Fox, 1, 50);
        Assert.Equal(HttpStatusCode.Conflict, fourth.StatusCode);
        Assert.Equal("LISTING_LIMIT_REACHED", await fourth.CodeAsync());

        // Giới hạn lô thẻ không chặn lô tài nguyên
        Assert.Equal(HttpStatusCode.Created, (await ListAsync(client, "wood", 10, 50)).StatusCode);
    }
}
