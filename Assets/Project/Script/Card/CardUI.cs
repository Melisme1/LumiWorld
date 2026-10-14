using System.Collections;
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

    [Header("Count Badge Juice")]
    [Tooltip("Số lượng hiển thị badge khi đạt mức này (1 = luôn hiện, giúp người chơi biết mình có gì).")]
    [SerializeField] private int badgeShowFromCount = 1;

    [Tooltip("Hệ số phóng to khi badge vừa tăng số lượng (punch).")]
    [SerializeField] private float badgePunchScale = 1.6f;

    [Tooltip("Thời gian badge nhảy (giây).")]
    [SerializeField] private float badgePunchDuration = 0.26f;

    [Tooltip("Màu badge khi vừa tăng số lượng, sau đó trả về màu gốc.")]
    [SerializeField] private Color badgeHighlightColor = new Color(1f, 0.94f, 0.45f, 1f);

    [Header("Lock / Escrow Badge")]
    [SerializeField] private Image lockBadgeImage;

    public CardData CardData => cardData;

    /// <summary>
    /// Trạng thái khóa của lá bài (Marketplace Escrow, Breeding, hoặc Account-Bound)
    /// </summary>
    public bool IsLocked { get; private set; }
    public CardLockType CurrentLockType { get; private set; }
    public string CurrentLockReason { get; private set; }

    /// <summary>
    /// Số lượng card cùng loại đang có trong chồng (do CardDeskController quản lý).
    /// </summary>
    public int Count { get; private set; } = 1;

    private void OnEnable()
    {
        if (CardLockManager.Instance != null)
        {
            CardLockManager.Instance.OnLockStateChanged += Refresh;
        }
    }

    private void OnDisable()
    {
        if (CardLockManager.HasInstance)
        {
            CardLockManager.Instance.OnLockStateChanged -= Refresh;
        }
    }

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
    /// Cập nhật số lượng hiển thị trên badge. Badge luôn hiện khi Count >= badgeShowFromCount
    /// (mặc định 1) để người chơi luôn nhận biết được mình đang có bao nhiêu lá.
    /// Nếu số lượng TĂNG, badge sẽ nhảy + sáng lên để báo hiệu vừa nhận thêm card.
    /// </summary>
    public void SetCount(int count)
    {
        int clampedCount = Mathf.Max(1, count);
        bool increased = clampedCount > Count;

        Count = clampedCount;
        UpdateCountBadge();

        if (increased)
        {
            PlayBadgePunch();
        }
    }

    public void Refresh()
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
        UpdateLockBadge();
    }

    private void UpdateLockBadge()
    {
        CardLockType lockType = CardLockType.None;
        string reason = string.Empty;
        IsLocked = CardLockManager.Instance != null && CardLockManager.Instance.IsCardLocked(cardData, out lockType, out reason);
        CurrentLockType = IsLocked ? lockType : CardLockType.None;
        CurrentLockReason = IsLocked ? reason : string.Empty;

        if (IsLocked)
        {
            if (lockBadgeImage == null)
            {
                lockBadgeImage = CreateLockBadge();
            }
            if (lockBadgeImage != null)
            {
                lockBadgeImage.gameObject.SetActive(true);
            }
            if (cardImage != null)
            {
                cardImage.color = new Color(0.72f, 0.72f, 0.72f, 0.88f); // Hơi tối màu để biểu thị đang bị khóa
            }
        }
        else
        {
            if (lockBadgeImage != null)
            {
                lockBadgeImage.gameObject.SetActive(false);
            }
            if (cardImage != null)
            {
                cardImage.color = Color.white;
            }
        }
    }

    private Image CreateLockBadge()
    {
        GameObject lockObj = new GameObject("LockBadge", typeof(RectTransform), typeof(Image));
        lockObj.transform.SetParent(transform, false);

        RectTransform rt = lockObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(4f, -4f);
        rt.sizeDelta = new Vector2(26f, 26f);

        Image img = lockObj.GetComponent<Image>();
        img.sprite = PreserveDiamondStyle.LockIcon;
        img.color = Color.white;
        img.raycastTarget = false;
        return img;
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

        bool show = Count >= Mathf.Max(1, badgeShowFromCount);
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
    /// Badge nhảy lên (punch scale) + loé màu vàng rồi trả về bình thường.
    /// Dùng coroutine thuần để không cần setup Animator trong prefab.
    /// </summary>
    private void PlayBadgePunch()
    {
        if (countText == null)
        {
            return;
        }

        if (badgePunchCoroutine != null)
        {
            StopCoroutine(badgePunchCoroutine);
        }

        badgePunchCoroutine = StartCoroutine(BadgePunchRoutine());
    }

    private IEnumerator BadgePunchRoutine()
    {
        RectTransform badgeRect = countText.rectTransform;
        Vector3 baseScale = Vector3.one;
        Color baseColor = badgeBaseColor;

        float duration = Mathf.Max(0.05f, badgePunchDuration);
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);

            // Phóng to nhanh ở 40% đầu rồi thu về, kèm nhún nhẹ.
            float scaleT = t < 0.4f
                ? Mathf.Lerp(1f, badgePunchScale, Mathf.SmoothStep(0f, 1f, t / 0.4f))
                : Mathf.Lerp(badgePunchScale, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.4f) / 0.6f));

            badgeRect.localScale = baseScale * scaleT;
            countText.color = Color.Lerp(badgeHighlightColor, baseColor, t);

            yield return null;
        }

        badgeRect.localScale = baseScale;
        countText.color = baseColor;
        badgePunchCoroutine = null;
    }

    private Coroutine badgePunchCoroutine;
    private Color badgeBaseColor = Color.white;

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
        badgeRect.anchoredPosition = new Vector2(20f, -12f);
        badgeRect.sizeDelta = new Vector2(44f, 44f);

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

        badgeBaseColor = Color.white;

        badgeObj.SetActive(false);
        return tmp;
    }
}