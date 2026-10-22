using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Phần thế giới trên màn chơi. Bản đồ, ô tìm kiếm, Top islands và Share đã chuyển sang scene WorldMap,
/// ở đây chỉ còn:
/// - Đảo của mình (MapBuilding): nút World map, lưu đảo rồi mở scene WorldMap.
/// - Đang thăm (VisitWorld): tên chủ đảo, điểm, toạ độ, Listings, Back (về WorldMap đúng đảo vừa thăm) và Home.
/// Tự tạo khi vào scene có WorldSaveSystem. Chữ hiện ra là tiếng Anh.
/// </summary>
public class WorldVisitHud : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.13f, 0.92f);
    private static readonly Color AccentColor = new Color(0.22f, 0.55f, 0.95f, 1f);
    private static readonly Color DangerColor = new Color(0.78f, 0.32f, 0.30f, 1f);
    private static readonly Color NeutralColor = new Color(0.26f, 0.30f, 0.38f, 1f);
    private static readonly Color MutedColor = new Color(0.72f, 0.76f, 0.84f, 1f);
    private static readonly Color TextColor = Color.white;

    // Cột trái nằm dưới thanh tài nguyên (InventoryUI ở (20, 20), cao 48)
    private const float ColumnX = 20f;
    private const float ColumnWidth = 360f;
    private const float PanelTop = 86f;

    // Phóng theo Scale Factor của Canvas như EconomyHUD: 1 đơn vị = 1 pixel màn hình
    private Canvas canvas;
    private float fittedCanvasScale;
    private Vector2 fittedCanvasSize;

    private TextMeshProUGUI statusText;
    private ListingsWindow listings;

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
    // ĐẢO CỦA MÌNH: CHỈ CÒN NÚT WORLD MAP
    // =========================================================

    private void BuildOwnPanel()
    {
        RectTransform panel = CreatePanel(new Vector2(ColumnWidth, 112f));

        AddButton(panel, "World map", new Vector2(12f, -12f), new Vector2(ColumnWidth - 24f, 44f), AccentColor, OpenWorldMap);

        statusText = CreateLine(panel, new Vector2(14f, -64f), new Vector2(ColumnWidth - 28f, 40f));
        statusText.text = MapAvailable
            ? "See other islands, visit them and share yours."
            : "World map scene is missing: run Tools > LumiWorld > Tạo scene WorldMap.";
    }

    private void OpenWorldMap()
    {
        if (!MapAvailable)
        {
            statusText.text = "World map scene is missing: run Tools > LumiWorld > Tạo scene WorldMap.";
            return;
        }
        WorldMapController.Open();
    }

    // =========================================================
    // ĐANG THĂM ĐẢO NGƯỜI KHÁC
    // =========================================================

    private void BuildVisitPanel()
    {
        RectTransform panel = CreatePanel(new Vector2(ColumnWidth, 210f));

        TextMeshProUGUI title = EconomyHUD.CreateText("Title", panel, 22f, TextColor, TextAlignmentOptions.Left);
        SetTopLeft(title.rectTransform, new Vector2(14f, -10f), new Vector2(ColumnWidth - 28f, 30f));
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Ellipsis;
        title.text = VisitSession.OwnerName + "'s island";

        TextMeshProUGUI line = CreateLine(panel, new Vector2(14f, -42f), new Vector2(ColumnWidth - 28f, 48f));
        line.text = "Score " + VisitSession.OwnerScore + " pts  ·  [" + VisitSession.OwnerX + ":" + VisitSession.OwnerY + "]"
            + "\nLooking only: nothing here can be changed.";

        float half = (ColumnWidth - 36f) / 2f;
        bool backToMap = VisitSession.FromWorldMap && MapAvailable;
        AddButton(panel, backToMap ? "Back to map" : "Back", new Vector2(12f, -98f), new Vector2(half, 42f), AccentColor, VisitSession.Leave);
        AddButton(panel, "Home", new Vector2(24f + half, -98f), new Vector2(half, 42f), DangerColor, GoHome);
        AddButton(panel, "Listings", new Vector2(12f, -150f), new Vector2(ColumnWidth - 24f, 42f), NeutralColor,
            () => listings = ListingsWindow.Open(listings, transform, VisitSession.OwnerId, VisitSession.OwnerName));
    }

    private static void GoHome()
    {
        // Kết thúc chuyến thăm trước khi vào đảo của mình để WorldSaveSystem ghi lại bình thường
        VisitSession.End();
        SceneManager.LoadScene(VisitSession.OwnScene);
    }

    // =========================================================
    // DỰNG UI
    // =========================================================

    private RectTransform CreatePanel(Vector2 size)
    {
        RectTransform panel = EconomyHUD.CreateRect("WorldPanel", transform);
        SetTopLeft(panel, new Vector2(ColumnX, -PanelTop), size);
        EconomyHUD.CreatePanel(panel, PanelColor).raycastTarget = true;
        return panel;
    }

    private static TextMeshProUGUI CreateLine(RectTransform parent, Vector2 position, Vector2 size)
    {
        TextMeshProUGUI line = EconomyHUD.CreateText("Line", parent, 16f, MutedColor, TextAlignmentOptions.Left);
        SetTopLeft(line.rectTransform, position, size);
        line.textWrappingMode = TextWrappingModes.Normal;
        return line;
    }

    private static void AddButton(RectTransform parent, string text, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = EconomyHUD.CreateRect(text + "Button", parent);
        SetTopLeft(rect, position, size);
        Button button = EconomyHUD.AddButton(rect, color);
        button.onClick.AddListener(onClick);

        TextMeshProUGUI label = EconomyHUD.CreateText("Label", rect, 18f, TextColor, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(label.rectTransform);
        label.text = text;
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
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = canvasSize * canvasScale;
        root.localScale = new Vector3(1f / canvasScale, 1f / canvasScale, 1f);
    }

    private static Transform FindScreenCanvas(Scene scene)
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>())
        {
            if (canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace && canvas.gameObject.scene == scene)
                return canvas.transform;
        }

        // Scene chưa có Canvas màn hình: tạo một Canvas riêng
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
