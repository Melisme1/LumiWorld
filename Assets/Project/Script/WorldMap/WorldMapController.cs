using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene WorldMap: biển với các đảo đã chia sẻ đặt ở ô [x:y] của server, kiểu màn World của Ikariam.
/// Tải bản đồ theo khối ChunkRows hàng quanh vùng camera đang nhìn và nhớ các khối đã tải.
/// Ô trống trong vùng đã tải hiện đảo hoang mờ. Bấm một đảo thì chọn nó và hiện thông tin ở WorldMapHud.
/// Scene tạo bằng Tools > LumiWorld > Tạo scene WorldMap, không cần gắn gì thêm bằng tay.
/// </summary>
public class WorldMapController : MonoBehaviour
{
    public const string SceneName = "WorldMap";
    private const int ChunkRows = 20;

    [SerializeField] private IslandArtSet art;
    [SerializeField] private Color oceanColor = new Color(0.11f, 0.53f, 0.78f);

    public static WorldMapController Instance { get; private set; }

    /// <summary>Mở bản đồ ở ô này lần tới (đảo vừa thăm); null thì mở ở đảo của mình.</summary>
    public static Vector2Int? FocusOnOpen;

    public WorldMapCamera MapCamera { get; private set; }
    public WorldMapIsland Selected => selected != null ? selected.Island : null;
    public WorldMapIsland Own { get; private set; }
    public bool OwnKnown { get; private set; }

    public event System.Action SelectionChanged;
    public event System.Action OwnChanged;
    public event System.Action<string> Status;

    private readonly Dictionary<Vector2Int, WorldMapIslandView> islands = new Dictionary<Vector2Int, WorldMapIslandView>();
    private readonly Dictionary<Vector2Int, WorldMapIslandView> wilds = new Dictionary<Vector2Int, WorldMapIslandView>();
    private readonly HashSet<int> loadedChunks = new HashSet<int>();
    private readonly HashSet<int> loadingChunks = new HashSet<int>();
    private Transform islandRoot;
    private WorldMapIslandView selected;
    private int totalRows = 1;
    private string pendingSelectId;

    private void Awake()
    {
        Instance = this;

        Camera cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (cam == null) cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = oceanColor;
        MapCamera = cam.GetComponent<WorldMapCamera>();
        if (MapCamera == null) MapCamera = cam.gameObject.AddComponent<WorldMapCamera>();
        MapCamera.Clicked += HandleClick;

        islandRoot = new GameObject("Islands").transform;
        islandRoot.SetParent(transform, false);

        WorldMapHud.Create(this);
        WorldShareService.Shared += HandleShared;
    }

    private void OnDestroy()
    {
        WorldShareService.Shared -= HandleShared;
        if (Instance == this) Instance = null;
    }

    private IEnumerator Start()
    {
        if (!AuthSession.IsLoggedIn)
        {
            OwnKnown = true;
            OwnChanged?.Invoke();
            Status?.Invoke("Log in from the main menu to see the world map.");
            yield break;
        }

        // Biết đảo của mình trước để mở bản đồ đúng chỗ và tô vòng sáng
        yield return WorldShareService.LoadOwnIsland(mine => Own = ToMapIsland(mine), message => Status?.Invoke(message));
        OwnKnown = true;
        OwnChanged?.Invoke();
        SelectionChanged?.Invoke();

        Vector2Int start = FocusOnOpen ?? (Own != null ? new Vector2Int(Own.x, Own.y) : new Vector2Int(1, 1));
        FocusOnOpen = null;
        MapCamera.JumpTo(WorldMapLayout.SlotPosition(start.x, start.y));

        yield return LoadChunk(ChunkOf(start.y));
        SelectAt(start.x, start.y);
    }

