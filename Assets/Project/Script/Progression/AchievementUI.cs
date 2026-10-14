using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bảng Tiến Trình Hệ Sinh Thái Đảo (Achievement & Progression Modal).
/// Chuẩn xác theo thiết kế GDD & mockup mỹ thuật:
/// - Background nghệ thuật 3000x1406 (thanhtuu/Backgorund).
/// - Thanh tiến trình nằm ngang nối liền 4 cột mốc từ trái sang phải.
/// - 4 Cột mốc với Hexagon Icon, hiển thị ổ khóa khi chưa hoàn thành và mở sáng khi đạt điều kiện.
/// - Nút "NHẬN" (thanhtuu/nhan) sáng rực khi đạt điều kiện, khi nhận xong sẽ khóa lại và hiện chữ "ĐÃ NHẬN".
/// - Nút "CHI TIẾT" (thanhtuu/chitiet) mở Panel Thông Báo (GameDialogUI.ShowNotice) mô tả chi tiết nhiệm vụ và phần thưởng.
/// - Nút Thành Tựu trên HUD (UI_Panels/AchievementBadge) nằm cạnh nút Habitat, tự động nhấp nháy liên tục khi có mốc hoàn thành sẵn sàng nhận thưởng.
/// </summary>
public class AchievementUI : MonoBehaviour
{
    private static AchievementUI instance;
    public static AchievementUI Instance => instance;

    [Header("UI Structure")]
    private RectTransform windowModal;
    private RectTransform panelRect;

    // High-Tech Cyber Progress Track Components
    private RectTransform trackFillRect;
    private Image trackFillImage;
    private RectTransform trackBloomRect;
    private Image trackBloomImage;
    private RectTransform trackLaserRect;
    private RectTransform trackPulseWaveRect;
    private Image trackPulseWaveImage;
    private RectTransform trackHeadRect;
    private RectTransform trackHeadHaloRect;

    // 4 Cột mốc (Tọa độ X chuẩn tâm khớp chính xác từng cột mốc và ô phần thưởng)
    private readonly float[] columnPositionsX = new float[] { -368.7f, -123.6f, +122.7f, +368.7f };
    private readonly string[] milestoneIconNames = new string[]
    {
        "thanhtuu/moitruongsongdautien",
        "thanhtuu/loaimoi",
        "thanhtuu/lencap",
        "thanhtuu/cauchuyendao"
    };

    private Image[] nodeImages = new Image[4];
    private GameObject[] lockOverlays = new GameObject[4];

    private Button[] claimButtons = new Button[4];
    private Image[] claimButtonImages = new Image[4];
    private TextMeshProUGUI[] claimButtonLabels = new TextMeshProUGUI[4];

    private Button[] detailButtons = new Button[4];

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
        // 1. Kiểm tra và định vị Nút Thành Tựu trên HUD cạnh nút Habitat
        EnsureHUDButton();

        // 2. Cập nhật hiệu ứng nhấp nháy trên HUD Button khi có mốc hoàn thành chưa nhận
        UpdateHUDButtonFlashing();

        // 3. Cập nhật hiệu ứng thanh tiến trình công nghệ năng lượng Cyber FX
        UpdateCyberTrackFX();
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
        dimmerImg.color = new Color(0.04f, 0.08f, 0.10f, 0.70f);

        Button dismissBtn = modalObj.AddComponent<Button>();
        dismissBtn.onClick.AddListener(CloseWindow);

        // 2. Main Panel (Kích thước 960 x 450 chuẩn tỉ lệ 2.133 của ảnh background 3000x1406)
        GameObject panelObj = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(windowModal, false);
        panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(960f, 443f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);

        Image panelImg = panelObj.GetComponent<Image>();
        Sprite bgSprite = Resources.Load<Sprite>("thanhtuu/Backgorund") ?? Resources.Load<Sprite>("thanhtuu/Background");
        panelImg.sprite = bgSprite;
        panelImg.type = Image.Type.Simple;
        panelImg.preserveAspect = true;
        panelImg.raycastTarget = true;

