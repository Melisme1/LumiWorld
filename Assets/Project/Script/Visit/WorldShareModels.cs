using System;
using System.Collections.Generic;

// Tên trường khớp JSON của server (JsonUtility so đúng tên trường).

/// <summary>
/// Snapshot đảo gửi lên server (giai đoạn 3): bản rút gọn của WorldSaveData, không có hand, creature hay timer.
/// </summary>
[Serializable]
public class WorldSnapshot
{
    public int version = WorldSaveData.CurrentVersion;
    public int score;
    public List<TileRecord> tiles = new List<TileRecord>();
    public List<PlacementRecord> placements = new List<PlacementRecord>();
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
