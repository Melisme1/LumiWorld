using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Login and registration screen.
///
/// Builds its entire UI at runtime, so the scene only needs this one component:
/// nothing to wire in the Inspector, nothing that breaks when the scene is edited.
/// Create the scene with Tools > LumiWorld > Set Up Login Scene.
/// </summary>
[DisallowMultipleComponent]
public class AuthScreen : MonoBehaviour
{
    [Header("Flow")]
    [Tooltip("Scene loaded after a successful login. Must be in Build Settings.")]
    [SerializeField] private string gameSceneName = "MapBuilding";

    [Tooltip("Go straight to the game when a saved, unexpired session exists. " +
             "Off while testing, so the login screen always shows.")]
    [SerializeField] private bool skipIfLoggedIn = false;

    [Tooltip("Show the server address field. Useful while testing against a " +
             "tunnel URL that changes every session; hide it for release.")]
    [SerializeField] private bool showServerField = true;

    // Mirrors the backend's rules. Catching mistakes here matters more than it
    // looks: the auth endpoints allow only 5 requests per minute.
    private static readonly Regex UsernamePattern = new Regex("^[A-Za-z0-9_]{3,20}$");
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 128;

    private const float PanelWidth = 600f;

    private static readonly Color BackgroundColor = new Color32(22, 48, 43, 255);
    private static readonly Color PanelColor = new Color32(243, 246, 239, 255);
    private static readonly Color FieldColor = new Color32(224, 233, 220, 255);
    private static readonly Color InkColor = new Color32(31, 58, 51, 255);
    private static readonly Color MutedColor = new Color32(104, 128, 119, 255);
    private static readonly Color AccentColor = new Color32(63, 125, 90, 255);
    private static readonly Color ErrorColor = new Color32(176, 72, 52, 255);

    private enum Mode
    {
        Login,
        Register
    }

    private Mode mode = Mode.Login;
    private bool busy;

    private Sprite roundedSprite;
    private TMP_DefaultControls.Resources uiResources;

    private TMP_Text subtitleText;
    private Button loginTab;
    private Button registerTab;
    private TMP_Text loginTabLabel;
    private TMP_Text registerTabLabel;
    private TMP_InputField usernameField;
    private TMP_InputField passwordField;
    private TMP_InputField confirmField;
    private GameObject confirmRow;
    private TMP_InputField serverField;
    private TMP_Text messageText;
    private Button submitButton;
    private TMP_Text submitLabel;

    // ------------------------------------------------------------------
    //  Lifecycle
    // ------------------------------------------------------------------

    private void Awake()
    {
        EnsureEventSystem();
        BuildUi();
    }

    private void Start()
    {
        if (skipIfLoggedIn && AuthSession.IsLoggedIn && CanLoadGameScene())
        {
            SceneManager.LoadScene(gameSceneName);
            return;
        }

        usernameField.text = AuthSession.Username;
        SetMode(Mode.Login);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || busy)
            return;

