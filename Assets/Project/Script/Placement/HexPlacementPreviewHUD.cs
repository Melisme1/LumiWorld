using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LumiWorld.Acs;

/// <summary>
/// Giao diện xem trước kết quả và phản hồi khi kéo thả đặt khối lục giác (Preserve Style Hex Placement Preview & Feedback HUD).
/// Hiển thị trực quan:
/// 1. Vùng ảnh hưởng & Tính hợp lệ (Xanh lá / Đỏ).
/// 2. Kết quả dự kiến (Kích thước nhóm habitat mới, Sức chứa cư dân mới, Slot tài nguyên mới).
/// 3. Chi phí hiển thị trước commit (Coins / Placement Fee).
/// 4. Phản hồi UI (Feedback Banner): Giải thích chi tiết lý do placement không hợp lệ.
/// 5. Toast thông báo hủy đặt an toàn: Thẻ quay lại Hand/Inventory và không trừ Coins.
/// </summary>
public class HexPlacementPreviewHUD : MonoBehaviour
{
    private static HexPlacementPreviewHUD instance;
    public static HexPlacementPreviewHUD Instance => instance;

    [Header("UI References")]
    private Canvas hudCanvas;
    private RectTransform previewPanel;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI clusterSizeText;
    private TextMeshProUGUI capacityAndSlotText;
    private TextMeshProUGUI feeText;
    private TextMeshProUGUI statusText;
    private Image statusBgImage;