        // Nút Đóng X ở góc trên bên phải
        GameObject closeObj = new GameObject("CloseBtn", typeof(RectTransform), typeof(Button), typeof(Image));
        closeObj.transform.SetParent(panelRect, false);
        RectTransform closeRect = closeObj.GetComponent<RectTransform>();
        closeRect.anchoredPosition = new Vector2(445f, 195f);
        closeRect.sizeDelta = new Vector2(34f, 34f);

        Image closeImg = closeObj.GetComponent<Image>();
        closeImg.sprite = EconomyHUD.RoundedSprite;
        closeImg.type = Image.Type.Sliced;
        closeImg.color = new Color(0.12f, 0.18f, 0.15f, 0.85f);

        Button closeBtn = closeObj.GetComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(CloseWindow);

        GameObject closeTxtObj = new GameObject("X", typeof(RectTransform), typeof(TextMeshProUGUI));
        closeTxtObj.transform.SetParent(closeRect, false);
        TextMeshProUGUI closeTxt = closeTxtObj.GetComponent<TextMeshProUGUI>();
        closeTxt.text = "✕";
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.fontSize = 20f;
        closeTxt.fontStyle = FontStyles.Bold;
        closeTxt.color = new Color(0.85f, 0.95f, 0.90f, 1f);

        DialogButtonJuice closeJuice = closeObj.AddComponent<DialogButtonJuice>();
        closeJuice.Initialize(null, new Color(0.04f, 0.08f, 0.04f, 0.32f));

        // 3. High-Tech Cyber Energy Progress Track
        // A. Nền rãnh kim loại tối màu công nghệ (Dark Cyber Conduit Groove)
        GameObject trackBgObj = new GameObject("TrackCyberGroove", typeof(RectTransform), typeof(Image));
        trackBgObj.transform.SetParent(panelRect, false);
        RectTransform trackBgRect = trackBgObj.GetComponent<RectTransform>();
        trackBgRect.anchorMin = new Vector2(0.5f, 0.5f);
        trackBgRect.anchorMax = new Vector2(0.5f, 0.5f);
        trackBgRect.pivot = new Vector2(0.5f, 0.5f);
        trackBgRect.anchoredPosition = new Vector2(0f, 61f);
        trackBgRect.sizeDelta = new Vector2(738f, 14f);

        Image trackBgImg = trackBgObj.GetComponent<Image>();
        trackBgImg.sprite = EconomyHUD.RoundedSprite;
        trackBgImg.type = Image.Type.Sliced;
        trackBgImg.color = new Color(0.02f, 0.07f, 0.09f, 0.85f);
        trackBgImg.raycastTarget = false;

        // B. Outer Neon Bloom Aura (Vầng phát sáng Neon bao ngoài)
        GameObject bloomObj = new GameObject("TrackBloom", typeof(RectTransform), typeof(Image));
        bloomObj.transform.SetParent(panelRect, false);
        trackBloomRect = bloomObj.GetComponent<RectTransform>();
        trackBloomRect.pivot = new Vector2(0f, 0.5f);
        trackBloomRect.anchorMin = new Vector2(0.5f, 0.5f);
        trackBloomRect.anchorMax = new Vector2(0.5f, 0.5f);
        trackBloomRect.anchoredPosition = new Vector2(-368.7f, 61f);
        trackBloomRect.sizeDelta = new Vector2(0f, 22f);

        trackBloomImage = bloomObj.GetComponent<Image>();
        trackBloomImage.sprite = EconomyHUD.RoundedSprite;
        trackBloomImage.type = Image.Type.Sliced;
        trackBloomImage.color = new Color(0.04f, 0.85f, 1f, 0.40f); // Electric Cyan
        trackBloomImage.raycastTarget = false;

