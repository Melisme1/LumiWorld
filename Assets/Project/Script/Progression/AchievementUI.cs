using System.Collections.Generic;
using System.Globalization;
using LumiWorld.Acs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Redesigned Achievements & Island Milestones modal implementing P-13 using LumiWorldUITheme.
/// Combines all 4 Island Milestones (A-01..A-04) and 7 Achievements (A-05..A-11) into a unified tabbed browser.
/// </summary>
[DisallowMultipleComponent]
public class AchievementUI : MonoBehaviour
{
    public static AchievementUI Instance { get; private set; }

    private enum AchievementCategory
    {
        All = 0,
        Building = 1,
        Creatures = 2,
        Collection = 3
    }

    private sealed class AchievementEntry
    {
        public string Key;
        public bool IsMilestone;
        public int MilestoneIndex;
        public string Id;
        public string BadgeId;
        public AchievementCategory Category;
        public string CategoryLabel;
        public string Title;
        public string Description;
        public int Current;
        public int Target;
        public float ProgressFraction;
        public int RewardCoins;
        public int RewardHarmony;
        public string RewardBonus;
        public bool IsCompleted;
        public bool IsClaimed;

        public bool CanClaim => IsCompleted && !IsClaimed;
    }

    private Canvas _canvas;
    private GameObject _backdropGo;
    private RectTransform _windowRect;

    private TextMeshProUGUI _harmonyChipText;
    private TextMeshProUGUI _readyCountText;

    private readonly Image[] _tabBg = new Image[4];
    private readonly Image[] _tabIcon = new Image[4];
    private readonly TextMeshProUGUI[] _tabLabel = new TextMeshProUGUI[4];
    private readonly Shadow[] _tabShadow = new Shadow[4];

    private RectTransform _listContent;
    private ScrollRect _listScroll;
    private TextMeshProUGUI _emptyListLabel;

    private GameObject _detailFilledGo;
    private TextMeshProUGUI _detailEmptyLabel;
    private Image _detailBadgeImg;
    private TextMeshProUGUI _detailTitle;
    private TextMeshProUGUI _detailCategory;
    private TextMeshProUGUI _detailProgressText;
    private RectTransform _detailProgressFill;
    private TextMeshProUGUI _detailDescription;
    private TextMeshProUGUI _detailCoinRewardText;
    private GameObject _detailCoinRewardRow;
    private TextMeshProUGUI _detailHarmonyRewardText;
    private GameObject _detailHarmonyRewardRow;
    private TextMeshProUGUI _detailBonusText;
    private TextMeshProUGUI _detailStatusText;
    private Button _claimBtn;
    private Image _claimBtnBg;
    private Image _claimBtnIcon;
    private TextMeshProUGUI _claimBtnLabel;

    private AchievementCategory _activeCategory = AchievementCategory.All;
    private readonly List<AchievementEntry> _allEntries = new List<AchievementEntry>();
    private readonly List<AchievementEntry> _filteredEntries = new List<AchievementEntry>();
    private readonly Dictionary<string, GameObject> _selectionBorders = new Dictionary<string, GameObject>();
    private string _selectedKey;
    private string _feedbackMsg = string.Empty;
    private bool _feedbackError;

