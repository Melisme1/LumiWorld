using System.Collections.Generic;
using UnityEngine.SceneManagement;

/// <summary>
/// Đảo người khác đang xem (giai đoạn 3). Snapshot tải từ server, giữ cho scene VisitWorld dựng lại.
/// Không ghi gì xuống máy. Rời đảo thì quay về đảo của mình, save của mình không bị ghi lại.
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

    public static void Begin(WorldVisitResponse visit)
    {
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
        WorldLaunch.OpenOwn(SaveSlot.PlayerId);
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
