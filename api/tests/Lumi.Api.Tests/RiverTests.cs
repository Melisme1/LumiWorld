using System.Net;
using System.Net.Http.Json;

namespace Lumi.Api.Tests;

[Collection("api")]
public sealed class RiverTests(LumiApiFactory factory)
{
    private static readonly object[] Tiles =
    [
        new { q = 0, r = 0, model = "Tile_Leafwood_Lush" },
        new { q = 1, r = 0, model = "Tile_Leafwood_Lush" },
        new { q = 2, r = 0, model = "Tile_Leafwood_Lush" },
    ];

    private static object Save(long baseRevision, object[] placements) => new
    {
        baseRevision,
        world = new { version = 1, tiles = Tiles, placements },
        economy = new { version = 1, coins = 0 }
    };

    [Fact]
    public async Task Server_reads_rivers_from_the_save()
    {
        var player = await factory.CreateClient().RegisterAsync();
        var client = factory.As(player);

        var put = await client.PutAsJsonAsync("/api/v1/saves/me", Save(0,
        [
            new { cardID = "Leafwood", q = 0, r = 0 },
            new { cardID = "River", q = 0, r = 0, riverFlowStep = 0, riverIsOrigin = true },
            new { cardID = "River", q = 1, r = 0, riverFlowStep = 1, riverIsOrigin = false },
        ]));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var rivers = await (await client.GetAsync("/api/v1/rivers/me")).JsonAsync();
        Assert.Equal(2, rivers.GetProperty("riverTiles").GetInt32());
        Assert.Equal(1, rivers.GetProperty("springs").GetInt32());
        Assert.Equal(2, rivers.GetProperty("longestFlow").GetInt32());
        Assert.True(rivers.GetProperty("tiles")[0].GetProperty("isOrigin").GetBoolean());
    }

    [Fact]
    public async Task Old_save_without_flow_data_is_accepted()
    {
        var client = factory.As(await factory.CreateClient().RegisterAsync());

        var put = await client.PutAsJsonAsync("/api/v1/saves/me", Save(0, [new { cardID = "River", q = 2, r = 0 }]));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var rivers = await (await client.GetAsync("/api/v1/rivers/me")).JsonAsync();
        Assert.Equal(1, rivers.GetProperty("riverTiles").GetInt32());
        Assert.Equal(0, rivers.GetProperty("longestFlow").GetInt32());
    }

    [Fact]
    public async Task River_off_the_island_or_with_bad_flow_is_rejected()
    {
        var client = factory.As(await factory.CreateClient().RegisterAsync());

        var offIsland = await client.PutAsJsonAsync("/api/v1/saves/me", Save(0, [new { cardID = "River", q = 9, r = 9 }]));
        Assert.Equal(HttpStatusCode.BadRequest, offIsland.StatusCode);
        Assert.Equal("SAVE_INVALID", await offIsland.CodeAsync());

        var badFlow = await client.PutAsJsonAsync("/api/v1/saves/me",
            Save(0, [new { cardID = "River", q = 0, r = 0, riverFlowStep = 99 }]));
        Assert.Equal(HttpStatusCode.BadRequest, badFlow.StatusCode);

        var snapshot = await client.PutAsJsonAsync("/api/v1/worlds/me", new
        {
            version = 1,
            score = 0,
            tiles = Tiles,
            placements = new[] { new { cardID = "River", q = 5, r = 5 } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, snapshot.StatusCode);
        Assert.Equal("SNAPSHOT_INVALID", await snapshot.CodeAsync());
    }
}
