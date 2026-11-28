using System.Globalization;
using Lumi.Api.Features.Config;
using Npgsql;

namespace Lumi.Api.Features.Market;

/// <summary>
/// Tham số chợ đọc lúc xử lý: tham số game (phí, giới hạn, giá sàn/trần) cộng baseValue của từng loại.
/// </summary>
public sealed record MarketSettings(
    int FeePercent,
    int MaxActiveListings,
    int ListingHours,
    int MaxCardsPerListing,
    int MaxActiveCardListings,
    decimal MinPriceFactor,
    decimal MaxPriceFactor,
    IReadOnlyDictionary<string, int> BaseValues)
{
    private const string BaseValuePrefix = "market.baseValue.";

    public static async Task<MarketSettings> LoadAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, GameConfigService config, CancellationToken ct)
    {
        GameConfig game = await config.GetAsync(ct);

        var baseValues = new Dictionary<string, int>();
        await using (var cmd = new NpgsqlCommand(
            "select key, value from game_config where key like 'market.baseValue.%'", conn, tx))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                string key = reader.GetString(0);
                if (int.TryParse(reader.GetString(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                    baseValues[key[BaseValuePrefix.Length..]] = value;
            }
        }

        return new MarketSettings(
            game.MarketFeePercent,
            game.MarketMaxActiveListings,
            game.MarketListingHours,
            game.MarketMaxCardsPerListing,
            game.MarketMaxActiveCardListings,
            (decimal)game.MarketMinPriceFactor,
            (decimal)game.MarketMaxPriceFactor,
            baseValues);
    }
}

/// <summary>
/// Quy tắc giá và phí của chợ, viết thành hàm thuần để test trực tiếp.
/// </summary>
public static class MarketRules
{
    public const int MaxAmount = 999;

    /// <summary>Lô thẻ thú: resource_id = "card.&lt;cardID&gt;". Mọi thứ khác là tài nguyên.</summary>
    public const string CardPrefix = "card.";

    public static bool IsCard(string resourceId) => resourceId.StartsWith(CardPrefix, StringComparison.Ordinal);

    // Lô giá 1 Coin thì người bán nhận 0 sau phí, nên tối thiểu 2 Coins
    public const int MinPriceCoins = 2;

    /// <summary>Phí 5% làm tròn lên, ít nhất 1 Coin.</summary>
    public static int Fee(int priceCoins, int feePercent)
    {
        long raw = ((long)priceCoins * feePercent + 99) / 100;
        return (int)Math.Max(1, raw);
    }

    /// <summary>
    /// Khoảng giá cả lô được chấp nhận: amount × hệ số × max(baseValue, 1), làm tròn xuống ở hai đầu.
    /// </summary>
    public static (int Min, int Max) PriceRange(int amount, int baseValue, MarketSettings settings)
    {
        decimal unitBase = Math.Max(baseValue, 1);
        long min = (long)Math.Floor(amount * settings.MinPriceFactor * unitBase);
        long max = (long)Math.Floor(amount * settings.MaxPriceFactor * unitBase);
        return ((int)Math.Max(MinPriceCoins, min), (int)max);
    }
}
