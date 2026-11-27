using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LumiWorld.Acs;

public class CardDeskController : MonoBehaviour
{
    public static CardDeskController Instance;

    public enum TrayCategory
    {
        Nature = 0,
        Creature = 1,
        Building = 2,
        Special = 3
    }

    private const string SavedCategoryPrefKey = "LumiWorld_CardTray_Category";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        int savedCat = PlayerPrefs.GetInt(SavedCategoryPrefKey, (int)TrayCategory.Nature);
        activeCategory = Enum.IsDefined(typeof(TrayCategory), savedCat)
            ? (TrayCategory)savedCat
            : TrayCategory.Nature;
    }

    [Header("References")]
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private Transform cardContainer;

    [Header("Cards")]
    [SerializeField] private List<CardData> cardsToSpawn = new();

    [Header("Layout")]
    [Tooltip("Khoảng cách tối đa giữa 2 tâm lá.")]
    [SerializeField] private float maxSpacing = 165f;
    [Tooltip("Khoảng cách tối thiểu. PHẢI >= chiều rộng lá để các lá không chồng lên nhau.")]
    [SerializeField] private float minSpacing = 150f;
    [SerializeField] private float handWidth = 900f;

    [Tooltip("Bề rộng 1 lá (dùng làm khoảng cách tối thiểu tuyệt đối để chặn chồng). Khớp với SizeDelta.x của card prefab.")]
    [SerializeField] private float cardWidth = 150f;

    [Header("Appear Animation")]
    [SerializeField] private float appearDelay = 0.15f;

    [Header("Rearrange Animation")]
    [SerializeField] private float rearrangeDuration = 0.25f;

    [Header("Reward Feedback (game feel khi nhận card)")]
    [SerializeField] private bool showRewardToast = false;
    [SerializeField] private bool playRewardJuice = true;
    [SerializeField] private Vector2 toastOffset = new Vector2(0f, 190f);
    [SerializeField] private bool showStartingHandSummary = false;

    private readonly List<GameObject> cards = new();
    private readonly Dictionary<string, GameObject> slotByCardID = new();
    private readonly Dictionary<string, int> handCounts = new();
    private readonly List<HandCardStack> unresolvedHand = new();

    private bool handLoaded;
    private readonly List<CardData> earlyRewards = new();
    private bool suppressRewardFeedback;

    // P-03 Card Tray UI (Monolithic Warm Stone silhouette with concave shoulders & integrated chevron dome)
    private const float TrayBottomMargin = 8f;
    private const float TrayShelfHeight = 132f;
    private const float TrayShelfMinWidth = 592f;
    private const float TrayShelfMaxWidth = 728f;
    private const float TabDockWidth = 436f;
    private const float TabDockHeight = 42f;
    private const float ChevronDomeHalfWidth = 24f;
    private const float ChevronDomeHeight = 19f;
    private const float TrayExpandedTotalHeight = TrayShelfHeight + TabDockHeight + ChevronDomeHeight;

    private TrayCategory activeCategory = TrayCategory.Nature;
    private bool isTrayExpanded = true;
    private float currentShelfWidth = TrayShelfMinWidth;
    private int lastVisibleCardCount;
    private RectTransform deskRectTransform;
    private RectTransform trayMonolithRect;
    private CardTrayShapeGraphic trayShapeGraphic;
    private RectTransform trayTabsRowRect;
    private RectTransform chevronBtnRect;
    private Image chevronGlyphImg;
    private TextMeshProUGUI emptyCategoryLabel;
    private int lastFittedScreenW;
    private int lastFittedScreenH;
    private float lastFittedCanvasScale;

    private static Sprite s_ActiveTabPillSprite;
    private static Sprite s_InactiveTabPillSprite;

    private class CategoryTabView
    {
        public TrayCategory category;
        public Image bg;
        public Shadow shadow;
        public Image icon;
        public TextMeshProUGUI label;
    }

    private readonly List<CategoryTabView> categoryTabs = new();

    private void Start()
    {
        BuildCardTrayUI();
        StartCoroutine(SpawnCards());
    }

    private void LateUpdate()
    {
        SyncTrayResponsiveScale();
    }

    // =========================================================
    // P-03 CARD TRAY & CATEGORY FILTERING
    // =========================================================

    public static TrayCategory ClassifyCard(CardData data)
    {
        if (data == null) return TrayCategory.Nature;
        if (data.IsRiverCard() || data.IsTileTransformCard() || data.cardType == CardType.Special ||
            string.Equals(data.cardID, "Rain", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(data.cardID, "River", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(data.cardName, "Rain", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(data.cardName, "River", StringComparison.OrdinalIgnoreCase))
        {
            return TrayCategory.Special;
        }
        if (data.cardType == CardType.Creature || data.animalSpeciesData != null)
        {
            return TrayCategory.Creature;
        }
        if (data.cardType == CardType.Building)
        {
            return TrayCategory.Building;
        }
        return TrayCategory.Nature;
    }

    private void BuildCardTrayUI()
    {
        if (WorldLaunch.IsVisit || cardContainer == null || trayMonolithRect != null) return;

        RectTransform containerRect = cardContainer as RectTransform;
        Transform parentTransform = cardContainer.parent != null ? cardContainer.parent : transform;
        deskRectTransform = parentTransform as RectTransform;

        // Normalize root CardDesk rect so stale scene offsets/raycasts never shift or block cards
        if (deskRectTransform != null)
        {
            deskRectTransform.anchorMin = new Vector2(0.5f, 0f);
            deskRectTransform.anchorMax = new Vector2(0.5f, 0f);
            deskRectTransform.pivot = new Vector2(0.5f, 0f);
            deskRectTransform.anchoredPosition = Vector2.zero;
            deskRectTransform.sizeDelta = new Vector2(TrayShelfMaxWidth + 40f, TrayExpandedTotalHeight + 24f);

            Image deskImg = deskRectTransform.GetComponent<Image>();
            if (deskImg != null)
            {
                deskImg.color = Color.clear;
                deskImg.raycastTarget = false;
                deskImg.enabled = false;
            }
        }

        SyncTrayResponsiveScale(force: true);

        // Normalize cardContainer dead-center inside the shelf so targetPosition (x, 0) centers every card cleanly
        if (containerRect != null)
        {
            containerRect.anchorMin = new Vector2(0.5f, 0f);
            containerRect.anchorMax = new Vector2(0.5f, 0f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = new Vector2(0f, TrayBottomMargin + TrayShelfHeight * 0.5f);
            containerRect.sizeDelta = new Vector2(TrayShelfMaxWidth, TrayShelfHeight);
        }

        int insertIndex = cardContainer.GetSiblingIndex();

        // 1. Single Monolithic Warm Stone Tray (Shelf + Concave Shoulders + Raised Tab Dock + Integrated Chevron Dome)
        GameObject monolithObj = new GameObject("P03_CardTrayMonolith", typeof(RectTransform), typeof(CardTrayShapeGraphic));
        trayMonolithRect = (RectTransform)monolithObj.transform;
        trayMonolithRect.SetParent(parentTransform, false);
        trayMonolithRect.anchorMin = new Vector2(0.5f, 0f);
        trayMonolithRect.anchorMax = new Vector2(0.5f, 0f);
        trayMonolithRect.pivot = new Vector2(0.5f, 0f);
        trayMonolithRect.anchoredPosition = new Vector2(0f, TrayBottomMargin);
        trayMonolithRect.sizeDelta = new Vector2(currentShelfWidth, TrayExpandedTotalHeight);
        trayMonolithRect.SetSiblingIndex(insertIndex++);

        trayShapeGraphic = monolithObj.GetComponent<CardTrayShapeGraphic>();
        trayShapeGraphic.ConfigureGeometry(
            newShelfWidth: currentShelfWidth,
            newShelfHeight: TrayShelfHeight,
            newDockWidth: TabDockWidth,
            newDockHeight: TabDockHeight,
            newShelfCornerRadius: 26f,
            newShoulderFilletRadius: 19f,
            newDockCornerRadius: 23f,
            includeChevronDome: true,
            newDomeHalfWidth: ChevronDomeHalfWidth,
            newDomeHeight: ChevronDomeHeight,
            newDomeBaseFillet: 9f,
            newDomeTopRadius: 10f);
        trayShapeGraphic.ConfigureStyle(
            fill: new Color(0.937f, 0.918f, 0.886f, 1f),          // Warm Stone #EFEAE2
            border: new Color(0.835f, 0.808f, 0.765f, 1f),        // Crisp hairline #D5CEC3
            shadow: new Color(0.145f, 0.106f, 0.071f, 0.17f));    // Soft warm drop shadow
        trayShapeGraphic.raycastTarget = true;

        // Empty category hint inside tray shelf
        GameObject emptyObj = new GameObject("EmptyCategoryNotice", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform emptyRect = (RectTransform)emptyObj.transform;
        emptyRect.SetParent(trayMonolithRect, false);
        emptyRect.anchorMin = new Vector2(0.5f, 0f);
        emptyRect.anchorMax = new Vector2(0.5f, 0f);
        emptyRect.pivot = new Vector2(0.5f, 0.5f);
        emptyRect.sizeDelta = new Vector2(420f, 36f);
        emptyRect.anchoredPosition = new Vector2(0f, TrayShelfHeight * 0.5f);

        emptyCategoryLabel = emptyObj.GetComponent<TextMeshProUGUI>();
        emptyCategoryLabel.font = LumiWorldUITheme.Body;
        emptyCategoryLabel.fontSize = 13f;
        emptyCategoryLabel.fontStyle = FontStyles.Bold;
        emptyCategoryLabel.color = LumiWorldUITheme.Muted;
        emptyCategoryLabel.alignment = TextAlignmentOptions.Center;
        emptyCategoryLabel.raycastTarget = false;
        emptyCategoryLabel.text = "No cards in this category";
        emptyObj.SetActive(false);

        // 2. Centered Category Tabs nestled concentrically inside the raised Tab Dock
        GameObject tabsRowObj = new GameObject("CategoryTabsRow", typeof(RectTransform));
        trayTabsRowRect = (RectTransform)tabsRowObj.transform;
        trayTabsRowRect.SetParent(trayMonolithRect, false);
        trayTabsRowRect.anchorMin = new Vector2(0.5f, 0f);
        trayTabsRowRect.anchorMax = new Vector2(0.5f, 0f);
        trayTabsRowRect.pivot = new Vector2(0.5f, 0.5f);
        // Concentric with dock top corner radius (23f): tab top sits 8px below dock top
        trayTabsRowRect.anchoredPosition = new Vector2(0f, TrayShelfHeight + TabDockHeight - 23f);
        trayTabsRowRect.sizeDelta = new Vector2(TabDockWidth - 18f, 30f);

        (TrayCategory cat, string label, string glyphCode)[] tabDefs =
        {
            (TrayCategory.Nature, "Nature", "G-11"),
            (TrayCategory.Creature, "Creature", "G-12"),
            (TrayCategory.Building, "Building", "G-13"),
            (TrayCategory.Special, "Special", "G-14")
        };

        categoryTabs.Clear();
        const float tabW = 100f;
        const float tabH = 30f;
        const float tabGap = 6f;
        float totalTabsW = tabDefs.Length * tabW + (tabDefs.Length - 1) * tabGap;
        float startX = -totalTabsW * 0.5f + tabW * 0.5f;

        for (int i = 0; i < tabDefs.Length; i++)
        {
            var def = tabDefs[i];
            float x = startX + i * (tabW + tabGap);

            GameObject tabObj = new GameObject("Tab_" + def.label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup));
            RectTransform tabRect = (RectTransform)tabObj.transform;
            tabRect.SetParent(trayTabsRowRect, false);
            tabRect.anchorMin = tabRect.anchorMax = tabRect.pivot = new Vector2(0.5f, 0.5f);
            tabRect.anchoredPosition = new Vector2(x, 0f);
            tabRect.sizeDelta = new Vector2(tabW, tabH);

            Image tabBg = tabObj.GetComponent<Image>();
            tabBg.sprite = GetTabPillSprite(false);
            tabBg.type = Image.Type.Sliced;
            tabBg.color = Color.white;
            tabBg.raycastTarget = true;
            LumiWorldUITheme.AddSoftElevation(tabObj, 2.5f, 0.15f);
            Shadow tabShadow = tabObj.GetComponent<Shadow>();

            HorizontalLayoutGroup hlg = tabObj.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 5f;
            hlg.padding = new RectOffset(8, 8, 0, 0);
            hlg.childControlWidth = true;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            GameObject icObj = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            RectTransform icRect = (RectTransform)icObj.transform;
            icRect.SetParent(tabRect, false);
            icRect.sizeDelta = new Vector2(13f, 13f);

            LayoutElement icLe = icObj.GetComponent<LayoutElement>();
            icLe.minWidth = 13f;
            icLe.preferredWidth = 13f;
            icLe.flexibleWidth = 0f;

            Image icImg = icObj.GetComponent<Image>();
            icImg.sprite = LumiWorldUITheme.Glyph(def.glyphCode);
            icImg.preserveAspect = true;
            icImg.raycastTarget = false;

            GameObject lblObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            RectTransform lblRect = (RectTransform)lblObj.transform;
            lblRect.SetParent(tabRect, false);
            lblRect.sizeDelta = new Vector2(0f, 24f);

            LayoutElement lblLe = lblObj.GetComponent<LayoutElement>();
            lblLe.minWidth = 20f;
            lblLe.flexibleWidth = 0f;

            TextMeshProUGUI lblTmp = lblObj.GetComponent<TextMeshProUGUI>();
            lblTmp.font = LumiWorldUITheme.Body;
            lblTmp.text = def.label;
            lblTmp.fontSize = 10.5f;
            lblTmp.enableAutoSizing = true;
            lblTmp.fontSizeMin = 8.5f;
            lblTmp.fontSizeMax = 10.5f;
            lblTmp.fontStyle = FontStyles.Bold;
            lblTmp.alignment = TextAlignmentOptions.MidlineLeft;
            lblTmp.overflowMode = TextOverflowModes.Ellipsis;
            lblTmp.textWrappingMode = TextWrappingModes.NoWrap;
            lblTmp.raycastTarget = false;

            Button btn = tabObj.GetComponent<Button>();
            btn.targetGraphic = tabBg;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            TrayCategory capturedCat = def.cat;
            btn.onClick.AddListener(() => SelectCategory(capturedCat));

            categoryTabs.Add(new CategoryTabView
            {
                category = def.cat,
                bg = tabBg,
                shadow = tabShadow,
                icon = icImg,
                label = lblTmp
            });
        }

        // 3. Integrated Show/Hide Chevron hit button seated directly on the sculpted dome (or collapsed pill)
        GameObject chevObj = new GameObject("P03_TrayChevronButton", typeof(RectTransform), typeof(Image), typeof(Button));
        chevronBtnRect = (RectTransform)chevObj.transform;
        chevronBtnRect.SetParent(parentTransform, false);
        chevronBtnRect.anchorMin = new Vector2(0.5f, 0f);
        chevronBtnRect.anchorMax = new Vector2(0.5f, 0f);
        chevronBtnRect.pivot = new Vector2(0.5f, 0f);
        chevronBtnRect.sizeDelta = new Vector2(66f, 26f);
        chevronBtnRect.SetSiblingIndex(insertIndex++);

        Image chevHit = chevObj.GetComponent<Image>();
        chevHit.color = Color.clear;
        chevHit.raycastTarget = true;

        GameObject chevIcObj = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
        RectTransform chevIcRect = (RectTransform)chevIcObj.transform;
        chevIcRect.SetParent(chevronBtnRect, false);
        chevIcRect.anchorMin = chevIcRect.anchorMax = chevIcRect.pivot = new Vector2(0.5f, 0.5f);
        chevIcRect.sizeDelta = new Vector2(13f, 13f);
        chevIcRect.anchoredPosition = new Vector2(0f, 1.5f);

        chevronGlyphImg = chevIcObj.GetComponent<Image>();
        chevronGlyphImg.preserveAspect = true;
        chevronGlyphImg.color = Color.white;
        chevronGlyphImg.raycastTarget = false;

        Button chevBtn = chevObj.GetComponent<Button>();
        chevBtn.targetGraphic = chevronGlyphImg;
        chevBtn.transition = Selectable.Transition.ColorTint;
        ColorBlock chevColors = chevBtn.colors;
        chevColors.normalColor = new Color(0.35f, 0.30f, 0.25f, 1f);
        chevColors.highlightedColor = LumiWorldUITheme.Text;
        chevColors.pressedColor = new Color(0.15f, 0.11f, 0.08f, 1f);
        chevColors.selectedColor = chevColors.normalColor;
        chevColors.colorMultiplier = 1f;
        chevColors.fadeDuration = 0.08f;
        chevBtn.colors = chevColors;
        chevBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        chevBtn.onClick.AddListener(ToggleTrayExpanded);

        // Keep cardContainer as the highest sibling so hovered/dragged cards float cleanly over tabs & dome
        cardContainer.SetSiblingIndex(insertIndex);

        UpdateCategoryTabVisuals();
        UpdateTrayExpandedVisuals();
    }

    private void SyncTrayResponsiveScale(bool force = false)
    {
        if (deskRectTransform == null) return;
        Canvas rootCanvas = deskRectTransform.GetComponentInParent<Canvas>()?.rootCanvas;
        if (rootCanvas == null || rootCanvas.scaleFactor <= 0.01f) return;

        if (!force && lastFittedScreenW == Screen.width && lastFittedScreenH == Screen.height &&
            Mathf.Approximately(lastFittedCanvasScale, rootCanvas.scaleFactor))
        {
            return;
        }

        lastFittedScreenW = Screen.width;
        lastFittedScreenH = Screen.height;
        lastFittedCanvasScale = rootCanvas.scaleFactor;

        Vector2 pixels = ((RectTransform)rootCanvas.transform).rect.size * rootCanvas.scaleFactor;
        float fit = ResourceInventoryUILayout.FitScale(pixels.x, pixels.y);
        if (fit <= 0.01f) return;

        deskRectTransform.localScale = Vector3.one * (fit / rootCanvas.scaleFactor);
    }

    private void UpdateTrayShelfWidth(int visibleCount, float cardScale, float spacing)
    {
        float targetW = TrayShelfMinWidth;
        if (visibleCount > 0)
        {
            float effW = Mathf.Max(120f, cardWidth) * cardScale;
            float needed = (visibleCount - 1) * spacing + effW + 40f;
            targetW = Mathf.Clamp(Mathf.Max(TrayShelfMinWidth, needed), TrayShelfMinWidth, TrayShelfMaxWidth);
        }

        currentShelfWidth = targetW;

        if (trayShapeGraphic != null)
        {
            trayShapeGraphic.ShelfWidth = targetW;
        }

        if (trayMonolithRect != null && isTrayExpanded)
        {
            trayMonolithRect.sizeDelta = new Vector2(targetW, TrayExpandedTotalHeight);
        }
    }

    private static Sprite GetTabPillSprite(bool active)
    {
        if (active && s_ActiveTabPillSprite != null) return s_ActiveTabPillSprite;
        if (!active && s_InactiveTabPillSprite != null) return s_InactiveTabPillSprite;

        Sprite sp = active
            ? CreateRoundedBorderSprite(
                "P03ActiveTabSprite",
                36,
                32,
                15,
                LumiWorldUITheme.Slot,                              // #FCFBF8 bright elevated pill
                new Color(0.89f, 0.86f, 0.82f, 1f),                 // Whisper-soft warm edge
                1.2f)
            : CreateRoundedBorderSprite(
                "P03InactiveTabSprite",
                36,
                32,
                15,
                new Color(0.890f, 0.867f, 0.831f, 1f),              // Recessed Warm Stone #E3DDD4
                new Color(0.855f, 0.828f, 0.788f, 1f),              // Subtle inner seam
                1.0f);

        if (active) s_ActiveTabPillSprite = sp;
        else s_InactiveTabPillSprite = sp;
        return sp;
    }

    private static Sprite CreateRoundedBorderSprite(string name, int w, int h, int radius, Color fill, Color border, float borderThickness)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = name;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[w * h];

        float r = radius;
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
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }

    public void SelectCategory(TrayCategory category)
    {
        CancelAnyActiveCardDrag();
        activeCategory = category;
        PlayerPrefs.SetInt(SavedCategoryPrefKey, (int)activeCategory);
        PlayerPrefs.Save();

        if (!isTrayExpanded)
        {
            isTrayExpanded = true;
            UpdateTrayExpandedVisuals();
        }

        GameAudioManager.Instance?.PlayClick();
        UpdateCategoryTabVisuals();
        RearrangeCards(null, false);
    }

    public void ToggleTrayExpanded()
    {
        if (isTrayExpanded)
        {
            // Collapsing tray while dragging/previewing cancels uncommitted placement without spending card/coins
            CancelAnyActiveCardDrag();
        }

        isTrayExpanded = !isTrayExpanded;
        GameAudioManager.Instance?.PlayClick();
        UpdateTrayExpandedVisuals();
    }

    private void CancelAnyActiveCardDrag()
    {
        foreach (GameObject card in cards)
        {
            if (card == null) continue;
            CardDrag drag = card.GetComponent<CardDrag>();
            if (drag != null && (drag.IsDragging || drag.IsReturningToHand))
            {
                drag.CancelDragImmediately();
            }
        }

        if (HexSinglePlacementController.Instance != null && HexSinglePlacementController.Instance.IsPreviewing)
        {
            HexSinglePlacementController.Instance.CancelPreview();
        }
    }

    private void UpdateCategoryTabVisuals()
    {
        // Count cards per category so players can also see category counts at a glance
        var countsByCat = new Dictionary<TrayCategory, int>
        {
            { TrayCategory.Nature, 0 },
            { TrayCategory.Creature, 0 },
            { TrayCategory.Building, 0 },
            { TrayCategory.Special, 0 }
        };

        foreach (GameObject card in cards)
        {
            if (card == null) continue;
            CardUI ui = card.GetComponent<CardUI>();
            if (ui == null || !BelongsToActiveIsland(ui.CardData)) continue;
            TrayCategory cat = ClassifyCard(ui.CardData);
            countsByCat[cat] = countsByCat[cat] + Mathf.Max(1, ui.Count);
        }

        foreach (var tab in categoryTabs)
        {
            bool active = tab.category == activeCategory;
            tab.bg.sprite = GetTabPillSprite(active);
            tab.bg.color = Color.white;
            if (tab.shadow != null) tab.shadow.enabled = active;
            Color fg = active ? LumiWorldUITheme.Text : new Color(0.39f, 0.34f, 0.30f, 1f);
            if (tab.icon != null) tab.icon.color = fg;
            if (tab.label != null)
            {
                tab.label.color = fg;
                string baseName = tab.category.ToString();
                int count = countsByCat.TryGetValue(tab.category, out int c) ? c : 0;
                tab.label.text = count > 0 ? $"{baseName} ({count})" : baseName;
            }
        }
    }

    private void UpdateTrayExpandedVisuals()
    {
        if (trayShapeGraphic != null)
        {
            trayShapeGraphic.CollapsedMode = !isTrayExpanded;
        }

        if (trayMonolithRect != null)
        {
            float collapsedW = trayShapeGraphic != null ? trayShapeGraphic.CollapsedWidth : 56f;
            float collapsedH = trayShapeGraphic != null ? trayShapeGraphic.CollapsedHeight : 26f;
            trayMonolithRect.sizeDelta = isTrayExpanded
                ? new Vector2(currentShelfWidth, TrayExpandedTotalHeight)
                : new Vector2(collapsedW, collapsedH);
        }

        if (trayTabsRowRect != null)
        {
            trayTabsRowRect.gameObject.SetActive(isTrayExpanded);
        }

        if (emptyCategoryLabel != null)
        {
            emptyCategoryLabel.gameObject.SetActive(isTrayExpanded && lastVisibleCardCount == 0);
        }

        if (cardContainer != null)
        {
            cardContainer.gameObject.SetActive(isTrayExpanded);
        }

        if (chevronBtnRect != null)
        {
            float expandedY = TrayBottomMargin + TrayShelfHeight + TabDockHeight - 3f;
            float collapsedH = trayShapeGraphic != null ? trayShapeGraphic.CollapsedHeight : 26f;
            chevronBtnRect.anchoredPosition = isTrayExpanded
                ? new Vector2(0f, expandedY)
                : new Vector2(0f, TrayBottomMargin);
            chevronBtnRect.sizeDelta = isTrayExpanded
                ? new Vector2(66f, 26f)
                : new Vector2(60f, collapsedH);
        }

        if (chevronGlyphImg != null)
        {
            chevronGlyphImg.sprite = LumiWorldUITheme.Glyph(isTrayExpanded ? "ChevronDown" : "ChevronUp");
            chevronGlyphImg.rectTransform.anchoredPosition = isTrayExpanded
                ? new Vector2(0f, -1.5f)
                : Vector2.zero;
        }
    }

    private IEnumerator SpawnCards()
    {
        if (WorldLaunch.IsVisit) yield break;

        suppressRewardFeedback = true;

        bool dealStarterHand;
        List<CardData> startingCards = LoadSavedHand(out dealStarterHand);

        List<CardData> uniqueCards = new List<CardData>();
        Dictionary<string, int> countByCardID = new Dictionary<string, int>();

        foreach (CardData data in startingCards)
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

        slotByCardID.Clear();
        cards.Clear();

        handCounts.Clear();
        foreach (KeyValuePair<string, int> pair in countByCardID) handCounts[pair.Key] = pair.Value;
        RecordDiscoveredCreatures(uniqueCards);
        handLoaded = true;
        if (dealStarterHand) SaveHand();

        int cardCount = uniqueCards.Count;
        if (cardCount == 0)
        {
            if (dealStarterHand)
                Debug.LogWarning("CardDeskController: No CardData assigned.");

            suppressRewardFeedback = false;
            UpdateCategoryTabVisuals();
            UpdateEmptyCategoryNotice(0);
            GrantEarlyRewards();
            yield break;
        }

        // If the saved category has 0 cards on startup, auto-select the first category that has cards
        bool hasActiveCatCard = uniqueCards.Exists(c => ClassifyCard(c) == activeCategory);
        if (!hasActiveCatCard && uniqueCards.Count > 0)
        {
            activeCategory = ClassifyCard(uniqueCards[0]);
            PlayerPrefs.SetInt(SavedCategoryPrefKey, (int)activeCategory);
        }

        for (int i = 0; i < cardCount; i++)
        {
            GameObject card = Instantiate(cardPrefab, cardContainer);

            CardUI cardUI = card.GetComponent<CardUI>();
            if (cardUI != null)
            {
                cardUI.Setup(uniqueCards[i]);
                int count = countByCardID.TryGetValue(GetCardKey(uniqueCards[i]), out int c) ? c : 1;
                cardUI.SetCount(count);
            }

            slotByCardID[GetCardKey(uniqueCards[i])] = card;
            card.transform.SetSiblingIndex(i);
            cards.Add(card);
        }

        UpdateCategoryTabVisuals();
        LayoutVisibleCards(initialSpawn: true);

        if (appearDelay > 0f && cardCount > 0)
        {
            yield return new WaitForSeconds(appearDelay * Mathf.Min(cardCount, 4) + 0.35f);
        }

        suppressRewardFeedback = false;
        GrantEarlyRewards();

        if (showStartingHandSummary)
        {
            ShowStartingHandSummary();
        }
    }

    private void LayoutVisibleCards(bool initialSpawn)
    {
        cards.RemoveAll(card => card == null);

        List<GameObject> visibleCards = new List<GameObject>();
        foreach (GameObject card in cards)
        {
            CardUI ui = card.GetComponent<CardUI>();
            bool match = ui != null && BelongsToActiveIsland(ui.CardData) && ClassifyCard(ui.CardData) == activeCategory;
            if (match)
            {
                visibleCards.Add(card);
            }
            else if (card.activeSelf)
            {
                card.GetComponent<CardMoveToPosition>()?.Stop();
                card.SetActive(false);
            }
        }

        int visibleCount = visibleCards.Count;
        UpdateEmptyCategoryNotice(visibleCount);

        if (visibleCount == 0)
        {
            UpdateTrayShelfWidth(0, 0.56f, 0f);
            return;
        }

        float cardScale = CalculateCardScale(visibleCount);
        float spacing = CalculateSpacing(visibleCount, cardScale);
        UpdateTrayShelfWidth(visibleCount, cardScale, spacing);
        float totalWidth = (visibleCount - 1) * spacing;

        for (int i = 0; i < visibleCount; i++)
        {
            GameObject card = visibleCards[i];
            bool wasInactive = !card.activeSelf;
            if (wasInactive)
            {
                card.SetActive(true);
            }

            card.transform.SetSiblingIndex(i);

            float x = i * spacing - totalWidth / 2f;
            Vector2 targetPosition = new Vector2(x, 0f);

            CardHandSlot handSlot = card.GetComponent<CardHandSlot>();
            if (handSlot != null)
            {
                handSlot.SetTargetPosition(targetPosition);
            }

            CardHover hover = card.GetComponent<CardHover>();
            if (hover != null)
            {
                hover.SetBaseScale(cardScale);
            }

            CardAppear appear = card.GetComponent<CardAppear>();
            if (appear != null)
            {
                appear.SetBaseScale(cardScale);
            }

            CardMoveToPosition mover = card.GetComponent<CardMoveToPosition>();

            if (initialSpawn && appear != null)
            {
                appear.Play(targetPosition, i * appearDelay);
            }
            else if (wasInactive)
            {
                mover?.Stop();
                if (appear != null) appear.ShowImmediately(targetPosition);
                else
                {
                    RectTransform rect = card.GetComponent<RectTransform>();
                    rect.anchoredPosition = targetPosition;
                    rect.localScale = Vector3.one * cardScale;
                }
                hover?.SyncAfterReturn();
            }
            else if (mover != null)
            {
                mover.MoveTo(targetPosition, rearrangeDuration);
            }
            else
            {
                RectTransform rect = card.GetComponent<RectTransform>();
                rect.anchoredPosition = targetPosition;
                rect.localScale = Vector3.one * cardScale;
            }
        }
    }

    private void UpdateEmptyCategoryNotice(int visibleCount)
    {
        lastVisibleCardCount = visibleCount;
        if (emptyCategoryLabel != null)
        {
            emptyCategoryLabel.gameObject.SetActive(isTrayExpanded && visibleCount == 0);
            if (visibleCount == 0)
            {
                emptyCategoryLabel.text = $"No {activeCategory.ToString().ToLowerInvariant()} cards in hand";
            }
        }
    }

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

            string rawName = !string.IsNullOrEmpty(ui.CardData.cardName)
                ? ui.CardData.cardName
                : ui.CardData.cardID;
            string name = CardShop.TranslateToEnglish(rawName, ui.CardData.name);

            totalCards += ui.Count;
            parts.Add(ui.Count > 1 ? $"{name} x{ui.Count}" : name);
        }

        if (parts.Count == 0)
        {
            return;
        }

        string title = $"{parts.Count} CARD TYPES";
        string body = string.Join("  ·  ", parts) + $"  ({totalCards} CARDS)";

        ShowToast(title, body);
    }

    private void ShowToast(string title, string body, CardData highlightCard = null)
    {
        CardRewardToast.Show(
            title,
            body,
            GetToastAnchorPosition(),
            highlightCard != null ? highlightCard.cardImage : null
        );
    }

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
    // SAVE / LOAD HAND (economy_<id>.json)
    // =========================================

    private static EconomySaveData SavedHandData()
    {
        EconomySaveSystem saveSystem = EconomySaveSystem.Instance;
        return saveSystem != null ? saveSystem.Progress : null;
    }

    private static bool CanSaveHand()
    {
        if (WorldLaunch.IsVisit || SaveSync.SavesSuspended) return false;
        return WorldSaveSystem.Instance == null || !WorldSaveSystem.Instance.SavesBlocked;
    }

    private List<CardData> LoadSavedHand(out bool dealStarter)
    {
        unresolvedHand.Clear();
        EconomySaveData saved = SavedHandData();
        if (saved == null || !saved.starterHandGranted)
        {
            dealStarter = true;
            return new List<CardData>(cardsToSpawn);
        }

        dealStarter = false;
        bool migratedLegacyCards = false;
        var result = new List<CardData>();
        CardCatalog catalog = WorldSaveSystem.Instance != null ? WorldSaveSystem.Instance.Cards : null;
        foreach (HandCardStack stack in saved.hand)
        {
            if (stack == null || stack.count <= 0 || string.IsNullOrEmpty(stack.cardId)) continue;

            string lookupId = stack.cardId;
            if (string.Equals(lookupId, "Rain.Wet", StringComparison.OrdinalIgnoreCase))
            {
                CardData unifiedRain = catalog != null ? catalog.Find("Rain") : null;
                if (unifiedRain != null)
                {
                    for (int i = 0; i < stack.count; i++) result.Add(unifiedRain);
                    migratedLegacyCards = true;
                    continue;
                }
            }

            CardData data = catalog != null ? catalog.Find(lookupId) : null;
            if (data == null)
            {
                unresolvedHand.Add(stack);
                Debug.LogWarning($"[LumiWorld Save] Không tìm thấy thẻ '{stack.cardId}' trong CardCatalog; giữ trong bản lưu nhưng chưa hiện trên tay. Chạy Tools > LumiWorld > Xây danh mục save.");
                continue;
            }
            for (int i = 0; i < stack.count; i++) result.Add(data);
        }
        if (migratedLegacyCards) dealStarter = true;
        return result;
    }

    private void SaveHand()
    {
        if (!handLoaded || !CanSaveHand()) return;

        EconomySaveData saved = SavedHandData();
        if (saved == null) return;

        saved.hand.Clear();
        foreach (KeyValuePair<string, int> pair in handCounts)
        {
            if (pair.Value > 0) saved.hand.Add(new HandCardStack { cardId = pair.Key, count = pair.Value });
        }
        foreach (HandCardStack stack in unresolvedHand) saved.hand.Add(stack);
        saved.starterHandGranted = true;

        EconomySaveSystem.Instance.MarkDirty();
        if (!deferHandFileWrite) EconomySaveSystem.Instance.SaveNow();
    }

    private bool deferHandFileWrite;

    public string ActiveIslandId { get; private set; } = "starter";
    private bool BelongsToActiveIsland(CardData data) =>
        LumiWorld.Acs.Islands.IslandPlacementRules.CanCardBelongToIsland(data, ActiveIslandId);

    public void SelectIsland(string islandId)
    {
        if (string.IsNullOrEmpty(islandId) || islandId == ActiveIslandId) return;
        ActiveIslandId = islandId;
        UpdateCategoryTabVisuals();
        LayoutVisibleCards(initialSpawn: true);
    }

    public bool HandReady => handLoaded && !WorldLaunch.IsVisit;

    public int GetHeldCount(string cardId) => handCounts.TryGetValue(cardId, out int held) ? held : 0;

    public List<KeyValuePair<CardData, int>> GetHeldCards()
    {
        var result = new List<KeyValuePair<CardData, int>>();
        foreach (KeyValuePair<string, GameObject> pair in slotByCardID)
        {
            CardUI ui = pair.Value != null ? pair.Value.GetComponent<CardUI>() : null;
            int held = GetHeldCount(pair.Key);
            if (ui != null && ui.CardData != null && held > 0) result.Add(new KeyValuePair<CardData, int>(ui.CardData, held));
        }
        return result;
    }

    public bool TakeCardsWithoutSaving(string cardId, int count)
    {
        if (!HandReady || count <= 0 || GetHeldCount(cardId) < count) return false;

        int left = GetHeldCount(cardId) - count;
        if (left > 0) handCounts[cardId] = left;
        else handCounts.Remove(cardId);

        deferHandFileWrite = true;
        try { SaveHand(); }
        finally { deferHandFileWrite = false; }

        if (slotByCardID.TryGetValue(cardId, out GameObject slot) && slot != null)
        {
            if (left > 0)
            {
                CardUI ui = slot.GetComponent<CardUI>();
                if (ui != null) ui.SetCount(left);
                UpdateCategoryTabVisuals();
            }
            else
            {
                slotByCardID.Remove(cardId);
                cards.Remove(slot);
                Destroy(slot);
                RearrangeCards();
            }
        }
        return true;
    }

    public void AddCardsWithoutSaving(CardData cardData, int count)
    {
        if (!HandReady || cardData == null || count <= 0) return;

        bool wasSuppressed = suppressRewardFeedback;
        suppressRewardFeedback = true;
        deferHandFileWrite = true;
        try
        {
            for (int i = 0; i < count; i++) AddRewardCard(cardData);
        }
        finally
        {
            deferHandFileWrite = false;
            suppressRewardFeedback = wasSuppressed;
        }
    }

    private void GrantEarlyRewards()
    {
        if (earlyRewards.Count == 0) return;
        var pending = new List<CardData>(earlyRewards);
        earlyRewards.Clear();
        foreach (CardData card in pending) AddRewardCard(card);
    }

    // =========================================
    // REMOVE CARD
    // =========================================

    public void RemoveCard(GameObject card)
    {
        if (card == null || !cards.Contains(card))
        {
            return;
        }

        CardUI cardUI = card.GetComponent<CardUI>();

        if (cardUI != null && cardUI.CardData != null)
        {
            string handKey = GetCardKey(cardUI.CardData);
            if (handCounts.TryGetValue(handKey, out int held))
            {
                if (held > 1) handCounts[handKey] = held - 1;
                else handCounts.Remove(handKey);
            }
            SaveHand();
        }

        if (cardUI != null && cardUI.Count > 1)
        {
            cardUI.SetCount(cardUI.Count - 1);
            UpdateCategoryTabVisuals();
            return;
        }

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

    private void RearrangeCards(GameObject cardAlreadyAnimating, bool sortCards)
    {
        if (sortCards)
        {
            SortCardsByTypeAndName();
        }

        UpdateCategoryTabVisuals();
        LayoutVisibleCards(initialSpawn: false);
    }

    // =========================================
    // ADD REWARD CARD
    // =========================================

    public void AddRewardCard(CardData cardData)
    {
        if (cardData == null)
        {
            Debug.LogWarning("CardDeskController: Reward CardData is null.");
            return;
        }

        if (cardPrefab == null || cardContainer == null)
        {
            return;
        }

        if (WorldLaunch.IsVisit) return;

        if (!handLoaded)
        {
            earlyRewards.Add(cardData);
            return;
        }

        string key = GetCardKey(cardData);

        if (cardData.reusable && GetHeldCount(key) > 0) return;

        handCounts[key] = (handCounts.TryGetValue(key, out int heldBefore) ? heldBefore : 0) + 1;
        if (cardData.cardType == CardType.Creature)
            EconomySaveSystem.Instance?.MarkCreatureDiscovered(key);
        SaveHand();

        if (slotByCardID.TryGetValue(key, out GameObject existingSlot) && existingSlot != null)
        {
            CardUI existingUI = existingSlot.GetComponent<CardUI>();
            if (existingUI != null)
            {
                existingUI.SetCount(existingUI.Count + 1);
            }

            UpdateCategoryTabVisuals();

            if (existingSlot.activeInHierarchy)
            {
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
            }

            return;
        }

        GameObject card = Instantiate(cardPrefab, cardContainer);

        CardUI cardUI = card.GetComponent<CardUI>();
        if (cardUI != null)
        {
            cardUI.Setup(cardData);
            cardUI.SetCount(1);
        }

        cards.Add(card);
        slotByCardID[key] = card;

        SortCardsByTypeAndName();
        UpdateCategoryTabVisuals();
        LayoutVisibleCards(initialSpawn: false);

        if (card.activeInHierarchy)
        {
            TryPlayRewardFeedback(
                cardData,
                card.GetComponent<RectTransform>(),
                isNewSlot: true,
                totalCount: 1
            );
        }
    }

    // =========================================
    // REWARD FEEDBACK
    // =========================================

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

        string rawDisplayName = !string.IsNullOrEmpty(cardData.cardName)
            ? cardData.cardName
            : (!string.IsNullOrEmpty(cardData.cardID) ? cardData.cardID : "Card");
        string displayName = CardShop.TranslateToEnglish(rawDisplayName, cardData.name);

        if (showRewardToast)
        {
            string title = isNewSlot ? "NEW CARD RECEIVED" : "CARD ADDED";
            string body = totalCount > 1
                ? $"{displayName} x{totalCount}"
                : displayName;

            ShowToast(title, body, cardData);
        }

        if (playRewardJuice)
        {
            CardRewardJuice.Play(slotRect, isNewSlot);
        }
    }

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
    // CALCULATE CARD SCALE & SPACING
    // =========================================

    public float CalculateCardScale(int cardCount)
    {
        if (cardCount <= 5)
        {
            return 0.57f;
        }

        float t = Mathf.Clamp01((cardCount - 5f) / 5f);
        return Mathf.Lerp(0.57f, 0.47f, t);
    }

    private float CalculateSpacing(int cardCount, float cardScale)
    {
        if (cardCount <= 1)
        {
            return 0f;
        }

        float effectiveWidth = Mathf.Max(120f, cardWidth);
        float scaledWidth = effectiveWidth * cardScale;
        float safeSpan = 684f;
        float targetSpacing = (safeSpan - scaledWidth) / (cardCount - 1);

        if (cardCount <= 5)
        {
            return Mathf.Clamp(targetSpacing, 88f, 97f);
        }

        float floorSpacing = Mathf.Min(scaledWidth + 5f, 76f);
        return Mathf.Clamp(targetSpacing, floorSpacing, 95f);
    }

    private static void RecordDiscoveredCreatures(List<CardData> cardsToCheck)
    {
        if (WorldLaunch.IsVisit || cardsToCheck == null || cardsToCheck.Count == 0) return;
        EconomySaveSystem save = EconomySaveSystem.Instance;
        if (save == null) return;

        List<string> creatureIds = null;
        foreach (CardData data in cardsToCheck)
        {
            if (data == null || data.cardType != CardType.Creature) continue;
            string key = GetCardKey(data);
            if (string.IsNullOrEmpty(key)) continue;
            creatureIds ??= new List<string>();
            creatureIds.Add(key);
        }
        if (creatureIds != null) save.MarkCreaturesDiscovered(creatureIds);
    }

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
