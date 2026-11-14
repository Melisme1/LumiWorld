using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using LumiWorld.Acs;

/// <summary>
/// Creation Lab UI (Lai tạo thú) – manages 3 views:
///   View 0 (Chamber Overview): 3 test tube slots with status and sleek laboratory capsule backgrounds
///   View 1 (Creature Selection): 8 tabs (All + 7 biomes) + hand card grid + 3D Braluff preview & parent avatars + Evolve
///   View 2 (Incubation In Progress): live parent tubes, countdown timer, speed-up buttons
///
/// Designed as an EconomyHUD overlay (100% English, responsive scaling, blue/white rounded aesthetics, zero unicode emojis).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CreationLabUI : MonoBehaviour
{
    public static CreationLabUI Instance { get; private set; }

    // ─── Theme Colors ─────────────────────────────────────────────────────────
    private static readonly Color ThemeBlue       = new Color(0.08f, 0.58f, 0.95f, 1f);
    private static readonly Color ThemeBlueDark   = new Color(0.05f, 0.35f, 0.65f, 1f);
    private static readonly Color ThemeCyanBorder = new Color(0.20f, 0.65f, 0.95f, 0.85f);
    private static readonly Color PanelBg         = new Color(0.93f, 0.97f, 1.00f, 0.98f);
    private static readonly Color CardBg          = Color.white;
    private static readonly Color ChamberPodBg    = new Color(0.08f, 0.12f, 0.18f, 1f);     // Dark pod eliminates white washout!
    private static readonly Color ChamberOuterBg  = new Color(0.12f, 0.17f, 0.25f, 1f);
    private static readonly Color TextDark        = new Color(0.08f, 0.15f, 0.28f, 1f);
    private static readonly Color TextMuted       = new Color(0.40f, 0.50f, 0.65f, 1f);
    private static readonly Color TextLight       = new Color(0.88f, 0.94f, 1.00f, 1f);
    private static readonly Color GreenProgress   = new Color(0.15f, 0.78f, 0.38f, 1f);
    private static readonly Color CoinGold        = new Color(0.92f, 0.68f, 0.10f, 1f);
    private static readonly Color DimmerColor     = new Color(0.04f, 0.08f, 0.14f, 0.70f);
    private static readonly Color LockColor       = new Color(0.06f, 0.08f, 0.12f, 0.75f);

    // ─── Biome Definition List ────────────────────────────────────────────────
    public static readonly string[] BiomeNames = {
        "Bloomfield",
        "Leafwood",
        "Lilywater",
        "Rainleaf",
        "Reedmarsh",
        "Streamstone",
        "Windheath"
    };

    // ─── State ───────────────────────────────────────────────────────────────
    private int activeChamberIndex = 0;   // which chamber is being viewed/configured
    private int viewMode = 0;             // 0 = overview, 1 = selection, 2 = incubation
    private float timerUpdateCounter = 0f;

    // Selection state
    private CardData selectedParentA;
    private CardData selectedParentB;
    private BreedingRecipe foundRecipe;

    // ─── UI References ────────────────────────────────────────────────────────
    private GameObject window;
    private RectTransform panel;

    // View containers
    private GameObject overviewView;
    private GameObject selectionView;
    private GameObject incubationView;

    // Overview widgets
    private ChamberSlotWidget[] chamberWidgets;

    // Selection widgets
    private GameObject selCardGrid;
    private TextMeshProUGUI selTitleText;
    private RawImage selHybrid3DRawImage;
    private Image selHybrid2DImage;
    private TextMeshProUGUI selComingSoonText;
    private TextMeshProUGUI selHybridName;
    private RawImage selParentA3DRawImage;
    private Image selParentAAvatar;
    private TextMeshProUGUI selParentAName;
    private RawImage selParentB3DRawImage;
    private Image selParentBAvatar;
    private TextMeshProUGUI selParentBName;
    private Button evolveButton;
    private TextMeshProUGUI evolveDurationText;
    private List<Button> biomeTabButtons = new List<Button>();
    private List<GameObject> cardGridItems = new List<GameObject>();
    private string activeTab = "All";

    // Incubation widgets
    private RawImage incubFather3DRawImg;
    private Image incubFatherImg;
    private TextMeshProUGUI incubFatherName;
    private RawImage incubMother3DRawImg;
    private Image incubMotherImg;
    private TextMeshProUGUI incubMotherName;
    private RawImage incubOffspring3DRawImg;
    private Image incubOffspringImg;
    private TextMeshProUGUI incubOffspringName;
    private Slider incubProgressBar;
    private TextMeshProUGUI incubTimerText;
    private Button speedUpCoinsBtn;
    private Button speedUpAdBtn;

    // Congrats popup
    private GameObject congratsPopup;
    private TextMeshProUGUI congratsTitle;
    private TextMeshProUGUI congratsSub;
    private Image congratsCardImg;
    private RectTransform congratsBadgesContainer;
    private CardData pendingClaimOffspring;

    public bool IsOpen => window != null && window.activeSelf;

    // ─── Lifecycle ───────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        BuildWindow();
        SubscribeEvents();
    }

    private void OnDestroy()
    {
        if (BreedingManager.Instance != null)
        {
            BreedingManager.Instance.OnChamberStateChanged -= OnChamberChanged;
            BreedingManager.Instance.OnBreedingComplete    -= OnBreedingComplete;
        }
    }

    private void SubscribeEvents()
    {
        if (BreedingManager.Instance == null) return;
        BreedingManager.Instance.OnChamberStateChanged += OnChamberChanged;
        BreedingManager.Instance.OnBreedingComplete    -= OnBreedingComplete;
        BreedingManager.Instance.OnBreedingComplete    += OnBreedingComplete;
    }

    private void Update()
    {
        if (!IsOpen) return;

        timerUpdateCounter += Time.unscaledDeltaTime;
        if (timerUpdateCounter >= 0.25f)
        {
            timerUpdateCounter = 0f;
            if (viewMode == 0) RefreshOverviewTimers();
            else if (viewMode == 2) RefreshIncubationView();
        }
    }

    // ─── Public API ──────────────────────────────────────────────────────────
    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (window == null) BuildWindow();

        transform.SetAsLastSibling();
        window.SetActive(true);
        EconomyHUD.BringWindowToFront(window);
        GameAudioManager.Instance?.PlayClick();

        ShowView(0);
        RefreshOverview();
    }

    public void Close()
    {
        if (congratsPopup != null && congratsPopup.activeSelf)
        {
            congratsPopup.SetActive(false);
        }
        if (window != null && window.activeSelf)
        {
            window.SetActive(false);
            EconomyHUD.MarkWindowClosed();
            GameAudioManager.Instance?.PlayClick();
            CreatureModelPreviewer.Instance?.ClearAllSlots();
        }
    }

    // ─── UI Builder ──────────────────────────────────────────────────────────
    private void BuildWindow()
    {
        if (window != null) return;

        // 1. Root full-screen window
        RectTransform windowRect = EconomyHUD.CreateRect("Window", transform);
        EconomyHUD.Stretch(windowRect);
        window = windowRect.gameObject;
        EconomyHUD.RegisterWindow(window, Close);

        // 2. Full-screen backdrop (dimmer)
        RectTransform backdropRect = EconomyHUD.CreateRect("Backdrop", windowRect);
        EconomyHUD.Stretch(backdropRect);
        Image backdropImg = backdropRect.gameObject.AddComponent<Image>();
        backdropImg.color = DimmerColor;
        Button backdropBtn = backdropRect.gameObject.AddComponent<Button>();
        backdropBtn.transition = Selectable.Transition.None;
        backdropBtn.onClick.AddListener(Close);

        // 3. Main Dialog Panel (940 x 630) centered
        panel = EconomyHUD.CreateRect("MainPanel", windowRect);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(940f, 630f);
        panel.anchoredPosition = Vector2.zero;
        EconomyHUD.CreateBorderedPanel(panel, PanelBg, ThemeBlue).raycastTarget = true;

        // 4. Header Bar (56px high)
        BuildHeader();

        // 5. Content Area (fills below header)
        RectTransform contentArea = EconomyHUD.CreateRect("ContentArea", panel);
        contentArea.anchorMin = Vector2.zero;
        contentArea.anchorMax = Vector2.one;
        contentArea.offsetMin = Vector2.zero;
        contentArea.offsetMax = new Vector2(0f, -56f);

        // 6. Three views inside contentArea
        overviewView   = BuildOverviewView(contentArea);
        selectionView  = BuildSelectionView(contentArea);
        incubationView = BuildIncubationView(contentArea);

        // 7. Congrats Popup Modal
        BuildCongratsPopup(windowRect);

        window.SetActive(false);
    }

    // ─────────────────────── Header ──────────────────────────────────────────
    private void BuildHeader()
    {
        RectTransform headerRect = EconomyHUD.CreateRect("Header", panel);
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot     = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 56f);
        headerRect.anchoredPosition = Vector2.zero;

        Image headerBg = headerRect.gameObject.AddComponent<Image>();
        headerBg.sprite = EconomyHUD.RoundedSprite;
        headerBg.type = Image.Type.Sliced;
        headerBg.color = ThemeBlue;

        // Title icon (Breeding Gene Icon)
        RectTransform iconRect = EconomyHUD.CreateRect("TitleIcon", headerRect);
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(20f, 0f);
        iconRect.sizeDelta = new Vector2(30f, 30f);
        Image iconImg = iconRect.gameObject.AddComponent<Image>();
        Sprite geneSprite = LoadSpriteSafe("UI_Buttons/icon_breeding");
        if (geneSprite != null)
        {
            iconImg.sprite = geneSprite;
            iconImg.preserveAspect = true;
        }
        else
        {
            iconImg.color = Color.clear;
        }

        // Title text (Pure English, no emojis -> NO □)
        TextMeshProUGUI title = EconomyHUD.CreateText("Title", headerRect, 20f, Color.white, TextAlignmentOptions.MidlineLeft);
        title.rectTransform.anchorMin = new Vector2(0f, 0f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.offsetMin = new Vector2(58f, 0f);
        title.rectTransform.offsetMax = new Vector2(-70f, 0f);
        title.text = "CREATION LAB";
        title.fontStyle = FontStyles.Bold;

        // Close button 'X' (Pure ASCII 'X' -> NO □)
        RectTransform closeRect = EconomyHUD.CreateRect("CloseBtn", headerRect);
        closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-12f, 0f);
        closeRect.sizeDelta = new Vector2(36f, 36f);

        Button closeBtn = EconomyHUD.AddButton(closeRect, Color.white);
        TextMeshProUGUI closeLbl = EconomyHUD.CreateText("Label", closeRect, 18f, ThemeBlueDark, TextAlignmentOptions.Center);
        closeLbl.rectTransform.anchorMin = Vector2.zero;
        closeLbl.rectTransform.anchorMax = Vector2.one;
        closeLbl.rectTransform.offsetMin = closeLbl.rectTransform.offsetMax = Vector2.zero;
        closeLbl.text = "X";
        closeLbl.fontStyle = FontStyles.Bold;
        closeBtn.onClick.AddListener(Close);
    }

    // ─────────────────────── View 0: Overview ─────────────────────────────────
    private GameObject BuildOverviewView(RectTransform parent)
    {
        RectTransform root = EconomyHUD.CreateRect("OverviewView", parent);
        EconomyHUD.Stretch(root);

        // Subtitle
        TextMeshProUGUI sub = EconomyHUD.CreateText("Subtitle", root, 14f, TextMuted, TextAlignmentOptions.Center);
        sub.rectTransform.anchorMin = new Vector2(0f, 1f);
        sub.rectTransform.anchorMax = new Vector2(1f, 1f);
        sub.rectTransform.pivot = new Vector2(0.5f, 1f);
        sub.rectTransform.anchoredPosition = new Vector2(0f, -8f);
        sub.rectTransform.sizeDelta = new Vector2(0f, 28f);
        sub.text = "Choose an incubation chamber to breed new hybrid creatures!";

        // Horizontal Row for 3 chambers
        RectTransform row = EconomyHUD.CreateRect("ChambersRow", root);
        row.anchorMin = new Vector2(0f, 0f);
        row.anchorMax = new Vector2(1f, 1f);
        row.offsetMin = new Vector2(24f, 20f);
        row.offsetMax = new Vector2(-24f, -40f);

        HorizontalLayoutGroup hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 24f;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        int total = 3;
        chamberWidgets = new ChamberSlotWidget[total];
        for (int i = 0; i < total; i++)
        {
            chamberWidgets[i] = BuildChamberSlot(row, i);
        }

        return root.gameObject;
    }

    private ChamberSlotWidget BuildChamberSlot(RectTransform parent, int index)
    {
        var slot = new ChamberSlotWidget();
        slot.index = index;

        // Outer Slot Card (Dark Slate Container)
        RectTransform slotRect = EconomyHUD.CreateRect($"Chamber_{index}", parent);
        EconomyHUD.CreateBorderedPanel(slotRect, ChamberOuterBg, ThemeCyanBorder);

        // Chamber number tag at top
        TextMeshProUGUI numTag = EconomyHUD.CreateText("ChamberNum", slotRect, 15f, TextLight, TextAlignmentOptions.Center);
        numTag.rectTransform.anchorMin = new Vector2(0f, 1f);
        numTag.rectTransform.anchorMax = new Vector2(1f, 1f);
        numTag.rectTransform.pivot = new Vector2(0.5f, 1f);
        numTag.rectTransform.anchoredPosition = new Vector2(0f, -8f);
        numTag.rectTransform.sizeDelta = new Vector2(0f, 26f);
        numTag.text = $"Chamber {index + 1}";
        numTag.fontStyle = FontStyles.Bold;

        // Dark Pod Inner Window - Eliminates white washout so cyan glow & glass look vibrant!
        RectTransform podRect = EconomyHUD.CreateRect("PodWindow", slotRect);
        podRect.anchorMin = new Vector2(0.06f, 0.23f);
        podRect.anchorMax = new Vector2(0.94f, 0.92f);
        podRect.offsetMin = podRect.offsetMax = Vector2.zero;
        EconomyHUD.CreateBorderedPanel(podRect, ChamberPodBg, new Color(0.25f, 0.55f, 0.85f, 0.5f));

        // Test tube image (sitting inside dark pod)
        RectTransform tubeRect = EconomyHUD.CreateRect("TubeImage", podRect);
        tubeRect.anchorMin = new Vector2(0.05f, 0.05f);
        tubeRect.anchorMax = new Vector2(0.95f, 0.95f);
        tubeRect.offsetMin = tubeRect.offsetMax = Vector2.zero;

        slot.tubeImage = tubeRect.gameObject.AddComponent<Image>();
        slot.tubeImage.preserveAspect = true;
        slot.tubeImage.raycastTarget = false;

        // Inside tube: 2D fallback preview
        RectTransform c2DRect = EconomyHUD.CreateRect("Creature2D", tubeRect);
        c2DRect.anchorMin = new Vector2(0.25f, 0.24f);
        c2DRect.anchorMax = new Vector2(0.75f, 0.76f);
        c2DRect.offsetMin = c2DRect.offsetMax = Vector2.zero;
        slot.creature2DImg = c2DRect.gameObject.AddComponent<Image>();
        slot.creature2DImg.preserveAspect = true;
        slot.creature2DImg.color = Color.clear;
        slot.creature2DImg.raycastTarget = false;

        // Inside tube: 3D live hybrid preview (1:1 aspect ratio, sized to fit cleanly inside the glass cylinder)
        RectTransform c3DRect = EconomyHUD.CreateRect("Creature3D", tubeRect);
        c3DRect.anchorMin = new Vector2(0.26f, 0.25f);
        c3DRect.anchorMax = new Vector2(0.74f, 0.75f);
        c3DRect.offsetMin = c3DRect.offsetMax = Vector2.zero;
        slot.creature3DImg = c3DRect.gameObject.AddComponent<RawImage>();
        slot.creature3DImg.color = Color.white;
        slot.creature3DImg.raycastTarget = false;
        var slotArf = c3DRect.gameObject.AddComponent<AspectRatioFitter>();
        slotArf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        slotArf.aspectRatio = 1f;
        slot.creature3DImg.gameObject.SetActive(false);

        // Status label
        slot.statusText = EconomyHUD.CreateText("StatusLabel", slotRect, 13f, TextLight, TextAlignmentOptions.Center);
        slot.statusText.rectTransform.anchorMin = new Vector2(0.05f, 0.14f);
        slot.statusText.rectTransform.anchorMax = new Vector2(0.95f, 0.22f);
        slot.statusText.rectTransform.offsetMin = slot.statusText.rectTransform.offsetMax = Vector2.zero;
        slot.statusText.text = "Empty";

        // Progress bar (Slider)
        RectTransform barRect = EconomyHUD.CreateRect("ProgressBar", slotRect);
        barRect.anchorMin = new Vector2(0.10f, 0.12f);
        barRect.anchorMax = new Vector2(0.90f, 0.15f);
        barRect.offsetMin = barRect.offsetMax = Vector2.zero;

        slot.progressBar = barRect.gameObject.AddComponent<Slider>();
        slot.progressBar.minValue = 0f;
        slot.progressBar.maxValue = 1f;
        slot.progressBar.value = 0f;
        slot.progressBar.interactable = false;
        BuildSliderVisuals(slot.progressBar, barRect);
        barRect.gameObject.SetActive(false);
        slot.progressBarGo = barRect.gameObject;

        // Action Button ("Get Lumi" / "View" / "Claim!" / "Locked")
        RectTransform btnRect = EconomyHUD.CreateRect("ActionBtn", slotRect);
        btnRect.anchorMin = new Vector2(0.10f, 0.03f);
        btnRect.anchorMax = new Vector2(0.90f, 0.11f);
        btnRect.offsetMin = btnRect.offsetMax = Vector2.zero;

        slot.actionBtn = EconomyHUD.AddButton(btnRect, ThemeBlue);
        slot.actionBtnText = EconomyHUD.CreateText("Label", btnRect, 13f, Color.white, TextAlignmentOptions.Center);
        slot.actionBtnText.rectTransform.anchorMin = Vector2.zero;
        slot.actionBtnText.rectTransform.anchorMax = Vector2.one;
        slot.actionBtnText.rectTransform.offsetMin = slot.actionBtnText.rectTransform.offsetMax = Vector2.zero;
        slot.actionBtnText.text = "Get Lumi";
        slot.actionBtnText.fontStyle = FontStyles.Bold;

        int capturedIndex = index;
        slot.actionBtn.onClick.AddListener(() => OnChamberButtonClick(capturedIndex));

        // Lock overlay: just the lock icon and text floating over the locked tube
        // No dark rectangle image background!
        slot.lockOverlay = EconomyHUD.CreateRect("LockOverlay", podRect).gameObject;
        RectTransform loRect = (RectTransform)slot.lockOverlay.transform;
        EconomyHUD.Stretch(loRect);

        // Lock Icon sprite
        RectTransform lockIconRect = EconomyHUD.CreateRect("LockIcon", loRect);
        lockIconRect.anchorMin = lockIconRect.anchorMax = lockIconRect.pivot = new Vector2(0.5f, 0.55f);
        lockIconRect.sizeDelta = new Vector2(44f, 44f);
        Image lockIconImg = lockIconRect.gameObject.AddComponent<Image>();
        Sprite lockSprite = LoadSpriteSafe("PreserveLockIcon");
        if (lockSprite != null)
        {
            lockIconImg.sprite = lockSprite;
            lockIconImg.preserveAspect = true;
            lockIconImg.color = new Color(0.95f, 0.82f, 0.35f, 1f); // golden
        }

        TextMeshProUGUI loText = EconomyHUD.CreateText("LockText", loRect, 13f, Color.white, TextAlignmentOptions.Center);
        loText.rectTransform.anchorMin = new Vector2(0f, 0.30f);
        loText.rectTransform.anchorMax = new Vector2(1f, 0.45f);
        loText.rectTransform.offsetMin = loText.rectTransform.offsetMax = Vector2.zero;
        loText.text = "LOCKED";
        loText.fontStyle = FontStyles.Bold;

        slot.lockOverlay.SetActive(false);

        return slot;
    }

    // ─────────────────────── View 1: Selection ────────────────────────────────
    private GameObject BuildSelectionView(RectTransform parent)
    {
        RectTransform root = EconomyHUD.CreateRect("SelectionView", parent);
        EconomyHUD.Stretch(root);

        // Top navigation bar (Back button)
        RectTransform navBar = EconomyHUD.CreateRect("NavBar", root);
        navBar.anchorMin = new Vector2(0f, 1f);
        navBar.anchorMax = new Vector2(1f, 1f);
        navBar.pivot = new Vector2(0.5f, 1f);
        navBar.anchoredPosition = new Vector2(0f, -4f);
        navBar.sizeDelta = new Vector2(0f, 36f);

        RectTransform backRect = EconomyHUD.CreateRect("BackBtn", navBar);
        backRect.anchorMin = backRect.anchorMax = backRect.pivot = new Vector2(0f, 0.5f);
        backRect.anchoredPosition = new Vector2(16f, 0f);
        backRect.sizeDelta = new Vector2(160f, 32f);

        Button backBtn = EconomyHUD.AddButton(backRect, Color.white);
        TextMeshProUGUI backLbl = EconomyHUD.CreateText("Label", backRect, 13f, ThemeBlueDark, TextAlignmentOptions.Center);
        backLbl.rectTransform.anchorMin = Vector2.zero;
        backLbl.rectTransform.anchorMax = Vector2.one;
        backLbl.rectTransform.offsetMin = backLbl.rectTransform.offsetMax = Vector2.zero;
        backLbl.text = "<- Back to Chambers";
        backLbl.fontStyle = FontStyles.Bold;
        backBtn.onClick.AddListener(() => ShowView(0));

        // Two-column split below navbar
        RectTransform body = EconomyHUD.CreateRect("Body", root);
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(16f, 16f);
        body.offsetMax = new Vector2(-16f, -44f);

        // Left Column (56% width) - 8 Biome Tabs + Hand Card Grid
        RectTransform leftCol = EconomyHUD.CreateRect("LeftCol", body);
        leftCol.anchorMin = new Vector2(0f, 0f);
        leftCol.anchorMax = new Vector2(0.55f, 1f);
        leftCol.offsetMin = leftCol.offsetMax = Vector2.zero;
        EconomyHUD.CreateBorderedPanel(leftCol, CardBg, ThemeBlue);

        Build8BiomeTabs(leftCol);
        selCardGrid = BuildScrollGrid("CardGrid", leftCol, new RectOffset(10, 10, 48, 10));

        // Right Column (43% width) - 3D Braluff Preview & Parent Avatars & Evolve
        RectTransform rightCol = EconomyHUD.CreateRect("RightCol", body);
        rightCol.anchorMin = new Vector2(0.57f, 0f);
        rightCol.anchorMax = new Vector2(1f, 1f);
        rightCol.offsetMin = rightCol.offsetMax = Vector2.zero;
        EconomyHUD.CreateBorderedPanel(rightCol, CardBg, ThemeBlue);

        BuildSelectionPreview(rightCol);

        root.gameObject.SetActive(false);
        return root.gameObject;
    }

    /// <summary>
    /// Builds the 8 selection tabs:
    ///   Tab 0: All (loads all creature cards on hand)
    ///   Tabs 1-7: Bloomfield to Windheath with biome icons
    /// </summary>
    private void Build8BiomeTabs(RectTransform parent)
    {
        RectTransform bar = EconomyHUD.CreateRect("TabsBar", parent);
        bar.anchorMin = new Vector2(0f, 1f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(0.5f, 1f);
        bar.anchoredPosition = new Vector2(0f, -6f);
        bar.sizeDelta = new Vector2(0f, 36f);

        HorizontalLayoutGroup hlg = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 6f;
        hlg.padding = new RectOffset(8, 8, 0, 0);
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        biomeTabButtons.Clear();

        // 1. "All" text tab
        RectTransform allRect = EconomyHUD.CreateRect("Tab_All", bar);
        allRect.sizeDelta = new Vector2(52f, 32f);
        Button allBtn = EconomyHUD.AddButton(allRect, ThemeBlue);
        TextMeshProUGUI allTxt = EconomyHUD.CreateText("Label", allRect, 12f, Color.white, TextAlignmentOptions.Center);
        allTxt.rectTransform.anchorMin = Vector2.zero;
        allTxt.rectTransform.anchorMax = Vector2.one;
        allTxt.rectTransform.offsetMin = allTxt.rectTransform.offsetMax = Vector2.zero;
        allTxt.text = "All";
        allTxt.fontStyle = FontStyles.Bold;
        allBtn.onClick.AddListener(() => SwitchBiomeTab("All"));
        biomeTabButtons.Add(allBtn);

        // 2-8. 7 Biome icon tabs (Bloomfield to Windheath)
        foreach (string biome in BiomeNames)
        {
            string capBiome = biome;
            RectTransform tabRect = EconomyHUD.CreateRect($"Tab_{capBiome}", bar);
            tabRect.sizeDelta = new Vector2(40f, 32f);

            Button btn = EconomyHUD.AddButton(tabRect, Color.white);

            // Icon inside button
            RectTransform iconRect = EconomyHUD.CreateRect("Icon", tabRect);
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(26f, 26f);

            Image iconImg = iconRect.gameObject.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            Sprite s = LoadSpriteSafe("UI_Icon_card/" + capBiome);
            if (s != null)
            {
                iconImg.sprite = s;
                iconImg.color = Color.white;
            }
            else
            {
                iconImg.color = Color.clear;
            }

            btn.onClick.AddListener(() => SwitchBiomeTab(capBiome));
            biomeTabButtons.Add(btn);
        }
    }

    private void BuildSelectionPreview(RectTransform parent)
    {
        // Title
        selTitleText = EconomyHUD.CreateText("PreviewTitle", parent, 15f, ThemeBlueDark, TextAlignmentOptions.Center);
        selTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        selTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        selTitleText.rectTransform.pivot = new Vector2(0.5f, 1f);
        selTitleText.rectTransform.anchoredPosition = new Vector2(0f, -10f);
        selTitleText.rectTransform.sizeDelta = new Vector2(0f, 26f);
        selTitleText.text = "Select 2 parent creatures";
        selTitleText.fontStyle = FontStyles.Bold;

        // Large 3D Hybrid Model Display Container (top 46%)
        RectTransform offBox = EconomyHUD.CreateRect("OffspringBox", parent);
        offBox.anchorMin = new Vector2(0.08f, 0.44f);
        offBox.anchorMax = new Vector2(0.92f, 0.90f);
        offBox.offsetMin = offBox.offsetMax = Vector2.zero;
        EconomyHUD.CreateBorderedPanel(offBox, new Color(0.96f, 0.98f, 1f), ThemeBlue);

        // 2D Fallback Image
        RectTransform artRect = EconomyHUD.CreateRect("Art2D", offBox);
        artRect.anchorMin = new Vector2(0.08f, 0.18f);
        artRect.anchorMax = new Vector2(0.92f, 0.92f);
        artRect.offsetMin = artRect.offsetMax = Vector2.zero;
        selHybrid2DImage = artRect.gameObject.AddComponent<Image>();
        selHybrid2DImage.preserveAspect = true;
        selHybrid2DImage.color = Color.clear;

        // 3D Model RawImage (URP Render-to-Texture from CreatureModelPreviewer)
        RectTransform rawRect = EconomyHUD.CreateRect("Art3D", offBox);
        rawRect.anchorMin = new Vector2(0.05f, 0.15f);
        rawRect.anchorMax = new Vector2(0.95f, 0.95f);
        rawRect.offsetMin = rawRect.offsetMax = Vector2.zero;
        selHybrid3DRawImage = rawRect.gameObject.AddComponent<RawImage>();
        selHybrid3DRawImage.color = Color.white;
        var offArf = rawRect.gameObject.AddComponent<AspectRatioFitter>();
        offArf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        offArf.aspectRatio = 1f;
        selHybrid3DRawImage.gameObject.SetActive(false);

        // Coming Soon text when non-working hybrid is selected
        selComingSoonText = EconomyHUD.CreateText("ComingSoonText", offBox, 20f, new Color(0.35f, 0.45f, 0.60f, 1f), TextAlignmentOptions.Center);
        selComingSoonText.rectTransform.anchorMin = new Vector2(0.05f, 0.22f);
        selComingSoonText.rectTransform.anchorMax = new Vector2(0.95f, 0.88f);
        selComingSoonText.rectTransform.offsetMin = selComingSoonText.rectTransform.offsetMax = Vector2.zero;
        selComingSoonText.text = "Coming Soon";
        selComingSoonText.fontStyle = FontStyles.Bold;
        selComingSoonText.gameObject.SetActive(false);

        // Add Drag Rotate handler to 3D box
        var dragTrigger = offBox.gameObject.AddComponent<EventTrigger>();
        var dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        dragEntry.callback.AddListener((data) => {
            var ped = (PointerEventData)data;
            CreatureModelPreviewer.Instance?.OnDragRotate(ped.delta.x);
        });
        dragTrigger.triggers.Add(dragEntry);
        var endDragEntry = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
        endDragEntry.callback.AddListener((data) => {
            CreatureModelPreviewer.Instance?.OnEndDragRotate();
        });
        dragTrigger.triggers.Add(endDragEntry);

        // Offspring Name
        selHybridName = EconomyHUD.CreateText("Name", offBox, 16f, TextDark, TextAlignmentOptions.Center);
        selHybridName.rectTransform.anchorMin = new Vector2(0f, 0f);
        selHybridName.rectTransform.anchorMax = new Vector2(1f, 0.18f);
        selHybridName.rectTransform.offsetMin = selHybridName.rectTransform.offsetMax = Vector2.zero;
        selHybridName.text = "—";
        selHybridName.fontStyle = FontStyles.Bold;

        // Parents avatars row
        RectTransform parentsRow = EconomyHUD.CreateRect("ParentsRow", parent);
        parentsRow.anchorMin = new Vector2(0.08f, 0.22f);
        parentsRow.anchorMax = new Vector2(0.92f, 0.40f);
        parentsRow.offsetMin = parentsRow.offsetMax = Vector2.zero;

        // Parent A avatar
        selParentAAvatar = BuildParentSlot(parentsRow, "ParentA", new Vector2(0f, 0f), new Vector2(0.42f, 1f), "Parent 1", CreatureModelPreviewer.SlotParentA, out selParentA3DRawImage, out selParentAName);

        // "+" sign
        TextMeshProUGUI plusSign = EconomyHUD.CreateText("Plus", parentsRow, 22f, ThemeBlue, TextAlignmentOptions.Center);
        plusSign.rectTransform.anchorMin = new Vector2(0.44f, 0f);
        plusSign.rectTransform.anchorMax = new Vector2(0.56f, 1f);
        plusSign.rectTransform.offsetMin = plusSign.rectTransform.offsetMax = Vector2.zero;
        plusSign.text = "+";
        plusSign.fontStyle = FontStyles.Bold;

        // Parent B avatar
        selParentBAvatar = BuildParentSlot(parentsRow, "ParentB", new Vector2(0.58f, 0f), new Vector2(1f, 1f), "Parent 2", CreatureModelPreviewer.SlotParentB, out selParentB3DRawImage, out selParentBName);

        // Duration text
        evolveDurationText = EconomyHUD.CreateText("Duration", parent, 12f, TextMuted, TextAlignmentOptions.Center);
        evolveDurationText.rectTransform.anchorMin = new Vector2(0.05f, 0.12f);
        evolveDurationText.rectTransform.anchorMax = new Vector2(0.95f, 0.18f);
        evolveDurationText.rectTransform.offsetMin = evolveDurationText.rectTransform.offsetMax = Vector2.zero;
        evolveDurationText.text = "";

        // Evolve Button (Pure text EVOLVE -> NO □)
        RectTransform evBtnRect = EconomyHUD.CreateRect("EvolveBtn", parent);
        evBtnRect.anchorMin = new Vector2(0.12f, 0.03f);
        evBtnRect.anchorMax = new Vector2(0.88f, 0.11f);
        evBtnRect.offsetMin = evBtnRect.offsetMax = Vector2.zero;

        evolveButton = EconomyHUD.AddButton(evBtnRect, ThemeBlue);
        TextMeshProUGUI evLbl = EconomyHUD.CreateText("Label", evBtnRect, 15f, Color.white, TextAlignmentOptions.Center);
        evLbl.rectTransform.anchorMin = Vector2.zero;
        evLbl.rectTransform.anchorMax = Vector2.one;
        evLbl.rectTransform.offsetMin = evLbl.rectTransform.offsetMax = Vector2.zero;
        evLbl.text = "EVOLVE";
        evLbl.fontStyle = FontStyles.Bold;
        evolveButton.interactable = false;

        evolveButton.onClick.AddListener(OnEvolveClicked);
    }

    private Image BuildParentSlot(RectTransform parent, string name, Vector2 min, Vector2 max, string placeholder, int previewSlotIndex, out RawImage raw3DImg, out TextMeshProUGUI nameLabel)
    {
        RectTransform slot = EconomyHUD.CreateRect(name, parent);
        slot.anchorMin = min;
        slot.anchorMax = max;
        slot.offsetMin = slot.offsetMax = Vector2.zero;
        Image slotBg = EconomyHUD.CreateBorderedPanel(slot, new Color(0.92f, 0.96f, 1f), ThemeBlue);
        slotBg.raycastTarget = true;

        RectTransform imgRect = EconomyHUD.CreateRect("Avatar", slot);
        imgRect.anchorMin = new Vector2(0.08f, 0.22f);
        imgRect.anchorMax = new Vector2(0.92f, 0.94f);
        imgRect.offsetMin = imgRect.offsetMax = Vector2.zero;

        Image img = imgRect.gameObject.AddComponent<Image>();
        img.preserveAspect = true;
        img.color = Color.clear;
        img.raycastTarget = false;

        RectTransform rawRect = EconomyHUD.CreateRect("Avatar3D", slot);
        rawRect.anchorMin = new Vector2(0.04f, 0.20f);
        rawRect.anchorMax = new Vector2(0.96f, 0.96f);
        rawRect.offsetMin = rawRect.offsetMax = Vector2.zero;

        raw3DImg = rawRect.gameObject.AddComponent<RawImage>();
        raw3DImg.color = Color.white;
        raw3DImg.raycastTarget = false;
        var parentArf = rawRect.gameObject.AddComponent<AspectRatioFitter>();
        parentArf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        parentArf.aspectRatio = 1f;
        raw3DImg.gameObject.SetActive(false);

        int capturedSlot = previewSlotIndex;
        var dragTrigger = slot.gameObject.AddComponent<EventTrigger>();
        var dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        dragEntry.callback.AddListener((data) => {
            var ped = (PointerEventData)data;
            CreatureModelPreviewer.Instance?.OnDragRotateSlot(capturedSlot, ped.delta.x);
        });
        dragTrigger.triggers.Add(dragEntry);
        var endDragEntry = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
        endDragEntry.callback.AddListener((data) => {
            CreatureModelPreviewer.Instance?.OnEndDragRotateSlot(capturedSlot);
        });
        dragTrigger.triggers.Add(endDragEntry);

        nameLabel = EconomyHUD.CreateText("Label", slot, 10f, TextDark, TextAlignmentOptions.Center);
        nameLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        nameLabel.rectTransform.anchorMax = new Vector2(1f, 0.22f);
        nameLabel.rectTransform.offsetMin = nameLabel.rectTransform.offsetMax = Vector2.zero;
        nameLabel.text = placeholder;

        return img;
    }

    // ─────────────────────── View 2: Incubation ───────────────────────────────
    private GameObject BuildIncubationView(RectTransform parent)
    {
        RectTransform root = EconomyHUD.CreateRect("IncubationView", parent);
        EconomyHUD.Stretch(root);

        // Top navigation bar
        RectTransform navBar = EconomyHUD.CreateRect("NavBar", root);
        navBar.anchorMin = new Vector2(0f, 1f);
        navBar.anchorMax = new Vector2(1f, 1f);
        navBar.pivot = new Vector2(0.5f, 1f);
        navBar.anchoredPosition = new Vector2(0f, -4f);
        navBar.sizeDelta = new Vector2(0f, 36f);

        RectTransform backRect = EconomyHUD.CreateRect("BackBtn", navBar);
        backRect.anchorMin = backRect.anchorMax = backRect.pivot = new Vector2(0f, 0.5f);
        backRect.anchoredPosition = new Vector2(16f, 0f);
        backRect.sizeDelta = new Vector2(160f, 32f);

        Button backBtn = EconomyHUD.AddButton(backRect, Color.white);
        TextMeshProUGUI backLbl = EconomyHUD.CreateText("Label", backRect, 13f, ThemeBlueDark, TextAlignmentOptions.Center);
        backLbl.rectTransform.anchorMin = Vector2.zero;
        backLbl.rectTransform.anchorMax = Vector2.one;
        backLbl.rectTransform.offsetMin = backLbl.rectTransform.offsetMax = Vector2.zero;
        backLbl.text = "<- Back to Chambers";
        backLbl.fontStyle = FontStyles.Bold;
        backBtn.onClick.AddListener(() => ShowView(0));

        // 3 Columns Container
        RectTransform body = EconomyHUD.CreateRect("Body", root);
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(20f, 20f);
        body.offsetMax = new Vector2(-20f, -44f);

        // Left Tube Column (Father) - 28%
        RectTransform leftCol = EconomyHUD.CreateRect("FatherCol", body);
        leftCol.anchorMin = new Vector2(0f, 0f);
        leftCol.anchorMax = new Vector2(0.28f, 1f);
        leftCol.offsetMin = leftCol.offsetMax = Vector2.zero;
        EconomyHUD.CreateBorderedPanel(leftCol, ChamberOuterBg, ThemeCyanBorder);
        BuildIncubatorColumn(leftCol, "Father", out incubFather3DRawImg, out incubFatherImg, out incubFatherName);

        // Right Tube Column (Mother) - 28%
        RectTransform rightCol = EconomyHUD.CreateRect("MotherCol", body);
        rightCol.anchorMin = new Vector2(0.72f, 0f);
        rightCol.anchorMax = new Vector2(1f, 1f);
        rightCol.offsetMin = rightCol.offsetMax = Vector2.zero;
        EconomyHUD.CreateBorderedPanel(rightCol, ChamberOuterBg, ThemeCyanBorder);
        BuildIncubatorColumn(rightCol, "Mother", out incubMother3DRawImg, out incubMotherImg, out incubMotherName);

        // Center Column (Offspring + Progress + Speed up) - 40%
        RectTransform centerCol = EconomyHUD.CreateRect("CenterCol", body);
        centerCol.anchorMin = new Vector2(0.31f, 0f);
        centerCol.anchorMax = new Vector2(0.69f, 1f);
        centerCol.offsetMin = centerCol.offsetMax = Vector2.zero;
        EconomyHUD.CreateBorderedPanel(centerCol, CardBg, ThemeBlue);

        // Title
        TextMeshProUGUI centerTitle = EconomyHUD.CreateText("Title", centerCol, 17f, ThemeBlueDark, TextAlignmentOptions.Center);
        centerTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
        centerTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
        centerTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
        centerTitle.rectTransform.anchoredPosition = new Vector2(0f, -10f);
        centerTitle.rectTransform.sizeDelta = new Vector2(0f, 28f);
        centerTitle.text = "HYBRID IN PROGRESS";
        centerTitle.fontStyle = FontStyles.Bold;

        // Offspring display container (with 3D and 2D support)
        RectTransform offArtRect = EconomyHUD.CreateRect("OffspringArt", centerCol);
        offArtRect.anchorMin = new Vector2(0.12f, 0.46f);
        offArtRect.anchorMax = new Vector2(0.88f, 0.88f);
        offArtRect.offsetMin = offArtRect.offsetMax = Vector2.zero;

        incubOffspringImg = offArtRect.gameObject.AddComponent<Image>();
        incubOffspringImg.preserveAspect = true;
        incubOffspringImg.color = Color.clear;

        RectTransform off3DRect = EconomyHUD.CreateRect("Offspring3D", offArtRect);
        EconomyHUD.Stretch(off3DRect);
        incubOffspring3DRawImg = off3DRect.gameObject.AddComponent<RawImage>();
        incubOffspring3DRawImg.color = Color.white;
        var incubOffArf = off3DRect.gameObject.AddComponent<AspectRatioFitter>();
        incubOffArf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        incubOffArf.aspectRatio = 1f;
        incubOffspring3DRawImg.gameObject.SetActive(false);

        // Offspring name
        incubOffspringName = EconomyHUD.CreateText("OffspringName", centerCol, 16f, TextDark, TextAlignmentOptions.Center);
        incubOffspringName.rectTransform.anchorMin = new Vector2(0.05f, 0.38f);
        incubOffspringName.rectTransform.anchorMax = new Vector2(0.95f, 0.45f);
        incubOffspringName.rectTransform.offsetMin = incubOffspringName.rectTransform.offsetMax = Vector2.zero;
        incubOffspringName.text = "Braluff";
        incubOffspringName.fontStyle = FontStyles.Bold;

        // Timer remaining
        incubTimerText = EconomyHUD.CreateText("Timer", centerCol, 14f, ThemeBlue, TextAlignmentOptions.Center);
        incubTimerText.rectTransform.anchorMin = new Vector2(0.05f, 0.31f);
        incubTimerText.rectTransform.anchorMax = new Vector2(0.95f, 0.37f);
        incubTimerText.rectTransform.offsetMin = incubTimerText.rectTransform.offsetMax = Vector2.zero;
        incubTimerText.text = "02:00 remaining";

        // Progress bar
        RectTransform barRect = EconomyHUD.CreateRect("ProgressBar", centerCol);
        barRect.anchorMin = new Vector2(0.08f, 0.24f);
        barRect.anchorMax = new Vector2(0.92f, 0.29f);
        barRect.offsetMin = barRect.offsetMax = Vector2.zero;

        incubProgressBar = barRect.gameObject.AddComponent<Slider>();
        incubProgressBar.minValue = 0f;
        incubProgressBar.maxValue = 1f;
        incubProgressBar.value = 0f;
        incubProgressBar.interactable = false;
        BuildSliderVisuals(incubProgressBar, barRect);

        // Speed up buttons (no unicode emoji)
        // Button 1: Coins
        RectTransform coinBtnRect = EconomyHUD.CreateRect("SpeedUpCoinsBtn", centerCol);
        coinBtnRect.anchorMin = new Vector2(0.08f, 0.12f);
        coinBtnRect.anchorMax = new Vector2(0.92f, 0.21f);
        coinBtnRect.offsetMin = coinBtnRect.offsetMax = Vector2.zero;

        speedUpCoinsBtn = EconomyHUD.AddButton(coinBtnRect, CoinGold);
        TextMeshProUGUI coinLbl = EconomyHUD.CreateText("Label", coinBtnRect, 13f, Color.white, TextAlignmentOptions.Center);
        coinLbl.rectTransform.anchorMin = Vector2.zero;
        coinLbl.rectTransform.anchorMax = Vector2.one;
        coinLbl.rectTransform.offsetMin = coinLbl.rectTransform.offsetMax = Vector2.zero;
        coinLbl.text = "Finish Now (50 Coins)";
        coinLbl.fontStyle = FontStyles.Bold;
        speedUpCoinsBtn.onClick.AddListener(OnSpeedUpWithCoinsClicked);

        // Button 2: Ad
        RectTransform adBtnRect = EconomyHUD.CreateRect("SpeedUpAdBtn", centerCol);
        adBtnRect.anchorMin = new Vector2(0.08f, 0.02f);
        adBtnRect.anchorMax = new Vector2(0.92f, 0.10f);
        adBtnRect.offsetMin = adBtnRect.offsetMax = Vector2.zero;

        speedUpAdBtn = EconomyHUD.AddButton(adBtnRect, GreenProgress);
        TextMeshProUGUI adLbl = EconomyHUD.CreateText("Label", adBtnRect, 13f, Color.white, TextAlignmentOptions.Center);
        adLbl.rectTransform.anchorMin = Vector2.zero;
        adLbl.rectTransform.anchorMax = Vector2.one;
        adLbl.rectTransform.offsetMin = adLbl.rectTransform.offsetMax = Vector2.zero;
        adLbl.text = "Watch Ad (-30% Time)";
        adLbl.fontStyle = FontStyles.Bold;
        speedUpAdBtn.onClick.AddListener(OnSpeedUpWithAdClicked);

        root.gameObject.SetActive(false);
        return root.gameObject;
    }

    private void BuildIncubatorColumn(RectTransform parent, string role, out RawImage creature3DImg, out Image creatureImg, out TextMeshProUGUI creatureName)
    {
        // Dark Pod Background (no Father/Mother header label at top)
        RectTransform podRect = EconomyHUD.CreateRect("Pod", parent);
        podRect.anchorMin = new Vector2(0.08f, 0.16f);
        podRect.anchorMax = new Vector2(0.92f, 0.94f);
        podRect.offsetMin = podRect.offsetMax = Vector2.zero;
        EconomyHUD.CreateBorderedPanel(podRect, ChamberPodBg, new Color(0.25f, 0.55f, 0.85f, 0.5f));

        // Tube background
        RectTransform tubeRect = EconomyHUD.CreateRect("Tube", podRect);
        tubeRect.anchorMin = new Vector2(0.05f, 0.05f);
        tubeRect.anchorMax = new Vector2(0.95f, 0.95f);
        tubeRect.offsetMin = tubeRect.offsetMax = Vector2.zero;
        Image tubeImg = tubeRect.gameObject.AddComponent<Image>();
        Sprite tubeSprite = LoadSpriteSafe("UI_Breeding/tube_active");
        if (tubeSprite != null)
        {
            tubeImg.sprite = tubeSprite;
            tubeImg.preserveAspect = true;
        }
        else
        {
            tubeImg.color = Color.clear;
        }

        // Inside tube: Creature 2D fallback image
        RectTransform cImgRect = EconomyHUD.CreateRect("CreatureImg", tubeRect);
        cImgRect.anchorMin = new Vector2(0.12f, 0.12f);
        cImgRect.anchorMax = new Vector2(0.88f, 0.88f);
        cImgRect.offsetMin = cImgRect.offsetMax = Vector2.zero;
        creatureImg = cImgRect.gameObject.AddComponent<Image>();
        creatureImg.preserveAspect = true;
        creatureImg.color = Color.clear;

        // Inside tube: Creature 3D preview RawImage (1:1 AspectRatioFitter, sized inside glass walls)
        RectTransform c3DRect = EconomyHUD.CreateRect("Creature3D", tubeRect);
        c3DRect.anchorMin = new Vector2(0.18f, 0.20f);
        c3DRect.anchorMax = new Vector2(0.82f, 0.80f);
        c3DRect.offsetMin = c3DRect.offsetMax = Vector2.zero;
        creature3DImg = c3DRect.gameObject.AddComponent<RawImage>();
        creature3DImg.color = Color.white;
        creature3DImg.raycastTarget = false;
        var arf = c3DRect.gameObject.AddComponent<AspectRatioFitter>();
        arf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        arf.aspectRatio = 1f;
        creature3DImg.gameObject.SetActive(false);

        // Creature name at bottom
        creatureName = EconomyHUD.CreateText("Name", parent, 14f, TextLight, TextAlignmentOptions.Center);
        creatureName.rectTransform.anchorMin = new Vector2(0f, 0.03f);
        creatureName.rectTransform.anchorMax = new Vector2(1f, 0.15f);
        creatureName.rectTransform.offsetMin = creatureName.rectTransform.offsetMax = Vector2.zero;
        creatureName.text = "—";
        creatureName.fontStyle = FontStyles.Bold;
    }

    // ─────────────────────── Congrats Popup ──────────────────────────────────
    private void BuildCongratsPopup(RectTransform parent)
    {
        RectTransform popRect = EconomyHUD.CreateRect("CongratsPopup", parent);
        popRect.anchorMin = popRect.anchorMax = popRect.pivot = new Vector2(0.5f, 0.5f);
        popRect.sizeDelta = new Vector2(480f, 360f);
        EconomyHUD.CreateBorderedPanel(popRect, CardBg, ThemeBlue).raycastTarget = true;
        congratsPopup = popRect.gameObject;

        congratsTitle = EconomyHUD.CreateText("Title", popRect, 20f, ThemeBlue, TextAlignmentOptions.Center);
        congratsTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
        congratsTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
        congratsTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
        congratsTitle.rectTransform.anchoredPosition = new Vector2(0f, -14f);
        congratsTitle.rectTransform.sizeDelta = new Vector2(0f, 30f);
        congratsTitle.text = "CONGRATULATIONS!";
        congratsTitle.fontStyle = FontStyles.Bold;

        congratsSub = EconomyHUD.CreateText("Subtitle", popRect, 13f, TextDark, TextAlignmentOptions.Center);
        congratsSub.rectTransform.anchorMin = new Vector2(0.05f, 0.72f);
        congratsSub.rectTransform.anchorMax = new Vector2(0.95f, 0.88f);
        congratsSub.rectTransform.offsetMin = congratsSub.rectTransform.offsetMax = Vector2.zero;
        congratsSub.text = "You successfully bred a new hybrid creature!";

        // Card image display (3 biome icons are already baked onto the top-left of the card art)
        RectTransform imgRect = EconomyHUD.CreateRect("CardImage", popRect);
        imgRect.anchorMin = new Vector2(0.28f, 0.17f);
        imgRect.anchorMax = new Vector2(0.72f, 0.71f);
        imgRect.offsetMin = imgRect.offsetMax = Vector2.zero;
        congratsCardImg = imgRect.gameObject.AddComponent<Image>();
        congratsCardImg.preserveAspect = true;

        // OK Button
        RectTransform okRect = EconomyHUD.CreateRect("ClaimBtn", popRect);
        okRect.anchorMin = new Vector2(0.20f, 0.04f);
        okRect.anchorMax = new Vector2(0.80f, 0.14f);
        okRect.offsetMin = okRect.offsetMax = Vector2.zero;

        Button okBtn = EconomyHUD.AddButton(okRect, ThemeBlue);
        TextMeshProUGUI okLbl = EconomyHUD.CreateText("Label", okRect, 14f, Color.white, TextAlignmentOptions.Center);
        okLbl.rectTransform.anchorMin = Vector2.zero;
        okLbl.rectTransform.anchorMax = Vector2.one;
        okLbl.rectTransform.offsetMin = okLbl.rectTransform.offsetMax = Vector2.zero;
        okLbl.text = "Claim Card to Hand";
        okLbl.fontStyle = FontStyles.Bold;

        okBtn.onClick.AddListener(() =>
        {
            congratsPopup.SetActive(false);
            pendingClaimOffspring = null;
            ShowView(0);
            RefreshOverview();
        });

        congratsPopup.SetActive(false);
    }

    // ─────────────────────── View Switching ──────────────────────────────────
    public void ShowView(int mode)
    {
        viewMode = mode;
        if (overviewView   != null) overviewView.SetActive(mode == 0);
        if (selectionView  != null) selectionView.SetActive(mode == 1);
        if (incubationView != null) incubationView.SetActive(mode == 2);

        if (mode == 0)
        {
            CreatureModelPreviewer.Instance?.ClearAllSlots();
            RefreshOverview();
        }
        else if (mode == 1)
        {
            selectedParentA = null;
            selectedParentB = null;
            foundRecipe = null;
            PopulateCardGrid(activeTab);
            RefreshSelectionPreview();
        }
        else if (mode == 2)
        {
            RefreshIncubationView();
        }
    }

    // ─────────────────────── Refresh Logic ────────────────────────────────────
    private void RefreshOverview()
    {
        if (chamberWidgets == null) return;
        for (int i = 0; i < chamberWidgets.Length; i++)
            RefreshChamberSlot(i);
    }

    private void RefreshOverviewTimers()
    {
        if (chamberWidgets == null || BreedingManager.Instance == null) return;
        for (int i = 0; i < chamberWidgets.Length; i++)
        {
            var ch = BreedingManager.Instance.GetChamber(i);
            if (ch != null && ch.IsBusy)
            {
                var w = chamberWidgets[i];
                if (w != null)
                {
                    w.statusText.text = $"Incubating... {ch.FormatTimeRemaining()}";
                    if (w.progressBar != null) w.progressBar.value = ch.progress;
                }
            }
        }
    }

    private void RefreshChamberSlot(int i)
    {
        if (chamberWidgets == null || i >= chamberWidgets.Length) return;
        var w = chamberWidgets[i];
        if (w == null) return;

        var ch = BreedingManager.Instance != null ? BreedingManager.Instance.GetChamber(i) : null;
        bool unlocked = BreedingManager.Instance != null && BreedingManager.Instance.IsChamberUnlocked(i);

        w.lockOverlay?.SetActive(!unlocked);

        // Load tube sprite safely
        Sprite activeSprite = LoadSpriteSafe("UI_Breeding/tube_active");
        Sprite lockedSprite = LoadSpriteSafe("UI_Breeding/tube_locked");
        Sprite targetSprite = unlocked ? activeSprite : lockedSprite;

        if (targetSprite != null)
        {
            w.tubeImage.sprite = targetSprite;
            w.tubeImage.color = Color.white;
        }
        else
        {
            w.tubeImage.color = Color.clear;
        }

        if (!unlocked)
        {
            w.statusText.text = "Locked";
            w.actionBtnText.text = "Locked";
            w.actionBtn.interactable = false;
            w.progressBarGo?.SetActive(false);
            if (w.creature3DImg != null) w.creature3DImg.gameObject.SetActive(false);
            if (w.creature2DImg != null) w.creature2DImg.color = Color.clear;
            return;
        }

        if (ch == null) return;

        switch (ch.status)
        {
            case BreedingChamberState.Status.Empty:
                w.statusText.text = "Empty";
                w.actionBtnText.text = "Get Lumi";
                w.actionBtn.interactable = true;
                w.progressBarGo?.SetActive(false);
                if (w.creature3DImg != null) w.creature3DImg.gameObject.SetActive(false);
                if (w.creature2DImg != null) w.creature2DImg.color = Color.clear;
                if (viewMode == 0) CreatureModelPreviewer.Instance?.ClearSlot(i);
                break;

            case BreedingChamberState.Status.Incubating:
                w.statusText.text = $"Incubating... {ch.FormatTimeRemaining()}";
                w.actionBtnText.text = "View";
                w.actionBtn.interactable = true;
                if (w.progressBar != null) w.progressBar.value = ch.progress;
                w.progressBarGo?.SetActive(true);
                ShowChamberTubeCreature(w, i, ch);
                break;

            case BreedingChamberState.Status.Done:
                w.statusText.text = "Ready!";
                w.actionBtnText.text = "Claim!";
                w.actionBtn.interactable = true;
                if (w.progressBar != null) w.progressBar.value = 1f;
                w.progressBarGo?.SetActive(true);
                ShowChamberTubeCreature(w, i, ch);
                break;
        }
    }

    private void ShowChamberTubeCreature(ChamberSlotWidget w, int slotIndex, BreedingChamberState ch)
    {
        if (w == null || ch == null) return;

        CardData offCard = ch.recipe?.offspring;
        string offName = offCard != null ? GetEnglishCreatureName(offCard) : "Braluff";

        if (viewMode == 0)
        {
            bool showed3D = TryShow3DPreviewInSlot(slotIndex, offName, offCard);
            if (showed3D && w.creature3DImg != null && CreatureModelPreviewer.Instance != null)
            {
                w.creature3DImg.texture = CreatureModelPreviewer.Instance.GetSlotTexture(slotIndex);
                w.creature3DImg.gameObject.SetActive(true);
                if (w.creature2DImg != null) w.creature2DImg.color = Color.clear;
                return;
            }
        }

        if (w.creature3DImg != null) w.creature3DImg.gameObject.SetActive(false);
        if (w.creature2DImg != null)
        {
            Sprite spr = offCard?.cardImage ?? LoadSpriteSafe("Breeding/Braluff") ?? LoadSpriteSafe("CreatureAvatars/Braluff");
            w.creature2DImg.sprite = spr;
            w.creature2DImg.color = (spr != null) ? Color.white : Color.clear;
        }
    }

    private void SwitchBiomeTab(string label)
    {
        activeTab = label;
        int activeIdx = (label == "All") ? 0 : Array.IndexOf(BiomeNames, label) + 1;

        for (int i = 0; i < biomeTabButtons.Count; i++)
        {
            var btn = biomeTabButtons[i];
            bool active = (i == activeIdx);

            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = active ? ThemeBlue : Color.white;
            }

            ColorBlock cb = btn.colors;
            cb.normalColor = active ? ThemeBlue : Color.white;
            cb.selectedColor = cb.normalColor;
            cb.highlightedColor = active ? ThemeBlueDark : new Color(0.92f, 0.95f, 1f, 1f);
            btn.colors = cb;

            var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.color = active ? Color.white : ThemeBlueDark;
            }
        }

        PopulateCardGrid(label);
    }

    /// <summary>
    /// Loads ONLY creature cards that the player currently owns on hand.
    /// </summary>
    private void PopulateCardGrid(string biomeFilter)
    {
        if (selCardGrid == null) return;

        // Clear existing items
        foreach (var g in cardGridItems)
            if (g != null) Destroy(g);
        cardGridItems.Clear();

        // 1. Gather ONLY creature cards currently on player's hand
        var allCreatureCards = new List<CardData>();
        var handCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var cardUIs = FindObjectsByType<CardUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (cardUIs != null && cardUIs.Length > 0)
        {
            foreach (var cui in cardUIs)
            {
                if (cui == null || cui.CardData == null) continue;
                if (cui.CardData.cardType != CardType.Creature) continue;
                if (cui.Count <= 0) continue;

                string key = cui.CardData.cardID ?? cui.CardData.name;
                if (!handCounts.ContainsKey(key))
                {
                    handCounts[key] = cui.Count;
                    allCreatureCards.Add(cui.CardData);
                }
                else
                {
                    handCounts[key] += cui.Count;
                }
            }
        }

        // If player has no creature cards on hand
        if (allCreatureCards.Count == 0)
        {
            RectTransform noticeRt = EconomyHUD.CreateRect("EmptyNotice", selCardGrid.transform);
            noticeRt.sizeDelta = new Vector2(280f, 60f);
            var noticeTxt = EconomyHUD.CreateText("EmptyTxt", noticeRt, 13f, TextMuted, TextAlignmentOptions.Center);
            noticeTxt.rectTransform.anchorMin = Vector2.zero;
            noticeTxt.rectTransform.anchorMax = Vector2.one;
            noticeTxt.rectTransform.offsetMin = noticeTxt.rectTransform.offsetMax = Vector2.zero;
            noticeTxt.text = "No creature cards in hand.\nObtain cards to breed!";
            cardGridItems.Add(noticeRt.gameObject);
            return;
        }

        // 2. Filter and create UI items
        int matchingCount = 0;
        foreach (var card in allCreatureCards)
        {
            if (card == null) continue;
            string key = card.cardID ?? card.name;
            int count = handCounts.TryGetValue(key, out int cnt) ? cnt : 1;

            if (biomeFilter != "All" && !MatchesBiome(card, biomeFilter))
                continue;

            var item = BuildCardGridItem(selCardGrid.transform, card, count);
            cardGridItems.Add(item);
            matchingCount++;
        }

        // If specific biome tab has no creatures in hand
        if (matchingCount == 0 && biomeFilter != "All")
        {
            RectTransform noticeRt = EconomyHUD.CreateRect("EmptyNotice", selCardGrid.transform);
            noticeRt.sizeDelta = new Vector2(280f, 60f);
            var noticeTxt = EconomyHUD.CreateText("EmptyTxt", noticeRt, 13f, TextMuted, TextAlignmentOptions.Center);
            noticeTxt.rectTransform.anchorMin = Vector2.zero;
            noticeTxt.rectTransform.anchorMax = Vector2.one;
            noticeTxt.rectTransform.offsetMin = noticeTxt.rectTransform.offsetMax = Vector2.zero;
            noticeTxt.text = $"No {biomeFilter} creatures in hand.";
            cardGridItems.Add(noticeRt.gameObject);
        }

        // Force immediate layout update
        if (selCardGrid != null)
        {
            var rt = selCardGrid.GetComponent<RectTransform>();
            if (rt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }

    private static bool MatchesBiome(CardData card, string biome)
    {
        if (card == null || string.IsNullOrEmpty(biome)) return true;

        string id = card.cardID?.ToLowerInvariant() ?? "";
        string nm = card.cardName?.ToLowerInvariant() ?? "";
        string assetNm = card.name?.ToLowerInvariant() ?? "";
        string sprNm = (card.cardImage != null ? card.cardImage.name : "").ToLowerInvariant();
        string eng = GetEnglishCreatureName(card).ToLowerInvariant();
        string combined = $"{id} {nm} {assetNm} {sprNm} {eng}";
        string bLower = biome.ToLowerInvariant();

        if (combined.Contains(bLower)) return true;

        bool isSpriglide  = combined.Contains("squirrel") || combined.Contains("spriglide") || combined.Contains("sóc") || combined.Contains("soc") || combined.Contains("01_");
        bool isBramblet   = combined.Contains("deer") || combined.Contains("bramblet") || combined.Contains("nai") || combined.Contains("02_");
        bool isFrondle    = combined.Contains("grasshopper") || combined.Contains("frondle") || combined.Contains("châu chấu") || combined.Contains("chau chau") || combined.Contains("03_");
        bool isPetaluff   = combined.Contains("caterpillar") || combined.Contains("petaluff") || combined.Contains("sâu") || combined.Contains("sau") || combined.Contains("04_");
        bool isDandrift   = combined.Contains("fox") || combined.Contains("dandrift") || combined.Contains("cáo") || combined.Contains("cao") || combined.Contains("05_");
        bool isGalume     = combined.Contains("peacock") || combined.Contains("galume") || combined.Contains("công") || combined.Contains("cong") || combined.Contains("06_");
        bool isHeathorn   = combined.Contains("cow") || combined.Contains("heathorn") || combined.Contains("bò") || combined.Contains("bo ") || combined.Contains("07_");
        bool isReedle     = combined.Contains("blossomfrog") || combined.Contains("frog") || combined.Contains("reedle") || combined.Contains("ếch") || combined.Contains("ech") || combined.Contains("08_");
        bool isCraggle    = combined.Contains("craggle") || combined.Contains("canoc") || combined.Contains("puffer") || combined.Contains("cá nóc") || combined.Contains("ca noc") || combined.Contains("09_") || combined.Contains("20_");
        bool isLumaqua    = combined.Contains("dragonfly") || combined.Contains("lumaqua") || combined.Contains("chuồn") || combined.Contains("chuon") || combined.Contains("10_");
        bool isSwirliggle = combined.Contains("swirliggle") || combined.Contains("tadpole") || combined.Contains("echduoica") || combined.Contains("11_");
        bool isMossback   = combined.Contains("capybara") || combined.Contains("mossback") || combined.Contains("12_");
        bool isVerdantail = combined.Contains("parrot") || combined.Contains("verdantail") || combined.Contains("vẹt") || combined.Contains("vet") || combined.Contains("13_");
        bool isBamboop    = combined.Contains("flowerdog") || combined.Contains("bamboop") || combined.Contains("chó") || combined.Contains("cho ") || combined.Contains("14_");
        bool isLotosleep  = combined.Contains("otter") || combined.Contains("lotosleep") || combined.Contains("rái cá") || combined.Contains("rai ca") || combined.Contains("15_");
        bool isDewdrop    = combined.Contains("swan") || combined.Contains("dewdrop") || combined.Contains("thiên nga") || combined.Contains("thien nga") || combined.Contains("16_");
        bool isBraluff    = combined.Contains("braluff") || combined.Contains("17_");
        bool isCow        = combined.Contains("cow") || combined.Contains("bò") || combined.Contains("bo ") || combined.Contains("18_");
        bool isFox        = combined.Contains("creature_fox") || combined.Contains("fox") || combined.Contains("cáo") || combined.Contains("cao") || combined.Contains("19_");

        // Dual-biome (and 3-biome hybrid) mappings matching exact card icons
        switch (bLower)
        {
            case "bloomfield":
                return isFrondle || isPetaluff || isDandrift || isGalume || isHeathorn || isDewdrop || isBraluff;
            case "leafwood":
                return isSpriglide || isBramblet || isFrondle || isPetaluff || isVerdantail || isBraluff || isCow;
            case "lilywater":
                return isLumaqua || isSwirliggle || isMossback || isLotosleep || isDewdrop || isCraggle;
            case "rainleaf":
                return isVerdantail || isBamboop || isLotosleep || isBramblet;
            case "reedmarsh":
                return isReedle || isCraggle || isMossback || isBamboop;
            case "streamstone":
                return isReedle || isCraggle || isLumaqua || isSwirliggle;
            case "windheath":
                return isSpriglide || isBramblet || isDandrift || isGalume || isHeathorn || isBraluff || isFox;
        }

        return false;
    }

    private GameObject BuildCardGridItem(Transform parent, CardData card, int count)
    {
        RectTransform go = EconomyHUD.CreateRect($"Card_{card.cardID}", parent);
        go.sizeDelta = new Vector2(74f, 105f);
        Image bgImg = EconomyHUD.CreateBorderedPanel(go, CardBg, ThemeBlue);
        bgImg.raycastTarget = true;

        // Art
        Sprite artSprite = card.cardImage;
        if (artSprite == null)
        {
            string engName = GetEnglishCreatureName(card);
            artSprite = LoadSpriteSafe("CreatureAvatars/" + engName)
                     ?? LoadSpriteSafe("CreatureAvatars/02_" + engName)
                     ?? LoadSpriteSafe("CreatureAvatars/04_" + engName)
                     ?? LoadSpriteSafe("AVT_ANM/" + engName)
                     ?? LoadSpriteSafe("CreatureAvatars/" + card.name)
                     ?? LoadSpriteSafe("CreatureAvatars/" + card.cardID);
        }

        if (artSprite != null)
        {
            RectTransform artRect = EconomyHUD.CreateRect("Art", go);
            artRect.anchorMin = new Vector2(0.06f, 0.28f);
            artRect.anchorMax = new Vector2(0.94f, 0.94f);
            artRect.offsetMin = artRect.offsetMax = Vector2.zero;
            Image art = artRect.gameObject.AddComponent<Image>();
            art.sprite = artSprite;
            art.preserveAspect = true;
            art.raycastTarget = false;
        }

        // Count Badge (e.g. x2)
        if (count > 0)
        {
            RectTransform badgeRect = EconomyHUD.CreateRect("CountBadge", go);
            badgeRect.anchorMin = badgeRect.anchorMax = badgeRect.pivot = new Vector2(1f, 1f);
            badgeRect.anchoredPosition = new Vector2(-2f, -2f);
            badgeRect.sizeDelta = new Vector2(24f, 16f);
            Image badgeBg = badgeRect.gameObject.AddComponent<Image>();
            badgeBg.color = ThemeBlueDark;
            badgeBg.raycastTarget = false;

            TextMeshProUGUI badgeTxt = EconomyHUD.CreateText("Txt", badgeRect, 9f, Color.white, TextAlignmentOptions.Center);
            badgeTxt.rectTransform.anchorMin = Vector2.zero;
            badgeTxt.rectTransform.anchorMax = Vector2.one;
            badgeTxt.rectTransform.offsetMin = badgeTxt.rectTransform.offsetMax = Vector2.zero;
            badgeTxt.text = $"x{count}";
            badgeTxt.fontStyle = FontStyles.Bold;
        }

        // English Name
        TextMeshProUGUI nameTxt = EconomyHUD.CreateText("Name", go, 9f, TextDark, TextAlignmentOptions.Center);
        nameTxt.rectTransform.anchorMin = Vector2.zero;
        nameTxt.rectTransform.anchorMax = new Vector2(1f, 0.26f);
        nameTxt.rectTransform.offsetMin = nameTxt.rectTransform.offsetMax = Vector2.zero;
        nameTxt.text = GetEnglishCreatureName(card);
        nameTxt.fontStyle = FontStyles.Bold;

        // Click button
        Button btn = go.gameObject.AddComponent<Button>();
        btn.targetGraphic = bgImg;
        btn.transition = Selectable.Transition.ColorTint;
        CardData capturedCard = card;
        btn.onClick.AddListener(() => OnCardSelected(capturedCard));

        return go.gameObject;
    }

    public static bool IsDeer(CardData card)
    {
        if (card == null) return false;
        string id = (card.cardID ?? "").ToLowerInvariant();
        string name = (card.cardName ?? "").ToLowerInvariant();
        string assetNm = (card.name ?? "").ToLowerInvariant();
        string sprNm = (card.cardImage != null ? card.cardImage.name : "").ToLowerInvariant();
        return id.Contains("deer") || id.Contains("bramblet") || name.Contains("nai") || name.Contains("bramblet")
            || assetNm.Contains("deer") || sprNm.Contains("bramblet") || sprNm.Contains("02_");
    }

    public static bool IsCaterpillar(CardData card)
    {
        if (card == null) return false;
        string id = (card.cardID ?? "").ToLowerInvariant();
        string name = (card.cardName ?? "").ToLowerInvariant();
        string assetNm = (card.name ?? "").ToLowerInvariant();
        string sprNm = (card.cardImage != null ? card.cardImage.name : "").ToLowerInvariant();
        return id.Contains("caterpillar") || id.Contains("petaluff") || name.Contains("sâu") || name.Contains("sau") || name.Contains("petaluff")
            || assetNm.Contains("caterpillar") || sprNm.Contains("petaluff") || sprNm.Contains("04_");
    }

    private CardData FindDeerCardInHand()
    {
        var cardUIs = FindObjectsByType<CardUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (cardUIs != null)
        {
            foreach (var c in cardUIs)
            {
                if (c != null && c.CardData != null && c.Count > 0 && IsDeer(c.CardData))
                    return c.CardData;
            }
        }
        return null;
    }

    private CardData FindCaterpillarCardInHand()
    {
        var cardUIs = FindObjectsByType<CardUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (cardUIs != null)
        {
            foreach (var c in cardUIs)
            {
                if (c != null && c.CardData != null && c.Count > 0 && IsCaterpillar(c.CardData))
                    return c.CardData;
            }
        }
        return null;
    }

    public static string GetEnglishCreatureName(CardData card)
    {
        if (card == null) return "—";
        string sprName = card.cardImage != null ? card.cardImage.name : "";
        return ToEnglishCreatureName(card.cardName, (card.cardID ?? "") + " " + (card.name ?? "") + " " + sprName);
    }

    public static string ToEnglishCreatureName(string name, string id = null)
    {
        string n = (name ?? "").Trim();
        string i = (id ?? "").ToLowerInvariant();

        if (n.Equals("Nai Rừng", StringComparison.OrdinalIgnoreCase) || n.Equals("Nai", StringComparison.OrdinalIgnoreCase) || i.Contains("deer") || i.Contains("bramblet") || n.Equals("Bramblet", StringComparison.OrdinalIgnoreCase))
            return "Bramblet";

        if (n.Equals("Sâu Bướm", StringComparison.OrdinalIgnoreCase) || n.Equals("Sâu", StringComparison.OrdinalIgnoreCase) || i.Contains("caterpillar") || i.Contains("petaluff") || n.Equals("Petaluff", StringComparison.OrdinalIgnoreCase))
            return "Petaluff";

        if (n.Equals("Braluff", StringComparison.OrdinalIgnoreCase) || i.Contains("braluff"))
            return "Braluff";

        if (n.Equals("Chuồn Chuồn", StringComparison.OrdinalIgnoreCase) || i.Contains("dragonfly") || i.Contains("lumaqua"))
            return "Dragonfly";

        if (n.Equals("Ếch Hoa", StringComparison.OrdinalIgnoreCase) || n.Equals("Ech Hoa", StringComparison.OrdinalIgnoreCase) || i.Contains("blossomfrog") || i.Contains("reedle") || i.Contains("frog"))
            return "Blossom Frog";

        if (n.Equals("Capybara", StringComparison.OrdinalIgnoreCase) || i.Contains("capybara") || i.Contains("mossback"))
            return "Capybara";

        if (n.Equals("Chó Hoa", StringComparison.OrdinalIgnoreCase) || n.Equals("Cho Hoa", StringComparison.OrdinalIgnoreCase) || i.Contains("flowerdog") || i.Contains("bamboop"))
            return "Flower Dog";

        if (n.Equals("Cáo Cỏ", StringComparison.OrdinalIgnoreCase) || n.Equals("Cao Co", StringComparison.OrdinalIgnoreCase) || i.Contains("grassfox") || i.Contains("dandrift"))
            return "Grass Fox";

        if (n.Equals("Cáo", StringComparison.OrdinalIgnoreCase) || n.Equals("Cao", StringComparison.OrdinalIgnoreCase) || i.Contains("fox"))
            return "Fox";

        if (n.Equals("Châu Chấu", StringComparison.OrdinalIgnoreCase) || n.Equals("Chau Chau", StringComparison.OrdinalIgnoreCase) || i.Contains("grasshopper") || i.Contains("frondle"))
            return "Grasshopper";

        if (n.Equals("Bò Lông Dài", StringComparison.OrdinalIgnoreCase) || n.Equals("Bo Long Dai", StringComparison.OrdinalIgnoreCase) || i.Contains("highlandcow") || i.Contains("heathorn"))
            return "Highland Cow";

        if (n.Equals("Bò Sữa", StringComparison.OrdinalIgnoreCase) || n.Equals("Bo Sua", StringComparison.OrdinalIgnoreCase) || n.Equals("Bò", StringComparison.OrdinalIgnoreCase) || i.Contains("cow"))
            return "Cow";

        if (n.Equals("Rái Cá", StringComparison.OrdinalIgnoreCase) || n.Equals("Rai Ca", StringComparison.OrdinalIgnoreCase) || i.Contains("otter") || i.Contains("lotosleep"))
            return "Otter";

        if (n.Equals("Chim Vẹt", StringComparison.OrdinalIgnoreCase) || n.Equals("Chim Vet", StringComparison.OrdinalIgnoreCase) || n.Equals("Vẹt", StringComparison.OrdinalIgnoreCase) || i.Contains("parrot") || i.Contains("verdantail"))
            return "Parrot";

        if (n.Equals("Chim Công", StringComparison.OrdinalIgnoreCase) || n.Equals("Chim Cong", StringComparison.OrdinalIgnoreCase) || n.Equals("Công", StringComparison.OrdinalIgnoreCase) || i.Contains("peacock") || i.Contains("galume"))
            return "Peacock";

        if (n.Equals("Sóc Bay", StringComparison.OrdinalIgnoreCase) || n.Equals("Soc Bay", StringComparison.OrdinalIgnoreCase) || i.Contains("flyingsquirrel") || i.Contains("squirrel") || i.Contains("spriglide"))
            return "Flying Squirrel";

        if (n.Equals("Thiên Nga", StringComparison.OrdinalIgnoreCase) || n.Equals("Thien Nga", StringComparison.OrdinalIgnoreCase) || i.Contains("swan") || i.Contains("dewdrop"))
            return "Swan";

        if (n.Equals("Cá Nóc", StringComparison.OrdinalIgnoreCase) || n.Equals("Ca Noc", StringComparison.OrdinalIgnoreCase) || i.Contains("canoc") || i.Contains("puffer") || i.Contains("craggle"))
            return "Pufferfish";

        if (!string.IsNullOrEmpty(name)) return name;
        if (!string.IsNullOrEmpty(id)) return id;
        return "Unknown";
    }

    private void OnCardSelected(CardData card)
    {
        if (card == null) return;
        GameAudioManager.Instance?.PlayClick();

        if (BreedingManager.Instance == null)
        {
            var bmGo = new GameObject("BreedingManager");
            bmGo.AddComponent<BreedingManager>();
        }

        bool isDeer = IsDeer(card);
        bool isCaterpillar = IsCaterpillar(card);

        if (isDeer || isCaterpillar)
        {
            // Only auto-pair if the player ACTUALLY OWNS BOTH Bramblet and Petaluff in hand!
            CardData deerCard = isDeer ? card : FindDeerCardInHand();
            CardData caterCard = isCaterpillar ? card : FindCaterpillarCardInHand();

            if (deerCard != null && caterCard != null)
            {
                selectedParentA = deerCard;
                selectedParentB = caterCard;
                foundRecipe = BreedingManager.Instance?.FindRecipe(deerCard, caterCard)
                           ?? BreedingManager.Instance?.FindRecipeForParent(card)
                           ?? BreedingManager.Instance?.FindRecipeForParent(deerCard)
                           ?? BreedingManager.Instance?.FindRecipeForParent(caterCard);

                if (foundRecipe == null)
                {
                    foundRecipe = ScriptableObject.CreateInstance<BreedingRecipe>();
                    foundRecipe.name = "recipe_braluff";
                    foundRecipe.parentA = deerCard;
                    foundRecipe.parentB = caterCard;
                    foundRecipe.incubationSeconds = 120f;
                    foundRecipe.offspring = BreedingManager.GetOrCreateBraluffCard();
                }
                else if (foundRecipe.offspring != null)
                {
                    BreedingManager.EnsureBraluffCardConfigured(foundRecipe.offspring);
                }
            }
            else
            {
                // Player only has 1 of the 2 parent cards in hand -> only show the owned card in Parent 1!
                selectedParentA = card;
                selectedParentB = null;
                foundRecipe = null;
            }
        }
        else
        {
            // Any other card: Parent 1 selected, Parent 2 empty, Top box "Coming Soon"
            selectedParentA = card;
            selectedParentB = null;
            foundRecipe = null;
        }

        RefreshSelectionPreview();
    }

    private void RefreshSelectionPreview()
    {
        if (selHybridName == null) return;

        if (foundRecipe != null && foundRecipe.offspring != null && selectedParentA != null && selectedParentB != null)
        {
            string offspringName = GetEnglishCreatureName(foundRecipe.offspring);
            if (string.IsNullOrEmpty(offspringName) || offspringName == "—") offspringName = "Braluff";

            selHybridName.text = offspringName;
            selTitleText.text = $"Offspring: {offspringName}";
            evolveDurationText.text = $"Incubation Duration: {FormatDuration(foundRecipe.incubationSeconds)}";
            evolveButton.interactable = true;
            if (selComingSoonText != null) selComingSoonText.gameObject.SetActive(false);

            // 1. Show 3D Model of Offspring (Braluff) in Slot 0 (OffspringBox)
            bool showed3D = TryShow3DPreviewInSlot(CreatureModelPreviewer.SlotMain, offspringName, foundRecipe.offspring);
            if (showed3D && CreatureModelPreviewer.Instance != null && CreatureModelPreviewer.Instance.GetSlotTexture(CreatureModelPreviewer.SlotMain) != null)
            {
                selHybrid3DRawImage.texture = CreatureModelPreviewer.Instance.GetSlotTexture(CreatureModelPreviewer.SlotMain);
                selHybrid3DRawImage.gameObject.SetActive(true);
                selHybrid2DImage.color = Color.clear;
            }
            else
            {
                selHybrid3DRawImage.gameObject.SetActive(false);
                Sprite fallback = foundRecipe.offspring.cardImage
                               ?? LoadSpriteSafe("Breeding/Braluff")
                               ?? LoadSpriteSafe("CreatureAvatars/Braluff")
                               ?? LoadSpriteSafe("CreatureAvatars/17_Braluff")
                               ?? LoadSpriteSafe("conlai_laithu/17_Braluff");
                if (fallback != null)
                {
                    selHybrid2DImage.sprite = fallback;
                    selHybrid2DImage.color = Color.white;
                }
            }

            // 2. Show 3D Models of Parent 1 (Bramblet) in Slot 1 and Parent 2 (Petaluff) in Slot 2
            UpdateParentSlot(CreatureModelPreviewer.SlotParentA, selParentA3DRawImage, selParentAAvatar, selParentAName, selectedParentA, "Parent 1");
            UpdateParentSlot(CreatureModelPreviewer.SlotParentB, selParentB3DRawImage, selParentBAvatar, selParentBName, selectedParentB, "Parent 2");
        }
        else if (selectedParentA != null)
        {
            CreatureModelPreviewer.Instance?.ClearSlot(CreatureModelPreviewer.SlotMain);
            selHybrid3DRawImage.gameObject.SetActive(false);
            selHybrid2DImage.color = Color.clear;
            evolveButton.interactable = false;

            bool isDeer = IsDeer(selectedParentA);
            bool isCater = IsCaterpillar(selectedParentA);
            if (isDeer || isCater)
            {
                string missingPartner = isDeer ? "Petaluff" : "Bramblet";
                if (selComingSoonText != null)
                {
                    selComingSoonText.text = $"Missing Partner Card\nNeed: {missingPartner}";
                    selComingSoonText.gameObject.SetActive(true);
                }
                selHybridName.text = $"Missing {missingPartner}";
                selTitleText.text = $"Missing {missingPartner} in hand";
                evolveDurationText.text = $"Obtain {missingPartner} card in hand to breed Braluff!";

                UpdateParentSlot(CreatureModelPreviewer.SlotParentA, selParentA3DRawImage, selParentAAvatar, selParentAName, selectedParentA, "Parent 1");
                UpdateParentSlot(CreatureModelPreviewer.SlotParentB, selParentB3DRawImage, selParentBAvatar, selParentBName, null, $"Need {missingPartner}");
            }
            else
            {
                if (selComingSoonText != null)
                {
                    selComingSoonText.text = "Coming Soon";
                    selComingSoonText.gameObject.SetActive(true);
                }
                selHybridName.text = "Coming Soon";
                selTitleText.text = "Select 2 parent creatures";
                evolveDurationText.text = "";

                UpdateParentSlot(CreatureModelPreviewer.SlotParentA, selParentA3DRawImage, selParentAAvatar, selParentAName, selectedParentA, "Parent 1");
                UpdateParentSlot(CreatureModelPreviewer.SlotParentB, selParentB3DRawImage, selParentBAvatar, selParentBName, null, "—");
            }
        }
        else
        {
            // Initial empty state
            CreatureModelPreviewer.Instance?.ClearAllSlots();
            selHybrid3DRawImage.gameObject.SetActive(false);
            selHybrid2DImage.color = Color.clear;
            if (selComingSoonText != null) selComingSoonText.gameObject.SetActive(false);

            selHybridName.text = "—";
            selTitleText.text = "Select 2 parent creatures";
            evolveDurationText.text = "";
            evolveButton.interactable = false;

            UpdateParentSlot(CreatureModelPreviewer.SlotParentA, selParentA3DRawImage, selParentAAvatar, selParentAName, null, "Parent 1");
            UpdateParentSlot(CreatureModelPreviewer.SlotParentB, selParentB3DRawImage, selParentBAvatar, selParentBName, null, "Parent 2");
        }
    }

    private bool TryShow3DPreview(string creatureName)
    {
        return TryShow3DPreviewInSlot(CreatureModelPreviewer.SlotMain, creatureName, null);
    }

    private bool TryShow3DPreviewInSlot(int slotIndex, string creatureName, CardData card)
    {
        if (CreatureModelPreviewer.Instance == null) return false;

        string id = card?.cardID?.ToLowerInvariant() ?? "";
        string nm = card?.cardName?.ToLowerInvariant() ?? "";
        string sprNm = (card?.cardImage != null ? card.cardImage.name : "").ToLowerInvariant();
        string cName = (creatureName ?? "").ToLowerInvariant();
        string combined = $"{cName} {id} {nm} {sprNm}";

        GameObject prefab = null;
        AnimationClip fallbackClip = null;

        bool isBraluff = combined.Contains("braluff") || combined.Contains("17_");
        if (isBraluff)
        {
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/conlai_laithu/Braluff.glb");
            if (prefab == null) prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Resources/CreatureModels/Braluff.glb");
            var subAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/conlai_laithu/Braluff.glb");
            if (subAssets != null)
            {
                foreach (var obj in subAssets)
                {
                    if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        fallbackClip = clip;
                        break;
                    }
                }
            }
#endif
            if (prefab == null) prefab = Resources.Load<GameObject>("CreatureModels/Braluff");
            if (fallbackClip == null)
            {
                var clips = Resources.LoadAll<AnimationClip>("CreatureModels/Braluff");
                if (clips != null && clips.Length > 0) fallbackClip = clips[0];
            }
        }
        else
        {
            string resModelName = ResolveCreatureModelResourceName(combined);
            if (!string.IsNullOrEmpty(resModelName))
            {
                prefab = Resources.Load<GameObject>("CreatureModels/" + resModelName);
#if UNITY_EDITOR
                if (prefab == null)
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Project/Resources/CreatureModels/{resModelName}.prefab");
                if (prefab == null)
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Zoo_model3d/model_anm/{resModelName}.prefab");
#endif
            }
        }

        if (prefab != null)
        {
            return CreatureModelPreviewer.Instance.ShowModelInSlot(slotIndex, prefab, fallbackClip);
        }
        return false;
    }

    private static string ResolveCreatureModelResourceName(string combined)
    {
        if (combined.Contains("deer") || combined.Contains("bramblet") || combined.Contains("nai") || combined.Contains("02_"))
            return "nai_anm";
        if (combined.Contains("caterpillar") || combined.Contains("petaluff") || combined.Contains("sâu") || combined.Contains("sau") || combined.Contains("04_"))
            return "saubuom_anm";
        if (combined.Contains("squirrel") || combined.Contains("spriglide") || combined.Contains("sóc") || combined.Contains("soc") || combined.Contains("01_"))
            return "socbay_anm";
        if (combined.Contains("grasshopper") || combined.Contains("frondle") || combined.Contains("châu chấu") || combined.Contains("chau chau") || combined.Contains("03_"))
            return "chauchau_anm";
        if (combined.Contains("grassfox") || combined.Contains("grass fox") || combined.Contains("dandrift") || combined.Contains("cáo cỏ") || combined.Contains("05_"))
            return "caoco_anm";
        if (combined.Contains("fox") || combined.Contains("cáo") || combined.Contains("cao"))
            return "cao_anm";
        if (combined.Contains("peacock") || combined.Contains("galume") || combined.Contains("công") || combined.Contains("cong") || combined.Contains("06_"))
            return "chimcong_anm";
        if (combined.Contains("highland") || combined.Contains("heathorn") || combined.Contains("bò lông dài") || combined.Contains("07_"))
            return "bolongdai_anm";
        if (combined.Contains("cow") || combined.Contains("bò") || combined.Contains("bo "))
            return "bo_anm";
        if (combined.Contains("blossomfrog") || combined.Contains("blossom frog") || combined.Contains("reedle") || combined.Contains("ếch hoa") || combined.Contains("08_"))
            return "echhoa_anm";
        if (combined.Contains("craggle") || combined.Contains("canoc") || combined.Contains("09_"))
            return "canoc_anm";
        if (combined.Contains("dragonfly") || combined.Contains("lumaqua") || combined.Contains("chuồn") || combined.Contains("chuon") || combined.Contains("10_"))
            return "chuongchuong_anm";
        if (combined.Contains("swirliggle") || combined.Contains("tadpole") || combined.Contains("echduoica") || combined.Contains("11_"))
            return "echduoica_anm";
        if (combined.Contains("capybara") || combined.Contains("mossback") || combined.Contains("12_"))
            return "capybara_anm";
        if (combined.Contains("parrot") || combined.Contains("verdantail") || combined.Contains("vẹt") || combined.Contains("vet") || combined.Contains("13_"))
            return "chimvet7mau_anm";
        if (combined.Contains("flowerdog") || combined.Contains("flower dog") || combined.Contains("bamboop") || combined.Contains("chó hoa") || combined.Contains("14_"))
            return "chohoa_anm";
        if (combined.Contains("otter") || combined.Contains("lotosleep") || combined.Contains("rái cá") || combined.Contains("rai ca") || combined.Contains("15_"))
            return "raica_anm";
        if (combined.Contains("swan") || combined.Contains("dewdrop") || combined.Contains("thiên nga") || combined.Contains("thien nga") || combined.Contains("16_"))
            return "thiennga_anm";

        return null;
    }

    private void UpdateParentSlot(int slotIndex, RawImage raw3DImg, Image avatarImg, TextMeshProUGUI label, CardData card, string fallback)
    {
        if (card != null)
        {
            string eng = GetEnglishCreatureName(card);
            if (label != null) label.text = eng;

            bool showed3D = TryShow3DPreviewInSlot(slotIndex, eng, card);
            if (showed3D && raw3DImg != null && CreatureModelPreviewer.Instance != null)
            {
                raw3DImg.texture = CreatureModelPreviewer.Instance.GetSlotTexture(slotIndex);
                raw3DImg.gameObject.SetActive(true);
                if (avatarImg != null) avatarImg.color = Color.clear;
                return;
            }

            if (raw3DImg != null) raw3DImg.gameObject.SetActive(false);
            if (avatarImg != null)
            {
                Sprite spr = card.cardImage
                          ?? LoadSpriteSafe("CreatureAvatars/" + eng)
                          ?? LoadSpriteSafe("CreatureAvatars/02_" + eng)
                          ?? LoadSpriteSafe("CreatureAvatars/04_" + eng)
                          ?? LoadSpriteSafe("AVT_ANM/" + eng);
                avatarImg.sprite = spr;
                avatarImg.color = (spr != null) ? Color.white : Color.clear;
            }
        }
        else
        {
            CreatureModelPreviewer.Instance?.ClearSlot(slotIndex);
            if (raw3DImg != null) raw3DImg.gameObject.SetActive(false);
            if (avatarImg != null)
            {
                avatarImg.sprite = null;
                avatarImg.color = Color.clear;
            }
            if (label != null) label.text = fallback;
        }
    }

    private void OnEvolveClicked()
    {
        if (selectedParentA == null || selectedParentB == null || foundRecipe == null) return;
        if (FindDeerCardInHand() == null || FindCaterpillarCardInHand() == null)
        {
            GameDialogUI.ShowNotice("You must own both Bramblet and Petaluff cards in hand to breed!");
            PopulateCardGrid(activeTab);
            RefreshSelectionPreview();
            return;
        }
        bool started = BreedingManager.Instance != null && BreedingManager.Instance.StartBreeding(activeChamberIndex, selectedParentA, selectedParentB);
        if (started)
        {
            ShowView(2);
        }
    }

    private void RefreshIncubationView()
    {
        if (BreedingManager.Instance == null) return;
        var ch = BreedingManager.Instance.GetChamber(activeChamberIndex);
        if (ch == null) return;

        // Father (SlotParentA) & Mother (SlotParentB)
        UpdateParentSlot(CreatureModelPreviewer.SlotParentA, incubFather3DRawImg, incubFatherImg, incubFatherName, ch.parentA, "Father");
        UpdateParentSlot(CreatureModelPreviewer.SlotParentB, incubMother3DRawImg, incubMotherImg, incubMotherName, ch.parentB, "Mother");

        // Offspring display (SlotMain)
        string offName = ch.recipe?.offspring != null ? GetEnglishCreatureName(ch.recipe.offspring) : "Braluff";
        if (incubOffspringName != null) incubOffspringName.text = offName;

        bool showed3D = TryShow3DPreviewInSlot(CreatureModelPreviewer.SlotMain, offName, ch.recipe?.offspring);
        if (showed3D && CreatureModelPreviewer.Instance != null && CreatureModelPreviewer.Instance.GetSlotTexture(CreatureModelPreviewer.SlotMain) != null)
        {
            incubOffspring3DRawImg.texture = CreatureModelPreviewer.Instance.GetSlotTexture(CreatureModelPreviewer.SlotMain);
            incubOffspring3DRawImg.gameObject.SetActive(true);
            incubOffspringImg.color = Color.clear;
        }
        else
        {
            incubOffspring3DRawImg.gameObject.SetActive(false);
            Sprite fallback = ch.recipe?.offspring?.cardImage ?? LoadSpriteSafe("Breeding/Braluff") ?? LoadSpriteSafe("CreatureAvatars/Braluff");
            if (fallback != null)
            {
                incubOffspringImg.sprite = fallback;
                incubOffspringImg.color = Color.white;
            }
        }

        // Progress & Timer (no unicode emojis)
        if (incubProgressBar != null) incubProgressBar.value = ch.progress;
        if (incubTimerText != null)
        {
            if (ch.IsReady) incubTimerText.text = "Incubation Ready!";
            else incubTimerText.text = $"{ch.FormatTimeRemaining()} remaining";
        }
    }

    private void OnSpeedUpWithCoinsClicked()
    {
        if (BreedingManager.Instance == null) return;
        bool ok = BreedingManager.Instance.TrySpeedUpWithCoins(activeChamberIndex);
        if (ok)
        {
            GameAudioManager.Instance?.PlayClick();
            RefreshIncubationView();
        }
        else
        {
            GameDialogUI.ShowNotice("Not enough Coins to speed up!");
        }
    }

    private void OnSpeedUpWithAdClicked()
    {
        if (BreedingManager.Instance == null) return;
        BreedingManager.Instance.SpeedUpWithAd(activeChamberIndex);
        GameAudioManager.Instance?.PlayClick();
        RefreshIncubationView();
    }

    private void OnChamberButtonClick(int chamberIndex)
    {
        activeChamberIndex = chamberIndex;
        var ch = BreedingManager.Instance != null ? BreedingManager.Instance.GetChamber(chamberIndex) : null;
        if (ch == null) return;

        if (ch.IsEmpty)
        {
            ShowView(1);
        }
        else if (ch.IsReady)
        {
            var offspring = BreedingManager.Instance.ClaimOffspring(chamberIndex);
            if (offspring != null)
            {
                ShowCongratsPopup(offspring);
            }
        }
        else
        {
            ShowView(2);
        }
    }

    private void OnChamberChanged(int index)
    {
        if (viewMode == 0) RefreshChamberSlot(index);
        if (viewMode == 2 && index == activeChamberIndex) RefreshIncubationView();
    }

    private void OnBreedingComplete(int chamberIndex, CardData offspring)
    {
        if (viewMode == 2 && chamberIndex == activeChamberIndex)
        {
            BreedingManager.Instance?.ClaimOffspring(chamberIndex);
            ShowCongratsPopup(offspring);
        }
        else
        {
            RefreshChamberSlot(chamberIndex);
        }
    }

    private void ShowCongratsPopup(CardData offspring)
    {
        pendingClaimOffspring = offspring;
        if (congratsPopup == null) return;

        string name = offspring?.cardName ?? "Braluff";
        if (congratsSub != null)
        {
            congratsSub.text = $"You successfully bred: <b>{name}</b>!\nThis hybrid creature thrives across 3 habitats!";
        }

        // Show card image (with 3 biome icons on top-left and 4 rounded corners)
        Sprite spr = LoadSpriteSafe("Breeding/Braluff")
                  ?? LoadSpriteSafe("CreatureAvatars/17_Braluff")
                  ?? LoadSpriteSafe("Acs/UI Cards/UI Cards/17_Braluff")
                  ?? offspring?.cardImage;
        if (offspring != null && spr != null)
        {
            offspring.cardImage = spr;
        }
        if (congratsCardImg != null && spr != null)
        {
            congratsCardImg.sprite = spr;
            congratsCardImg.color = Color.white;
        }

        congratsPopup.SetActive(true);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    public static Sprite LoadSpriteSafe(string path)
    {
        Sprite s = Resources.Load<Sprite>(path);
        if (s != null) return s;
        Texture2D t = Resources.Load<Texture2D>(path);
        if (t != null)
        {
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
        }

#if UNITY_EDITOR
        string clean = path.Replace("\\", "/");
        if (!clean.StartsWith("Assets/"))
        {
            string p1 = "Assets/" + clean + ".png";
            s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p1);
            if (s != null) return s;

            string p2 = "Assets/AVT_ANM/" + System.IO.Path.GetFileName(clean) + ".png";
            s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p2);
            if (s != null) return s;

            string p3 = "Assets/conlai_laithu/" + System.IO.Path.GetFileName(clean) + ".png";
            s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p3);
            if (s != null) return s;
        }
        else
        {
            s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(clean.EndsWith(".png") ? clean : clean + ".png");
            if (s != null) return s;
        }
