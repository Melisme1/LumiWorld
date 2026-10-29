using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Chia sẻ đảo lên server và thăm đảo người khác (giai đoạn 3). Snapshot lấy từ file save của mình trên máy.
/// Mọi hàm là coroutine, gọi bằng StartCoroutine. done/ok nhận lời nhắn tiếng Anh để hiện cho người chơi.
/// </summary>
public static class WorldShareService
{
    public const int MaxSnapshotBytes = 300 * 1024;   // khớp WorldEndpoints.MaxSnapshotBytes trên server
    /// <summary>Lưới bản đồ rộng 10 cột (khớp WorldEndpoints.MapColumns trên server).</summary>
    public const int MapColumns = 10;

    private const string MePath = "/api/v1/worlds/me";
    private const string GalleryPath = "/api/v1/worlds?sort=score&pageSize=20";

    /// <summary>Đưa đảo của mình lên gallery, hoặc cập nhật bản đã chia sẻ.</summary>
    public static IEnumerator Share(Action<bool, string> done)
    {
        string json = BuildSnapshotJson(out string error);
        if (json == null)
        {
            done?.Invoke(false, error);
            yield break;
        }

        string ack = null;
        ApiError failure = null;
        yield return ApiClient.PutRaw(MePath, json, true, text => ack = text, err => failure = err);

        if (string.IsNullOrEmpty(ack))
        {
            done?.Invoke(false, MarketSync.Describe(failure));
            yield break;
        }

        WorldShareResponse result = JsonUtility.FromJson<WorldShareResponse>(ack);
        Shared?.Invoke(result);
        done?.Invoke(true, "Island shared at [" + result.x + ":" + result.y + "]. Score " + result.score + ".");
    }

    /// <summary>Báo cho HUD toạ độ và điểm mới sau khi chia sẻ thành công.</summary>
    public static event Action<WorldShareResponse> Shared;

    /// <summary>
    /// Đảo của mình trên bản đồ: ok nhận null khi chưa chia sẻ (server trả WORLD_NOT_FOUND).
    /// Xem đảo của chính mình không tính là lượt thăm.
    /// </summary>
    public static IEnumerator LoadOwnIsland(Action<WorldVisitResponse> ok, Action<string> fail)
    {
        WorldVisitResponse mine = null;
        ApiError failure = null;
        yield return ApiClient.Get<WorldVisitResponse>("/api/v1/worlds/" + SaveSlot.PlayerId, true, r => mine = r, err => failure = err);

        if (mine != null) ok?.Invoke(mine);
        else if (failure != null && failure.Code == "WORLD_NOT_FOUND") ok?.Invoke(null);
        else fail?.Invoke(MarketSync.Describe(failure));
    }

    /// <summary>Các đảo trong các hàng [top, top + rows) của bản đồ, đủ cả chiều ngang.</summary>
    public static IEnumerator LoadMap(int top, int rows, Action<WorldMapResponse> ok, Action<string> fail)
    {
        string path = "/api/v1/worlds/map?x=1&y=" + Mathf.Max(1, top) + "&w=" + MapColumns + "&h=" + rows;
        WorldMapResponse map = null;
        ApiError failure = null;
        yield return ApiClient.Get<WorldMapResponse>(path, true, r => map = r, err => failure = err);

        if (map != null) ok?.Invoke(map);
        else fail?.Invoke(MarketSync.Describe(failure));
    }

    /// <summary>Tìm đảo theo tên chủ đảo, hoặc theo toạ độ "3:2" / "[3:2]".</summary>
    public static IEnumerator Search(string query, Action<List<WorldMapIsland>> ok, Action<string> fail)
    {
        WorldSearchResponse found = null;
        ApiError failure = null;
        yield return ApiClient.Get<WorldSearchResponse>("/api/v1/worlds/search?q=" + UnityWebRequest.EscapeURL(query), true,
            r => found = r, err => failure = err);

        if (found != null) ok?.Invoke(found.items);
        else fail?.Invoke(MarketSync.Describe(failure));
    }

    /// <summary>Gỡ đảo khỏi gallery. Save trên máy không đổi.</summary>
    public static IEnumerator Unshare(Action<bool, string> done)
    {
        bool removed = false;
        ApiError failure = null;
        yield return ApiClient.Delete<EmptyRequest>(MePath, true, _ => removed = true, err => failure = err);

        done?.Invoke(removed, removed ? "Island removed from the gallery." : MarketSync.Describe(failure));
    }

    public static IEnumerator LoadGallery(Action<WorldGalleryResponse> ok, Action<string> fail)
    {
        WorldGalleryResponse page = null;
        ApiError failure = null;
        yield return ApiClient.Get<WorldGalleryResponse>(GalleryPath, true, r => page = r, err => failure = err);

        if (page != null) ok?.Invoke(page);
        else fail?.Invoke(MarketSync.Describe(failure));
    }

