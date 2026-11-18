using System;
using LumiWorld.Acs;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shared modal confirmation and notification dialogs styled with LumiWorldUITheme.
/// </summary>
[DisallowMultipleComponent]
public class GameDialogUI : MonoBehaviour
{
    private static GameDialogUI _instance;

    private Canvas _canvas;
    private GameObject _backdropGo;
    private RectTransform _dialogWindowRect;

    public static bool IsAnyOpen => _instance != null && _instance._backdropGo != null && _instance._backdropGo.activeSelf;

    public static void ShowConfirm(
        string message,
        Action onConfirm,
        Action onCancel = null,
        string title = "Confirmation",
        string confirmText = "Confirm",
        string cancelText = "Cancel")
    {
        GameDialogUI host = EnsureInstance();
        host.OpenConfirmInternal(
            string.IsNullOrEmpty(title) ? "Confirmation" : title,
            message ?? string.Empty,
            onConfirm,
            onCancel,
            string.IsNullOrEmpty(confirmText) ? "Confirm" : confirmText,
            string.IsNullOrEmpty(cancelText) ? "Cancel" : cancelText);
    }

    public static void ShowNotice(
        string message,
        Action onOk = null,
        string title = "Notice",
        string okText = "OK")
    {
        GameDialogUI host = EnsureInstance();
        host.OpenNoticeInternal(
            string.IsNullOrEmpty(title) ? "Notice" : title,
            message ?? string.Empty,
            onOk,
            string.IsNullOrEmpty(okText) ? "OK" : okText);
    }

