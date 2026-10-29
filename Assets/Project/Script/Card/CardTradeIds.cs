using System;

/// <summary>
/// Thẻ thú trên chợ dùng id "card.&lt;cardID&gt;" (khớp listings.resource_id phía server).
/// Chỉ thẻ Creature trên tay mới bán được; thú đã thả xuống đảo không bán.
/// </summary>
public static class CardTradeIds
{
    public const string Prefix = "card.";

    public static bool IsCardId(string marketId) =>
        marketId != null && marketId.StartsWith(Prefix, StringComparison.Ordinal);

    public static bool IsTradable(CardData card) =>
        card != null && card.cardType == CardType.Creature && !string.IsNullOrEmpty(card.cardID);

    public static string ToMarketId(CardData card) => Prefix + card.cardID;

    /// <summary>cardID trong tay bài (bỏ tiền tố); null nếu không phải id thẻ.</summary>
    public static string ToCardId(string marketId) => IsCardId(marketId) ? marketId.Substring(Prefix.Length) : null;

    /// <summary>CardData của id chợ, chỉ trả thẻ Creature; null khi không tra được.</summary>
    public static CardData Find(string marketId)
    {
        string cardId = ToCardId(marketId);
        CardCatalog catalog = WorldSaveSystem.Instance != null ? WorldSaveSystem.Instance.Cards : null;
        CardData card = cardId != null && catalog != null ? catalog.Find(cardId) : null;
        return IsTradable(card) ? card : null;
    }
}
