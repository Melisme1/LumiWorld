using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LumiWorld.Acs;

/// <summary>
/// Top-left collapsible gameplay toolbar & top-right utility bar (P-03 / §5 HUD spec):
/// - Top-left: Toggle button + expandable bar with [Inventory, Crafting, Card Shop, Orders, Market, Auto Feeding, Creation Lab].
/// - Hovering any button displays a crisp short-name tooltip pill below it.
/// - Top-right: [World Map (G-10)] + [CoinHUD pill] + [Settings (G-09)] aligned on a single clean row.
/// - Achievements are accessed by clicking the bottom-left Harmony progress badge (ScoreUI).
/// </summary>
public class CollapsibleHUDToolbar : MonoBehaviour
{
    private static CollapsibleHUDToolbar instance;
    public static CollapsibleHUDToolbar Instance => instance;

    [Header("Configuration")]
    [SerializeField] private Vector2 toolbarPosition = new Vector2(20f, -14f);
    [SerializeField] private float buttonSize = 50f;
    [SerializeField] private float innerButtonSize = 46f;
    [SerializeField] private float innerIconSize = 28f;
    [SerializeField] private float itemSpacing = 10f;
    [SerializeField] private float panelPadding = 16f;
    [SerializeField] private float toolbarHeight = 56f;

    private RectTransform toolbarContainer;
    private RectTransform toggleBtnRect;
    private RectTransform toolbarPanelRect;
    private Image toolbarPanelImage;

    private RectTransform topRightContainer;
    private RectTransform hoverTooltipRect;
    private TextMeshProUGUI hoverTooltipText;

    private readonly List<RectTransform> buttonRects = new List<RectTransform>();
    private GameObject craftingBadgeObj;
    private TextMeshProUGUI craftingBadgeText;
    private int pendingCraftingReadyCount;
    private bool isExpanded = true;
    private Coroutine slideRoutine;
    private float targetPanelWidth;

    private static readonly Color ButtonNormalColor = LumiWorldUITheme.Slot;
    private static readonly Color ButtonHoverColor = LumiWorldUITheme.Hover;
    private static readonly Color ButtonPressedColor = LumiWorldUITheme.Inset;

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
        BuildTopRightBar();
        BuildHoverTooltip();
        StartCoroutine(HideOldStandaloneButtonsDelayed());
    }

    private IEnumerator HideOldStandaloneButtonsDelayed()
    {
        yield return null;
        yield return null;

        HideOldButton("OrdersButton");
        HideOldButton("ShopButton");
        HideOldButton("MarketButton");
        HideOldButton("AchievementHUDButton");
        HideOldButton("AchievementCaption");
    }

    private void HideOldButton(string objName)
    {
        GameObject obj = GameObject.Find(objName);
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
        GameObject containerObj = new GameObject("ToolbarContainer", typeof(RectTransform));
        containerObj.transform.SetParent(transform, false);
        toolbarContainer = containerObj.GetComponent<RectTransform>();
        toolbarContainer.anchorMin = new Vector2(0f, 1f);
        toolbarContainer.anchorMax = new Vector2(0f, 1f);
        toolbarContainer.pivot = new Vector2(0f, 1f);
        toolbarContainer.anchoredPosition = toolbarPosition;
        toolbarContainer.sizeDelta = new Vector2(560f, toolbarHeight);

        BuildToggleButton();
        BuildHorizontalPanel();
<<<<<<< HEAD

        // 3. Populate the 9 buttons inside the panel
        BuildNineButtons();

        // Mặc định mở rộng để người chơi thấy ngay
=======
        BuildGameplayButtons();
>>>>>>> Acs
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
        toggleBtnRect.anchoredPosition = Vector2.zero;
        toggleBtnRect.sizeDelta = new Vector2(buttonSize, buttonSize);

        Image btnImg = toggleObj.GetComponent<Image>();
        btnImg.sprite = LumiWorldUITheme.SurfaceSprite ?? EconomyHUD.RoundedSprite;
        btnImg.type = Image.Type.Sliced;
        btnImg.color = ButtonNormalColor;
        btnImg.raycastTarget = true;
        LumiWorldUITheme.AddSoftElevation(toggleObj, 3f, 0.16f);

        GameObject iconObj = new GameObject("MenuIcon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(toggleObj.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(26f, 26f);

        Image iconImg = iconObj.GetComponent<Image>();
        iconImg.sprite = LumiWorldUITheme.Glyph("Menu") ?? Resources.Load<Sprite>("UI_Buttons/icon_menu");
        iconImg.color = LumiWorldUITheme.Glyph("Menu") != null ? LumiWorldUITheme.Text : Color.white;
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

        AttachTooltipTrigger(toggleBtnRect, "Menu");
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

        toolbarPanelImage = panelObj.GetComponent<Image>();
        toolbarPanelImage.sprite = LumiWorldUITheme.SurfaceSprite ?? EconomyHUD.RoundedSprite;
        toolbarPanelImage.type = Image.Type.Sliced;
        toolbarPanelImage.color = LumiWorldUITheme.Window;
        LumiWorldUITheme.AddSoftElevation(panelObj, 3.5f, 0.16f);

<<<<<<< HEAD
        // 9 buttons: 9 * 46 + 8 * 10 + 2 * 22 = 414 + 80 + 44 = 538px
        targetPanelWidth = 9 * innerButtonSize + 8 * itemSpacing + 2 * panelPadding;
        toolbarPanelRect.sizeDelta = new Vector2(targetPanelWidth, toolbarHeight);
    }

    private void BuildNineButtons()
=======
        const int buttonCount = 7;
        targetPanelWidth = buttonCount * innerButtonSize + (buttonCount - 1) * itemSpacing + 2 * panelPadding;
        toolbarPanelRect.sizeDelta = new Vector2(targetPanelWidth, toolbarHeight);
    }

    private void BuildGameplayButtons()
