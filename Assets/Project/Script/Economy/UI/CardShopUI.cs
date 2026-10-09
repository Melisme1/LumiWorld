using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Cửa hàng thẻ trên màn chơi. Nút "Cửa hàng" nằm ngay dưới nút "Đơn hàng".
/// Bấm vào mở bảng: mỗi ô là một thẻ đang bán, kèm loại thẻ, phí đặt, số lá đang có trên tay, giá
/// và nút Mua (chỉ bật khi đủ Coins cho cả giá thẻ lẫn phí đặt). Thẻ mua xong vào thẳng bài trên tay.
/// Nhiều thẻ thì cuộn chuột để xem hết.
/// Bảng đang mở thì chặn chuột tới thẻ bài và bản đồ phía sau. Đóng bằng nút X, bấm ra ngoài bảng hoặc phím Esc.
/// EconomyHUD tự tạo script này, không cần gắn tay.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CardShopUI : MonoBehaviour
{
    // Kích thước tính bằng pixel màn hình (EconomyHUD bù Scale Factor của Canvas)
    private const float PanelPadding = 24f;
    private const float HeaderHeight = 84f;
    private const int Columns = 2;
    private const float TileWidth = 356f;
    private const float TileHeight = 146f;
    private const float TileSpacing = 12f;
    private const float ArtWidth = 92f;
    private const float ArtHeight = 118f;
    private const int VisibleRows = 3;
    private const float ScrollbarWidth = 8f;
    private const float ScrollbarGap = 10f;

    [Header("Nút mở cửa hàng")]
    [Tooltip("Góc trên phải của nút, tính từ góc trên phải màn hình (mặc định nằm ngay dưới nút Đơn hàng)")]
    [SerializeField] private Vector2 buttonCorner = new Vector2(-20f, -128f);
    [SerializeField] private Vector2 buttonSize = new Vector2(170f, 44f);

    [Header("Look")]
    [SerializeField] private Color hudColor = new Color(0.06f, 0.09f, 0.16f, 0.88f);
    [SerializeField] private Color backdropColor = new Color(0f, 0f, 0f, 0.45f);
    [SerializeField] private Color panelColor = new Color(0.08f, 0.11f, 0.19f, 0.97f);
    [SerializeField] private Color cardColor = new Color(0.13f, 0.17f, 0.27f, 1f);
    [SerializeField] private Color sectionColor = new Color(0.05f, 0.07f, 0.13f, 0.9f);
    [SerializeField] private Color textColor = new Color(0.93f, 0.95f, 0.99f, 1f);
    [SerializeField] private Color mutedColor = new Color(0.62f, 0.68f, 0.80f, 1f);
    [SerializeField] private Color enoughColor = new Color(0.45f, 0.95f, 0.55f, 1f);
    [SerializeField] private Color missingColor = new Color(1f, 0.62f, 0.45f, 1f);
    [SerializeField] private Color coinColor = new Color(1f, 0.84f, 0.32f, 1f);
    [SerializeField] private Color buyColor = new Color(0.24f, 0.47f, 0.85f, 1f);
    [SerializeField] private Color secondaryColor = new Color(0.26f, 0.30f, 0.42f, 1f);

    [Header("Màu nền thẻ chưa có ảnh, theo loại thẻ")]
    [SerializeField] private Color creatureColor = new Color(0.78f, 0.50f, 0.24f, 1f);
    [SerializeField] private Color terrainColor = new Color(0.30f, 0.62f, 0.36f, 1f);
    [SerializeField] private Color buildingColor = new Color(0.36f, 0.48f, 0.70f, 1f);
    [SerializeField] private Color specialColor = new Color(0.50f, 0.40f, 0.78f, 1f);

    private class TileView
    {
        public CardData card;
        public RectTransform rect;
        public TextMeshProUGUI owned;
        public TextMeshProUGUI price;
        public Button buy;
        public TextMeshProUGUI buyLabel;
        public RectTransform floatLayer;
        public Coroutine punch;
    }

    private CurrencyWallet wallet;

    private RectTransform openButton;
    private GameObject window;
    private RectTransform panel;
    private RectTransform balanceChip;
    private TextMeshProUGUI balanceText;
    private float panelScale = 1f;
    private Coroutine popRoutine;
    private Coroutine balancePunch;
    private readonly List<TileView> tiles = new List<TileView>();

    public bool IsOpen => window != null && window.activeSelf;

    private void Start()
    {
        // Lần đầu gọi Instance sẽ tạo ví và nạp số dư đã lưu
        wallet = CurrencyWallet.Instance;

        BuildOpenButton();
        BuildWindow();

        wallet.OnBalanceChanged += HandleBalanceChanged;
        ShowBalance();
    }

    private void Update()
    {
        if (!IsOpen) return;

    }

    private void OnDisable()
    {
        // Coroutine dừng khi object bị tắt: trả mọi thứ về kích thước chuẩn và bỏ số bay còn dở
        if (panel != null) panel.localScale = new Vector3(panelScale, panelScale, 1f);
        if (balanceChip != null) balanceChip.localScale = Vector3.one;

        foreach (TileView tile in tiles)
        {
            tile.punch = null;
            if (tile.rect != null) tile.rect.localScale = Vector3.one;
            EconomyHUD.ClearChildren(tile.floatLayer);
        }
    }

    private void OnDestroy()
    {
        if (wallet != null) wallet.OnBalanceChanged -= HandleBalanceChanged;
    }

    // =========================================================
    // MỞ VÀ ĐÓNG CỬA HÀNG
    // =========================================================

    public void Open()
    {
        if (window == null || IsOpen) return;

        // Lên trên cùng Canvas để phủ cả thẻ bài và thông báo đang hiện
        transform.SetAsLastSibling();
        window.SetActive(true);
        EconomyHUD.BringWindowToFront(window);

        // Màn hình nhỏ (ví dụ cửa sổ Game trong Editor) thì thu nhỏ bảng cho vừa
        Rect area = ((RectTransform)transform).rect;
        panelScale = area.width > 0f && area.height > 0f
            ? Mathf.Clamp(Mathf.Min((area.width - 32f) / panel.sizeDelta.x, (area.height - 32f) / panel.sizeDelta.y), 0.5f, 1f)
            : 1f;
        Refresh();

        if (popRoutine != null) StopCoroutine(popRoutine);
        popRoutine = StartCoroutine(PopIn());
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

    private IEnumerator PopIn()
    {
        const float duration = 0.14f;
        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            float scale = panelScale * Mathf.Lerp(0.92f, 1f, 1f - (1f - t) * (1f - t));
            panel.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        panel.localScale = new Vector3(panelScale, panelScale, 1f);
        popRoutine = null;
    }

    // =========================================================
    // CẬP NHẬT THEO DỮ LIỆU
    // =========================================================

    private void HandleBalanceChanged(int balance, int delta, CoinReason reason)
    {
        ShowBalance();
        if (!IsOpen || !isActiveAndEnabled) return;

        Punch(ref balancePunch, balanceChip, 1.1f);
        Refresh();
    }

    private void ShowBalance()
    {
        balanceText.text = FormatNumber(wallet.Balance);
    }

    private void Refresh()
    {
        if (!IsOpen) return;

        Dictionary<string, int> hand = CardShop.CountHand();
        foreach (TileView tile in tiles)
        {
            hand.TryGetValue(CardShop.CardKey(tile.card), out int owned);
            tile.owned.text = owned > 0 ? "In hand: " + owned : "Not in hand";

            // Phải đủ cả giá thẻ lẫn phí đặt; thiếu thì nút ghi rõ số Coins cần có
            int required = CardShop.GetRequiredCoins(tile.card);
            bool affordable = wallet.CanAfford(required);
            tile.price.color = affordable ? coinColor : missingColor;

            bool canBuy = CardShop.CanBuy(tile.card);
            tile.buy.interactable = canBuy;
            tile.buyLabel.color = canBuy ? Color.white : mutedColor;
            tile.buyLabel.text = affordable ? "Buy" : "Need " + FormatNumber(required) + " Coins";
        }
    }

    // =========================================================
    // NÚT BẤM
    // =========================================================

    private void Buy(TileView tile)
    {
        if (!CardShop.TryBuy(tile.card)) return;

        // Thẻ đã vào bài trên tay (kèm tiếng "ding" của bài thưởng); cập nhật số lá đang có
        Refresh();
        Punch(ref tile.punch, tile.rect, 1.05f);

        float x = TileWidth * 0.5f - 12f - 56f;
        StartCoroutine(EconomyHUD.FloatText(tile.floatLayer, "+1 card", enoughColor, 22f,
            new Vector2(x, 54f), new Vector2(x, 100f), 1f));
    }

    private void Punch(ref Coroutine routine, Transform target, float peak)
    {
        if (routine != null) StopCoroutine(routine);
        target.localScale = Vector3.one;
        routine = StartCoroutine(EconomyHUD.Punch(target, peak, 0.25f));
    }

    // =========================================================
    // DỰNG UI BẰNG CODE
    // =========================================================

    private void BuildOpenButton()
    {
        // Neo góc trên phải, pivot ở giữa để nút nảy quanh tâm
        openButton = EconomyHUD.CreateRect("ShopButton", transform);
        openButton.anchorMin = new Vector2(1f, 1f);
        openButton.anchorMax = new Vector2(1f, 1f);
        openButton.pivot = new Vector2(0.5f, 0.5f);
        openButton.sizeDelta = buttonSize;
        openButton.anchoredPosition = buttonCorner - buttonSize * 0.5f;

        EconomyHUD.AddButton(openButton, hudColor).onClick.AddListener(Toggle);
        CreateLabel(openButton, "Shop", 20f);
    }

    private void BuildWindow()
    {
        IReadOnlyList<CardData> cards = CardShop.Cards;
        int rows = Mathf.Max(1, (cards.Count + Columns - 1) / Columns);
        bool scrolls = rows > VisibleRows;

        float gridWidth = Columns * TileWidth + (Columns - 1) * TileSpacing;
        float gridHeight = rows * TileHeight + (rows - 1) * TileSpacing;

        // Nhiều thẻ thì chỉ hiện 3 hàng, hàng thứ tư ló ra một phần để người chơi biết là cuộn được
        float viewHeight = scrolls ? VisibleRows * (TileHeight + TileSpacing) + TileHeight * 0.4f : gridHeight;
        float scrollWidth = gridWidth + (scrolls ? ScrollbarGap + ScrollbarWidth : 0f);
        float panelWidth = PanelPadding * 2f + scrollWidth;
        float panelHeight = HeaderHeight + viewHeight + PanelPadding;

        RectTransform windowRect = EconomyHUD.CreateRect("Window", transform);
        EconomyHUD.Stretch(windowRect);
        window = windowRect.gameObject;
        EconomyHUD.RegisterWindow(window, Close);

        // Nền mờ phủ cả màn hình: chặn chuột tới thẻ bài và bản đồ, bấm vào thì đóng cửa hàng
        RectTransform backdropRect = EconomyHUD.CreateRect("Backdrop", windowRect);
        EconomyHUD.Stretch(backdropRect);
        Image backdrop = backdropRect.gameObject.AddComponent<Image>();
        backdrop.color = backdropColor;
        Button backdropButton = backdropRect.gameObject.AddComponent<Button>();
        backdropButton.transition = Selectable.Transition.None;
        backdropButton.navigation = new Navigation { mode = Navigation.Mode.None };
        backdropButton.onClick.AddListener(Close);

        // Bảng nằm giữa màn hình; nền bảng nhận chuột nên bấm bên trong không đóng bảng
        panel = EconomyHUD.CreateRect("Panel", windowRect);
        panel.sizeDelta = new Vector2(panelWidth, panelHeight);
        EconomyHUD.CreatePanel(panel, panelColor).raycastTarget = true;

        BuildHeader();

        RectTransform content = BuildScrollArea(new Vector2(scrollWidth, viewHeight), gridHeight, scrolls);
        for (int i = 0; i < cards.Count; i++)
        {
            tiles.Add(BuildTile(content, cards[i], i));
        }

        if (cards.Count == 0)
        {
            TextMeshProUGUI empty = EconomyHUD.CreateText("Empty", content, 17f, mutedColor, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(empty.rectTransform);
            empty.text = "The shop has no cards for sale yet";
        }

        window.SetActive(false);
    }

    private void BuildHeader()
    {
        TextMeshProUGUI title = EconomyHUD.CreateText("Title", panel, 26f, textColor, TextAlignmentOptions.Left);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(PanelPadding, -16f), new Vector2(420f, 36f));
        title.text = "Card Shop";

        TextMeshProUGUI subtitle = EconomyHUD.CreateText("Subtitle", panel, 15f, mutedColor, TextAlignmentOptions.Left);
        subtitle.fontStyle = FontStyles.Normal;
        Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(PanelPadding, -52f), new Vector2(500f, 22f));
        subtitle.text = "The price is paid once, the placement fee every time you place the card";

        // Nút đóng ở góc phải, số dư Coins nằm ngay bên trái nút
        RectTransform closeRect = EconomyHUD.CreateRect("CloseButton", panel);
        Place(closeRect, new Vector2(1f, 1f), new Vector2(-PanelPadding, -18f), new Vector2(44f, 40f));
        EconomyHUD.AddButton(closeRect, secondaryColor).onClick.AddListener(Close);
        CreateLabel(closeRect, "X", 20f);

        balanceChip = EconomyHUD.CreateRect("Balance", panel);
        balanceChip.anchorMin = new Vector2(1f, 1f);
        balanceChip.anchorMax = new Vector2(1f, 1f);
        balanceChip.pivot = new Vector2(0.5f, 0.5f);
        balanceChip.sizeDelta = new Vector2(150f, 40f);
        balanceChip.anchoredPosition = new Vector2(-(PanelPadding + 44f + 10f + 75f), -38f);
        EconomyHUD.CreatePanel(balanceChip, sectionColor);
        EconomyHUD.CreateIcon("CoinIcon", balanceChip, EconomyHUD.CoinSprite, 26f, 10f);

        balanceText = EconomyHUD.CreateText("Value", balanceChip, 20f, Color.white, TextAlignmentOptions.Right);
        RectTransform valueRect = balanceText.rectTransform;
        valueRect.anchorMin = Vector2.zero;
        valueRect.anchorMax = Vector2.one;
        valueRect.offsetMin = new Vector2(42f, 0f);
        valueRect.offsetMax = new Vector2(-12f, 0f);
    }

    /// <summary>
    /// Vùng cuộn dọc dưới tiêu đề: viewport cắt phần thừa, content chứa các ô thẻ, thanh cuộn khi thẻ nhiều hơn 3 hàng.
    /// Trả về content để đặt ô thẻ vào.
    /// </summary>
    private RectTransform BuildScrollArea(Vector2 size, float contentHeight, bool scrolls)
    {
        RectTransform scrollRect = EconomyHUD.CreateRect("Cards", panel);
        Place(scrollRect, new Vector2(0f, 1f), new Vector2(PanelPadding, -HeaderHeight), size);
        ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();

        RectTransform viewport = EconomyHUD.CreateRect("Viewport", scrollRect);
        EconomyHUD.Stretch(viewport);
        if (scrolls) viewport.offsetMax = new Vector2(-(ScrollbarGap + ScrollbarWidth), 0f);
        viewport.gameObject.AddComponent<RectMask2D>();

        // Ảnh trong suốt để lăn chuột ở khe giữa các ô vẫn cuộn được
        Image hitArea = viewport.gameObject.AddComponent<Image>();
        hitArea.color = Color.clear;

        RectTransform content = EconomyHUD.CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, contentHeight);
        content.anchoredPosition = Vector2.zero;

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 10f;

        if (scrolls) scroll.verticalScrollbar = BuildScrollbar(scrollRect);
        return content;
    }

    private Scrollbar BuildScrollbar(RectTransform parent)
    {
        RectTransform track = EconomyHUD.CreateRect("Scrollbar", parent);
        track.anchorMin = new Vector2(1f, 0f);
        track.anchorMax = new Vector2(1f, 1f);
        track.pivot = new Vector2(1f, 0.5f);
        track.sizeDelta = new Vector2(ScrollbarWidth, 0f);
        track.anchoredPosition = Vector2.zero;
        EconomyHUD.CreatePanel(track, sectionColor).raycastTarget = true;

        RectTransform slidingArea = EconomyHUD.CreateRect("SlidingArea", track);
        EconomyHUD.Stretch(slidingArea);

        RectTransform handle = EconomyHUD.CreateRect("Handle", slidingArea);
        EconomyHUD.Stretch(handle);
        Image handleImage = EconomyHUD.CreatePanel(handle, mutedColor);
        handleImage.raycastTarget = true;

        Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
        return scrollbar;
    }

    private TileView BuildTile(RectTransform content, CardData card, int index)
    {
        TileView tile = new TileView { card = card };
        int column = index % Columns;
        int row = index / Columns;

        // Pivot ở giữa để ô nảy quanh tâm
        tile.rect = EconomyHUD.CreateRect(card.name, content);
        tile.rect.anchorMin = new Vector2(0f, 1f);
        tile.rect.anchorMax = new Vector2(0f, 1f);
        tile.rect.pivot = new Vector2(0.5f, 0.5f);
        tile.rect.sizeDelta = new Vector2(TileWidth, TileHeight);
        tile.rect.anchoredPosition = new Vector2(
            column * (TileWidth + TileSpacing) + TileWidth * 0.5f,
            -(row * (TileHeight + TileSpacing) + TileHeight * 0.5f));
        EconomyHUD.CreatePanel(tile.rect, cardColor);

        // Ảnh thẻ bên trái. Thẻ chưa có ảnh: chữ cái đầu của tên trên nền màu theo loại thẻ
        RectTransform artRect = EconomyHUD.CreateRect("Art", tile.rect);
        Place(artRect, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(ArtWidth, ArtHeight));
        Image art = artRect.gameObject.AddComponent<Image>();
        art.raycastTarget = false;
        if (card.cardImage != null)
        {
            art.sprite = card.cardImage;
            art.preserveAspect = true;
        }
        else
        {
            art.sprite = EconomyHUD.RoundedSprite;
            art.type = Image.Type.Sliced;
            art.color = TypeColor(card.cardType);
            CreateLabel(artRect, Initials(CardShop.CardName(card)), 34f);
        }

        // Cột chữ bên phải ảnh: tên, loại thẻ và phí đặt, số lá đang có
        float textX = 12f + ArtWidth + 14f;
        float textWidth = TileWidth - textX - 12f;

        TextMeshProUGUI cardName = EconomyHUD.CreateText("Name", tile.rect, 19f, textColor, TextAlignmentOptions.Left);
        Place(cardName.rectTransform, new Vector2(0f, 1f), new Vector2(textX, -12f), new Vector2(textWidth, 26f));
        cardName.enableAutoSizing = true;
        cardName.fontSizeMin = 13f;
        cardName.fontSizeMax = 19f;
        cardName.text = CardShop.CardName(card);

        TextMeshProUGUI info = EconomyHUD.CreateText("Info", tile.rect, 13f, mutedColor, TextAlignmentOptions.Left);
        info.fontStyle = FontStyles.Normal;
        Place(info.rectTransform, new Vector2(0f, 1f), new Vector2(textX, -40f), new Vector2(textWidth, 18f));
        info.text = TypeName(card.cardType) + " · " + (card.placementFee > 0 ? "fee " + FormatNumber(card.placementFee) : "free to place");

        tile.owned = EconomyHUD.CreateText("Owned", tile.rect, 13f, mutedColor, TextAlignmentOptions.Left);
        tile.owned.fontStyle = FontStyles.Normal;
        Place(tile.owned.rectTransform, new Vector2(0f, 1f), new Vector2(textX, -60f), new Vector2(textWidth, 18f));

        // Giá: đồng xu và số Coins ở góc dưới cột chữ, nút Mua ở góc dưới bên phải
        RectTransform priceRect = EconomyHUD.CreateRect("Price", tile.rect);
        Place(priceRect, Vector2.zero, new Vector2(textX, 12f), new Vector2(textWidth - 124f, 40f));
        EconomyHUD.CreateIcon("CoinIcon", priceRect, EconomyHUD.CoinSprite, 24f, 0f);

        tile.price = EconomyHUD.CreateText("Value", priceRect, 21f, coinColor, TextAlignmentOptions.Left);
        RectTransform valueRect = tile.price.rectTransform;
        valueRect.anchorMin = Vector2.zero;
        valueRect.anchorMax = Vector2.one;
        valueRect.offsetMin = new Vector2(30f, 0f);
        valueRect.offsetMax = Vector2.zero;
        tile.price.text = FormatNumber(CardShop.GetPrice(card));

        RectTransform buyRect = EconomyHUD.CreateRect("BuyButton", tile.rect);
        Place(buyRect, new Vector2(1f, 0f), new Vector2(-12f, 12f), new Vector2(112f, 40f));
        tile.buy = EconomyHUD.AddButton(buyRect, buyColor);
        tile.buy.onClick.AddListener(() => Buy(tile));
        // Chữ tự thu nhỏ khi dài (ví dụ "Cần 1,250 Coins"), chừa lề hai bên nút
        tile.buyLabel = CreateLabel(buyRect, "Buy", 16f);
        tile.buyLabel.enableAutoSizing = true;
        tile.buyLabel.fontSizeMin = 11f;
        tile.buyLabel.fontSizeMax = 16f;
        tile.buyLabel.rectTransform.offsetMin = new Vector2(6f, 0f);
        tile.buyLabel.rectTransform.offsetMax = new Vector2(-6f, 0f);

        tile.floatLayer = EconomyHUD.CreateRect("FloatingNumbers", tile.rect);
        EconomyHUD.Stretch(tile.floatLayer);
        return tile;
    }

    private Color TypeColor(CardType type)
    {
        switch (type)
        {
            case CardType.Creature: return creatureColor;
            case CardType.Terrain: return terrainColor;
            case CardType.Building: return buildingColor;
            default: return specialColor;
        }
    }

    private static string TypeName(CardType type)
    {
        switch (type)
        {
            case CardType.Creature: return "Creature card";
            case CardType.Terrain: return "Terrain card";
            case CardType.Building: return "Building card";
            default: return "Special card";
        }
    }

    /// <summary>
    /// Đặt rect theo một góc (hoặc cạnh) của parent: anchor và pivot cùng ở điểm đó, position tính từ điểm đó.
    /// </summary>
    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    /// <summary>
    /// Chữ đậm màu trắng căn giữa, phủ kín parent (nhãn của nút, chữ trên ảnh thẻ).
    /// </summary>
    private static TextMeshProUGUI CreateLabel(RectTransform parent, string text, float fontSize)
    {
        TextMeshProUGUI label = EconomyHUD.CreateText("Label", parent, fontSize, Color.white, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(label.rectTransform);
        label.text = text;
        return label;
    }

    /// <summary>
    /// Chữ cái đầu của hai từ đầu tiên, ví dụ "Highland Cow" thành "HC".
    /// </summary>
    private static string Initials(string name)
    {
        string initials = string.Empty;
        foreach (string word in name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            initials += char.ToUpperInvariant(word[0]);
            if (initials.Length == 2) break;
        }
        return initials.Length > 0 ? initials : "?";
    }

    private static string FormatNumber(int value)
    {
        return value.ToString("#,0", CultureInfo.InvariantCulture);
    }
}