        // C. Core Plasma Energy Beam (Chùm năng lượng chính)
        GameObject trackFillObj = new GameObject("TrackBeam", typeof(RectTransform), typeof(Image));
        trackFillObj.transform.SetParent(panelRect, false);
        trackFillRect = trackFillObj.GetComponent<RectTransform>();
        trackFillRect.pivot = new Vector2(0f, 0.5f);
        trackFillRect.anchorMin = new Vector2(0.5f, 0.5f);
        trackFillRect.anchorMax = new Vector2(0.5f, 0.5f);
        trackFillRect.anchoredPosition = new Vector2(-368.7f, 61f);
        trackFillRect.sizeDelta = new Vector2(0f, 11f);

        trackFillImage = trackFillObj.GetComponent<Image>();
        trackFillImage.sprite = EconomyHUD.RoundedSprite;
        trackFillImage.type = Image.Type.Sliced;
        trackFillImage.color = new Color(0.12f, 0.98f, 0.82f, 0.95f); // Neon Cyan / Emerald
        trackFillImage.raycastTarget = false;

        // D. Laser Filament Core (Tia laser lõi siêu sáng giữa dòng năng lượng)
        GameObject laserObj = new GameObject("TrackLaserCore", typeof(RectTransform), typeof(Image));
        laserObj.transform.SetParent(trackFillObj.transform, false);
        trackLaserRect = laserObj.GetComponent<RectTransform>();
        trackLaserRect.anchorMin = new Vector2(0f, 0.5f);
        trackLaserRect.anchorMax = new Vector2(1f, 0.5f);
        trackLaserRect.pivot = new Vector2(0.5f, 0.5f);
        trackLaserRect.anchoredPosition = Vector2.zero;
        trackLaserRect.sizeDelta = new Vector2(0f, 3f);

        Image laserImg = laserObj.GetComponent<Image>();
        laserImg.sprite = EconomyHUD.RoundedSprite;
        laserImg.type = Image.Type.Sliced;
        laserImg.color = new Color(0.95f, 1f, 1f, 0.95f); // Trắng sáng photon
        laserImg.raycastTarget = false;

        // E. Cyber Energy Pulse Wave (Sóng xung điện năng lượng chạy dọc thanh)
        GameObject waveObj = new GameObject("TrackPulseWave", typeof(RectTransform), typeof(Image));
        waveObj.transform.SetParent(trackFillObj.transform, false);
        trackPulseWaveRect = waveObj.GetComponent<RectTransform>();
        trackPulseWaveRect.pivot = new Vector2(0.5f, 0.5f);
        trackPulseWaveRect.anchorMin = new Vector2(0f, 0.5f);
        trackPulseWaveRect.anchorMax = new Vector2(0f, 0.5f);
        trackPulseWaveRect.anchoredPosition = Vector2.zero;
        trackPulseWaveRect.sizeDelta = new Vector2(48f, 14f);

        trackPulseWaveImage = waveObj.GetComponent<Image>();
        trackPulseWaveImage.sprite = EconomyHUD.RoundedSprite;
        trackPulseWaveImage.type = Image.Type.Sliced;
        trackPulseWaveImage.color = new Color(1f, 1f, 0.85f, 0.65f);
        trackPulseWaveImage.raycastTarget = false;

        // F. Leading Cyber Energy Head (Đầu hạt năng lượng công nghệ cao)
        GameObject headObj = new GameObject("CyberEnergyHead", typeof(RectTransform));
        headObj.transform.SetParent(trackFillObj.transform, false);
        trackHeadRect = headObj.GetComponent<RectTransform>();
        trackHeadRect.anchorMin = new Vector2(1f, 0.5f);
        trackHeadRect.anchorMax = new Vector2(1f, 0.5f);
        trackHeadRect.pivot = new Vector2(0.5f, 0.5f);
        trackHeadRect.anchoredPosition = Vector2.zero;
        trackHeadRect.sizeDelta = new Vector2(34f, 34f);