    // Toast thông báo phản hồi (Feedback Toast)
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
        // 1. PREVIEW PANEL (HỘP XEM TRƯỚC KẾT QUẢ DỰ KIẾN KHI KÉO THẺ)
        // =====================================================================
        GameObject panelObj = new GameObject("PlacementPreviewPanel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(transform, false);
        previewPanel = panelObj.GetComponent<RectTransform>();
        previewPanel.anchorMin = new Vector2(0.5f, 1f);
        previewPanel.anchorMax = new Vector2(0.5f, 1f);
        previewPanel.pivot = new Vector2(0.5f, 1f);
        previewPanel.anchoredPosition = new Vector2(0f, -25f);
        previewPanel.sizeDelta = new Vector2(460f, 115f);

        Image panelImg = panelObj.GetComponent<Image>();
        panelImg.sprite = PreserveDiamondStyle.DiamondBase;
        panelImg.color = new Color(0.08f, 0.12f, 0.16f, 0.94f);

        // Header: Tên thẻ & Biome
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(previewPanel, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(0f, -14f);
        titleRect.sizeDelta = new Vector2(420f, 24f);

        titleText = titleObj.GetComponent<TextMeshProUGUI>();
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 17f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.95f, 0.95f, 0.95f, 1f);

        // Hàng 1: Kích thước nhóm mới
        GameObject clusterObj = new GameObject("ClusterText", typeof(RectTransform), typeof(TextMeshProUGUI));
        clusterObj.transform.SetParent(previewPanel, false);
        RectTransform clusterRect = clusterObj.GetComponent<RectTransform>();
        clusterRect.anchoredPosition = new Vector2(-105f, -40f);
        clusterRect.sizeDelta = new Vector2(210f, 22f);

        clusterSizeText = clusterObj.GetComponent<TextMeshProUGUI>();
        clusterSizeText.alignment = TextAlignmentOptions.Left;
        clusterSizeText.fontSize = 13f;
        clusterSizeText.color = new Color(0.45f, 0.95f, 0.65f, 1f);

        // Hàng 1 bên phải: Chi phí đặt (Placement Fee)
        GameObject feeObj = new GameObject("FeeText", typeof(RectTransform), typeof(TextMeshProUGUI));
        feeObj.transform.SetParent(previewPanel, false);
        RectTransform feeRect = feeObj.GetComponent<RectTransform>();
        feeRect.anchoredPosition = new Vector2(105f, -40f);
        feeRect.sizeDelta = new Vector2(210f, 22f);

        feeText = feeObj.GetComponent<TextMeshProUGUI>();
        feeText.alignment = TextAlignmentOptions.Right;
        feeText.fontSize = 13f;
        feeText.fontStyle = FontStyles.Bold;
        feeText.color = new Color(1f, 0.85f, 0.35f, 1f);

        // Hàng 2: Sức chứa & Slot tài nguyên mới
        GameObject capObj = new GameObject("CapText", typeof(RectTransform), typeof(TextMeshProUGUI));
        capObj.transform.SetParent(previewPanel, false);
        RectTransform capRect = capObj.GetComponent<RectTransform>();
        capRect.anchoredPosition = new Vector2(0f, -63f);
        capRect.sizeDelta = new Vector2(420f, 20f);

        capacityAndSlotText = capObj.GetComponent<TextMeshProUGUI>();
        capacityAndSlotText.alignment = TextAlignmentOptions.Center;
        capacityAndSlotText.fontSize = 13f;
        capacityAndSlotText.color = new Color(0.85f, 0.90f, 0.95f, 0.95f);

        // Hàng 3: Trạng thái hợp lệ / Lý do từ chối
        GameObject statusObj = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusObj.transform.SetParent(previewPanel, false);
        RectTransform statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchoredPosition = new Vector2(0f, -88f);
        statusRect.sizeDelta = new Vector2(420f, 22f);

        statusText = statusObj.GetComponent<TextMeshProUGUI>();
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.fontSize = 13f;
        statusText.fontStyle = FontStyles.Bold;

        previewPanel.gameObject.SetActive(false);

        // =====================================================================
        // 2. FEEDBACK TOAST (BANNER PHẢN HỒI KHI HỦY HOẶC ĐẶT SAI)
        // =====================================================================
        GameObject toastObj = new GameObject("FeedbackToast", typeof(RectTransform), typeof(Image));
        toastObj.transform.SetParent(transform, false);
        toastPanel = toastObj.GetComponent<RectTransform>();
        toastPanel.anchorMin = new Vector2(0.5f, 0.5f);
        toastPanel.anchorMax = new Vector2(0.5f, 0.5f);
        toastPanel.pivot = new Vector2(0.5f, 0.5f);
        toastPanel.anchoredPosition = new Vector2(0f, 160f);
        toastPanel.sizeDelta = new Vector2(560f, 54f);

        Image toastBg = toastObj.GetComponent<Image>();
        toastBg.sprite = PreserveDiamondStyle.DiamondBase;
        toastBg.color = new Color(0.15f, 0.12f, 0.08f, 0.96f);

        GameObject toastLabelObj = new GameObject("ToastLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        toastLabelObj.transform.SetParent(toastPanel, false);
        RectTransform toastLabelRect = toastLabelObj.GetComponent<RectTransform>();
        toastLabelRect.sizeDelta = new Vector2(530f, 44f);

        toastText = toastLabelObj.GetComponent<TextMeshProUGUI>();
        toastText.alignment = TextAlignmentOptions.Center;
        toastText.fontSize = 15f;
        toastText.color = new Color(1f, 0.90f, 0.50f, 1f);

        toastPanel.gameObject.SetActive(false);
    }

    /// <summary>
    /// Hiển thị thông tin xem trước kết quả khi đang kéo thẻ lên một ô lục giác
    /// </summary>
    public void UpdatePlacementPreview(
        CardData cardData,
        HexCoordinates targetHex,
        bool isValid,
        string validationError,
        int prospectiveClusterSize,
        int placementFee,
        bool canAffordFee)
    {
        // Vô hiệu hóa Banner đen cũ để không gây lỗi font và che khuất màn hình.
        // Trạng thái trực quan 3D (Ghost Xanh/Đỏ và -X Coins) đã được HexGhostVisual hiển thị chuẩn xác tại ô đất.
        // Mọi thông báo lỗi (thiếu Coins, vị trí không hợp lệ) sẽ được chuyển về Panel Thông Báo chuẩn của game khi thả chuột.
        HidePreview();
    }

    public void HidePreview()
    {
        if (previewPanel != null)
        {
            previewPanel.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Hiển thị thông báo Toast phản hồi khi hủy đặt, đặt sai, hoặc thẻ bị khóa.
    /// Cam kết: Thẻ quay lại Hand/Inventory và không trừ Coins!
    /// </summary>
    public static void ShowFeedbackToast(string message, float duration = 2.8f)
    {
        // Hiển thị trực tiếp lên Panel Thông Báo chuẩn mới của game (kèm nút OK để đóng)
        GameDialogUI.ShowNotice(message);
    }

    private void TriggerToast(string message, float duration)
    {
        if (toastCoroutine != null)
        {
            StopCoroutine(toastCoroutine);
        }
        toastCoroutine = StartCoroutine(ToastRoutine(message, duration));
    }

    private IEnumerator ToastRoutine(string message, float duration)
    {
        toastText.text = message;
        toastPanel.gameObject.SetActive(true);
        toastPanel.localScale = Vector3.one * 0.85f;

        // Punch animation
        float elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / 0.2f;
            toastPanel.localScale = Vector3.Lerp(Vector3.one * 0.85f, Vector3.one, Mathf.SmoothStep(0, 1, t));
            yield return null;
        }

        toastPanel.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(duration);

        toastPanel.gameObject.SetActive(false);
        toastCoroutine = null;
    }
}