    public bool IsOpen => _backdropGo != null && _backdropGo.activeSelf;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        HideLegacyHudButton();
        BuildModalIfNeeded();
        if (_backdropGo != null)
            _backdropGo.SetActive(false);
    }

    private void OnEnable()
    {
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnMilestonesChanged += HandleDataChanged;
            AchievementManager.Instance.OnAchievementsUpdated += HandleDataChanged;
            AchievementManager.Instance.OnHarmonyChanged += HandleHarmonyChanged;
        }
    }

    private void OnDisable()
    {
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnMilestonesChanged -= HandleDataChanged;
            AchievementManager.Instance.OnAchievementsUpdated -= HandleDataChanged;
            AchievementManager.Instance.OnHarmonyChanged -= HandleHarmonyChanged;
        }
    }

    private void OnDestroy()
    {
        EconomyHUD.UnregisterWindow(_backdropGo);
        if (Instance == this)
            Instance = null;
    }

    private void HideLegacyHudButton()
    {
        Transform local = transform.Find("AchievementHUDButton");
        if (local != null)
            local.gameObject.SetActive(false);

        GameObject global = GameObject.Find("AchievementHUDButton");
        if (global != null)
            global.SetActive(false);
    }

    public void ToggleWindow()
    {
        if (IsOpen) CloseWindow();
        else OpenWindow();
    }

    public void OpenWindow()
    {
        BuildModalIfNeeded();
        HideLegacyHudButton();
        if (_backdropGo != null)
        {
            _backdropGo.SetActive(true);
            EconomyHUD.RegisterWindow(_backdropGo, CloseWindow);
            EconomyHUD.BringWindowToFront(_backdropGo);
        }

        ApplyScale();
        _feedbackMsg = string.Empty;

        if (AchievementManager.Instance != null)
            AchievementManager.Instance.EvaluateMilestones();

        RefreshData();
    }

    public void CloseWindow()
    {
        if (IsOpen) EconomyHUD.MarkWindowClosed();
        if (_backdropGo != null)
            _backdropGo.SetActive(false);
    }

    public void RefreshData()
    {
        if (!IsOpen)
            return;

        RebuildEntries();
        ApplyCategoryFilter(resetScroll: true);
    }

    private void HandleDataChanged()
    {
        if (IsOpen)
        {
            RebuildEntries();
            ApplyCategoryFilter(resetScroll: false);
        }
    }

    private void HandleHarmonyChanged(int _, int __)
    {
        if (IsOpen)
            UpdateHeaderCounters();
    }

    private void ApplyScale()
    {
        if (_canvas == null || _windowRect == null)
            return;
        RectTransform canvasRt = _canvas.GetComponent<RectTransform>();
        float fit = LumiWorldUITheme.ComputeFitScale(canvasRt, out _);
        _windowRect.localScale = new Vector3(fit, fit, 1f);
    }

    private void BuildModalIfNeeded()
    {
        if (_backdropGo != null)
            return;

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null)
        {
            GameObject canvasGo = new GameObject("AchievementUICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 120;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        }

        _backdropGo = new GameObject("AchievementBackdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        _backdropGo.transform.SetParent(_canvas.transform, false);
        _backdropGo.transform.SetAsLastSibling();
        LumiWorldUITheme.Stretch(_backdropGo.GetComponent<RectTransform>());
        _backdropGo.GetComponent<Image>().color = LumiWorldUITheme.Backdrop;
        Button bgBtn = _backdropGo.GetComponent<Button>();
        bgBtn.transition = Selectable.Transition.None;
        bgBtn.onClick.AddListener(() => { });

        Image winImg = LumiWorldUITheme.CreateRoundedSurface(
            _backdropGo.transform, "AchievementWindow", LumiWorldUITheme.Window, LumiWorldUITheme.Edge,
            radius: 22, borderThickness: 1.5f, shadowDepth: 6f, shadowAlpha: 0.22f, raycastTarget: true);
        _windowRect = winImg.rectTransform;
        _windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        _windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        _windowRect.pivot = new Vector2(0.5f, 0.5f);
        _windowRect.anchoredPosition = Vector2.zero;
        _windowRect.sizeDelta = new Vector2(980f, 610f);

        BuildHeader();
        BuildCategoryTabs();
        BuildSplitBody();
    }

    private void BuildHeader()
    {
        string headerGlyph = LumiWorldUITheme.Glyph("G-02") != null ? "G-02" : "G-03";
        Button badgeTitle = LumiWorldUITheme.CreatePillButton(
            _windowRect, "TitleBadge", "Achievements", headerGlyph, true, 16f,
            () => { }, out _, out _, out _, radius: 14);
        badgeTitle.interactable = false;
        LumiWorldUITheme.PlaceTopLeft(badgeTitle.GetComponent<RectTransform>(), 24f, 16f, 194f, 42f);

        Image readyPill = LumiWorldUITheme.CreateRoundedSurface(
            _windowRect, "ReadyCountPill", LumiWorldUITheme.Surface, LumiWorldUITheme.Edge,
            radius: 17, borderThickness: 1.2f, shadowDepth: 1.5f, shadowAlpha: 0.08f, raycastTarget: false);
        LumiWorldUITheme.PlaceTopRight(readyPill.rectTransform, 212f, 20f, 128f, 34f);
        _readyCountText = LumiWorldUITheme.CreateLabel(
            readyPill.transform, "ReadyTxt", "0 ready", 13.5f, LumiWorldUITheme.Text,
            headingFont: false, align: TextAlignmentOptions.Center);
        LumiWorldUITheme.Stretch(_readyCountText.rectTransform, 6f);

        Image harmPill = LumiWorldUITheme.CreateRoundedSurface(
            _windowRect, "HarmonyChip", LumiWorldUITheme.Primary, Color.clear,
            radius: 17, borderThickness: 0f, shadowDepth: 2f, shadowAlpha: 0.14f, raycastTarget: false);
        LumiWorldUITheme.PlaceTopRight(harmPill.rectTransform, 68f, 20f, 134f, 34f);

        Sprite harmSprite = ResolveHarmonySprite();
        if (harmSprite != null)
        {
            GameObject iconGo = new GameObject("HarmonyIcon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(harmPill.transform, false);
            LumiWorldUITheme.PlaceTopLeft(iconGo.GetComponent<RectTransform>(), 10f, 7f, 20f, 20f);
            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.sprite = harmSprite;
            iconImg.preserveAspect = true;
        }

        _harmonyChipText = LumiWorldUITheme.CreateLabel(
            harmPill.transform, "HarmonyVal", "0", 14.5f, Color.white,
            headingFont: true, align: TextAlignmentOptions.MidlineRight);
        RectTransform hTxtRt = _harmonyChipText.rectTransform;
        hTxtRt.anchorMin = Vector2.zero;
        hTxtRt.anchorMax = Vector2.one;
        hTxtRt.offsetMin = new Vector2(32f, 0f);
        hTxtRt.offsetMax = new Vector2(-14f, 0f);

        LumiWorldUITheme.CreateCloseButton(_windowRect, 22f, 20f, CloseWindow, 34f);
    }

    private void BuildCategoryTabs()
    {
        string allGlyph = LumiWorldUITheme.Glyph("G-01") != null ? "G-01" : null;
        string bldGlyph = LumiWorldUITheme.Glyph("G-13") != null ? "G-13" : "G-19";
        string crtGlyph = LumiWorldUITheme.Glyph("G-12") != null ? "G-12" : "G-20";
        string colGlyph = LumiWorldUITheme.Glyph("G-11") != null ? "G-11" : "G-08";

        string[] labels = { "All", "Building", "Creatures", "Collection" };
        string[] glyphs = { allGlyph, bldGlyph, crtGlyph, colGlyph };
        float[] widths = { 98f, 128f, 136f, 138f };

        float x = 24f;
        float y = 72f;
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            Button btn = LumiWorldUITheme.CreateSubTabButton(
                _windowRect, "AchTab_" + labels[i], labels[i], glyphs[i], 14f,
                () => SelectCategory((AchievementCategory)idx),
                out _tabBg[i], out _tabIcon[i], out _tabLabel[i], out _tabShadow[i]);
            LumiWorldUITheme.PlaceTopLeft(btn.GetComponent<RectTransform>(), x, y, widths[i], 36f);
            x += widths[i] + 8f;
        }
    }

    private void BuildSplitBody()
    {
        Image wellImg = LumiWorldUITheme.CreateRoundedSurface(
            _windowRect, "AchListWell", LumiWorldUITheme.Well, LumiWorldUITheme.Edge,
            radius: 18, borderThickness: 1.5f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: true);
        RectTransform wellRt = wellImg.rectTransform;
        LumiWorldUITheme.PlaceTopLeft(wellRt, 24f, 120f, 560f, 466f);

        GameObject vpGo = new GameObject("ScrollViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        vpGo.transform.SetParent(wellRt, false);
        RectTransform vpRt = vpGo.GetComponent<RectTransform>();
        LumiWorldUITheme.Stretch(vpRt);
        vpGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);

        GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(vpRt, false);
        _listContent = contentGo.GetComponent<RectTransform>();
        _listContent.anchorMin = new Vector2(0f, 1f);
        _listContent.anchorMax = new Vector2(1f, 1f);
        _listContent.pivot = new Vector2(0.5f, 1f);
        _listContent.anchoredPosition = Vector2.zero;
        _listContent.sizeDelta = Vector2.zero;

        VerticalLayoutGroup vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 12, 12);
        vlg.spacing = 8f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = contentGo.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _listScroll = vpGo.GetComponent<ScrollRect>();
        _listScroll.viewport = vpRt;
        _listScroll.content = _listContent;
        _listScroll.horizontal = false;
        _listScroll.vertical = true;
        _listScroll.movementType = ScrollRect.MovementType.Clamped;
        _listScroll.scrollSensitivity = 28f;

        _emptyListLabel = LumiWorldUITheme.CreateLabel(
            wellRt, "EmptyList", "No achievements in this category.", 15f, LumiWorldUITheme.Muted,
            headingFont: false, align: TextAlignmentOptions.Center, wrap: true);
        LumiWorldUITheme.Stretch(_emptyListLabel.rectTransform, 24f);
        _emptyListLabel.gameObject.SetActive(false);

        Image detailImg = LumiWorldUITheme.CreateRoundedSurface(
            _windowRect, "AchDetailPane", LumiWorldUITheme.Surface, LumiWorldUITheme.Edge,
            radius: 18, borderThickness: 1.5f, shadowDepth: 2f, shadowAlpha: 0.08f, raycastTarget: true);
        RectTransform detailRt = detailImg.rectTransform;
        LumiWorldUITheme.PlaceTopRight(detailRt, 24f, 120f, 358f, 466f);

        _detailEmptyLabel = LumiWorldUITheme.CreateLabel(
            detailRt, "DetailEmpty", "Select an achievement to view details.", 15f, LumiWorldUITheme.Muted,
            headingFont: false, align: TextAlignmentOptions.Center, wrap: true);
        LumiWorldUITheme.Stretch(_detailEmptyLabel.rectTransform, 24f);

        _detailFilledGo = new GameObject("DetailFilled", typeof(RectTransform));
        _detailFilledGo.transform.SetParent(detailRt, false);
        LumiWorldUITheme.Stretch(_detailFilledGo.GetComponent<RectTransform>());

        Image badgeBox = LumiWorldUITheme.CreateRoundedSurface(
            _detailFilledGo.transform, "BadgeBox", LumiWorldUITheme.Inset, LumiWorldUITheme.Edge,
            radius: 16, borderThickness: 1.2f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: false);
        LumiWorldUITheme.PlaceTopLeft(badgeBox.rectTransform, 123f, 16f, 112f, 112f);

        GameObject badgeImgGo = new GameObject("BadgeArt", typeof(RectTransform), typeof(Image));
        badgeImgGo.transform.SetParent(badgeBox.transform, false);
        LumiWorldUITheme.Stretch(badgeImgGo.GetComponent<RectTransform>(), 10f);
        _detailBadgeImg = badgeImgGo.GetComponent<Image>();
        _detailBadgeImg.preserveAspect = true;

        _detailTitle = LumiWorldUITheme.CreateLabel(
            _detailFilledGo.transform, "DetailTitle", string.Empty, 19f, LumiWorldUITheme.Text,
            headingFont: true, align: TextAlignmentOptions.Center, wrap: false);
        LumiWorldUITheme.PlaceTopLeft(_detailTitle.rectTransform, 18f, 134f, 322f, 32f);

        _detailCategory = LumiWorldUITheme.CreateLabel(
            _detailFilledGo.transform, "DetailCat", string.Empty, 12.5f, LumiWorldUITheme.Muted,
            headingFont: false, align: TextAlignmentOptions.Center);
        LumiWorldUITheme.PlaceTopLeft(_detailCategory.rectTransform, 18f, 166f, 322f, 20f);

        Image progBox = LumiWorldUITheme.CreateRoundedSurface(
            _detailFilledGo.transform, "ProgressBox", LumiWorldUITheme.Inset, LumiWorldUITheme.Edge,
            radius: 12, borderThickness: 1f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: false);
        RectTransform progRt = progBox.rectTransform;
        LumiWorldUITheme.PlaceTopLeft(progRt, 20f, 194f, 318f, 48f);

        TextMeshProUGUI progLbl = LumiWorldUITheme.CreateLabel(
            progRt, "ProgLbl", "Progress", 13f, LumiWorldUITheme.Muted, headingFont: false);
        LumiWorldUITheme.PlaceTopLeft(progLbl.rectTransform, 12f, 6f, 100f, 20f);

        _detailProgressText = LumiWorldUITheme.CreateLabel(
            progRt, "ProgVal", "0 / 1", 13f, LumiWorldUITheme.Text,
            headingFont: true, align: TextAlignmentOptions.MidlineRight);
        LumiWorldUITheme.PlaceTopRight(_detailProgressText.rectTransform, 12f, 6f, 150f, 22f);

        Image trackImg = LumiWorldUITheme.CreateRoundedSurface(
            progRt, "BarTrack", LumiWorldUITheme.Well, LumiWorldUITheme.Edge,
            radius: 5, borderThickness: 0.8f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: false);
        RectTransform trackRt = trackImg.rectTransform;
        LumiWorldUITheme.PlaceTopLeft(trackRt, 12f, 31f, 294f, 9f);

        Image fillImg = LumiWorldUITheme.CreateRoundedSurface(
            trackRt, "BarFill", LumiWorldUITheme.Success, Color.clear,
            radius: 5, borderThickness: 0f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: false);
        _detailProgressFill = fillImg.rectTransform;
        _detailProgressFill.anchorMin = new Vector2(0f, 0f);
        _detailProgressFill.anchorMax = new Vector2(0f, 1f);
        _detailProgressFill.offsetMin = Vector2.zero;
        _detailProgressFill.offsetMax = Vector2.zero;

        _detailDescription = LumiWorldUITheme.CreateLabel(
            _detailFilledGo.transform, "DetailDesc", string.Empty, 13.5f, LumiWorldUITheme.Text,
            headingFont: false, align: TextAlignmentOptions.TopLeft, wrap: true);
        LumiWorldUITheme.PlaceTopLeft(_detailDescription.rectTransform, 22f, 252f, 314f, 52f);

        Image rewardBox = LumiWorldUITheme.CreateRoundedSurface(
            _detailFilledGo.transform, "RewardsBox", LumiWorldUITheme.Inset, LumiWorldUITheme.Edge,
            radius: 12, borderThickness: 1f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: false);
        RectTransform rewardRt = rewardBox.rectTransform;
        LumiWorldUITheme.PlaceTopLeft(rewardRt, 20f, 308f, 318f, 62f);

        TextMeshProUGUI rewardHdr = LumiWorldUITheme.CreateLabel(
            rewardRt, "RewardsHdr", "Rewards", 12.5f, LumiWorldUITheme.Muted, headingFont: false);
        LumiWorldUITheme.PlaceTopLeft(rewardHdr.rectTransform, 12f, 6f, 100f, 18f);

        _detailCoinRewardRow = CreateRewardChip(rewardRt, "CoinRew", EconomyHUD.CoinSprite, 12f, 28f, out _detailCoinRewardText);
        _detailHarmonyRewardRow = CreateRewardChip(rewardRt, "HarmonyRew", ResolveHarmonySprite(), 156f, 28f, out _detailHarmonyRewardText);

        _detailBonusText = LumiWorldUITheme.CreateLabel(
            _detailFilledGo.transform, "BonusInfo", string.Empty, 12f, LumiWorldUITheme.Muted,
            headingFont: false, align: TextAlignmentOptions.Center, wrap: false);
        LumiWorldUITheme.PlaceTopLeft(_detailBonusText.rectTransform, 20f, 374f, 318f, 20f);

        _detailStatusText = LumiWorldUITheme.CreateLabel(
            _detailFilledGo.transform, "DetailStatus", string.Empty, 12.5f, LumiWorldUITheme.Success,
            headingFont: false, align: TextAlignmentOptions.Center);
        LumiWorldUITheme.PlaceTopLeft(_detailStatusText.rectTransform, 20f, 392f, 318f, 20f);

        _claimBtn = LumiWorldUITheme.CreatePillButton(
            _detailFilledGo.transform, "ClaimBtn", "Claim reward", null, true, 15f,
            ExecuteClaimSelected, out _claimBtnBg, out _claimBtnIcon, out _claimBtnLabel, radius: 14);
        LumiWorldUITheme.PlaceTopLeft(_claimBtn.GetComponent<RectTransform>(), 20f, 412f, 318f, 42f);
    }

    private static GameObject CreateRewardChip(
        Transform parent, string name, Sprite icon, float x, float y, out TextMeshProUGUI label)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        LumiWorldUITheme.PlaceTopLeft(go.GetComponent<RectTransform>(), x, y, 140f, 26f);

        if (icon != null)
        {
            GameObject icGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icGo.transform.SetParent(go.transform, false);
            LumiWorldUITheme.PlaceTopLeft(icGo.GetComponent<RectTransform>(), 0f, 3f, 20f, 20f);
            Image icImg = icGo.GetComponent<Image>();
            icImg.sprite = icon;
            icImg.preserveAspect = true;
        }

        label = LumiWorldUITheme.CreateLabel(
            go.transform, "Text", string.Empty, 13.5f, LumiWorldUITheme.Text, headingFont: true);
        LumiWorldUITheme.PlaceTopLeft(label.rectTransform, icon != null ? 26f : 0f, 2f, 114f, 24f);
        return go;
    }

    private void SelectCategory(AchievementCategory category)
    {
        _activeCategory = category;
        _feedbackMsg = string.Empty;
        ApplyCategoryFilter(resetScroll: true);
    }

    private void RebuildEntries()
    {
        _allEntries.Clear();
        AchievementManager mgr = AchievementManager.Instance;
        if (mgr == null)
            return;

        if (mgr.Milestones != null)
        {
            for (int i = 0; i < mgr.Milestones.Count; i++)
            {
                IslandMilestone m = mgr.Milestones[i];
                if (m == null) continue;
                AchievementCategory cat = ResolveCategory(m.id);
                float frac = m.isCompleted ? 1f : Mathf.Clamp01(m.currentProgress);
                _allEntries.Add(new AchievementEntry
                {
                    Key = "M_" + m.id,
                    IsMilestone = true,
                    MilestoneIndex = m.index,
                    Id = m.id,
                    BadgeId = ResolveBadgeId(m.id, true, i),
                    Category = cat,
                    CategoryLabel = cat.ToString(),
                    Title = string.IsNullOrEmpty(m.title) ? m.id : m.title,
                    Description = string.IsNullOrEmpty(m.description) ? m.shortCondition : m.description,
                    Current = m.isCompleted ? 100 : Mathf.RoundToInt(frac * 100f),
                    Target = 100,
                    ProgressFraction = frac,
                    RewardCoins = m.rewardCoins,
                    RewardHarmony = m.rewardHarmony,
                    RewardBonus = m.rewardBonusInfo,
                    IsCompleted = m.isCompleted,
                    IsClaimed = m.isClaimed
                });
            }
        }

        if (mgr.Achievements != null)
        {
            for (int i = 0; i < mgr.Achievements.Count; i++)
            {
                AchievementGoal g = mgr.Achievements[i];
                if (g == null) continue;
                AchievementCategory cat = ResolveCategory(g.id);
                int target = Mathf.Max(1, g.targetCount);
                int cur = Mathf.Clamp(g.currentCount, 0, target);
                _allEntries.Add(new AchievementEntry
                {
                    Key = "A_" + g.id,
                    IsMilestone = false,
                    MilestoneIndex = -1,
                    Id = g.id,
                    BadgeId = ResolveBadgeId(g.id, false, i),
                    Category = cat,
                    CategoryLabel = cat.ToString(),
                    Title = string.IsNullOrEmpty(g.title) ? g.id : g.title,
                    Description = g.description,
                    Current = cur,
                    Target = target,
                    ProgressFraction = Mathf.Clamp01(cur / (float)target),
                    RewardCoins = g.rewardCoins,
                    RewardHarmony = g.harmonyPoints,
                    RewardBonus = string.Empty,
                    IsCompleted = g.IsCompleted,
                    IsClaimed = g.isClaimed
                });
            }
        }

        _allEntries.Sort((a, b) =>
        {
            int rankA = a.CanClaim ? 0 : (!a.IsClaimed ? 1 : 2);
            int rankB = b.CanClaim ? 0 : (!b.IsClaimed ? 1 : 2);
            if (rankA != rankB) return rankA.CompareTo(rankB);
            if (a.IsMilestone != b.IsMilestone) return a.IsMilestone ? -1 : 1;
            return string.CompareOrdinal(a.BadgeId, b.BadgeId);
        });
    }

    private void ApplyCategoryFilter(bool resetScroll)
    {
        for (int i = 0; i < 4; i++)
            LumiWorldUITheme.SetSubTabActive(_tabBg[i], _tabIcon[i], _tabLabel[i], _tabShadow[i], i == (int)_activeCategory, darkActivePill: false);

        _filteredEntries.Clear();
        for (int i = 0; i < _allEntries.Count; i++)
        {
            if (_activeCategory == AchievementCategory.All || _allEntries[i].Category == _activeCategory)
                _filteredEntries.Add(_allEntries[i]);
        }

        bool foundSelected = false;
        for (int i = 0; i < _filteredEntries.Count; i++)
        {
            if (_filteredEntries[i].Key == _selectedKey)
            {
                foundSelected = true;
                break;
            }
        }
        if (!foundSelected)
            _selectedKey = _filteredEntries.Count > 0 ? _filteredEntries[0].Key : null;

        UpdateHeaderCounters();
        RebuildRows();

        if (resetScroll && _listScroll != null)
            _listScroll.verticalNormalizedPosition = 1f;

        RefreshDetailPane();
    }

    private void UpdateHeaderCounters()
    {
        int ready = 0;
        for (int i = 0; i < _allEntries.Count; i++)
        {
            if (_allEntries[i].CanClaim)
                ready++;
        }

        if (_readyCountText != null)
        {
            _readyCountText.text = $"{ready} ready";
            _readyCountText.color = ready > 0 ? LumiWorldUITheme.Success : LumiWorldUITheme.Muted;
        }

        int totalHarmony = AchievementManager.Instance != null ? AchievementManager.Instance.TotalHarmony : 0;
        if (_harmonyChipText != null)
            _harmonyChipText.text = $"{totalHarmony.ToString("N0", CultureInfo.InvariantCulture)}";
    }

    private void RebuildRows()
    {
        ClearChildren(_listContent);
        _selectionBorders.Clear();

        _emptyListLabel.gameObject.SetActive(_filteredEntries.Count == 0);

        for (int i = 0; i < _filteredEntries.Count; i++)
        {
            AchievementEntry entry = _filteredEntries[i];
            Image rowImg = LumiWorldUITheme.CreateRoundedSurface(
                _listContent, "AchRow_" + entry.BadgeId, LumiWorldUITheme.Surface, LumiWorldUITheme.Edge,
                radius: 14, borderThickness: 1.2f, shadowDepth: 2f, shadowAlpha: 0.08f, raycastTarget: true);
            LayoutElement le = rowImg.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 82f;
            le.minHeight = 82f;

            Button rowBtn = rowImg.gameObject.AddComponent<Button>();
            rowBtn.transition = Selectable.Transition.None;
            string key = entry.Key;
            rowBtn.onClick.AddListener(() => SelectEntry(key));

            GameObject border = LumiWorldUITheme.CreateSelectionBorder(rowImg.transform, 14, 2.5f).gameObject;
            border.SetActive(entry.Key == _selectedKey);
            _selectionBorders[entry.Key] = border;

            Image thumb = LumiWorldUITheme.CreateRoundedSurface(
                rowImg.transform, "BadgeThumb", LumiWorldUITheme.Inset, LumiWorldUITheme.Edge,
                radius: 12, borderThickness: 1f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: false);
            LumiWorldUITheme.PlaceTopLeft(thumb.rectTransform, 12f, 13f, 56f, 56f);

            GameObject iconGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(thumb.transform, false);
            LumiWorldUITheme.Stretch(iconGo.GetComponent<RectTransform>(), 5f);
            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.sprite = LumiWorldUITheme.AchievementBadge(entry.BadgeId);
            iconImg.enabled = iconImg.sprite != null;

            TextMeshProUGUI titleTxt = LumiWorldUITheme.CreateLabel(
                rowImg.transform, "Title", entry.Title, 15.5f, LumiWorldUITheme.Text, headingFont: true);
            LumiWorldUITheme.PlaceTopLeft(titleTxt.rectTransform, 80f, 11f, 340f, 26f);

            string typeLabel = entry.IsMilestone ? "Milestone" : entry.CategoryLabel;
            string rewardSum = $"{entry.RewardCoins}c  +{entry.RewardHarmony} Harmony";
            TextMeshProUGUI metaTxt = LumiWorldUITheme.CreateLabel(
                rowImg.transform, "Meta", $"{typeLabel}  |  {rewardSum}", 12f, LumiWorldUITheme.Muted, headingFont: false);
            LumiWorldUITheme.PlaceTopLeft(metaTxt.rectTransform, 80f, 37f, 340f, 18f);

            Image miniTrack = LumiWorldUITheme.CreateRoundedSurface(
                rowImg.transform, "MiniTrack", LumiWorldUITheme.Inset, LumiWorldUITheme.Edge,
                radius: 4, borderThickness: 0.8f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: false);
            RectTransform trackRt = miniTrack.rectTransform;
            LumiWorldUITheme.PlaceTopLeft(trackRt, 80f, 60f, 230f, 8f);

            float pct = Mathf.Clamp01(entry.ProgressFraction);
            Image fillImg = LumiWorldUITheme.CreateRoundedSurface(
                trackRt, "MiniFill", entry.IsCompleted ? LumiWorldUITheme.Success : LumiWorldUITheme.Primary, Color.clear,
                radius: 4, borderThickness: 0f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: false);
            RectTransform fillRt = fillImg.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(pct, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            string countLabel = entry.IsMilestone
                ? (entry.IsCompleted ? "1 / 1" : $"{Mathf.RoundToInt(pct * 100f)}%")
                : $"{entry.Current}/{entry.Target}";
            TextMeshProUGUI countTxt = LumiWorldUITheme.CreateLabel(
                rowImg.transform, "ProgressCount", countLabel, 12f, LumiWorldUITheme.Muted, headingFont: false);
            LumiWorldUITheme.PlaceTopLeft(countTxt.rectTransform, 318f, 55f, 80f, 18f);

            Color badgeFill;
            Color badgeTextCol;
            string badgeLabel;
            if (entry.CanClaim)
            {
                badgeFill = LumiWorldUITheme.Success;
                badgeTextCol = Color.white;
                badgeLabel = "Claim";
            }
            else if (entry.IsClaimed)
            {
                badgeFill = LumiWorldUITheme.Inset;
                badgeTextCol = LumiWorldUITheme.Muted;
                badgeLabel = "Claimed";
            }
            else
            {
                badgeFill = LumiWorldUITheme.Well;
                badgeTextCol = LumiWorldUITheme.Text;
                badgeLabel = $"{Mathf.FloorToInt(pct * 100f)}%";
            }

            Image statusPill = LumiWorldUITheme.CreateRoundedSurface(
                rowImg.transform, "StatusPill", badgeFill, Color.clear,
                radius: 10, borderThickness: 0f,
                shadowDepth: entry.CanClaim ? 2f : 0f,
                shadowAlpha: entry.CanClaim ? 0.16f : 0f,
                raycastTarget: entry.CanClaim);
            LumiWorldUITheme.PlaceTopRight(statusPill.rectTransform, 14f, 25f, 88f, 32f);
            statusPill.transform.SetAsLastSibling();

            TextMeshProUGUI statusTxt = LumiWorldUITheme.CreateLabel(
                statusPill.transform, "StatusTxt", badgeLabel, 13f, badgeTextCol,
                headingFont: true, align: TextAlignmentOptions.Center);
            LumiWorldUITheme.Stretch(statusTxt.rectTransform);

            if (entry.CanClaim)
            {
                Button rowClaimBtn = statusPill.gameObject.AddComponent<Button>();
                rowClaimBtn.targetGraphic = statusPill;
                rowClaimBtn.navigation = new Navigation { mode = Navigation.Mode.None };
                ColorBlock cb = rowClaimBtn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(0.92f, 0.96f, 0.90f, 1f);
                cb.pressedColor = new Color(0.80f, 0.88f, 0.78f, 1f);
                cb.selectedColor = Color.white;
                cb.fadeDuration = 0.08f;
                rowClaimBtn.colors = cb;
                rowClaimBtn.onClick.AddListener(() => ExecuteClaimEntry(key));
            }
        }
    }

    private void SelectEntry(string key)
    {
        _selectedKey = key;
        _feedbackMsg = string.Empty;
        foreach (KeyValuePair<string, GameObject> kv in _selectionBorders)
        {
            if (kv.Value != null)
                kv.Value.SetActive(kv.Key == _selectedKey);
        }
        RefreshDetailPane();
    }

    private void RefreshDetailPane()
    {
        AchievementEntry entry = FindSelectedEntry();
        if (entry == null)
        {
            _detailFilledGo.SetActive(false);
            _detailEmptyLabel.gameObject.SetActive(true);
            return;
        }

        _detailEmptyLabel.gameObject.SetActive(false);
        _detailFilledGo.SetActive(true);

        _detailBadgeImg.sprite = LumiWorldUITheme.AchievementBadge(entry.BadgeId);
        _detailBadgeImg.enabled = _detailBadgeImg.sprite != null;

        _detailTitle.text = entry.Title;
        string kind = entry.IsMilestone ? "Island Milestone" : "Achievement";
        _detailCategory.text = $"{entry.CategoryLabel}  |  {kind}";

        float pct = Mathf.Clamp01(entry.ProgressFraction);
        _detailProgressText.text = entry.IsMilestone
            ? $"{Mathf.RoundToInt(pct * 100f)}%"
            : $"{entry.Current} / {entry.Target}";
        _detailProgressFill.anchorMax = new Vector2(pct, 1f);

        _detailDescription.text = entry.Description;

        _detailCoinRewardRow.SetActive(entry.RewardCoins > 0);
        _detailCoinRewardText.text = $"{entry.RewardCoins} Coins";

        _detailHarmonyRewardRow.SetActive(entry.RewardHarmony > 0);
        _detailHarmonyRewardText.text = $"+{entry.RewardHarmony} Harmony";

        _detailBonusText.text = string.IsNullOrEmpty(entry.RewardBonus) ? string.Empty : entry.RewardBonus;
        _detailStatusText.text = _feedbackMsg;
        _detailStatusText.color = _feedbackError ? LumiWorldUITheme.Error : LumiWorldUITheme.Success;

        if (entry.CanClaim)
            SetClaimButtonVisual(true, "Claim reward", LumiWorldUITheme.Success);
        else if (entry.IsClaimed)
            SetClaimButtonVisual(false, "Claimed", LumiWorldUITheme.Disabled);
        else
            SetClaimButtonVisual(false, "In progress", LumiWorldUITheme.Disabled);
    }

    private void SetClaimButtonVisual(bool enabled, string text, Color fill)
    {
        if (_claimBtn == null)
            return;
        _claimBtn.interactable = enabled;
        LumiWorldUITheme.ApplyButtonDisabledNeutral(_claimBtn);
        if (_claimBtnBg != null)
            _claimBtnBg.color = fill;
        if (_claimBtnLabel != null)
        {
            _claimBtnLabel.text = text;
            _claimBtnLabel.color = enabled ? Color.white : LumiWorldUITheme.Text;
        }
    }

    private void ExecuteClaimSelected()
    {
        ExecuteClaimEntry(_selectedKey);
    }

    private void ExecuteClaimEntry(string entryKey)
    {
        if (string.IsNullOrEmpty(entryKey) || AchievementManager.Instance == null)
            return;

        _selectedKey = entryKey;
        AchievementEntry entry = FindEntryByKey(entryKey);
        if (entry == null || !entry.CanClaim)
        {
            SelectEntry(entryKey);
            return;
        }

        GameAudioManager.Instance?.PlayClick();

        bool ok;
        string rewardInfo;
        if (entry.IsMilestone)
            ok = AchievementManager.Instance.ClaimMilestone(entry.MilestoneIndex, out rewardInfo);
        else
            ok = AchievementManager.Instance.ClaimAchievement(entry.Id, out rewardInfo);

        _feedbackError = !ok;
        _feedbackMsg = ok
            ? (string.IsNullOrEmpty(rewardInfo) ? "Reward claimed!" : rewardInfo)
            : (string.IsNullOrEmpty(rewardInfo) ? "Cannot claim reward yet." : rewardInfo);

        RebuildEntries();
        ApplyCategoryFilter(resetScroll: false);
    }

    private AchievementEntry FindSelectedEntry()
    {
        return FindEntryByKey(_selectedKey);
    }

    private AchievementEntry FindEntryByKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        for (int i = 0; i < _filteredEntries.Count; i++)
        {
            if (_filteredEntries[i].Key == key)
                return _filteredEntries[i];
        }
        return null;
    }

    private static AchievementCategory ResolveCategory(string id)
    {
        switch (id)
        {
            case "M1_FIRST_HABITAT":
            case "ACH_FIRST_NATURE":
            case "ACH_HABITAT_3":
            case "ACH_HABITAT_6":
                return AchievementCategory.Building;

            case "M2_NEW_SPECIES":
            case "M3_RANK_UP":
            case "ACH_FIRST_CREATURE":
            case "ACH_CONNECTION_DANCE":
            case "ACH_FIRST_CARE":
                return AchievementCategory.Creatures;

            case "M4_ISLAND_STORY":
            case "ACH_HARVEST_10":
                return AchievementCategory.Collection;

            default:
                return AchievementCategory.All;
        }
    }

    private static string ResolveBadgeId(string id, bool isMilestone, int fallbackIndex)
    {
        switch (id)
        {
            case "M1_FIRST_HABITAT": return "A-01";
            case "M2_NEW_SPECIES": return "A-02";
            case "M3_RANK_UP": return "A-03";
            case "M4_ISLAND_STORY": return "A-04";
            case "ACH_FIRST_NATURE": return "A-05";
            case "ACH_HABITAT_3": return "A-06";
            case "ACH_HABITAT_6": return "A-07";
            case "ACH_FIRST_CREATURE": return "A-08";
            case "ACH_CONNECTION_DANCE": return "A-09";
            case "ACH_FIRST_CARE": return "A-10";
            case "ACH_HARVEST_10": return "A-11";
            default:
                int slot = isMilestone ? Mathf.Clamp(fallbackIndex + 1, 1, 4) : Mathf.Clamp(fallbackIndex + 5, 5, 11);
                return $"A-{slot:00}";
        }
    }

    private static Sprite ResolveHarmonySprite()
    {
        return LumiWorldUITheme.Art("H-01") ?? LumiWorldUITheme.Glyph("H-01");
    }

    private static void ClearChildren(RectTransform root)
    {
        if (root == null)
            return;
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }
}
