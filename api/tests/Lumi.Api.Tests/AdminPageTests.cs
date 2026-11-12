using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Lumi.Api.Tests;

/// <summary>Trang /admin là file tĩnh trong wwwroot/admin, có header chặn nhúng và chỉ cho script cùng nguồn.</summary>
[Collection("api")]
public sealed class AdminPageTests(LumiApiFactory factory)
{
    [Fact]
    public async Task Admin_page_and_assets_are_served_with_safe_headers()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var bare = await client.GetAsync("/admin");
        Assert.Equal(HttpStatusCode.Redirect, bare.StatusCode);
        Assert.Equal("/admin/", bare.Headers.Location?.OriginalString);

        var page = await client.GetAsync("/admin/");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("text/html", page.Content.Headers.ContentType?.MediaType);
        Assert.Contains("LumiWorld Admin", await page.Content.ReadAsStringAsync());
        Assert.Contains("frame-ancestors 'none'", page.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("DENY", page.Headers.GetValues("X-Frame-Options").Single());

        foreach (string asset in new[] { "/admin/admin.js", "/admin/admin.css" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(asset)).StatusCode);
    }

    [Fact]
    public async Task Page_has_no_data_and_the_admin_api_still_needs_an_admin_token()
    {
        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/stats/daily")).StatusCode);

        var player = await factory.CreateClient().RegisterAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.As(player).GetAsync("/api/v1/admin/players")).StatusCode);
    }
}