        // Vòng hào quang đầu hạt
        GameObject headHaloObj = new GameObject("Halo", typeof(RectTransform), typeof(Image));
        headHaloObj.transform.SetParent(headObj.transform, false);
        trackHeadHaloRect = headHaloObj.GetComponent<RectTransform>();
        trackHeadHaloRect.anchoredPosition = Vector2.zero;
        trackHeadHaloRect.sizeDelta = new Vector2(34f, 34f);
        Image headHaloImg = headHaloObj.GetComponent<Image>();
        headHaloImg.sprite = EconomyHUD.RoundedSprite;
        headHaloImg.color = new Color(0.1f, 0.95f, 1f, 0.55f);
        headHaloImg.raycastTarget = false;

        // Khối Diamond Cyber xoay 45 độ
        GameObject headDiamondObj = new GameObject("Diamond", typeof(RectTransform), typeof(Image));
        headDiamondObj.transform.SetParent(headObj.transform, false);
        RectTransform diamondRect = headDiamondObj.GetComponent<RectTransform>();
        diamondRect.anchoredPosition = Vector2.zero;
        diamondRect.sizeDelta = new Vector2(16f, 16f);
        diamondRect.localEulerAngles = new Vector3(0f, 0f, 45f);
        Image diamondImg = headDiamondObj.GetComponent<Image>();
        diamondImg.sprite = EconomyHUD.RoundedSprite;
        diamondImg.color = new Color(0.40f, 1f, 0.88f, 0.95f);
        diamondImg.raycastTarget = false;

        // Điểm sáng photon lõi
        GameObject headCoreObj = new GameObject("Core", typeof(RectTransform), typeof(Image));
        headCoreObj.transform.SetParent(headObj.transform, false);
        RectTransform coreRect = headCoreObj.GetComponent<RectTransform>();
        coreRect.anchoredPosition = Vector2.zero;
        coreRect.sizeDelta = new Vector2(7f, 7f);
        Image coreImg = headCoreObj.GetComponent<Image>();
        coreImg.sprite = EconomyHUD.RoundedSprite;
        coreImg.color = Color.white;
        coreImg.raycastTarget = false;

        // 4. Tạo 4 Cột Mốc Thành Tựu
        Sprite lockSprite = Resources.Load<Sprite>("PreserveLockIcon");
        Sprite nhanSprite = Resources.Load<Sprite>("thanhtuu/nhan");
        Sprite chitietSprite = Resources.Load<Sprite>("thanhtuu/chitiet");