>>>>>>> Acs
    {
        // §5 HUD order: Inventory, Crafting, Card Shop, Orders, Market, Auto Feeding, Creation Lab
        CreateToolbarButton("Btn_Warehouse", "G-01", "Inventory", 0, () =>
        {
            var ctrl = FindAnyObjectByType<ResourceInventoryUIController>();
            if (ctrl != null) ctrl.OpenWarehouse();
        });

        CreateToolbarButton("Btn_Crafting", "G-02", "Crafting", 1, () =>
        {
            FindAnyObjectByType<LumiWorld.Economy.Crafting.CraftingUI>()?.Toggle();
        });

        CreateToolbarButton("Btn_Shop", "G-03", "Card Shop", 2, () =>
        {
            FindAnyObjectByType<CardShopUI>()?.Toggle();
        });

        CreateToolbarButton("Btn_Orders", "G-04", "Orders", 3, () =>
        {
            FindAnyObjectByType<OrderBoardUI>()?.Toggle();
        });

        CreateToolbarButton("Btn_Market", "G-05", "Market", 4, () =>
        {
            FindAnyObjectByType<MarketUI>()?.Toggle();
        });

        CreateToolbarButton("Btn_AutoFeed", "G-06", "Auto Feeding", 5, () =>
        {
            AutoFeedUI.Instance?.Toggle();
        });

        CreateToolbarButton("Btn_CreationLab", "G-07", "Creation Lab", 6, () =>
        {
            GameDialogUI.ShowNotice("Creation Lab chambers are being prepared for this island.");
        });

        // 9. Lai tạo thú (Creation Lab / Breeding)
        CreateToolbarButton("Btn_Breeding", "icon_breeding", 8, () =>
        {
            var lab = FindAnyObjectByType<CreationLabUI>(FindObjectsInactive.Include);
            if (lab != null)
            {
                lab.Toggle();
            }
            else
            {
                CreationLabUI.Instance?.Toggle();
            }
        });
    }

    private static Sprite LoadSpriteSafe(string path)
    {
        Sprite s = Resources.Load<Sprite>(path);
        if (s != null) return s;
        Texture2D t = Resources.Load<Texture2D>(path);
        if (t != null)
        {
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
        }
        return null;
    }

    private void CreateToolbarButton(string objName, string glyphId, string tooltipLabel, int index, Action onClick)
    {
        GameObject btnObj = new GameObject(objName, typeof(RectTransform), typeof(Image), typeof(Button));
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
        btnImg.sprite = LumiWorldUITheme.SurfaceSprite ?? EconomyHUD.RoundedSprite;
        btnImg.type = Image.Type.Sliced;
        btnImg.color = ButtonNormalColor;
        btnImg.raycastTarget = true;
        LumiWorldUITheme.AddSoftElevation(btnObj, 2.5f, 0.13f);

        GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(btnObj.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(innerIconSize, innerIconSize);

        Image iconImg = iconObj.GetComponent<Image>();
<<<<<<< HEAD
        Sprite iconSprite = LoadSpriteSafe("UI_Buttons/" + iconResource)
                         ?? LoadSpriteSafe(iconResource);
        iconImg.sprite = iconSprite;
=======
        iconImg.sprite = LumiWorldUITheme.Glyph(glyphId);
        iconImg.color = LumiWorldUITheme.Text;
>>>>>>> Acs
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        // If sprite not found, keep color transparent so button bg still shows
        if (iconSprite == null)
        {
            iconImg.color = Color.clear;
            // Add a fallback text label so the button is not blank
            GameObject lblGo = new GameObject("FallbackLabel", typeof(RectTransform));
            lblGo.transform.SetParent(btnObj.transform, false);
            var lblRect = lblGo.GetComponent<RectTransform>();
            lblRect.anchorMin = Vector2.zero;
            lblRect.anchorMax = Vector2.one;
            lblRect.offsetMin = lblRect.offsetMax = Vector2.zero;
            var lbl = lblGo.AddComponent<TMPro.TextMeshProUGUI>();
            lbl.text = iconResource.Replace("icon_", "").Substring(0, 1).ToUpper(); // first letter
            lbl.fontSize = 18;
            lbl.fontStyle = TMPro.FontStyles.Bold;
            lbl.color = new Color(0.08f, 0.45f, 0.80f);
            lbl.alignment = TMPro.TextAlignmentOptions.Center;
            lbl.raycastTarget = false;
        }

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

        AttachTooltipTrigger(btnRect, tooltipLabel);

        if (objName == "Btn_Crafting")
        {
            RectTransform badgeRect = EconomyHUD.CreateRect("Badge", btnRect);
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(-3f, -3f);
            badgeRect.sizeDelta = new Vector2(20f, 20f);
            Image badgeImg = badgeRect.gameObject.AddComponent<Image>();
            badgeImg.sprite = EconomyHUD.CircleSprite;
            badgeImg.color = LumiWorldUITheme.Success;
            badgeImg.raycastTarget = false;
            craftingBadgeObj = badgeRect.gameObject;
            craftingBadgeText = EconomyHUD.CreateText("Count", badgeRect, 12f, Color.white, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(craftingBadgeText.rectTransform);
            SetCraftingReadyCount(pendingCraftingReadyCount);
        }
    }

    private void BuildTopRightBar()
    {
        // §5 HUD: Top-right row = World Map (globe G-10) + CoinHUD pill + Settings (gear G-09)
        // CoinHUD is anchored at (-20, -14) with width 134, or we shift CoinHUD between WorldMap and Settings.
        GameObject trObj = new GameObject("TopRightUtilityBar", typeof(RectTransform));
        trObj.transform.SetParent(transform, false);
        topRightContainer = trObj.GetComponent<RectTransform>();
        topRightContainer.anchorMin = new Vector2(1f, 1f);
        topRightContainer.anchorMax = new Vector2(1f, 1f);
        topRightContainer.pivot = new Vector2(1f, 1f);
        topRightContainer.anchoredPosition = new Vector2(-20f, -14f);
        topRightContainer.sizeDelta = new Vector2(320f, 50f);

        // 1. Settings Gear (rightmost, x = 0 from right edge)
        BuildTopRightIconButton("Btn_Settings", "G-09", "Settings", new Vector2(0f, 0f), () =>
        {
            FindAnyObjectByType<GameSettingsUI>()?.ToggleWindow();
        });

        // 2. World Map Globe (left of CoinHUD, x = -202f from right edge)
        BuildTopRightIconButton("Btn_WorldMap", "G-10", "World Map", new Vector2(-202f, 0f), () =>
        {
            if (Application.CanStreamedLevelBeLoaded(WorldMapController.SceneName))
                WorldMapController.Open();
            else
                GameDialogUI.ShowNotice("World Map scene is not in Build Settings.");
        });
    }

    private void BuildTopRightIconButton(string objName, string glyphId, string tooltipLabel, Vector2 topRightOffset, Action onClick)
    {
        GameObject btnObj = new GameObject(objName, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(topRightContainer, false);
        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = btnRect.anchorMax = btnRect.pivot = new Vector2(1f, 1f);
        btnRect.anchoredPosition = topRightOffset;
        btnRect.sizeDelta = new Vector2(48f, 48f);

        Image btnImg = btnObj.GetComponent<Image>();
        btnImg.sprite = LumiWorldUITheme.SurfaceSprite ?? EconomyHUD.RoundedSprite;
        btnImg.type = Image.Type.Sliced;
        btnImg.color = LumiWorldUITheme.Surface;
        btnImg.raycastTarget = true;
        LumiWorldUITheme.AddSoftElevation(btnObj, 3f, 0.16f);

        GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(btnObj.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(26f, 26f);

        Image iconImg = iconObj.GetComponent<Image>();
        iconImg.sprite = LumiWorldUITheme.Glyph(glyphId);
        iconImg.color = LumiWorldUITheme.Text;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = btnImg;
        var colors = btn.colors;
        colors.normalColor = LumiWorldUITheme.Surface;
        colors.highlightedColor = ButtonHoverColor;
        colors.pressedColor = ButtonPressedColor;
        btn.colors = colors;
        btn.onClick.AddListener(() =>
        {
            GameAudioManager.Instance?.PlayClick();
            onClick?.Invoke();
        });

        AttachTooltipTrigger(btnRect, tooltipLabel);
    }

    private void BuildHoverTooltip()
    {
        GameObject tipObj = new GameObject("HoverTooltip", typeof(RectTransform), typeof(Image));
        tipObj.transform.SetParent(transform, false);
        hoverTooltipRect = tipObj.GetComponent<RectTransform>();
        hoverTooltipRect.pivot = new Vector2(0.5f, 1f);
        hoverTooltipRect.sizeDelta = new Vector2(110f, 32f);

        Image bg = tipObj.GetComponent<Image>();
        bg.sprite = LumiWorldUITheme.SurfaceSprite ?? EconomyHUD.RoundedSprite;
        bg.type = Image.Type.Sliced;
        bg.color = LumiWorldUITheme.Slot;
        bg.raycastTarget = false;
        LumiWorldUITheme.AddSoftElevation(tipObj, 2.5f, 0.16f);

        hoverTooltipText = EconomyHUD.CreateText("Label", hoverTooltipRect, 13.5f, LumiWorldUITheme.Text, TextAlignmentOptions.Center);
        if (LumiWorldUITheme.Heading != null) hoverTooltipText.font = LumiWorldUITheme.Heading;
        EconomyHUD.Stretch(hoverTooltipText.rectTransform);
        hoverTooltipText.raycastTarget = false;

        tipObj.SetActive(false);
    }

    private void AttachTooltipTrigger(RectTransform target, string label)
    {
        var trigger = target.gameObject.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => ShowTooltip(target, label));
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => HideTooltip());
        trigger.triggers.Add(exit);
    }

    private void ShowTooltip(RectTransform target, string label)
    {
        if (hoverTooltipRect == null || hoverTooltipText == null || target == null) return;
        hoverTooltipText.text = label;
        float w = Mathf.Max(84f, label.Length * 8.5f + 26f);
        hoverTooltipRect.sizeDelta = new Vector2(w, 32f);
        hoverTooltipRect.position = target.position;
        hoverTooltipRect.anchoredPosition += new Vector2(0f, -34f);
        hoverTooltipRect.SetAsLastSibling();
        hoverTooltipRect.gameObject.SetActive(true);
    }

    private void HideTooltip()
    {
        if (hoverTooltipRect != null)
            hoverTooltipRect.gameObject.SetActive(false);
    }

    public void SetCraftingReadyCount(int count)
    {
        pendingCraftingReadyCount = count;
        if (craftingBadgeObj != null)
            craftingBadgeObj.SetActive(count > 0);
        if (craftingBadgeText != null)
            craftingBadgeText.text = count > 9 ? "9+" : count.ToString();
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
