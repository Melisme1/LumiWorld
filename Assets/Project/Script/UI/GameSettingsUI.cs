using LumiWorld.Acs;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Redesigned Settings modal implementing P-14 using LumiWorldUITheme:
/// Audio tab (Mute pill + approved #0E87EB blue BGM/SFX sliders) and Account tab (Player + Home + Log out).
/// </summary>
[DisallowMultipleComponent]
public class GameSettingsUI : MonoBehaviour
{
    public static GameSettingsUI Instance { get; private set; }

    private enum SettingsTab
    {
        Audio = 0,
        Account = 1
    }

    // Approved P-14 slider accent color (#0E87EB)
    private static readonly Color SliderBlue = new Color(0.055f, 0.529f, 0.922f, 1f);

    private Canvas _canvas;
    private GameObject _backdropGo;
    private RectTransform _windowRect;

    private readonly Image[] _tabBg = new Image[2];
    private readonly Image[] _tabIcon = new Image[2];
    private readonly TextMeshProUGUI[] _tabLabel = new TextMeshProUGUI[2];
    private readonly Shadow[] _tabShadow = new Shadow[2];

    private GameObject _audioViewGo;
    private GameObject _accountViewGo;

    // Audio controls
    private Image _muteTrackImg;
    private RectTransform _muteKnobRt;
    private TextMeshProUGUI _muteStateLabel;
    private Slider _bgmSlider;
    private TextMeshProUGUI _bgmPercentText;
    private Slider _sfxSlider;
    private TextMeshProUGUI _sfxPercentText;
    private bool _suppressAudioCallbacks;

    // Account controls
    private TextMeshProUGUI _playerNameText;
    private TextMeshProUGUI _playerSubText;
    private Button _homeBtn;
    private Button _logoutBtn;
    private Image _logoutBg;
    private Image _logoutIcon;
    private TextMeshProUGUI _logoutLabel;

    private SettingsTab _activeTab = SettingsTab.Audio;
    private bool _isMainMenuMode;

    public bool IsOpen => _backdropGo != null && _backdropGo.activeSelf;

    public static void OpenOrCreate(bool isMainMenu = false)
    {
        GameSettingsUI inst = Instance != null
            ? Instance
            : FindAnyObjectByType<GameSettingsUI>(FindObjectsInactive.Include);

        if (inst == null)
        {
            GameObject go = new GameObject("GameSettingsUI");
            inst = go.AddComponent<GameSettingsUI>();
        }

        inst._isMainMenuMode = isMainMenu || IsCurrentlyMainMenuScene();
        inst.Open();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BuildModalIfNeeded();
        if (_backdropGo != null)
            _backdropGo.SetActive(false);
    }

    private void OnEnable()
    {
        if (GameAudioManager.Instance != null)
            GameAudioManager.Instance.OnVolumeChanged += HandleAudioChanged;
    }

    private void OnDisable()
    {
        if (GameAudioManager.Instance != null)
            GameAudioManager.Instance.OnVolumeChanged -= HandleAudioChanged;
    }

