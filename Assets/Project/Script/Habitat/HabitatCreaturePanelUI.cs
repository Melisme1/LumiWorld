using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LumiWorld.Acs;

/// <summary>
/// Giao diện Quản lý Sinh vật & Môi trường sống (Habitat Living Creatures & Details Panel).
/// Nâng cấp chuyên nghiệp:
/// 1. Panel 1:
///    - Thanh Năng Lượng (Energy Bar) công nghệ cao thay thế UI oval.
///    - Thanh Chữ Nhật nằm ngang phong cách phù hợp môi trường ở chân panel.
///    - Lưới tổ ong lục giác so le đối xứng (3-2-3-2), nút thu gọn nằm an toàn bên trong mép panel.
/// 2. Panel 2:
///    - Trực tiếp render 3D Model sinh vật xoay nhẹ nhàng trên nền màu hệ môi trường.
///    - Xóa các ô xám cồng kềnh, thay bằng hệ thống Chip / Nút thông số game (Stat Chips).
///    - Thanh cuộn ScrollRect mượt mà, đảm bảo mọi thông tin (Món ăn ưa thích...) nằm gọn gàng bên trong panel.
///    - Bộ 3 nút hành động phong cách 3D Game có độ nảy và bóng đổ chuyên nghiệp.
/// </summary>
public class HabitatCreaturePanelUI : MonoBehaviour
{
    private static HabitatCreaturePanelUI instance;
    public static HabitatCreaturePanelUI Instance => instance;

    [Header("Root Canvas & Windows")]
    private GameObject rootModal;
    private CanvasGroup rootCanvasGroup;

    // Panel 1: Danh sách thú đang sinh sống
    private RectTransform panelListRect;
    private Image panelListBg;
    private TextMeshProUGUI weatherText;
    private TextMeshProUGUI habitatNameText;
    private Slider capacitySlider;
    private Image capacityFillImg;
    private TextMeshProUGUI capacityLabel;
    private Transform creatureGridRoot;
    private TextMeshProUGUI footerSummaryText;
    private TextMeshProUGUI footerScaleText;
    private TextMeshProUGUI footerResidentText;

    // Panel 2: Chi tiết sinh vật
    private RectTransform panelDetailRect;
    private Image panelDetailBg;
    private RawImage creatureModelRawImg;
    private Image creatureArtFallbackImg;
    private TextMeshProUGUI creatureNameText;
    private Button renameButton;
    private TextMeshProUGUI creatureBadgeText;
    private GameObject renameModal;
    private TMP_InputField renameInputField;

    // Scrollable Content trong Panel 2
    private ScrollRect infoScrollRect;
    private TextMeshProUGUI statusBannerTitle;
    private TextMeshProUGUI statusBannerDesc;
    private TextMeshProUGUI chipSpeciesText;
    private TextMeshProUGUI chipRankText;
    private TextMeshProUGUI chipStarsText;
    private TextMeshProUGUI chipTraitText;
    private Slider hungerSlider;
    private TextMeshProUGUI hungerProgressLabel;
    private TextMeshProUGUI favoriteFoodText;
    private TextMeshProUGUI foodLabelText;

    // Bộ 3 nút hành động 3D
    private Button feedButton;
    private TextMeshProUGUI feedButtonText;
    private Button medicineButton;
    private TextMeshProUGUI medicineButtonText;
    private Button rankUpButton;
    private TextMeshProUGUI rankUpButtonText;
    private TextMeshProUGUI feedbackNotifyText;

    // Dữ liệu đang chọn
    private HabitatState currentHabitat;
    private HabitatResident currentSelectedResident;
    private AnimalIndividual currentSelectedAnimal;
    private readonly List<GameObject> spawnedSlotButtons = new List<GameObject>();
    private Transform resourceSlotsRoot;
    private readonly List<GameObject> spawnedResourceCards = new List<GameObject>();
    private Button harvestAllButton;
    private TextMeshProUGUI harvestAllButtonText;
    private Coroutine feedbackFadeRoutine;

