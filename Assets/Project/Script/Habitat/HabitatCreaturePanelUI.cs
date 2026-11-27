using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LumiWorld.Acs;

/// <summary>
/// P-05 Biome / Habitat Panel & P-06 Creature Detail Panel — LumiWorld UI Production Pack:
/// - Scaled consistently with Inventory/Crafting via ResourceInventoryUILayout.FitScale (1280x720 design stage).
/// - P-05 (Top-right docked, 340×524 du -> ~510×786 px at 1080p, grows downward to 640 du for 4-6 residents):
///   + Colored Biome Emblem (B-01..B-07) + Biome Name (no "Habitat" word) + Footprint glyph (3/6/10 hex).
///   + Residents (current/capacity): Centered per row (1 centered, 2 centered evenly, 3 fill row 1; 4-6 expand panel and wrap onto row 2),
///     using the 16 cropped portrait cutouts, status dot ("Working" / "Needs care"), "!" warning badge when hungry,
///     and dark graphite selection border (#403F3C). Clicking a resident opens P-06 immediately to the left.
///   + Production: "Production" header + "[Refresh] Change resource" button, production slot cards, and "[G-24] Collect all (N)".
/// - P-06 (Docked immediately to the left of P-05, 262×476 du, clear of P-03 Card Tray):
///   + "[← Back to <Biome>]" pill + "[×]" close button.
///   + Creature Portrait Stage using the official non-background cutout PNGs.
///   + Creature Name + [Pencil] Rename button + clickable Rank I/II/III pill (opens Rank Popover with Care & Rank Up).
///   + Fullness progress bar + status pill + Food card (icon + name only).
///   + Bottom action row: "[G-22] Feed" and "[G-23] Treat" side-by-side.
/// </summary>
public class HabitatCreaturePanelUI : MonoBehaviour
{
    private static HabitatCreaturePanelUI instance;
    public static HabitatCreaturePanelUI Instance => instance;

    private GameObject rootModal;
    private CanvasGroup rootCanvasGroup;
    private RectTransform panelsStageRect;
    private int lastStageScreenW;
    private int lastStageScreenH;
    private float lastStageParentScale;

    // Panel 1 (P-05): Biome / Residents / Production (4 slots: T1#1, T1#2, T2#1, T2#2)
    private const float P05PanelWidth = 340f;
    private const float P05PanelBaseHeight = 542f;
    private const float P05ResidentsBaseHeight = 124f;
    private const float P05ResidentsExtraRowHeight = 104f;
    private const float P05ResidentsWidth = 308f;
    private const float P05SlotCardHeight = 46f;
    private const float P05SlotCardPitch = 51f;

    private RectTransform panelListRect;
    private Image biomeEmblemImg;
    private TextMeshProUGUI habitatNameText;
    private Image footprintGlyphImg;
    private TextMeshProUGUI footprintCountText;
    private TextMeshProUGUI residentsHeaderText;
    private RectTransform creatureGridRoot;
    private RectTransform productionHeaderRect;
    private RectTransform changeResourceBtnRect;
    private RectTransform resourceSlotsRoot;
    private Button changeResourceBtn;
    private Button harvestAllButton;
    private TextMeshProUGUI harvestAllButtonText;
    private Image harvestAllIconImg;

    // Panel 2 (P-06): Creature Detail
    private const float P06PanelWidth = 262f;
    private const float P06PanelHeight = 476f;

    private RectTransform panelDetailRect;
    private TextMeshProUGUI backToBiomeText;
    private Image creatureArtImg;
    private TextMeshProUGUI creatureNameText;
    private Button renameButton;

    // Rank Pill & Rank Popover
    private Button rankPillButton;
    private Image rankPillGlyphImg;
    private TextMeshProUGUI rankPillText;
    private GameObject rankPopoverObj;
    private TextMeshProUGUI rankPopoverTitleText;
    private TextMeshProUGUI rankPopoverCareText;
    private Image rankPopoverCareFill;
    private Button rankUpButton;
    private TextMeshProUGUI rankUpButtonText;

    // Fullness & Food Card
    private TextMeshProUGUI statusPillText;
    private Image statusPillBg;
    private TextMeshProUGUI hungerProgressLabel;
    private Image hungerFillImg;
    private Image foodIconImg;
    private TextMeshProUGUI foodNameText;

    // Side-by-side Feed & Treat buttons
    private Button feedButton;
    private TextMeshProUGUI feedButtonText;
    private Button medicineButton;
    private TextMeshProUGUI medicineButtonText;
    private TextMeshProUGUI feedbackNotifyText;

    // Rename Modal
    private GameObject renameModal;
    private TMP_InputField renameInputField;

    // Runtime selection state
    private HabitatState currentHabitat;
    private HabitatResident currentSelectedResident;
    private AnimalIndividual currentSelectedAnimal;
    private readonly List<GameObject> spawnedSlotButtons = new List<GameObject>();
    private readonly List<GameObject> spawnedResourceCards = new List<GameObject>();
    private Coroutine feedbackFadeRoutine;

    private static Sprite s_SelectionOutlineSprite;

