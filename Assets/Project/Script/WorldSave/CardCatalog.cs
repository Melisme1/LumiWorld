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

    public CardData Find(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return null;

        if (byId == null) Build();
        if (byId.TryGetValue(cardId, out CardData card) && card != null)
        {
            if (cardId.IndexOf("braluff", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                BreedingManager.EnsureBraluffCardConfigured(card);
            }
            return card;
        }

        if (cardId.IndexOf("braluff", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            CardData braluff = BreedingManager.GetOrCreateBraluffCard();
            if (braluff != null)
            {
                byId[cardId] = braluff;
                return braluff;
            }
        }

        return null;
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

    /// <summary>All cards in the catalog (read-only). Used for listing/filtering in UI.</summary>
    public System.Collections.Generic.IReadOnlyList<CardData> AllCards => cards;
}
