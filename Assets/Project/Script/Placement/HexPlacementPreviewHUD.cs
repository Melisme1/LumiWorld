using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
        hudCanvas = gameObject.GetComponentInParent<Canvas>();
        if (hudCanvas == null)
        {
            hudCanvas = gameObject.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.sortingOrder = 999;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();
        }
        else
        {
            hudCanvas = hudCanvas.rootCanvas;
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
        // 2. FEEDBACK TOAST (non-blocking top banner, never overlaps top HUD or freezes gameplay)
        // =====================================================================
        GameObject toastObj = new GameObject("FeedbackToast", typeof(RectTransform), typeof(Image));
        toastObj.transform.SetParent(transform, false);
        toastPanel = toastObj.GetComponent<RectTransform>();
        toastPanel.anchorMin = new Vector2(0.5f, 1f);
        toastPanel.anchorMax = new Vector2(0.5f, 1f);
        toastPanel.pivot = new Vector2(0.5f, 1f);
        toastPanel.anchoredPosition = new Vector2(0f, -86f);
        toastPanel.sizeDelta = new Vector2(360f, 40f);

        Image toastBg = toastObj.GetComponent<Image>();
        toastBg.sprite = LumiWorldUITheme.PillSprite;
        toastBg.type = Image.Type.Sliced;
        toastBg.color = LumiWorldUITheme.Slot;
        toastBg.raycastTarget = false;
        LumiWorldUITheme.AddSoftElevation(toastObj, 4f, 0.22f);

        GameObject toastLabelObj = new GameObject("ToastLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        toastLabelObj.transform.SetParent(toastPanel, false);
        RectTransform toastLabelRect = toastLabelObj.GetComponent<RectTransform>();
        EconomyHUD.Stretch(toastLabelRect);
        toastLabelRect.offsetMin = new Vector2(18f, 0f);
        toastLabelRect.offsetMax = new Vector2(-18f, 0f);

        toastText = toastLabelObj.GetComponent<TextMeshProUGUI>();
        toastText.font = LumiWorldUITheme.Body;
        toastText.alignment = TextAlignmentOptions.Center;
        toastText.fontSize = 14.5f;
        toastText.fontStyle = FontStyles.Bold;
        toastText.color = LumiWorldUITheme.Text;
        toastText.textWrappingMode = TextWrappingModes.NoWrap;
        toastText.overflowMode = TextOverflowModes.Ellipsis;
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

        // Position tooltip right above the dragged card / pointer in screen space
        RectTransform parentRect = tooltipPill.parent as RectTransform;
        if (hudCanvas != null && parentRect != null)
        {
            Vector2 screenPos = Vector2.zero;
            bool hasScreenPos = false;

            if (Pointer.current != null)
            {
                screenPos = Pointer.current.position.ReadValue();
                hasScreenPos = true;
            }
            else if (Camera.main != null)
            {
                Vector3 worldPos = HexMetrics.HexToWorldPosition(targetHex, HexMetrics.TileHeight + 0.65f);
                Vector3 sp = Camera.main.WorldToScreenPoint(worldPos);
                if (sp.z > 0f)
                {
                    screenPos = new Vector2(sp.x, sp.y);
                    hasScreenPos = true;
                }
            }

            if (hasScreenPos)
            {
                float scale = hudCanvas.scaleFactor > 0f ? hudCanvas.scaleFactor : 1f;
                float halfPillPx = (pillW * 0.5f + 12f) * scale;
                screenPos.x = Mathf.Clamp(screenPos.x, halfPillPx, Mathf.Max(halfPillPx, Screen.width - halfPillPx));
                screenPos.y = Mathf.Clamp(screenPos.y + 64f * scale, 80f * scale, Mathf.Max(80f * scale, Screen.height - 90f * scale));

                Camera uiCam = hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : hudCanvas.worldCamera;
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parentRect, screenPos, uiCam, out Vector3 worldPt))
                {
                    tooltipPill.position = worldPt;
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
                if (toastCoroutine != null)
                {
                    StopCoroutine(toastCoroutine);
                    toastCoroutine = null;
                }
                string reason = !canAffordFee
                    ? $"Not enough Coins ({placementFee} required)."
                    : (!string.IsNullOrEmpty(validationError) ? validationError : "Cannot place here.");
                toastText.text = reason;
                toastText.color = LumiWorldUITheme.Error;
                ResizeToastToFit(reason);
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
        if (toastCoroutine == null && toastPanel != null && toastPanel.gameObject.activeSelf)
        {
            toastPanel.gameObject.SetActive(false);
        }
    }

    public static void ShowFeedbackToast(string message, float duration = 2.8f)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (instance == null)
        {
            instance = FindAnyObjectByType<HexPlacementPreviewHUD>();
        }
        if (instance == null) return;
        instance.ShowToastInternal(message, duration);
    }

    private void ShowToastInternal(string message, float duration)
    {
        if (toastPanel == null || toastText == null) return;

        if (toastCoroutine != null)
        {
            StopCoroutine(toastCoroutine);
            toastCoroutine = null;
        }

        toastText.text = message;
        toastText.color = LumiWorldUITheme.Text;
        ResizeToastToFit(message);
        toastPanel.gameObject.SetActive(true);
        toastPanel.SetAsLastSibling();
        toastCoroutine = StartCoroutine(HideToastAfterDelay(Mathf.Max(0.8f, duration)));
    }

    private void ResizeToastToFit(string message)
    {
        if (toastPanel == null || toastText == null) return;
        float preferred = toastText.GetPreferredValues(message).x;
        float width = Mathf.Clamp(preferred + 40f, 220f, 620f);
        toastPanel.sizeDelta = new Vector2(width, 40f);
    }

    private IEnumerator HideToastAfterDelay(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        toastCoroutine = null;
        if (toastPanel != null)
        {
            toastPanel.gameObject.SetActive(false);
        }
    }
}
