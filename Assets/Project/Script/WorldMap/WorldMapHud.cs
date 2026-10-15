using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Giao diện scene WorldMap, bố cục theo màn World của Ikariam. Chữ hiện ra là tiếng Anh.
/// - Bảng trái: ô tìm người chơi hoặc x:y có danh sách gợi ý thả xuống khi gõ, các nút World / Island / Home,
///   dòng breadcrumb, khung thông tin đảo đang chọn với Visit và Listings.
/// - Bảng phải: đảo của mình (Share / Update / Remove) và Top islands (20 đảo điểm cao nhất, bấm để bay tới).
/// - Cửa sổ Listings: lô đang bán của chủ đảo, mua tại chỗ qua MarketSync như trong chợ.
/// Dựng hoàn toàn bằng code trên Canvas riêng.
/// </summary>
public class WorldMapHud : MonoBehaviour
{
    private const float Margin = 16f;
    private const float LeftWidth = 360f;
    private const float RightWidth = 340f;
    private const float RowHeight = 34f;
    private const int MaxResults = 8;
    private const float SuggestDelay = 0.35f;

    private static readonly Color PanelColor = new Color(0.22f, 0.15f, 0.08f, 0.88f);
    private static readonly Color RowColor = new Color(0.98f, 0.93f, 0.8f, 1f);
    private static readonly Color TextColor = new Color(0.98f, 0.95f, 0.88f, 1f);
    private static readonly Color DarkText = new Color(0.25f, 0.17f, 0.08f, 1f);
    private static readonly Color MutedColor = new Color(0.85f, 0.8f, 0.7f, 1f);
    private static readonly Color AccentColor = new Color(0.75f, 0.2f, 0.15f, 1f);
    private static readonly Color NeutralColor = new Color(0.45f, 0.35f, 0.22f, 1f);
    private static readonly Color DangerColor = new Color(0.55f, 0.12f, 0.1f, 1f);

    private WorldMapController map;

    // Bảng trái
    private TMP_InputField searchInput;
    private RectTransform results;
    private TextMeshProUGUI breadcrumb;
    private TextMeshProUGUI infoTitle;
    private TextMeshProUGUI infoLine;
    private TextMeshProUGUI statusText;
    private RectTransform infoActions;
    private Coroutine suggestRoutine;
    private int searchVersion;

    // Bảng phải
    private TextMeshProUGUI ownLine;
    private RectTransform ownActions;
    private TextMeshProUGUI topStatus;
    private RectTransform topList;

    // Cửa sổ Listings (dùng chung với VisitWorld)
    private ListingsWindow listings;

    private bool busy;

    public static WorldMapHud Create(WorldMapController map)
    {
        var canvasObject = new GameObject("WorldMapCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(map.transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        // Nút và ô nhập cần EventSystem; scene tạo bằng công cụ đã có sẵn, đây chỉ là dự phòng
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

        RectTransform root = EconomyHUD.CreateRect("WorldMapHud", canvasObject.transform);
        EconomyHUD.Stretch(root);
        var hud = root.gameObject.AddComponent<WorldMapHud>();
        hud.map = map;
        hud.Build();
        return hud;
    }

    private void Build()
    {
        BuildLeft();
        BuildRight();

        map.SelectionChanged += RefreshSelection;
        map.OwnChanged += RefreshOwn;
        map.Status += SetStatus;
        RefreshSelection();
        RefreshOwn();
    }

    private IEnumerator Start()
    {
        // Chờ đăng nhập được xác nhận rồi mới tải Top islands
        if (!AuthSession.IsLoggedIn)
        {
            topStatus.text = "Log in to see the top islands.";
            yield break;
        }
        yield return LoadTopIslands();
    }

    private void OnDestroy()
    {
        if (map == null) return;
        map.SelectionChanged -= RefreshSelection;
        map.OwnChanged -= RefreshOwn;
        map.Status -= SetStatus;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) HideResults();
    }

    // =========================================================
    // BẢNG TRÁI: TÌM KIẾM, NÚT, BREADCRUMB, THÔNG TIN ĐẢO
    // =========================================================

