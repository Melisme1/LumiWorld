using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hệ thống Modal Dialog & Notification tập trung chuẩn mỹ thuật LumiWorld.
/// Quản lý 2 loại giao diện:
/// 1. Panel Xác Nhận (Confirmation Panel):
///    - Dùng cho các thao tác quan trọng (đắp thêm mảnh đất trống mới, mua thẻ bài, nâng rank thú...).
///    - 2 nút bấm lục giác phía dưới: "HỦY BỎ" (trái) và "XÁC NHẬN" (phải).
/// 2. Panel Thông Báo (Notification Panel):
///    - Dùng cho mọi thông báo trong game (lỗi đặt thẻ, hoàn thành nhiệm vụ, phản hồi gameplay...).
///    - Nút "OK" ở đáy giữa để người chơi đóng thông báo.
/// </summary>
public class GameDialogUI : MonoBehaviour
{
    private static GameDialogUI instance;
    public static GameDialogUI Instance
    {
        get
        {
            if (instance == null)
            {
                EnsureInstance();
            }
            return instance;
        }
    }

    [Header("Sprite References")]
    [SerializeField] private Sprite confirmationPanelSprite;
    [SerializeField] private Sprite notificationPanelSprite;

    private Canvas dialogCanvas;
    private CanvasGroup backdropGroup;
    private GameObject backdropObj;

    // --- Modal 1: Confirmation Panel ---
    private GameObject confirmModalObj;
    private RectTransform confirmPanelRect;
    private CanvasGroup confirmCanvasGroup;
    private TextMeshProUGUI confirmTitleText;
    private TextMeshProUGUI confirmMessageText;
    private Button cancelButton;
    private Button confirmButton;
    private TextMeshProUGUI cancelButtonText;
    private TextMeshProUGUI confirmButtonText;

    private Action onConfirmCallback;
    private Action onCancelCallback;

    // --- Modal 2: Notification Panel ---
    private GameObject noticeModalObj;
    private RectTransform noticePanelRect;
    private CanvasGroup noticeCanvasGroup;
    private TextMeshProUGUI noticeTitleText;
    private TextMeshProUGUI noticeMessageText;
    private Button okButton;

    private Action onOkCallback;
    private Coroutine currentAnimRoutine;

    public static void EnsureInstance()
    {
        if (instance != null) return;

        instance = FindAnyObjectByType<GameDialogUI>();
        if (instance != null) return;

        GameObject go = new GameObject("GameDialogUI");
        instance = go.AddComponent<GameDialogUI>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        LoadResources();
        BuildUI();
    }

    private TMP_FontAsset vietnameseFont;

    private void LoadResources()
    {
        if (confirmationPanelSprite == null)
        {
            confirmationPanelSprite = Resources.Load<Sprite>("UI_Panels/ConfirmationPanel");
        }
        if (notificationPanelSprite == null)
        {
            notificationPanelSprite = Resources.Load<Sprite>("UI_Panels/NotificationPanel");
        }
        if (vietnameseFont == null)
        {
            vietnameseFont = Resources.Load<TMP_FontAsset>("Fonts/LumiWorldVietnameseSDF");
        }
    }

