using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using LumiWorld.Acs;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// P-10 Orders & P-11 Sell Resources — LumiWorld UI Production Pack:
/// - Header icon + "Orders" + Coin balance + [×] (no LumiWorld logo).
/// - Two sub-tabs: Orders (P-10) | Sell resources (P-11).
/// - P-10 Orders:
///   + Left list shows NPC portraits (N-01..N-12 deterministic per template/order), order type tag,
///     resource requirements with clamped green/red bordered min(owned, req)/req fraction pills (NO "Ready/Short" text labels).
///   + Right inspector shows selected customer, large resource art + name + fraction (NO "owned" count), Reward coins, and Deliver button.
/// - P-11 Sell resources:
///   + Left list shows stocked/catalog resources with coin price (NO "coins each" text word).
///   + Right detail sidebar shows art + name + unit price + quantity stepper + Total received + direct Sell CTA (NO "owned" on right, NO "Remaining", NO P-16 popup),
///     and a "← Back" button that collapses the sidebar so the list widens cleanly.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class OrderBoardUI : MonoBehaviour
{
    private enum ModeTab
    {
        Orders,
        SellResources
    }

    private const float PanelWidth = 880f;
    private const float PanelHeight = 560f;
    private const float LeftPaneWidth = 532f;
    private const float RightPaneWidth = 290f;
    private const float DeliverLockSeconds = 0.6f;

    [Header("Legacy / Scene References")]
    [SerializeField] private Vector2 buttonCorner = new Vector2(-20f, -76f);
    [SerializeField] private Vector2 buttonSize = new Vector2(170f, 44f);

    private class OrderRowView
    {
        public int slot;
        public RectTransform rect;
        public Image selectionBorder;
        public GameObject activeContent;
        public Image portrait;
        public TextMeshProUGUI customerName;
        public TextMeshProUGUI tagText;
        public readonly List<ReqPillView> reqRows = new List<ReqPillView>();
        public TextMeshProUGUI rewardText;
        public Button skipButton;
        public GameObject emptyContent;
        public TextMeshProUGUI emptyText;
        public RectTransform floatLayer;
        public Coroutine punch;
        public float deliverLockedUntil;
    }

    private class ReqPillView
    {
        public GameObject root;
        public Image icon;
        public TextMeshProUGUI name;
        public Image pillBg;
        public TextMeshProUGUI count;
    }

    private class SellRowView
    {
        public ResourceData resource;
        public RectTransform rect;
        public Image selectionBorder;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI qtyText;
        public TextMeshProUGUI priceText;
        public RectTransform floatLayer;
    }

    private OrderBoardSystem board;
    private ResourceInventory inventory;

    private RectTransform openButton;
    private GameObject badge;
    private TextMeshProUGUI badgeText;

    private GameObject window;
    private RectTransform stageRect;
    private RectTransform panel;

    private Button ordersTabBtn;
    private Image ordersTabBg;
    private Image ordersTabIcon;
    private TextMeshProUGUI ordersTabLabel;
    private Shadow ordersTabShadow;

    private Button sellTabBtn;
    private Image sellTabBg;
    private Image sellTabIcon;
    private TextMeshProUGUI sellTabLabel;
    private Shadow sellTabShadow;

    // P-10 Orders Pane
    private GameObject ordersPane;
    private readonly List<OrderRowView> orderRows = new List<OrderRowView>();
    private RectTransform orderDetailPane;
    private Image detailPortrait;
    private TextMeshProUGUI detailCustomerName;
    private TextMeshProUGUI detailOrderTag;
    private readonly List<ReqPillView> detailReqs = new List<ReqPillView>();
    private Image detailHeroArt;
    private TextMeshProUGUI detailHeroName;
    private TextMeshProUGUI detailRewardValue;
    private Button deliverButton;
    private Image deliverBg;
    private TextMeshProUGUI deliverLabel;
    private Button discardButton;
    private TextMeshProUGUI orderDetailEmptyLabel;

    // P-11 Sell Resources Pane
    private GameObject sellPane;
    private RectTransform sellListWellRect;
    private RectTransform sellListContent;
    private TextMeshProUGUI sellListEmpty;
    private readonly List<SellRowView> sellRows = new List<SellRowView>();
    private RectTransform sellDetailPane;
    private Image sellDetailArt;
    private TextMeshProUGUI sellDetailName;
    private TextMeshProUGUI sellDetailUnitPrice;
    private TMP_InputField sellQtyInput;
    private TextMeshProUGUI sellTotalReceivedText;
    private Button sellExecuteButton;
    private Image sellExecuteBg;
    private Image sellExecuteIcon;
    private TextMeshProUGUI sellExecuteLabel;
    private RectTransform sellDetailFloatLayer;

    private ModeTab activeTab = ModeTab.Orders;
    private int selectedOrderSlot = 0;
    private ResourceData selectedSellResource;
    private int sellQuantity = 1;
    private bool sellSidebarVisible = true;
    private float countdownTimer;
    private float panelScale = 1f;
    private int lastScreenW;
    private int lastScreenH;
    private float lastLossyScale;
    private Coroutine popRoutine;

    private Sprite greenReqPillSprite;
    private Sprite redReqPillSprite;

    public bool IsOpen => window != null && window.activeSelf;

    private void Awake()
    {
        EnsureEventSystem();
    }

    private void Start()
    {
        board = OrderBoardSystem.Instance;
        inventory = ResourceInventory.Instance;

        BuildOpenButton();
        BuildWindow();

        if (board != null) board.OnBoardChanged += Refresh;
        if (inventory != null) inventory.OnInventoryChanged += HandleInventoryChanged;

        Refresh();
    }

    private void Update()
    {
        if (!IsOpen) return;
        SyncStageScale();

        if (activeTab == ModeTab.Orders && board != null)
        {
            countdownTimer -= Time.unscaledDeltaTime;
            if (countdownTimer <= 0f)
            {
                countdownTimer = 0.25f;
                foreach (var row in orderRows)
                {
                    if (board.GetOrder(row.slot) == null) UpdateEmptyOrderRow(row);
                }
            }
        }
    }

    private void OnDisable()
    {
        if (panel != null) panel.localScale = Vector3.one;
        foreach (var row in orderRows)
        {
            row.punch = null;
            if (row.rect != null) row.rect.localScale = Vector3.one;
            EconomyHUD.ClearChildren(row.floatLayer);
        }
        foreach (var s in sellRows)
        {
            EconomyHUD.ClearChildren(s.floatLayer);
        }
        EconomyHUD.ClearChildren(sellDetailFloatLayer);
    }

    private void OnDestroy()
    {
        if (board != null) board.OnBoardChanged -= Refresh;
        if (inventory != null) inventory.OnInventoryChanged -= HandleInventoryChanged;
    }

    public void Open()
    {
        if (window == null || IsOpen) return;
        transform.SetAsLastSibling();
        window.SetActive(true);
        EconomyHUD.BringWindowToFront(window);
        SyncStageScale(force: true);
        countdownTimer = 0f;
        SelectModeTab(activeTab);

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
            float scale = Mathf.Lerp(0.93f, 1f, 1f - (1f - t) * (1f - t));
            if (panel != null) panel.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
        if (panel != null) panel.localScale = Vector3.one;
        popRoutine = null;
    }

    private void SyncStageScale(bool force = false)
    {
        if (stageRect == null) return;
        float parentScale = transform.lossyScale.x;
        if (!force && lastScreenW == Screen.width && lastScreenH == Screen.height && Mathf.Approximately(lastLossyScale, parentScale))
            return;

        lastScreenW = Screen.width;
        lastScreenH = Screen.height;
        lastLossyScale = parentScale;

        panelScale = LumiWorldUITheme.ComputeFitScale(this, out Vector2 stageSize);
        stageRect.anchorMin = stageRect.anchorMax = stageRect.pivot = Vector2.zero;
        stageRect.anchoredPosition = Vector2.zero;
        stageRect.sizeDelta = stageSize;
        stageRect.localScale = Vector3.one * panelScale;
    }

    private void HandleInventoryChanged(string resourceId, int amount, int delta)
    {
        Refresh();
    }

    private void SelectModeTab(ModeTab tab)
    {
        activeTab = tab;
        if (ordersPane != null) ordersPane.SetActive(tab == ModeTab.Orders);
        if (sellPane != null) sellPane.SetActive(tab == ModeTab.SellResources);

        LumiWorldUITheme.SetSubTabActive(ordersTabBg, ordersTabIcon, ordersTabLabel, ordersTabShadow, tab == ModeTab.Orders);
        LumiWorldUITheme.SetSubTabActive(sellTabBg, sellTabIcon, sellTabLabel, sellTabShadow, tab == ModeTab.SellResources);

        Refresh();
    }

    private void Refresh()
    {
        if (board == null) return;

        if (badge != null && badgeText != null)
        {
            int ready = board.CountFulfillable();
            badge.SetActive(ready > 0);
            badgeText.text = ready.ToString(CultureInfo.InvariantCulture);
        }

        if (!IsOpen) return;

        if (activeTab == ModeTab.Orders) RefreshOrdersTab();
        else RefreshSellTab();
    }

    // =========================================================
    // P-10 ORDERS TAB
    // =========================================================

    private void RefreshOrdersTab()
    {
        if (board.GetOrder(selectedOrderSlot) == null)
        {
            for (int i = 0; i < board.SlotCount; i++)
            {
                if (board.GetOrder(i) != null) { selectedOrderSlot = i; break; }
            }
        }

        foreach (OrderRowView row in orderRows)
        {
            ActiveOrder order = board.GetOrder(row.slot);
            row.activeContent.SetActive(order != null);
            row.emptyContent.gameObject.SetActive(order == null);
            if (row.selectionBorder != null)
                row.selectionBorder.enabled = order != null && row.slot == selectedOrderSlot;

            if (order == null)
            {
                UpdateEmptyOrderRow(row);
                continue;
            }

            OrderTemplate tpl = board.FindTemplate(order.templateId);
            string customer = tpl != null && !string.IsNullOrEmpty(tpl.customerName) ? tpl.customerName : "Customer";
            row.customerName.text = customer;
            row.portrait.sprite = ResolvePortraitSprite(tpl, order.templateId, customer);

            bool baseline = row.slot == OrderBoardSystem.BaselineSlot;
            row.tagText.text = baseline ? "Basic order" : (order.requirements.Count > 1 ? "Mixed order" : "Regular order");
            row.tagText.color = baseline ? LumiWorldUITheme.Warning : LumiWorldUITheme.Muted;

            if (row.skipButton != null) row.skipButton.gameObject.SetActive(!baseline);

            for (int i = 0; i < row.reqRows.Count; i++)
            {
                ReqPillView req = row.reqRows[i];
                bool used = i < order.requirements.Count;
                req.root.SetActive(used);
                if (!used) continue;

                ResourceStack need = order.requirements[i];
                ResourceData res = ResourceCatalog.Find(need.resourceId);
                int have = inventory != null ? inventory.GetAmount(need.resourceId) : 0;
                bool ok = have >= need.amount;

                req.icon.sprite = BiomeHarvestIndicator.GetResourceSprite(res);
                req.name.text = ResourceInventoryUIPresentation.LocalizeResourceName(need.resourceId, res != null ? res.resourceName : need.resourceId);
                ApplyReqFractionPill(req, have, need.amount, ok);
            }

            row.rewardText.text = FormatNumber(order.rewardCoins) + " coins";
        }

        RefreshOrderDetail();
    }

    private void RefreshOrderDetail()
    {
        ActiveOrder order = board.GetOrder(selectedOrderSlot);
        bool hasOrder = order != null;
        orderDetailEmptyLabel.gameObject.SetActive(!hasOrder);
        detailPortrait.transform.parent.gameObject.SetActive(hasOrder);
        detailHeroArt.transform.parent.gameObject.SetActive(hasOrder);
        deliverButton.gameObject.SetActive(hasOrder);
        if (discardButton != null) discardButton.gameObject.SetActive(hasOrder && selectedOrderSlot != OrderBoardSystem.BaselineSlot);

        if (!hasOrder) return;

        OrderTemplate tpl = board.FindTemplate(order.templateId);
        string customer = tpl != null && !string.IsNullOrEmpty(tpl.customerName) ? tpl.customerName : "Customer";
        detailCustomerName.text = customer;
        detailOrderTag.text = selectedOrderSlot == OrderBoardSystem.BaselineSlot
            ? "Basic order"
            : (order.requirements.Count > 1 ? "Mixed order" : "Regular order");
        detailPortrait.sprite = ResolvePortraitSprite(tpl, order.templateId, customer);

        if (order.requirements.Count > 0)
        {
            ResourceStack first = order.requirements[0];
            ResourceData firstRes = ResourceCatalog.Find(first.resourceId);
            detailHeroArt.sprite = BiomeHarvestIndicator.GetResourceSprite(firstRes);
            detailHeroName.text = ResourceInventoryUIPresentation.LocalizeResourceName(
                first.resourceId,
                firstRes != null ? firstRes.resourceName : first.resourceId);
        }

        // Strict P-10 rule: Art + Resource name + fraction ONLY (NO "owned" label)
        for (int i = 0; i < detailReqs.Count; i++)
        {
            ReqPillView req = detailReqs[i];
            bool used = i < order.requirements.Count;
            req.root.SetActive(used);
            if (!used) continue;

            ResourceStack need = order.requirements[i];
            ResourceData res = ResourceCatalog.Find(need.resourceId);
            int have = inventory != null ? inventory.GetAmount(need.resourceId) : 0;
            bool ok = have >= need.amount;

            req.icon.sprite = BiomeHarvestIndicator.GetResourceSprite(res);
            req.name.text = ResourceInventoryUIPresentation.LocalizeResourceName(need.resourceId, res != null ? res.resourceName : need.resourceId);
            ApplyReqFractionPill(req, have, need.amount, ok);
        }

        detailRewardValue.text = FormatNumber(order.rewardCoins) + " coins";

        bool ready = board.CanFulfill(selectedOrderSlot);
        deliverButton.interactable = ready;
        deliverBg.color = ready ? LumiWorldUITheme.Primary : new Color(0.62f, 0.59f, 0.55f, 1f);
        deliverLabel.color = Color.white;
        deliverLabel.text = ready ? "Deliver" : "Not enough";
    }

    private void ApplyReqFractionPill(ReqPillView pill, int have, int required, bool met)
    {
        int clamped = Mathf.Min(have, required);
        pill.count.text = $"{clamped} / {required}";
        pill.count.color = met ? LumiWorldUITheme.Success : LumiWorldUITheme.Error;
        if (pill.pillBg != null)
        {
            pill.pillBg.sprite = met ? GetGreenReqPillSprite() : GetRedReqPillSprite();
            pill.pillBg.type = Image.Type.Sliced;
            pill.pillBg.color = Color.white;
        }
    }

    private void UpdateEmptyOrderRow(OrderRowView row)
    {
        TimeSpan wait = board.GetTimeUntilRefill(row.slot);
        if (wait > TimeSpan.Zero)
        {
            row.emptyText.text = $"New order in  {FormatWait(wait)}";
        }
        else if (board.IsWaitingForResources(row.slot))
        {
            row.emptyText.text = "Place creatures on habitats to unlock customer orders";
        }
        else
        {
            row.emptyText.text = "A customer is on the way...";
        }
    }

    private void OnDeliverClicked()
    {
        OrderRowView row = orderRows.Find(r => r.slot == selectedOrderSlot);
        if (row != null && Time.unscaledTime < row.deliverLockedUntil) return;

        ActiveOrder order = board.GetOrder(selectedOrderSlot);
        if (order == null) return;

        int reward = order.rewardCoins;
        if (!board.TryFulfill(selectedOrderSlot)) return;

        if (row != null)
        {
            row.deliverLockedUntil = Time.unscaledTime + DeliverLockSeconds;
            Punch(ref row.punch, row.rect, 1.04f);
            StartCoroutine(EconomyHUD.FloatText(row.floatLayer, "+" + FormatNumber(reward) + " coins", LumiWorldUITheme.Success, 20f,
                new Vector2(120f, 10f), new Vector2(120f, 44f), 1.1f));
        }
    }

    private void OnDiscardSlot(int slot)
    {
        if (board.Discard(slot))
        {
            OrderRowView row = orderRows.Find(r => r.slot == slot);
            if (row != null) Punch(ref row.punch, row.rect, 0.96f);
        }
    }

    // =========================================================
    // P-11 SELL RESOURCES TAB
    // =========================================================

    private void RefreshSellTab()
    {
        float targetWellW = sellSidebarVisible ? LeftPaneWidth : (PanelWidth - 48f);
        if (sellListWellRect != null)
        {
            sellListWellRect.sizeDelta = new Vector2(targetWellW, sellListWellRect.sizeDelta.y);
        }
        if (sellDetailPane != null)
        {
            sellDetailPane.gameObject.SetActive(sellSidebarVisible);
        }

        int shown = 0;
        const float rowH = 54f;
        const float rowGap = 8f;
        ResourceData firstAvailable = null;

        foreach (SellRowView view in sellRows)
        {
            int stock = inventory != null ? inventory.GetAmount(view.resource) : 0;
            bool visible = stock > 0;
            view.rect.gameObject.SetActive(visible);
            if (!visible) continue;

            if (firstAvailable == null) firstAvailable = view.resource;
            view.rect.sizeDelta = new Vector2(targetWellW - 24f, rowH);
            view.rect.anchoredPosition = new Vector2(12f, -(10f + shown * (rowH + rowGap)));
            shown++;

            view.nameText.text = ResourceInventoryUIPresentation.LocalizeResourceName(view.resource.resourceID, view.resource.resourceName);
            view.qtyText.text = "×" + FormatNumber(stock);
            view.priceText.text = FormatNumber(NpcMerchant.GetUnitPrice(view.resource));
        }

        sellListContent.sizeDelta = new Vector2(0f, Mathf.Max(200f, shown * (rowH + rowGap) + 16f));
        sellListEmpty.gameObject.SetActive(shown == 0);

        if (selectedSellResource == null || (inventory != null && inventory.GetAmount(selectedSellResource) <= 0))
        {
            selectedSellResource = firstAvailable;
            sellQuantity = 1;
        }

        foreach (SellRowView view in sellRows)
        {
            if (view.selectionBorder != null)
            {
                view.selectionBorder.enabled = view.resource == selectedSellResource;
            }
        }

        RefreshSellDetail();
    }

    private void SelectSellResource(ResourceData res)
    {
        selectedSellResource = res;
        sellSidebarVisible = true;
        int stock = inventory != null && res != null ? inventory.GetAmount(res) : 0;
        sellQuantity = stock > 0 ? Mathf.Min(10, stock) : 1;
        if (sellQtyInput != null) sellQtyInput.SetTextWithoutNotify(sellQuantity.ToString(CultureInfo.InvariantCulture));
        RefreshSellTab();
    }

    private void RefreshSellDetail()
    {
        if (!sellSidebarVisible || sellDetailPane == null) return;

        if (selectedSellResource == null)
        {
            sellDetailName.text = "No resources available";
            sellDetailUnitPrice.text = "-";
            sellTotalReceivedText.text = "-";
            sellExecuteButton.interactable = false;
            sellExecuteBg.color = new Color(0.62f, 0.59f, 0.55f, 1f);
            sellExecuteLabel.text = "Sell";
            return;
        }

        int stock = inventory != null ? inventory.GetAmount(selectedSellResource) : 0;
        sellQuantity = stock > 0 ? Mathf.Clamp(sellQuantity, 1, stock) : 1;
        if (sellQtyInput != null && !sellQtyInput.isFocused)
        {
            sellQtyInput.SetTextWithoutNotify(sellQuantity.ToString(CultureInfo.InvariantCulture));
        }

        sellDetailArt.sprite = BiomeHarvestIndicator.GetResourceSprite(selectedSellResource);
        sellDetailName.text = ResourceInventoryUIPresentation.LocalizeResourceName(selectedSellResource.resourceID, selectedSellResource.resourceName);

        int unit = NpcMerchant.GetUnitPrice(selectedSellResource);
        sellDetailUnitPrice.text = $"{FormatNumber(unit)} coins each";

        int totalCoins = unit * sellQuantity;
        sellTotalReceivedText.text = $"{FormatNumber(totalCoins)} coins";

        bool canSell = stock > 0 && sellQuantity > 0 && sellQuantity <= stock;
        sellExecuteButton.interactable = canSell;
        sellExecuteBg.color = canSell ? LumiWorldUITheme.Primary : new Color(0.62f, 0.59f, 0.55f, 1f);
        sellExecuteIcon.color = Color.white;
        sellExecuteLabel.color = Color.white;
        sellExecuteLabel.text = $"Sell {sellQuantity}";
    }

    private void StepSellQuantity(int delta)
    {
        if (selectedSellResource == null || inventory == null) return;
        int stock = Mathf.Max(1, inventory.GetAmount(selectedSellResource));
        sellQuantity = Mathf.Clamp(sellQuantity + delta, 1, stock);
        if (sellQtyInput != null) sellQtyInput.SetTextWithoutNotify(sellQuantity.ToString(CultureInfo.InvariantCulture));
        RefreshSellDetail();
    }

    private void OnExecuteDirectSell()
    {
        if (selectedSellResource == null || inventory == null) return;
        int stock = inventory.GetAmount(selectedSellResource);
        int amount = Mathf.Clamp(sellQuantity, 1, Mathf.Max(1, stock));
        if (stock < amount || amount <= 0) return;

        int coins = amount * NpcMerchant.GetUnitPrice(selectedSellResource);
        // P-11 & P-16: Execute directly without confirmation popup
        if (!NpcMerchant.TrySell(selectedSellResource, amount)) return;

        StartCoroutine(EconomyHUD.FloatText(sellDetailFloatLayer, "+" + FormatNumber(coins) + " coins", LumiWorldUITheme.Success, 20f,
            new Vector2(0f, 30f), new Vector2(0f, 70f), 1.0f));
        RefreshSellTab();
    }

    private void Punch(ref Coroutine routine, Transform target, float peak)
    {
        if (target == null || !isActiveAndEnabled) return;
        if (routine != null) StopCoroutine(routine);
        target.localScale = Vector3.one;
        routine = StartCoroutine(EconomyHUD.Punch(target, peak, 0.22f));
    }

    // =========================================================
    // BUILD UI
    // =========================================================

    private void BuildOpenButton()
    {
        openButton = EconomyHUD.CreateRect("OrdersButton", transform);
        openButton.anchorMin = new Vector2(1f, 1f);
        openButton.anchorMax = new Vector2(1f, 1f);
        openButton.pivot = new Vector2(0.5f, 0.5f);
        openButton.sizeDelta = buttonSize;
        openButton.anchoredPosition = buttonCorner - buttonSize * 0.5f;

        var cg = openButton.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        RectTransform badgeRect = EconomyHUD.CreateRect("ReadyBadge", openButton);
        Image badgeImg = badgeRect.gameObject.AddComponent<Image>();
        badgeImg.color = Color.clear;
        badgeText = LumiWorldUITheme.CreateLabel(badgeRect, "0", "0", 12f, Color.clear);
        badge = badgeRect.gameObject;
        badge.SetActive(false);
    }

    private void BuildWindow()
    {
        RectTransform windowRect = EconomyHUD.CreateRect("Window", transform);
        LumiWorldUITheme.Stretch(windowRect);
        window = windowRect.gameObject;
        EconomyHUD.RegisterWindow(window, Close);

        GameObject bdObj = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        bdObj.transform.SetParent(windowRect, false);
        LumiWorldUITheme.Stretch(bdObj.GetComponent<RectTransform>());
        bdObj.GetComponent<Image>().color = LumiWorldUITheme.Backdrop;
        Button bdBtn = bdObj.GetComponent<Button>();
        bdBtn.transition = Selectable.Transition.None;
        bdBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        bdBtn.onClick.AddListener(Close);

        GameObject stageObj = new GameObject("FitStage", typeof(RectTransform));
        stageObj.transform.SetParent(windowRect, false);
        stageRect = stageObj.GetComponent<RectTransform>();
        SyncStageScale(force: true);

        Image panelImg = LumiWorldUITheme.CreateRoundedSurface(stageRect, "Panel", LumiWorldUITheme.Window, LumiWorldUITheme.Edge, 24, 1.5f, 6f, 0.22f, true);
        panel = panelImg.rectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panel.anchoredPosition = Vector2.zero;

        // Header
        GameObject hdrIc = new GameObject("HeaderIcon", typeof(RectTransform), typeof(Image));
        hdrIc.transform.SetParent(panel, false);
        LumiWorldUITheme.PlaceTopLeft(hdrIc.GetComponent<RectTransform>(), 24f, 18f, 24f, 24f);
        Image hdrIcImg = hdrIc.GetComponent<Image>();
        hdrIcImg.sprite = LumiWorldUITheme.Glyph("G-04");
        hdrIcImg.color = LumiWorldUITheme.Text;
        hdrIcImg.preserveAspect = true;
        hdrIcImg.raycastTarget = false;

        TextMeshProUGUI titleTmp = LumiWorldUITheme.CreateLabel(panel, "Title", "Orders", 20f, LumiWorldUITheme.Text, true);
        LumiWorldUITheme.PlaceTopLeft(titleTmp.rectTransform, 56f, 13f, 220f, 34f);

        LumiWorldUITheme.CreateCloseButton(panel, 22f, 14f, Close, 34f);

        // Mode Tabs: Orders | Sell resources
        ordersTabBtn = LumiWorldUITheme.CreateSubTabButton(panel, "Tab_Orders", "Orders", "G-04", 13.5f, () => SelectModeTab(ModeTab.Orders),
            out ordersTabBg, out ordersTabIcon, out ordersTabLabel, out ordersTabShadow);
        LumiWorldUITheme.PlaceTopLeft((RectTransform)ordersTabBtn.transform, 24f, 60f, 128f, 32f);

        sellTabBtn = LumiWorldUITheme.CreateSubTabButton(panel, "Tab_SellResources", "Sell resources", "G-26", 13.5f, () => SelectModeTab(ModeTab.SellResources),
            out sellTabBg, out sellTabIcon, out sellTabLabel, out sellTabShadow);
        LumiWorldUITheme.PlaceTopLeft((RectTransform)sellTabBtn.transform, 160f, 60f, 164f, 32f);

        BuildOrdersPane();
        BuildSellPane();

        window.SetActive(false);
    }

    private void BuildOrdersPane()
    {
        ordersPane = new GameObject("OrdersPane", typeof(RectTransform));
        ordersPane.transform.SetParent(panel, false);
        LumiWorldUITheme.Stretch(ordersPane.GetComponent<RectTransform>());

        // Left scrollable list
        Image well = LumiWorldUITheme.CreateRoundedSurface(ordersPane.transform, "OrdersWell", LumiWorldUITheme.Well, new Color(0.84f, 0.80f, 0.75f, 1f), 18, 1.2f, 0f, 0f, true);
        LumiWorldUITheme.PlaceTopLeft(well.rectTransform, 24f, 102f, LeftPaneWidth, 434f);

        int slots = board != null ? board.SlotCount : 3;
        const float rowH = 126f;
        const float rowGap = 12f;

        orderRows.Clear();
        for (int i = 0; i < slots; i++)
        {
            orderRows.Add(CreateOrderRow(well.rectTransform, i, 14f + i * (rowH + rowGap), LeftPaneWidth - 24f, rowH));
        }

        // Right Order Inspector
        Image inspImg = LumiWorldUITheme.CreateRoundedSurface(ordersPane.transform, "OrderInspector", LumiWorldUITheme.Surface, new Color(0.87f, 0.84f, 0.79f, 1f), 20, 1.4f, 4f, 0.16f, true);
        orderDetailPane = inspImg.rectTransform;
        LumiWorldUITheme.PlaceTopLeft(orderDetailPane, 568f, 60f, RightPaneWidth, 476f);

        orderDetailEmptyLabel = LumiWorldUITheme.CreateLabel(orderDetailPane, "Empty", "Select an order to inspect.", 14f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.Center);
        LumiWorldUITheme.Stretch(orderDetailEmptyLabel.rectTransform);

        GameObject npcRow = new GameObject("NpcHeader", typeof(RectTransform));
        npcRow.transform.SetParent(orderDetailPane, false);
        LumiWorldUITheme.PlaceTopLeft(npcRow.GetComponent<RectTransform>(), 16f, 16f, RightPaneWidth - 32f, 64f);

        Image pBg = LumiWorldUITheme.CreateRoundedSurface(npcRow.transform, "PortraitBg", LumiWorldUITheme.Window, LumiWorldUITheme.Edge, 32, 1.5f, 2f, 0.10f);
        LumiWorldUITheme.PlaceTopLeft(pBg.rectTransform, 0f, 0f, 62f, 62f);

        GameObject dpObj = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        dpObj.transform.SetParent(pBg.rectTransform, false);
        LumiWorldUITheme.Stretch(dpObj.GetComponent<RectTransform>(), 4f);
        detailPortrait = dpObj.GetComponent<Image>();
        detailPortrait.preserveAspect = true;
        detailPortrait.raycastTarget = false;

        detailCustomerName = LumiWorldUITheme.CreateLabel(npcRow.transform, "CustomerName", "Customer", 17f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(detailCustomerName.rectTransform, 74f, 4f, RightPaneWidth - 110f, 28f);

        detailOrderTag = LumiWorldUITheme.CreateLabel(npcRow.transform, "OrderTag", "Basic order", 12f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(detailOrderTag.rectTransform, 74f, 34f, RightPaneWidth - 110f, 20f);

        // Hero resource art & name (NO "owned" text per P-10 rule)
        GameObject heroGroup = new GameObject("HeroResource", typeof(RectTransform));
        heroGroup.transform.SetParent(orderDetailPane, false);
        LumiWorldUITheme.PlaceTopLeft(heroGroup.GetComponent<RectTransform>(), 16f, 92f, RightPaneWidth - 32f, 118f);

        Image heroCard = LumiWorldUITheme.CreateRoundedSurface(heroGroup.transform, "Bg", LumiWorldUITheme.Slot, LumiWorldUITheme.Edge, 14, 1.2f, 0f, 0f);
        LumiWorldUITheme.Stretch(heroCard.rectTransform);

        GameObject haObj = new GameObject("Art", typeof(RectTransform), typeof(Image));
        haObj.transform.SetParent(heroGroup.transform, false);
        RectTransform haRt = haObj.GetComponent<RectTransform>();
        haRt.anchorMin = haRt.anchorMax = haRt.pivot = new Vector2(0.5f, 1f);
        haRt.anchoredPosition = new Vector2(0f, -8f);
        haRt.sizeDelta = new Vector2(76f, 76f);
        detailHeroArt = haObj.GetComponent<Image>();
        detailHeroArt.preserveAspect = true;
        detailHeroArt.raycastTarget = false;

        detailHeroName = LumiWorldUITheme.CreateLabel(heroGroup.transform, "Name", "Oak Wood", 15.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.Center);
        LumiWorldUITheme.PlaceTopLeft(detailHeroName.rectTransform, 10f, 88f, RightPaneWidth - 52f, 26f);

        // Requirements stack (each shows Art + Name + min(have,req)/req pill, no "owned")
        detailReqs.Clear();
        for (int i = 0; i < 3; i++)
        {
            detailReqs.Add(CreateDetailRequirementRow(orderDetailPane, 222f + i * 38f, RightPaneWidth - 32f));
        }

        // Reward pill
        Image rewBg = LumiWorldUITheme.CreateRoundedSurface(orderDetailPane, "RewardPill", LumiWorldUITheme.Well, LumiWorldUITheme.Edge, 12, 1f, 0f, 0f);
        LumiWorldUITheme.PlaceTopLeft(rewBg.rectTransform, 16f, 344f, RightPaneWidth - 32f, 38f);
        TextMeshProUGUI rewLbl = LumiWorldUITheme.CreateLabel(rewBg.rectTransform, "Label", "Reward", 13f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(rewLbl.rectTransform, 14f, 7f, 80f, 24f);

        GameObject rcObj = new GameObject("Coin", typeof(RectTransform), typeof(Image));
        rcObj.transform.SetParent(rewBg.rectTransform, false);
        LumiWorldUITheme.PlaceTopLeft(rcObj.GetComponent<RectTransform>(), 122f, 10f, 18f, 18f);
        Image rcImg = rcObj.GetComponent<Image>();
        rcImg.sprite = EconomyHUD.CoinSprite;
        rcImg.preserveAspect = true;
        rcImg.raycastTarget = false;

        detailRewardValue = LumiWorldUITheme.CreateLabel(rewBg.rectTransform, "Value", "0 coins", 14.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineRight);
        LumiWorldUITheme.PlaceTopLeft(detailRewardValue.rectTransform, 142f, 7f, RightPaneWidth - 186f, 24f);

        // Primary Deliver CTA
        deliverButton = LumiWorldUITheme.CreatePillButton(
            orderDetailPane,
            "DeliverBtn",
            "Deliver",
            null,
            true,
            15f,
            OnDeliverClicked,
            out deliverBg,
            out _,
            out deliverLabel,
            18);
        LumiWorldUITheme.PlaceTopLeft((RectTransform)deliverButton.transform, 16f, 394f, RightPaneWidth - 32f, 44f);

        discardButton = LumiWorldUITheme.CreatePillButton(
            orderDetailPane,
            "DiscardBtn",
            "Discard order",
            null,
            false,
            12f,
            () => OnDiscardSlot(selectedOrderSlot),
            out Image discBg,
            out _,
            out _,
            12);
        LumiWorldUITheme.PlaceTopLeft((RectTransform)discardButton.transform, 16f, 443f, RightPaneWidth - 32f, 24f);
        discBg.color = LumiWorldUITheme.Window;
    }

    private OrderRowView CreateOrderRow(Transform parent, int slot, float y, float width, float height)
    {
        OrderRowView row = new OrderRowView { slot = slot };
        Image cardBg = LumiWorldUITheme.CreateRoundedSurface(parent, "OrderSlot_" + slot, LumiWorldUITheme.Slot, new Color(0.88f, 0.85f, 0.80f, 1f), 16, 1.2f, 2.5f, 0.13f, true);
        row.rect = cardBg.rectTransform;
        LumiWorldUITheme.PlaceTopLeft(row.rect, 12f, y, width, height);

        Button selectBtn = cardBg.gameObject.AddComponent<Button>();
        selectBtn.targetGraphic = cardBg;
        LumiWorldUITheme.ApplyButtonDisabledNeutral(selectBtn);
        int captured = slot;
        selectBtn.onClick.AddListener(() => { selectedOrderSlot = captured; RefreshOrdersTab(); });

        GameObject activeObj = new GameObject("Active", typeof(RectTransform));
        activeObj.transform.SetParent(row.rect, false);
        LumiWorldUITheme.Stretch(activeObj.GetComponent<RectTransform>());
        row.activeContent = activeObj;

        Image pBg = LumiWorldUITheme.CreateRoundedSurface(activeObj.transform, "PortraitFrame", LumiWorldUITheme.Window, LumiWorldUITheme.Edge, 32, 1.5f, 0f, 0f);
        LumiWorldUITheme.PlaceTopLeft(pBg.rectTransform, 14f, 14f, 64f, 64f);
        GameObject pImgObj = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        pImgObj.transform.SetParent(pBg.rectTransform, false);
        LumiWorldUITheme.Stretch(pImgObj.GetComponent<RectTransform>(), 4f);
        row.portrait = pImgObj.GetComponent<Image>();
        row.portrait.preserveAspect = true;
        row.portrait.raycastTarget = false;

        row.customerName = LumiWorldUITheme.CreateLabel(activeObj.transform, "Customer", "Customer", 15f, LumiWorldUITheme.Text, true);
        LumiWorldUITheme.PlaceTopLeft(row.customerName.rectTransform, 90f, 10f, 240f, 24f);

        row.tagText = LumiWorldUITheme.CreateLabel(activeObj.transform, "Tag", "Basic order", 11f, LumiWorldUITheme.Muted, false);
        LumiWorldUITheme.PlaceTopLeft(row.tagText.rectTransform, 90f, 88f, 180f, 18f);

        // Requirements inside row (art + name + min(have, req)/req pill; NO "Ready/Short")
        for (int i = 0; i < 3; i++)
        {
            ReqPillView req = CreateCompactRequirementRow(activeObj.transform, 90f, 36f + i * 24f, 276f);
            row.reqRows.Add(req);
        }

        // Right Reward column inside row
        TextMeshProUGUI rewLbl = LumiWorldUITheme.CreateLabel(activeObj.transform, "RewardLabel", "Reward", 11f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.MidlineRight);
        LumiWorldUITheme.PlaceTopLeft(rewLbl.rectTransform, width - 140f, 16f, 122f, 18f);

        GameObject cObj = new GameObject("Coin", typeof(RectTransform), typeof(Image));
        cObj.transform.SetParent(activeObj.transform, false);
        LumiWorldUITheme.PlaceTopLeft(cObj.GetComponent<RectTransform>(), width - 120f, 38f, 18f, 18f);
        Image cImg = cObj.GetComponent<Image>();
        cImg.sprite = EconomyHUD.CoinSprite;
        cImg.preserveAspect = true;
        cImg.raycastTarget = false;

        row.rewardText = LumiWorldUITheme.CreateLabel(activeObj.transform, "RewardVal", "60 coins", 14.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineRight);
        LumiWorldUITheme.PlaceTopLeft(row.rewardText.rectTransform, width - 98f, 35f, 80f, 24f);

        if (slot != OrderBoardSystem.BaselineSlot)
        {
            row.skipButton = LumiWorldUITheme.CreatePillButton(activeObj.transform, "Skip", "Skip", null, false, 11f, () => OnDiscardSlot(captured),
                out Image skBg, out _, out _, 10);
            LumiWorldUITheme.PlaceTopLeft((RectTransform)row.skipButton.transform, width - 74f, 86f, 58f, 24f);
            skBg.color = LumiWorldUITheme.Window;
        }

        GameObject emptyObj = new GameObject("Empty", typeof(RectTransform));
        emptyObj.transform.SetParent(row.rect, false);
        LumiWorldUITheme.Stretch(emptyObj.GetComponent<RectTransform>());
        row.emptyContent = emptyObj;
        row.emptyText = LumiWorldUITheme.CreateLabel(emptyObj.transform, "EmptyLabel", "Refilling...", 13.5f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.Center, wrap: true);
        LumiWorldUITheme.Stretch(row.emptyText.rectTransform, 16f);

        row.selectionBorder = LumiWorldUITheme.CreateSelectionBorder(row.rect, 16, 2.5f);
        row.selectionBorder.enabled = false;

        row.floatLayer = EconomyHUD.CreateRect("Floating", row.rect);
        LumiWorldUITheme.Stretch(row.floatLayer);

        return row;
    }

    private ReqPillView CreateCompactRequirementRow(Transform parent, float x, float y, float width)
    {
        GameObject root = new GameObject("Req", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        LumiWorldUITheme.PlaceTopLeft(root.GetComponent<RectTransform>(), x, y, width, 22f);

        GameObject icObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icObj.transform.SetParent(root.transform, false);
        LumiWorldUITheme.PlaceTopLeft(icObj.GetComponent<RectTransform>(), 0f, 2f, 18f, 18f);
        Image ic = icObj.GetComponent<Image>();
        ic.preserveAspect = true;
        ic.raycastTarget = false;

        TextMeshProUGUI nameTxt = LumiWorldUITheme.CreateLabel(root.transform, "Name", "Resource", 12.5f, LumiWorldUITheme.Text, false, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(nameTxt.rectTransform, 24f, 0f, width - 92f, 22f);

        GameObject pillObj = new GameObject("FracPill", typeof(RectTransform), typeof(Image));
        pillObj.transform.SetParent(root.transform, false);
        LumiWorldUITheme.PlaceTopLeft(pillObj.GetComponent<RectTransform>(), width - 64f, 1f, 64f, 20f);
        Image pillBg = pillObj.GetComponent<Image>();
        pillBg.raycastTarget = false;

        TextMeshProUGUI countTxt = LumiWorldUITheme.CreateLabel(pillObj.transform, "Count", "0 / 10", 11.5f, LumiWorldUITheme.Success, true, TextAlignmentOptions.Center);
        LumiWorldUITheme.Stretch(countTxt.rectTransform);

        return new ReqPillView { root = root, icon = ic, name = nameTxt, pillBg = pillBg, count = countTxt };
    }

    private ReqPillView CreateDetailRequirementRow(Transform parent, float y, float width)
    {
        GameObject root = new GameObject("DetailReq", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        LumiWorldUITheme.PlaceTopLeft(root.GetComponent<RectTransform>(), 16f, y, width, 32f);

        Image rowBg = root.AddComponent<Image>();
        rowBg.sprite = LumiWorldUITheme.PillSprite;
        rowBg.type = Image.Type.Sliced;
        rowBg.color = LumiWorldUITheme.Window;
        rowBg.raycastTarget = false;

        GameObject icObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icObj.transform.SetParent(root.transform, false);
        LumiWorldUITheme.PlaceTopLeft(icObj.GetComponent<RectTransform>(), 8f, 5f, 22f, 22f);
        Image ic = icObj.GetComponent<Image>();
        ic.preserveAspect = true;
        ic.raycastTarget = false;

        TextMeshProUGUI nameTxt = LumiWorldUITheme.CreateLabel(root.transform, "Name", "Resource", 12.5f, LumiWorldUITheme.Text, false, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(nameTxt.rectTransform, 36f, 4f, width - 114f, 24f);

        GameObject pillObj = new GameObject("FracPill", typeof(RectTransform), typeof(Image));
        pillObj.transform.SetParent(root.transform, false);
        LumiWorldUITheme.PlaceTopLeft(pillObj.GetComponent<RectTransform>(), width - 74f, 5f, 66f, 22f);
        Image pillBg = pillObj.GetComponent<Image>();
        pillBg.raycastTarget = false;

        TextMeshProUGUI countTxt = LumiWorldUITheme.CreateLabel(pillObj.transform, "Count", "0 / 10", 12f, LumiWorldUITheme.Success, true, TextAlignmentOptions.Center);
        LumiWorldUITheme.Stretch(countTxt.rectTransform);

        return new ReqPillView { root = root, icon = ic, name = nameTxt, pillBg = pillBg, count = countTxt };
    }

    private void BuildSellPane()
    {
        sellPane = new GameObject("SellResourcesPane", typeof(RectTransform));
        sellPane.transform.SetParent(panel, false);
        LumiWorldUITheme.Stretch(sellPane.GetComponent<RectTransform>());

        // Left resource list
        Image well = LumiWorldUITheme.CreateRoundedSurface(sellPane.transform, "SellWell", LumiWorldUITheme.Well, new Color(0.84f, 0.80f, 0.75f, 1f), 18, 1.2f, 0f, 0f, true);
        sellListWellRect = well.rectTransform;
        LumiWorldUITheme.PlaceTopLeft(sellListWellRect, 24f, 102f, LeftPaneWidth, 434f);

        GameObject scrollObj = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
        scrollObj.transform.SetParent(sellListWellRect, false);
        RectTransform scrollRt = scrollObj.GetComponent<RectTransform>();
        LumiWorldUITheme.Stretch(scrollRt, 4f);
        ScrollRect scroll = scrollObj.GetComponent<ScrollRect>();

        GameObject vpObj = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        vpObj.transform.SetParent(scrollRt, false);
        LumiWorldUITheme.Stretch(vpObj.GetComponent<RectTransform>());
        vpObj.GetComponent<Image>().color = Color.clear;

        GameObject contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(vpObj.transform, false);
        sellListContent = contentObj.GetComponent<RectTransform>();
        sellListContent.anchorMin = new Vector2(0f, 1f);
        sellListContent.anchorMax = new Vector2(1f, 1f);
        sellListContent.pivot = new Vector2(0.5f, 1f);
        sellListContent.anchoredPosition = Vector2.zero;
        sellListContent.sizeDelta = new Vector2(0f, 300f);

        scroll.viewport = vpObj.GetComponent<RectTransform>();
        scroll.content = sellListContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 18f;

        sellListEmpty = LumiWorldUITheme.CreateLabel(vpObj.transform, "Empty", "No resources in inventory to sell yet.", 14.5f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.Center);
        LumiWorldUITheme.Stretch(sellListEmpty.rectTransform);

        sellRows.Clear();
        IReadOnlyList<ResourceData> catalog = ResourceCatalog.All;
        for (int i = 0; i < catalog.Count; i++)
        {
            ResourceData res = catalog[i];
            if (res == null) continue;
            sellRows.Add(CreateSellRow(sellListContent, res));
        }

        // Right Sell Inspector (collapsible via ← Back per P-11)
        Image inspImg = LumiWorldUITheme.CreateRoundedSurface(sellPane.transform, "SellInspector", LumiWorldUITheme.Surface, new Color(0.87f, 0.84f, 0.79f, 1f), 20, 1.4f, 4f, 0.16f, true);
        sellDetailPane = inspImg.rectTransform;
        LumiWorldUITheme.PlaceTopLeft(sellDetailPane, 568f, 60f, RightPaneWidth, 476f);

        GameObject artObj = new GameObject("Art", typeof(RectTransform), typeof(Image));
        artObj.transform.SetParent(sellDetailPane, false);
        RectTransform artRt = artObj.GetComponent<RectTransform>();
        artRt.anchorMin = artRt.anchorMax = artRt.pivot = new Vector2(0.5f, 1f);
        artRt.anchoredPosition = new Vector2(0f, -24f);
        artRt.sizeDelta = new Vector2(96f, 96f);
        sellDetailArt = artObj.GetComponent<Image>();
        sellDetailArt.preserveAspect = true;
        sellDetailArt.raycastTarget = false;

        sellDetailName = LumiWorldUITheme.CreateLabel(sellDetailPane, "Name", "Oak Wood", 19f, LumiWorldUITheme.Text, true, TextAlignmentOptions.Center);
        LumiWorldUITheme.PlaceTopLeft(sellDetailName.rectTransform, 16f, 132f, RightPaneWidth - 32f, 30f);

        // Unit Price Pill (NO "owned" count on right per P-11)
        Image upBg = LumiWorldUITheme.CreateRoundedSurface(sellDetailPane, "UnitPricePill", LumiWorldUITheme.Window, LumiWorldUITheme.Edge, 14, 1f, 0f, 0f);
        LumiWorldUITheme.PlaceTopLeft(upBg.rectTransform, 54f, 170f, RightPaneWidth - 108f, 30f);
        GameObject upCoin = new GameObject("Coin", typeof(RectTransform), typeof(Image));
        upCoin.transform.SetParent(upBg.rectTransform, false);
        LumiWorldUITheme.PlaceTopLeft(upCoin.GetComponent<RectTransform>(), 12f, 6f, 18f, 18f);
        Image upCoinImg = upCoin.GetComponent<Image>();
        upCoinImg.sprite = EconomyHUD.CoinSprite;
        upCoinImg.preserveAspect = true;
        upCoinImg.raycastTarget = false;
        sellDetailUnitPrice = LumiWorldUITheme.CreateLabel(upBg.rectTransform, "Val", "3 coins each", 12.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(sellDetailUnitPrice.rectTransform, 36f, 3f, RightPaneWidth - 150f, 24f);

        // Quantity Stepper
        float stepY = 226f;
        LumiWorldUITheme.CreatePillButton(sellDetailPane, "MinusBtn", "", "Minus", false, 14f, () => StepSellQuantity(-1), out Image mBg, out _, out _, 14);
        LumiWorldUITheme.PlaceTopLeft((RectTransform)mBg.transform, 40f, stepY, 44f, 38f);

        Image qBox = LumiWorldUITheme.CreateRoundedSurface(sellDetailPane, "QtyInput", LumiWorldUITheme.Slot, LumiWorldUITheme.Edge, 12, 1.2f, 0f, 0f, true);
        LumiWorldUITheme.PlaceTopLeft(qBox.rectTransform, 92f, stepY, 106f, 38f);
        TextMeshProUGUI qTxt = LumiWorldUITheme.CreateLabel(qBox.rectTransform, "Text", "1", 17f, LumiWorldUITheme.Text, true, TextAlignmentOptions.Center);
        LumiWorldUITheme.Stretch(qTxt.rectTransform);
        sellQtyInput = qBox.gameObject.AddComponent<TMP_InputField>();
        sellQtyInput.targetGraphic = qBox;
        sellQtyInput.textComponent = qTxt;
        sellQtyInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        sellQtyInput.characterLimit = 4;
        sellQtyInput.lineType = TMP_InputField.LineType.SingleLine;
        sellQtyInput.onValueChanged.AddListener(val =>
        {
            if (int.TryParse(val, out int parsed))
            {
                sellQuantity = Mathf.Max(1, parsed);
                RefreshSellDetail();
            }
        });
        sellQtyInput.onEndEdit.AddListener(val =>
        {
            int stock = selectedSellResource != null && inventory != null ? inventory.GetAmount(selectedSellResource) : 1;
            sellQuantity = int.TryParse(val, out int parsed) ? Mathf.Clamp(parsed, 1, Mathf.Max(1, stock)) : 1;
            sellQtyInput.SetTextWithoutNotify(sellQuantity.ToString(CultureInfo.InvariantCulture));
            RefreshSellDetail();
        });

        LumiWorldUITheme.CreatePillButton(sellDetailPane, "PlusBtn", "", "Plus", false, 14f, () => StepSellQuantity(1), out Image pBg, out _, out _, 14);
        LumiWorldUITheme.PlaceTopLeft((RectTransform)pBg.transform, 206f, stepY, 44f, 38f);

        // Total Received Inset (NO "Remaining" row per P-11)
        Image totBg = LumiWorldUITheme.CreateRoundedSurface(sellDetailPane, "TotalReceivedBox", LumiWorldUITheme.Well, LumiWorldUITheme.Edge, 12, 1f, 0f, 0f);
        LumiWorldUITheme.PlaceTopLeft(totBg.rectTransform, 18f, 290f, RightPaneWidth - 36f, 42f);
        TextMeshProUGUI totLbl = LumiWorldUITheme.CreateLabel(totBg.rectTransform, "Label", "Total received", 13f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(totLbl.rectTransform, 12f, 9f, 110f, 24f);

        GameObject trCoin = new GameObject("Coin", typeof(RectTransform), typeof(Image));
        trCoin.transform.SetParent(totBg.rectTransform, false);
        LumiWorldUITheme.PlaceTopLeft(trCoin.GetComponent<RectTransform>(), 144f, 12f, 18f, 18f);
        Image trCoinImg = trCoin.GetComponent<Image>();
        trCoinImg.sprite = EconomyHUD.CoinSprite;
        trCoinImg.preserveAspect = true;
        trCoinImg.raycastTarget = false;

        sellTotalReceivedText = LumiWorldUITheme.CreateLabel(totBg.rectTransform, "Val", "0 coins", 14.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineRight);
        LumiWorldUITheme.PlaceTopLeft(sellTotalReceivedText.rectTransform, 164f, 9f, RightPaneWidth - 212f, 24f);

        // Sell CTA (Direct sell, NO P-16 popup)
        sellExecuteButton = LumiWorldUITheme.CreatePillButton(
            sellDetailPane,
            "SellExecuteBtn",
            "Sell",
            "G-26",
            true,
            15f,
            OnExecuteDirectSell,
            out sellExecuteBg,
            out sellExecuteIcon,
            out sellExecuteLabel,
            18);
        LumiWorldUITheme.PlaceTopLeft((RectTransform)sellExecuteButton.transform, 18f, 366f, RightPaneWidth - 36f, 46f);

        // Back button collapses right detail sidebar per P-11
        Button backBtn = LumiWorldUITheme.CreatePillButton(
            sellDetailPane,
            "BackBtn",
            "Back",
            "ArrowLeft",
            false,
            13.5f,
            () => { sellSidebarVisible = false; RefreshSellTab(); },
            out _,
            out _,
            out _,
            16);
        LumiWorldUITheme.PlaceTopLeft((RectTransform)backBtn.transform, 18f, 420f, RightPaneWidth - 36f, 38f);

        sellDetailFloatLayer = EconomyHUD.CreateRect("Floating", sellDetailPane);
        LumiWorldUITheme.Stretch(sellDetailFloatLayer);
    }

    private SellRowView CreateSellRow(RectTransform parent, ResourceData resource)
    {
        SellRowView view = new SellRowView { resource = resource };
        Image rowBg = LumiWorldUITheme.CreateRoundedSurface(parent, "SellRow_" + resource.resourceID, LumiWorldUITheme.Slot, new Color(0.88f, 0.85f, 0.80f, 1f), 14, 1.2f, 2f, 0.12f, true);
        view.rect = rowBg.rectTransform;
        LumiWorldUITheme.PlaceTopLeft(view.rect, 12f, 0f, LeftPaneWidth - 24f, 54f);

        Button rowBtn = rowBg.gameObject.AddComponent<Button>();
        rowBtn.targetGraphic = rowBg;
        LumiWorldUITheme.ApplyButtonDisabledNeutral(rowBtn);
        ResourceData captured = resource;
        rowBtn.onClick.AddListener(() => SelectSellResource(captured));

        GameObject icObj = new GameObject("Art", typeof(RectTransform), typeof(Image));
        icObj.transform.SetParent(view.rect, false);
        LumiWorldUITheme.PlaceTopLeft(icObj.GetComponent<RectTransform>(), 12f, 8f, 38f, 38f);
        Image ic = icObj.GetComponent<Image>();
        ic.sprite = BiomeHarvestIndicator.GetResourceSprite(resource);
        ic.preserveAspect = true;
        ic.raycastTarget = false;

        view.nameText = LumiWorldUITheme.CreateLabel(view.rect, "Name", resource.resourceName, 14.5f, LumiWorldUITheme.Text, true);
        LumiWorldUITheme.PlaceTopLeft(view.nameText.rectTransform, 60f, 14f, 220f, 26f);

        // Quantity pill on listing (NO "owned" / NO "coins each" per P-11)
        Image qtyPill = LumiWorldUITheme.CreateRoundedSurface(view.rect, "QtyPill", LumiWorldUITheme.Window, LumiWorldUITheme.Edge, 12, 1f, 0f, 0f);
        qtyPill.rectTransform.anchorMin = qtyPill.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        qtyPill.rectTransform.pivot = new Vector2(1f, 0.5f);
        qtyPill.rectTransform.anchoredPosition = new Vector2(-108f, 0f);
        qtyPill.rectTransform.sizeDelta = new Vector2(72f, 28f);
        view.qtyText = LumiWorldUITheme.CreateLabel(qtyPill.rectTransform, "Qty", "×0", 13f, LumiWorldUITheme.Text, true, TextAlignmentOptions.Center);
        LumiWorldUITheme.Stretch(view.qtyText.rectTransform);

        GameObject cObj = new GameObject("Coin", typeof(RectTransform), typeof(Image));
        cObj.transform.SetParent(view.rect, false);
        RectTransform cRt = cObj.GetComponent<RectTransform>();
        cRt.anchorMin = cRt.anchorMax = new Vector2(1f, 0.5f);
        cRt.pivot = new Vector2(1f, 0.5f);
        cRt.anchoredPosition = new Vector2(-74f, 0f);
        cRt.sizeDelta = new Vector2(20f, 20f);
        Image cImg = cObj.GetComponent<Image>();
        cImg.sprite = EconomyHUD.CoinSprite;
        cImg.preserveAspect = true;
        cImg.raycastTarget = false;

        view.priceText = LumiWorldUITheme.CreateLabel(view.rect, "Price", "0", 15f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineRight);
        view.priceText.rectTransform.anchorMin = view.priceText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        view.priceText.rectTransform.pivot = new Vector2(1f, 0.5f);
        view.priceText.rectTransform.anchoredPosition = new Vector2(-16f, 0f);
        view.priceText.rectTransform.sizeDelta = new Vector2(54f, 28f);

        view.selectionBorder = LumiWorldUITheme.CreateSelectionBorder(view.rect, 14, 2.5f);
        view.selectionBorder.enabled = false;

        view.floatLayer = EconomyHUD.CreateRect("Floating", view.rect);
        LumiWorldUITheme.Stretch(view.floatLayer);

        return view;
    }

    private Sprite ResolvePortraitSprite(OrderTemplate tpl, string templateId, string customerName)
    {
        if (tpl != null && tpl.portrait != null) return tpl.portrait;
        string npcId = LumiWorldUITheme.ResolveOrderNpcPortraitId(templateId, customerName);
        return LumiWorldUITheme.NpcPortrait(npcId) ?? EconomyHUD.CircleSprite;
    }

    private Sprite GetGreenReqPillSprite()
    {
        if (greenReqPillSprite != null) return greenReqPillSprite;
        greenReqPillSprite = LumiWorldUITheme.GetBorderedSurfaceSprite(
            "ReqPillGreen",
            new Color(0.89f, 0.94f, 0.87f, 1f),
            new Color(0.54f, 0.72f, 0.48f, 1f),
            10,
            1.2f);
        return greenReqPillSprite;
    }

    private Sprite GetRedReqPillSprite()
    {
        if (redReqPillSprite != null) return redReqPillSprite;
        redReqPillSprite = LumiWorldUITheme.GetBorderedSurfaceSprite(
            "ReqPillRed",
            new Color(0.96f, 0.89f, 0.88f, 1f),
            new Color(0.80f, 0.45f, 0.42f, 1f),
            10,
            1.2f);
        return redReqPillSprite;
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
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
