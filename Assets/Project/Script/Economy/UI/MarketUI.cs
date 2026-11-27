using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LumiWorld.Acs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Redesigned Player Market window implementing P-17 (Buy), P-19 (Sell), and P-20 (My listings)
/// using LumiWorldUITheme warm-stone chrome and monochrome glyphs.
/// </summary>
[DisallowMultipleComponent]
public class MarketUI : MonoBehaviour
{
    public static MarketUI Instance { get; private set; }

    public enum MarketTab
    {
        Buy = 0,
        Sell = 1,
        MyListings = 2
    }

    private enum BrowseCategory
    {
        All = 0,
        Resources = 1,
        CreatureCards = 2
    }

    private enum SellCategory
    {
        Resources = 0,
        CreatureCards = 1
    }

    private sealed class SellableEntry
    {
        public string MarketId;
        public string DisplayName;
        public string KindLabel;
        public bool IsCard;
        public int OwnedCount;
        public int MaxPerListing;
        public int BaseValue;
        public Sprite Icon;
    }

    private sealed class MyRowBinding
    {
        public MarketListingDto Lot;
        public GameObject SelectionBorder;
        public Image CheckboxBg;
        public TextMeshProUGUI CheckboxMark;
    }

    private const int BrowsePageSize = 20;

    [Header("Legacy Scene Placement (Hidden in Phase 3+ HUD)")]
    [SerializeField] private Vector2 buttonCorner = new Vector2(-180f, -20f);
    [SerializeField] private Vector2 buttonSize = new Vector2(140f, 44f);

    private Canvas _canvas;
    private RectTransform _modalRoot;
    private GameObject _backdropGo;
    private RectTransform _windowRect;

    // Top nav tabs
    private readonly Image[] _topTabBg = new Image[3];
    private readonly Image[] _topTabIcon = new Image[3];
    private readonly TextMeshProUGUI[] _topTabLabel = new TextMeshProUGUI[3];
    private readonly Shadow[] _topTabShadow = new Shadow[3];

    // Tab views
    private GameObject _buyViewGo;
    private GameObject _sellViewGo;
    private GameObject _myViewGo;

    // Buy (P-17) UI
    private readonly Image[] _buyCatBg = new Image[3];
    private readonly Image[] _buyCatIcon = new Image[3];
    private readonly TextMeshProUGUI[] _buyCatLabel = new TextMeshProUGUI[3];
    private readonly Shadow[] _buyCatShadow = new Shadow[3];
    private TextMeshProUGUI _sortBtnLabel;
    private RectTransform _buyListContent;
    private ScrollRect _buyScroll;
    private TextMeshProUGUI _buyEmptyLabel;
    private TextMeshProUGUI _buyPageLabel;
    private Button _buyPrevPageBtn;
    private Button _buyNextPageBtn;
    private GameObject _buyDetailFilledGo;
    private TextMeshProUGUI _buyDetailEmptyLabel;
    private Image _buyDetailArtwork;
    private TextMeshProUGUI _buyDetailTitle;
    private TextMeshProUGUI _buyDetailKind;
    private TextMeshProUGUI _buyDetailQty;
    private TextMeshProUGUI _buyDetailSeller;
    private TextMeshProUGUI _buyDetailTotalPrice;
    private TextMeshProUGUI _buyDetailExpires;
    private TextMeshProUGUI _buyDetailStatus;
    private Button _buyLotBtn;
    private Image _buyLotBtnBg;
    private Image _buyLotBtnIcon;
    private TextMeshProUGUI _buyLotBtnLabel;

    // Sell (P-19) UI
    private readonly Image[] _sellCatBg = new Image[2];
    private readonly Image[] _sellCatIcon = new Image[2];
    private readonly TextMeshProUGUI[] _sellCatLabel = new TextMeshProUGUI[2];
    private readonly Shadow[] _sellCatShadow = new Shadow[2];
    private TextMeshProUGUI _sellQuotaLabel;
    private RectTransform _sellListContent;
    private ScrollRect _sellScroll;
    private TextMeshProUGUI _sellEmptyLabel;
    private GameObject _sellDetailFilledGo;
    private TextMeshProUGUI _sellDetailEmptyLabel;
    private Image _sellDetailArtwork;
    private TextMeshProUGUI _sellDetailTitle;
    private TextMeshProUGUI _sellDetailOwned;
    private Button _sellQtyMinusBtn;
    private Button _sellQtyPlusBtn;
    private Button _sellQtyMaxBtn;
    private TextMeshProUGUI _sellQtyValueText;
    private TMP_InputField _sellPriceInput;
    private TextMeshProUGUI _sellRangeHintText;
    private TextMeshProUGUI _sellFeeLabelText;
    private TextMeshProUGUI _sellFeeValueText;
    private TextMeshProUGUI _sellReceiveValueText;
    private TextMeshProUGUI _sellBuyerTotalValueText;
    private TextMeshProUGUI _sellDetailStatus;
    private Button _sellCreateBtn;
    private Image _sellCreateBtnBg;
    private Image _sellCreateBtnIcon;
    private TextMeshProUGUI _sellCreateBtnLabel;

    // My listings (P-20) UI
    private TextMeshProUGUI _myCountLabel;
    private Button _mySelectAllBtn;
    private TextMeshProUGUI _mySelectAllLabel;
    private Button _myCancelSelectedBtn;
    private Image _myCancelSelectedBg;
    private Image _myCancelSelectedIcon;
    private TextMeshProUGUI _myCancelSelectedLabel;
    private RectTransform _myListContent;
    private ScrollRect _myScroll;
    private TextMeshProUGUI _myEmptyLabel;
    private GameObject _myDetailFilledGo;
    private TextMeshProUGUI _myDetailEmptyLabel;
    private Image _myDetailArtwork;
    private TextMeshProUGUI _myDetailTitle;
    private TextMeshProUGUI _myDetailKind;
    private TextMeshProUGUI _myDetailQty;
    private TextMeshProUGUI _myDetailTotalPrice;
    private TextMeshProUGUI _myDetailExpires;
    private TextMeshProUGUI _myDetailStatusText;
    private Button _myCancelSingleBtn;
    private Image _myCancelSingleBg;
    private Image _myCancelSingleIcon;
    private TextMeshProUGUI _myCancelSingleLabel;

    // Runtime State
    private MarketTab _activeTab = MarketTab.Buy;
    private BrowseCategory _browseCategory = BrowseCategory.All;
    private bool _newestFirst = true;
    private int _browsePage = 1;
    private bool _browseHasMore;
    private readonly List<MarketListingDto> _browseListings = new List<MarketListingDto>();
    private readonly Dictionary<string, GameObject> _buySelectionBorders = new Dictionary<string, GameObject>();
    private string _selectedBuyId;
    private bool _isLoadingBrowse;
    private bool _isBuying;
    private string _buyFeedback = string.Empty;
    private bool _buyFeedbackError;

    private SellCategory _sellCategory = SellCategory.Resources;
    private readonly List<SellableEntry> _sellEntries = new List<SellableEntry>();
    private readonly Dictionary<string, GameObject> _sellSelectionBorders = new Dictionary<string, GameObject>();
    private string _selectedSellMarketId;
    private int _sellQty = 1;
    private int _sellTotalPrice;
    private bool _suppressPriceInputCallback;
    private bool _isCreatingListing;
    private string _sellFeedback = string.Empty;
    private bool _sellFeedbackError;

