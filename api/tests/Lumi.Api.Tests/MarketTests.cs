using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lumi.Api.Features.Market;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Lumi.Api.Tests;

[Collection("api")]
public sealed class MarketTests(LumiApiFactory factory)
{
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object? body, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) };
        if (key != null) request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    private static string Key() => Guid.NewGuid().ToString("N");

    private static async Task<JsonElement> CreateLotAsync(HttpClient seller, string resourceId = "wood", int amount = 10, int price = 50)
    {
        var response = await PostAsync(seller, "/api/v1/market/listings",
            new { resourceId, amount, priceCoins = price }, Key());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.JsonAsync();
    }

    private static async Task<List<JsonElement>> InboxAsync(HttpClient client) =>
        (await (await client.GetAsync("/api/v1/inbox")).JsonAsync())
        .GetProperty("items").EnumerateArray().ToList();

    private static JsonElement Payload(JsonElement item) =>
        JsonDocument.Parse(item.GetProperty("payload").GetString()!).RootElement.Clone();

    private static List<JsonElement> OfKind(List<JsonElement> items, string kind) =>
        items.Where(i => i.GetProperty("kind").GetString() == kind).ToList();

    [Fact]
    public async Task Sale_pays_seller_after_fee_and_gives_buyer_the_lot()
    {
        var seller = await factory.CreateClient().RegisterAsync();
        var buyer = await factory.CreateClient().RegisterAsync();

        var lot = await CreateLotAsync(factory.As(seller), "wood", 10, 50);
        string id = lot.GetProperty("id").GetString()!;
        Assert.Equal(seller.PlayerId, lot.GetProperty("sellerId").GetString());
        Assert.Equal(5.0, lot.GetProperty("pricePerUnit").GetDouble());

        var browse = await (await factory.As(buyer).GetAsync("/api/v1/market/listings?resourceId=wood")).JsonAsync();
        Assert.Contains(browse.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetString() == id);

        var buy = await PostAsync(factory.As(buyer), $"/api/v1/market/listings/{id}/buy", null, Key());
        Assert.Equal(HttpStatusCode.OK, buy.StatusCode);
        Assert.Equal(50, (await buy.JsonAsync()).GetProperty("paidCoins").GetInt32());

        var buyerItems = OfKind(await InboxAsync(factory.As(buyer)), "ItemsBought");
        Assert.Single(buyerItems);
        var goods = Payload(buyerItems[0]);
        Assert.Equal("wood", goods.GetProperty("resourceId").GetString());
        Assert.Equal(10, goods.GetProperty("amount").GetInt32());

        // Phí 5% của 50 Coins là 3, người bán nhận 47
        var sellerItems = OfKind(await InboxAsync(factory.As(seller)), "CoinsFromSale");
        Assert.Single(sellerItems);
        var coins = Payload(sellerItems[0]);
        Assert.Equal(47, coins.GetProperty("coins").GetInt32());
        Assert.Equal(3, coins.GetProperty("fee").GetInt32());

        var mine = await (await factory.As(seller).GetAsync("/api/v1/market/listings/mine")).JsonAsync();
        Assert.DoesNotContain(mine.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetString() == id);
    }

    [Fact]
    public async Task Crafted_goods_can_be_listed_and_bought()
    {
        // Bột mì có giá gốc 15 từ migration 004; trước đó bị từ chối UNKNOWN_RESOURCE
        var seller = await factory.CreateClient().RegisterAsync();
        var buyer = await factory.CreateClient().RegisterAsync();

        var lot = await CreateLotAsync(factory.As(seller), "resource.plain.wheat_flour", 4, 60);
        string id = lot.GetProperty("id").GetString()!;
        Assert.Equal(15.0, lot.GetProperty("pricePerUnit").GetDouble());

        var buy = await PostAsync(factory.As(buyer), $"/api/v1/market/listings/{id}/buy", null, Key());
        Assert.Equal(HttpStatusCode.OK, buy.StatusCode);

        var goods = Payload(Assert.Single(OfKind(await InboxAsync(factory.As(buyer)), "ItemsBought")));
        Assert.Equal("resource.plain.wheat_flour", goods.GetProperty("resourceId").GetString());
        Assert.Equal(4, goods.GetProperty("amount").GetInt32());
    }

    [Fact]
    public async Task Second_buyer_of_the_same_lot_is_refused()
    {
        var seller = await factory.CreateClient().RegisterAsync();
        var first = await factory.CreateClient().RegisterAsync();
        var second = await factory.CreateClient().RegisterAsync();
        string id = (await CreateLotAsync(factory.As(seller))).GetProperty("id").GetString()!;

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(factory.As(first), $"/api/v1/market/listings/{id}/buy", null, Key())).StatusCode);

        var late = await PostAsync(factory.As(second), $"/api/v1/market/listings/{id}/buy", null, Key());
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Equal("LISTING_UNAVAILABLE", await late.CodeAsync());
        Assert.Empty(OfKind(await InboxAsync(factory.As(second)), "ItemsBought"));
    }

    [Fact]
    public async Task Five_buyers_racing_for_one_lot_produce_exactly_one_sale()
    {
        var seller = await factory.CreateClient().RegisterAsync();
        string id = (await CreateLotAsync(factory.As(seller))).GetProperty("id").GetString()!;

        var buyers = new List<HttpClient>();
        for (int i = 0; i < 5; i++)
            buyers.Add(factory.As(await factory.CreateClient().RegisterAsync()));

        var responses = await Task.WhenAll(buyers.Select(b =>
            PostAsync(b, $"/api/v1/market/listings/{id}/buy", null, Key())));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(4, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Same_key_sent_twice_creates_one_listing_and_one_purchase()
    {
        var seller = await factory.CreateClient().RegisterAsync();
        var buyer = await factory.CreateClient().RegisterAsync();
        var sellerClient = factory.As(seller);
        var buyerClient = factory.As(buyer);

        string listKey = Key();
        var body = new { resourceId = "wood", amount = 10, priceCoins = 50 };
        var firstList = await (await PostAsync(sellerClient, "/api/v1/market/listings", body, listKey)).JsonAsync();
        var secondList = await PostAsync(sellerClient, "/api/v1/market/listings", body, listKey);
        Assert.Equal(HttpStatusCode.Created, secondList.StatusCode);
        Assert.Equal(firstList.GetProperty("id").GetString(), (await secondList.JsonAsync()).GetProperty("id").GetString());

        var mine = await (await sellerClient.GetAsync("/api/v1/market/listings/mine")).JsonAsync();
        Assert.Single(mine.GetProperty("items").EnumerateArray());

        string id = firstList.GetProperty("id").GetString()!;
        string buyKey = Key();
        var firstBuy = await PostAsync(buyerClient, $"/api/v1/market/listings/{id}/buy", null, buyKey);
        var secondBuy = await PostAsync(buyerClient, $"/api/v1/market/listings/{id}/buy", null, buyKey);
        Assert.Equal(HttpStatusCode.OK, firstBuy.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondBuy.StatusCode);

        Assert.Single(OfKind(await InboxAsync(buyerClient), "ItemsBought"));
        Assert.Single(OfKind(await InboxAsync(sellerClient), "CoinsFromSale"));
    }

    [Fact]
    public async Task Rejects_bad_listings_with_codes()
    {
        var seller = await factory.CreateClient().RegisterAsync();
        var client = factory.As(seller);

        var unknown = await PostAsync(client, "/api/v1/market/listings", new { resourceId = "dragon", amount = 1, priceCoins = 5 }, Key());
        Assert.Equal("UNKNOWN_RESOURCE", await unknown.CodeAsync());

        var zero = await PostAsync(client, "/api/v1/market/listings", new { resourceId = "wood", amount = 0, priceCoins = 5 }, Key());
        Assert.Equal("INVALID_AMOUNT", await zero.CodeAsync());

        var tooHigh = await PostAsync(client, "/api/v1/market/listings", new { resourceId = "wood", amount = 10, priceCoins = 999 }, Key());
        Assert.Equal(HttpStatusCode.BadRequest, tooHigh.StatusCode);
        Assert.Equal("PRICE_OUT_OF_RANGE", await tooHigh.CodeAsync());
        var range = await tooHigh.JsonAsync();
        Assert.Equal(25, range.GetProperty("minPriceCoins").GetInt32());
        Assert.Equal(150, range.GetProperty("maxPriceCoins").GetInt32());

        var noKey = await PostAsync(client, "/api/v1/market/listings", new { resourceId = "wood", amount = 1, priceCoins = 5 }, null);
        Assert.Equal("IDEMPOTENCY_KEY_MISSING", await noKey.CodeAsync());
    }

    [Fact]
    public async Task Cannot_buy_own_lot()
    {
        var seller = await factory.CreateClient().RegisterAsync();
        var client = factory.As(seller);
        string id = (await CreateLotAsync(client)).GetProperty("id").GetString()!;

        var response = await PostAsync(client, $"/api/v1/market/listings/{id}/buy", null, Key());
        Assert.Equal("OWN_LISTING", await response.CodeAsync());
    }

    [Fact]
    public async Task Seller_can_hold_five_lots_and_no_more()
    {
        var seller = await factory.CreateClient().RegisterAsync();
        var client = factory.As(seller);

        for (int i = 0; i < 5; i++)
            await CreateLotAsync(client, "wood", 1, 5);

        var sixth = await PostAsync(client, "/api/v1/market/listings", new { resourceId = "wood", amount = 1, priceCoins = 5 }, Key());
        Assert.Equal(HttpStatusCode.Conflict, sixth.StatusCode);
        Assert.Equal("LISTING_LIMIT_REACHED", await sixth.CodeAsync());
    }

    [Fact]
    public async Task Only_the_seller_can_cancel_and_the_goods_go_back_to_inbox()
    {
        var seller = await factory.CreateClient().RegisterAsync();
        var other = await factory.CreateClient().RegisterAsync();
        string id = (await CreateLotAsync(factory.As(seller), "wood", 7, 30)).GetProperty("id").GetString()!;

        var stolen = await PostAsync(factory.As(other), $"/api/v1/market/listings/{id}/cancel", null, Key());
        Assert.Equal(HttpStatusCode.Forbidden, stolen.StatusCode);
        Assert.Equal("NOT_YOUR_LISTING", await stolen.CodeAsync());

        var cancelled = await PostAsync(factory.As(seller), $"/api/v1/market/listings/{id}/cancel", null, Key());
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        var returned = OfKind(await InboxAsync(factory.As(seller)), "ItemsReturned");
        Assert.Single(returned);
        var goods = Payload(returned[0]);
        Assert.Equal(7, goods.GetProperty("amount").GetInt32());
        Assert.Equal("cancelled", goods.GetProperty("reason").GetString());

        var again = await PostAsync(factory.As(seller), $"/api/v1/market/listings/{id}/cancel", null, Key());
        Assert.Equal("LISTING_UNAVAILABLE", await again.CodeAsync());
    }

    [Fact]
    public async Task Expired_lot_returns_goods_once_and_cannot_be_bought()
    {
        var seller = await factory.CreateClient().RegisterAsync();
        var buyer = await factory.CreateClient().RegisterAsync();
        string id = (await CreateLotAsync(factory.As(seller), "wood", 4, 20)).GetProperty("id").GetString()!;

        var db = factory.Services.GetRequiredService<NpgsqlDataSource>();
        await using (var conn = await db.OpenConnectionAsync())
        await using (var cmd = new NpgsqlCommand(
            "update listings set expires_at = now() - interval '1 minute' where id = @id", conn))
        {
            cmd.Parameters.AddWithValue("id", Guid.Parse(id));
            await cmd.ExecuteNonQueryAsync();
        }

        var late = await PostAsync(factory.As(buyer), $"/api/v1/market/listings/{id}/buy", null, Key());
        Assert.Equal("LISTING_UNAVAILABLE", await late.CodeAsync());

        // Job có thể đã chạy trước lời gọi này; dù ai chạy thì mỗi lô chỉ trả hàng một lần
        await ListingExpiry.RunOnceAsync(db);
        await ListingExpiry.RunOnceAsync(db);

        var returned = OfKind(await InboxAsync(factory.As(seller)), "ItemsReturned");
        Assert.Single(returned);
        Assert.Equal("expired", Payload(returned[0]).GetProperty("reason").GetString());
    }

    [Fact]
    public void Fee_and_price_range_follow_the_plan()
    {
        // Phí 5% làm tròn lên, ít nhất 1 Coin
        Assert.Equal(1, MarketRules.Fee(1, 5));
        Assert.Equal(3, MarketRules.Fee(50, 5));
        Assert.Equal(5, MarketRules.Fee(100, 5));

        var settings = new MarketSettings(5, 5, 72, 10, 3, 0.5m, 3m, new Dictionary<string, int>());

        // Wood baseValue 5: một đơn vị từ 2 đến 15 Coins (kế hoạch)
        Assert.Equal((2, 15), MarketRules.PriceRange(1, 5, settings));

        // baseValue 0 được tính như 1
        Assert.Equal((2, 3), MarketRules.PriceRange(1, 0, settings));
    }
}
