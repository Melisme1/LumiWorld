using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Banner thông báo "Nhận được: TÊN CARD x1" trượt vào gần vị trí thẻ vừa được thêm.
///
/// MỤC ĐÍCH: khi người chơi được cộng card (reward theo điểm), họ cần BIẾT NGAY
/// là vừa nhận lá gì — trước đây chỉ có Debug.Log nên rất khó nhận ra.
///
/// CÁCH DÙNG: chỉ cần gọi
///     CardRewardToast.Show(cardData, handPosition);
/// Script sẽ tự sinh GameObject tại runtime (không cần setup prefab, không cần sửa Scene).
/// Font chữ lấy từ TMP Settings mặc định của project (LiberationSans SDF).
/// </summary>
public class CardRewardToast : MonoBehaviour
{
    // =========================================================
    // TUNING (có thể chỉnh trong Inspector khi object được sinh ra)
    // =========================================================

    [Header("Motion")]
    [Tooltip("Thời gian banner trượt vào + phóng to.")]
    [SerializeField] private float introDuration = 0.28f;

    [Tooltip("Thời gian banner đứng yên đọc được.")]
    [SerializeField] private float holdDuration = 1.1f;

    [Tooltip("Thời gian banner mờ dần rồi biến mất.")]
    [SerializeField] private float outroDuration = 0.32f;

    [Tooltip("Khoảng cách banner tự trôi lên trong lúc hold (pixel).")]
    [SerializeField] private float riseDistance = 26f;

    [Header("Look")]
    [SerializeField] private Vector2 toastSize = new Vector2(420f, 84f);
    [SerializeField] private Color backgroundColor = new Color(0.06f, 0.09f, 0.16f, 0.94f);
    [SerializeField] private Color accentColor = new Color(1f, 0.85f, 0.35f, 1f);
    [SerializeField] private Color titleColor = new Color(1f, 0.85f, 0.35f, 1f);
    [SerializeField] private Color nameColor = Color.white;

    [Header("Icon")]
    [Tooltip("Nếu bật, card icon nhỏ hiện bên trái banner (giúp nhận biết nhanh hơn cả tên).")]
    [SerializeField] private bool showCardIcon = true;
    [SerializeField] private Vector2 iconSize = new Vector2(46f, 60f);

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    private Vector2 targetAnchoredPosition;

    // =========================================================
    // SPAWNER
    // =========================================================

    /// <summary>
    /// Hiện banner thông báo nhận card tại vị trí UI (thường là vị trí slot card trong hand).
    /// </summary>
    public static CardRewardToast Show(CardData cardData, Vector2 anchoredPosition)
    {
        if (cardData == null) return null;

        string displayName = !string.IsNullOrEmpty(cardData.cardName)
            ? cardData.cardName
            : (!string.IsNullOrEmpty(cardData.cardID) ? cardData.cardID : "Card");

        return Show("NHẬN CARD MỚI", displayName, anchoredPosition, cardData.cardImage);
    }

    /// <summary>
    /// Hiện banner với tiêu đề + nội dung tuỳ biến (dùng cho cả tóm tắt hand ban đầu).
    /// </summary>
    public static CardRewardToast Show(
        string title,
        string body,
        Vector2 anchoredPosition,
        Sprite cardIcon = null)
    {
        GameObject toastObj = new GameObject("CardRewardToast");
        toastObj.SetActive(false);

        CardRewardToast toast = toastObj.AddComponent<CardRewardToast>();
        toast.Build(title, body, cardIcon);
        toast.SetPosition(anchoredPosition);

        toastObj.SetActive(true);
        return toast;
    }

    // =========================================================
    // BUILD UI
    // =========================================================

