using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LumiWorld.Acs;
using LumiWorld.Economy.Crafting;

/// <summary>
/// Giao diện Cho Ăn Tự Động (Auto-Feed Dispenser UI) của LumiWorld:
/// - Mở từ nút Cho Ăn ("choan_button" / "Btn_AutoFeed") trên thanh Toolbar HUD thu gọn.
/// - Bên trái: Danh sách các loại thức ăn đang có trong Kho (Food Storage Grid).
/// - Ở giữa: Mũi tên chuyển giao thức ăn vào hệ thống phân phối.
/// - Bên phải: 
///   + Thời lượng cho ăn tự động ước tính (Estimated Auto-Feeding Duration).
///   + Thống kê mức tiêu thụ mỗi phút (Consumption / Min) & Tổng năng lượng thức ăn (Total Food Energy).
///   + 3 ô thức ăn tự động (3 Dispenser Slots) kèm nút hủy (-) từng món.
///   + Nút "Fill All" (Tự động nạp nhanh) và "Confirm Auto-Feed" (Xác nhận cho ăn tự động).
/// - Toàn bộ ngôn ngữ chuẩn 100% tiếng Anh theo yêu cầu.
/// </summary>
public class AutoFeedUI : MonoBehaviour
{
    private static AutoFeedUI instance;
    public static AutoFeedUI Instance => instance;

    public bool IsOpen => window != null && window.activeSelf;

    // Window components
    private GameObject window;
    private RectTransform panel;
    private RectTransform foodGridContent;
    private TextMeshProUGUI availableFoodCountText;
    private TextMeshProUGUI emptyFoodNoticeText;

    // Right card components
    private TextMeshProUGUI durationText;
    private TextMeshProUGUI consumptionRateText;
    private TextMeshProUGUI totalEnergyText;
    private TextMeshProUGUI feedbackNotifyText;
    private Coroutine feedbackFadeRoutine;
    private Button confirmButton;
    private Button fillAllButton;

    // 3 Food Slots
    private readonly DispenserSlotView[] slotViews = new DispenserSlotView[3];

    // State: 3 assigned food slots
    private static readonly string[] AssignedFoodIds = new string[3];
    private static readonly int[] AssignedFoodAmounts = new int[3];

    // Cached Food Items from Inventory
    private readonly List<ResourceUIItem> currentFoodList = new List<ResourceUIItem>();
    private readonly Dictionary<string, Image> foodCardBorders = new Dictionary<string, Image>(StringComparer.Ordinal);

    private class DispenserSlotView
    {
        public int index;
        public RectTransform root;
        public Image plateImage;
        public Image iconImage;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI amountText;
        public Button removeButton;
        public GameObject emptyPlaceholder;
        public GameObject occupiedContainer;
        public GameObject stepperRow;
        public Button decButton;
        public Button incButton;
        public Button amountButton;
    }

    // Quantity Modal
    private GameObject quantityModalObj;
    private Image modalFoodIcon;
    private TextMeshProUGUI modalFoodNameText;
    private TextMeshProUGUI modalStorageText;
    private TextMeshProUGUI modalAmountValueText;
    private Slider modalSlider;
    private int modalEditingSlot = -1;
    private int modalCurrentAmount = 1;
    private int modalMaxStock = 1;

    private static readonly Color ThemeCyan = new Color(0.04f, 0.70f, 0.90f, 1f);
    private static readonly Color ThemeDark = new Color(0.06f, 0.12f, 0.22f, 1f);
    private static readonly Color CardBg = new Color(0.96f, 0.985f, 1f, 0.98f);
    private static readonly Color SubTextCol = new Color(0.35f, 0.45f, 0.58f, 1f);

