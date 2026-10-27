using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bảng Tiến Trình Hệ Sinh Thái Đảo (Achievement & Progression Modal).
/// Thiết kế hiện đại đồng bộ UI tươi sáng xanh trắng:
/// - Kích thước panel vừa vặn nghệ thuật (880x554px) với 4 góc bo tròn mềm mại.
/// - Tiêu đề ngoài phía trên panel: "ACHIEVEMENTS" (Full tiếng Anh).
/// - Nút X thoát ra ngoài góc 1h của panel, bấm thoát dứt khoát không chạm vào ô thẻ.
/// - 4 hàng thẻ ngang: Icon lục giác đặc trưng, tiêu đề tiếng Anh, điều kiện & phần thưởng tinh tế (không còn ô vuông xanh rườm rà).
/// - Dấu tick hoàn thành dùng chữ "V" sắc nét chuẩn font, không bị lỗi ô vuông.
/// - Bấm vào ô thẻ để xem chi tiết; bấm nút Claim để nhận thưởng.
/// - Thanh tiến trình dưới tinh tế, phong cách sáng hiện đại, không thao tác bấm mà tự động kích hoạt mốc & tick "V" khi claim thành tựu.
/// </summary>
public class AchievementUI : MonoBehaviour
{
    private static AchievementUI instance;
    public static AchievementUI Instance => instance;

    [Header("UI Structure")]
    private RectTransform windowModal;
    private RectTransform panelRect;

    // Kích thước chuẩn gọn gàng
    private const float PanelWidth = 880f;
    private const float PanelHeight = 554f;

    // 4 Hàng thẻ ngang (Y chuẩn tâm khớp chính xác 4 ô trắng của panel_bg_cards đã scale 0.86)
    private readonly float[] rowPositionsY = new float[] { 219f, 98f, -24f, -144f };
    private readonly string[] milestoneIconNames = new string[]
    {
        "thanhtuu/moitruongsongdautien",
        "thanhtuu/loaimoi",
        "thanhtuu/lencap",
        "thanhtuu/cauchuyendao"
    };

    private readonly string[] milestoneDefaultTitles = new string[]
    {
        "First Habitat",
        "New Species",
        "Rank Up",
        "Island Story"
    };

    private Image[] nodeImages = new Image[4];
    private GameObject[] lockOverlays = new GameObject[4];
    private GameObject[] checkBadges = new GameObject[4];
    private TextMeshProUGUI[] titleTexts = new TextMeshProUGUI[4];
    private TextMeshProUGUI[] descTexts = new TextMeshProUGUI[4];
    private TextMeshProUGUI[] rewardTexts = new TextMeshProUGUI[4];
    private Button[] claimButtons = new Button[4];
    private Image[] claimButtonImages = new Image[4];
    private TextMeshProUGUI[] claimButtonLabels = new TextMeshProUGUI[4];
    private Image[] claimCheckIcons = new Image[4];
    private DialogButtonJuice[] claimJuices = new DialogButtonJuice[4];
    private Sprite checkSprite;

    // Bottom Progress Bar Components (Display Only)
    private TextMeshProUGUI progressBadgeText;
    private RectTransform trackFillRect;
    private Image trackFillImage;
    private RectTransform trackBloomRect;
    private Image trackBloomImage;
    private RectTransform trackPulseWaveRect;
    private Image trackPulseWaveImage;

    // 4 Cột mốc trên thanh tiến trình dưới
    private readonly int[] trackRewardCoins = new int[] { 50, 100, 150, 300 };
    private readonly float[] trackNodePositionsX = new float[] { -127.5f, 30.0f, 187.5f, 345.0f };
    private Image[] trackMilestoneBgs = new Image[4];
    private Image[] trackMilestoneBorders = new Image[4];
    private GameObject[] trackMilestoneChecks = new GameObject[4];
    private TextMeshProUGUI[] trackMilestoneCoins = new TextMeshProUGUI[4];
    private bool[] trackMilestonesClaimed = new bool[4];

    // HUD Button
    private GameObject hudButtonObj;
    private RectTransform hudButtonRect;
    private Image hudGlowOverlay;
    private bool isHudFlashing = false;

