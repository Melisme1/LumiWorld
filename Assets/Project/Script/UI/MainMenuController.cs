using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("UI Panels")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Buttons")]
    [SerializeField] private Button continueButton;

    // Scene đăng nhập, phải có trong Build Settings (xem Tools > LumiWorld > Set Up Login Scene)
    private const string LoginSceneName = "Login";

    private static readonly Color32 AccentColor = new Color32(63, 125, 90, 255);

    // Dòng trạng thái tài khoản và nút Log In / Log Out, tạo lúc chạy để không phải sửa scene
    private TextMeshProUGUI accountText;
    private TextMeshProUGUI accountButtonLabel;
    private Button accountButton;

    // Server vừa từ chối token (401), nên dòng trạng thái báo hết phiên
    private bool sessionRejected;


    private void OnEnable()
    {
        ApiClient.SessionExpired += HandleSessionExpired;
    }

    private void OnDisable()
    {
        ApiClient.SessionExpired -= HandleSessionExpired;
    }

    private void Start()
    {
        InitializeMenu();
    }


    private void InitializeMenu()
    {
        // Hide Settings Panel when starting
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        BuildAccountBar();
        RefreshAccountBar();

        // Continue resumes the current save; New Game replaces it after confirmation.
        if (continueButton != null) continueButton.gameObject.SetActive(true);

        ApplyMenuUIRedesign();
    }


    // ========================================
    // PLAY
    // ========================================

    private bool startingGame;

    /// <summary>
    /// Nút Continue. Chưa đăng nhập thì mở màn đăng nhập, đăng nhập xong quay về đây.
    /// Đã đăng nhập thì tải save mới nhất từ server (nếu lấy được), rồi vào MapBuilding:
    /// có save thì dựng lại đảo cũ, chưa có thì sinh đảo mới (WorldSaveSystem).
    /// </summary>
    public void OnPlay()
    {
        if (!AuthSession.IsLoggedIn)
        {
            Debug.Log("Chưa đăng nhập, mở màn đăng nhập.");
            SceneManager.LoadScene(LoginSceneName);
            return;
        }

        if (startingGame) return;
        startingGame = true;
        if (accountText != null) Destroy(accountText.gameObject);
        if (accountButton != null) Destroy(accountButton.gameObject);
        // Rời đảo người khác bằng đường khác ngoài Back home (về menu) thì vẫn vào đảo của mình
        if (VisitSession.Active) VisitSession.End();
        StartCoroutine(SaveSync.PullBeforePlay(() => SceneManager.LoadScene(gameSceneName)));
    }

    public void OnNewGame()
    {
        if (!AuthSession.IsLoggedIn)
        {
            SceneManager.LoadScene(LoginSceneName);
            return;
        }
        if (startingGame) return;
        GameDialogUI.ShowConfirm(
            "Creating a new world will overwrite your account's current world and inventory. Do you want to continue?",
            () => StartCoroutine(CreateNewGame()),
            confirmText: "NEW GAME", cancelText: "KEEP WORLD", title: "NEW GAME");
    }

    private System.Collections.IEnumerator CreateNewGame()
    {
        if (startingGame) yield break;
        startingGame = true;
        if (VisitSession.Active) VisitSession.End();
        bool created = false;
        string error = null;
        yield return SaveSync.StartNewGame((success, message) => { created = success; error = message; });
        if (!created)
        {
            startingGame = false;
            GameDialogUI.ShowNotice(error ?? "Failed to create new world.");
            yield break;
        }
        // Unity's Destroy is deferred; let old persistent economy objects disappear first.
        yield return null;
        if (accountText != null) Destroy(accountText.gameObject);
        if (accountButton != null) Destroy(accountButton.gameObject);
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnContinue() => OnPlay();


    // ========================================
    // ACCOUNT
    // ========================================

    private void OnAccountButton()
    {
        if (AuthSession.IsLoggedIn)
        {
            AuthSession.LogOut();
            sessionRejected = false;
            RefreshAccountBar();
            return;
        }

        SceneManager.LoadScene(LoginSceneName);
    }

    private void HandleSessionExpired()
    {
        sessionRejected = true;
        RefreshAccountBar();
    }

    private GameObject _accountPillGo;
    private TMPro.TextMeshProUGUI _accountPillNameText;
    private TMPro.TextMeshProUGUI _accountPillActionText;
    private UnityEngine.UI.Image _accountPillActionBg;
    private UnityEngine.UI.Image _continueBg;
    private UnityEngine.UI.Image _continueGlyph;
    private TMPro.TMP_Text _continueTmp;

    private void OnDestroy()
    {
        AuthSession.AccountChanging -= HandleAuthChanging;
        if (accountText != null) Destroy(accountText.gameObject);
        if (accountButton != null) Destroy(accountButton.gameObject);
        if (_accountPillGo != null) Destroy(_accountPillGo);
    }

    private void ApplyMenuUIRedesign()
    {
        AuthSession.AccountChanging -= HandleAuthChanging;
        AuthSession.AccountChanging += HandleAuthChanging;

        RestyleSceneButtons();
        BuildOrUpdateAccountPill();
    }

    private void HandleAuthChanging()
    {
        if (this != null && gameObject.activeInHierarchy)
            Invoke(nameof(RefreshRedesignedVisualsDelayed), 0.05f);
    }

    private void RefreshRedesignedVisualsDelayed()
    {
        RefreshContinueVisual();
        BuildOrUpdateAccountPill();
    }

    private void RestyleSceneButtons()
    {
        Canvas canvas = FindMainCanvas();
        if (canvas == null)
            return;

        UnityEngine.UI.Button continueBtn = continueButton != null ? continueButton : FindButtonInChildren(canvas.transform, "Continue");
        UnityEngine.UI.Button newGameBtn = FindButtonInChildren(canvas.transform, "NewGame");
        UnityEngine.UI.Button settingBtn = FindButtonInChildren(canvas.transform, "Setting");
        UnityEngine.UI.Button quitBtn = FindButtonInChildren(canvas.transform, "Quit");

        UnityEngine.UI.Button[] ordered = { continueBtn, newGameBtn, settingBtn, quitBtn };
        float topY = float.MinValue;
        float colX = 0f;
        int validCount = 0;

        for (int i = 0; i < ordered.Length; i++)
        {
            if (ordered[i] == null) continue;
            RectTransform rt = ordered[i].GetComponent<RectTransform>();
            if (rt != null)
            {
                if (rt.anchoredPosition.y > topY) topY = rt.anchoredPosition.y;
                colX += rt.anchoredPosition.x;
                validCount++;
            }
        }

        if (validCount > 0)
            colX /= validCount;
        else
            topY = 60f;

        float pitch = 76f;
        if (continueBtn != null)
            RestyleMenuButton(continueBtn, "Continue", "G-32", colX, topY, AuthSession.IsLoggedIn, out _continueBg, out _continueGlyph, out _continueTmp);
        if (newGameBtn != null)
            RestyleMenuButton(newGameBtn, "New Game", "G-33", colX, topY - pitch, false, out _, out _, out _);
        if (settingBtn != null)
        {
            RestyleMenuButton(settingBtn, "Settings", "G-09", colX, topY - pitch * 2f, false, out _, out _, out _);
            settingBtn.onClick.RemoveListener(OnSettings);
            settingBtn.onClick.AddListener(OnSettings);
        }
        if (quitBtn != null)
            RestyleMenuButton(quitBtn, "Quit", "G-31", colX, topY - pitch * 3f, false, out _, out _, out _);
    }

    private static void RestyleMenuButton(
        UnityEngine.UI.Button btn,
        string labelText,
        string glyphId,
        float posX,
        float posY,
        bool isDarkPrimary,
        out UnityEngine.UI.Image bgImg,
        out UnityEngine.UI.Image glyphImg,
        out TMPro.TMP_Text labelTmp)
    {
        bgImg = null;
        glyphImg = null;
        labelTmp = null;

        if (btn == null)
            return;

        RectTransform rt = btn.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.sizeDelta = new Vector2(300f, 62f);
            rt.anchoredPosition = new Vector2(posX, posY);
        }

        // Subtle pill border (also supplies the crisp 9-sliced rounded sprite to the button background)
        Transform existingBorder = btn.transform.Find("PillBorderOverlay");
        UnityEngine.UI.Image borderOverlayImg = null;
        if (existingBorder == null)
        {
            borderOverlayImg = LumiWorld.Acs.LumiWorldUITheme.CreateRoundedSurface(
                btn.transform, "PillBorderOverlay", new Color(0f, 0f, 0f, 0f), LumiWorld.Acs.LumiWorldUITheme.Edge,
                radius: 18, borderThickness: 1.4f, shadowDepth: 0f, shadowAlpha: 0f, raycastTarget: false);
            LumiWorld.Acs.LumiWorldUITheme.Stretch(borderOverlayImg.rectTransform);
        }
        else
        {
            borderOverlayImg = existingBorder.GetComponent<UnityEngine.UI.Image>();
        }

        bgImg = btn.GetComponent<UnityEngine.UI.Image>();
        if (bgImg != null)
        {
            if (borderOverlayImg != null && borderOverlayImg.sprite != null)
            {
                bgImg.sprite = borderOverlayImg.sprite;
                bgImg.type = UnityEngine.UI.Image.Type.Sliced;
            }
            bgImg.color = isDarkPrimary ? LumiWorld.Acs.LumiWorldUITheme.Primary : LumiWorld.Acs.LumiWorldUITheme.Surface;
        }
        if (borderOverlayImg != null)
            borderOverlayImg.enabled = false;

        // Disable legacy decorative graphics / legacy Text on children
        UnityEngine.UI.Image[] childImgs = btn.GetComponentsInChildren<UnityEngine.UI.Image>(true);
        for (int i = 0; i < childImgs.Length; i++)
        {
            if (childImgs[i] != bgImg && childImgs[i].gameObject.name != "MenuGlyphIcon" && childImgs[i].gameObject.name != "PillBorderOverlay")
                childImgs[i].enabled = false;
        }

        UnityEngine.UI.Text[] legacyTexts = btn.GetComponentsInChildren<UnityEngine.UI.Text>(true);
        for (int i = 0; i < legacyTexts.Length; i++)
            legacyTexts[i].enabled = false;

        UnityEngine.UI.Shadow shadow = btn.GetComponent<UnityEngine.UI.Shadow>();
        if (shadow == null) shadow = btn.gameObject.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = new Color(0.16f, 0.13f, 0.09f, 0.18f);
        shadow.effectDistance = new Vector2(0f, -3f);

        UnityEngine.UI.ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.95f, 0.94f, 0.92f, 1f);
        cb.pressedColor = new Color(0.88f, 0.86f, 0.83f, 1f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(0.76f, 0.74f, 0.71f, 1f);
        cb.colorMultiplier = 1f;
        btn.colors = cb;
        btn.transition = UnityEngine.UI.Selectable.Transition.ColorTint;

        Color fg = isDarkPrimary ? Color.white : LumiWorld.Acs.LumiWorldUITheme.Text;

        // Glyph
        Transform existingGlyph = btn.transform.Find("MenuGlyphIcon");
        if (existingGlyph == null)
        {
            GameObject gGo = new GameObject("MenuGlyphIcon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            gGo.transform.SetParent(btn.transform, false);
            existingGlyph = gGo.transform;
        }
        RectTransform gRt = existingGlyph as RectTransform;
        if (gRt != null)
        {
            gRt.anchorMin = new Vector2(0f, 0.5f);
            gRt.anchorMax = new Vector2(0f, 0.5f);
            gRt.pivot = new Vector2(0.5f, 0.5f);
            gRt.anchoredPosition = new Vector2(36f, 0f);
            gRt.sizeDelta = new Vector2(22f, 22f);
        }
        glyphImg = existingGlyph.GetComponent<UnityEngine.UI.Image>();
        glyphImg.sprite = LumiWorld.Acs.LumiWorldUITheme.Glyph(glyphId);
        glyphImg.color = fg;
        glyphImg.preserveAspect = true;
        glyphImg.raycastTarget = false;
        glyphImg.enabled = glyphImg.sprite != null;

        // TMP Label
        labelTmp = btn.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (labelTmp == null)
        {
            labelTmp = LumiWorld.Acs.LumiWorldUITheme.CreateLabel(
                btn.transform, "Label", labelText, 18f, fg, headingFont: true, align: TMPro.TextAlignmentOptions.MidlineLeft);
        }
        labelTmp.text = labelText;
        if (LumiWorld.Acs.LumiWorldUITheme.Heading != null)
            labelTmp.font = LumiWorld.Acs.LumiWorldUITheme.Heading;
        labelTmp.fontSize = 18f;
        labelTmp.color = fg;
        labelTmp.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
        labelTmp.raycastTarget = false;
        RectTransform lRt = labelTmp.rectTransform;
        lRt.anchorMin = Vector2.zero;
        lRt.anchorMax = Vector2.one;
        lRt.offsetMin = new Vector2(62f, 0f);
        lRt.offsetMax = new Vector2(-18f, 0f);

        if (btn.GetComponent<DialogButtonJuice>() == null)
            btn.gameObject.AddComponent<DialogButtonJuice>();
    }

    private void RefreshContinueVisual()
    {
        bool loggedIn = AuthSession.IsLoggedIn;
        if (_continueBg != null)
            _continueBg.color = loggedIn ? LumiWorld.Acs.LumiWorldUITheme.Primary : LumiWorld.Acs.LumiWorldUITheme.Surface;
        Color fg = loggedIn ? Color.white : LumiWorld.Acs.LumiWorldUITheme.Text;
        if (_continueGlyph != null) _continueGlyph.color = fg;
        if (_continueTmp != null) _continueTmp.color = fg;
    }

    private void BuildOrUpdateAccountPill()
    {
        HideLegacyAccountBar(accountText != null ? accountText.gameObject : null);
        HideLegacyAccountBar(accountButton != null ? accountButton.gameObject : null);

        Canvas canvas = FindMainCanvas();
        if (canvas == null)
            return;

        if (_accountPillGo == null)
        {
            UnityEngine.UI.Image pillImg = LumiWorld.Acs.LumiWorldUITheme.CreateRoundedSurface(
                canvas.transform, "MainMenuAccountPill",
                LumiWorld.Acs.LumiWorldUITheme.Surface, LumiWorld.Acs.LumiWorldUITheme.Edge,
                radius: 22, borderThickness: 1.5f, shadowDepth: 3f, shadowAlpha: 0.16f, raycastTarget: true);
            _accountPillGo = pillImg.gameObject;
            LumiWorld.Acs.LumiWorldUITheme.PlaceTopRight(_accountPillGo.GetComponent<RectTransform>(), 24f, 20f, 272f, 44f);

            GameObject gGo = new GameObject("AccountGlyph", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            gGo.transform.SetParent(_accountPillGo.transform, false);
            LumiWorld.Acs.LumiWorldUITheme.PlaceTopLeft(gGo.GetComponent<RectTransform>(), 14f, 12f, 20f, 20f);
            UnityEngine.UI.Image gImg = gGo.GetComponent<UnityEngine.UI.Image>();
            gImg.sprite = LumiWorld.Acs.LumiWorldUITheme.Glyph("G-29");
            gImg.color = LumiWorld.Acs.LumiWorldUITheme.Text;
            gImg.preserveAspect = true;
            gImg.enabled = gImg.sprite != null;

            _accountPillNameText = LumiWorld.Acs.LumiWorldUITheme.CreateLabel(
                _accountPillGo.transform, "AccountName", "Guest", 14f, LumiWorld.Acs.LumiWorldUITheme.Text,
                headingFont: true, align: TMPro.TextAlignmentOptions.MidlineLeft);
            LumiWorld.Acs.LumiWorldUITheme.PlaceTopLeft(_accountPillNameText.rectTransform, 42f, 6f, 132f, 32f);

            UnityEngine.UI.Button actionBtn = LumiWorld.Acs.LumiWorldUITheme.CreatePillButton(
                _accountPillGo.transform, "AccountActionBtn", "Log In", null, false, 13f,
                OnAccountPillClicked, out _accountPillActionBg, out _, out _accountPillActionText, radius: 14);
            LumiWorld.Acs.LumiWorldUITheme.PlaceTopRight(actionBtn.GetComponent<RectTransform>(), 6f, 6f, 88f, 32f);
            actionBtn.gameObject.AddComponent<DialogButtonJuice>();
        }

        _accountPillGo.transform.SetAsLastSibling();

        bool loggedIn = AuthSession.IsLoggedIn;
        if (_accountPillNameText != null)
        {
            string playerName = loggedIn
                ? (!string.IsNullOrEmpty(AuthSession.DisplayName) ? AuthSession.DisplayName : AuthSession.Username)
                : "Guest";
            if (string.IsNullOrEmpty(playerName)) playerName = "Player";
            _accountPillNameText.text = playerName;
            _accountPillNameText.color = LumiWorld.Acs.LumiWorldUITheme.Text;
        }

        if (_accountPillActionBg != null)
            _accountPillActionBg.color = loggedIn ? LumiWorld.Acs.LumiWorldUITheme.Inset : LumiWorld.Acs.LumiWorldUITheme.Primary;
        if (_accountPillActionText != null)
        {
            _accountPillActionText.text = loggedIn ? "Log Out" : "Log In";
            _accountPillActionText.color = loggedIn ? LumiWorld.Acs.LumiWorldUITheme.Text : Color.white;
        }
    }

    private void OnAccountPillClicked()
    {
        if (accountButton != null && accountButton.onClick != null)
        {
            accountButton.onClick.Invoke();
            RefreshRedesignedVisualsDelayed();
            return;
        }

        if (AuthSession.IsLoggedIn)
        {
            AuthSession.LogOut();
            RefreshAccountBar();
            RefreshRedesignedVisualsDelayed();
        }
        else if (Application.CanStreamedLevelBeLoaded("Login"))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Login");
        }
    }

    private Canvas FindMainCanvas()
    {
        if (continueButton != null)
        {
            Canvas c = continueButton.GetComponentInParent<Canvas>();
            if (c != null) return c;
        }
        return FindAnyObjectByType<Canvas>();
    }

    private static UnityEngine.UI.Button FindButtonInChildren(Transform root, string objectName)
    {
        if (root == null) return null;
        UnityEngine.UI.Button[] all = root.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (string.Equals(all[i].gameObject.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                return all[i];
        }
        return null;
    }

    private static void HideLegacyAccountBar(GameObject go)
    {
        if (go == null) return;
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    private void BuildAccountBar()
    {
        if (accountText != null) return;

        // Ưu tiên tìm Canvas thuộc Scene MainMenu để tránh gán nhầm vào Canvas DontDestroyOnLoad (GameDialogUI)
        Canvas canvas = null;
        var rootObjects = gameObject.scene.GetRootGameObjects();
        foreach (var root in rootObjects)
        {
            canvas = root.GetComponentInChildren<Canvas>(true);
            if (canvas != null) break;
        }
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning("MainMenuController: không có Canvas, không hiện được dòng tài khoản.");
            return;
        }

        var textObject = new GameObject("AccountText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvas.transform, false);
        accountText = textObject.GetComponent<TextMeshProUGUI>();
        accountText.fontSize = 26;
        accountText.alignment = TextAlignmentOptions.TopRight;
        accountText.color = Color.white;
        PlaceTopRight((RectTransform)textObject.transform, new Vector2(-24f, -24f), new Vector2(480f, 40f));

        var buttonObject = new GameObject("AccountButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(canvas.transform, false);
        var image = buttonObject.GetComponent<Image>();
        image.color = AccentColor;
        accountButton = buttonObject.GetComponent<Button>();
        accountButton.targetGraphic = image;
        accountButton.onClick.AddListener(OnAccountButton);
        PlaceTopRight((RectTransform)buttonObject.transform, new Vector2(-24f, -76f), new Vector2(180f, 44f));

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        accountButtonLabel = labelObject.GetComponent<TextMeshProUGUI>();
        accountButtonLabel.fontSize = 22;
        accountButtonLabel.alignment = TextAlignmentOptions.Center;
        accountButtonLabel.color = Color.white;
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    private static void PlaceTopRight(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void RefreshAccountBar()
    {
        if (accountText == null) return;

        if (AuthSession.IsLoggedIn)
        {
            accountText.text = "Logged in as " + AuthSession.DisplayName;
            accountButtonLabel.text = "Log Out";
        }
        else if (sessionRejected || !string.IsNullOrEmpty(AuthSession.AccessToken))
        {
            accountText.text = "Session expired. Log in again.";
            accountButtonLabel.text = "Log In";
        }
        else
        {
            accountText.text = "Not logged in";
            accountButtonLabel.text = "Log In";
        }
    }


    // ========================================
    // SETTINGS
    // ========================================

    public void OnSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        GameSettingsUI.OpenOrCreate(isMainMenu: true);
    }


    public void OnCloseSettings()
    {
        if (settingsPanel == null)
            return;

        settingsPanel.SetActive(false);
    }


    // ========================================
    // QUIT
    // ========================================

    public void OnQuit()
    {
        Debug.Log("Quit Game");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }


}
