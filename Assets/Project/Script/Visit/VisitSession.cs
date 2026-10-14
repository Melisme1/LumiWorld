using System.Collections.Generic;
using UnityEngine.SceneManagement;

/// <summary>
/// Đảo người khác đang xem (giai đoạn 3). Snapshot tải từ server, giữ cho scene VisitWorld dựng lại.
/// Không ghi gì xuống máy. Rời đảo thì quay về WorldMap (nếu đi từ đó) hoặc đảo của mình,
/// save của mình không bị ghi lại.
/// </summary>
public static class VisitSession
{
    public const string OwnScene = "MapBuilding";
    public const string VisitScene = "VisitWorld";

    public static WorldSnapshot Snapshot { get; private set; }
    public static string OwnerId { get; private set; } = "";
    public static string OwnerName { get; private set; } = "";
    public static int OwnerScore { get; private set; }
    public static int OwnerX { get; private set; }
    public static int OwnerY { get; private set; }

    public static bool Active => Snapshot != null;

    /// <summary>Chuyến thăm bắt đầu từ scene WorldMap: nút Back quay về đó, đúng đảo vừa thăm.</summary>
    public static bool FromWorldMap { get; private set; }

    public static void Begin(WorldVisitResponse visit)
    {
        FromWorldMap = SceneManager.GetActiveScene().name == WorldMapController.SceneName;
        Snapshot = visit.snapshot;
        OwnerId = visit.playerId ?? "";
        OwnerName = visit.displayName ?? "";
        OwnerScore = visit.score;
        OwnerX = visit.x;
        OwnerY = visit.y;
        WorldLaunch.OpenVisit(OwnerId);
    }

    /// <summary>Bỏ snapshot và trở về chế độ đảo của mình.</summary>
    public static void End()
    {
        Snapshot = null;
        OwnerId = "";
        OwnerName = "";
        OwnerScore = 0;
        OwnerX = 0;
        OwnerY = 0;
        FromWorldMap = false;
        WorldLaunch.OpenOwn(SaveSlot.PlayerId);
    }

    /// <summary>
    /// Nút Back khi đang thăm: về WorldMap đúng đảo vừa thăm (đảo đó được chọn sẵn), hoặc về đảo của mình
    /// nếu chuyến thăm không bắt đầu từ WorldMap hay WorldMap chưa có trong Build Settings.
    /// </summary>
    public static void Leave()
    {
        bool toMap = FromWorldMap && UnityEngine.Application.CanStreamedLevelBeLoaded(WorldMapController.SceneName);
        var focus = new UnityEngine.Vector2Int(OwnerX, OwnerY);

        // Kết thúc chuyến thăm trước khi đổi scene để save của mình chạy lại bình thường
        End();
        if (toMap)
        {
            WorldMapController.FocusOnOpen = focus;
            SceneManager.LoadScene(WorldMapController.SceneName);
        }
        else
        {
            SceneManager.LoadScene(OwnScene);
        }
    }

    /// <summary>Snapshot dưới dạng WorldSaveData để WorldSaveSystem dựng lại như một đảo đã lưu.</summary>
    public static WorldSaveData ToSaveData()
    {
        return new WorldSaveData
        {
            playerId = OwnerId,
            tiles = Snapshot.tiles ?? new List<TileRecord>(),
            placements = Snapshot.placements ?? new List<PlacementRecord>(),
        };
    }
}
