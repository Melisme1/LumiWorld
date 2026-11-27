using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using LumiWorld.Acs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// P-09 Card Shop — LumiWorld UI Production Pack:
/// - Tabs: Nature (G-11), Creatures (G-12), and Special (G-14, shown when special cards exist in catalog).
/// - Nature cards display their shared Island Origin glyph (I-01 Starter plain / I-02 Rainy marsh);
///   clicking the island glyph reveals the island name popover without purchasing the card.
/// - Single selection uses crisp #403F3C border (no checkmark).
/// - Right inspector shows card preview, owned count, Buy price, Placement fee (without "when placing later"),
///   quantity stepper, total cost, and "[G-25] Buy card".
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CardShopUI : MonoBehaviour
{
    private enum ShopTab
    {
        Nature,
        Creatures,
        Special
    }

    private const float PanelWidth = 880f;
    private const float PanelHeight = 560f;
    private const float GridAreaWidth = 556f;
    private const float TileWidth = 168f;
    private const float TileHeight = 132f;
    private const float TileGap = 12f;
    private const int Columns = 3;

    [Header("Legacy / Scene References")]
    [SerializeField] private Vector2 buttonCorner = new Vector2(-20f, -128f);
    [SerializeField] private Vector2 buttonSize = new Vector2(170f, 44f);

    private class TileView
    {
        public CardData card;
        public RectTransform rect;
        public Image selectionBorder;
        public Image rarityFrameOverlay;
        public Image raiseArrow;
        public Image lowerArrow;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI priceText;
        public Button islandButton;
        public RectTransform floatLayer;
        public Coroutine punch;
    }

    private class TabView
    {
        public ShopTab tab;
        public Button button;
        public Image bg;
        public Image icon;
        public TextMeshProUGUI label;
        public Shadow shadow;
    }

    private CurrencyWallet wallet;
    private RectTransform openButton;
    private GameObject window;
    private RectTransform stageRect;
    private RectTransform panel;

    private readonly List<TabView> tabViews = new List<TabView>();
    private readonly List<TileView> allTiles = new List<TileView>();
    private readonly List<TileView> visibleTiles = new List<TileView>();

    private RectTransform gridContent;
    private TextMeshProUGUI emptyGridLabel;

    // Right detail inspector
    private RectTransform detailArtBoxRect;
    private Image detailArt;
    private Image detailRarityFrame;
    private Image detailRaiseArrow;
    private Image detailLowerArrow;
    private Image detailFallbackBg;
    private TextMeshProUGUI detailFallbackInitials;
    private TextMeshProUGUI detailTitle;
    private TextMeshProUGUI detailOwned;
    private Button detailIslandPillBtn;
    private Image detailIslandGlyph;
    private TextMeshProUGUI detailIslandLabel;
    private TextMeshProUGUI detailBuyPriceValue;
    private TextMeshProUGUI detailFeeValue;
    private TMP_InputField qtyInput;
    private Button minusBtn;
    private Button plusBtn;
    private TextMeshProUGUI totalPriceText;
    private Button buyButton;
    private Image buyBtnBg;
    private Image buyBtnIcon;
    private TextMeshProUGUI buyBtnLabel;

    // Island origin popover when clicking I-01 / I-02
    private GameObject islandPopover;
    private RectTransform islandPopoverCard;
    private Image islandPopoverGlyph;
    private TextMeshProUGUI islandPopoverTitle;
    private TextMeshProUGUI islandPopoverSub;

    private ShopTab activeTab = ShopTab.Nature;
    private CardData selectedCard;
    private int selectedQuantity = 1;
    private float panelScale = 1f;
    private int lastScreenW;
    private int lastScreenH;
    private float lastLossyScale;
    private Coroutine popRoutine;

    public bool IsOpen => window != null && window.activeSelf;

    private void Start()
    {
        wallet = CurrencyWallet.Instance;
        BuildOpenButton();
        BuildWindow();

        if (wallet != null)
        {
            wallet.OnBalanceChanged += HandleBalanceChanged;
        }
    }

    private void LateUpdate()
    {
        if (!IsOpen) return;
        SyncStageScale();
    }

    private void OnDisable()
    {
        if (panel != null) panel.localScale = Vector3.one;
        foreach (TileView tile in allTiles)
        {
            tile.punch = null;
            if (tile.rect != null) tile.rect.localScale = Vector3.one;
            EconomyHUD.ClearChildren(tile.floatLayer);
        }
    }

    private void OnDestroy()
    {
        if (wallet != null)
        {
            wallet.OnBalanceChanged -= HandleBalanceChanged;
        }
    }

    public void Open()
    {
        if (window == null || IsOpen) return;
        transform.SetAsLastSibling();
        window.SetActive(true);
        EconomyHUD.BringWindowToFront(window);
        SyncStageScale(force: true);
        HideIslandPopover();
        SelectTab(activeTab);

        if (popRoutine != null) StopCoroutine(popRoutine);
        popRoutine = StartCoroutine(PopIn());
    }

    public void Close()
    {
        if (!IsOpen) return;
        HideIslandPopover();
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

    private void HandleBalanceChanged(int balance, int delta, CoinReason reason)
    {
        if (!IsOpen || !isActiveAndEnabled) return;
        Refresh();
    }

    private void SelectTab(ShopTab nextTab)
    {
        activeTab = nextTab;
        HideIslandPopover();

        foreach (var tv in tabViews)
        {
            bool active = tv.tab == activeTab;
            LumiWorldUITheme.SetSubTabActive(tv.bg, tv.icon, tv.label, tv.shadow, active);
        }

        float contentW = gridContent != null && gridContent.rect.width > 100f ? gridContent.rect.width : (GridAreaWidth - 8f);
        float totalTilesW = Columns * TileWidth + (Columns - 1) * TileGap;
        float startX = Mathf.Max(6f, (contentW - totalTilesW) * 0.5f);
        float startY = 10f;

        visibleTiles.Clear();
        int idx = 0;
        foreach (TileView tile in allTiles)
        {
            bool match = ClassifyShopCard(tile.card) == activeTab && CardShop.IsAvailable(tile.card);
            tile.rect.gameObject.SetActive(match);
            if (!match) continue;

            int col = idx % Columns;
            int row = idx / Columns;
            tile.rect.anchoredPosition = new Vector2(
                startX + col * (TileWidth + TileGap) + TileWidth * 0.5f,
                -(startY + row * (TileHeight + TileGap) + TileHeight * 0.5f));
            visibleTiles.Add(tile);
            idx++;
        }

        int totalRows = Mathf.Max(1, (visibleTiles.Count + Columns - 1) / Columns);
        float contentH = visibleTiles.Count > 0 ? startY * 2f + totalRows * TileHeight + (totalRows - 1) * TileGap : 200f;
        gridContent.sizeDelta = new Vector2(0f, contentH);
        gridContent.anchoredPosition = Vector2.zero;

        if (emptyGridLabel != null)
        {
            emptyGridLabel.gameObject.SetActive(visibleTiles.Count == 0);
        }

        if (selectedCard == null || !CardShop.IsAvailable(selectedCard) || ClassifyShopCard(selectedCard) != activeTab)
        {
            selectedCard = visibleTiles.Count > 0 ? visibleTiles[0].card : null;
            selectedQuantity = 1;
            if (qtyInput != null) qtyInput.SetTextWithoutNotify("1");
        }

        Refresh();
    }

    private void SelectCard(CardData card)
    {
        if (card == null) return;
        HideIslandPopover();
        selectedCard = card;
        selectedQuantity = 1;
        if (qtyInput != null) qtyInput.SetTextWithoutNotify("1");
        Refresh();
    }

    private void Refresh()
    {
        if (!IsOpen) return;

        Dictionary<string, int> hand = CardShop.CountHand();
        int balance = wallet != null ? wallet.Balance : 0;

        foreach (TileView tile in visibleTiles)
        {
            bool isSelected = tile.card == selectedCard;
            if (tile.selectionBorder != null) tile.selectionBorder.enabled = isSelected;

            int price = CardShop.GetPrice(tile.card);
            int minRequired = CardShop.GetRequiredCoins(tile.card, 1);
            tile.priceText.text = FormatNumber(price);
            tile.priceText.color = balance >= minRequired ? LumiWorldUITheme.Text : LumiWorldUITheme.Error;
        }

        RefreshInspector(hand);
    }

    private void RefreshInspector(Dictionary<string, int> hand)
    {
        if (selectedCard == null)
        {
            detailTitle.text = "No card selected";
            detailOwned.text = "";
            detailBuyPriceValue.text = "-";
            detailFeeValue.text = "-";
            totalPriceText.text = "-";
            if (detailRarityFrame != null) detailRarityFrame.enabled = false;
            if (detailRaiseArrow != null) detailRaiseArrow.enabled = false;
            if (detailLowerArrow != null) detailLowerArrow.enabled = false;
            if (detailIslandPillBtn != null) detailIslandPillBtn.gameObject.SetActive(false);
            buyButton.interactable = false;
            buyBtnBg.color = new Color(0.62f, 0.59f, 0.55f, 1f);
            buyBtnLabel.text = "Buy card";
            return;
        }

        if (selectedCard.cardImage != null)
        {
            detailArt.sprite = selectedCard.cardImage;
            detailArt.enabled = true;
            detailFallbackBg.enabled = false;
            detailFallbackInitials.gameObject.SetActive(false);
            CardRarityFrameUtility.ApplyCardOverlays(
                detailArtBoxRect != null ? detailArtBoxRect : detailArt.rectTransform,
                detailArt.rectTransform,
                selectedCard,
                ref detailRarityFrame,
                ref detailRaiseArrow,
                ref detailLowerArrow);
        }
        else
        {
            detailArt.enabled = false;
            if (detailRarityFrame != null) detailRarityFrame.enabled = false;
            if (detailRaiseArrow != null) detailRaiseArrow.enabled = false;
            if (detailLowerArrow != null) detailLowerArrow.enabled = false;
            detailFallbackBg.enabled = true;
            detailFallbackBg.color = TypeColor(selectedCard.cardType);
            detailFallbackInitials.gameObject.SetActive(true);
            detailFallbackInitials.text = Initials(CardShop.CardName(selectedCard));
        }

        detailTitle.text = CardShop.CardName(selectedCard);
        hand.TryGetValue(CardShop.CardKey(selectedCard), out int owned);
        detailOwned.text = "Owned " + FormatNumber(owned);

        bool isNature = ClassifyShopCard(selectedCard) == ShopTab.Nature;
        if (detailIslandPillBtn != null)
        {
            detailIslandPillBtn.gameObject.SetActive(isNature);
            if (isNature)
            {
                string islandName = LumiWorldUITheme.GetCardOriginIslandName(selectedCard);
                detailIslandGlyph.sprite = LumiWorldUITheme.IslandGlyph(islandName);
                detailIslandLabel.text = islandName;
            }
        }

        int unitPrice = CardShop.GetPrice(selectedCard);
        int fee = Mathf.Max(0, selectedCard.placementFee);
        int totalCost = unitPrice * selectedQuantity;
        int required = CardShop.GetRequiredCoins(selectedCard, selectedQuantity);
        bool affordable = wallet != null && wallet.CanAfford(required);
        bool canBuy = CardShop.CanBuy(selectedCard, selectedQuantity);

        detailBuyPriceValue.text = FormatNumber(unitPrice) + " coins";
        detailFeeValue.text = FormatNumber(fee) + " coins";

        totalPriceText.text = FormatNumber(totalCost) + " coins";
        totalPriceText.color = affordable ? LumiWorldUITheme.Text : LumiWorldUITheme.Error;

        buyButton.interactable = canBuy;
        buyBtnBg.color = canBuy ? LumiWorldUITheme.Primary : new Color(0.62f, 0.59f, 0.55f, 1f);
        buyBtnIcon.color = Color.white;
        buyBtnLabel.color = Color.white;
        buyBtnLabel.text = affordable ? "Buy card" : "Need " + FormatNumber(required);
    }

    private void ShowIslandPopover(CardData card, RectTransform anchor)
    {
        if (islandPopover == null || card == null || anchor == null) return;
        string islandName = LumiWorldUITheme.GetCardOriginIslandName(card);
        islandPopoverGlyph.sprite = LumiWorldUITheme.IslandGlyph(islandName);
        islandPopoverTitle.text = islandName;
        islandPopoverSub.text = "Island origin";

        islandPopover.SetActive(true);
        islandPopover.transform.SetAsLastSibling();

        Vector3 worldPos = anchor.TransformPoint(new Vector3(anchor.rect.width * 0.5f, anchor.rect.height + 6f, 0f));
        Vector2 localPoint = panel.InverseTransformPoint(worldPos);
        float halfW = panel.rect.width * 0.5f;
        float halfH = panel.rect.height * 0.5f;
        localPoint.x = Mathf.Clamp(localPoint.x, -halfW + 96f, halfW - 96f);
        localPoint.y = Mathf.Clamp(localPoint.y, -halfH + 40f, halfH - 54f);
        islandPopoverCard.anchoredPosition = localPoint;
    }

    private void HideIslandPopover()
    {
        if (islandPopover != null && islandPopover.activeSelf)
        {
            islandPopover.SetActive(false);
        }
    }

    private void OnBuyClicked()
    {
        if (selectedCard == null) return;
        CardData cardToBuy = selectedCard;
        int qty = Mathf.Clamp(selectedQuantity, 1, 99);
        int unitPrice = CardShop.GetPrice(cardToBuy);
        int totalPrice = unitPrice * qty;
        string cardName = CardShop.CardName(cardToBuy);

        string question = qty == 1
            ? $"Buy 1× {cardName} for {totalPrice} Coins?"
            : $"Buy {qty}× {cardName} for {totalPrice} Coins ({unitPrice} each)?";

        GameDialogUI.ShowConfirm(question, () =>
        {
            if (!CardShop.TryBuy(cardToBuy, qty))
            {
                GameDialogUI.ShowNotice("Not enough Coins to buy this card!");
                return;
            }

            TileView tile = visibleTiles.Find(t => t.card == cardToBuy);
            if (tile != null)
            {
                Punch(ref tile.punch, tile.rect, 1.05f);
                string floatTxt = qty == 1 ? "+1 card" : $"+{qty} cards";
                StartCoroutine(EconomyHUD.FloatText(tile.floatLayer, floatTxt, LumiWorldUITheme.Success, 20f,
                    new Vector2(0f, 16f), new Vector2(0f, 54f), 1f));
            }
            Refresh();
        }, title: "BUY CARD");
    }

    private void Punch(ref Coroutine routine, Transform target, float peak)
    {
        if (target == null || !isActiveAndEnabled) return;
        if (routine != null) StopCoroutine(routine);
        target.localScale = Vector3.one;
        routine = StartCoroutine(EconomyHUD.Punch(target, peak, 0.22f));
    }

    private void BuildOpenButton()
    {
        // Legacy hidden anchor kept only for CollapsibleHUDToolbar cleanup compatibility
        openButton = EconomyHUD.CreateRect("ShopButton", transform);
        openButton.anchorMin = new Vector2(1f, 1f);
        openButton.anchorMax = new Vector2(1f, 1f);
        openButton.pivot = new Vector2(0.5f, 0.5f);
        openButton.sizeDelta = buttonSize;
        openButton.anchoredPosition = buttonCorner - buttonSize * 0.5f;
        var cg = openButton.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;
    }

    private void BuildWindow()
    {
        RectTransform windowRect = EconomyHUD.CreateRect("Window", transform);
        LumiWorldUITheme.Stretch(windowRect);
        window = windowRect.gameObject;
        EconomyHUD.RegisterWindow(window, Close);

        // Backdrop
        GameObject bdObj = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        bdObj.transform.SetParent(windowRect, false);
        LumiWorldUITheme.Stretch(bdObj.GetComponent<RectTransform>());
        bdObj.GetComponent<Image>().color = LumiWorldUITheme.Backdrop;
        Button bdBtn = bdObj.GetComponent<Button>();
        bdBtn.transition = Selectable.Transition.None;
        bdBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        bdBtn.onClick.AddListener(Close);

        // Responsive FitScale stage (matches P-00 / P-01 / P-05)
        GameObject stageObj = new GameObject("FitStage", typeof(RectTransform));
        stageObj.transform.SetParent(windowRect, false);
        stageRect = stageObj.GetComponent<RectTransform>();
        SyncStageScale(force: true);

        // Main Window
        Image panelImg = LumiWorldUITheme.CreateRoundedSurface(
            stageRect,
            "Panel",
            LumiWorldUITheme.Window,
            LumiWorldUITheme.Edge,
            24,
            1.5f,
            6f,
            0.22f,
            raycastTarget: true);
        panel = panelImg.rectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panel.anchoredPosition = Vector2.zero;

        // Header (No LumiWorld logo)
        GameObject hdrIconObj = new GameObject("HeaderIcon", typeof(RectTransform), typeof(Image));
        hdrIconObj.transform.SetParent(panel, false);
        LumiWorldUITheme.PlaceTopLeft(hdrIconObj.GetComponent<RectTransform>(), 24f, 18f, 24f, 24f);
        Image hdrIcon = hdrIconObj.GetComponent<Image>();
        hdrIcon.sprite = LumiWorldUITheme.Glyph("G-03");
        hdrIcon.color = LumiWorldUITheme.Text;
        hdrIcon.preserveAspect = true;
        hdrIcon.raycastTarget = false;

        TextMeshProUGUI titleTmp = LumiWorldUITheme.CreateLabel(panel, "Title", "Card Shop", 20f, LumiWorldUITheme.Text, true);
        LumiWorldUITheme.PlaceTopLeft(titleTmp.rectTransform, 56f, 13f, 260f, 34f);

        LumiWorldUITheme.CreateCloseButton(panel, 22f, 14f, Close, 34f);

        // Category Sub-Tabs (Nature | Creatures | Special if present)
        IReadOnlyList<CardData> catalogCards = CardShop.Cards;
        bool hasSpecial = false;
        foreach (CardData c in catalogCards)
        {
            if (ClassifyShopCard(c) == ShopTab.Special) { hasSpecial = true; break; }
        }

        BuildTabs(hasSpecial);
        BuildLeftGrid(catalogCards);
        BuildRightInspector();
        BuildIslandPopover();

        window.SetActive(false);
    }

    private void BuildTabs(bool includeSpecial)
    {
        var defs = new List<(ShopTab tab, string label, string glyph, float w)>
        {
            (ShopTab.Nature, "Nature", "G-11", 122f),
            (ShopTab.Creatures, "Creatures", "G-12", 134f)
        };
        if (includeSpecial)
        {
            defs.Add((ShopTab.Special, "Special", "G-14", 122f));
        }

        float curX = 24f;
        tabViews.Clear();
        foreach (var d in defs)
        {
            ShopTab captured = d.tab;
            Button btn = LumiWorldUITheme.CreateSubTabButton(
                panel,
                "Tab_" + d.label,
                d.label,
                d.glyph,
                13.5f,
                () => SelectTab(captured),
                out Image bg,
                out Image ic,
                out TextMeshProUGUI lbl,
                out Shadow sh);
            LumiWorldUITheme.PlaceTopLeft((RectTransform)btn.transform, curX, 60f, d.w, 32f);
            curX += d.w + 8f;

            tabViews.Add(new TabView { tab = d.tab, button = btn, bg = bg, icon = ic, label = lbl, shadow = sh });
        }
    }

    private void BuildLeftGrid(IReadOnlyList<CardData> catalogCards)
    {
        Image wellImg = LumiWorldUITheme.CreateRoundedSurface(
            panel,
            "GridWell",
            LumiWorldUITheme.Well,
            new Color(0.84f, 0.80f, 0.75f, 1f),
            18,
            1.2f,
            0f,
            0f,
            raycastTarget: true);
        LumiWorldUITheme.PlaceTopLeft(wellImg.rectTransform, 24f, 102f, GridAreaWidth, 434f);

        GameObject scrollObj = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
        scrollObj.transform.SetParent(wellImg.transform, false);
        RectTransform scrollRt = scrollObj.GetComponent<RectTransform>();
        LumiWorldUITheme.Stretch(scrollRt, 4f);
        ScrollRect scroll = scrollObj.GetComponent<ScrollRect>();

        GameObject vpObj = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        vpObj.transform.SetParent(scrollRt, false);
        RectTransform vpRt = vpObj.GetComponent<RectTransform>();
        LumiWorldUITheme.Stretch(vpRt);
        vpObj.GetComponent<Image>().color = Color.clear;

        GameObject contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(vpRt, false);
        gridContent = contentObj.GetComponent<RectTransform>();
        gridContent.anchorMin = new Vector2(0f, 1f);
        gridContent.anchorMax = new Vector2(1f, 1f);
        gridContent.pivot = new Vector2(0.5f, 1f);
        gridContent.anchoredPosition = Vector2.zero;
        gridContent.sizeDelta = new Vector2(0f, 300f);

        scroll.viewport = vpRt;
        scroll.content = gridContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 18f;

        emptyGridLabel = LumiWorldUITheme.CreateLabel(vpRt, "Empty", "No cards in this category yet.", 15f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.Center);
        LumiWorldUITheme.Stretch(emptyGridLabel.rectTransform);
        emptyGridLabel.gameObject.SetActive(false);

        allTiles.Clear();
        for (int i = 0; i < catalogCards.Count; i++)
        {
            CardData c = catalogCards[i];
            if (c == null) continue;
            allTiles.Add(CreateTile(gridContent, c));
        }
    }

    private TileView CreateTile(RectTransform parent, CardData card)
    {
        TileView tile = new TileView { card = card };

        Image bgImg = LumiWorldUITheme.CreateRoundedSurface(
            parent,
            "Tile_" + card.name,
            LumiWorldUITheme.Slot,
            new Color(0.88f, 0.85f, 0.80f, 1f),
            15,
            1.2f,
            2.5f,
            0.13f,
            raycastTarget: true);
        tile.rect = bgImg.rectTransform;
        tile.rect.anchorMin = tile.rect.anchorMax = new Vector2(0f, 1f);
        tile.rect.pivot = new Vector2(0.5f, 0.5f);
        tile.rect.sizeDelta = new Vector2(TileWidth, TileHeight);

        Button cardBtn = bgImg.gameObject.AddComponent<Button>();
        cardBtn.targetGraphic = bgImg;
        LumiWorldUITheme.ApplyButtonDisabledNeutral(cardBtn);
        cardBtn.onClick.AddListener(() => SelectCard(card));

        // Left vertical Card Art (exact 2:3 74 x 111 inside 174 x 132 tile so overlays fit edge-to-edge)
        GameObject artObj = new GameObject("Art", typeof(RectTransform));
        artObj.transform.SetParent(tile.rect, false);
        RectTransform artRt = artObj.GetComponent<RectTransform>();
        LumiWorldUITheme.PlaceTopLeft(artRt, 11f, 10.5f, 74f, 111f);
        if (card.cardImage != null)
        {
            GameObject maskObj = new GameObject("ArtMask", typeof(RectTransform), typeof(Image), typeof(Mask));
            maskObj.transform.SetParent(artRt, false);
            RectTransform maskRt = maskObj.GetComponent<RectTransform>();
            LumiWorldUITheme.Stretch(maskRt);
            maskRt.offsetMin = new Vector2(1f, 1f);
            maskRt.offsetMax = new Vector2(-1f, -1f);
            Image maskImg = maskObj.GetComponent<Image>();
            maskImg.sprite = CardRarityFrameUtility.GetRoundedMaskSprite(6);
            maskImg.type = Image.Type.Sliced;
            maskImg.color = Color.white;
            maskImg.raycastTarget = false;
            maskObj.GetComponent<Mask>().showMaskGraphic = false;

            GameObject imgObj = new GameObject("ArtImage", typeof(RectTransform), typeof(Image));
            imgObj.transform.SetParent(maskRt, false);
            LumiWorldUITheme.Stretch(imgObj.GetComponent<RectTransform>());
            Image artImg = imgObj.GetComponent<Image>();
            artImg.raycastTarget = false;
            artImg.sprite = card.cardImage;
            artImg.preserveAspect = false;

            CardRarityFrameUtility.ApplyCardOverlays(
                artRt,
                artImg.rectTransform,
                card,
                ref tile.rarityFrameOverlay,
                ref tile.raiseArrow,
                ref tile.lowerArrow);
        }
        else
        {
            Image artImg = artObj.AddComponent<Image>();
            artImg.raycastTarget = false;
            artImg.sprite = LumiWorldUITheme.SurfaceSprite;
            artImg.type = Image.Type.Sliced;
            artImg.color = TypeColor(card.cardType);
            TextMeshProUGUI initTmp = LumiWorldUITheme.CreateLabel(artObj.transform, "Init", Initials(CardShop.CardName(card)), 22f, Color.white, true, TextAlignmentOptions.Center);
            LumiWorldUITheme.Stretch(initTmp.rectTransform);
        }

        // Right Column inside Tile
        float infoX = 92f;
        float infoW = TileWidth - infoX - 6f;
        bool isNature = ClassifyShopCard(card) == ShopTab.Nature;

        if (isNature)
        {
            string islandName = LumiWorldUITheme.GetCardOriginIslandName(card);
            GameObject iBtnObj = new GameObject("IslandOriginBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            iBtnObj.transform.SetParent(tile.rect, false);
            RectTransform iRt = iBtnObj.GetComponent<RectTransform>();
            LumiWorldUITheme.PlaceTopLeft(iRt, infoX, 12f, 24f, 24f);

            Image iBg = iBtnObj.GetComponent<Image>();
            iBg.sprite = LumiWorldUITheme.PillSprite;
            iBg.type = Image.Type.Sliced;
            iBg.color = LumiWorldUITheme.Window;
            iBg.raycastTarget = true;

            GameObject iIcObj = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
            iIcObj.transform.SetParent(iRt, false);
            LumiWorldUITheme.Stretch(iIcObj.GetComponent<RectTransform>(), 4f);
            Image iIc = iIcObj.GetComponent<Image>();
            iIc.sprite = LumiWorldUITheme.IslandGlyph(islandName);
            iIc.color = LumiWorldUITheme.Text;
            iIc.preserveAspect = true;
            iIc.raycastTarget = false;

            Button iBtn = iBtnObj.GetComponent<Button>();
            iBtn.targetGraphic = iBg;
            LumiWorldUITheme.ApplyButtonDisabledNeutral(iBtn);
            iBtn.onClick.AddListener(() => ShowIslandPopover(card, iRt));
            tile.islandButton = iBtn;
        }

        tile.nameText = LumiWorldUITheme.CreateLabel(
            tile.rect,
            "CardName",
            CardShop.CardName(card),
            12.5f,
            LumiWorldUITheme.Text,
            false,
            TextAlignmentOptions.TopLeft,
            wrap: true);
        tile.nameText.enableAutoSizing = true;
        tile.nameText.fontSizeMin = 10f;
        tile.nameText.fontSizeMax = 13f;
        LumiWorldUITheme.PlaceTopLeft(tile.nameText.rectTransform, infoX, isNature ? 40f : 16f, infoW, 44f);

        // Coin Price Row at bottom right of tile
        GameObject coinObj = new GameObject("Coin", typeof(RectTransform), typeof(Image));
        coinObj.transform.SetParent(tile.rect, false);
        LumiWorldUITheme.PlaceTopLeft(coinObj.GetComponent<RectTransform>(), infoX, 100f, 16f, 16f);
        Image coinImg = coinObj.GetComponent<Image>();
        coinImg.sprite = EconomyHUD.CoinSprite;
        coinImg.preserveAspect = true;
        coinImg.raycastTarget = false;

        tile.priceText = LumiWorldUITheme.CreateLabel(tile.rect, "Price", FormatNumber(CardShop.GetPrice(card)), 13.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(tile.priceText.rectTransform, infoX + 20f, 97f, infoW - 20f, 22f);

        tile.selectionBorder = LumiWorldUITheme.CreateSelectionBorder(tile.rect, 15, 2.5f);
        tile.selectionBorder.enabled = false;

        tile.floatLayer = EconomyHUD.CreateRect("Floating", tile.rect);
        LumiWorldUITheme.Stretch(tile.floatLayer);

        return tile;
    }

    private void BuildRightInspector()
    {
        Image inspImg = LumiWorldUITheme.CreateRoundedSurface(
            panel,
            "DetailInspector",
            LumiWorldUITheme.Surface,
            new Color(0.87f, 0.84f, 0.79f, 1f),
            20,
            1.4f,
            4f,
            0.16f,
            raycastTarget: true);
        RectTransform inspRt = inspImg.rectTransform;
        LumiWorldUITheme.PlaceTopLeft(inspRt, 594f, 60f, 262f, 476f);

        // Card Art Box (exact 2:3 80 x 120)
        GameObject artBox = new GameObject("PreviewArt", typeof(RectTransform), typeof(Image));
        artBox.transform.SetParent(inspRt, false);
        detailArtBoxRect = artBox.GetComponent<RectTransform>();
        LumiWorldUITheme.PlaceTopLeft(detailArtBoxRect, 20f, 16f, 80f, 120f);
        detailFallbackBg = artBox.GetComponent<Image>();
        detailFallbackBg.sprite = LumiWorldUITheme.SurfaceSprite;
        detailFallbackBg.type = Image.Type.Sliced;
        detailFallbackBg.raycastTarget = false;
        detailFallbackInitials = LumiWorldUITheme.CreateLabel(artBox.transform, "Init", "", 24f, Color.white, true, TextAlignmentOptions.Center);
        LumiWorldUITheme.Stretch(detailFallbackInitials.rectTransform);

        GameObject detailMaskObj = new GameObject("ArtMask", typeof(RectTransform), typeof(Image), typeof(Mask));
        detailMaskObj.transform.SetParent(detailArtBoxRect, false);
        RectTransform detailMaskRt = detailMaskObj.GetComponent<RectTransform>();
        LumiWorldUITheme.Stretch(detailMaskRt);
        detailMaskRt.offsetMin = new Vector2(1f, 1f);
        detailMaskRt.offsetMax = new Vector2(-1f, -1f);
        Image detailMaskImg = detailMaskObj.GetComponent<Image>();
        detailMaskImg.sprite = CardRarityFrameUtility.GetRoundedMaskSprite(6);
        detailMaskImg.type = Image.Type.Sliced;
        detailMaskImg.color = Color.white;
        detailMaskImg.raycastTarget = false;
        detailMaskObj.GetComponent<Mask>().showMaskGraphic = false;

        GameObject imgObj = new GameObject("Sprite", typeof(RectTransform), typeof(Image));
        imgObj.transform.SetParent(detailMaskRt, false);
        LumiWorldUITheme.Stretch(imgObj.GetComponent<RectTransform>());
        detailArt = imgObj.GetComponent<Image>();
        detailArt.preserveAspect = false;
        detailArt.raycastTarget = false;
        CardRarityFrameUtility.EnsureOverlayChildren(
            detailArtBoxRect,
            detailArt.rectTransform,
            ref detailRarityFrame,
            ref detailRaiseArrow,
            ref detailLowerArrow);

        detailTitle = LumiWorldUITheme.CreateLabel(inspRt, "Title", "Leafwood", 18.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.TopLeft, wrap: true);
        detailTitle.enableAutoSizing = true;
        detailTitle.fontSizeMin = 13f;
        detailTitle.fontSizeMax = 19f;
        LumiWorldUITheme.PlaceTopLeft(detailTitle.rectTransform, 114f, 18f, 134f, 52f);

        detailOwned = LumiWorldUITheme.CreateLabel(inspRt, "Owned", "Owned 0", 12.5f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(detailOwned.rectTransform, 114f, 74f, 134f, 20f);

        // Island Origin Pill (clickable on Nature cards)
        GameObject islBtnObj = new GameObject("DetailIslandPill", typeof(RectTransform), typeof(Image), typeof(Button));
        islBtnObj.transform.SetParent(inspRt, false);
        RectTransform islRt = islBtnObj.GetComponent<RectTransform>();
        LumiWorldUITheme.PlaceTopLeft(islRt, 114f, 100f, 134f, 26f);
        Image islBg = islBtnObj.GetComponent<Image>();
        islBg.sprite = LumiWorldUITheme.PillSprite;
        islBg.type = Image.Type.Sliced;
        islBg.color = LumiWorldUITheme.Window;

        GameObject islIcObj = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
        islIcObj.transform.SetParent(islRt, false);
        LumiWorldUITheme.PlaceTopLeft(islIcObj.GetComponent<RectTransform>(), 7f, 5f, 16f, 16f);
        detailIslandGlyph = islIcObj.GetComponent<Image>();
        detailIslandGlyph.color = LumiWorldUITheme.Text;
        detailIslandGlyph.preserveAspect = true;
        detailIslandGlyph.raycastTarget = false;

        detailIslandLabel = LumiWorldUITheme.CreateLabel(islRt, "Text", "Starter plain", 11f, LumiWorldUITheme.Text, false, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(detailIslandLabel.rectTransform, 27f, 2f, 100f, 22f);
        detailIslandPillBtn = islBtnObj.GetComponent<Button>();
        detailIslandPillBtn.targetGraphic = islBg;
        LumiWorldUITheme.ApplyButtonDisabledNeutral(detailIslandPillBtn);
        detailIslandPillBtn.onClick.AddListener(() => ShowIslandPopover(selectedCard, islRt));

        // Divider
        GameObject div = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        div.transform.SetParent(inspRt, false);
        LumiWorldUITheme.PlaceTopLeft(div.GetComponent<RectTransform>(), 16f, 148f, 230f, 1.5f);
        div.GetComponent<Image>().color = LumiWorldUITheme.Edge;

        // Price Breakdown Rows (No "when placing later")
        detailBuyPriceValue = BuildDetailCostRow(inspRt, 164f, "Buy price", "G-26");
        detailFeeValue = BuildDetailCostRow(inspRt, 206f, "Placement fee", "G-34");

        // Quantity Stepper
        float stepY = 264f;
        LumiWorldUITheme.CreatePillButton(inspRt, "MinusBtn", "", "Minus", false, 14f, () => StepQuantity(-1), out Image mBg, out _, out _, 14);
        LumiWorldUITheme.PlaceTopLeft((RectTransform)mBg.transform, 42f, stepY, 36f, 34f);
        minusBtn = mBg.GetComponent<Button>();

        Image qtyBoxImg = LumiWorldUITheme.CreateRoundedSurface(inspRt, "QtyBox", LumiWorldUITheme.Slot, LumiWorldUITheme.Edge, 12, 1.2f, 0f, 0f, true);
        LumiWorldUITheme.PlaceTopLeft(qtyBoxImg.rectTransform, 86f, stepY, 90f, 34f);
        TextMeshProUGUI qtyText = LumiWorldUITheme.CreateLabel(qtyBoxImg.transform, "QtyText", "1", 15f, LumiWorldUITheme.Text, true, TextAlignmentOptions.Center);
        LumiWorldUITheme.Stretch(qtyText.rectTransform);
        qtyInput = qtyBoxImg.gameObject.AddComponent<TMP_InputField>();
        qtyInput.targetGraphic = qtyBoxImg;
        qtyInput.textComponent = qtyText;
        qtyInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        qtyInput.characterLimit = 2;
        qtyInput.lineType = TMP_InputField.LineType.SingleLine;
        qtyInput.SetTextWithoutNotify("1");
        qtyInput.onValueChanged.AddListener(v =>
        {
            if (int.TryParse(v, out int parsed))
            {
                selectedQuantity = Mathf.Clamp(parsed, 1, 99);
                Refresh();
            }
        });
        qtyInput.onEndEdit.AddListener(v =>
        {
            selectedQuantity = int.TryParse(v, out int parsed) ? Mathf.Clamp(parsed, 1, 99) : 1;
            qtyInput.SetTextWithoutNotify(selectedQuantity.ToString(CultureInfo.InvariantCulture));
            Refresh();
        });

        LumiWorldUITheme.CreatePillButton(inspRt, "PlusBtn", "", "Plus", false, 14f, () => StepQuantity(1), out Image pBg, out _, out _, 14);
        LumiWorldUITheme.PlaceTopLeft((RectTransform)pBg.transform, 184f, stepY, 36f, 34f);
        plusBtn = pBg.GetComponent<Button>();

        // Total Row
        TextMeshProUGUI totalLbl = LumiWorldUITheme.CreateLabel(inspRt, "TotalLabel", "Total", 14.5f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(totalLbl.rectTransform, 18f, 326f, 80f, 28f);

        GameObject totCoinObj = new GameObject("TotalCoin", typeof(RectTransform), typeof(Image));
        totCoinObj.transform.SetParent(inspRt, false);
        LumiWorldUITheme.PlaceTopLeft(totCoinObj.GetComponent<RectTransform>(), 106f, 330f, 20f, 20f);
        Image totCoin = totCoinObj.GetComponent<Image>();
        totCoin.sprite = EconomyHUD.CoinSprite;
        totCoin.preserveAspect = true;
        totCoin.raycastTarget = false;

        totalPriceText = LumiWorldUITheme.CreateLabel(inspRt, "TotalValue", "50 coins", 16f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineRight);
        LumiWorldUITheme.PlaceTopLeft(totalPriceText.rectTransform, 130f, 326f, 114f, 28f);

        // Primary Buy CTA
        buyButton = LumiWorldUITheme.CreatePillButton(
            inspRt,
            "BuyCardBtn",
            "Buy card",
            "G-25",
            true,
            15f,
            OnBuyClicked,
            out buyBtnBg,
            out buyBtnIcon,
            out buyBtnLabel,
            18);
        LumiWorldUITheme.PlaceTopLeft((RectTransform)buyButton.transform, 16f, 412f, 230f, 46f);
    }

    private TextMeshProUGUI BuildDetailCostRow(RectTransform parent, float y, string title, string glyphId)
    {
        Image rowBg = LumiWorldUITheme.CreateRoundedSurface(parent, "Cost_" + title, LumiWorldUITheme.Well, new Color(0.86f, 0.82f, 0.77f, 1f), 11, 1f, 0f, 0f);
        RectTransform rt = rowBg.rectTransform;
        LumiWorldUITheme.PlaceTopLeft(rt, 16f, y, 230f, 34f);

        GameObject icObj = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
        icObj.transform.SetParent(rt, false);
        LumiWorldUITheme.PlaceTopLeft(icObj.GetComponent<RectTransform>(), 10f, 9f, 16f, 16f);
        Image icImg = icObj.GetComponent<Image>();
        icImg.sprite = LumiWorldUITheme.Glyph(glyphId);
        icImg.color = LumiWorldUITheme.Muted;
        icImg.preserveAspect = true;
        icImg.raycastTarget = false;

        TextMeshProUGUI lbl = LumiWorldUITheme.CreateLabel(rt, "Label", title, 12.5f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(lbl.rectTransform, 32f, 5f, 98f, 24f);

        GameObject coinObj = new GameObject("Coin", typeof(RectTransform), typeof(Image));
        coinObj.transform.SetParent(rt, false);
        LumiWorldUITheme.PlaceTopLeft(coinObj.GetComponent<RectTransform>(), 134f, 9f, 16f, 16f);
        Image cImg = coinObj.GetComponent<Image>();
        cImg.sprite = EconomyHUD.CoinSprite;
        cImg.preserveAspect = true;
        cImg.raycastTarget = false;

        TextMeshProUGUI val = LumiWorldUITheme.CreateLabel(rt, "Value", "50 coins", 12.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineRight);
        LumiWorldUITheme.PlaceTopLeft(val.rectTransform, 152f, 5f, 68f, 24f);
        return val;
    }

    private void BuildIslandPopover()
    {
        islandPopover = new GameObject("IslandPopover", typeof(RectTransform), typeof(Image), typeof(Button));
        islandPopover.transform.SetParent(panel, false);
        LumiWorldUITheme.Stretch(islandPopover.GetComponent<RectTransform>());
        islandPopover.GetComponent<Image>().color = Color.clear;
        Button dismiss = islandPopover.GetComponent<Button>();
        dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.AddListener(HideIslandPopover);

        Image cardImg = LumiWorldUITheme.CreateRoundedSurface(
            islandPopover.transform,
            "PopoverCard",
            LumiWorldUITheme.Slot,
            LumiWorldUITheme.Edge,
            14,
            1.5f,
            4f,
            0.22f,
            raycastTarget: true);
        islandPopoverCard = cardImg.rectTransform;
        islandPopoverCard.anchorMin = islandPopoverCard.anchorMax = new Vector2(0.5f, 0.5f);
        islandPopoverCard.pivot = new Vector2(0.5f, 0f);
        islandPopoverCard.sizeDelta = new Vector2(182f, 54f);

        GameObject icObj = new GameObject("IslandGlyph", typeof(RectTransform), typeof(Image));
        icObj.transform.SetParent(islandPopoverCard, false);
        LumiWorldUITheme.PlaceTopLeft(icObj.GetComponent<RectTransform>(), 12f, 13f, 28f, 28f);
        islandPopoverGlyph = icObj.GetComponent<Image>();
        islandPopoverGlyph.color = LumiWorldUITheme.Text;
        islandPopoverGlyph.preserveAspect = true;
        islandPopoverGlyph.raycastTarget = false;

        islandPopoverTitle = LumiWorldUITheme.CreateLabel(islandPopoverCard, "IslandName", "Starter plain", 13.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(islandPopoverTitle.rectTransform, 48f, 8f, 124f, 22f);

        islandPopoverSub = LumiWorldUITheme.CreateLabel(islandPopoverCard, "Sub", "Island origin", 10.5f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(islandPopoverSub.rectTransform, 48f, 28f, 124f, 18f);

        islandPopover.SetActive(false);
    }

    private void StepQuantity(int delta)
    {
        selectedQuantity = Mathf.Clamp(selectedQuantity + delta, 1, 99);
        if (qtyInput != null) qtyInput.SetTextWithoutNotify(selectedQuantity.ToString(CultureInfo.InvariantCulture));
        Refresh();
    }

    private static ShopTab ClassifyShopCard(CardData card)
    {
        if (card == null) return ShopTab.Nature;
        if (card.cardType == CardType.Creature || card.animalSpeciesData != null) return ShopTab.Creatures;
        if (card.IsRiverCard() || card.IsTileTransformCard() || card.cardType == CardType.Special ||
            string.Equals(card.cardID, "Rain", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(card.cardID, "River", StringComparison.OrdinalIgnoreCase))
        {
            return ShopTab.Special;
        }
        return ShopTab.Nature;
    }

    private Color TypeColor(CardType type)
    {
        switch (type)
        {
            case CardType.Creature: return new Color(0.78f, 0.56f, 0.34f, 1f);
            case CardType.Terrain: return LumiWorldUITheme.Success;
            case CardType.Building: return new Color(0.45f, 0.54f, 0.66f, 1f);
            default: return new Color(0.55f, 0.48f, 0.72f, 1f);
        }
    }

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