    private void Update()
    {
        if (!AuthSession.IsLoggedIn || loadedChunks.Count == 0) return;

        // Tải thêm khi kéo tới vùng chưa có; chỉ trong phạm vi số hàng server đang có
        MapCamera.VisibleRows(out int top, out int bottom);
        int first = ChunkOf(Mathf.Max(1, top - 2));
        int last = ChunkOf(Mathf.Min(totalRows, bottom + 2));
        for (int chunk = first; chunk <= last; chunk++)
        {
            if (!loadedChunks.Contains(chunk) && !loadingChunks.Contains(chunk))
                StartCoroutine(LoadChunk(chunk));
        }
    }

    // =========================================================
    // TẢI BẢN ĐỒ
    // =========================================================

    private static int ChunkOf(int row) => Mathf.Max(0, (row - 1) / ChunkRows);

    private IEnumerator LoadChunk(int chunk)
    {
        if (loadedChunks.Contains(chunk) || !loadingChunks.Add(chunk)) yield break;

        int top = chunk * ChunkRows + 1;
        WorldMapResponse map = null;
        yield return WorldShareService.LoadMap(top, ChunkRows, r => map = r, message => Status?.Invoke(message));
        loadingChunks.Remove(chunk);
        if (map == null) yield break;

        loadedChunks.Add(chunk);
        totalRows = Mathf.Max(totalRows, map.rows, 1);
        MapCamera.SetBounds(WorldMapLayout.Bounds(totalRows));

        foreach (WorldMapIsland island in map.islands)
            AddIsland(island);

        // Ô trống trong các hàng đã có trên server: đảo hoang trang trí
        int lastRow = Mathf.Min(top + ChunkRows - 1, totalRows);
        for (int y = top; y <= lastRow; y++)
            for (int x = 1; x <= WorldMapLayout.Columns; x++)
                if (!islands.ContainsKey(new Vector2Int(x, y))) AddWild(x, y);

        if (pendingSelectId != null) SelectPlayer(pendingSelectId);
        if (map.islands.Count == 0 && islands.Count == 0)
            Status?.Invoke("No shared islands yet. Share yours from your island to appear here.");
    }

    private void AddIsland(WorldMapIsland island)
    {
        var key = new Vector2Int(island.x, island.y);
        if (islands.TryGetValue(key, out WorldMapIslandView old) && old != null)
        {
            // Cùng chủ đảo (đã thêm sẵn từ ô tìm kiếm) thì giữ nguyên để không mất vòng chọn
            if (old.Island.playerId == island.playerId) return;
            if (selected == old) Select(null);
            Destroy(old.gameObject);
        }
        if (wilds.TryGetValue(key, out WorldMapIslandView wild) && wild != null)
        {
            Destroy(wild.gameObject);
            wilds.Remove(key);
        }

        bool own = island.playerId == SaveSlot.PlayerId;
        islands[key] = WorldMapIslandView.CreatePlayer(islandRoot, island, art, own);
    }

    private void AddWild(int x, int y)
    {
        var key = new Vector2Int(x, y);
        if (!wilds.ContainsKey(key)) wilds[key] = WorldMapIslandView.CreateWild(islandRoot, x, y, art);
    }

    // =========================================================
    // CHỌN ĐẢO
    // =========================================================

    private void HandleClick(Vector2 screen)
    {
        Vector2 world = MapCamera.Camera.ScreenToWorldPoint(screen);
        Collider2D hit = Physics2D.OverlapPoint(world);
        WorldMapIslandView view = hit != null ? hit.GetComponentInParent<WorldMapIslandView>() : null;
        Select(view);
    }

    public void SelectAt(int x, int y)
    {
        islands.TryGetValue(new Vector2Int(x, y), out WorldMapIslandView view);
        Select(view);
    }

    /// <summary>Bay tới đảo ở ô [x:y] và chọn nó khi đã tải xong (dùng cho ô tìm kiếm).</summary>
    public void FocusIsland(WorldMapIsland island)
    {
        if (island == null) return;
        MapCamera.FlyTo(WorldMapLayout.SlotPosition(island.x, island.y));
        pendingSelectId = island.playerId;
        AddIsland(island);
        SelectPlayer(island.playerId);
        StartCoroutine(LoadChunk(ChunkOf(island.y)));
    }