        if (keyboard.tabKey.wasPressedThisFrame)
            FocusNext(keyboard.shiftKey.isPressed ? -1 : 1);
        else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            Submit();
    }

    private void OnDestroy()
    {
        if (roundedSprite == null)
            return;

        Destroy(roundedSprite.texture);
        Destroy(roundedSprite);
    }

    // ------------------------------------------------------------------
    //  Submitting
    // ------------------------------------------------------------------

    private void Submit()
    {
        if (busy)
            return;

        string username = usernameField.text.Trim();
        string password = passwordField.text;

        string problem = Validate(username, password, confirmField.text);

        if (problem != null)
        {
            ShowMessage(problem, true);
            return;
        }

        if (serverField != null)
            ApiClient.BaseUrl = serverField.text;

        SetBusy(true);

        if (mode == Mode.Login)
        {
            ShowMessage(Texts.LoggingIn, false);

            var body = new LoginRequest { username = username, password = password };
            StartCoroutine(ApiClient.Post<LoginRequest, AuthResponse>(
                "/api/v1/auth/login", body, false, OnAuthSucceeded, OnAuthFailed));
        }
        else
        {
            ShowMessage(Texts.Registering, false);

            var body = new RegisterRequest
            {
                username = username,
                password = password,
                confirmPassword = confirmField.text
            };
            StartCoroutine(ApiClient.Post<RegisterRequest, AuthResponse>(
                "/api/v1/auth/register", body, false, OnAuthSucceeded, OnAuthFailed));
        }
    }

    private string Validate(string username, string password, string confirm)
    {
        if (mode == Mode.Login)
        {
            if (username.Length == 0)
                return Texts.UsernameRequired;

            if (password.Length == 0)
                return Texts.PasswordRequired;

            return null;
        }

        if (!UsernamePattern.IsMatch(username))
            return Texts.UsernameInvalid;

        if (password.Length < MinPasswordLength)
            return Texts.PasswordTooShort;

        if (password != confirm)
            return Texts.PasswordMismatch;

        return null;
    }

    private void OnAuthSucceeded(AuthResponse response)
    {
        // The request can outlive this screen; Unity's null check catches that.
        if (this == null)
            return;

        AuthSession.Save(response);
        ShowMessage(string.Format(Texts.Welcome, response.displayName), false);

        if (!CanLoadGameScene())
        {
            Debug.LogError(
                "AuthScreen: scene '" + gameSceneName + "' is not in Build Settings. " +
                "Run Tools > LumiWorld > Set Up Login Scene.",
                this
            );
            ShowMessage(Texts.GameSceneMissing, true);
            SetBusy(false);
            return;
        }

        SceneManager.LoadScene(gameSceneName);
    }

    private void OnAuthFailed(ApiError error)
    {
        if (this == null)
            return;

        Debug.LogWarning("AuthScreen: " + error, this);

        ShowMessage(Describe(error), true);
        SetBusy(false);

        if (error.Code == "INVALID_CREDENTIALS")
        {
            passwordField.text = "";
            Focus(passwordField);
        }
    }

    private static string Describe(ApiError error)
    {
        if (error.IsNetworkError)
        {
            return error.Detail.Contains("Insecure connection")
                ? Texts.InsecureHttpBlocked
                : Texts.CannotReachServer;
        }

        if (error.StatusCode == 429)
            return Texts.TooManyAttempts;

        // Branch on the stable code, never on the server's detail text.
        switch (error.Code)
        {
            case "USERNAME_TAKEN": return Texts.UsernameTaken;
            case "USERNAME_INVALID": return Texts.UsernameInvalid;
            case "PASSWORD_TOO_SHORT": return Texts.PasswordTooShort;
            case "PASSWORD_MISMATCH": return Texts.PasswordMismatch;
            case "INVALID_CREDENTIALS": return Texts.InvalidCredentials;
            case "ACCOUNT_SUSPENDED": return Texts.AccountSuspended;
        }

        if (error.StatusCode >= 500)
            return Texts.ServerError;

        return string.IsNullOrEmpty(error.Detail)
            ? string.Format(Texts.UnknownError, error.StatusCode)
            : error.Detail;
    }

    private bool CanLoadGameScene()
    {
        return Application.CanStreamedLevelBeLoaded(gameSceneName);
    }

    // ------------------------------------------------------------------
    //  State
    // ------------------------------------------------------------------

    private void SetMode(Mode newMode)
    {
        if (busy)
            return;

        mode = newMode;
        bool register = mode == Mode.Register;

        confirmRow.SetActive(register);
        confirmField.text = "";

        subtitleText.text = register ? Texts.RegisterSubtitle : Texts.LoginSubtitle;
        submitLabel.text = register ? Texts.RegisterButton : Texts.LoginButton;

        StyleTab(loginTab, loginTabLabel, !register);
        StyleTab(registerTab, registerTabLabel, register);

        ShowMessage("", false);
        Focus(usernameField.text.Length == 0 ? usernameField : passwordField);
    }

    private void SetBusy(bool value)
    {
        busy = value;

        submitButton.interactable = !value;
        loginTab.interactable = !value;
        registerTab.interactable = !value;
        usernameField.interactable = !value;
        passwordField.interactable = !value;
        confirmField.interactable = !value;

        if (serverField != null)
            serverField.interactable = !value;
    }

    private void ShowMessage(string message, bool isError)
    {
        messageText.text = message;
        messageText.color = isError ? ErrorColor : MutedColor;
    }

    private void FocusNext(int direction)
    {
        TMP_InputField[] order = mode == Mode.Register
            ? new[] { usernameField, passwordField, confirmField }
            : new[] { usernameField, passwordField };

        GameObject selected = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;

        int current = -1;

        for (int i = 0; i < order.Length; i++)
        {
            if (order[i].gameObject == selected)
            {
                current = i;
                break;
            }
        }

        int next = current < 0 ? 0 : (current + direction + order.Length) % order.Length;
        Focus(order[next]);
    }

    private static void Focus(TMP_InputField field)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(field.gameObject);

        field.ActivateInputField();
    }

    private static void StyleTab(Button tab, TMP_Text label, bool active)
    {
        // Inactive tabs match the panel, so they are invisible until hovered.
        tab.GetComponent<Image>().color = active ? FieldColor : PanelColor;
        label.color = active ? InkColor : MutedColor;
    }

    // ------------------------------------------------------------------
    //  Building the UI
    // ------------------------------------------------------------------

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        // This project uses the new Input System only (Player Settings > Active
        // Input Handling). The default StandaloneInputModule would throw every
        // frame, so use InputSystemUIInputModule, and give it actions explicitly:
        // one added from code otherwise starts with none and ignores clicks.
        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private void BuildUi()
    {
        roundedSprite = CreateRoundedSprite(16);
        uiResources = new TMP_DefaultControls.Resources
        {
            standard = roundedSprite,
            background = roundedSprite,
            inputField = roundedSprite
        };

        var canvasObject = new GameObject(
            "AuthCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        Image background = CreateImage("Background", canvasObject.transform, BackgroundColor, null);
        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Image panel = CreateImage("Panel", canvasObject.transform, PanelColor, roundedSprite);
        panel.type = Image.Type.Sliced;
        panel.pixelsPerUnitMultiplier = 0.55f;   // larger corners than the fields

        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(PanelWidth, 0f);

        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(56, 56, 48, 44);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Transform content = panel.transform;

        TMP_Text title = AddText(content, "LumiWorld", 52f, InkColor, FontStyles.Bold);
        title.alignment = TextAlignmentOptions.Center;

        subtitleText = AddText(content, "", 22f, MutedColor, FontStyles.Normal);
        subtitleText.alignment = TextAlignmentOptions.Center;

        var tabRow = new GameObject("Tabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        tabRow.transform.SetParent(content, false);
        var tabLayout = tabRow.GetComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 8f;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childForceExpandHeight = true;
        SetHeight(tabRow, 54f);

        loginTab = AddButton(tabRow.transform, Texts.LoginTab, PanelColor, MutedColor, 54f, out loginTabLabel);
        registerTab = AddButton(tabRow.transform, Texts.RegisterTab, PanelColor, MutedColor, 54f, out registerTabLabel);
        loginTab.onClick.AddListener(() => SetMode(Mode.Login));
        registerTab.onClick.AddListener(() => SetMode(Mode.Register));

        usernameField = AddLabeledField(content, Texts.UsernameLabel, Texts.UsernamePlaceholder, false, out _);
        usernameField.characterLimit = 20;
        usernameField.onValidateInput = (text, index, added) => IsUsernameCharacter(added) ? added : '\0';

        passwordField = AddLabeledField(content, Texts.PasswordLabel, Texts.PasswordPlaceholder, true, out _);
        confirmField = AddLabeledField(content, Texts.ConfirmLabel, Texts.ConfirmPlaceholder, true, out confirmRow);

        messageText = AddText(content, "", 20f, MutedColor, FontStyles.Normal);
        messageText.alignment = TextAlignmentOptions.Center;
        SetHeight(messageText.gameObject, 54f);   // reserve two lines so the panel does not jump

        submitButton = AddButton(content, "", AccentColor, Color.white, 64f, out submitLabel);
        submitButton.onClick.AddListener(Submit);

        if (showServerField)
        {
            serverField = AddLabeledField(content, Texts.ServerLabel, "http://localhost:8080", false, out _);
            serverField.pointSize = 18f;
            serverField.characterLimit = 200;
            serverField.text = ApiClient.BaseUrl;
            SetHeight(serverField.gameObject, 44f);
        }
    }

    private TMP_InputField AddLabeledField(
        Transform parent,
        string label,
        string placeholder,
        bool password,
        out GameObject row)
    {
        row = new GameObject(label, typeof(RectTransform), typeof(VerticalLayoutGroup));
        row.transform.SetParent(parent, false);

        var layout = row.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        AddText(row.transform, label, 19f, MutedColor, FontStyles.Bold);

        GameObject fieldObject = TMP_DefaultControls.CreateInputField(uiResources);
        fieldObject.name = label + " Field";
        fieldObject.transform.SetParent(row.transform, false);
        SetHeight(fieldObject, 60f);

        var image = fieldObject.GetComponent<Image>();
        image.color = FieldColor;
        image.type = Image.Type.Sliced;

        var field = fieldObject.GetComponent<TMP_InputField>();
        field.pointSize = 25f;
        field.textComponent.color = InkColor;
        field.textComponent.alignment = TextAlignmentOptions.MidlineLeft;

        var placeholderText = (TMP_Text)field.placeholder;
        placeholderText.text = placeholder;
        placeholderText.color = MutedColor;
        placeholderText.fontStyle = FontStyles.Normal;
        placeholderText.alignment = TextAlignmentOptions.MidlineLeft;

        RectTransform textArea = field.textViewport;
        textArea.offsetMin = new Vector2(18f, 4f);
        textArea.offsetMax = new Vector2(-18f, -4f);

        if (password)
        {
            field.contentType = TMP_InputField.ContentType.Password;
            field.asteriskChar = '•';
            field.characterLimit = MaxPasswordLength;
        }

        // Tab moves between fields; it must never be typed into one. The username
        // field replaces this with a stricter validator that also rejects tab.
        field.onValidateInput = (text, index, added) => added == '\t' ? '\0' : added;

        return field;
    }

    private Button AddButton(
        Transform parent,
        string label,
        Color background,
        Color textColor,
        float height,
        out TMP_Text labelText)
    {
        GameObject buttonObject = TMP_DefaultControls.CreateButton(uiResources);
        buttonObject.name = string.IsNullOrEmpty(label) ? "Submit Button" : label + " Button";
        buttonObject.transform.SetParent(parent, false);
        SetHeight(buttonObject, height);

        var image = buttonObject.GetComponent<Image>();
        image.color = background;
        image.type = Image.Type.Sliced;

        var button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.93f, 0.93f, 0.93f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.55f);
        button.colors = colors;

        labelText = buttonObject.GetComponentInChildren<TMP_Text>();
        labelText.text = label;
        labelText.fontSize = 25f;
        labelText.fontStyle = FontStyles.Bold;
        labelText.color = textColor;

        return button;
    }

    private TMP_Text AddText(Transform parent, string value, float size, Color color, FontStyles style)
    {
        GameObject textObject = TMP_DefaultControls.CreateText(uiResources);
        textObject.transform.SetParent(parent, false);

        var text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;

        return text;
    }

    private static Image CreateImage(string name, Transform parent, Color color, Sprite sprite)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        var image = imageObject.GetComponent<Image>();
        image.color = color;
        image.sprite = sprite;

        return image;
    }

    private static void SetHeight(GameObject target, float height)
    {
        // Explicit check rather than ??: Unity objects override == null, and ??
        // bypasses that override.
        if (!target.TryGetComponent(out LayoutElement element))
            element = target.AddComponent<LayoutElement>();

        element.minHeight = height;
        element.preferredHeight = height;
    }

    private static bool IsUsernameCharacter(char c)
    {
        return (c >= 'a' && c <= 'z') ||
               (c >= 'A' && c <= 'Z') ||
               (c >= '0' && c <= '9') ||
               c == '_';
    }

    /// <summary>
    /// A white square with anti-aliased rounded corners, 9-sliced so it
    /// stretches to any size. Generated here to avoid shipping a texture asset.
    /// </summary>
    private static Sprite CreateRoundedSprite(int radius)
    {
        int size = radius * 2 + 2;

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "AuthScreenRounded",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;

                // Distance from this pixel to the nearest corner centre; zero
                // along the straight edges.
                float dx = Mathf.Max(radius - px, Mathf.Max(0f, px - (size - radius)));
                float dy = Mathf.Max(radius - py, Mathf.Max(0f, py - (size - radius)));
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius)
        );
    }

    // ------------------------------------------------------------------
    //  Player-facing text, in one place so it can be localized later.
    // ------------------------------------------------------------------

    private static class Texts
    {
        public const string LoginTab = "Log In";
        public const string RegisterTab = "Register";
        public const string LoginSubtitle = "Welcome back";
        public const string RegisterSubtitle = "Create an account to begin your journey";

        public const string UsernameLabel = "Username";
        public const string UsernamePlaceholder = "3–20 letters, numbers or _";
        public const string PasswordLabel = "Password";
        public const string PasswordPlaceholder = "Enter your password";
        public const string ConfirmLabel = "Confirm password";
        public const string ConfirmPlaceholder = "Enter your password again";
        public const string ServerLabel = "Server";

        public const string LoginButton = "Log In";
        public const string RegisterButton = "Create Account";
        public const string LoggingIn = "Logging in…";
        public const string Registering = "Creating your account…";
        public const string Welcome = "Welcome, {0}!";

        public const string UsernameRequired = "Enter your username.";
        public const string PasswordRequired = "Enter your password.";
        public const string UsernameInvalid = "Usernames must be 3–20 characters, using only letters, numbers and underscores.";
        public const string PasswordTooShort = "Passwords must be at least 8 characters.";
        public const string PasswordMismatch = "The passwords don't match.";
        public const string UsernameTaken = "That username is already taken.";
        public const string InvalidCredentials = "Incorrect username or password.";
        public const string AccountSuspended = "This account can't be used right now.";

        public const string CannotReachServer = "Can't reach the server. Check the server address below.";
        public const string InsecureHttpBlocked = "Unity is blocking http:// connections. Use an https:// address instead.";
        public const string TooManyAttempts = "Too many attempts. Wait a minute, then try again.";
        public const string ServerError = "The server ran into a problem. Try again later.";
        public const string UnknownError = "Something went wrong (code {0}).";
        public const string GameSceneMissing = "You're logged in, but the game scene isn't in Build Settings yet.";
    }
}