        for (int i = 0; i < 4; i++)
        {
            int colIndex = i;
            float posX = columnPositionsX[i];

            // A. Hexagon Node Icon (Y = 61f)
            GameObject nodeObj = new GameObject($"Node_{i}", typeof(RectTransform), typeof(Image));
            nodeObj.transform.SetParent(panelRect, false);
            RectTransform nodeRect = nodeObj.GetComponent<RectTransform>();
            nodeRect.anchoredPosition = new Vector2(posX, 61f);
            nodeRect.sizeDelta = new Vector2(74f, 84f);

            Image nodeImg = nodeObj.GetComponent<Image>();
            Sprite iconSp = Resources.Load<Sprite>(milestoneIconNames[i]);
            nodeImg.sprite = iconSp;
            nodeImg.preserveAspect = true;
            nodeImages[i] = nodeImg;

            // Ổ Khóa đè lên icon khi chưa hoàn thành
            GameObject lockObj = new GameObject("LockOverlay", typeof(RectTransform), typeof(Image));
            lockObj.transform.SetParent(nodeObj.transform, false);
            RectTransform lockRect = lockObj.GetComponent<RectTransform>();
            lockRect.anchoredPosition = Vector2.zero;
            lockRect.sizeDelta = new Vector2(32f, 32f);

            Image lockImg = lockObj.GetComponent<Image>();
            lockImg.sprite = lockSprite;
            lockImg.preserveAspect = true;
            lockImg.color = Color.white;
            lockImg.raycastTarget = false;
            lockOverlays[i] = lockObj;

            // B. Nút "NHẬN" (Y = -94px)
            GameObject nhanBtnObj = new GameObject($"NhanBtn_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
            nhanBtnObj.transform.SetParent(panelRect, false);
            RectTransform nhanRect = nhanBtnObj.GetComponent<RectTransform>();
            nhanRect.anchoredPosition = new Vector2(posX, -94f);
            nhanRect.sizeDelta = new Vector2(98f, 36f);

            Image nhanImg = nhanBtnObj.GetComponent<Image>();
            nhanImg.sprite = nhanSprite;
            nhanImg.preserveAspect = true;
            claimButtonImages[i] = nhanImg;

            Button nhanBtn = nhanBtnObj.GetComponent<Button>();
            nhanBtn.transition = Selectable.Transition.None;
            nhanBtn.targetGraphic = nhanImg;
            nhanBtn.onClick.AddListener(() => OnClaimClicked(colIndex));
            claimButtons[i] = nhanBtn;

            // Nhãn chữ "ĐÃ NHẬN" khi hoàn thành
            GameObject nhanTxtObj = new GameObject("StatusLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            nhanTxtObj.transform.SetParent(nhanBtnObj.transform, false);
            RectTransform nhanTxtRect = nhanTxtObj.GetComponent<RectTransform>();
            nhanTxtRect.anchorMin = Vector2.zero;
            nhanTxtRect.anchorMax = Vector2.one;
            nhanTxtRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI nhanTxt = nhanTxtObj.GetComponent<TextMeshProUGUI>();
            nhanTxt.text = "";
            nhanTxt.alignment = TextAlignmentOptions.Center;
            nhanTxt.verticalAlignment = VerticalAlignmentOptions.Middle;
            nhanTxt.fontSize = 13f;
            nhanTxt.fontStyle = FontStyles.Bold;
            nhanTxt.color = new Color(0.95f, 0.90f, 0.60f, 1f);
            nhanTxt.raycastTarget = false;
            claimButtonLabels[i] = nhanTxt;

            DialogButtonJuice nhanJuice = nhanBtnObj.AddComponent<DialogButtonJuice>();
            nhanJuice.Initialize(null, new Color(0.04f, 0.08f, 0.04f, 0.32f));

            // C. Nút "CHI TIẾT" (Y = -155px)
            GameObject chiTietBtnObj = new GameObject($"ChiTietBtn_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
            chiTietBtnObj.transform.SetParent(panelRect, false);
            RectTransform chiTietRect = chiTietBtnObj.GetComponent<RectTransform>();
            chiTietRect.anchoredPosition = new Vector2(posX, -155f);
            chiTietRect.sizeDelta = new Vector2(140f, 46f);

            Image chiTietImg = chiTietBtnObj.GetComponent<Image>();
            chiTietImg.sprite = chitietSprite;
            chiTietImg.preserveAspect = true;

            Button chiTietBtn = chiTietBtnObj.GetComponent<Button>();
            chiTietBtn.targetGraphic = chiTietImg;
            chiTietBtn.onClick.AddListener(() => OnDetailClicked(colIndex));
            detailButtons[i] = chiTietBtn;

            DialogButtonJuice chiTietJuice = chiTietBtnObj.AddComponent<DialogButtonJuice>();
            chiTietJuice.Initialize(null, new Color(0.95f, 0.75f, 0.25f, 0.4f));
        }

        windowModal.gameObject.SetActive(false);
    }

    // =========================================================
    // HUD ACHIEVEMENT BUTTON ATTACHER
    // =========================================================

    private void EnsureHUDButton()
    {
        if (hudButtonObj != null) return;

        // Tìm nút WarehouseButton (Kho tài nguyên) hoặc HabitatButton trên giao diện chính của game
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

        // Canh tọa độ dời sang phải vừa phải gần nút Kho tài nguyên, giữ khoảng cách thoáng đãng để 2 dòng chữ không chạm nhau
        Vector2 pos = anchorRect.anchoredPosition;
        pos.x += anchorRect.sizeDelta.x + 36f;
        hudButtonRect.anchoredPosition = pos;
        hudButtonRect.sizeDelta = new Vector2(52f, 52f);

        // Biểu tượng lục giác cúp vàng phóng lớn trực tiếp làm nút (Bỏ hoàn toàn vòng tròn trắng bao ngoài)
        Sprite badgeSprite = Resources.Load<Sprite>("UI_Panels/AchievementBadge") ?? Resources.Load<Sprite>("thanhtuu/danhhieu_tienvang");
        Image btnImg = hudButtonObj.GetComponent<Image>();
        btnImg.sprite = badgeSprite;
        btnImg.type = Image.Type.Simple;
        btnImg.preserveAspect = true;
        btnImg.color = Color.white;
        btnImg.raycastTarget = true;

        Button btn = hudButtonObj.GetComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.interactable = true;
        btn.onClick.AddListener(ToggleWindow);

        // Nhãn caption chữ "Thành tựu" nằm dưới nút, căn giữa, in đậm rõ ràng chuyên nghiệp
        GameObject capObj = new GameObject("AchievementCaption", typeof(RectTransform), typeof(TextMeshProUGUI));
        capObj.transform.SetParent(anchorBtn.transform.parent, false);
        RectTransform capRect = capObj.GetComponent<RectTransform>();
        capRect.anchorMin = anchorRect.anchorMin;
        capRect.anchorMax = anchorRect.anchorMax;
        capRect.pivot = anchorRect.pivot;
        // Căn giữa theo tâm nút: tâm nút tại pos.x + 26f. Với width 96, x = pos.x + 26 - 48 = pos.x - 22f
        capRect.anchoredPosition = new Vector2(pos.x - 22f, pos.y - anchorRect.sizeDelta.y - 4f);
        capRect.sizeDelta = new Vector2(96f, 22f);

        var capText = capObj.GetComponent<TextMeshProUGUI>();
        var anchorCap = anchorBtn.transform.parent.Find("WarehouseCaption")?.GetComponent<TextMeshProUGUI>();
        if (anchorCap != null)
        {
            capText.font = anchorCap.font;
        }
        capText.fontSize = 13f;
        capText.fontStyle = FontStyles.Bold;
        capText.color = new Color(0.06f, 0.12f, 0.20f, 1f);
        capText.alignment = TextAlignmentOptions.Center;
        capText.overflowMode = TextOverflowModes.Overflow;
        capText.textWrappingMode = TextWrappingModes.NoWrap;
        capText.text = "Thành tựu";
        capText.raycastTarget = false;

        // Flash Glow Overlay cho HUD Button (ôm sát theo khung lục giác cúp vàng)
        GameObject glowObj = new GameObject("GlowOverlay", typeof(RectTransform), typeof(Image));
        glowObj.transform.SetParent(hudButtonObj.transform, false);
        RectTransform glowRect = glowObj.GetComponent<RectTransform>();
        glowRect.anchorMin = new Vector2(0.5f, 0.5f);
        glowRect.anchorMax = new Vector2(0.5f, 0.5f);
        glowRect.pivot = new Vector2(0.5f, 0.5f);
        glowRect.anchoredPosition = Vector2.zero;
        glowRect.sizeDelta = new Vector2(58f, 58f);

        hudGlowOverlay = glowObj.GetComponent<Image>();
        hudGlowOverlay.sprite = badgeSprite;
        hudGlowOverlay.type = Image.Type.Simple;
        hudGlowOverlay.preserveAspect = true;
        hudGlowOverlay.color = Color.clear;
        hudGlowOverlay.raycastTarget = false;

        DialogButtonJuice juice = hudButtonObj.AddComponent<DialogButtonJuice>();
        juice.Initialize(hudGlowOverlay, new Color(1f, 0.85f, 0.35f, 0.6f));

        Debug.Log("[AchievementUI] Successfully created and attached Achievement HUD Button next to WarehouseButton!");
    }

    private void UpdateHUDButtonFlashing()
    {
        if (hudButtonObj == null || AchievementManager.Instance == null) return;

        bool hasUnclaimed = AchievementManager.Instance.HasAnyUnclaimedMilestone();
        if (hasUnclaimed)
        {
            // Hiệu ứng nhấp nháy liên tục (Continuous scale pulsation & golden glow)
            float t = Time.unscaledTime * 4.5f;
            float pulse = Mathf.PingPong(t, 1f);
            float scale = 1f + pulse * 0.14f;
            hudButtonRect.localScale = new Vector3(scale, scale, 1f);

            if (hudGlowOverlay != null)
            {
                hudGlowOverlay.color = new Color(1f, 0.85f, 0.25f, Mathf.Lerp(0.1f, 0.65f, pulse));
            }
            isHudFlashing = true;
        }
        else if (isHudFlashing)
        {
            // Reset về trạng thái bình thường
            hudButtonRect.localScale = Vector3.one;
            if (hudGlowOverlay != null) hudGlowOverlay.color = Color.clear;
            isHudFlashing = false;
        }
    }

    private void UpdateCyberTrackFX()
    {
        if (windowModal == null || !windowModal.gameObject.activeSelf) return;

        float t = Time.unscaledTime;

        // 1. Nhịp thở phát quang của đầu hạt năng lượng
        if (trackHeadHaloRect != null)
        {
            float pulse = 1f + 0.22f * Mathf.Sin(t * 5f);
            trackHeadHaloRect.localScale = new Vector3(pulse, pulse, 1f);
        }

        // 2. Chùm tia Laser Bloom thở nhẹ nhịp nhàng
        if (trackBloomImage != null)
        {
            float bloomAlpha = 0.35f + 0.15f * Mathf.Sin(t * 3.5f);
            trackBloomImage.color = new Color(0.04f, 0.85f, 1f, bloomAlpha);
        }

        // 3. Xung điện năng lượng công nghệ (Pulse Wave) chạy dọc luồng điện
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

        float targetRatio = AchievementManager.Instance.GetOverallProgressRatio();
        if (trackAnimRoutine != null) StopCoroutine(trackAnimRoutine);
        trackAnimRoutine = StartCoroutine(AnimateTrackFill(targetRatio));

        for (int i = 0; i < 4; i++)
        {
            var m = milestones[i];

            // 1. Cập nhật Icon Hexagon (Sắc nét, không tô loang màu vàng)
            if (m.isCompleted)
            {
                // Mở khóa: Sáng rõ, tắt ổ khóa
                nodeImages[i].color = Color.white;
                lockOverlays[i].SetActive(false);
            }
            else
            {
                // Chưa hoàn thành: Tối màu nhẹ, hiện ổ khóa
                nodeImages[i].color = new Color(0.40f, 0.46f, 0.46f, 0.80f);
                lockOverlays[i].SetActive(true);
            }

            // 2. Cập nhật Nút "NHẬN" (Đồng bộ chuẩn 3 trạng thái: ĐÃ NHẬN / SẴN SÀNG NHẬN / CHƯA ĐẠT)
            if (m.isClaimed)
            {
                // ĐÃ NHẬN: Nút tối màu, hiện chữ "ĐÃ NHẬN"
                claimButtons[i].interactable = false;
                claimButtonImages[i].color = new Color(0.28f, 0.34f, 0.36f, 0.70f);
                claimButtonLabels[i].text = "ĐÃ NHẬN";
                claimButtonLabels[i].color = new Color(0.85f, 0.88f, 0.88f, 0.90f);
            }
            else if (m.isCompleted)
            {
                // SẴN SÀNG NHẬN: Nút vàng sáng rực rỡ, sẵn sàng bấm nhận
                claimButtons[i].interactable = true;
                claimButtonImages[i].color = Color.white;
                claimButtonLabels[i].text = "";
            }
            else
            {
                // CHƯA ĐẠT: Nút tối màu thể hiện trạng thái chưa mở khóa, hiện chữ "CHƯA ĐẠT"
                claimButtons[i].interactable = true;
                claimButtonImages[i].color = new Color(0.26f, 0.32f, 0.34f, 0.60f);
                claimButtonLabels[i].text = "CHƯA ĐẠT";
                claimButtonLabels[i].color = new Color(0.72f, 0.78f, 0.80f, 0.85f);
            }
        }
    }

    private IEnumerator AnimateTrackFill(float targetRatio)
    {
        float startRatio = currentAnimRatio;
        float elapsed = 0f;
        const float duration = 0.45f;
        const float totalTrackLength = 737.4f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            currentAnimRatio = Mathf.Lerp(startRatio, targetRatio, t);
            float width = currentAnimRatio * totalTrackLength;
            trackFillRect.sizeDelta = new Vector2(width, 11f);
            if (trackBloomRect != null) trackBloomRect.sizeDelta = new Vector2(width, 22f);
            yield return null;
        }

        currentAnimRatio = targetRatio;
        float finalWidth = currentAnimRatio * totalTrackLength;
        trackFillRect.sizeDelta = new Vector2(finalWidth, 11f);
        if (trackBloomRect != null) trackBloomRect.sizeDelta = new Vector2(finalWidth, 22f);
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
            GameDialogUI.ShowNotice($"Bạn chưa hoàn thành cột mốc này!\n\nTiến độ: {Mathf.RoundToInt(m.currentProgress * 100)}%\nHãy nhấn nút [CHI TIẾT] để xem nhiệm vụ cần thực hiện nhé!", null, "CHƯA ĐẠT ĐIỀU KIỆN");
            return;
        }

        if (AchievementManager.Instance.ClaimMilestone(index, out string rewardInfo))
        {
            HexPlacementPreviewHUD.ShowFeedbackToast($"Nhận thành tựu: {rewardInfo}");
            RefreshData();
        }
        else
        {
            GameDialogUI.ShowNotice(rewardInfo, null, "THÔNG BÁO");
        }
    }

