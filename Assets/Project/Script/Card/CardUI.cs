using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LumiWorld.Acs;

public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float FrameInset = 7f;
    private const float RareArtInset = 2f;
    private const float CoverInset = 6f;

    private static readonly Color EpicBackdropTint   = new Color(0.84f, 0.87f, 0.91f, 1f);
    private static readonly Color LegendBackdropTint = new Color(0.90f, 0.80f, 0.56f, 1f);

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
    private bool cardChromeApplied;
    private bool isPointerHovering;
    private RectTransform artworkMaskRect;
    private Image artworkImage;
    private Image artworkCornerCoverImage;
    private Image rarityFrameOverlay;
    private Image terraformRaiseArrow;
    private Image terraformLowerArrow;
    private bool isTerraformActive;
    private int terraformDirection = 1;
    private bool terraformCanRaise = true;
    private bool terraformCanLower = true;

    private static Sprite s_BadgeSprite;
    private static Sprite s_CardFrameSprite;
    private static Sprite s_CardFrameHoverSprite;
    private static Sprite s_ArtworkMaskSprite;
    private static Sprite s_ArtworkCornerCoverSprite;

    private void OnEnable()
    {
        if (CardLockManager.Instance != null)
        {
            CardLockManager.Instance.OnLockStateChanged += Refresh;
        }
    }

    private void OnDisable()
    {
        isPointerHovering = false;
        UpdateFrameHighlight();

        if (CardLockManager.HasInstance)
        {
            CardLockManager.Instance.OnLockStateChanged -= Refresh;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerHovering = true;
        UpdateFrameHighlight();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerHovering = false;
        UpdateFrameHighlight();
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

        if (artworkImage != null)
        {
            artworkImage.sprite = cardData.cardImage;
            CardRarityFrameUtility.ApplyCardOverlays(
                (RectTransform)transform,
                artworkMaskRect != null ? artworkMaskRect : (artworkImage.rectTransform.parent as RectTransform),
                cardData,
                ref rarityFrameOverlay,
                ref terraformRaiseArrow,
                ref terraformLowerArrow);
        }
        else if (cardImage != null)
        {
            cardImage.sprite = cardData.cardImage;
        }

        UpdateCountBadge();
        UpdateLockBadge();
        UpdateTerraformArrowsVisual();
        UpdateFrameHighlight();
        ApplyRarityBackdropChrome();
        EnsureOverlaySiblingOrder();
    }

    private void EnsureCardChrome()
    {
        if (cardChromeApplied || cardImage == null)
        {
            return;
        }
        cardChromeApplied = true;

        // 1. Root Image becomes the crisp rounded white card frame (#FCFBF8) with subtle border
        cardImage.sprite = GetCardFrameSprite();
        cardImage.type = Image.Type.Sliced;
        cardImage.color = Color.white;
        cardImage.preserveAspect = false;

        // Remove legacy 4-pass Outline if present (9-slice frame already draws an anti-aliased hairline rim)
        Outline legacyOutline = cardImage.GetComponent<Outline>();
        if (legacyOutline != null)
        {
            legacyOutline.enabled = false;
        }

        // Two-tier soft card elevation shadow matching mockup cards on Warm Stone tray
        Shadow[] existingShadows = cardImage.GetComponents<Shadow>();
        bool hasNonOutlineShadow = false;
        for (int i = 0; i < existingShadows.Length; i++)
        {
            if (!(existingShadows[i] is Outline))
            {
                hasNonOutlineShadow = true;
                break;
            }
        }
        if (!hasNonOutlineShadow)
        {
            Shadow contactShadow = cardImage.gameObject.AddComponent<Shadow>();
            contactShadow.effectColor = new Color(0.16f, 0.12f, 0.08f, 0.10f);
            contactShadow.effectDistance = new Vector2(0f, -2.5f);
            contactShadow.useGraphicAlpha = true;

            Shadow liftShadow = cardImage.gameObject.AddComponent<Shadow>();
            liftShadow.effectColor = new Color(0.16f, 0.12f, 0.08f, 0.15f);
            liftShadow.effectDistance = new Vector2(0f, -6.0f);
            liftShadow.useGraphicAlpha = true;
        }

        // 2. Rounded stencil mask child for inner card artwork (inset 7px inside the white frame)
        Transform existingMask = transform.Find("ArtworkMask");
        GameObject maskObj = existingMask != null
            ? existingMask.gameObject
            : new GameObject("ArtworkMask", typeof(RectTransform), typeof(Image), typeof(Mask));
        if (existingMask == null)
        {
            maskObj.transform.SetParent(transform, false);
        }

        RectTransform maskRect = (RectTransform)maskObj.transform;
        artworkMaskRect = maskRect;
        maskRect.anchorMin = Vector2.zero;
        maskRect.anchorMax = Vector2.one;
        maskRect.pivot = new Vector2(0.5f, 0.5f);
        maskRect.offsetMin = new Vector2(FrameInset, FrameInset);
        maskRect.offsetMax = new Vector2(-FrameInset, -FrameInset);
        maskRect.SetSiblingIndex(0);

        Image maskImg = maskObj.GetComponent<Image>();
        maskImg.sprite = GetArtworkMaskSprite();
        maskImg.type = Image.Type.Sliced;
        maskImg.color = Color.white;
        maskImg.raycastTarget = false;

        Mask stencilMask = maskObj.GetComponent<Mask>();
        stencilMask.showMaskGraphic = false;

        Transform existingArt = maskRect.Find("ArtworkImage");
        GameObject artObj = existingArt != null
            ? existingArt.gameObject
            : new GameObject("ArtworkImage", typeof(RectTransform), typeof(Image));
        if (existingArt == null)
        {
            artObj.transform.SetParent(maskRect, false);
        }

        RectTransform artRect = (RectTransform)artObj.transform;
        artRect.anchorMin = Vector2.zero;
        artRect.anchorMax = Vector2.one;
        artRect.pivot = new Vector2(0.5f, 0.5f);
        artRect.offsetMin = Vector2.zero;
        artRect.offsetMax = Vector2.zero;

        artworkImage = artObj.GetComponent<Image>();
        artworkImage.color = Color.white;
        artworkImage.preserveAspect = false;
        artworkImage.raycastTarget = false;

        Transform legacyOverlay = maskRect.Find("RarityFrameOverlay");
        if (legacyOverlay != null)
        {
            legacyOverlay.SetParent(transform, false);
        }

        CardRarityFrameUtility.EnsureOverlayChildren(
            (RectTransform)transform,
            maskRect,
            ref rarityFrameOverlay,
            ref terraformRaiseArrow,
            ref terraformLowerArrow);

        // 3. Anti-aliased inner corner collar to smooth over the 1-bit stencil mask boundary
        Transform existingCover = transform.Find("ArtworkCornerCover");
        GameObject coverObj = existingCover != null
            ? existingCover.gameObject
            : new GameObject("ArtworkCornerCover", typeof(RectTransform), typeof(Image));
        if (existingCover == null)
        {
            coverObj.transform.SetParent(transform, false);
        }

        RectTransform coverRect = (RectTransform)coverObj.transform;
        coverRect.anchorMin = Vector2.zero;
        coverRect.anchorMax = Vector2.one;
        coverRect.pivot = new Vector2(0.5f, 0.5f);
        coverRect.offsetMin = new Vector2(CoverInset, CoverInset);
        coverRect.offsetMax = new Vector2(-CoverInset, -CoverInset);
        coverRect.SetSiblingIndex(1);

        artworkCornerCoverImage = coverObj.GetComponent<Image>();
        artworkCornerCoverImage.sprite = GetArtworkCornerCoverSprite();
        artworkCornerCoverImage.type = Image.Type.Sliced;
        artworkCornerCoverImage.color = Color.white;
        artworkCornerCoverImage.raycastTarget = false;

        EnsureOverlaySiblingOrder();
    }

    private void EnsureOverlaySiblingOrder()
    {
        if (artworkMaskRect != null && artworkMaskRect.parent == transform)
        {
            artworkMaskRect.SetSiblingIndex(0);
        }
        if (artworkCornerCoverImage != null && artworkCornerCoverImage.transform.parent == transform)
        {
            artworkCornerCoverImage.transform.SetSiblingIndex(1);
        }
        if (rarityFrameOverlay != null && rarityFrameOverlay.transform.parent == transform)
        {
            rarityFrameOverlay.transform.SetAsLastSibling();
        }
        if (badgeRootRect != null)
        {
            badgeRootRect.SetAsLastSibling();
        }
        if (lockBadgeImage != null)
        {
            lockBadgeImage.transform.SetAsLastSibling();
        }
    }

    private void ApplyRarityBackdropChrome()
    {
        bool isRare = cardData != null && cardData.rarity != CardRarity.Common;
        if (artworkMaskRect != null)
        {
            float inset = isRare ? RareArtInset : FrameInset;
            artworkMaskRect.offsetMin = new Vector2(inset, inset);
            artworkMaskRect.offsetMax = new Vector2(-inset, -inset);
        }

        if (artworkCornerCoverImage != null)
        {
            artworkCornerCoverImage.enabled = !isRare;
        }

        if (cardImage != null)
        {
            if (isRare)
            {
                Color baseTint = cardData.rarity == CardRarity.Legend ? LegendBackdropTint : EpicBackdropTint;
                cardImage.color = IsLocked
                    ? new Color(baseTint.r * 0.85f, baseTint.g * 0.85f, baseTint.b * 0.85f, 0.95f)
                    : baseTint;
            }
            else
            {
                cardImage.color = IsLocked ? new Color(0.95f, 0.94f, 0.92f, 0.95f) : Color.white;
            }
        }
    }

    private void UpdateFrameHighlight()
    {
        if (!cardChromeApplied || cardImage == null) return;
        bool highlight = (isPointerHovering || isTerraformActive) && !IsLocked;
        cardImage.sprite = highlight ? GetCardFrameHoverSprite() : GetCardFrameSprite();

        if (rarityFrameOverlay != null)
        {
            bool isRare = cardData != null && cardData.rarity != CardRarity.Common;
            if (!isRare)
            {
                rarityFrameOverlay.color = Color.white;
            }
            else if (IsLocked)
            {
                rarityFrameOverlay.color = new Color(0.80f, 0.80f, 0.80f, 0.92f);
            }
            else
            {
                rarityFrameOverlay.color = highlight ? new Color(1.08f, 1.08f, 1.08f, 1f) : Color.white;
            }
        }
    }

    public void SetTerraformVisualState(bool active, int direction, bool canRaise, bool canLower)
    {
        isTerraformActive = active;
        terraformDirection = direction >= 0 ? 1 : -1;
        terraformCanRaise = canRaise;
        terraformCanLower = canLower;
        UpdateFrameHighlight();
        UpdateTerraformArrowsVisual();
    }

    public int GetTerraformClickZone(PointerEventData eventData)
    {
        RectTransform rt = transform as RectTransform;
        if (rt == null || eventData == null) return 0;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, eventData.pressEventCamera, out Vector2 local))
        {
            return 0;
        }
        float halfW = rt.rect.width * 0.5f;
        if (halfW <= 1f) return 0;
        float normX = local.x / halfW;
        if (normX <= -0.22f) return 1;
        if (normX >= 0.22f) return -1;
        return 0;
    }

    private void UpdateTerraformArrowsVisual()
    {
        bool show = cardData != null && cardData.IsTerraformTool();
        if (terraformRaiseArrow != null)
        {
            terraformRaiseArrow.enabled = show && terraformRaiseArrow.sprite != null;
            if (terraformRaiseArrow.enabled)
            {
                if (!isTerraformActive)
                {
                    terraformRaiseArrow.color = !terraformCanRaise
                        ? new Color(0.55f, 0.55f, 0.55f, 0.28f)
                        : new Color(1f, 1f, 1f, 0.96f);
                    terraformRaiseArrow.rectTransform.localScale = Vector3.one;
                }
                else if (!terraformCanRaise)
                {
                    terraformRaiseArrow.color = new Color(0.55f, 0.55f, 0.55f, 0.24f);
                    terraformRaiseArrow.rectTransform.localScale = Vector3.one * 0.88f;
                }
                else if (terraformDirection > 0)
                {
                    terraformRaiseArrow.color = Color.white;
                    terraformRaiseArrow.rectTransform.localScale = Vector3.one * 1.14f;
                }
                else
                {
                    terraformRaiseArrow.color = new Color(0.85f, 0.85f, 0.85f, 0.48f);
                    terraformRaiseArrow.rectTransform.localScale = Vector3.one * 0.94f;
                }
            }
        }

        if (terraformLowerArrow != null)
        {
            terraformLowerArrow.enabled = show && terraformLowerArrow.sprite != null;
            if (terraformLowerArrow.enabled)
            {
                if (!isTerraformActive)
                {
                    terraformLowerArrow.color = !terraformCanLower
                        ? new Color(0.55f, 0.55f, 0.55f, 0.28f)
                        : new Color(1f, 1f, 1f, 0.96f);
                    terraformLowerArrow.rectTransform.localScale = Vector3.one;
                }
                else if (!terraformCanLower)
                {
                    terraformLowerArrow.color = new Color(0.55f, 0.55f, 0.55f, 0.24f);
                    terraformLowerArrow.rectTransform.localScale = Vector3.one * 0.88f;
                }
                else if (terraformDirection < 0)
                {
                    terraformLowerArrow.color = Color.white;
                    terraformLowerArrow.rectTransform.localScale = Vector3.one * 1.14f;
                }
                else
                {
                    terraformLowerArrow.color = new Color(0.85f, 0.85f, 0.85f, 0.48f);
                    terraformLowerArrow.rectTransform.localScale = Vector3.one * 0.94f;
                }
            }
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
                lockBadgeImage.transform.SetAsLastSibling();
            }
            if (artworkImage != null)
            {
                artworkImage.color = new Color(0.70f, 0.70f, 0.70f, 0.90f);
            }
            Color mutedFrame = new Color(0.95f, 0.94f, 0.92f, 0.95f);
            if (cardImage != null) cardImage.color = mutedFrame;
            if (artworkCornerCoverImage != null) artworkCornerCoverImage.color = mutedFrame;
        }
        else
        {
            if (lockBadgeImage != null)
            {
                lockBadgeImage.gameObject.SetActive(false);
            }
            if (artworkImage != null)
            {
                artworkImage.color = Color.white;
            }
            if (cardImage != null) cardImage.color = Color.white;
            if (artworkCornerCoverImage != null) artworkCornerCoverImage.color = Color.white;
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
        rt.anchoredPosition = new Vector2(11f, -11f);
        rt.sizeDelta = new Vector2(24f, 24f);

        Image img = lockObj.GetComponent<Image>();
        img.sprite = LumiWorldUITheme.Glyph("Lock") ?? PreserveDiamondStyle.LockIcon;
        img.color = LumiWorldUITheme.Text;
        img.raycastTarget = false;
        lockObj.transform.SetAsLastSibling();
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

        bool show = !(cardData != null && cardData.reusable) && Count >= Mathf.Max(1, badgeShowFromCount);
        GameObject targetObj = badgeRootRect != null ? badgeRootRect.gameObject : countText.gameObject;
        if (targetObj.activeSelf != show)
        {
            targetObj.SetActive(show);
        }

        if (show)
        {
            // P-03 style: "×N" crisp ivory pill nestled neatly inside bottom-right of rounded artwork
            countText.text = "×" + Count;
            countText.font = LumiWorldUITheme.Body ?? countText.font;
            countText.color = LumiWorldUITheme.Text;
            countText.outlineWidth = 0f;

            if (badgeRootRect != null)
            {
                float width = Count >= 10 ? 46f : 38f;
                badgeRootRect.anchorMin = new Vector2(1f, 0f);
                badgeRootRect.anchorMax = new Vector2(1f, 0f);
                badgeRootRect.pivot = new Vector2(1f, 0f);
                badgeRootRect.sizeDelta = new Vector2(width, 26f);
                badgeRootRect.anchoredPosition = new Vector2(-11f, 11f);
                if (badgeBackgroundImage != null)
                {
                    badgeBackgroundImage.sprite = GetBadgeSprite();
                    badgeBackgroundImage.type = Image.Type.Sliced;
                    badgeBackgroundImage.color = Color.white;
                }
            }

            EnsureOverlaySiblingOrder();
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
        badgeRootRect.sizeDelta = new Vector2(38f, 26f);
        badgeRootRect.anchoredPosition = new Vector2(-11f, 11f);

        badgeBackgroundImage = badgeObj.GetComponent<Image>();
        badgeBackgroundImage.sprite = GetBadgeSprite();
        badgeBackgroundImage.type = Image.Type.Sliced;
        badgeBackgroundImage.color = Color.white;
        badgeBackgroundImage.raycastTarget = false;
        LumiWorldUITheme.AddSoftElevation(badgeObj, 2f, 0.18f);

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

        badgeObj.SetActive(false);
        return tmp;
    }

    private static Sprite GetCardFrameSprite()
    {
        if (s_CardFrameSprite != null) return s_CardFrameSprite;
        s_CardFrameSprite = BuildRoundedRectSprite(
            "CardWhiteFrameSprite",
            48,
            48,
            18f,
            18,
            LumiWorldUITheme.Slot,                      // Crisp warm white #FCFBF8
            new Color(0.855f, 0.827f, 0.788f, 1f),      // Subtle warm stone rim #DAD3C9
            1.6f);
        return s_CardFrameSprite;
    }

    private static Sprite GetCardFrameHoverSprite()
    {
        if (s_CardFrameHoverSprite != null) return s_CardFrameHoverSprite;
        s_CardFrameHoverSprite = BuildRoundedRectSprite(
            "CardWhiteFrameHoverSprite",
            48,
            48,
            18f,
            18,
            LumiWorldUITheme.Slot,                      // Crisp warm white #FCFBF8
            new Color(0.42f, 0.37f, 0.32f, 0.92f),      // Crisp warm-espresso hover accent
            2.2f);
        return s_CardFrameHoverSprite;
    }

    private static Sprite GetArtworkMaskSprite()
    {
        if (s_ArtworkMaskSprite != null) return s_ArtworkMaskSprite;

        const int w = 36;
        const int h = 36;
        const float r = 11.4f;
        const int border = 12;

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = "CardArtworkMaskSprite";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[w * h];

        float halfW = w * 0.5f;
        float halfH = h * 0.5f;
        float bx = Mathf.Max(0f, halfW - r);
        float by = Mathf.Max(0f, halfH - r);

        for (int y = 0; y < h; y++)
        {
            float py = y + 0.5f;
            float dy = Mathf.Abs(py - halfH) - by;
            for (int x = 0; x < w; x++)
            {
                float pxCoord = x + 0.5f;
                float dx = Mathf.Abs(pxCoord - halfW) - bx;
                float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
                float sdf = outside + inside - r;

                // Extend slightly under ArtworkCornerCover so stencil mask never leaves a gap
                px[y * w + x] = sdf <= 0.35f ? Color.white : Color.clear;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        s_ArtworkMaskSprite = Sprite.Create(
            tex,
            new Rect(0, 0, w, h),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));
        return s_ArtworkMaskSprite;
    }

    private static Sprite GetArtworkCornerCoverSprite()
    {
        if (s_ArtworkCornerCoverSprite != null) return s_ArtworkCornerCoverSprite;

        const int w = 40;
        const int h = 40;
        const float pad = 1.2f;
        const float rInner = 11f;
        const int border = 14;

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = "CardArtworkCornerCoverSprite";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[w * h];

        Color frameFill = LumiWorldUITheme.Slot;                     // Matches #FCFBF8 frame seamlessly
        Color innerSeam = new Color(0.918f, 0.898f, 0.867f, 1f);     // Whisper-subtle recessed artwork edge

        float halfW = w * 0.5f;
        float halfH = h * 0.5f;
        float bx = Mathf.Max(0f, halfW - pad - rInner);
        float by = Mathf.Max(0f, halfH - pad - rInner);

        for (int y = 0; y < h; y++)
        {
            float py = y + 0.5f;
            float dy = Mathf.Abs(py - halfH) - by;
            for (int x = 0; x < w; x++)
            {
                float pxCoord = x + 0.5f;
                float dx = Mathf.Abs(pxCoord - halfW) - bx;
                float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
                float sdf = outside + inside - rInner;

                // Negative inside transparent artwork window; positive in the rounded collar
                float coverAlpha = Mathf.Clamp01(sdf + 0.5f);
                if (coverAlpha <= 0.005f)
                {
                    px[y * w + x] = Color.clear;
                    continue;
                }

                // Subtle inner rim transitioning smoothly into solid white frame fill
                float rimT = Mathf.Clamp01((sdf - 0.1f) / 1.1f);
                Color c = Color.Lerp(innerSeam, frameFill, rimT);
                c.a = coverAlpha;
                px[y * w + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        s_ArtworkCornerCoverSprite = Sprite.Create(
            tex,
            new Rect(0, 0, w, h),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));
        return s_ArtworkCornerCoverSprite;
    }

    private static Sprite GetBadgeSprite()
    {
        if (s_BadgeSprite != null) return s_BadgeSprite;
        s_BadgeSprite = BuildRoundedRectSprite(
            "CardCountPillSprite",
            40,
            28,
            13f,
            13,
            LumiWorldUITheme.Slot,
            new Color(0.86f, 0.83f, 0.79f, 1f),
            1.2f);
        return s_BadgeSprite;
    }

    private static Sprite BuildRoundedRectSprite(
        string name,
        int w,
        int h,
        float radius,
        int sliceBorder,
        Color fill,
        Color border,
        float borderThickness)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = name;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[w * h];

        float halfW = w * 0.5f;
        float halfH = h * 0.5f;
        float bx = Mathf.Max(0f, halfW - radius);
        float by = Mathf.Max(0f, halfH - radius);

        for (int y = 0; y < h; y++)
        {
            float py = y + 0.5f;
            float dy = Mathf.Abs(py - halfH) - by;
            for (int x = 0; x < w; x++)
            {
                float pxCoord = x + 0.5f;
                float dx = Mathf.Abs(pxCoord - halfW) - bx;
                float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
                float sdf = outside + inside - radius;

                float alpha = Mathf.Clamp01(0.5f - sdf);
                if (alpha <= 0.005f)
                {
                    px[y * w + x] = Color.clear;
                    continue;
                }

                float edgeT = Mathf.Clamp01(sdf + borderThickness + 0.5f);
                Color c = Color.Lerp(fill, border, edgeT);
                c.a *= alpha;
                px[y * w + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(
            tex,
            new Rect(0, 0, w, h),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(sliceBorder, sliceBorder, sliceBorder, sliceBorder));
    }
}
