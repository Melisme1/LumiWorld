using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Chợ listing trên màn chơi. Nút "Market" nằm dưới nút Shop, mở bảng có ba tab:
/// Buy (mua cả lô còn hạn, lọc theo tài nguyên, sắp theo giá mỗi đơn vị hoặc mới nhất),
/// Sell (đăng bán hàng trong kho, xem trước giá và phí), My Listings (xem và hủy lô của mình).
/// Chữ hiển thị tiếng Anh. EconomyHUD tự tạo script này, không cần gắn tay.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MarketUI : MonoBehaviour
{
    private enum Tab { Buy, Sell, Mine }

    private const int RowCount = 8;
    private const int MaxAmount = 999;

    // Kích thước tính bằng pixel màn hình (EconomyHUD bù Scale Factor của Canvas)
    private const float PanelWidth = 820f;
    private const float PanelHeight = 740f;
    private const float RowHeight = 46f;
    private const float RowGap = 8f;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    [Header("Nút mở bảng")]
    [Tooltip("Góc trên phải của nút, tính từ góc trên phải màn hình (mặc định nằm dưới nút Shop)")]
    [SerializeField] private Vector2 buttonCorner = new Vector2(-20f, -180f);
    [SerializeField] private Vector2 buttonSize = new Vector2(170f, 44f);

    [Header("Look")]
    [SerializeField] private Color hudColor = new Color(0.01f, 0.60f, 0.90f, 0.95f);
    [SerializeField] private Color backdropColor = new Color(0.04f, 0.08f, 0.14f, 0.65f);
    [SerializeField] private Color panelColor = new Color(0.85f, 0.91f, 0.96f, 0.98f);
    [SerializeField] private Color rowColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private Color sectionColor = new Color(0.92f, 0.96f, 1f, 0.95f);
    [SerializeField] private Color textColor = new Color(0.06f, 0.12f, 0.22f, 1f);
    [SerializeField] private Color mutedColor = new Color(0.38f, 0.46f, 0.58f, 1f);
    [SerializeField] private Color accentColor = new Color(0.01f, 0.60f, 0.90f, 1f);
    [SerializeField] private Color secondaryColor = new Color(0.84f, 0.90f, 0.96f, 1f);
    [SerializeField] private Color warningColor = new Color(0.90f, 0.35f, 0.25f, 1f);

    private sealed class Row
    {
        public GameObject root;
        public TextMeshProUGUI title;
        public TextMeshProUGUI detail;
        public Button action;
        public TextMeshProUGUI actionLabel;
        public MarketListingDto listing;
    }

    private GameObject window;
    private RectTransform panel;
    private RectTransform listRoot;
    private RectTransform buyGroup;
    private RectTransform mineGroup;
    private RectTransform sellGroup;
    private float panelScale = 1f;
    private Tab tab = Tab.Buy;

    // Mỗi lần tải đổi số này; trả lời của lần cũ bị bỏ qua
    private int requestId;

    private readonly List<Row> rows = new List<Row>();
    private readonly Button[] tabButtons = new Button[3];
    private ResourceInventory inventory;

    // Tab Buy
    private int buyResourceIndex = -1;
    private bool buyNewestFirst;
    private int buyPage = 1;
    private bool buyHasMore;
    private Button buyFilterButton;
    private Button sortButton;
    private Button prevButton;
    private Button nextButton;
    private TextMeshProUGUI buyFilterLabel;
    private TextMeshProUGUI sortLabel;
    private TextMeshProUGUI pageText;

    // Tab Sell
    private int sellResourceIndex;
    private TMP_InputField amountInput;
    private TMP_InputField priceInput;
    private Button sellResourceButton;
    private Button sellButton;
    private TextMeshProUGUI sellResourceLabel;
    private TextMeshProUGUI stockText;
    private TextMeshProUGUI sellHint;

    // Dòng trạng thái ở cuối bảng
    private TextMeshProUGUI statusText;
    private RectTransform balanceChip;
    private TextMeshProUGUI balanceText;

    public bool IsOpen => window != null && window.activeSelf;

    private void Awake()
    {
        EnsureEventSystem();
    }

    private void Start()
    {
        BuildOpenButton();
        BuildWindow();

        inventory = ResourceInventory.Instance;
        inventory.OnInventoryChanged += HandleInventoryChanged;
        if (CurrencyWallet.Instance != null) CurrencyWallet.Instance.OnBalanceChanged += HandleBalanceChanged;
        _ = MarketSync.Instance;
    }

    private void OnDestroy()
    {
        if (inventory != null) inventory.OnInventoryChanged -= HandleInventoryChanged;
        if (CurrencyWallet.Instance != null) CurrencyWallet.Instance.OnBalanceChanged -= HandleBalanceChanged;
    }

    private void HandleBalanceChanged(int balance, int delta, CoinReason reason) => RefreshBalance();

    private void RefreshBalance()
    {
        if (balanceText != null && CurrencyWallet.Instance != null)
            balanceText.text = Format(CurrencyWallet.Instance.Balance);
    }

    // =========================================================
    // MỞ / ĐÓNG
    // =========================================================

    public void Open()
    {
        if (window == null || IsOpen) return;

        // Lên trên cùng Canvas để phủ cả thẻ bài và thông báo đang hiện
        transform.SetAsLastSibling();
        window.SetActive(true);
        EconomyHUD.BringWindowToFront(window);

        // Màn hình nhỏ thì thu nhỏ bảng cho vừa
        Rect area = ((RectTransform)transform).rect;
        panelScale = area.width > 0f && area.height > 0f
            ? Mathf.Clamp(Mathf.Min((area.width - 32f) / PanelWidth, (area.height - 32f) / PanelHeight), 0.5f, 1f)
            : 1f;
        panel.localScale = new Vector3(panelScale, panelScale, 1f);

        RefreshBalance();
        ShowTab(tab);
    }

    public void Close()
    {
        if (!IsOpen) return;
        window.SetActive(false);
        EconomyHUD.MarkWindowClosed();
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    private void HandleInventoryChanged(string resourceId, int amount, int delta)
    {
        if (IsOpen && tab == Tab.Sell) UpdateSell();
    }

    // =========================================================
    // TAB
    // =========================================================

    private void ShowTab(Tab next)
    {
        tab = next;
        buyGroup.gameObject.SetActive(next == Tab.Buy);
        mineGroup.gameObject.SetActive(next == Tab.Mine);
        sellGroup.gameObject.SetActive(next == Tab.Sell);
        listRoot.gameObject.SetActive(next != Tab.Sell);

        Highlight(tabButtons[0], next == Tab.Buy);
        Highlight(tabButtons[1], next == Tab.Sell);
        Highlight(tabButtons[2], next == Tab.Mine);

        switch (next)
        {
            case Tab.Buy:
                LoadBuy();
                break;
            case Tab.Mine:
                LoadMine();
                break;
            default:
                SetStatus(string.Empty, false);
                UpdateSell();
                break;
        }
    }

    private void Highlight(Button button, bool active)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = active ? accentColor : secondaryColor;
        colors.highlightedColor = Color.Lerp(colors.normalColor, Color.white, 0.18f);
        button.colors = colors;
        var lbl = button.GetComponentInChildren<TextMeshProUGUI>();
        if (lbl != null) lbl.color = active ? Color.white : textColor;
    }

    // =========================================================
    // TAB BUY
    // =========================================================

    private void LoadBuy()
    {
        int id = ++requestId;
        SetStatus("Loading listings...", false);

        string resourceId = buyResourceIndex < 0 ? null : Catalog()[buyResourceIndex].resourceID;
        MarketSync.Browse(resourceId, buyNewestFirst, buyPage, RowCount,
            response =>
            {
                if (id != requestId || !IsOpen || tab != Tab.Buy) return;

                buyHasMore = response.hasMore;
                RenderRows(response.items, false);
                pageText.text = "Page " + buyPage;
                prevButton.interactable = buyPage > 1;
                nextButton.interactable = buyHasMore;
                SetStatus(response.items.Count == 0 ? "No lots for this filter yet." : string.Empty, false);
            },
            message =>
            {
                if (id != requestId || !IsOpen || tab != Tab.Buy) return;
                RenderRows(new List<MarketListingDto>(), false);
                SetStatus(message, true);
            });
    }

    private void CycleBuyResource()
    {
        int count = Catalog().Count;
        buyResourceIndex = buyResourceIndex + 1 >= count ? -1 : buyResourceIndex + 1;
        buyPage = 1;
        UpdateBuyLabels();
        LoadBuy();
    }

    private void ToggleSort()
    {
        buyNewestFirst = !buyNewestFirst;
        buyPage = 1;
        UpdateBuyLabels();
        LoadBuy();
    }

    private void UpdateBuyLabels()
    {
        buyFilterLabel.text = "Resource: " + (buyResourceIndex < 0 ? "All" : Catalog()[buyResourceIndex].resourceName) + "  >";
        sortLabel.text = "Sort: " + (buyNewestFirst ? "Newest" : "Price per unit") + "  >";
    }

    // =========================================================
    // TAB MINE
    // =========================================================

    private void LoadMine()
    {
        int id = ++requestId;
        SetStatus("Loading your lots...", false);

        MarketSync.Mine(
            response =>
            {
                if (id != requestId || !IsOpen || tab != Tab.Mine) return;
                RenderRows(response.items, true);
                SetStatus(response.items.Count == 0 ? "You have no active lots." : string.Empty, false);
            },
            message =>
            {
                if (id != requestId || !IsOpen || tab != Tab.Mine) return;
                RenderRows(new List<MarketListingDto>(), true);
                SetStatus(message, true);
            });
    }

    // =========================================================
    // TAB SELL
    // =========================================================

    private void CycleSellResource()
    {
        int count = Catalog().Count;
        if (count == 0) return;
        sellResourceIndex = (sellResourceIndex + 1) % count;
        UpdateSell();
    }

    private void UpdateSell()
    {
        IReadOnlyList<ResourceData> catalog = Catalog();
        if (catalog.Count == 0) return;

        ResourceData resource = catalog[sellResourceIndex];
        int stock = inventory.GetAmount(resource);
        sellResourceLabel.text = "Resource: " + resource.resourceName + "  >";
        stockText.text = "In stock: " + Format(stock);

        bool hasAmount = int.TryParse(amountInput.text, NumberStyles.Integer, Inv, out int amount);
        bool hasPrice = int.TryParse(priceInput.text, NumberStyles.Integer, Inv, out int price);

        if (!hasAmount || amount < 1 || amount > MaxAmount)
        {
            sellHint.text = "Enter an amount from 1 to " + MaxAmount + ".";
            sellHint.color = mutedColor;
            sellButton.interactable = false;
            return;
        }

        if (amount > stock)
        {
            sellHint.text = "You only have " + Format(stock) + " " + resource.resourceName + " in stock.";
            sellHint.color = warningColor;
            sellButton.interactable = false;
            return;
        }

        MarketSync.PriceRange(amount, resource.baseValue, out int minPrice, out int maxPrice);
        string range = "Allowed price for this lot: " + Format(minPrice) + " to " + Format(maxPrice) + " Coins.";

        if (!hasPrice || price < minPrice || price > maxPrice)
        {
            sellHint.text = range + (hasPrice ? " Adjust the price." : string.Empty);
            sellHint.color = hasPrice ? warningColor : mutedColor;
            sellButton.interactable = false;
            return;
        }

        int fee = MarketSync.Fee(price);
        sellHint.text = range + "\nFee " + MarketSync.FeePercent + "%: " + Format(fee) +
                        " Coins. You receive " + Format(price - fee) + " Coins.";
        sellHint.color = textColor;
        sellButton.interactable = true;
    }

    private void OnSellClicked()
    {
        IReadOnlyList<ResourceData> catalog = Catalog();
        if (catalog.Count == 0) return;

        ResourceData resource = catalog[sellResourceIndex];
        int.TryParse(amountInput.text, NumberStyles.Integer, Inv, out int amount);
        int.TryParse(priceInput.text, NumberStyles.Integer, Inv, out int price);

        // Chặn bấm lần hai cho tới khi server trả lời
        sellButton.interactable = false;
        MarketSync.Sell(resource.resourceID, amount, price, (ok, message) =>
        {
            SetStatus(message, !ok);
            if (ok)
            {
                priceInput.text = string.Empty;
                ShowTab(Tab.Mine);
            }
            else if (IsOpen && tab == Tab.Sell)
            {
                UpdateSell();
            }
        });
    }

    // =========================================================
    // DANH SÁCH DÙNG CHUNG (TAB BUY VÀ MINE)
    // =========================================================

    private void RenderRows(List<MarketListingDto> items, bool mine)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            bool has = items != null && i < items.Count;
            row.root.SetActive(has);
            row.listing = has ? items[i] : null;
            if (!has) continue;

            MarketListingDto lot = items[i];
            bool own = lot.sellerId == AuthSession.PlayerId;

            row.title.text = MarketSync.NameOf(lot.resourceId) + "  x" + Format(lot.amount);
            row.detail.text = Format(lot.priceCoins) + " Coins  (" + lot.pricePerUnit.ToString("0.0", Inv) + " each)" +
                              (mine ? string.Empty : "  by " + lot.sellerName);

            if (mine)
            {
                row.actionLabel.text = "Cancel";
                row.action.interactable = true;
            }
            else
            {
                row.actionLabel.text = own ? "Your lot" : "Buy";
                row.action.interactable = !own;
            }
        }
    }

    private void OnRowAction(Row row)
    {
        MarketListingDto lot = row.listing;
        if (lot == null) return;

        row.action.interactable = false;
        if (tab == Tab.Mine)
        {
            MarketSync.Cancel(lot.id, (ok, message) =>
            {
                SetStatus(message, !ok);
                if (IsOpen && tab == Tab.Mine) LoadMine();
            });
            return;
        }

        MarketSync.Buy(lot, (ok, message) =>
        {
            SetStatus(message, !ok);
            if (IsOpen && tab == Tab.Buy) LoadBuy();
        });
    }

    private void SetStatus(string message, bool warning)
    {
        if (statusText == null) return;
        statusText.text = message;
        statusText.color = warning ? warningColor : mutedColor;
    }

    private static IReadOnlyList<ResourceData> Catalog() => ResourceCatalog.All;

    private static string Format(int value) => value.ToString("N0", Inv);

    // =========================================================
    // DỰNG UI BẰNG CODE
    // =========================================================

    private void BuildOpenButton()
    {
        // Neo góc trên phải, pivot ở giữa để nút nảy quanh tâm
        RectTransform rect = EconomyHUD.CreateRect("MarketButton", transform);
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = buttonSize;
        rect.anchoredPosition = buttonCorner - buttonSize * 0.5f;

        EconomyHUD.AddButton(rect, hudColor).onClick.AddListener(Toggle);
        CreateLabel(rect, "Market", 20f);
    }

    private void BuildWindow()
    {
        RectTransform windowRect = EconomyHUD.CreateRect("Window", transform);
        EconomyHUD.Stretch(windowRect);
        window = windowRect.gameObject;
        EconomyHUD.RegisterWindow(window, Close);

        // Nền mờ phủ cả màn hình: bấm vào nền thì đóng bảng
        RectTransform backdropRect = EconomyHUD.CreateRect("Backdrop", windowRect);
        EconomyHUD.Stretch(backdropRect);
        backdropRect.gameObject.AddComponent<Image>().color = backdropColor;
        Button backdropButton = backdropRect.gameObject.AddComponent<Button>();
        backdropButton.transition = Selectable.Transition.None;
        backdropButton.navigation = new Navigation { mode = Navigation.Mode.None };
        backdropButton.onClick.AddListener(Close);

        // Bảng nằm giữa màn hình; nền bảng nhận chuột nên bấm bên trong không đóng bảng
        panel = EconomyHUD.CreateRect("Panel", windowRect);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panel.anchoredPosition = Vector2.zero;
        EconomyHUD.CreatePanel(panel, panelColor).raycastTarget = true;

        TextMeshProUGUI title = EconomyHUD.CreateText("Title", panel, 26f, textColor, TextAlignmentOptions.Left);
        Place(title.rectTransform, 28f, 20f, 400f, 40f);
        title.text = "Market";

        balanceChip = EconomyHUD.CreateRect("Balance", panel);
        Place(balanceChip, PanelWidth - 72f - 160f, 22f, 150f, 40f);
        EconomyHUD.CreateBorderedPanel(balanceChip, new Color(0.06f, 0.09f, 0.16f, 0.92f), new Color(0.08f, 0.58f, 0.95f, 0.60f));
        EconomyHUD.CreateIcon("CoinIcon", balanceChip, EconomyHUD.CoinSprite, 26f, 10f);

        balanceText = EconomyHUD.CreateText("Value", balanceChip, 20f, Color.white, TextAlignmentOptions.Right);
        balanceText.fontStyle = FontStyles.Bold;
        RectTransform valueRect = balanceText.rectTransform;
        valueRect.anchorMin = Vector2.zero;
        valueRect.anchorMax = Vector2.one;
        valueRect.offsetMin = new Vector2(42f, 0f);
        valueRect.offsetMax = new Vector2(-12f, 0f);

        Button closeButton = MakeButton("CloseButton", panel, PanelWidth - 72f, 20f, 44f, 44f, "X", secondaryColor);
        closeButton.onClick.AddListener(Close);

        tabButtons[0] = MakeButton("TabBuy", panel, 28f, 76f, 150f, 44f, "Buy", secondaryColor);
        tabButtons[0].onClick.AddListener(() => ShowTab(Tab.Buy));
        tabButtons[1] = MakeButton("TabSell", panel, 192f, 76f, 150f, 44f, "Sell", secondaryColor);
        tabButtons[1].onClick.AddListener(() => ShowTab(Tab.Sell));
        tabButtons[2] = MakeButton("TabMine", panel, 356f, 76f, 200f, 44f, "My Listings", secondaryColor);
        tabButtons[2].onClick.AddListener(() => ShowTab(Tab.Mine));

        statusText = EconomyHUD.CreateText("Status", panel, 16f, mutedColor, TextAlignmentOptions.Left);
        Place(statusText.rectTransform, 28f, 684f, 764f, 32f);

        BuildList();
        BuildBuyGroup();
        BuildMineGroup();
        BuildSellGroup();

        window.SetActive(false);
    }

    private void BuildList()
    {
        listRoot = EconomyHUD.CreateRect("Listings", panel);
        Place(listRoot, 28f, 196f, 764f, RowCount * (RowHeight + RowGap) - RowGap);

        for (int i = 0; i < RowCount; i++)
        {
            Row row = new Row();

            RectTransform rowRect = EconomyHUD.CreateRect("Row" + i, listRoot);
            Place(rowRect, 0f, i * (RowHeight + RowGap), 764f, RowHeight);
            EconomyHUD.CreateBorderedPanel(rowRect, rowColor);
            row.root = rowRect.gameObject;

            row.title = EconomyHUD.CreateText("Title", rowRect, 19f, textColor, TextAlignmentOptions.Left);
            Place(row.title.rectTransform, 16f, 0f, 320f, RowHeight);

            row.detail = EconomyHUD.CreateText("Detail", rowRect, 15f, mutedColor, TextAlignmentOptions.Left);
            Place(row.detail.rectTransform, 340f, 0f, 290f, RowHeight);

            RectTransform actionRect = EconomyHUD.CreateRect("Action", rowRect);
            Place(actionRect, 640f, 6f, 110f, 34f);
            row.action = EconomyHUD.AddButton(actionRect, accentColor);
            row.actionLabel = CreateLabel(actionRect, "Buy", 17f);

            Row captured = row;
            row.action.onClick.AddListener(() => OnRowAction(captured));
            rows.Add(row);
        }
    }

    private void BuildBuyGroup()
    {
        buyGroup = EconomyHUD.CreateRect("BuyControls", panel);
        EconomyHUD.Stretch(buyGroup);

        buyFilterButton = MakeButton("BuyFilter", buyGroup, 28f, 136f, 300f, 44f, "Resource: All", secondaryColor);
        buyFilterLabel = buyFilterButton.GetComponentInChildren<TextMeshProUGUI>();
        buyFilterButton.onClick.AddListener(CycleBuyResource);

        sortButton = MakeButton("Sort", buyGroup, 340f, 136f, 260f, 44f, "Sort: Price per unit", secondaryColor);
        sortLabel = sortButton.GetComponentInChildren<TextMeshProUGUI>();
        sortButton.onClick.AddListener(ToggleSort);

        Button refresh = MakeButton("RefreshBuy", buyGroup, 612f, 136f, 180f, 44f, "Refresh", secondaryColor);
        refresh.onClick.AddListener(LoadBuy);

        prevButton = MakeButton("Prev", buyGroup, 28f, 628f, 140f, 40f, "< Prev", secondaryColor);
        prevButton.onClick.AddListener(() => { buyPage = Math.Max(1, buyPage - 1); LoadBuy(); });

        pageText = EconomyHUD.CreateText("Page", buyGroup, 17f, textColor, TextAlignmentOptions.Center);
        Place(pageText.rectTransform, 180f, 628f, 460f, 40f);

        nextButton = MakeButton("Next", buyGroup, 652f, 628f, 140f, 40f, "Next >", secondaryColor);
        nextButton.onClick.AddListener(() => { if (buyHasMore) { buyPage++; LoadBuy(); } });

        UpdateBuyLabels();
    }

    private void BuildMineGroup()
    {
        mineGroup = EconomyHUD.CreateRect("MineControls", panel);
        EconomyHUD.Stretch(mineGroup);

        TextMeshProUGUI header = EconomyHUD.CreateText("Header", mineGroup, 17f, mutedColor, TextAlignmentOptions.Left);
        Place(header.rectTransform, 28f, 136f, 560f, 44f);
        header.text = "Your active lots (up to " + MarketSync.MaxActiveListings + " at a time). Cancelled goods return to your inventory.";
        header.enableWordWrapping = true;

        Button refresh = MakeButton("RefreshMine", mineGroup, 612f, 136f, 180f, 44f, "Refresh", secondaryColor);
        refresh.onClick.AddListener(LoadMine);
    }

    private void BuildSellGroup()
    {
        sellGroup = EconomyHUD.CreateRect("SellControls", panel);
        EconomyHUD.Stretch(sellGroup);

        sellResourceButton = MakeButton("SellResource", sellGroup, 28f, 136f, 380f, 48f, "Resource", secondaryColor);
        sellResourceLabel = sellResourceButton.GetComponentInChildren<TextMeshProUGUI>();
        sellResourceButton.onClick.AddListener(CycleSellResource);

        stockText = EconomyHUD.CreateText("Stock", sellGroup, 18f, textColor, TextAlignmentOptions.Left);
        Place(stockText.rectTransform, 424f, 136f, 368f, 48f);

        TextMeshProUGUI amountLabel = EconomyHUD.CreateText("AmountLabel", sellGroup, 16f, mutedColor, TextAlignmentOptions.Left);
        Place(amountLabel.rectTransform, 28f, 206f, 280f, 26f);
        amountLabel.text = "Amount (1 to " + MaxAmount + ")";

        TextMeshProUGUI priceLabel = EconomyHUD.CreateText("PriceLabel", sellGroup, 16f, mutedColor, TextAlignmentOptions.Left);
        Place(priceLabel.rectTransform, 320f, 206f, 460f, 26f);
        priceLabel.text = "Price for the whole lot (Coins)";

        amountInput = MakeInput("AmountInput", sellGroup, 28f, 236f, 260f, 48f, "1", TMP_InputField.ContentType.IntegerNumber, 3);
        priceInput = MakeInput("PriceInput", sellGroup, 320f, 236f, 260f, 48f, "e.g. 20", TMP_InputField.ContentType.IntegerNumber, 6);
        amountInput.text = "1";
        amountInput.onValueChanged.AddListener(_ => UpdateSell());
        priceInput.onValueChanged.AddListener(_ => UpdateSell());

        sellHint = EconomyHUD.CreateText("Hint", sellGroup, 17f, mutedColor, TextAlignmentOptions.Left);
        Place(sellHint.rectTransform, 28f, 300f, 764f, 70f);
        sellHint.enableWordWrapping = true;

        sellButton = MakeButton("SellButton", sellGroup, 28f, 384f, 320f, 56f, "List for sale", accentColor);
        sellButton.onClick.AddListener(OnSellClicked);
    }

    // Đặt rect theo góc trên trái của parent (x, y tính bằng pixel), dùng cho mọi phần tử của bảng
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);
    }

    private Button MakeButton(string objectName, RectTransform parent, float x, float y, float width, float height,
        string text, Color color)
    {
        RectTransform rect = EconomyHUD.CreateRect(objectName, parent);
        Place(rect, x, y, width, height);

        Button button = EconomyHUD.AddButton(rect, color);
        Color labelColor = color == secondaryColor ? textColor : Color.white;
        CreateLabel(rect, text, 18f, labelColor);
        return button;
    }

    private TMP_InputField MakeInput(string objectName, RectTransform parent, float x, float y, float width, float height,
        string placeholder, TMP_InputField.ContentType type, int characterLimit)
    {
        RectTransform rect = EconomyHUD.CreateRect(objectName, parent);
        Place(rect, x, y, width, height);

        Image background = EconomyHUD.CreateBorderedPanel(rect, sectionColor);
        background.raycastTarget = true;

        TextMeshProUGUI text = EconomyHUD.CreateText("Text", rect, 22f, textColor, TextAlignmentOptions.Left);
        EconomyHUD.Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(14f, 4f);
        text.rectTransform.offsetMax = new Vector2(-14f, -4f);

        TextMeshProUGUI hint = EconomyHUD.CreateText("Placeholder", rect, 22f, mutedColor, TextAlignmentOptions.Left);
        EconomyHUD.Stretch(hint.rectTransform);
        hint.rectTransform.offsetMin = new Vector2(14f, 4f);
        hint.rectTransform.offsetMax = new Vector2(-14f, -4f);
        hint.text = placeholder;

        TMP_InputField field = rect.gameObject.AddComponent<TMP_InputField>();
        field.targetGraphic = background;
        field.textComponent = text;
        field.placeholder = hint;
        field.contentType = type;
        field.characterLimit = characterLimit;
        field.lineType = TMP_InputField.LineType.SingleLine;
        return field;
    }

    private static TextMeshProUGUI CreateLabel(RectTransform parent, string text, float fontSize, Color? color = null)
    {
        Color c = color ?? Color.white;
        TextMeshProUGUI label = EconomyHUD.CreateText("Label", parent, fontSize, c, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(label.rectTransform);
        label.text = text;
        return label;
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;

        // Project chỉ dùng Input System mới, nên dùng InputSystemUIInputModule như OrderBoardUI
        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }
}