    private float currentAnimRatio = 0f;
    private Coroutine trackAnimRoutine;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        LoadTrackMilestones();
        BuildUI();
    }

    private void Start()
    {
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnMilestonesChanged += RefreshData;
            AchievementManager.Instance.OnHarmonyChanged += (h, t) => RefreshData();
            AchievementManager.Instance.OnAchievementsUpdated += RefreshData;
        }

        EconomyHUD.RegisterWindow(windowModal.gameObject, CloseWindow);
        RefreshData();
    }

    private void OnDestroy()
    {
        if (AchievementManager.HasInstance)
        {
            AchievementManager.Instance.OnMilestonesChanged -= RefreshData;
            AchievementManager.Instance.OnAchievementsUpdated -= RefreshData;
        }
    }

    private void Update()
    {
        EnsureHUDButton();
        UpdateHUDButtonFlashing();
        UpdateCyberTrackFX();
    }

    private void LoadTrackMilestones()
    {
        for (int i = 0; i < 4; i++)
        {
            trackMilestonesClaimed[i] = PlayerPrefs.GetInt("ACH_TRACK_CLAIMED_" + i, 0) == 1;
        }
    }

    // =========================================================
    // UI BUILDER
    // =========================================================

    private void BuildUI()
    {
        // 1. Root Modal (Backdrop Dimmer)
        GameObject modalObj = new GameObject("AchievementModal", typeof(RectTransform), typeof(Image));
        modalObj.transform.SetParent(transform, false);
        windowModal = modalObj.GetComponent<RectTransform>();
        EconomyHUD.Stretch(windowModal);

        Image dimmerImg = modalObj.GetComponent<Image>();
        dimmerImg.color = new Color(0.04f, 0.08f, 0.12f, 0.65f);

        Button dismissBtn = modalObj.AddComponent<Button>();
        dismissBtn.transition = Selectable.Transition.None;
        dismissBtn.onClick.AddListener(CloseWindow);

        // 2. Main Panel (Kích thước 880 x 554 vừa vặn hoàn hảo)
        GameObject panelObj = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(windowModal, false);
        panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);

        Image panelImg = panelObj.GetComponent<Image>();
        Sprite bgSprite = Resources.Load<Sprite>("thanhtuu/panel_bg_cards") ?? Resources.Load<Sprite>("thanhtuu/Backgorund");
        panelImg.sprite = bgSprite;
        panelImg.type = Image.Type.Simple;
        panelImg.preserveAspect = true;
        panelImg.raycastTarget = true; // Chặn chuột không xuyên qua backdrop phía sau

        // Tiêu đề ngoài phía trên panel: "ACHIEVEMENTS"
        BuildExternalTitle();

        // 3. Xây dựng 4 hàng ô thẻ thành tựu ngang
        Sprite lockSprite = Resources.Load<Sprite>("PreserveLockIcon");
        Sprite claimBtnSprite = Resources.Load<Sprite>("thanhtuu/btn_claim");
        checkSprite = Resources.Load<Sprite>("thanhtuu/icon_check");

        for (int i = 0; i < 4; i++)
        {
            int colIndex = i;
            float posY = rowPositionsY[i];

            // A. Khung ô thẻ hàng ngang (Kích thước 834 x 108px)
            GameObject cardObj = new GameObject($"CardRow_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
            cardObj.transform.SetParent(panelRect, false);
            RectTransform cardRect = cardObj.GetComponent<RectTransform>();
            cardRect.anchoredPosition = new Vector2(0f, posY);
            cardRect.sizeDelta = new Vector2(834f, 108f);

            Image cardImg = cardObj.GetComponent<Image>();
            cardImg.color = Color.clear;
            cardImg.raycastTarget = true;

            // Bấm vào ô thẻ để mở chi tiết nhiệm vụ
            Button cardBtn = cardObj.GetComponent<Button>();
            cardBtn.transition = Selectable.Transition.None;
            cardBtn.targetGraphic = cardImg;
            cardBtn.onClick.AddListener(() => OnDetailClicked(colIndex));

            // B. Hexagon Icon ở bên trái
            GameObject nodeObj = new GameObject("HexIcon", typeof(RectTransform), typeof(Image));
            nodeObj.transform.SetParent(cardObj.transform, false);
            RectTransform nodeRect = nodeObj.GetComponent<RectTransform>();
            nodeRect.anchoredPosition = new Vector2(-360f, 0f);
            nodeRect.sizeDelta = new Vector2(70f, 80f);

            Image nodeImg = nodeObj.GetComponent<Image>();
            Sprite iconSp = Resources.Load<Sprite>(milestoneIconNames[i]);
            nodeImg.sprite = iconSp;
            nodeImg.preserveAspect = true;
            nodeImg.raycastTarget = false;
            nodeImages[i] = nodeImg;

            // Ổ Khóa đè lên icon khi chưa hoàn thành
            GameObject lockObj = new GameObject("LockOverlay", typeof(RectTransform), typeof(Image));
            lockObj.transform.SetParent(nodeObj.transform, false);
            RectTransform lockRect = lockObj.GetComponent<RectTransform>();
            lockRect.anchoredPosition = Vector2.zero;
            lockRect.sizeDelta = new Vector2(28f, 28f);

            Image lockImg = lockObj.GetComponent<Image>();
            lockImg.sprite = lockSprite;
            lockImg.preserveAspect = true;
            lockImg.color = Color.white;
            lockImg.raycastTarget = false;
            lockOverlays[i] = lockObj;

            // Huy hiệu hoàn thành với icon tick chuẩn đồ họa (từ icon_check.png, thay thế hoàn toàn chữ 'V' và ô vuông unicode)
            GameObject checkObj = new GameObject("CheckBadge", typeof(RectTransform), typeof(Image));
            checkObj.transform.SetParent(nodeObj.transform, false);
            RectTransform checkRect = checkObj.GetComponent<RectTransform>();
            checkRect.anchoredPosition = new Vector2(24f, 26f);
            checkRect.sizeDelta = new Vector2(24f, 24f);

            Image checkBg = checkObj.GetComponent<Image>();
            checkBg.sprite = EconomyHUD.RoundedSprite;
            checkBg.color = Color.white;
            checkBg.raycastTarget = false;
            EconomyHUD.AddBlueBorder(checkRect, new Color(0.05f, 0.72f, 0.42f, 0.95f));

            GameObject checkIconObj = new GameObject("TickIcon", typeof(RectTransform), typeof(Image));
            checkIconObj.transform.SetParent(checkObj.transform, false);
            RectTransform checkIconRect = checkIconObj.GetComponent<RectTransform>();
            checkIconRect.anchoredPosition = Vector2.zero;
            checkIconRect.sizeDelta = new Vector2(17f, 17f);

            Image checkIconImg = checkIconObj.GetComponent<Image>();
            checkIconImg.sprite = checkSprite;
            checkIconImg.preserveAspect = true;
            checkIconImg.raycastTarget = false;

            checkBadges[i] = checkObj;

            // C. Cụm chữ thông tin: Tiêu đề tiếng Anh, điều kiện & dòng phần thưởng thanh lịch
            GameObject infoObj = new GameObject("InfoGroup", typeof(RectTransform));
            infoObj.transform.SetParent(cardObj.transform, false);
            RectTransform infoRect = infoObj.GetComponent<RectTransform>();
            infoRect.anchoredPosition = new Vector2(-40f, 0f);
            infoRect.sizeDelta = new Vector2(490f, 96f);

            // Tiêu đề thành tựu
            GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(infoObj.transform, false);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0f, 24f);
            titleRect.sizeDelta = new Vector2(490f, 26f);

            TextMeshProUGUI titleTxt = titleObj.GetComponent<TextMeshProUGUI>();
            titleTxt.text = milestoneDefaultTitles[i];
            titleTxt.fontSize = 19f;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.color = new Color(0.10f, 0.15f, 0.24f, 1f);
            titleTxt.alignment = TextAlignmentOptions.Left;
            titleTxt.raycastTarget = false;
            titleTexts[i] = titleTxt;

            // Điều kiện ngắn gọn
            GameObject descObj = new GameObject("Desc", typeof(RectTransform), typeof(TextMeshProUGUI));
            descObj.transform.SetParent(infoObj.transform, false);
            RectTransform descRect = descObj.GetComponent<RectTransform>();
            descRect.anchoredPosition = new Vector2(0f, 1f);
            descRect.sizeDelta = new Vector2(490f, 20f);

            TextMeshProUGUI descTxt = descObj.GetComponent<TextMeshProUGUI>();
            descTxt.fontSize = 12.5f;
            descTxt.color = new Color(0.38f, 0.45f, 0.55f, 1f);
            descTxt.alignment = TextAlignmentOptions.Left;
            descTxt.raycastTarget = false;
            descTexts[i] = descTxt;

            // Dòng phần thưởng thanh lịch, KHÔNG CÒN các ô vuông xanh lá rườm rà
            GameObject rewardObj = new GameObject("RewardLine", typeof(RectTransform), typeof(TextMeshProUGUI));
            rewardObj.transform.SetParent(infoObj.transform, false);
            RectTransform rewardRect = rewardObj.GetComponent<RectTransform>();
            rewardRect.anchoredPosition = new Vector2(0f, -22f);
            rewardRect.sizeDelta = new Vector2(490f, 20f);

            TextMeshProUGUI rewardTxt = rewardObj.GetComponent<TextMeshProUGUI>();
            rewardTxt.fontSize = 12.5f;
            rewardTxt.alignment = TextAlignmentOptions.Left;
            rewardTxt.raycastTarget = false;
            rewardTexts[i] = rewardTxt;

            // D. Nút CLAIM (btn_claim) ở bên phải ô thẻ
            GameObject claimBtnObj = new GameObject("ClaimBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            claimBtnObj.transform.SetParent(cardObj.transform, false);
            RectTransform claimRect = claimBtnObj.GetComponent<RectTransform>();
            claimRect.anchoredPosition = new Vector2(335f, 0f);
            claimRect.sizeDelta = new Vector2(130f, 42f);

            Image claimImg = claimBtnObj.GetComponent<Image>();
            claimImg.sprite = claimBtnSprite;
            claimImg.preserveAspect = true;
            claimImg.raycastTarget = true;
            claimButtonImages[i] = claimImg;

            Button claimBtn = claimBtnObj.GetComponent<Button>();
            claimBtn.targetGraphic = claimImg;
            claimBtn.onClick.AddListener(() => OnClaimClicked(colIndex));
            claimButtons[i] = claimBtn;

            // Nhãn chữ bên trong nút Claim
            GameObject claimTxtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            claimTxtObj.transform.SetParent(claimBtnObj.transform, false);
            RectTransform claimTxtRect = claimTxtObj.GetComponent<RectTransform>();
            claimTxtRect.anchorMin = Vector2.zero;
            claimTxtRect.anchorMax = Vector2.one;
            claimTxtRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI claimTxt = claimTxtObj.GetComponent<TextMeshProUGUI>();
            claimTxt.text = "Claim";
            claimTxt.alignment = TextAlignmentOptions.Center;
            claimTxt.verticalAlignment = VerticalAlignmentOptions.Middle;
            claimTxt.fontSize = 15.5f;
            claimTxt.fontStyle = FontStyles.Bold;
            claimTxt.color = Color.white;
            claimTxt.raycastTarget = false;
            claimButtonLabels[i] = claimTxt;

            // Icon checkmark đồ họa trong nút Claim khi đã nhận thưởng
            GameObject claimCheckObj = new GameObject("CheckIcon", typeof(RectTransform), typeof(Image));
            claimCheckObj.transform.SetParent(claimBtnObj.transform, false);
            RectTransform claimCheckRect = claimCheckObj.GetComponent<RectTransform>();
            claimCheckRect.anchoredPosition = new Vector2(-36f, 0f);
            claimCheckRect.sizeDelta = new Vector2(18f, 18f);

            Image claimCheckImg = claimCheckObj.GetComponent<Image>();
            claimCheckImg.sprite = checkSprite;
            claimCheckImg.preserveAspect = true;
            claimCheckImg.raycastTarget = false;
            claimCheckIcons[i] = claimCheckImg;
            claimCheckObj.SetActive(false);

            DialogButtonJuice juice = claimBtnObj.AddComponent<DialogButtonJuice>();
            juice.Initialize(null, new Color(0.08f, 0.58f, 0.95f, 0.45f));
            claimJuices[i] = juice;
        }

        // 4. Xây dựng Thanh Tiến Trình Công Nghệ Tươi Sáng Sắc Nét Bên Dưới
        BuildBottomProgressBar();

        // 5. Nút Thoát (X) đưa hoàn toàn ra ngoài viền góc 1h của panel (Đảm bảo click không chạm vào thẻ)
        BuildCornerExitButton();

        windowModal.gameObject.SetActive(false);
    }

    private void BuildExternalTitle()
    {
        // Thanh tiêu đề nổi bên ngoài phía trên panel: "ACHIEVEMENTS"
        GameObject titleObj = new GameObject("PanelExternalTitle", typeof(RectTransform), typeof(Image));
        titleObj.transform.SetParent(panelRect, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(0f, (PanelHeight * 0.5f) + 20f);
        titleRect.sizeDelta = new Vector2(250f, 40f);

        Image titleBg = titleObj.GetComponent<Image>();
        titleBg.sprite = EconomyHUD.RoundedSprite;
        titleBg.type = Image.Type.Sliced;
        titleBg.color = new Color(0.06f, 0.10f, 0.18f, 0.95f);
        titleBg.raycastTarget = false;
        EconomyHUD.AddBlueBorder(titleRect, new Color(0.08f, 0.58f, 0.95f, 0.95f));

        GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(titleObj.transform, false);
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        EconomyHUD.Stretch(txtRect);

        TextMeshProUGUI txt = txtObj.GetComponent<TextMeshProUGUI>();
        txt.text = "ACHIEVEMENTS";
        txt.alignment = TextAlignmentOptions.Center;
        txt.verticalAlignment = VerticalAlignmentOptions.Middle;
        txt.fontSize = 17f;
        txt.fontStyle = FontStyles.Bold;
        txt.color = Color.white;
        txt.raycastTarget = false;
    }

    private void BuildCornerExitButton()
    {
        // Nút X đặt dính viền góc ngoài hướng 1h của panel (X = +452, Y = +289)
        GameObject closeObj = new GameObject("CornerExitBtn", typeof(RectTransform), typeof(Button), typeof(Image));
        closeObj.transform.SetParent(panelRect, false);
        RectTransform closeRect = closeObj.GetComponent<RectTransform>();
        closeRect.anchoredPosition = new Vector2((PanelWidth * 0.5f) + 12f, (PanelHeight * 0.5f) + 12f);
        closeRect.sizeDelta = new Vector2(38f, 38f);

        Image closeImg = closeObj.GetComponent<Image>();
        closeImg.sprite = EconomyHUD.RoundedSprite;
        closeImg.type = Image.Type.Sliced;
        closeImg.color = Color.white;
        closeImg.raycastTarget = true;
        EconomyHUD.AddBlueBorder(closeRect, new Color(0.08f, 0.58f, 0.95f, 0.95f));

        Button closeBtn = closeObj.GetComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(CloseWindow);

        GameObject closeTxtObj = new GameObject("X", typeof(RectTransform), typeof(TextMeshProUGUI));
        closeTxtObj.transform.SetParent(closeRect, false);
        TextMeshProUGUI closeTxt = closeTxtObj.GetComponent<TextMeshProUGUI>();
        closeTxt.text = "X";
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.fontSize = 17f;
        closeTxt.fontStyle = FontStyles.Bold;
        closeTxt.color = new Color(0.06f, 0.12f, 0.22f, 1f);
        closeTxt.raycastTarget = false;

        DialogButtonJuice closeJuice = closeObj.AddComponent<DialogButtonJuice>();
        closeJuice.Initialize(null, new Color(0.08f, 0.58f, 0.95f, 0.35f));

        // Đảm bảo nút X nằm trên cùng tuyệt đối của raycast
        closeObj.transform.SetAsLastSibling();
    }

    private void BuildBottomProgressBar()
    {
        GameObject bottomGroup = new GameObject("BottomProgressArea", typeof(RectTransform));
        bottomGroup.transform.SetParent(panelRect, false);
        RectTransform groupRect = bottomGroup.GetComponent<RectTransform>();
        groupRect.anchoredPosition = new Vector2(0f, -237f);
        groupRect.sizeDelta = new Vector2(834f, 50f);

        // 1. Huy hiệu đếm tiến độ bên trái (X = -360f)
        GameObject badgeObj = new GameObject("ProgressBadge", typeof(RectTransform), typeof(Image));
        badgeObj.transform.SetParent(bottomGroup.transform, false);
        RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
        badgeRect.anchoredPosition = new Vector2(-360f, 0f);
        badgeRect.sizeDelta = new Vector2(80f, 34f);

        Image badgeImg = badgeObj.GetComponent<Image>();
        badgeImg.sprite = EconomyHUD.RoundedSprite;
        badgeImg.type = Image.Type.Sliced;
        badgeImg.color = new Color(0.06f, 0.10f, 0.18f, 0.95f);
        badgeImg.raycastTarget = false;
        EconomyHUD.AddBlueBorder(badgeRect, new Color(0.08f, 0.58f, 0.95f, 0.85f));

        GameObject badgeTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        badgeTxtObj.transform.SetParent(badgeObj.transform, false);
        RectTransform badgeTxtRect = badgeTxtObj.GetComponent<RectTransform>();
        EconomyHUD.Stretch(badgeTxtRect);

        progressBadgeText = badgeTxtObj.GetComponent<TextMeshProUGUI>();
        progressBadgeText.text = "0/4";
        progressBadgeText.alignment = TextAlignmentOptions.Center;
        progressBadgeText.verticalAlignment = VerticalAlignmentOptions.Middle;
        progressBadgeText.fontSize = 15f;
        progressBadgeText.fontStyle = FontStyles.Bold;
        progressBadgeText.color = Color.white;
        progressBadgeText.raycastTarget = false;

        // 2. Rãnh thanh tiến trình sáng sắc nét (Dài 630px, tâm X = 30px)
        const float trackLength = 630f;
        GameObject trackBgObj = new GameObject("TrackGroove", typeof(RectTransform), typeof(Image));
        trackBgObj.transform.SetParent(bottomGroup.transform, false);
        RectTransform trackBgRect = trackBgObj.GetComponent<RectTransform>();
        trackBgRect.anchoredPosition = new Vector2(30f, 0f);
        trackBgRect.sizeDelta = new Vector2(trackLength, 10f);

        Image trackBgImg = trackBgObj.GetComponent<Image>();
        trackBgImg.sprite = EconomyHUD.RoundedSprite;
        trackBgImg.type = Image.Type.Sliced;
        trackBgImg.color = new Color(0.80f, 0.86f, 0.92f, 1f); // Nền rãnh sáng dịu mắt, không còn màu đen thô
        trackBgImg.raycastTarget = false;
        EconomyHUD.AddBlueBorder(trackBgRect, new Color(0.65f, 0.78f, 0.90f, 0.80f));

        // Vầng hào quang phát sáng neon (Neon Bloom)
        GameObject bloomObj = new GameObject("TrackBloom", typeof(RectTransform), typeof(Image));
        bloomObj.transform.SetParent(bottomGroup.transform, false);
        trackBloomRect = bloomObj.GetComponent<RectTransform>();
        trackBloomRect.pivot = new Vector2(0f, 0.5f);
        trackBloomRect.anchorMin = new Vector2(0.5f, 0.5f);
        trackBloomRect.anchorMax = new Vector2(0.5f, 0.5f);
        trackBloomRect.anchoredPosition = new Vector2(30f - trackLength * 0.5f, 0f);
        trackBloomRect.sizeDelta = new Vector2(0f, 18f);

        trackBloomImage = bloomObj.GetComponent<Image>();
        trackBloomImage.sprite = EconomyHUD.RoundedSprite;
        trackBloomImage.type = Image.Type.Sliced;
        trackBloomImage.color = new Color(0.04f, 0.85f, 1f, 0.40f);
        trackBloomImage.raycastTarget = false;

        // Chùm tia năng lượng chính (Core Beam)
        GameObject trackFillObj = new GameObject("TrackBeam", typeof(RectTransform), typeof(Image));
        trackFillObj.transform.SetParent(bottomGroup.transform, false);
        trackFillRect = trackFillObj.GetComponent<RectTransform>();
        trackFillRect.pivot = new Vector2(0f, 0.5f);
        trackFillRect.anchorMin = new Vector2(0.5f, 0.5f);
        trackFillRect.anchorMax = new Vector2(0.5f, 0.5f);
        trackFillRect.anchoredPosition = new Vector2(30f - trackLength * 0.5f, 0f);
        trackFillRect.sizeDelta = new Vector2(0f, 8f);

        trackFillImage = trackFillObj.GetComponent<Image>();
        trackFillImage.sprite = EconomyHUD.RoundedSprite;
        trackFillImage.type = Image.Type.Sliced;
        trackFillImage.color = new Color(0.05f, 0.68f, 0.95f, 0.95f);
        trackFillImage.raycastTarget = false;

        // Xung điện chạy dọc thanh
        GameObject waveObj = new GameObject("TrackPulseWave", typeof(RectTransform), typeof(Image));
        waveObj.transform.SetParent(trackFillObj.transform, false);
        trackPulseWaveRect = waveObj.GetComponent<RectTransform>();
        trackPulseWaveRect.pivot = new Vector2(0.5f, 0.5f);
        trackPulseWaveRect.anchorMin = new Vector2(0f, 0.5f);
        trackPulseWaveRect.anchorMax = new Vector2(0f, 0.5f);
        trackPulseWaveRect.anchoredPosition = Vector2.zero;
        trackPulseWaveRect.sizeDelta = new Vector2(35f, 10f);

        trackPulseWaveImage = waveObj.GetComponent<Image>();
        trackPulseWaveImage.sprite = EconomyHUD.RoundedSprite;
        trackPulseWaveImage.type = Image.Type.Sliced;
        trackPulseWaveImage.color = new Color(1f, 1f, 0.85f, 0.65f);
        trackPulseWaveImage.raycastTarget = false;

        // 3. Xây dựng 4 Cột mốc hiển thị tiền xu (CHỈ HIỂN THỊ, KHÔNG THAO TÁC BẤM)
        for (int i = 0; i < 4; i++)
        {
            float nodeX = trackNodePositionsX[i];

            // Nhãn chỉ số mốc ở trên: 1, 2, 3, 4 (Chuẩn chữ rõ ràng, không dùng ký tự unicode lỗi)
            GameObject tagObj = new GameObject($"MilestoneTag_{i}", typeof(RectTransform), typeof(TextMeshProUGUI));
            tagObj.transform.SetParent(bottomGroup.transform, false);
            RectTransform tagRect = tagObj.GetComponent<RectTransform>();
            tagRect.anchoredPosition = new Vector2(nodeX, 26f);
            tagRect.sizeDelta = new Vector2(40f, 16f);

            TextMeshProUGUI tagTxt = tagObj.GetComponent<TextMeshProUGUI>();
            tagTxt.text = (i + 1).ToString();
            tagTxt.alignment = TextAlignmentOptions.Center;
            tagTxt.fontSize = 12.5f;
            tagTxt.fontStyle = FontStyles.Bold;
            tagTxt.color = new Color(0.25f, 0.35f, 0.48f, 1f);
            tagTxt.raycastTarget = false;

            // Hộp mốc phần thưởng nền sáng bo góc viền vàng/xanh thanh lịch (Kích thước 42 x 42px, KHÔNG CÒN viền đen thô)
            GameObject nodeObj = new GameObject($"MilestoneNode_{i}", typeof(RectTransform), typeof(Image));
            nodeObj.transform.SetParent(bottomGroup.transform, false);
            RectTransform nodeRect = nodeObj.GetComponent<RectTransform>();
            nodeRect.anchoredPosition = new Vector2(nodeX, 0f);
            nodeRect.sizeDelta = new Vector2(42f, 42f);

            Image nodeBg = nodeObj.GetComponent<Image>();
            nodeBg.sprite = EconomyHUD.RoundedSprite;
            nodeBg.type = Image.Type.Sliced;
            nodeBg.color = Color.white;
            nodeBg.raycastTarget = false;
            trackMilestoneBgs[i] = nodeBg;

            Image borderImg = EconomyHUD.AddBlueBorder(nodeRect, new Color(0.08f, 0.58f, 0.95f, 0.85f));
            trackMilestoneBorders[i] = borderImg;

            // Icon Lumi Coin
            GameObject coinIconObj = new GameObject("CoinIcon", typeof(RectTransform), typeof(Image));
            coinIconObj.transform.SetParent(nodeObj.transform, false);
            RectTransform coinRect = coinIconObj.GetComponent<RectTransform>();
            coinRect.anchoredPosition = new Vector2(0f, 6f);
            coinRect.sizeDelta = new Vector2(21f, 21f);

            Image coinImg = coinIconObj.GetComponent<Image>();
            coinImg.sprite = EconomyHUD.CoinSprite;
            coinImg.preserveAspect = true;
            coinImg.raycastTarget = false;

            // Số tiền thưởng bên dưới
            GameObject coinTxtObj = new GameObject("Amount", typeof(RectTransform), typeof(TextMeshProUGUI));
            coinTxtObj.transform.SetParent(nodeObj.transform, false);
            RectTransform coinTxtRect = coinTxtObj.GetComponent<RectTransform>();
            coinTxtRect.anchoredPosition = new Vector2(0f, -11f);
            coinTxtRect.sizeDelta = new Vector2(40f, 14f);

            TextMeshProUGUI coinTxt = coinTxtObj.GetComponent<TextMeshProUGUI>();
            coinTxt.text = trackRewardCoins[i].ToString();
            coinTxt.alignment = TextAlignmentOptions.Center;
            coinTxt.fontSize = 10.5f;
            coinTxt.fontStyle = FontStyles.Bold;
            coinTxt.color = new Color(0.85f, 0.55f, 0.05f, 1f);
            coinTxt.raycastTarget = false;
            trackMilestoneCoins[i] = coinTxt;

            // Checkmark overlay với icon tick chuẩn đồ họa (từ icon_check.png) khi đã đạt mốc
            GameObject nodeCheckObj = new GameObject("CheckOverlay", typeof(RectTransform), typeof(Image));
            nodeCheckObj.transform.SetParent(nodeObj.transform, false);
            RectTransform nodeCheckRect = nodeCheckObj.GetComponent<RectTransform>();
            EconomyHUD.Stretch(nodeCheckRect);

            Image nodeCheckBg = nodeCheckObj.GetComponent<Image>();
            nodeCheckBg.sprite = EconomyHUD.RoundedSprite;
            nodeCheckBg.type = Image.Type.Sliced;
            nodeCheckBg.color = new Color(0.96f, 1f, 0.98f, 0.98f);
            nodeCheckBg.raycastTarget = false;

            GameObject nodeCheckIconObj = new GameObject("TickIcon", typeof(RectTransform), typeof(Image));
            nodeCheckIconObj.transform.SetParent(nodeCheckObj.transform, false);
            RectTransform tickRect = nodeCheckIconObj.GetComponent<RectTransform>();
            tickRect.anchoredPosition = Vector2.zero;
            tickRect.sizeDelta = new Vector2(26f, 26f);

            Image tickImg = nodeCheckIconObj.GetComponent<Image>();
            tickImg.sprite = checkSprite;
            tickImg.preserveAspect = true;
            tickImg.raycastTarget = false;

            trackMilestoneChecks[i] = nodeCheckObj;
            nodeCheckObj.SetActive(false);
        }
    }

    // =========================================================
    // HUD ACHIEVEMENT BUTTON ATTACHER (WHITE CIRCLE + BLUE BORDER)
    // =========================================================

    private void EnsureHUDButton()
    {
        if (hudButtonObj != null) return;

        // Tìm nút WarehouseButton (Kho tài nguyên) trên HUD
        GameObject anchorBtn = GameObject.Find("WarehouseButton");
        if (anchorBtn == null) anchorBtn = GameObject.Find("HabitatButton");
        if (anchorBtn == null)
        {
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);
            foreach (var b in buttons)
            {
                if (b != null && (b.gameObject.name == "WarehouseButton" || b.gameObject.name == "HabitatButton"))
                {
                    anchorBtn = b.gameObject;
                    break;
                }
            }
        }
        if (anchorBtn == null) return;

        RectTransform anchorRect = anchorBtn.GetComponent<RectTransform>();
        if (anchorRect == null || anchorBtn.transform.parent == null) return;

        // Tạo nút Thành Tựu nằm ngay kế bên nút Kho tài nguyên
        hudButtonObj = new GameObject("AchievementHUDButton", typeof(RectTransform), typeof(Image), typeof(Button));
        hudButtonObj.transform.SetParent(anchorBtn.transform.parent, false);

        hudButtonRect = hudButtonObj.GetComponent<RectTransform>();
        hudButtonRect.anchorMin = anchorRect.anchorMin;
        hudButtonRect.anchorMax = anchorRect.anchorMax;
        hudButtonRect.pivot = anchorRect.pivot;

        Vector2 pos = anchorRect.anchoredPosition;
        pos.x += anchorRect.sizeDelta.x + 50f;
        hudButtonRect.anchoredPosition = pos;
        hudButtonRect.sizeDelta = new Vector2(52f, 52f);

        // Bọc nút trong ô tròn trắng viền xanh đồng bộ hoàn toàn với UI hiện tại đã thống nhất
        Sprite circleSprite = anchorBtn.GetComponent<Image>()?.sprite ?? EconomyHUD.RoundedSprite;
        Image btnImg = hudButtonObj.GetComponent<Image>();
        btnImg.sprite = circleSprite;
        btnImg.type = Image.Type.Simple;
        btnImg.color = Color.white;
        btnImg.raycastTarget = true;

        EconomyHUD.AddBlueBorder(hudButtonRect, new Color(0.08f, 0.58f, 0.95f, 0.95f));

        // Biểu tượng lục giác cúp vàng nằm gọn gàng bên trong ô tròn trắng
        GameObject iconObj = new GameObject("BadgeIcon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(hudButtonObj.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(36f, 36f);

        Sprite badgeSprite = Resources.Load<Sprite>("UI_Panels/AchievementBadge") ?? Resources.Load<Sprite>("thanhtuu/danhhieu_tienvang");
        Image iconImg = iconObj.GetComponent<Image>();
        iconImg.sprite = badgeSprite;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        Button btn = hudButtonObj.GetComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.interactable = true;
        btn.onClick.AddListener(ToggleWindow);

        // Nhãn caption chữ "Achievements" nằm dưới nút, căn giữa, in đậm rõ ràng chuyên nghiệp
        GameObject capObj = new GameObject("AchievementCaption", typeof(RectTransform), typeof(TextMeshProUGUI));
        capObj.transform.SetParent(anchorBtn.transform.parent, false);
        RectTransform capRect = capObj.GetComponent<RectTransform>();
        capRect.anchorMin = anchorRect.anchorMin;
        capRect.anchorMax = anchorRect.anchorMax;
        capRect.pivot = anchorRect.pivot;
        capRect.anchoredPosition = new Vector2(pos.x - 22f, pos.y - anchorRect.sizeDelta.y - 4f);
        capRect.sizeDelta = new Vector2(96f, 22f);

        var capText = capObj.GetComponent<TextMeshProUGUI>();
        var anchorCap = anchorBtn.transform.parent.Find("WarehouseCaption")?.GetComponent<TextMeshProUGUI>();
        if (anchorCap != null)
        {
            capText.font = anchorCap.font;
        }
        capText.fontSize = 12.5f;
        capText.fontStyle = FontStyles.Bold;
        capText.color = new Color(0.06f, 0.12f, 0.20f, 1f);
        capText.alignment = TextAlignmentOptions.Center;
        capText.overflowMode = TextOverflowModes.Overflow;
        capText.textWrappingMode = TextWrappingModes.NoWrap;
        capText.text = "Achievements";
        capText.raycastTarget = false;

        // Flash Glow Overlay cho HUD Button khi có mốc sẵn sàng nhận
        GameObject glowObj = new GameObject("GlowOverlay", typeof(RectTransform), typeof(Image));
        glowObj.transform.SetParent(hudButtonObj.transform, false);
        RectTransform glowRect = glowObj.GetComponent<RectTransform>();
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.sizeDelta = Vector2.zero;

        hudGlowOverlay = glowObj.GetComponent<Image>();
        hudGlowOverlay.sprite = circleSprite;
        hudGlowOverlay.color = Color.clear;
        hudGlowOverlay.raycastTarget = false;

        DialogButtonJuice juice = hudButtonObj.AddComponent<DialogButtonJuice>();
        juice.Initialize(hudGlowOverlay, new Color(0.08f, 0.58f, 0.95f, 0.60f));

        Debug.Log("[AchievementUI] Successfully created and attached Achievement HUD Button with white circular frame and blue border!");
    }

    private void UpdateHUDButtonFlashing()
    {
        if (hudButtonObj == null || AchievementManager.Instance == null) return;

        bool hasUnclaimed = AchievementManager.Instance.HasAnyUnclaimedMilestone();

        if (hasUnclaimed)
        {
            float t = Time.unscaledTime * 4.5f;
            float pulse = Mathf.PingPong(t, 1f);
            float scale = 1f + pulse * 0.12f;
            hudButtonRect.localScale = new Vector3(scale, scale, 1f);

            if (hudGlowOverlay != null)
            {
                hudGlowOverlay.color = new Color(0.08f, 0.58f, 0.95f, Mathf.Lerp(0.1f, 0.65f, pulse));
            }
            isHudFlashing = true;
        }
        else if (isHudFlashing)
        {
            hudButtonRect.localScale = Vector3.one;
            if (hudGlowOverlay != null) hudGlowOverlay.color = Color.clear;
            isHudFlashing = false;
        }
    }

    private void UpdateCyberTrackFX()
    {
        if (windowModal == null || !windowModal.gameObject.activeSelf) return;

        float t = Time.unscaledTime;

        if (trackBloomImage != null)
        {
            float bloomAlpha = 0.35f + 0.15f * Mathf.Sin(t * 3.5f);
            trackBloomImage.color = new Color(0.04f, 0.85f, 1f, bloomAlpha);
        }

        if (trackPulseWaveRect != null && trackFillRect != null)
        {
            float currentWidth = trackFillRect.sizeDelta.x;
            if (currentWidth > 35f)
            {
                trackPulseWaveRect.gameObject.SetActive(true);
                float waveT = (t * 1.3f) % 1f;
                float waveX = waveT * currentWidth;
                trackPulseWaveRect.anchoredPosition = new Vector2(waveX, 0f);
                float edgeFade = Mathf.Sin(waveT * Mathf.PI);
                trackPulseWaveImage.color = new Color(1f, 1f, 0.85f, 0.65f * edgeFade);
            }
            else
            {
                trackPulseWaveRect.gameObject.SetActive(false);
            }
        }
    }

    // =========================================================
    // DATA REFRESH & PROGRESS BAR
    // =========================================================

    public void RefreshData()
    {
        if (AchievementManager.Instance == null) return;

        var milestones = AchievementManager.Instance.Milestones;
        if (milestones == null || milestones.Count < 4) return;

        // Cập nhật 4 hàng thành tựu
        for (int i = 0; i < 4; i++)
        {
            var m = milestones[i];

            if (titleTexts[i] != null) titleTexts[i].text = m.title;
            if (descTexts[i] != null) descTexts[i].text = m.shortCondition;

            // Dòng phần thưởng thanh lịch
            if (rewardTexts[i] != null)
            {
                string bonus = i == 0 ? "Care Food" : (i == 1 ? "Terrain Card" : (i == 2 ? "Supply Pack" : "Champion Title"));
                rewardTexts[i].text = $"<color=#D97706><b>+{m.rewardCoins} Coins</b></color>   ·   <color=#0284C7><b>+{m.rewardHarmony} Harmony</b></color>   ·   <color=#059669><b>{bonus}</b></color>";
            }

            // 1. Cập nhật Icon Hexagon
            if (m.isCompleted)
            {
                nodeImages[i].color = Color.white;
                lockOverlays[i].SetActive(false);
            }
            else
            {
                nodeImages[i].color = new Color(0.40f, 0.46f, 0.46f, 0.80f);
                lockOverlays[i].SetActive(true);
            }

            // 2. Huy hiệu checkmark khi đã nhận thưởng
            if (checkBadges[i] != null)
            {
                checkBadges[i].SetActive(m.isClaimed);
            }

            // 3. Nút Claim (Claim / Claimed + Tick Icon / In Progress)
            if (m.isClaimed)
            {
                claimButtons[i].interactable = false;
                claimButtonImages[i].color = new Color(0.24f, 0.28f, 0.36f, 0.65f);
                claimButtonLabels[i].text = "Claimed";
                claimButtonLabels[i].rectTransform.anchoredPosition = new Vector2(10f, 0f);
                claimButtonLabels[i].color = new Color(0.68f, 0.74f, 0.84f, 0.90f);
                if (claimCheckIcons[i] != null) claimCheckIcons[i].gameObject.SetActive(true);
            }
            else if (m.isCompleted)
            {
                claimButtons[i].interactable = true;
                claimButtonImages[i].color = Color.white;
                claimButtonLabels[i].text = "Claim";
                claimButtonLabels[i].rectTransform.anchoredPosition = Vector2.zero;
                claimButtonLabels[i].color = Color.white;
                if (claimCheckIcons[i] != null) claimCheckIcons[i].gameObject.SetActive(false);
            }
            else
            {
                claimButtons[i].interactable = true;
                claimButtonImages[i].color = new Color(0.32f, 0.36f, 0.44f, 0.75f);
                int pct = Mathf.RoundToInt(m.currentProgress * 100);
                claimButtonLabels[i].text = pct > 0 ? $"{pct}%" : "In Progress";
                claimButtonLabels[i].rectTransform.anchoredPosition = Vector2.zero;
                claimButtonLabels[i].color = new Color(0.78f, 0.82f, 0.88f, 0.90f);
                if (claimCheckIcons[i] != null) claimCheckIcons[i].gameObject.SetActive(false);
            }
        }

        // Cập nhật Thanh Tiến Trình Dưới
        int claimedCount = GetClaimedCount();
        if (progressBadgeText != null)
        {
            progressBadgeText.text = $"{claimedCount}/4";
        }

        float targetRatio = claimedCount / 4f;
        if (trackAnimRoutine != null) StopCoroutine(trackAnimRoutine);
        trackAnimRoutine = StartCoroutine(AnimateTrackFill(targetRatio));

        // Tự động kiểm tra và tặng thưởng mốc thanh tiến trình khi người chơi đạt mốc qua Claim
        CheckAutoAwardTrackMilestones(claimedCount);

        // Cập nhật 4 Cột Mốc Tiền Xu trên thanh tiến trình (Hiển thị icon tick chuẩn khi đã đạt mốc)
        for (int i = 0; i < 4; i++)
        {
            bool reached = claimedCount >= (i + 1);

            if (reached)
            {
                trackMilestoneChecks[i].SetActive(true); // Hiện icon tick
                trackMilestoneBorders[i].color = new Color(0.05f, 0.72f, 0.42f, 0.95f);
                trackMilestoneBgs[i].color = new Color(0.95f, 0.99f, 0.96f, 1f);
                trackMilestoneCoins[i].color = new Color(0.04f, 0.50f, 0.32f, 1f);
            }
            else
            {
                trackMilestoneChecks[i].SetActive(false);
                trackMilestoneBorders[i].color = new Color(0.08f, 0.58f, 0.95f, 0.75f);
                trackMilestoneBgs[i].color = Color.white;
                trackMilestoneCoins[i].color = new Color(0.85f, 0.55f, 0.05f, 1f);
            }
        }
    }

    private void CheckAutoAwardTrackMilestones(int claimedCount)
    {
        for (int i = 0; i < 4; i++)
        {
            if (claimedCount >= (i + 1) && !trackMilestonesClaimed[i])
            {
                trackMilestonesClaimed[i] = true;
                PlayerPrefs.SetInt("ACH_TRACK_CLAIMED_" + i, 1);
                PlayerPrefs.Save();

                int bonus = trackRewardCoins[i];
                if (CurrencyWallet.Instance != null)
                {
                    CurrencyWallet.Instance.Add(bonus, CoinReason.Milestone);
                }
                HexPlacementPreviewHUD.ShowFeedbackToast($"Milestone {i + 1} reached: +{bonus} bonus Coins!");
            }
        }
    }

    private int GetClaimedCount()
    {
        if (AchievementManager.Instance == null) return 0;
        int count = 0;
        foreach (var m in AchievementManager.Instance.Milestones)
        {
            if (m.isClaimed) count++;
        }
        return count;
    }

    private IEnumerator AnimateTrackFill(float targetRatio)
    {
        float startRatio = currentAnimRatio;
        float elapsed = 0f;
        const float duration = 0.45f;
        const float totalTrackLength = 630f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            currentAnimRatio = Mathf.Lerp(startRatio, targetRatio, t);
            float width = currentAnimRatio * totalTrackLength;
            if (trackFillRect != null) trackFillRect.sizeDelta = new Vector2(width, 8f);
            if (trackBloomRect != null) trackBloomRect.sizeDelta = new Vector2(width, 18f);
            yield return null;
        }

        currentAnimRatio = targetRatio;
        float finalWidth = currentAnimRatio * totalTrackLength;
        if (trackFillRect != null) trackFillRect.sizeDelta = new Vector2(finalWidth, 8f);
        if (trackBloomRect != null) trackBloomRect.sizeDelta = new Vector2(finalWidth, 18f);
        trackAnimRoutine = null;
    }

    // =========================================================
    // ACTIONS & NOTIFICATIONS
    // =========================================================

    private void OnClaimClicked(int index)
    {
        if (AchievementManager.Instance == null) return;
        var milestones = AchievementManager.Instance.Milestones;
        if (index < 0 || index >= milestones.Count) return;

        var m = milestones[index];
        if (m.isClaimed) return;

        if (!m.isCompleted)
        {
            GameDialogUI.ShowNotice($"You haven't completed this achievement yet!\n\nProgress: {Mathf.RoundToInt(m.currentProgress * 100)}%\nClick the card row to view requirements!", null, "REQUIREMENTS NOT MET");
            return;
        }

        if (AchievementManager.Instance.ClaimMilestone(index, out string rewardInfo))
        {
            HexPlacementPreviewHUD.ShowFeedbackToast($"Claimed achievement: {rewardInfo}");
            RefreshData();
        }
        else
        {
            GameDialogUI.ShowNotice(rewardInfo, null, "NOTICE");
        }
    }

    private void OnDetailClicked(int index)
    {
        if (AchievementManager.Instance == null) return;
        var milestones = AchievementManager.Instance.Milestones;
        if (index < 0 || index >= milestones.Count) return;

        var m = milestones[index];

        string statusBadge = m.isClaimed
            ? "<color=#4B5563><b>[ Reward Claimed ]</b></color>"
            : (m.isCompleted
                ? "<color=#059669><b>[ Ready to Claim! ]</b></color>"
                : $"<color=#D97706><b>[ In Progress: {Mathf.RoundToInt(m.currentProgress * 100)}% ]</b></color>");

        string msg = $"<b>Task:</b> {m.description}\n\n" +
                     $"<b>Status:</b> {statusBadge}\n" +
                     $"<b>Reward:</b> +{m.rewardCoins} Coins, +{m.rewardHarmony} Harmony, {m.rewardBonusInfo}";

        GameDialogUI.ShowNotice(msg, null, $"ACHIEVEMENT {index + 1}: {m.title.ToUpper()}");
    }

    public void ToggleWindow()
    {
        if (windowModal == null) return;
        if (windowModal.gameObject.activeSelf)
        {
            CloseWindow();
        }
        else
        {
            OpenWindow();
        }
    }

    public void OpenWindow()
    {
        if (windowModal == null) return;
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.EvaluateMilestones();
        }

        // Tự động scale panel nhẹ nhàng nếu màn hình bé hơn
        Rect area = ((RectTransform)transform).rect;
        float scale = area.width > 0f && area.height > 0f
            ? Mathf.Clamp(Mathf.Min((area.width - 32f) / PanelWidth, (area.height - 32f) / PanelHeight), 0.5f, 1f)
            : 1f;
        panelRect.localScale = new Vector3(scale, scale, 1f);

        RefreshData();
        windowModal.gameObject.SetActive(true);
        windowModal.SetAsLastSibling();
        EconomyHUD.BringWindowToFront(windowModal.gameObject);
    }

    public void CloseWindow()
    {
        if (windowModal != null)
        {
            windowModal.gameObject.SetActive(false);
            EconomyHUD.MarkWindowClosed();
        }
    }
}