    /// <summary>Tải snapshot của đảo người khác rồi chuyển sang scene VisitWorld.</summary>
    public static IEnumerator Visit(string playerId, Action<string> fail)
    {
        // Thiếu scene VisitWorld trong Build Settings thì báo ngay, không bắt đầu chuyến thăm dở dang
        if (!Application.CanStreamedLevelBeLoaded(VisitSession.VisitScene))
        {
            fail?.Invoke("The visit scene is missing. Run Tools > LumiWorld > Tạo scene VisitWorld (copy MapBuilding).");
            yield break;
        }

        WorldVisitResponse visit = null;
        ApiError failure = null;
        yield return ApiClient.Get<WorldVisitResponse>("/api/v1/worlds/" + playerId, true, r => visit = r, err => failure = err);

        if (visit == null || visit.snapshot == null)
        {
            fail?.Invoke(MarketSync.Describe(failure));
            yield break;
        }

        // Ghi nốt thay đổi của đảo mình trước khi chuyển sang chế độ chỉ xem (lúc đó WorldSaveSystem không ghi nữa)
        if (!WorldLaunch.IsVisit && WorldSaveSystem.Instance != null) WorldSaveSystem.Instance.SaveNow();

        VisitSession.Begin(visit);
        SceneManager.LoadScene(VisitSession.VisitScene);
    }

    /// <summary>Gửi một obstacle tới inbox chủ đảo đang thăm. Server xác thực phí và giới hạn lượt.</summary>
    public static IEnumerator SendObstacle(
        string targetPlayerId,
        string kind,
        int q,
        int r,
        string idempotencyKey,
        Action<ObstacleSendResponse> ok,
        Action<ApiError> fail)
    {
        string target = UnityWebRequest.EscapeURL(targetPlayerId ?? string.Empty);
        var request = new ObstacleSendRequest { kind = kind, q = q, r = r };
        yield return ApiClient.Post<ObstacleSendRequest, ObstacleSendResponse>(
            "/api/v1/worlds/" + target + "/obstacles", request, true, ok, fail, idempotencyKey);
    }

    /// <summary>Lô đang bán của một người chơi, mới nhất trước.</summary>
    public static IEnumerator LoadListings(string sellerId, Action<List<MarketListingDto>> ok, Action<string> fail)
    {
        string path = "/api/v1/market/listings?sellerId=" + sellerId + "&sort=newest&pageSize=50";
        MarketListingsResponse page = null;
        ApiError failure = null;
        yield return ApiClient.Get<MarketListingsResponse>(path, true, r => page = r, err => failure = err);

        if (page != null) ok?.Invoke(page.items);
        else fail?.Invoke(MarketSync.Describe(failure));
    }

    /// <summary>Điểm đảo của mình ghi lần cuối khi còn ở màn chơi; -1 nếu chưa có.</summary>
    public static int LastKnownScore => PlayerPrefs.GetInt(ScoreKey(), -1);

    /// <summary>Ghi lại điểm hiện tại (gọi trước khi rời màn chơi) để Share từ WorldMap gửi đúng điểm.</summary>
    public static void RememberScore()
    {
        if (ScoreManager.Instance == null || !SaveSlot.IsAccount) return;
        PlayerPrefs.SetInt(ScoreKey(), ScoreManager.Instance.CurrentScore);
        PlayerPrefs.Save();
    }

    private static string ScoreKey() => "lumi.world.lastScore." + SaveSlot.PlayerId;

    // Đọc file save của mình rồi cắt còn snapshot. Trả về null và lý do nếu không làm được.
    private static string BuildSnapshotJson(out string error)
    {
        error = null;

        WorldSaveData world = new LocalWorldSaveStore(SaveSlot.PlayerId).Load();
        if (world == null)
        {
            error = "There is no island to share yet.";
            return null;
        }

        // Ngoài màn chơi (scene WorldMap) không có ScoreManager: dùng điểm nhớ lần cuối, chưa có thì không gửi điểm 0
        int score;
        if (ScoreManager.Instance != null)
        {
            score = ScoreManager.Instance.CurrentScore;
            RememberScore();
        }
        else
        {
            score = LastKnownScore;
            if (score < 0)
            {
                error = "Open your island once before sharing it.";
                return null;
            }
        }

        var snapshot = new WorldSnapshot
        {
            score = score,
            tiles = world.tiles ?? new List<TileRecord>(),
        };

        // Chỉ gửi phần người thăm cần; bản lưu của mình không đổi
        if (world.placements != null)
            foreach (PlacementRecord record in world.placements)
                if (record != null) snapshot.placements.Add(SnapshotPlacement.From(record));

        // Ô cháy thành tro (bước 8), để người thăm thấy ô đen và không có cây như chủ đảo
        if (world.disasters != null && world.disasters.tiles != null)
            foreach (DisasterTileRecord tile in world.disasters.tiles)
                if (tile != null && tile.isBurned) snapshot.burned.Add(new HexPointRecord { q = tile.q, r = tile.r });

        string json = JsonUtility.ToJson(snapshot);
        if (Encoding.UTF8.GetByteCount(json) > MaxSnapshotBytes)
        {
            error = "The island is too large to share (over 300 KB).";
            return null;
        }

        return json;
    }
}
