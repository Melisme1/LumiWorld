using System;
using System.Collections.Generic;

// Tên trường khớp JSON của server (JsonUtility so đúng tên trường).

/// <summary>
/// Snapshot đảo gửi lên server (giai đoạn 3): bản rút gọn của WorldSaveData, không có hand, creature hay timer.
/// Thêm trường mới thì không đổi version (server chỉ nhận version 1); snapshot cũ thiếu trường thì để mặc định.
/// </summary>
[Serializable]
public class WorldSnapshot
{
    public int version = WorldSaveData.CurrentVersion;
    public int score;
    public List<TileRecord> tiles = new List<TileRecord>();
    public List<SnapshotPlacement> placements = new List<SnapshotPlacement>();

    // Ô đã cháy thành tro: người thăm thấy ô đen, không có cây. Snapshot cũ không có trường này.
    public List<HexPointRecord> burned = new List<HexPointRecord>();
}

/// <summary>
/// Thẻ đã đặt trong snapshot: chỉ phần người thăm cần để dựng lại đảo (thẻ, ô, cụm, rank, tên, sao).
/// Không gửi id nội bộ (placementId, animalId) và thời gian hồi combo; người thăm dựng lại với id mới.
/// </summary>
[Serializable]
public class SnapshotPlacement
{
    public string cardID;
    public int q, r;
    public int clusterId = -1;
    public int displayedScore;
    public int qualityRank;
    public string animalName;
    public int animalStar;
    public int animalTrait;
    public int riverFlowStep = -1;
    public bool riverIsOrigin;

    public static SnapshotPlacement From(PlacementRecord record) => new SnapshotPlacement
    {
        cardID = record.cardID, q = record.q, r = record.r, clusterId = record.clusterId,
        displayedScore = record.displayedScore, qualityRank = record.qualityRank,
        animalName = record.animalName, animalStar = record.animalStar, animalTrait = record.animalTrait,
        riverFlowStep = record.riverFlowStep, riverIsOrigin = record.riverIsOrigin,
    };

    public PlacementRecord ToRecord() => new PlacementRecord
    {
        cardID = cardID, q = q, r = r, clusterId = clusterId, displayedScore = displayedScore,
        qualityRank = qualityRank, animalName = animalName, animalStar = animalStar, animalTrait = animalTrait,
        riverFlowStep = riverFlowStep, riverIsOrigin = riverIsOrigin,
    };
}

[Serializable]
public class WorldShareResponse
{
    public bool published;
    public int score;
    public int x;
    public int y;
}

[Serializable]
public class WorldGalleryItem
{
    public string playerId;
    public string displayName;
    public int score;
    public string updatedAt;
    public int x;
    public int y;
}

/// <summary>Kết quả của GET /api/v1/worlds (có phân trang).</summary>
[Serializable]
public class WorldGalleryResponse
{
    public List<WorldGalleryItem> items = new List<WorldGalleryItem>();
    public int page;
    public bool hasMore;
}

/// <summary>Kết quả của GET /api/v1/worlds/{playerId}: snapshot của đảo người khác.</summary>
[Serializable]
public class WorldVisitResponse
{
    public string playerId;
    public string displayName;
    public int score;
    public string updatedAt;
    public int x;
    public int y;
    public WorldSnapshot snapshot;
}

/// <summary>Một đảo trên bản đồ thế giới, ô [x:y] cố định (toạ độ bắt đầu từ 1).</summary>
[Serializable]
public class WorldMapIsland
{
    public string playerId;
    public string displayName;
    public int score;
    public int x;
    public int y;
}

/// <summary>Kết quả của GET /api/v1/worlds/map: các đảo trong vùng, kèm kích thước lưới hiện có.</summary>
[Serializable]
public class WorldMapResponse
{
    public int x;
    public int y;
    public int w;
    public int h;
    public int columns;
    public int rows;
    public List<WorldMapIsland> islands = new List<WorldMapIsland>();
}

/// <summary>Kết quả của GET /api/v1/worlds/search?q=.</summary>
[Serializable]
public class WorldSearchResponse
{
    public List<WorldMapIsland> items = new List<WorldMapIsland>();
}