    private static Sprite panelSprite;
    private static Sprite cardBoxSprite;
    private static Sprite pillSprite;
    private static Sprite circlePlateSprite;
    private static Sprite buttonPrimarySprite;
    private static Sprite buttonSecondarySprite;
    private static Sprite clockIconSprite;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        LoadSavedSlots();
        BuildWindow();
    }

    private void Start()
    {
        if (window != null)
        {
            EconomyHUD.RegisterWindow(window, Close);
        }
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (window == null) BuildWindow();
        window.SetActive(true);
        window.transform.SetAsLastSibling();
        EconomyHUD.BringWindowToFront(window);
        RefreshFoodInventory();
        RefreshDispenserStats();
        GameAudioManager.Instance?.PlayClick();
    }

    public void Close()
    {
        if (quantityModalObj != null && quantityModalObj.activeSelf)
        {
            quantityModalObj.SetActive(false);
        }
        if (window != null && window.activeSelf)
        {
            window.SetActive(false);
            EconomyHUD.MarkWindowClosed();
            GameAudioManager.Instance?.PlayClick();
        }
    }

    private void LoadSavedSlots()
    {
        for (int i = 0; i < 3; i++)
        {
            AssignedFoodIds[i] = PlayerPrefs.GetString("AutoFeed_Slot_" + i, string.Empty);
            AssignedFoodAmounts[i] = PlayerPrefs.GetInt("AutoFeed_Amount_" + i, 0);
        }
    }

    private void SaveSlots()
    {
        for (int i = 0; i < 3; i++)
        {
            PlayerPrefs.SetString("AutoFeed_Slot_" + i, AssignedFoodIds[i] ?? string.Empty);
            PlayerPrefs.SetInt("AutoFeed_Amount_" + i, AssignedFoodAmounts[i]);
        }
        PlayerPrefs.Save();
    }

    private void BuildWindow()
    {
        if (window != null) return;

        // Dimmer background
        window = new GameObject("AutoFeedWindow", typeof(RectTransform));
        window.transform.SetParent(transform, false);
        var winRect = window.GetComponent<RectTransform>();
        winRect.anchorMin = Vector2.zero;
        winRect.anchorMax = Vector2.one;
        winRect.offsetMin = Vector2.zero;
        winRect.offsetMax = Vector2.zero;

        var dimObj = new GameObject("Dimmer", typeof(RectTransform), typeof(Image));
        dimObj.transform.SetParent(window.transform, false);
        var dimRect = dimObj.GetComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;
        var dimImg = dimObj.GetComponent<Image>();
        dimImg.color = new Color(0.04f, 0.08f, 0.14f, 0.65f);
        dimImg.raycastTarget = true;
        dimObj.AddComponent<ResourceUIInputBlocker>();

        // Main Panel (940x560)
        GameObject panelObj = new GameObject("MainPanel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(window.transform, false);
        panel = panelObj.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(940f, 560f);

        var pImg = panelObj.GetComponent<Image>();
        pImg.sprite = GetPanelSprite();
        pImg.type = Image.Type.Sliced;
        pImg.color = Color.white;
        pImg.raycastTarget = true;
        panelObj.AddComponent<ResourceUIInputBlocker>();

        // Header
        BuildHeader();

        // Left Section: Available Food Grid
        BuildFoodInventorySection();

        // Center Arrow
        BuildCenterArrow();

        // Right Section: Auto-Feed Dispenser & Stats
        BuildDispenserSection();

        window.SetActive(false);
    }

    private void BuildHeader()
    {
        // Title
        var titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(panel, false);
        var tRect = titleObj.GetComponent<RectTransform>();
        tRect.anchorMin = tRect.anchorMax = new Vector2(0f, 1f);
        tRect.pivot = new Vector2(0f, 1f);
        tRect.anchoredPosition = new Vector2(30f, -18f);
        tRect.sizeDelta = new Vector2(600f, 32f);

        var tText = titleObj.GetComponent<TextMeshProUGUI>();
        tText.font = Resources.Load<TMP_FontAsset>("Fonts/Nunito-Bold SDF");
        tText.text = "AUTOMATIC FEEDING";
        tText.fontSize = 22f;
        tText.fontStyle = FontStyles.Bold;
        tText.color = ThemeDark;
        tText.alignment = TextAlignmentOptions.MidlineLeft;

        // Subtitle
        var subObj = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        subObj.transform.SetParent(panel, false);
        var sRect = subObj.GetComponent<RectTransform>();
        sRect.anchorMin = sRect.anchorMax = new Vector2(0f, 1f);
        sRect.pivot = new Vector2(0f, 1f);
        sRect.anchoredPosition = new Vector2(30f, -48f);
        sRect.sizeDelta = new Vector2(700f, 22f);

        var sText = subObj.GetComponent<TextMeshProUGUI>();
        sText.font = tText.font;
        sText.text = "Assign food from warehouse to automated dispenser slots to feed creatures continuously";
        sText.fontSize = 13f;
        sText.color = SubTextCol;
        sText.alignment = TextAlignmentOptions.MidlineLeft;

        // Close Button (X)
        GameObject closeObj = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        closeObj.transform.SetParent(panel, false);
        var cRect = closeObj.GetComponent<RectTransform>();
        cRect.anchorMin = cRect.anchorMax = new Vector2(1f, 1f);
        cRect.pivot = new Vector2(1f, 1f);
        cRect.anchoredPosition = new Vector2(-22f, -18f);
        cRect.sizeDelta = new Vector2(36f, 36f);

        var cImg = closeObj.GetComponent<Image>();
        cImg.sprite = GetCardBoxSprite();
        cImg.type = Image.Type.Sliced;
        cImg.color = Color.white;

        var cTxtObj = new GameObject("X", typeof(RectTransform), typeof(TextMeshProUGUI));
        cTxtObj.transform.SetParent(closeObj.transform, false);
        var ctRect = cTxtObj.GetComponent<RectTransform>();
        ctRect.anchorMin = Vector2.zero; ctRect.anchorMax = Vector2.one;
        ctRect.offsetMin = Vector2.zero; ctRect.offsetMax = Vector2.zero;
        var ctText = cTxtObj.GetComponent<TextMeshProUGUI>();
        ctText.font = tText.font;
        ctText.text = "X";
        ctText.fontSize = 18f;
        ctText.fontStyle = FontStyles.Bold;
        ctText.color = new Color(0.35f, 0.45f, 0.58f, 1f);
        ctText.alignment = TextAlignmentOptions.Center;

        var cBtn = closeObj.GetComponent<Button>();
        cBtn.targetGraphic = cImg;
        cBtn.onClick.AddListener(Close);
    }

    private void BuildFoodInventorySection()
    {
        // Container
        var contObj = new GameObject("FoodStorageContainer", typeof(RectTransform));
        contObj.transform.SetParent(panel, false);
        var cRect = contObj.GetComponent<RectTransform>();
        cRect.anchorMin = cRect.anchorMax = new Vector2(0f, 1f);
        cRect.pivot = new Vector2(0f, 1f);
        cRect.anchoredPosition = new Vector2(26f, -76f);
        cRect.sizeDelta = new Vector2(510f, 460f);

        // Header row
        var headerObj = new GameObject("GridHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerObj.transform.SetParent(contObj.transform, false);
        var hRect = headerObj.GetComponent<RectTransform>();
        hRect.anchorMin = hRect.anchorMax = new Vector2(0f, 1f);
        hRect.pivot = new Vector2(0f, 1f);
        hRect.anchoredPosition = new Vector2(6f, 0f);
        hRect.sizeDelta = new Vector2(300f, 26f);

        availableFoodCountText = headerObj.GetComponent<TextMeshProUGUI>();
        availableFoodCountText.font = Resources.Load<TMP_FontAsset>("Fonts/Nunito-Bold SDF");
        availableFoodCountText.text = "FOOD STORAGE (0 ITEMS)";
        availableFoodCountText.fontSize = 13.5f;
        availableFoodCountText.fontStyle = FontStyles.Bold;
        availableFoodCountText.color = ThemeDark;

        // Sub hint
        var hintObj = new GameObject("GridHint", typeof(RectTransform), typeof(TextMeshProUGUI));
        hintObj.transform.SetParent(contObj.transform, false);
        var hintRect = hintObj.GetComponent<RectTransform>();
        hintRect.anchorMin = hintRect.anchorMax = new Vector2(1f, 1f);
        hintRect.pivot = new Vector2(1f, 1f);
        hintRect.anchoredPosition = new Vector2(0f, 0f);
        hintRect.sizeDelta = new Vector2(200f, 26f);
        var hintText = hintObj.GetComponent<TextMeshProUGUI>();
        hintText.font = availableFoodCountText.font;
        hintText.text = "Tap food to assign →";
        hintText.fontSize = 12f;
        hintText.color = ThemeCyan;
        hintText.alignment = TextAlignmentOptions.MidlineRight;

        // Scroll View
        var scrollObj = new GameObject("FoodScrollView", typeof(RectTransform), typeof(ScrollRect));
        scrollObj.transform.SetParent(contObj.transform, false);
        var sRect = scrollObj.GetComponent<RectTransform>();
        sRect.anchorMin = sRect.anchorMax = new Vector2(0f, 1f);
        sRect.pivot = new Vector2(0f, 1f);
        sRect.anchoredPosition = new Vector2(0f, -32f);
        sRect.sizeDelta = new Vector2(510f, 420f);

        var scroll = scrollObj.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        // Viewport
        var viewObj = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewObj.transform.SetParent(scrollObj.transform, false);
        var vRect = viewObj.GetComponent<RectTransform>();
        vRect.anchorMin = Vector2.zero; vRect.anchorMax = Vector2.one;
        vRect.offsetMin = Vector2.zero; vRect.offsetMax = Vector2.zero;
        var vImg = viewObj.GetComponent<Image>();
        vImg.color = Color.clear;
        vImg.raycastTarget = true;
        viewObj.AddComponent<ResourceUIInputBlocker>();

        // Content
        var contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(viewObj.transform, false);
        foodGridContent = contentObj.GetComponent<RectTransform>();
        foodGridContent.anchorMin = new Vector2(0f, 1f);
        foodGridContent.anchorMax = new Vector2(1f, 1f);
        foodGridContent.pivot = new Vector2(0f, 1f);
        foodGridContent.anchoredPosition = Vector2.zero;
        foodGridContent.sizeDelta = new Vector2(0f, 420f);

        scroll.viewport = vRect;
        scroll.content = foodGridContent;

        // Empty state label
        var emptyObj = new GameObject("EmptyNotice", typeof(RectTransform), typeof(TextMeshProUGUI));
        emptyObj.transform.SetParent(scrollObj.transform, false);
        var eRect = emptyObj.GetComponent<RectTransform>();
        eRect.anchorMin = Vector2.zero; eRect.anchorMax = Vector2.one;
        eRect.offsetMin = Vector2.zero; eRect.offsetMax = Vector2.zero;
        emptyFoodNoticeText = emptyObj.GetComponent<TextMeshProUGUI>();
        emptyFoodNoticeText.font = availableFoodCountText.font;
        emptyFoodNoticeText.fontSize = 14.5f;
        emptyFoodNoticeText.color = SubTextCol;
        emptyFoodNoticeText.alignment = TextAlignmentOptions.Center;
        emptyFoodNoticeText.text = "No food in inventory.\nHarvest berries or craft nutritious meals in the Workshop!";
        emptyObj.SetActive(false);
    }

    private void BuildCenterArrow()
    {
        var arrowObj = new GameObject("CenterArrow", typeof(RectTransform), typeof(TextMeshProUGUI));
        arrowObj.transform.SetParent(panel, false);
        var aRect = arrowObj.GetComponent<RectTransform>();
        aRect.anchorMin = aRect.anchorMax = new Vector2(0f, 1f);
        aRect.pivot = new Vector2(0.5f, 0.5f);
        aRect.anchoredPosition = new Vector2(548f, -280f);
        aRect.sizeDelta = new Vector2(30f, 50f);

        var aText = arrowObj.GetComponent<TextMeshProUGUI>();
        aText.font = Resources.Load<TMP_FontAsset>("Fonts/Nunito-Bold SDF");
        aText.text = ">>";
        aText.fontSize = 20f;
        aText.fontStyle = FontStyles.Bold;
        aText.color = new Color(0.25f, 0.65f, 0.90f, 0.85f);
        aText.alignment = TextAlignmentOptions.Center;
    }

    private void BuildDispenserSection()
    {
        // Right Card Container
        var cardObj = new GameObject("DispenserCard", typeof(RectTransform), typeof(Image));
        cardObj.transform.SetParent(panel, false);
        var cardRect = cardObj.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(1f, 1f);
        cardRect.pivot = new Vector2(1f, 1f);
        cardRect.anchoredPosition = new Vector2(-26f, -76f);
        cardRect.sizeDelta = new Vector2(350f, 452f);

        var cardImg = cardObj.GetComponent<Image>();
        cardImg.sprite = GetCardBoxSprite();
        cardImg.type = Image.Type.Sliced;
        cardImg.color = CardBg;
        cardObj.AddComponent<ResourceUIInputBlocker>();

        // 1. Duration Pill
        var durObj = new GameObject("DurationPill", typeof(RectTransform), typeof(Image));
        durObj.transform.SetParent(cardObj.transform, false);
        var durRect = durObj.GetComponent<RectTransform>();
        durRect.anchorMin = durRect.anchorMax = new Vector2(0.5f, 1f);
        durRect.pivot = new Vector2(0.5f, 1f);
        durRect.anchoredPosition = new Vector2(0f, -14f);
        durRect.sizeDelta = new Vector2(322f, 48f);

        var durImg = durObj.GetComponent<Image>();
        durImg.sprite = GetPillSprite();
        durImg.type = Image.Type.Sliced;
        durImg.color = new Color(0.92f, 0.96f, 1f, 1f);

        // Clock Icon
        var clkObj = new GameObject("ClockIcon", typeof(RectTransform), typeof(Image));
        clkObj.transform.SetParent(durObj.transform, false);
        var clkRect = clkObj.GetComponent<RectTransform>();
        clkRect.anchorMin = clkRect.anchorMax = new Vector2(0f, 0.5f);
        clkRect.pivot = new Vector2(0f, 0.5f);
        clkRect.anchoredPosition = new Vector2(12f, 0f);
        clkRect.sizeDelta = new Vector2(22f, 22f);
        var clkImg = clkObj.GetComponent<Image>();
        clkImg.sprite = GetClockIconSprite();
        clkImg.preserveAspect = true;
        clkImg.color = ThemeCyan;

        // Duration Label
        var durTxtObj = new GameObject("DurationLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        durTxtObj.transform.SetParent(durObj.transform, false);
        var dtRect = durTxtObj.GetComponent<RectTransform>();
        dtRect.anchorMin = dtRect.anchorMax = new Vector2(0f, 0.5f);
        dtRect.pivot = new Vector2(0f, 0.5f);
        dtRect.anchoredPosition = new Vector2(40f, 0f);
        dtRect.sizeDelta = new Vector2(270f, 38f);

        durationText = durTxtObj.GetComponent<TextMeshProUGUI>();
        durationText.font = Resources.Load<TMP_FontAsset>("Fonts/Nunito-Bold SDF");
        durationText.text = "<b>ESTIMATED DURATION</b>\n<color=#0284c7>Idle · No Food Assigned</color>";
        durationText.fontSize = 12.5f;
        durationText.color = ThemeDark;
        durationText.alignment = TextAlignmentOptions.MidlineLeft;

        // 2. Stats Row (Consumption & Total Energy)
        BuildStatsRow(cardObj.transform);

        // 3. Section Title: "DISPENSER SLOTS (3 MAX)"
        var slotsHeaderObj = new GameObject("SlotsHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
        slotsHeaderObj.transform.SetParent(cardObj.transform, false);
        var shRect = slotsHeaderObj.GetComponent<RectTransform>();
        shRect.anchorMin = shRect.anchorMax = new Vector2(0f, 1f);
        shRect.pivot = new Vector2(0f, 1f);
        shRect.anchoredPosition = new Vector2(16f, -142f);
        shRect.sizeDelta = new Vector2(318f, 22f);

        var shText = slotsHeaderObj.GetComponent<TextMeshProUGUI>();
        shText.font = durationText.font;
        shText.text = "DISPENSER SLOTS (3 MAX)";
        shText.fontSize = 12f;
        shText.fontStyle = FontStyles.Bold;
        shText.color = SubTextCol;

        // Divider
        var divObj = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        divObj.transform.SetParent(cardObj.transform, false);
        var divRect = divObj.GetComponent<RectTransform>();
        divRect.anchorMin = divRect.anchorMax = new Vector2(0.5f, 1f);
        divRect.pivot = new Vector2(0.5f, 1f);
        divRect.anchoredPosition = new Vector2(0f, -166f);
        divRect.sizeDelta = new Vector2(318f, 1.5f);
        var divImg = divObj.GetComponent<Image>();
        divImg.color = new Color(0.85f, 0.90f, 0.96f, 1f);

        // 4. 3 Food Slots Container
        BuildThreeSlots(cardObj.transform);

        // 5. Feedback Toast text
        var fbObj = new GameObject("FeedbackNotify", typeof(RectTransform), typeof(TextMeshProUGUI));
        fbObj.transform.SetParent(cardObj.transform, false);
        var fbRect = fbObj.GetComponent<RectTransform>();
        fbRect.anchorMin = fbRect.anchorMax = new Vector2(0.5f, 0f);
        fbRect.pivot = new Vector2(0.5f, 0f);
        fbRect.anchoredPosition = new Vector2(0f, 58f);
        fbRect.sizeDelta = new Vector2(320f, 26f);

        feedbackNotifyText = fbObj.GetComponent<TextMeshProUGUI>();
        feedbackNotifyText.font = durationText.font;
        feedbackNotifyText.fontSize = 12.5f;
        feedbackNotifyText.fontStyle = FontStyles.Bold;
        feedbackNotifyText.alignment = TextAlignmentOptions.Center;
        feedbackNotifyText.text = "";

        // 6. Action Buttons at bottom
        BuildBottomActionButtons(cardObj.transform);
    }

    private void BuildStatsRow(Transform parent)
    {
        float y = -70f;
        float w = 156f;
        float h = 60f;

        // Box 1: Consumption / Min
        var box1 = new GameObject("ConsumptionBox", typeof(RectTransform), typeof(Image));
        box1.transform.SetParent(parent, false);
        var b1Rect = box1.GetComponent<RectTransform>();
        b1Rect.anchorMin = b1Rect.anchorMax = new Vector2(0f, 1f);
        b1Rect.pivot = new Vector2(0f, 1f);
        b1Rect.anchoredPosition = new Vector2(14f, y);
        b1Rect.sizeDelta = new Vector2(w, h);

        var b1Img = box1.GetComponent<Image>();
        b1Img.sprite = GetCardBoxSprite();
        b1Img.type = Image.Type.Sliced;
        b1Img.color = Color.white;

        var l1Obj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        l1Obj.transform.SetParent(box1.transform, false);
        var l1Rect = l1Obj.GetComponent<RectTransform>();
        l1Rect.anchorMin = Vector2.zero; l1Rect.anchorMax = Vector2.one;
        l1Rect.offsetMin = new Vector2(8f, 4f); l1Rect.offsetMax = new Vector2(-8f, -4f);

        consumptionRateText = l1Obj.GetComponent<TextMeshProUGUI>();
        consumptionRateText.font = durationText.font;
        consumptionRateText.text = "<color=#64748b>Consumption/Min</color>\n<b><size=17><color=#0284c7>340</color></size></b>";
        consumptionRateText.fontSize = 11.5f;
        consumptionRateText.alignment = TextAlignmentOptions.Center;

        // Box 2: Total Food Energy
        var box2 = new GameObject("EnergyBox", typeof(RectTransform), typeof(Image));
        box2.transform.SetParent(parent, false);
        var b2Rect = box2.GetComponent<RectTransform>();
        b2Rect.anchorMin = b2Rect.anchorMax = new Vector2(1f, 1f);
        b2Rect.pivot = new Vector2(1f, 1f);
        b2Rect.anchoredPosition = new Vector2(-14f, y);
        b2Rect.sizeDelta = new Vector2(w, h);

        var b2Img = box2.GetComponent<Image>();
        b2Img.sprite = GetCardBoxSprite();
        b2Img.type = Image.Type.Sliced;
        b2Img.color = Color.white;

        var l2Obj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        l2Obj.transform.SetParent(box2.transform, false);
        var l2Rect = l2Obj.GetComponent<RectTransform>();
        l2Rect.anchorMin = Vector2.zero; l2Rect.anchorMax = Vector2.one;
        l2Rect.offsetMin = new Vector2(8f, 4f); l2Rect.offsetMax = new Vector2(-8f, -4f);

        totalEnergyText = l2Obj.GetComponent<TextMeshProUGUI>();
        totalEnergyText.font = durationText.font;
        totalEnergyText.text = "<color=#64748b>Total Food Energy</color>\n<b><size=17><color=#0284c7>0</color></size></b>";
        totalEnergyText.fontSize = 11.5f;
        totalEnergyText.alignment = TextAlignmentOptions.Center;
    }

    private void BuildThreeSlots(Transform parent)
    {
        float startX = 14f;
        float slotW = 100f;
        float slotH = 135f;
        float gapX = 11f;

        for (int i = 0; i < 3; i++)
        {
            int slotIndex = i;
            var slotObj = new GameObject("Slot_" + i, typeof(RectTransform));
            slotObj.transform.SetParent(parent, false);
            var sRect = slotObj.GetComponent<RectTransform>();
            sRect.anchorMin = sRect.anchorMax = new Vector2(0f, 1f);
            sRect.pivot = new Vector2(0f, 1f);
            sRect.anchoredPosition = new Vector2(startX + i * (slotW + gapX), -175f);
            sRect.sizeDelta = new Vector2(slotW, slotH);

            var view = new DispenserSlotView { index = slotIndex, root = sRect };

            // Plate frame
            var plateObj = new GameObject("Plate", typeof(RectTransform), typeof(Image));
            plateObj.transform.SetParent(slotObj.transform, false);
            var pRect = plateObj.GetComponent<RectTransform>();
            pRect.anchorMin = pRect.anchorMax = new Vector2(0.5f, 1f);
            pRect.pivot = new Vector2(0.5f, 1f);
            pRect.anchoredPosition = new Vector2(0f, 0f);
            pRect.sizeDelta = new Vector2(76f, 76f);

            var pImg = plateObj.GetComponent<Image>();
            pImg.sprite = GetCirclePlateSprite();
            pImg.type = Image.Type.Simple;
            pImg.preserveAspect = true;
            view.plateImage = pImg;

            // Empty placeholder (+)
            var emptyObj = new GameObject("EmptyPlaceholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            emptyObj.transform.SetParent(plateObj.transform, false);
            var epRect = emptyObj.GetComponent<RectTransform>();
            epRect.anchorMin = Vector2.zero; epRect.anchorMax = Vector2.one;
            epRect.offsetMin = Vector2.zero; epRect.offsetMax = Vector2.zero;
            var epText = emptyObj.GetComponent<TextMeshProUGUI>();
            epText.font = durationText.font;
            epText.text = "+";
            epText.fontSize = 28f;
            epText.fontStyle = FontStyles.Bold;
            epText.color = new Color(0.60f, 0.70f, 0.82f, 0.8f);
            epText.alignment = TextAlignmentOptions.Center;
            view.emptyPlaceholder = emptyObj;

            // Occupied container
            var occObj = new GameObject("Occupied", typeof(RectTransform));
            occObj.transform.SetParent(plateObj.transform, false);
            var ocRect = occObj.GetComponent<RectTransform>();
            ocRect.anchorMin = Vector2.zero; ocRect.anchorMax = Vector2.one;
            ocRect.offsetMin = Vector2.zero; ocRect.offsetMax = Vector2.zero;
            view.occupiedContainer = occObj;

            // Food Icon
            var icObj = new GameObject("FoodIcon", typeof(RectTransform), typeof(Image));
            icObj.transform.SetParent(occObj.transform, false);
            var icRect = icObj.GetComponent<RectTransform>();
            icRect.anchorMin = icRect.anchorMax = new Vector2(0.5f, 0.5f);
            icRect.pivot = new Vector2(0.5f, 0.5f);
            icRect.anchoredPosition = Vector2.zero;
            icRect.sizeDelta = new Vector2(46f, 46f);
            var icImg = icObj.GetComponent<Image>();
            icImg.preserveAspect = true;
            icImg.raycastTarget = false;
            view.iconImage = icImg;

            // Minus Cancel Button (-)
            var rmObj = new GameObject("RemoveBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            rmObj.transform.SetParent(slotObj.transform, false);
            var rmRect = rmObj.GetComponent<RectTransform>();
            rmRect.anchorMin = rmRect.anchorMax = new Vector2(0f, 1f);
            rmRect.pivot = new Vector2(0f, 1f);
            rmRect.anchoredPosition = new Vector2(6f, 0f);
            rmRect.sizeDelta = new Vector2(22f, 22f);

            var rmImg = rmObj.GetComponent<Image>();
            rmImg.sprite = GetCardBoxSprite();
            rmImg.type = Image.Type.Sliced;
            rmImg.color = new Color(0.92f, 0.28f, 0.28f, 1f);

            var rmTxtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            rmTxtObj.transform.SetParent(rmObj.transform, false);
            var rmtRect = rmTxtObj.GetComponent<RectTransform>();
            rmtRect.anchorMin = Vector2.zero; rmtRect.anchorMax = Vector2.one;
            rmtRect.offsetMin = Vector2.zero; rmtRect.offsetMax = Vector2.zero;
            var rmt = rmTxtObj.GetComponent<TextMeshProUGUI>();
            rmt.font = durationText.font;
            rmt.text = "–";
            rmt.fontSize = 15f;
            rmt.fontStyle = FontStyles.Bold;
            rmt.color = Color.white;
            rmt.alignment = TextAlignmentOptions.Center;

            var rmBtn = rmObj.GetComponent<Button>();
            rmBtn.targetGraphic = rmImg;
            rmBtn.onClick.AddListener(() => ClearSlot(slotIndex));
            view.removeButton = rmBtn;

            // Quantity Stepper Row ([-], Amount [Button], [+])
            var stepRowObj = new GameObject("QuantityStepperRow", typeof(RectTransform));
            stepRowObj.transform.SetParent(slotObj.transform, false);
            var srRect = stepRowObj.GetComponent<RectTransform>();
            srRect.anchorMin = srRect.anchorMax = new Vector2(0.5f, 1f);
            srRect.pivot = new Vector2(0.5f, 1f);
            srRect.anchoredPosition = new Vector2(0f, -80f);
            srRect.sizeDelta = new Vector2(94f, 22f);
            view.stepperRow = stepRowObj;

            // Dec Button [-]
            var decObj = new GameObject("DecBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            decObj.transform.SetParent(stepRowObj.transform, false);
            var dRect = decObj.GetComponent<RectTransform>();
            dRect.anchorMin = dRect.anchorMax = new Vector2(0f, 0.5f);
            dRect.pivot = new Vector2(0f, 0.5f);
            dRect.anchoredPosition = new Vector2(0f, 0f);
            dRect.sizeDelta = new Vector2(20f, 20f);

            var dImg = decObj.GetComponent<Image>();
            dImg.sprite = GetCardBoxSprite();
            dImg.type = Image.Type.Sliced;
            dImg.color = new Color(0.92f, 0.95f, 0.98f, 1f);

            var dTxtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            dTxtObj.transform.SetParent(decObj.transform, false);
            var dtRect = dTxtObj.GetComponent<RectTransform>();
            dtRect.anchorMin = Vector2.zero; dtRect.anchorMax = Vector2.one;
            dtRect.offsetMin = Vector2.zero; dtRect.offsetMax = Vector2.zero;
            var dt = dTxtObj.GetComponent<TextMeshProUGUI>();
            dt.font = durationText.font;
            dt.text = "–";
            dt.fontSize = 13f;
            dt.fontStyle = FontStyles.Bold;
            dt.color = ThemeDark;
            dt.alignment = TextAlignmentOptions.Center;

            var dBtn = decObj.GetComponent<Button>();
            dBtn.targetGraphic = dImg;
            dBtn.onClick.AddListener(() => ChangeSlotAmount(slotIndex, -1));
            view.decButton = dBtn;

            // Amount Button (Center)
            var amtBtnObj = new GameObject("AmountBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            amtBtnObj.transform.SetParent(stepRowObj.transform, false);
            var abRect = amtBtnObj.GetComponent<RectTransform>();
            abRect.anchorMin = abRect.anchorMax = new Vector2(0.5f, 0.5f);
            abRect.pivot = new Vector2(0.5f, 0.5f);
            abRect.anchoredPosition = new Vector2(0f, 0f);
            abRect.sizeDelta = new Vector2(50f, 20f);

            var abImg = amtBtnObj.GetComponent<Image>();
            abImg.sprite = GetCardBoxSprite();
            abImg.type = Image.Type.Sliced;
            abImg.color = new Color(0.95f, 0.98f, 1f, 1f);

            var aTxtObj = new GameObject("AmountText", typeof(RectTransform), typeof(TextMeshProUGUI));
            aTxtObj.transform.SetParent(amtBtnObj.transform, false);
            var atRect = aTxtObj.GetComponent<RectTransform>();
            atRect.anchorMin = Vector2.zero; atRect.anchorMax = Vector2.one;
            atRect.offsetMin = Vector2.zero; atRect.offsetMax = Vector2.zero;
            var aTxt = aTxtObj.GetComponent<TextMeshProUGUI>();
            aTxt.font = durationText.font;
            aTxt.text = "";
            aTxt.fontSize = 11.5f;
            aTxt.fontStyle = FontStyles.Bold;
            aTxt.color = ThemeDark;
            aTxt.alignment = TextAlignmentOptions.Center;
            view.amountText = aTxt;

            var aBtn = amtBtnObj.GetComponent<Button>();
            aBtn.targetGraphic = abImg;
            aBtn.onClick.AddListener(() => OpenQuantityModal(slotIndex));
            view.amountButton = aBtn;

            // Inc Button [+]
            var incObj = new GameObject("IncBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            incObj.transform.SetParent(stepRowObj.transform, false);
            var iRect = incObj.GetComponent<RectTransform>();
            iRect.anchorMin = iRect.anchorMax = new Vector2(1f, 0.5f);
            iRect.pivot = new Vector2(1f, 0.5f);
            iRect.anchoredPosition = new Vector2(0f, 0f);
            iRect.sizeDelta = new Vector2(20f, 20f);

            var iImg = incObj.GetComponent<Image>();
            iImg.sprite = GetCardBoxSprite();
            iImg.type = Image.Type.Sliced;
            iImg.color = new Color(0.92f, 0.95f, 0.98f, 1f);

            var iTxtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            iTxtObj.transform.SetParent(incObj.transform, false);
            var itRect = iTxtObj.GetComponent<RectTransform>();
            itRect.anchorMin = Vector2.zero; itRect.anchorMax = Vector2.one;
            itRect.offsetMin = Vector2.zero; itRect.offsetMax = Vector2.zero;
            var it = iTxtObj.GetComponent<TextMeshProUGUI>();
            it.font = durationText.font;
            it.text = "+";
            it.fontSize = 13f;
            it.fontStyle = FontStyles.Bold;
            it.color = ThemeDark;
            it.alignment = TextAlignmentOptions.Center;

            var iBtn = incObj.GetComponent<Button>();
            iBtn.targetGraphic = iImg;
            iBtn.onClick.AddListener(() => ChangeSlotAmount(slotIndex, 1));
            view.incButton = iBtn;

            // Food Name
            var nameObj = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameObj.transform.SetParent(slotObj.transform, false);
            var nRect = nameObj.GetComponent<RectTransform>();
            nRect.anchorMin = nRect.anchorMax = new Vector2(0.5f, 1f);
            nRect.pivot = new Vector2(0.5f, 1f);
            nRect.anchoredPosition = new Vector2(0f, -104f);
            nRect.sizeDelta = new Vector2(slotW, 28f);

            var nTxt = nameObj.GetComponent<TextMeshProUGUI>();
            nTxt.font = durationText.font;
            nTxt.text = "Empty";
            nTxt.fontSize = 11f;
            nTxt.color = SubTextCol;
            nTxt.alignment = TextAlignmentOptions.Center;
            nTxt.textWrappingMode = TextWrappingModes.Normal;
            nTxt.overflowMode = TextOverflowModes.Ellipsis;
            view.nameText = nTxt;

            slotViews[i] = view;
        }
    }

    private void BuildBottomActionButtons(Transform parent)
    {
        // 1. Fill All Button
        var fillObj = new GameObject("FillAllBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        fillObj.transform.SetParent(parent, false);
        var fRect = fillObj.GetComponent<RectTransform>();
        fRect.anchorMin = fRect.anchorMax = new Vector2(0f, 0f);
        fRect.pivot = new Vector2(0f, 0f);
        fRect.anchoredPosition = new Vector2(14f, 14f);
        fRect.sizeDelta = new Vector2(104f, 40f);

        var fImg = fillObj.GetComponent<Image>();
        fImg.sprite = GetButtonSecondarySprite();
        fImg.type = Image.Type.Sliced;
        fImg.color = Color.white;
        fillObj.AddComponent<ResourceUIInputBlocker>();

        var fTxtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        fTxtObj.transform.SetParent(fillObj.transform, false);
        var ftRect = fTxtObj.GetComponent<RectTransform>();
        ftRect.anchorMin = Vector2.zero; ftRect.anchorMax = Vector2.one;
        ftRect.offsetMin = Vector2.zero; ftRect.offsetMax = Vector2.zero;
        var ftText = fTxtObj.GetComponent<TextMeshProUGUI>();
        ftText.font = durationText.font;
        ftText.text = "Fill Max";
        ftText.fontSize = 13.5f;
        ftText.fontStyle = FontStyles.Bold;
        ftText.color = ThemeDark;
        ftText.alignment = TextAlignmentOptions.Center;

        fillAllButton = fillObj.GetComponent<Button>();
        fillAllButton.targetGraphic = fImg;
        fillAllButton.onClick.AddListener(OnFillAllClicked);

        // 2. Confirm Auto-Feed Button
        var confObj = new GameObject("ConfirmBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        confObj.transform.SetParent(parent, false);
        var cRect = confObj.GetComponent<RectTransform>();
        cRect.anchorMin = cRect.anchorMax = new Vector2(1f, 0f);
        cRect.pivot = new Vector2(1f, 0f);
        cRect.anchoredPosition = new Vector2(-14f, 14f);
        cRect.sizeDelta = new Vector2(210f, 40f);

        var cImg = confObj.GetComponent<Image>();
        cImg.sprite = GetButtonPrimarySprite();
        cImg.type = Image.Type.Sliced;
        cImg.color = Color.white;
        confObj.AddComponent<ResourceUIInputBlocker>();

        var cTxtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        cTxtObj.transform.SetParent(confObj.transform, false);
        var ctRect = cTxtObj.GetComponent<RectTransform>();
        ctRect.anchorMin = Vector2.zero; ctRect.anchorMax = Vector2.one;
        ctRect.offsetMin = Vector2.zero; ctRect.offsetMax = Vector2.zero;
        var ctText = cTxtObj.GetComponent<TextMeshProUGUI>();
        ctText.font = durationText.font;
        ctText.text = "Confirm Auto-Feed";
        ctText.fontSize = 14f;
        ctText.fontStyle = FontStyles.Bold;
        ctText.color = Color.white;
        ctText.alignment = TextAlignmentOptions.Center;

        confirmButton = confObj.GetComponent<Button>();
        confirmButton.targetGraphic = cImg;
        confirmButton.onClick.AddListener(OnConfirmAutoFeedClicked);
    }

    public void RefreshFoodInventory()
    {
        currentFoodList.Clear();
        foodCardBorders.Clear();

        // Destroy existing grid items
        for (int i = foodGridContent.childCount - 1; i >= 0; i--)
        {
            Destroy(foodGridContent.GetChild(i).gameObject);
        }

        var production = ResourceProductionRuntime.Instance;
        var inventory = production != null ? production.Inventory : null;

        ResourceProductionCatalog catalog = null;
        if (production != null && production.HabitatManager != null)
        {
            catalog = production.HabitatManager.Catalog;
        }
        else if (HabitatRuntimeManager.TryGetActive(out var hrm))
        {
            catalog = hrm.Catalog;
        }

        if (inventory != null && catalog != null)
        {
            var all = ResourceInventoryUIPresentation.Warehouse(catalog, inventory.GetAmount, onlyCollected: false);
            if (all != null)
            {
                foreach (var item in all)
                {
                    if (item != null && string.Equals(item.category, "Food", StringComparison.OrdinalIgnoreCase))
                    {
                        if (item.stock > 0)
                        {
                            currentFoodList.Add(item);
                        }
                    }
                }
            }
        }

        // Fallback: check CraftingCatalog recipes with category == Food
        if (currentFoodList.Count == 0 && inventory != null)
        {
            foreach (var recipe in CraftingCatalog.All)
            {
                if (recipe != null && recipe.category == CraftingCategory.Food && recipe.outputResource != null)
                {
                    string id = recipe.outputResource.resourceID;
                    int stock = inventory.GetAmount(id);
                    if (stock > 0 && !currentFoodList.Exists(x => x.id == id))
                    {
                        currentFoodList.Add(new ResourceUIItem
                        {
                            id = id,
                            name = recipe.recipeName,
                            icon = recipe.GetIcon(),
                            category = "Food",
                            stock = stock
                        });
                    }
                }
            }
        }

        if (availableFoodCountText != null)
        {
            availableFoodCountText.text = $"FOOD STORAGE ({currentFoodList.Count} ITEMS)";
        }

        if (emptyFoodNoticeText != null)
        {
            emptyFoodNoticeText.gameObject.SetActive(currentFoodList.Count == 0);
        }

        if (currentFoodList.Count == 0)
        {
            foodGridContent.sizeDelta = new Vector2(0f, 420f);
            return;
        }

        // Render food grid
        const int cols = 6;
        const float itemW = 74f;
        const float itemH = 74f;
        const float gapX = 10f;
        const float gapY = 10f;
        const float startX = 6f;
        const float startY = 6f;

        for (int i = 0; i < currentFoodList.Count; i++)
        {
            var item = currentFoodList[i];
            int col = i % cols;
            int row = i / cols;
            float x = startX + col * (itemW + gapX);
            float y = -(startY + row * (itemH + gapY));

            var cardObj = new GameObject("FoodItem_" + item.id, typeof(RectTransform), typeof(Image), typeof(Button));
            cardObj.transform.SetParent(foodGridContent, false);
            var cardRect = cardObj.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0f, 1f);
            cardRect.anchorMax = new Vector2(0f, 1f);
            cardRect.pivot = new Vector2(0f, 1f);
            cardRect.anchoredPosition = new Vector2(x, y);
            cardRect.sizeDelta = new Vector2(itemW, itemH);

            var cardImg = cardObj.GetComponent<Image>();
            cardImg.sprite = GetCardBoxSprite();
            cardImg.type = Image.Type.Sliced;
            cardImg.color = Color.white;
            cardObj.AddComponent<ResourceUIInputBlocker>();

            // Selected border
            var selObj = new GameObject("Border", typeof(RectTransform), typeof(Image));
            selObj.transform.SetParent(cardObj.transform, false);
            var selRect = selObj.GetComponent<RectTransform>();
            selRect.anchorMin = Vector2.zero; selRect.anchorMax = Vector2.one;
            selRect.offsetMin = Vector2.zero; selRect.offsetMax = Vector2.zero;
            var selImg = selObj.GetComponent<Image>();
            selImg.sprite = GetCardBoxSprite();
            selImg.type = Image.Type.Sliced;
            selImg.color = ThemeCyan;
            selImg.raycastTarget = false;
            selObj.SetActive(IsFoodAssigned(item.id));
            foodCardBorders[item.id] = selImg;

            // Icon
            if (item.icon != null)
            {
                var icObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                icObj.transform.SetParent(cardObj.transform, false);
                var icRect = icObj.GetComponent<RectTransform>();
                icRect.anchorMin = icRect.anchorMax = new Vector2(0.5f, 0.5f);
                icRect.pivot = new Vector2(0.5f, 0.5f);
                icRect.anchoredPosition = new Vector2(0f, 4f);
                icRect.sizeDelta = new Vector2(46f, 46f);

                var icImg = icObj.GetComponent<Image>();
                icImg.sprite = item.icon;
                icImg.preserveAspect = true;
                icImg.raycastTarget = false;
            }

            // Stock Count
            var cntObj = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
            cntObj.transform.SetParent(cardObj.transform, false);
            var cntRect = cntObj.GetComponent<RectTransform>();
            cntRect.anchorMin = new Vector2(0f, 0f);
            cntRect.anchorMax = new Vector2(1f, 0f);
            cntRect.pivot = new Vector2(1f, 0f);
            cntRect.anchoredPosition = new Vector2(-4f, 4f);
            cntRect.sizeDelta = new Vector2(itemW - 8f, 18f);

            var cntTxt = cntObj.GetComponent<TextMeshProUGUI>();
            cntTxt.font = availableFoodCountText.font;
            cntTxt.text = item.stock.ToString();
            cntTxt.fontSize = 12f;
            cntTxt.fontStyle = FontStyles.Bold;
            cntTxt.color = ThemeCyan;
            cntTxt.alignment = TextAlignmentOptions.BottomRight;

            var btn = cardObj.GetComponent<Button>();
            btn.targetGraphic = cardImg;
            string capturedId = item.id;
            btn.onClick.AddListener(() => OnFoodItemClicked(capturedId));
        }

        int totalRows = Mathf.CeilToInt(currentFoodList.Count / (float)cols);
        float totalHeight = startY * 2 + totalRows * (itemH + gapY);
        foodGridContent.sizeDelta = new Vector2(0f, Mathf.Max(420f, totalHeight));
    }

    private bool IsFoodAssigned(string foodId)
    {
        if (string.IsNullOrEmpty(foodId)) return false;
        for (int i = 0; i < 3; i++)
        {
            if (string.Equals(AssignedFoodIds[i], foodId, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private void OnFoodItemClicked(string foodId)
    {
        GameAudioManager.Instance?.PlayClick();

        // 1. If already assigned in a slot, show notice
        for (int i = 0; i < 3; i++)
        {
            if (string.Equals(AssignedFoodIds[i], foodId, StringComparison.OrdinalIgnoreCase))
            {
                ShowFeedback($"Item is already assigned to Slot {i + 1}!", new Color(0.2f, 0.70f, 0.95f, 1f));
                return;
            }
        }

        // 2. Find first empty slot
        int targetSlot = -1;
        for (int i = 0; i < 3; i++)
        {
            if (string.IsNullOrEmpty(AssignedFoodIds[i]))
            {
                targetSlot = i;
                break;
            }
        }

        // 3. If all slots full, replace slot 0
        if (targetSlot == -1)
        {
            targetSlot = 0;
            ShowFeedback("All 3 slots full. Replaced Slot 1 with selected item.", new Color(0.95f, 0.65f, 0.20f, 1f));
        }

        AssignFoodToSlot(targetSlot, foodId);
    }

    private void AssignFoodToSlot(int slotIndex, string foodId)
    {
        if (slotIndex < 0 || slotIndex >= 3) return;

        var food = currentFoodList.Find(f => f.id == foodId);
        int stock = food != null ? food.stock : 0;
        int assignAmount = stock;

        AssignedFoodIds[slotIndex] = foodId;
        AssignedFoodAmounts[slotIndex] = assignAmount;

        SaveSlots();
        RefreshDispenserStats();
        UpdateBorders();

        string name = food != null ? food.name : foodId;
        ShowFeedback($"Assigned {name} ({assignAmount}) to Slot {slotIndex + 1}.", new Color(0.12f, 0.85f, 0.40f, 1f));
    }

    private void ClearSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 3) return;
        GameAudioManager.Instance?.PlayClick();

        string clearedId = AssignedFoodIds[slotIndex];
        AssignedFoodIds[slotIndex] = string.Empty;
        AssignedFoodAmounts[slotIndex] = 0;

        SaveSlots();
        RefreshDispenserStats();
        UpdateBorders();

        ShowFeedback($"Cleared Slot {slotIndex + 1}.", new Color(0.85f, 0.45f, 0.45f, 1f));
    }

    private void ChangeSlotAmount(int slotIndex, int delta)
    {
        if (slotIndex < 0 || slotIndex >= 3) return;
        string foodId = AssignedFoodIds[slotIndex];
        if (string.IsNullOrEmpty(foodId)) return;

        var food = currentFoodList.Find(f => f.id == foodId);
        int maxStock = food != null ? food.stock : 1;
        if (maxStock <= 0) maxStock = 1;

        int current = AssignedFoodAmounts[slotIndex];
        int next = Mathf.Clamp(current + delta, 1, maxStock);
        if (next != current)
        {
            AssignedFoodAmounts[slotIndex] = next;
            SaveSlots();
            RefreshDispenserStats();
            GameAudioManager.Instance?.PlayClick();
        }
    }

    private void OpenQuantityModal(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 3) return;
        string foodId = AssignedFoodIds[slotIndex];
        if (string.IsNullOrEmpty(foodId)) return;

        var food = currentFoodList.Find(f => f.id == foodId);
        int maxStock = food != null ? food.stock : 1;
        if (maxStock <= 0) maxStock = 1;

        modalEditingSlot = slotIndex;
        modalMaxStock = maxStock;
        modalCurrentAmount = Mathf.Clamp(AssignedFoodAmounts[slotIndex], 1, maxStock);

        if (quantityModalObj == null)
        {
            BuildQuantityModal();
        }

        if (modalFoodIcon != null)
        {
            modalFoodIcon.sprite = food != null ? food.icon : null;
            modalFoodIcon.gameObject.SetActive(modalFoodIcon.sprite != null);
        }
        if (modalFoodNameText != null)
        {
            modalFoodNameText.text = food != null ? food.name : foodId;
        }
        if (modalStorageText != null)
        {
            modalStorageText.text = $"Available in Storage: <color=#0284c7><b>{maxStock:N0}</b></color>";
        }

        if (modalSlider != null)
        {
            modalSlider.minValue = 1;
            modalSlider.maxValue = maxStock;
            modalSlider.SetValueWithoutNotify(modalCurrentAmount);
        }

        UpdateModalAmountDisplay();
        quantityModalObj.SetActive(true);
        quantityModalObj.transform.SetAsLastSibling();
        GameAudioManager.Instance?.PlayClick();
    }

    private void BuildQuantityModal()
    {
        if (panel == null) return;

        // Overlay backdrop
        quantityModalObj = new GameObject("QuantityModalOverlay", typeof(RectTransform), typeof(Image));
        quantityModalObj.transform.SetParent(panel, false);
        var qRect = quantityModalObj.GetComponent<RectTransform>();
        qRect.anchorMin = Vector2.zero;
        qRect.anchorMax = Vector2.one;
        qRect.offsetMin = Vector2.zero;
        qRect.offsetMax = Vector2.zero;

        var qImg = quantityModalObj.GetComponent<Image>();
        qImg.color = new Color(0.04f, 0.08f, 0.16f, 0.65f);
        quantityModalObj.AddComponent<ResourceUIInputBlocker>();

        // Dialog Card
        var cardObj = new GameObject("Card", typeof(RectTransform), typeof(Image));
        cardObj.transform.SetParent(quantityModalObj.transform, false);
        var cRect = cardObj.GetComponent<RectTransform>();
        cRect.anchorMin = cRect.anchorMax = new Vector2(0.5f, 0.5f);
        cRect.pivot = new Vector2(0.5f, 0.5f);
        cRect.anchoredPosition = Vector2.zero;
        cRect.sizeDelta = new Vector2(330f, 250f);

        var cImg = cardObj.GetComponent<Image>();
        cImg.sprite = GetCardBoxSprite();
        cImg.type = Image.Type.Sliced;
        cImg.color = Color.white;
        EconomyHUD.AddBlueBorder(cRect, ThemeCyan);

        // Header Title
        var titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(cardObj.transform, false);
        var tRect = titleObj.GetComponent<RectTransform>();
        tRect.anchorMin = tRect.anchorMax = new Vector2(0.5f, 1f);
        tRect.pivot = new Vector2(0.5f, 1f);
        tRect.anchoredPosition = new Vector2(0f, -14f);
        tRect.sizeDelta = new Vector2(240f, 24f);

        var tTxt = titleObj.GetComponent<TextMeshProUGUI>();
        tTxt.font = durationText.font;
        tTxt.text = "ADJUST FOOD QUANTITY";
        tTxt.fontSize = 13f;
        tTxt.fontStyle = FontStyles.Bold;
        tTxt.color = ThemeDark;
        tTxt.alignment = TextAlignmentOptions.Center;

        // Close X button
        var closeObj = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        closeObj.transform.SetParent(cardObj.transform, false);
        var clRect = closeObj.GetComponent<RectTransform>();
        clRect.anchorMin = clRect.anchorMax = new Vector2(1f, 1f);
        clRect.pivot = new Vector2(1f, 1f);
        clRect.anchoredPosition = new Vector2(-10f, -10f);
        clRect.sizeDelta = new Vector2(26f, 26f);

        var clImg = closeObj.GetComponent<Image>();
        clImg.sprite = GetCardBoxSprite();
        clImg.type = Image.Type.Sliced;
        clImg.color = new Color(0.92f, 0.95f, 0.98f, 1f);

        var clTxtObj = new GameObject("X", typeof(RectTransform), typeof(TextMeshProUGUI));
        clTxtObj.transform.SetParent(closeObj.transform, false);
        var cltRect = clTxtObj.GetComponent<RectTransform>();
        cltRect.anchorMin = Vector2.zero; cltRect.anchorMax = Vector2.one;
        cltRect.offsetMin = Vector2.zero; cltRect.offsetMax = Vector2.zero;
        var clTxt = clTxtObj.GetComponent<TextMeshProUGUI>();
        clTxt.font = durationText.font;
        clTxt.text = "X";
        clTxt.fontSize = 14f;
        clTxt.fontStyle = FontStyles.Bold;
        clTxt.color = SubTextCol;
        clTxt.alignment = TextAlignmentOptions.Center;

        var clBtn = closeObj.GetComponent<Button>();
        clBtn.targetGraphic = clImg;
        clBtn.onClick.AddListener(CloseQuantityModal);

        // Food Info Row (y = -44f)
        var iconObj = new GameObject("FoodIcon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(cardObj.transform, false);
        var iRect = iconObj.GetComponent<RectTransform>();
        iRect.anchorMin = iRect.anchorMax = new Vector2(0f, 1f);
        iRect.pivot = new Vector2(0f, 1f);
        iRect.anchoredPosition = new Vector2(20f, -44f);
        iRect.sizeDelta = new Vector2(34f, 34f);
        modalFoodIcon = iconObj.GetComponent<Image>();
        modalFoodIcon.preserveAspect = true;

        var nameObj = new GameObject("FoodName", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameObj.transform.SetParent(cardObj.transform, false);
        var nRect = nameObj.GetComponent<RectTransform>();
        nRect.anchorMin = nRect.anchorMax = new Vector2(0f, 1f);
        nRect.pivot = new Vector2(0f, 1f);
        nRect.anchoredPosition = new Vector2(62f, -44f);
        nRect.sizeDelta = new Vector2(250f, 18f);
        modalFoodNameText = nameObj.GetComponent<TextMeshProUGUI>();
        modalFoodNameText.font = durationText.font;
        modalFoodNameText.fontSize = 13f;
        modalFoodNameText.fontStyle = FontStyles.Bold;
        modalFoodNameText.color = ThemeDark;

        var stObj = new GameObject("StorageInfo", typeof(RectTransform), typeof(TextMeshProUGUI));
        stObj.transform.SetParent(cardObj.transform, false);
        var sRect = stObj.GetComponent<RectTransform>();
        sRect.anchorMin = sRect.anchorMax = new Vector2(0f, 1f);
        sRect.pivot = new Vector2(0f, 1f);
        sRect.anchoredPosition = new Vector2(62f, -62f);
        sRect.sizeDelta = new Vector2(250f, 16f);
        modalStorageText = stObj.GetComponent<TextMeshProUGUI>();
        modalStorageText.font = durationText.font;
        modalStorageText.fontSize = 11f;
        modalStorageText.color = SubTextCol;

        // Big Quantity Display (y = -86f)
        var valObj = new GameObject("AmountValue", typeof(RectTransform), typeof(TextMeshProUGUI));
        valObj.transform.SetParent(cardObj.transform, false);
        var vRect = valObj.GetComponent<RectTransform>();
        vRect.anchorMin = vRect.anchorMax = new Vector2(0.5f, 1f);
        vRect.pivot = new Vector2(0.5f, 1f);
        vRect.anchoredPosition = new Vector2(0f, -86f);
        vRect.sizeDelta = new Vector2(200f, 26f);
        modalAmountValueText = valObj.GetComponent<TextMeshProUGUI>();
        modalAmountValueText.font = durationText.font;
        modalAmountValueText.fontSize = 22f;
        modalAmountValueText.fontStyle = FontStyles.Bold;
        modalAmountValueText.color = new Color(0.02f, 0.48f, 0.72f, 1f);
        modalAmountValueText.alignment = TextAlignmentOptions.Center;

        // Slider (y = -120f)
        var sliderObj = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderObj.transform.SetParent(cardObj.transform, false);
        var slRect = sliderObj.GetComponent<RectTransform>();
        slRect.anchorMin = slRect.anchorMax = new Vector2(0.5f, 1f);
        slRect.pivot = new Vector2(0.5f, 1f);
        slRect.anchoredPosition = new Vector2(0f, -120f);
        slRect.sizeDelta = new Vector2(270f, 18f);

        modalSlider = sliderObj.GetComponent<Slider>();
        modalSlider.wholeNumbers = true;

        // Slider Track
        var trackObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
        trackObj.transform.SetParent(sliderObj.transform, false);
        var trRect = trackObj.GetComponent<RectTransform>();
        trRect.anchorMin = new Vector2(0f, 0.35f);
        trRect.anchorMax = new Vector2(1f, 0.65f);
        trRect.offsetMin = Vector2.zero; trRect.offsetMax = Vector2.zero;
        var trImg = trackObj.GetComponent<Image>();
        trImg.sprite = GetCardBoxSprite();
        trImg.type = Image.Type.Sliced;
        trImg.color = new Color(0.88f, 0.92f, 0.96f, 1f);

        // Fill Area
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObj.transform, false);
        var faRect = fillArea.GetComponent<RectTransform>();
        faRect.anchorMin = new Vector2(0f, 0.35f);
        faRect.anchorMax = new Vector2(1f, 0.65f);
        faRect.sizeDelta = new Vector2(-12f, 0f);

        var fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObj.transform.SetParent(fillArea.transform, false);
        var fRect = fillObj.GetComponent<RectTransform>();
        fRect.anchorMin = Vector2.zero; fRect.anchorMax = Vector2.one;
        fRect.offsetMin = Vector2.zero; fRect.offsetMax = Vector2.zero;
        var fImg = fillObj.GetComponent<Image>();
        fImg.sprite = GetCardBoxSprite();
        fImg.type = Image.Type.Sliced;
        fImg.color = ThemeCyan;
        modalSlider.fillRect = fRect;

        // Handle Slide Area
        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderObj.transform, false);
        var haRect = handleArea.GetComponent<RectTransform>();
        haRect.anchorMin = Vector2.zero; haRect.anchorMax = Vector2.one;
        haRect.sizeDelta = new Vector2(-16f, 0f);

        var handleObj = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObj.transform.SetParent(handleArea.transform, false);
        var hRect = handleObj.GetComponent<RectTransform>();
        hRect.sizeDelta = new Vector2(18f, 18f);
        var hImg = handleObj.GetComponent<Image>();
        hImg.sprite = GetCirclePlateSprite();
        hImg.color = Color.white;
        modalSlider.handleRect = hRect;
        modalSlider.targetGraphic = hImg;

        modalSlider.onValueChanged.AddListener((v) =>
        {
            modalCurrentAmount = Mathf.RoundToInt(v);
            UpdateModalAmountDisplay();
        });

        // Preset Buttons Row (y = -150f)
        float[] presetOffsets = { -105f, -52f, 0f, 52f, 105f };
        string[] presetLabels = { "Min", "-10", "+10", "50%", "Max" };
        Action[] presetActions =
        {
            () => SetModalAmount(1),
            () => SetModalAmount(modalCurrentAmount - 10),
            () => SetModalAmount(modalCurrentAmount + 10),
            () => SetModalAmount(Mathf.Max(1, modalMaxStock / 2)),
            () => SetModalAmount(modalMaxStock)
        };

        for (int p = 0; p < 5; p++)
        {
            int pIdx = p;
            var pBtnObj = new GameObject("Preset_" + presetLabels[p], typeof(RectTransform), typeof(Image), typeof(Button));
            pBtnObj.transform.SetParent(cardObj.transform, false);
            var pbRect = pBtnObj.GetComponent<RectTransform>();
            pbRect.anchorMin = pbRect.anchorMax = new Vector2(0.5f, 1f);
            pbRect.pivot = new Vector2(0.5f, 1f);
            pbRect.anchoredPosition = new Vector2(presetOffsets[p], -150f);
            pbRect.sizeDelta = new Vector2(46f, 24f);

            var pbImg = pBtnObj.GetComponent<Image>();
            pbImg.sprite = GetCardBoxSprite();
            pbImg.type = Image.Type.Sliced;
            pbImg.color = new Color(0.93f, 0.96f, 0.99f, 1f);

            var pbtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            pbtObj.transform.SetParent(pBtnObj.transform, false);
            var pbtRect = pbtObj.GetComponent<RectTransform>();
            pbtRect.anchorMin = Vector2.zero; pbtRect.anchorMax = Vector2.one;
            pbtRect.offsetMin = Vector2.zero; pbtRect.offsetMax = Vector2.zero;
            var pbt = pbtObj.GetComponent<TextMeshProUGUI>();
            pbt.font = durationText.font;
            pbt.text = presetLabels[p];
            pbt.fontSize = 11f;
            pbt.fontStyle = FontStyles.Bold;
            pbt.color = ThemeDark;
            pbt.alignment = TextAlignmentOptions.Center;

            var pbBtn = pBtnObj.GetComponent<Button>();
            pbBtn.targetGraphic = pbImg;
            pbBtn.onClick.AddListener(() => presetActions[pIdx]());
        }

        // Action Buttons: Cancel & Apply (y = -196f)
        var cancelObj = new GameObject("CancelBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        cancelObj.transform.SetParent(cardObj.transform, false);
        var cbRect = cancelObj.GetComponent<RectTransform>();
        cbRect.anchorMin = cbRect.anchorMax = new Vector2(0.5f, 1f);
        cbRect.pivot = new Vector2(0.5f, 1f);
        cbRect.anchoredPosition = new Vector2(-65f, -196f);
        cbRect.sizeDelta = new Vector2(110f, 34f);

        var cbImg = cancelObj.GetComponent<Image>();
        cbImg.sprite = GetButtonSecondarySprite();
        cbImg.type = Image.Type.Sliced;
        cbImg.color = Color.white;

        var cbtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        cbtObj.transform.SetParent(cancelObj.transform, false);
        var cbtRect = cbtObj.GetComponent<RectTransform>();
        cbtRect.anchorMin = Vector2.zero; cbtRect.anchorMax = Vector2.one;
        cbtRect.offsetMin = Vector2.zero; cbtRect.offsetMax = Vector2.zero;
        var cbt = cbtObj.GetComponent<TextMeshProUGUI>();
        cbt.font = durationText.font;
        cbt.text = "Cancel";
        cbt.fontSize = 12f;
        cbt.fontStyle = FontStyles.Bold;
        cbt.color = SubTextCol;
        cbt.alignment = TextAlignmentOptions.Center;

        var cbBtn = cancelObj.GetComponent<Button>();
        cbBtn.targetGraphic = cbImg;
        cbBtn.onClick.AddListener(CloseQuantityModal);

        var applyObj = new GameObject("ApplyBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        applyObj.transform.SetParent(cardObj.transform, false);
        var abRect = applyObj.GetComponent<RectTransform>();
        abRect.anchorMin = abRect.anchorMax = new Vector2(0.5f, 1f);
        abRect.pivot = new Vector2(0.5f, 1f);
        abRect.anchoredPosition = new Vector2(65f, -196f);
        abRect.sizeDelta = new Vector2(110f, 34f);

        var abImg = applyObj.GetComponent<Image>();
        abImg.sprite = GetButtonPrimarySprite();
        abImg.type = Image.Type.Sliced;
        abImg.color = Color.white;

        var abtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        abtObj.transform.SetParent(applyObj.transform, false);
        var abtRect = abtObj.GetComponent<RectTransform>();
        abtRect.anchorMin = Vector2.zero; abtRect.anchorMax = Vector2.one;
        abtRect.offsetMin = Vector2.zero; abtRect.offsetMax = Vector2.zero;
        var abt = abtObj.GetComponent<TextMeshProUGUI>();
        abt.font = durationText.font;
        abt.text = "Apply";
        abt.fontSize = 12f;
        abt.fontStyle = FontStyles.Bold;
        abt.color = Color.white;
        abt.alignment = TextAlignmentOptions.Center;

        var abBtn = applyObj.GetComponent<Button>();
        abBtn.targetGraphic = abImg;
        abBtn.onClick.AddListener(OnApplyQuantityModal);
    }

    private void SetModalAmount(int target)
    {
        modalCurrentAmount = Mathf.Clamp(target, 1, modalMaxStock);
        if (modalSlider != null)
        {
            modalSlider.SetValueWithoutNotify(modalCurrentAmount);
        }
        UpdateModalAmountDisplay();
        GameAudioManager.Instance?.PlayClick();
    }

    private void UpdateModalAmountDisplay()
    {
        if (modalAmountValueText != null)
        {
            modalAmountValueText.text = $"{modalCurrentAmount:N0}";
        }
    }

    private void CloseQuantityModal()
    {
        if (quantityModalObj != null)
        {
            quantityModalObj.SetActive(false);
        }
        GameAudioManager.Instance?.PlayClick();
    }

    private void OnApplyQuantityModal()
    {
        if (modalEditingSlot >= 0 && modalEditingSlot < 3)
        {
            AssignedFoodAmounts[modalEditingSlot] = modalCurrentAmount;
            SaveSlots();
            RefreshDispenserStats();

            string foodId = AssignedFoodIds[modalEditingSlot];
            var food = currentFoodList.Find(f => f.id == foodId);
            string name = food != null ? food.name : foodId;
            ShowFeedback($"Updated {name} quantity to {modalCurrentAmount:N0}.", new Color(0.12f, 0.85f, 0.40f, 1f));
        }

        CloseQuantityModal();
    }

    private void OnFillAllClicked()
    {
        GameAudioManager.Instance?.PlayClick();

        int assignedCount = 0;
        for (int i = 0; i < 3; i++)
        {
            if (string.IsNullOrEmpty(AssignedFoodIds[i]))
            {
                // Find a food item not already assigned in other slots
                var candidate = currentFoodList.Find(f => !IsFoodAssigned(f.id));
                if (candidate != null)
                {
                    AssignedFoodIds[i] = candidate.id;
                    AssignedFoodAmounts[i] = candidate.stock;
                    assignedCount++;
                }
            }
        }

        SaveSlots();
        RefreshDispenserStats();
        UpdateBorders();

        if (assignedCount > 0)
        {
            ShowFeedback($"Filled {assignedCount} empty slot(s) with available food.", new Color(0.12f, 0.85f, 0.40f, 1f));
        }
        else
        {
            ShowFeedback("All slots already filled or no more unique foods available.", new Color(0.95f, 0.65f, 0.20f, 1f));
        }
    }

    private void OnConfirmAutoFeedClicked()
    {
        GameAudioManager.Instance?.PlayClick();

        int totalAssigned = 0;
        for (int i = 0; i < 3; i++)
        {
            if (!string.IsNullOrEmpty(AssignedFoodIds[i])) totalAssigned++;
        }

        if (totalAssigned == 0)
        {
            ShowFeedback("Please assign at least 1 food item to start auto-feed!", new Color(0.95f, 0.35f, 0.35f, 1f));
            return;
        }

        // Transfer food to pantries / dispenser
        var production = ResourceProductionRuntime.Instance;
        HabitatRuntimeManager habManager = null;
        if (production != null && production.HabitatManager != null)
        {
            habManager = production.HabitatManager;
        }
        else
        {
            HabitatRuntimeManager.TryGetActive(out habManager);
        }

        if (production != null && production.CareSettings != null && habManager != null && habManager.Habitats != null)
        {
            foreach (var h in habManager.Habitats)
            {
                if (h == null || h.residents == null) continue;
                foreach (var resident in h.residents)
                {
                    if (resident == null) continue;
                    for (int s = 0; s < 3; s++)
                    {
                        string fId = AssignedFoodIds[s];
                        if (!string.IsNullOrEmpty(fId))
                        {
                            // Check if we can deposit to habitat residents
                            int amt = Mathf.Min(AssignedFoodAmounts[s], 10);
                            if (amt > 0)
                            {
                                production.DepositCreatureFood(h.habitatId, resident.individualId, fId, amt, out _);
                            }
                        }
                    }
                }
            }
        }

        SaveSlots();
        RefreshDispenserStats();
        ShowFeedback($"✓ Auto-Feed Active! Dispenser loaded with {totalAssigned} food item(s).", new Color(0.10f, 0.88f, 0.45f, 1f));
    }

    private void UpdateBorders()
    {
        foreach (var kvp in foodCardBorders)
        {
            if (kvp.Value != null)
            {
                kvp.Value.gameObject.SetActive(IsFoodAssigned(kvp.Key));
            }
        }
    }

    private void RefreshDispenserStats()
    {
        int totalFoodUnits = 0;
        double totalSatiety = 0;

        for (int i = 0; i < 3; i++)
        {
            var view = slotViews[i];
            if (view == null) continue;

            string foodId = AssignedFoodIds[i];
            int amount = AssignedFoodAmounts[i];

            if (string.IsNullOrEmpty(foodId))
            {
                // Empty slot
                view.emptyPlaceholder.SetActive(true);
                view.occupiedContainer.SetActive(false);
                view.removeButton.gameObject.SetActive(false);
                if (view.stepperRow != null) view.stepperRow.SetActive(false);
                view.amountText.text = "";
                view.nameText.text = "Empty Slot";
                view.nameText.color = SubTextCol;
            }
            else
            {
                // Occupied slot
                view.emptyPlaceholder.SetActive(false);
                view.occupiedContainer.SetActive(true);
                view.removeButton.gameObject.SetActive(true);
                if (view.stepperRow != null) view.stepperRow.SetActive(true);

                var item = currentFoodList.Find(f => f.id == foodId);
                Sprite icon = item != null ? item.icon : null;
                string dispName = item != null ? item.name : foodId;

                if (item != null && item.stock > 0)
                {
                    amount = Mathf.Clamp(amount, 1, item.stock);
                    AssignedFoodAmounts[i] = amount;
                }

                view.iconImage.sprite = icon;
                view.iconImage.gameObject.SetActive(icon != null);
                view.amountText.text = $"{amount:N0}";
                view.nameText.text = dispName;
                view.nameText.color = ThemeDark;

                totalFoodUnits += amount;
                totalSatiety += amount * 25.0; // Satiety estimate
            }
        }

        // Consumption calculation
        int activeCreatures = 1;
        HabitatRuntimeManager statsHabManager = null;
        if (ResourceProductionRuntime.Instance != null && ResourceProductionRuntime.Instance.HabitatManager != null)
        {
            statsHabManager = ResourceProductionRuntime.Instance.HabitatManager;
        }
        else
        {
            HabitatRuntimeManager.TryGetActive(out statsHabManager);
        }

        if (statsHabManager != null && statsHabManager.Habitats != null)
        {
            int totalResidents = 0;
            foreach (var h in statsHabManager.Habitats)
            {
                if (h != null && h.residents != null)
                {
                    totalResidents += h.residents.Count;
                }
            }
            if (totalResidents > 0)
            {
                activeCreatures = totalResidents;
            }
        }

        double consumptionPerMin = activeCreatures * 10.0; // 10 units / min per working creature
        if (consumptionRateText != null)
        {
            consumptionRateText.text = $"<color=#64748b>Consumption/Min</color>\n<b><size=17><color=#0284c7>{consumptionPerMin:0}</color></size></b>";
        }

        if (totalEnergyText != null)
        {
            totalEnergyText.text = $"<color=#64748b>Total Food Energy</color>\n<b><size=17><color=#0284c7>{totalSatiety:0}</color></size></b>";
        }

        // Estimated Duration
        if (durationText != null)
        {
            if (totalFoodUnits == 0)
            {
                durationText.text = "<b>ESTIMATED DURATION</b>\n<color=#64748b>Idle · No Food Assigned</color>";
            }
            else
            {
                double totalMinutes = totalSatiety / consumptionPerMin;
                int days = (int)(totalMinutes / 1440);
                int hours = (int)((totalMinutes % 1440) / 60);
                int mins = (int)(totalMinutes % 60);

                string formattedDuration;
                if (days > 0) formattedDuration = $"{days}d {hours}h {mins}m";
                else if (hours > 0) formattedDuration = $"{hours}h {mins}m";
                else formattedDuration = $"{Mathf.Max(1, mins)}m";

                durationText.text = $"<b>ESTIMATED DURATION</b>\n<color=#0284c7>🕒 {formattedDuration}</color>";
            }
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
        yield return new WaitForSeconds(3.5f);
        if (feedbackNotifyText != null)
        {
            feedbackNotifyText.text = "";
        }
    }

    // -------------------------------------------------------------
    // Procedural Sprites
    // -------------------------------------------------------------
    private static Sprite GetPanelSprite()
    {
        if (panelSprite == null)
            panelSprite = CreateTechBox(64, 64, 14, new Color(0.965f, 0.985f, 1f, 0.98f), new Color(0f, 0.70f, 0.88f, 1f));
        return panelSprite;
    }

    private static Sprite GetCardBoxSprite()
    {
        if (cardBoxSprite == null)
            cardBoxSprite = CreateTechBox(64, 64, 10, Color.white, new Color(0.82f, 0.88f, 0.94f, 1f));
        return cardBoxSprite;
    }

    private static Sprite GetPillSprite()
    {
        if (pillSprite == null)
            pillSprite = CreateCapsule(64, 32, 16, new Color(0.92f, 0.96f, 1f, 1f), new Color(0.70f, 0.85f, 0.96f, 1f));
        return pillSprite;
    }

    private static Sprite GetButtonPrimarySprite()
    {
        if (buttonPrimarySprite == null)
            buttonPrimarySprite = CreateTechBox(64, 36, 8, new Color(0f, 0.68f, 0.88f, 1f), new Color(0.35f, 0.85f, 1f, 1f));
        return buttonPrimarySprite;
    }

    private static Sprite GetButtonSecondarySprite()
    {
        if (buttonSecondarySprite == null)
            buttonSecondarySprite = CreateTechBox(64, 36, 8, Color.white, new Color(0.70f, 0.82f, 0.90f, 1f));
        return buttonSecondarySprite;
    }

    private static Sprite GetCirclePlateSprite()
    {
        if (circlePlateSprite == null)
        {
            int w = 80, h = 80;
            float cx = w * 0.5f, cy = h * 0.5f, r = 38f;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[w * h];
            Color rim = new Color(0.15f, 0.65f, 0.92f, 1f);
            Color innerRim = new Color(0.86f, 0.93f, 0.99f, 1f);
            Color fill = new Color(0.95f, 0.98f, 1f, 1f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > r) px[y * w + x] = Color.clear;
                    else if (dist >= r - 3.5f) px[y * w + x] = rim;
                    else if (dist >= r - 7.5f) px[y * w + x] = innerRim;
                    else px[y * w + x] = fill;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            circlePlateSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
        return circlePlateSprite;
    }

    private static Sprite GetClockIconSprite()
    {
        if (clockIconSprite == null)
        {
            int w = 24, h = 24;
            float cx = 12f, cy = 12f, r = 10f;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[w * h];
            Color cyan = ThemeCyan;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > r) px[y * w + x] = Color.clear;
                    else if (dist >= r - 2f) px[y * w + x] = cyan;
                    else
                    {
                        bool isHour = (Mathf.Abs(dx) <= 1f && dy >= 0 && dy <= 6f);
                        bool isMin = (Mathf.Abs(dy) <= 1f && dx >= 0 && dx <= 6f);
                        bool isCenter = dist <= 2.2f;
                        px[y * w + x] = (isHour || isMin || isCenter) ? cyan : Color.clear;
                    }
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            clockIconSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
        return clockIconSprite;
    }

    private static Sprite CreateTechBox(int w, int h, int r, Color fillColor, Color borderColor)
    {
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

                if (dist > r) px[y * w + x] = Color.clear;
                else if (dist >= r - 1.5f || x == 0 || x == w - 1 || y == 0 || y == h - 1) px[y * w + x] = borderColor;
                else px[y * w + x] = fillColor;
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        Vector4 border = new Vector4(r + 2, r + 2, r + 2, r + 2);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    private static Sprite CreateCapsule(int w, int h, int r, Color fillColor, Color borderColor)
    {
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

                if (dist > r) px[y * w + x] = Color.clear;
                else if (dist >= r - 1.5f || x == 0 || x == w - 1 || y == 0 || y == h - 1) px[y * w + x] = borderColor;
                else px[y * w + x] = fillColor;
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        Vector4 border = new Vector4(r, 0, r, 0);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }
}
