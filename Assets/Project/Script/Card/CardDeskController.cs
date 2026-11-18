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

    // P-03 Card Tray UI
    private const float TrayBottomMargin = 8f;
    private const float TrayShelfHeight = 128f;
    private const float TrayShelfMinWidth = 464f;
    private const float TrayShelfMaxWidth = 640f;
    private const float TabDockWidth = 432f;
    private const float TabDockHeight = 54f;
    private const float TabDockOverlap = 20f;

    private TrayCategory activeCategory = TrayCategory.Nature;
    private bool isTrayExpanded = true;
    private RectTransform deskRectTransform;
    private RectTransform trayFrameRect;
    private RectTransform tabDockRect;
    private RectTransform dockHeaderOverlayRect;
    private RectTransform trayTabsRowRect;
    private RectTransform chevronBtnRect;
    private Image chevronGlyphImg;
    private TextMeshProUGUI emptyCategoryLabel;
    private int lastFittedScreenW;
    private int lastFittedScreenH;
    private float lastFittedCanvasScale;

    private static Sprite s_TrayPanelSprite;
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
        if (WorldLaunch.IsVisit || cardContainer == null || trayFrameRect != null) return;

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
            deskRectTransform.sizeDelta = new Vector2(TrayShelfMaxWidth + 40f, 220f);

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

        // 1. Raised Top Tab Dock (connects seamlessly to top-center of Shelf)
        float dockBottomY = TrayBottomMargin + TrayShelfHeight - TabDockOverlap;
        GameObject dockObj = new GameObject("P03_TabDock", typeof(RectTransform), typeof(Image));
        tabDockRect = (RectTransform)dockObj.transform;
        tabDockRect.SetParent(parentTransform, false);
        tabDockRect.anchorMin = new Vector2(0.5f, 0f);
        tabDockRect.anchorMax = new Vector2(0.5f, 0f);
        tabDockRect.pivot = new Vector2(0.5f, 0f);
        tabDockRect.anchoredPosition = new Vector2(0f, dockBottomY);
        tabDockRect.sizeDelta = new Vector2(TabDockWidth, TabDockHeight);
        tabDockRect.SetSiblingIndex(insertIndex++);

        Image dockBg = dockObj.GetComponent<Image>();
        dockBg.sprite = GetTrayPanelSprite();
        dockBg.type = Image.Type.Sliced;
        dockBg.color = Color.white;
        dockBg.raycastTarget = true;
        LumiWorldUITheme.AddSoftElevation(dockObj, 4f, 0.20f);

        // 2. Monolithic Warm-Ivory Card Shelf
        GameObject trayObj = new GameObject("P03_CardTrayFrame", typeof(RectTransform), typeof(Image));
        trayFrameRect = (RectTransform)trayObj.transform;
        trayFrameRect.SetParent(parentTransform, false);
        trayFrameRect.anchorMin = new Vector2(0.5f, 0f);
        trayFrameRect.anchorMax = new Vector2(0.5f, 0f);
        trayFrameRect.pivot = new Vector2(0.5f, 0f);
        trayFrameRect.anchoredPosition = new Vector2(0f, TrayBottomMargin);
        trayFrameRect.sizeDelta = new Vector2(TrayShelfMinWidth, TrayShelfHeight);
        trayFrameRect.SetSiblingIndex(insertIndex++);

        Image trayBg = trayObj.GetComponent<Image>();
        trayBg.sprite = GetTrayPanelSprite();
        trayBg.type = Image.Type.Sliced;
        trayBg.color = Color.white;
        trayBg.raycastTarget = true;
        LumiWorldUITheme.AddSoftElevation(trayObj, 5f, 0.22f);

        // Empty category hint inside tray shelf
        GameObject emptyObj = new GameObject("EmptyCategoryNotice", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform emptyRect = (RectTransform)emptyObj.transform;
        emptyRect.SetParent(trayFrameRect, false);
        emptyRect.anchorMin = new Vector2(0.5f, 0.5f);
        emptyRect.anchorMax = new Vector2(0.5f, 0.5f);
        emptyRect.sizeDelta = new Vector2(400f, 36f);
        emptyRect.anchoredPosition = Vector2.zero;

        emptyCategoryLabel = emptyObj.GetComponent<TextMeshProUGUI>();
        emptyCategoryLabel.font = LumiWorldUITheme.Body;
        emptyCategoryLabel.fontSize = 13f;
        emptyCategoryLabel.fontStyle = FontStyles.Bold;
        emptyCategoryLabel.color = LumiWorldUITheme.Muted;
        emptyCategoryLabel.alignment = TextAlignmentOptions.Center;
        emptyCategoryLabel.raycastTarget = false;
        emptyCategoryLabel.text = "No cards in this category";
        emptyObj.SetActive(false);

        // 3. Dock Header Overlay: seamless ivory join patch + centered 4 category tabs
        GameObject overlayObj = new GameObject("P03_DockHeaderOverlay", typeof(RectTransform));
        dockHeaderOverlayRect = (RectTransform)overlayObj.transform;
        dockHeaderOverlayRect.SetParent(parentTransform, false);
        dockHeaderOverlayRect.anchorMin = new Vector2(0.5f, 0f);
        dockHeaderOverlayRect.anchorMax = new Vector2(0.5f, 0f);
        dockHeaderOverlayRect.pivot = new Vector2(0.5f, 0f);
        dockHeaderOverlayRect.anchoredPosition = new Vector2(0f, dockBottomY);
        dockHeaderOverlayRect.sizeDelta = new Vector2(TabDockWidth, TabDockHeight);
        dockHeaderOverlayRect.SetSiblingIndex(insertIndex++);

        GameObject seamObj = new GameObject("SeamBridgeFill", typeof(RectTransform), typeof(Image));
        RectTransform seamRect = (RectTransform)seamObj.transform;
        seamRect.SetParent(dockHeaderOverlayRect, false);
        seamRect.anchorMin = new Vector2(0.5f, 0f);
        seamRect.anchorMax = new Vector2(0.5f, 0f);
        seamRect.pivot = new Vector2(0.5f, 0f);
        seamRect.anchoredPosition = new Vector2(0f, -2f);
        seamRect.sizeDelta = new Vector2(TabDockWidth - 4f, TabDockOverlap + 4f);
        Image seamImg = seamObj.GetComponent<Image>();
        seamImg.color = LumiWorldUITheme.Surface;
        seamImg.raycastTarget = false;

        GameObject tabsRowObj = new GameObject("CategoryTabsRow", typeof(RectTransform));
        trayTabsRowRect = (RectTransform)tabsRowObj.transform;
        trayTabsRowRect.SetParent(dockHeaderOverlayRect, false);
        trayTabsRowRect.anchorMin = new Vector2(0.5f, 1f);
        trayTabsRowRect.anchorMax = new Vector2(0.5f, 1f);
        trayTabsRowRect.pivot = new Vector2(0.5f, 1f);
        trayTabsRowRect.anchoredPosition = new Vector2(0f, -6f);
        trayTabsRowRect.sizeDelta = new Vector2(TabDockWidth - 16f, 28f);

        (TrayCategory cat, string label, string glyphCode)[] tabDefs =
        {
            (TrayCategory.Nature, "Nature", "G-11"),
            (TrayCategory.Creature, "Creature", "G-12"),
            (TrayCategory.Building, "Building", "G-13"),
            (TrayCategory.Special, "Special", "G-14")
        };

        categoryTabs.Clear();
        const float tabW = 98f;
        const float tabH = 28f;
        const float tabGap = 6f;
        float totalTabsW = tabDefs.Length * tabW + (tabDefs.Length - 1) * tabGap;
        float startX = -totalTabsW * 0.5f + tabW * 0.5f;

        for (int i = 0; i < tabDefs.Length; i++)
        {
            var def = tabDefs[i];
            float x = startX + i * (tabW + tabGap);

            GameObject tabObj = new GameObject("Tab_" + def.label, typeof(RectTransform), typeof(Image), typeof(Button));
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
            LumiWorldUITheme.AddSoftElevation(tabObj, 2f, 0.16f);
            Shadow tabShadow = tabObj.GetComponent<Shadow>();

            GameObject icObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            RectTransform icRect = (RectTransform)icObj.transform;
            icRect.SetParent(tabRect, false);
            icRect.anchorMin = icRect.anchorMax = new Vector2(0f, 0.5f);
            icRect.pivot = new Vector2(0f, 0.5f);
            icRect.anchoredPosition = new Vector2(8f, 0f);
            icRect.sizeDelta = new Vector2(13f, 13f);

            Image icImg = icObj.GetComponent<Image>();
            icImg.sprite = LumiWorldUITheme.Glyph(def.glyphCode);
            icImg.preserveAspect = true;
            icImg.raycastTarget = false;

            GameObject lblObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform lblRect = (RectTransform)lblObj.transform;
            lblRect.SetParent(tabRect, false);
            lblRect.anchorMin = new Vector2(0f, 0f);
            lblRect.anchorMax = new Vector2(1f, 1f);
            lblRect.offsetMin = new Vector2(24f, 0f);
            lblRect.offsetMax = new Vector2(-6f, 0f);

            TextMeshProUGUI lblTmp = lblObj.GetComponent<TextMeshProUGUI>();
            lblTmp.font = LumiWorldUITheme.Body;
            lblTmp.text = def.label;
            lblTmp.fontSize = 10f;
            lblTmp.enableAutoSizing = true;
            lblTmp.fontSizeMin = 8f;
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

        // Ensure cards render above tray shelf & dock bridge
        cardContainer.SetSiblingIndex(insertIndex);

        // 4. Centered Show/Hide Chevron Pill right above the Tab Dock
        GameObject chevObj = new GameObject("P03_TrayChevronButton", typeof(RectTransform), typeof(Image), typeof(Button));
        chevronBtnRect = (RectTransform)chevObj.transform;
        chevronBtnRect.SetParent(parentTransform, false);
        chevronBtnRect.anchorMin = new Vector2(0.5f, 0f);
        chevronBtnRect.anchorMax = new Vector2(0.5f, 0f);
        chevronBtnRect.pivot = new Vector2(0.5f, 0f);
        chevronBtnRect.sizeDelta = new Vector2(44f, 20f);
        chevronBtnRect.SetAsLastSibling();

        Image chevBg = chevObj.GetComponent<Image>();
        chevBg.sprite = GetTrayPanelSprite();
        chevBg.type = Image.Type.Sliced;
        chevBg.color = Color.white;
        chevBg.raycastTarget = true;
        LumiWorldUITheme.AddSoftElevation(chevObj, 2.5f, 0.18f);

        GameObject chevIcObj = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
        RectTransform chevIcRect = (RectTransform)chevIcObj.transform;
        chevIcRect.SetParent(chevronBtnRect, false);
        chevIcRect.anchorMin = chevIcRect.anchorMax = chevIcRect.pivot = new Vector2(0.5f, 0.5f);
        chevIcRect.sizeDelta = new Vector2(12f, 12f);
        chevIcRect.anchoredPosition = Vector2.zero;

        chevronGlyphImg = chevIcObj.GetComponent<Image>();
        chevronGlyphImg.preserveAspect = true;
        chevronGlyphImg.color = LumiWorldUITheme.Text;
        chevronGlyphImg.raycastTarget = false;

        Button chevBtn = chevObj.GetComponent<Button>();
        chevBtn.targetGraphic = chevBg;
        chevBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        chevBtn.onClick.AddListener(ToggleTrayExpanded);

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
        if (trayFrameRect == null) return;
        float targetW = TrayShelfMinWidth;
        if (visibleCount > 0)
        {
            float effW = Mathf.Max(120f, cardWidth) * cardScale;
            float needed = (visibleCount - 1) * spacing + effW + 32f;
            targetW = Mathf.Clamp(Mathf.Max(TrayShelfMinWidth, needed), TrayShelfMinWidth, TrayShelfMaxWidth);
        }
        trayFrameRect.sizeDelta = new Vector2(targetW, TrayShelfHeight);
    }

    private static Sprite GetTrayPanelSprite()
    {
        if (s_TrayPanelSprite != null) return s_TrayPanelSprite;
        s_TrayPanelSprite = CreateRoundedBorderSprite(
            "P03TrayPanelSprite",
            48,
            48,
            18,
            LumiWorldUITheme.Surface,
            new Color(0.86f, 0.83f, 0.78f, 1f),
            1.5f);
        return s_TrayPanelSprite;
    }

    private static Sprite GetTabPillSprite(bool active)
    {
        if (active && s_ActiveTabPillSprite != null) return s_ActiveTabPillSprite;
        if (!active && s_InactiveTabPillSprite != null) return s_InactiveTabPillSprite;

        Sprite sp = active
            ? CreateRoundedBorderSprite("P03ActiveTabSprite", 40, 32, 13, LumiWorldUITheme.Slot, new Color(0.85f, 0.82f, 0.77f, 1f), 1.3f)
            : CreateRoundedBorderSprite("P03InactiveTabSprite", 40, 32, 13, new Color(0.90f, 0.88f, 0.84f, 1f), new Color(0.86f, 0.84f, 0.79f, 1f), 1f);

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
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = x < r ? r - x : (x > w - 1 - r ? x - (w - 1 - r) : 0f);
                float dy = y < r ? r - y : (y > h - 1 - r ? y - (h - 1 - r) : 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(r - dist + 0.5f);
                if (alpha <= 0.005f)
                {
                    px[y * w + x] = Color.clear;
                    continue;
                }

                float edgeT = Mathf.Clamp01((dist - (r - borderThickness)) + 0.5f);
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
            if (ui == null || ui.CardData == null) continue;
            TrayCategory cat = ClassifyCard(ui.CardData);
            countsByCat[cat] = countsByCat[cat] + Mathf.Max(1, ui.Count);
        }

        foreach (var tab in categoryTabs)
        {
            bool active = tab.category == activeCategory;
            tab.bg.sprite = GetTabPillSprite(active);
            tab.bg.color = Color.white;
            if (tab.shadow != null) tab.shadow.enabled = active;
            Color fg = active ? LumiWorldUITheme.Text : new Color(0.41f, 0.37f, 0.33f, 1f);
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
        if (tabDockRect != null)
        {
            tabDockRect.gameObject.SetActive(isTrayExpanded);
        }

        if (trayFrameRect != null)
        {
            trayFrameRect.gameObject.SetActive(isTrayExpanded);
        }

        if (dockHeaderOverlayRect != null)
        {
            dockHeaderOverlayRect.gameObject.SetActive(isTrayExpanded);
        }

        if (cardContainer != null)
        {
            cardContainer.gameObject.SetActive(isTrayExpanded);
        }

        if (chevronBtnRect != null)
        {
            float expandedY = TrayBottomMargin + TrayShelfHeight - TabDockOverlap + TabDockHeight + 2f;
            chevronBtnRect.anchoredPosition = isTrayExpanded
                ? new Vector2(0f, expandedY)
                : new Vector2(0f, 10f);
            chevronBtnRect.sizeDelta = isTrayExpanded
                ? new Vector2(44f, 20f)
                : new Vector2(48f, 24f);
        }

        if (chevronGlyphImg != null)
        {
            chevronGlyphImg.sprite = LumiWorldUITheme.Glyph(isTrayExpanded ? "ChevronDown" : "ChevronUp");
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
            bool match = ui != null && ui.CardData != null && ClassifyCard(ui.CardData) == activeCategory;
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
        if (emptyCategoryLabel != null)
        {
            emptyCategoryLabel.gameObject.SetActive(visibleCount == 0);
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
        var result = new List<CardData>();
        CardCatalog catalog = WorldSaveSystem.Instance != null ? WorldSaveSystem.Instance.Cards : null;
        foreach (HandCardStack stack in saved.hand)
        {
            if (stack == null || stack.count <= 0 || string.IsNullOrEmpty(stack.cardId)) continue;

            CardData data = catalog != null ? catalog.Find(stack.cardId) : null;
            if (data == null)
            {
                unresolvedHand.Add(stack);
                Debug.LogWarning($"[LumiWorld Save] Không tìm thấy thẻ '{stack.cardId}' trong CardCatalog; giữ trong bản lưu nhưng chưa hiện trên tay. Chạy Tools > LumiWorld > Xây danh mục save.");
                continue;
            }
            for (int i = 0; i < stack.count; i++) result.Add(data);
        }
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

        handCounts[key] = (handCounts.TryGetValue(key, out int heldBefore) ? heldBefore : 0) + 1;
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
            return 0.56f;
        }

        float t = Mathf.Clamp01((cardCount - 5f) / 5f);
        return Mathf.Lerp(0.56f, 0.45f, t);
    }

    private float CalculateSpacing(int cardCount, float cardScale)
    {
        if (cardCount <= 1)
        {
            return 0f;
        }

        float effectiveWidth = Mathf.Max(120f, cardWidth);
        float scaledWidth = effectiveWidth * cardScale;
        float safeSpan = 590f;
        float targetSpacing = (safeSpan - scaledWidth) / (cardCount - 1);

        if (cardCount <= 5)
        {
            return Mathf.Clamp(targetSpacing, 84f, 94f);
        }

        float floorSpacing = Mathf.Min(scaledWidth + 4f, 70f);
        return Mathf.Clamp(targetSpacing, floorSpacing, 92f);
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
