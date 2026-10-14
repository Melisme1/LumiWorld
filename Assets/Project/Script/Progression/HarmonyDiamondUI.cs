using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Giao diện Tiến trình Cột mốc Harmony hình thoi ở góc trái bên dưới màn hình (Preserve Style Diamond Progress UI - như Ảnh 1).
/// Hiển thị trực quan:
/// - Khung hình thoi viền sáng, góc bo nhẹ phong cách Preserve.
/// - Điểm Harmony tích lũy vĩnh viễn (Milestone Score e.g. 29/50).
/// - Thanh tiến trình hình thoi (Diamond fill gauge).
/// - Biểu tượng mầm lá sinh thái ở đỉnh.
/// - Chú thích điều kiện mở đảo mới (1 trong 3 điều kiện bắt buộc).
/// - Bấm vào sẽ mở Bảng Tiến Trình & Thành Tựu (Progression & Achievement Panel)!
/// </summary>
public class HarmonyDiamondUI : MonoBehaviour
{
    private static HarmonyDiamondUI instance;
    public static HarmonyDiamondUI Instance => instance;

    [Header("Settings")]
    [SerializeField] private Vector2 screenPosition = new Vector2(95f, 95f);
    [SerializeField] private Vector2 diamondSize = new Vector2(110f, 110f);

    private RectTransform rootRect;
    private Image diamondBgImage;
    private Image diamondProgressFillImage;
    private Image diamondBorderImage;
    private Image leafIconImage;
    private TextMeshProUGUI scoreText;
    private TextMeshProUGUI subtitleText;
    private Button openAchievementButton;

    private int currentHarmony = 29;
    private int targetHarmony = 50;

    private void Awake()
    {
        // Người chơi yêu cầu xóa Harmony UI góc trái
        Destroy(gameObject);
    }

    private void Start()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnScoreChanged += HandleScoreChanged;
            currentHarmony = ScoreManager.Instance.CurrentScore;
        }

        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnHarmonyChanged += HandleHarmonyChanged;
            currentHarmony = Mathf.Max(currentHarmony, AchievementManager.Instance.TotalHarmony);
            targetHarmony = AchievementManager.Instance.NextIslandThreshold;
        }

        RefreshUI();
    }

    private void OnDestroy()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnScoreChanged -= HandleScoreChanged;
        }
        if (AchievementManager.HasInstance)
        {
            AchievementManager.Instance.OnHarmonyChanged -= HandleHarmonyChanged;
        }
    }

    private void HandleScoreChanged(int newScore)
    {
        currentHarmony = newScore;
        RefreshUI();
    }

    private void HandleHarmonyChanged(int newHarmony, int nextThreshold)
    {
        currentHarmony = newHarmony;
        targetHarmony = nextThreshold;
        RefreshUI();
    }

    private void BuildDiamondHUD()
    {
        rootRect = GetComponent<RectTransform>();
        if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();

        // Đặt ở góc dưới bên trái màn hình
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.zero;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = screenPosition;
        rootRect.sizeDelta = diamondSize;

        // 1. Nền hình thoi tối
        GameObject bgObj = new GameObject("DiamondBG", typeof(RectTransform), typeof(Image));
        bgObj.transform.SetParent(transform, false);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.sizeDelta = diamondSize;
        bgRect.localRotation = Quaternion.Euler(0, 0, 45f);

        diamondBgImage = bgObj.GetComponent<Image>();
        diamondBgImage.sprite = PreserveDiamondStyle.DiamondBase;
        diamondBgImage.color = new Color(0.06f, 0.09f, 0.12f, 0.94f);

        // 2. Viền hình thoi sáng
        GameObject borderObj = new GameObject("DiamondBorder", typeof(RectTransform), typeof(Image));
        borderObj.transform.SetParent(bgObj.transform, false);
        RectTransform borderRect = borderObj.GetComponent<RectTransform>();
        borderRect.sizeDelta = diamondSize;

        diamondBorderImage = borderObj.GetComponent<Image>();
        diamondBorderImage.sprite = PreserveDiamondStyle.DiamondOutline;
        diamondBorderImage.color = new Color(0.40f, 0.85f, 0.58f, 1f);

        // Nút bấm mở bảng thành tựu
        openAchievementButton = bgObj.AddComponent<Button>();
        openAchievementButton.targetGraphic = diamondBgImage;
        openAchievementButton.onClick.AddListener(OnDiamondClicked);

        // 3. Nội dung hiển thị thẳng đứng
        GameObject contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(transform, false);
        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.sizeDelta = diamondSize;

        // Biểu tượng mầm lá sinh thái ở đỉnh
        GameObject leafObj = new GameObject("LeafIcon", typeof(RectTransform), typeof(Image));
        leafObj.transform.SetParent(contentRect, false);
        RectTransform leafRect = leafObj.GetComponent<RectTransform>();
        leafRect.anchoredPosition = new Vector2(0f, 26f);
        leafRect.sizeDelta = new Vector2(24f, 24f);

        leafIconImage = leafObj.GetComponent<Image>();
        leafIconImage.sprite = PreserveDiamondStyle.LeafIcon;
        leafIconImage.raycastTarget = false;

        // Con số điểm to rõ (e.g. 29 / 50)
        GameObject scoreObj = new GameObject("ScoreText", typeof(RectTransform), typeof(TextMeshProUGUI));
        scoreObj.transform.SetParent(contentRect, false);
        RectTransform scoreRect = scoreObj.GetComponent<RectTransform>();
        scoreRect.anchoredPosition = new Vector2(0f, -4f);
        scoreRect.sizeDelta = new Vector2(90f, 32f);

        scoreText = scoreObj.GetComponent<TextMeshProUGUI>();
        scoreText.alignment = TextAlignmentOptions.Center;
        scoreText.fontSize = 20f;
        scoreText.fontStyle = FontStyles.Bold;
        scoreText.color = Color.white;
        scoreText.raycastTarget = false;

        // Chú thích phụ (Harmony)
        GameObject subObj = new GameObject("SubtitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        subObj.transform.SetParent(contentRect, false);
        RectTransform subRect = subObj.GetComponent<RectTransform>();
        subRect.anchoredPosition = new Vector2(0f, -26f);
        subRect.sizeDelta = new Vector2(90f, 18f);

        subtitleText = subObj.GetComponent<TextMeshProUGUI>();
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.fontSize = 10f;
        subtitleText.fontStyle = FontStyles.Bold;
        subtitleText.color = new Color(0.45f, 0.95f, 0.65f, 0.95f);
        subtitleText.text = "HARMONY";
        subtitleText.raycastTarget = false;
    }

    public void RefreshUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $"{currentHarmony}\n<size=12><color=#A7F3D0>/ {targetHarmony}</color></size>";
        }

        float ratio = Mathf.Clamp01((float)currentHarmony / Mathf.Max(1, targetHarmony));
        if (diamondBorderImage != null)
        {
            // Gradient đổi màu viền khi sắp đạt mốc
            diamondBorderImage.color = Color.Lerp(new Color(0.35f, 0.75f, 0.50f, 1f), new Color(0.55f, 1f, 0.70f, 1f), ratio);
        }
    }

    private void OnDiamondClicked()
    {
        // Mở Bảng Tiến Trình & Thành Tựu
        if (AchievementUI.Instance != null)
        {
            AchievementUI.Instance.ToggleWindow();
        }
    }
}
