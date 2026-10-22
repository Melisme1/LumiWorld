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
    [Tooltip("Text hiển thị số lượng card cùng loại. Để trống = tự sinh badge nổi bật ở góc dưới-trái (7h) khi cần.")]
    [SerializeField] private TMP_Text countText;

    [Tooltip("Tự sinh badge số nếu Count Text chưa được gán.")]
    [SerializeField] private bool autoCreateCountBadge = true;

    [SerializeField] private RectTransform badgeRootRect;
    [SerializeField] private Image badgeBackgroundImage;

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

        if (badgeRootRect == null && countText.transform.parent != null && countText.transform.parent != transform)
        {
            badgeRootRect = countText.transform.parent as RectTransform;
            if (badgeBackgroundImage == null && badgeRootRect != null)
            {
                badgeBackgroundImage = badgeRootRect.GetComponent<Image>();
            }
        }

        bool show = Count >= Mathf.Max(1, badgeShowFromCount);
        GameObject targetObj = badgeRootRect != null ? badgeRootRect.gameObject : countText.gameObject;
        if (targetObj.activeSelf != show)
        {
            targetObj.SetActive(show);
        }

        if (show)
        {
            countText.text = Count.ToString();

            if (badgeRootRect != null)
            {
                float width = Count >= 10 ? 48f : 38f;
                badgeRootRect.sizeDelta = new Vector2(width, 38f);
                badgeRootRect.anchoredPosition = new Vector2(7f + width * 0.5f, 26f);
            }
            else
            {
                RectTransform rt = countText.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(7f, 7f);
                countText.alignment = TextAlignmentOptions.BottomLeft;
            }
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
        RectTransform targetRect = badgeRootRect != null ? badgeRootRect : countText.rectTransform;
        Vector3 baseScale = Vector3.one;
        Color baseTextColor = badgeBaseColor;

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

            if (targetRect != null)
            {
                targetRect.localScale = baseScale * scaleT;
            }
            if (countText != null)
            {
                countText.color = Color.Lerp(badgeHighlightColor, baseTextColor, t);
            }

            yield return null;
        }

        if (targetRect != null)
        {
            targetRect.localScale = baseScale;
        }
        if (countText != null)
        {
            countText.color = baseTextColor;
        }
        badgePunchCoroutine = null;
    }

    private Coroutine badgePunchCoroutine;
    private Color badgeBaseColor = Color.white;
    private static Sprite s_BadgeSprite;

    /// <summary>
    /// Tự sinh một badge số nổi bật ở góc dưới-trái thẻ (hướng 7h).
    /// Gồm khung nền viền vàng kim loại sắc nét + nền tối than bóng tương phản cao,
    /// bên trong là chữ số trắng rõ nét, không bị che khuất khi các lá bài xếp chồng lên nhau.
    /// </summary>
    private TMP_Text CreateCountBadge()
    {
        if (!TryGetComponent(out RectTransform cardRect))
        {
            return null;
        }

        GameObject badgeObj = new GameObject("CountBadge", typeof(RectTransform), typeof(Image));
        badgeObj.transform.SetParent(cardRect, false);

        badgeRootRect = badgeObj.GetComponent<RectTransform>();
        badgeRootRect.anchorMin = new Vector2(0f, 0f);
        badgeRootRect.anchorMax = new Vector2(0f, 0f);
        badgeRootRect.pivot = new Vector2(0.5f, 0.5f);
        badgeRootRect.sizeDelta = new Vector2(38f, 38f);
        badgeRootRect.anchoredPosition = new Vector2(26f, 26f); // Cách lề trái 7px, cách lề dưới 7px

        badgeBackgroundImage = badgeObj.GetComponent<Image>();
        badgeBackgroundImage.sprite = GetBadgeSprite();
        badgeBackgroundImage.type = Image.Type.Sliced;
        badgeBackgroundImage.raycastTarget = false;

        GameObject textObj = new GameObject("CountText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(badgeRootRect, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = new Vector2(0f, 0.5f);

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = "1";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 13f;
        tmp.fontSizeMax = 24f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.outlineWidth = 0.22f;
        tmp.outlineColor = new Color(0f, 0f, 0f, 0.95f);

        badgeBaseColor = Color.white;

        badgeObj.SetActive(false);
        return tmp;
    }

    private static Sprite GetBadgeSprite()
    {
        if (s_BadgeSprite != null) return s_BadgeSprite;

        int size = 64;
        float radius = 29.5f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "CardBadgeTexture";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        Color rimColor = new Color(1.0f, 0.85f, 0.35f, 1.0f);     // Vàng kim sáng
        Color rimOuter = new Color(0.04f, 0.04f, 0.06f, 0.92f);    // Viền tối ngoài tương phản
        Color fillTop = new Color(0.22f, 0.22f, 0.28f, 0.96f);     // Mặt trong trên
        Color fillBottom = new Color(0.08f, 0.09f, 0.13f, 0.96f);  // Mặt trong dưới

        int cornerR = 29;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float dx = Mathf.Max(cornerR - px, px - (size - cornerR), 0f);
                float dy = Mathf.Max(cornerR - py, py - (size - cornerR), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                if (alpha <= 0.005f)
                {
                    pixels[y * size + x] = Color.clear;
                    continue;
                }

                Color c;
                if (dist > radius - 2.5f)
                {
                    c = rimOuter;
                }
                else if (dist > radius - 6.5f)
                {
                    float t = Mathf.Clamp01((dist - (radius - 6.5f)) / 4.0f);
                    c = Color.Lerp(rimColor, rimOuter, t * 0.5f);
                }
                else
                {
                    float gradY = (float)y / size;
                    Color inner = Color.Lerp(fillBottom, fillTop, gradY);
                    float edgeBlend = Mathf.Clamp01((radius - 6.5f - dist) / 2.0f);
                    c = Color.Lerp(rimColor, inner, edgeBlend);
                }

                c.a *= alpha;
                pixels[y * size + x] = c;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        Vector4 border = new Vector4(cornerR, cornerR, cornerR, cornerR);
        s_BadgeSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        s_BadgeSprite.name = "CardBadgeSprite";
        return s_BadgeSprite;
    }
}