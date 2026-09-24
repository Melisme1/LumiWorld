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

    [Header("Reward Feedback (game feel khi nhận card)")]
    [Tooltip("Hiện banner 'NHẬN CARD MỚI: <tên>'. Mặc định TẮT: banner che mất bản đồ, thay vào đó dùng tiếng 'ding' + tia sáng + badge số lượng.")]
    [SerializeField] private bool showRewardToast = false;

    [Tooltip("Tiếng 'ding' + tia sáng + vòng highlight quanh thẻ vừa nhận.")]
    [SerializeField] private bool playRewardJuice = true;

    [Tooltip("Vị trí banner so với slot card (pixel, theo hệ toạ độ của CardContainer).")]
    [SerializeField] private Vector2 toastOffset = new Vector2(0f, 190f);

    [Tooltip("Hiện banner tóm tắt toàn bộ hand lúc bắt đầu game. Mặc định TẮT vì banner này che mất bản đồ.")]
    [SerializeField] private bool showStartingHandSummary = false;

    private readonly List<GameObject> cards = new();

    // cardID -> slot đại diện đang có trên tay (gộp các card cùng loại)
    private readonly Dictionary<string, GameObject> slotByCardID = new();

    /// <summary>
    /// Bỏ qua hiệu ứng nhận card khi spawn hand ban đầu (lúc đó cả bàn tay
    /// xuất hiện cùng lúc nên highlight/tia sáng/banner cho từng lá sẽ rất rối).
    /// </summary>
    private bool suppressRewardFeedback;

    private void Start()
    {
        StartCoroutine(SpawnCards());
    }

    private IEnumerator SpawnCards()
    {
        suppressRewardFeedback = true;

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

        suppressRewardFeedback = false;

        // KHÔNG hiện banner tóm tắt hand ban đầu.
        // Banner "N LOẠI CARD: ..." che mất bản đồ ngay khi vào game, trong khi
        // người chơi đã nhìn thấy toàn bộ hand ở dưới màn hình rồi (kèm badge số lượng).
        // Bật lại bằng showStartingHandSummary nếu vẫn muốn dùng.
        if (showStartingHandSummary)
        {
            ShowStartingHandSummary();
        }
    }

    /// <summary>
    /// Tóm tắt hand ban đầu thành 1 banner duy nhất, ví dụ:
    /// "3 LOẠI CARD: Forest x2 · Rock x1 · River x1 (4 LÁ)".
    /// </summary>
    private void ShowStartingHandSummary()
    {
        if (!showRewardToast || cards.Count == 0)
        {
            return;
        }

        List<string> parts = new List<string>();
        int totalCards = 0;

        foreach (GameObject card in cards)
        {
            if (card == null) continue;

            CardUI ui = card.GetComponent<CardUI>();
            if (ui == null || ui.CardData == null) continue;

            string name = !string.IsNullOrEmpty(ui.CardData.cardName)
                ? ui.CardData.cardName
                : ui.CardData.cardID;

            totalCards += ui.Count;
            parts.Add(ui.Count > 1 ? $"{name} x{ui.Count}" : name);
        }

        if (parts.Count == 0)
        {
            return;
        }

        string title = $"{parts.Count} LOẠI CARD";
        string body = string.Join("  ·  ", parts) + $"  ({totalCards} LÁ)";

        ShowToast(title, body);
    }

    /// <summary>
    /// Gọi banner nhận card, neo ở vị trí giữa hand để không bị lệch khi số thẻ thay đổi.
    /// </summary>
    private void ShowToast(string title, string body, CardData highlightCard = null)
    {
        CardRewardToast.Show(
            title,
            body,
            GetToastAnchorPosition(),
            highlightCard != null ? highlightCard.cardImage : null
        );
    }

    /// <summary>
    /// Neo banner ở vị trí giữa hand để không bị lệch khi số lượng thẻ thay đổi.
    /// </summary>
    private Vector2 GetToastAnchorPosition()
    {
        RectTransform containerRect = cardContainer as RectTransform;

        if (containerRect != null)
        {
            return containerRect.anchoredPosition + toastOffset;
        }

        return toastOffset;
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
            // Chồng còn nhiều lá -> chỉ giảm số lượng, giữ slot lại.
            // KHÔNG sắp xếp lại vị trí: thứ tự hand không đổi khi chỉ giảm số lượng,
            // nếu rearrange ở đây card có thể bị kéo về home position trong lúc
            // CardDrag đang chạy animation trả bài -> giật/hai animation đánh nhau.
            cardUI.SetCount(cardUI.Count - 1);
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
        RearrangeCards(cardAlreadyAnimating, true);
    }

    /// <param name="sortCards">
    /// Sort lại hand trước khi tính vị trí. Truyền false khi caller vừa sort xong
    /// (tránh đổi sibling index/thứ tự hai nhịp gây nhảy lớp).
    /// </param>
    private void RearrangeCards(GameObject cardAlreadyAnimating, bool sortCards)
    {
        if (sortCards)
        {
            SortCardsByTypeAndName();
        }

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

        // Nhảy nhẹ tại chỗ (không dịch chuyển) để báo hiệu đã cộng thêm.
        RectTransform existingRect = existingSlot.GetComponent<RectTransform>();

        CardAppear existingAppear = existingSlot.GetComponent<CardAppear>();
        if (existingAppear != null)
        {
            existingAppear.PlayRewardPunch();
        }

        TryPlayRewardFeedback(
            cardData,
            existingRect,
            isNewSlot: false,
            totalCount: existingUI != null ? existingUI.Count : 1
        );
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

    // Sắp xếp hand một lần duy nhất sau khi thêm card mới.
    // RearrangeCards() bên dưới dùng lại thứ tự này và không sort lần nữa.
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

    // Hủy mọi animation dời chỗ còn sót để không tranh chấp với appear.
    CardMoveToPosition newMover =
        card.GetComponent<CardMoveToPosition>();

    if (newMover != null)
    {
        newMover.Stop();
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

    // Các card cũ tự sắp xếp lại (không cần sort lại nữa)
    RearrangeCards(card, false);

    TryPlayRewardFeedback(
        cardData,
        card.GetComponent<RectTransform>(),
        isNewSlot: true,
        totalCount: 1
    );

    Debug.Log(
        $"Reward Card added: {cardData.cardName}"
    );
}

// =========================================
// REWARD FEEDBACK
// =========================================

/// <summary>
/// Báo cho người chơi biết vừa NHẬN ĐƯỢC card gì:
///  - banner "NHẬN CARD MỚI: <tên>" (hoặc "<tên> xN" khi cộng dồn)
///  - tiếng 'ding' + tia sáng + vòng highlight quanh thẻ
/// Bỏ qua khi đang spawn hand ban đầu (suppressRewardFeedback).
/// </summary>
private void NotifyRewardCard(
    CardData cardData,
    RectTransform slotRect,
    bool isNewSlot,
    int totalCount)
{
    if (suppressRewardFeedback || cardData == null)
    {
        return;
    }

    string displayName = !string.IsNullOrEmpty(cardData.cardName)
        ? cardData.cardName
        : (!string.IsNullOrEmpty(cardData.cardID) ? cardData.cardID : "Card");

    if (showRewardToast)
    {
        string title = isNewSlot ? "NHẬN CARD MỚI" : "CỘNG THÊM CARD";
        string body = totalCount > 1
            ? $"{displayName} x{totalCount}"
            : displayName;

        ShowToast(title, body, cardData);
    }

    if (playRewardJuice)
    {
        // Phản hồi khi nhận card giờ chỉ còn tiếng 'ding' (đã bỏ tia sáng/vệt sáng
        // vì hay lệch khỏi khung thẻ và gây rối mắt); badge số lượng do CardUI vẽ.
        CardRewardJuice.Play(slotRect, isNewSlot);
    }
}

/// <summary>
/// Gọi feedback trong try/catch có chủ đích: hiệu ứng chỉ là lớp trang trí,
/// TUYỆT ĐỐI không được phép làm hỏng luồng thêm card.
///
/// Trước đây một lỗi trong CardRewardToast ném ra từ ScoreManager.CheckMilestoneRewards
/// đã hủy vòng lặp cấp thưởng giữa chừng, khiến milestone nhiều card chỉ nhận được 1 lá.
/// Bắt lỗi tại đây để một hiệu ứng hỏng không bao giờ chặn được card tiếp theo.
/// </summary>
private void TryPlayRewardFeedback(
    CardData cardData,
    RectTransform slotRect,
    bool isNewSlot,
    int totalCount)
{
    try
    {
        NotifyRewardCard(cardData, slotRect, isNewSlot, totalCount);
    }
    catch (Exception exception)
    {
        Debug.LogWarning(
            $"CardDeskController: Reward feedback failed for '{cardData?.cardName}' " +
            $"but the card was added successfully. Error: {exception.Message}"
        );
    }
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
