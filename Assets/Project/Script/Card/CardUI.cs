using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardUI : MonoBehaviour
{
    [Header("Card Data")]
    [SerializeField] private CardData cardData;

    [Header("UI")]
    [SerializeField] private Image cardImage;

    [Header("Count Badge")]
    [Tooltip("Text hiển thị số lượng card cùng loại. Để trống = tự sinh badge ở góc dưới-phải khi cần.")]
    [SerializeField] private TMP_Text countText;

    [Tooltip("Tự sinh badge số nếu Count Text chưa được gán.")]
    [SerializeField] private bool autoCreateCountBadge = true;

    public CardData CardData => cardData;

    /// <summary>
    /// Số lượng card cùng loại đang có trong chồng (do CardDeskController quản lý).
    /// </summary>
    public int Count { get; private set; } = 1;

    private void Start()
    {
        Refresh();
    }

    public void Setup(CardData data)
    {
        cardData = data;
        Refresh();
    }

    /// <summary>
    /// Cập nhật số lượng hiển thị trên badge. Badge chỉ hiện khi Count >= 2.
    /// </summary>
    public void SetCount(int count)
    {
        Count = Mathf.Max(1, count);
        UpdateCountBadge();
    }

    private void Refresh()
    {
        if (cardData == null)
        {
            Debug.LogWarning(
                "CardUI: CardData is not assigned.",
                this
            );
            return;
        }

        if (cardImage != null)
        {
            cardImage.sprite = cardData.cardImage;
        }

        UpdateCountBadge();
    }

    private void UpdateCountBadge()
    {
        if (countText == null && autoCreateCountBadge)
        {
            countText = CreateCountBadge();
        }

        if (countText == null)
        {
            return;
        }

        bool show = Count >= 2;
        if (countText.gameObject.activeSelf != show)
        {
            countText.gameObject.SetActive(show);
        }

        if (show)
        {
            countText.text = Count.ToString();
        }
    }

    /// <summary>
    /// Tự sinh một badge số ở góc dưới-phải thẻ nếu prefab chưa có sẵn.
    /// Dùng TextMeshProUGUI + Outline để nổi bật trên mọi nền ảnh.
    /// </summary>
    private TMP_Text CreateCountBadge()
    {
        if (!TryGetComponent(out RectTransform cardRect))
        {
            return null;
        }

        GameObject badgeObj = new GameObject("CountBadge", typeof(RectTransform));
        badgeObj.transform.SetParent(cardRect, false);

        RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0f, 1f);
        badgeRect.anchorMax = new Vector2(0f, 1f);
        badgeRect.pivot = new Vector2(0f, 1f);
        badgeRect.anchoredPosition = new Vector2(25f, -15f);
        badgeRect.sizeDelta = new Vector2(60f, 60f);

        TextMeshProUGUI tmp = badgeObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "1";
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 10f;
        tmp.fontSizeMax = 48f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = Color.black;

        badgeObj.SetActive(false);
        return tmp;
    }
}