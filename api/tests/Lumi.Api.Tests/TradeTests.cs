using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lumi.Api.Features.Trades;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Lumi.Api.Tests;

/// <summary>Đổi thẻ thú khi thăm đảo: 1 loại đổi 1 loại, server giữ thẻ người gửi, kết quả về qua hộp thư.</summary>
[Collection("api")]
public sealed class TradeTests(LumiApiFactory factory)
{
    private const string Fox = "card.creature_fox";
    private const string Deer = "card.creature_deer";

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object? body, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> OfferAsync(HttpClient from, string toPlayerId,
        string offer = Fox, int offerCount = 1, string request = Deer, int requestCount = 1) =>
        PostAsync(from, "/api/v1/trades",
            new { toPlayerId, offerCardId = offer, offerCount, requestCardId = request, requestCount });

    private static async Task<List<JsonElement>> InboxAsync(HttpClient client, string kind) =>
        (await (await client.GetAsync("/api/v1/inbox")).JsonAsync())
        .GetProperty("items").EnumerateArray()
        .Where(i => i.GetProperty("kind").GetString() == kind).ToList();

    private static JsonElement Payload(JsonElement item) =>
        JsonDocument.Parse(item.GetProperty("payload").GetString()!).RootElement.Clone();

    private async Task<string> CreatedIdAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.JsonAsync()).GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task Accepted_offer_gives_each_side_the_other_cards()
    {
        var a = await factory.CreateClient().RegisterAsync();
        var b = await factory.CreateClient().RegisterAsync();

        string id = await CreatedIdAsync(await OfferAsync(factory.As(a), b.PlayerId, Fox, 2, Deer, 1));

        var offered = Assert.Single(await InboxAsync(factory.As(b), "TradeOffered"));
        Assert.Equal(id, Payload(offered).GetProperty("tradeId").GetString());

        var incoming = await (await factory.As(b).GetAsync("/api/v1/trades/incoming")).JsonAsync();
        Assert.Contains(incoming.GetProperty("items").EnumerateArray(), t => t.GetProperty("id").GetString() == id);
        var outgoing = await (await factory.As(a).GetAsync("/api/v1/trades/outgoing")).JsonAsync();
        Assert.Contains(outgoing.GetProperty("items").EnumerateArray(), t => t.GetProperty("id").GetString() == id);

        // Người gửi không tự đồng ý được
        var self = await PostAsync(factory.As(a), $"/api/v1/trades/{id}/accept", null);
        Assert.Equal("NOT_YOUR_TRADE", await self.CodeAsync());

        string key = Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(factory.As(b), $"/api/v1/trades/{id}/accept", null, key)).StatusCode);
        // Gửi lại cùng khóa: không tạo thêm thẻ
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(factory.As(b), $"/api/v1/trades/{id}/accept", null, key)).StatusCode);

        var aGets = Assert.Single(await InboxAsync(factory.As(a), "TradeReceived"));
        Assert.Equal(Deer, Payload(aGets).GetProperty("resourceId").GetString());
        Assert.Equal(1, Payload(aGets).GetProperty("amount").GetInt32());

        var bGets = Assert.Single(await InboxAsync(factory.As(b), "TradeReceived"));
        Assert.Equal(Fox, Payload(bGets).GetProperty("resourceId").GetString());
        Assert.Equal(2, Payload(bGets).GetProperty("amount").GetInt32());

        // Đã đồng ý thì không hủy được nữa
        var late = await PostAsync(factory.As(a), $"/api/v1/trades/{id}/cancel", null);
        Assert.Equal("TRADE_UNAVAILABLE", await late.CodeAsync());
    }

    [Fact]
    public async Task Declined_cancelled_and_expired_offers_return_cards_to_sender()
    {
        var a = await factory.CreateClient().RegisterAsync();
        var b = await factory.CreateClient().RegisterAsync();
        var c = await factory.CreateClient().RegisterAsync();
        var d = await factory.CreateClient().RegisterAsync();

        string declined = await CreatedIdAsync(await OfferAsync(factory.As(a), b.PlayerId));
        string cancelled = await CreatedIdAsync(await OfferAsync(factory.As(a), c.PlayerId));
        string expired = await CreatedIdAsync(await OfferAsync(factory.As(a), d.PlayerId));

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(factory.As(b), $"/api/v1/trades/{declined}/decline", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(factory.As(a), $"/api/v1/trades/{cancelled}/cancel", null)).StatusCode);

        var db = factory.Services.GetRequiredService<NpgsqlDataSource>();
        await using (var conn = await db.OpenConnectionAsync())
        await using (var cmd = new NpgsqlCommand("update card_trades set expires_at = now() - interval '1 minute' where id = @id", conn))
        {
            cmd.Parameters.AddWithValue("id", Guid.Parse(expired));
            await cmd.ExecuteNonQueryAsync();
        }
        await TradeExpiry.RunOnceAsync(db);
        await TradeExpiry.RunOnceAsync(db);

        var returned = await InboxAsync(factory.As(a), "TradeReturned");
        Assert.Equal(3, returned.Count);
        Assert.All(returned, r => Assert.Equal(Fox, Payload(r).GetProperty("resourceId").GetString()));
        Assert.Equal(new[] { "cancelled", "declined", "expired" },
            returned.Select(r => Payload(r).GetProperty("reason").GetString()).OrderBy(s => s).ToArray());
        Assert.Empty(await InboxAsync(factory.As(b), "TradeReceived"));
    }

    [Fact]
    public async Task Offer_rules_are_enforced()
    {
        var a = await factory.CreateClient().RegisterAsync();
        var b = await factory.CreateClient().RegisterAsync();
        var client = factory.As(a);

        Assert.Equal("TRADE_OWN_WORLD", await (await OfferAsync(client, a.PlayerId)).CodeAsync());
        Assert.Equal("UNKNOWN_RESOURCE", await (await OfferAsync(client, b.PlayerId, "card.not_a_creature")).CodeAsync());
        Assert.Equal("UNKNOWN_RESOURCE", await (await OfferAsync(client, b.PlayerId, "wood")).CodeAsync());
        Assert.Equal("INVALID_AMOUNT", await (await OfferAsync(client, b.PlayerId, Fox, 11)).CodeAsync());
        Assert.Equal("INVALID_AMOUNT", await (await OfferAsync(client, b.PlayerId, Fox, 1, Deer, 0)).CodeAsync());

        await CreatedIdAsync(await OfferAsync(client, b.PlayerId));
        // Mỗi cặp người chơi chỉ một đề nghị đang chờ
        Assert.Equal("TRADE_LIMIT_REACHED", await (await OfferAsync(client, b.PlayerId)).CodeAsync());

        // Tối đa 5 đề nghị đang chờ
        for (int i = 0; i < 4; i++)
        {
            var other = await factory.CreateClient().RegisterAsync();
            await CreatedIdAsync(await OfferAsync(client, other.PlayerId));
        }
        var sixth = await factory.CreateClient().RegisterAsync();
        Assert.Equal("TRADE_LIMIT_REACHED", await (await OfferAsync(client, sixth.PlayerId)).CodeAsync());
    }
}