    private readonly List<MarketListingDto> _myListings = new List<MarketListingDto>();
    private readonly HashSet<string> _selectedMyListingIds = new HashSet<string>();
    private readonly List<MyRowBinding> _myRowBindings = new List<MyRowBinding>();
    private string _selectedMyFocusId;
    private bool _isLoadingMine;
    private bool _isCancelling;
    private string _myFeedback = string.Empty;
    private bool _myFeedbackError;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        HideLegacyMarketButton();
        BuildModalIfNeeded();
        if (_backdropGo != null)
            _backdropGo.SetActive(false);
    }

    private void OnEnable()
    {
        if (CurrencyWallet.HasInstance)
            CurrencyWallet.Instance.OnBalanceChanged += HandleBalanceChanged;
        if (ResourceInventory.HasInstance)
            ResourceInventory.Instance.OnInventoryChanged += HandleInventoryChanged;
    }

    private void OnDisable()
    {
        if (CurrencyWallet.HasInstance)
            CurrencyWallet.Instance.OnBalanceChanged -= HandleBalanceChanged;
        if (ResourceInventory.HasInstance)
            ResourceInventory.Instance.OnInventoryChanged -= HandleInventoryChanged;
    }

    private void OnDestroy()
    {
        EconomyHUD.UnregisterWindow(_backdropGo);
        if (Instance == this)
            Instance = null;
    }

    private void HideLegacyMarketButton()
    {
        Transform legacy = transform.Find("MarketButton");
        if (legacy != null)
        {
            legacy.gameObject.SetActive(false);
            return;
        }

        GameObject named = GameObject.Find("MarketButton");
        if (named != null)
            named.SetActive(false);
    }

    public void Open()
    {
        BuildModalIfNeeded();
        if (_backdropGo != null)
        {
            _backdropGo.SetActive(true);
            EconomyHUD.RegisterWindow(_backdropGo, Close);
            EconomyHUD.BringWindowToFront(_backdropGo);
        }

        IsOpen = true;
        ApplyScale();
        SwitchTab(_activeTab);
    }

    public void Close()
    {
        if (IsOpen) EconomyHUD.MarkWindowClosed();
        IsOpen = false;
        if (_backdropGo != null)
            _backdropGo.SetActive(false);
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    private void HandleBalanceChanged(int _, int __, CoinReason ___)
    {
        if (!IsOpen)
            return;

        if (_activeTab == MarketTab.Buy)
            RefreshBuyDetail();
    }

    private void HandleInventoryChanged(string _, int __, int ___)
    {
        if (!IsOpen)
            return;

        if (_activeTab == MarketTab.Sell)
            RefreshSellCatalog(false);
    }

    private void ApplyScale()
    {
        if (_canvas == null || _modalRoot == null || _windowRect == null)
            return;

        RectTransform canvasRt = _canvas.GetComponent<RectTransform>();
        float fit = LumiWorldUITheme.ComputeFitScale(canvasRt, out _);
        _windowRect.localScale = new Vector3(fit, fit, 1f);
    }

    private void BuildModalIfNeeded()
    {
        if (_backdropGo != null)
            return;

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null)
        {
            GameObject canvasGo = new GameObject("MarketUICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 120;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        }

        _backdropGo = new GameObject("MarketModalBackdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        _backdropGo.transform.SetParent(_canvas.transform, false);
        _backdropGo.transform.SetAsLastSibling();
        RectTransform bgRt = _backdropGo.GetComponent<RectTransform>();
        LumiWorldUITheme.Stretch(bgRt);
        _backdropGo.GetComponent<Image>().color = LumiWorldUITheme.Backdrop;
        Button bgBtn = _backdropGo.GetComponent<Button>();
        bgBtn.transition = Selectable.Transition.None;
        bgBtn.onClick.AddListener(() => { });

        _modalRoot = bgRt;

        Image winImg = LumiWorldUITheme.CreateRoundedSurface(
            _modalRoot, "MarketWindow", LumiWorldUITheme.Window, LumiWorldUITheme.Edge,
            radius: 22, borderThickness: 1.5f, shadowDepth: 6f, shadowAlpha: 0.22f, raycastTarget: true);
        _windowRect = winImg.rectTransform;
        _windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        _windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        _windowRect.pivot = new Vector2(0.5f, 0.5f);
        _windowRect.anchoredPosition = Vector2.zero;
        _windowRect.sizeDelta = new Vector2(980f, 610f);

        BuildTopBar();
        BuildBuyView();
        BuildSellView();
        BuildMyListingsView();
    }

    private void BuildTopBar()
    {
        float tabX = 24f;
        float tabY = 16f;
        string[] tabLabels = { "Buy", "Sell", "My listings" };
        string[] tabGlyphs =
        {
            "G-25",
            "G-26",
            "G-04"
        };
        float[] tabWidths = { 126f, 126f, 186f };

        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            Button btn = LumiWorldUITheme.CreateSubTabButton(
                _windowRect, "TopTab_" + tabLabels[i], tabLabels[i], tabGlyphs[i], 15f,
                () => SwitchTab((MarketTab)idx),
                out _topTabBg[i], out _topTabIcon[i], out _topTabLabel[i], out _topTabShadow[i]);
            LumiWorldUITheme.PlaceTopLeft(btn.GetComponent<RectTransform>(), tabX, tabY, tabWidths[i], 42f);
            tabX += tabWidths[i] + 10f;
        }

        Button closeBtn = LumiWorldUITheme.CreateCloseButton(_windowRect, 22f, 20f, Close, 34f);
        closeBtn.transform.SetAsLastSibling();
    }

    private void BuildBuyView()
    {
        _buyViewGo = new GameObject("BuyView", typeof(RectTransform));
        _buyViewGo.transform.SetParent(_windowRect, false);
        LumiWorldUITheme.Stretch(_buyViewGo.GetComponent<RectTransform>());

        float subY = 72f;
        string allGlyph = LumiWorldUITheme.Glyph("G-01") != null ? "G-01" : null;
        string[] catLabels = { "All", "Resources", "Creature cards" };
        string[] catGlyphs = { allGlyph, "G-19", "G-20" };
        float[] catWidths = { 94f, 134f, 168f };
        float subX = 24f;

        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            Button btn = LumiWorldUITheme.CreateSubTabButton(
                _buyViewGo.transform, "BuyCat_" + catLabels[i], catLabels[i], catGlyphs[i], 14f,
                () => SetBuyCategory((BrowseCategory)idx),
                out _buyCatBg[i], out _buyCatIcon[i], out _buyCatLabel[i], out _buyCatShadow[i]);
            LumiWorldUITheme.PlaceTopLeft(btn.GetComponent<RectTransform>(), subX, subY, catWidths[i], 36f);
            subX += catWidths[i] + 8f;
        }

        Button refreshBtn = LumiWorldUITheme.CreatePillButton(
            _buyViewGo.transform, "BuyRefreshBtn", "Refresh", null, false, 13f,
            () => FetchBrowseListings(),
            out _, out _, out _, radius: 12);
        LumiWorldUITheme.PlaceTopRight(refreshBtn.GetComponent<RectTransform>(), 24f, subY, 92f, 36f);

        Button sortBtn = LumiWorldUITheme.CreatePillButton(
            _buyViewGo.transform, "BuySortBtn", "Sort: Newest", null, false, 13f,
            ToggleBrowseSort,
            out _, out _, out _sortBtnLabel, radius: 12);
        LumiWorldUITheme.PlaceTopRight(sortBtn.GetComponent<RectTransform>(), 124f, subY, 152f, 36f);

        Image wellImg = LumiWorldUITheme.CreateRoundedSurface(
            _buyViewGo.transform, "BuyListWell", LumiWorldUITheme.Well, LumiWorldUITheme.Edge,
            radius: 18, borderThickness: 1.5f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: true);
        RectTransform wellRt = wellImg.rectTransform;
        LumiWorldUITheme.PlaceTopLeft(wellRt, 24f, 120f, 572f, 466f);

        CreateScrollList(wellRt, 0f, 44f, out _buyScroll, out _buyListContent);

        _buyEmptyLabel = LumiWorldUITheme.CreateLabel(
            wellRt, "BuyEmpty", "No listings found.", 15f, LumiWorldUITheme.Muted,
            headingFont: false, align: TextAlignmentOptions.Center, wrap: true);
        LumiWorldUITheme.Stretch(_buyEmptyLabel.rectTransform, 24f);
        _buyEmptyLabel.gameObject.SetActive(false);

        GameObject pagerGo = new GameObject("PagerBar", typeof(RectTransform));
        pagerGo.transform.SetParent(wellRt, false);
        RectTransform pagerRt = pagerGo.GetComponent<RectTransform>();
        pagerRt.anchorMin = new Vector2(0f, 0f);
        pagerRt.anchorMax = new Vector2(1f, 0f);
        pagerRt.pivot = new Vector2(0.5f, 0f);
        pagerRt.offsetMin = new Vector2(12f, 8f);
        pagerRt.offsetMax = new Vector2(-12f, 42f);

        _buyPrevPageBtn = LumiWorldUITheme.CreatePillButton(
            pagerRt, "PrevPageBtn", "Prev", null, false, 13f,
            () => ChangeBrowsePage(-1), out _, out _, out _, radius: 10);
        LumiWorldUITheme.PlaceTopLeft(_buyPrevPageBtn.GetComponent<RectTransform>(), 0f, 2f, 82f, 30f);

        _buyPageLabel = LumiWorldUITheme.CreateLabel(
            pagerRt, "PageLabel", "Page 1", 13f, LumiWorldUITheme.Muted,
            headingFont: false, align: TextAlignmentOptions.Center);
        LumiWorldUITheme.Stretch(_buyPageLabel.rectTransform);

        _buyNextPageBtn = LumiWorldUITheme.CreatePillButton(
            pagerRt, "NextPageBtn", "Next", null, false, 13f,
            () => ChangeBrowsePage(1), out _, out _, out _, radius: 10);
        LumiWorldUITheme.PlaceTopRight(_buyNextPageBtn.GetComponent<RectTransform>(), 0f, 2f, 82f, 30f);

        Image detailImg = LumiWorldUITheme.CreateRoundedSurface(
            _buyViewGo.transform, "BuyDetailPane", LumiWorldUITheme.Surface, LumiWorldUITheme.Edge,
            radius: 18, borderThickness: 1.5f, shadowDepth: 2f, shadowAlpha: 0.08f, raycastTarget: true);
        RectTransform detailRt = detailImg.rectTransform;
        LumiWorldUITheme.PlaceTopRight(detailRt, 24f, 120f, 346f, 466f);

        _buyDetailEmptyLabel = LumiWorldUITheme.CreateLabel(
            detailRt, "BuyDetailEmpty", "Select a listing to view details.", 15f, LumiWorldUITheme.Muted,
            headingFont: false, align: TextAlignmentOptions.Center, wrap: true);
        LumiWorldUITheme.Stretch(_buyDetailEmptyLabel.rectTransform, 24f);

        _buyDetailFilledGo = new GameObject("BuyDetailFilled", typeof(RectTransform));
        _buyDetailFilledGo.transform.SetParent(detailRt, false);
        LumiWorldUITheme.Stretch(_buyDetailFilledGo.GetComponent<RectTransform>());

        Image artFrame = LumiWorldUITheme.CreateRoundedSurface(
            _buyDetailFilledGo.transform, "BuyArtBox", LumiWorldUITheme.Inset, LumiWorldUITheme.Edge,
            radius: 16, borderThickness: 1.2f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: false);
        LumiWorldUITheme.PlaceTopLeft(artFrame.rectTransform, 113f, 20f, 120f, 120f);

        GameObject artImgGo = new GameObject("Artwork", typeof(RectTransform), typeof(Image));
        artImgGo.transform.SetParent(artFrame.transform, false);
        LumiWorldUITheme.Stretch(artImgGo.GetComponent<RectTransform>(), 14f);
        _buyDetailArtwork = artImgGo.GetComponent<Image>();
        _buyDetailArtwork.preserveAspect = true;

        _buyDetailTitle = LumiWorldUITheme.CreateLabel(
            _buyDetailFilledGo.transform, "Title", string.Empty, 20f, LumiWorldUITheme.Text,
            headingFont: true, align: TextAlignmentOptions.Center, wrap: false);
        LumiWorldUITheme.PlaceTopLeft(_buyDetailTitle.rectTransform, 18f, 150f, 310f, 34f);

        _buyDetailKind = LumiWorldUITheme.CreateLabel(
            _buyDetailFilledGo.transform, "Kind", string.Empty, 13f, LumiWorldUITheme.Muted,
            headingFont: false, align: TextAlignmentOptions.Center);
        LumiWorldUITheme.PlaceTopLeft(_buyDetailKind.rectTransform, 18f, 184f, 310f, 22f);

        CreateDivider(_buyDetailFilledGo.transform, 18f, 214f, 310f);

        _buyDetailQty = CreateDetailRow(_buyDetailFilledGo.transform, "Quantity", 228f);
        _buyDetailSeller = CreateDetailRow(_buyDetailFilledGo.transform, "Seller", 260f);
        _buyDetailTotalPrice = CreateDetailRow(_buyDetailFilledGo.transform, "Total price", 292f);
        _buyDetailExpires = CreateDetailRow(_buyDetailFilledGo.transform, "Expires in", 324f);

        _buyDetailStatus = LumiWorldUITheme.CreateLabel(
            _buyDetailFilledGo.transform, "BuyStatus", string.Empty, 13f, LumiWorldUITheme.Muted,
            headingFont: false, align: TextAlignmentOptions.Center, wrap: true);
        LumiWorldUITheme.PlaceTopLeft(_buyDetailStatus.rectTransform, 18f, 362f, 310f, 34f);

        _buyLotBtn = LumiWorldUITheme.CreatePillButton(
            _buyDetailFilledGo.transform, "BuyLotBtn", "Buy lot", "G-25", true, 16f,
            ExecuteBuySelectedLot,
            out _buyLotBtnBg, out _buyLotBtnIcon, out _buyLotBtnLabel, radius: 14);
        LumiWorldUITheme.PlaceTopLeft(_buyLotBtn.GetComponent<RectTransform>(), 20f, 402f, 306f, 46f);
    }

    private void BuildSellView()
    {
        _sellViewGo = new GameObject("SellView", typeof(RectTransform));
        _sellViewGo.transform.SetParent(_windowRect, false);
        LumiWorldUITheme.Stretch(_sellViewGo.GetComponent<RectTransform>());

        float subY = 72f;
        string[] catLabels = { "Resources", "Creature cards" };
        string[] catGlyphs = { "G-19", "G-20" };
        float[] catWidths = { 136f, 168f };
        float subX = 24f;

        for (int i = 0; i < 2; i++)
        {
            int idx = i;
            Button btn = LumiWorldUITheme.CreateSubTabButton(
                _sellViewGo.transform, "SellCat_" + catLabels[i], catLabels[i], catGlyphs[i], 14f,
                () => SetSellCategory((SellCategory)idx),
                out _sellCatBg[i], out _sellCatIcon[i], out _sellCatLabel[i], out _sellCatShadow[i]);
            LumiWorldUITheme.PlaceTopLeft(btn.GetComponent<RectTransform>(), subX, subY, catWidths[i], 36f);
            subX += catWidths[i] + 8f;
        }

        _sellQuotaLabel = LumiWorldUITheme.CreateLabel(
            _sellViewGo.transform, "SellQuota", string.Empty, 13f, LumiWorldUITheme.Muted,
            headingFont: false, align: TextAlignmentOptions.MidlineRight);
        LumiWorldUITheme.PlaceTopRight(_sellQuotaLabel.rectTransform, 24f, subY, 340f, 36f);

        Image wellImg = LumiWorldUITheme.CreateRoundedSurface(
            _sellViewGo.transform, "SellListWell", LumiWorldUITheme.Well, LumiWorldUITheme.Edge,
            radius: 18, borderThickness: 1.5f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: true);
        RectTransform wellRt = wellImg.rectTransform;
        LumiWorldUITheme.PlaceTopLeft(wellRt, 24f, 120f, 572f, 466f);

        CreateScrollList(wellRt, 0f, 0f, out _sellScroll, out _sellListContent);

        _sellEmptyLabel = LumiWorldUITheme.CreateLabel(
            wellRt, "SellEmpty", "No sellable items in this category.", 15f, LumiWorldUITheme.Muted,
            headingFont: false, align: TextAlignmentOptions.Center, wrap: true);
        LumiWorldUITheme.Stretch(_sellEmptyLabel.rectTransform, 24f);
        _sellEmptyLabel.gameObject.SetActive(false);

        Image detailImg = LumiWorldUITheme.CreateRoundedSurface(
            _sellViewGo.transform, "SellInspector", LumiWorldUITheme.Surface, LumiWorldUITheme.Edge,
            radius: 18, borderThickness: 1.5f, shadowDepth: 2f, shadowAlpha: 0.08f, raycastTarget: true);
        RectTransform detailRt = detailImg.rectTransform;
        LumiWorldUITheme.PlaceTopRight(detailRt, 24f, 120f, 346f, 466f);

        _sellDetailEmptyLabel = LumiWorldUITheme.CreateLabel(
            detailRt, "SellDetailEmpty", "Select an item to list on the market.", 15f, LumiWorldUITheme.Muted,
            headingFont: false, align: TextAlignmentOptions.Center, wrap: true);
        LumiWorldUITheme.Stretch(_sellDetailEmptyLabel.rectTransform, 24f);

        _sellDetailFilledGo = new GameObject("SellDetailFilled", typeof(RectTransform));
        _sellDetailFilledGo.transform.SetParent(detailRt, false);
        LumiWorldUITheme.Stretch(_sellDetailFilledGo.GetComponent<RectTransform>());

        Image artFrame = LumiWorldUITheme.CreateRoundedSurface(
            _sellDetailFilledGo.transform, "SellArtBox", LumiWorldUITheme.Inset, LumiWorldUITheme.Edge,
            radius: 14, borderThickness: 1.2f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: false);
        LumiWorldUITheme.PlaceTopLeft(artFrame.rectTransform, 20f, 18f, 68f, 68f);

        GameObject artImgGo = new GameObject("Artwork", typeof(RectTransform), typeof(Image));
        artImgGo.transform.SetParent(artFrame.transform, false);
        LumiWorldUITheme.Stretch(artImgGo.GetComponent<RectTransform>(), 8f);
        _sellDetailArtwork = artImgGo.GetComponent<Image>();
        _sellDetailArtwork.preserveAspect = true;

        _sellDetailTitle = LumiWorldUITheme.CreateLabel(
            _sellDetailFilledGo.transform, "SellTitle", string.Empty, 18f, LumiWorldUITheme.Text,
            headingFont: true, align: TextAlignmentOptions.MidlineLeft, wrap: false);
        LumiWorldUITheme.PlaceTopLeft(_sellDetailTitle.rectTransform, 100f, 20f, 226f, 30f);

        _sellDetailOwned = LumiWorldUITheme.CreateLabel(
            _sellDetailFilledGo.transform, "SellOwned", string.Empty, 13f, LumiWorldUITheme.Muted,
            headingFont: false, align: TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(_sellDetailOwned.rectTransform, 100f, 54f, 226f, 24f);

        CreateDivider(_sellDetailFilledGo.transform, 20f, 98f, 306f);

        TextMeshProUGUI qtyLbl = LumiWorldUITheme.CreateLabel(
            _sellDetailFilledGo.transform, "QtyLbl", "Quantity", 14f, LumiWorldUITheme.Text, headingFont: false);
        LumiWorldUITheme.PlaceTopLeft(qtyLbl.rectTransform, 20f, 114f, 100f, 34f);

        _sellQtyMinusBtn = LumiWorldUITheme.CreatePillButton(
            _sellDetailFilledGo.transform, "QtyMinus", "-", null, false, 16f,
            () => AdjustSellQuantity(-1), out _, out _, out _, radius: 10);
        LumiWorldUITheme.PlaceTopLeft(_sellQtyMinusBtn.GetComponent<RectTransform>(), 146f, 114f, 34f, 34f);

        _sellQtyValueText = LumiWorldUITheme.CreateLabel(
            _sellDetailFilledGo.transform, "QtyVal", "1", 15f, LumiWorldUITheme.Text,
            headingFont: true, align: TextAlignmentOptions.Center);
        LumiWorldUITheme.PlaceTopLeft(_sellQtyValueText.rectTransform, 182f, 114f, 44f, 34f);

        _sellQtyPlusBtn = LumiWorldUITheme.CreatePillButton(
            _sellDetailFilledGo.transform, "QtyPlus", "+", null, false, 16f,
            () => AdjustSellQuantity(1), out _, out _, out _, radius: 10);
        LumiWorldUITheme.PlaceTopLeft(_sellQtyPlusBtn.GetComponent<RectTransform>(), 228f, 114f, 34f, 34f);

        _sellQtyMaxBtn = LumiWorldUITheme.CreatePillButton(
            _sellDetailFilledGo.transform, "QtyMax", "Max", null, false, 13f,
            SetSellQuantityMax, out _, out _, out _, radius: 10);
        LumiWorldUITheme.PlaceTopLeft(_sellQtyMaxBtn.GetComponent<RectTransform>(), 268f, 114f, 58f, 34f);

        TextMeshProUGUI priceLbl = LumiWorldUITheme.CreateLabel(
            _sellDetailFilledGo.transform, "PriceLbl", "Total price", 14f, LumiWorldUITheme.Text, headingFont: false);
        LumiWorldUITheme.PlaceTopLeft(priceLbl.rectTransform, 20f, 162f, 110f, 36f);

        _sellPriceInput = CreatePriceInputField(_sellDetailFilledGo.transform, 146f, 162f, 180f, 36f);
        _sellPriceInput.onValueChanged.AddListener(OnSellPriceInputChanged);
        _sellPriceInput.onEndEdit.AddListener(OnSellPriceEndEdit);

        _sellRangeHintText = LumiWorldUITheme.CreateLabel(
            _sellDetailFilledGo.transform, "RangeHint", string.Empty, 12f, LumiWorldUITheme.Muted,
            headingFont: false, align: TextAlignmentOptions.MidlineRight);
        LumiWorldUITheme.PlaceTopLeft(_sellRangeHintText.rectTransform, 20f, 202f, 306f, 20f);

        Image breakImg = LumiWorldUITheme.CreateRoundedSurface(
            _sellDetailFilledGo.transform, "BreakdownBox", LumiWorldUITheme.Inset, LumiWorldUITheme.Edge,
            radius: 14, borderThickness: 1f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: false);
        RectTransform breakRt = breakImg.rectTransform;
        LumiWorldUITheme.PlaceTopLeft(breakRt, 20f, 234f, 306f, 114f);

        _sellFeeLabelText = LumiWorldUITheme.CreateLabel(
            breakRt, "FeeLbl", "Listing fee", 13f, LumiWorldUITheme.Muted, headingFont: false);
        LumiWorldUITheme.PlaceTopLeft(_sellFeeLabelText.rectTransform, 14f, 12f, 160f, 24f);
        _sellFeeValueText = LumiWorldUITheme.CreateLabel(
            breakRt, "FeeVal", "0 coins", 13f, LumiWorldUITheme.Text,
            headingFont: false, align: TextAlignmentOptions.MidlineRight);
        LumiWorldUITheme.PlaceTopLeft(_sellFeeValueText.rectTransform, 174f, 12f, 118f, 24f);

        TextMeshProUGUI receiveLbl = LumiWorldUITheme.CreateLabel(
            breakRt, "RecvLbl", "You receive", 14f, LumiWorldUITheme.Text, headingFont: false);
        LumiWorldUITheme.PlaceTopLeft(receiveLbl.rectTransform, 14f, 44f, 140f, 24f);
        _sellReceiveValueText = LumiWorldUITheme.CreateLabel(
            breakRt, "RecvVal", "0 coins", 14f, LumiWorldUITheme.Success,
            headingFont: true, align: TextAlignmentOptions.MidlineRight);
        LumiWorldUITheme.PlaceTopLeft(_sellReceiveValueText.rectTransform, 154f, 44f, 138f, 26f);

        TextMeshProUGUI buyerLbl = LumiWorldUITheme.CreateLabel(
            breakRt, "BuyerLbl", "Buyer total", 13f, LumiWorldUITheme.Muted, headingFont: false);
        LumiWorldUITheme.PlaceTopLeft(buyerLbl.rectTransform, 14f, 76f, 140f, 24f);
        _sellBuyerTotalValueText = LumiWorldUITheme.CreateLabel(
            breakRt, "BuyerVal", "0 coins", 14f, LumiWorldUITheme.Text,
            headingFont: false, align: TextAlignmentOptions.MidlineRight);
        LumiWorldUITheme.PlaceTopLeft(_sellBuyerTotalValueText.rectTransform, 154f, 76f, 138f, 24f);

        _sellDetailStatus = LumiWorldUITheme.CreateLabel(
            _sellDetailFilledGo.transform, "SellStatus", string.Empty, 13f, LumiWorldUITheme.Muted,
            headingFont: false, align: TextAlignmentOptions.Center, wrap: true);
        LumiWorldUITheme.PlaceTopLeft(_sellDetailStatus.rectTransform, 20f, 358f, 306f, 36f);

        _sellCreateBtn = LumiWorldUITheme.CreatePillButton(
            _sellDetailFilledGo.transform, "CreateListingBtn", "Create listing", "G-26", true, 15f,
            ExecuteCreateListing,
            out _sellCreateBtnBg, out _sellCreateBtnIcon, out _sellCreateBtnLabel, radius: 14);
        LumiWorldUITheme.PlaceTopLeft(_sellCreateBtn.GetComponent<RectTransform>(), 20f, 402f, 306f, 46f);
    }

    private void BuildMyListingsView()
    {
        _myViewGo = new GameObject("MyListingsView", typeof(RectTransform));
        _myViewGo.transform.SetParent(_windowRect, false);
        LumiWorldUITheme.Stretch(_myViewGo.GetComponent<RectTransform>());

        float subY = 72f;
        _myCountLabel = LumiWorldUITheme.CreateLabel(
            _myViewGo.transform, "MyCount", "Active listings (0 / 0)", 14f, LumiWorldUITheme.Text, headingFont: false);
        LumiWorldUITheme.PlaceTopLeft(_myCountLabel.rectTransform, 24f, subY, 320f, 36f);

        _myCancelSelectedBtn = LumiWorldUITheme.CreatePillButton(
            _myViewGo.transform, "CancelSelectedBtn", "Cancel selected (0)", "G-27", true, 13f,
            ExecuteCancelSelectedBatch,
            out _myCancelSelectedBg, out _myCancelSelectedIcon, out _myCancelSelectedLabel, radius: 12);
        LumiWorldUITheme.PlaceTopRight(_myCancelSelectedBtn.GetComponent<RectTransform>(), 24f, subY, 194f, 36f);

        _mySelectAllBtn = LumiWorldUITheme.CreatePillButton(
            _myViewGo.transform, "SelectAllBtn", "Select all", null, false, 13f,
            ToggleSelectAllMyListings,
            out _, out _, out _mySelectAllLabel, radius: 12);
        LumiWorldUITheme.PlaceTopRight(_mySelectAllBtn.GetComponent<RectTransform>(), 226f, subY, 120f, 36f);

        Image wellImg = LumiWorldUITheme.CreateRoundedSurface(
            _myViewGo.transform, "MyListWell", LumiWorldUITheme.Well, LumiWorldUITheme.Edge,
            radius: 18, borderThickness: 1.5f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: true);
        RectTransform wellRt = wellImg.rectTransform;
        LumiWorldUITheme.PlaceTopLeft(wellRt, 24f, 120f, 572f, 466f);

        CreateScrollList(wellRt, 0f, 0f, out _myScroll, out _myListContent);

        _myEmptyLabel = LumiWorldUITheme.CreateLabel(
            wellRt, "MyEmpty", "You have no active market listings.", 15f, LumiWorldUITheme.Muted,
            headingFont: false, align: TextAlignmentOptions.Center, wrap: true);
        LumiWorldUITheme.Stretch(_myEmptyLabel.rectTransform, 24f);
        _myEmptyLabel.gameObject.SetActive(false);

        Image detailImg = LumiWorldUITheme.CreateRoundedSurface(
            _myViewGo.transform, "MyDetailPane", LumiWorldUITheme.Surface, LumiWorldUITheme.Edge,
            radius: 18, borderThickness: 1.5f, shadowDepth: 2f, shadowAlpha: 0.08f, raycastTarget: true);
        RectTransform detailRt = detailImg.rectTransform;
        LumiWorldUITheme.PlaceTopRight(detailRt, 24f, 120f, 346f, 466f);

        _myDetailEmptyLabel = LumiWorldUITheme.CreateLabel(
            detailRt, "MyDetailEmpty", "Select a listing to inspect or cancel.", 15f, LumiWorldUITheme.Muted,
            headingFont: false, align: TextAlignmentOptions.Center, wrap: true);
        LumiWorldUITheme.Stretch(_myDetailEmptyLabel.rectTransform, 24f);

        _myDetailFilledGo = new GameObject("MyDetailFilled", typeof(RectTransform));
        _myDetailFilledGo.transform.SetParent(detailRt, false);
        LumiWorldUITheme.Stretch(_myDetailFilledGo.GetComponent<RectTransform>());

        Image artFrame = LumiWorldUITheme.CreateRoundedSurface(
            _myDetailFilledGo.transform, "MyArtBox", LumiWorldUITheme.Inset, LumiWorldUITheme.Edge,
            radius: 16, borderThickness: 1.2f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: false);
        LumiWorldUITheme.PlaceTopLeft(artFrame.rectTransform, 113f, 20f, 120f, 120f);

        GameObject artImgGo = new GameObject("Artwork", typeof(RectTransform), typeof(Image));
        artImgGo.transform.SetParent(artFrame.transform, false);
        LumiWorldUITheme.Stretch(artImgGo.GetComponent<RectTransform>(), 14f);
        _myDetailArtwork = artImgGo.GetComponent<Image>();
        _myDetailArtwork.preserveAspect = true;

        _myDetailTitle = LumiWorldUITheme.CreateLabel(
            _myDetailFilledGo.transform, "Title", string.Empty, 20f, LumiWorldUITheme.Text,
            headingFont: true, align: TextAlignmentOptions.Center);
        LumiWorldUITheme.PlaceTopLeft(_myDetailTitle.rectTransform, 18f, 150f, 310f, 34f);

        _myDetailKind = LumiWorldUITheme.CreateLabel(
            _myDetailFilledGo.transform, "Kind", string.Empty, 13f, LumiWorldUITheme.Muted,
            headingFont: false, align: TextAlignmentOptions.Center);
        LumiWorldUITheme.PlaceTopLeft(_myDetailKind.rectTransform, 18f, 184f, 310f, 22f);

        CreateDivider(_myDetailFilledGo.transform, 18f, 214f, 310f);

        _myDetailQty = CreateDetailRow(_myDetailFilledGo.transform, "Quantity", 228f);
        _myDetailTotalPrice = CreateDetailRow(_myDetailFilledGo.transform, "Total price", 262f);
        _myDetailExpires = CreateDetailRow(_myDetailFilledGo.transform, "Expires in", 296f);

        _myDetailStatusText = LumiWorldUITheme.CreateLabel(
            _myDetailFilledGo.transform, "MyStatus", string.Empty, 13f, LumiWorldUITheme.Muted,
            headingFont: false, align: TextAlignmentOptions.Center, wrap: true);
        LumiWorldUITheme.PlaceTopLeft(_myDetailStatusText.rectTransform, 18f, 354f, 310f, 40f);

        _myCancelSingleBtn = LumiWorldUITheme.CreatePillButton(
            _myDetailFilledGo.transform, "CancelSingleBtn", "Cancel listing", "G-27", true, 15f,
            ExecuteCancelSingleFocused,
            out _myCancelSingleBg, out _myCancelSingleIcon, out _myCancelSingleLabel, radius: 14);
        LumiWorldUITheme.PlaceTopLeft(_myCancelSingleBtn.GetComponent<RectTransform>(), 20f, 402f, 306f, 46f);
    }

    private void SwitchTab(MarketTab tab)
    {
        _activeTab = tab;
        for (int i = 0; i < 3; i++)
            LumiWorldUITheme.SetSubTabActive(_topTabBg[i], _topTabIcon[i], _topTabLabel[i], _topTabShadow[i], i == (int)tab, darkActivePill: true);

        _buyViewGo.SetActive(tab == MarketTab.Buy);
        _sellViewGo.SetActive(tab == MarketTab.Sell);
        _myViewGo.SetActive(tab == MarketTab.MyListings);

        if (tab == MarketTab.Buy)
        {
            UpdateBuyCategoryButtons();
            FetchBrowseListings();
        }
        else if (tab == MarketTab.Sell)
        {
            UpdateSellCategoryButtons();
            RefreshSellCatalog(true);
            FetchMyListings(false);
        }
        else
        {
            FetchMyListings(true);
        }
    }

    // ============================================================
    // P-17 BUY TAB
    // ============================================================

    private void SetBuyCategory(BrowseCategory category)
    {
        _browseCategory = category;
        _browsePage = 1;
        _buyFeedback = string.Empty;
        UpdateBuyCategoryButtons();
        FetchBrowseListings();
    }

    private void UpdateBuyCategoryButtons()
    {
        for (int i = 0; i < 3; i++)
            LumiWorldUITheme.SetSubTabActive(_buyCatBg[i], _buyCatIcon[i], _buyCatLabel[i], _buyCatShadow[i], i == (int)_browseCategory, darkActivePill: false);
    }

    private void ToggleBrowseSort()
    {
        _newestFirst = !_newestFirst;
        if (_sortBtnLabel != null)
            _sortBtnLabel.text = _newestFirst ? "Sort: Newest" : "Sort: Unit price";
        _browsePage = 1;
        FetchBrowseListings();
    }

    private void ChangeBrowsePage(int delta)
    {
        int nextPage = Mathf.Max(1, _browsePage + delta);
        if (nextPage == _browsePage)
            return;
        _browsePage = nextPage;
        FetchBrowseListings();
    }

    private void FetchBrowseListings()
    {
        if (_isLoadingBrowse)
            return;

        _isLoadingBrowse = true;
        _buyEmptyLabel.gameObject.SetActive(true);
        _buyEmptyLabel.text = "Loading market listings...";

        string kind = _browseCategory == BrowseCategory.All
            ? null
            : (_browseCategory == BrowseCategory.Resources ? "resource" : "card");

        MarketSync.Browse(
            null,
            _newestFirst,
            _browsePage,
            BrowsePageSize,
            res =>
            {
                _isLoadingBrowse = false;
                _browseListings.Clear();
                if (res != null && res.items != null)
                    _browseListings.AddRange(res.items);
                _browseHasMore = res != null && res.hasMore;
                RebuildBrowseRows();
            },
            err =>
            {
                _isLoadingBrowse = false;
                _browseListings.Clear();
                _browseHasMore = false;
                _buyFeedback = string.IsNullOrEmpty(err) ? "Could not load market listings." : err;
                _buyFeedbackError = true;
                RebuildBrowseRows();
            },
            kind);
    }

    private void RebuildBrowseRows()
    {
        ClearChildren(_buyListContent);
        _buySelectionBorders.Clear();

        bool stillExists = false;
        for (int i = 0; i < _browseListings.Count; i++)
        {
            if (_browseListings[i].id == _selectedBuyId)
            {
                stillExists = true;
                break;
            }
        }
        if (!stillExists)
            _selectedBuyId = _browseListings.Count > 0 ? _browseListings[0].id : null;

        _buyEmptyLabel.gameObject.SetActive(_browseListings.Count == 0);
        if (_browseListings.Count == 0)
            _buyEmptyLabel.text = _buyFeedbackError && !string.IsNullOrEmpty(_buyFeedback)
                ? _buyFeedback
                : "No active listings match this filter.";

        for (int i = 0; i < _browseListings.Count; i++)
        {
            MarketListingDto lot = _browseListings[i];
            Image rowImg = LumiWorldUITheme.CreateRoundedSurface(
                _buyListContent, "BuyRow_" + i, LumiWorldUITheme.Surface, LumiWorldUITheme.Edge,
                radius: 14, borderThickness: 1.2f, shadowDepth: 2f, shadowAlpha: 0.08f, raycastTarget: true);
            LayoutElement le = rowImg.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 76f;
            le.minHeight = 76f;

            Button rowBtn = rowImg.gameObject.AddComponent<Button>();
            rowBtn.transition = Selectable.Transition.None;
            string lotId = lot.id;
            rowBtn.onClick.AddListener(() => SelectBuyListing(lotId));

            GameObject border = LumiWorldUITheme.CreateSelectionBorder(rowImg.transform, 14, 2.5f).gameObject;
            border.SetActive(lot.id == _selectedBuyId);
            _buySelectionBorders[lot.id] = border;

            Image thumb = LumiWorldUITheme.CreateRoundedSurface(
                rowImg.transform, "Thumb", LumiWorldUITheme.Inset, LumiWorldUITheme.Edge,
                radius: 10, borderThickness: 1f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: false);
            LumiWorldUITheme.PlaceTopLeft(thumb.rectTransform, 12f, 14f, 48f, 48f);

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(thumb.transform, false);
            LumiWorldUITheme.Stretch(iconGo.GetComponent<RectTransform>(), 6f);
            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.sprite = ResolveMarketItemSprite(lot.resourceId);
            iconImg.enabled = iconImg.sprite != null;

            string itemName = MarketSync.NameOf(lot.resourceId);
            TextMeshProUGUI nameTxt = LumiWorldUITheme.CreateLabel(
                rowImg.transform, "Name", $"{itemName}  x{lot.amount}", 15f, LumiWorldUITheme.Text, headingFont: false);
            LumiWorldUITheme.PlaceTopLeft(nameTxt.rectTransform, 72f, 12f, 340f, 24f);

            string sellerName = string.IsNullOrEmpty(lot.sellerName) ? "Unknown" : lot.sellerName;
            string subText = $"Seller: {sellerName}  |  Expires in {FormatExpiry(lot.expiresAt)}";
            TextMeshProUGUI subTxt = LumiWorldUITheme.CreateLabel(
                rowImg.transform, "Sub", subText, 12.5f, LumiWorldUITheme.Muted, headingFont: false);
            LumiWorldUITheme.PlaceTopLeft(subTxt.rectTransform, 72f, 39f, 355f, 22f);

            TextMeshProUGUI priceTxt = LumiWorldUITheme.CreateLabel(
                rowImg.transform, "Price", $"{lot.priceCoins.ToString("N0", CultureInfo.InvariantCulture)} c",
                16f, LumiWorldUITheme.Text, headingFont: true, align: TextAlignmentOptions.MidlineRight);
            LumiWorldUITheme.PlaceTopRight(priceTxt.rectTransform, 14f, 22f, 110f, 32f);
        }

        _buyPageLabel.text = $"Page {_browsePage}";
        _buyPrevPageBtn.interactable = _browsePage > 1;
        _buyNextPageBtn.interactable = _browseHasMore;
        LumiWorldUITheme.ApplyButtonDisabledNeutral(_buyPrevPageBtn);
        LumiWorldUITheme.ApplyButtonDisabledNeutral(_buyNextPageBtn);

        if (_buyScroll != null)
            _buyScroll.verticalNormalizedPosition = 1f;

        RefreshBuyDetail();
    }

    private void SelectBuyListing(string lotId)
    {
        _selectedBuyId = lotId;
        _buyFeedback = string.Empty;
        foreach (KeyValuePair<string, GameObject> kv in _buySelectionBorders)
        {
            if (kv.Value != null)
                kv.Value.SetActive(kv.Key == _selectedBuyId);
        }
        RefreshBuyDetail();
    }

    private void RefreshBuyDetail()
    {
        MarketListingDto lot = FindListingById(_browseListings, _selectedBuyId);
        if (lot == null)
        {
            _buyDetailFilledGo.SetActive(false);
            _buyDetailEmptyLabel.gameObject.SetActive(true);
            return;
        }

        _buyDetailEmptyLabel.gameObject.SetActive(false);
        _buyDetailFilledGo.SetActive(true);

        _buyDetailArtwork.sprite = ResolveMarketItemSprite(lot.resourceId);
        _buyDetailArtwork.enabled = _buyDetailArtwork.sprite != null;

        bool isCard = CardTradeIds.IsCardId(lot.resourceId);
        _buyDetailTitle.text = MarketSync.NameOf(lot.resourceId);
        _buyDetailKind.text = isCard ? "Creature card" : "Resource";
        _buyDetailQty.text = $"x{lot.amount}";
        _buyDetailSeller.text = string.IsNullOrEmpty(lot.sellerName) ? "Unknown" : lot.sellerName;
        _buyDetailTotalPrice.text = $"{lot.priceCoins.ToString("N0", CultureInfo.InvariantCulture)} coins";
        _buyDetailExpires.text = FormatExpiry(lot.expiresAt);

        int balance = CurrencyWallet.HasInstance ? CurrencyWallet.Instance.Balance : 0;
        bool isOwn = !string.IsNullOrEmpty(AuthSession.PlayerId) && lot.sellerId == AuthSession.PlayerId;
        bool canAfford = balance >= lot.priceCoins;
        bool canBuy = !_isBuying && !isOwn && canAfford;

        if (!string.IsNullOrEmpty(_buyFeedback))
        {
            _buyDetailStatus.text = _buyFeedback;
            _buyDetailStatus.color = _buyFeedbackError ? LumiWorldUITheme.Error : LumiWorldUITheme.Success;
        }
        else if (isOwn)
        {
            _buyDetailStatus.text = "This is your own listing.";
            _buyDetailStatus.color = LumiWorldUITheme.Muted;
        }
        else if (!canAfford)
        {
            _buyDetailStatus.text = "Not enough coins for this lot.";
            _buyDetailStatus.color = LumiWorldUITheme.Warning;
        }
        else
        {
            _buyDetailStatus.text = string.Empty;
        }

        SetPrimaryActionButtonState(_buyLotBtn, _buyLotBtnBg, _buyLotBtnIcon, _buyLotBtnLabel,
            canBuy, _isBuying ? "Buying..." : "Buy lot");
    }

    private void ExecuteBuySelectedLot()
    {
        MarketListingDto lot = FindListingById(_browseListings, _selectedBuyId);
        if (lot == null || _isBuying)
            return;

        _isBuying = true;
        _buyFeedback = string.Empty;
        RefreshBuyDetail();

        MarketSync.Buy(lot, (ok, msg) =>
        {
            _isBuying = false;
            _buyFeedbackError = !ok;
            _buyFeedback = string.IsNullOrEmpty(msg)
                ? (ok ? "Purchased listing!" : "Purchase failed.")
                : msg;
            if (ok)
                FetchBrowseListings();
            else
                RefreshBuyDetail();
        });
    }

    // ============================================================
    // P-19 SELL TAB
    // ============================================================

    private void SetSellCategory(SellCategory cat)
    {
        _sellCategory = cat;
        _sellFeedback = string.Empty;
        UpdateSellCategoryButtons();
        RefreshSellCatalog(true);
    }

    private void UpdateSellCategoryButtons()
    {
        for (int i = 0; i < 2; i++)
            LumiWorldUITheme.SetSubTabActive(_sellCatBg[i], _sellCatIcon[i], _sellCatLabel[i], _sellCatShadow[i], i == (int)_sellCategory, darkActivePill: false);
    }

    private void RefreshSellCatalog(bool resetScroll)
    {
        _sellEntries.Clear();
        ClearChildren(_sellListContent);
        _sellSelectionBorders.Clear();

        if (_sellCategory == SellCategory.Resources)
            CollectSellableResources();
        else
            CollectSellableCards();

        _sellQuotaLabel.text = $"Active listings: {_myListings.Count} / {MarketSync.MaxActiveListings}  |  {MarketSync.ListingHours}h";

        bool selectedFound = false;
        for (int i = 0; i < _sellEntries.Count; i++)
        {
            if (_sellEntries[i].MarketId == _selectedSellMarketId)
            {
                selectedFound = true;
                break;
            }
        }
        if (!selectedFound)
            _selectedSellMarketId = _sellEntries.Count > 0 ? _sellEntries[0].MarketId : null;

        _sellEmptyLabel.gameObject.SetActive(_sellEntries.Count == 0);

        for (int i = 0; i < _sellEntries.Count; i++)
        {
            SellableEntry entry = _sellEntries[i];
            Image rowImg = LumiWorldUITheme.CreateRoundedSurface(
                _sellListContent, "SellRow_" + i, LumiWorldUITheme.Surface, LumiWorldUITheme.Edge,
                radius: 14, borderThickness: 1.2f, shadowDepth: 2f, shadowAlpha: 0.08f, raycastTarget: true);
            LayoutElement le = rowImg.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 72f;
            le.minHeight = 72f;

            Button btn = rowImg.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            string mId = entry.MarketId;
            btn.onClick.AddListener(() => SelectSellItem(mId));

            GameObject border = LumiWorldUITheme.CreateSelectionBorder(rowImg.transform, 14, 2.5f).gameObject;
            border.SetActive(entry.MarketId == _selectedSellMarketId);
            _sellSelectionBorders[entry.MarketId] = border;

            Image thumb = LumiWorldUITheme.CreateRoundedSurface(
                rowImg.transform, "Thumb", LumiWorldUITheme.Inset, LumiWorldUITheme.Edge,
                radius: 10, borderThickness: 1f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: false);
            LumiWorldUITheme.PlaceTopLeft(thumb.rectTransform, 12f, 12f, 48f, 48f);

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(thumb.transform, false);
            LumiWorldUITheme.Stretch(iconGo.GetComponent<RectTransform>(), 6f);
            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.sprite = entry.Icon;
            iconImg.enabled = entry.Icon != null;

            TextMeshProUGUI nameTxt = LumiWorldUITheme.CreateLabel(
                rowImg.transform, "Name", entry.DisplayName, 15f, LumiWorldUITheme.Text, headingFont: false);
            LumiWorldUITheme.PlaceTopLeft(nameTxt.rectTransform, 72f, 12f, 340f, 24f);

            TextMeshProUGUI subTxt = LumiWorldUITheme.CreateLabel(
                rowImg.transform, "Sub", $"{entry.KindLabel}  |  Base {entry.BaseValue} c", 12.5f, LumiWorldUITheme.Muted, headingFont: false);
            LumiWorldUITheme.PlaceTopLeft(subTxt.rectTransform, 72f, 38f, 340f, 22f);

            TextMeshProUGUI ownedTxt = LumiWorldUITheme.CreateLabel(
                rowImg.transform, "Owned", $"x{entry.OwnedCount}", 15f, LumiWorldUITheme.Text,
                headingFont: true, align: TextAlignmentOptions.MidlineRight);
            LumiWorldUITheme.PlaceTopRight(ownedTxt.rectTransform, 14f, 20f, 100f, 32f);
        }

        if (resetScroll && _sellScroll != null)
            _sellScroll.verticalNormalizedPosition = 1f;

        SyncSellPriceDefaults();
        RefreshSellInspector();
    }

    private void CollectSellableResources()
    {
        IReadOnlyList<ResourceData> defs = ResourceCatalog.All;
        if (defs == null || !ResourceInventory.HasInstance)
            return;

        for (int i = 0; i < defs.Count; i++)
        {
            ResourceData res = defs[i];
            if (res == null || string.IsNullOrEmpty(res.resourceID))
                continue;
            int owned = ResourceInventory.Instance.GetAmount(res.resourceID);
            if (owned <= 0)
                continue;

            _sellEntries.Add(new SellableEntry
            {
                MarketId = res.resourceID,
                DisplayName = string.IsNullOrEmpty(res.resourceName) ? res.resourceID : res.resourceName,
                KindLabel = "Resource",
                IsCard = false,
                OwnedCount = owned,
                MaxPerListing = owned,
                BaseValue = Mathf.Max(1, res.baseValue),
                Icon = res.icon != null ? res.icon : ResourceIconUtility.GetResourceSprite(res)
            });
        }
    }

    private void CollectSellableCards()
    {
        List<KeyValuePair<CardData, int>> entries = HandCards.Creatures();
        if (entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            CardData card = entries[i].Key;
            int count = entries[i].Value;
            if (card == null || count <= 0)
                continue;

            string mId = CardTradeIds.ToMarketId(card);
            if (string.IsNullOrEmpty(mId))
                continue;

            int baseVal = card.shopPrice > 0 ? card.shopPrice : 20;

            _sellEntries.Add(new SellableEntry
            {
                MarketId = mId,
                DisplayName = string.IsNullOrEmpty(card.cardName) ? card.name : card.cardName,
                KindLabel = "Creature card",
                IsCard = true,
                OwnedCount = count,
                MaxPerListing = Mathf.Min(count, Mathf.Max(1, MarketSync.MaxCardsPerListing)),
                BaseValue = baseVal,
                Icon = card.cardImage
            });
        }
    }

    private void SelectSellItem(string marketId)
    {
        _selectedSellMarketId = marketId;
        _sellFeedback = string.Empty;
        foreach (KeyValuePair<string, GameObject> kv in _sellSelectionBorders)
        {
            if (kv.Value != null)
                kv.Value.SetActive(kv.Key == _selectedSellMarketId);
        }
        _sellQty = 1;
        SyncSellPriceDefaults();
        RefreshSellInspector();
    }

    private void SyncSellPriceDefaults()
    {
        SellableEntry entry = FindSelectedSellEntry();
        if (entry == null)
            return;

        _sellQty = Mathf.Clamp(_sellQty, 1, Mathf.Max(1, entry.MaxPerListing));
        MarketSync.PriceRange(_sellQty, entry.BaseValue, out int min, out int max);
        _sellTotalPrice = Mathf.Clamp(_sellQty * entry.BaseValue, min, max);

        if (_sellPriceInput != null)
        {
            _suppressPriceInputCallback = true;
            _sellPriceInput.text = _sellTotalPrice.ToString(CultureInfo.InvariantCulture);
            _suppressPriceInputCallback = false;
        }
    }

    private void AdjustSellQuantity(int delta)
    {
        SellableEntry entry = FindSelectedSellEntry();
        if (entry == null)
            return;

        int nextQty = Mathf.Clamp(_sellQty + delta, 1, Mathf.Max(1, entry.MaxPerListing));
        if (nextQty == _sellQty)
            return;

        _sellQty = nextQty;
        _sellFeedback = string.Empty;
        SyncSellPriceDefaults();
        RefreshSellInspector();
    }

    private void SetSellQuantityMax()
    {
        SellableEntry entry = FindSelectedSellEntry();
        if (entry == null)
            return;

        _sellQty = Mathf.Max(1, entry.MaxPerListing);
        _sellFeedback = string.Empty;
        SyncSellPriceDefaults();
        RefreshSellInspector();
    }

    private void OnSellPriceInputChanged(string raw)
    {
        if (_suppressPriceInputCallback)
            return;

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            _sellTotalPrice = Mathf.Max(0, parsed);
        else
            _sellTotalPrice = 0;

        RefreshSellBreakdownOnly();
    }

    private void OnSellPriceEndEdit(string raw)
    {
        if (_suppressPriceInputCallback)
            return;

        SellableEntry entry = FindSelectedSellEntry();
        if (entry == null)
            return;

        MarketSync.PriceRange(_sellQty, entry.BaseValue, out int min, out int max);
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            parsed = _sellQty * entry.BaseValue;

        _sellTotalPrice = Mathf.Clamp(parsed, min, max);
        _suppressPriceInputCallback = true;
        if (_sellPriceInput != null)
            _sellPriceInput.text = _sellTotalPrice.ToString(CultureInfo.InvariantCulture);
        _suppressPriceInputCallback = false;
        RefreshSellInspector();
    }

    private void RefreshSellInspector()
    {
        SellableEntry entry = FindSelectedSellEntry();
        if (entry == null)
        {
            _sellDetailFilledGo.SetActive(false);
            _sellDetailEmptyLabel.gameObject.SetActive(true);
            return;
        }

        _sellDetailEmptyLabel.gameObject.SetActive(false);
        _sellDetailFilledGo.SetActive(true);

        _sellDetailArtwork.sprite = entry.Icon;
        _sellDetailArtwork.enabled = entry.Icon != null;
        _sellDetailTitle.text = entry.DisplayName;
        _sellDetailOwned.text = $"Owned: x{entry.OwnedCount}";
        _sellQtyValueText.text = _sellQty.ToString(CultureInfo.InvariantCulture);

        _sellQtyMinusBtn.interactable = _sellQty > 1;
        _sellQtyPlusBtn.interactable = _sellQty < entry.MaxPerListing;
        _sellQtyMaxBtn.interactable = _sellQty < entry.MaxPerListing;
        LumiWorldUITheme.ApplyButtonDisabledNeutral(_sellQtyMinusBtn);
        LumiWorldUITheme.ApplyButtonDisabledNeutral(_sellQtyPlusBtn);
        LumiWorldUITheme.ApplyButtonDisabledNeutral(_sellQtyMaxBtn);

        RefreshSellBreakdownOnly();
    }

    private void RefreshSellBreakdownOnly()
    {
        SellableEntry entry = FindSelectedSellEntry();
        if (entry == null)
            return;

        MarketSync.PriceRange(_sellQty, entry.BaseValue, out int min, out int max);
        _sellRangeHintText.text = $"Allowed range: {min} - {max} coins";

        int fee = MarketSync.Fee(_sellTotalPrice);
        int receive = Mathf.Max(0, _sellTotalPrice - fee);

        _sellFeeLabelText.text = $"Listing fee ({MarketSync.FeePercent}%)";
        _sellFeeValueText.text = $"-{fee.ToString("N0", CultureInfo.InvariantCulture)} coins";
        _sellReceiveValueText.text = $"{receive.ToString("N0", CultureInfo.InvariantCulture)} coins";
        _sellBuyerTotalValueText.text = $"{_sellTotalPrice.ToString("N0", CultureInfo.InvariantCulture)} coins";

        bool atLimit = _myListings.Count >= MarketSync.MaxActiveListings;
        bool inRange = _sellTotalPrice >= min && _sellTotalPrice <= max;
        bool canCreate = !_isCreatingListing && !atLimit && inRange && _sellQty > 0 && _sellQty <= entry.MaxPerListing;

        if (!string.IsNullOrEmpty(_sellFeedback))
        {
            _sellDetailStatus.text = _sellFeedback;
            _sellDetailStatus.color = _sellFeedbackError ? LumiWorldUITheme.Error : LumiWorldUITheme.Success;
        }
        else if (atLimit)
        {
            _sellDetailStatus.text = $"Max active listings ({MarketSync.MaxActiveListings}) reached.";
            _sellDetailStatus.color = LumiWorldUITheme.Warning;
        }
        else if (!inRange)
        {
            _sellDetailStatus.text = $"Price must be between {min} and {max} coins.";
            _sellDetailStatus.color = LumiWorldUITheme.Warning;
        }
        else
        {
            _sellDetailStatus.text = string.Empty;
        }

        SetPrimaryActionButtonState(_sellCreateBtn, _sellCreateBtnBg, _sellCreateBtnIcon, _sellCreateBtnLabel,
            canCreate, _isCreatingListing ? "Creating..." : "Create listing");
    }

    private void ExecuteCreateListing()
    {
        SellableEntry entry = FindSelectedSellEntry();
        if (entry == null || _isCreatingListing)
            return;

        MarketSync.PriceRange(_sellQty, entry.BaseValue, out int min, out int max);
        int clampedPrice = Mathf.Clamp(_sellTotalPrice, min, max);
        if (clampedPrice != _sellTotalPrice)
        {
            _sellTotalPrice = clampedPrice;
            SyncSellPriceDefaults();
        }

        _isCreatingListing = true;
        _sellFeedback = string.Empty;
        RefreshSellInspector();

        MarketSync.Sell(entry.MarketId, _sellQty, clampedPrice, (ok, msg) =>
        {
            _isCreatingListing = false;
            _sellFeedbackError = !ok;
            _sellFeedback = string.IsNullOrEmpty(msg)
                ? (ok ? "Listing created!" : "Could not create listing.")
                : msg;

            RefreshSellCatalog(false);
            if (ok)
                FetchMyListings(false);
        });
    }

    // ============================================================
    // P-20 MY LISTINGS TAB (Independent Multi-Select + Batch Cancel)
    // ============================================================

    private void FetchMyListings(bool refreshView)
    {
        if (_isLoadingMine)
            return;

        _isLoadingMine = true;
        if (refreshView)
        {
            _myEmptyLabel.gameObject.SetActive(true);
            _myEmptyLabel.text = "Loading your listings...";
        }

        MarketSync.Mine(
            res =>
            {
                _isLoadingMine = false;
                _myListings.Clear();
                if (res != null && res.items != null)
                    _myListings.AddRange(res.items);

                HashSet<string> validIds = new HashSet<string>();
                for (int i = 0; i < _myListings.Count; i++)
                    validIds.Add(_myListings[i].id);
                _selectedMyListingIds.RemoveWhere(id => !validIds.Contains(id));

                _sellQuotaLabel.text = $"Active listings: {_myListings.Count} / {MarketSync.MaxActiveListings}  |  {MarketSync.ListingHours}h";
                UpdateMyListingsTabTitle();

                if (_activeTab == MarketTab.MyListings || refreshView)
                    RebuildMyListingRows();
            },
            err =>
            {
                _isLoadingMine = false;
                if (_activeTab == MarketTab.MyListings || refreshView)
                {
                    _myFeedback = string.IsNullOrEmpty(err) ? "Could not load your listings." : err;
                    _myFeedbackError = true;
                    RebuildMyListingRows();
                }
            });
    }

    private void UpdateMyListingsTabTitle()
    {
        if (_topTabLabel[2] != null)
            _topTabLabel[2].text = _myListings.Count > 0 ? $"My listings ({_myListings.Count})" : "My listings";
    }

    private void RebuildMyListingRows()
    {
        ClearChildren(_myListContent);
        _myRowBindings.Clear();

        bool focusValid = false;
        for (int i = 0; i < _myListings.Count; i++)
        {
            if (_myListings[i].id == _selectedMyFocusId)
            {
                focusValid = true;
                break;
            }
        }
        if (!focusValid)
            _selectedMyFocusId = _myListings.Count > 0 ? _myListings[0].id : null;

        _myCountLabel.text = $"Active listings ({_myListings.Count} / {MarketSync.MaxActiveListings})";
        _myEmptyLabel.gameObject.SetActive(_myListings.Count == 0);
        if (_myListings.Count == 0)
            _myEmptyLabel.text = _myFeedbackError && !string.IsNullOrEmpty(_myFeedback)
                ? _myFeedback
                : "You have no active market listings.";

        for (int i = 0; i < _myListings.Count; i++)
        {
            MarketListingDto lot = _myListings[i];
            Image rowImg = LumiWorldUITheme.CreateRoundedSurface(
                _myListContent, "MyRow_" + i, LumiWorldUITheme.Surface, LumiWorldUITheme.Edge,
                radius: 14, borderThickness: 1.2f, shadowDepth: 2f, shadowAlpha: 0.08f, raycastTarget: true);
            LayoutElement le = rowImg.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 76f;
            le.minHeight = 76f;

            Button rowBtn = rowImg.gameObject.AddComponent<Button>();
            rowBtn.transition = Selectable.Transition.None;
            string focusId = lot.id;
            rowBtn.onClick.AddListener(() => FocusMyListing(focusId));

            GameObject border = LumiWorldUITheme.CreateSelectionBorder(rowImg.transform, 14, 2.5f).gameObject;
            border.SetActive(lot.id == _selectedMyFocusId);

            GameObject checkHitGo = new GameObject("CheckboxHit", typeof(RectTransform), typeof(Image), typeof(Button));
            checkHitGo.transform.SetParent(rowImg.transform, false);
            LumiWorldUITheme.PlaceTopLeft(checkHitGo.GetComponent<RectTransform>(), 8f, 18f, 38f, 40f);
            checkHitGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            Button checkBtn = checkHitGo.GetComponent<Button>();
            checkBtn.transition = Selectable.Transition.None;
            string toggleId = lot.id;
            checkBtn.onClick.AddListener(() => ToggleMyListingCheckbox(toggleId));

            Image checkBoxBg = LumiWorldUITheme.CreateRoundedSurface(
                checkHitGo.transform, "Box", LumiWorldUITheme.Inset, LumiWorldUITheme.Edge,
                radius: 7, borderThickness: 1.5f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: false);
            RectTransform boxRt = checkBoxBg.rectTransform;
            boxRt.anchorMin = new Vector2(0.5f, 0.5f);
            boxRt.anchorMax = new Vector2(0.5f, 0.5f);
            boxRt.pivot = new Vector2(0.5f, 0.5f);
            boxRt.anchoredPosition = Vector2.zero;
            boxRt.sizeDelta = new Vector2(22f, 22f);

            TextMeshProUGUI checkMark = LumiWorldUITheme.CreateLabel(
                boxRt, "Mark", "v", 13f, Color.white, headingFont: true, align: TextAlignmentOptions.Center);
            LumiWorldUITheme.Stretch(checkMark.rectTransform);

            Image thumb = LumiWorldUITheme.CreateRoundedSurface(
                rowImg.transform, "Thumb", LumiWorldUITheme.Inset, LumiWorldUITheme.Edge,
                radius: 10, borderThickness: 1f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: false);
            LumiWorldUITheme.PlaceTopLeft(thumb.rectTransform, 50f, 14f, 48f, 48f);

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(thumb.transform, false);
            LumiWorldUITheme.Stretch(iconGo.GetComponent<RectTransform>(), 6f);
            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.sprite = ResolveMarketItemSprite(lot.resourceId);
            iconImg.enabled = iconImg.sprite != null;

            string itemName = MarketSync.NameOf(lot.resourceId);
            TextMeshProUGUI nameTxt = LumiWorldUITheme.CreateLabel(
                rowImg.transform, "Name", $"{itemName}  x{lot.amount}", 15f, LumiWorldUITheme.Text, headingFont: false);
            LumiWorldUITheme.PlaceTopLeft(nameTxt.rectTransform, 108f, 12f, 305f, 24f);

            TextMeshProUGUI expTxt = LumiWorldUITheme.CreateLabel(
                rowImg.transform, "Exp", $"Expires in {FormatExpiry(lot.expiresAt)}", 12.5f, LumiWorldUITheme.Muted, headingFont: false);
            LumiWorldUITheme.PlaceTopLeft(expTxt.rectTransform, 108f, 39f, 305f, 22f);

            TextMeshProUGUI priceTxt = LumiWorldUITheme.CreateLabel(
                rowImg.transform, "Price", $"{lot.priceCoins.ToString("N0", CultureInfo.InvariantCulture)} c",
                16f, LumiWorldUITheme.Text, headingFont: true, align: TextAlignmentOptions.MidlineRight);
            LumiWorldUITheme.PlaceTopRight(priceTxt.rectTransform, 14f, 22f, 110f, 32f);

            _myRowBindings.Add(new MyRowBinding
            {
                Lot = lot,
                SelectionBorder = border,
                CheckboxBg = checkBoxBg,
                CheckboxMark = checkMark
            });
        }

        RefreshMyCheckboxesVisuals();
        RefreshMyDetail();
    }

    private void FocusMyListing(string lotId)
    {
        _selectedMyFocusId = lotId;
        _myFeedback = string.Empty;
        for (int i = 0; i < _myRowBindings.Count; i++)
        {
            if (_myRowBindings[i].SelectionBorder != null)
                _myRowBindings[i].SelectionBorder.SetActive(_myRowBindings[i].Lot.id == _selectedMyFocusId);
        }
        RefreshMyDetail();
    }

    private void ToggleMyListingCheckbox(string lotId)
    {
        if (_isCancelling)
            return;

        if (_selectedMyListingIds.Contains(lotId))
            _selectedMyListingIds.Remove(lotId);
        else
            _selectedMyListingIds.Add(lotId);

        RefreshMyCheckboxesVisuals();
    }

    private void ToggleSelectAllMyListings()
    {
        if (_isCancelling || _myListings.Count == 0)
            return;

        bool allSelected = _selectedMyListingIds.Count == _myListings.Count;
        _selectedMyListingIds.Clear();
        if (!allSelected)
        {
            for (int i = 0; i < _myListings.Count; i++)
                _selectedMyListingIds.Add(_myListings[i].id);
        }

        RefreshMyCheckboxesVisuals();
    }

    private void RefreshMyCheckboxesVisuals()
    {
        for (int i = 0; i < _myRowBindings.Count; i++)
        {
            MyRowBinding b = _myRowBindings[i];
            bool checkedItem = _selectedMyListingIds.Contains(b.Lot.id);
            if (b.CheckboxBg != null)
                b.CheckboxBg.color = checkedItem ? LumiWorldUITheme.Primary : LumiWorldUITheme.Inset;
            if (b.CheckboxMark != null)
                b.CheckboxMark.gameObject.SetActive(checkedItem);
        }

        int count = _selectedMyListingIds.Count;
        bool allSelected = _myListings.Count > 0 && count == _myListings.Count;
        if (_mySelectAllLabel != null)
            _mySelectAllLabel.text = allSelected ? "Deselect all" : "Select all";

        _mySelectAllBtn.interactable = !_isCancelling && _myListings.Count > 0;
        LumiWorldUITheme.ApplyButtonDisabledNeutral(_mySelectAllBtn);

        SetPrimaryActionButtonState(_myCancelSelectedBtn, _myCancelSelectedBg, _myCancelSelectedIcon, _myCancelSelectedLabel,
            !_isCancelling && count > 0,
            _isCancelling ? "Cancelling..." : $"Cancel selected ({count})");
    }

    private void RefreshMyDetail()
    {
        MarketListingDto lot = FindListingById(_myListings, _selectedMyFocusId);
        if (lot == null)
        {
            _myDetailFilledGo.SetActive(false);
            _myDetailEmptyLabel.gameObject.SetActive(true);
            return;
        }

        _myDetailEmptyLabel.gameObject.SetActive(false);
        _myDetailFilledGo.SetActive(true);

        _myDetailArtwork.sprite = ResolveMarketItemSprite(lot.resourceId);
        _myDetailArtwork.enabled = _myDetailArtwork.sprite != null;

        bool isCard = CardTradeIds.IsCardId(lot.resourceId);
        _myDetailTitle.text = MarketSync.NameOf(lot.resourceId);
        _myDetailKind.text = isCard ? "Creature card" : "Resource";
        _myDetailQty.text = $"x{lot.amount}";
        _myDetailTotalPrice.text = $"{lot.priceCoins.ToString("N0", CultureInfo.InvariantCulture)} coins";
        _myDetailExpires.text = FormatExpiry(lot.expiresAt);

        _myDetailStatusText.text = _myFeedback;
        _myDetailStatusText.color = _myFeedbackError ? LumiWorldUITheme.Error : LumiWorldUITheme.Success;

        SetPrimaryActionButtonState(_myCancelSingleBtn, _myCancelSingleBg, _myCancelSingleIcon, _myCancelSingleLabel,
            !_isCancelling, _isCancelling ? "Cancelling..." : "Cancel listing");
    }

    private void ExecuteCancelSingleFocused()
    {
        MarketListingDto lot = FindListingById(_myListings, _selectedMyFocusId);
        if (lot == null || _isCancelling)
            return;

        _isCancelling = true;
        _myFeedback = string.Empty;
        RefreshMyCheckboxesVisuals();
        RefreshMyDetail();

        MarketSync.Cancel(lot.id, (ok, msg) =>
        {
            _isCancelling = false;
            if (ok)
                _selectedMyListingIds.Remove(lot.id);

            _myFeedbackError = !ok;
            _myFeedback = string.IsNullOrEmpty(msg)
                ? (ok ? "Listing cancelled and items returned." : "Failed to cancel listing.")
                : msg;
            FetchMyListings(true);
        });
    }

    private void ExecuteCancelSelectedBatch()
    {
        if (_isCancelling || _selectedMyListingIds.Count == 0)
            return;

        List<string> queue = new List<string>(_selectedMyListingIds);
        _isCancelling = true;
        _myFeedback = $"Cancelling {queue.Count} listings...";
        _myFeedbackError = false;
        RefreshMyCheckboxesVisuals();
        RefreshMyDetail();

        CancelNextSequential(queue, 0, 0, new List<string>());
    }

    private void CancelNextSequential(List<string> queue, int index, int succeeded, List<string> failures)
    {
        if (index >= queue.Count)
        {
            _isCancelling = false;
            if (failures.Count == 0)
            {
                _myFeedbackError = false;
                _myFeedback = $"Cancelled {succeeded} listing(s).";
            }
            else
            {
                _myFeedbackError = true;
                StringBuilder sb = new StringBuilder();
                sb.Append($"Cancelled {succeeded}, failed {failures.Count}");
                if (!string.IsNullOrEmpty(failures[0]))
                    sb.Append(": ").Append(failures[0]);
                _myFeedback = sb.ToString();
            }

            FetchMyListings(true);
            return;
        }

        string listingId = queue[index];
        MarketSync.Cancel(listingId, (ok, msg) =>
        {
            if (ok)
            {
                succeeded++;
                _selectedMyListingIds.Remove(listingId);
            }
            else
            {
                failures.Add(string.IsNullOrEmpty(msg) ? listingId : msg);
            }
            CancelNextSequential(queue, index + 1, succeeded, failures);
        });
    }

    // ============================================================
    // SHARED HELPERS
    // ============================================================

    private SellableEntry FindSelectedSellEntry()
    {
        for (int i = 0; i < _sellEntries.Count; i++)
        {
            if (_sellEntries[i].MarketId == _selectedSellMarketId)
                return _sellEntries[i];
        }
        return null;
    }

    private static MarketListingDto FindListingById(List<MarketListingDto> list, string id)
    {
        if (list == null || string.IsNullOrEmpty(id))
            return null;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].id == id)
                return list[i];
        }
        return null;
    }

    private static Sprite ResolveMarketItemSprite(string marketId)
    {
        if (string.IsNullOrEmpty(marketId))
            return null;

        if (CardTradeIds.IsCardId(marketId))
        {
            CardData card = CardTradeIds.Find(marketId);
            return card != null ? card.cardImage : null;
        }

        ResourceData res = ResourceCatalog.Find(marketId);
        if (res == null)
            return null;
        return res.icon != null ? res.icon : ResourceIconUtility.GetResourceSprite(res);
    }

    private static string FormatExpiry(string expiresAtIso)
    {
        if (string.IsNullOrEmpty(expiresAtIso))
            return "--";

        if (DateTimeOffset.TryParse(expiresAtIso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset dt))
        {
            TimeSpan rem = dt.UtcDateTime - DateTime.UtcNow;
            if (rem.TotalSeconds <= 0)
                return "Expired";
            if (rem.TotalHours >= 1)
                return $"{Mathf.FloorToInt((float)rem.TotalHours)}h {rem.Minutes}m";
            return $"{Mathf.Max(1, Mathf.FloorToInt((float)rem.TotalMinutes))}m";
        }

        return "--";
    }

    private static RectTransform CreateScrollList(Transform parent, float topPad, float bottomPad, out ScrollRect scroll, out RectTransform content)
    {
        GameObject vpGo = new GameObject("ScrollViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        vpGo.transform.SetParent(parent, false);
        RectTransform vpRt = vpGo.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = new Vector2(0f, bottomPad);
        vpRt.offsetMax = new Vector2(0f, -topPad);
        vpGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);

        GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(vpRt, false);
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        VerticalLayoutGroup vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 12, 12);
        vlg.spacing = 8f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = contentGo.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll = vpGo.GetComponent<ScrollRect>();
        scroll.viewport = vpRt;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
        return vpRt;
    }

    private static void CreateDivider(Transform parent, float x, float y, float w)
    {
        GameObject go = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        LumiWorldUITheme.PlaceTopLeft(go.GetComponent<RectTransform>(), x, y, w, 1.5f);
        go.GetComponent<Image>().color = LumiWorldUITheme.Edge;
    }

    private static TextMeshProUGUI CreateDetailRow(Transform parent, string label, float y)
    {
        TextMeshProUGUI keyTxt = LumiWorldUITheme.CreateLabel(
            parent, label + "_Key", label, 13.5f, LumiWorldUITheme.Muted, headingFont: false);
        LumiWorldUITheme.PlaceTopLeft(keyTxt.rectTransform, 20f, y, 124f, 24f);

        TextMeshProUGUI valTxt = LumiWorldUITheme.CreateLabel(
            parent, label + "_Val", "--", 14f, LumiWorldUITheme.Text,
            headingFont: false, align: TextAlignmentOptions.MidlineRight);
        LumiWorldUITheme.PlaceTopLeft(valTxt.rectTransform, 146f, y, 180f, 24f);
        return valTxt;
    }

    private static TMP_InputField CreatePriceInputField(Transform parent, float x, float y, float w, float h)
    {
        Image boxImg = LumiWorldUITheme.CreateRoundedSurface(
            parent, "PriceInputBox", LumiWorldUITheme.Slot, LumiWorldUITheme.Edge,
            radius: 10, borderThickness: 1.4f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: true);
        LumiWorldUITheme.PlaceTopLeft(boxImg.rectTransform, x, y, w, h);

        GameObject textArea = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        textArea.transform.SetParent(boxImg.transform, false);
        RectTransform areaRt = textArea.GetComponent<RectTransform>();
        areaRt.anchorMin = Vector2.zero;
        areaRt.anchorMax = Vector2.one;
        areaRt.offsetMin = new Vector2(10f, 4f);
        areaRt.offsetMax = new Vector2(-10f, -4f);

        TextMeshProUGUI textTmp = LumiWorldUITheme.CreateLabel(
            areaRt, "Text", string.Empty, 15f, LumiWorldUITheme.Text,
            headingFont: false, align: TextAlignmentOptions.MidlineRight);
        LumiWorldUITheme.Stretch(textTmp.rectTransform);

        TMP_InputField input = boxImg.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = areaRt;
        input.textComponent = textTmp;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.characterLimit = 7;
        input.caretColor = LumiWorldUITheme.Text;
        input.selectionColor = new Color(0.25f, 0.25f, 0.23f, 0.25f);
        return input;
    }

    private static void SetPrimaryActionButtonState(
        Button btn, Image bg, Image icon, TextMeshProUGUI label, bool enabled, string text)
    {
        if (btn == null)
            return;

        btn.interactable = enabled;
        LumiWorldUITheme.ApplyButtonDisabledNeutral(btn);

        if (bg != null)
            bg.color = enabled ? LumiWorldUITheme.Primary : LumiWorldUITheme.Disabled;
        Color fg = enabled ? Color.white : LumiWorldUITheme.Text;
        if (icon != null)
            icon.color = fg;
        if (label != null)
        {
            label.color = fg;
            label.text = text;
        }
    }

    private static void ClearChildren(RectTransform root)
    {
        if (root == null)
            return;
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }
}
