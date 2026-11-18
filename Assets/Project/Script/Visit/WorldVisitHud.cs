using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LumiWorld.Acs;

/// <summary>
/// World Visit HUD:
/// - Own island (MapBuilding): Top-right World Map button is handled by CollapsibleHUDToolbar; no old dark left-column WorldPanel.
///   Only shows a compact warm trade-offer pill if an incoming card trade offer is received.
/// - Visiting another island (VisitWorld): Warm P-00 panel showing island owner, score, coordinates, Listings, Trade cards, Back, and Home.
/// </summary>
public class WorldVisitHud : MonoBehaviour
{
    private const float ColumnX = 20f;
    private const float ColumnWidth = 336f;
    private const float PanelTop = 82f;
    private static readonly Vector2 RootOffset = Vector2.zero;

    private Canvas canvas;
    private float fittedCanvasScale;
    private Vector2 fittedCanvasSize;

    private RectTransform tradeAlertPill;
    private TextMeshProUGUI statusText;
    private ListingsWindow listings;
    private TradeOfferWindow tradeOffer;
    private TradeOffersWindow tradeOffers;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallAutoSpawn()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        SpawnIfWorldScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SpawnIfWorldScene();
    }

    private static void SpawnIfWorldScene()
    {
        if (FindAnyObjectByType<WorldVisitHud>(FindObjectsInactive.Include) != null) return;
        if (FindAnyObjectByType<WorldSaveSystem>() == null) return;

        RectTransform root = EconomyHUD.CreateRect("WorldVisitHud", FindScreenCanvas(SceneManager.GetActiveScene()));
        EconomyHUD.Stretch(root);
        root.gameObject.AddComponent<WorldVisitHud>();
    }

    private static bool MapAvailable => Application.CanStreamedLevelBeLoaded(WorldMapController.SceneName);

    private void Awake()
    {
        if (VisitSession.Active) BuildVisitPanel();
        else BuildOwnPanel();
    }

    private void LateUpdate()
    {
        FitToCanvas();
    }

    // =========================================================
    // OWN ISLAND: NO OLD WORLD MAP BOX (Moved to Top-Right HUD)
    // =========================================================

    private void BuildOwnPanel()
    {
        // Compact trade-offer notification pill (hidden until an incoming offer arrives)
        tradeAlertPill = EconomyHUD.CreateRect("TradeOfferAlertPill", transform);
        SetTopLeft(tradeAlertPill, new Vector2(ColumnX, -PanelTop), new Vector2(260f, 42f));

        Image pillBg = tradeAlertPill.gameObject.AddComponent<Image>();
        pillBg.sprite = LumiWorldUITheme.PillSprite;
        pillBg.type = Image.Type.Sliced;
        pillBg.color = LumiWorldUITheme.Surface;
        LumiWorldUITheme.AddSoftElevation(tradeAlertPill.gameObject, 3f, 0.18f);

        Button btn = tradeAlertPill.gameObject.AddComponent<Button>();
        btn.targetGraphic = pillBg;
        btn.onClick.AddListener(() =>
        {
            tradeAlertPill.gameObject.SetActive(false);
            tradeOffers = TradeOffersWindow.Open(tradeOffers, transform);
        });

        statusText = EconomyHUD.CreateText("Status", tradeAlertPill, 13.5f, LumiWorldUITheme.Text, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(statusText.rectTransform);
        statusText.font = LumiWorldUITheme.Body ?? statusText.font;
        statusText.fontStyle = FontStyles.Bold;
        statusText.text = "Trade offers";

        tradeAlertPill.gameObject.SetActive(false);
        TradeSync.OfferReceived += HandleOfferReceived;
    }

    private void OnDestroy()
    {
        TradeSync.OfferReceived -= HandleOfferReceived;
    }

    private void HandleOfferReceived(string fromName)
    {
        if (tradeAlertPill != null)
        {
            tradeAlertPill.gameObject.SetActive(true);
        }
        if (statusText != null)
        {
            statusText.text = "Trade offer from " + fromName + " · View";
        }
        if (tradeOffers != null && tradeOffers.IsOpen)
        {
            tradeOffers.Refresh();
        }
    }

    // =========================================================
    // VISITING ANOTHER PLAYER'S ISLAND (Warm P-00 Theme)
    // =========================================================

    private void BuildVisitPanel()
    {
        RectTransform panel = CreatePanel(new Vector2(ColumnWidth, 248f));

        TextMeshProUGUI title = EconomyHUD.CreateText("Title", panel, 20f, LumiWorldUITheme.Text, TextAlignmentOptions.Left);
        SetTopLeft(title.rectTransform, new Vector2(18f, -14f), new Vector2(ColumnWidth - 36f, 28f));
        title.font = LumiWorldUITheme.Heading ?? title.font;
        title.fontStyle = FontStyles.Bold;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Ellipsis;
        title.text = VisitSession.OwnerName + "'s island";

        TextMeshProUGUI line = CreateLine(panel, new Vector2(18f, -44f), new Vector2(ColumnWidth - 36f, 44f));
        line.text = "Harmony " + VisitSession.OwnerScore + "  ·  [" + VisitSession.OwnerX + ":" + VisitSession.OwnerY + "]"
            + "\nVisiting island mode";

        float half = (ColumnWidth - 44f) / 2f;
        bool backToMap = VisitSession.FromWorldMap && MapAvailable;
        AddButton(panel, backToMap ? "Back to map" : "Back", new Vector2(16f, -96f), new Vector2(half, 40f), LumiWorldUITheme.Inset, LumiWorldUITheme.Text, VisitSession.Leave);
        AddButton(panel, "Home", new Vector2(28f + half, -96f), new Vector2(half, 40f), LumiWorldUITheme.Primary, Color.white, GoHome);
        AddButton(panel, "Listings", new Vector2(16f, -144f), new Vector2(ColumnWidth - 32f, 40f), LumiWorldUITheme.Surface, LumiWorldUITheme.Text,
            () => listings = ListingsWindow.Open(listings, transform, VisitSession.OwnerId, VisitSession.OwnerName));
        AddButton(panel, "Trade cards", new Vector2(16f, -192f), new Vector2(ColumnWidth - 32f, 40f), LumiWorldUITheme.Primary, Color.white,
            () => tradeOffer = TradeOfferWindow.Open(tradeOffer, transform, VisitSession.OwnerId, VisitSession.OwnerName));
    }

    private static void GoHome()
    {
        VisitSession.End();
        SceneManager.LoadScene(VisitSession.OwnScene);
    }

    private RectTransform CreatePanel(Vector2 size)
    {
        RectTransform panel = EconomyHUD.CreateRect("WorldPanel", transform);
        SetTopLeft(panel, new Vector2(ColumnX, -PanelTop), size);
        Image bg = panel.gameObject.AddComponent<Image>();
        bg.sprite = LumiWorldUITheme.WindowSprite;
        bg.type = Image.Type.Sliced;
        bg.color = LumiWorldUITheme.Window;
        bg.raycastTarget = true;
        LumiWorldUITheme.AddSoftElevation(panel.gameObject, 4f, 0.20f);
        return panel;
    }

    private static TextMeshProUGUI CreateLine(RectTransform parent, Vector2 position, Vector2 size)
    {
        TextMeshProUGUI line = EconomyHUD.CreateText("Line", parent, 14f, LumiWorldUITheme.Muted, TextAlignmentOptions.Left);
        SetTopLeft(line.rectTransform, position, size);
        line.font = LumiWorldUITheme.Body ?? line.font;
        line.textWrappingMode = TextWrappingModes.Normal;
        return line;
    }

    private static Button AddButton(RectTransform parent, string text, Vector2 position, Vector2 size, Color bgColor, Color textColor, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = EconomyHUD.CreateRect(text + "Button", parent);
        SetTopLeft(rect, position, size);

        Image img = rect.gameObject.AddComponent<Image>();
        img.sprite = LumiWorldUITheme.SurfaceSprite;
        img.type = Image.Type.Sliced;
        img.color = bgColor;
        img.raycastTarget = true;
        LumiWorldUITheme.AddSoftElevation(rect.gameObject, 2f, 0.14f);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.onClick.AddListener(onClick);

        TextMeshProUGUI label = EconomyHUD.CreateText("Label", rect, 15f, textColor, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(label.rectTransform);
        label.font = LumiWorldUITheme.Body ?? label.font;
        label.fontStyle = FontStyles.Bold;
        label.text = text;
        return button;
    }

    private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void FitToCanvas()
    {
        if (canvas == null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null) return;
            canvas = parentCanvas.rootCanvas;
        }

        float canvasScale = canvas.scaleFactor;
        Vector2 canvasSize = ((RectTransform)canvas.transform).rect.size;
        if (canvasScale <= 0f || canvasSize.x <= 0f || canvasSize.y <= 0f) return;
        if (canvasScale == fittedCanvasScale && canvasSize == fittedCanvasSize) return;

        fittedCanvasScale = canvasScale;
        fittedCanvasSize = canvasSize;

        RectTransform root = (RectTransform)transform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.zero;
        root.pivot = Vector2.zero;
        root.anchoredPosition = RootOffset;
        root.sizeDelta = canvasSize * canvasScale;
        root.localScale = new Vector3(1f / canvasScale, 1f / canvasScale, 1f);
    }

    private static Transform FindScreenCanvas(Scene scene)
    {
        foreach (Canvas c in FindObjectsByType<Canvas>())
        {
            if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace && c.gameObject.scene == scene)
                return c.transform;
        }

        GameObject canvasObject = new GameObject("WorldVisitCanvas", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);

        Canvas ownCanvas = canvasObject.AddComponent<Canvas>();
        ownCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        ownCanvas.sortingOrder = 45;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
        return canvasObject.transform;
    }
}
