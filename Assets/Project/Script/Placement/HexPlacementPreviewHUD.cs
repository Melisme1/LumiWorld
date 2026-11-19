using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LumiWorld.Acs;

/// <summary>
/// P-04 Placement Tooltip & Feedback HUD:
/// - Displays a clean white floating pill (LumiWorldUITheme.Slot #FCFBF8) above the hovered hex tile
///   showing ONLY Card Name + Coin Cost (no Rain icon, no multi-line dark tech box).
/// - Nature/Rain highlights a single hex (via HexGhostVisual); Creature highlights the valid biome cluster (via HexCreatureTileGuide).
/// </summary>
public class HexPlacementPreviewHUD : MonoBehaviour
{
    private static HexPlacementPreviewHUD instance;
    public static HexPlacementPreviewHUD Instance => instance;

    private Canvas hudCanvas;
    private RectTransform tooltipPill;
    private TextMeshProUGUI cardNameText;
    private RectTransform costGroup;
    private Image coinIconImg;
    private TextMeshProUGUI costValueText;

    private RectTransform toastPanel;
    private TextMeshProUGUI toastText;
    private Coroutine toastCoroutine;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        BuildHUD();
    }

    private void BuildHUD()
    {
        hudCanvas = gameObject.GetComponent<Canvas>();
        if (hudCanvas == null)
        {
            hudCanvas = gameObject.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.sortingOrder = 999;
        }

        var scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        // =====================================================================
        // 1. P-04 FLOATING WHITE TOOLTIP PILL (Card Name + Coin Cost Only)
        // =====================================================================
        GameObject pillObj = new GameObject("P04_PlacementTooltipPill", typeof(RectTransform), typeof(Image));
        pillObj.transform.SetParent(transform, false);
        tooltipPill = pillObj.GetComponent<RectTransform>();
        tooltipPill.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipPill.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipPill.pivot = new Vector2(0.5f, 0f);
        tooltipPill.sizeDelta = new Vector2(176f, 42f);

        Image pillBg = pillObj.GetComponent<Image>();
        pillBg.sprite = LumiWorldUITheme.PillSprite;
        pillBg.type = Image.Type.Sliced;
        pillBg.color = LumiWorldUITheme.Slot; // Clean warm white #FCFBF8
        pillBg.raycastTarget = false;
        LumiWorldUITheme.AddSoftElevation(pillObj, 4f, 0.22f);

        // Card Name (left)
        GameObject nameObj = new GameObject("CardName", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameObj.transform.SetParent(tooltipPill, false);
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.offsetMin = new Vector2(18f, 0f);
        nameRect.offsetMax = new Vector2(-68f, 0f);

        cardNameText = nameObj.GetComponent<TextMeshProUGUI>();
        cardNameText.font = LumiWorldUITheme.Body;
        cardNameText.alignment = TextAlignmentOptions.MidlineLeft;
        cardNameText.fontSize = 16.5f;
        cardNameText.fontStyle = FontStyles.Bold;
        cardNameText.color = LumiWorldUITheme.Text;
        cardNameText.textWrappingMode = TextWrappingModes.NoWrap;
        cardNameText.raycastTarget = false;

        // Cost Group (right: Coin Icon + Cost Number)
        GameObject costObj = new GameObject("CostGroup", typeof(RectTransform));
        costObj.transform.SetParent(tooltipPill, false);
        costGroup = costObj.GetComponent<RectTransform>();
        costGroup.anchorMin = new Vector2(1f, 0.5f);
        costGroup.anchorMax = new Vector2(1f, 0.5f);
        costGroup.pivot = new Vector2(1f, 0.5f);
        costGroup.anchoredPosition = new Vector2(-16f, 0f);
        costGroup.sizeDelta = new Vector2(54f, 28f);

        GameObject coinObj = new GameObject("CoinIcon", typeof(RectTransform), typeof(Image));
        coinObj.transform.SetParent(costGroup, false);
        RectTransform coinRect = coinObj.GetComponent<RectTransform>();
        coinRect.anchorMin = new Vector2(0f, 0.5f);
        coinRect.anchorMax = new Vector2(0f, 0.5f);
        coinRect.pivot = new Vector2(0f, 0.5f);
        coinRect.anchoredPosition = new Vector2(0f, 0f);
        coinRect.sizeDelta = new Vector2(20f, 20f);

        coinIconImg = coinObj.GetComponent<Image>();
        coinIconImg.sprite = EconomyHUD.CoinSprite;
        coinIconImg.preserveAspect = true;
        coinIconImg.raycastTarget = false;

        GameObject valObj = new GameObject("CostValue", typeof(RectTransform), typeof(TextMeshProUGUI));
        valObj.transform.SetParent(costGroup, false);
        RectTransform valRect = valObj.GetComponent<RectTransform>();
        valRect.anchorMin = new Vector2(0f, 0f);
        valRect.anchorMax = new Vector2(1f, 1f);
        valRect.offsetMin = new Vector2(24f, 0f);
        valRect.offsetMax = Vector2.zero;

        costValueText = valObj.GetComponent<TextMeshProUGUI>();
        costValueText.font = LumiWorldUITheme.Body;
        costValueText.alignment = TextAlignmentOptions.MidlineLeft;
        costValueText.fontSize = 16.5f;
        costValueText.fontStyle = FontStyles.Bold;
        costValueText.color = LumiWorldUITheme.Text;
        costValueText.raycastTarget = false;

        tooltipPill.gameObject.SetActive(false);

        // =====================================================================
        // 2. FEEDBACK TOAST
        // =====================================================================
        GameObject toastObj = new GameObject("FeedbackToast", typeof(RectTransform), typeof(Image));
        toastObj.transform.SetParent(transform, false);
        toastPanel = toastObj.GetComponent<RectTransform>();
        toastPanel.anchorMin = new Vector2(0.5f, 0.5f);
        toastPanel.anchorMax = new Vector2(0.5f, 0.5f);
        toastPanel.pivot = new Vector2(0.5f, 0.5f);
        toastPanel.anchoredPosition = new Vector2(0f, 160f);
        toastPanel.sizeDelta = new Vector2(520f, 48f);

        Image toastBg = toastObj.GetComponent<Image>();
        toastBg.sprite = LumiWorldUITheme.PillSprite;
        toastBg.type = Image.Type.Sliced;
        toastBg.color = LumiWorldUITheme.Surface;
        toastBg.raycastTarget = false;
        LumiWorldUITheme.AddSoftElevation(toastObj, 4f, 0.22f);

        GameObject toastLabelObj = new GameObject("ToastLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        toastLabelObj.transform.SetParent(toastPanel, false);
        RectTransform toastLabelRect = toastLabelObj.GetComponent<RectTransform>();
        EconomyHUD.Stretch(toastLabelRect);

        toastText = toastLabelObj.GetComponent<TextMeshProUGUI>();
        toastText.font = LumiWorldUITheme.Body;
        toastText.alignment = TextAlignmentOptions.Center;
        toastText.fontSize = 15f;
        toastText.fontStyle = FontStyles.Bold;
        toastText.color = LumiWorldUITheme.Text;
        toastText.raycastTarget = false;

        toastPanel.gameObject.SetActive(false);
    }

    public void UpdatePlacementPreview(
        CardData cardData,
        HexCoordinates targetHex,
        bool isValid,
        string validationError,
        int prospectiveClusterSize,
        int placementFee,
        bool canAffordFee)
    {
        if (cardData == null || tooltipPill == null)
        {
            HidePreview();
            return;
        }

        string rawName = !string.IsNullOrEmpty(cardData.cardName) ? cardData.cardName : cardData.cardID;
        string displayName = CardShop.TranslateToEnglish(rawName, cardData.name);

        cardNameText.text = displayName;
        float textW = Mathf.Clamp(cardNameText.preferredWidth, 48f, 220f);

        bool hasFee = placementFee > 0;
        if (costGroup != null) costGroup.gameObject.SetActive(hasFee);

        float pillW;
        if (hasFee)
        {
            if (costValueText != null)
            {
                costValueText.text = placementFee.ToString();
                costValueText.color = canAffordFee ? LumiWorldUITheme.Text : LumiWorldUITheme.Error;
            }
            float costW = placementFee >= 10 ? 54f : 44f;
            costGroup.sizeDelta = new Vector2(costW, 28f);
            cardNameText.rectTransform.offsetMax = new Vector2(-(costW + 24f), 0f);
            pillW = textW + costW + 46f;
        }
        else
        {
            cardNameText.rectTransform.offsetMax = new Vector2(-18f, 0f);
            pillW = textW + 36f;
        }

        tooltipPill.sizeDelta = new Vector2(pillW, 42f);

        // Position tooltip above the hovered hex in screen space
        Camera cam = Camera.main;
        if (cam != null && hudCanvas != null)
        {
            Vector3 worldPos = HexMetrics.HexToWorldPosition(targetHex, HexMetrics.TileHeight + 0.65f);
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            if (screenPos.z > 0f)
            {
                RectTransform canvasRect = (RectTransform)hudCanvas.transform;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRect,
                        new Vector2(screenPos.x, screenPos.y),
                        hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : hudCanvas.worldCamera,
                        out Vector2 localPt))
                {
                    tooltipPill.anchoredPosition = localPt + new Vector2(0f, 24f);
                }
            }
        }

        if (!tooltipPill.gameObject.activeSelf)
        {
            tooltipPill.gameObject.SetActive(true);
        }

        if (toastPanel != null && toastText != null)
        {
            if (isValid && canAffordFee)
            {
                if (toastPanel.gameObject.activeSelf) toastPanel.gameObject.SetActive(false);
            }
            else
            {
                string reason = !canAffordFee
                    ? $"Not enough Coins ({placementFee} required)."
                    : (!string.IsNullOrEmpty(validationError) ? validationError : "Cannot place here.");
                toastText.text = reason;
                toastText.color = LumiWorldUITheme.Error;
                if (!toastPanel.gameObject.activeSelf) toastPanel.gameObject.SetActive(true);
            }
        }
    }

    public void HidePreview()
    {
        if (tooltipPill != null && tooltipPill.gameObject.activeSelf)
        {
            tooltipPill.gameObject.SetActive(false);
        }
        if (toastPanel != null && toastPanel.gameObject.activeSelf)
        {
            toastPanel.gameObject.SetActive(false);
        }
    }

    public static void ShowFeedbackToast(string message, float duration = 2.8f)
    {
        GameDialogUI.ShowNotice(message);
    }
}
