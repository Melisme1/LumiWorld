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

    private void BuildAccountBar()
    {
        if (accountText != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
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
        if (settingsPanel == null)
            return;

        settingsPanel.SetActive(true);
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