    private void OnDestroy()
    {
        EconomyHUD.UnregisterWindow(_backdropGo);
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (_isMainMenuMode && IsOpen && !EconomyHUD.IsWindowOpen &&
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    public void SetMainMenuMode(bool isMainMenu)
    {
        _isMainMenuMode = isMainMenu;
        if (IsOpen)
            RefreshAccountControls();
    }

    public void OpenWindow() => Open();
    public void CloseWindow() => Close();
    public void ToggleWindow() => Toggle();

    public void Open()
    {
        _isMainMenuMode = _isMainMenuMode || IsCurrentlyMainMenuScene();
        BuildModalIfNeeded();
        if (_backdropGo != null)
        {
            _backdropGo.SetActive(true);
            EconomyHUD.BringWindowToFront(_backdropGo);
            if (_isMainMenuMode) EconomyHUD.UnregisterWindow(_backdropGo);
            else EconomyHUD.RegisterWindow(_backdropGo, Close);
        }

        ApplyScale();
        RefreshAudioControls();
        RefreshAccountControls();
        SwitchTab(_activeTab);
    }

    public void Close()
    {
        if (IsOpen) EconomyHUD.MarkWindowClosed();
        if (_backdropGo != null)
            _backdropGo.SetActive(false);
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    private void HandleAudioChanged()
    {
        if (IsOpen && !_suppressAudioCallbacks)
            RefreshAudioControls();
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
            GameObject canvasGo = new GameObject("GameSettingsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 140;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        }

        _backdropGo = new GameObject("SettingsBackdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        _backdropGo.transform.SetParent(_canvas.transform, false);
        _backdropGo.transform.SetAsLastSibling();
        LumiWorldUITheme.Stretch(_backdropGo.GetComponent<RectTransform>());
        _backdropGo.GetComponent<Image>().color = LumiWorldUITheme.Backdrop;
        Button bgBtn = _backdropGo.GetComponent<Button>();
        bgBtn.transition = Selectable.Transition.None;
        bgBtn.onClick.AddListener(Close);

        Image winImg = LumiWorldUITheme.CreateRoundedSurface(
            _backdropGo.transform, "SettingsWindow", LumiWorldUITheme.Window, LumiWorldUITheme.Edge,
            radius: 22, borderThickness: 1.5f, shadowDepth: 6f, shadowAlpha: 0.24f, raycastTarget: true);
        _windowRect = winImg.rectTransform;
        _windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        _windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        _windowRect.pivot = new Vector2(0.5f, 0.5f);
        _windowRect.anchoredPosition = Vector2.zero;
        _windowRect.sizeDelta = new Vector2(540f, 434f);

        Button titleBadge = LumiWorldUITheme.CreatePillButton(
            _windowRect, "SettingsBadge", "Settings", "G-09", true, 16f,
            () => { }, out _, out _, out _, radius: 14);
        titleBadge.interactable = false;
        LumiWorldUITheme.PlaceTopLeft(titleBadge.GetComponent<RectTransform>(), 24f, 18f, 152f, 40f);

        LumiWorldUITheme.CreateCloseButton(_windowRect, 22f, 21f, Close, 34f);

        string[] tabLabels = { "Audio", "Account" };
        string[] tabGlyphs = { "G-28", "G-29" };
        float tabX = 24f;
        for (int i = 0; i < 2; i++)
        {
            int idx = i;
            Button tabBtn = LumiWorldUITheme.CreateSubTabButton(
                _windowRect, "SettingsTab_" + tabLabels[i], tabLabels[i], tabGlyphs[i], 14.5f,
                () => SwitchTab((SettingsTab)idx),
                out _tabBg[i], out _tabIcon[i], out _tabLabel[i], out _tabShadow[i]);
            LumiWorldUITheme.PlaceTopLeft(tabBtn.GetComponent<RectTransform>(), tabX, 72f, 134f, 36f);
            tabX += 144f;
        }

        Image bodyCard = LumiWorldUITheme.CreateRoundedSurface(
            _windowRect, "BodySurface", LumiWorldUITheme.Surface, LumiWorldUITheme.Edge,
            radius: 18, borderThickness: 1.5f, shadowDepth: 2f, shadowAlpha: 0.08f, raycastTarget: true);
        RectTransform bodyRt = bodyCard.rectTransform;
        LumiWorldUITheme.PlaceTopLeft(bodyRt, 24f, 120f, 492f, 266f);

        BuildAudioView(bodyRt);
        BuildAccountView(bodyRt);

        TextMeshProUGUI escHint = LumiWorldUITheme.CreateLabel(
            _windowRect, "EscBackHint", "Esc   Back", 12.5f, LumiWorldUITheme.Muted,
            headingFont: false, align: TextAlignmentOptions.Center);
        LumiWorldUITheme.PlaceTopLeft(escHint.rectTransform, 24f, 396f, 492f, 24f);
    }

    private void BuildAudioView(RectTransform parent)
    {
        _audioViewGo = new GameObject("AudioView", typeof(RectTransform));
        _audioViewGo.transform.SetParent(parent, false);
        LumiWorldUITheme.Stretch(_audioViewGo.GetComponent<RectTransform>());

        Image muteRow = LumiWorldUITheme.CreateRoundedSurface(
            _audioViewGo.transform, "MuteRow", LumiWorldUITheme.Inset, LumiWorldUITheme.Edge,
            radius: 14, borderThickness: 1f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: false);
        LumiWorldUITheme.PlaceTopLeft(muteRow.rectTransform, 18f, 18f, 456f, 64f);

        TextMeshProUGUI muteTitle = LumiWorldUITheme.CreateLabel(
            muteRow.transform, "MuteTitle", "Mute all audio", 15f, LumiWorldUITheme.Text, headingFont: true);
        LumiWorldUITheme.PlaceTopLeft(muteTitle.rectTransform, 16f, 10f, 220f, 24f);

        TextMeshProUGUI muteSub = LumiWorldUITheme.CreateLabel(
            muteRow.transform, "MuteSub", "Silence music and sound effects", 12f, LumiWorldUITheme.Muted, headingFont: false);
        LumiWorldUITheme.PlaceTopLeft(muteSub.rectTransform, 16f, 34f, 240f, 18f);

        _muteStateLabel = LumiWorldUITheme.CreateLabel(
            muteRow.transform, "MuteState", "Off", 13f, LumiWorldUITheme.Muted,
            headingFont: true, align: TextAlignmentOptions.MidlineRight);
        LumiWorldUITheme.PlaceTopRight(_muteStateLabel.rectTransform, 86f, 20f, 70f, 24f);

        _muteTrackImg = LumiWorldUITheme.CreateRoundedSurface(
            muteRow.transform, "MuteToggleBtn", LumiWorldUITheme.Disabled, Color.clear,
            radius: 16, borderThickness: 0f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: true);
        LumiWorldUITheme.PlaceTopRight(_muteTrackImg.rectTransform, 14f, 16f, 62f, 32f);

        Button toggleBtn = _muteTrackImg.gameObject.AddComponent<Button>();
        toggleBtn.transition = Selectable.Transition.None;
        toggleBtn.onClick.AddListener(OnMutePillClicked);

        Image knobImg = LumiWorldUITheme.CreateRoundedSurface(
            _muteTrackImg.transform, "Knob", LumiWorldUITheme.Surface, LumiWorldUITheme.Edge,
            radius: 12, borderThickness: 1f, shadowDepth: 1.5f, shadowAlpha: 0.16f, raycastTarget: false);
        _muteKnobRt = knobImg.rectTransform;
        _muteKnobRt.anchorMin = new Vector2(0f, 0.5f);
        _muteKnobRt.anchorMax = new Vector2(0f, 0.5f);
        _muteKnobRt.pivot = new Vector2(0.5f, 0.5f);
        _muteKnobRt.sizeDelta = new Vector2(24f, 24f);
        _muteKnobRt.anchoredPosition = new Vector2(16f, 0f);

        _bgmSlider = BuildBlueVolumeSlider(
            _audioViewGo.transform, "BgmRow", "Background music", 98f,
            out _bgmPercentText, OnBgmSliderChanged);

        _sfxSlider = BuildBlueVolumeSlider(
            _audioViewGo.transform, "SfxRow", "Sound effects", 178f,
            out _sfxPercentText, OnSfxSliderChanged);
    }

    private static Slider BuildBlueVolumeSlider(
        Transform parent, string name, string title, float y,
        out TextMeshProUGUI percentText, UnityEngine.Events.UnityAction<float> onValueChanged)
    {
        GameObject row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        LumiWorldUITheme.PlaceTopLeft(row.GetComponent<RectTransform>(), 18f, y, 456f, 66f);

        TextMeshProUGUI lbl = LumiWorldUITheme.CreateLabel(
            row.transform, "Label", title, 14.5f, LumiWorldUITheme.Text, headingFont: false);
        LumiWorldUITheme.PlaceTopLeft(lbl.rectTransform, 4f, 4f, 260f, 24f);

        percentText = LumiWorldUITheme.CreateLabel(
            row.transform, "Percent", "100%", 14f, SliderBlue,
            headingFont: true, align: TextAlignmentOptions.MidlineRight);
        LumiWorldUITheme.PlaceTopRight(percentText.rectTransform, 4f, 4f, 80f, 24f);

        GameObject sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderGo.transform.SetParent(row.transform, false);
        LumiWorldUITheme.PlaceTopLeft(sliderGo.GetComponent<RectTransform>(), 4f, 34f, 448f, 24f);

        Image trackBg = LumiWorldUITheme.CreateRoundedSurface(
            sliderGo.transform, "Background", LumiWorldUITheme.Inset, LumiWorldUITheme.Edge,
            radius: 5, borderThickness: 1f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: true);
        RectTransform bgRt = trackBg.rectTransform;
        bgRt.anchorMin = new Vector2(0f, 0.5f);
        bgRt.anchorMax = new Vector2(1f, 0.5f);
        bgRt.offsetMin = new Vector2(0f, -5f);
        bgRt.offsetMax = new Vector2(0f, 5f);

        GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGo.transform, false);
        RectTransform fillAreaRt = fillArea.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRt.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRt.offsetMin = new Vector2(0f, -5f);
        fillAreaRt.offsetMax = new Vector2(0f, 5f);

        Image fillImg = LumiWorldUITheme.CreateRoundedSurface(
            fillAreaRt, "Fill", SliderBlue, Color.clear,
            radius: 5, borderThickness: 0f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: false);
        RectTransform fillRt = fillImg.rectTransform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        GameObject handleArea = new GameObject("HandleSlideArea", typeof(RectTransform));
        handleArea.transform.SetParent(sliderGo.transform, false);
        RectTransform handleAreaRt = handleArea.GetComponent<RectTransform>();
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = new Vector2(10f, 0f);
        handleAreaRt.offsetMax = new Vector2(-10f, 0f);

        Image handleImg = LumiWorldUITheme.CreateRoundedSurface(
            handleAreaRt, "Handle", Color.white, SliderBlue,
            radius: 11, borderThickness: 2.2f, shadowDepth: 2f, shadowAlpha: 0.18f, raycastTarget: true);
        RectTransform handleRt = handleImg.rectTransform;
        handleRt.sizeDelta = new Vector2(22f, 22f);

        Slider slider = sliderGo.GetComponent<Slider>();
        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImg;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.onValueChanged.AddListener(onValueChanged);
        return slider;
    }

    private void BuildAccountView(RectTransform parent)
    {
        _accountViewGo = new GameObject("AccountView", typeof(RectTransform));
        _accountViewGo.transform.SetParent(parent, false);
        LumiWorldUITheme.Stretch(_accountViewGo.GetComponent<RectTransform>());

        Image cardImg = LumiWorldUITheme.CreateRoundedSurface(
            _accountViewGo.transform, "PlayerCard", LumiWorldUITheme.Inset, LumiWorldUITheme.Edge,
            radius: 14, borderThickness: 1f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: false);
        LumiWorldUITheme.PlaceTopLeft(cardImg.rectTransform, 18f, 20f, 456f, 82f);

        Image avatarFrame = LumiWorldUITheme.CreateRoundedSurface(
            cardImg.transform, "AvatarCircle", LumiWorldUITheme.Surface, LumiWorldUITheme.Edge,
            radius: 26, borderThickness: 1.2f, shadowDepth: 1f, shadowAlpha: 0.08f, raycastTarget: false);
        LumiWorldUITheme.PlaceTopLeft(avatarFrame.rectTransform, 14f, 15f, 52f, 52f);

        GameObject avatarGlyphGo = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
        avatarGlyphGo.transform.SetParent(avatarFrame.transform, false);
        LumiWorldUITheme.Stretch(avatarGlyphGo.GetComponent<RectTransform>(), 13f);
        Image avImg = avatarGlyphGo.GetComponent<Image>();
        avImg.sprite = LumiWorldUITheme.Glyph("G-29");
        avImg.color = LumiWorldUITheme.Text;
        avImg.preserveAspect = true;
        avImg.enabled = avImg.sprite != null;

        _playerNameText = LumiWorldUITheme.CreateLabel(
            cardImg.transform, "PlayerName", "Player", 17f, LumiWorldUITheme.Text, headingFont: true);
        LumiWorldUITheme.PlaceTopLeft(_playerNameText.rectTransform, 80f, 16f, 356f, 28f);

        _playerSubText = LumiWorldUITheme.CreateLabel(
            cardImg.transform, "PlayerSub", "Signed in", 13f, LumiWorldUITheme.Muted, headingFont: false);
        LumiWorldUITheme.PlaceTopLeft(_playerSubText.rectTransform, 80f, 46f, 356f, 20f);

        _homeBtn = LumiWorldUITheme.CreatePillButton(
            _accountViewGo.transform, "HomeBtn", "Home", "G-30", false, 15f,
            OnHomeClicked, out _, out _, out _, radius: 14);
        LumiWorldUITheme.PlaceTopLeft(_homeBtn.GetComponent<RectTransform>(), 18f, 126f, 456f, 48f);

        _logoutBtn = LumiWorldUITheme.CreatePillButton(
            _accountViewGo.transform, "LogoutBtn", "Log out", "G-31", true, 15f,
            OnLogoutClicked, out _logoutBg, out _logoutIcon, out _logoutLabel, radius: 14);
        LumiWorldUITheme.PlaceTopLeft(_logoutBtn.GetComponent<RectTransform>(), 18f, 188f, 456f, 48f);
    }

    private void SwitchTab(SettingsTab tab)
    {
        _activeTab = tab;
        for (int i = 0; i < 2; i++)
            LumiWorldUITheme.SetSubTabActive(_tabBg[i], _tabIcon[i], _tabLabel[i], _tabShadow[i], i == (int)tab, darkActivePill: false);

        _audioViewGo.SetActive(tab == SettingsTab.Audio);
        _accountViewGo.SetActive(tab == SettingsTab.Account);
    }

    private void RefreshAudioControls()
    {
        GameAudioManager audio = GameAudioManager.Instance;
        float bgm = audio != null ? audio.BGMVolume : 0.7f;
        float sfx = audio != null ? audio.SFXVolume : 0.85f;
        bool muted = audio != null && audio.IsMuted;

        _suppressAudioCallbacks = true;
        if (_bgmSlider != null) _bgmSlider.value = bgm;
        if (_sfxSlider != null) _sfxSlider.value = sfx;
        _suppressAudioCallbacks = false;

        if (_bgmPercentText != null) _bgmPercentText.text = $"{Mathf.RoundToInt(bgm * 100f)}%";
        if (_sfxPercentText != null) _sfxPercentText.text = $"{Mathf.RoundToInt(sfx * 100f)}%";

        UpdateMuteVisual(muted);
    }

    private void UpdateMuteVisual(bool muted)
    {
        if (_muteTrackImg != null)
            _muteTrackImg.color = muted ? SliderBlue : LumiWorldUITheme.Disabled;
        if (_muteKnobRt != null)
            _muteKnobRt.anchoredPosition = new Vector2(muted ? 46f : 16f, 0f);
        if (_muteStateLabel != null)
        {
            _muteStateLabel.text = muted ? "Muted" : "Off";
            _muteStateLabel.color = muted ? SliderBlue : LumiWorldUITheme.Muted;
        }
    }

    private void RefreshAccountControls()
    {
        bool loggedIn = AuthSession.IsLoggedIn;
        if (loggedIn)
        {
            string display = !string.IsNullOrEmpty(AuthSession.DisplayName) ? AuthSession.DisplayName : AuthSession.Username;
            if (string.IsNullOrEmpty(display)) display = "Player";
            _playerNameText.text = display;
            _playerSubText.text = !string.IsNullOrEmpty(AuthSession.Username)
                ? $"@{AuthSession.Username}  ·  Signed in"
                : "Signed in";
        }
        else
        {
            _playerNameText.text = "Guest";
            _playerSubText.text = "Not signed in";
        }

        if (_homeBtn != null)
            _homeBtn.gameObject.SetActive(!_isMainMenuMode);

        RectTransform logoutRt = _logoutBtn.GetComponent<RectTransform>();
        LumiWorldUITheme.PlaceTopLeft(logoutRt, 18f, _isMainMenuMode ? 126f : 188f, 456f, 48f);

        if (_logoutLabel != null)
            _logoutLabel.text = loggedIn ? "Log out" : "Log in";
    }

    private void OnMutePillClicked()
    {
        if (GameAudioManager.Instance == null)
            return;
        _suppressAudioCallbacks = true;
        GameAudioManager.Instance.ToggleMute();
        _suppressAudioCallbacks = false;
        UpdateMuteVisual(GameAudioManager.Instance.IsMuted);
    }

    private void OnBgmSliderChanged(float value)
    {
        if (_bgmPercentText != null)
            _bgmPercentText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        if (_suppressAudioCallbacks || GameAudioManager.Instance == null)
            return;

        _suppressAudioCallbacks = true;
        GameAudioManager.Instance.SetBGMVolume(value);
        _suppressAudioCallbacks = false;
    }

    private void OnSfxSliderChanged(float value)
    {
        if (_sfxPercentText != null)
            _sfxPercentText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        if (_suppressAudioCallbacks || GameAudioManager.Instance == null)
            return;

        _suppressAudioCallbacks = true;
        GameAudioManager.Instance.SetSFXVolume(value);
        _suppressAudioCallbacks = false;
    }

    private void OnHomeClicked()
    {
        if (WorldSaveSystem.Instance != null)
            WorldSaveSystem.Instance.SaveNow();
        if (VisitSession.Active)
            VisitSession.End();

        Close();
        SceneManager.LoadScene("MainMenu");
    }

    private void OnLogoutClicked()
    {
        if (!AuthSession.IsLoggedIn)
        {
            Close();
            LoadLoginOrMenu();
            return;
        }

        GameDialogUI.ShowConfirm(
            "Save progress and log out of this account?",
            onConfirm: () =>
            {
                if (WorldSaveSystem.Instance != null)
                    WorldSaveSystem.Instance.SaveNow();
                if (VisitSession.Active)
                    VisitSession.End();

                AuthSession.LogOut();
                Close();
                LoadLoginOrMenu();
            },
            title: "Log out",
            confirmText: "Log out",
            cancelText: "Stay");
    }

    private static void LoadLoginOrMenu()
    {
        string target = Application.CanStreamedLevelBeLoaded("Login") ? "Login" : "MainMenu";
        SceneManager.LoadScene(target);
    }

    private static bool IsCurrentlyMainMenuScene()
    {
        return string.Equals(SceneManager.GetActiveScene().name, "MainMenu", System.StringComparison.OrdinalIgnoreCase);
    }
}
