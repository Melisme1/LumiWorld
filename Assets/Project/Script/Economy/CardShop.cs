using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cửa hàng thẻ: mua thẻ bằng Coins, thẻ mua xong vào thẳng bài trên tay (giống thẻ thưởng).
/// Sở hữu thẻ và đặt thẻ là hai khoản riêng (Capstone 1 FR-2.4): shopPrice trả một lần lúc mua,
/// placementFee trả mỗi lần đặt thẻ lên bản đồ.
/// Danh sách thẻ lấy từ các asset ShopCatalog trong thư mục Resources (Assets/Data/Resources/Shop).
/// </summary>
public static class CardShop
{
    private static List<CardData> cards;
    private static Dictionary<string, int> testPrices;

    /// <summary>
    /// Các thẻ đang bán (đã có giá), xếp theo loại thẻ rồi tới tên giống bài trên tay.
    /// </summary>
    public static IReadOnlyList<CardData> Cards
    {
        get
        {
            if (cards == null) Load();
            return cards;
        }
    }

    public static int GetPrice(CardData card)
    {
        if (card == null) return 0;
        if (cards == null) Load();
        return testPrices.TryGetValue(CardKey(card), out int temporaryPrice) ?
            temporaryPrice : Mathf.Max(0, card.shopPrice);
    }

    /// <summary>
    /// Số Coins phải có để mua một lá: giá thẻ cộng phí đặt. Chỉ trừ giá thẻ, phần phí đặt còn lại trong ví
    /// để thẻ vừa mua luôn đặt được ngay, người chơi không bị kẹt vì tiêu hết Coins vào cửa hàng.
    /// </summary>
    public static int GetRequiredCoins(CardData card)
    {
        return card != null ? GetPrice(card) + Mathf.Max(0, card.placementFee) : 0;
    }

    /// <summary>
    /// Mua được khi thẻ có giá, màn chơi có bài trên tay và đủ Coins cho cả giá thẻ lẫn phí đặt.
    /// </summary>
    public static bool CanBuy(CardData card)
    {
        return GetPrice(card) > 0
            && CardDeskController.Instance != null
            && CurrencyWallet.Instance.CanAfford(GetRequiredCoins(card));
    }

    /// <summary>
    /// Mua một lá: trừ giá thẻ rồi thêm thẻ vào bài trên tay. Không đủ Coins thì không làm gì và trả về false.
    /// </summary>
    public static bool TryBuy(CardData card)
    {
        int price = GetPrice(card);
        if (price <= 0) return false;

        CardDeskController hand = CardDeskController.Instance;
        if (hand == null)
        {
            Debug.LogWarning("[LumiWorld Cửa hàng] Màn này chưa có bài trên tay (CardDeskController) nên chưa mua được thẻ.");
            return false;
        }

        CurrencyWallet wallet = CurrencyWallet.Instance;
        if (!wallet.CanAfford(GetRequiredCoins(card)) || !wallet.TrySpend(price, CoinReason.CardPurchase)) return false;

        hand.AddRewardCard(card);
        Debug.Log($"<color=#A78BFA>🛒 [LumiWorld Cửa hàng] Đã mua 1 lá {CardName(card)}, giá {price} Coins.</color>");
        return true;
    }

    /// <summary>
    /// Số lá của từng loại thẻ đang có trên tay, theo CardKey. Bài cùng loại được gộp thành một chồng có số lượng.
    /// </summary>
    public static Dictionary<string, int> CountHand()
    {
        Dictionary<string, int> counts = new Dictionary<string, int>();
        foreach (CardUI slot in UnityEngine.Object.FindObjectsByType<CardUI>())
        {
            if (slot.CardData == null) continue;

            string key = CardKey(slot.CardData);
            counts.TryGetValue(key, out int count);
            counts[key] = count + slot.Count;
        }
        return counts;
    }

    /// <summary>
    /// Khóa gộp bài giống CardDeskController: ưu tiên cardID, trống thì dùng tên thẻ.
    /// </summary>
    public static string CardKey(CardData card)
    {
        if (card == null) return string.Empty;
        if (!string.IsNullOrEmpty(card.cardID)) return card.cardID;
        return card.cardName ?? string.Empty;
    }

    public static string CardName(CardData card)
    {
        if (card == null) return string.Empty;
        if (!string.IsNullOrEmpty(card.cardName)) return card.cardName;
        return !string.IsNullOrEmpty(card.cardID) ? card.cardID : card.name;
    }

    // Khi tắt Domain Reload lúc vào Play Mode, biến static không tự xóa: nạp lại danh sách mỗi lần chạy
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        cards = null;
        testPrices = null;
    }

    private static void Load()
    {
        cards = new List<CardData>();
        testPrices = new Dictionary<string, int>();
        HashSet<string> seenKeys = new HashSet<string>();
        List<string> unpriced = new List<string>();

        foreach (ShopCatalog catalog in Resources.LoadAll<ShopCatalog>(string.Empty))
        {
            List<CardData> offeredCards = catalog.useTestCardList ? catalog.testCards : catalog.cards;
            if (offeredCards == null) continue;

            foreach (CardData card in offeredCards)
            {
                if (card == null || !seenKeys.Add(CardKey(card))) continue;

                int price = Mathf.Max(0, card.shopPrice);
                if (price <= 0 && catalog.useTestCardList)
                {
                    price = Mathf.Max(1, catalog.unpricedTestCardPrice);
                    testPrices[CardKey(card)] = price;
                }
                if (price <= 0)
                {
                    unpriced.Add(CardName(card));
                    continue;
                }
                cards.Add(card);
            }
        }

        cards.Sort(CompareCards);

        if (unpriced.Count > 0)
        {
            Debug.LogWarning($"[LumiWorld Cửa hàng] Thẻ chưa có giá (shopPrice = 0) nên chưa bán: {string.Join(", ", unpriced)}.");
        }

        if (cards.Count == 0)
        {
            Debug.LogWarning("[LumiWorld Cửa hàng] Chưa có thẻ nào để bán. Thêm thẻ vào ShopCatalog trong Assets/Data/Resources/Shop.");
        }
    }

    // Cùng thứ tự với bài trên tay: loại thẻ, rồi tên, rồi cardID
    private static int CompareCards(CardData left, CardData right)
    {
        int typeComparison = left.cardType.CompareTo(right.cardType);
        if (typeComparison != 0) return typeComparison;

        int nameComparison = string.Compare(left.cardName, right.cardName, StringComparison.OrdinalIgnoreCase);
        if (nameComparison != 0) return nameComparison;

        return string.Compare(left.cardID, right.cardID, StringComparison.OrdinalIgnoreCase);
    }
}
