using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Lumi.Api.Tests;

/// <summary>Admin quản lý người chơi: tìm, khóa / mở khóa (có hiệu lực ngay với token cũ), đổi quyền.</summary>
[Collection("api")]
public sealed class AdminPlayerTests(LumiApiFactory factory)
{
    private async Task<ApiHelpers.Auth> AdminAsync()
    {
        var admin = await factory.CreateClient().RegisterAsync();
        await using var cmd = factory.Services.GetRequiredService<NpgsqlDataSource>()
            .CreateCommand("update players set role = 'admin' where id = @p");
        cmd.Parameters.AddWithValue("p", Guid.Parse(admin.PlayerId));
        await cmd.ExecuteNonQueryAsync();
        return admin;
    }

    private Task<HttpResponseMessage> PostAsync(ApiHelpers.Auth who, string url, object? body = null) =>
        factory.As(who).PostAsJsonAsync(url, body ?? new { });

    private async Task<long> LogCountAsync(string action, string playerId)
    {
        await using var cmd = factory.Services.GetRequiredService<NpgsqlDataSource>().CreateCommand(
            "select count(*) from action_log where action = @a and details ->> 'playerId' = @p");
        cmd.Parameters.AddWithValue("a", action);
        cmd.Parameters.AddWithValue("p", playerId);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Players_cannot_use_admin_player_tools()
    {
        var player = await factory.CreateClient().RegisterAsync();
        var other = await factory.CreateClient().RegisterAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await factory.As(player).GetAsync("/api/v1/admin/players")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(player, $"/api/v1/admin/players/{other.PlayerId}/suspend")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await PostAsync(player, $"/api/v1/admin/players/{player.PlayerId}/role", new { role = "admin" })).StatusCode);
    }

    [Fact]
    public async Task Admin_finds_players_by_name()
    {
        var admin = await AdminAsync();
        var target = await factory.CreateClient().RegisterAsync();

        var found = await (await factory.As(admin).GetAsync(
            "/api/v1/admin/players?search=" + target.Username.ToUpperInvariant())).JsonAsync();
        var item = Assert.Single(found.GetProperty("items").EnumerateArray());
        Assert.Equal(target.PlayerId, item.GetProperty("playerId").GetString());
        Assert.Equal("player", item.GetProperty("role").GetString());
        Assert.False(item.GetProperty("isSuspended").GetBoolean());

        // % không phải ký tự đại diện
        var none = await (await factory.As(admin).GetAsync("/api/v1/admin/players?search=%25%25%25")).JsonAsync();
        Assert.Empty(none.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Suspension_blocks_the_old_token_at_once_and_unsuspend_restores_it()
    {
        var admin = await AdminAsync();
        var target = await factory.CreateClient().RegisterAsync();
        var targetClient = factory.As(target);
        Assert.Equal(HttpStatusCode.OK, (await targetClient.GetAsync("/api/v1/trades/incoming")).StatusCode);

        var suspend = await PostAsync(admin, $"/api/v1/admin/players/{target.PlayerId}/suspend", new { reason = "spam" });
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);
        Assert.True((await suspend.JsonAsync()).GetProperty("isSuspended").GetBoolean());
        Assert.Equal(1, await LogCountAsync("admin.suspend", target.PlayerId));

        // Token cũ còn hạn nhưng bị từ chối ngay
        var blocked = await targetClient.GetAsync("/api/v1/trades/incoming");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("ACCOUNT_SUSPENDED", await blocked.CodeAsync());

        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { username = target.Username, password = "password123" });
        Assert.Equal("ACCOUNT_SUSPENDED", await login.CodeAsync());

        var listed = await (await factory.As(admin).GetAsync("/api/v1/admin/players?suspended=true&pageSize=50")).JsonAsync();
        Assert.Contains(listed.GetProperty("items").EnumerateArray(), i => i.GetProperty("playerId").GetString() == target.PlayerId);

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(admin, $"/api/v1/admin/players/{target.PlayerId}/unsuspend")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await targetClient.GetAsync("/api/v1/trades/incoming")).StatusCode);
    }

    [Fact]
    public async Task Role_changes_follow_the_safety_rules()
    {
        var admin = await AdminAsync();
        var target = await factory.CreateClient().RegisterAsync();
        string url = $"/api/v1/admin/players/{target.PlayerId}";

        // Không tự khóa, không tự đổi quyền: luôn còn ít nhất một admin
        Assert.Equal("ADMIN_SELF", await (await PostAsync(admin, $"/api/v1/admin/players/{admin.PlayerId}/suspend")).CodeAsync());
        Assert.Equal("ADMIN_SELF",
            await (await PostAsync(admin, $"/api/v1/admin/players/{admin.PlayerId}/role", new { role = "player" })).CodeAsync());
        Assert.Equal("BAD_REQUEST", await (await PostAsync(admin, url + "/role", new { role = "owner" })).CodeAsync());

        var promote = await PostAsync(admin, url + "/role", new { role = "admin" });
        Assert.Equal("admin", (await promote.JsonAsync()).GetProperty("role").GetString());
        Assert.Equal(HttpStatusCode.OK, (await factory.As(target).GetAsync("/api/v1/admin/config")).StatusCode);
        Assert.Equal(1, await LogCountAsync("admin.role", target.PlayerId));

        // Admin khác phải bị gỡ quyền trước khi khóa
        Assert.Equal("ADMIN_TARGET_IS_ADMIN", await (await PostAsync(admin, url + "/suspend")).CodeAsync());

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(admin, url + "/role", new { role = "player" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.As(target).GetAsync("/api/v1/admin/config")).StatusCode);

        // Tài khoản đang bị khóa không được cấp quyền admin
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(admin, url + "/suspend")).StatusCode);
        Assert.Equal("ADMIN_TARGET_SUSPENDED", await (await PostAsync(admin, url + "/role", new { role = "admin" })).CodeAsync());

        Assert.Equal("NOT_FOUND", await (await PostAsync(admin, $"/api/v1/admin/players/{Guid.NewGuid()}/unsuspend")).CodeAsync());
    }
}
