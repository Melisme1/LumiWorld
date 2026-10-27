using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LumiWorld.Acs;

/// <summary>
/// Thanh công cụ HUD thu gọn (Collapsible HUD Toolbar) của LumiWorld:
/// - Đặt tại góc 10h bên trên World Map (x=20, y=-14).
/// - Nút "chitiet_button" (viền tròn trắng xanh) để mở/đóng thanh panel ngang.
/// - Khi bấm, thanh panel ("panel_toolbar.png" bo tròn mượt mà chuẩn Image 3) xổ ngang ra.
/// - Chứa trọn bộ 7 nút chức năng:
///   1. Kho tài nguyên (Warehouse)
///   2. Thành tựu (Achievements)
///   3. Đơn hàng (Orders)
///   4. Chợ (Market)
///   5. Cửa hàng (Shop)
///   6. Chế biến (Crafting)
///   7. Cài đặt âm thanh (Settings)
/// - Cả 7 nút đều được bọc viền tròn trắng viền xanh dương đồng bộ hoàn hảo với toàn bộ UI game.
/// - Tự động thu gọn các nút rời cũ để màn hình tinh giản, chuyên nghiệp.
/// </summary>
public class CollapsibleHUDToolbar : MonoBehaviour
{
    private static CollapsibleHUDToolbar instance;
    public static CollapsibleHUDToolbar Instance => instance;

    [Header("Configuration")]
    [SerializeField] private Vector2 toolbarPosition = new Vector2(20f, -14f);
    [SerializeField] private float buttonSize = 48f;
    [SerializeField] private float innerButtonSize = 46f;
    [SerializeField] private float innerIconSize = 30f;
    [SerializeField] private float itemSpacing = 10f;
    [SerializeField] private float panelPadding = 22f;
    [SerializeField] private float toolbarHeight = 56f;

    private RectTransform toolbarContainer;
    private RectTransform toggleBtnRect;
    private RectTransform toolbarPanelRect;
    private RectMask2D toolbarMask;
    private Image toolbarPanelImage;

    private readonly List<RectTransform> buttonRects = new List<RectTransform>();
    private bool isExpanded = true;
    private Coroutine slideRoutine;
    private float targetPanelWidth;

    private static readonly Color ThemeBlue = new Color(0.08f, 0.58f, 0.95f, 0.95f);
    private static readonly Color ButtonNormalColor = Color.white;
    private static readonly Color ButtonHoverColor = new Color(0.92f, 0.96f, 1f, 1f);
    private static readonly Color ButtonPressedColor = new Color(0.85f, 0.92f, 0.98f, 1f);

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void Start()
    {
        BuildToolbar();
        StartCoroutine(HideOldStandaloneButtonsDelayed());
    }

    private IEnumerator HideOldStandaloneButtonsDelayed()
    {
        // Chờ 2 frame để tất cả các UI Overlay khởi tạo xong
        yield return null;
        yield return null;

        HideOldButton("OrdersButton");
        HideOldButton("ShopButton");
        HideOldButton("MarketButton");
        HideOldButton("CraftingOpenButton");
        HideOldButton("WarehouseButton");
        HideOldButton("WarehouseCaption");
        HideOldButton("AchievementHUDButton");
        HideOldButton("AchievementCaption");
    }

    private void HideOldButton(string name)
    {
        GameObject obj = GameObject.Find(name);
        if (obj != null)
        {
            var cg = obj.GetComponent<CanvasGroup>();
            if (cg == null) cg = obj.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }
    }

    private void BuildToolbar()
    {
        // Container độc lập neo ở góc trên bên trái (Top-Left), bên trên World Map
        GameObject containerObj = new GameObject("ToolbarContainer", typeof(RectTransform));
        containerObj.transform.SetParent(transform, false);
        toolbarContainer = containerObj.GetComponent<RectTransform>();
        toolbarContainer.anchorMin = new Vector2(0f, 1f);
        toolbarContainer.anchorMax = new Vector2(0f, 1f);
        toolbarContainer.pivot = new Vector2(0f, 1f);
        toolbarContainer.anchoredPosition = toolbarPosition;
        toolbarContainer.sizeDelta = new Vector2(600f, toolbarHeight);

        // 1. Toggle Button ("chitiet_button")
        BuildToggleButton();

        // 2. Horizontal Expandable Panel ("panel_toolbar")
        BuildHorizontalPanel();

        // 3. Populate the 8 buttons inside the panel
        BuildEightButtons();

        // Mặc định mở rộng để người chơi thấy ngay
        SetExpandedInstant(true);
    }