    public void FocusOwn()
    {
        if (Own != null) FocusIsland(Own);
        else Status?.Invoke("Your island is not on the map yet. Share it from your island first.");
    }

    private void SelectPlayer(string playerId)
    {
        foreach (WorldMapIslandView view in islands.Values)
        {
            if (view != null && view.Island.playerId == playerId)
            {
                pendingSelectId = null;
                Select(view);
                return;
            }
        }
    }

    private void Select(WorldMapIslandView view)
    {
        if (selected == view) return;
        if (selected != null) selected.SetSelected(false);
        selected = view;
        if (selected != null) selected.SetSelected(true);
        SelectionChanged?.Invoke();
    }

    // =========================================================
    // ĐẢO CỦA MÌNH: SHARE / UPDATE / REMOVE
    // =========================================================

    // Share hoặc Update xong: cập nhật đảo của mình trên bản đồ (toạ độ không đổi, điểm có thể đổi)
    private void HandleShared(WorldShareResponse result)
    {
        Own = new WorldMapIsland
        {
            playerId = SaveSlot.PlayerId,
            displayName = AuthSession.DisplayName,
            score = result.score,
            x = result.x,
            y = result.y,
        };

        var key = new Vector2Int(result.x, result.y);
        if (islands.TryGetValue(key, out WorldMapIslandView old) && old != null)
        {
            if (selected == old) selected = null;
            Destroy(old.gameObject);
            islands.Remove(key);
        }
        AddIsland(Own);
        totalRows = Mathf.Max(totalRows, result.y);
        MapCamera.SetBounds(WorldMapLayout.Bounds(totalRows));

        OwnChanged?.Invoke();
        FocusIsland(Own);
    }

    /// <summary>Đảo của mình vừa được gỡ khỏi bản đồ: ô đó thành đảo hoang (server giữ ô cho lần Share sau).</summary>
    public void RemoveOwn()
    {
        if (Own != null)
        {
            var key = new Vector2Int(Own.x, Own.y);
            if (islands.TryGetValue(key, out WorldMapIslandView view) && view != null)
            {
                if (selected == view) Select(null);
                Destroy(view.gameObject);
            }
            islands.Remove(key);
            if (loadedChunks.Contains(ChunkOf(Own.y))) AddWild(Own.x, Own.y);
        }

        Own = null;
        OwnChanged?.Invoke();
    }

    // =========================================================
    // CHUYỂN SCENE
    // =========================================================

    /// <summary>Về đảo của mình (scene MapBuilding).</summary>
    public static void GoHome()
    {
        if (VisitSession.Active) VisitSession.End();
        else WorldLaunch.OpenOwn(SaveSlot.PlayerId);
        SceneManager.LoadScene(VisitSession.OwnScene);
    }

    /// <summary>Mở WorldMap từ màn chơi: lưu đảo trước để không mất thay đổi.</summary>
    public static void Open(Vector2Int? focus = null)
    {
        if (!WorldLaunch.IsVisit && WorldSaveSystem.Instance != null) WorldSaveSystem.Instance.SaveNow();

        // WorldMap không có ScoreManager: nhớ điểm hiện tại để Share từ đó gửi đúng điểm
        if (!WorldLaunch.IsVisit) WorldShareService.RememberScore();

        // Mở từ VisitWorld: kết thúc chuyến thăm để WorldMap và lần vào đảo sau là của mình
        if (VisitSession.Active) VisitSession.End();
        FocusOnOpen = focus;
        SceneManager.LoadScene(SceneName);
    }

    private static WorldMapIsland ToMapIsland(WorldVisitResponse visit)
    {
        if (visit == null) return null;
        return new WorldMapIsland
        {
            playerId = visit.playerId,
            displayName = visit.displayName,
            score = visit.score,
            x = visit.x,
            y = visit.y,
        };
    }
}