#endif
        return null;
    }

    private static string FormatDuration(float seconds)
    {
        int mins = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        if (mins > 0) return $"{mins}m {secs:D2}s";
        return $"{secs}s";
    }

    private static void BuildSliderVisuals(Slider slider, RectTransform sliderRect)
    {
        // Background
        RectTransform bgRect = EconomyHUD.CreateRect("Background", sliderRect);
        EconomyHUD.Stretch(bgRect);
        Image bgImg = bgRect.gameObject.AddComponent<Image>();
        bgImg.color = new Color(0.82f, 0.88f, 0.95f, 1f);

        // Fill area
        RectTransform fillArea = EconomyHUD.CreateRect("FillArea", sliderRect);
        fillArea.anchorMin = Vector2.zero;
        fillArea.anchorMax = Vector2.one;
        fillArea.offsetMin = new Vector2(2f, 2f);
        fillArea.offsetMax = new Vector2(-2f, -2f);

        RectTransform fillRect = EconomyHUD.CreateRect("Fill", fillArea);
        EconomyHUD.Stretch(fillRect);
        Image fillImg = fillRect.gameObject.AddComponent<Image>();
        fillImg.color = GreenProgress;

        slider.fillRect = fillRect;
    }

    private static GameObject BuildScrollGrid(string name, RectTransform parent, RectOffset inset)
    {
        RectTransform scrollRect = EconomyHUD.CreateRect(name + "Scroll", parent);
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(inset.left, inset.bottom);
        scrollRect.offsetMax = new Vector2(-inset.right, -inset.top);

        Image scrollBg = scrollRect.gameObject.AddComponent<Image>();
        scrollBg.color = new Color(0.92f, 0.95f, 0.98f, 0.5f);

        ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical   = true;

        RectTransform vpRect = EconomyHUD.CreateRect("Viewport", scrollRect);
        EconomyHUD.Stretch(vpRect);
        vpRect.gameObject.AddComponent<RectMask2D>();
        scroll.viewport = vpRect;

        RectTransform contentRect = EconomyHUD.CreateRect("Content", vpRect);
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot     = new Vector2(0f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        GridLayoutGroup glp = contentRect.gameObject.AddComponent<GridLayoutGroup>();
        glp.childAlignment = TextAnchor.UpperLeft;
        glp.cellSize = new Vector2(74f, 105f);
        glp.spacing  = new Vector2(8f, 8f);
        glp.padding  = new RectOffset(10, 10, 10, 10);
        glp.constraint = GridLayoutGroup.Constraint.Flexible;

        ContentSizeFitter csf = contentRect.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        return contentRect.gameObject;
    }
}

internal class ChamberSlotWidget
{
    public int index;
    public Image tubeImage;
    public Image creature2DImg;
    public RawImage creature3DImg;
    public TextMeshProUGUI statusText;
    public Slider progressBar;
    public GameObject progressBarGo;
    public Button actionBtn;
    public TextMeshProUGUI actionBtnText;
    public GameObject lockOverlay;
}