    private static GameDialogUI EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        _instance = FindAnyObjectByType<GameDialogUI>(FindObjectsInactive.Include);
        if (_instance == null)
        {
            GameObject go = new GameObject("GameDialogUI");
            _instance = go.AddComponent<GameDialogUI>();
            DontDestroyOnLoad(go);
        }
        return _instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureCanvasAndBackdrop();
        if (_backdropGo != null)
            _backdropGo.SetActive(false);
    }

    private void Update()
    {
        if (!IsAnyOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            CloseCurrent();
    }

    private void EnsureCanvasAndBackdrop()
    {
        if (_canvas == null)
        {
            GameObject cGo = new GameObject("GameDialogCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cGo.transform.SetParent(transform, false);
            _canvas = cGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500;
            CanvasScaler scaler = cGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        }

        if (_backdropGo == null)
        {
            _backdropGo = new GameObject("DialogBackdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            _backdropGo.transform.SetParent(_canvas.transform, false);
            LumiWorldUITheme.Stretch(_backdropGo.GetComponent<RectTransform>());
            _backdropGo.GetComponent<Image>().color = LumiWorldUITheme.Backdrop;
            Button bgBtn = _backdropGo.GetComponent<Button>();
            bgBtn.transition = Selectable.Transition.None;
            bgBtn.onClick.AddListener(() => { });
        }
    }

    private void ClearDialogBody()
    {
        EnsureCanvasAndBackdrop();
        for (int i = _backdropGo.transform.childCount - 1; i >= 0; i--)
            Destroy(_backdropGo.transform.GetChild(i).gameObject);
        _dialogWindowRect = null;
    }

    private void OpenConfirmInternal(
        string title, string message, Action onConfirm, Action onCancel, string confirmText, string cancelText)
    {
        ClearDialogBody();
        _backdropGo.SetActive(true);
        _backdropGo.transform.SetAsLastSibling();

        float winW = 476f;
        float msgH = Mathf.Clamp(EstimateWrappedHeight(message, 404f, 14.5f), 58f, 210f);
        float winH = 148f + msgH;

        Image winImg = LumiWorldUITheme.CreateRoundedSurface(
            _backdropGo.transform, "ConfirmWindow", LumiWorldUITheme.Window, LumiWorldUITheme.Edge,
            radius: 22, borderThickness: 1.5f, shadowDepth: 6f, shadowAlpha: 0.25f, raycastTarget: true);
        _dialogWindowRect = winImg.rectTransform;
        _dialogWindowRect.anchorMin = new Vector2(0.5f, 0.5f);
        _dialogWindowRect.anchorMax = new Vector2(0.5f, 0.5f);
        _dialogWindowRect.pivot = new Vector2(0.5f, 0.5f);
        _dialogWindowRect.anchoredPosition = Vector2.zero;
        _dialogWindowRect.sizeDelta = new Vector2(winW, winH);
        ApplyFitScale();

        // Title
        TextMeshProUGUI titleTxt = LumiWorldUITheme.CreateLabel(
            _dialogWindowRect, "Title", title, 19f, LumiWorldUITheme.Text,
            headingFont: true, align: TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(titleTxt.rectTransform, 24f, 18f, winW - 84f, 34f);

        LumiWorldUITheme.CreateCloseButton(_dialogWindowRect, 20f, 19f, () =>
        {
            CloseCurrent();
            onCancel?.Invoke();
        }, 32f);

        // Inner Surface Message Card
        Image msgCard = LumiWorldUITheme.CreateRoundedSurface(
            _dialogWindowRect, "MessageSurface", LumiWorldUITheme.Surface, LumiWorldUITheme.Edge,
            radius: 16, borderThickness: 1.2f, shadowDepth: 1.5f, shadowAlpha: 0.06f, raycastTarget: false);
        RectTransform msgCardRt = msgCard.rectTransform;
        LumiWorldUITheme.PlaceTopLeft(msgCardRt, 22f, 64f, winW - 44f, msgH + 14f);

        TextMeshProUGUI msgTxt = LumiWorldUITheme.CreateLabel(
            msgCardRt, "Message", message, 14.5f, LumiWorldUITheme.Text,
            headingFont: false, align: TextAlignmentOptions.Center, wrap: true);
        LumiWorldUITheme.Stretch(msgTxt.rectTransform, 16f);

        // Bottom Buttons
        float btnY = 64f + msgH + 28f;
        float halfW = (winW - 56f) * 0.5f;

        Button cancelBtn = LumiWorldUITheme.CreatePillButton(
            _dialogWindowRect, "CancelBtn", cancelText, null, false, 15f,
            () =>
            {
                CloseCurrent();
                onCancel?.Invoke();
            },
            out _, out _, out _, radius: 14);
        LumiWorldUITheme.PlaceTopLeft(cancelBtn.GetComponent<RectTransform>(), 22f, btnY, halfW, 44f);
        cancelBtn.gameObject.AddComponent<DialogButtonJuice>();

        Button confirmBtn = LumiWorldUITheme.CreatePillButton(
            _dialogWindowRect, "ConfirmBtn", confirmText, null, true, 15f,
            () =>
            {
                CloseCurrent();
                onConfirm?.Invoke();
            },
            out _, out _, out _, radius: 14);
        LumiWorldUITheme.PlaceTopRight(confirmBtn.GetComponent<RectTransform>(), 22f, btnY, halfW, 44f);
        confirmBtn.gameObject.AddComponent<DialogButtonJuice>();
    }

    private void OpenNoticeInternal(string title, string message, Action onOk, string okText)
    {
        ClearDialogBody();
        _backdropGo.SetActive(true);
        _backdropGo.transform.SetAsLastSibling();

        float winW = 456f;
        float msgH = Mathf.Clamp(EstimateWrappedHeight(message, 384f, 14.5f), 58f, 220f);
        float winH = 148f + msgH;

        Image winImg = LumiWorldUITheme.CreateRoundedSurface(
            _backdropGo.transform, "NoticeWindow", LumiWorldUITheme.Window, LumiWorldUITheme.Edge,
            radius: 22, borderThickness: 1.5f, shadowDepth: 6f, shadowAlpha: 0.25f, raycastTarget: true);
        _dialogWindowRect = winImg.rectTransform;
        _dialogWindowRect.anchorMin = new Vector2(0.5f, 0.5f);
        _dialogWindowRect.anchorMax = new Vector2(0.5f, 0.5f);
        _dialogWindowRect.pivot = new Vector2(0.5f, 0.5f);
        _dialogWindowRect.anchoredPosition = Vector2.zero;
        _dialogWindowRect.sizeDelta = new Vector2(winW, winH);
        ApplyFitScale();

        TextMeshProUGUI titleTxt = LumiWorldUITheme.CreateLabel(
            _dialogWindowRect, "Title", title, 19f, LumiWorldUITheme.Text,
            headingFont: true, align: TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(titleTxt.rectTransform, 24f, 18f, winW - 84f, 34f);

        LumiWorldUITheme.CreateCloseButton(_dialogWindowRect, 20f, 19f, () =>
        {
            CloseCurrent();
            onOk?.Invoke();
        }, 32f);

        Image msgCard = LumiWorldUITheme.CreateRoundedSurface(
            _dialogWindowRect, "MessageSurface", LumiWorldUITheme.Surface, LumiWorldUITheme.Edge,
            radius: 16, borderThickness: 1.2f, shadowDepth: 1.5f, shadowAlpha: 0.06f, raycastTarget: false);
        RectTransform msgCardRt = msgCard.rectTransform;
        LumiWorldUITheme.PlaceTopLeft(msgCardRt, 22f, 64f, winW - 44f, msgH + 14f);

        TextMeshProUGUI msgTxt = LumiWorldUITheme.CreateLabel(
            msgCardRt, "Message", message, 14.5f, LumiWorldUITheme.Text,
            headingFont: false, align: TextAlignmentOptions.Center, wrap: true);
        LumiWorldUITheme.Stretch(msgTxt.rectTransform, 16f);

        float btnY = 64f + msgH + 28f;
        Button okBtn = LumiWorldUITheme.CreatePillButton(
            _dialogWindowRect, "OkBtn", okText, null, true, 15f,
            () =>
            {
                CloseCurrent();
                onOk?.Invoke();
            },
            out _, out _, out _, radius: 14);
        LumiWorldUITheme.PlaceTopLeft(okBtn.GetComponent<RectTransform>(), (winW - 180f) * 0.5f, btnY, 180f, 44f);
        okBtn.gameObject.AddComponent<DialogButtonJuice>();
    }

    private void CloseCurrent()
    {
        if (_backdropGo != null)
            _backdropGo.SetActive(false);
    }

    private void ApplyFitScale()
    {
        if (_canvas == null || _dialogWindowRect == null)
            return;
        RectTransform canvasRt = _canvas.GetComponent<RectTransform>();
        float fit = LumiWorldUITheme.ComputeFitScale(canvasRt, out _);
        _dialogWindowRect.localScale = new Vector3(fit, fit, 1f);
    }

    private static float EstimateWrappedHeight(string msg, float width, float fontSize)
    {
        if (string.IsNullOrEmpty(msg))
            return 58f;
        float charsPerLine = Mathf.Max(18f, width / (fontSize * 0.52f));
        string[] rawLines = msg.Split('\n');
        int lines = 0;
        for (int i = 0; i < rawLines.Length; i++)
            lines += Mathf.Max(1, Mathf.CeilToInt(rawLines[i].Length / charsPerLine));
        return lines * (fontSize * 1.45f) + 22f;
    }
}

/// <summary>
/// Preserved lightweight pointer hover/press scale animation for dialog action buttons.
/// </summary>
public class DialogButtonJuice : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private Vector3 _baseScale = Vector3.one;
    private float _targetScale = 1f;

    private void Awake()
    {
        _baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        _targetScale = 1f;
        transform.localScale = _baseScale;
    }

    private void Update()
    {
        Vector3 desired = _baseScale * _targetScale;
        if ((transform.localScale - desired).sqrMagnitude > 0.00001f)
            transform.localScale = Vector3.Lerp(transform.localScale, desired, Time.unscaledDeltaTime * 18f);
    }

    public void OnPointerEnter(PointerEventData eventData) => _targetScale = 1.03f;
    public void OnPointerExit(PointerEventData eventData) => _targetScale = 1f;
    public void OnPointerDown(PointerEventData eventData) => _targetScale = 0.96f;
    public void OnPointerUp(PointerEventData eventData) => _targetScale = 1.03f;
}
