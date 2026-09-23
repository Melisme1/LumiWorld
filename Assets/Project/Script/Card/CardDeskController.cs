using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardDeskController : MonoBehaviour
{

    public static CardDeskController Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    [Header("References")]
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private Transform cardContainer;

    [Header("Cards")]
    [SerializeField] private List<CardData> cardsToSpawn = new();

    [Header("Layout")]
    [SerializeField] private float maxSpacing = 170f;
    [SerializeField] private float minSpacing = 80f;
    [SerializeField] private float handWidth = 900f;

    [Header("Appear Animation")]
    [SerializeField] private float appearDelay = 0.15f;

    [Header("Rearrange Animation")]
    [SerializeField] private float rearrangeDuration = 0.25f;

    private readonly List<GameObject> cards = new();

    // cardID -> slot đại diện đang có trên tay (gộp các card cùng loại)
    private readonly Dictionary<string, GameObject> slotByCardID = new();

    private void Start()
    {
        StartCoroutine(SpawnCards());
    }

    private IEnumerator SpawnCards()
    {
        // Gộp các CardData cùng loại (theo cardID) thành 1 slot + đếm số lượng
        List<CardData> uniqueCards = new List<CardData>();
        Dictionary<string, int> countByCardID = new Dictionary<string, int>();

        foreach (CardData data in cardsToSpawn)
        {
            if (data == null) continue;

            string id = GetCardKey(data);
            if (countByCardID.TryGetValue(id, out int existing))
            {
                countByCardID[id] = existing + 1;
            }
            else
            {
                countByCardID[id] = 1;
                uniqueCards.Add(data);
            }
        }

        uniqueCards.Sort(CompareCardData);

        int cardCount =
            uniqueCards.Count;

        if (cardCount == 0)
        {
            Debug.LogWarning(
                "CardDeskController: No CardData assigned."
            );

            yield break;
        }

        float spacing =
            CalculateSpacing(cardCount);

        float totalWidth =
            (cardCount - 1) * spacing;

        for (int i = 0; i < cardCount; i++)
        {
            GameObject card =
                Instantiate(
                    cardPrefab,
                    cardContainer
                );

            // -------------------------
            // CARD DATA
            // -------------------------

            CardUI cardUI =
                card.GetComponent<CardUI>();

            if (cardUI != null)
            {
                cardUI.Setup(
                    uniqueCards[i]
                );

                int count = countByCardID.TryGetValue(
                    GetCardKey(uniqueCards[i]),
                    out int c) ? c : 1;

                cardUI.SetCount(count);
            }

            slotByCardID[GetCardKey(uniqueCards[i])] = card;

            // -------------------------
            // POSITION
            // -------------------------

            float x =
                i * spacing -
                totalWidth / 2f;

            Vector2 targetPosition =
                new Vector2(x, 0f);

            // Tell Card its Home Position
            CardHandSlot handSlot =
                card.GetComponent<CardHandSlot>();

            if (handSlot != null)
            {
                handSlot.SetTargetPosition(
                    targetPosition
                );
            }

            // -------------------------
            // APPEAR
            // -------------------------

            CardAppear appear =
                card.GetComponent<CardAppear>();

            if (appear != null)
            {
                appear.Play(
                    targetPosition,
                    i * appearDelay
                );
            }
            else
            {
                RectTransform rect =
                    card.GetComponent<RectTransform>();

                rect.anchoredPosition =
                    targetPosition;
            }

            cards.Add(card);

            yield return new WaitForSeconds(
                appearDelay
            );
        }
    }

    // =========================================
    // REMOVE CARD
    // =========================================

    public void RemoveCard(
        GameObject card
    )
    {
        if (card == null || !cards.Contains(card))
        {
            return;
        }

        // CardDrag gọi hàm này khi đặt bài thành công -> mỗi lần đặt TIÊU 1 LÁ.
        CardUI cardUI = card.GetComponent<CardUI>();

        if (cardUI != null && cardUI.Count > 1)
        {
            // Chồng còn nhiều lá -> chỉ giảm số lượng, giữ slot lại
            cardUI.SetCount(cardUI.Count - 1);

            // Slot vị trí không đổi nhưng vẫn sắp xếp lại cho chắc chắn
            RearrangeCards();
            return;
        }

        // Hết lá -> xóa slot và cập nhật bảng tra cứu
        cards.Remove(card);

        if (cardUI != null && cardUI.CardData != null)
        {
            string key = GetCardKey(cardUI.CardData);
            if (slotByCardID.TryGetValue(key, out GameObject tracked) && tracked == card)
            {
                slotByCardID.Remove(key);
            }
        }

        Destroy(card);

        RearrangeCards();
    }

    // =========================================
    // REARRANGE CARDS
    // =========================================

    public void RearrangeCards()
    {
        RearrangeCards(null);
    }

    private void RearrangeCards(GameObject cardAlreadyAnimating)
    {
        SortCardsByTypeAndName();

        int cardCount =
            cards.Count;

        if (cardCount == 0)
        {
            return;
        }

        float spacing =
            CalculateSpacing(cardCount);

        float totalWidth =
            (cardCount - 1) * spacing;

        for (int i = 0; i < cardCount; i++)
        {
            float x =
                i * spacing -
                totalWidth / 2f;

            Vector2 targetPosition =
                new Vector2(x, 0f);

            // Update Home Position
            CardHandSlot handSlot =
                cards[i].GetComponent<CardHandSlot>();

            if (handSlot != null)
            {
                handSlot.SetTargetPosition(
                    targetPosition
                );
            }

            // Move Card
            CardMoveToPosition mover =
                cards[i].GetComponent<CardMoveToPosition>();

            if (cards[i] == cardAlreadyAnimating)
            {
                continue;
            }
            else if (mover != null)
            {
                mover.MoveTo(
                    targetPosition,
                    rearrangeDuration
                );
            }
            else
            {
                RectTransform rect =
                    cards[i].GetComponent<RectTransform>();

                rect.anchoredPosition =
                    targetPosition;
            }
        }
    }

    // =========================================
    // ADD REWARD CARD
    // =========================================

public void AddRewardCard(CardData cardData)
{
    if (cardData == null)
    {
        Debug.LogWarning(
            "CardDeskController: Reward CardData is null."
        );
        return;
    }

    if (cardPrefab == null)
    {
        Debug.LogError(
            "CardDeskController: Card Prefab is not assigned."
        );
        return;
    }

    if (cardContainer == null)
    {
        Debug.LogError(
            "CardDeskController: Card Container is not assigned."
        );
        return;
    }

    // -------------------------
    // GỘP THEO CARD ID
    // -------------------------
    // Nếu trên tay đã có slot cùng loại -> chỉ tăng số lượng, không tạo card mới.
    string key = GetCardKey(cardData);

    if (slotByCardID.TryGetValue(key, out GameObject existingSlot) && existingSlot != null)
    {
        CardUI existingUI = existingSlot.GetComponent<CardUI>();
        if (existingUI != null)
        {
            existingUI.SetCount(existingUI.Count + 1);
        }

        // Nhảy nhẹ để báo hiệu đã cộng thêm
        CardAppear existingAppear = existingSlot.GetComponent<CardAppear>();
        if (existingAppear != null)
        {
            RectTransform existingRect = existingSlot.GetComponent<RectTransform>();
            Vector2 pos = existingRect != null ? existingRect.anchoredPosition : Vector2.zero;
            existingAppear.Play(pos, 0f);
        }

        Debug.Log(
            $"Reward Card stacked: {cardData.cardName} (+1)"
        );
        return;
    }

    GameObject card =
        Instantiate(
            cardPrefab,
            cardContainer
        );

    CardUI cardUI =
        card.GetComponent<CardUI>();

    if (cardUI != null)
    {
        cardUI.Setup(cardData);
        cardUI.SetCount(1);
    }

    cards.Add(card);

    slotByCardID[key] = card;

    SortCardsByTypeAndName();

    // Tính lại vị trí cho toàn bộ hand
    int cardCount = cards.Count;

    float spacing =
        CalculateSpacing(cardCount);

    float totalWidth =
        (cardCount - 1) * spacing;

    int newCardIndex =
        cards.IndexOf(card);

    float x =
        newCardIndex * spacing -
        totalWidth / 2f;

    Vector2 targetPosition =
        new Vector2(x, 0f);

    // Card mới
    CardHandSlot handSlot =
        card.GetComponent<CardHandSlot>();

    if (handSlot != null)
    {
        handSlot.SetTargetPosition(
            targetPosition
        );
    }

    CardAppear appear =
        card.GetComponent<CardAppear>();

    if (appear != null)
    {
        appear.Play(
            targetPosition,
            0f
        );
    }
    else
    {
        RectTransform rect =
            card.GetComponent<RectTransform>();

        if (rect != null)
        {
            rect.anchoredPosition =
                targetPosition;
        }
    }

    // Các card cũ tự sắp xếp lại
    RearrangeCards(card);

    Debug.Log(
        $"Reward Card added: {cardData.cardName}"
    );
}
    // =========================================
    // CALCULATE SPACING
    // =========================================

    private float CalculateSpacing(
        int cardCount
    )
    {
        if (cardCount <= 1)
        {
            return 0f;
        }

        float spacing =
            handWidth /
            (cardCount - 1);

        return Mathf.Clamp(
            spacing,
            minSpacing,
            maxSpacing
        );
    }

    // =========================================
    // CARD KEY
    // =========================================

    /// <summary>
    /// Khóa gộp card: ưu tiên cardID; nếu trống thì dùng tên để tránh gộp nhầm các card không có ID.
    /// </summary>
    private static string GetCardKey(CardData data)
    {
        if (data == null) return string.Empty;
        if (!string.IsNullOrEmpty(data.cardID)) return data.cardID;
        return data.cardName ?? string.Empty;
    }

    private void SortCardsByTypeAndName()
    {
        cards.Sort((left, right) => CompareCardData(
            left != null ? left.GetComponent<CardUI>()?.CardData : null,
            right != null ? right.GetComponent<CardUI>()?.CardData : null
        ));

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null)
            {
                cards[i].transform.SetSiblingIndex(i);
            }
        }
    }

    // Hand order: type first, then card name, then ID for a deterministic tie-break.
    private static int CompareCardData(CardData left, CardData right)
    {
        if (left == right) return 0;
        if (left == null) return 1;
        if (right == null) return -1;

        int typeComparison = left.cardType.CompareTo(right.cardType);
        if (typeComparison != 0) return typeComparison;

        int nameComparison = string.Compare(
            left.cardName,
            right.cardName,
            StringComparison.OrdinalIgnoreCase
        );
        if (nameComparison != 0) return nameComparison;

        return string.Compare(
            left.cardID,
            right.cardID,
            StringComparison.OrdinalIgnoreCase
        );
    }
}
