using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Danh sách thẻ bán trong Cửa hàng thẻ. Giá mua và phí đặt nằm trên từng CardData (shopPrice, placementFee);
/// thẻ có shopPrice = 0 là chưa có giá nên chưa hiện trong cửa hàng.
///
/// Asset phải nằm trong một thư mục Resources (hiện là Assets/Data/Resources/Shop) để game tự tìm thấy.
/// Thêm thẻ mới: kéo CardData vào danh sách, hoặc bấm menu ⋮ của asset rồi chọn
/// "Thêm mọi thẻ CardData trong project" và "Điền giá mặc định cho thẻ chưa có giá".
/// </summary>
[CreateAssetMenu(fileName = "ShopCatalog", menuName = "LumiWorld/Shop Catalog", order = 12)]
public class ShopCatalog : ScriptableObject
{
    [Tooltip("Các thẻ bán trong cửa hàng. Cửa hàng tự xếp theo loại thẻ rồi tới tên, giống bài trên tay.")]
    public List<CardData> cards = new List<CardData>();

    [Tooltip("Giá theo loại thẻ (bảng giá trong plan Coins). Chỉ dùng cho menu \"Điền giá mặc định cho thẻ chưa có giá\", lúc chơi không đọc tới.")]
    public List<CardTypePrice> defaultPrices = new List<CardTypePrice>
    {
        new CardTypePrice { cardType = CardType.Terrain, shopPrice = 20, placementFee = 5 },
        new CardTypePrice { cardType = CardType.Creature, shopPrice = 50, placementFee = 10 },
        new CardTypePrice { cardType = CardType.Building, shopPrice = 80, placementFee = 10 },
        new CardTypePrice { cardType = CardType.Special, shopPrice = 30, placementFee = 5 },
    };

#if UNITY_EDITOR
    [ContextMenu("Thêm mọi thẻ CardData trong project")]
    private void AddAllCards()
    {
        UnityEditor.Undo.RecordObject(this, "Thêm thẻ vào cửa hàng");

        int added = 0;
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:CardData"))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            CardData card = UnityEditor.AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (card == null || cards.Contains(card)) continue;

            cards.Add(card);
            added++;
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[LumiWorld Cửa hàng] Đã thêm {added} thẻ vào {name}.", this);
    }

    [ContextMenu("Điền giá mặc định cho thẻ chưa có giá")]
    private void FillDefaultPrices()
    {
        int filled = 0;
        foreach (CardData card in cards)
        {
            // Thẻ đã có giá mua coi như đã chỉnh tay, không đụng tới
            if (card == null || card.shopPrice > 0) continue;

            CardTypePrice price = defaultPrices.Find(entry => entry != null && entry.cardType == card.cardType);
            if (price == null) continue;

            UnityEditor.Undo.RecordObject(card, "Điền giá thẻ");
            card.shopPrice = price.shopPrice;
            if (card.placementFee == 0) card.placementFee = price.placementFee;
            UnityEditor.EditorUtility.SetDirty(card);
            filled++;
        }

        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[LumiWorld Cửa hàng] Đã điền giá mặc định cho {filled} thẻ.", this);
    }
#endif
}

/// <summary>
/// Giá mua và phí đặt mặc định của một loại thẻ.
/// </summary>
[Serializable]
public class CardTypePrice
{
    public CardType cardType;

    [Min(0)]
    public int shopPrice;

    [Min(0)]
    public int placementFee;
}