    public bool IsOpen => rootModal != null && rootModal.activeSelf;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        HabitatCreaturePanelStyle.ClearCache();
        BuildUI();
    }

    private void Start()
    {
        if (rootModal != null)
        {
            EconomyHUD.RegisterWindow(rootModal, ClosePanels);
        }

        if (CreatureCareManager.Instance != null)
        {
            CreatureCareManager.Instance.OnCareProgressChanged += HandleCareProgressChanged;
            CreatureCareManager.Instance.OnRankUpgraded += HandleRankUpgraded;
        }
    }

    private void OnDestroy()
    {
        if (CreatureCareManager.HasInstance)
        {
            CreatureCareManager.Instance.OnCareProgressChanged -= HandleCareProgressChanged;
            CreatureCareManager.Instance.OnRankUpgraded -= HandleRankUpgraded;
        }
    }

    public void OpenForHabitat(HabitatState habitat)
    {
        if (habitat == null) return;
        currentHabitat = habitat;

        if (rootModal != null)
        {
            rootModal.SetActive(true);
            rootCanvasGroup.alpha = 1f;
        }

        // Mở Panel 1, Panel 2 chỉ mở khi người chơi nhấp vào avatar lục giác
        currentSelectedResident = null;
        currentSelectedAnimal = null;
        if (panelDetailRect != null)
        {
            panelDetailRect.gameObject.SetActive(false);
        }
        if (CreatureModelPreviewer.Instance != null)
        {
            CreatureModelPreviewer.Instance.ClearModel();
        }

        RefreshPanel1();
    }

    public void ClosePanels()
    {
        CloseRenameModal();
        if (rootModal != null)
        {
            rootModal.SetActive(false);
        }
        currentSelectedResident = null;
        currentSelectedAnimal = null;

        if (CreatureModelPreviewer.Instance != null)
        {
            CreatureModelPreviewer.Instance.ClearModel();
        }
    }

    private void HandleCareProgressChanged(string id, int newCare)
    {
        if (!IsOpen) return;
        RefreshPanel1();
        if (panelDetailRect != null && panelDetailRect.gameObject.activeSelf) RefreshPanel2();
    }

    private void HandleRankUpgraded(string id, CreatureQualityRank newRank)
    {
        if (!IsOpen) return;
        RefreshPanel1();
        if (panelDetailRect != null && panelDetailRect.gameObject.activeSelf) RefreshPanel2();
    }

    #region UI Construction

    private void BuildUI()
    {
        // 1. Root Modal
        rootModal = new GameObject("HabitatCreaturePanelModal", typeof(RectTransform), typeof(CanvasGroup));
        rootModal.transform.SetParent(transform, false);
        RectTransform modalRect = rootModal.GetComponent<RectTransform>();
        EconomyHUD.Stretch(modalRect);

        rootCanvasGroup = rootModal.GetComponent<CanvasGroup>();

        GameObject backdropObj = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        backdropObj.transform.SetParent(rootModal.transform, false);
        EconomyHUD.Stretch(backdropObj.GetComponent<RectTransform>());

        Image bdImg = backdropObj.GetComponent<Image>();
        bdImg.color = new Color(0.02f, 0.04f, 0.06f, 0.45f);

        Button bdBtn = backdropObj.GetComponent<Button>();
        bdBtn.onClick.AddListener(ClosePanels);

        // 2. PANEL 1: Danh sách thú đang sinh sống
        BuildPanel1(rootModal.transform);

        // 3. PANEL 2: Chi tiết sinh vật
        BuildPanel2(rootModal.transform);

        rootModal.SetActive(false);
    }

    private void BuildPanel1(Transform parent)
    {
        GameObject p1Obj = new GameObject("Panel1_LivingCreatures", typeof(RectTransform), typeof(Image));
        p1Obj.transform.SetParent(parent, false);
        panelListRect = p1Obj.GetComponent<RectTransform>();

        panelListRect.anchorMin = new Vector2(1f, 0.5f);
        panelListRect.anchorMax = new Vector2(1f, 0.5f);
        panelListRect.pivot = new Vector2(1f, 0.5f);
        panelListRect.anchoredPosition = new Vector2(-12f, 0f);
        panelListRect.sizeDelta = new Vector2(264f, 620f);

        panelListBg = p1Obj.GetComponent<Image>();
        Sprite bg1 = HabitatCreaturePanelStyle.PanelBackground;
        if (bg1 != null)
        {
            panelListBg.sprite = bg1;
            panelListBg.type = Image.Type.Simple;
        }
        else
        {
            panelListBg.color = new Color(0.10f, 0.14f, 0.20f, 0.96f);
        }

        // --- Header ---
        GameObject weatherObj = new GameObject("WeatherPill", typeof(RectTransform), typeof(TextMeshProUGUI));
        weatherObj.transform.SetParent(panelListRect, false);
        RectTransform wRect = weatherObj.GetComponent<RectTransform>();
        wRect.anchorMin = new Vector2(0f, 1f);
        wRect.anchorMax = new Vector2(0f, 1f);
        wRect.pivot = new Vector2(0f, 1f);
        wRect.anchoredPosition = new Vector2(16f, -14f);
        wRect.sizeDelta = new Vector2(100f, 24f);

        weatherText = weatherObj.GetComponent<TextMeshProUGUI>();
        weatherText.text = "Nắng";
        weatherText.fontSize = 13f;
        weatherText.fontStyle = FontStyles.Bold;
        weatherText.color = new Color(1f, 0.95f, 0.65f, 1f);

        GameObject closeBtnObj = new GameObject("CloseBtn", typeof(RectTransform), typeof(Button), typeof(Image));
        closeBtnObj.transform.SetParent(panelListRect, false);
        RectTransform closeRect = closeBtnObj.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-12f, -12f);
        closeRect.sizeDelta = new Vector2(28f, 28f);

        Image closeImg = closeBtnObj.GetComponent<Image>();
        closeImg.sprite = HabitatCreaturePanelStyle.ButtonExit;
        closeImg.type = Image.Type.Simple;
        closeImg.preserveAspect = true;
        closeImg.color = Color.white;

        Button closeBtn = closeBtnObj.GetComponent<Button>();
        closeBtn.onClick.AddListener(() => PlayCloseWithAnimation(closeRect, ClosePanels));

        // Tên cụm Habitat
        GameObject nameObj = new GameObject("HabitatName", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameObj.transform.SetParent(panelListRect, false);
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.5f, 1f);
        nameRect.anchorMax = new Vector2(0.5f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = new Vector2(0f, -48f);
        nameRect.sizeDelta = new Vector2(230f, 26f);

        habitatNameText = nameObj.GetComponent<TextMeshProUGUI>();
        habitatNameText.alignment = TextAlignmentOptions.Center;
        habitatNameText.fontSize = 15f;
        habitatNameText.fontStyle = FontStyles.Bold;
        habitatNameText.color = Color.white;
        habitatNameText.text = "Đồng Hoa Bloomfield";

        // --- YÊU CẦU 1: THANH TIẾN TRÌNH CÔNG NGHỆ CHILL (100% khi đủ 6 thú) ---
        GameObject capBarObj = new GameObject("EnergyBar", typeof(RectTransform), typeof(Slider));
        capBarObj.transform.SetParent(panelListRect, false);
        RectTransform capRect = capBarObj.GetComponent<RectTransform>();
        capRect.anchorMin = new Vector2(0.5f, 1f);
        capRect.anchorMax = new Vector2(0.5f, 1f);
        capRect.pivot = new Vector2(0.5f, 1f);
        capRect.anchoredPosition = new Vector2(0f, -78f);
        capRect.sizeDelta = new Vector2(226f, 22f);

        capacitySlider = capBarObj.GetComponent<Slider>();
        capacitySlider.interactable = false;
        capacitySlider.minValue = 0f;
        capacitySlider.maxValue = 1f;

        // Khung năng lượng hình hộp công nghệ cao
        GameObject capBg = new GameObject("EnergyTrack", typeof(RectTransform), typeof(Image));
        capBg.transform.SetParent(capBarObj.transform, false);
        EconomyHUD.Stretch(capBg.GetComponent<RectTransform>());
        Image capBgImg = capBg.GetComponent<Image>();
        capBgImg.sprite = HabitatCreaturePanelStyle.EnergyTrackSprite;
        capBgImg.type = Image.Type.Sliced;
        capBgImg.color = Color.white;

        // Vùng lõi năng lượng phát sáng
        GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(capBarObj.transform, false);
        RectTransform faRect = fillArea.GetComponent<RectTransform>();
        faRect.anchorMin = Vector2.zero;
        faRect.anchorMax = Vector2.one;
        faRect.offsetMin = new Vector2(3f, 3f);
        faRect.offsetMax = new Vector2(-3f, -3f);

        GameObject fillObj = new GameObject("EnergyCore", typeof(RectTransform), typeof(Image));
        fillObj.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        capacityFillImg = fillObj.GetComponent<Image>();
        capacityFillImg.sprite = HabitatCreaturePanelStyle.EnergyFillSprite;
        capacityFillImg.type = Image.Type.Sliced;
        capacityFillImg.color = Color.white;
        capacitySlider.fillRect = fillRect;

        // Nhãn text năng lượng (không ký tự ô vuông lỗi)
        GameObject capLblObj = new GameObject("EnergyLabel", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
        capLblObj.transform.SetParent(capBarObj.transform, false);
        EconomyHUD.Stretch(capLblObj.GetComponent<RectTransform>());
        capacityLabel = capLblObj.GetComponent<TextMeshProUGUI>();
        capacityLabel.alignment = TextAlignmentOptions.Center;
        capacityLabel.fontSize = 11f;
        capacityLabel.fontStyle = FontStyles.Bold;
        capacityLabel.color = Color.white;
        capacityLabel.text = "0/6 Cư Dân (0%)";

        var capShadow = capLblObj.GetComponent<Shadow>();
        capShadow.effectColor = new Color(0.04f, 0.08f, 0.15f, 0.90f);
        capShadow.effectDistance = new Vector2(1f, -1f);

        // --- Khu vực lưới tổ ong lục giác ---
        GameObject gridObj = new GameObject("CreatureGrid", typeof(RectTransform));
        gridObj.transform.SetParent(panelListRect, false);
        RectTransform gridRect = gridObj.GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 1f);
        gridRect.anchorMax = new Vector2(0.5f, 1f);
        gridRect.pivot = new Vector2(0.5f, 1f);
        gridRect.anchoredPosition = new Vector2(0f, -112f);
        gridRect.sizeDelta = new Vector2(230f, 210f);
        creatureGridRoot = gridObj.transform;

        // --- Header mục Khai Thác Tài Nguyên ---
        GameObject resHeaderObj = new GameObject("ResourceHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
        resHeaderObj.transform.SetParent(panelListRect, false);
        RectTransform rhRect = resHeaderObj.GetComponent<RectTransform>();
        rhRect.anchorMin = new Vector2(0.5f, 1f);
        rhRect.anchorMax = new Vector2(0.5f, 1f);
        rhRect.pivot = new Vector2(0.5f, 1f);
        rhRect.anchoredPosition = new Vector2(0f, -330f);
        rhRect.sizeDelta = new Vector2(230f, 20f);

        var rhText = resHeaderObj.GetComponent<TextMeshProUGUI>();
        rhText.alignment = TextAlignmentOptions.Center;
        rhText.fontSize = 11.5f;
        rhText.fontStyle = FontStyles.Bold;
        rhText.color = new Color(0.22f, 0.75f, 0.98f, 1f);
        rhText.text = "TÀI NGUYÊN KHAI THÁC";

        // --- Container các ô tài nguyên ---
        GameObject resGridObj = new GameObject("ResourceSlotsContainer", typeof(RectTransform));
        resGridObj.transform.SetParent(panelListRect, false);
        RectTransform resGridRect = resGridObj.GetComponent<RectTransform>();
        resGridRect.anchorMin = new Vector2(0.5f, 1f);
        resGridRect.anchorMax = new Vector2(0.5f, 1f);
        resGridRect.pivot = new Vector2(0.5f, 1f);
        resGridRect.anchoredPosition = new Vector2(0f, -358f);
        resGridRect.sizeDelta = new Vector2(236f, 155f);
        resourceSlotsRoot = resGridObj.transform;

        // --- Nút Thu Hoạch Tất Cả (Sát hàng Quy mô ô đất, không chạm) ---
        GameObject hBarObj = new GameObject("HarvestBar", typeof(RectTransform), typeof(Button), typeof(Image));
        hBarObj.transform.SetParent(panelListRect, false);
        RectTransform hbRect = hBarObj.GetComponent<RectTransform>();
        hbRect.anchorMin = new Vector2(0.5f, 1f);
        hbRect.anchorMax = new Vector2(0.5f, 1f);
        hbRect.pivot = new Vector2(0.5f, 1f);
        hbRect.anchoredPosition = new Vector2(0f, -524f);
        hbRect.sizeDelta = new Vector2(236f, 36f);

        Image hbImg = hBarObj.GetComponent<Image>();
        hbImg.sprite = HabitatCreaturePanelStyle.TechPillEmeraldSprite;
        hbImg.type = Image.Type.Sliced;
        hbImg.color = Color.white;

        harvestAllButton = hBarObj.GetComponent<Button>();
        harvestAllButton.targetGraphic = hbImg;
        harvestAllButton.navigation = new Navigation { mode = Navigation.Mode.None };
        harvestAllButton.onClick.AddListener(OnHarvestAllClicked);

        GameObject hbLblObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        hbLblObj.transform.SetParent(hBarObj.transform, false);
        EconomyHUD.Stretch(hbLblObj.GetComponent<RectTransform>());
        harvestAllButtonText = hbLblObj.GetComponent<TextMeshProUGUI>();
        harvestAllButtonText.alignment = TextAlignmentOptions.Center;
        harvestAllButtonText.fontSize = 11.5f;
        harvestAllButtonText.fontStyle = FontStyles.Bold;
        harvestAllButtonText.color = Color.white;
        harvestAllButtonText.text = "Chưa có tài nguyên chờ thu";

        // --- YÊU CẦU 2: BANNER CHỮ NHẬT DƯỚI CÙNG (pannel_footer_banner.png) TỈ LỆ CHUẨN ĐÚNG MẪU ẢNH 3 ---
        GameObject footerObj = new GameObject("FooterBar", typeof(RectTransform), typeof(Image));
        footerObj.transform.SetParent(panelListRect, false);
        RectTransform footerRect = footerObj.GetComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0.5f, 0f);
        footerRect.anchorMax = new Vector2(0.5f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition = new Vector2(0f, 13f);
        footerRect.sizeDelta = new Vector2(236f, 28.5f);

        Image footImg = footerObj.GetComponent<Image>();
        footImg.sprite = HabitatCreaturePanelStyle.RectBannerSprite;
        footImg.type = Image.Type.Simple;
        footImg.preserveAspect = true;
        footImg.color = Color.white;

        // Text 1: Quy mô ô đất (đặt sau icon lập phương của ảnh)
        GameObject scaleTxtObj = new GameObject("ScaleText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
        scaleTxtObj.transform.SetParent(footerObj.transform, false);
        RectTransform stRect = scaleTxtObj.GetComponent<RectTransform>();
        stRect.anchorMin = new Vector2(0f, 0f);
        stRect.anchorMax = new Vector2(0f, 1f);
        stRect.pivot = new Vector2(0f, 0.5f);
        stRect.anchoredPosition = new Vector2(32f, 0.5f);
        stRect.sizeDelta = new Vector2(104f, 28.5f);

        footerScaleText = scaleTxtObj.GetComponent<TextMeshProUGUI>();
        footerScaleText.alignment = TextAlignmentOptions.Center;
        footerScaleText.fontSize = 10.5f;
        footerScaleText.fontStyle = FontStyles.Bold;
        footerScaleText.color = new Color(0.98f, 0.96f, 0.92f, 1f);
        footerScaleText.text = "Quy mô: 3 Ô Đất";

        var stShadow = scaleTxtObj.GetComponent<Shadow>();
        stShadow.effectColor = new Color(0.06f, 0.10f, 0.18f, 0.90f);
        stShadow.effectDistance = new Vector2(1f, -1f);

        // Text 2: Cư dân (đặt sau icon người/nhà của ảnh)
        GameObject resTxtObj = new GameObject("ResidentText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
        resTxtObj.transform.SetParent(footerObj.transform, false);
        RectTransform rtRect = resTxtObj.GetComponent<RectTransform>();
        rtRect.anchorMin = new Vector2(1f, 0f);
        rtRect.anchorMax = new Vector2(1f, 1f);
        rtRect.pivot = new Vector2(1f, 0.5f);
        rtRect.anchoredPosition = new Vector2(-4f, 0.5f);
        rtRect.sizeDelta = new Vector2(72f, 28.5f);

        footerResidentText = resTxtObj.GetComponent<TextMeshProUGUI>();
        footerResidentText.alignment = TextAlignmentOptions.Center;
        footerResidentText.fontSize = 10.5f;
        footerResidentText.fontStyle = FontStyles.Bold;
        footerResidentText.color = new Color(0.98f, 0.96f, 0.92f, 1f);
        footerResidentText.text = "2/2 Cư Dân";

        var rtShadow = resTxtObj.GetComponent<Shadow>();
        rtShadow.effectColor = new Color(0.06f, 0.10f, 0.18f, 0.90f);
        rtShadow.effectDistance = new Vector2(1f, -1f);
    }

    private void BuildPanel2(Transform parent)
    {
        GameObject p2Obj = new GameObject("Panel2_CreatureDetail", typeof(RectTransform), typeof(Image));
        p2Obj.transform.SetParent(parent, false);
        panelDetailRect = p2Obj.GetComponent<RectTransform>();

        panelDetailRect.anchorMin = new Vector2(1f, 0.5f);
        panelDetailRect.anchorMax = new Vector2(1f, 0.5f);
        panelDetailRect.pivot = new Vector2(1f, 0.5f);
        panelDetailRect.anchoredPosition = new Vector2(-284f, 0f);
        panelDetailRect.sizeDelta = new Vector2(280f, 620f);

        panelDetailBg = p2Obj.GetComponent<Image>();
        panelDetailBg.type = Image.Type.Simple;

        // Nút đóng Panel 2
        GameObject close2Obj = new GameObject("ClosePanel2Btn", typeof(RectTransform), typeof(Button), typeof(Image));
        close2Obj.transform.SetParent(panelDetailRect, false);
        RectTransform c2Rect = close2Obj.GetComponent<RectTransform>();
        c2Rect.anchorMin = new Vector2(1f, 1f);
        c2Rect.anchorMax = new Vector2(1f, 1f);
        c2Rect.pivot = new Vector2(1f, 1f);
        c2Rect.anchoredPosition = new Vector2(-12f, -12f);
        c2Rect.sizeDelta = new Vector2(28f, 28f);

        Image c2Img = close2Obj.GetComponent<Image>();
        c2Img.sprite = HabitatCreaturePanelStyle.ButtonExit;
        c2Img.type = Image.Type.Simple;
        c2Img.preserveAspect = true;
        c2Img.color = Color.white;

        Button close2Btn = close2Obj.GetComponent<Button>();
        close2Btn.onClick.AddListener(() => PlayCloseWithAnimation(c2Rect, () =>
        {
            CloseRenameModal();
            panelDetailRect.gameObject.SetActive(false);
        }));

        // --- HIỂN THỊ 3D MODEL ĐỨNG CHÍNH GIỮA Ô CHỮ NHẬT MÀU HỆ MÔI TRƯỜNG ---
        GameObject modelArea = new GameObject("ModelDisplayArea", typeof(RectTransform));
        modelArea.transform.SetParent(panelDetailRect, false);
        RectTransform mRect = modelArea.GetComponent<RectTransform>();
        mRect.anchorMin = new Vector2(0.5f, 1f);
        mRect.anchorMax = new Vector2(0.5f, 1f);
        mRect.pivot = new Vector2(0.5f, 0.5f);
        mRect.anchoredPosition = new Vector2(0f, -88f); // Đặt trọn vẹn trong vùng chữ nhật màu xanh (y = 0 đến -196px)
        mRect.sizeDelta = new Vector2(170f, 130f);

        // Hỗ trợ chạm/kéo chuột ngang để xoay ngắm thú 360 độ linh hoạt
        var trigger = modelArea.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var dragEntry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.Drag };
        dragEntry.callback.AddListener((data) => {
            var ped = (UnityEngine.EventSystems.PointerEventData)data;
            if (CreatureModelPreviewer.Instance != null)
                CreatureModelPreviewer.Instance.OnDragRotate(ped.delta.x);
        });
        trigger.triggers.Add(dragEntry);

        var endDragEntry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.EndDrag };
        endDragEntry.callback.AddListener((data) => {
            if (CreatureModelPreviewer.Instance != null)
                CreatureModelPreviewer.Instance.OnEndDragRotate();
        });
        trigger.triggers.Add(endDragEntry);

        // 2D Art lót phía dưới (đảm bảo không bao giờ bị trống)
        GameObject artObj = new GameObject("CreatureArtFallback", typeof(RectTransform), typeof(Image));
        artObj.transform.SetParent(modelArea.transform, false);
        EconomyHUD.Stretch(artObj.GetComponent<RectTransform>());
        creatureArtFallbackImg = artObj.GetComponent<Image>();
        creatureArtFallbackImg.preserveAspect = true;

        // 3D Model RawImage đè lên trên (Render-to-Texture trong suốt từ URP Camera)
        GameObject rawObj = new GameObject("ModelRawImage", typeof(RectTransform), typeof(RawImage));
        rawObj.transform.SetParent(modelArea.transform, false);
        EconomyHUD.Stretch(rawObj.GetComponent<RectTransform>());
        creatureModelRawImg = rawObj.GetComponent<RawImage>();
        if (CreatureModelPreviewer.Instance != null && CreatureModelPreviewer.Instance.PreviewTexture != null)
        {
            creatureModelRawImg.texture = CreatureModelPreviewer.Instance.PreviewTexture;
            creatureModelRawImg.color = Color.white;
        }

        // --- TIÊU ĐỀ TÊN SINH VẬT NẰM Ở VÙNG MÀU TRẮNG DƯỚI Ô CHỮ NHẬT XANH (CÓ THỂ ĐỔI TÊN) ---
        GameObject nameHeaderObj = new GameObject("CreatureNameHeader", typeof(RectTransform));
        nameHeaderObj.transform.SetParent(panelDetailRect, false);
        RectTransform nhRect = nameHeaderObj.GetComponent<RectTransform>();
        nhRect.anchorMin = new Vector2(0.5f, 1f);
        nhRect.anchorMax = new Vector2(0.5f, 1f);
        nhRect.pivot = new Vector2(0.5f, 1f);
        nhRect.anchoredPosition = new Vector2(0f, -204f); // Nằm hoàn toàn ở vùng màu trắng dưới ô xanh
        nhRect.sizeDelta = new Vector2(250f, 28f);

        GameObject nameTxtObj = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Button));
        nameTxtObj.transform.SetParent(nameHeaderObj.transform, false);
        RectTransform ntRect = nameTxtObj.GetComponent<RectTransform>();
        ntRect.anchorMin = new Vector2(0f, 0f);
        ntRect.anchorMax = new Vector2(1f, 1f);
        ntRect.offsetMin = new Vector2(24f, 0f);
        ntRect.offsetMax = new Vector2(-24f, 0f);

        creatureNameText = nameTxtObj.GetComponent<TextMeshProUGUI>();
        creatureNameText.alignment = TextAlignmentOptions.Center;
        creatureNameText.fontSize = 16f;
        creatureNameText.fontStyle = FontStyles.Bold;
        creatureNameText.color = new Color(0.08f, 0.16f, 0.24f, 1f);
        creatureNameText.text = "Nai Rừng";

        Button nameClickBtn = nameTxtObj.GetComponent<Button>();
        nameClickBtn.transition = Selectable.Transition.None;
        nameClickBtn.onClick.AddListener(OnRenameClicked);

        // Nút bút chì đổi tên nhỏ bên cạnh (Sử dụng icon sprite cây bút thực tế, không dùng ký tự emoji tránh lỗi ô vuông)
        GameObject editBtnObj = new GameObject("EditBtn", typeof(RectTransform), typeof(Button), typeof(Image));
        editBtnObj.transform.SetParent(nameHeaderObj.transform, false);
        RectTransform ebRect = editBtnObj.GetComponent<RectTransform>();
        ebRect.anchorMin = new Vector2(1f, 0.5f);
        ebRect.anchorMax = new Vector2(1f, 0.5f);
        ebRect.pivot = new Vector2(1f, 0.5f);
        ebRect.anchoredPosition = new Vector2(-6f, 0f);
        ebRect.sizeDelta = new Vector2(18f, 18f);

        Image ebImg = editBtnObj.GetComponent<Image>();
        ebImg.sprite = HabitatCreaturePanelStyle.PencilIconSprite;
        ebImg.preserveAspect = true;
        ebImg.color = Color.white;

        renameButton = editBtnObj.GetComponent<Button>();
        renameButton.targetGraphic = ebImg;
        renameButton.transition = Selectable.Transition.ColorTint;
        ColorBlock ebColors = renameButton.colors;
        ebColors.normalColor = Color.white;
        ebColors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        ebColors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        renameButton.colors = ebColors;
        renameButton.onClick.AddListener(OnRenameClicked);

        // Huy hiệu hệ môi trường & phẩm cấp dưới tên sinh vật (trong vùng màu trắng)
        GameObject badgeObj = new GameObject("CreatureBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
        badgeObj.transform.SetParent(panelDetailRect, false);
        RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0.5f, 1f);
        badgeRect.anchorMax = new Vector2(0.5f, 1f);
        badgeRect.pivot = new Vector2(0.5f, 1f);
        badgeRect.anchoredPosition = new Vector2(0f, -232f);
        badgeRect.sizeDelta = new Vector2(260f, 18f);

        creatureBadgeText = badgeObj.GetComponent<TextMeshProUGUI>();
        creatureBadgeText.alignment = TextAlignmentOptions.Center;
        creatureBadgeText.fontSize = 11.5f;
        creatureBadgeText.fontStyle = FontStyles.Bold;
        creatureBadgeText.color = new Color(0.24f, 0.32f, 0.40f, 1f);

        // --- THANH CUỘN SCROLLRECT ĐẢM BẢO MỌI THÔNG TIN NẰM GỌN TRONG PHẦN MÀU TRẮNG ---
        GameObject scrollObj = new GameObject("InfoScrollView", typeof(RectTransform), typeof(ScrollRect));
        scrollObj.transform.SetParent(panelDetailRect, false);
        RectTransform scrollRectTransform = scrollObj.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = new Vector2(12f, 96f); // Chừa chỗ phía dưới cho bộ nút bấm
        scrollRectTransform.offsetMax = new Vector2(-12f, -254f); // Bắt đầu từ dưới nhãn thông tin, hoàn toàn trong phần màu trắng

        infoScrollRect = scrollObj.GetComponent<ScrollRect>();
        infoScrollRect.horizontal = false;
        infoScrollRect.vertical = true;
        infoScrollRect.movementType = ScrollRect.MovementType.Clamped;
        infoScrollRect.scrollSensitivity = 25f;

        // Viewport
        GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewportObj.transform.SetParent(scrollObj.transform, false);
        EconomyHUD.Stretch(viewportObj.GetComponent<RectTransform>());
        infoScrollRect.viewport = viewportObj.GetComponent<RectTransform>();

        // Content
        GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObj.transform.SetParent(viewportObj.transform, false);
        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 180f);

        VerticalLayoutGroup vLayout = contentObj.GetComponent<VerticalLayoutGroup>();
        vLayout.spacing = 4f;
        vLayout.childControlHeight = true; // BẮT BUỘC ĐỂ CHIỀU CAO Ô CO VỀ ĐÚNG 28-30PX NHƯ ẢNH 4
        vLayout.childControlWidth = true;
        vLayout.childForceExpandHeight = false;
        vLayout.childForceExpandWidth = true;

        ContentSizeFitter csf = contentObj.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        infoScrollRect.content = contentRect;

        // --- CÁC TRƯỜNG THÔNG TIN DÀI (ochitiet_thu) THON GỌN CAO 28PX THEO CHUẨN ẢNH 4 ---
        // 1. Tình trạng hiện tại (Hàng ngang thon gọn cao 28px)
        GameObject statusCard = CreateGlassCard(contentObj.transform, "StatusCard", 28f);

        GameObject stLblObj = new GameObject("StatusLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        stLblObj.transform.SetParent(statusCard.transform, false);
        RectTransform stLRect = stLblObj.GetComponent<RectTransform>();
        stLRect.anchorMin = new Vector2(0f, 0f);
        stLRect.anchorMax = new Vector2(0.40f, 1f);
        stLRect.offsetMin = new Vector2(12f, 0f);
        stLRect.offsetMax = new Vector2(0f, 0f);

        var stLbl = stLblObj.GetComponent<TextMeshProUGUI>();
        stLbl.fontSize = 12f;
        stLbl.fontStyle = FontStyles.Bold;
        stLbl.alignment = TextAlignmentOptions.MidlineLeft;
        stLbl.color = new Color(0.18f, 0.22f, 0.28f, 1f);
        stLbl.text = "Tình Trạng:";

        GameObject stValObj = new GameObject("StatusValue", typeof(RectTransform), typeof(TextMeshProUGUI));
        stValObj.transform.SetParent(statusCard.transform, false);
        RectTransform stVRect = stValObj.GetComponent<RectTransform>();
        stVRect.anchorMin = new Vector2(0.40f, 0f);
        stVRect.anchorMax = new Vector2(1f, 1f);
        stVRect.offsetMin = new Vector2(0f, 0f);
        stVRect.offsetMax = new Vector2(-12f, 0f);

        statusBannerTitle = stValObj.GetComponent<TextMeshProUGUI>();
        statusBannerTitle.fontSize = 12f;
        statusBannerTitle.fontStyle = FontStyles.Bold;
        statusBannerTitle.alignment = TextAlignmentOptions.MidlineRight;
        statusBannerTitle.color = new Color(0.12f, 0.65f, 0.28f, 1f);
        statusBannerTitle.text = "Khỏe Mạnh";

        // 2. Chỉ số No & Chăm sóc (Hàng ngang thon gọn cao 28px kèm thanh tiến độ mini)
        GameObject hungerCard = CreateGlassCard(contentObj.transform, "HungerCard", 28f);

        GameObject hLabelObj = new GameObject("HungerLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        hLabelObj.transform.SetParent(hungerCard.transform, false);
        RectTransform hlRect = hLabelObj.GetComponent<RectTransform>();
        hlRect.anchorMin = new Vector2(0f, 0f);
        hlRect.anchorMax = new Vector2(0.40f, 1f);
        hlRect.offsetMin = new Vector2(12f, 0f);
        hlRect.offsetMax = new Vector2(0f, 0f);

        var hlTxt = hLabelObj.GetComponent<TextMeshProUGUI>();
        hlTxt.fontSize = 12f;
        hlTxt.fontStyle = FontStyles.Bold;
        hlTxt.alignment = TextAlignmentOptions.MidlineLeft;
        hlTxt.color = new Color(0.18f, 0.22f, 0.28f, 1f);
        hlTxt.text = "Chỉ Số No:";

        GameObject hValObj = new GameObject("HungerValue", typeof(RectTransform), typeof(TextMeshProUGUI));
        hValObj.transform.SetParent(hungerCard.transform, false);
        RectTransform hvRect = hValObj.GetComponent<RectTransform>();
        hvRect.anchorMin = new Vector2(0.40f, 0f);
        hvRect.anchorMax = new Vector2(1f, 1f);
        hvRect.offsetMin = new Vector2(0f, 0f);
        hvRect.offsetMax = new Vector2(-12f, 0f);

        hungerProgressLabel = hValObj.GetComponent<TextMeshProUGUI>();
        hungerProgressLabel.fontSize = 12f;
        hungerProgressLabel.fontStyle = FontStyles.Bold;
        hungerProgressLabel.alignment = TextAlignmentOptions.MidlineRight;
        hungerProgressLabel.color = new Color(0.85f, 0.50f, 0.10f, 1f);
        hungerProgressLabel.text = "50/100 (50%)";

        // Mini slider ở đáy ô chỉ số
        GameObject hSliderObj = new GameObject("HungerSlider", typeof(RectTransform), typeof(Slider));
        hSliderObj.transform.SetParent(hungerCard.transform, false);
        RectTransform hsRect = hSliderObj.GetComponent<RectTransform>();
        hsRect.anchorMin = new Vector2(0f, 0f);
        hsRect.anchorMax = new Vector2(1f, 0f);
        hsRect.pivot = new Vector2(0.5f, 0f);
        hsRect.anchoredPosition = new Vector2(0f, 2f);
        hsRect.sizeDelta = new Vector2(-24f, 3f);

        hungerSlider = hSliderObj.GetComponent<Slider>();
        hungerSlider.interactable = false;
        hungerSlider.minValue = 0f;
        hungerSlider.maxValue = 100f;

        GameObject hsBg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        hsBg.transform.SetParent(hSliderObj.transform, false);
        EconomyHUD.Stretch(hsBg.GetComponent<RectTransform>());
        Image hsBgImg = hsBg.GetComponent<Image>();
        hsBgImg.color = new Color(0f, 0f, 0f, 0.10f);

        GameObject hsFillArea = new GameObject("FillArea", typeof(RectTransform));
        hsFillArea.transform.SetParent(hSliderObj.transform, false);
        EconomyHUD.Stretch(hsFillArea.GetComponent<RectTransform>());

        GameObject hsFill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        hsFill.transform.SetParent(hsFillArea.transform, false);
        RectTransform hsFillRect = hsFill.GetComponent<RectTransform>();
        hsFillRect.anchorMin = Vector2.zero;
        hsFillRect.anchorMax = Vector2.one;
        hsFillRect.sizeDelta = Vector2.zero;
        Image hsFillImg = hsFill.GetComponent<Image>();
        hsFillImg.color = new Color(0.95f, 0.65f, 0.15f, 0.95f);
        hungerSlider.fillRect = hsFillRect;

        // 3. Món ăn ưa thích (Hàng ngang thon gọn cao 28px như Ảnh 4, không biểu tượng ô vuông lỗi)
        GameObject foodCard = CreateGlassCard(contentObj.transform, "FoodCard", 28f);

        GameObject fLblObj = new GameObject("FoodLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        fLblObj.transform.SetParent(foodCard.transform, false);
        RectTransform fLRect = fLblObj.GetComponent<RectTransform>();
        fLRect.anchorMin = new Vector2(0f, 0f);
        fLRect.anchorMax = new Vector2(0.35f, 1f);
        fLRect.offsetMin = new Vector2(12f, 0f);
        fLRect.offsetMax = new Vector2(0f, 0f);

        var fLbl = fLblObj.GetComponent<TextMeshProUGUI>();
        foodLabelText = fLbl;
        fLbl.fontSize = 12f;
        fLbl.fontStyle = FontStyles.Bold;
        fLbl.alignment = TextAlignmentOptions.MidlineLeft;
        fLbl.color = new Color(0.18f, 0.22f, 0.28f, 1f);
        fLbl.text = "Thức ăn:";

        GameObject fValObj = new GameObject("FoodValue", typeof(RectTransform), typeof(TextMeshProUGUI));
        fValObj.transform.SetParent(foodCard.transform, false);
        RectTransform fVRect = fValObj.GetComponent<RectTransform>();
        fVRect.anchorMin = new Vector2(0.35f, 0f);
        fVRect.anchorMax = new Vector2(1f, 1f);
        fVRect.offsetMin = new Vector2(0f, 0f);
        fVRect.offsetMax = new Vector2(-12f, 0f);

        favoriteFoodText = fValObj.GetComponent<TextMeshProUGUI>();
        favoriteFoodText.fontSize = 11.5f;
        favoriteFoodText.fontStyle = FontStyles.Bold;
        favoriteFoodText.alignment = TextAlignmentOptions.MidlineRight;
        favoriteFoodText.color = new Color(0.18f, 0.22f, 0.28f, 1f);
        favoriteFoodText.text = "Lá mầm ngọt & Quả rừng tươi";

        // --- CÁC TRƯỜNG THÔNG TIN NHỎ (ochitietnho_thu) 2 HÀNG X 2 CỘT ---
        GameObject chipsGrid = new GameObject("StatChipsGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
        chipsGrid.transform.SetParent(contentObj.transform, false);
        LayoutElement leGrid = chipsGrid.GetComponent<LayoutElement>();
        leGrid.preferredHeight = 62f;
        leGrid.minHeight = 62f;

        GridLayoutGroup glg = chipsGrid.GetComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(124f, 28f);
        glg.spacing = new Vector2(8f, 4f);

        CreateStatChip(chipsGrid.transform, "ChipSpecies", "Sâu Bướm", out chipSpeciesText);
        CreateStatChip(chipsGrid.transform, "ChipRank", "Rank I", out chipRankText);
        CreateStatChip(chipsGrid.transform, "ChipStars", "Rừng Leafwood", out chipStarsText);
        CreateStatChip(chipsGrid.transform, "ChipTrait", "Thân Thiện", out chipTraitText);

        // Thông báo phản hồi đặt phía trên cụm nút
        GameObject fbObj = new GameObject("FeedbackText", typeof(RectTransform), typeof(TextMeshProUGUI));
        fbObj.transform.SetParent(panelDetailRect, false);
        RectTransform fbRect = fbObj.GetComponent<RectTransform>();
        fbRect.anchorMin = new Vector2(0.5f, 0f);
        fbRect.anchorMax = new Vector2(0.5f, 0f);
        fbRect.pivot = new Vector2(0.5f, 0f);
        fbRect.anchoredPosition = new Vector2(0f, 88f);
        fbRect.sizeDelta = new Vector2(256f, 16f);

        feedbackNotifyText = fbObj.GetComponent<TextMeshProUGUI>();
        feedbackNotifyText.alignment = TextAlignmentOptions.Center;
        feedbackNotifyText.fontSize = 11f;
        feedbackNotifyText.fontStyle = FontStyles.Bold;
        feedbackNotifyText.color = new Color(0.12f, 0.65f, 0.28f, 1f);
        feedbackNotifyText.text = "";

        // --- BỘ 3 NÚT HÀNH ĐỘNG THEO BỐ CỤC 2 HÀNG CỦA ẢNH 1 (KHÔNG BÓP MÉO) ---
        GameObject buttonBar = new GameObject("ButtonBar", typeof(RectTransform));
        buttonBar.transform.SetParent(panelDetailRect, false);
        RectTransform btnBarRect = buttonBar.GetComponent<RectTransform>();
        btnBarRect.anchorMin = new Vector2(0.5f, 0f);
        btnBarRect.anchorMax = new Vector2(0.5f, 0f);
        btnBarRect.pivot = new Vector2(0.5f, 0f);
        btnBarRect.anchoredPosition = new Vector2(0f, 8f);
        btnBarRect.sizeDelta = new Vector2(256f, 78f);

        // Hàng 1: Nút Cho Ăn (Trái) & Nút Trị Liệu (Phải) - Đã xóa (-1), (-2), chữ in đậm nổi bật
        feedButton = CreateActionCardButton(buttonBar.transform, "FeedBtn", new Vector2(-66f, 42f), new Vector2(124f, 36f),
            HabitatCreaturePanelStyle.ButtonChoAn, "Cho Ăn", 13.5f, out feedButtonText);
        feedButton.onClick.AddListener(OnFeedClicked);

        medicineButton = CreateActionCardButton(buttonBar.transform, "MedicineBtn", new Vector2(66f, 42f), new Vector2(124f, 36f),
            HabitatCreaturePanelStyle.ButtonTriLieu, "Trị Liệu", 13.5f, out medicineButtonText);
        medicineButton.onClick.AddListener(OnMedicineClicked);

        // Hàng 2: Nút Nâng Cấp (Nằm ngang toàn chiều rộng)
        rankUpButton = CreateActionCardButton(buttonBar.transform, "RankUpBtn", new Vector2(0f, 2f), new Vector2(256f, 36f),
            HabitatCreaturePanelStyle.ButtonNang, "Nâng Cấp", 14f, out rankUpButtonText);
        rankUpButton.onClick.AddListener(OnRankUpClicked);

        // Modal đổi tên sinh vật
        BuildRenameModal(panelDetailRect);
    }

    private GameObject CreateGlassCard(Transform parent, string name, float height)
    {
        GameObject card = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        card.transform.SetParent(parent, false);

        RectTransform rt = card.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, height);

        LayoutElement le = card.GetComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        Image img = card.GetComponent<Image>();
        img.sprite = HabitatCreaturePanelStyle.OChiTietThu;
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        return card;
    }

    private void CreateStatChip(Transform parent, string name, string defaultText, out TextMeshProUGUI textComp)
    {
        GameObject chip = new GameObject(name, typeof(RectTransform), typeof(Image));
        chip.transform.SetParent(parent, false);

        Image img = chip.GetComponent<Image>();
        img.sprite = HabitatCreaturePanelStyle.OChiTietNhoThu;
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(chip.transform, false);
        EconomyHUD.Stretch(txtObj.GetComponent<RectTransform>());

        textComp = txtObj.GetComponent<TextMeshProUGUI>();
        textComp.text = defaultText;
        textComp.alignment = TextAlignmentOptions.Center;
        textComp.fontSize = 12f;
        textComp.fontStyle = FontStyles.Bold;
        textComp.color = new Color(0.15f, 0.20f, 0.26f, 1f);
    }

    private Button CreateActionCardButton(Transform parent, string name, Vector2 pos, Vector2 size, Sprite btnSprite, string label, float fontSize, out TextMeshProUGUI labelTmp)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(Image));
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = btnObj.GetComponent<Image>();
        img.sprite = btnSprite ?? HabitatCreaturePanelStyle.GameButtonSprite;
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        Button btn = btnObj.GetComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.55f);
        btn.colors = cb;

        // Tự động kích hoạt hiệu ứng nháy nảy (Flash & Punch) khi bấm nút
        btn.onClick.AddListener(() => StartCoroutine(AnimateButtonPunch(rt)));

        GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(btnObj.transform, false);
        EconomyHUD.Stretch(txtObj.GetComponent<RectTransform>());

        labelTmp = txtObj.GetComponent<TextMeshProUGUI>();
        labelTmp.text = label;
        labelTmp.alignment = TextAlignmentOptions.Center;
        labelTmp.fontSize = fontSize;
        labelTmp.fontStyle = FontStyles.Bold;
        labelTmp.color = Color.white;

        var shadow = txtObj.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(1f, -1.2f);

        return btn;
    }

    private void PlayCloseWithAnimation(RectTransform btnRect, Action closeAction)
    {
        StartCoroutine(AnimateButtonPunch(btnRect, closeAction));
    }

    private IEnumerator AnimateButtonPunch(RectTransform targetRect, Action onComplete = null)
    {
        if (targetRect == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        Vector3 originalScale = targetRect.localScale;
        Image img = targetRect.GetComponent<Image>();
        Color originalColor = img != null ? img.color : Color.white;
        Color flashColor = new Color(1.3f, 1.3f, 1.3f, 1f);

        float halfDuration = 0.08f;
        float elapsed = 0f;

        // Thu nhỏ và phát sáng nháy một cái
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / halfDuration;
            targetRect.localScale = Vector3.Lerp(originalScale, originalScale * 0.86f, t);
            if (img != null) img.color = Color.Lerp(originalColor, flashColor, t);
            yield return null;
        }

        elapsed = 0f;
        // Nảy về ban đầu
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / halfDuration;
            targetRect.localScale = Vector3.Lerp(originalScale * 0.86f, originalScale, t);
            if (img != null) img.color = Color.Lerp(flashColor, originalColor, t);
            yield return null;
        }

        targetRect.localScale = originalScale;
        if (img != null) img.color = originalColor;

        onComplete?.Invoke();
    }

    private void BuildRenameModal(Transform parent)
    {
        renameModal = new GameObject("RenameModal", typeof(RectTransform), typeof(Image));
        renameModal.transform.SetParent(parent, false);
        RectTransform modalRect = renameModal.GetComponent<RectTransform>();
        EconomyHUD.Stretch(modalRect);

        Image modalBg = renameModal.GetComponent<Image>();
        modalBg.color = new Color(0.04f, 0.08f, 0.14f, 0.75f);
        modalBg.raycastTarget = true;

        // Bấm nền mờ để đóng modal
        Button modalDimmerBtn = renameModal.AddComponent<Button>();
        modalDimmerBtn.transition = Selectable.Transition.None;
        modalDimmerBtn.onClick.AddListener(CloseRenameModal);

        // Khung hộp hội thoại trung tâm (Dialog Box)
        GameObject boxObj = new GameObject("DialogBox", typeof(RectTransform), typeof(Image));
        boxObj.transform.SetParent(renameModal.transform, false);
        RectTransform boxRect = boxObj.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.anchoredPosition = new Vector2(0f, 20f);
        boxRect.sizeDelta = new Vector2(250f, 175f);

        Image boxBg = boxObj.GetComponent<Image>();
        boxBg.sprite = HabitatCreaturePanelStyle.ResourceCardNormalSprite ?? EconomyHUD.RoundedSprite;
        boxBg.type = Image.Type.Sliced;
        boxBg.color = Color.white;

        // Chặn click xuyên qua box ra dimmer
        boxObj.AddComponent<CanvasGroup>();

        // Tiêu đề: ĐỔI TÊN SINH VẬT
        GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(boxObj.transform, false);
        RectTransform tRect = titleObj.GetComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.5f, 1f);
        tRect.anchorMax = new Vector2(0.5f, 1f);
        tRect.pivot = new Vector2(0.5f, 1f);
        tRect.anchoredPosition = new Vector2(0f, -14f);
        tRect.sizeDelta = new Vector2(220f, 22f);

        var titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "ĐỔI TÊN SINH VẬT";
        titleTmp.fontSize = 13.5f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = new Color(0.22f, 0.75f, 0.98f, 1f);

        // Mô tả phụ: Nhập tên hoặc biệt danh mới
        GameObject descObj = new GameObject("Desc", typeof(RectTransform), typeof(TextMeshProUGUI));
        descObj.transform.SetParent(boxObj.transform, false);
        RectTransform dRect = descObj.GetComponent<RectTransform>();
        dRect.anchorMin = new Vector2(0.5f, 1f);
        dRect.anchorMax = new Vector2(0.5f, 1f);
        dRect.pivot = new Vector2(0.5f, 1f);
        dRect.anchoredPosition = new Vector2(0f, -38f);
        dRect.sizeDelta = new Vector2(220f, 18f);

        var descTmp = descObj.GetComponent<TextMeshProUGUI>();
        descTmp.text = "Nhập biệt danh yêu thích:";
        descTmp.fontSize = 11f;
        descTmp.alignment = TextAlignmentOptions.Center;
        descTmp.color = new Color(0.70f, 0.78f, 0.88f, 1f);

        // Khung ô nhập văn bản (TMP_InputField)
        GameObject inputObj = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputObj.transform.SetParent(boxObj.transform, false);
        RectTransform inRect = inputObj.GetComponent<RectTransform>();
        inRect.anchorMin = new Vector2(0.5f, 1f);
        inRect.anchorMax = new Vector2(0.5f, 1f);
        inRect.pivot = new Vector2(0.5f, 1f);
        inRect.anchoredPosition = new Vector2(0f, -66f);
        inRect.sizeDelta = new Vector2(216f, 34f);

        Image inBg = inputObj.GetComponent<Image>();
        inBg.sprite = HabitatCreaturePanelStyle.TechArtBoxSprite ?? EconomyHUD.RoundedSprite;
        inBg.type = Image.Type.Sliced;
        inBg.color = Color.white;

        // TextArea Viewport
        GameObject textArea = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        textArea.transform.SetParent(inputObj.transform, false);
        RectTransform taRect = textArea.GetComponent<RectTransform>();
        taRect.anchorMin = Vector2.zero;
        taRect.anchorMax = Vector2.one;
        taRect.offsetMin = new Vector2(8f, 2f);
        taRect.offsetMax = new Vector2(-8f, -2f);

        // Placeholder Text
        GameObject phObj = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        phObj.transform.SetParent(textArea.transform, false);
        EconomyHUD.Stretch(phObj.GetComponent<RectTransform>());
        TextMeshProUGUI phComp = phObj.GetComponent<TextMeshProUGUI>();
        phComp.text = "Nhập tên mới...";
        phComp.fontSize = 12f;
        phComp.fontStyle = FontStyles.Italic;
        phComp.alignment = TextAlignmentOptions.MidlineLeft;
        phComp.color = new Color(0.55f, 0.65f, 0.75f, 0.65f);

        // Value Text
        GameObject valObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        valObj.transform.SetParent(textArea.transform, false);
        EconomyHUD.Stretch(valObj.GetComponent<RectTransform>());
        TextMeshProUGUI valComp = valObj.GetComponent<TextMeshProUGUI>();
        valComp.fontSize = 12.5f;
        valComp.fontStyle = FontStyles.Bold;
        valComp.alignment = TextAlignmentOptions.MidlineLeft;
        valComp.color = Color.white;

        renameInputField = inputObj.GetComponent<TMP_InputField>();
        renameInputField.textViewport = taRect;
        renameInputField.textComponent = valComp;
        renameInputField.placeholder = phComp;
        renameInputField.characterLimit = 20;

        // Bộ nút Hủy & Lưu
        GameObject btnGroup = new GameObject("ButtonGroup", typeof(RectTransform));
        btnGroup.transform.SetParent(boxObj.transform, false);
        RectTransform bgRect = btnGroup.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 0f);
        bgRect.anchorMax = new Vector2(0.5f, 0f);
        bgRect.pivot = new Vector2(0.5f, 0f);
        bgRect.anchoredPosition = new Vector2(0f, 14f);
        bgRect.sizeDelta = new Vector2(216f, 32f);

        // Nút Hủy
        GameObject cancelBtnObj = new GameObject("CancelBtn", typeof(RectTransform), typeof(Button), typeof(Image));
        cancelBtnObj.transform.SetParent(btnGroup.transform, false);
        RectTransform cbRect = cancelBtnObj.GetComponent<RectTransform>();
        cbRect.anchorMin = new Vector2(0f, 0.5f);
        cbRect.anchorMax = new Vector2(0f, 0.5f);
        cbRect.pivot = new Vector2(0f, 0.5f);
        cbRect.anchoredPosition = new Vector2(0f, 0f);
        cbRect.sizeDelta = new Vector2(100f, 30f);

        Image cbImg = cancelBtnObj.GetComponent<Image>();
        cbImg.sprite = HabitatCreaturePanelStyle.ResourceCardLockedSprite ?? EconomyHUD.RoundedSprite;
        cbImg.type = Image.Type.Sliced;
        cbImg.color = Color.white;

        GameObject cbTxtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        cbTxtObj.transform.SetParent(cancelBtnObj.transform, false);
        EconomyHUD.Stretch(cbTxtObj.GetComponent<RectTransform>());
        var cbTxt = cbTxtObj.GetComponent<TextMeshProUGUI>();
        cbTxt.text = "Hủy";
        cbTxt.fontSize = 11.5f;
        cbTxt.fontStyle = FontStyles.Bold;
        cbTxt.alignment = TextAlignmentOptions.Center;
        cbTxt.color = new Color(0.80f, 0.85f, 0.90f, 1f);

        Button cancelBtn = cancelBtnObj.GetComponent<Button>();
        cancelBtn.onClick.AddListener(CloseRenameModal);

        // Nút Lưu
        GameObject confirmBtnObj = new GameObject("ConfirmBtn", typeof(RectTransform), typeof(Button), typeof(Image));
        confirmBtnObj.transform.SetParent(btnGroup.transform, false);
        RectTransform cfbRect = confirmBtnObj.GetComponent<RectTransform>();
        cfbRect.anchorMin = new Vector2(1f, 0.5f);
        cfbRect.anchorMax = new Vector2(1f, 0.5f);
        cfbRect.pivot = new Vector2(1f, 0.5f);
        cfbRect.anchoredPosition = new Vector2(0f, 0f);
        cfbRect.sizeDelta = new Vector2(100f, 30f);

        Image cfbImg = confirmBtnObj.GetComponent<Image>();
        cfbImg.sprite = HabitatCreaturePanelStyle.TechPillCyanSprite ?? EconomyHUD.RoundedSprite;
        cfbImg.type = Image.Type.Sliced;
        cfbImg.color = Color.white;

        GameObject cfbTxtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        cfbTxtObj.transform.SetParent(confirmBtnObj.transform, false);
        EconomyHUD.Stretch(cfbTxtObj.GetComponent<RectTransform>());
        var cfbTxt = cfbTxtObj.GetComponent<TextMeshProUGUI>();
        cfbTxt.text = "Lưu";
        cfbTxt.fontSize = 11.5f;
        cfbTxt.fontStyle = FontStyles.Bold;
        cfbTxt.alignment = TextAlignmentOptions.Center;
        cfbTxt.color = Color.white;

        Button confirmBtn = confirmBtnObj.GetComponent<Button>();
        confirmBtn.onClick.AddListener(OnConfirmRename);

        renameModal.SetActive(false);
    }

    #endregion

    #region Data Refresh

    private float nextResourceRefresh;
    private string lastPanelStructureKey;

    private void Update()
    {
        if (!IsOpen || currentHabitat == null) return;
        if (Time.unscaledTime >= nextResourceRefresh)
        {
            nextResourceRefresh = Time.unscaledTime + 1f;
            RefreshResourceProductionSlots();
            if (panelDetailRect != null && panelDetailRect.gameObject.activeSelf)
                RefreshPanel2(false);
        }
    }

    private void RefreshResourceProductionSlots()
    {
        if (currentHabitat == null || resourceSlotsRoot == null) return;
        var runtime = ResourceProductionRuntime.Instance;
        ResourceUIHabitat habModel = null;
        if (runtime != null && runtime.HabitatManager != null && runtime.HabitatManager.Catalog != null)
        {
            var prodState = runtime.FindState(currentHabitat.habitatId);
            habModel = ResourceInventoryUIPresentation.Habitat(currentHabitat, prodState, runtime.HabitatManager.Catalog, runtime.Settings);
        }

        if (habModel != null && habModel.structureKey != lastPanelStructureKey)
        {
            RefreshPanel1();
            return;
        }

        long totalPending = 0;
        if (habModel != null && habModel.buffers != null)
        {
            foreach (var b in habModel.buffers)
            {
                if (b != null) totalPending += b.amount;
            }
        }

        if (harvestAllButton != null && harvestAllButtonText != null)
        {
            if (totalPending > 0)
            {
                harvestAllButton.interactable = true;
                harvestAllButton.image.color = Color.white;
                harvestAllButtonText.text = $"Thu Hoạch ({totalPending})";
            }
            else
            {
                harvestAllButton.interactable = false;
                harvestAllButton.image.color = new Color(0.6f, 0.7f, 0.75f, 0.45f);
                harvestAllButtonText.text = "Chưa có tài nguyên chờ thu";
            }
        }
    }

    public void RefreshCurrentHabitat()
    {
        if (currentHabitat == null) return;
        var runtime = ResourceProductionRuntime.Instance;
        if (runtime != null && runtime.HabitatManager != null)
        {
            foreach (var h in runtime.HabitatManager.Habitats)
            {
                if (h != null && h.habitatId == currentHabitat.habitatId)
                {
                    currentHabitat = h;
                    break;
                }
            }
        }
        RefreshPanel1();
        if (currentSelectedResident != null)
        {
            RefreshPanel2();
        }
    }

    private void OnHarvestAllClicked()
    {
        if (currentHabitat == null) return;
        var runtime = ResourceProductionRuntime.Instance;
        if (runtime != null)
        {
            runtime.CollectHabitat(currentHabitat.habitatId);
            RefreshCurrentHabitat();
        }
    }

    private void RefreshPanel1()
    {
        if (currentHabitat == null) return;

        string affName = HabitatCreaturePanelStyle.GetAffinityDisplayName(currentHabitat.affinity);
        habitatNameText.text = affName;

        int currentResidents = currentHabitat.residents != null ? currentHabitat.residents.Count : 0;
        int maxCapacity = currentHabitat.CreatureCapacity; // 0, 2, 4, 6
        
        // YÊU CẦU: Full 100% khi đủ 6 thú (mốc tối đa toàn môi trường). Chưa đủ 6 thú sẽ không đạt 100%
        float capRatio = Mathf.Clamp01((float)currentResidents / 6f);

        capacitySlider.value = capRatio;
        capacityLabel.text = $"{currentResidents}/6 Cư Dân ({Mathf.RoundToInt(capRatio * 100f)}%)";

        // --- 1. Lưới 6 ô sinh vật tổ ong ---
        foreach (var old in spawnedSlotButtons)
        {
            if (old != null) Destroy(old);
        }
        spawnedSlotButtons.Clear();

        Sprite hexFrame = HabitatCreaturePanelStyle.HexButtonFrame;

        // Quy luật mới: 3 -> 6 -> 10 hexes, capacity tương ứng 2 -> 4 -> 6 thú:
        // Cố định 6 slot tổ ong chia đều 3 hàng x 2 cột (mỗi hàng tương ứng 1 mốc)
        const int totalSlots = 6;
        for (int i = 0; i < totalSlots; i++)
        {
            bool hasResident = currentHabitat.residents != null && i < currentHabitat.residents.Count;
            HabitatResident resident = hasResident ? currentHabitat.residents[i] : null;
            bool isSlotUnlocked = i < maxCapacity;
            int requiredHexes = i < 2 ? 3 : (i < 4 ? 6 : 10);

            GameObject slotObj = CreateHoneycombSlotButton(i, resident, isSlotUnlocked, requiredHexes, hexFrame);
            spawnedSlotButtons.Add(slotObj);
        }

        // --- 2. Các ô khai thác tài nguyên nằm ngay dưới lưới thú (Yêu cầu ảnh 1 & 4) ---
        foreach (var old in spawnedResourceCards)
        {
            if (old != null) Destroy(old);
        }
        spawnedResourceCards.Clear();

        var runtime = ResourceProductionRuntime.Instance;
        ResourceUIHabitat habModel = null;
        if (runtime != null && runtime.HabitatManager != null && runtime.HabitatManager.Catalog != null)
        {
            var prodState = runtime.FindState(currentHabitat.habitatId);
            habModel = ResourceInventoryUIPresentation.Habitat(currentHabitat, prodState, runtime.HabitatManager.Catalog, runtime.Settings);
        }
        lastPanelStructureKey = habModel?.structureKey;

        var slotConfigs = new (ResourceTier tier, int subIndex)[]
        {
            (ResourceTier.Tier1, 0),
            (ResourceTier.Tier1, 1),
            (ResourceTier.Tier2, 0)
        };

        for (int i = 0; i < slotConfigs.Length; i++)
        {
            var (tier, subIndex) = slotConfigs[i];
            ResourceUISlot slotData = null;
            if (habModel != null && habModel.slots != null)
            {
                slotData = habModel.slots.Find(s => s != null && s.tier == tier && s.index == subIndex);
            }

            GameObject cardObj = CreateResourceSlotCard(i, tier, subIndex, slotData, resourceSlotsRoot);
            spawnedResourceCards.Add(cardObj);
        }

        // Cập nhật nút thu hoạch tất cả của cụm
        long totalPending = 0;
        if (habModel != null && habModel.buffers != null)
        {
            foreach (var b in habModel.buffers)
            {
                if (b != null) totalPending += b.amount;
            }
        }

        if (harvestAllButton != null && harvestAllButtonText != null)
        {
            if (totalPending > 0)
            {
                harvestAllButton.interactable = true;
                harvestAllButton.image.color = Color.white;
                harvestAllButtonText.text = $"Thu Hoạch ({totalPending})";
            }
            else
            {
                harvestAllButton.interactable = false;
                harvestAllButton.image.color = new Color(0.6f, 0.7f, 0.75f, 0.45f);
                harvestAllButtonText.text = "Chưa có tài nguyên chờ thu";
            }
        }

        int hexCount = currentHabitat.HexCount;
        if (footerScaleText != null)
        {
            footerScaleText.text = $"Quy mô: {hexCount} Ô Đất";
        }
        if (footerResidentText != null)
        {
            footerResidentText.text = $"{currentResidents}/{maxCapacity} Cư Dân";
        }
        if (footerSummaryText != null)
        {
            footerSummaryText.text = $"Quy mô: {hexCount} Ô Đất  |  {currentResidents}/{maxCapacity} Cư Dân";
        }
    }

    private GameObject CreateResourceSlotCard(int slotIndex, ResourceTier tier, int subIndex, ResourceUISlot slotData, Transform parent)
    {
        bool isUnlocked = slotData != null ? slotData.unlocked : HabitatEligibilityService.IsSlotUnlocked(currentHabitat, tier, subIndex);
        string resName = slotData != null ? slotData.name : (isUnlocked ? "Chưa chọn" : "Khóa");
        Sprite icon = slotData != null ? slotData.icon : null;
        double rate = slotData != null ? slotData.rate : 0.0;
        string reason = slotData != null ? slotData.reason : (!isUnlocked ? "Mở ở " + (tier == ResourceTier.Tier2 ? "6" : (subIndex == 0 ? "3" : "4")) + " Ô Đất" : "");
        bool isProducing = isUnlocked && rate > 0.001;

        GameObject cardObj = new GameObject($"ResSlot_{slotIndex}", typeof(RectTransform), typeof(Image));
        cardObj.transform.SetParent(parent, false);
        RectTransform rt = cardObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -slotIndex * 48f);
        rt.sizeDelta = new Vector2(236f, 44f);

        Image bg = cardObj.GetComponent<Image>();
        bg.sprite = isProducing ? HabitatCreaturePanelStyle.ResourceCardProducingSprite :
                    (isUnlocked ? HabitatCreaturePanelStyle.ResourceCardNormalSprite : HabitatCreaturePanelStyle.ResourceCardLockedSprite);
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;

        // 1. Icon Box bên trái (36x36px)
        GameObject iconBox = new GameObject("IconBox", typeof(RectTransform), typeof(Image));
        iconBox.transform.SetParent(cardObj.transform, false);
        RectTransform ibRect = iconBox.GetComponent<RectTransform>();
        ibRect.anchorMin = new Vector2(0f, 0.5f);
        ibRect.anchorMax = new Vector2(0f, 0.5f);
        ibRect.pivot = new Vector2(0f, 0.5f);
        ibRect.anchoredPosition = new Vector2(6f, 0f);
        ibRect.sizeDelta = new Vector2(36f, 36f);

        Image ibImg = iconBox.GetComponent<Image>();
        ibImg.sprite = HabitatCreaturePanelStyle.TechArtBoxSprite;
        ibImg.type = Image.Type.Sliced;
        ibImg.color = Color.white;

        if (icon != null && isUnlocked)
        {
            GameObject icObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icObj.transform.SetParent(iconBox.transform, false);
            EconomyHUD.Stretch(icObj.GetComponent<RectTransform>());
            Image icImg = icObj.GetComponent<Image>();
            icImg.sprite = icon;
            icImg.preserveAspect = true;
            icImg.raycastTarget = false;
        }
        else if (!isUnlocked)
        {
            GameObject lockObj = new GameObject("LockIcon", typeof(RectTransform), typeof(Image));
            lockObj.transform.SetParent(iconBox.transform, false);
            RectTransform lockRect = lockObj.GetComponent<RectTransform>();
            lockRect.anchorMin = new Vector2(0.5f, 0.5f);
            lockRect.anchorMax = new Vector2(0.5f, 0.5f);
            lockRect.pivot = new Vector2(0.5f, 0.5f);
            lockRect.anchoredPosition = Vector2.zero;
            lockRect.sizeDelta = new Vector2(20f, 20f);

            Image lockImg = lockObj.GetComponent<Image>();
            lockImg.sprite = PreserveDiamondStyle.LockWhiteSprite;
            lockImg.preserveAspect = true;
            lockImg.color = new Color(0.70f, 0.76f, 0.88f, 0.90f);
            lockImg.raycastTarget = false;
        }

        // 2. Center Text (Tag + Tên tài nguyên & Tốc độ / Lý do)
        string tierLabel = (tier == ResourceTier.Tier1 ? "T1" : "T2") + $" · Ô {subIndex + 1}";
        GameObject infoObj = new GameObject("Info", typeof(RectTransform));
        infoObj.transform.SetParent(cardObj.transform, false);
        RectTransform infoRect = infoObj.GetComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0f, 0f);
        infoRect.anchorMax = new Vector2(1f, 1f);
        infoRect.offsetMin = new Vector2(48f, 4f);
        infoRect.offsetMax = new Vector2(isUnlocked ? -54f : -6f, -4f);

        // Line 1: Tag + Name
        GameObject line1Obj = new GameObject("TitleLine", typeof(RectTransform), typeof(TextMeshProUGUI));
        line1Obj.transform.SetParent(infoObj.transform, false);
        RectTransform l1Rect = line1Obj.GetComponent<RectTransform>();
        l1Rect.anchorMin = new Vector2(0f, 1f);
        l1Rect.anchorMax = new Vector2(1f, 1f);
        l1Rect.pivot = new Vector2(0f, 1f);
        l1Rect.anchoredPosition = new Vector2(0f, 0f);
        l1Rect.sizeDelta = new Vector2(0f, 20f);

        var l1Text = line1Obj.GetComponent<TextMeshProUGUI>();
        l1Text.fontSize = 11.5f;
        l1Text.fontStyle = FontStyles.Bold;
        l1Text.overflowMode = TextOverflowModes.Ellipsis;
        l1Text.text = isUnlocked ? $"<color=#38BDF8>{tierLabel}</color>  <color=#FFFFFF>{resName}</color>" :
                                  $"<color=#64748B>{tierLabel}</color>  <color=#94A3B8>{reason}</color>";

        // Line 2: Rate / Subtitle
        GameObject line2Obj = new GameObject("SubtitleLine", typeof(RectTransform), typeof(TextMeshProUGUI));
        line2Obj.transform.SetParent(infoObj.transform, false);
        RectTransform l2Rect = line2Obj.GetComponent<RectTransform>();
        l2Rect.anchorMin = new Vector2(0f, 0f);
        l2Rect.anchorMax = new Vector2(1f, 0f);
        l2Rect.pivot = new Vector2(0f, 0f);
        l2Rect.anchoredPosition = new Vector2(0f, 0f);
        l2Rect.sizeDelta = new Vector2(0f, 18f);

        var l2Text = line2Obj.GetComponent<TextMeshProUGUI>();
        l2Text.fontSize = 10f;
        l2Text.overflowMode = TextOverflowModes.Ellipsis;
        if (isProducing)
        {
            l2Text.text = $"<color=#4ADE80>● {rate:0.##}/phút</color>";
        }
        else if (isUnlocked)
        {
            l2Text.text = string.IsNullOrEmpty(slotData?.resourceId) ?
                "<color=#94A3B8>Chưa chọn tài nguyên</color>" :
                $"<color=#F87171>{reason}</color>";
        }
        else
        {
            l2Text.text = "<color=#64748B>Chưa mở khóa</color>";
        }

        // 3. Right Button ("Chọn")
        if (isUnlocked)
        {
            GameObject btnObj = new GameObject("ChooseBtn", typeof(RectTransform), typeof(Button), typeof(Image));
            btnObj.transform.SetParent(cardObj.transform, false);
            RectTransform bRect = btnObj.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(1f, 0.5f);
            bRect.anchorMax = new Vector2(1f, 0.5f);
            bRect.pivot = new Vector2(1f, 0.5f);
            bRect.anchoredPosition = new Vector2(-6f, 0f);
            bRect.sizeDelta = new Vector2(48f, 26f);

            Image btnImg = btnObj.GetComponent<Image>();
            btnImg.sprite = HabitatCreaturePanelStyle.TechPillCyanSprite;
            btnImg.type = Image.Type.Sliced;
            btnImg.color = Color.white;

            GameObject lblObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblObj.transform.SetParent(btnObj.transform, false);
            EconomyHUD.Stretch(lblObj.GetComponent<RectTransform>());
            var lblText = lblObj.GetComponent<TextMeshProUGUI>();
            lblText.text = "Chọn";
            lblText.fontSize = 11f;
            lblText.fontStyle = FontStyles.Bold;
            lblText.alignment = TextAlignmentOptions.Center;
            lblText.color = Color.white;

            Button btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            ResourceTier capturedTier = tier;
            int capturedIndex = subIndex;
            btn.onClick.AddListener(() =>
            {
                if (ResourceInventoryUIController.Instance != null)
                {
                    ResourceInventoryUIController.Instance.OpenSelectorFor(currentHabitat.habitatId, capturedTier, capturedIndex);
                }
            });
        }

        return cardObj;
    }

    /// <summary>
    /// Tạo nút tổ ong lục giác với tọa độ 3 hàng x 2 cột đối xứng tuyệt đối (Mốc 3 Ô -> 6 Ô -> 10 Ô)
    /// </summary>
    private GameObject CreateHoneycombSlotButton(int slotIndex, HabitatResident resident, bool isUnlocked, int requiredHexes, Sprite hexFrame)
    {
        GameObject slotObj = new GameObject($"Slot_{slotIndex}", typeof(RectTransform));
        slotObj.transform.SetParent(creatureGridRoot, false);
        RectTransform rt = slotObj.GetComponent<RectTransform>();

        // Tọa độ 3 hàng x 2 cột đối xứng hoàn hảo trong khung 230px, khoảng cách giữa các hàng rộng rãi không bị dính vào nhau:
        // Hàng 0 (Slot 0, 1 - Mốc 3 Ô):  x = -38f, +38f; y = -4f (đáy -58f)
        // Hàng 1 (Slot 2, 3 - Mốc 6 Ô):  x = -38f, +38f; y = -68f (đáy -122f, cách hàng 0: 10px)
        // Hàng 2 (Slot 4, 5 - Mốc 10 Ô): x = -38f, +38f; y = -132f (đáy -186f, cách hàng 1: 10px)
        int row = slotIndex / 2;
        int col = slotIndex % 2;
        float posX = (col == 0) ? -38f : 38f;
        float posY = -4f - row * 64f;

        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(posX, posY);
        rt.sizeDelta = new Vector2(48f, 54f);

        GameObject btnObj = new GameObject("Button", typeof(RectTransform), typeof(Button), typeof(Image));
        btnObj.transform.SetParent(slotObj.transform, false);
        EconomyHUD.Stretch(btnObj.GetComponent<RectTransform>());

        Image frameImg = btnObj.GetComponent<Image>();
        frameImg.sprite = hexFrame ?? EconomyHUD.RoundedSprite;
        frameImg.color = isUnlocked ? Color.white : new Color(0.6f, 0.6f, 0.65f, 0.6f);

        Button btn = btnObj.GetComponent<Button>();

        if (resident != null && isUnlocked)
        {
            GameObject avtObj = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            avtObj.transform.SetParent(btnObj.transform, false);
            RectTransform avtRect = avtObj.GetComponent<RectTransform>();
            avtRect.anchorMin = new Vector2(0.5f, 0.5f);
            avtRect.anchorMax = new Vector2(0.5f, 0.5f);
            avtRect.pivot = new Vector2(0.5f, 0.5f);
            avtRect.sizeDelta = new Vector2(38f, 38f);

            Image avtImg = avtObj.GetComponent<Image>();
            avtImg.preserveAspect = true;
            avtImg.raycastTarget = false;

            Sprite avtSprite = HabitatCreaturePanelStyle.GetCreatureAvatar(resident.speciesId, resident.qualityRank);
            if (avtSprite != null)
            {
                avtImg.sprite = avtSprite;
                avtImg.color = Color.white;
                avtImg.enabled = true;
            }
            else
            {
                avtImg.enabled = false;
            }

            bool isSelected = currentSelectedResident != null && currentSelectedResident.individualId == resident.individualId;
            if (isSelected)
            {
                frameImg.color = new Color(1f, 0.88f, 0.35f, 1f);
            }

            HabitatResident capturedResident = resident;
            btn.onClick.AddListener(() =>
            {
                SelectResident(capturedResident);
            });
        }
        else if (!isUnlocked)
        {
            GameObject lockObj = new GameObject("LockIcon", typeof(RectTransform), typeof(Image));
            lockObj.transform.SetParent(btnObj.transform, false);
            RectTransform lockRect = lockObj.GetComponent<RectTransform>();
            lockRect.anchoredPosition = new Vector2(0f, 7f);
            lockRect.sizeDelta = new Vector2(16f, 18f);

            Image lockImg = lockObj.GetComponent<Image>();
            lockImg.sprite = PreserveDiamondStyle.LockWhiteSprite;
            lockImg.color = new Color(0.9f, 0.92f, 0.96f, 0.85f);
            lockImg.raycastTarget = false;

            GameObject lockTxtObj = new GameObject("LockText", typeof(RectTransform), typeof(TextMeshProUGUI));
            lockTxtObj.transform.SetParent(btnObj.transform, false);
            RectTransform ltRect = lockTxtObj.GetComponent<RectTransform>();
            ltRect.anchoredPosition = new Vector2(0f, -11f);
            ltRect.sizeDelta = new Vector2(52f, 16f);

            TextMeshProUGUI ltTxt = lockTxtObj.GetComponent<TextMeshProUGUI>();
            ltTxt.text = $"{requiredHexes} Ô";
            ltTxt.alignment = TextAlignmentOptions.Center;
            ltTxt.fontSize = 9.5f;
            ltTxt.fontStyle = FontStyles.Bold;
            ltTxt.color = new Color(1f, 0.85f, 0.35f, 0.95f);

            btn.interactable = false;
        }
        else
        {
            GameObject plusObj = new GameObject("EmptyIcon", typeof(RectTransform), typeof(TextMeshProUGUI));
            plusObj.transform.SetParent(btnObj.transform, false);
            EconomyHUD.Stretch(plusObj.GetComponent<RectTransform>());

            TextMeshProUGUI plusTxt = plusObj.GetComponent<TextMeshProUGUI>();
            plusTxt.text = "+";
            plusTxt.alignment = TextAlignmentOptions.Center;
            plusTxt.fontSize = 18f;
            plusTxt.color = new Color(1f, 1f, 1f, 0.35f);

            btn.interactable = false;
        }

        return slotObj;
    }

    private void SelectResident(HabitatResident resident)
    {
        if (resident == null) return;
        currentSelectedResident = resident;

        currentSelectedAnimal = FindAnimalIndividual(resident.individualId, resident.speciesId);

        if (panelDetailRect != null)
        {
            panelDetailRect.gameObject.SetActive(true);
            RefreshPanel2();
        }

        RefreshPanel1();
    }

    private void RefreshPanel2(bool resetScroll = true)
    {
        if (currentSelectedResident == null || currentHabitat == null) return;

        // 1. Background theo hệ môi trường
        Sprite detailBg = HabitatCreaturePanelStyle.GetDetailBackground(currentHabitat.affinity);
        if (detailBg != null)
        {
            panelDetailBg.sprite = detailBg;
            panelDetailBg.color = Color.white;
        }

        var resData = HabitatCreaturePanelStyle.ResolveCreatureData(currentSelectedResident.speciesId);

        // Tiêu đề tên sinh vật (Mặc định là tên file avatar canonical, người chơi có thể đổi tên tự do)
        string defaultName = !string.IsNullOrEmpty(resData.canonicalAvt) ? resData.canonicalAvt : resData.displayName;
        if (creatureNameText != null)
        {
            creatureNameText.text = GetCreatureName(currentSelectedResident, defaultName);
        }

        // Không tạo lại model 3D khi chỉ làm mới độ no theo thời gian.
        if (resetScroll)
        {
            // 2. Tải trực tiếp 3D Model sinh vật lên sân khấu xoay
            bool modelLoaded = false;
            GameObject targetPrefabOrInstance = resData.modelPrefab;

        // Ưu tiên dùng trực tiếp Prefab, nếu chưa có thì dùng GameObject đang đứng trong bản đồ
            if (targetPrefabOrInstance == null && currentSelectedAnimal != null)
            {
                targetPrefabOrInstance = currentSelectedAnimal.gameObject;
            }

            if (CreatureModelPreviewer.Instance != null && targetPrefabOrInstance != null)
            {
                modelLoaded = CreatureModelPreviewer.Instance.ShowModel(targetPrefabOrInstance);
            }

            if (modelLoaded)
            {
                creatureModelRawImg.enabled = true;
                creatureModelRawImg.color = Color.white;
                creatureArtFallbackImg.enabled = false;
            }
            else
            {
                // Dự phòng 2D art nếu model 3D chưa nạp
                creatureModelRawImg.enabled = false;
                Sprite fullArt = HabitatCreaturePanelStyle.GetCreatureAvatar(currentSelectedResident.speciesId, currentSelectedResident.qualityRank);
                if (fullArt != null)
                {
                    creatureArtFallbackImg.sprite = fullArt;
                    creatureArtFallbackImg.enabled = true;
                }
                else
                {
                    creatureArtFallbackImg.enabled = false;
                }
            }
        }

        // 3. Huy hiệu hệ & cấp sao
        string affName = HabitatCreaturePanelStyle.GetAffinityDisplayName(currentHabitat.affinity);
        int stars = currentSelectedAnimal != null ? currentSelectedAnimal.StarLevel : 2;
        string starStr = new string('★', stars) + new string('☆', Mathf.Max(0, 5 - stars));

        creatureBadgeText.text = $"<color=#1B5E20>[{affName.ToUpper()}]</color>  <color=#E65100>[{currentSelectedResident.qualityRank}]</color>";

        // Care Progress dùng cho nâng rank; độ no và trạng thái sản xuất thuộc production runtime.
        string indId = currentSelectedResident.individualId;
        int care = CreatureCareManager.Instance != null ? CreatureCareManager.Instance.GetCareProgress(indId) : 100;
        var hunger = ResourceProductionRuntime.Instance != null ? ResourceProductionRuntime.Instance.GetCareState(indId) : null;
        int satiety = hunger != null ? Mathf.Clamp(Mathf.RoundToInt((float)hunger.Satiety), 0, 100) : 0;

        // 5. Tình trạng hiện tại (Đặt lên đầu)
        if (hunger != null && hunger.Status == CreatureHungerStatus.Hungry)
        {
            statusBannerTitle.text = "Đói Bụng · Dừng Sản Xuất";
            statusBannerTitle.color = new Color(0.90f, 0.45f, 0.15f, 1f);
        }
        else if (hunger != null && hunger.Status == CreatureHungerStatus.Eating)
        {
            statusBannerTitle.text = hunger.WaitingForFood ? "Chờ Thức Ăn · Dừng Sản Xuất" : "Đang Ăn · Dừng Sản Xuất";
            statusBannerTitle.color = new Color(0.85f, 0.55f, 0.05f, 1f);
        }
        else if (hunger != null && care >= 100)
        {
            statusBannerTitle.text = "Sẵn Sàng Nâng Rank";
            statusBannerTitle.color = new Color(0.85f, 0.55f, 0.05f, 1f);
        }
        else if (hunger != null)
        {
            statusBannerTitle.text = "Khỏe Mạnh";
            statusBannerTitle.color = new Color(0.12f, 0.65f, 0.28f, 1f);
        }
        else
        {
            statusBannerTitle.text = "Chưa có dữ liệu trạng thái";
            statusBannerTitle.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        }

        // 6. Cập nhật 4 Stat Chips (Ảnh 1: thay ô ký tự sao thành tên môi trường thú đang sống)
        string traitStr = currentSelectedAnimal != null ? currentSelectedAnimal.Trait.ToString() : "Thân Thiện";
        chipSpeciesText.text = resData.displayName;
        chipRankText.text = currentSelectedResident.qualityRank.ToString();
        chipStarsText.text = affName; // Tên môi trường sống (Rừng Leafwood, Đồng Hoa Bloomfield,...)
        chipTraitText.text = traitStr;

        // 7. Thanh chỉ số No & Chăm sóc
        hungerSlider.value = satiety;
        hungerProgressLabel.text = hunger != null ? $"{satiety}/100 ({satiety}%)" : "Chưa có dữ liệu";

        // 8. Món ăn ưa thích (sạch sẽ, không biểu tượng ô vuông lỗi)
        var production = ResourceProductionRuntime.Instance;
        string foodId = production != null && production.CareSettings != null
            ? production.CareSettings.FoodResourceId : string.Empty;
        ResourceData foodData = production != null && production.CareSettings != null &&
            production.CareSettings.food != null ? production.CareSettings.food : ResourceCatalog.Find(foodId);
        string foodName = foodData != null ? foodData.resourceName : "Thức ăn";
        int warehouseFood = production != null && production.Inventory != null && !string.IsNullOrEmpty(foodId)
            ? production.Inventory.GetAmount(foodId) : 0;
        long pantryFood = production != null && !string.IsNullOrEmpty(foodId)
            ? production.GetCreatureFoodAmount(indId, foodId) : 0;
        if (foodLabelText != null) foodLabelText.text = foodName + ":";
        favoriteFoodText.text = $"Kho {warehouseFood} · Ngăn {pantryFood}";

        // 9. Nút hành động (Xóa (-1), (-2), chữ in đậm to rõ, nổi bật)
        if (feedButton != null)
        {
            // Có thể nạp thức ăn sớm vào ngăn riêng ngay cả khi creature đang no.
            feedButton.interactable = hunger != null;
            feedButtonText.text = warehouseFood > 0 ? "Nạp Thức Ăn" : "Kho hết Thức Ăn";
        }

        if (medicineButton != null)
        {
            medicineButtonText.text = "Trị Liệu";
        }

        if (rankUpButton != null)
        {
            bool canRankUp = care >= 100 && currentSelectedResident.qualityRank < CreatureQualityRank.RankIII;
            rankUpButton.interactable = canRankUp;
            if (currentSelectedResident.qualityRank >= CreatureQualityRank.RankIII)
            {
                rankUpButtonText.text = "Đã Đạt Max Rank";
            }
            else
            {
                rankUpButtonText.text = "Nâng Cấp";
            }
        }

        // Reset scroll position về trên cùng
        if (resetScroll && infoScrollRect != null)
        {
            infoScrollRect.normalizedPosition = new Vector2(0f, 1f);
        }
    }

    #endregion

    #region Action Buttons

    private void OnFeedClicked()
    {
        if (currentSelectedResident == null || currentHabitat == null) return;
        var production = ResourceProductionRuntime.Instance;
        if (production == null || production.CareSettings == null) return;
        string foodId = production.CareSettings.FoodResourceId;
        ResourceData foodData = production.CareSettings.food != null
            ? production.CareSettings.food : ResourceCatalog.Find(foodId);
        string foodName = foodData != null ? foodData.resourceName : "thức ăn";
        int available = production.Inventory != null ? production.Inventory.GetAmount(foodId) : 0;
        long pantryBefore = production.GetCreatureFoodAmount(currentSelectedResident.individualId, foodId);
        int amount = Mathf.Min(available, production.CareSettings.seedFoodUnitsToFull);
        string reason = string.Empty;
        bool deposited = amount > 0 && production.DepositCreatureFood(currentHabitat.habitatId,
            currentSelectedResident.individualId, foodId, amount, out reason);
        int remaining = production.Inventory != null ? production.Inventory.GetAmount(foodId) : available;
        long pantryAfter = production.GetCreatureFoodAmount(currentSelectedResident.individualId, foodId);
        string feedback = deposited ? $"Đã chuyển {amount} {foodName}: Kho {available} → {remaining}, ngăn cá thể {pantryBefore} → {pantryAfter}." :
            available == 0 ? "Kho chưa có thức ăn phù hợp." : reason;
        if (deposited)
        {
            ShowFeedback(feedback, new Color(0.2f, 0.85f, 0.4f, 1f));
        }
        else
        {
            ShowFeedback(feedback, new Color(0.95f, 0.35f, 0.35f, 1f));
        }

        RefreshPanel2();
    }

    private void OnMedicineClicked()
    {
        if (currentSelectedResident == null) return;

        const int medCost = 1;
        if (CurrencyWallet.Instance != null && !CurrencyWallet.Instance.CanAfford(medCost))
        {
            ShowFeedback("Không đủ Coins để mua thuốc trị liệu!", new Color(0.95f, 0.35f, 0.35f, 1f));
            return;
        }

        if (CurrencyWallet.Instance != null)
        {
            CurrencyWallet.Instance.TrySpend(medCost, CoinReason.CardPurchase);
        }

        ShowFeedback("Đã dùng Dược Liệu! Thú tràn đầy sinh lực và hạnh phúc!", new Color(0.3f, 0.75f, 1f, 1f));
        RefreshPanel2();
    }

    private void OnRankUpClicked()
    {
        if (currentSelectedResident == null || CreatureCareManager.Instance == null) return;

        HabitatPlacementIdentity identity = null;
        if (currentSelectedAnimal != null)
        {
            identity = currentSelectedAnimal.GetComponentInParent<HabitatPlacementIdentity>();
        }

        if (CreatureCareManager.Instance.TryRankUp(currentSelectedAnimal, identity, out string resultInfo))
        {
            if (currentSelectedResident.qualityRank == CreatureQualityRank.RankI)
                currentSelectedResident.qualityRank = CreatureQualityRank.RankII;
            else if (currentSelectedResident.qualityRank == CreatureQualityRank.RankII)
                currentSelectedResident.qualityRank = CreatureQualityRank.RankIII;

            ShowFeedback(resultInfo, new Color(1f, 0.85f, 0.25f, 1f));
            RefreshPanel1();
            RefreshPanel2();
        }
        else
        {
            ShowFeedback(resultInfo, new Color(0.95f, 0.35f, 0.35f, 1f));
        }
    }

    private void ShowFeedback(string msg, Color col)
    {
        if (feedbackNotifyText == null) return;
        feedbackNotifyText.text = msg;
        feedbackNotifyText.color = col;

        if (feedbackFadeRoutine != null) StopCoroutine(feedbackFadeRoutine);
        feedbackFadeRoutine = StartCoroutine(FadeFeedbackRoutine());
    }

    private IEnumerator FadeFeedbackRoutine()
    {
        yield return new WaitForSeconds(3.2f);
        if (feedbackNotifyText != null)
        {
            feedbackNotifyText.text = "";
        }
    }

    private AnimalIndividual FindAnimalIndividual(string individualId, string speciesId)
    {
        var allAnimals = FindObjectsByType<AnimalIndividual>(FindObjectsSortMode.None);
        foreach (var a in allAnimals)
        {
            if (a == null) continue;
            // individualId của habitat là placementId; UniqueID chỉ khớp với dữ liệu cũ
            if (string.Equals(CreatureCareManager.KeyOf(a, null), individualId, StringComparison.Ordinal) ||
                string.Equals(a.UniqueID, individualId, StringComparison.OrdinalIgnoreCase)) return a;
            if (a.SpeciesData != null && string.Equals(a.SpeciesData.speciesID, speciesId, StringComparison.OrdinalIgnoreCase)) return a;
        }
        return null;
    }

    private void OnRenameClicked()
    {
        if (currentSelectedResident == null) return;
        var resData = HabitatCreaturePanelStyle.ResolveCreatureData(currentSelectedResident.speciesId);
        string defaultName = !string.IsNullOrEmpty(resData.canonicalAvt) ? resData.canonicalAvt : resData.displayName;
        string curName = GetCreatureName(currentSelectedResident, defaultName);

        if (renameInputField != null)
        {
            renameInputField.text = curName;
            renameInputField.ActivateInputField();
        }

        if (renameModal != null)
        {
            renameModal.SetActive(true);
        }
    }

    private void CloseRenameModal()
    {
        if (renameModal != null)
        {
            renameModal.SetActive(false);
        }
    }

    private void OnConfirmRename()
    {
        if (currentSelectedResident == null)
        {
            CloseRenameModal();
            return;
        }

        string newName = renameInputField != null ? renameInputField.text.Trim() : "";
        if (!string.IsNullOrEmpty(newName))
        {
            SaveCreatureName(currentSelectedResident, newName);
            if (creatureNameText != null)
            {
                creatureNameText.text = newName;
            }
            ShowFeedback($"Đã đổi tên thành: {newName}", new Color(0.12f, 0.85f, 0.4f, 1f));
        }

        CloseRenameModal();
    }

    private string GetCreatureName(HabitatResident resident, string fallback)
    {
        if (resident == null) return fallback ?? "Sinh Vật";
        if (!string.IsNullOrEmpty(resident.customName))
        {
            return resident.customName;
        }

        // Tên đặt nằm trong bản lưu đảo (qua CreatureCareManager)
        string saved = CreatureCareManager.Instance != null ? CreatureCareManager.Instance.GetCustomName(resident.individualId) : null;
        if (string.IsNullOrEmpty(saved))
        {
            // Tên cũ lưu bằng PlayerPrefs (theo máy): chuyển một lần sang bản lưu đảo rồi xóa khóa
            string legacyKey = $"CreatureCustomName_{resident.individualId}";
            saved = PlayerPrefs.GetString(legacyKey, "");
            if (!string.IsNullOrEmpty(saved) && CreatureCareManager.Instance != null && !WorldLaunch.IsVisit)
            {
                CreatureCareManager.Instance.SetCustomName(resident.individualId, saved);
                PlayerPrefs.DeleteKey(legacyKey);
                PlayerPrefs.Save();
            }
        }
        if (!string.IsNullOrEmpty(saved))
        {
            resident.customName = saved;
            return saved;
        }

        return !string.IsNullOrEmpty(fallback) ? fallback : resident.speciesId;
    }

    private void SaveCreatureName(HabitatResident resident, string newName)
    {
        if (resident == null || WorldLaunch.IsVisit) return;
        resident.customName = newName;
        if (CreatureCareManager.Instance != null) CreatureCareManager.Instance.SetCustomName(resident.individualId, newName);
    }

    #endregion
}