    private void BuildLeft()
    {
        RectTransform nav = EconomyHUD.CreateRect("NavPanel", transform);
        SetTopLeft(nav, new Vector2(Margin, -Margin), new Vector2(LeftWidth, 156f));
        EconomyHUD.CreatePanel(nav, PanelColor).raycastTarget = true;

        searchInput = CreateInput(nav, new Vector2(12f, -12f), new Vector2(236f, 44f), "Player or x:y", 32);
        searchInput.onValueChanged.AddListener(_ => QueueSuggest());
        searchInput.onSubmit.AddListener(_ => RunSearch(showEmpty: true));
        AddButton(nav, "Search", new Vector2(256f, -12f), new Vector2(92f, 44f), AccentColor, () => RunSearch(showEmpty: true));

        AddButton(nav, "World", new Vector2(12f, -66f), new Vector2(106f, 40f), NeutralColor, ShowWorld);
        AddButton(nav, "Island", new Vector2(127f, -66f), new Vector2(106f, 40f), NeutralColor, ShowIsland);
        AddButton(nav, "Home", new Vector2(242f, -66f), new Vector2(106f, 40f), AccentColor, WorldMapController.GoHome);

        breadcrumb = EconomyHUD.CreateText("Breadcrumb", nav, 16f, MutedColor, TextAlignmentOptions.Left);
        SetTopLeft(breadcrumb.rectTransform, new Vector2(14f, -114f), new Vector2(LeftWidth - 28f, 30f));
        breadcrumb.textWrappingMode = TextWrappingModes.NoWrap;
        breadcrumb.overflowMode = TextOverflowModes.Ellipsis;

        RectTransform info = EconomyHUD.CreateRect("InfoPanel", transform);
        SetTopLeft(info, new Vector2(Margin, -184f), new Vector2(LeftWidth, 200f));
        EconomyHUD.CreatePanel(info, PanelColor).raycastTarget = true;

        infoTitle = EconomyHUD.CreateText("Title", info, 22f, TextColor, TextAlignmentOptions.Left);
        SetTopLeft(infoTitle.rectTransform, new Vector2(14f, -10f), new Vector2(LeftWidth - 28f, 30f));

        infoLine = EconomyHUD.CreateText("Line", info, 16f, MutedColor, TextAlignmentOptions.Left);
        SetTopLeft(infoLine.rectTransform, new Vector2(14f, -42f), new Vector2(LeftWidth - 28f, 50f));
        infoLine.textWrappingMode = TextWrappingModes.Normal;

        infoActions = EconomyHUD.CreateRect("Actions", info);
        SetTopLeft(infoActions, new Vector2(12f, -96f), new Vector2(LeftWidth - 24f, 42f));

        statusText = EconomyHUD.CreateText("Status", info, 15f, MutedColor, TextAlignmentOptions.Left);
        SetTopLeft(statusText.rectTransform, new Vector2(14f, -146f), new Vector2(LeftWidth - 28f, 46f));
        statusText.textWrappingMode = TextWrappingModes.Normal;

        // Gợi ý thả xuống ngay dưới ô nhập, đè lên khung thông tin
        results = EconomyHUD.CreateRect("SearchResults", transform);
        SetTopLeft(results, new Vector2(Margin + 12f, -Margin - 58f), new Vector2(LeftWidth - 24f, 0f));
        EconomyHUD.CreatePanel(results, PanelColor).raycastTarget = true;
        results.gameObject.SetActive(false);
    }

    private void RefreshSelection()
    {
        WorldMapIsland island = map.Selected;
        string trail = "World";
        if (island != null) trail += "  >  " + Coords(island.x, island.y) + " " + island.displayName;
        breadcrumb.text = trail;

        EconomyHUD.ClearChildren(infoActions);
        if (island == null)
        {
            infoTitle.text = "World map";
            infoLine.text = "Click an island to see who lives there. Drag or use WASD to move, scroll to zoom.";
            return;
        }

        bool own = island.playerId == SaveSlot.PlayerId;
        infoTitle.text = own ? "Your island" : island.displayName;
        infoLine.text = Coords(island.x, island.y) + "  ·  " + island.score + " pts";

        if (own)
        {
            AddButton(infoActions, "Go home", Vector2.zero, new Vector2(160f, 42f), AccentColor, WorldMapController.GoHome);
            return;
        }

        AddButton(infoActions, "Visit", Vector2.zero, new Vector2(160f, 42f), AccentColor, () => Visit(island));
        AddButton(infoActions, "Listings", new Vector2(172f, 0f), new Vector2(160f, 42f), NeutralColor,
            () => OpenListings(island.playerId, island.displayName));
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message ?? "";
    }

