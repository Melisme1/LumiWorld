using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Giao diện Ô Điểm Số & Vòng Tròn Mầm Sống Milestone (Botanical Milestone Medallion UI).
/// Thiết kế chuẩn mỹ thuật cao theo phong cách huy hiệu cổ tích:
/// - Huy hiệu tròn chạm khắc gỗ sồi cổ kính bọc viền đá rêu (Carved Wood & Stone Medallion).
/// - Dây leo mọc uốn lượn quanh vành tròn với hoa cỏ tự nhiên:
///   + Nửa bên trái: Dây leo căng tràn sức sống với dòng nhựa Lumi xanh ngọc phát sáng.
///   + Nửa bên phải: Cành gỗ nâu tự nhiên.
/// - Đính các chiếc Lá Cột Mốc mỹ thuật cao (Milestone Leaves) theo cung tròn:
///   + Lá trước: Đã đạt (Xanh tươi 100%).
///   + Lá giữa (trên đỉnh): Đang nạp điểm (chuyển dần từ khô sang xanh). Khi đủ điểm sẽ NỞ RỘ & NHẤP NHÁY HÀO QUANG!
///   + Lá sau: Chưa tới (Lá khô nâu cằn cỗi 0%).
/// - Con số điểm to rõ, bóng bẩy ở chính giữa mặt gỗ, hoàn toàn không bị che khuất!
/// </summary>
public class ScoreUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public static ScoreUI Instance { get; private set; }

    [Header("Tham chiếu mặc định từ Scene")]
    [SerializeField] private TMP_Text scoreText;

    // Kích thước chuẩn tròn của Huy Hiệu Điểm Số
    private static readonly Vector2 MedallionSize = new Vector2(170f, 170f);

    // Thành phần UI chính
    private RectTransform rootRect;
    private Image medallionBaseImage;
    private Image vineFrameImage;

    private TextMeshProUGUI headerLabel;
    private TextMeshProUGUI targetSubLabel;

    // Bộ 3 Lá Cột Mốc đính trên vành tròn
    private struct MilestoneLeafSlot
    {
        public RectTransform root;
        public Image dryLeafImage;
        public Image livingLeafImage;
        public TextMeshProUGUI thresholdLabel;
    }

    private MilestoneLeafSlot[] leafSlots = new MilestoneLeafSlot[3];

    // Hiệu ứng Lá Nở Rộ & Nhấp Nháy (Active Blooming Leaf trên đỉnh)
    private Image activeBloomAuraImage;
    private readonly List<Image> activeSparkles = new List<Image>();
    private RectTransform chargeBadgeRoot;
    private TextMeshProUGUI chargeBadgeText;

    // Trạng thái & Dữ liệu
    private int currentScore = 0;
    private int completedThreshold = 0;
    private int activeThreshold = 10;
    private int upcomingThreshold = 30;
    private float currentFillRatio = 0f;
    private float targetFillRatio = 0f;
    private int availableCharges = 0;
    private bool isInPlacement = false;
    private bool isHovered = false;

    private float punchScaleMultiplier = 1f;
    private Coroutine punchRoutine;
    private Coroutine promptRoutine;
    private string temporaryPrompt = null;

    // Procedural Sprites (Handcrafted Vector Mathematics)
    private static Sprite medallionBaseSprite;
    private static Sprite windingVineSprite;
    private static Sprite livingLeafSprite;
    private static Sprite dryLeafSprite;
    private static Sprite glowHaloSprite;
    private static Sprite sparkleSprite;
    private static Sprite berryBadgeSprite;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        rootRect = (RectTransform)transform;

        BuildMedallionUI();
    }

    private void Start()
    {
        HideLegacyCreateButton();
        SubscribeEvents();
        RefreshMilestoneData(animatePunch: false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        UnsubscribeEvents();
    }

    private void SubscribeEvents()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnScoreChanged += HandleScoreChanged;
            ScoreManager.Instance.OnMilestoneReached += HandleMilestoneReached;
        }

        if (HexPlacementController.Instance != null)
        {
            HexPlacementController.Instance.OnExpansionChargesChanged += HandleExpansionChargesChanged;
            HexPlacementController.Instance.OnPlacementModeChanged += HandlePlacementModeChanged;
        }
    }

    private void UnsubscribeEvents()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnScoreChanged -= HandleScoreChanged;
            ScoreManager.Instance.OnMilestoneReached -= HandleMilestoneReached;
        }

        if (HexPlacementController.Instance != null)
        {
            HexPlacementController.Instance.OnExpansionChargesChanged -= HandleExpansionChargesChanged;
            HexPlacementController.Instance.OnPlacementModeChanged -= HandlePlacementModeChanged;
        }
    }

    private void HideLegacyCreateButton()
    {
        if (HexPlacementController.Instance != null && HexPlacementController.Instance.CreateLandButton != null)
        {
            HexPlacementController.Instance.CreateLandButton.gameObject.SetActive(false);
        }
        else
        {
            var oldBtn = GameObject.Find("Canvas/Button");
            if (oldBtn != null)
            {
                oldBtn.SetActive(false);
            }
        }
    }

    // =========================================================
    // API CẬP NHẬT ĐIỂM SỐ
    // =========================================================

    public void UpdateScore(int score)
    {
        currentScore = score;
        RefreshMilestoneData(animatePunch: true);
    }

    private void HandleScoreChanged(int newScore)
    {
        currentScore = newScore;
        RefreshMilestoneData(animatePunch: true);
    }

    private void HandleMilestoneReached(ScoreRewardMilestone milestone)
    {
        if (milestone != null && milestone.grantLandExpansion)
        {
            TriggerBloomFanfare();
        }
    }

    private void HandleExpansionChargesChanged(int newCharges)
    {
        int prev = availableCharges;
        availableCharges = newCharges;

        if (availableCharges > prev)
        {
            TriggerBloomFanfare();
        }

        UpdateVisuals();
    }

    private void HandlePlacementModeChanged(bool inPlacement)
    {
        isInPlacement = inPlacement;
        UpdateVisuals();
    }

    private void RefreshMilestoneData(bool animatePunch)
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.GetMilestoneLeafTrio(
                out completedThreshold,
                out activeThreshold,
                out upcomingThreshold,
                out targetFillRatio
            );
        }

        if (HexPlacementController.Instance != null)
        {
            availableCharges = HexPlacementController.Instance.AvailableExpansionCharges;
            isInPlacement = HexPlacementController.Instance.IsInPlacementMode;
        }

        if (animatePunch)
        {
            TriggerPunch(1.12f, 0.20f);
        }

        UpdateVisuals();
    }

    private void TriggerBloomFanfare()
    {
        TriggerPunch(1.24f, 0.36f);
    }

    // =========================================================
    // VÒNG LẶP UPDATE (DÂNG NHỰA SỐNG & NHẤP NHÁY HÀO QUANG)
    // =========================================================

    private void Update()
    {
        // 1. Dâng nhựa sống cho chiếc lá mốc trên đỉnh
        currentFillRatio = Mathf.MoveTowards(currentFillRatio, targetFillRatio, Time.unscaledDeltaTime * 1.5f);
        if (leafSlots[1].livingLeafImage != null)
        {
            leafSlots[1].livingLeafImage.fillAmount = currentFillRatio;
        }

        // 2. Hiệu ứng phóng to khi rê chuột
        float hoverScale = isHovered ? 1.04f : 1.0f;
        rootRect.localScale = new Vector3(punchScaleMultiplier * hoverScale, punchScaleMultiplier * hoverScale, 1f);

        // 3. Hiệu ứng Hào quang Nhấp Nháy trên chiếc lá mốc đỉnh khi đã đủ lượt (availableCharges > 0)
        bool hasCharges = (availableCharges > 0);

        if (activeBloomAuraImage != null)
        {
            activeBloomAuraImage.gameObject.SetActive(hasCharges);

            if (hasCharges)
            {
                float flashPhase = 0.5f + 0.5f * Mathf.Sin(Time.time * 4.2f);
                float auraScale = 1.06f + 0.18f * Mathf.Sin(Time.time * 4.2f);
                activeBloomAuraImage.transform.localScale = new Vector3(auraScale, auraScale, 1f);

                if (isInPlacement)
                {
                    // Đang chọn ô đặt đất -> ánh hào quang xanh ngọc biển
                    activeBloomAuraImage.color = new Color(0.35f, 0.90f, 1.00f, 0.85f);
                }
                else
                {
                    // Nhấp nháy vàng chanh - ngọc lục bảo rực rỡ
                    activeBloomAuraImage.color = Color.Lerp(
                        new Color(1.00f, 0.92f, 0.40f, 0.55f),
                        new Color(0.35f, 1.00f, 0.60f, 0.95f),
                        flashPhase
                    );
                }

                // Nhẹ nhàng đung đưa chiếc lá trên đỉnh
                if (leafSlots[1].root != null)
                {
                    float bob = Mathf.Sin(Time.time * 2.8f) * 3f;
                    float tilt = Mathf.Sin(Time.time * 2.0f) * 4f;
                    leafSlots[1].root.anchoredPosition = new Vector2(0f, 68f + bob);
                    leafSlots[1].root.localRotation = Quaternion.Euler(0f, 0f, tilt);
                }

                // Bụi sao thần tiên xoay quanh chiếc lá đỉnh
                for (int i = 0; i < activeSparkles.Count; i++)
                {
                    Image sp = activeSparkles[i];
                    if (sp == null) continue;

                    sp.gameObject.SetActive(true);
                    float angle = Time.time * 1.8f + i * (Mathf.PI * 2f / activeSparkles.Count);
                    float radius = 24f + 2f * Mathf.Sin(Time.time * 3f + i);
                    sp.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);

                    float alpha = Mathf.Clamp01(0.4f + 0.6f * Mathf.Sin(Time.time * 5f + i * 2f));
                    sp.color = new Color(1f, 0.98f, 0.75f, alpha);
                }
            }
            else
            {
                // Trạng thái bình thường: lá nằm yên trên cành
                if (leafSlots[1].root != null)
                {
                    leafSlots[1].root.anchoredPosition = new Vector2(0f, 66f);
                    leafSlots[1].root.localRotation = Quaternion.identity;
                }

                for (int i = 0; i < activeSparkles.Count; i++)
                {
                    if (activeSparkles[i] != null) activeSparkles[i].gameObject.SetActive(false);
                }
            }
        }
    }

    // =========================================================
    // CẬP NHẬT NỘI DUNG VĂN BẢN TRÊN HUY HIỆU
    // =========================================================

    private void UpdateVisuals()
    {
        // 1. Điểm số hiển thị ở chính giữa mặt gỗ
        if (scoreText != null)
        {
            scoreText.text = currentScore.ToString();
        }

        // 2. Dòng hiển thị mục tiêu ở đáy mặt gỗ
        if (temporaryPrompt != null)
        {
            if (targetSubLabel != null) targetSubLabel.text = temporaryPrompt;
        }
        else if (isInPlacement)
        {
            if (targetSubLabel != null)
            {
                targetSubLabel.text = "<size=10><color=#7DD3FC><b>ĐẶT ĐẤT</b></color></size>";
            }
        }
        else if (availableCharges > 0)
        {
            if (targetSubLabel != null)
            {
                targetSubLabel.text = "<size=10><color=#FEF08A><b>MỞ ĐẤT</b></color></size>";
            }
        }
        else
        {
            if (targetSubLabel != null)
            {
                targetSubLabel.text = $"<size=11><color=#CBD5E1><b>/ {activeThreshold}</b></color></size>";
            }
        }

        // 3. Bộ 3 Lá Cột Mốc
        // Lá 0: Mốc trước (xanh tươi 100%)
        if (leafSlots[0].livingLeafImage != null)
        {
            leafSlots[0].livingLeafImage.fillAmount = 1f;
        }
        if (leafSlots[0].thresholdLabel != null)
        {
            leafSlots[0].thresholdLabel.text = completedThreshold > 0 ? completedThreshold.ToString() : "-";
        }

        // Lá 1: Mốc hiện tại trên đỉnh
        if (leafSlots[1].thresholdLabel != null)
        {
            leafSlots[1].thresholdLabel.text = activeThreshold.ToString();
        }

        // Lá 2: Mốc tương lai (khô nâu 0%)
        if (leafSlots[2].livingLeafImage != null)
        {
            leafSlots[2].livingLeafImage.fillAmount = 0f;
        }
        if (leafSlots[2].thresholdLabel != null)
        {
            leafSlots[2].thresholdLabel.text = upcomingThreshold.ToString();
        }

        // 4. Huy hiệu đếm lượt (Badge góc trên nếu >= 2 lượt)
        if (chargeBadgeRoot != null)
        {
            chargeBadgeRoot.gameObject.SetActive(availableCharges > 1);
            if (chargeBadgeText != null)
            {
                chargeBadgeText.text = $"x{availableCharges}";
            }
        }
    }

    // =========================================================
    // TƯƠNG TÁC NGƯỜI CHƠI
    // =========================================================

    public void OnPointerClick(PointerEventData eventData)
    {
        if (availableCharges > 0)
        {
            TriggerPunch(0.94f, 0.16f);

            if (HexPlacementController.Instance != null)
            {
                if (HexPlacementController.Instance.IsInPlacementMode)
                {
                    HexPlacementController.Instance.CancelPlacement();
                }
                else
                {
                    HexPlacementController.Instance.StartPlacement();
                }
            }
        }
        else
        {
            TriggerPunch(1.08f, 0.18f);

            int remaining = Mathf.Max(0, activeThreshold - currentScore);
            ShowTemporaryPrompt($"<size=10><color=#FCA5A5><b>+{remaining}đ</b></color></size>");
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }

    private void ShowTemporaryPrompt(string text)
    {
        if (promptRoutine != null) StopCoroutine(promptRoutine);
        promptRoutine = StartCoroutine(CoPrompt(text, 1.6f));
    }

    private IEnumerator CoPrompt(string text, float duration)
    {
        temporaryPrompt = text;
        UpdateVisuals();
        yield return new WaitForSecondsRealtime(duration);
        temporaryPrompt = null;
        UpdateVisuals();
        promptRoutine = null;
    }

    private void TriggerPunch(float peakScale, float duration)
    {
        if (punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(CoPunch(peakScale, duration));
    }

    private IEnumerator CoPunch(float peakScale, float duration)
    {
        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            float wave = Mathf.Sin(t * Mathf.PI);
            punchScaleMultiplier = Mathf.Lerp(1f, peakScale, wave);
            yield return null;
        }

        punchScaleMultiplier = 1f;
        punchRoutine = null;
    }

    // =========================================================
    // XÂY DỰNG HUY HIỆU GỖ & ĐÁ TRÒN (BOTANICAL MEDALLION)
    // =========================================================

    private void BuildMedallionUI()
    {
        // Ẩn phông nền cam thô cũ (Score_BG)
        Transform oldBg = transform.Find("Score_BG");
        if (oldBg != null) oldBg.gameObject.SetActive(false);

        Image oldSelfImg = GetComponent<Image>();
        if (oldSelfImg != null) oldSelfImg.enabled = false;

        rootRect.sizeDelta = MedallionSize;
        rootRect.pivot = new Vector2(0.5f, 0.5f);

        // 1. Mặt Huy Hiệu Tròn Gỗ & Đá (Carved Wood & Stone Disc)
        GameObject discObj = new GameObject("MedallionBase", typeof(RectTransform));
        RectTransform discRect = (RectTransform)discObj.transform;
        discRect.SetParent(rootRect, false);
        discRect.anchorMin = Vector2.zero;
        discRect.anchorMax = Vector2.one;
        discRect.offsetMin = Vector2.zero;
        discRect.offsetMax = Vector2.zero;
        discRect.SetAsFirstSibling();

        medallionBaseImage = discObj.AddComponent<Image>();
        medallionBaseImage.sprite = MedallionBaseSprite;
        medallionBaseImage.color = Color.white;
        medallionBaseImage.raycastTarget = true;

        // 2. Dây Leo & Hoa Cỏ Uốn Quanh Vành Tròn (Winding Vine & Floral Garland)
        GameObject vineObj = new GameObject("WindingVineRing", typeof(RectTransform));
        RectTransform vineRect = (RectTransform)vineObj.transform;
        vineRect.SetParent(discRect, false);
        vineRect.anchorMin = Vector2.zero;
        vineRect.anchorMax = Vector2.one;
        vineRect.offsetMin = Vector2.zero;
        vineRect.offsetMax = Vector2.zero;

        vineFrameImage = vineObj.AddComponent<Image>();
        vineFrameImage.sprite = WindingVineSprite;
        vineFrameImage.color = Color.white;
        vineFrameImage.raycastTarget = false;

        // 3. Đính 3 Chiếc Lá Cột Mốc Mỹ Thuật Cao (Milestone Leaves along Arc)
        BuildMilestoneLeaves(discRect);

        // 4. Định hình Text Điểm Số nằm gọn gàng, trang nhã giữa lòng gỗ
        if (scoreText == null)
        {
            Transform textTr = transform.Find("Score_Text");
            if (textTr != null) scoreText = textTr.GetComponent<TMP_Text>();
        }

        if (scoreText != null)
        {
            RectTransform textRect = scoreText.rectTransform;
            textRect.SetParent(discRect, false);
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(110f, 44f);
            textRect.anchoredPosition = new Vector2(0f, -2f);

            scoreText.alignment = TextAlignmentOptions.Center;
            scoreText.fontSize = 36f;
            scoreText.fontStyle = FontStyles.Bold;
            scoreText.color = new Color(1f, 0.95f, 0.65f, 1f); // Vàng ánh kim Lumi
        }

        // Nhãn tiêu đề "ĐIỂM SỐ" nhỏ phía trên số
        headerLabel = CreateTMP("HeaderLabel", discRect, 10f, new Color(0.98f, 0.90f, 0.60f, 1f), TextAlignmentOptions.Center);
        headerLabel.rectTransform.anchoredPosition = new Vector2(0f, 24f);
        headerLabel.text = "ĐIỂM SỐ";

        // Nhãn mục tiêu phía dưới số
        targetSubLabel = CreateTMP("TargetSubLabel", discRect, 11f, new Color(0.85f, 0.90f, 0.95f, 1f), TextAlignmentOptions.Center);
        targetSubLabel.rectTransform.anchoredPosition = new Vector2(0f, -28f);
        targetSubLabel.text = "/ 10";

        // 5. Huy hiệu số lượt mở rộng đất (Badge nếu >= 2 lượt)
        GameObject badgeObj = new GameObject("ChargeBadge", typeof(RectTransform));
        chargeBadgeRoot = (RectTransform)badgeObj.transform;
        chargeBadgeRoot.SetParent(discRect, false);
        chargeBadgeRoot.anchorMin = new Vector2(0.5f, 1f);
        chargeBadgeRoot.anchorMax = new Vector2(0.5f, 1f);
        chargeBadgeRoot.pivot = new Vector2(0.5f, 0.5f);
        chargeBadgeRoot.sizeDelta = new Vector2(22f, 22f);
        chargeBadgeRoot.anchoredPosition = new Vector2(26f, -10f);

        Image badgeBg = badgeObj.AddComponent<Image>();
        badgeBg.sprite = BerryBadgeSprite;
        badgeBg.raycastTarget = false;

        chargeBadgeText = CreateTMP("Count", chargeBadgeRoot, 10f, Color.white, TextAlignmentOptions.Center);
        chargeBadgeText.rectTransform.anchorMin = Vector2.zero;
        chargeBadgeText.rectTransform.anchorMax = Vector2.one;
        chargeBadgeText.rectTransform.offsetMin = Vector2.zero;
        chargeBadgeText.rectTransform.offsetMax = Vector2.zero;
        badgeObj.SetActive(false);
    }

    private void BuildMilestoneLeaves(RectTransform parent)
    {
        // 3 Vị trí lá phân bố tự nhiên theo vành tròn như ảnh mẫu:
        // Lá 0: Dưới-Trái (Góc ~205°) - Mốc trước
        // Lá 1: Đỉnh-Giữa (Góc 90°) - Mốc hiện tại
        // Lá 2: Trên-Phải (Góc ~25°) - Mốc tương lai
        Vector2[] positions = new Vector2[]
        {
            new Vector2(-68f, -22f),
            new Vector2(0f, 66f),
            new Vector2(68f, 24f)
        };

        float[] rotations = new float[] { 40f, 0f, -45f };

        for (int i = 0; i < 3; i++)
        {
            GameObject slotObj = new GameObject($"MilestoneLeaf_{i}", typeof(RectTransform));
            RectTransform slotRect = (RectTransform)slotObj.transform;
            slotRect.SetParent(parent, false);
            float leafSize = (i == 1) ? 38f : 30f;
            slotRect.sizeDelta = new Vector2(leafSize, leafSize);
            slotRect.anchoredPosition = positions[i];
            slotRect.localRotation = Quaternion.Euler(0f, 0f, rotations[i]);

            // Nếu là chiếc lá đỉnh (i == 1): Thêm vầng hào quang nhấp nháy phía sau
            if (i == 1)
            {
                GameObject auraObj = new GameObject("BloomAura", typeof(RectTransform));
                RectTransform auraRect = (RectTransform)auraObj.transform;
                auraRect.SetParent(slotRect, false);
                auraRect.sizeDelta = new Vector2(68f, 68f);
                auraRect.anchoredPosition = Vector2.zero;

                activeBloomAuraImage = auraObj.AddComponent<Image>();
                activeBloomAuraImage.sprite = GlowHaloSprite;
                activeBloomAuraImage.color = new Color(1f, 0.92f, 0.40f, 0.75f);
                activeBloomAuraImage.raycastTarget = false;
                auraObj.SetActive(false);

                // 3 hạt bụi sao
                for (int s = 0; s < 3; s++)
                {
                    GameObject spObj = new GameObject($"Sparkle_{s}", typeof(RectTransform));
                    RectTransform spRect = (RectTransform)spObj.transform;
                    spRect.SetParent(slotRect, false);
                    spRect.sizeDelta = new Vector2(14f, 14f);

                    Image spImg = spObj.AddComponent<Image>();
                    spImg.sprite = SparkleSprite;
                    spImg.raycastTarget = false;
                    spObj.SetActive(false);
                    activeSparkles.Add(spImg);
                }
            }

            // 1. Lớp Lá Khô Nâu Cằn Cỗi (Withered Autumn Dry Leaf)
            GameObject dryObj = new GameObject("DryLeaf", typeof(RectTransform));
            RectTransform dryRect = (RectTransform)dryObj.transform;
            dryRect.SetParent(slotRect, false);
            dryRect.anchorMin = Vector2.zero;
            dryRect.anchorMax = Vector2.one;
            dryRect.offsetMin = Vector2.zero;
            dryRect.offsetMax = Vector2.zero;

            Image dryImg = dryObj.AddComponent<Image>();
            dryImg.sprite = DryLeafSprite;
            dryImg.raycastTarget = false;

            // 2. Lớp Lá Xanh Tươi Dâng Lên (Living Green Nectar Fill)
            GameObject livingObj = new GameObject("LivingGreenFill", typeof(RectTransform));
            RectTransform livingRect = (RectTransform)livingObj.transform;
            livingRect.SetParent(slotRect, false);
            livingRect.anchorMin = Vector2.zero;
            livingRect.anchorMax = Vector2.one;
            livingRect.offsetMin = Vector2.zero;
            livingRect.offsetMax = Vector2.zero;

            Image livingImg = livingObj.AddComponent<Image>();
            livingImg.sprite = LivingLeafSprite;
            livingImg.type = Image.Type.Filled;
            livingImg.fillMethod = Image.FillMethod.Vertical;
            livingImg.fillOrigin = 0; // Dâng từ cuống lên ngọn
            livingImg.fillAmount = (i == 0) ? 1f : 0f;
            livingImg.raycastTarget = false;

            // 3. Nhãn số mốc bên cạnh lá
            TextMeshProUGUI label = CreateTMP($"Badge_{i}", slotRect, 9f, Color.white, TextAlignmentOptions.Center);
            label.rectTransform.anchoredPosition = (i == 1) ? new Vector2(0f, 22f) : (i == 0) ? new Vector2(0f, -18f) : new Vector2(18f, 0f);
            label.text = "-";

            leafSlots[i] = new MilestoneLeafSlot
            {
                root = slotRect,
                dryLeafImage = dryImg,
                livingLeafImage = livingImg,
                thresholdLabel = label
            };
        }
    }

    private static TextMeshProUGUI CreateTMP(string name, RectTransform parent, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)obj.transform;
        rt.SetParent(parent, false);

        TextMeshProUGUI label = obj.AddComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    // =========================================================
    // TẠO SPRITES PROCEDURAL CHUẨN MỸ THUẬT HUY HIỆU
    // =========================================================

    public static Sprite MedallionBaseSprite
    {
        get
        {
            if (medallionBaseSprite == null) medallionBaseSprite = CreateMedallionBaseSprite(256);
            return medallionBaseSprite;
        }
    }

    public static Sprite WindingVineSprite
    {
        get
        {
            if (windingVineSprite == null) windingVineSprite = CreateWindingVineSprite(256);
            return windingVineSprite;
        }
    }

    public static Sprite LivingLeafSprite
    {
        get
        {
            if (livingLeafSprite == null) livingLeafSprite = CreateStylizedBotanicalLeafSprite(128, isDry: false);
            return livingLeafSprite;
        }
    }

    public static Sprite DryLeafSprite
    {
        get
        {
            if (dryLeafSprite == null) dryLeafSprite = CreateStylizedBotanicalLeafSprite(128, isDry: true);
            return dryLeafSprite;
        }
    }

    public static Sprite GlowHaloSprite
    {
        get
        {
            if (glowHaloSprite == null) glowHaloSprite = CreateGlowHaloSprite(128);
            return glowHaloSprite;
        }
    }

    public static Sprite SparkleSprite
    {
        get
        {
            if (sparkleSprite == null) sparkleSprite = CreateSparkleSprite(64);
            return sparkleSprite;
        }
    }

    public static Sprite BerryBadgeSprite
    {
        get
        {
            if (berryBadgeSprite == null) berryBadgeSprite = CreateBerryBadgeSprite(64);
            return berryBadgeSprite;
        }
    }

    /// <summary>
    /// Vẽ Mặt Huy Hiệu Gỗ & Vành Đá Tròn cổ kính
    /// </summary>
    private static Sprite CreateMedallionBaseSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] colors = new Color[size * size];

        float center = (size - 1) * 0.5f;
        float maxR = center * 0.96f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / maxR;
                float dy = (y - center) / maxR;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                if (r > 1.0f)
                {
                    colors[y * size + x] = Color.clear;
                    continue;
                }

                float outerAlpha = Mathf.Clamp01((1.0f - r) / 0.02f);

                // 1. Vành Đá Cổ Kính Ngoài Cùng (r trong khoảng [0.82, 1.0])
                if (r > 0.82f)
                {
                    float tStone = (r - 0.82f) / 0.18f;
                    // Đổ bóng 3D xiên từ góc trên-trái
                    float light = (dx * -0.5f + dy * 0.7f) * 0.35f + 0.65f;
                    Color stoneBase = new Color(0.42f, 0.46f, 0.48f, 1f) * light;
                    Color stoneDark = new Color(0.24f, 0.28f, 0.30f, 1f);

                    Color c = Color.Lerp(stoneDark, stoneBase, tStone);

                    // Mạch ghép khối đá ở 6 góc
                    float seamDist = Mathf.Abs(Mathf.Sin(angle * 3f));
                    if (seamDist < 0.04f)
                    {
                        c = Color.Lerp(c, new Color(0.15f, 0.18f, 0.20f, 1f), 0.65f);
                    }

                    c.a = outerAlpha;
                    colors[y * size + x] = c;
                }
                // 2. Viền Gỗ Chạm Khắc Cổ Thụ (r trong khoảng [0.70, 0.82])
                else if (r > 0.70f)
                {
                    float tRelief = (r - 0.70f) / 0.12f;
                    Color woodDarkRim = new Color(0.22f, 0.14f, 0.08f, 1f);
                    Color woodRelief = new Color(0.38f, 0.24f, 0.14f, 1f);

                    float carveWave = Mathf.Sin(angle * 12f) * 0.5f + 0.5f;
                    Color c = Color.Lerp(woodDarkRim, woodRelief, carveWave * (1f - Mathf.Abs(tRelief - 0.5f) * 2f));
                    c.a = outerAlpha;
                    colors[y * size + x] = c;
                }
                // 3. Mặt Gỗ Sồi Ấm Áp Bên Trong (r <= 0.70)
                else
                {
                    float tWood = r / 0.70f;
                    // Vân thớ gỗ sồi
                    float grain = Mathf.Sin(y * 0.45f) * 0.035f;

                    // Tâm vàng nâu ấm, viền ngoài đậm sâu
                    Color woodCenter = new Color(0.48f, 0.30f, 0.16f, 1f);
                    Color woodEdge = new Color(0.20f, 0.12f, 0.06f, 1f);

                    Color c = Color.Lerp(woodCenter, woodEdge, Mathf.Pow(tWood, 1.4f) + grain);
                    c.a = outerAlpha;
                    colors[y * size + x] = c;
                }
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// Vẽ Dây Leo & Cụm Hoa Cỏ uốn quanh vành tròn huy hiệu
    /// </summary>
    private static Sprite CreateWindingVineSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] colors = new Color[size * size];

        float center = (size - 1) * 0.5f;
        float maxR = center * 0.96f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / maxR;
                float dy = (y - center) / maxR;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                // Trục dây leo chạy ở bán kính r ~ 0.88
                float vineWave = 0.88f + 0.04f * Mathf.Sin(angle * 7f);
                float distToVine = Mathf.Abs(r - vineWave) * maxR;

                Color pixelColor = Color.clear;

                if (distToVine < 5.5f)
                {
                    float alpha = Mathf.Clamp01((5.5f - distToVine) / 1.5f);

                    // Nửa trái (x < 0, angle trong [60°, 240°]): Dây leo xanh tươi phát sáng nhựa sống
                    if (dx < 0.15f)
                    {
                        Color vineGreen = new Color(0.20f, 0.62f, 0.28f, 1f);
                        // Lõi nhựa sống ngọc lam phát sáng
                        if (distToVine < 2.0f)
                        {
                            vineGreen = Color.Lerp(vineGreen, new Color(0.50f, 1.00f, 0.85f, 1f), 0.85f);
                        }
                        pixelColor = vineGreen;
                    }
                    else
                    {
                        // Nửa phải: Dây gỗ nâu rừng
                        Color vineBark = new Color(0.32f, 0.20f, 0.12f, 1f);
                        pixelColor = vineBark;
                    }

                    pixelColor.a = alpha;
                }

                // Cụm hoa dại nhỏ điểm xuyết quanh vành
                // 1. Hoa hồng phấn ở góc trên-trái (angle ~ 2.4 rad)
                float dFlower1 = Mathf.Sqrt(Mathf.Pow(dx + 0.62f, 2f) + Mathf.Pow(dy - 0.65f, 2f)) * maxR;
                if (dFlower1 < 9f)
                {
                    float fAlpha = Mathf.Clamp01(9f - dFlower1);
                    Color flowerPink = (dFlower1 < 3f) ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(1.0f, 0.55f, 0.72f, 1f);
                    pixelColor = Color.Lerp(pixelColor, flowerPink, fAlpha);
                }

                // 2. Hoa xanh dương ở trên (angle ~ 1.8 rad)
                float dFlower2 = Mathf.Sqrt(Mathf.Pow(dx + 0.25f, 2f) + Mathf.Pow(dy - 0.86f, 2f)) * maxR;
                if (dFlower2 < 8f)
                {
                    float fAlpha = Mathf.Clamp01(8f - dFlower2);
                    Color flowerBlue = (dFlower2 < 2.5f) ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(0.35f, 0.78f, 1.00f, 1f);
                    pixelColor = Color.Lerp(pixelColor, flowerBlue, fAlpha);
                }

                // 3. Hoa vàng dại ở góc dưới (angle ~ -1.3 rad)
                float dFlower3 = Mathf.Sqrt(Mathf.Pow(dx - 0.25f, 2f) + Mathf.Pow(dy + 0.84f, 2f)) * maxR;
                if (dFlower3 < 9f)
                {
                    float fAlpha = Mathf.Clamp01(9f - dFlower3);
                    Color flowerYellow = (dFlower3 < 3f) ? new Color(0.9f, 0.45f, 0.1f, 1f) : new Color(1.00f, 0.88f, 0.25f, 1f);
                    pixelColor = Color.Lerp(pixelColor, flowerYellow, fAlpha);
                }

                colors[y * size + x] = pixelColor;
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// Vẽ Chiếc Lá Mỹ Thuật Cao (Stylized Fantasy Leaf)
    /// </summary>
    private static Sprite CreateStylizedBotanicalLeafSprite(int size, bool isDry)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] colors = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            float ny = (float)y / (size - 1);

            // Đường cong sống lưng chữ S uyển chuyển
            float spineOffset = Mathf.Sin(ny * Mathf.PI) * 0.035f - Mathf.Sin(ny * Mathf.PI * 2f) * 0.015f;
            float spineX = 0.5f + spineOffset;

            float t = Mathf.Clamp01((ny - 0.06f) / 0.88f);

            // Bề rộng phiến lá tự nhiên
            float baseWidth = (ny < 0.06f) 
                ? 0.035f 
                : Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.72f) * (1.0f - 0.28f * t) * 0.40f;

            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / (size - 1);
                float dx = nx - spineX;

                float sideWidth = (dx < 0f) ? baseWidth * 1.05f : baseWidth * 0.95f;
                float distFromEdge = Mathf.Abs(dx) - sideWidth;
                float feather = 1.8f / size;

                if (distFromEdge > feather)
                {
                    colors[y * size + x] = Color.clear;
                    continue;
                }

                float alpha = Mathf.Clamp01(-distFromEdge / feather);
                float u = Mathf.Clamp(dx / Mathf.Max(0.001f, sideWidth), -1f, 1f);

                if (isDry)
                {
                    // LÁ KHÔ NÂU CẰN CỖI (Withered Autumn Dry Leaf)
                    Color darkBase = Color.Lerp(
                        new Color(0.24f, 0.16f, 0.10f, 0.98f),
                        new Color(0.38f, 0.26f, 0.16f, 0.98f),
                        ny
                    );

                    float centralVein = Mathf.Abs(dx);
                    if (centralVein < 0.025f && ny > 0.04f)
                    {
                        darkBase = Color.Lerp(darkBase, new Color(0.55f, 0.40f, 0.25f, 1f), 0.7f);
                    }

                    darkBase.a *= alpha;
                    colors[y * size + x] = darkBase;
                }
                else
                {
                    // LÁ XANH TƯƠI SỐNG (Lush Living Leaf)
                    Color leafLight = Color.Lerp(
                        new Color(0.16f, 0.78f, 0.36f, 1f),
                        new Color(0.68f, 1.00f, 0.42f, 1f),
                        ny
                    );

                    Color leafShade = Color.Lerp(
                        new Color(0.08f, 0.48f, 0.22f, 1f),
                        new Color(0.20f, 0.72f, 0.34f, 1f),
                        ny
                    );

                    Color bladeCol = (u < 0f) 
                        ? Color.Lerp(leafLight, leafLight * 1.08f, -u * 0.4f) 
                        : Color.Lerp(leafLight, leafShade, u * 0.85f);

                    // Gân chính phát sáng rực rỡ
                    float centralVein = Mathf.Abs(dx);
                    if (centralVein < 0.022f && ny > 0.04f)
                    {
                        bladeCol = Color.Lerp(bladeCol, new Color(0.92f, 1.00f, 0.65f, 1f), 0.85f);
                    }

                    // Viền lá bắt sáng
                    if (distFromEdge > -2.5f / size)
                    {
                        bladeCol = Color.Lerp(bladeCol, new Color(0.85f, 1.00f, 0.70f, 1f), 0.70f);
                    }

                    bladeCol.a *= alpha;
                    colors[y * size + x] = bladeCol;
                }
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateGlowHaloSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] colors = new Color[size * size];
        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / center;

                if (dist >= 1f)
                {
                    colors[y * size + x] = Color.clear;
                }
                else
                {
                    float intensity = Mathf.Pow(1f - dist, 1.8f);
                    colors[y * size + x] = new Color(1f, 0.95f, 0.50f, intensity);
                }
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateSparkleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] colors = new Color[size * size];
        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = Mathf.Abs((x - center) / center);
                float ny = Mathf.Abs((y - center) / center);

                float beam = Mathf.Max(
                    Mathf.Pow(Mathf.Max(0f, 1f - nx), 6f) * Mathf.Pow(Mathf.Max(0f, 1f - ny), 1.2f),
                    Mathf.Pow(Mathf.Max(0f, 1f - ny), 6f) * Mathf.Pow(Mathf.Max(0f, 1f - nx), 1.2f)
                );
                float core = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Sqrt(nx * nx + ny * ny)), 2.5f);
                float val = Mathf.Clamp01(beam * 0.85f + core);

                colors[y * size + x] = new Color(1f, 0.98f, 0.85f, val);
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateBerryBadgeSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] colors = new Color[size * size];
        float center = (size - 1) * 0.5f;
        float radius = center - 1f;

        Color rim = new Color(0.85f, 0.40f, 0.10f, 1f);
        Color face = new Color(1.00f, 0.65f, 0.18f, 1f);
        Color shine = new Color(1.00f, 0.96f, 0.80f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > radius + 0.5f)
                {
                    colors[y * size + x] = Color.clear;
                    continue;
                }

                float r = dist / radius;
                Color c = Color.Lerp(face, rim, Mathf.SmoothStep(0.5f, 1f, r));

                float sx = dx + radius * 0.35f;
                float sy = dy - radius * 0.35f;
                float shineDist = Mathf.Sqrt(sx * sx + sy * sy) / (radius * 0.35f);
                float shineAmount = Mathf.Clamp01(1f - shineDist);
                c = Color.Lerp(c, shine, shineAmount * 0.85f);

                c.a = Mathf.Clamp01(radius - dist + 0.5f);
                colors[y * size + x] = c;
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }
}