    private void BuildUI()
    {
        dialogCanvas = gameObject.GetComponent<Canvas>();
        if (dialogCanvas == null)
        {
            dialogCanvas = gameObject.AddComponent<Canvas>();
            dialogCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            dialogCanvas.sortingOrder = 9999;
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

        // --- BACKDROP OVERLAY ---
        backdropObj = new GameObject("BackdropOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        backdropObj.transform.SetParent(transform, false);
        RectTransform bdRect = backdropObj.GetComponent<RectTransform>();
        bdRect.anchorMin = Vector2.zero;
        bdRect.anchorMax = Vector2.one;
        bdRect.sizeDelta = Vector2.zero;

        Image bdImg = backdropObj.GetComponent<Image>();
        bdImg.color = new Color(0f, 0f, 0f, 0.45f);

        backdropGroup = backdropObj.GetComponent<CanvasGroup>();
        backdropObj.SetActive(false);

        // --- 1. BUILD CONFIRMATION MODAL ---
        confirmModalObj = new GameObject("ConfirmationModal", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        confirmModalObj.transform.SetParent(transform, false);
        confirmPanelRect = confirmModalObj.GetComponent<RectTransform>();
        confirmPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        confirmPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        confirmPanelRect.pivot = new Vector2(0.5f, 0.5f);
        confirmPanelRect.sizeDelta = new Vector2(700f, 208f);

        Image confirmBg = confirmModalObj.GetComponent<Image>();
        confirmBg.sprite = confirmationPanelSprite;
        confirmBg.color = Color.white;
        confirmBg.preserveAspect = true;
        confirmCanvasGroup = confirmModalObj.GetComponent<CanvasGroup>();

        // Tiêu đề Confirmation
        GameObject cTitleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        cTitleObj.transform.SetParent(confirmPanelRect, false);
        RectTransform cTitleRect = cTitleObj.GetComponent<RectTransform>();
        cTitleRect.anchoredPosition = new Vector2(0f, 54f);
        cTitleRect.sizeDelta = new Vector2(600f, 32f);

        confirmTitleText = cTitleObj.GetComponent<TextMeshProUGUI>();
        confirmTitleText.alignment = TextAlignmentOptions.Center;
        confirmTitleText.fontSize = 24f;
        confirmTitleText.fontStyle = FontStyles.Bold;
        confirmTitleText.color = new Color(0.08f, 0.22f, 0.10f, 1f); // Xanh rêu đậm sắc nét

        // Nội dung Message Confirmation
        GameObject cMsgObj = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        cMsgObj.transform.SetParent(confirmPanelRect, false);
        RectTransform cMsgRect = cMsgObj.GetComponent<RectTransform>();
        cMsgRect.anchoredPosition = new Vector2(0f, 12f);
        cMsgRect.sizeDelta = new Vector2(600f, 60f);

        confirmMessageText = cMsgObj.GetComponent<TextMeshProUGUI>();
        confirmMessageText.alignment = TextAlignmentOptions.Center;
        confirmMessageText.verticalAlignment = VerticalAlignmentOptions.Middle;
        confirmMessageText.horizontalAlignment = HorizontalAlignmentOptions.Center;
        confirmMessageText.fontSize = 19f;
        confirmMessageText.fontStyle = FontStyles.Bold;
        confirmMessageText.color = new Color(0.10f, 0.18f, 0.12f, 1f); // Đậm rõ nét, không bị mờ
        confirmMessageText.enableWordWrapping = true;

        // Nút HỦY BỎ (Bên trái) - Canh chuẩn tâm lục giác trái (-109f)
        GameObject cancelBtnObj = new GameObject("CancelButton", typeof(RectTransform), typeof(Image), typeof(Button));
        cancelBtnObj.transform.SetParent(confirmPanelRect, false);
        RectTransform cancelRect = cancelBtnObj.GetComponent<RectTransform>();
        cancelRect.anchoredPosition = new Vector2(-109f, -63f);
        cancelRect.sizeDelta = new Vector2(215f, 62f);

        Image cancelImg = cancelBtnObj.GetComponent<Image>();
        cancelImg.color = Color.clear; // Phủ lên ô lục giác có sẵn trong ảnh
        cancelButton = cancelBtnObj.GetComponent<Button>();
        cancelButton.targetGraphic = cancelImg;
        cancelButton.onClick.AddListener(OnCancelClicked);

        // Hiệu ứng nháy đậm khi ấn (Flash Overlay)
        GameObject cancelFlashObj = new GameObject("FlashOverlay", typeof(RectTransform), typeof(Image));
        cancelFlashObj.transform.SetParent(cancelBtnObj.transform, false);
        RectTransform cancelFlashRect = cancelFlashObj.GetComponent<RectTransform>();
        cancelFlashRect.anchorMin = Vector2.zero;
        cancelFlashRect.anchorMax = Vector2.one;
        cancelFlashRect.sizeDelta = new Vector2(-16f, -10f); // Khớp gọn bên trong viền lục giác
        Image cancelFlashImg = cancelFlashObj.GetComponent<Image>();
        cancelFlashImg.sprite = EconomyHUD.RoundedSprite;
        cancelFlashImg.type = Image.Type.Sliced;
        cancelFlashImg.color = Color.clear;
        cancelFlashImg.raycastTarget = false;

        GameObject cancelTxtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        cancelTxtObj.transform.SetParent(cancelBtnObj.transform, false);
        RectTransform cancelTxtRect = cancelTxtObj.GetComponent<RectTransform>();
        cancelTxtRect.anchoredPosition = Vector2.zero;
        cancelTxtRect.sizeDelta = new Vector2(190f, 40f);

        cancelButtonText = cancelTxtObj.GetComponent<TextMeshProUGUI>();
        cancelButtonText.text = "HỦY BỎ";
        cancelButtonText.alignment = TextAlignmentOptions.Center;
        cancelButtonText.verticalAlignment = VerticalAlignmentOptions.Middle;
        cancelButtonText.horizontalAlignment = HorizontalAlignmentOptions.Center;
        cancelButtonText.fontSize = 20f;
        cancelButtonText.fontStyle = FontStyles.Bold;
        cancelButtonText.color = new Color(0.06f, 0.18f, 0.05f, 1f); // Xanh rêu đậm nét, rất đậm và rõ
        cancelButtonText.raycastTarget = false;

        DialogButtonJuice cancelJuice = cancelBtnObj.AddComponent<DialogButtonJuice>();
        cancelJuice.Initialize(cancelFlashImg, new Color(0.04f, 0.08f, 0.04f, 0.32f));

        // Nút XÁC NHẬN (Bên phải) - Canh chuẩn tâm lục giác phải (+152f)
        GameObject confirmBtnObj = new GameObject("ConfirmButton", typeof(RectTransform), typeof(Image), typeof(Button));
        confirmBtnObj.transform.SetParent(confirmPanelRect, false);
        RectTransform confirmRect = confirmBtnObj.GetComponent<RectTransform>();
        confirmRect.anchoredPosition = new Vector2(152f, -63f);
        confirmRect.sizeDelta = new Vector2(215f, 62f);

        Image confirmBtnImg = confirmBtnObj.GetComponent<Image>();
        confirmBtnImg.color = Color.clear;
        confirmButton = confirmBtnObj.GetComponent<Button>();
        confirmButton.targetGraphic = confirmBtnImg;
        confirmButton.onClick.AddListener(OnConfirmClicked);

        // Hiệu ứng nháy đậm khi ấn (Flash Overlay)
        GameObject confirmFlashObj = new GameObject("FlashOverlay", typeof(RectTransform), typeof(Image));
        confirmFlashObj.transform.SetParent(confirmBtnObj.transform, false);
        RectTransform confirmFlashRect = confirmFlashObj.GetComponent<RectTransform>();
        confirmFlashRect.anchorMin = Vector2.zero;
        confirmFlashRect.anchorMax = Vector2.one;
        confirmFlashRect.sizeDelta = new Vector2(-16f, -10f);
        Image confirmFlashImg = confirmFlashObj.GetComponent<Image>();
        confirmFlashImg.sprite = EconomyHUD.RoundedSprite;
        confirmFlashImg.type = Image.Type.Sliced;
        confirmFlashImg.color = Color.clear;
        confirmFlashImg.raycastTarget = false;

        GameObject confirmTxtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        confirmTxtObj.transform.SetParent(confirmBtnObj.transform, false);
        RectTransform confirmTxtRect = confirmTxtObj.GetComponent<RectTransform>();
        confirmTxtRect.anchoredPosition = Vector2.zero;
        confirmTxtRect.sizeDelta = new Vector2(190f, 40f);

        confirmButtonText = confirmTxtObj.GetComponent<TextMeshProUGUI>();
        confirmButtonText.text = "XÁC NHẬN";
        confirmButtonText.alignment = TextAlignmentOptions.Center;
        confirmButtonText.verticalAlignment = VerticalAlignmentOptions.Middle;
        confirmButtonText.horizontalAlignment = HorizontalAlignmentOptions.Center;
        confirmButtonText.fontSize = 20f;
        confirmButtonText.fontStyle = FontStyles.Bold;
        confirmButtonText.color = new Color(0.06f, 0.18f, 0.05f, 1f); // Xanh rêu đậm nét, rất đậm và rõ
        confirmButtonText.raycastTarget = false;

        DialogButtonJuice confirmJuice = confirmBtnObj.AddComponent<DialogButtonJuice>();
        confirmJuice.Initialize(confirmFlashImg, new Color(0.04f, 0.08f, 0.04f, 0.32f));

        confirmModalObj.SetActive(false);

        // --- 2. BUILD NOTIFICATION MODAL ---
        noticeModalObj = new GameObject("NotificationModal", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        noticeModalObj.transform.SetParent(transform, false);
        noticePanelRect = noticeModalObj.GetComponent<RectTransform>();
        noticePanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        noticePanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        noticePanelRect.pivot = new Vector2(0.5f, 0.5f);
        noticePanelRect.sizeDelta = new Vector2(860f, 211f);

        Image noticeBg = noticeModalObj.GetComponent<Image>();
        noticeBg.sprite = notificationPanelSprite;
        noticeBg.color = Color.white;
        noticeBg.preserveAspect = true;
        noticeCanvasGroup = noticeModalObj.GetComponent<CanvasGroup>();

        // Tiêu đề Notification (Sát trên đầu mép trên panel)
        GameObject nTitleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        nTitleObj.transform.SetParent(noticePanelRect, false);
        RectTransform nTitleRect = nTitleObj.GetComponent<RectTransform>();
        nTitleRect.anchoredPosition = new Vector2(0f, 72f);
        nTitleRect.sizeDelta = new Vector2(760f, 34f);

        noticeTitleText = nTitleObj.GetComponent<TextMeshProUGUI>();
        noticeTitleText.alignment = TextAlignmentOptions.Center;
        noticeTitleText.fontSize = 21f;
        noticeTitleText.fontStyle = FontStyles.Bold;
        noticeTitleText.characterSpacing = 1.2f;
        noticeTitleText.color = new Color(0.06f, 0.12f, 0.22f, 1f); // Deep Slate Navy sắc nét

        // Nội dung Message Notification (Nằm bên dưới tiêu đề, khoảng cách hàng thoáng đãng, in đậm toàn bộ)
        GameObject nMsgObj = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        nMsgObj.transform.SetParent(noticePanelRect, false);
        RectTransform nMsgRect = nMsgObj.GetComponent<RectTransform>();
        nMsgRect.anchoredPosition = new Vector2(0f, 6f);
        nMsgRect.sizeDelta = new Vector2(740f, 96f);

        noticeMessageText = nMsgObj.GetComponent<TextMeshProUGUI>();
        noticeMessageText.alignment = TextAlignmentOptions.Center;
        noticeMessageText.verticalAlignment = VerticalAlignmentOptions.Middle;
        noticeMessageText.horizontalAlignment = HorizontalAlignmentOptions.Center;
        noticeMessageText.lineSpacing = 12f; // Giãn dòng cân đối, rõ ràng
        noticeMessageText.enableAutoSizing = true;
        noticeMessageText.fontSizeMin = 11.5f;
        noticeMessageText.fontSizeMax = 15f;
        noticeMessageText.fontSize = 14f;
        noticeMessageText.fontStyle = FontStyles.Bold; // In đậm toàn bộ
        noticeMessageText.color = new Color(0.06f, 0.10f, 0.18f, 1f); // Đậm và rõ nét
        noticeMessageText.enableWordWrapping = true;

        // Nút OK ở đáy giữa
        GameObject okBtnObj = new GameObject("OkButton", typeof(RectTransform), typeof(Image), typeof(Button));
        okBtnObj.transform.SetParent(noticePanelRect, false);
        RectTransform okRect = okBtnObj.GetComponent<RectTransform>();
        okRect.anchoredPosition = new Vector2(0f, -77f);
        okRect.sizeDelta = new Vector2(195f, 56f);

        Image okImg = okBtnObj.GetComponent<Image>();
        okImg.color = Color.clear; // Đè trực tiếp lên nút OK có sẵn trên ảnh
        okButton = okBtnObj.GetComponent<Button>();
        okButton.targetGraphic = okImg;
        okButton.onClick.AddListener(OnOkClicked);

        // Hiệu ứng nháy đậm khi ấn nút OK (Flash Overlay)
        GameObject okFlashObj = new GameObject("FlashOverlay", typeof(RectTransform), typeof(Image));
        okFlashObj.transform.SetParent(okBtnObj.transform, false);
        RectTransform okFlashRect = okFlashObj.GetComponent<RectTransform>();
        okFlashRect.anchorMin = Vector2.zero;
        okFlashRect.anchorMax = Vector2.one;
        okFlashRect.sizeDelta = new Vector2(-12f, -8f);
        Image okFlashImg = okFlashObj.GetComponent<Image>();
        okFlashImg.sprite = EconomyHUD.RoundedSprite;
        okFlashImg.type = Image.Type.Sliced;
        okFlashImg.color = Color.clear;
        okFlashImg.raycastTarget = false;

        DialogButtonJuice okJuice = okBtnObj.AddComponent<DialogButtonJuice>();
        okJuice.Initialize(okFlashImg, new Color(0.02f, 0.05f, 0.10f, 0.35f));

        noticeModalObj.SetActive(false);

        if (vietnameseFont != null)
        {
            confirmTitleText.font = vietnameseFont;
            confirmMessageText.font = vietnameseFont;
            cancelButtonText.font = vietnameseFont;
            confirmButtonText.font = vietnameseFont;
            noticeTitleText.font = vietnameseFont;
            noticeMessageText.font = vietnameseFont;
        }
    }

    private static string SanitizeText(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        return raw.Replace("★", "").Replace("✖", "").Replace("✔", "").Replace("[!]", "").Trim();
    }

    // =========================================================
    // STATIC PUBLIC API
    // =========================================================

    /// <summary>
    /// Hiển thị Panel Thông Báo (Notification Panel) kèm nút OK để tắt
    /// </summary>
    public static void ShowNotice(string message, Action onOk = null, string title = "THÔNG BÁO")
    {
        EnsureInstance();
        instance.InternalShowNotice(message, onOk, title);
    }

    /// <summary>
    /// Hiển thị Panel Xác Nhận (Confirmation Panel) với 2 nút HỦY BỎ và XÁC NHẬN
    /// </summary>
    public static void ShowConfirm(
        string message,
        Action onConfirm,
        Action onCancel = null,
        string confirmText = "XÁC NHẬN",
        string cancelText = "HỦY BỎ",
        string title = "XÁC NHẬN")
    {
        EnsureInstance();
        instance.InternalShowConfirm(message, onConfirm, onCancel, confirmText, cancelText, title);
    }

    // =========================================================
    // INTERNAL IMPLEMENTATION
    // =========================================================

    private void InternalShowNotice(string message, Action onOk, string title)
    {
        CloseAllImmediate();

        this.onOkCallback = onOk;

        noticeTitleText.text = SanitizeText(!string.IsNullOrEmpty(title) ? title : "THÔNG BÁO");
        noticeMessageText.text = SanitizeText(message);

        backdropObj.SetActive(true);
        noticeModalObj.SetActive(true);

        if (currentAnimRoutine != null) StopCoroutine(currentAnimRoutine);
        currentAnimRoutine = StartCoroutine(AnimateModalIn(noticePanelRect, noticeCanvasGroup));
    }

    private void InternalShowConfirm(
        string message,
        Action onConfirm,
        Action onCancel,
        string confirmText,
        string cancelText,
        string title)
    {
        CloseAllImmediate();

        this.onConfirmCallback = onConfirm;
        this.onCancelCallback = onCancel;

        confirmTitleText.text = SanitizeText(!string.IsNullOrEmpty(title) ? title : "XÁC NHẬN");
        confirmMessageText.text = SanitizeText(message);

        confirmButtonText.text = SanitizeText(!string.IsNullOrEmpty(confirmText) ? confirmText : "XÁC NHẬN");
        cancelButtonText.text = SanitizeText(!string.IsNullOrEmpty(cancelText) ? cancelText : "HỦY BỎ");

        backdropObj.SetActive(true);
        confirmModalObj.SetActive(true);

        if (currentAnimRoutine != null) StopCoroutine(currentAnimRoutine);
        currentAnimRoutine = StartCoroutine(AnimateModalIn(confirmPanelRect, confirmCanvasGroup));
    }

    private void OnOkClicked()
    {
        var cb = onOkCallback;
        onOkCallback = null;

        StartCoroutine(AnimateModalOut(noticePanelRect, noticeCanvasGroup, () =>
        {
            noticeModalObj.SetActive(false);
            backdropObj.SetActive(false);
            cb?.Invoke();
        }));
    }

    private void OnConfirmClicked()
    {
        var cb = onConfirmCallback;
        onConfirmCallback = null;
        onCancelCallback = null;

        StartCoroutine(AnimateModalOut(confirmPanelRect, confirmCanvasGroup, () =>
        {
            confirmModalObj.SetActive(false);
            backdropObj.SetActive(false);
            cb?.Invoke();
        }));
    }

    private void OnCancelClicked()
    {
        var cb = onCancelCallback;
        onConfirmCallback = null;
        onCancelCallback = null;

        StartCoroutine(AnimateModalOut(confirmPanelRect, confirmCanvasGroup, () =>
        {
            confirmModalObj.SetActive(false);
            backdropObj.SetActive(false);
            cb?.Invoke();
        }));
    }

    private void CloseAllImmediate()
    {
        if (currentAnimRoutine != null)
        {
            StopCoroutine(currentAnimRoutine);
            currentAnimRoutine = null;
        }

        if (confirmModalObj != null) confirmModalObj.SetActive(false);
        if (noticeModalObj != null) noticeModalObj.SetActive(false);
        if (backdropObj != null) backdropObj.SetActive(false);
    }

    private IEnumerator AnimateModalIn(RectTransform panel, CanvasGroup group)
    {
        panel.localScale = Vector3.one * 0.85f;
        group.alpha = 0f;
        backdropGroup.alpha = 0f;

        float elapsed = 0f;
        const float duration = 0.22f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Pop-in bounce
            float scale = Mathf.Lerp(0.85f, 1.05f, Mathf.SmoothStep(0f, 1f, t));
            panel.localScale = Vector3.one * scale;

            group.alpha = t;
            backdropGroup.alpha = t;

            yield return null;
        }

        // Settle về 1.0f
        elapsed = 0f;
        const float settleDuration = 0.08f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / settleDuration);
            panel.localScale = Vector3.one * Mathf.Lerp(1.05f, 1.0f, t);
            yield return null;
        }

        panel.localScale = Vector3.one;
        group.alpha = 1f;
        backdropGroup.alpha = 1f;
        currentAnimRoutine = null;
    }

    private IEnumerator AnimateModalOut(RectTransform panel, CanvasGroup group, Action onComplete)
    {
        float elapsed = 0f;
        const float duration = 0.14f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            panel.localScale = Vector3.one * Mathf.Lerp(1.0f, 0.90f, t);
            group.alpha = 1f - t;
            backdropGroup.alpha = 1f - t;

            yield return null;
        }

        group.alpha = 0f;
        backdropGroup.alpha = 0f;
        currentAnimRoutine = null;
        onComplete?.Invoke();
    }
}