    public bool IsOpen => rootModal != null && rootModal.activeSelf;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        HabitatCreaturePanelStyle.ClearCache();
        BuildUI();
    }

    private void Start()
    {
        if (rootModal != null)
        {
            EconomyHUD.RegisterWindow(rootModal, () =>
            {
                if (renameModal != null && renameModal.activeSelf) CloseRenameModal();
                else if (rankPopoverObj != null && rankPopoverObj.activeSelf) CloseRankPopover();
                else if (panelDetailRect != null && panelDetailRect.gameObject.activeSelf) CloseDetailPanelOnly();
                else ClosePanels();
            });
        }

        if (CreatureCareManager.Instance != null)
        {
            CreatureCareManager.Instance.OnCareProgressChanged += HandleCareProgressChanged;
            CreatureCareManager.Instance.OnRankUpgraded += HandleRankUpgraded;
        }
    }

    private void OnDestroy()
    {
        EconomyHUD.UnregisterWindow(rootModal);
        if (instance == this) instance = null;
        if (CreatureCareManager.HasInstance)
        {
            CreatureCareManager.Instance.OnCareProgressChanged -= HandleCareProgressChanged;
            CreatureCareManager.Instance.OnRankUpgraded -= HandleRankUpgraded;
        }
    }

    public void OpenForHabitat(HabitatState habitat)
    {
        if (habitat == null || !habitat.IsFormedBiome) return;
        currentHabitat = habitat;

        if (rootModal != null)
        {
            rootModal.SetActive(true);
            EconomyHUD.BringWindowToFront(rootModal);
            rootCanvasGroup.alpha = 1f;
            SyncPanelsStageScale(force: true);
        }

        currentSelectedResident = null;
        currentSelectedAnimal = null;
        CloseRankPopover();
        if (panelDetailRect != null)
        {
            panelDetailRect.gameObject.SetActive(false);
        }

        RefreshPanel1();
    }

    public void ClosePanels()
    {
        CloseRenameModal();
        CloseRankPopover();
        if (rootModal != null)
        {
            if (rootModal.activeSelf) EconomyHUD.MarkWindowClosed();
            rootModal.SetActive(false);
        }
        currentSelectedResident = null;
        currentSelectedAnimal = null;
    }

    private void CloseDetailPanelOnly()
    {
        CloseRenameModal();
        CloseRankPopover();
        currentSelectedResident = null;
        currentSelectedAnimal = null;
        if (panelDetailRect != null)
        {
            panelDetailRect.gameObject.SetActive(false);
        }
        RefreshPanel1();
    }

    private void HandleCareProgressChanged(string id, int newCare)
    {
        if (!IsOpen) return;
        RefreshPanel1();
        if (panelDetailRect != null && panelDetailRect.gameObject.activeSelf) RefreshPanel2(false);
    }

    private void HandleRankUpgraded(string id, CreatureQualityRank newRank)
    {
        if (!IsOpen) return;
        RefreshPanel1();
        if (panelDetailRect != null && panelDetailRect.gameObject.activeSelf) RefreshPanel2(false);
    }

    #region UI Construction

    private void BuildUI()
    {
        rootModal = new GameObject("HabitatCreaturePanelModal", typeof(RectTransform), typeof(CanvasGroup));
        rootModal.transform.SetParent(transform, false);
        RectTransform modalRect = rootModal.GetComponent<RectTransform>();
        EconomyHUD.Stretch(modalRect);

        rootCanvasGroup = rootModal.GetComponent<CanvasGroup>();

        // Transparent click-outside backdrop so the 3D island remains clearly visible while P-05/P-06 are docked on the right
        GameObject backdropObj = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        backdropObj.transform.SetParent(rootModal.transform, false);
        EconomyHUD.Stretch(backdropObj.GetComponent<RectTransform>());

        Image bdImg = backdropObj.GetComponent<Image>();
        bdImg.color = new Color(0.10f, 0.09f, 0.08f, 0.14f);

        Button bdBtn = backdropObj.GetComponent<Button>();
        bdBtn.transition = Selectable.Transition.None;
        bdBtn.onClick.AddListener(ClosePanels);

        GameObject stageObj = new GameObject("PanelsStage", typeof(RectTransform));
        stageObj.transform.SetParent(rootModal.transform, false);
        panelsStageRect = stageObj.GetComponent<RectTransform>();
        SyncPanelsStageScale(force: true);

        BuildPanel1(panelsStageRect);
        BuildPanel2(panelsStageRect);

        rootModal.SetActive(false);
    }

    private void SyncPanelsStageScale(bool force = false)
    {
        if (panelsStageRect == null) return;
        Canvas rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        float parentScale = transform.lossyScale.x;
        if (parentScale <= 0.0001f)
        {
            parentScale = rootCanvas != null && rootCanvas.scaleFactor > 0.01f ? rootCanvas.scaleFactor : 1f;
        }

        if (!force && lastStageScreenW == Screen.width && lastStageScreenH == Screen.height &&
            Mathf.Approximately(lastStageParentScale, parentScale))
        {
            return;
        }

        lastStageScreenW = Screen.width;
        lastStageScreenH = Screen.height;
        lastStageParentScale = parentScale;

        Vector2 pixels = rootCanvas != null && rootCanvas.scaleFactor > 0.01f
            ? ((RectTransform)rootCanvas.transform).rect.size * rootCanvas.scaleFactor
            : new Vector2(Screen.width, Screen.height);
        float fit = ResourceInventoryUILayout.FitScale(pixels.x, pixels.y);
        if (fit <= 0.01f) return;

        panelsStageRect.anchorMin = Vector2.zero;
        panelsStageRect.anchorMax = Vector2.zero;
        panelsStageRect.pivot = Vector2.zero;
        panelsStageRect.anchoredPosition = Vector2.zero;
        panelsStageRect.sizeDelta = pixels / fit;
        panelsStageRect.localScale = Vector3.one * (fit / parentScale);
    }

    private void BuildPanel1(Transform parent)
    {
        // P-05: Top-right docked panel (340 x 524 du -> ~510 x 786 px at 1080p), grows down for 4-6 residents
        GameObject p1Obj = new GameObject("P05_HabitatPanel", typeof(RectTransform), typeof(Image));
        p1Obj.transform.SetParent(parent, false);
        panelListRect = p1Obj.GetComponent<RectTransform>();
        panelListRect.anchorMin = new Vector2(1f, 1f);
        panelListRect.anchorMax = new Vector2(1f, 1f);
        panelListRect.pivot = new Vector2(1f, 1f);
        panelListRect.anchoredPosition = new Vector2(-14f, -40f);
        panelListRect.sizeDelta = new Vector2(P05PanelWidth, P05PanelBaseHeight);

        Image p1Bg = p1Obj.GetComponent<Image>();
        p1Bg.sprite = LumiWorldUITheme.WindowSprite;
        p1Bg.type = Image.Type.Sliced;
        p1Bg.color = LumiWorldUITheme.Window;
        p1Bg.raycastTarget = true;
        p1Obj.AddComponent<ResourceUIInputBlocker>();
        LumiWorldUITheme.AddSoftElevation(p1Obj, 6f, 0.24f);

        // --- 1. HEADER: Biome Emblem (B-01..B-07) + Biome Name + Footprint + [x] ---
        GameObject emblemObj = new GameObject("BiomeEmblem", typeof(RectTransform), typeof(Image));
        emblemObj.transform.SetParent(panelListRect, false);
        RectTransform emRect = emblemObj.GetComponent<RectTransform>();
        SetTopLeft(emRect, new Vector2(16f, -12f), new Vector2(54f, 54f));
        biomeEmblemImg = emblemObj.GetComponent<Image>();
        biomeEmblemImg.preserveAspect = true;
        biomeEmblemImg.raycastTarget = false;

        habitatNameText = CreateLabel(panelListRect, "BiomeName", "Leafwood", 20f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        habitatNameText.font = LumiWorldUITheme.Heading ?? habitatNameText.font;
        habitatNameText.enableAutoSizing = true;
        habitatNameText.fontSizeMin = 14f;
        habitatNameText.fontSizeMax = 21f;
        habitatNameText.overflowMode = TextOverflowModes.Overflow;
        SetTopLeft(habitatNameText.rectTransform, new Vector2(78f, -12f), new Vector2(210f, 34f));

        // Footprint row under Biome Name
        GameObject fpRow = new GameObject("FootprintRow", typeof(RectTransform));
        fpRow.transform.SetParent(panelListRect, false);
        RectTransform fpRect = fpRow.GetComponent<RectTransform>();
        SetTopLeft(fpRect, new Vector2(78f, -44f), new Vector2(204f, 22f));

        GameObject fpIcObj = new GameObject("FootprintGlyph", typeof(RectTransform), typeof(Image));
        fpIcObj.transform.SetParent(fpRect, false);
        RectTransform fpIcRect = fpIcObj.GetComponent<RectTransform>();
        SetTopLeft(fpIcRect, new Vector2(0f, -2f), new Vector2(18f, 18f));
        footprintGlyphImg = fpIcObj.GetComponent<Image>();
        footprintGlyphImg.preserveAspect = true;
        footprintGlyphImg.color = LumiWorldUITheme.Muted;
        footprintGlyphImg.raycastTarget = false;

        footprintCountText = CreateLabel(fpRect, "FootprintText", "3 hex", 12f, LumiWorldUITheme.Muted, true, TextAlignmentOptions.MidlineLeft);
        footprintCountText.overflowMode = TextOverflowModes.Overflow;
        SetTopLeft(footprintCountText.rectTransform, new Vector2(23f, 0f), new Vector2(176f, 22f));

        // Close [x] button
        CreateCloseButton(panelListRect, new Vector2(-14f, -14f), () => PlayCloseWithAnimation(panelListRect, ClosePanels), 28f);

        // --- 2. RESIDENTS SECTION ---
        residentsHeaderText = CreateLabel(panelListRect, "ResidentsHeader", "Residents (0/2)", 13.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        residentsHeaderText.overflowMode = TextOverflowModes.Overflow;
        SetTopLeft(residentsHeaderText.rectTransform, new Vector2(16f, -74f), new Vector2(240f, 24f));

        GameObject resBoxObj = new GameObject("ResidentsContainer", typeof(RectTransform), typeof(Image));
        resBoxObj.transform.SetParent(panelListRect, false);
        creatureGridRoot = resBoxObj.GetComponent<RectTransform>();
        SetTopLeft(creatureGridRoot, new Vector2(16f, -100f), new Vector2(P05ResidentsWidth, P05ResidentsBaseHeight));

        Image resBoxBg = resBoxObj.GetComponent<Image>();
        resBoxBg.sprite = LumiWorldUITheme.SurfaceSprite;
        resBoxBg.type = Image.Type.Sliced;
        resBoxBg.color = LumiWorldUITheme.Surface;
        resBoxBg.raycastTarget = false;
        LumiWorldUITheme.AddSoftElevation(resBoxObj, 2.5f, 0.14f);

        // --- 3. PRODUCTION SECTION ---
        TextMeshProUGUI prodHdr = CreateLabel(panelListRect, "ProductionHeader", "Production", 13.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        prodHdr.overflowMode = TextOverflowModes.Overflow;
        productionHeaderRect = prodHdr.rectTransform;
        SetTopLeft(productionHeaderRect, new Vector2(16f, -238f), new Vector2(140f, 26f));

        // "[Refresh] Change resource" button on right of Production header
        GameObject chgObj = new GameObject("ChangeResourceBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        chgObj.transform.SetParent(panelListRect, false);
        changeResourceBtnRect = chgObj.GetComponent<RectTransform>();
        changeResourceBtnRect.anchorMin = changeResourceBtnRect.anchorMax = changeResourceBtnRect.pivot = new Vector2(1f, 1f);
        changeResourceBtnRect.anchoredPosition = new Vector2(-16f, -236f);
        changeResourceBtnRect.sizeDelta = new Vector2(144f, 26f);

        Image chgBg = chgObj.GetComponent<Image>();
        chgBg.sprite = LumiWorldUITheme.PillSprite;
        chgBg.type = Image.Type.Sliced;
        chgBg.color = LumiWorldUITheme.Inset;
        LumiWorldUITheme.AddSoftElevation(chgObj, 2f, 0.14f);

        GameObject chgIcObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        chgIcObj.transform.SetParent(changeResourceBtnRect, false);
        RectTransform chgIcRect = chgIcObj.GetComponent<RectTransform>();
        chgIcRect.anchorMin = chgIcRect.anchorMax = new Vector2(0f, 0.5f);
        chgIcRect.pivot = new Vector2(0f, 0.5f);
        chgIcRect.anchoredPosition = new Vector2(10f, 0f);
        chgIcRect.sizeDelta = new Vector2(13f, 13f);
        Image chgIcImg = chgIcObj.GetComponent<Image>();
        chgIcImg.sprite = LumiWorldUITheme.Glyph("Refresh");
        chgIcImg.preserveAspect = true;
        chgIcImg.color = LumiWorldUITheme.Text;
        chgIcImg.raycastTarget = false;

        TextMeshProUGUI chgLbl = CreateLabel(changeResourceBtnRect, "Label", "Change resource", 11f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        chgLbl.rectTransform.anchorMin = Vector2.zero;
        chgLbl.rectTransform.anchorMax = Vector2.one;
        chgLbl.rectTransform.offsetMin = new Vector2(27f, 0f);
        chgLbl.rectTransform.offsetMax = new Vector2(-8f, 0f);

        changeResourceBtn = chgObj.GetComponent<Button>();
        changeResourceBtn.targetGraphic = chgBg;
        changeResourceBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        changeResourceBtn.onClick.AddListener(OnChangePrimaryResourceClicked);

        // Production Slots Container (4 slots: 2 T1 + 2 T2)
        GameObject resSlotsObj = new GameObject("ResourceSlotsContainer", typeof(RectTransform));
        resSlotsObj.transform.SetParent(panelListRect, false);
        resourceSlotsRoot = resSlotsObj.GetComponent<RectTransform>();
        SetTopLeft(resourceSlotsRoot, new Vector2(16f, -266f), new Vector2(P05ResidentsWidth, 206f));

        // --- 4. BOTTOM PRIMARY CTA: "[G-24] Collect all (N)" ---
        GameObject hBarObj = new GameObject("CollectAllBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        hBarObj.transform.SetParent(panelListRect, false);
        RectTransform hbRect = hBarObj.GetComponent<RectTransform>();
        hbRect.anchorMin = hbRect.anchorMax = hbRect.pivot = new Vector2(0.5f, 0f);
        hbRect.anchoredPosition = new Vector2(0f, 16f);
        hbRect.sizeDelta = new Vector2(P05ResidentsWidth, 42f);

        Image hbImg = hBarObj.GetComponent<Image>();
        hbImg.sprite = LumiWorldUITheme.ButtonPrimarySprite;
        hbImg.type = Image.Type.Sliced;
        hbImg.color = LumiWorldUITheme.Primary;
        LumiWorldUITheme.AddSoftElevation(hBarObj, 3f, 0.20f);

        harvestAllButton = hBarObj.GetComponent<Button>();
        harvestAllButton.targetGraphic = hbImg;
        harvestAllButton.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock hbColors = harvestAllButton.colors;
        hbColors.disabledColor = Color.white;
        harvestAllButton.colors = hbColors;
        harvestAllButton.onClick.AddListener(OnHarvestAllClicked);

        GameObject hbIcObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        hbIcObj.transform.SetParent(hbRect, false);
        RectTransform hbIcRect = hbIcObj.GetComponent<RectTransform>();
        hbIcRect.anchorMin = hbIcRect.anchorMax = hbIcRect.pivot = new Vector2(0.5f, 0.5f);
        hbIcRect.anchoredPosition = new Vector2(-62f, 0f);
        hbIcRect.sizeDelta = new Vector2(16f, 16f);
        harvestAllIconImg = hbIcObj.GetComponent<Image>();
        harvestAllIconImg.sprite = LumiWorldUITheme.Glyph("G-24");
        harvestAllIconImg.preserveAspect = true;
        harvestAllIconImg.color = Color.white;
        harvestAllIconImg.raycastTarget = false;

        harvestAllButtonText = CreateLabel(hbRect, "Label", "Collect all (0)", 13.5f, Color.white, true, TextAlignmentOptions.Center);
        harvestAllButtonText.overflowMode = TextOverflowModes.Overflow;
        EconomyHUD.Stretch(harvestAllButtonText.rectTransform);
    }

    private void UpdatePanel1DynamicLayout(int residentCount)
    {
        float extraY = residentCount >= 4 ? P05ResidentsExtraRowHeight : 0f;
        if (panelListRect != null)
        {
            panelListRect.sizeDelta = new Vector2(P05PanelWidth, P05PanelBaseHeight + extraY);
        }
        if (creatureGridRoot != null)
        {
            creatureGridRoot.sizeDelta = new Vector2(P05ResidentsWidth, P05ResidentsBaseHeight + extraY);
        }
        if (productionHeaderRect != null)
        {
            productionHeaderRect.anchoredPosition = new Vector2(16f, -(234f + extraY));
        }
        if (changeResourceBtnRect != null)
        {
            changeResourceBtnRect.anchoredPosition = new Vector2(-16f, -(232f + extraY));
        }
        if (resourceSlotsRoot != null)
        {
            resourceSlotsRoot.anchoredPosition = new Vector2(16f, -(266f + extraY));
        }
    }

    private void BuildPanel2(Transform parent)
    {
        // P-06: Docked immediately left of P-05 (262 x 476 du -> bottom stays cleanly above P-03 Card Tray)
        GameObject p2Obj = new GameObject("P06_CreatureDetailPanel", typeof(RectTransform), typeof(Image));
        p2Obj.transform.SetParent(parent, false);
        panelDetailRect = p2Obj.GetComponent<RectTransform>();
        panelDetailRect.anchorMin = new Vector2(1f, 1f);
        panelDetailRect.anchorMax = new Vector2(1f, 1f);
        panelDetailRect.pivot = new Vector2(1f, 1f);
        panelDetailRect.anchoredPosition = new Vector2(-364f, -48f);
        panelDetailRect.sizeDelta = new Vector2(P06PanelWidth, P06PanelHeight);

        Image p2Bg = p2Obj.GetComponent<Image>();
        p2Bg.sprite = LumiWorldUITheme.WindowSprite;
        p2Bg.type = Image.Type.Sliced;
        p2Bg.color = LumiWorldUITheme.Window;
        p2Bg.raycastTarget = true;
        p2Obj.AddComponent<ResourceUIInputBlocker>();
        LumiWorldUITheme.AddSoftElevation(p2Obj, 6f, 0.24f);

        // --- 1. HEADER: "[<- Back to <Biome>]" pill + "[x]" close button ---
        GameObject backBtnObj = new GameObject("BackToBiomeBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        backBtnObj.transform.SetParent(panelDetailRect, false);
        RectTransform backRect = backBtnObj.GetComponent<RectTransform>();
        SetTopLeft(backRect, new Vector2(16f, -14f), new Vector2(166f, 28f));

        Image backBg = backBtnObj.GetComponent<Image>();
        backBg.sprite = LumiWorldUITheme.PillSprite;
        backBg.type = Image.Type.Sliced;
        backBg.color = LumiWorldUITheme.Inset;

        GameObject backIcObj = new GameObject("ArrowIcon", typeof(RectTransform), typeof(Image));
        backIcObj.transform.SetParent(backRect, false);
        RectTransform backIcRect = backIcObj.GetComponent<RectTransform>();
        backIcRect.anchorMin = backIcRect.anchorMax = new Vector2(0f, 0.5f);
        backIcRect.pivot = new Vector2(0f, 0.5f);
        backIcRect.anchoredPosition = new Vector2(10f, 0f);
        backIcRect.sizeDelta = new Vector2(13f, 13f);
        Image backIcImg = backIcObj.GetComponent<Image>();
        backIcImg.sprite = LumiWorldUITheme.Glyph("ArrowLeft");
        backIcImg.preserveAspect = true;
        backIcImg.color = LumiWorldUITheme.Text;
        backIcImg.raycastTarget = false;

        backToBiomeText = CreateLabel(backRect, "Label", "Back to Leafwood", 11.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        backToBiomeText.rectTransform.anchorMin = Vector2.zero;
        backToBiomeText.rectTransform.anchorMax = Vector2.one;
        backToBiomeText.rectTransform.offsetMin = new Vector2(28f, 0f);
        backToBiomeText.rectTransform.offsetMax = new Vector2(-8f, 0f);

        Button backBtn = backBtnObj.GetComponent<Button>();
        backBtn.targetGraphic = backBg;
        backBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        backBtn.onClick.AddListener(CloseDetailPanelOnly);

        CreateCloseButton(panelDetailRect, new Vector2(-14f, -14f), CloseDetailPanelOnly, 28f);

        // --- 2. CREATURE PORTRAIT STAGE (Official non-background cutout PNG) ---
        GameObject stageObj = new GameObject("CreatureDisplayStage", typeof(RectTransform), typeof(Image));
        stageObj.transform.SetParent(panelDetailRect, false);
        RectTransform mRect = stageObj.GetComponent<RectTransform>();
        SetTopLeft(mRect, new Vector2(16f, -50f), new Vector2(230f, 166f));

        Image stageBg = stageObj.GetComponent<Image>();
        stageBg.sprite = LumiWorldUITheme.SurfaceSprite;
        stageBg.type = Image.Type.Sliced;
        stageBg.color = LumiWorldUITheme.Surface;
        stageBg.raycastTarget = false;
        LumiWorldUITheme.AddSoftElevation(stageObj, 3f, 0.14f);

        GameObject artObj = new GameObject("CreatureArtwork", typeof(RectTransform), typeof(Image));
        artObj.transform.SetParent(stageObj.transform, false);
        RectTransform artRect = artObj.GetComponent<RectTransform>();
        artRect.anchorMin = Vector2.zero;
        artRect.anchorMax = Vector2.one;
        artRect.offsetMin = new Vector2(10f, 8f);
        artRect.offsetMax = new Vector2(-10f, -8f);
        creatureArtImg = artObj.GetComponent<Image>();
        creatureArtImg.preserveAspect = true;
        creatureArtImg.raycastTarget = false;

        // --- 3. CREATURE NAME + RENAME BUTTON + CLICKABLE RANK PILL ---
        GameObject nameRow = new GameObject("NameAndRankRow", typeof(RectTransform));
        nameRow.transform.SetParent(panelDetailRect, false);
        RectTransform nrRect = nameRow.GetComponent<RectTransform>();
        SetTopLeft(nrRect, new Vector2(16f, -226f), new Vector2(230f, 34f));

        creatureNameText = CreateLabel(nrRect, "CreatureName", "Bramblet", 16.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        creatureNameText.font = LumiWorldUITheme.Heading ?? creatureNameText.font;
        creatureNameText.enableAutoSizing = true;
        creatureNameText.fontSizeMin = 12f;
        creatureNameText.fontSizeMax = 17f;
        creatureNameText.overflowMode = TextOverflowModes.Overflow;
        SetTopLeft(creatureNameText.rectTransform, new Vector2(0f, -1f), new Vector2(118f, 32f));

        Button nameClickBtn = creatureNameText.gameObject.AddComponent<Button>();
        nameClickBtn.transition = Selectable.Transition.None;
        nameClickBtn.onClick.AddListener(OnRenameClicked);

        // Pencil rename button
        GameObject editBtnObj = new GameObject("EditBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        editBtnObj.transform.SetParent(nrRect, false);
        RectTransform ebRect = editBtnObj.GetComponent<RectTransform>();
        SetTopLeft(ebRect, new Vector2(120f, -4f), new Vector2(24f, 24f));

        Image ebBg = editBtnObj.GetComponent<Image>();
        ebBg.sprite = LumiWorldUITheme.PillSprite;
        ebBg.type = Image.Type.Sliced;
        ebBg.color = LumiWorldUITheme.Inset;

        GameObject ebIcObj = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
        ebIcObj.transform.SetParent(ebRect, false);
        RectTransform ebIcRect = ebIcObj.GetComponent<RectTransform>();
        ebIcRect.anchorMin = ebIcRect.anchorMax = ebIcRect.pivot = new Vector2(0.5f, 0.5f);
        ebIcRect.sizeDelta = new Vector2(13f, 13f);
        Image ebIcImg = ebIcObj.GetComponent<Image>();
        ebIcImg.sprite = LumiWorldUITheme.Glyph("Pencil");
        ebIcImg.preserveAspect = true;
        ebIcImg.color = LumiWorldUITheme.Text;
        ebIcImg.raycastTarget = false;

        renameButton = editBtnObj.GetComponent<Button>();
        renameButton.targetGraphic = ebBg;
        renameButton.navigation = new Navigation { mode = Navigation.Mode.None };
        renameButton.onClick.AddListener(OnRenameClicked);

        // Clickable Rank Pill on the right (Rank I / II / III with 1 / 2 / 3 hex glyph)
        GameObject rankPillObj = new GameObject("RankPillBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        rankPillObj.transform.SetParent(nrRect, false);
        RectTransform rpRect = rankPillObj.GetComponent<RectTransform>();
        rpRect.anchorMin = rpRect.anchorMax = rpRect.pivot = new Vector2(1f, 0.5f);
        rpRect.anchoredPosition = new Vector2(0f, 0f);
        rpRect.sizeDelta = new Vector2(82f, 28f);

        Image rpBg = rankPillObj.GetComponent<Image>();
        rpBg.sprite = LumiWorldUITheme.PillSprite;
        rpBg.type = Image.Type.Sliced;
        rpBg.color = LumiWorldUITheme.Surface;
        LumiWorldUITheme.AddSoftElevation(rankPillObj, 2f, 0.16f);

        GameObject rpIcObj = new GameObject("RankHexGlyph", typeof(RectTransform), typeof(Image));
        rpIcObj.transform.SetParent(rpRect, false);
        RectTransform rpIcRect = rpIcObj.GetComponent<RectTransform>();
        rpIcRect.anchorMin = rpIcRect.anchorMax = new Vector2(0f, 0.5f);
        rpIcRect.pivot = new Vector2(0f, 0.5f);
        rpIcRect.anchoredPosition = new Vector2(8f, 0f);
        rpIcRect.sizeDelta = new Vector2(15f, 15f);
        rankPillGlyphImg = rpIcObj.GetComponent<Image>();
        rankPillGlyphImg.sprite = LumiWorldUITheme.RankGlyph(1);
        rankPillGlyphImg.preserveAspect = true;
        rankPillGlyphImg.color = LumiWorldUITheme.Text;
        rankPillGlyphImg.raycastTarget = false;

        rankPillText = CreateLabel(rpRect, "RankLabel", "Rank I", 11.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        rankPillText.rectTransform.anchorMin = Vector2.zero;
        rankPillText.rectTransform.anchorMax = Vector2.one;
        rankPillText.rectTransform.offsetMin = new Vector2(27f, 0f);
        rankPillText.rectTransform.offsetMax = new Vector2(-6f, 0f);

        rankPillButton = rankPillObj.GetComponent<Button>();
        rankPillButton.targetGraphic = rpBg;
        rankPillButton.navigation = new Navigation { mode = Navigation.Mode.None };
        rankPillButton.onClick.AddListener(ToggleRankPopover);

        // --- 4. FULLNESS & STATUS CARD ---
        GameObject fullCard = new GameObject("FullnessCard", typeof(RectTransform), typeof(Image));
        fullCard.transform.SetParent(panelDetailRect, false);
        RectTransform fcRect = fullCard.GetComponent<RectTransform>();
        SetTopLeft(fcRect, new Vector2(16f, -268f), new Vector2(230f, 76f));

        Image fcBg = fullCard.GetComponent<Image>();
        fcBg.sprite = LumiWorldUITheme.SurfaceSprite;
        fcBg.type = Image.Type.Sliced;
        fcBg.color = LumiWorldUITheme.Surface;
        LumiWorldUITheme.AddSoftElevation(fullCard, 2.5f, 0.14f);

        TextMeshProUGUI fullTitle = CreateLabel(fcRect, "FullnessLabel", "Fullness", 12.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        SetTopLeft(fullTitle.rectTransform, new Vector2(14f, -8f), new Vector2(96f, 20f));

        hungerProgressLabel = CreateLabel(fcRect, "FullnessValue", "50 / 100", 12f, LumiWorldUITheme.Muted, true, TextAlignmentOptions.MidlineRight);
        SetTopLeft(hungerProgressLabel.rectTransform, new Vector2(120f, -8f), new Vector2(96f, 20f));

        // Progress Bar Track
        GameObject barTrack = new GameObject("ProgressTrack", typeof(RectTransform), typeof(Image));
        barTrack.transform.SetParent(fcRect, false);
        RectTransform btRect = barTrack.GetComponent<RectTransform>();
        SetTopLeft(btRect, new Vector2(14f, -33f), new Vector2(202f, 10f));
        Image btImg = barTrack.GetComponent<Image>();
        btImg.sprite = LumiWorldUITheme.PillSprite;
        btImg.type = Image.Type.Sliced;
        btImg.color = LumiWorldUITheme.Inset;

        GameObject barFill = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
        barFill.transform.SetParent(btRect, false);
        RectTransform bfRect = barFill.GetComponent<RectTransform>();
        bfRect.anchorMin = Vector2.zero;
        bfRect.anchorMax = Vector2.one;
        bfRect.offsetMin = Vector2.zero;
        bfRect.offsetMax = Vector2.zero;
        hungerFillImg = barFill.GetComponent<Image>();
        hungerFillImg.sprite = LumiWorldUITheme.PillSprite;
        hungerFillImg.type = Image.Type.Filled;
        hungerFillImg.fillMethod = Image.FillMethod.Horizontal;
        hungerFillImg.fillOrigin = 0;
        hungerFillImg.fillAmount = 0.5f;
        hungerFillImg.color = LumiWorldUITheme.Success;

        // Status Pill inside Fullness Card
        GameObject stPillObj = new GameObject("StatusPill", typeof(RectTransform), typeof(Image));
        stPillObj.transform.SetParent(fcRect, false);
        RectTransform stPillRect = stPillObj.GetComponent<RectTransform>();
        SetTopLeft(stPillRect, new Vector2(14f, -49f), new Vector2(106f, 20f));
        statusPillBg = stPillObj.GetComponent<Image>();
        statusPillBg.sprite = LumiWorldUITheme.PillSprite;
        statusPillBg.type = Image.Type.Sliced;
        statusPillBg.color = new Color(0.33f, 0.55f, 0.24f, 0.14f);

        statusPillText = CreateLabel(stPillRect, "StatusText", "Healthy", 10.5f, LumiWorldUITheme.Success, true, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(statusPillText.rectTransform);

        // --- 5. FAVORITE / ASSIGNED FOOD CARD ---
        TextMeshProUGUI foodHdr = CreateLabel(panelDetailRect, "FoodHeader", "Food", 12.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        SetTopLeft(foodHdr.rectTransform, new Vector2(16f, -352f), new Vector2(140f, 20f));

        GameObject foodCard = new GameObject("FoodCard", typeof(RectTransform), typeof(Image));
        foodCard.transform.SetParent(panelDetailRect, false);
        RectTransform fdRect = foodCard.GetComponent<RectTransform>();
        SetTopLeft(fdRect, new Vector2(16f, -374f), new Vector2(230f, 44f));

        Image fdBg = foodCard.GetComponent<Image>();
        fdBg.sprite = LumiWorldUITheme.SurfaceSprite;
        fdBg.type = Image.Type.Sliced;
        fdBg.color = LumiWorldUITheme.Surface;
        LumiWorldUITheme.AddSoftElevation(foodCard, 2.5f, 0.14f);

        GameObject fdIcObj = new GameObject("FoodIcon", typeof(RectTransform), typeof(Image));
        fdIcObj.transform.SetParent(fdRect, false);
        RectTransform fdIcRect = fdIcObj.GetComponent<RectTransform>();
        SetTopLeft(fdIcRect, new Vector2(10f, -6f), new Vector2(32f, 32f));
        foodIconImg = fdIcObj.GetComponent<Image>();
        foodIconImg.preserveAspect = true;
        foodIconImg.raycastTarget = false;

        foodNameText = CreateLabel(fdRect, "FoodName", "Raspberries", 13f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        SetTopLeft(foodNameText.rectTransform, new Vector2(50f, -10f), new Vector2(170f, 24f));

        // Feedback notification line above buttons
        feedbackNotifyText = CreateLabel(panelDetailRect, "FeedbackText", "", 10.5f, LumiWorldUITheme.Success, true, TextAlignmentOptions.Center);
        feedbackNotifyText.rectTransform.anchorMin = feedbackNotifyText.rectTransform.anchorMax = feedbackNotifyText.rectTransform.pivot = new Vector2(0.5f, 0f);
        feedbackNotifyText.rectTransform.anchoredPosition = new Vector2(0f, 54f);
        feedbackNotifyText.rectTransform.sizeDelta = new Vector2(230f, 16f);

        // --- 6. SIDE-BY-SIDE FEED (G-22) & TREAT (G-23) BUTTONS ---
        feedButton = CreateActionPillButton(
            panelDetailRect,
            "FeedBtn",
            new Vector2(-59f, 14f),
            new Vector2(110f, 36f),
            isPrimary: true,
            glyphCode: "G-22",
            label: "Feed",
            out feedButtonText);
        feedButton.onClick.AddListener(OnFeedClicked);

        medicineButton = CreateActionPillButton(
            panelDetailRect,
            "TreatBtn",
            new Vector2(59f, 14f),
            new Vector2(110f, 36f),
            isPrimary: false,
            glyphCode: "G-23",
            label: "Treat",
            out medicineButtonText);
        medicineButton.onClick.AddListener(OnMedicineClicked);

        // --- 7. RANK POPOVER ---
        BuildRankPopover(panelDetailRect);

        // --- 8. RENAME MODAL ---
        BuildRenameModal(panelDetailRect);
    }

    private void BuildRankPopover(RectTransform parent)
    {
        rankPopoverObj = new GameObject("RankPopover", typeof(RectTransform), typeof(Image), typeof(Button));
        rankPopoverObj.transform.SetParent(parent, false);
        RectTransform overlayRect = rankPopoverObj.GetComponent<RectTransform>();
        EconomyHUD.Stretch(overlayRect);

        Image overlayImg = rankPopoverObj.GetComponent<Image>();
        overlayImg.color = new Color(0.14f, 0.12f, 0.10f, 0.12f);
        overlayImg.raycastTarget = true;

        Button dismissBtn = rankPopoverObj.GetComponent<Button>();
        dismissBtn.transition = Selectable.Transition.None;
        dismissBtn.onClick.AddListener(CloseRankPopover);

        GameObject cardObj = new GameObject("PopoverCard", typeof(RectTransform), typeof(Image));
        cardObj.transform.SetParent(rankPopoverObj.transform, false);
        RectTransform cardRect = cardObj.GetComponent<RectTransform>();
        SetTopLeft(cardRect, new Vector2(24f, -264f), new Vector2(214f, 134f));

        Image cardBg = cardObj.GetComponent<Image>();
        cardBg.sprite = LumiWorldUITheme.SurfaceSprite;
        cardBg.type = Image.Type.Sliced;
        cardBg.color = LumiWorldUITheme.Surface;
        cardBg.raycastTarget = true;
        cardObj.AddComponent<ResourceUIInputBlocker>();
        LumiWorldUITheme.AddSoftElevation(cardObj, 5f, 0.24f);

        rankPopoverTitleText = CreateLabel(cardRect, "Title", "Rank I", 14f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        rankPopoverTitleText.overflowMode = TextOverflowModes.Overflow;
        SetTopLeft(rankPopoverTitleText.rectTransform, new Vector2(14f, -10f), new Vector2(146f, 24f));

        CreateCloseButton(cardRect, new Vector2(-10f, -10f), CloseRankPopover, 24f);

        rankPopoverCareText = CreateLabel(cardRect, "CareLabel", "Care progress: 100 / 100", 11.5f, LumiWorldUITheme.Muted, true, TextAlignmentOptions.MidlineLeft);
        SetTopLeft(rankPopoverCareText.rectTransform, new Vector2(14f, -38f), new Vector2(186f, 18f));

        GameObject careTrack = new GameObject("CareTrack", typeof(RectTransform), typeof(Image));
        careTrack.transform.SetParent(cardRect, false);
        RectTransform ctRect = careTrack.GetComponent<RectTransform>();
        SetTopLeft(ctRect, new Vector2(14f, -60f), new Vector2(186f, 10f));
        Image ctImg = careTrack.GetComponent<Image>();
        ctImg.sprite = LumiWorldUITheme.PillSprite;
        ctImg.type = Image.Type.Sliced;
        ctImg.color = LumiWorldUITheme.Inset;

        GameObject careFill = new GameObject("CareFill", typeof(RectTransform), typeof(Image));
        careFill.transform.SetParent(ctRect, false);
        RectTransform cfRect = careFill.GetComponent<RectTransform>();
        EconomyHUD.Stretch(cfRect);
        rankPopoverCareFill = careFill.GetComponent<Image>();
        rankPopoverCareFill.sprite = LumiWorldUITheme.PillSprite;
        rankPopoverCareFill.type = Image.Type.Filled;
        rankPopoverCareFill.fillMethod = Image.FillMethod.Horizontal;
        rankPopoverCareFill.fillOrigin = 0;
        rankPopoverCareFill.fillAmount = 1f;
        rankPopoverCareFill.color = LumiWorldUITheme.Success;

        GameObject rBtnObj = new GameObject("RankUpBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        rBtnObj.transform.SetParent(cardRect, false);
        RectTransform rbRect = rBtnObj.GetComponent<RectTransform>();
        rbRect.anchorMin = rbRect.anchorMax = rbRect.pivot = new Vector2(0.5f, 0f);
        rbRect.anchoredPosition = new Vector2(0f, 12f);
        rbRect.sizeDelta = new Vector2(186f, 34f);

        Image rbImg = rBtnObj.GetComponent<Image>();
        rbImg.sprite = LumiWorldUITheme.ButtonPrimarySprite;
        rbImg.type = Image.Type.Sliced;
        rbImg.color = LumiWorldUITheme.Primary;

        rankUpButton = rBtnObj.GetComponent<Button>();
        rankUpButton.targetGraphic = rbImg;
        rankUpButton.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock rbColors = rankUpButton.colors;
        rbColors.disabledColor = Color.white;
        rankUpButton.colors = rbColors;
        rankUpButton.onClick.AddListener(OnRankUpClicked);

        rankUpButtonText = CreateLabel(rbRect, "Label", "Rank Up", 12.5f, Color.white, true, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(rankUpButtonText.rectTransform);

        rankPopoverObj.SetActive(false);
    }

    private void ToggleRankPopover()
    {
        if (rankPopoverObj == null) return;
        bool next = !rankPopoverObj.activeSelf;
        rankPopoverObj.SetActive(next);
        if (next)
        {
            rankPopoverObj.transform.SetAsLastSibling();
            RefreshPanel2(false);
        }
        GameAudioManager.Instance?.PlayClick();
    }

    private void CloseRankPopover()
    {
        if (rankPopoverObj != null && rankPopoverObj.activeSelf)
        {
            rankPopoverObj.SetActive(false);
        }
    }

    private Button CreateActionPillButton(
        Transform parent,
        string name,
        Vector2 bottomPos,
        Vector2 size,
        bool isPrimary,
        string glyphCode,
        string label,
        out TextMeshProUGUI labelTmp)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = bottomPos;
        rt.sizeDelta = size;

        Image img = btnObj.GetComponent<Image>();
        img.sprite = isPrimary ? LumiWorldUITheme.ButtonPrimarySprite : LumiWorldUITheme.ButtonSecondarySprite;
        img.type = Image.Type.Sliced;
        img.color = isPrimary ? LumiWorldUITheme.Primary : LumiWorldUITheme.Inset;
        LumiWorldUITheme.AddSoftElevation(btnObj, 2.5f, 0.16f);

        Color fg = isPrimary ? Color.white : LumiWorldUITheme.Text;

        GameObject icObj = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
        icObj.transform.SetParent(rt, false);
        RectTransform icRect = icObj.GetComponent<RectTransform>();
        icRect.anchorMin = icRect.anchorMax = icRect.pivot = new Vector2(0.5f, 0.5f);
        icRect.anchoredPosition = new Vector2(-22f, 0f);
        icRect.sizeDelta = new Vector2(15f, 15f);
        Image icImg = icObj.GetComponent<Image>();
        icImg.sprite = LumiWorldUITheme.Glyph(glyphCode);
        icImg.preserveAspect = true;
        icImg.color = fg;
        icImg.raycastTarget = false;

        labelTmp = CreateLabel(rt, "Label", label, 13f, fg, true, TextAlignmentOptions.MidlineLeft);
        labelTmp.rectTransform.anchorMin = Vector2.zero;
        labelTmp.rectTransform.anchorMax = Vector2.one;
        labelTmp.rectTransform.offsetMin = new Vector2(44f, 0f);
        labelTmp.rectTransform.offsetMax = new Vector2(-6f, 0f);

        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.onClick.AddListener(() => StartCoroutine(AnimateButtonPunch(rt)));
        return btn;
    }

    private static Button CreateCloseButton(Transform parent, Vector2 topRightPos, Action onClick, float size = 34f)
    {
        GameObject closeBtnObj = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        closeBtnObj.transform.SetParent(parent, false);
        RectTransform closeRect = closeBtnObj.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = topRightPos;
        closeRect.sizeDelta = new Vector2(size, size);

        Image closeBg = closeBtnObj.GetComponent<Image>();
        closeBg.sprite = LumiWorldUITheme.PillSprite;
        closeBg.type = Image.Type.Sliced;
        closeBg.color = LumiWorldUITheme.Inset;

        GameObject icObj = new GameObject("X", typeof(RectTransform), typeof(Image));
        icObj.transform.SetParent(closeRect, false);
        RectTransform icRect = icObj.GetComponent<RectTransform>();
        icRect.anchorMin = icRect.anchorMax = icRect.pivot = new Vector2(0.5f, 0.5f);
        icRect.sizeDelta = new Vector2(size * 0.45f, size * 0.45f);
        Image icImg = icObj.GetComponent<Image>();
        icImg.sprite = LumiWorldUITheme.Glyph("Close");
        icImg.preserveAspect = true;
        icImg.color = LumiWorldUITheme.Text;
        icImg.raycastTarget = false;

        Button btn = closeBtnObj.GetComponent<Button>();
        btn.targetGraphic = closeBg;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.onClick.AddListener(() => onClick?.Invoke());
        return btn;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float size, Color color, bool bold, TextAlignmentOptions align)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
        tmp.font = LumiWorldUITheme.Body;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.color = color;
        tmp.alignment = align;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void SetTopLeft(RectTransform rect, Vector2 pos, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    private void PlayCloseWithAnimation(RectTransform btnRect, Action closeAction)
    {
        StartCoroutine(AnimateButtonPunch(btnRect, closeAction));
    }

    private IEnumerator AnimateButtonPunch(RectTransform targetRect, Action onComplete = null)
    {
        if (targetRect == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        Vector3 originalScale = targetRect.localScale;
        float halfDuration = 0.07f;
        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / halfDuration;
            targetRect.localScale = Vector3.Lerp(originalScale, originalScale * 0.94f, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / halfDuration;
            targetRect.localScale = Vector3.Lerp(originalScale * 0.94f, originalScale, t);
            yield return null;
        }

        targetRect.localScale = originalScale;
        onComplete?.Invoke();
    }

    private void BuildRenameModal(Transform parent)
    {
        renameModal = new GameObject("RenameModal", typeof(RectTransform), typeof(Image), typeof(Button));
        renameModal.transform.SetParent(parent, false);
        RectTransform modalRect = renameModal.GetComponent<RectTransform>();
        EconomyHUD.Stretch(modalRect);

        Image modalBg = renameModal.GetComponent<Image>();
        modalBg.color = LumiWorldUITheme.Backdrop;
        modalBg.raycastTarget = true;

        Button modalDimmerBtn = renameModal.GetComponent<Button>();
        modalDimmerBtn.transition = Selectable.Transition.None;
        modalDimmerBtn.onClick.AddListener(CloseRenameModal);

        GameObject boxObj = new GameObject("DialogBox", typeof(RectTransform), typeof(Image));
        boxObj.transform.SetParent(renameModal.transform, false);
        RectTransform boxRect = boxObj.GetComponent<RectTransform>();
        boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.anchoredPosition = new Vector2(0f, 12f);
        boxRect.sizeDelta = new Vector2(232f, 160f);

        Image boxBg = boxObj.GetComponent<Image>();
        boxBg.sprite = LumiWorldUITheme.SurfaceSprite;
        boxBg.type = Image.Type.Sliced;
        boxBg.color = LumiWorldUITheme.Surface;
        boxObj.AddComponent<ResourceUIInputBlocker>();
        LumiWorldUITheme.AddSoftElevation(boxObj, 5f, 0.24f);

        TextMeshProUGUI titleTmp = CreateLabel(boxRect, "Title", "Rename creature", 14f, LumiWorldUITheme.Text, true, TextAlignmentOptions.Center);
        SetTopLeft(titleTmp.rectTransform, new Vector2(12f, -14f), new Vector2(208f, 22f));

        TextMeshProUGUI descTmp = CreateLabel(boxRect, "Desc", "Enter creature nickname:", 11f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.Center);
        SetTopLeft(descTmp.rectTransform, new Vector2(12f, -36f), new Vector2(208f, 16f));

        GameObject inputObj = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputObj.transform.SetParent(boxObj.transform, false);
        RectTransform inRect = inputObj.GetComponent<RectTransform>();
        SetTopLeft(inRect, new Vector2(16f, -60f), new Vector2(200f, 34f));

        Image inBg = inputObj.GetComponent<Image>();
        inBg.sprite = LumiWorldUITheme.SurfaceSprite;
        inBg.type = Image.Type.Sliced;
        inBg.color = LumiWorldUITheme.Window;

        GameObject textArea = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        textArea.transform.SetParent(inputObj.transform, false);
        RectTransform taRect = textArea.GetComponent<RectTransform>();
        taRect.anchorMin = Vector2.zero;
        taRect.anchorMax = Vector2.one;
        taRect.offsetMin = new Vector2(10f, 2f);
        taRect.offsetMax = new Vector2(-10f, -2f);

        TextMeshProUGUI phComp = CreateLabel(taRect, "Placeholder", "Enter new name...", 11.5f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.MidlineLeft);
        EconomyHUD.Stretch(phComp.rectTransform);

        TextMeshProUGUI valComp = CreateLabel(taRect, "Text", "", 12.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        EconomyHUD.Stretch(valComp.rectTransform);

        renameInputField = inputObj.GetComponent<TMP_InputField>();
        renameInputField.textViewport = taRect;
        renameInputField.textComponent = valComp;
        renameInputField.placeholder = phComp;
        renameInputField.characterLimit = 20;

        // Cancel & Save buttons
        GameObject cancelBtnObj = new GameObject("CancelBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        cancelBtnObj.transform.SetParent(boxRect, false);
        RectTransform cbRect = cancelBtnObj.GetComponent<RectTransform>();
        cbRect.anchorMin = cbRect.anchorMax = cbRect.pivot = new Vector2(0f, 0f);
        cbRect.anchoredPosition = new Vector2(16f, 14f);
        cbRect.sizeDelta = new Vector2(94f, 32f);
        Image cbImg = cancelBtnObj.GetComponent<Image>();
        cbImg.sprite = LumiWorldUITheme.ButtonSecondarySprite;
        cbImg.type = Image.Type.Sliced;
        cbImg.color = LumiWorldUITheme.Inset;
        TextMeshProUGUI cbTxt = CreateLabel(cbRect, "Label", "Cancel", 12f, LumiWorldUITheme.Text, true, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(cbTxt.rectTransform);
        cancelBtnObj.GetComponent<Button>().onClick.AddListener(CloseRenameModal);

        GameObject confirmBtnObj = new GameObject("ConfirmBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        confirmBtnObj.transform.SetParent(boxRect, false);
        RectTransform cfbRect = confirmBtnObj.GetComponent<RectTransform>();
        cfbRect.anchorMin = cfbRect.anchorMax = cfbRect.pivot = new Vector2(1f, 0f);
        cfbRect.anchoredPosition = new Vector2(-16f, 14f);
        cfbRect.sizeDelta = new Vector2(94f, 32f);
        Image cfbImg = confirmBtnObj.GetComponent<Image>();
        cfbImg.sprite = LumiWorldUITheme.ButtonPrimarySprite;
        cfbImg.type = Image.Type.Sliced;
        cfbImg.color = LumiWorldUITheme.Primary;
        TextMeshProUGUI cfbTxt = CreateLabel(cfbRect, "Label", "Save", 12f, Color.white, true, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(cfbTxt.rectTransform);
        confirmBtnObj.GetComponent<Button>().onClick.AddListener(OnConfirmRename);

        renameModal.SetActive(false);
    }

    #endregion

    #region Data Refresh

    private float nextResourceRefresh;
    private string lastPanelStructureKey;

    private void Update()
    {
        if (!IsOpen) return;
        SyncPanelsStageScale();
        if (currentHabitat == null) return;
        if (Time.unscaledTime >= nextResourceRefresh)
        {
            nextResourceRefresh = Time.unscaledTime + 1f;
            RefreshResourceProductionSlots();
            if (panelDetailRect != null && panelDetailRect.gameObject.activeSelf)
                RefreshPanel2(false);
        }
    }

    private void RefreshResourceProductionSlots()
    {
        if (currentHabitat == null || resourceSlotsRoot == null) return;
        var runtime = ResourceProductionRuntime.Instance;
        ResourceUIHabitat habModel = null;
        if (runtime != null && runtime.HabitatManager != null && runtime.HabitatManager.Catalog != null)
        {
            var liveHabitat = runtime.HabitatManager.FindHabitat(currentHabitat.habitatId);
            if (liveHabitat == null || !liveHabitat.IsFormedBiome)
            {
                ClosePanels();
                return;
            }
            if (liveHabitat != currentHabitat)
            {
                RefreshCurrentHabitat();
                return;
            }
            var prodState = runtime.FindState(currentHabitat.habitatId);
            habModel = ResourceInventoryUIPresentation.Habitat(currentHabitat, prodState, runtime.HabitatManager.Catalog, runtime.Settings);
        }

        if (habModel != null && habModel.structureKey != lastPanelStructureKey)
        {
            RefreshPanel1();
            return;
        }

        long totalPending = 0;
        if (habModel != null && habModel.buffers != null)
        {
            foreach (var b in habModel.buffers)
            {
                if (b != null) totalPending += b.amount;
            }
        }

        UpdateCollectAllButtonState(totalPending);
    }

    private void UpdateCollectAllButtonState(long totalPending)
    {
        if (harvestAllButton == null || harvestAllButtonText == null) return;
        bool active = totalPending > 0;
        harvestAllButton.interactable = active;
        harvestAllButton.image.color = active
            ? LumiWorldUITheme.Primary
            : new Color(0.62f, 0.59f, 0.55f, 1f);
        harvestAllButtonText.color = Color.white;
        if (harvestAllIconImg != null) harvestAllIconImg.color = Color.white;
        harvestAllButtonText.text = $"Collect all ({totalPending})";
    }

    public void RefreshCurrentHabitat()
    {
        if (currentHabitat == null) return;
        var runtime = ResourceProductionRuntime.Instance;
        if (runtime != null && runtime.HabitatManager != null)
        {
            foreach (var h in runtime.HabitatManager.Habitats)
            {
                if (h != null && h.habitatId == currentHabitat.habitatId)
                {
                    currentHabitat = h;
                    if (currentSelectedResident != null && h.residents != null)
                    {
                        string residentId = currentSelectedResident.individualId;
                        var liveResident = h.residents.Find(r => r != null && r.individualId == residentId);
                        if (liveResident != null) currentSelectedResident = liveResident;
                    }
                    break;
                }
            }
        }
        RefreshPanel1();
        if (currentSelectedResident != null)
        {
            RefreshPanel2(false);
        }
    }

    private void OnHarvestAllClicked()
    {
        if (currentHabitat == null) return;
        var runtime = ResourceProductionRuntime.Instance;
        if (runtime != null)
        {
            runtime.CollectHabitat(currentHabitat.habitatId);
            RefreshCurrentHabitat();
        }
    }

    private void OnChangePrimaryResourceClicked()
    {
        if (currentHabitat == null || ResourceInventoryUIController.Instance == null) return;
        ResourceInventoryUIController.Instance.OpenSelectorFor(currentHabitat.habitatId, ResourceTier.Tier1, 0);
    }

    private void RefreshPanel1()
    {
        if (currentHabitat == null) return;
        if (changeResourceBtn != null)
            changeResourceBtn.interactable = HabitatEligibilityService.IsSlotUnlocked(currentHabitat, ResourceTier.Tier1, 0);

        string affName = HabitatCreaturePanelStyle.GetAffinityDisplayName(currentHabitat.affinity);
        if (habitatNameText != null) habitatNameText.text = affName;

        if (biomeEmblemImg != null)
        {
            biomeEmblemImg.sprite = LumiWorldUITheme.BiomeEmblem(affName);
            biomeEmblemImg.color = biomeEmblemImg.sprite != null ? Color.white : Color.clear;
        }

        int hexCount = currentHabitat.HexCount;
        if (footprintGlyphImg != null)
        {
            footprintGlyphImg.sprite = LumiWorldUITheme.FootprintGlyph(hexCount);
        }
        if (footprintCountText != null)
        {
            footprintCountText.text = $"{hexCount} hex";
        }

        int currentResidents = currentHabitat.residents != null ? currentHabitat.residents.Count : 0;
        int maxCapacity = currentHabitat.CreatureCapacity;
        if (residentsHeaderText != null)
        {
            residentsHeaderText.text = $"Residents ({currentResidents}/{maxCapacity})";
        }

        // If selected resident was removed from habitat, close P-06 cleanly
        if (currentSelectedResident != null &&
            (currentHabitat.residents == null ||
             !currentHabitat.residents.Exists(r => r != null && r.individualId == currentSelectedResident.individualId)))
        {
            currentSelectedResident = null;
            currentSelectedAnimal = null;
            if (panelDetailRect != null) panelDetailRect.gameObject.SetActive(false);
        }

        // --- 1. RENDER RESIDENTS WITH SYMMETRIC ROW CENTERING & EXPAND FOR 4-6 RESIDENTS ---
        UpdatePanel1DynamicLayout(currentResidents);

        foreach (var old in spawnedSlotButtons)
        {
            if (old != null) Destroy(old);
        }
        spawnedSlotButtons.Clear();

        if (currentResidents == 0)
        {
            TextMeshProUGUI emptyLbl = CreateLabel(
                creatureGridRoot,
                "EmptyResidents",
                "No residents placed in this biome yet",
                12f,
                LumiWorldUITheme.Muted,
                true,
                TextAlignmentOptions.Center);
            EconomyHUD.Stretch(emptyLbl.rectTransform);
            spawnedSlotButtons.Add(emptyLbl.gameObject);
        }
        else
        {
            const float cardW = 90f;
            float cardH = currentResidents >= 4 ? 98f : 106f;
            const float gapX = 8f;
            const float gapY = 6f;
            const float topPad = 8f;

            List<HabitatResident> validResidents = new List<HabitatResident>();
            for (int i = 0; i < currentHabitat.residents.Count && validResidents.Count < 6; i++)
            {
                if (currentHabitat.residents[i] != null) validResidents.Add(currentHabitat.residents[i]);
            }

            int totalToShow = validResidents.Count;
            for (int i = 0; i < totalToShow; i++)
            {
                int row = i / 3;
                int col = i % 3;
                int itemsInRow = Mathf.Min(3, totalToShow - row * 3);
                float rowWidth = itemsInRow * cardW + Mathf.Max(0, itemsInRow - 1) * gapX;
                float startX = (P05ResidentsWidth - rowWidth) * 0.5f;
                float cardX = startX + col * (cardW + gapX);
                float cardY = -(topPad + row * (cardH + gapY));

                GameObject cardObj = CreateResidentPortraitCard(i, validResidents[i], cardX, cardY, cardW, cardH);
                spawnedSlotButtons.Add(cardObj);
            }
        }

        // --- 2. PRODUCTION SLOTS ---
        foreach (var old in spawnedResourceCards)
        {
            if (old != null) Destroy(old);
        }
        spawnedResourceCards.Clear();

        var runtime = ResourceProductionRuntime.Instance;
        ResourceUIHabitat habModel = null;
        if (runtime != null && runtime.HabitatManager != null && runtime.HabitatManager.Catalog != null)
        {
            var prodState = runtime.FindState(currentHabitat.habitatId);
            habModel = ResourceInventoryUIPresentation.Habitat(currentHabitat, prodState, runtime.HabitatManager.Catalog, runtime.Settings);
        }
        lastPanelStructureKey = habModel?.structureKey;

        var slotConfigs = new (ResourceTier tier, int subIndex)[]
        {
            (ResourceTier.Tier1, 0),
            (ResourceTier.Tier1, 1),
            (ResourceTier.Tier2, 0),
            (ResourceTier.Tier2, 1)
        };

        for (int i = 0; i < slotConfigs.Length; i++)
        {
            var (tier, subIndex) = slotConfigs[i];
            ResourceUISlot slotData = null;
            if (habModel != null && habModel.slots != null)
            {
                slotData = habModel.slots.Find(s => s != null && s.tier == tier && s.index == subIndex);
            }

            long pendingForSlot = 0;
            if (slotData != null && !string.IsNullOrEmpty(slotData.resourceId) && habModel?.buffers != null)
            {
                var buf = habModel.buffers.Find(b => b != null && string.Equals(b.resourceId, slotData.resourceId, StringComparison.OrdinalIgnoreCase));
                if (buf != null) pendingForSlot = buf.amount;
            }

            GameObject cardObj = CreateResourceSlotCard(i, tier, subIndex, slotData, pendingForSlot, resourceSlotsRoot);
            spawnedResourceCards.Add(cardObj);
        }

        long totalPending = 0;
        if (habModel != null && habModel.buffers != null)
        {
            foreach (var b in habModel.buffers)
            {
                if (b != null) totalPending += b.amount;
            }
        }
        UpdateCollectAllButtonState(totalPending);
    }

    private GameObject CreateResidentPortraitCard(int index, HabitatResident resident, float x, float y, float cardW, float cardH)
    {
        GameObject cardObj = new GameObject($"Resident_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        cardObj.transform.SetParent(creatureGridRoot, false);
        RectTransform rt = cardObj.GetComponent<RectTransform>();
        SetTopLeft(rt, new Vector2(x, y), new Vector2(cardW, cardH));

        Image cardBg = cardObj.GetComponent<Image>();
        cardBg.sprite = LumiWorldUITheme.SurfaceSprite;
        cardBg.type = Image.Type.Sliced;
        cardBg.color = LumiWorldUITheme.Slot;
        LumiWorldUITheme.AddSoftElevation(cardObj, 3f, 0.16f);

        // Official zoomed portrait from Creatures/Portraits/
        Sprite portrait = HabitatCreaturePanelStyle.GetCreatureAvatar(resident.speciesId, resident.qualityRank);
        if (portrait != null)
        {
            GameObject avtObj = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            avtObj.transform.SetParent(rt, false);
            RectTransform avtRect = avtObj.GetComponent<RectTransform>();
            SetTopLeft(avtRect, new Vector2((cardW - 58f) * 0.5f, -6f), new Vector2(58f, 58f));
            Image avtImg = avtObj.GetComponent<Image>();
            avtImg.sprite = portrait;
            avtImg.preserveAspect = true;
            avtImg.raycastTarget = false;
        }

        var resData = HabitatCreaturePanelStyle.ResolveCreatureData(resident.speciesId);
        string defaultName = !string.IsNullOrEmpty(resData.canonicalAvt) ? resData.canonicalAvt : resData.displayName;
        string cName = GetCreatureName(resident, defaultName);

        TextMeshProUGUI nameLbl = CreateLabel(rt, "Name", cName, 11.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.Center);
        nameLbl.enableAutoSizing = true;
        nameLbl.fontSizeMin = 9.5f;
        nameLbl.fontSizeMax = 12f;
        SetTopLeft(nameLbl.rectTransform, new Vector2(4f, -66f), new Vector2(cardW - 8f, 18f));

        // Status ("Working" vs "Needs care") + "!" warning badge when hungry
        var hunger = ResourceProductionRuntime.Instance != null
            ? ResourceProductionRuntime.Instance.GetCareState(resident.individualId)
            : null;
        bool needsCare = hunger != null && hunger.Status != CreatureHungerStatus.Healthy;

        TextMeshProUGUI statusLbl = CreateLabel(
            rt,
            "Status",
            needsCare ? "● Needs care" : "● Working",
            9.8f,
            needsCare ? LumiWorldUITheme.Warning : LumiWorldUITheme.Success,
            true,
            TextAlignmentOptions.Center);
        statusLbl.overflowMode = TextOverflowModes.Overflow;
        SetTopLeft(statusLbl.rectTransform, new Vector2(4f, -85f), new Vector2(cardW - 8f, 16f));

        if (needsCare)
        {
            GameObject warnObj = new GameObject("WarningBadge", typeof(RectTransform), typeof(Image));
            warnObj.transform.SetParent(rt, false);
            RectTransform warnRect = warnObj.GetComponent<RectTransform>();
            warnRect.anchorMin = warnRect.anchorMax = warnRect.pivot = new Vector2(1f, 1f);
            warnRect.anchoredPosition = new Vector2(-5f, -5f);
            warnRect.sizeDelta = new Vector2(16f, 16f);
            Image warnImg = warnObj.GetComponent<Image>();
            warnImg.sprite = LumiWorldUITheme.StatusLowSprite;
            warnImg.preserveAspect = true;
            warnImg.raycastTarget = false;
        }

        // Selected border (3px dark graphite #403F3C, no checkmark per P-05 spec)
        bool isSelected = currentSelectedResident != null &&
                          string.Equals(currentSelectedResident.individualId, resident.individualId, StringComparison.Ordinal);
        if (isSelected)
        {
            GameObject selObj = new GameObject("SelectedBorder", typeof(RectTransform), typeof(Image));
            selObj.transform.SetParent(rt, false);
            EconomyHUD.Stretch(selObj.GetComponent<RectTransform>());
            Image selImg = selObj.GetComponent<Image>();
            selImg.sprite = GetSelectionOutlineSprite();
            selImg.type = Image.Type.Sliced;
            selImg.color = Color.white;
            selImg.raycastTarget = false;
        }

        Button btn = cardObj.GetComponent<Button>();
        btn.targetGraphic = cardBg;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        HabitatResident capturedResident = resident;
        btn.onClick.AddListener(() => SelectResident(capturedResident));

        return cardObj;
    }

    private GameObject CreateResourceSlotCard(int slotIndex, ResourceTier tier, int subIndex, ResourceUISlot slotData, long pendingAmount, Transform parent)
    {
        bool isUnlocked = slotData != null ? slotData.unlocked : HabitatEligibilityService.IsSlotUnlocked(currentHabitat, tier, subIndex);
        bool hasAssignedResource = isUnlocked && slotData != null && !string.IsNullOrEmpty(slotData.resourceId);
        string resName = hasAssignedResource ? slotData.name : (isUnlocked ? "Unassigned slot" : "Locked slot");
        Sprite icon = slotData != null ? slotData.icon : null;
        double rate = slotData != null ? slotData.rate : 0.0;
        string reason = slotData != null ? slotData.reason : (!isUnlocked ? "Unlocks at " + ResourceInventoryUILayout.UnlockHexCount(tier, subIndex) + " Hexes" : "");
        bool isProducing = isUnlocked && rate > 0.001;

        GameObject cardObj = new GameObject($"ResSlot_{slotIndex}", typeof(RectTransform), typeof(Image));
        cardObj.transform.SetParent(parent, false);
        RectTransform rt = cardObj.GetComponent<RectTransform>();
        SetTopLeft(rt, new Vector2(0f, -slotIndex * P05SlotCardPitch), new Vector2(P05ResidentsWidth, P05SlotCardHeight));

        Image bg = cardObj.GetComponent<Image>();
        bg.sprite = LumiWorldUITheme.SurfaceSprite;
        bg.type = Image.Type.Sliced;
        bg.color = isUnlocked ? LumiWorldUITheme.Surface : LumiWorldUITheme.Inset;
        if (isUnlocked) LumiWorldUITheme.AddSoftElevation(cardObj, 2.5f, 0.14f);

        // 1. Icon Box (34x34)
        GameObject iconBox = new GameObject("IconBox", typeof(RectTransform), typeof(Image));
        iconBox.transform.SetParent(rt, false);
        RectTransform ibRect = iconBox.GetComponent<RectTransform>();
        SetTopLeft(ibRect, new Vector2(7f, -6f), new Vector2(34f, 34f));
        Image ibImg = iconBox.GetComponent<Image>();
        ibImg.sprite = LumiWorldUITheme.SurfaceSprite;
        ibImg.type = Image.Type.Sliced;
        ibImg.color = LumiWorldUITheme.Slot;

        if (icon != null && isUnlocked)
        {
            GameObject icObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icObj.transform.SetParent(ibRect, false);
            RectTransform icRect = icObj.GetComponent<RectTransform>();
            icRect.anchorMin = Vector2.zero;
            icRect.anchorMax = Vector2.one;
            icRect.offsetMin = new Vector2(4f, 4f);
            icRect.offsetMax = new Vector2(-4f, -4f);
            Image icImg = icObj.GetComponent<Image>();
            icImg.sprite = icon;
            icImg.preserveAspect = true;
            icImg.raycastTarget = false;
        }
        else
        {
            GameObject lockObj = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
            lockObj.transform.SetParent(ibRect, false);
            RectTransform lockRect = lockObj.GetComponent<RectTransform>();
            lockRect.anchorMin = lockRect.anchorMax = lockRect.pivot = new Vector2(0.5f, 0.5f);
            lockRect.sizeDelta = new Vector2(16f, 16f);
            Image lockImg = lockObj.GetComponent<Image>();
            lockImg.sprite = LumiWorldUITheme.Glyph(isUnlocked ? "Plus" : "Lock");
            lockImg.preserveAspect = true;
            lockImg.color = LumiWorldUITheme.Muted;
            lockImg.raycastTarget = false;
        }

        // 2. Title & Status lines
        TextMeshProUGUI l1Text = CreateLabel(rt, "Title", resName, 12f, isUnlocked ? LumiWorldUITheme.Text : LumiWorldUITheme.Muted, true, TextAlignmentOptions.MidlineLeft);
        SetTopLeft(l1Text.rectTransform, new Vector2(49f, -4f), new Vector2(179f, 19f));

        string subStr;
        Color subCol = LumiWorldUITheme.Muted;
        if (!isUnlocked)
        {
            subStr = reason;
        }
        else if (pendingAmount > 0)
        {
            subStr = $"Ready · ×{pendingAmount}";
            subCol = LumiWorldUITheme.Success;
        }
        else if (isProducing)
        {
            subStr = $"Producing · {rate:0.##}/min";
            subCol = LumiWorldUITheme.Success;
        }
        else if (!hasAssignedResource)
        {
            subStr = "Tap to assign resource";
        }
        else
        {
            subStr = reason;
            subCol = LumiWorldUITheme.Warning;
        }

        TextMeshProUGUI l2Text = CreateLabel(rt, "Subtitle", subStr, 10f, subCol, true, TextAlignmentOptions.MidlineLeft);
        SetTopLeft(l2Text.rectTransform, new Vector2(49f, -23f), new Vector2(179f, 17f));

        // 3. Action button on the right ("Assign" / "Change")
        if (isUnlocked)
        {
            GameObject btnObj = new GameObject("SlotActionBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(rt, false);
            RectTransform bRect = btnObj.GetComponent<RectTransform>();
            bRect.anchorMin = bRect.anchorMax = bRect.pivot = new Vector2(1f, 0.5f);
            bRect.anchoredPosition = new Vector2(-8f, 0f);
            bRect.sizeDelta = new Vector2(66f, 28f);

            Image btnImg = btnObj.GetComponent<Image>();
            btnImg.sprite = LumiWorldUITheme.PillSprite;
            btnImg.type = Image.Type.Sliced;
            btnImg.color = hasAssignedResource ? LumiWorldUITheme.Inset : LumiWorldUITheme.Primary;

            TextMeshProUGUI lblText = CreateLabel(
                bRect,
                "Label",
                hasAssignedResource ? "Change" : "Assign",
                11f,
                hasAssignedResource ? LumiWorldUITheme.Text : Color.white,
                true,
                TextAlignmentOptions.Center);
            EconomyHUD.Stretch(lblText.rectTransform);

            Button btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            ResourceTier capturedTier = tier;
            int capturedIndex = subIndex;
            btn.onClick.AddListener(() =>
            {
                if (ResourceInventoryUIController.Instance != null)
                {
                    ResourceInventoryUIController.Instance.OpenSelectorFor(currentHabitat.habitatId, capturedTier, capturedIndex);
                }
            });
        }

        return cardObj;
    }

    private void SelectResident(HabitatResident resident)
    {
        if (resident == null) return;
        currentSelectedResident = resident;
        currentSelectedAnimal = FindAnimalIndividual(resident.individualId, resident.speciesId);
        CloseRankPopover();

        if (panelDetailRect != null)
        {
            panelDetailRect.gameObject.SetActive(true);
            RefreshPanel2(true);
        }

        RefreshPanel1();
    }

    private void RefreshPanel2(bool reloadModel = true)
    {
        if (currentSelectedResident == null || currentHabitat == null) return;

        string affName = HabitatCreaturePanelStyle.GetAffinityDisplayName(currentHabitat.affinity);
        if (backToBiomeText != null)
        {
            backToBiomeText.text = $"Back to {affName}";
        }

        var resData = HabitatCreaturePanelStyle.ResolveCreatureData(currentSelectedResident.speciesId);
        string defaultName = !string.IsNullOrEmpty(resData.canonicalAvt) ? resData.canonicalAvt : resData.displayName;
        if (creatureNameText != null)
        {
            creatureNameText.text = GetCreatureName(currentSelectedResident, defaultName);
        }

        if (reloadModel && creatureArtImg != null)
        {
            Sprite detailSprite = HabitatCreaturePanelStyle.GetCreatureDetailSprite(currentSelectedResident.speciesId);
            if (detailSprite == null)
            {
                detailSprite = HabitatCreaturePanelStyle.GetCreatureAvatar(currentSelectedResident.speciesId, currentSelectedResident.qualityRank);
            }
            creatureArtImg.sprite = detailSprite;
            creatureArtImg.enabled = detailSprite != null;
            creatureArtImg.color = Color.white;
        }

        // Rank Pill (1/2/3 hex glyph + Rank I/II/III)
        int rankNum = currentSelectedResident.qualityRank == CreatureQualityRank.RankI ? 1
            : (currentSelectedResident.qualityRank == CreatureQualityRank.RankII ? 2 : 3);
        string rankLabel = rankNum == 1 ? "Rank I" : (rankNum == 2 ? "Rank II" : "Rank III");

        if (rankPillGlyphImg != null)
        {
            rankPillGlyphImg.sprite = LumiWorldUITheme.RankGlyph(rankNum);
        }
        if (rankPillText != null)
        {
            rankPillText.text = rankLabel;
        }

        string indId = currentSelectedResident.individualId;
        int care = CreatureCareManager.Instance != null ? CreatureCareManager.Instance.GetCareProgress(indId) : 100;
        var hunger = ResourceProductionRuntime.Instance != null ? ResourceProductionRuntime.Instance.GetCareState(indId) : null;
        int satiety = hunger != null ? Mathf.Clamp(Mathf.RoundToInt((float)hunger.Satiety), 0, 100) : 100;

        // Fullness & Status
        if (hungerProgressLabel != null)
        {
            hungerProgressLabel.text = $"{satiety} / 100";
        }
        if (hungerFillImg != null)
        {
            hungerFillImg.fillAmount = Mathf.Clamp01(satiety / 100f);
            bool isHungry = hunger != null && hunger.Status != CreatureHungerStatus.Healthy;
            hungerFillImg.color = isHungry ? LumiWorldUITheme.Warning : LumiWorldUITheme.Success;
        }

        if (statusPillText != null && statusPillBg != null)
        {
            bool isHungry = hunger != null && hunger.Status != CreatureHungerStatus.Healthy;
            statusPillText.text = isHungry ? "Needs food" : "Healthy";
            statusPillText.color = isHungry ? LumiWorldUITheme.Warning : LumiWorldUITheme.Success;
            statusPillBg.color = isHungry
                ? new Color(0.84f, 0.51f, 0.16f, 0.14f)
                : new Color(0.33f, 0.55f, 0.24f, 0.14f);
        }

        // Food Card (Icon + Name only, no pantry/warehouse rows per P-06 spec)
        var production = ResourceProductionRuntime.Instance;
        string foodId = production != null && production.CareSettings != null
            ? production.CareSettings.FoodResourceId : string.Empty;
        ResourceData foodData = production != null && production.CareSettings != null &&
            production.CareSettings.food != null ? production.CareSettings.food : ResourceCatalog.Find(foodId);
        string foodName = ResourceInventoryUIPresentation.LocalizeResourceName(foodId, foodData != null ? foodData.resourceName : "Raspberries");

        if (foodIconImg != null)
        {
            foodIconImg.sprite = foodData != null ? foodData.icon : null;
            foodIconImg.color = foodIconImg.sprite != null ? Color.white : Color.clear;
        }
        if (foodNameText != null)
        {
            foodNameText.text = foodName;
        }

        // Rank Popover refresh
        if (rankPopoverTitleText != null) rankPopoverTitleText.text = rankLabel;
        if (rankPopoverCareText != null) rankPopoverCareText.text = $"Care progress: {care} / 100";
        if (rankPopoverCareFill != null) rankPopoverCareFill.fillAmount = Mathf.Clamp01(care / 100f);

        if (rankUpButton != null && rankUpButtonText != null)
        {
            bool canRankUp = care >= 100 && currentSelectedResident.qualityRank < CreatureQualityRank.RankIII;
            rankUpButton.interactable = canRankUp;
            rankUpButton.image.color = canRankUp ? LumiWorldUITheme.Primary : new Color(0.62f, 0.59f, 0.55f, 1f);
            rankUpButtonText.color = Color.white;
            rankUpButtonText.text = currentSelectedResident.qualityRank >= CreatureQualityRank.RankIII
                ? "Max Rank"
                : "Rank Up";
        }
    }

    private static Sprite GetSelectionOutlineSprite()
    {
        if (s_SelectionOutlineSprite != null) return s_SelectionOutlineSprite;

        int w = 64, h = 64, r = 12;
        float stroke = 3f;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = (x < r) ? r - x : (x > w - 1 - r ? x - (w - 1 - r) : 0f);
                float dy = (y < r) ? r - y : (y > h - 1 - r ? y - (h - 1 - r) : 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > r)
                    px[y * w + x] = Color.clear;
                else if (dist >= r - stroke || x < stroke || x >= w - stroke || y < stroke || y >= h - stroke)
                    px[y * w + x] = LumiWorldUITheme.Primary;
                else
                    px[y * w + x] = Color.clear;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        s_SelectionOutlineSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
        return s_SelectionOutlineSprite;
    }

    #endregion

    #region Action Buttons

    private void OnFeedClicked()
    {
        if (currentSelectedResident == null || currentHabitat == null) return;
        var production = ResourceProductionRuntime.Instance;
        if (production == null || production.CareSettings == null) return;

        string foodId = production.CareSettings.FoodResourceId;
        ResourceData foodData = production.CareSettings.food != null
            ? production.CareSettings.food : ResourceCatalog.Find(foodId);
        string foodName = ResourceInventoryUIPresentation.LocalizeResourceName(foodId, foodData != null ? foodData.resourceName : "food");
        int available = production.Inventory != null ? production.Inventory.GetAmount(foodId) : 0;
        int amount = Mathf.Min(available, production.CareSettings.seedFoodUnitsToFull);
        string reason = string.Empty;
        bool deposited = amount > 0 && production.DepositCreatureFood(currentHabitat.habitatId,
            currentSelectedResident.individualId, foodId, amount, out reason);

        string feedback = deposited
            ? $"Fed {amount} {foodName}."
            : (available == 0 ? "Out of food in Inventory." : reason);

        ShowFeedback(feedback, deposited ? LumiWorldUITheme.Success : LumiWorldUITheme.Error);
        RefreshPanel1();
        RefreshPanel2(false);
    }

    private void OnMedicineClicked()
    {
        if (currentSelectedResident == null) return;
        ShowFeedback("Treatment is still in development.", LumiWorldUITheme.Warning);
    }

    private void OnRankUpClicked()
    {
        if (currentSelectedResident == null || CreatureCareManager.Instance == null) return;

        HabitatPlacementIdentity identity = null;
        if (currentSelectedAnimal != null)
        {
            identity = currentSelectedAnimal.GetComponentInParent<HabitatPlacementIdentity>();
        }

        if (CreatureCareManager.Instance.TryRankUp(currentSelectedAnimal, identity, out string resultInfo))
        {
            if (currentSelectedResident.qualityRank == CreatureQualityRank.RankI)
                currentSelectedResident.qualityRank = CreatureQualityRank.RankII;
            else if (currentSelectedResident.qualityRank == CreatureQualityRank.RankII)
                currentSelectedResident.qualityRank = CreatureQualityRank.RankIII;

            ShowFeedback(resultInfo, LumiWorldUITheme.Success);
            CloseRankPopover();
            RefreshPanel1();
            RefreshPanel2(false);
        }
        else
        {
            ShowFeedback(resultInfo, LumiWorldUITheme.Error);
        }
    }

    private void ShowFeedback(string msg, Color col)
    {
        if (feedbackNotifyText == null) return;
        feedbackNotifyText.text = msg;
        feedbackNotifyText.color = col;

        if (feedbackFadeRoutine != null) StopCoroutine(feedbackFadeRoutine);
        feedbackFadeRoutine = StartCoroutine(FadeFeedbackRoutine());
    }

    private IEnumerator FadeFeedbackRoutine()
    {
        yield return new WaitForSeconds(3.0f);
        if (feedbackNotifyText != null)
        {
            feedbackNotifyText.text = "";
        }
    }

    private AnimalIndividual FindAnimalIndividual(string individualId, string speciesId)
    {
        var allAnimals = FindObjectsByType<AnimalIndividual>(FindObjectsSortMode.None);
        foreach (var a in allAnimals)
        {
            if (a == null) continue;
            if (string.Equals(CreatureCareManager.KeyOf(a, null), individualId, StringComparison.Ordinal) ||
                string.Equals(a.UniqueID, individualId, StringComparison.OrdinalIgnoreCase)) return a;
            if (a.SpeciesData != null && string.Equals(a.SpeciesData.speciesID, speciesId, StringComparison.OrdinalIgnoreCase)) return a;
        }
        return null;
    }

    private void OnRenameClicked()
    {
        if (currentSelectedResident == null) return;
        var resData = HabitatCreaturePanelStyle.ResolveCreatureData(currentSelectedResident.speciesId);
        string defaultName = !string.IsNullOrEmpty(resData.canonicalAvt) ? resData.canonicalAvt : resData.displayName;
        string curName = GetCreatureName(currentSelectedResident, defaultName);

        if (renameInputField != null)
        {
            renameInputField.text = curName;
            renameInputField.ActivateInputField();
        }

        if (renameModal != null)
        {
            renameModal.SetActive(true);
            renameModal.transform.SetAsLastSibling();
        }
    }

    private void CloseRenameModal()
    {
        if (renameModal != null)
        {
            renameModal.SetActive(false);
        }
    }

    private void OnConfirmRename()
    {
        if (currentSelectedResident == null)
        {
            CloseRenameModal();
            return;
        }

        string newName = renameInputField != null ? renameInputField.text.Trim() : "";
        if (!string.IsNullOrEmpty(newName))
        {
            SaveCreatureName(currentSelectedResident, newName);
            if (creatureNameText != null)
            {
                creatureNameText.text = newName;
            }
            RefreshPanel1();
            ShowFeedback($"Renamed to: {newName}", LumiWorldUITheme.Success);
        }

        CloseRenameModal();
    }

    private string GetCreatureName(HabitatResident resident, string fallback)
    {
        if (resident == null) return fallback ?? "Creature";
        if (!string.IsNullOrEmpty(resident.customName))
        {
            return resident.customName;
        }

        string saved = CreatureCareManager.Instance != null ? CreatureCareManager.Instance.GetCustomName(resident.individualId) : null;
        if (string.IsNullOrEmpty(saved))
        {
            string legacyKey = $"CreatureCustomName_{resident.individualId}";
            saved = PlayerPrefs.GetString(legacyKey, "");
            if (!string.IsNullOrEmpty(saved) && CreatureCareManager.Instance != null && !WorldLaunch.IsVisit)
            {
                CreatureCareManager.Instance.SetCustomName(resident.individualId, saved);
                PlayerPrefs.DeleteKey(legacyKey);
                PlayerPrefs.Save();
            }
        }
        if (!string.IsNullOrEmpty(saved))
        {
            resident.customName = saved;
            return saved;
        }

        return !string.IsNullOrEmpty(fallback) ? fallback : resident.speciesId;
    }

    private void SaveCreatureName(HabitatResident resident, string newName)
    {
        if (resident == null || WorldLaunch.IsVisit) return;
        resident.customName = newName;
        if (CreatureCareManager.Instance != null) CreatureCareManager.Instance.SetCustomName(resident.individualId, newName);
    }

    #endregion
}