    private void ShowWorld()
    {
        HideResults();
        map.MapCamera.ZoomOut();
    }

    /// <summary>Phóng to vào đảo đang chọn, chưa chọn thì vào đảo của mình (như Show Island của Ikariam).</summary>
    private void ShowIsland()
    {
        HideResults();
        WorldMapIsland island = map.Selected ?? map.Own;
        if (island == null)
        {
            SetStatus("Pick an island on the map first.");
            return;
        }
        map.FocusIsland(island);
        map.MapCamera.ZoomIn();
    }

    private void Visit(WorldMapIsland island)
    {
        if (busy) return;

        busy = true;
        SetStatus("Sailing to " + island.displayName + "...");
        WorldMapController.FocusOnOpen = new Vector2Int(island.x, island.y);
        StartCoroutine(WorldShareService.Visit(island.playerId, message =>
        {
            busy = false;
            WorldMapController.FocusOnOpen = null;
            SetStatus(message);
        }));
    }

    // =========================================================
    // TÌM KIẾM VÀ GỢI Ý
    // =========================================================

    // Gõ tới đâu gợi ý tới đó, nhưng chờ người chơi ngừng gõ một chút để không gọi server mỗi phím
    private void QueueSuggest()
    {
        if (suggestRoutine != null) StopCoroutine(suggestRoutine);
        if (searchInput.text.Trim().Length < 2)
        {
            HideResults();
            return;
        }
        suggestRoutine = StartCoroutine(SuggestAfterDelay());
    }

    private IEnumerator SuggestAfterDelay()
    {
        yield return new WaitForSecondsRealtime(SuggestDelay);
        suggestRoutine = null;
        RunSearch(showEmpty: false);
    }

    private void RunSearch(bool showEmpty)
    {
        string query = searchInput.text.Trim();
        if (query.Length == 0) return;
        if (!AuthSession.IsLoggedIn)
        {
            SetStatus("Log in to search the world.");
            return;
        }

        // Kết quả của lần tìm cũ về muộn thì bỏ qua
        int version = ++searchVersion;
        if (showEmpty) SetStatus("Searching...");
        StartCoroutine(WorldShareService.Search(query,
            found => { if (version == searchVersion) ShowResults(found, showEmpty); },
            message => { if (version == searchVersion) SetStatus(message); }));
    }

    private void ShowResults(List<WorldMapIsland> found, bool showEmpty)
    {
        EconomyHUD.ClearChildren(results);

        if (found == null || found.Count == 0)
        {
            HideResults();
            if (showEmpty) SetStatus("No island found. Islands appear once their owner shares them.");
            return;
        }

        if (showEmpty) SetStatus("");
        int count = Mathf.Min(found.Count, MaxResults);
        float width = results.sizeDelta.x;
        results.sizeDelta = new Vector2(width, count * (RowHeight + 4f) + 8f);
        for (int i = 0; i < count; i++)
        {
            WorldMapIsland island = found[i];
            RectTransform row = EconomyHUD.CreateRect("Result" + i, results);
            SetTopLeft(row, new Vector2(4f, -4f - i * (RowHeight + 4f)), new Vector2(width - 8f, RowHeight));
            Button button = EconomyHUD.AddButton(row, RowColor);
            button.onClick.AddListener(() =>
            {
                HideResults();
                map.FocusIsland(island);
            });
            AddRowLabel(row, island.displayName + "  " + Coords(island.x, island.y) + "  ·  " + island.score + " pts", DarkText);
        }
        results.gameObject.SetActive(true);
        results.SetAsLastSibling();
    }

