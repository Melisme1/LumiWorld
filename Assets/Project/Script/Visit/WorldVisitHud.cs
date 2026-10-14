using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Giao diện bản đồ thế giới và thăm đảo (giai đoạn 3), bố cục theo màn World của Ikariam:
/// - Cột trái, dưới thanh tài nguyên: ô tìm kiếm (tên chủ đảo hoặc [x:y]) có danh sách kết quả thả xuống,
///   ba nút World / Island / Home và breadcrumb "World > ...", rồi khung thông tin của đảo đang xem hoặc đang chọn.
/// - Giữa màn hình: bản đồ dạng lưới, mỗi ô [x:y] là một đảo đã chia sẻ, bấm để chọn, kéo lên xuống bằng Up / Down.
/// - Bên phải giữ nguyên cột nút có sẵn (Orders, Card Shop, Market) và Coins ở góc trên.
/// Tự tạo khi vào scene có WorldSaveSystem. Chữ hiện ra là tiếng Anh.
/// </summary>
public class WorldVisitHud : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.13f, 0.92f);
    private static readonly Color RowColor = new Color(0.14f, 0.17f, 0.23f, 1f);
    private static readonly Color AccentColor = new Color(0.22f, 0.55f, 0.95f, 1f);
    private static readonly Color DangerColor = new Color(0.78f, 0.32f, 0.30f, 1f);
    private static readonly Color NeutralColor = new Color(0.26f, 0.30f, 0.38f, 1f);
    private static readonly Color MutedColor = new Color(0.72f, 0.76f, 0.84f, 1f);
    private static readonly Color TextColor = Color.white;
    private static readonly Color OceanColor = new Color(0.05f, 0.20f, 0.33f, 0.97f);
    private static readonly Color EmptyCellColor = new Color(1f, 1f, 1f, 0.04f);
    private static readonly Color IslandColor = new Color(0.20f, 0.46f, 0.30f, 1f);
    private static readonly Color OwnIslandColor = new Color(0.78f, 0.60f, 0.20f, 1f);
    private static readonly Color HereIslandColor = new Color(0.36f, 0.42f, 0.70f, 1f);
    private static readonly Color SelectedIslandColor = new Color(0.30f, 0.72f, 0.48f, 1f);

    // Cột trái nằm dưới thanh tài nguyên (InventoryUI ở (20, 20), cao 48)
    private const float ColumnX = 20f;
    private const float ColumnWidth = 360f;
    private const float NavTop = 80f;
    private const float NavHeight = 150f;
    private const float InfoTop = NavTop + NavHeight + 10f;
    private const float InfoHeight = 196f;

    // Bản đồ chừa cột trái và cột nút bên phải (nút rộng 170, cách mép 20)
    private const float MapLeft = ColumnX + ColumnWidth + 20f;
    private const float MapRight = 210f;
    private const float MapTop = 80f;
    private const float MapBottom = 20f;
    private const int MapRows = 6;
    private const int MapScrollStep = 3;

    private const float ResultRowHeight = 40f;
    private const float RowHeight = 56f;

    // Phóng theo Scale Factor của Canvas như EconomyHUD: 1 đơn vị = 1 pixel màn hình
    private Canvas canvas;
    private float fittedCanvasScale;
    private Vector2 fittedCanvasSize;

    // Cột trái
    private TMP_InputField searchInput;
    private GameObject searchFocus;
    private RectTransform searchResults;
    private TextMeshProUGUI breadcrumb;
    private TextMeshProUGUI infoTitle;
    private TextMeshProUGUI infoLine;
    private RectTransform infoActions;
    private TextMeshProUGUI statusText;

    // Bản đồ
    private GameObject mapWindow;
    private RectTransform mapGrid;
    private TextMeshProUGUI mapRange;
    private int mapTop = 1;
    private int mapTotalRows;
    private int mapRequest;
    private string pendingSelectId;
    private readonly List<WorldMapIsland> mapIslands = new List<WorldMapIsland>();
    private WorldMapIsland selected;

    // Đảo của mình trên bản đồ
    private bool ownKnown;
    private bool ownShared;
    private int ownX;
    private int ownY;
    private int ownScore;

    // Cửa sổ phụ
    private GameObject topWindow;
    private RectTransform topList;
    private TextMeshProUGUI topStatus;
    private GameObject listingsWindow;
    private TextMeshProUGUI listingsTitle;
    private RectTransform listingsList;
    private TextMeshProUGUI listingsStatus;
    private string listingsSellerId;

    private bool busy;

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

    private bool Visiting => VisitSession.Active;
    private bool MapOpen => mapWindow != null && mapWindow.activeSelf;

    private void Awake()
    {
        // Bản đồ dựng trước để nằm dưới cột trái và các cửa sổ
        BuildMap();
        BuildNavPanel();
        BuildInfoPanel();
        BuildSearchResults();

        WorldShareService.Shared += HandleShared;
        RefreshInfo();
    }

    private void Start()
    {
        if (!Visiting && SaveSlot.IsAccount)
            StartCoroutine(WorldShareService.LoadOwnIsland(HandleOwnIsland, SetStatus));
        else
            ownKnown = true;
        RefreshInfo();
    }

    private void OnDestroy()
    {
        WorldShareService.Shared -= HandleShared;
    }

    private void LateUpdate()
    {
        FitToCanvas();
    }

    // =========================================================
    // CỘT TRÁI: TÌM KIẾM, NÚT ĐIỀU HƯỚNG, BREADCRUMB
    // =========================================================

    private void BuildNavPanel()
    {
        RectTransform nav = EconomyHUD.CreateRect("NavPanel", transform);
        SetTopLeft(nav, new Vector2(ColumnX, -NavTop), new Vector2(ColumnWidth, NavHeight));
        EconomyHUD.CreatePanel(nav, PanelColor).raycastTarget = true;

        // Đang gõ tìm kiếm thì coi như có cửa sổ mở: camera và phím tắt (I, WASD...) không ăn phím
        RectTransform focus = EconomyHUD.CreateRect("SearchFocus", transform);
        searchFocus = focus.gameObject;
        searchFocus.SetActive(false);
        EconomyHUD.RegisterWindow(searchFocus, () =>
        {
            searchFocus.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        });

        searchInput = CreateInput(nav, new Vector2(12f, -12f), new Vector2(236f, 44f), "Player or x:y", 32);
        searchInput.onSubmit.AddListener(_ => RunSearch());
        searchInput.onSelect.AddListener(_ => searchFocus.SetActive(true));
        searchInput.onDeselect.AddListener(_ => searchFocus.SetActive(false));
        AddButton(nav, "Search", new Vector2(256f, -12f), new Vector2(92f, 44f), AccentColor, RunSearch);

        AddButton(nav, "World", new Vector2(12f, -66f), new Vector2(106f, 40f), AccentColor, ShowWorld);
        AddButton(nav, "Island", new Vector2(127f, -66f), new Vector2(106f, 40f), NeutralColor, ShowIsland);
        AddButton(nav, "Home", new Vector2(242f, -66f), new Vector2(106f, 40f), NeutralColor, ShowHome);

        breadcrumb = EconomyHUD.CreateText("Breadcrumb", nav, 16f, MutedColor, TextAlignmentOptions.Left);
        SetTopLeft(breadcrumb.rectTransform, new Vector2(14f, -114f), new Vector2(ColumnWidth - 28f, 26f));
        breadcrumb.enableWordWrapping = false;
    }

    private void BuildInfoPanel()
    {
        RectTransform info = EconomyHUD.CreateRect("InfoPanel", transform);
        SetTopLeft(info, new Vector2(ColumnX, -InfoTop), new Vector2(ColumnWidth, InfoHeight));
        EconomyHUD.CreatePanel(info, PanelColor).raycastTarget = true;

        infoTitle = EconomyHUD.CreateText("Title", info, 20f, TextColor, TextAlignmentOptions.Left);
        SetTopLeft(infoTitle.rectTransform, new Vector2(14f, -10f), new Vector2(ColumnWidth - 28f, 28f));
        infoTitle.enableWordWrapping = false;

        infoLine = EconomyHUD.CreateText("Line", info, 16f, MutedColor, TextAlignmentOptions.Left);
        SetTopLeft(infoLine.rectTransform, new Vector2(14f, -40f), new Vector2(ColumnWidth - 28f, 48f));
        infoLine.enableWordWrapping = true;

        infoActions = EconomyHUD.CreateRect("Actions", info);
        SetTopLeft(infoActions, new Vector2(12f, -92f), new Vector2(ColumnWidth - 24f, 44f));

        statusText = EconomyHUD.CreateText("Status", info, 15f, MutedColor, TextAlignmentOptions.Left);
        SetTopLeft(statusText.rectTransform, new Vector2(14f, -142f), new Vector2(ColumnWidth - 28f, 46f));
        statusText.enableWordWrapping = true;
    }

    private void ShowWorld()
    {
        HideResults();
        if (MapOpen) return;

        // Mở bản đồ ở đảo đang đứng: đảo đang thăm, hoặc đảo của mình nếu đã chia sẻ
        if (Visiting) OpenMapAt(VisitSession.OwnerY, VisitSession.OwnerId);
        else if (ownShared) OpenMapAt(ownY, SaveSlot.PlayerId);
        else OpenMapAt(1, null);
    }

    /// <summary>Về góc nhìn đảo đang đứng (đóng bản đồ), như nút Show Island của Ikariam.</summary>
    private void ShowIsland()
    {
        HideResults();
        CloseMap();
    }

    /// <summary>Về đảo của mình. Đang thăm thì rời đảo người khác.</summary>
    private void ShowHome()
    {
        HideResults();
        if (Visiting) GoHome();
        else CloseMap();
    }

    private void RefreshBreadcrumb()
    {
        if (breadcrumb == null) return;

        string trail = "World";
        if (MapOpen)
        {
            if (selected != null) trail += "  >  " + selected.displayName + " " + Coords(selected.x, selected.y);
        }
        else if (Visiting)
        {
            trail += "  >  " + VisitSession.OwnerName + "'s island " + Coords(VisitSession.OwnerX, VisitSession.OwnerY);
        }
        else
        {
            trail += "  >  Your island" + (ownShared ? " " + Coords(ownX, ownY) : "");
        }
        breadcrumb.text = trail;
    }

    // =========================================================
    // TÌM KIẾM: TÊN CHỦ ĐẢO HOẶC [x:y], KẾT QUẢ THẢ XUỐNG DƯỚI Ô TÌM
    // =========================================================

    private void BuildSearchResults()
    {
        searchResults = EconomyHUD.CreateRect("SearchResults", transform);
        SetTopLeft(searchResults, new Vector2(ColumnX + 12f, -(NavTop + 60f)), new Vector2(ColumnWidth - 24f, ResultRowHeight));
        EconomyHUD.CreatePanel(searchResults, RowColor).raycastTarget = true;
        searchResults.gameObject.SetActive(false);
        EconomyHUD.RegisterWindow(searchResults.gameObject, HideResults);
    }

    private void RunSearch()
    {
        string query = searchInput != null ? searchInput.text.Trim() : "";
        if (query.Length == 0)
        {
            HideResults();
            return;
        }
        if (!SaveSlot.IsAccount)
        {
            ShowResultsMessage("Log in to search the world.");
            return;
        }

        ShowResultsMessage("Searching...");
        StartCoroutine(WorldShareService.Search(query, FillResults, ShowResultsMessage));
    }

    private void FillResults(List<WorldMapIsland> found)
    {
        if (found.Count == 0)
        {
            ShowResultsMessage("No island found. Islands appear once their owner shares them.");
            return;
        }

        PrepareResults(found.Count + 1);
        for (int i = 0; i < found.Count; i++)
        {
            WorldMapIsland island = found[i];
            Button row = AddResultRow(i, island.displayName + "  " + Coords(island.x, island.y) + "  ·  " + island.score + " pts", TextColor);
            row.onClick.AddListener(() => PickIsland(island));
        }
        AddResultRow(found.Count, "Close", MutedColor).onClick.AddListener(HideResults);
    }

    private void ShowResultsMessage(string message)
    {
        PrepareResults(2);
        TextMeshProUGUI label = EconomyHUD.CreateText("Message", searchResults, 15f, MutedColor, TextAlignmentOptions.Left);
        SetTopLeft(label.rectTransform, new Vector2(12f, -4f), new Vector2(ColumnWidth - 48f, ResultRowHeight));
        label.enableWordWrapping = true;
        label.text = message;
        AddResultRow(1, "Close", MutedColor).onClick.AddListener(HideResults);
    }

    private void PrepareResults(int rows)
    {
        EconomyHUD.ClearChildren(searchResults);
        searchResults.sizeDelta = new Vector2(ColumnWidth - 24f, rows * ResultRowHeight + 8f);
        searchResults.gameObject.SetActive(true);
        EconomyHUD.BringWindowToFront(searchResults.gameObject);
        searchResults.SetAsLastSibling();
    }

    private Button AddResultRow(int index, string text, Color color)
    {
        RectTransform row = EconomyHUD.CreateRect("Result", searchResults);
        SetTopLeft(row, new Vector2(4f, -4f - index * ResultRowHeight), new Vector2(ColumnWidth - 32f, ResultRowHeight - 4f));
        Button button = EconomyHUD.AddButton(row, PanelColor);

        TextMeshProUGUI label = EconomyHUD.CreateText("Label", row, 16f, color, TextAlignmentOptions.Left);
        EconomyHUD.Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(10f, 0f);
        label.rectTransform.offsetMax = new Vector2(-10f, 0f);
        label.enableWordWrapping = false;
        label.text = text;
        return button;
    }

    private void HideResults()
    {
        if (searchResults != null) searchResults.gameObject.SetActive(false);
    }

    /// <summary>Chọn một đảo từ kết quả tìm kiếm hoặc bảng Top: mở bản đồ ở hàng của đảo đó và chọn nó.</summary>
    private void PickIsland(WorldMapIsland island)
    {
        HideResults();
        selected = island;
        OpenMapAt(island.y, island.playerId);
    }

    // =========================================================
    // BẢN ĐỒ THẾ GIỚI
    // =========================================================

    private void BuildMap()
    {
        RectTransform window = EconomyHUD.CreateRect("WorldMap", transform);
        window.anchorMin = Vector2.zero;
        window.anchorMax = Vector2.one;
        window.offsetMin = new Vector2(MapLeft, MapBottom);
        window.offsetMax = new Vector2(-MapRight, -MapTop);
        EconomyHUD.CreatePanel(window, OceanColor).raycastTarget = true;
        mapWindow = window.gameObject;
        EconomyHUD.RegisterWindow(mapWindow, CloseMap);

        TextMeshProUGUI title = EconomyHUD.CreateText("Title", window, 22f, TextColor, TextAlignmentOptions.Left);
        SetTopLeft(title.rectTransform, new Vector2(16f, -12f), new Vector2(150f, 32f));
        title.text = "World map";

        mapRange = EconomyHUD.CreateText("Range", window, 16f, MutedColor, TextAlignmentOptions.Left);
        SetTopLeft(mapRange.rectTransform, new Vector2(166f, -16f), new Vector2(240f, 26f));
        mapRange.enableWordWrapping = false;

        // Nút xếp từ phải sang trái: Close, Down, Up, Top islands
        float right = -12f;
        right = AddTopRightButton(window, "Close", right, 84f, DangerColor, CloseMap);
        right = AddTopRightButton(window, "Down", right, 84f, NeutralColor, () => ScrollMap(MapScrollStep));
        right = AddTopRightButton(window, "Up", right, 84f, NeutralColor, () => ScrollMap(-MapScrollStep));
        AddTopRightButton(window, "Top islands", right, 130f, AccentColor, OpenTopIslands);

        mapGrid = EconomyHUD.CreateRect("Grid", window);
        mapGrid.anchorMin = Vector2.zero;
        mapGrid.anchorMax = Vector2.one;
        mapGrid.offsetMin = new Vector2(12f, 12f);
        mapGrid.offsetMax = new Vector2(-12f, -60f);

        mapWindow.SetActive(false);
    }

    // Trả về mép phải cho nút kế tiếp
    private static float AddTopRightButton(RectTransform parent, string label, float right, float width, Color color, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = EconomyHUD.CreateRect(label, parent);
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(right, -10f);
        rect.sizeDelta = new Vector2(width, 40f);
        EconomyHUD.AddButton(rect, color).onClick.AddListener(onClick);
        CreateLabel(rect, label, 17f);
        return right - width - 8f;
    }

    /// <summary>Mở bản đồ sao cho hàng y nằm gần giữa; selectId là đảo cần chọn khi tải xong (có thể null).</summary>
    private void OpenMapAt(int y, string selectId)
    {
        if (!SaveSlot.IsAccount)
        {
            SetStatus("Log in to see the world map.");
            return;
        }

        mapWindow.SetActive(true);
        EconomyHUD.BringWindowToFront(mapWindow);
        // Bản đồ nằm dưới cột trái trong HUD này: ô tìm kiếm và khung thông tin luôn bấm được
        mapWindow.transform.SetAsFirstSibling();

        pendingSelectId = selectId;
        if (selectId == null) selected = null;
        mapTop = Mathf.Max(1, y - MapRows / 2);
        LoadMap();
        RefreshInfo();
    }

    private void CloseMap()
    {
        if (mapWindow == null || !mapWindow.activeSelf) return;
        mapWindow.SetActive(false);
        selected = null;
        RefreshInfo();
    }

    private void ScrollMap(int delta)
    {
        int lastTop = Mathf.Max(1, mapTotalRows - MapRows + 1);
        int next = Mathf.Clamp(mapTop + delta, 1, lastTop);
        if (next == mapTop) return;

        mapTop = next;
        pendingSelectId = selected != null ? selected.playerId : null;
        LoadMap();
    }

    private void LoadMap()
    {
        // Bỏ kết quả của lần tải cũ nếu người chơi đã cuộn hoặc tìm tiếp
        int request = ++mapRequest;
        mapRange.text = "Loading...";
        StartCoroutine(WorldShareService.LoadMap(mapTop, MapRows,
            map => { if (request == mapRequest) ShowMap(map); },
            message => { if (request == mapRequest) mapRange.text = message; }));
    }

    private void ShowMap(WorldMapResponse map)
    {
        mapTotalRows = map.rows;
        mapIslands.Clear();
        if (map.islands != null) mapIslands.AddRange(map.islands);

        if (pendingSelectId != null)
        {
            WorldMapIsland match = mapIslands.Find(island => island.playerId == pendingSelectId);
            if (match != null) selected = match;
            pendingSelectId = null;
        }

        int last = mapTop + MapRows - 1;
        mapRange.text = mapTotalRows == 0
            ? "No island shared yet"
            : "Rows " + mapTop + " to " + last + " of " + Mathf.Max(mapTotalRows, last);
        DrawMap();
        RefreshInfo();
    }

    private void DrawMap()
    {
        EconomyHUD.ClearChildren(mapGrid);
        int columns = WorldShareService.MapColumns;

        for (int row = 0; row < MapRows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int x = column + 1;
                int y = mapTop + row;
                RectTransform cell = EconomyHUD.CreateRect("Cell " + x + ":" + y, mapGrid);
                cell.anchorMin = new Vector2((float)column / columns, 1f - (float)(row + 1) / MapRows);
                cell.anchorMax = new Vector2((float)(column + 1) / columns, 1f - (float)row / MapRows);
                cell.offsetMin = new Vector2(3f, 3f);
                cell.offsetMax = new Vector2(-3f, -3f);

                WorldMapIsland island = mapIslands.Find(item => item.x == x && item.y == y);
                if (island == null) DrawEmptyCell(cell, x, y);
                else DrawIslandCell(cell, island);
            }
        }
    }

    private static void DrawEmptyCell(RectTransform cell, int x, int y)
    {
        EconomyHUD.CreatePanel(cell, EmptyCellColor);
        TextMeshProUGUI label = EconomyHUD.CreateText("Coords", cell, 12f, new Color(1f, 1f, 1f, 0.25f), TextAlignmentOptions.Center);
        EconomyHUD.Stretch(label.rectTransform);
        label.text = Coords(x, y);
    }

    private void DrawIslandCell(RectTransform cell, WorldMapIsland island)
    {
        bool own = island.playerId == SaveSlot.PlayerId;
        bool here = Visiting && island.playerId == VisitSession.OwnerId;
        bool isSelected = selected != null && selected.playerId == island.playerId;

        Color color = isSelected ? SelectedIslandColor : own ? OwnIslandColor : here ? HereIslandColor : IslandColor;
        Button button = EconomyHUD.AddButton(cell, color);
        button.onClick.AddListener(() => SelectIsland(island));

        TextMeshProUGUI label = EconomyHUD.CreateText("Label", cell, 15f, TextColor, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(4f, 2f);
        label.rectTransform.offsetMax = new Vector2(-4f, -2f);
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 16f;
        label.text = island.displayName + "\n" + Coords(island.x, island.y) + "\n" + island.score + " pts";
    }

    private void SelectIsland(WorldMapIsland island)
    {
        selected = island;
        DrawMap();
        RefreshInfo();
    }

    // =========================================================
    // KHUNG THÔNG TIN: ĐẢO ĐANG CHỌN, ĐẢO ĐANG THĂM HOẶC ĐẢO CỦA MÌNH
    // =========================================================

    private void RefreshInfo()
    {
        RefreshBreadcrumb();
        if (infoActions == null) return;
        EconomyHUD.ClearChildren(infoActions);

        if (MapOpen) ShowSelectedInfo();
        else if (Visiting) ShowVisitInfo();
        else ShowOwnInfo();
    }

    private void ShowSelectedInfo()
    {
        if (selected == null)
        {
            infoTitle.text = "World map";
            infoLine.text = "Pick an island on the map, or search by player name or x:y.";
            return;
        }

        bool own = selected.playerId == SaveSlot.PlayerId;
        bool here = Visiting && selected.playerId == VisitSession.OwnerId;
        WorldMapIsland island = selected;

        infoTitle.text = own ? "Your island" : island.displayName + "'s island";
        infoLine.text = "Score " + island.score + " pts  ·  " + Coords(island.x, island.y)
            + (here ? "\nYou are visiting this island." : "");

        if (own)
        {
            AddInfoButton("Go home", 0, NeutralColor, ShowHome);
            return;
        }

        if (here) AddInfoButton("Show island", 0, NeutralColor, ShowIsland);
        else AddInfoButton("Visit", 0, AccentColor, () => Visit(island.playerId));
        AddInfoButton("Listings", 1, NeutralColor, () => OpenListings(island.playerId, island.displayName));
    }

    private void ShowVisitInfo()
    {
        infoTitle.text = VisitSession.OwnerName + "'s island";
        infoLine.text = "Score " + VisitSession.OwnerScore + " pts  ·  " + Coords(VisitSession.OwnerX, VisitSession.OwnerY)
            + "\nLooking only: nothing here can be changed.";

        AddInfoButton("Listings", 0, AccentColor, () => OpenListings(VisitSession.OwnerId, VisitSession.OwnerName));
        AddInfoButton("Back home", 1, DangerColor, GoHome);
    }

    private void ShowOwnInfo()
    {
        infoTitle.text = "Your island";

        if (!SaveSlot.IsAccount)
        {
            infoLine.text = "Log in to put your island on the world map.";
            return;
        }
        if (!ownKnown)
        {
            infoLine.text = "Checking the world map...";
            return;
        }

        infoLine.text = ownShared
            ? "On the world map at " + Coords(ownX, ownY) + "  ·  " + ownScore + " pts"
            : "Not on the world map yet. Share it so other players can find and visit it.";

        AddInfoButton(ownShared ? "Update" : "Share", 0, AccentColor, OnShare);
        if (ownShared) AddInfoButton("Remove", 1, DangerColor, OnUnshare);
    }

    private void AddInfoButton(string label, int slot, Color color, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = EconomyHUD.CreateRect(label, infoActions);
        SetTopLeft(rect, new Vector2(slot * 172f, 0f), new Vector2(164f, 44f));
        EconomyHUD.AddButton(rect, color).onClick.AddListener(onClick);
        CreateLabel(rect, label, 18f);
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    // =========================================================
    // ĐẢO MÌNH: CHIA SẺ, GỠ
    // =========================================================

    private void HandleOwnIsland(WorldVisitResponse mine)
    {
        ownKnown = true;
        ownShared = mine != null;
        if (mine != null)
        {
            ownX = mine.x;
            ownY = mine.y;
            ownScore = mine.score;
        }
        RefreshInfo();
    }

    private void HandleShared(WorldShareResponse result)
    {
        ownKnown = true;
        ownShared = true;
        ownX = result.x;
        ownY = result.y;
        ownScore = result.score;
        RefreshInfo();
        if (MapOpen) LoadMap();
    }

    private void OnShare()
    {
        if (busy) return;

        busy = true;
        SetStatus("Sharing...");
        StartCoroutine(WorldShareService.Share(Finish));
    }

    private void OnUnshare()
    {
        if (busy) return;

        busy = true;
        SetStatus("Removing...");
        StartCoroutine(WorldShareService.Unshare((ok, message) =>
        {
            if (ok)
            {
                ownShared = false;
                RefreshInfo();
                if (MapOpen) LoadMap();
            }
            Finish(ok, message);
        }));
    }

    private void Finish(bool ok, string message)
    {
        busy = false;
        SetStatus(message);
    }

    private void Visit(string playerId)
    {
        if (busy) return;

        busy = true;
        SetStatus("Loading island...");
        StartCoroutine(WorldShareService.Visit(playerId, message =>
        {
            busy = false;
            SetStatus(message);
        }));
    }

    private void GoHome()
    {
        VisitSession.End();
        SceneManager.LoadScene(VisitSession.OwnScene);
    }

    // =========================================================
    // BẢNG TOP ĐẢO (ĐIỂM CAO NHẤT)
    // =========================================================

    private void OpenTopIslands()
    {
        if (topWindow == null)
            topWindow = BuildWindow("Top islands", out _, out topList, out topStatus, () => topWindow.SetActive(false));

        topWindow.SetActive(true);
        EconomyHUD.BringWindowToFront(topWindow);
        topWindow.transform.SetAsLastSibling();
        EconomyHUD.ClearChildren(topList);
        topStatus.text = "Loading islands...";
        StartCoroutine(WorldShareService.LoadGallery(FillTopIslands, message => topStatus.text = message));
    }

    private void FillTopIslands(WorldGalleryResponse page)
    {
        EconomyHUD.ClearChildren(topList);
        if (page.items.Count == 0)
        {
            topStatus.text = "No island has been shared yet.";
            return;
        }

        topStatus.text = "Show puts the island in the middle of the map.";
        foreach (WorldGalleryItem item in page.items)
        {
            var island = new WorldMapIsland { playerId = item.playerId, displayName = item.displayName, score = item.score, x = item.x, y = item.y };
            RectTransform row = AddRow(topList);
            CreateRowLabel(row, item.displayName + "  " + Coords(item.x, item.y) + "  ·  " + item.score + " pts");
            AddRowButton(row, "Show", AccentColor).onClick.AddListener(() =>
            {
                topWindow.SetActive(false);
                PickIsland(island);
            });
        }
    }

    // =========================================================
    // LÔ ĐANG BÁN CỦA MỘT NGƯỜI CHƠI (MUA TẠI ĐÓ)
    // =========================================================

    private void OpenListings(string sellerId, string sellerName)
    {
        if (listingsWindow == null)
            listingsWindow = BuildWindow("", out listingsTitle, out listingsList, out listingsStatus, () => listingsWindow.SetActive(false));

        listingsSellerId = sellerId;
        listingsTitle.text = "Listings from " + sellerName;
        listingsWindow.SetActive(true);
        EconomyHUD.BringWindowToFront(listingsWindow);
        listingsWindow.transform.SetAsLastSibling();
        RefreshListings();
    }

    private void RefreshListings()
    {
        EconomyHUD.ClearChildren(listingsList);
        SetListingsStatus("Loading...");
        StartCoroutine(WorldShareService.LoadListings(listingsSellerId, FillListings, SetListingsStatus));
    }

    private void FillListings(List<MarketListingDto> lots)
    {
        EconomyHUD.ClearChildren(listingsList);
        if (lots.Count == 0)
        {
            SetListingsStatus("This player has nothing for sale.");
            return;
        }

        SetListingsStatus("Buying adds the goods to your inventory and takes Coins from you.");
        foreach (MarketListingDto lot in lots)
        {
            MarketListingDto captured = lot;
            RectTransform row = AddRow(listingsList);
            CreateRowLabel(row, MarketSync.NameOf(lot.resourceId) + " x" + lot.amount + "  ·  " + lot.priceCoins + " Coins");

            AddRowButton(row, "Buy", AccentColor).onClick.AddListener(() => Buy(captured));
        }
    }

    private void Buy(MarketListingDto lot)
    {
        if (MarketSync.Instance == null)
        {
            SetListingsStatus("The market is not ready yet.");
            return;
        }

        MarketSync.Buy(lot, (ok, message) =>
        {
            SetListingsStatus(message);
            if (ok) RefreshListings();
        });
    }

    private void SetListingsStatus(string message)
    {
        if (listingsStatus != null) listingsStatus.text = message;
    }

    // =========================================================
    // DỰNG CỬA SỔ, DÒNG DỮ LIỆU VÀ Ô NHẬP
    // =========================================================

    private GameObject BuildWindow(string title, out TextMeshProUGUI heading, out RectTransform list, out TextMeshProUGUI status, Action close)
    {
        RectTransform windowRect = EconomyHUD.CreateRect("Window", transform);
        EconomyHUD.Stretch(windowRect);
        GameObject window = windowRect.gameObject;
        EconomyHUD.RegisterWindow(window, close);

        RectTransform backdrop = EconomyHUD.CreateRect("Backdrop", windowRect);
        EconomyHUD.Stretch(backdrop);
        EconomyHUD.CreatePanel(backdrop, new Color(0f, 0f, 0f, 0.55f)).raycastTarget = true;

        RectTransform panel = EconomyHUD.CreateRect("Panel", windowRect);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(560f, 640f);
        panel.anchoredPosition = Vector2.zero;
        EconomyHUD.CreatePanel(panel, PanelColor).raycastTarget = true;

        heading = EconomyHUD.CreateText("Title", panel, 22f, TextColor, TextAlignmentOptions.Left);
        SetTopLeft(heading.rectTransform, new Vector2(20f, -16f), new Vector2(420f, 32f));
        heading.enableWordWrapping = false;
        heading.text = title;

        RectTransform closeRect = EconomyHUD.CreateRect("Close", panel);
        SetTopLeft(closeRect, new Vector2(440f, -12f), new Vector2(100f, 40f));
        EconomyHUD.AddButton(closeRect, DangerColor).onClick.AddListener(() => close());
        CreateLabel(closeRect, "Close", 18f);

        list = EconomyHUD.CreateRect("List", panel);
        SetTopLeft(list, new Vector2(20f, -64f), new Vector2(520f, 520f));
        VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;

        status = EconomyHUD.CreateText("Status", panel, 16f, MutedColor, TextAlignmentOptions.Left);
        SetTopLeft(status.rectTransform, new Vector2(20f, -596f), new Vector2(520f, 40f));
        status.enableWordWrapping = true;

        window.SetActive(false);
        return window;
    }

    private static RectTransform AddRow(RectTransform list)
    {
        RectTransform row = EconomyHUD.CreateRect("Row", list);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
        EconomyHUD.CreatePanel(row, RowColor).raycastTarget = false;
        return row;
    }

    private static void CreateRowLabel(RectTransform row, string text)
    {
        TextMeshProUGUI label = EconomyHUD.CreateText("Label", row, 18f, TextColor, TextAlignmentOptions.Left);
        label.text = text;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(14f, 0f);
        rect.offsetMax = new Vector2(-140f, 0f);
    }

    private static Button AddRowButton(RectTransform row, string label, Color color)
    {
        RectTransform rect = EconomyHUD.CreateRect("Button", row);
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-10f, 0f);
        rect.sizeDelta = new Vector2(116f, 40f);
        Button button = EconomyHUD.AddButton(rect, color);
        CreateLabel(rect, label, 18f);
        return button;
    }

    private static void AddButton(RectTransform parent, string label, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = EconomyHUD.CreateRect(label, parent);
        SetTopLeft(rect, position, size);
        EconomyHUD.AddButton(rect, color).onClick.AddListener(onClick);
        CreateLabel(rect, label, 18f);
    }

    private static TMP_InputField CreateInput(RectTransform parent, Vector2 position, Vector2 size, string placeholder, int characterLimit)
    {
        RectTransform rect = EconomyHUD.CreateRect("SearchInput", parent);
        SetTopLeft(rect, position, size);

        Image background = EconomyHUD.CreatePanel(rect, RowColor);
        background.raycastTarget = true;

        TextMeshProUGUI text = EconomyHUD.CreateText("Text", rect, 18f, TextColor, TextAlignmentOptions.Left);
        EconomyHUD.Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(12f, 4f);
        text.rectTransform.offsetMax = new Vector2(-12f, -4f);

        TextMeshProUGUI hint = EconomyHUD.CreateText("Placeholder", rect, 18f, MutedColor, TextAlignmentOptions.Left);
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

    private static TextMeshProUGUI CreateLabel(RectTransform host, string text, float fontSize)
    {
        TextMeshProUGUI label = EconomyHUD.CreateText("Label", host, fontSize, TextColor, TextAlignmentOptions.Center);
        label.text = text;
        EconomyHUD.Stretch(label.rectTransform);
        return label;
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

    // Gốc phủ kín Canvas nhưng 1 đơn vị bên trong bằng 1 pixel, cùng cỡ chữ và vị trí với EconomyHUD
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
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace && canvas.gameObject.scene == scene)
                return canvas.transform;
        }

        // Scene chưa có Canvas màn hình: tạo một Canvas riêng cho bản đồ
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
