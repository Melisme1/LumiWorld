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
        string raw = !string.IsNullOrEmpty(card.cardName) ? card.cardName : (!string.IsNullOrEmpty(card.cardID) ? card.cardID : card.name);
        return TranslateToEnglish(raw, card.name);
    }

    public static string TranslateToEnglish(string name, string fallbackAsset = "")
    {
        if (string.IsNullOrEmpty(name)) return fallbackAsset ?? string.Empty;
        string key = name.Trim().ToLowerInvariant();

        if (key.Contains("bò lông dài") || key.Contains("bolongdai") || key.Contains("highland")) return "Highland Cow";
        if (key.Contains("chim công") || key.Contains("chimcong") || key.Contains("peacock")) return "Peacock";
        if (key.Contains("châu chấu") || key.Contains("chauchau") || key.Contains("grasshopper")) return "Grasshopper";
        if (key.Contains("chó hoa") || key.Contains("chohoa") || key.Contains("flowerdog")) return "Flower Dog";
        if (key.Contains("nai rừng") || key.Contains("nai") || key.Contains("deer")) return "Forest Deer";
        if (key.Contains("sâu bướm") || key.Contains("saubuom") || key.Contains("caterpillar")) return "Caterpillar";
        if (key.Contains("sóc bay") || key.Contains("socbay") || key.Contains("flyingsquirrel")) return "Flying Squirrel";
        if (key.Contains("bò cỏ") || key.Contains("bo") || key.Contains("cow")) return "Meadow Cow";
        if (key.Contains("chuồn chuồn") || key.Contains("chuonchuon") || key.Contains("dragonfly")) return "Dragonfly";
        if (key.Contains("rái cá") || key.Contains("raica") || key.Contains("otter")) return "River Otter";
        if (key.Contains("ếch hoa") || key.Contains("echhoa") || key.Contains("blossomfrog")) return "Blossom Frog";
        if (key.Contains("cáo cỏ") || key.Contains("caoco") || key.Contains("grassfox")) return "Grass Fox";
        if (key.Contains("cáo đỏ") || key.Contains("cao") || key.Contains("fox")) return "Red Fox";
        if (key.Contains("vẹt 7 màu") || key.Contains("chimvet") || key.Contains("parrot")) return "Rainbow Parrot";
        if (key.Contains("thiên nga") || key.Contains("thiennga") || key.Contains("swan")) return "Swan";
        if (key.Contains("capybara")) return "Capybara";
        if (key.Contains("mưa") || key.Contains("rain")) return "Rain";
        if (key.Contains("cháy rừng") || key.Contains("hoa hoan") || key.Contains("forest fire")) return "Forest Fire";
        if (key.Contains("lốc xoáy") || key.Contains("loc xoay") || key.Contains("cyclone")) return "Cyclone";
        if (key.Contains("hạn hán") || key.Contains("han han") || key.Contains("drought")) return "Drought";
        if (key.Contains("đất cằn") || key.Contains("dat can") || key.Contains("barren")) return "Barren Land";
        if (key.Contains("chướng ngại") || key.Contains("chuong ngai") || key.Contains("obstacle")) return "Obstacle";
        if (key.Contains("leafwood")) return "Leafwood";
        if (key.Contains("bloomfield")) return "Bloomfield";
        if (key.Contains("windheath")) return "Windheath";
        if (key.Contains("reedmarsh")) return "Reedmarsh";
        if (key.Contains("lilywater")) return "Lilywater";
        if (key.Contains("streamstone")) return "Streamstone";
        if (key.Contains("rainleaf")) return "Rainleaf";
        if (key.Contains("đồng cỏ") || key.Contains("dong co")) return "Meadows";

        return name;
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