    private void OnDetailClicked(int index)
    {
        if (AchievementManager.Instance == null) return;
        var milestones = AchievementManager.Instance.Milestones;
        if (index < 0 || index >= milestones.Count) return;

        var m = milestones[index];

        string statusBadge = m.isClaimed
            ? "<color=#4B5563><b>[ Đã nhận phần thưởng ]</b></color>"
            : (m.isCompleted
                ? "<color=#059669><b>[ Sẵn sàng nhận thưởng! ]</b></color>"
                : $"<color=#D97706><b>[ Đang thực hiện: {Mathf.RoundToInt(m.currentProgress * 100)}% ]</b></color>");

        string msg = $"<b>Nhiệm vụ:</b> {m.description}\n" +
                     $"<b>Trạng thái:</b> {statusBadge}\n" +
                     $"<b>Phần thưởng:</b> +{m.rewardCoins} Vàng, +{m.rewardHarmony} Harmony, {m.rewardBonusInfo}";

        GameDialogUI.ShowNotice(msg, null, $"CỘT MỐC {index + 1}: {m.title.ToUpper()}");
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
        RefreshData();
        windowModal.gameObject.SetActive(true);
        windowModal.SetAsLastSibling();
    }

    public void CloseWindow()
    {
        if (windowModal != null)
        {
            windowModal.gameObject.SetActive(false);
        }
    }
}