    private void Build(string titleText, string bodyText, Sprite cardIcon)
    {
        rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0f);
        rectTransform.anchorMax = new Vector2(0.5f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = toastSize;

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        // --- Nền banner ---
        Image background = gameObject.AddComponent<Image>();
        background.color = backgroundColor;
        background.raycastTarget = false;

        // --- Vạch accent bên trái ---
        GameObject accentObj = new GameObject("Accent", typeof(RectTransform));
        accentObj.transform.SetParent(rectTransform, false);

        RectTransform accentRect = accentObj.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.sizeDelta = new Vector2(6f, 0f);
        accentRect.anchoredPosition = new Vector2(0f, 0f);

        Image accent = accentObj.AddComponent<Image>();
        accent.color = accentColor;
        accent.raycastTarget = false;

        // --- Icon card (tuỳ chọn) ---
        float textStartX = 16f;

        if (showCardIcon && cardIcon != null)
        {
            GameObject iconObj = new GameObject("CardIcon", typeof(RectTransform));
            iconObj.transform.SetParent(rectTransform, false);

            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = iconSize;
            iconRect.anchoredPosition = new Vector2(16f, 0f);

            Image icon = iconObj.AddComponent<Image>();
            icon.sprite = cardIcon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            textStartX = 16f + iconSize.x + 12f;
        }

        // --- Dòng tiêu đề "NHẬN CARD" ---
        GameObject titleObj = new GameObject("Title", typeof(RectTransform));
        titleObj.transform.SetParent(rectTransform, false);

        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(0f, 0.5f);
        titleRect.pivot = new Vector2(0f, 0.5f);
        titleRect.sizeDelta = new Vector2(toastSize.x - textStartX - 12f, 26f);
        titleRect.anchoredPosition = new Vector2(textStartX, 18f);

        TextMeshProUGUI title = titleObj.AddComponent<TextMeshProUGUI>();
        title.text = titleText;
        title.fontSize = 20f;
        title.fontStyle = FontStyles.Bold;
        title.color = titleColor;
        title.alignment = TextAlignmentOptions.Left;
        title.raycastTarget = false;

        // --- Dòng tên card ---
        GameObject nameObj = new GameObject("CardName", typeof(RectTransform));
        nameObj.transform.SetParent(rectTransform, false);

        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0.5f);
        nameRect.anchorMax = new Vector2(0f, 0.5f);
        nameRect.pivot = new Vector2(0f, 0.5f);
        nameRect.sizeDelta = new Vector2(toastSize.x - textStartX - 12f, 32f);
        nameRect.anchoredPosition = new Vector2(textStartX, -13f);

        TextMeshProUGUI nameLabel = nameObj.AddComponent<TextMeshProUGUI>();
        nameLabel.text = bodyText;
        nameLabel.fontSize = 26f;
        nameLabel.fontStyle = FontStyles.Bold;
        nameLabel.color = nameColor;
        nameLabel.alignment = TextAlignmentOptions.Left;
        nameLabel.enableAutoSizing = true;
        nameLabel.fontSizeMin = 14f;
        nameLabel.fontSizeMax = 26f;
        nameLabel.raycastTarget = false;

        // KHÔNG set outlineWidth/outlineColor ở đây.
        // TMP chưa gán material mặc định trong cùng frame tạo component, nên
        // set outline sẽ gọi CreateMaterialInstance(null) -> ArgumentNullException,
        // làm hỏng vòng lặp thêm card trong ScoreManager (chỉ nhận được 1 card).
        // Nếu cần viền chữ, set outline sau khi TMP đã khởi tạo (ví dụ trong Start).
    }

    private void SetPosition(Vector2 anchoredPosition)
    {
        targetAnchoredPosition = anchoredPosition;
        rectTransform.anchoredPosition = anchoredPosition;

        // Banner phải nằm cùng Canvas với hand để dùng chung hệ toạ độ anchoredPosition.
        Canvas parentCanvas = FindAnyObjectByType<Canvas>();
        if (parentCanvas != null)
        {
            transform.SetParent(parentCanvas.transform, false);
        }
    }

    private void Start()
    {
        StartCoroutine(PlayRoutine());
    }

    // =========================================================
    // ANIMATION
    // =========================================================

    private IEnumerator PlayRoutine()
    {
        Vector2 basePosition = targetAnchoredPosition;
        Vector2 startPosition = basePosition + Vector2.down * 34f;

        // ---------- INTRO: trượt lên + phóng nhẹ ----------
        float time = 0f;
        while (time < introDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / introDuration));

            rectTransform.anchoredPosition = Vector2.Lerp(startPosition, basePosition, t);

            float scale = Mathf.Lerp(0.82f, 1.06f, t);
            rectTransform.localScale = Vector3.one * scale;

            canvasGroup.alpha = t;

            yield return null;
        }

        // ---------- SETTLE: thu về tỉ lệ chuẩn ----------
        float settle = 0f;
        while (settle < 0.12f)
        {
            settle += Time.deltaTime;
            float t = Mathf.Clamp01(settle / 0.12f);

            rectTransform.localScale = Vector3.one * Mathf.Lerp(1.06f, 1f, t);

            yield return null;
        }

        rectTransform.localScale = Vector3.one;

        // ---------- HOLD: trôi nhẹ lên để banner "sống" ----------
        time = 0f;
        while (time < holdDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / holdDuration);

            rectTransform.anchoredPosition = basePosition + Vector2.up * (riseDistance * t);

            yield return null;
        }

        // ---------- OUTRO: mờ dần + trôi tiếp lên ----------
        Vector2 outroStart = rectTransform.anchoredPosition;
        Vector2 outroEnd = outroStart + Vector2.up * 18f;

        time = 0f;
        while (time < outroDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / outroDuration));

            rectTransform.anchoredPosition = Vector2.Lerp(outroStart, outroEnd, t);
            canvasGroup.alpha = 1f - t;

            yield return null;
        }

        Destroy(gameObject);
    }
}