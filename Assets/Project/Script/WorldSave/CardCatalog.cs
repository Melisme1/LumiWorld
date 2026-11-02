using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mọi CardData của game, tra theo cardID lúc nạp save. Tạo bằng Tools > LumiWorld > Xây danh mục save.
/// </summary>
public class CardCatalog : ScriptableObject
{
    [SerializeField] private List<CardData> cards = new List<CardData>();

    private Dictionary<string, CardData> byId;

    public void SetCards(List<CardData> list)
    {
        cards = list;
        byId = null;
    }

    public IReadOnlyList<CardData> All => cards;

    public CardData Find(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return null;

        if (byId == null) Build();
        return byId.TryGetValue(cardId, out CardData card) ? card : null;
    }

    private void Build()
    {
        byId = new Dictionary<string, CardData>();
        foreach (CardData card in cards)
        {
            if (card == null || string.IsNullOrEmpty(card.cardID)) continue;

            if (byId.ContainsKey(card.cardID))
            {
                Debug.LogWarning($"[LumiWorld World] cardID '{card.cardID}' bị trùng trong CardCatalog.", this);
                continue;
            }
            byId.Add(card.cardID, card);
        }
    }
}
