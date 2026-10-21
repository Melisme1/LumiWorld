using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Giao diện Cài đặt Âm thanh & Tài khoản (Game Settings UI) của LumiWorld:
/// - Mở từ nút Cài đặt ("caidat_button") trên thanh Toolbar thu gọn.
/// - Đồng bộ chuẩn UI tươi sáng xanh - trắng (khung trắng, viền xanh dương, tiêu đề ngoài, nút X ngoài góc).
/// - Điều chỉnh Master Mute, BGM Volume (Nhạc nền), SFX Volume (Hiệu ứng âm thanh).
/// - Hiển thị Tên người chơi (Player Name / Account).
/// - Nút thoát / Đăng xuất: Về trang Home (MainMenu) hoặc Đăng nhập (Login).
/// </summary>
public class GameSettingsUI : MonoBehaviour
{
    private static GameSettingsUI instance;
    public static GameSettingsUI Instance => instance;

    public bool IsOpen => window != null && window.activeSelf;

    private GameObject window;
    private RectTransform panel;
    private Slider bgmSlider;
    private Slider sfxSlider;
    private TextMeshProUGUI bgmValueText;
    private TextMeshProUGUI sfxValueText;
    private TextMeshProUGUI playerNameText;
    private Image muteCheckmark;
    private Coroutine popRoutine;