    private void HideResults()
    {
        if (results != null) results.gameObject.SetActive(false);
    }

    // =========================================================
    // BẢNG PHẢI: ĐẢO CỦA MÌNH VÀ TOP ISLANDS
    // =========================================================

    private void BuildRight()
    {
        RectTransform own = EconomyHUD.CreateRect("OwnPanel", transform);
        SetTopRight(own, new Vector2(-Margin, -Margin), new Vector2(RightWidth, 140f));
        EconomyHUD.CreatePanel(own, PanelColor).raycastTarget = true;

        TextMeshProUGUI ownTitle = EconomyHUD.CreateText("Title", own, 20f, TextColor, TextAlignmentOptions.Left);
        SetTopLeft(ownTitle.rectTransform, new Vector2(14f, -10f), new Vector2(RightWidth - 28f, 28f));
        ownTitle.text = "Your island";

        ownLine = EconomyHUD.CreateText("Line", own, 15f, MutedColor, TextAlignmentOptions.Left);
        SetTopLeft(ownLine.rectTransform, new Vector2(14f, -40f), new Vector2(RightWidth - 28f, 40f));
        ownLine.textWrappingMode = TextWrappingModes.Normal;

        ownActions = EconomyHUD.CreateRect("Actions", own);
        SetTopLeft(ownActions, new Vector2(12f, -86f), new Vector2(RightWidth - 24f, 42f));

        float topHeight = 20 * (RowHeight + 2f) + 84f;
        RectTransform top = EconomyHUD.CreateRect("TopPanel", transform);
        SetTopRight(top, new Vector2(-Margin, -Margin - 152f), new Vector2(RightWidth, topHeight));
        EconomyHUD.CreatePanel(top, PanelColor).raycastTarget = true;

        TextMeshProUGUI topTitle = EconomyHUD.CreateText("Title", top, 20f, TextColor, TextAlignmentOptions.Left);
        SetTopLeft(topTitle.rectTransform, new Vector2(14f, -10f), new Vector2(200f, 28f));
        topTitle.text = "Top islands";
        AddButton(top, "Refresh", new Vector2(RightWidth - 112f, -8f), new Vector2(100f, 32f), NeutralColor,
            () => StartCoroutine(LoadTopIslands()));

        topStatus = EconomyHUD.CreateText("Status", top, 14f, MutedColor, TextAlignmentOptions.Left);
        SetTopLeft(topStatus.rectTransform, new Vector2(14f, -44f), new Vector2(RightWidth - 28f, 24f));

        topList = EconomyHUD.CreateRect("List", top);
        SetTopLeft(topList, new Vector2(10f, -72f), new Vector2(RightWidth - 20f, topHeight - 80f));
    }

    private void RefreshOwn()
    {
        EconomyHUD.ClearChildren(ownActions);

        if (!SaveSlot.IsAccount)
        {
            ownLine.text = "Log in to put your island on the world map.";
            return;
        }
        if (!map.OwnKnown)
        {
            ownLine.text = "Checking...";
            return;
        }

        WorldMapIsland own = map.Own;
        ownLine.text = own != null
            ? "Shared at " + Coords(own.x, own.y) + "  ·  " + own.score + " pts"
            : "Not on the map yet. Share it so others can visit.";

        AddButton(ownActions, own != null ? "Update" : "Share", Vector2.zero, new Vector2(150f, 40f), AccentColor, Share);
        if (own != null)
            AddButton(ownActions, "Remove", new Vector2(162f, 0f), new Vector2(150f, 40f), DangerColor, Unshare);
    }

    private void Share()
    {
        if (busy) return;

        busy = true;
        SetStatus("Sharing...");
        StartCoroutine(WorldShareService.Share((ok, message) =>
        {
            busy = false;
            SetStatus(message);
            if (ok) StartCoroutine(LoadTopIslands());
        }));
    }

    private void Unshare()
    {
        if (busy) return;

        busy = true;
        SetStatus("Removing...");
        StartCoroutine(WorldShareService.Unshare((ok, message) =>
        {
            busy = false;
            SetStatus(message);
            if (!ok) return;
            map.RemoveOwn();
            StartCoroutine(LoadTopIslands());
        }));
    }

