using System.Collections.Generic;
using UnityEngine;

public class HexGroupDetector : MonoBehaviour
{
    public static HexGroupDetector Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // =========================================================
    // TÌM TOÀN BỘ GROUP CÙNG LOẠI
    // =========================================================

    public List<PlacedCard> FindGroup(PlacedCard startCard)
    {
        List<PlacedCard> group = new List<PlacedCard>();

        if (startCard == null || startCard.cardData == null)
            return group;

        // Lấy tất cả card đã được đặt trong Scene
        PlacedCard[] allCards =
            FindObjectsByType<PlacedCard>(
                FindObjectsSortMode.None
            );

        // Hex -> PlacedCard
        Dictionary<HexCoordinates, PlacedCard> cardsByHex =
            new Dictionary<HexCoordinates, PlacedCard>();

        foreach (PlacedCard card in allCards)
        {
            if (card == null || card.cardData == null)
                continue;

            if (!cardsByHex.ContainsKey(card.placedHex))
            {
                cardsByHex.Add(
                    card.placedHex,
                    card
                );
            }
        }

        // =====================================================
        // FLOOD FILL / BFS
        // =====================================================

        Queue<PlacedCard> queue =
            new Queue<PlacedCard>();

        HashSet<PlacedCard> visited =
            new HashSet<PlacedCard>();

        queue.Enqueue(startCard);
        visited.Add(startCard);

        while (queue.Count > 0)
        {
            PlacedCard currentCard =
                queue.Dequeue();

            group.Add(currentCard);

            // Kiểm tra 6 ô xung quanh
            for (int direction = 0; direction < 6; direction++)
            {
                HexCoordinates neighbourHex =
                    currentCard.placedHex.GetNeighbor(direction);

                if (!cardsByHex.TryGetValue(
                    neighbourHex,
                    out PlacedCard neighbourCard))
                {
                    continue;
                }

                // Không cùng loại -> không thuộc group
                if (!IsSameCardType(
                    startCard,
                    neighbourCard))
                {
                    continue;
                }

                // Đã kiểm tra rồi -> bỏ qua
                if (visited.Contains(neighbourCard))
                    continue;

                visited.Add(neighbourCard);
                queue.Enqueue(neighbourCard);
            }
        }

        return group;
    }

    // =========================================================
    // KIỂM TRA 2 CARD CÓ CÙNG LOẠI KHÔNG
    // =========================================================

    private bool IsSameCardType(
        PlacedCard a,
        PlacedCard b)
    {
        if (a == null || b == null)
            return false;

        if (a.cardData == null || b.cardData == null)
            return false;

        // Cùng CardID = cùng loại cụ thể
        return a.CardID == b.CardID;
    }
}