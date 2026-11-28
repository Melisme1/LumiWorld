using System.Globalization;
using Npgsql;

namespace Lumi.Api.Features.Config;

/// <summary>
/// Tham số game client cần biết. Tên trường camelCase khớp JsonUtility phía client.
/// Giá trị mặc định dùng khi bảng game_config thiếu key.
/// </summary>
public sealed record GameConfig
{
    public int MarketFeePercent { get; init; } = 5;
    public int MarketMaxActiveListings { get; init; } = 5;
    public int MarketListingHours { get; init; } = 72;
    public double MarketMinPriceFactor { get; init; } = 0.5;
    public double MarketMaxPriceFactor { get; init; } = 3;
    public int MarketMaxCardsPerListing { get; init; } = 10;
    public int MarketMaxActiveCardListings { get; init; } = 3;
    public int TradeOfferHours { get; init; } = 72;
    public int TradeMaxPendingOutgoing { get; init; } = 5;
    public int TradeMaxCardsPerSide { get; init; } = 10;
    public string[] ObstacleAllowedKinds { get; init; } = ["ForestFire", "Cyclone"];
    public int ObstacleFeeCoins { get; init; } = 20;
    public int ObstacleDailyLimit { get; init; } = 3;
    public int ObstacleMaxPendingPerWorld { get; init; } = 2;
    public int ObstacleNewPlayerProtectionHours { get; init; } = 72;
    public int SyncInboxSeconds { get; init; } = 60;
    public int SaveUploadSeconds { get; init; } = 300;
    public int WorldMaxTiles { get; init; } = 500;
}

/// <summary>
/// Đọc game_config. Giữ trong bộ nhớ 30 giây để mỗi request không phải đọc bảng;
/// admin đổi tham số thì sau tối đa 30 giây là có hiệu lực.
/// </summary>
public sealed class GameConfigService(NpgsqlDataSource db)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private GameConfig? _cached;
    private DateTimeOffset _loadedAt;

    /// <summary>Bỏ cache để lần đọc kế tiếp lấy giá trị mới (dùng sau khi admin đổi tham số).</summary>
    public void Invalidate() => _cached = null;

    public async Task<GameConfig> GetAsync(CancellationToken ct = default)
    {
        if (_cached != null && DateTimeOffset.UtcNow - _loadedAt < CacheFor)
            return _cached;

        await _lock.WaitAsync(ct);
        try
        {
            if (_cached != null && DateTimeOffset.UtcNow - _loadedAt < CacheFor)
                return _cached;

            var values = new Dictionary<string, string>();
            await using (var conn = await db.OpenConnectionAsync(ct))
            await using (var cmd = new NpgsqlCommand("select key, value from game_config", conn))
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                    values[reader.GetString(0)] = reader.GetString(1);
            }

            var defaults = new GameConfig();
            _cached = new GameConfig
            {
                MarketFeePercent = Int(values, "market.feePercent", defaults.MarketFeePercent),
                MarketMaxActiveListings = Int(values, "market.maxActiveListings", defaults.MarketMaxActiveListings),
                MarketListingHours = Int(values, "market.listingHours", defaults.MarketListingHours),
                MarketMinPriceFactor = Double(values, "market.minPriceFactor", defaults.MarketMinPriceFactor),
                MarketMaxPriceFactor = Double(values, "market.maxPriceFactor", defaults.MarketMaxPriceFactor),
                MarketMaxCardsPerListing = Int(values, "market.maxCardsPerListing", defaults.MarketMaxCardsPerListing),
                MarketMaxActiveCardListings = Int(values, "market.maxActiveCardListings", defaults.MarketMaxActiveCardListings),
                TradeOfferHours = Int(values, "trade.offerHours", defaults.TradeOfferHours),
                TradeMaxPendingOutgoing = Int(values, "trade.maxPendingOutgoing", defaults.TradeMaxPendingOutgoing),
                TradeMaxCardsPerSide = Int(values, "trade.maxCardsPerSide", defaults.TradeMaxCardsPerSide),
                ObstacleAllowedKinds = values.TryGetValue("obstacle.allowedKinds", out var kinds)
                    ? kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : defaults.ObstacleAllowedKinds,
                ObstacleFeeCoins = Int(values, "obstacle.feeCoins", defaults.ObstacleFeeCoins),
                ObstacleDailyLimit = Int(values, "obstacle.dailyLimit", defaults.ObstacleDailyLimit),
                ObstacleMaxPendingPerWorld = Int(values, "obstacle.maxPendingPerWorld", defaults.ObstacleMaxPendingPerWorld),
                ObstacleNewPlayerProtectionHours = Int(values, "obstacle.newPlayerProtectionHours", defaults.ObstacleNewPlayerProtectionHours),
                SyncInboxSeconds = Int(values, "sync.inboxSeconds", defaults.SyncInboxSeconds),
                SaveUploadSeconds = Int(values, "save.uploadSeconds", defaults.SaveUploadSeconds),
                WorldMaxTiles = Int(values, "world.maxTiles", defaults.WorldMaxTiles)
            };
            _loadedAt = DateTimeOffset.UtcNow;
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static int Int(Dictionary<string, string> values, string key, int fallback) =>
        values.TryGetValue(key, out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
            ? v
            : fallback;

    private static double Double(Dictionary<string, string> values, string key, double fallback) =>
        values.TryGetValue(key, out var raw) && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
            ? v
            : fallback;
}

public static class ConfigEndpoints
{
    public static void MapConfigEndpoints(this IEndpointRouteBuilder app)
    {
        // Không cần đăng nhập: client đọc được cả trước khi vào game
        app.MapGet("/api/v1/config", async (GameConfigService config, CancellationToken ct) =>
            Results.Ok(await config.GetAsync(ct)));
    }
}