    private IEnumerator LoadTopIslands()
    {
        topStatus.text = "Loading...";
        WorldGalleryResponse page = null;
        string failure = null;
        yield return WorldShareService.LoadGallery(r => page = r, message => failure = message);

        EconomyHUD.ClearChildren(topList);
        if (page == null)
        {
            topStatus.text = failure;
            yield break;
        }
        if (page.items.Count == 0)
        {
            topStatus.text = "No island has been shared yet.";
            yield break;
        }

        topStatus.text = "Click an island to fly there.";
        for (int i = 0; i < page.items.Count && i < 20; i++)
        {
            WorldGalleryItem item = page.items[i];
            var island = new WorldMapIsland { playerId = item.playerId, displayName = item.displayName, score = item.score, x = item.x, y = item.y };

            RectTransform row = EconomyHUD.CreateRect("Top" + i, topList);
            SetTopLeft(row, new Vector2(0f, -i * (RowHeight + 2f)), new Vector2(topList.sizeDelta.x, RowHeight));
            EconomyHUD.AddButton(row, item.playerId == SaveSlot.PlayerId ? new Color(1f, 0.88f, 0.55f) : RowColor)
                .onClick.AddListener(() => map.FocusIsland(island));
            AddRowLabel(row, (i + 1) + ".  " + item.displayName + "  " + Coords(item.x, item.y) + "  ·  " + item.score + " pts", DarkText);
        }
    }

    // =========================================================
    // CỬA SỔ LISTINGS (MUA TẠI CHỖ, XEM ListingsWindow)
    // =========================================================

    private void OpenListings(string sellerId, string sellerName)
    {
        listings = ListingsWindow.Open(listings, transform, sellerId, sellerName);
    }

    // =========================================================
    // DỰNG UI
    // =========================================================

    private static Button AddButton(RectTransform parent, string text, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = EconomyHUD.CreateRect(text + "Button", parent);
        SetTopLeft(rect, position, size);
        Button button = EconomyHUD.AddButton(rect, color);
        button.onClick.AddListener(onClick);

        TextMeshProUGUI label = EconomyHUD.CreateText("Label", rect, 17f, TextColor, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(label.rectTransform);
        label.text = text;
        return button;
    }

    private static void AddRowLabel(RectTransform row, string text, Color color)
    {
        TextMeshProUGUI label = EconomyHUD.CreateText("Label", row, 15f, color, TextAlignmentOptions.Left);
        EconomyHUD.Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(12f, 0f);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.text = text;
    }

    private static TMP_InputField CreateInput(RectTransform parent, Vector2 position, Vector2 size, string placeholder, int characterLimit)
    {
        RectTransform rect = EconomyHUD.CreateRect("SearchInput", parent);
        SetTopLeft(rect, position, size);

        Image background = EconomyHUD.CreatePanel(rect, RowColor);
        background.raycastTarget = true;

        TextMeshProUGUI text = EconomyHUD.CreateText("Text", rect, 18f, DarkText, TextAlignmentOptions.Left);
        EconomyHUD.Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(12f, 4f);
        text.rectTransform.offsetMax = new Vector2(-12f, -4f);

        TextMeshProUGUI hint = EconomyHUD.CreateText("Placeholder", rect, 18f, new Color(0.5f, 0.42f, 0.3f, 1f), TextAlignmentOptions.Left);
        EconomyHUD.Stretch(hint.rectTransform);
        hint.rectTransform.offsetMin = new Vector2(12f, 4f);
        hint.rectTransform.offsetMax = new Vector2(-12f, -4f);
        hint.text = placeholder;

        TMP_InputField field = rect.gameObject.AddComponent<TMP_InputField>();
        field.targetGraphic = background;
        field.textComponent = text;
        field.placeholder = hint;
        field.contentType = TMP_InputField.ContentType.Standard;
        field.characterLimit = characterLimit;
        field.lineType = TMP_InputField.LineType.SingleLine;
        return field;
    }

    private static string Coords(int x, int y) => "[" + x + ":" + y + "]";

    private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetTopRight(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
