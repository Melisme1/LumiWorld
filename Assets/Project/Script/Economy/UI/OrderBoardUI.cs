using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Bảng Đơn Hàng trên màn chơi. Nút "Đơn hàng" nằm ngay dưới số Coins, kèm huy hiệu xanh đếm số đơn đang giao được.
/// Bấm vào mở bảng: mỗi thẻ đơn hiện khách NPC, tài nguyên có/cần (7/10), thưởng Coins, nút Giao (chỉ bật khi đủ hàng)
/// và nút Bỏ. Bên dưới là quầy thương nhân mua tài nguyên dư theo giá sàn.
/// Bảng đang mở thì chặn chuột tới thẻ bài và bản đồ phía sau. Đóng bằng nút X, bấm ra ngoài bảng hoặc phím Esc.
/// EconomyHUD tự tạo script này, không cần gắn tay.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class OrderBoardUI : MonoBehaviour
{
    // Kích thước tính bằng pixel màn hình (EconomyHUD bù Scale Factor của Canvas)
    private const float PanelPadding = 24f;
    private const float HeaderHeight = 84f;
    private const float CardWidth = 236f;
    private const float CardHeight = 270f;
    private const float CardSpacing = 14f;
    private const float SectionGap = 16f;
    private const float MerchantHeight = 104f;
    private const int RequirementRows = 3;

    // Giao xong, ô có ngay đơn mới: trong khoảng này nút Giao của ô đó không nhận bấm,
    // để cú bấm thứ hai của một lần bấm đúp không giao luôn đơn mới khi người chơi chưa kịp xem
    private const float DeliverLockSeconds = 0.6f;

    [Header("Nút mở bảng")]
    [Tooltip("Góc trên phải của nút, tính từ góc trên phải màn hình (mặc định nằm ngay dưới CoinHUD)")]
    [SerializeField] private Vector2 buttonCorner = new Vector2(-20f, -76f);
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
    [SerializeField] private Color baselineColor = new Color(1f, 0.80f, 0.35f, 1f);
    [SerializeField] private Color deliverColor = new Color(0.20f, 0.66f, 0.38f, 1f);
    [SerializeField] private Color secondaryColor = new Color(0.26f, 0.30f, 0.42f, 1f);

    private class CardView
    {
        public int slot;
        public RectTransform rect;
        public GameObject orderContent;
        public Image portrait;
        public TextMeshProUGUI initials;
        public TextMeshProUGUI customer;
        public TextMeshProUGUI tag;
        public readonly List<RequirementRow> rows = new List<RequirementRow>();
        public TextMeshProUGUI reward;
        public Button deliver;
        public TextMeshProUGUI deliverLabel;
        public TextMeshProUGUI emptyText;
        public RectTransform floatLayer;
        public Coroutine punch;
        public float deliverLockedUntil;
    }

    private class RequirementRow
    {
        public GameObject root;
        public Image icon;
        public TextMeshProUGUI name;
        public TextMeshProUGUI count;
    }

    private class MerchantView
    {
        public ResourceData resource;
        public TextMeshProUGUI stock;
        public Button sellOne;
        public TextMeshProUGUI sellOneLabel;
        public Button sellTen;
        public TextMeshProUGUI sellTenLabel;
        public RectTransform floatLayer;
    }

    private OrderBoardSystem board;
    private ResourceInventory inventory;
    private CurrencyWallet wallet;

    private RectTransform openButton;
    private GameObject badge;
    private TextMeshProUGUI badgeText;
    private int shownReadyCount;
    private Coroutine buttonPunch;

    private GameObject window;
    private RectTransform panel;
    private RectTransform balanceChip;
    private TextMeshProUGUI balanceText;
    private float panelScale = 1f;
    private Coroutine popRoutine;
    private Coroutine balancePunch;
    private readonly List<CardView> cards = new List<CardView>();
    private readonly List<MerchantView> merchant = new List<MerchantView>();
    private float countdownTimer;

    public bool IsOpen => window != null && window.activeSelf;

    private void Awake()
    {
        EnsureEventSystem();
    }

    private void Start()
    {
        // Lần đầu gọi Instance sẽ tạo bảng đơn, ví và kho rồi nạp dữ liệu đã lưu
        board = OrderBoardSystem.Instance;
        inventory = ResourceInventory.Instance;
        wallet = CurrencyWallet.Instance;

        BuildOpenButton();
        BuildWindow();

        board.OnBoardChanged += Refresh;
        inventory.OnInventoryChanged += HandleInventoryChanged;
        wallet.OnBalanceChanged += HandleBalanceChanged;

        ShowBalance();
        Refresh();
    }

    private void Update()
    {
        if (!IsOpen) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        // Cập nhật đồng hồ đếm ngược của các ô trống
        countdownTimer -= Time.unscaledDeltaTime;
        if (countdownTimer > 0f) return;

        countdownTimer = 0.25f;
        foreach (CardView card in cards)
        {
            if (board.GetOrder(card.slot) == null) ShowEmptyCard(card);
        }
    }

    private void OnDisable()
    {
        // Coroutine dừng khi object bị tắt: trả mọi thứ về kích thước chuẩn và bỏ số bay còn dở
        if (openButton != null) openButton.localScale = Vector3.one;
        if (panel != null) panel.localScale = new Vector3(panelScale, panelScale, 1f);
        if (balanceChip != null) balanceChip.localScale = Vector3.one;

        foreach (CardView card in cards)
        {
            card.punch = null;
            if (card.rect != null) card.rect.localScale = Vector3.one;
            EconomyHUD.ClearChildren(card.floatLayer);
        }

        foreach (MerchantView view in merchant)
        {
            EconomyHUD.ClearChildren(view.floatLayer);
        }
    }

    private void OnDestroy()
    {
        if (board != null) board.OnBoardChanged -= Refresh;
        if (inventory != null) inventory.OnInventoryChanged -= HandleInventoryChanged;
        if (wallet != null) wallet.OnBalanceChanged -= HandleBalanceChanged;
    }

    // =========================================================
    // MỞ VÀ ĐÓNG BẢNG
    // =========================================================

    public void Open()
    {
        if (window == null || IsOpen) return;

        // Lên trên cùng Canvas để phủ cả thẻ bài và thông báo đang hiện
        transform.SetAsLastSibling();
        window.SetActive(true);

        // Màn hình nhỏ (ví dụ cửa sổ Game trong Editor) thì thu nhỏ bảng cho vừa
        Rect area = ((RectTransform)transform).rect;
        panelScale = area.width > 0f && area.height > 0f
            ? Mathf.Clamp(Mathf.Min((area.width - 32f) / panel.sizeDelta.x, (area.height - 32f) / panel.sizeDelta.y), 0.5f, 1f)
            : 1f;
        countdownTimer = 0f;
        Refresh();

        if (popRoutine != null) StopCoroutine(popRoutine);
        popRoutine = StartCoroutine(PopIn());
    }

    public void Close()
    {
        if (!IsOpen) return;
        window.SetActive(false);
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

    private void HandleInventoryChanged(string resourceId, int amount, int delta)
    {
        Refresh();
    }

    private void HandleBalanceChanged(int balance, int delta, CoinReason reason)
    {
        ShowBalance();
        if (IsOpen && isActiveAndEnabled) Punch(ref balancePunch, balanceChip, 1.1f);
    }

    private void ShowBalance()
    {
        balanceText.text = FormatNumber(wallet.Balance);
    }

    private void Refresh()
    {
        if (board == null) return;

        RefreshBadge();
        if (!IsOpen) return;

        foreach (CardView card in cards)
        {
            RefreshCard(card);
        }
        RefreshMerchant();
    }

    private void RefreshBadge()
    {
        int ready = board.CountFulfillable();
        badge.SetActive(ready > 0);
        badgeText.text = ready.ToString(CultureInfo.InvariantCulture);

        // Có thêm đơn giao được: nút nảy nhẹ để người chơi để ý
        if (ready > shownReadyCount && isActiveAndEnabled) Punch(ref buttonPunch, openButton, 1.12f);
        shownReadyCount = ready;
    }

    private void RefreshCard(CardView card)
    {
        ActiveOrder order = board.GetOrder(card.slot);
        card.orderContent.SetActive(order != null);
        card.emptyText.gameObject.SetActive(order == null);
        if (order == null)
        {
            ShowEmptyCard(card);
            return;
        }

        OrderTemplate template = board.FindTemplate(order.templateId);
        string customer = template != null && !string.IsNullOrEmpty(template.customerName) ? template.customerName : "Khách hàng";
        card.customer.text = customer;

        // Mẫu đơn chưa có ảnh khách: chữ cái đầu của tên trên nền tròn màu portraitColor
        bool hasPortrait = template != null && template.portrait != null;
        card.portrait.sprite = hasPortrait ? template.portrait : EconomyHUD.CircleSprite;
        card.portrait.color = hasPortrait ? Color.white : template != null ? template.portraitColor : secondaryColor;
        card.initials.gameObject.SetActive(!hasPortrait);
        card.initials.text = Initials(customer);

        bool baseline = card.slot == OrderBoardSystem.BaselineSlot;
        card.tag.text = baseline ? "Đơn cơ bản" : order.requirements.Count > 1 ? "Đơn nhiều loại" : "Đơn thường";
        card.tag.color = baseline ? baselineColor : mutedColor;

        for (int i = 0; i < card.rows.Count; i++)
        {
            RequirementRow row = card.rows[i];
            bool used = i < order.requirements.Count;
            row.root.SetActive(used);
            if (!used) continue;

            ResourceStack need = order.requirements[i];
            ResourceData resource = ResourceCatalog.Find(need.resourceId);
            int have = inventory.GetAmount(need.resourceId);

            row.icon.sprite = BiomeHarvestIndicator.GetResourceSprite(resource);
            row.name.text = resource != null ? resource.resourceName : need.resourceId;
            row.count.text = have + "/" + need.amount;
            row.count.color = have >= need.amount ? enoughColor : missingColor;
        }

        card.reward.text = FormatNumber(order.rewardCoins);

        bool ready = board.CanFulfill(card.slot);
        SetButton(card.deliver, card.deliverLabel, ready);
        card.deliverLabel.text = ready ? "Giao" : "Chưa đủ hàng";
    }

    private void ShowEmptyCard(CardView card)
    {
        TimeSpan wait = board.GetTimeUntilRefill(card.slot);
        if (wait > TimeSpan.Zero)
        {
            card.emptyText.text = "Đơn mới sau\n<size=34><color=#FFFFFF>" + FormatWait(wait) + "</color></size>";
        }
        else if (board.IsWaitingForResources(card.slot))
        {
            card.emptyText.text = "Chưa có đơn\n<size=14>Đặt thú lên vùng đất để làm ra tài nguyên, khách sẽ tới đặt hàng.</size>";
        }
        else
        {
            card.emptyText.text = "Khách sắp tới...";
        }
    }

    private void RefreshMerchant()
    {
        foreach (MerchantView view in merchant)
        {
            int stock = inventory.GetAmount(view.resource);
            view.stock.text = stock.ToString(CultureInfo.InvariantCulture);
            SetButton(view.sellOne, view.sellOneLabel, stock >= 1);
            SetButton(view.sellTen, view.sellTenLabel, stock >= 10);
        }
    }

    private void SetButton(Button button, TextMeshProUGUI label, bool on)
    {
        button.interactable = on;
        label.color = on ? Color.white : mutedColor;
    }

    // =========================================================
    // NÚT BẤM
    // =========================================================

    private void Deliver(CardView card)
    {
        if (Time.unscaledTime < card.deliverLockedUntil) return;

        ActiveOrder order = board.GetOrder(card.slot);
        if (order == null) return;

        int reward = order.rewardCoins;
        if (!board.TryFulfill(card.slot)) return;

        card.deliverLockedUntil = Time.unscaledTime + DeliverLockSeconds;

        // Thẻ đã chuyển sang đơn mới (qua OnBoardChanged); thêm hiệu ứng nhận thưởng
        Punch(ref card.punch, card.rect, 1.06f);
        StartCoroutine(EconomyHUD.FloatText(card.floatLayer, "+" + FormatNumber(reward) + " Coins", coinColor, 26f,
            new Vector2(0f, 64f), new Vector2(0f, 124f), 1.2f));
    }

    private void Discard(CardView card)
    {
        if (board.Discard(card.slot)) Punch(ref card.punch, card.rect, 0.95f);
    }

    private void Sell(MerchantView view, int amount)
    {
        int coins = amount * NpcMerchant.GetUnitPrice(view.resource);
        if (!NpcMerchant.TrySell(view.resource, amount)) return;

        // Số bay mọc lên từ chỗ hai nút bán
        float x = view.floatLayer.rect.width * 0.5f - 60f;
        StartCoroutine(EconomyHUD.FloatText(view.floatLayer, "+" + FormatNumber(coins), coinColor, 20f,
            new Vector2(x, 34f), new Vector2(x, 72f), 0.9f));
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
        openButton = EconomyHUD.CreateRect("OrdersButton", transform);
        openButton.anchorMin = new Vector2(1f, 1f);
        openButton.anchorMax = new Vector2(1f, 1f);
        openButton.pivot = new Vector2(0.5f, 0.5f);
        openButton.sizeDelta = buttonSize;
        openButton.anchoredPosition = buttonCorner - buttonSize * 0.5f;

        EconomyHUD.AddButton(openButton, hudColor).onClick.AddListener(Toggle);
        CreateLabel(openButton, "Đơn hàng", 20f);

        // Huy hiệu xanh ở góc trái: số đơn đang đủ hàng để giao
        RectTransform badgeRect = EconomyHUD.CreateRect("ReadyBadge", openButton);
        badgeRect.anchorMin = new Vector2(0f, 1f);
        badgeRect.anchorMax = new Vector2(0f, 1f);
        badgeRect.pivot = new Vector2(0.5f, 0.5f);
        badgeRect.sizeDelta = new Vector2(26f, 26f);
        badgeRect.anchoredPosition = new Vector2(4f, -4f);

        Image badgeImage = badgeRect.gameObject.AddComponent<Image>();
        badgeImage.sprite = EconomyHUD.CircleSprite;
        badgeImage.color = deliverColor;
        badgeImage.raycastTarget = false;

        badgeText = CreateLabel(badgeRect, "0", 15f);
        badge = badgeRect.gameObject;
        badge.SetActive(false);
    }

    private void BuildWindow()
    {
        int slots = board.SlotCount;
        float panelWidth = PanelPadding * 2f + slots * CardWidth + (slots - 1) * CardSpacing;
        float panelHeight = HeaderHeight + CardHeight + SectionGap + MerchantHeight + PanelPadding;

        RectTransform windowRect = EconomyHUD.CreateRect("Window", transform);
        EconomyHUD.Stretch(windowRect);
        window = windowRect.gameObject;
        EconomyHUD.RegisterWindow(window);

        // Nền mờ phủ cả màn hình: chặn chuột tới thẻ bài và bản đồ, bấm vào thì đóng bảng
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
        for (int slot = 0; slot < slots; slot++)
        {
            cards.Add(BuildCard(slot));
        }
        BuildMerchant(panelWidth - PanelPadding * 2f);

        window.SetActive(false);
    }

    private void BuildHeader()
    {
        TextMeshProUGUI title = EconomyHUD.CreateText("Title", panel, 26f, textColor, TextAlignmentOptions.Left);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(PanelPadding, -16f), new Vector2(420f, 36f));
        title.text = "Bảng Đơn Hàng";

        TextMeshProUGUI subtitle = EconomyHUD.CreateText("Subtitle", panel, 15f, mutedColor, TextAlignmentOptions.Left);
        subtitle.fontStyle = FontStyles.Normal;
        Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(PanelPadding, -52f), new Vector2(460f, 22f));
        subtitle.text = "Giao tài nguyên cho khách để nhận Coins";

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

    private CardView BuildCard(int slot)
    {
        CardView card = new CardView { slot = slot };
        bool baseline = slot == OrderBoardSystem.BaselineSlot;

        // Pivot ở giữa để thẻ nảy quanh tâm
        card.rect = EconomyHUD.CreateRect("OrderCard" + (slot + 1), panel);
        card.rect.anchorMin = new Vector2(0f, 1f);
        card.rect.anchorMax = new Vector2(0f, 1f);
        card.rect.pivot = new Vector2(0.5f, 0.5f);
        card.rect.sizeDelta = new Vector2(CardWidth, CardHeight);
        card.rect.anchoredPosition = new Vector2(
            PanelPadding + slot * (CardWidth + CardSpacing) + CardWidth * 0.5f,
            -HeaderHeight - CardHeight * 0.5f);
        EconomyHUD.CreatePanel(card.rect, cardColor);

        RectTransform content = EconomyHUD.CreateRect("Order", card.rect);
        EconomyHUD.Stretch(content);
        card.orderContent = content.gameObject;

        // Khách NPC: ảnh (hoặc chữ cái đầu trên nền tròn), tên và loại đơn
        RectTransform portraitRect = EconomyHUD.CreateRect("Portrait", content);
        Place(portraitRect, new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(48f, 48f));
        card.portrait = portraitRect.gameObject.AddComponent<Image>();
        card.portrait.preserveAspect = true;
        card.portrait.raycastTarget = false;
        card.initials = CreateLabel(portraitRect, "", 19f);

        card.customer = EconomyHUD.CreateText("Customer", content, 18f, textColor, TextAlignmentOptions.Left);
        Place(card.customer.rectTransform, new Vector2(0f, 1f), new Vector2(70f, -12f), new Vector2(CardWidth - 82f, 26f));
        card.customer.enableAutoSizing = true;
        card.customer.fontSizeMin = 12f;
        card.customer.fontSizeMax = 18f;

        card.tag = EconomyHUD.CreateText("Tag", content, 13f, mutedColor, TextAlignmentOptions.Left);
        Place(card.tag.rectTransform, new Vector2(0f, 1f), new Vector2(70f, -40f), new Vector2(CardWidth - 82f, 20f));

        // Mỗi dòng yêu cầu: icon, tên tài nguyên, số có/cần
        for (int i = 0; i < RequirementRows; i++)
        {
            RectTransform rowRect = EconomyHUD.CreateRect("Requirement" + (i + 1), content);
            Place(rowRect, new Vector2(0f, 1f), new Vector2(12f, -72f - i * 34f), new Vector2(CardWidth - 24f, 30f));

            RequirementRow row = new RequirementRow { root = rowRect.gameObject };
            row.icon = EconomyHUD.CreateIcon("Icon", rowRect, null, 26f, 0f);

            row.name = EconomyHUD.CreateText("Name", rowRect, 16f, textColor, TextAlignmentOptions.Left);
            row.name.fontStyle = FontStyles.Normal;
            RectTransform nameRect = row.name.rectTransform;
            nameRect.anchorMin = Vector2.zero;
            nameRect.anchorMax = Vector2.one;
            nameRect.offsetMin = new Vector2(34f, 0f);
            nameRect.offsetMax = new Vector2(-76f, 0f);

            row.count = EconomyHUD.CreateText("Count", rowRect, 18f, enoughColor, TextAlignmentOptions.Right);
            RectTransform countRect = row.count.rectTransform;
            countRect.anchorMin = new Vector2(1f, 0f);
            countRect.anchorMax = new Vector2(1f, 1f);
            countRect.pivot = new Vector2(1f, 0.5f);
            countRect.sizeDelta = new Vector2(76f, 0f);
            countRect.anchoredPosition = Vector2.zero;

            card.rows.Add(row);
        }

        // Thưởng: chữ "Thưởng" bên trái, số Coins và đồng xu bên phải
        RectTransform rewardRect = EconomyHUD.CreateRect("Reward", content);
        Place(rewardRect, new Vector2(0f, 1f), new Vector2(12f, -176f), new Vector2(CardWidth - 24f, 30f));

        TextMeshProUGUI rewardLabel = EconomyHUD.CreateText("Label", rewardRect, 15f, mutedColor, TextAlignmentOptions.Left);
        rewardLabel.fontStyle = FontStyles.Normal;
        EconomyHUD.Stretch(rewardLabel.rectTransform);
        rewardLabel.text = "Thưởng";

        RectTransform coinRect = EconomyHUD.CreateRect("CoinIcon", rewardRect);
        Place(coinRect, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
        Image coin = coinRect.gameObject.AddComponent<Image>();
        coin.sprite = EconomyHUD.CoinSprite;
        coin.raycastTarget = false;

        card.reward = EconomyHUD.CreateText("Value", rewardRect, 22f, coinColor, TextAlignmentOptions.Right);
        RectTransform valueRect = card.reward.rectTransform;
        valueRect.anchorMin = new Vector2(1f, 0f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.pivot = new Vector2(1f, 0.5f);
        valueRect.sizeDelta = new Vector2(100f, 0f);
        valueRect.anchoredPosition = new Vector2(-30f, 0f);

        // Nút Giao, và nút Bỏ cho mọi ô trừ đơn cơ bản
        const float discardWidth = 64f;
        RectTransform deliverRect = EconomyHUD.CreateRect("DeliverButton", content);
        float deliverWidth = CardWidth - 24f - (baseline ? 0f : discardWidth + 8f);
        Place(deliverRect, Vector2.zero, new Vector2(12f, 12f), new Vector2(deliverWidth, 42f));
        card.deliver = EconomyHUD.AddButton(deliverRect, deliverColor);
        card.deliver.onClick.AddListener(() => Deliver(card));
        card.deliverLabel = CreateLabel(deliverRect, "Giao", 18f);

        if (!baseline)
        {
            RectTransform discardRect = EconomyHUD.CreateRect("DiscardButton", content);
            Place(discardRect, new Vector2(1f, 0f), new Vector2(-12f, 12f), new Vector2(discardWidth, 42f));
            EconomyHUD.AddButton(discardRect, secondaryColor).onClick.AddListener(() => Discard(card));
            CreateLabel(discardRect, "Bỏ", 17f);
        }

        // Ô trống: đếm ngược tới đơn mới, hoặc hướng dẫn cách có đơn
        card.emptyText = EconomyHUD.CreateText("Empty", card.rect, 17f, mutedColor, TextAlignmentOptions.Center);
        RectTransform emptyRect = card.emptyText.rectTransform;
        emptyRect.anchorMin = Vector2.zero;
        emptyRect.anchorMax = Vector2.one;
        emptyRect.offsetMin = new Vector2(18f, 18f);
        emptyRect.offsetMax = new Vector2(-18f, -18f);

        card.floatLayer = EconomyHUD.CreateRect("FloatingNumbers", card.rect);
        EconomyHUD.Stretch(card.floatLayer);
        return card;
    }

    private void BuildMerchant(float width)
    {
        RectTransform section = EconomyHUD.CreateRect("Merchant", panel);
        Place(section, Vector2.zero, new Vector2(PanelPadding, PanelPadding), new Vector2(width, MerchantHeight));
        EconomyHUD.CreatePanel(section, sectionColor);

        TextMeshProUGUI title = EconomyHUD.CreateText("Title", section, 16f, textColor, TextAlignmentOptions.Left);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(width * 0.5f, 24f));
        title.text = "Thương nhân mua tài nguyên dư";

        TextMeshProUGUI hint = EconomyHUD.CreateText("Hint", section, 13f, mutedColor, TextAlignmentOptions.Right);
        hint.fontStyle = FontStyles.Normal;
        Place(hint.rectTransform, new Vector2(1f, 1f), new Vector2(-16f, -12f), new Vector2(width * 0.5f - 24f, 22f));
        hint.text = "Giá sàn, giao đơn được nhiều Coins hơn";

        IReadOnlyList<ResourceData> resources = ResourceCatalog.All;
        if (resources.Count == 0)
        {
            section.gameObject.SetActive(false);
            return;
        }

        const float spacing = 12f;
        float columnWidth = (width - 32f - spacing * (resources.Count - 1)) / resources.Count;

        for (int i = 0; i < resources.Count; i++)
        {
            MerchantView view = new MerchantView { resource = resources[i] };

            RectTransform column = EconomyHUD.CreateRect(view.resource.resourceID, section);
            Place(column, Vector2.zero, new Vector2(16f + i * (columnWidth + spacing), 12f), new Vector2(columnWidth, 52f));
            EconomyHUD.CreateIcon("Icon", column, BiomeHarvestIndicator.GetResourceSprite(view.resource), 32f, 0f);

            view.stock = EconomyHUD.CreateText("Stock", column, 20f, textColor, TextAlignmentOptions.Left);
            Place(view.stock.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -2f), new Vector2(64f, 26f));

            TextMeshProUGUI price = EconomyHUD.CreateText("Price", column, 13f, mutedColor, TextAlignmentOptions.Left);
            price.fontStyle = FontStyles.Normal;
            Place(price.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -28f), new Vector2(64f, 20f));
            price.text = "Giá " + NpcMerchant.GetUnitPrice(view.resource);

            RectTransform tenRect = EconomyHUD.CreateRect("SellTenButton", column);
            Place(tenRect, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(62f, 36f));
            view.sellTen = EconomyHUD.AddButton(tenRect, secondaryColor);
            view.sellTen.onClick.AddListener(() => Sell(view, 10));
            view.sellTenLabel = CreateLabel(tenRect, "Bán 10", 15f);

            RectTransform oneRect = EconomyHUD.CreateRect("SellOneButton", column);
            Place(oneRect, new Vector2(1f, 0.5f), new Vector2(-68f, 0f), new Vector2(54f, 36f));
            view.sellOne = EconomyHUD.AddButton(oneRect, secondaryColor);
            view.sellOne.onClick.AddListener(() => Sell(view, 1));
            view.sellOneLabel = CreateLabel(oneRect, "Bán 1", 15f);

            view.floatLayer = EconomyHUD.CreateRect("FloatingNumbers", column);
            EconomyHUD.Stretch(view.floatLayer);
            merchant.Add(view);
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
    /// Chữ đậm màu trắng căn giữa, phủ kín parent (nhãn của nút, chữ trong huy hiệu).
    /// </summary>
    private static TextMeshProUGUI CreateLabel(RectTransform parent, string text, float fontSize)
    {
        TextMeshProUGUI label = EconomyHUD.CreateText("Label", parent, fontSize, Color.white, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(label.rectTransform);
        label.text = text;
        return label;
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;

        // Project chỉ dùng Input System mới: StandaloneInputModule mặc định sẽ báo lỗi mỗi frame,
        // nên dùng InputSystemUIInputModule và gán sẵn action giống AuthScreen
        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    /// <summary>
    /// Chữ cái đầu của hai từ đầu tiên, ví dụ "Thợ mộc làng" thành "TM".
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

    private static string FormatWait(TimeSpan wait)
    {
        int seconds = Mathf.CeilToInt((float)wait.TotalSeconds);
        return (seconds / 60) + ":" + (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
    }

    private static string FormatNumber(int value)
    {
        return value.ToString("#,0", CultureInfo.InvariantCulture);
    }
}
