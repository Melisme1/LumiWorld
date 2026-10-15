using System;
using System.Collections.Generic;

/// <summary>
/// Toàn bộ world của một người chơi, ghi thành world_{playerId}.json cạnh file kinh tế.
/// Chỉ dùng kiểu mà JsonUtility đọc được: list và class [Serializable], không Dictionary.
/// Thêm trường mới thì không cần đổi version. Đổi tên hoặc bỏ trường thì phải tăng CurrentVersion và viết hàm nâng cấp.
/// </summary>
[Serializable]
public class WorldSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public string playerId;
    public string savedAtUtc;   // ISO 8601, giờ UTC

    // Only the New Game reset writes this marker with an empty tile list. An
    // unexpectedly empty legacy save must still be treated as a restore error.
    public bool initializeNewWorld;

    // Mọi ô đất, gồm cả mảnh mở rộng và ô đã tưới Rain
    public List<TileRecord> tiles = new List<TileRecord>();

    // Đúng thứ tự đã đặt, để lúc nạp lại không đổi cụm và điểm
    public List<PlacementRecord> placements = new List<PlacementRecord>();
    public int nextClusterId;
}

[Serializable]
public class TileRecord
{
    public int q, r;
    public string model;          // khóa prefab trong TileCatalog (GUID của asset)
    public string objectName;     // tên GameObject y hệt lúc lưu, thiên tai tìm ô theo tên
    public int terrainTypeIndex;  // -1 với ô đã tưới Rain
    public int rotationStep;      // 0..5, mỗi bước 60 độ
    public bool isOccupied;
}

[Serializable]
public class PlacementRecord
{
    public string cardID;       // tra trong CardCatalog
    public int q, r;
    public int clusterId = -1;
    public int displayedScore;
    public string placementId;  // stable habitat/resident identity across reloads
    public int qualityRank;      // 0 in older saves means Rank I
    public string animalId;
    public string animalName;
    public int animalStar;       // 0 in older saves means roll once on restore
    public int animalTrait;
}