    private static readonly Color ThemeBlue = new Color(0.08f, 0.58f, 0.95f, 1f);
    private static readonly Color ThemeDark = new Color(0.06f, 0.12f, 0.20f, 1f);
    private static readonly Color CardBg = new Color(0.97f, 0.99f, 1f, 0.98f);
    private static readonly Color RowBg = new Color(0.92f, 0.95f, 0.99f, 0.95f);
    private static readonly Color DangerRed = new Color(0.92f, 0.28f, 0.28f, 1f);

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        BuildWindow();
    }

    private void Start()
    {
        RefreshUIValues();
    }

    public void Open()
    {
        if (window == null) BuildWindow();
        window.SetActive(true);
        window.transform.SetAsLastSibling();
        EconomyHUD.BringWindowToFront(window);
        RefreshUIValues();

        if (popRoutine != null) StopCoroutine(popRoutine);
        popRoutine = StartCoroutine(PopIn());

        GameAudioManager.Instance?.PlayClick();
    }

    public void Close()
    {
        if (!IsOpen) return;
        window.SetActive(false);
        EconomyHUD.MarkWindowClosed();
        GameAudioManager.Instance?.PlayClick();
    }

    public void ToggleWindow()
    {
        if (IsOpen) Close();
        else Open();
    }

    private IEnumerator PopIn()
    {
        const float duration = 0.15f;
        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            float s = Mathf.Lerp(0.92f, 1f, 1f - Mathf.Pow(1f - t, 2f));
            if (panel != null) panel.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if (panel != null) panel.localScale = Vector3.one;
        popRoutine = null;
    }

    private void BuildWindow()
    {
        if (window != null) return;

        // 1. Root Modal Object
        window = new GameObject("GameSettingsModal", typeof(RectTransform));
        window.transform.SetParent(transform, false);
        RectTransform winRect = window.GetComponent<RectTransform>();
        winRect.anchorMin = Vector2.zero;
        winRect.anchorMax = Vector2.one;
        winRect.sizeDelta = Vector2.zero;

        // 2. Backdrop
        GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        backdrop.transform.SetParent(window.transform, false);
        RectTransform bdRect = backdrop.GetComponent<RectTransform>();
        bdRect.anchorMin = Vector2.zero;
        bdRect.anchorMax = Vector2.one;
        bdRect.sizeDelta = Vector2.zero;

        Image bdImg = backdrop.GetComponent<Image>();
        bdImg.color = new Color(0f, 0.05f, 0.12f, 0.45f);
        backdrop.GetComponent<Button>().onClick.AddListener(Close);

        // 3. Main Panel Container (490 x 420)
        GameObject panelObj = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(window.transform, false);
        panel = panelObj.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(490f, 420f);

        Image panelImg = panelObj.GetComponent<Image>();
        panelImg.sprite = EconomyHUD.RoundedSprite;
        panelImg.type = Image.Type.Sliced;
        panelImg.color = CardBg;

        // Blue outer border
        EconomyHUD.AddBlueBorder(panel, ThemeBlue);

        // 4. External Title: "SETTINGS"
        GameObject titleObj = new GameObject("ExternalTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(panelObj.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 0f);
        titleRect.anchoredPosition = new Vector2(0f, 12f);
        titleRect.sizeDelta = new Vector2(300f, 36f);

        var titleText = titleObj.GetComponent<TextMeshProUGUI>();
        titleText.fontSize = 24f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = ThemeDark;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.text = "SETTINGS";

        // 5. External Close "X" Button
        BuildCloseButton(panel);

        // 6. Content Vertical Layout inside panel
        BuildContent(panel);

        window.SetActive(false);
        EconomyHUD.RegisterWindow(window, Close);
    }

    private void BuildCloseButton(RectTransform parent)
    {
        GameObject closeObj = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        closeObj.transform.SetParent(parent, false);
        RectTransform closeRect = closeObj.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(0.5f, 0.5f);
        closeRect.anchoredPosition = new Vector2(14f, 14f);
        closeRect.sizeDelta = new Vector2(38f, 38f);

        Image closeImg = closeObj.GetComponent<Image>();
        closeImg.sprite = EconomyHUD.RoundedSprite;
        closeImg.type = Image.Type.Simple;
        closeImg.color = Color.white;
        EconomyHUD.AddBlueBorder(closeRect, ThemeBlue);

        GameObject xObj = new GameObject("XText", typeof(RectTransform), typeof(TextMeshProUGUI));
        xObj.transform.SetParent(closeObj.transform, false);
        RectTransform xRect = xObj.GetComponent<RectTransform>();
        xRect.anchorMin = Vector2.zero;
        xRect.anchorMax = Vector2.one;
        xRect.sizeDelta = Vector2.zero;

        var xText = xObj.GetComponent<TextMeshProUGUI>();
        xText.fontSize = 20f;
        xText.fontStyle = FontStyles.Bold;
        xText.color = ThemeBlue;
        xText.alignment = TextAlignmentOptions.Center;
        xText.text = "X";

        Button btn = closeObj.GetComponent<Button>();
        btn.targetGraphic = closeImg;
        btn.onClick.AddListener(Close);
    }

    private void BuildContent(RectTransform parent)
    {
        float startY = -38f;
        float spacing = 64f;

        // 1. Mute Sound Row
        BuildMuteRow(parent, new Vector2(0f, startY));

        // 2. BGM Volume Slider Row
        startY -= spacing;
        BuildSliderRow(parent, new Vector2(0f, startY), "Music (BGM)", out bgmSlider, out bgmValueText, (val) =>
        {
            GameAudioManager.Instance?.SetBGMVolume(val);
            if (bgmValueText != null) bgmValueText.text = Mathf.RoundToInt(val * 100f) + "%";
        });

        // 3. SFX Volume Slider Row
        startY -= spacing;
        BuildSliderRow(parent, new Vector2(0f, startY), "Sound Effects (SFX)", out sfxSlider, out sfxValueText, (val) =>
        {
            GameAudioManager.Instance?.SetSFXVolume(val);
            if (sfxValueText != null) sfxValueText.text = Mathf.RoundToInt(val * 100f) + "%";
        });

        // 4. Player Name Info Row
        startY -= 62f;
        BuildPlayerInfoRow(parent, new Vector2(0f, startY));

        // 5. Exit / Logout Buttons
        startY -= 56f;
        BuildExitButtons(parent, new Vector2(0f, startY));
    }

    private void BuildMuteRow(RectTransform parent, Vector2 pos)
    {
        GameObject row = new GameObject("MuteRow", typeof(RectTransform), typeof(Image));
        row.transform.SetParent(parent, false);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 1f);
        rowRect.anchorMax = new Vector2(0.5f, 1f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = pos;
        rowRect.sizeDelta = new Vector2(430f, 46f);

        Image rowImg = row.GetComponent<Image>();
        rowImg.sprite = EconomyHUD.RoundedSprite;
        rowImg.type = Image.Type.Sliced;
        rowImg.color = RowBg;
        EconomyHUD.AddBlueBorder(rowRect, new Color(0.80f, 0.90f, 0.98f, 0.9f));

        // Label
        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(row.transform, false);
        RectTransform lblRect = labelObj.GetComponent<RectTransform>();
        lblRect.anchorMin = new Vector2(0f, 0.5f);
        lblRect.anchorMax = new Vector2(0f, 0.5f);
        lblRect.pivot = new Vector2(0f, 0.5f);
        lblRect.anchoredPosition = new Vector2(16f, 0f);
        lblRect.sizeDelta = new Vector2(250f, 30f);

        var lbl = labelObj.GetComponent<TextMeshProUGUI>();
        lbl.fontSize = 14.5f;
        lbl.fontStyle = FontStyles.Bold;
        lbl.color = ThemeDark;
        lbl.text = "Mute All Audio";

        // Checkbox button
        GameObject checkObj = new GameObject("MuteButton", typeof(RectTransform), typeof(Image), typeof(Button));
        checkObj.transform.SetParent(row.transform, false);
        RectTransform checkRect = checkObj.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(1f, 0.5f);
        checkRect.anchorMax = new Vector2(1f, 0.5f);
        checkRect.pivot = new Vector2(1f, 0.5f);
        checkRect.anchoredPosition = new Vector2(-16f, 0f);
        checkRect.sizeDelta = new Vector2(28f, 28f);

        Image checkImg = checkObj.GetComponent<Image>();
        checkImg.sprite = EconomyHUD.RoundedSprite;
        checkImg.type = Image.Type.Sliced;
        checkImg.color = Color.white;
        EconomyHUD.AddBlueBorder(checkRect, ThemeBlue);

        // Checkmark tick
        GameObject tickObj = new GameObject("Tick", typeof(RectTransform), typeof(Image));
        tickObj.transform.SetParent(checkObj.transform, false);
        RectTransform tickRect = tickObj.GetComponent<RectTransform>();
        tickRect.anchorMin = new Vector2(0.5f, 0.5f);
        tickRect.anchorMax = new Vector2(0.5f, 0.5f);
        tickRect.pivot = new Vector2(0.5f, 0.5f);
        tickRect.anchoredPosition = Vector2.zero;
        tickRect.sizeDelta = new Vector2(18f, 18f);

        muteCheckmark = tickObj.GetComponent<Image>();
        muteCheckmark.sprite = Resources.Load<Sprite>("UI_Panels/CheckmarkIcon") ?? EconomyHUD.RoundedSprite;
        muteCheckmark.color = ThemeBlue;
        muteCheckmark.raycastTarget = false;

        checkObj.GetComponent<Button>().onClick.AddListener(() =>
        {
            GameAudioManager.Instance?.ToggleMute();
            RefreshUIValues();
            GameAudioManager.Instance?.PlayClick();
        });
    }

    private void BuildSliderRow(RectTransform parent, Vector2 pos, string label, out Slider slider, out TextMeshProUGUI valText, System.Action<float> onValueChanged)
    {
        GameObject row = new GameObject("SliderRow_" + label, typeof(RectTransform), typeof(Image));
        row.transform.SetParent(parent, false);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 1f);
        rowRect.anchorMax = new Vector2(0.5f, 1f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = pos;
        rowRect.sizeDelta = new Vector2(430f, 50f);

        Image rowImg = row.GetComponent<Image>();
        rowImg.sprite = EconomyHUD.RoundedSprite;
        rowImg.type = Image.Type.Sliced;
        rowImg.color = RowBg;
        EconomyHUD.AddBlueBorder(rowRect, new Color(0.80f, 0.90f, 0.98f, 0.9f));

        // Label
        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(row.transform, false);
        RectTransform lblRect = labelObj.GetComponent<RectTransform>();
        lblRect.anchorMin = new Vector2(0f, 1f);
        lblRect.anchorMax = new Vector2(0f, 1f);
        lblRect.pivot = new Vector2(0f, 1f);
        lblRect.anchoredPosition = new Vector2(16f, -6f);
        lblRect.sizeDelta = new Vector2(200f, 20f);

        var lbl = labelObj.GetComponent<TextMeshProUGUI>();
        lbl.fontSize = 13.5f;
        lbl.fontStyle = FontStyles.Bold;
        lbl.color = ThemeDark;
        lbl.text = label;

        // Percentage Value Text
        GameObject valObj = new GameObject("ValueText", typeof(RectTransform), typeof(TextMeshProUGUI));
        valObj.transform.SetParent(row.transform, false);
        RectTransform valRect = valObj.GetComponent<RectTransform>();
        valRect.anchorMin = new Vector2(1f, 1f);
        valRect.anchorMax = new Vector2(1f, 1f);
        valRect.pivot = new Vector2(1f, 1f);
        valRect.anchoredPosition = new Vector2(-16f, -6f);
        valRect.sizeDelta = new Vector2(60f, 20f);

        valText = valObj.GetComponent<TextMeshProUGUI>();
        valText.fontSize = 13.5f;
        valText.fontStyle = FontStyles.Bold;
        valText.color = ThemeBlue;
        valText.alignment = TextAlignmentOptions.Right;
        valText.text = "100%";

        // Slider Object
        GameObject sliderObj = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderObj.transform.SetParent(row.transform, false);
        RectTransform sRect = sliderObj.GetComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0f, 0f);
        sRect.anchorMax = new Vector2(1f, 0f);
        sRect.pivot = new Vector2(0.5f, 0f);
        sRect.anchoredPosition = new Vector2(0f, 8f);
        sRect.sizeDelta = new Vector2(-32f, 16f);

        slider = sliderObj.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        // Background Track
        GameObject trackObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
        trackObj.transform.SetParent(sliderObj.transform, false);
        RectTransform trackRect = trackObj.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0f, 0.25f);
        trackRect.anchorMax = new Vector2(1f, 0.75f);
        trackRect.sizeDelta = Vector2.zero;

        Image trackImg = trackObj.GetComponent<Image>();
        trackImg.sprite = EconomyHUD.RoundedSprite;
        trackImg.type = Image.Type.Sliced;
        trackImg.color = new Color(0.82f, 0.88f, 0.94f, 1f);

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform faRect = fillArea.GetComponent<RectTransform>();
        faRect.anchorMin = new Vector2(0f, 0.25f);
        faRect.anchorMax = new Vector2(1f, 0.75f);
        faRect.sizeDelta = new Vector2(-14f, 0f);

        GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObj.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.sizeDelta = Vector2.zero;

        Image fillImg = fillObj.GetComponent<Image>();
        fillImg.sprite = EconomyHUD.RoundedSprite;
        fillImg.type = Image.Type.Sliced;
        fillImg.color = ThemeBlue;
        slider.fillRect = fillRect;

        // Handle Slide Area
        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderObj.transform, false);
        RectTransform haRect = handleArea.GetComponent<RectTransform>();
        haRect.anchorMin = Vector2.zero;
        haRect.anchorMax = Vector2.one;
        haRect.sizeDelta = new Vector2(-16f, 0f);

        GameObject handleObj = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObj.transform.SetParent(handleArea.transform, false);
        RectTransform hRect = handleObj.GetComponent<RectTransform>();
        hRect.sizeDelta = new Vector2(20f, 20f);

        Image hImg = handleObj.GetComponent<Image>();
        hImg.sprite = EconomyHUD.RoundedSprite;
        hImg.type = Image.Type.Sliced;
        hImg.color = Color.white;
        EconomyHUD.AddBlueBorder(hRect, ThemeBlue);
        slider.handleRect = hRect;
        slider.targetGraphic = hImg;

        slider.onValueChanged.AddListener((v) => onValueChanged?.Invoke(v));
    }

    private void BuildPlayerInfoRow(RectTransform parent, Vector2 pos)
    {
        GameObject row = new GameObject("PlayerInfoRow", typeof(RectTransform), typeof(Image));
        row.transform.SetParent(parent, false);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 1f);
        rowRect.anchorMax = new Vector2(0.5f, 1f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = pos;
        rowRect.sizeDelta = new Vector2(430f, 44f);

        Image rowImg = row.GetComponent<Image>();
        rowImg.sprite = EconomyHUD.RoundedSprite;
        rowImg.type = Image.Type.Sliced;
        rowImg.color = RowBg;
        EconomyHUD.AddBlueBorder(rowRect, new Color(0.80f, 0.90f, 0.98f, 0.9f));

        // Label: "Player Account"
        GameObject labelObj = new GameObject("PlayerLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(row.transform, false);
        RectTransform lblRect = labelObj.GetComponent<RectTransform>();
        lblRect.anchorMin = new Vector2(0f, 0.5f);
        lblRect.anchorMax = new Vector2(0f, 0.5f);
        lblRect.pivot = new Vector2(0f, 0.5f);
        lblRect.anchoredPosition = new Vector2(16f, 0f);
        lblRect.sizeDelta = new Vector2(140f, 26f);

        var lbl = labelObj.GetComponent<TextMeshProUGUI>();
        lbl.fontSize = 13.5f;
        lbl.fontStyle = FontStyles.Bold;
        lbl.color = new Color(0.35f, 0.45f, 0.55f, 1f);
        lbl.text = "Player Account:";

        // Player Name Value
        GameObject nameObj = new GameObject("PlayerName", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameObj.transform.SetParent(row.transform, false);
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(1f, 0.5f);
        nameRect.anchorMax = new Vector2(1f, 0.5f);
        nameRect.pivot = new Vector2(1f, 0.5f);
        nameRect.anchoredPosition = new Vector2(-16f, 0f);
        nameRect.sizeDelta = new Vector2(250f, 26f);

        playerNameText = nameObj.GetComponent<TextMeshProUGUI>();
        playerNameText.fontSize = 14.5f;
        playerNameText.fontStyle = FontStyles.Bold;
        playerNameText.color = ThemeDark;
        playerNameText.alignment = TextAlignmentOptions.Right;
        playerNameText.text = GetCurrentPlayerName();
    }

    private void BuildExitButtons(RectTransform parent, Vector2 pos)
    {
        float totalWidth = 430f;
        float btnWidth = (totalWidth - 14f) * 0.5f;
        float btnHeight = 44f;

        // Button 1: Return to Home / Main Menu
        GameObject homeBtnObj = new GameObject("HomeButton", typeof(RectTransform), typeof(Image), typeof(Button));
        homeBtnObj.transform.SetParent(parent, false);
        RectTransform homeRect = homeBtnObj.GetComponent<RectTransform>();
        homeRect.anchorMin = new Vector2(0.5f, 1f);
        homeRect.anchorMax = new Vector2(0.5f, 1f);
        homeRect.pivot = new Vector2(0.5f, 0.5f);
        homeRect.anchoredPosition = new Vector2(pos.x - (btnWidth * 0.5f + 7f), pos.y);
        homeRect.sizeDelta = new Vector2(btnWidth, btnHeight);

        Image homeImg = homeBtnObj.GetComponent<Image>();
        homeImg.sprite = EconomyHUD.RoundedSprite;
        homeImg.type = Image.Type.Sliced;
        homeImg.color = Color.white;
        EconomyHUD.AddBlueBorder(homeRect, ThemeBlue);

        GameObject homeTextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        homeTextObj.transform.SetParent(homeBtnObj.transform, false);
        RectTransform htRect = homeTextObj.GetComponent<RectTransform>();
        htRect.anchorMin = Vector2.zero;
        htRect.anchorMax = Vector2.one;
        htRect.sizeDelta = Vector2.zero;

        var ht = homeTextObj.GetComponent<TextMeshProUGUI>();
        ht.fontSize = 13.5f;
        ht.fontStyle = FontStyles.Bold;
        ht.color = ThemeDark;
        ht.alignment = TextAlignmentOptions.Center;
        ht.text = "Home (Main Menu)";

        homeBtnObj.GetComponent<Button>().onClick.AddListener(() =>
        {
            GameAudioManager.Instance?.PlayClick();
            ExitToHome();
        });

        // Button 2: Log Out / Login Screen
        GameObject logoutBtnObj = new GameObject("LogoutButton", typeof(RectTransform), typeof(Image), typeof(Button));
        logoutBtnObj.transform.SetParent(parent, false);
        RectTransform logoutRect = logoutBtnObj.GetComponent<RectTransform>();
        logoutRect.anchorMin = new Vector2(0.5f, 1f);
        logoutRect.anchorMax = new Vector2(0.5f, 1f);
        logoutRect.pivot = new Vector2(0.5f, 0.5f);
        logoutRect.anchoredPosition = new Vector2(pos.x + (btnWidth * 0.5f + 7f), pos.y);
        logoutRect.sizeDelta = new Vector2(btnWidth, btnHeight);

        Image logoutImg = logoutBtnObj.GetComponent<Image>();
        logoutImg.sprite = EconomyHUD.RoundedSprite;
        logoutImg.type = Image.Type.Sliced;
        logoutImg.color = Color.white;
        EconomyHUD.AddBlueBorder(logoutRect, DangerRed);

        GameObject logoutTextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        logoutTextObj.transform.SetParent(logoutBtnObj.transform, false);
        RectTransform ltRect = logoutTextObj.GetComponent<RectTransform>();
        ltRect.anchorMin = Vector2.zero;
        ltRect.anchorMax = Vector2.one;
        ltRect.sizeDelta = Vector2.zero;

        var lt = logoutTextObj.GetComponent<TextMeshProUGUI>();
        lt.fontSize = 13.5f;
        lt.fontStyle = FontStyles.Bold;
        lt.color = DangerRed;
        lt.alignment = TextAlignmentOptions.Center;
        lt.text = "Log Out (Login)";

        logoutBtnObj.GetComponent<Button>().onClick.AddListener(() =>
        {
            GameAudioManager.Instance?.PlayClick();
            ExitToLogin();
        });
    }

    private string GetCurrentPlayerName()
    {
        string name = AuthSession.DisplayName;
        if (string.IsNullOrEmpty(name)) name = AuthSession.Username;
        if (string.IsNullOrEmpty(name)) name = "Lumi Explorer";
        return name;
    }

    private void ExitToHome()
    {
        // Lưu tiến trình thế giới trước khi rời
        SaveBeforeExit();
        Close();

        string sceneToLoad = Application.CanStreamedLevelBeLoaded("MainMenu") ? "MainMenu" : "Login";
        SceneManager.LoadScene(sceneToLoad);
    }

    private void ExitToLogin()
    {
        // Lưu tiến trình thế giới và xóa session đăng nhập
        SaveBeforeExit();
        AuthSession.LogOut();
        Close();

        string sceneToLoad = Application.CanStreamedLevelBeLoaded("Login") ? "Login" : "MainMenu";
        SceneManager.LoadScene(sceneToLoad);
    }

    private void SaveBeforeExit()
    {
        var saveSystem = FindAnyObjectByType<WorldSaveSystem>();
        if (saveSystem != null)
        {
            saveSystem.SaveNow();
        }
    }

    private void RefreshUIValues()
    {
        var audio = GameAudioManager.Instance;
        if (audio != null)
        {
            if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(audio.BGMVolume);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(audio.SFXVolume);
            if (bgmValueText != null) bgmValueText.text = Mathf.RoundToInt(audio.BGMVolume * 100f) + "%";
            if (sfxValueText != null) sfxValueText.text = Mathf.RoundToInt(audio.SFXVolume * 100f) + "%";
            if (muteCheckmark != null) muteCheckmark.gameObject.SetActive(audio.IsMuted);
        }

        if (playerNameText != null)
        {
            playerNameText.text = GetCurrentPlayerName();
        }
    }
}
