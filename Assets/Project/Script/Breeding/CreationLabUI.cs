using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using LumiWorld.Acs;

/// <summary>
/// Creation Lab UI (Lai tạo thú)
/// Fully updated according to designer instructions:
/// - Exact centered alignment inside jars (Shelf View 0) and glass cylinder (Progress View 2)
/// - Clean anti-aliased panel graphics with zero white borders and no Gemini watermark
/// - Avatar-based selection (tiles with creature chibi avatar + name, only owned creatures)
/// - Avatar display in parent slots (Parent A & B)
/// - Removed floating white stats box; values directly aligned onto pre-printed panel lines
/// - Removed Cancel button in progress view; resized creature card to fit inside glass dome
/// - Interactive Speed Up confirmation with random progress boost (30%, 50%, 70%, 100%)
/// - Tactile button bounce / punch animation and sound on every button
/// - Consumes 2 parent cards when breeding starts
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CreationLabUI : MonoBehaviour
{
    public static CreationLabUI Instance { get; private set; }

    // ─── Theme Colors ─────────────────────────────────────────────────────────
    private static readonly Color DimmerColor     = new Color(0.04f, 0.08f, 0.14f, 0.70f);
    private static readonly Color TextDark        = new Color(0.20f, 0.15f, 0.10f, 1f);
    private static readonly Color TextMuted       = new Color(0.45f, 0.40f, 0.35f, 1f);
    private static readonly Color GreenSuccess    = new Color(0.18f, 0.72f, 0.35f, 1f);
    private static readonly Color GoldColor       = new Color(0.95f, 0.75f, 0.15f, 1f);
    private static readonly Color CardBgColor     = new Color(0.98f, 0.97f, 0.95f, 1f);

    // ─── State ───────────────────────────────────────────────────────────────
    private int activeChamberIndex = 0;
    private int currentViewMode = 0; // 0 = Shelf, 1 = Setup, 2 = InProgress
    private float timerUpdateCounter = 0f;

    private CardData selectedParentA;
    private CardData selectedParentB;
    private BreedingRecipe activeRecipe;

    // ─── Sprites ─────────────────────────────────────────────────────────────
    private Sprite spShelfPanel;
    private Sprite spBtnTapToBreed;
    private Sprite spSetupPanel;
    private Sprite spBtnConfirmBreed;
    private Sprite spBtnBack;
    private Sprite spBtnCloseX;
    private Sprite spProgressPanel;
    private Sprite spBtnSpeedUp;
    private Sprite spBraluffCard;
    private Sprite spJarForeground;
    private Sprite spIncubatorDome;
    private Sprite spLabCreationBadge;
    private Sprite spBreedingEgg;
    private Sprite spBtnInfoHelp;

    // ─── UI References ────────────────────────────────────────────────────────
    private GameObject window;
    private RectTransform panelRoot;
    private Image flashOverlay;
    private bool isHatching = false;

    // View 0 (Shelf)
    private GameObject viewShelf;
    private Image shelfBgImage;
    private Button btnCloseShelf;
    private GameObject[] jarEmptyNodes = new GameObject[3];
    private GameObject[] jarBusyNodes = new GameObject[3];
    private Image[] jarOffspringIcons = new Image[3];
    private Slider[] jarProgressSliders = new Slider[3];
    private TextMeshProUGUI[] jarTimeLabels = new TextMeshProUGUI[3];
    private Button[] jarTapButtons = new Button[3];
    private Button[] jarBusyButtons = new Button[3];
    private Button[] jarSpeedUpButtons = new Button[3];
    private TextMeshProUGUI[] jarHatchGuideTexts = new TextMeshProUGUI[3];

    // View 1 (Breeding Setup)
    private GameObject viewSetup;
    private Image setupBgImage;
    private Button btnBackSetup;
    private Button btnCloseSetup;
    private RectTransform creatureGridContent;
    private List<CreatureSelectCardItem> creatureSelectCards = new List<CreatureSelectCardItem>();
    private Image slotParentAImg;
    private TextMeshProUGUI slotParentALbl;
    private TextMeshProUGUI slotParentANameLbl;
    private Image slotParentBImg;
    private TextMeshProUGUI slotParentBLbl;
    private TextMeshProUGUI slotParentBNameLbl;
    private Button slotParentABtn;
    private Button slotParentBBtn;
    private TextMeshProUGUI valSuccessRateText;
    private TextMeshProUGUI valDurationText;
    private TextMeshProUGUI valCostText;
    private GameObject coinIconObj;
    private Button btnConfirmBreed;
    private CanvasGroup confirmBtnCanvasGroup;
    private Button btnInfoHelp;
    private GameObject infoNoticeObj;
    private GameObject infoNoticeBackdrop;
    private TextMeshProUGUI infoNoticeText;
    private Coroutine infoNoticeCoroutine;

    // View 2 (Breeding in Progress)
    private GameObject viewProgress;
    private Image progressBgImage;
    private Button btnBackProgress;
    private Button btnCloseProgress;
    private RectTransform cardFloatingRect;
    private Image progressCardImage;
    private Slider progressSlider;
    private TextMeshProUGUI progressPercentText;
    private TextMeshProUGUI progressTimeRemainingText;
    private Button btnSpeedUp;
    private TextMeshProUGUI progressHatchGuideText;

    // Speed Up Confirm Dialog
    private GameObject speedUpDialog;
    private TextMeshProUGUI speedUpDialogDesc;
    private Button btnSpeedUpConfirm;
    private Button btnSpeedUpCancel;

    // Congrats Popup
    private GameObject congratsPopup;
    private Image congratsCardImage;
    private TextMeshProUGUI congratsRarityBadge;
    private TextMeshProUGUI congratsTitle;
    private TextMeshProUGUI congratsSub;
    private Button btnClaimCongrats;
    private CardData pendingClaimOffspring;
    private Image congratsRarityOverlay;
    private Image congratsDummyRaise;
    private Image congratsDummyLower;

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
        LoadSprites();
        BuildUI();
        SubscribeEvents();
    }

    private void OnDestroy()
    {
        if (BreedingManager.Instance != null)
        {
            BreedingManager.Instance.OnChamberStateChanged -= OnChamberStateChanged;
            BreedingManager.Instance.OnBreedingComplete    -= OnBreedingComplete;
        }
    }

    private void SubscribeEvents()
    {
        if (BreedingManager.Instance == null) return;
        BreedingManager.Instance.OnChamberStateChanged -= OnChamberStateChanged;
        BreedingManager.Instance.OnChamberStateChanged += OnChamberStateChanged;
        BreedingManager.Instance.OnBreedingComplete    -= OnBreedingComplete;
        BreedingManager.Instance.OnBreedingComplete    += OnBreedingComplete;
    }

    private void LoadSprites()
    {
        spShelfPanel        = LoadSpriteSafe("UI_Breeding/chambers_shelf");
        spBtnTapToBreed     = LoadSpriteSafe("UI_Breeding/btn_tap_to_breed");
        spSetupPanel        = LoadSpriteSafe("UI_Breeding/breeding_setup_panel");
        spBtnConfirmBreed   = LoadSpriteSafe("UI_Breeding/btn_confirm_breed");
        spBtnBack           = LoadSpriteSafe("UI_Breeding/btn_back");
        spBtnCloseX         = LoadSpriteSafe("UI_Breeding/btn_close_x");
        spProgressPanel     = LoadSpriteSafe("UI_Breeding/breeding_progress_panel");
        spBtnSpeedUp        = LoadSpriteSafe("UI_Breeding/btn_speed_up");
        spJarForeground     = LoadSpriteSafe("UI_Breeding/creation_lab_jar_fg");
        spIncubatorDome     = LoadSpriteSafe("UI_Breeding/breeding_incubator_dome");
        spLabCreationBadge  = LoadSpriteSafe("UI_Breeding/title_lab_creation");
        spBreedingEgg       = LoadSpriteSafe("UI_Breeding/breeding_egg");
        spBtnInfoHelp       = LoadSpriteSafe("UI_Breeding/btn_info_help");

        CardData braluffCard = BreedingManager.GetOrCreateBraluffCard();
        spBraluffCard = braluffCard?.cardImage ?? LoadSpriteSafe("UI Cards/UI Cards/17_Braluff") ?? LoadSpriteSafe("Breeding/Braluff");
    }

    private void Update()
    {
        if (!IsOpen) return;

        timerUpdateCounter += Time.unscaledDeltaTime;
        if (timerUpdateCounter >= 0.25f)
        {
            timerUpdateCounter = 0f;
            if (currentViewMode == 0) RefreshShelfTimers();
            else if (currentViewMode == 2) RefreshProgressView();
        }

        // Gentle rocking animation for egg inside shelf jars (View 0)
        if (currentViewMode == 0 && !isHatching)
        {
            for (int i = 0; i < 3; i++)
            {
                if (jarOffspringIcons[i] != null && jarOffspringIcons[i].gameObject.activeSelf)
                {
                    float wobble = Mathf.Sin(Time.unscaledTime * 3.5f + i * 1.3f) * 4.5f;
                    jarOffspringIcons[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, wobble);
                }
            }
        }

        // Gentle floating bobbing & wobble animation for egg inside incubation dome (View 2)
        if (currentViewMode == 2 && cardFloatingRect != null && !isHatching)
        {
            float bobbing = Mathf.Sin(Time.unscaledTime * 2.2f) * 4f;
            float wobble = Mathf.Sin(Time.unscaledTime * 3.5f) * 5f;
            cardFloatingRect.anchoredPosition = new Vector2(0f, 44f + bobbing);
            cardFloatingRect.localRotation = Quaternion.Euler(0f, 0f, wobble);
        }

        // Solid bold black for "Tap egg to hatch!" instruction texts
        Color guideColor = Color.black;
        if (progressHatchGuideText != null && progressHatchGuideText.gameObject.activeSelf)
        {
            progressHatchGuideText.color = guideColor;
        }
        for (int i = 0; i < 3; i++)
        {
            if (jarHatchGuideTexts[i] != null && jarHatchGuideTexts[i].gameObject.activeSelf)
            {
                jarHatchGuideTexts[i].color = guideColor;
            }
        }
    }

    // ─── Public Control ──────────────────────────────────────────────────────
    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (window == null) BuildUI();

        transform.SetAsLastSibling();
        window.SetActive(true);
        EconomyHUD.BringWindowToFront(window);
        GameAudioManager.Instance?.PlayClick();

        ShowView(0);
        RefreshShelfView();
    }

    public void Close()
    {
        if (infoNoticeObj != null && infoNoticeObj.activeSelf) ShowInfoNotice(false);
        if (congratsPopup != null && congratsPopup.activeSelf) congratsPopup.SetActive(false);
        if (speedUpDialog != null && speedUpDialog.activeSelf) speedUpDialog.SetActive(false);

        if (window != null && window.activeSelf)
        {
            window.SetActive(false);
            EconomyHUD.MarkWindowClosed();
            GameAudioManager.Instance?.PlayClick();
        }
    }

    public void ShowView(int viewIndex)
    {
        currentViewMode = viewIndex;
        if (infoNoticeObj != null && infoNoticeObj.activeSelf) ShowInfoNotice(false);
        if (viewShelf != null)    viewShelf.SetActive(viewIndex == 0);
        if (viewSetup != null)    viewSetup.SetActive(viewIndex == 1);
        if (viewProgress != null) viewProgress.SetActive(viewIndex == 2);

        if (viewIndex == 0) RefreshShelfView();
        else if (viewIndex == 1) RefreshSetupView();
        else if (viewIndex == 2) RefreshProgressView();
    }

    // ─── UI Builder ──────────────────────────────────────────────────────────
    private void BuildUI()
    {
        if (window != null) return;

        // Root Window
        RectTransform winRect = EconomyHUD.CreateRect("CreationLab_Window", transform);
        EconomyHUD.Stretch(winRect);
        window = winRect.gameObject;
        EconomyHUD.RegisterWindow(window, Close);

        // Backdrop Dimmer
        RectTransform backdropRect = EconomyHUD.CreateRect("Backdrop", winRect);
        EconomyHUD.Stretch(backdropRect);
        Image backdrop = backdropRect.gameObject.AddComponent<Image>();
        backdrop.color = DimmerColor;
        Button backdropBtn = backdropRect.gameObject.AddComponent<Button>();
        backdropBtn.transition = Selectable.Transition.None;
        backdropBtn.onClick.AddListener(Close);

        // Main Center Panel (890 x 478 - matching exact cropped panel dimensions)
        panelRoot = EconomyHUD.CreateRect("PanelRoot", winRect);
        panelRoot.anchorMin = panelRoot.anchorMax = panelRoot.pivot = new Vector2(0.5f, 0.5f);
        panelRoot.sizeDelta = new Vector2(890f, 478f);
        panelRoot.anchoredPosition = Vector2.zero;

        // Build 3 Views
        BuildShelfView(panelRoot);
        BuildSetupView(panelRoot);
        BuildProgressView(panelRoot);
        BuildSpeedUpDialog(winRect);
        BuildCongratsPopup(winRect);

        // Burst flash overlay for egg hatching
        RectTransform flashRect = EconomyHUD.CreateRect("FlashOverlay", winRect);
        EconomyHUD.Stretch(flashRect);
        flashOverlay = flashRect.gameObject.AddComponent<Image>();
        flashOverlay.color = new Color(1f, 1f, 1f, 0f);
        flashOverlay.raycastTarget = false;
        flashOverlay.gameObject.SetActive(false);

        window.SetActive(false);
    }

    // ─────────────────────── View 0: Shelf Overview ──────────────────────────
    private void BuildShelfView(RectTransform parent)
    {
        RectTransform root = EconomyHUD.CreateRect("View_Shelf", parent);
        EconomyHUD.Stretch(root);
        viewShelf = root.gameObject;

        shelfBgImage = root.gameObject.AddComponent<Image>();
        if (spShelfPanel != null)
        {
            shelfBgImage.sprite = spShelfPanel;
            shelfBgImage.preserveAspect = true;
        }

        // Close button 'X' at top right (+405, +205)
        RectTransform closeRect = EconomyHUD.CreateRect("Btn_Close", root);
        closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(0.5f, 0.5f);
        closeRect.anchoredPosition = new Vector2(405f, 205f);
        closeRect.sizeDelta = new Vector2(38f, 38f);
        btnCloseShelf = closeRect.gameObject.AddComponent<Button>();
        Image closeImg = closeRect.gameObject.AddComponent<Image>();
        if (spBtnCloseX != null)
        {
            closeImg.sprite = spBtnCloseX;
            closeImg.preserveAspect = true;
        }
        AddButtonFeedback(btnCloseShelf);
        btnCloseShelf.onClick.AddListener(Close);

        // Title Badge "LAB CREATION" at top left (-340, +205)
        RectTransform badgeRect = EconomyHUD.CreateRect("Badge_LabCreation", root);
        badgeRect.anchorMin = badgeRect.anchorMax = badgeRect.pivot = new Vector2(0.5f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(-340f, 205f);
        badgeRect.sizeDelta = new Vector2(160f, 40f);
        Image badgeImg = badgeRect.gameObject.AddComponent<Image>();
        if (spLabCreationBadge != null)
        {
            badgeImg.sprite = spLabCreationBadge;
            badgeImg.preserveAspect = true;
        }
        badgeImg.raycastTarget = false;

        // 3 Glass Jars: Widened slightly to 258 x 258, perfectly balanced across the wooden table
        // Spaced at -266, 0, +266 with equal gap between rims
        Vector2[] jarPositions = new Vector2[] {
            new Vector2(-266f, 24f),
            new Vector2(0f, 24f),
            new Vector2(266f, 24f)
        };

        for (int i = 0; i < 3; i++)
        {
            int chamberIdx = i;
            RectTransform jarRect = EconomyHUD.CreateRect($"Jar_{i}", root);
            jarRect.anchorMin = jarRect.anchorMax = jarRect.pivot = new Vector2(0.5f, 0.5f);
            jarRect.anchoredPosition = jarPositions[i];
            jarRect.sizeDelta = new Vector2(258f, 258f);

            // ─── Layer 1 (BEHIND GLASS): Breeding Egg when incubating/busy ───
            // Sized 132 x 165 to fill the glass jar bulb, centered at Y = 6f
            RectTransform cardHolderRect = EconomyHUD.CreateRect("EggHolder_BehindGlass", jarRect);
            cardHolderRect.anchorMin = cardHolderRect.anchorMax = cardHolderRect.pivot = new Vector2(0.5f, 0.5f);
            cardHolderRect.anchoredPosition = new Vector2(0f, 6f);
            cardHolderRect.sizeDelta = new Vector2(132f, 165f);

            jarOffspringIcons[i] = cardHolderRect.gameObject.AddComponent<Image>();
            jarOffspringIcons[i].preserveAspect = true;
            if (spBreedingEgg != null) jarOffspringIcons[i].sprite = spBreedingEgg;
            jarOffspringIcons[i].gameObject.SetActive(false);

            // ─── Layer 2 (FRONT GLASS): Jar Chamber Glass + Rim + Specular Reflections ───
            // Rendered ON TOP of the creature card so the card visibly sits inside the chamber
            RectTransform glassRect = EconomyHUD.CreateRect("JarGlassForeground", jarRect);
            EconomyHUD.Stretch(glassRect);
            Image glassImg = glassRect.gameObject.AddComponent<Image>();
            if (spJarForeground != null)
            {
                glassImg.sprite = spJarForeground;
                glassImg.preserveAspect = true;
            }
            glassImg.raycastTarget = false; // Never block UI clicks

            // ─── Layer 3 (INTERACTION & CONTROLS ON TOP OF GLASS) ─────────────
            // Empty state node: Button "Tap to breed" centered inside the enlarged glass bulb
            RectTransform emptyNode = EconomyHUD.CreateRect("EmptyNode", jarRect);
            EconomyHUD.Stretch(emptyNode);
            jarEmptyNodes[i] = emptyNode.gameObject;

            RectTransform tapBtnRect = EconomyHUD.CreateRect("Btn_TapToBreed", emptyNode);
            tapBtnRect.anchorMin = tapBtnRect.anchorMax = tapBtnRect.pivot = new Vector2(0.5f, 0.5f);
            tapBtnRect.anchoredPosition = new Vector2(0f, -4f);
            tapBtnRect.sizeDelta = new Vector2(102f, 84f);

            jarTapButtons[i] = tapBtnRect.gameObject.AddComponent<Button>();

            if (spBtnTapToBreed != null)
            {
                Image tapImg = tapBtnRect.gameObject.AddComponent<Image>();
                tapImg.sprite = spBtnTapToBreed;
                tapImg.preserveAspect = true;
                jarTapButtons[i].targetGraphic = tapImg;
            }
            else
            {
                // Procedural fallback with exact 1:1 circle
                RectTransform circleRect = EconomyHUD.CreateRect("CircleNode", tapBtnRect);
                circleRect.anchorMin = circleRect.anchorMax = circleRect.pivot = new Vector2(0.5f, 0.5f);
                circleRect.anchoredPosition = new Vector2(0f, 14f);
                circleRect.sizeDelta = new Vector2(56f, 56f);
                Image circleImg = circleRect.gameObject.AddComponent<Image>();
                circleImg.sprite = LumiWorldUITheme.GetCapsuleSprite(56);
                circleImg.color = new Color(0.35f, 0.30f, 0.27f, 1f);

                TextMeshProUGUI plusText = EconomyHUD.CreateText("PlusGlyph", circleRect, 30f, new Color(0.95f, 0.91f, 0.85f, 1f), TextAlignmentOptions.Center);
                plusText.rectTransform.anchorMin = plusText.rectTransform.anchorMax = plusText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                plusText.rectTransform.anchoredPosition = new Vector2(0f, 2f);
                plusText.rectTransform.sizeDelta = new Vector2(36f, 36f);
                plusText.fontStyle = FontStyles.Bold;
                plusText.text = "+";

                TextMeshProUGUI tapLbl = EconomyHUD.CreateText("TapLabel", tapBtnRect, 13f, new Color(0.28f, 0.22f, 0.18f, 1f), TextAlignmentOptions.Center);
                tapLbl.rectTransform.anchorMin = tapLbl.rectTransform.anchorMax = tapLbl.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                tapLbl.rectTransform.anchoredPosition = new Vector2(0f, -24f);
                tapLbl.rectTransform.sizeDelta = new Vector2(110f, 24f);
                tapLbl.text = "<b>Tap to breed</b>";

                jarTapButtons[i].targetGraphic = circleImg;
            }

            AddButtonFeedback(jarTapButtons[i]);
            jarTapButtons[i].onClick.AddListener(() => OnTapChamberEmpty(chamberIdx));

            // Busy state node: Controls placed ON TOP of glass so nothing is ever obscured!
            RectTransform busyNode = EconomyHUD.CreateRect("BusyNode", jarRect);
            EconomyHUD.Stretch(busyNode);
            jarBusyNodes[i] = busyNode.gameObject;
            busyNode.gameObject.SetActive(false);

            // Clickable area to open chamber progress or claim
            RectTransform busyBtnRect = EconomyHUD.CreateRect("Btn_BusyClick", busyNode);
            busyBtnRect.anchorMin = busyBtnRect.anchorMax = busyBtnRect.pivot = new Vector2(0.5f, 0.5f);
            busyBtnRect.anchoredPosition = new Vector2(0f, -2f);
            busyBtnRect.sizeDelta = new Vector2(160f, 160f);
            jarBusyButtons[i] = busyBtnRect.gameObject.AddComponent<Button>();
            jarBusyButtons[i].transition = Selectable.Transition.None;
            Image busyHitImg = busyBtnRect.gameObject.AddComponent<Image>();
            busyHitImg.color = Color.clear; // Invisible raycast target
            AddButtonFeedback(jarBusyButtons[i]);
            jarBusyButtons[i].onClick.AddListener(() => OnTapChamberBusy(chamberIdx));

            // Mini progress bar in lower liquid area (Y = -54f)
            RectTransform pBarRect = EconomyHUD.CreateRect("MiniBar", busyNode);
            pBarRect.anchorMin = pBarRect.anchorMax = pBarRect.pivot = new Vector2(0.5f, 0.5f);
            pBarRect.anchoredPosition = new Vector2(0f, -54f);
            pBarRect.sizeDelta = new Vector2(104f, 10f);

            Image pBarBg = pBarRect.gameObject.AddComponent<Image>();
            pBarBg.color = new Color(0.2f, 0.2f, 0.2f, 0.75f);

            RectTransform fillRect = EconomyHUD.CreateRect("Fill", pBarRect);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            Image fillImg = fillRect.gameObject.AddComponent<Image>();
            fillImg.color = GreenSuccess;

            Slider sld = pBarRect.gameObject.AddComponent<Slider>();
            sld.fillRect = fillRect;
            sld.minValue = 0f;
            sld.maxValue = 1f;
            sld.value = 0.5f;
            jarProgressSliders[i] = sld;

            // Time label below bar (Y = -68f)
            jarTimeLabels[i] = EconomyHUD.CreateText("TimeLbl", busyNode, 11f, Color.white, TextAlignmentOptions.Center);
            jarTimeLabels[i].rectTransform.anchorMin = jarTimeLabels[i].rectTransform.anchorMax = jarTimeLabels[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
            jarTimeLabels[i].rectTransform.anchoredPosition = new Vector2(0f, -68f);
            jarTimeLabels[i].rectTransform.sizeDelta = new Vector2(120f, 16f);
            jarTimeLabels[i].text = "<b>02h 15m</b>";

            // Speed Up button: Placed above top rim of jar at Y = +130f (clean floating gap, does not touch jar or panel top)
            RectTransform shelfSpeedUpRect = EconomyHUD.CreateRect("Btn_ShelfSpeedUp", busyNode);
            shelfSpeedUpRect.anchorMin = shelfSpeedUpRect.anchorMax = shelfSpeedUpRect.pivot = new Vector2(0.5f, 0.5f);
            shelfSpeedUpRect.anchoredPosition = new Vector2(0f, 130f);
            shelfSpeedUpRect.sizeDelta = new Vector2(112f, 28f);

            jarSpeedUpButtons[i] = shelfSpeedUpRect.gameObject.AddComponent<Button>();
            Image shelfSpeedUpBg = shelfSpeedUpRect.gameObject.AddComponent<Image>();
            shelfSpeedUpBg.sprite = LumiWorldUITheme.GetCapsuleSprite(28);
            shelfSpeedUpBg.type = Image.Type.Sliced;
            shelfSpeedUpBg.color = new Color(0.85f, 0.45f, 0.15f, 1f);

            TextMeshProUGUI shelfSpeedUpLbl = EconomyHUD.CreateText("Lbl", shelfSpeedUpRect, 12f, Color.white, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(shelfSpeedUpLbl.rectTransform);
            shelfSpeedUpLbl.text = "<b>Speed Up</b>";

            AddButtonFeedback(jarSpeedUpButtons[i]);
            jarSpeedUpButtons[i].onClick.AddListener(() =>
            {
                activeChamberIndex = chamberIdx;
                OnSpeedUpClicked();
            });

            // Hatch Guide Instruction Text ("Tap egg to hatch!"): Placed BELOW jar pedestal at Y = -122f onto tabletop, bold black
            RectTransform guideRect = EconomyHUD.CreateRect("HatchGuideNode", busyNode);
            guideRect.anchorMin = guideRect.anchorMax = guideRect.pivot = new Vector2(0.5f, 0.5f);
            guideRect.anchoredPosition = new Vector2(0f, -122f);
            guideRect.sizeDelta = new Vector2(170f, 26f);

            jarHatchGuideTexts[i] = EconomyHUD.CreateText("HatchGuideLbl", guideRect, 13f, Color.black, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(jarHatchGuideTexts[i].rectTransform);
            jarHatchGuideTexts[i].fontStyle = FontStyles.Bold;
            jarHatchGuideTexts[i].text = "<b>Tap egg to hatch!</b>";

            Button guideBtn = guideRect.gameObject.AddComponent<Button>();
            guideBtn.transition = Selectable.Transition.None;
            guideBtn.onClick.AddListener(() =>
            {
                activeChamberIndex = chamberIdx;
                TriggerHatchSequence(chamberIdx, fromProgressView: false);
            });
            jarHatchGuideTexts[i].gameObject.SetActive(false);
        }
    }

    // ─────────────────────── View 1: Breeding Setup ──────────────────────────
    private void BuildSetupView(RectTransform parent)
    {
        RectTransform root = EconomyHUD.CreateRect("View_Setup", parent);
        EconomyHUD.Stretch(root);
        viewSetup = root.gameObject;
        viewSetup.SetActive(false);

        setupBgImage = root.gameObject.AddComponent<Image>();
        if (spSetupPanel != null)
        {
            setupBgImage.sprite = spSetupPanel;
            setupBgImage.preserveAspect = true;
        }

        // Back button at top left (-395, 205)
        RectTransform backRect = EconomyHUD.CreateRect("Btn_Back", root);
        backRect.anchorMin = backRect.anchorMax = backRect.pivot = new Vector2(0.5f, 0.5f);
        backRect.anchoredPosition = new Vector2(-395f, 205f);
        backRect.sizeDelta = new Vector2(82f, 34f);
        btnBackSetup = backRect.gameObject.AddComponent<Button>();
        Image backImg = backRect.gameObject.AddComponent<Image>();
        if (spBtnBack != null)
        {
            backImg.sprite = spBtnBack;
            backImg.preserveAspect = true;
        }
        AddButtonFeedback(btnBackSetup);
        btnBackSetup.onClick.AddListener(() => ShowView(0));

        // Close button 'X' at top right (+405, 205)
        RectTransform closeRect = EconomyHUD.CreateRect("Btn_Close", root);
        closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(0.5f, 0.5f);
        closeRect.anchoredPosition = new Vector2(405f, 205f);
        closeRect.sizeDelta = new Vector2(38f, 38f);
        btnCloseSetup = closeRect.gameObject.AddComponent<Button>();
        Image closeImg = closeRect.gameObject.AddComponent<Image>();
        if (spBtnCloseX != null)
        {
            closeImg.sprite = spBtnCloseX;
            closeImg.preserveAspect = true;
        }
        AddButtonFeedback(btnCloseSetup);
        btnCloseSetup.onClick.AddListener(Close);

        // ─── LEFT COLUMN: "Select Creatures" (Avatar Tiles) ─────────────────
        RectTransform leftCol = EconomyHUD.CreateRect("LeftCol_SelectCreatures", root);
        leftCol.anchorMin = leftCol.anchorMax = leftCol.pivot = new Vector2(0.5f, 0.5f);
        leftCol.anchoredPosition = new Vector2(-215f, -22f);
        leftCol.sizeDelta = new Vector2(370f, 350f);

        // Scrollable view for creature avatar tiles
        GameObject scrollGo = new GameObject("ScrollRect", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
        scrollGo.transform.SetParent(leftCol, false);
        RectTransform scrollRect = scrollGo.GetComponent<RectTransform>();
        EconomyHUD.Stretch(scrollRect);
        scrollRect.offsetMin = new Vector2(10f, 10f);
        scrollRect.offsetMax = new Vector2(-10f, -40f);

        Image scrollMaskImg = scrollGo.GetComponent<Image>();
        scrollMaskImg.color = Color.white;
        Mask mask = scrollGo.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        ScrollRect sr = scrollGo.GetComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;

        RectTransform content = EconomyHUD.CreateRect("GridContent", scrollRect);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, 400f);
        sr.content = content;
        creatureGridContent = content;

        GridLayoutGroup glg = content.gameObject.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(94f, 96f);
        glg.spacing = new Vector2(10f, 10f);
        glg.padding = new RectOffset(6, 6, 6, 6);
        glg.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ─── RIGHT COLUMN: "Breeding Setup" ──────────────────────────────────
        RectTransform rightCol = EconomyHUD.CreateRect("RightCol_BreedingSetup", root);
        rightCol.anchorMin = rightCol.anchorMax = rightCol.pivot = new Vector2(0.5f, 0.5f);
        rightCol.anchoredPosition = new Vector2(215f, -22f);
        rightCol.sizeDelta = new Vector2(370f, 350f);

        // Info / Help button '?' placed at top right corner of Breeding Setup card (X = 148f, Y = 160f to align with title)
        RectTransform infoBtnRect = EconomyHUD.CreateRect("Btn_InfoHelp", rightCol);
        infoBtnRect.anchorMin = infoBtnRect.anchorMax = infoBtnRect.pivot = new Vector2(0.5f, 0.5f);
        infoBtnRect.anchoredPosition = new Vector2(148f, 160f);
        infoBtnRect.sizeDelta = new Vector2(28f, 28f);

        btnInfoHelp = infoBtnRect.gameObject.AddComponent<Button>();
        Image infoImg = infoBtnRect.gameObject.AddComponent<Image>();
        if (spBtnInfoHelp != null)
        {
            infoImg.sprite = spBtnInfoHelp;
            infoImg.preserveAspect = true;
        }
        else
        {
            infoImg.sprite = LumiWorldUITheme.GetCapsuleSprite(28);
            infoImg.color = new Color(0.92f, 0.90f, 0.86f, 1f);
            TextMeshProUGUI qLbl = EconomyHUD.CreateText("Q", infoBtnRect, 18f, new Color(0.28f, 0.24f, 0.22f, 1f), TextAlignmentOptions.Center);
            EconomyHUD.Stretch(qLbl.rectTransform);
            qLbl.text = "<b>?</b>";
        }
        AddButtonFeedback(btnInfoHelp);
        btnInfoHelp.onClick.AddListener(ToggleInfoNotice);

        // Parent A & B Avatar Slots (Y = +92, DNA icon is on background panel already)
        RectTransform parentSlotsRow = EconomyHUD.CreateRect("ParentSlotsRow", rightCol);
        parentSlotsRow.anchorMin = parentSlotsRow.anchorMax = parentSlotsRow.pivot = new Vector2(0.5f, 0.5f);
        parentSlotsRow.anchoredPosition = new Vector2(0f, 92f);
        parentSlotsRow.sizeDelta = new Vector2(330f, 96f);

        // Parent A slot tile (Shifted left to X = -102 to avoid covering center DNA icon)
        RectTransform slotARect = EconomyHUD.CreateRect("SlotA", parentSlotsRow);
        slotARect.anchorMin = slotARect.anchorMax = slotARect.pivot = new Vector2(0.5f, 0.5f);
        slotARect.anchoredPosition = new Vector2(-102f, 0f);
        slotARect.sizeDelta = new Vector2(84f, 90f);

        Image slotABg = slotARect.gameObject.AddComponent<Image>();
        slotABg.sprite = EconomyHUD.RoundedSprite;
        slotABg.type = Image.Type.Sliced;
        slotABg.color = Color.white;
        slotParentABtn = slotARect.gameObject.AddComponent<Button>();
        AddButtonFeedback(slotParentABtn);
        slotParentABtn.onClick.AddListener(() => UnselectParent(true));

        RectTransform slotAImgRect = EconomyHUD.CreateRect("Avatar", slotARect);
        slotAImgRect.anchorMin = slotAImgRect.anchorMax = slotAImgRect.pivot = new Vector2(0.5f, 1f);
        slotAImgRect.anchoredPosition = new Vector2(0f, -6f);
        slotAImgRect.sizeDelta = new Vector2(56f, 56f);
        slotParentAImg = slotAImgRect.gameObject.AddComponent<Image>();
        slotParentAImg.preserveAspect = true;
        slotParentAImg.color = Color.clear;

        slotParentANameLbl = EconomyHUD.CreateText("Name", slotARect, 11.5f, TextDark, TextAlignmentOptions.Center);
        slotParentANameLbl.rectTransform.anchorMin = new Vector2(0f, 0f);
        slotParentANameLbl.rectTransform.anchorMax = new Vector2(1f, 0f);
        slotParentANameLbl.rectTransform.pivot = new Vector2(0.5f, 0f);
        slotParentANameLbl.rectTransform.anchoredPosition = new Vector2(0f, 6f);
        slotParentANameLbl.rectTransform.sizeDelta = new Vector2(0f, 20f);
        slotParentANameLbl.text = "";

        slotParentALbl = EconomyHUD.CreateText("+", slotARect, 24f, TextMuted, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(slotParentALbl.rectTransform);

        // Parent B slot tile (Shifted right to X = +102 to avoid covering center DNA icon)
        RectTransform slotBRect = EconomyHUD.CreateRect("SlotB", parentSlotsRow);
        slotBRect.anchorMin = slotBRect.anchorMax = slotBRect.pivot = new Vector2(0.5f, 0.5f);
        slotBRect.anchoredPosition = new Vector2(102f, 0f);
        slotBRect.sizeDelta = new Vector2(84f, 90f);

        Image slotBBg = slotBRect.gameObject.AddComponent<Image>();
        slotBBg.sprite = EconomyHUD.RoundedSprite;
        slotBBg.type = Image.Type.Sliced;
        slotBBg.color = Color.white;
        slotParentBBtn = slotBRect.gameObject.AddComponent<Button>();
        AddButtonFeedback(slotParentBBtn);
        slotParentBBtn.onClick.AddListener(() => UnselectParent(false));

        RectTransform slotBImgRect = EconomyHUD.CreateRect("Avatar", slotBRect);
        slotBImgRect.anchorMin = slotBImgRect.anchorMax = slotBImgRect.pivot = new Vector2(0.5f, 1f);
        slotBImgRect.anchoredPosition = new Vector2(0f, -6f);
        slotBImgRect.sizeDelta = new Vector2(56f, 56f);
        slotParentBImg = slotBImgRect.gameObject.AddComponent<Image>();
        slotParentBImg.preserveAspect = true;
        slotParentBImg.color = Color.clear;

        slotParentBNameLbl = EconomyHUD.CreateText("Name", slotBRect, 11.5f, TextDark, TextAlignmentOptions.Center);
        slotParentBNameLbl.rectTransform.anchorMin = new Vector2(0f, 0f);
        slotParentBNameLbl.rectTransform.anchorMax = new Vector2(1f, 0f);
        slotParentBNameLbl.rectTransform.pivot = new Vector2(0.5f, 0f);
        slotParentBNameLbl.rectTransform.anchoredPosition = new Vector2(0f, 6f);
        slotParentBNameLbl.rectTransform.sizeDelta = new Vector2(0f, 20f);
        slotParentBNameLbl.text = "";

        slotParentBLbl = EconomyHUD.CreateText("+", slotBRect, 24f, TextMuted, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(slotParentBLbl.rectTransform);

        // Dynamic Stat Values aligned directly onto pre-printed panel lines (NO extra white box!)
        // All three values left-align at X = 110f so the digit '8', digit '0', and coin icon form a clean vertical column:
        // 1. Success Rate value: Shifted left to X = 60f so it sits comfortably inside the panel without touching the right border
        valSuccessRateText = EconomyHUD.CreateText("Val_Success", rightCol, 13f, GreenSuccess, TextAlignmentOptions.MidlineLeft);
        valSuccessRateText.rectTransform.anchorMin = valSuccessRateText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        valSuccessRateText.rectTransform.pivot = new Vector2(0f, 0.5f);
        valSuccessRateText.rectTransform.anchoredPosition = new Vector2(60f, -51f);
        valSuccessRateText.rectTransform.sizeDelta = new Vector2(115f, 24f);
        valSuccessRateText.enableAutoSizing = true;
        valSuccessRateText.fontSizeMin = 9f;
        valSuccessRateText.fontSizeMax = 13f;
        valSuccessRateText.textWrappingMode = TextWrappingModes.NoWrap;
        valSuccessRateText.text = "";

        // 2. Duration value (Aligned onto Line 2 at Y = -86f, starts at X = 110f)
        valDurationText = EconomyHUD.CreateText("Val_Duration", rightCol, 14f, TextDark, TextAlignmentOptions.MidlineLeft);
        valDurationText.rectTransform.anchorMin = valDurationText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        valDurationText.rectTransform.pivot = new Vector2(0f, 0.5f);
        valDurationText.rectTransform.anchoredPosition = new Vector2(110f, -86f);
        valDurationText.rectTransform.sizeDelta = new Vector2(80f, 24f);
        valDurationText.text = "";

        // 3. Cost value with Official Coin Sprite (Shifted UP to Y = -115f, coin starts at X = 110f, 50 starts at X = 133f)
        coinIconObj = new GameObject("CoinIcon", typeof(RectTransform), typeof(Image));
        coinIconObj.transform.SetParent(rightCol, false);
        RectTransform coinIconRt = coinIconObj.GetComponent<RectTransform>();
        coinIconRt.anchorMin = coinIconRt.anchorMax = new Vector2(0.5f, 0.5f);
        coinIconRt.pivot = new Vector2(0f, 0.5f);
        coinIconRt.anchoredPosition = new Vector2(110f, -115f);
        coinIconRt.sizeDelta = new Vector2(20f, 20f);
        Image coinIconImg = coinIconObj.GetComponent<Image>();
        coinIconImg.sprite = EconomyHUD.CoinSprite;
        coinIconImg.preserveAspect = true;
        coinIconObj.SetActive(false);

        valCostText = EconomyHUD.CreateText("Val_Cost", rightCol, 15f, GoldColor, TextAlignmentOptions.MidlineLeft);
        valCostText.rectTransform.anchorMin = valCostText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        valCostText.rectTransform.pivot = new Vector2(0f, 0.5f);
        valCostText.rectTransform.anchoredPosition = new Vector2(133f, -115f);
        valCostText.rectTransform.sizeDelta = new Vector2(40f, 24f);
        valCostText.text = "";

        // Button "Confirm Breed" placed right below Cost line inside the panel
        RectTransform confirmRect = EconomyHUD.CreateRect("Btn_ConfirmBreed", rightCol);
        confirmRect.anchorMin = confirmRect.anchorMax = confirmRect.pivot = new Vector2(0.5f, 0.5f);
        confirmRect.anchoredPosition = new Vector2(0f, -165f);
        confirmRect.sizeDelta = new Vector2(230f, 40f);

        confirmBtnCanvasGroup = confirmRect.gameObject.AddComponent<CanvasGroup>();
        btnConfirmBreed = confirmRect.gameObject.AddComponent<Button>();
        Image confirmImg = confirmRect.gameObject.AddComponent<Image>();
        if (spBtnConfirmBreed != null)
        {
            confirmImg.sprite = spBtnConfirmBreed;
            confirmImg.preserveAspect = true;
        }
        AddButtonFeedback(btnConfirmBreed);
        btnConfirmBreed.onClick.AddListener(OnConfirmBreedClicked);

        // Fullscreen click-catcher to dismiss notice when tapping anywhere
        GameObject noticeBackdrop = new GameObject("InfoNoticeBackdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        noticeBackdrop.transform.SetParent(root, false);
        RectTransform nbRect = noticeBackdrop.GetComponent<RectTransform>();
        EconomyHUD.Stretch(nbRect);
        Image nbImg = noticeBackdrop.GetComponent<Image>();
        nbImg.color = Color.clear;
        Button nbBtn = noticeBackdrop.GetComponent<Button>();
        nbBtn.transition = Selectable.Transition.None;
        nbBtn.onClick.AddListener(() => ShowInfoNotice(false));
        infoNoticeBackdrop = noticeBackdrop;
        infoNoticeBackdrop.SetActive(false);

        // 1-line Notice Bubble: Sleek floating toast anchored at top-right of setup view
        infoNoticeObj = new GameObject("InfoNoticeBubble", typeof(RectTransform), typeof(Image), typeof(Button));
        infoNoticeObj.transform.SetParent(root, false);
        RectTransform noticeRect = infoNoticeObj.GetComponent<RectTransform>();
        noticeRect.anchorMin = noticeRect.anchorMax = noticeRect.pivot = new Vector2(0.5f, 0.5f);
        noticeRect.anchoredPosition = new Vector2(215f, 96f);
        noticeRect.sizeDelta = new Vector2(340f, 40f);

        Image noticeBg = infoNoticeObj.GetComponent<Image>();
        noticeBg.sprite = LumiWorldUITheme.GetCapsuleSprite(20);
        noticeBg.type = Image.Type.Sliced;
        noticeBg.color = new Color(0.16f, 0.14f, 0.12f, 0.96f);

        Button noticeBtn = infoNoticeObj.GetComponent<Button>();
        noticeBtn.transition = Selectable.Transition.None;
        noticeBtn.onClick.AddListener(() => ShowInfoNotice(false));

        infoNoticeText = EconomyHUD.CreateText("NoticeText", noticeRect, 11.5f, Color.white, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(infoNoticeText.rectTransform);
        infoNoticeText.margin = new Vector4(12f, 2f, 12f, 2f);
        infoNoticeText.text = "<color=#A3E635><b>98%</b> Common</color>  •  <color=#70C7FF><b>1.5%</b> Epic</color>  •  <color=#FFD700><b>0.5%</b> Legendary</color>";

        infoNoticeObj.SetActive(false);
    }

    // ─────────────────────── View 2: Breeding in Progress ────────────────────
    private void BuildProgressView(RectTransform parent)
    {
        RectTransform root = EconomyHUD.CreateRect("View_Progress", parent);
        EconomyHUD.Stretch(root);
        viewProgress = root.gameObject;
        viewProgress.SetActive(false);

        progressBgImage = root.gameObject.AddComponent<Image>();
        if (spProgressPanel != null)
        {
            progressBgImage.sprite = spProgressPanel;
            progressBgImage.preserveAspect = true;
        }

        // Back button at top left (-395, 205)
        RectTransform backRect = EconomyHUD.CreateRect("Btn_Back", root);
        backRect.anchorMin = backRect.anchorMax = backRect.pivot = new Vector2(0.5f, 0.5f);
        backRect.anchoredPosition = new Vector2(-395f, 205f);
        backRect.sizeDelta = new Vector2(82f, 34f);
        btnBackProgress = backRect.gameObject.AddComponent<Button>();
        Image backImg = backRect.gameObject.AddComponent<Image>();
        if (spBtnBack != null)
        {
            backImg.sprite = spBtnBack;
            backImg.preserveAspect = true;
        }
        AddButtonFeedback(btnBackProgress);
        btnBackProgress.onClick.AddListener(() => ShowView(0));

        // Close button 'X' at top right (+405, 205)
        RectTransform closeRect = EconomyHUD.CreateRect("Btn_Close", root);
        closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(0.5f, 0.5f);
        closeRect.anchoredPosition = new Vector2(405f, 205f);
        closeRect.sizeDelta = new Vector2(38f, 38f);
        btnCloseProgress = closeRect.gameObject.AddComponent<Button>();
        Image closeImg = closeRect.gameObject.AddComponent<Image>();
        if (spBtnCloseX != null)
        {
            closeImg.sprite = spBtnCloseX;
            closeImg.preserveAspect = true;
        }
        AddButtonFeedback(btnCloseProgress);
        btnCloseProgress.onClick.AddListener(Close);

        // ─── Layer 1 (BEHIND GLASS): Breeding Egg Floating Inside Dome (Sized 3/4 of dome: 178 x 218) ───
        RectTransform cardRect = EconomyHUD.CreateRect("BreedingEgg_Floating", root);
        cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(0f, 44f);
        cardRect.sizeDelta = new Vector2(178f, 218f);
        cardFloatingRect = cardRect;

        progressCardImage = cardRect.gameObject.AddComponent<Image>();
        if (spBreedingEgg != null)
        {
            progressCardImage.sprite = spBreedingEgg;
            progressCardImage.preserveAspect = true;
        }

        // Egg click button so players can tap the egg directly when ready to hatch
        Button eggTapBtn = cardRect.gameObject.AddComponent<Button>();
        eggTapBtn.transition = Selectable.Transition.None;
        eggTapBtn.onClick.AddListener(OnTapProgressEgg);

        // ─── Layer 2 (FRONT GLASS): Bell Dome + Sparkles + Liquid Pool + Reflections ───
        // Shifted DOWN to Y = 36f so it never encroaches or touches the top border
        RectTransform domeRect = EconomyHUD.CreateRect("IncubatorDomeForeground", root);
        domeRect.anchorMin = domeRect.anchorMax = domeRect.pivot = new Vector2(0.5f, 0.5f);
        domeRect.anchoredPosition = new Vector2(0f, 36f);
        domeRect.sizeDelta = new Vector2(245f, 255f);
        Image domeImg = domeRect.gameObject.AddComponent<Image>();
        if (spIncubatorDome != null)
        {
            domeImg.sprite = spIncubatorDome;
            domeImg.preserveAspect = true;
        }
        domeImg.raycastTarget = false; // Never block UI clicks

        // Progress Bar Below Cylinder (Y = -92)
        RectTransform pBarRect = EconomyHUD.CreateRect("ProgressBar", root);
        pBarRect.anchorMin = pBarRect.anchorMax = pBarRect.pivot = new Vector2(0.5f, 0.5f);
        pBarRect.anchoredPosition = new Vector2(0f, -92f);
        pBarRect.sizeDelta = new Vector2(360f, 20f);

        Image pBarBg = pBarRect.gameObject.AddComponent<Image>();
        pBarBg.color = new Color(0.25f, 0.22f, 0.18f, 0.85f);

        RectTransform fillRect = EconomyHUD.CreateRect("Fill", pBarRect);
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        Image fillImg = fillRect.gameObject.AddComponent<Image>();
        fillImg.color = GreenSuccess;

        progressSlider = pBarRect.gameObject.AddComponent<Slider>();
        progressSlider.fillRect = fillRect;
        progressSlider.minValue = 0f;
        progressSlider.maxValue = 1f;
        progressSlider.value = 0.45f;

        // Progress percentage text centered on bar
        progressPercentText = EconomyHUD.CreateText("PercentText", pBarRect, 12f, Color.white, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(progressPercentText.rectTransform);
        progressPercentText.text = "45%";
        progressPercentText.fontStyle = FontStyles.Bold;

        // Remaining Time Text (Y = -118)
        progressTimeRemainingText = EconomyHUD.CreateText("RemainingTime", root, 14f, TextDark, TextAlignmentOptions.Center);
        progressTimeRemainingText.rectTransform.anchorMin = progressTimeRemainingText.rectTransform.anchorMax = progressTimeRemainingText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        progressTimeRemainingText.rectTransform.anchoredPosition = new Vector2(0f, -118f);
        progressTimeRemainingText.rectTransform.sizeDelta = new Vector2(360f, 24f);
        progressTimeRemainingText.text = "Remaining Time: 01h 14m 20s";

        // Button "Speed Up" (Centered at X = 0, Y = -165 - Cancel button is removed)
        RectTransform speedUpRect = EconomyHUD.CreateRect("Btn_SpeedUp", root);
        speedUpRect.anchorMin = speedUpRect.anchorMax = speedUpRect.pivot = new Vector2(0.5f, 0.5f);
        speedUpRect.anchoredPosition = new Vector2(0f, -165f);
        speedUpRect.sizeDelta = new Vector2(150f, 44f);
        btnSpeedUp = speedUpRect.gameObject.AddComponent<Button>();
        Image speedUpImg = speedUpRect.gameObject.AddComponent<Image>();
        if (spBtnSpeedUp != null)
        {
            speedUpImg.sprite = spBtnSpeedUp;
            speedUpImg.preserveAspect = true;
        }
        AddButtonFeedback(btnSpeedUp);
        btnSpeedUp.onClick.AddListener(OnSpeedUpClicked);

        // Hatch Guide Instruction Text ("Tap egg to hatch!"): Placed at Y = -165f when ready, bold black
        RectTransform progressGuideRect = EconomyHUD.CreateRect("HatchGuideNode", root);
        progressGuideRect.anchorMin = progressGuideRect.anchorMax = progressGuideRect.pivot = new Vector2(0.5f, 0.5f);
        progressGuideRect.anchoredPosition = new Vector2(0f, -165f);
        progressGuideRect.sizeDelta = new Vector2(240f, 44f);

        progressHatchGuideText = EconomyHUD.CreateText("HatchGuideLbl", progressGuideRect, 16f, Color.black, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(progressHatchGuideText.rectTransform);
        progressHatchGuideText.fontStyle = FontStyles.Bold;
        progressHatchGuideText.text = "<b>Tap egg to hatch!</b>";

        Button progressGuideBtn = progressGuideRect.gameObject.AddComponent<Button>();
        progressGuideBtn.transition = Selectable.Transition.None;
        progressGuideBtn.onClick.AddListener(OnTapProgressEgg);
        progressHatchGuideText.gameObject.SetActive(false);
    }

    // ─────────────────────── Speed Up Confirmation Dialog ─────────────────────
    private void BuildSpeedUpDialog(RectTransform parent)
    {
        RectTransform root = EconomyHUD.CreateRect("SpeedUpDialog", parent);
        EconomyHUD.Stretch(root);
        speedUpDialog = root.gameObject;

        Image bgDim = root.gameObject.AddComponent<Image>();
        bgDim.color = new Color(0.02f, 0.05f, 0.10f, 0.75f);

        RectTransform box = EconomyHUD.CreateRect("Box", root);
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(490f, 210f);
        box.anchoredPosition = Vector2.zero;

        Image boxBg = box.gameObject.AddComponent<Image>();
        boxBg.sprite = EconomyHUD.RoundedSprite;
        boxBg.type = Image.Type.Sliced;
        boxBg.color = new Color(0.97f, 0.96f, 0.94f, 1f);

        TextMeshProUGUI title = EconomyHUD.CreateText("Title", box, 20f, TextDark, TextAlignmentOptions.Center);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -24f);
        title.rectTransform.sizeDelta = new Vector2(420f, 28f);
        title.text = "<b>SPEED UP INCUBATION</b>";

        speedUpDialogDesc = EconomyHUD.CreateText("Desc", box, 15f, TextDark, TextAlignmentOptions.Center);
        speedUpDialogDesc.rectTransform.anchorMin = new Vector2(0f, 1f);
        speedUpDialogDesc.rectTransform.anchorMax = new Vector2(1f, 1f);
        speedUpDialogDesc.rectTransform.pivot = new Vector2(0.5f, 1f);
        speedUpDialogDesc.rectTransform.anchoredPosition = new Vector2(0f, -68f);
        speedUpDialogDesc.rectTransform.sizeDelta = new Vector2(340f, 48f);
        speedUpDialogDesc.textWrappingMode = TextWrappingModes.Normal;
        speedUpDialogDesc.text = "Do you want to spend <b><color=#d4a017>300 Coins</color></b>\nto speed up incubation?";

        // Confirm button (Pill capsule shape with rounded ends and anti-aliasing)
        RectTransform confBtnRect = EconomyHUD.CreateRect("Btn_Confirm", box);
        confBtnRect.anchorMin = confBtnRect.anchorMax = confBtnRect.pivot = new Vector2(0.5f, 0.5f);
        confBtnRect.anchoredPosition = new Vector2(-80f, -52f);
        confBtnRect.sizeDelta = new Vector2(136f, 40f);
        btnSpeedUpConfirm = confBtnRect.gameObject.AddComponent<Button>();
        Image confImg = confBtnRect.gameObject.AddComponent<Image>();
        confImg.sprite = LumiWorldUITheme.GetCapsuleSprite(40);
        confImg.type = Image.Type.Sliced;
        confImg.color = new Color(0.20f, 0.68f, 0.35f, 1f);
        TextMeshProUGUI confLbl = EconomyHUD.CreateText("ConfLbl", confBtnRect, 15f, Color.white, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(confLbl.rectTransform);
        confLbl.text = "<b>Confirm</b>";
        AddButtonFeedback(btnSpeedUpConfirm);
        btnSpeedUpConfirm.onClick.AddListener(OnConfirmSpeedUp);

        // Cancel button (Pill capsule shape with rounded ends and anti-aliasing)
        RectTransform canBtnRect = EconomyHUD.CreateRect("Btn_Cancel", box);
        canBtnRect.anchorMin = canBtnRect.anchorMax = canBtnRect.pivot = new Vector2(0.5f, 0.5f);
        canBtnRect.anchoredPosition = new Vector2(80f, -52f);
        canBtnRect.sizeDelta = new Vector2(136f, 40f);
        btnSpeedUpCancel = canBtnRect.gameObject.AddComponent<Button>();
        Image canImg = canBtnRect.gameObject.AddComponent<Image>();
        canImg.sprite = LumiWorldUITheme.GetCapsuleSprite(40);
        canImg.type = Image.Type.Sliced;
        canImg.color = new Color(0.50f, 0.48f, 0.45f, 1f);
        TextMeshProUGUI canLbl = EconomyHUD.CreateText("CanLbl", canBtnRect, 15f, Color.white, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(canLbl.rectTransform);
        canLbl.text = "<b>Cancel</b>";
        AddButtonFeedback(btnSpeedUpCancel);
        btnSpeedUpCancel.onClick.AddListener(() => speedUpDialog.SetActive(false));

        speedUpDialog.SetActive(false);
    }

    // ─────────────────────── Congrats Popup Modal ────────────────────────────
    private void BuildCongratsPopup(RectTransform parent)
    {
        RectTransform root = EconomyHUD.CreateRect("CongratsPopup", parent);
        EconomyHUD.Stretch(root);
        congratsPopup = root.gameObject;

        Image bgDim = root.gameObject.AddComponent<Image>();
        bgDim.color = new Color(0.02f, 0.05f, 0.10f, 0.85f);

        RectTransform box = EconomyHUD.CreateRect("Box", root);
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(460f, 490f);
        box.anchoredPosition = Vector2.zero;

        Image boxBg = box.gameObject.AddComponent<Image>();
        boxBg.sprite = EconomyHUD.RoundedSprite;
        boxBg.type = Image.Type.Sliced;
        boxBg.color = new Color(0.97f, 0.96f, 0.94f, 1f);

        // Header Title
        congratsTitle = EconomyHUD.CreateText("Title", box, 22f, GoldColor, TextAlignmentOptions.Center);
        congratsTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
        congratsTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
        congratsTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
        congratsTitle.rectTransform.anchoredPosition = new Vector2(0f, -18f);
        congratsTitle.rectTransform.sizeDelta = new Vector2(420f, 30f);
        congratsTitle.text = "<b>CONGRATULATIONS!</b>";

        // Rarity Badge (e.g. LEGENDARY CREATURE • EPIC CREATURE • COMMON CREATURE)
        congratsRarityBadge = EconomyHUD.CreateText("RarityBadge", box, 12f, GoldColor, TextAlignmentOptions.Center);
        congratsRarityBadge.rectTransform.anchorMin = new Vector2(0f, 1f);
        congratsRarityBadge.rectTransform.anchorMax = new Vector2(1f, 1f);
        congratsRarityBadge.rectTransform.pivot = new Vector2(0.5f, 1f);
        congratsRarityBadge.rectTransform.anchoredPosition = new Vector2(0f, -48f);
        congratsRarityBadge.rectTransform.sizeDelta = new Vector2(420f, 20f);
        congratsRarityBadge.text = "";

        // Subtitle message
        congratsSub = EconomyHUD.CreateText("Sub", box, 14.5f, TextDark, TextAlignmentOptions.Center);
        congratsSub.rectTransform.anchorMin = new Vector2(0f, 1f);
        congratsSub.rectTransform.anchorMax = new Vector2(1f, 1f);
        congratsSub.rectTransform.pivot = new Vector2(0.5f, 1f);
        congratsSub.rectTransform.anchoredPosition = new Vector2(0f, -70f);
        congratsSub.rectTransform.sizeDelta = new Vector2(420f, 24f);
        congratsSub.text = "You have received a new creature card!";

        // Large Creature Card Display in Middle
        RectTransform cardRect = EconomyHUD.CreateRect("OffspringCard", box);
        cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(0f, 25f);
        cardRect.sizeDelta = new Vector2(160f, 240f);
        congratsCardImage = cardRect.gameObject.AddComponent<Image>();
        congratsCardImage.preserveAspect = true;
        if (spBraluffCard != null) congratsCardImage.sprite = spBraluffCard;

        // Button "Claim Card"
        RectTransform claimBtnRect = EconomyHUD.CreateRect("Btn_Claim", box);
        claimBtnRect.anchorMin = claimBtnRect.anchorMax = claimBtnRect.pivot = new Vector2(0.5f, 0.5f);
        claimBtnRect.anchoredPosition = new Vector2(0f, -185f);
        claimBtnRect.sizeDelta = new Vector2(240f, 48f);

        btnClaimCongrats = claimBtnRect.gameObject.AddComponent<Button>();
        Image claimImg = claimBtnRect.gameObject.AddComponent<Image>();
        claimImg.sprite = LumiWorldUITheme.GetCapsuleSprite(48);
        claimImg.type = Image.Type.Sliced;
        claimImg.color = new Color(0.20f, 0.68f, 0.35f, 1f);
        TextMeshProUGUI claimLbl = EconomyHUD.CreateText("ClaimLbl", claimBtnRect, 16f, Color.white, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(claimLbl.rectTransform);
        claimLbl.text = "<b>Claim Card</b>";
        AddButtonFeedback(btnClaimCongrats);
        btnClaimCongrats.onClick.AddListener(OnClaimCongratsClicked);

        congratsPopup.SetActive(false);
    }

    // ─── Shelf Refresh ───────────────────────────────────────────────────────
    private void RefreshShelfView()
    {
        var mgr = BreedingManager.Instance;
        if (mgr == null) return;

        for (int i = 0; i < 3; i++)
        {
            var ch = mgr.GetChamber(i);
            bool isBusy = ch != null && ch.IsBusy;
            bool isReady = ch != null && ch.IsReady;

            if (isBusy || isReady)
            {
                if (jarEmptyNodes[i] != null) jarEmptyNodes[i].SetActive(false);
                if (jarBusyNodes[i] != null)  jarBusyNodes[i].SetActive(true);

                if (jarOffspringIcons[i] != null)
                {
                    jarOffspringIcons[i].gameObject.SetActive(true);
                    jarOffspringIcons[i].sprite = spBreedingEgg != null ? spBreedingEgg : spBraluffCard;
                }

                if (jarProgressSliders[i] != null)
                {
                    jarProgressSliders[i].value = ch.progress;
                }

                if (jarTimeLabels[i] != null)
                {
                    jarTimeLabels[i].text = isReady ? "<color=black><b>HATCH READY!</b></color>" : ch.FormatTimeRemaining();
                }

                if (jarSpeedUpButtons[i] != null)
                {
                    jarSpeedUpButtons[i].gameObject.SetActive(isBusy && !isReady);
                }

                if (jarHatchGuideTexts[i] != null)
                {
                    jarHatchGuideTexts[i].gameObject.SetActive(isReady);
                }
            }
            else
            {
                if (jarEmptyNodes[i] != null) jarEmptyNodes[i].SetActive(true);
                if (jarBusyNodes[i] != null)  jarBusyNodes[i].SetActive(false);
                if (jarOffspringIcons[i] != null) jarOffspringIcons[i].gameObject.SetActive(false);
                if (jarSpeedUpButtons[i] != null) jarSpeedUpButtons[i].gameObject.SetActive(false);
                if (jarHatchGuideTexts[i] != null) jarHatchGuideTexts[i].gameObject.SetActive(false);
            }
        }
    }

    private void RefreshShelfTimers()
    {
        var mgr = BreedingManager.Instance;
        if (mgr == null) return;

        for (int i = 0; i < 3; i++)
        {
            var ch = mgr.GetChamber(i);
            if (ch == null || ch.IsEmpty) continue;

            if (jarProgressSliders[i] != null) jarProgressSliders[i].value = ch.progress;
            if (jarTimeLabels[i] != null)
            {
                jarTimeLabels[i].text = ch.IsReady ? "<color=black><b>HATCH READY!</b></color>" : ch.FormatTimeRemaining();
            }
            if (jarSpeedUpButtons[i] != null)
            {
                jarSpeedUpButtons[i].gameObject.SetActive(ch.IsBusy && !ch.IsReady);
            }
            if (jarHatchGuideTexts[i] != null)
            {
                jarHatchGuideTexts[i].gameObject.SetActive(ch.IsReady);
            }
        }
    }

    private void OnTapChamberEmpty(int chamberIndex)
    {
        activeChamberIndex = chamberIndex;
        selectedParentA = null;
        selectedParentB = null;
        activeRecipe = null;
        ShowView(1);
    }

    private void OnTapChamberBusy(int chamberIndex)
    {
        activeChamberIndex = chamberIndex;
        var ch = BreedingManager.Instance?.GetChamber(chamberIndex);
        if (ch != null && ch.IsReady)
        {
            TriggerHatchSequence(chamberIndex, fromProgressView: false);
        }
        else
        {
            ShowView(2);
        }
    }

    // ─── Setup View Refresh & Creature Selection ─────────────────────────────
    private void RefreshSetupView()
    {
        // 1. Refresh Slot A & Slot B display with CREATURE AVATARS
        if (selectedParentA != null)
        {
            slotParentAImg.sprite = GetCreatureAvatar(selectedParentA);
            slotParentAImg.color = Color.white;
            if (slotParentANameLbl != null) slotParentANameLbl.text = $"<b>{GetCreatureDisplayName(selectedParentA)}</b>";
            slotParentALbl.gameObject.SetActive(false);
        }
        else
        {
            slotParentAImg.sprite = null;
            slotParentAImg.color = Color.clear;
            if (slotParentANameLbl != null) slotParentANameLbl.text = "";
            slotParentALbl.gameObject.SetActive(true);
        }

        if (selectedParentB != null)
        {
            slotParentBImg.sprite = GetCreatureAvatar(selectedParentB);
            slotParentBImg.color = Color.white;
            if (slotParentBNameLbl != null) slotParentBNameLbl.text = $"<b>{GetCreatureDisplayName(selectedParentB)}</b>";
            slotParentBLbl.gameObject.SetActive(false);
        }
        else
        {
            slotParentBImg.sprite = null;
            slotParentBImg.color = Color.clear;
            if (slotParentBNameLbl != null) slotParentBNameLbl.text = "";
            slotParentBLbl.gameObject.SetActive(true);
        }

        // 2. Check recipe and parameters
        activeRecipe = BreedingManager.Instance?.FindRecipe(selectedParentA, selectedParentB);
        bool hasValidPair = (selectedParentA != null && selectedParentB != null && activeRecipe != null);

        if (hasValidPair)
        {
            if (valSuccessRateText != null) valSuccessRateText.text = activeRecipe.GetSuccessRateSummary();
            if (valDurationText != null)    valDurationText.text = $"<b>{activeRecipe.durationDisplay}</b>";
            if (valCostText != null)        valCostText.text = $"<b>{activeRecipe.feeCoins}</b>";
            if (coinIconObj != null)        coinIconObj.SetActive(true);
        }
        else
        {
            // When no pair selected, do NOT display any numbers/fee info
            if (valSuccessRateText != null) valSuccessRateText.text = "";
            if (valDurationText != null)    valDurationText.text = "";
            if (valCostText != null)        valCostText.text = "";
            if (coinIconObj != null)        coinIconObj.SetActive(false);
        }

        // Confirm button enabled only if both parents selected
        bool canConfirm = hasValidPair;
        if (btnConfirmBreed != null) btnConfirmBreed.interactable = canConfirm;
        if (confirmBtnCanvasGroup != null) confirmBtnCanvasGroup.alpha = canConfirm ? 1f : 0.45f;

        // 3. Populate creature cards in Left Column (Only owned creatures!)
        PopulateCreatureSelectionList();
    }

    private void PopulateCreatureSelectionList()
    {
        // Clear old list items
        foreach (var item in creatureSelectCards)
        {
            if (item != null && item.gameObject != null) Destroy(item.gameObject);
        }
        creatureSelectCards.Clear();

        // Get all unique creature cards owned by player
        List<CardData> ownedCreatures = GetOwnedCreatureCards();

        foreach (var card in ownedCreatures)
        {
            if (card == null) continue;

            bool isSelected = (card == selectedParentA || card == selectedParentB);
            bool isDimmed = false;

            if (selectedParentA == null && selectedParentB == null)
            {
                isDimmed = !HasValidBreedingRecipe(card);
            }
            else if (selectedParentA != null && selectedParentB == null)
            {
                if (card == selectedParentA) isDimmed = false;
                else
                {
                    bool match = BreedingManager.Instance != null && BreedingManager.Instance.FindRecipe(selectedParentA, card) != null;
                    isDimmed = !match;
                }
            }
            else if (selectedParentB != null && selectedParentA == null)
            {
                if (card == selectedParentB) isDimmed = false;
                else
                {
                    bool match = BreedingManager.Instance != null && BreedingManager.Instance.FindRecipe(selectedParentB, card) != null;
                    isDimmed = !match;
                }
            }
            else
            {
                isDimmed = !isSelected;
            }

            var item = CreateCreatureAvatarTile(creatureGridContent, card, isSelected, isDimmed);
            creatureSelectCards.Add(item);
        }
    }

    public static bool IsOffspringOrHybrid(CardData card)
    {
        if (card == null) return false;
        string id = (card.cardID ?? "").ToLowerInvariant();
        string name = (card.cardName ?? "").ToLowerInvariant();
        string assetName = (card.name ?? "").ToLowerInvariant();

        if (id.Contains("braluff") || name.Contains("braluff") || assetName.Contains("braluff")) return true;
        if (id.Contains("fernbara") || id.Contains("lotriv") || id.Contains("bloomhorn") || id.Contains("raincroak") || id.Contains("featherglide")) return true;
        if (id.Contains("mossgarg") || id.Contains("prismagale") || id.Contains("seraphin") || id.Contains("breezehorn") || id.Contains("quetzalcroak")) return true;
        return false;
    }

    public static bool HasValidBreedingRecipe(CardData card)
    {
        if (card == null) return false;
        if (IsOffspringOrHybrid(card)) return false;

        if (BreedingManager.Instance != null)
        {
            BreedingManager.Instance.EnsureDefaultRecipes();
            return BreedingManager.Instance.HasRecipeWithParent(card);
        }

        return true;
    }

    private List<CardData> GetOwnedCreatureCards()
    {
        var result = new List<CardData>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cardUIs = FindObjectsByType<CardUI>(FindObjectsInactive.Include);
        if (cardUIs != null && cardUIs.Length > 0)
        {
            foreach (var cui in cardUIs)
            {
                if (cui == null || cui.CardData == null || cui.Count <= 0) continue;
                if (cui.CardData.cardType != CardType.Creature) continue;

                // STRICT FILTER: Only creatures that currently have a valid breeding recipe are shown!
                // Animals without recipes (e.g. Fox, Dragonfly, Cow) are completely hidden!
                if (!HasValidBreedingRecipe(cui.CardData)) continue;

                string key = cui.CardData.cardID ?? cui.CardData.name;
                if (!string.IsNullOrEmpty(key) && seenKeys.Add(key))
                {
                    result.Add(cui.CardData);
                }
            }
        }

        // Fallback for testing when hand deck is empty
        if (result.Count == 0)
        {
            var fallbackPatterns = new string[] {
                "pebbara", "capybara", "bramblet", "deer", "lotelle", "rivlet", "mudskipper",
                "reedle", "heathorn", "highlandcow", "petaluff", "caterpillar", "spriglide", "squirrel",
                "galume", "peacock", "dandrift", "flowerdog"
            };
            foreach (var p in fallbackPatterns)
            {
                CardData c = FindCardInResources(p);
                if (c != null && HasValidBreedingRecipe(c))
                {
                    string key = c.cardID ?? c.name;
                    if (seenKeys.Add(key)) result.Add(c);
                }
            }
        }

        return result;
    }

    private static CardData FindCardInResources(params string[] patterns)
    {
        var allCards = Resources.LoadAll<CardData>("");
        foreach (var c in allCards)
        {
            if (c == null) continue;
            string id = (c.cardID ?? "").ToLowerInvariant();
            string nm = (c.cardName ?? "").ToLowerInvariant();
            string an = (c.name ?? "").ToLowerInvariant();
            foreach (var p in patterns)
            {
                if (id.Contains(p) || nm.Contains(p) || an.Contains(p)) return c;
            }
        }
        return null;
    }

    private CreatureSelectCardItem CreateCreatureAvatarTile(RectTransform parent, CardData card, bool isSelected, bool isDimmed)
    {
        GameObject itemGo = new GameObject($"Tile_{card.cardName}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
        itemGo.transform.SetParent(parent, false);
        RectTransform rect = itemGo.GetComponent<RectTransform>();

        Image bg = itemGo.GetComponent<Image>();
        bg.sprite = EconomyHUD.RoundedSprite;
        bg.type = Image.Type.Sliced;
        bg.color = isSelected ? new Color(0.88f, 0.96f, 0.88f, 1f) : Color.white;

        CanvasGroup cg = itemGo.GetComponent<CanvasGroup>();
        cg.alpha = isDimmed ? 0.35f : 1f;

        Button btn = itemGo.GetComponent<Button>();
        btn.interactable = !isDimmed || isSelected;
        AddButtonFeedback(btn);
        btn.onClick.AddListener(() => OnClickCreatureCardItem(card));

        // Highlight border
        if (isSelected)
        {
            Outline outline = itemGo.AddComponent<Outline>();
            outline.effectColor = GreenSuccess;
            outline.effectDistance = new Vector2(3f, 3f);
        }

        // Chibi Creature Avatar (from Portraits folder)
        RectTransform avtRect = EconomyHUD.CreateRect("Avatar", rect);
        avtRect.anchorMin = avtRect.anchorMax = avtRect.pivot = new Vector2(0.5f, 1f);
        avtRect.anchoredPosition = new Vector2(0f, -6f);
        avtRect.sizeDelta = new Vector2(58f, 58f);
        Image avtImg = avtRect.gameObject.AddComponent<Image>();
        avtImg.sprite = GetCreatureAvatar(card);
        avtImg.preserveAspect = true;

        // Name text below avatar
        TextMeshProUGUI nameLbl = EconomyHUD.CreateText("Name", rect, 11.5f, TextDark, TextAlignmentOptions.Center);
        nameLbl.rectTransform.anchorMin = new Vector2(0f, 0f);
        nameLbl.rectTransform.anchorMax = new Vector2(1f, 0f);
        nameLbl.rectTransform.pivot = new Vector2(0.5f, 0f);
        nameLbl.rectTransform.anchoredPosition = new Vector2(0f, 6f);
        nameLbl.rectTransform.sizeDelta = new Vector2(0f, 22f);
        nameLbl.text = $"<b>{GetCreatureDisplayName(card)}</b>";

        var widget = itemGo.AddComponent<CreatureSelectCardItem>();
        widget.cardData = card;
        return widget;
    }

    public static string GetCreatureDisplayName(CardData card)
    {
        if (card == null) return "";
        string id = (card.cardID ?? "").ToLowerInvariant();
        string nm = (card.cardName ?? "").ToLowerInvariant();
        string an = (card.name ?? "").ToLowerInvariant();

        // 10 Hybrids
        if (id.Contains("fernbara") || nm.Contains("fernbara") || an.Contains("fernbara")) return "Fernbara";
        if (id.Contains("lotriv") || nm.Contains("lotriv") || an.Contains("lotriv")) return "Lotriv";
        if (id.Contains("bloomhorn") || nm.Contains("bloomhorn") || an.Contains("bloomhorn")) return "Bloomhorn";
        if (id.Contains("raincroak") || nm.Contains("raincroak") || an.Contains("raincroak")) return "Raincroak";
        if (id.Contains("featherglide") || nm.Contains("featherglide") || an.Contains("featherglide")) return "Featherglide";
        if (id.Contains("mossgarg") || nm.Contains("mossgarg") || an.Contains("mossgarg")) return "Mossgarg";
        if (id.Contains("seraphin") || nm.Contains("seraphin") || an.Contains("seraphin")) return "Seraphin";
        if (id.Contains("breezehorn") || nm.Contains("breezehorn") || an.Contains("breezehorn")) return "Breezehorn";
        if (id.Contains("prismagale") || nm.Contains("prismagale") || an.Contains("prismagale")) return "Prismagale";
        if (id.Contains("quetzalcroak") || nm.Contains("quetzalcroak") || an.Contains("quetzalcroak")) return "Quetzalcroak";

        // Base Species (English Canonical Game Names)
        if (id.Contains("bramblet") || nm.Contains("nai") || an.Contains("deer") || id.Contains("deer")) return "Bramblet";
        if (id.Contains("petaluff") || nm.Contains("sâu") || nm.Contains("sau") || an.Contains("caterpillar") || id.Contains("caterpillar")) return "Petaluff";
        if (id.Contains("braluff") || nm.Contains("braluff") || an.Contains("braluff")) return "Braluff";
        if (id.Contains("lotelle") || nm.Contains("thiên nga") || nm.Contains("thien nga") || an.Contains("swan") || id.Contains("swan")) return "Lotelle";
        if (id.Contains("rivlet") || nm.Contains("thòi lòi") || nm.Contains("thoi loi") || an.Contains("mudskipper") || id.Contains("mudskipper")) return "Rivlet";
        if (id.Contains("reedle") || nm.Contains("ếch") || nm.Contains("ech") || an.Contains("frog") || id.Contains("frog")) return "Reedle";
        if (id.Contains("heathorn") || nm.Contains("bò lông") || nm.Contains("bo long") || an.Contains("highland") || id.Contains("highland") || (id.Contains("cow") && !id.Contains("croak"))) return "Heathorn";
        if (id.Contains("spriglide") || nm.Contains("sóc") || nm.Contains("soc") || an.Contains("squirrel") || id.Contains("squirrel")) return "Spriglide";
        if (id.Contains("galume") || nm.Contains("công") || nm.Contains("cong") || an.Contains("peacock") || id.Contains("peacock")) return "Galume";
        if (id.Contains("dandrift") || id.Contains("flowerdog") || nm.Contains("chó hoa") || nm.Contains("cho hoa") || an.Contains("flowerdog")) return "Dandrift";
        if (id.Contains("pebbara") || id.Contains("capybara") || nm.Contains("capybara") || an.Contains("capybara")) return "Pebbara";
        if (id.Contains("craggle") || nm.Contains("nóc") || nm.Contains("noc") || an.Contains("craggle") || id.Contains("puffer")) return "Craggle";
        if (id.Contains("iriwing") || nm.Contains("chuồn") || nm.Contains("chuon") || an.Contains("dragonfly") || id.Contains("dragonfly")) return "Iriwing";
        if (id.Contains("frondle") || nm.Contains("châu chấu") || nm.Contains("chau chau") || an.Contains("frondle")) return "Frondle";
        if (id.Contains("fernkin") || nm.Contains("fernkin") || an.Contains("fernkin")) return "Fernkin";
        if (id.Contains("puddleaf") || nm.Contains("puddleaf") || an.Contains("puddleaf")) return "Puddleaf";
        if (id.Contains("heliko") || nm.Contains("heliko") || an.Contains("heliko")) return "Heliko";

        return card.cardName ?? card.name;
    }

    private void OnClickCreatureCardItem(CardData card)
    {
        if (card == selectedParentA)
        {
            selectedParentA = null;
        }
        else if (card == selectedParentB)
        {
            selectedParentB = null;
        }
        else
        {
            if (selectedParentA == null) selectedParentA = card;
            else if (selectedParentB == null) selectedParentB = card;
            else selectedParentA = card;
        }

        RefreshSetupView();
    }

    private void UnselectParent(bool isParentA)
    {
        if (isParentA) selectedParentA = null;
        else selectedParentB = null;
        RefreshSetupView();
    }

    private void ToggleInfoNotice()
    {
        if (infoNoticeObj == null)
        {
            GameDialogUI.ShowNotice("98% chance for Common creature, 1.5% for Epic, 0.5% for Legendary.", title: "Breeding Rates");
            return;
        }
        bool willShow = !infoNoticeObj.activeSelf;
        ShowInfoNotice(willShow);
    }

    private void ShowInfoNotice(bool show)
    {
        if (infoNoticeCoroutine != null)
        {
            StopCoroutine(infoNoticeCoroutine);
            infoNoticeCoroutine = null;
        }

        if (infoNoticeBackdrop != null)
        {
            infoNoticeBackdrop.SetActive(show);
        }

        if (infoNoticeObj != null)
        {
            infoNoticeObj.SetActive(show);
            if (show)
            {
                GameAudioManager.Instance?.PlayClick();
                infoNoticeCoroutine = StartCoroutine(AutoDismissInfoNotice(5f));
            }
        }
    }

    private IEnumerator AutoDismissInfoNotice(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (infoNoticeObj != null && infoNoticeObj.activeSelf)
        {
            infoNoticeObj.SetActive(false);
        }
        if (infoNoticeBackdrop != null && infoNoticeBackdrop.activeSelf)
        {
            infoNoticeBackdrop.SetActive(false);
        }
        infoNoticeCoroutine = null;
    }

    private void OnConfirmBreedClicked()
    {
        if (selectedParentA == null || selectedParentB == null) return;

        int fee = activeRecipe != null ? activeRecipe.feeCoins : 50;
        var wallet = CurrencyWallet.Instance;
        if (wallet != null && !wallet.CanAfford(fee))
        {
            GameDialogUI.ShowNotice($"Not enough coins! You need {fee} Coins to start breeding.");
            return;
        }

        if (wallet != null)
        {
            wallet.TrySpend(fee, CoinReason.PlacementFee);
        }

        CardData pA = selectedParentA;
        CardData pB = selectedParentB;

        // Reset selected parent variables immediately so they don't linger across views or chambers
        selectedParentA = null;
        selectedParentB = null;

        // Starts breeding (BreedingManager consumes the 2 parent cards from player hand)
        bool started = BreedingManager.Instance != null && BreedingManager.Instance.StartBreeding(activeChamberIndex, pA, pB);
        if (started)
        {
            ShowView(2); // Go to In-Progress view
        }
    }

    private void ConsumeCardFromHand(CardData card)
    {
        if (card == null) return;
        var desk = CardDeskController.Instance;
        var cardUIs = FindObjectsByType<CardUI>(FindObjectsInactive.Include);

        bool removed = false;
        if (cardUIs != null)
        {
            foreach (var cui in cardUIs)
            {
                if (cui == null || cui.CardData == null || cui.Count <= 0) continue;
                bool isMatch = (cui.CardData == card)
                    || (!string.IsNullOrEmpty(card.cardID) && string.Equals(cui.CardData.cardID, card.cardID, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrEmpty(card.cardName) && string.Equals(cui.CardData.cardName, card.cardName, StringComparison.OrdinalIgnoreCase))
                    || (IsDeer(card) && IsDeer(cui.CardData))
                    || (IsCaterpillar(card) && IsCaterpillar(cui.CardData));

                if (isMatch)
                {
                    if (desk != null)
                    {
                        desk.RemoveCard(cui.gameObject);
                    }
                    else
                    {
                        if (cui.Count > 1) cui.SetCount(cui.Count - 1);
                        else Destroy(cui.gameObject);
                    }
                    removed = true;
                    break;
                }
            }
        }

        if (!removed && desk != null)
        {
            if (!string.IsNullOrEmpty(card.cardID) && desk.TakeCardsWithoutSaving(card.cardID, 1)) return;
            desk.TakeCardsWithoutSaving(card.name, 1);
        }
    }

    // ─────────────────────── View 2: Progress Refresh ────────────────────────
    private void RefreshProgressView()
    {
        var ch = BreedingManager.Instance?.GetChamber(activeChamberIndex);
        if (ch == null || ch.IsEmpty)
        {
            ShowView(0);
            return;
        }

        if (progressCardImage != null)
        {
            progressCardImage.sprite = spBreedingEgg != null ? spBreedingEgg : spBraluffCard;
        }

        if (progressSlider != null)
        {
            progressSlider.value = ch.progress;
        }

        if (progressPercentText != null)
        {
            int pct = Mathf.Clamp(Mathf.RoundToInt(ch.progress * 100f), 0, 100);
            progressPercentText.text = $"{pct}%";
        }

        if (progressTimeRemainingText != null)
        {
            if (ch.IsReady)
            {
                progressTimeRemainingText.text = "<color=black><b>Incubation Complete! Tap egg to hatch!</b></color>";
            }
            else
            {
                progressTimeRemainingText.text = $"Remaining Time: <b>{ch.FormatTimeRemaining()}</b>";
            }
        }

        if (btnSpeedUp != null)
        {
            btnSpeedUp.gameObject.SetActive(ch.IsBusy && !ch.IsReady);
        }

        if (progressHatchGuideText != null)
        {
            progressHatchGuideText.gameObject.SetActive(ch.IsReady);
        }
    }

    private void OnTapProgressEgg()
    {
        var ch = BreedingManager.Instance?.GetChamber(activeChamberIndex);
        if (ch != null && ch.IsReady)
        {
            TriggerHatchSequence(activeChamberIndex, fromProgressView: true);
        }
    }

    private void OnSpeedUpClicked()
    {
        if (speedUpDialog != null)
        {
            speedUpDialog.SetActive(true);
            speedUpDialog.transform.SetAsLastSibling();
        }
    }

    private void OnConfirmSpeedUp()
    {
        if (speedUpDialog != null) speedUpDialog.SetActive(false);

        var ch = BreedingManager.Instance?.GetChamber(activeChamberIndex);
        if (ch == null || !ch.IsBusy) return;

        const int speedUpCost = 300;
        var wallet = CurrencyWallet.Instance;
        if (wallet != null && !wallet.CanAfford(speedUpCost))
        {
            GameDialogUI.ShowNotice($"Not enough coins! Need {speedUpCost} Coins to speed up.");
            return;
        }

        if (wallet != null)
        {
            wallet.TrySpend(speedUpCost, CoinReason.PlacementFee);
        }

        // Random roll boost: 30%, 50%, 70%, 100%
        float[] boosts = new float[] { 0.30f, 0.50f, 0.70f, 1.00f };
        float roll = boosts[UnityEngine.Random.Range(0, boosts.Length)];
        int boostPct = Mathf.RoundToInt(roll * 100f);

        ch.progress = Mathf.Min(1f, ch.progress + roll);
        ch.elapsedSeconds = ch.progress * ch.totalSeconds;
        if (ch.progress >= 1f)
        {
            ch.status = BreedingChamberState.Status.Done;
        }

        GameDialogUI.ShowNotice($"Speed Up! +{boostPct}% Incubation Progress!");
        if (currentViewMode == 0) RefreshShelfView();
        else RefreshProgressView();
    }

    // ─────────────────────── Completion & Congrats ───────────────────────────
    private void OnChamberStateChanged(int chamberIndex)
    {
        if (!IsOpen) return;
        if (currentViewMode == 0) RefreshShelfView();
        else if (currentViewMode == 2 && chamberIndex == activeChamberIndex) RefreshProgressView();
    }

    private void OnBreedingComplete(int chamberIndex, CardData offspring)
    {
        if (!IsOpen) return;
        if (currentViewMode == 0) RefreshShelfView();
        else if (currentViewMode == 2 && chamberIndex == activeChamberIndex) RefreshProgressView();
    }

    public void TriggerHatchSequence(int chamberIndex, bool fromProgressView)
    {
        if (isHatching) return;
        StartCoroutine(HatchEggSequence(chamberIndex, fromProgressView));
    }

    private IEnumerator HatchEggSequence(int chamberIndex, bool fromProgressView)
    {
        isHatching = true;
        GameAudioManager.Instance?.PlayClick();

        RectTransform targetEggRt = null;
        if (fromProgressView && cardFloatingRect != null)
        {
            targetEggRt = cardFloatingRect;
        }
        else if (!fromProgressView && jarOffspringIcons[chamberIndex] != null)
        {
            targetEggRt = jarOffspringIcons[chamberIndex].rectTransform;
        }

        Vector3 origScale = targetEggRt != null ? targetEggRt.localScale : Vector3.one;
        Vector2 origPos = targetEggRt != null ? targetEggRt.anchoredPosition : Vector2.zero;

        // 1. Violent shaking & swelling for ~0.85s (Egg rocks heavily and expands)
        float duration = 0.85f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = elapsed / duration;
            if (targetEggRt != null)
            {
                float shakeRot = Mathf.Sin(elapsed * 45f) * (8f + progress * 16f);
                float shakeX = Mathf.Sin(elapsed * 55f) * (1.5f + progress * 3f);
                float scaleGrow = 1f + progress * 0.32f;

                targetEggRt.localRotation = Quaternion.Euler(0f, 0f, shakeRot);
                targetEggRt.anchoredPosition = origPos + new Vector2(shakeX, 0f);
                targetEggRt.localScale = origScale * scaleGrow;
            }
            yield return null;
        }

        // Reset egg pose
        if (targetEggRt != null)
        {
            targetEggRt.localRotation = Quaternion.identity;
            targetEggRt.anchoredPosition = origPos;
            targetEggRt.localScale = origScale;
        }

        // 2. Burst Flash ("Bùm!") & Sound
        GameAudioManager.Instance?.PlayPlace();

        if (flashOverlay != null)
        {
            flashOverlay.gameObject.SetActive(true);
            float flashDur = 0.28f;
            float fElapsed = 0f;
            while (fElapsed < flashDur)
            {
                fElapsed += Time.unscaledDeltaTime;
                float a = 1f - (fElapsed / flashDur);
                flashOverlay.color = new Color(1f, 0.98f, 0.85f, a * 0.90f);
                yield return null;
            }
            flashOverlay.gameObject.SetActive(false);
        }

        // 3. Claim offspring from BreedingManager
        CardData offspring = BreedingManager.Instance?.ClaimOffspring(chamberIndex);
        if (offspring == null) offspring = BreedingManager.GetOrCreateBraluffCard();

        pendingClaimOffspring = offspring;

        // 4. Show Congratulations Modal with Silver/Gold Rarity Frame
        ShowCongratsPopup(offspring);

        isHatching = false;
    }

    private void ShowCongratsPopup(CardData offspring)
    {
        if (congratsPopup == null) return;

        string creatureName = offspring != null ? (offspring.cardName ?? offspring.name) : "Braluff";
        CardRarity rarity = offspring != null ? offspring.rarity : CardRarity.Common;

        if (congratsTitle != null)
        {
            switch (rarity)
            {
                case CardRarity.Legend:
                    congratsTitle.text = "<b><color=#FFD700>LEGENDARY DISCOVERY!</color></b>";
                    break;
                case CardRarity.Epic:
                    congratsTitle.text = "<b><color=#E0F2FE>EPIC DISCOVERY!</color></b>";
                    break;
                default:
                    congratsTitle.text = "<b><color=#2ecc71>CONGRATULATIONS!</color></b>";
                    break;
            }
        }

        if (congratsRarityBadge != null)
        {
            switch (rarity)
            {
                case CardRarity.Legend:
                    congratsRarityBadge.text = "<b><color=#FFD700>★ LEGENDARY CREATURE (GOLD FRAME) ★</color></b>";
                    break;
                case CardRarity.Epic:
                    congratsRarityBadge.text = "<b><color=#E0F2FE>◆ EPIC CREATURE (SILVER FRAME) ◆</color></b>";
                    break;
                default:
                    congratsRarityBadge.text = "<b><color=#4ade80>● GOOD HYBRID CREATURE ●</color></b>";
                    break;
            }
        }

        if (congratsSub != null)
        {
            congratsSub.text = $"You hatched a new creature: <b>{creatureName}</b>!";
        }

        // Guarantee all metadata, sprite, and animated 3D prefab are bound
        BreedingManager.EnsureCardFullyConfigured(offspring);

        if (congratsCardImage != null && offspring != null)
        {
            congratsCardImage.color = Color.white;
            congratsCardImage.sprite = offspring.cardImage ?? spBraluffCard;
            CardRarityFrameUtility.ApplyCardOverlays(
                congratsCardImage.rectTransform,
                offspring,
                ref congratsRarityOverlay,
                ref congratsDummyRaise,
                ref congratsDummyLower);
        }

        congratsPopup.SetActive(true);
        congratsPopup.transform.SetAsLastSibling();
        GameAudioManager.Instance?.PlayPlace();
    }

    private void OnClaimCongratsClicked()
    {
        if (pendingClaimOffspring != null)
        {
            BreedingManager.EnsureCardFullyConfigured(pendingClaimOffspring);
            CardDeskController.Instance?.AddRewardCard(pendingClaimOffspring);
            pendingClaimOffspring = null;
        }

        if (congratsPopup != null) congratsPopup.SetActive(false);
        ShowView(0); // Return to Overview Shelf
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    public static Sprite GetCreatureAvatar(CardData card)
    {
        if (card == null) return null;
        string canonicalName = GetCreatureDisplayName(card);

        // 1. Transparent Chibi Creature Detail Art (LumiWorld standard, transparent body cutout, NO diamond frames!)
        Sprite detailArt = LumiWorldUITheme.CreatureDetailArt(canonicalName);
        if (detailArt != null) return detailArt;

        string id = (card.cardID ?? "").ToLowerInvariant();
        string name = (card.cardName ?? "").ToLowerInvariant();
        string assetName = (card.name ?? "").ToLowerInvariant();

        // 2. Hybrid Creatures (Good & Advanced)
        if (id.Contains("fernbara") || name.Contains("fernbara") || assetName.Contains("fernbara"))
            return LoadSpriteSafe("CreatureAvatars/Avatar_Fernbara") ?? LoadSpriteSafe("UI_Breeding/Cards/fernbara_good_card");
        if (id.Contains("lotriv") || name.Contains("lotriv") || assetName.Contains("lotriv"))
            return LoadSpriteSafe("CreatureAvatars/Avatar_Lotriv") ?? LoadSpriteSafe("UI_Breeding/Cards/lotriv_good_card");
        if (id.Contains("bloomhorn") || name.Contains("bloomhorn") || assetName.Contains("bloomhorn"))
            return LoadSpriteSafe("CreatureAvatars/Avatar_Bloomhorn") ?? LoadSpriteSafe("UI_Breeding/Cards/bloomhorn_good_card");
        if (id.Contains("raincroak") || name.Contains("raincroak") || assetName.Contains("raincroak"))
            return LoadSpriteSafe("CreatureAvatars/Avatar_Raincroak") ?? LoadSpriteSafe("UI_Breeding/Cards/raincroak_good_card");
        if (id.Contains("featherglide") || name.Contains("featherglide") || assetName.Contains("featherglide"))
            return LoadSpriteSafe("CreatureAvatars/Avatar_Featherglide") ?? LoadSpriteSafe("UI_Breeding/Cards/featherglide_good_card");
        if (id.Contains("mossgarg") || name.Contains("mossgarg") || assetName.Contains("mossgarg"))
            return LoadSpriteSafe("CreatureAvatars/Avatar_Mossgarg") ?? LoadSpriteSafe("UI_Breeding/Cards/mossgarg_card");
        if (id.Contains("prismagale") || name.Contains("prismagale") || assetName.Contains("prismagale"))
            return LoadSpriteSafe("CreatureAvatars/Avatar_Prismagale") ?? LoadSpriteSafe("UI_Breeding/Cards/prismagale_card");
        if (id.Contains("seraphin") || name.Contains("seraphin") || assetName.Contains("seraphin"))
            return LoadSpriteSafe("CreatureAvatars/Avatar_Seraphin") ?? LoadSpriteSafe("UI_Breeding/Cards/seraphin_card");
        if (id.Contains("breezehorn") || name.Contains("breezehorn") || assetName.Contains("breezehorn"))
            return LoadSpriteSafe("CreatureAvatars/Avatar_Breezehorn") ?? LoadSpriteSafe("UI_Breeding/Cards/breezehorn_card");
        if (id.Contains("quetzalcroak") || name.Contains("quetzalcroak") || assetName.Contains("quetzalcroak"))
            return LoadSpriteSafe("CreatureAvatars/Avatar_Quetzalcroak") ?? LoadSpriteSafe("UI_Breeding/Cards/quetzalcroak_card");

        // 3. Direct name match in CreatureAvatars folder
        Sprite directAvatar = LoadSpriteSafe($"CreatureAvatars/Avatar_{card.name}")
                           ?? LoadSpriteSafe($"CreatureAvatars/Avatar_{canonicalName}")
                           ?? LoadSpriteSafe($"CreatureAvatars/{canonicalName}");
        if (directAvatar != null) return directAvatar;

        // 4. Starter Parent Creatures fallbacks
        if (id.Contains("bramblet") || id.Contains("deer") || name.Contains("nai") || assetName.Contains("deer"))
            return LoadSpriteSafe("CreatureAvatars/02_Bramblet") ?? LumiWorldUITheme.CreatureDetailArt("Bramblet");
        if (id.Contains("petaluff") || id.Contains("caterpillar") || name.Contains("sâu") || name.Contains("sau") || assetName.Contains("caterpillar"))
            return LoadSpriteSafe("CreatureAvatars/04_Petaluff") ?? LumiWorldUITheme.CreatureDetailArt("Petaluff");
        if (id.Contains("braluff") || name.Contains("braluff"))
            return LoadSpriteSafe("CreatureAvatars/17_Braluff") ?? LumiWorldUITheme.CreatureDetailArt("Braluff");
        if (id.Contains("pebbara") || id.Contains("capybara") || name.Contains("capybara") || assetName.Contains("pebbara"))
            return LoadSpriteSafe("CreatureAvatars/13_Pebbara") ?? LoadSpriteSafe("CreatureAvatars/06_Capybara") ?? LumiWorldUITheme.CreatureDetailArt("Pebbara");
        if (id.Contains("lotelle") || id.Contains("swan") || name.Contains("thiên nga") || assetName.Contains("lotelle"))
            return LoadSpriteSafe("CreatureAvatars/10_Lotelle") ?? LumiWorldUITheme.CreatureDetailArt("Lotelle");
        if (id.Contains("rivlet") || id.Contains("mudskipper") || name.Contains("cá thòi lòi") || assetName.Contains("rivlet"))
            return LoadSpriteSafe("CreatureAvatars/12_Rivlet") ?? LoadSpriteSafe("CreatureAvatars/07_Mudskipper") ?? LumiWorldUITheme.CreatureDetailArt("Rivlet");
        if (id.Contains("reedle") || id.Contains("frog") || name.Contains("ếch") || assetName.Contains("reedle"))
            return LoadSpriteSafe("CreatureAvatars/08_Reedle") ?? LumiWorldUITheme.CreatureDetailArt("Reedle");
        if (id.Contains("heathorn") || id.Contains("highland") || id.Contains("cow") || name.Contains("bò lông") || assetName.Contains("heathorn"))
            return LoadSpriteSafe("CreatureAvatars/07_Heathorn") ?? LoadSpriteSafe("CreatureAvatars/12_HighlandCow") ?? LumiWorldUITheme.CreatureDetailArt("Heathorn");
        if (id.Contains("spriglide") || id.Contains("squirrel") || name.Contains("sóc bay") || assetName.Contains("spriglide"))
            return LoadSpriteSafe("CreatureAvatars/01_Spriglide") ?? LumiWorldUITheme.CreatureDetailArt("Spriglide");
        if (id.Contains("galume") || id.Contains("peacock") || name.Contains("công") || assetName.Contains("galume"))
            return LoadSpriteSafe("CreatureAvatars/06_Galume") ?? LoadSpriteSafe("CreatureAvatars/14_Peacock") ?? LumiWorldUITheme.CreatureDetailArt("Galume");
        if (id.Contains("frondle") || name.Contains("frondle"))
            return LoadSpriteSafe("CreatureAvatars/03_Frondle") ?? LumiWorldUITheme.CreatureDetailArt("Frondle");
        if (id.Contains("dandrift") || name.Contains("dandrift") || id.Contains("flowerdog"))
            return LoadSpriteSafe("CreatureAvatars/05_Dandrift") ?? LumiWorldUITheme.CreatureDetailArt("Dandrift");
        if (id.Contains("craggle") || name.Contains("craggle"))
            return LoadSpriteSafe("CreatureAvatars/09_Craggle") ?? LumiWorldUITheme.CreatureDetailArt("Craggle");
        if (id.Contains("dragonfly") || id.Contains("iriwing") || name.Contains("chuồn") || name.Contains("chuon"))
            return LoadSpriteSafe("CreatureAvatars/11_Iriwing") ?? LumiWorldUITheme.CreatureDetailArt("Iriwing");

        return null;
    }

    public static bool IsDeer(CardData card)
    {
        if (card == null) return false;
        string id = (card.cardID ?? "").ToLowerInvariant();
        string name = (card.cardName ?? "").ToLowerInvariant();
        string assetNm = (card.name ?? "").ToLowerInvariant();
        return id.Contains("deer") || id.Contains("bramblet") || name.Contains("nai") || name.Contains("bramblet")
            || assetNm.Contains("deer");
    }

    public static bool IsCaterpillar(CardData card)
    {
        if (card == null) return false;
        string id = (card.cardID ?? "").ToLowerInvariant();
        string name = (card.cardName ?? "").ToLowerInvariant();
        string assetNm = (card.name ?? "").ToLowerInvariant();
        return id.Contains("caterpillar") || id.Contains("petaluff") || name.Contains("sâu") || name.Contains("sau") || name.Contains("petaluff")
            || assetNm.Contains("caterpillar");
    }

    public static void AddButtonFeedback(Button btn)
    {
        if (btn == null || btn.gameObject == null) return;
        if (btn.GetComponent<UIButtonPressFeedback>() == null)
        {
            btn.gameObject.AddComponent<UIButtonPressFeedback>();
        }
    }

    public static string GetEnglishCreatureName(CardData card)
    {
        if (card == null) return "—";
        return GetCreatureDisplayName(card);
    }

    public static string ToEnglishCreatureName(string name, string id = null)
    {
        string n = (name ?? "").Trim();
        string i = (id ?? "").ToLowerInvariant();

        if (n.Equals("Bò Lông Dài", StringComparison.OrdinalIgnoreCase) || n.Equals("Bo Long Dai", StringComparison.OrdinalIgnoreCase) || i.Contains("highlandcow") || i.Contains("heathorn"))
            return "Heathorn";

        if (n.Equals("Chim Công", StringComparison.OrdinalIgnoreCase) || n.Equals("Chim Cong", StringComparison.OrdinalIgnoreCase) || n.Equals("Công", StringComparison.OrdinalIgnoreCase) || i.Contains("peacock") || i.Contains("galume"))
            return "Galume";

        if (n.Equals("Sóc Bay", StringComparison.OrdinalIgnoreCase) || n.Equals("Soc Bay", StringComparison.OrdinalIgnoreCase) || i.Contains("flyingsquirrel") || i.Contains("squirrel") || i.Contains("spriglide"))
            return "Spriglide";

        if (n.Equals("Chó Hoa", StringComparison.OrdinalIgnoreCase) || n.Equals("Cho Hoa", StringComparison.OrdinalIgnoreCase) || i.Contains("flowerdog") || i.Contains("dandrift"))
            return "Dandrift";

        if (n.Equals("Capybara", StringComparison.OrdinalIgnoreCase) || i.Contains("capybara") || i.Contains("pebbara"))
            return "Pebbara";

        if (n.Equals("Nai Rừng", StringComparison.OrdinalIgnoreCase) || n.Equals("Nai", StringComparison.OrdinalIgnoreCase) || i.Contains("deer") || i.Contains("bramblet") || n.Equals("Bramblet", StringComparison.OrdinalIgnoreCase))
            return "Bramblet";

        if (n.Equals("Sâu Bướm", StringComparison.OrdinalIgnoreCase) || n.Equals("Sâu", StringComparison.OrdinalIgnoreCase) || i.Contains("caterpillar") || i.Contains("petaluff") || n.Equals("Petaluff", StringComparison.OrdinalIgnoreCase))
            return "Petaluff";

        if (n.Equals("Braluff", StringComparison.OrdinalIgnoreCase) || i.Contains("braluff"))
            return "Braluff";

        if (n.Equals("Chuồn Chuồn", StringComparison.OrdinalIgnoreCase) || i.Contains("dragonfly") || i.Contains("iriwing"))
            return "Iriwing";

        if (n.Equals("Ếch Hoa", StringComparison.OrdinalIgnoreCase) || n.Equals("Ech Hoa", StringComparison.OrdinalIgnoreCase) || i.Contains("blossomfrog") || i.Contains("reedle") || i.Contains("frog"))
            return "Reedle";

        if (n.Equals("Thiên Nga", StringComparison.OrdinalIgnoreCase) || n.Equals("Thien Nga", StringComparison.OrdinalIgnoreCase) || i.Contains("swan") || i.Contains("lotelle"))
            return "Lotelle";

        if (n.Equals("Cá Thòi Lòi", StringComparison.OrdinalIgnoreCase) || n.Equals("Ca Thoi Loi", StringComparison.OrdinalIgnoreCase) || i.Contains("mudskipper") || i.Contains("rivlet"))
            return "Rivlet";

        if (n.Equals("Cá Nóc", StringComparison.OrdinalIgnoreCase) || n.Equals("Ca Noc", StringComparison.OrdinalIgnoreCase) || i.Contains("canoc") || i.Contains("puffer") || i.Contains("craggle"))
            return "Craggle";

        if (n.Equals("Châu Chấu", StringComparison.OrdinalIgnoreCase) || n.Equals("Chau Chau", StringComparison.OrdinalIgnoreCase) || i.Contains("grasshopper") || i.Contains("frondle"))
            return "Frondle";

        if (!string.IsNullOrEmpty(name)) return name;
        if (!string.IsNullOrEmpty(id)) return id;
        return "Unknown";
    }

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

            string p4 = "Assets/Project/Resources/" + clean + ".png";
            s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p4);
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
}

/// <summary>
/// Provides instant tactile scale squeeze & game click sound for every button.
/// </summary>
public class UIButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    private Vector3 originalScale;
    private bool isPressed = false;

    private void Awake()
    {
        originalScale = transform.localScale;
        if (originalScale == Vector3.zero) originalScale = Vector3.one;
    }

    private void OnEnable()
    {
        transform.localScale = originalScale;
        isPressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        transform.localScale = originalScale * 0.92f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isPressed)
        {
            isPressed = false;
            transform.localScale = originalScale;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        GameAudioManager.Instance?.PlayClick();
    }
}

/// <summary>
/// Small helper component attached to each creature avatar tile in the selection grid.
/// </summary>
public class CreatureSelectCardItem : MonoBehaviour
{
    public CardData cardData;
}