    private void BuildToggleButton()
    {
        GameObject toggleObj = new GameObject("ToggleMenuButton", typeof(RectTransform), typeof(Image), typeof(Button));
        toggleObj.transform.SetParent(toolbarContainer, false);
        toggleBtnRect = toggleObj.GetComponent<RectTransform>();
        toggleBtnRect.anchorMin = new Vector2(0f, 0.5f);
        toggleBtnRect.anchorMax = new Vector2(0f, 0.5f);
        toggleBtnRect.pivot = new Vector2(0f, 0.5f);
        toggleBtnRect.anchoredPosition = new Vector2(0f, 0f);
        toggleBtnRect.sizeDelta = new Vector2(buttonSize, buttonSize);

        Image btnImg = toggleObj.GetComponent<Image>();
        btnImg.sprite = EconomyHUD.RoundedSprite;
        btnImg.type = Image.Type.Simple;
        btnImg.color = ButtonNormalColor;
        btnImg.raycastTarget = true;
        EconomyHUD.AddBlueBorder(toggleBtnRect, ThemeBlue);

        // Icon 3 chấm 3 gạch
        GameObject iconObj = new GameObject("MenuIcon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(toggleObj.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(28f, 28f);

        Image iconImg = iconObj.GetComponent<Image>();
        Sprite menuSprite = Resources.Load<Sprite>("UI_Buttons/icon_menu");
        iconImg.sprite = menuSprite;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        Button btn = toggleObj.GetComponent<Button>();
        btn.targetGraphic = btnImg;
        var colors = btn.colors;
        colors.normalColor = ButtonNormalColor;
        colors.highlightedColor = ButtonHoverColor;
        colors.pressedColor = ButtonPressedColor;
        btn.colors = colors;
        btn.onClick.AddListener(ToggleExpanded);
    }

    private void BuildHorizontalPanel()
    {
        GameObject panelObj = new GameObject("ToolbarPanel", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        panelObj.transform.SetParent(toolbarContainer, false);
        toolbarPanelRect = panelObj.GetComponent<RectTransform>();
        toolbarPanelRect.anchorMin = new Vector2(0f, 0.5f);
        toolbarPanelRect.anchorMax = new Vector2(0f, 0.5f);
        toolbarPanelRect.pivot = new Vector2(0f, 0.5f);
        toolbarPanelRect.anchoredPosition = new Vector2(buttonSize + 8f, 0f);

        toolbarMask = panelObj.GetComponent<RectMask2D>();

        toolbarPanelImage = panelObj.GetComponent<Image>();
        Sprite panelSprite = Resources.Load<Sprite>("UI_Buttons/panel_toolbar");
        if (panelSprite != null)
        {
            toolbarPanelImage.sprite = panelSprite;
            toolbarPanelImage.type = Image.Type.Sliced;
        }
        else
        {
            toolbarPanelImage.sprite = EconomyHUD.RoundedSprite;
            toolbarPanelImage.type = Image.Type.Sliced;
            toolbarPanelImage.color = new Color(0.27f, 0.31f, 0.37f, 0.95f);
        }

        // 8 buttons: 8 * 46 + 7 * 10 + 2 * 22 = 368 + 70 + 44 = 482px
        targetPanelWidth = 8 * innerButtonSize + 7 * itemSpacing + 2 * panelPadding;
        toolbarPanelRect.sizeDelta = new Vector2(targetPanelWidth, toolbarHeight);
    }

    private void BuildEightButtons()
    {
        // 1. Kho tài nguyên (Warehouse)
        CreateToolbarButton("Btn_Warehouse", "icon_warehouse", 0, () =>
        {
            var ctrl = FindAnyObjectByType<ResourceInventoryUIController>();
            if (ctrl != null)
            {
                ctrl.OpenWarehouse();
            }
            else
            {
                GameObject wh = GameObject.Find("WarehouseButton");
                if (wh != null) wh.GetComponent<Button>()?.onClick.Invoke();
            }
        });

        // 2. Thành tựu (Achievements)
        CreateToolbarButton("Btn_Achievements", "icon_achievements", 1, () =>
        {
            FindAnyObjectByType<AchievementUI>()?.ToggleWindow();
        });

        // 3. Đơn hàng (Orders)
        CreateToolbarButton("Btn_Orders", "icon_orders", 2, () =>
        {
            FindAnyObjectByType<OrderBoardUI>()?.Toggle();
        });

        // 4. Chợ (Market)
        CreateToolbarButton("Btn_Market", "icon_market", 3, () =>
        {
            FindAnyObjectByType<MarketUI>()?.Toggle();
        });

        // 5. Cửa hàng (Shop)
        CreateToolbarButton("Btn_Shop", "icon_shop", 4, () =>
        {
            FindAnyObjectByType<CardShopUI>()?.Toggle();
        });

        // 6. Chế biến (Crafting)
        CreateToolbarButton("Btn_Crafting", "icon_crafting", 5, () =>
        {
            FindAnyObjectByType<LumiWorld.Economy.Crafting.CraftingUI>()?.Toggle();
        });

        // 7. Cho ăn tự động (Auto-Feed)
        CreateToolbarButton("Btn_AutoFeed", "icon_autofeed", 6, () =>
        {
            AutoFeedUI.Instance?.Toggle();
        });

        // 8. Cài đặt âm thanh (Settings)
        CreateToolbarButton("Btn_Settings", "icon_settings", 7, () =>
        {
            var settings = FindAnyObjectByType<GameSettingsUI>();
            if (settings != null) settings.ToggleWindow();
        });
    }

    private void CreateToolbarButton(string name, string iconResource, int index, System.Action onClick)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(toolbarPanelRect, false);
        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0f, 0.5f);
        btnRect.anchorMax = new Vector2(0f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);

        float posX = panelPadding + index * (innerButtonSize + itemSpacing) + innerButtonSize * 0.5f;
        btnRect.anchoredPosition = new Vector2(posX, 0f);
        btnRect.sizeDelta = new Vector2(innerButtonSize, innerButtonSize);
        buttonRects.Add(btnRect);

        Image btnImg = btnObj.GetComponent<Image>();
        btnImg.sprite = EconomyHUD.RoundedSprite;
        btnImg.type = Image.Type.Simple;
        btnImg.color = ButtonNormalColor;
        btnImg.raycastTarget = true;
        EconomyHUD.AddBlueBorder(btnRect, ThemeBlue);

        // Icon inside button
        GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(btnObj.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(innerIconSize, innerIconSize);

        Image iconImg = iconObj.GetComponent<Image>();
        Sprite iconSprite = Resources.Load<Sprite>("UI_Buttons/" + iconResource)
                         ?? Resources.Load<Sprite>(iconResource);
        iconImg.sprite = iconSprite;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = btnImg;
        var colors = btn.colors;
        colors.normalColor = ButtonNormalColor;
        colors.highlightedColor = ButtonHoverColor;
        colors.pressedColor = ButtonPressedColor;
        btn.colors = colors;

        btn.onClick.AddListener(() =>
        {
            GameAudioManager.Instance?.PlayClick();
            onClick?.Invoke();
        });
    }

    public void ToggleExpanded()
    {
        SetExpanded(!isExpanded);
        GameAudioManager.Instance?.PlayClick();
    }

    public void SetExpanded(bool expand)
    {
        isExpanded = expand;
        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(AnimateSlide(expand));
    }

    private void SetExpandedInstant(bool expand)
    {
        isExpanded = expand;
        float w = expand ? targetPanelWidth : 0f;
        if (toolbarPanelRect != null)
        {
            toolbarPanelRect.sizeDelta = new Vector2(w, toolbarHeight);
            toolbarPanelRect.gameObject.SetActive(expand);
        }
    }

    private IEnumerator AnimateSlide(bool expand)
    {
        const float duration = 0.2f;
        float time = 0f;
        float startWidth = toolbarPanelRect.sizeDelta.x;
        float endWidth = expand ? targetPanelWidth : 0f;

        if (expand) toolbarPanelRect.gameObject.SetActive(true);

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            float curWidth = Mathf.Lerp(startWidth, endWidth, smoothT);
            toolbarPanelRect.sizeDelta = new Vector2(curWidth, toolbarHeight);
            yield return null;
        }

        toolbarPanelRect.sizeDelta = new Vector2(endWidth, toolbarHeight);
        if (!expand) toolbarPanelRect.gameObject.SetActive(false);
        slideRoutine = null;
    }
}