/// <summary>
/// Hiệu ứng xúc giác chuyên nghiệp cho các nút bấm trong dialog:
/// - Khi nhấn xuống: nháy đậm lên (flash dark tint) và co nhẹ scale (0.94f) mang lại cảm giác phản hồi vật lý.
/// - Khi thả / click: nhả về kèm độ nảy micro-bounce và fade out màu nháy mượt mà.
/// - Khi rê chuột qua: sáng/đậm nhẹ (hover feedback).
/// </summary>
public class DialogButtonJuice : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    private RectTransform rectTransform;
    private Image flashOverlay;
    private Color flashColor = new Color(0.04f, 0.08f, 0.04f, 0.32f);
    private Vector3 baseScale = Vector3.one;
    private Coroutine currentRoutine;

    public void Initialize(Image overlay = null, Color? targetFlashColor = null)
    {
        rectTransform = GetComponent<RectTransform>();
        flashOverlay = overlay;
        flashColor = targetFlashColor ?? new Color(0.04f, 0.08f, 0.04f, 0.32f);
        baseScale = Vector3.one;
        if (flashOverlay != null)
        {
            flashOverlay.color = Color.clear;
            flashOverlay.raycastTarget = false;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(AnimateDown());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(AnimateUp());
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(AnimateHover(true));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(AnimateHover(false));
    }

    private IEnumerator AnimateDown()
    {
        float elapsed = 0f;
        const float duration = 0.05f;
        Vector3 startScale = rectTransform.localScale;
        float startAlpha = flashOverlay != null ? flashOverlay.color.a : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rectTransform.localScale = Vector3.Lerp(startScale, baseScale * 0.94f, t);
            if (flashOverlay != null)
            {
                flashOverlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, Mathf.Lerp(startAlpha, flashColor.a, t));
            }
            yield return null;
        }

        rectTransform.localScale = baseScale * 0.94f;
        if (flashOverlay != null) flashOverlay.color = flashColor;
    }

    private IEnumerator AnimateUp()
    {
        float elapsed = 0f;
        const float duration = 0.16f;
        Vector3 startScale = rectTransform.localScale;
        float startAlpha = flashOverlay != null ? flashOverlay.color.a : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float bounce = Mathf.Sin(t * Mathf.PI) * 0.04f;
            rectTransform.localScale = Vector3.Lerp(startScale, baseScale, t) + Vector3.one * bounce;
            if (flashOverlay != null)
            {
                flashOverlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, Mathf.Lerp(startAlpha, 0f, t));
            }
            yield return null;
        }

        rectTransform.localScale = baseScale;
        if (flashOverlay != null) flashOverlay.color = Color.clear;
        currentRoutine = null;
    }

    private IEnumerator AnimateHover(bool isHover)
    {
        float elapsed = 0f;
        const float duration = 0.1f;
        Vector3 startScale = rectTransform.localScale;
        Vector3 targetScale = isHover ? baseScale * 1.025f : baseScale;
        float startAlpha = flashOverlay != null ? flashOverlay.color.a : 0f;
        float targetAlpha = isHover ? flashColor.a * 0.35f : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rectTransform.localScale = Vector3.Lerp(startScale, targetScale, t);
            if (flashOverlay != null)
            {
                flashOverlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, Mathf.Lerp(startAlpha, targetAlpha, t));
            }
            yield return null;
        }

        rectTransform.localScale = targetScale;
        if (flashOverlay != null) flashOverlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, targetAlpha);
        currentRoutine = null;
    }
}
