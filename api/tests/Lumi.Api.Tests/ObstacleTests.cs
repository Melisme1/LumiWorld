using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Lumi.Api.Tests;

[Collection("api")]
public sealed class ObstacleTests(LumiApiFactory factory)
{
    private static object Snapshot() => new
    {
        version = 1,
        score = 10,
        tiles = Enumerable.Range(0, 19).Select(i => new { q = i, r = 0, model = "Tile_Bloomfield_Lush" }),
        placements = new[] { new { cardID = "Leafwood", q = 0, r = 0, clusterId = 1 } }
    };

    // Đẩy ngày tạo tài khoản về quá khứ để qua thời gian bảo vệ người mới (chỉ dùng cho test)
    private async Task AgeAsync(string playerId)
    {
        await using var cmd = factory.Services.GetRequiredService<NpgsqlDataSource>()
            .CreateCommand("update players set created_at = now() - interval '10 days' where id = @p");
        cmd.Parameters.AddWithValue("p", Guid.Parse(playerId));
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string targetId, object body, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/worlds/{targetId}/obstacles")
        {
            Content = JsonContent.Create(body)
        };
        if (key != null) request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    private static string Key() => Guid.NewGuid().ToString("N");

    /// <summary>Tạo người nhận đã qua thời gian bảo vệ và đăng đảo lên.</summary>
    private async Task<ApiHelpers.Auth> OwnerAsync()
    {
        var owner = await factory.CreateClient().RegisterAsync();
        await AgeAsync(owner.PlayerId);
        var publish = await factory.As(owner).PutAsJsonAsync("/api/v1/worlds/me", Snapshot());
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        return owner;
    }

    private static async Task<List<JsonElement>> ObstacleItemsAsync(HttpClient client) =>
        (await (await client.GetAsync("/api/v1/inbox")).JsonAsync())
        .GetProperty("items").EnumerateArray()
        .Where(i => i.GetProperty("kind").GetString() == "Obstacle")
        .ToList();

    [Fact]
    public async Task Obstacle_lands_in_owner_inbox_once_even_when_resent()
    {
        var owner = await OwnerAsync();
        var sender = await factory.CreateClient().RegisterAsync();
        string key = Key();

        var first = await SendAsync(factory.As(sender), owner.PlayerId, new { kind = "Cyclone", q = 3, r = 0 }, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        string obstacleId = (await first.JsonAsync()).GetProperty("obstacleId").GetString()!;

        // Mất mạng rồi gửi lại cùng khóa: trả đúng kết quả cũ, không thêm mục thứ hai
        var again = await SendAsync(factory.As(sender), owner.PlayerId, new { kind = "Cyclone", q = 3, r = 0 }, key);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        Assert.Equal(obstacleId, (await again.JsonAsync()).GetProperty("obstacleId").GetString());

        var items = await ObstacleItemsAsync(factory.As(owner));
        Assert.Single(items);
        var payload = JsonDocument.Parse(items[0].GetProperty("payload").GetString()!).RootElement;
        Assert.Equal(obstacleId, payload.GetProperty("obstacleId").GetString());
        Assert.Equal("Cyclone", payload.GetProperty("kind").GetString());
        Assert.Equal(sender.PlayerId, payload.GetProperty("senderId").GetString());
    }

    [Fact]
    public async Task Bad_requests_are_refused_with_codes()
    {
        var owner = await OwnerAsync();
        var sender = await factory.CreateClient().RegisterAsync();
        var client = factory.As(sender);

        var noKey = await SendAsync(client, owner.PlayerId, new { kind = "ForestFire", q = 1, r = 0 }, null);
        Assert.Equal("IDEMPOTENCY_KEY_MISSING", await noKey.CodeAsync());

        var kind = await SendAsync(client, owner.PlayerId, new { kind = "Meteor", q = 1, r = 0 }, Key());
        Assert.Equal(HttpStatusCode.BadRequest, kind.StatusCode);
        Assert.Equal("OBSTACLE_KIND_NOT_ALLOWED", await kind.CodeAsync());

        // Từ migration 010 chỉ còn ForestFire và Cyclone
        foreach (string removed in new[] { "Obstacle", "Drought" })
        {
            var old = await SendAsync(client, owner.PlayerId, new { kind = removed, q = 1, r = 0 }, Key());
            Assert.Equal("OBSTACLE_KIND_NOT_ALLOWED", await old.CodeAsync());
        }

        var tile = await SendAsync(client, owner.PlayerId, new { kind = "ForestFire", q = 99, r = 99 }, Key());
        Assert.Equal("OBSTACLE_TILE_NOT_FOUND", await tile.CodeAsync());

        var own = await SendAsync(factory.As(owner), owner.PlayerId, new { kind = "ForestFire", q = 1, r = 0 }, Key());
        Assert.Equal("OBSTACLE_OWN_WORLD", await own.CodeAsync());

        var missing = await SendAsync(client, Guid.NewGuid().ToString(), new { kind = "ForestFire", q = 1, r = 0 }, Key());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("WORLD_NOT_FOUND", await missing.CodeAsync());
    }

    [Fact]
    public async Task New_players_worlds_are_protected()
    {
        // Tài khoản vừa tạo, chưa qua thời gian bảo vệ
        var fresh = await factory.CreateClient().RegisterAsync();
        var published = await factory.As(fresh).PutAsJsonAsync("/api/v1/worlds/me", Snapshot());
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        var sender = await factory.CreateClient().RegisterAsync();
        var refused = await SendAsync(factory.As(sender), fresh.PlayerId, new { kind = "ForestFire", q = 1, r = 0 }, Key());
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("OBSTACLE_PROTECTED", await refused.CodeAsync());
    }

    [Fact]
    public async Task Daily_and_pending_limits_are_enforced()
    {
        var targetA = await OwnerAsync();
        var targetB = await OwnerAsync();
        var sender = await factory.CreateClient().RegisterAsync();
        var client = factory.As(sender);

        // Hai obstacle tới A: đủ giới hạn chờ (2)
        Assert.Equal(HttpStatusCode.Created,
            (await SendAsync(client, targetA.PlayerId, new { kind = "ForestFire", q = 1, r = 0 }, Key())).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await SendAsync(client, targetA.PlayerId, new { kind = "Cyclone", q = 2, r = 0 }, Key())).StatusCode);

        // Obstacle thứ ba tới A: A còn hai obstacle chưa nhận
        var pending = await SendAsync(client, targetA.PlayerId, new { kind = "ForestFire", q = 3, r = 0 }, Key());
        Assert.Equal("OBSTACLE_PENDING_LIMIT", await pending.CodeAsync());

        // Người gửi đã dùng hai lượt trong ngày, còn một: gửi tới B được, lượt sau thì hết
        Assert.Equal(HttpStatusCode.Created,
            (await SendAsync(client, targetB.PlayerId, new { kind = "ForestFire", q = 4, r = 0 }, Key())).StatusCode);

        var daily = await SendAsync(client, targetB.PlayerId, new { kind = "ForestFire", q = 5, r = 0 }, Key());
        Assert.Equal("OBSTACLE_DAILY_LIMIT", await daily.CodeAsync());
    }
}
