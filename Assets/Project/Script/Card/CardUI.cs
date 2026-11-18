using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LumiWorld.Acs;

public class CardUI : MonoBehaviour
{
    [Header("Card Data")]
    [SerializeField] private CardData cardData;

    [Header("UI")]
    [SerializeField] private Image cardImage;

    [Header("Count Badge")]
    [Tooltip("Text hiển thị số lượng card cùng loại. Để trống = tự sinh badge P-03 ở góc dưới-phải khi cần.")]
    [SerializeField] private TMP_Text countText;

    [Tooltip("Tự sinh badge số nếu Count Text chưa được gán.")]
    [SerializeField] private bool autoCreateCountBadge = true;

    [SerializeField] private RectTransform badgeRootRect;
    [SerializeField] private Image badgeBackgroundImage;

    [Header("Count Badge Juice")]
    [SerializeField] private int badgeShowFromCount = 1;
    [SerializeField] private float badgePunchScale = 1.35f;
    [SerializeField] private float badgePunchDuration = 0.24f;
    [SerializeField] private Color badgeHighlightColor = new Color(0.33f, 0.55f, 0.24f, 1f);

    [Header("Lock / Escrow Badge")]
    [SerializeField] private Image lockBadgeImage;

    public CardData CardData => cardData;

    public bool IsLocked { get; private set; }
    public CardLockType CurrentLockType { get; private set; }
    public string CurrentLockReason { get; private set; }

    public int Count { get; private set; } = 1;

    private Coroutine badgePunchCoroutine;
    private Color badgeBaseColor = new Color(0.21f, 0.16f, 0.12f, 1f);
    private bool cardChromeApplied;
    private static Sprite s_BadgeSprite;

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

    public void SetCount(int count)
    {
        int clampedCount = Mathf.Max(1, count);
        bool increased = clampedCount > Count;

        Count = clampedCount;
        UpdateCountBadge();

        if (increased && isActiveAndEnabled)
        {
            PlayBadgePunch();
        }
    }

    public void Refresh()
    {
        EnsureCardChrome();

        if (cardData == null)
        {
            return;
        }

        if (cardImage != null)
        {
            cardImage.sprite = cardData.cardImage;
        }

        UpdateCountBadge();
        UpdateLockBadge();
    }

    private void EnsureCardChrome()
    {
        if (cardChromeApplied || cardImage == null)
        {
            return;
        }
        cardChromeApplied = true;

        if (cardImage.GetComponent<Outline>() == null)
        {
            Outline rim = cardImage.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color(0.86f, 0.82f, 0.76f, 0.90f);
            rim.effectDistance = new Vector2(1.5f, -1.5f);
            rim.useGraphicAlpha = true;
        }

        if (cardImage.GetComponent<Shadow>() == null)
        {
            LumiWorldUITheme.AddSoftElevation(cardImage.gameObject, 4.5f, 0.22f);
        }
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
                cardImage.color = new Color(0.72f, 0.72f, 0.72f, 0.88f);
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
        rt.anchoredPosition = new Vector2(6f, -6f);
        rt.sizeDelta = new Vector2(24f, 24f);

        Image img = lockObj.GetComponent<Image>();
        img.sprite = LumiWorldUITheme.Glyph("Lock") ?? PreserveDiamondStyle.LockIcon;
        img.color = LumiWorldUITheme.Text;
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
            // P-03 style: "×N" warm ivory pill at bottom-right of card
            countText.text = "×" + Count;
            countText.font = LumiWorldUITheme.Body ?? countText.font;
            countText.color = LumiWorldUITheme.Text;
            countText.outlineWidth = 0f;

            if (badgeRootRect != null)
            {
                float width = Count >= 10 ? 48f : 40f;
                badgeRootRect.anchorMin = new Vector2(1f, 0f);
                badgeRootRect.anchorMax = new Vector2(1f, 0f);
                badgeRootRect.pivot = new Vector2(1f, 0f);
                badgeRootRect.sizeDelta = new Vector2(width, 28f);
                badgeRootRect.anchoredPosition = new Vector2(-8f, 8f);
                if (badgeBackgroundImage != null)
                {
                    badgeBackgroundImage.sprite = GetBadgeSprite();
                    badgeBackgroundImage.type = Image.Type.Sliced;
                    badgeBackgroundImage.color = Color.white;
                }
            }
        }
    }

    private void PlayBadgePunch()
    {
        if (countText == null || !isActiveAndEnabled)
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
        Color baseTextColor = LumiWorldUITheme.Text;

        float duration = Mathf.Max(0.05f, badgePunchDuration);
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);

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

    private TMP_Text CreateCountBadge()
    {
        if (!TryGetComponent(out RectTransform cardRect))
        {
            return null;
        }

        GameObject badgeObj = new GameObject("CountBadge", typeof(RectTransform), typeof(Image));
        badgeObj.transform.SetParent(cardRect, false);

        badgeRootRect = badgeObj.GetComponent<RectTransform>();
        badgeRootRect.anchorMin = new Vector2(1f, 0f);
        badgeRootRect.anchorMax = new Vector2(1f, 0f);
        badgeRootRect.pivot = new Vector2(1f, 0f);
        badgeRootRect.sizeDelta = new Vector2(40f, 28f);
        badgeRootRect.anchoredPosition = new Vector2(-8f, 8f);

        badgeBackgroundImage = badgeObj.GetComponent<Image>();
        badgeBackgroundImage.sprite = GetBadgeSprite();
        badgeBackgroundImage.type = Image.Type.Sliced;
        badgeBackgroundImage.color = Color.white;
        badgeBackgroundImage.raycastTarget = false;
        LumiWorldUITheme.AddSoftElevation(badgeObj, 2f, 0.20f);

        GameObject textObj = new GameObject("CountText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(badgeRootRect, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.font = LumiWorldUITheme.Body ?? tmp.font;
        tmp.text = "×1";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 12f;
        tmp.fontSizeMax = 16.5f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = LumiWorldUITheme.Text;
        tmp.raycastTarget = false;

        badgeBaseColor = LumiWorldUITheme.Text;
        badgeObj.SetActive(false);
        return tmp;
    }

    private static Sprite GetBadgeSprite()
    {
        if (s_BadgeSprite != null) return s_BadgeSprite;

        int w = 48, h = 32, r = 13;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = "CardCountPillSprite";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[w * h];

        Color fill = LumiWorldUITheme.Slot;
        Color border = LumiWorldUITheme.Edge;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = (x < r) ? r - x : (x > w - 1 - r ? x - (w - 1 - r) : 0f);
                float dy = (y < r) ? r - y : (y > h - 1 - r ? y - (h - 1 - r) : 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(r - dist + 0.5f);
                if (alpha <= 0.005f)
                {
                    px[y * w + x] = Color.clear;
                    continue;
                }

                Color c = (dist >= r - 1.5f) ? border : fill;
                c.a *= alpha;
                px[y * w + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        s_BadgeSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return s_BadgeSprite;
    }
}
