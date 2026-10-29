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

    // Điểm thưởng, mốc đã thưởng và lượt mở rộng đất. progress.saved = false ở bản lưu cũ.
    public WorldProgressData progress = new WorldProgressData();

    // Habitat (id và tài nguyên đã chọn), hàng chưa thu của từng habitat, và trạng thái từng sinh vật
    // (độ no, thức ăn riêng, điểm chăm sóc, tên đặt). Bản lưu cũ để trống: dựng mới như trước.
    public List<HabitatSaveRecord> habitats = new List<HabitatSaveRecord>();
    public List<ProductionSaveRecord> production = new List<ProductionSaveRecord>();
    public List<CreatureSaveRecord> creatures = new List<CreatureSaveRecord>();

    // Thiên tai đang chạy và dấu trên ô (vật cản, cháy, khô, đã tưới). disasters.saved = false ở bản lưu cũ.
    public DisasterSaveData disasters = new DisasterSaveData();
}

/// <summary>
/// Thời gian lưu dạng "còn bao nhiêu giây" tính tại savedAtUtc của bản lưu, không lưu giờ đồng hồ tuyệt đối.
/// </summary>
[Serializable]
public class DisasterSaveData
{
    public bool saved;
    public int instanceCounter;
    public double nextSpawnInSeconds = -1;   // -1: chưa có lịch
    public List<string> appliedInboxObstacleIds = new List<string>();
    public List<DisasterTileRecord> tiles = new List<DisasterTileRecord>();
    public List<DisasterRecord> active = new List<DisasterRecord>();
}

[Serializable]
public class DisasterTileRecord
{
    public int q, r;
    public bool hasObstacle;
    public bool isBurned;
    public float aridity;
    public bool hasReceivedWater;
    public List<DisasterMarkRecord> marks = new List<DisasterMarkRecord>();
}

[Serializable]
public class DisasterMarkRecord
{
    public string kind;
    public string instanceId;
}

[Serializable]
public class DisasterRecord
{
    public string instanceId;
    public string kind;
    public int q, r;
    public int originAffinity;
    public double remainingSeconds;
    public double totalSeconds;
    public int rainApplied;
    public int destroyedCount;
    public double spreadInSeconds = -1;      // -1: không lan
    public List<HexPointRecord> frontier = new List<HexPointRecord>();
    public List<HexPointRecord> suppressed = new List<HexPointRecord>();
    public List<PendingFireRecord> pendingFire = new List<PendingFireRecord>();
}

[Serializable]
public class PendingFireRecord
{
    public int q, r;
    public double elapsedSeconds;
}

[Serializable]
public class HabitatSaveRecord
{
    public string habitatId;
    public string islandId;
    public string natureCardId;
    public int affinity;
    public int island;
    public List<string> memberIds = new List<string>();      // placementId của các ô Nature
    public List<HabitatSlotRecord> slots = new List<HabitatSlotRecord>();
}

[Serializable]
public class HabitatSlotRecord
{
    public int tier;
    public int index;
    public string resourceId;
}

[Serializable]
public class ProductionSaveRecord
{
    public string habitatId;
    public string islandId;
    public string natureCardId;
    public bool isRecovery;
    public List<string> memberIds = new List<string>();
    public List<ProductionBufferRecord> buffers = new List<ProductionBufferRecord>();
}

[Serializable]
public class ProductionBufferRecord
{
    public string resourceId;
    public int tier;
    public long amount;
    public double fractionalCarry;
}

[Serializable]
public class CreatureSaveRecord
{
    public string individualId;           // = placementId của thẻ sinh vật
    public int qualityRank = 1;
    public double satiety = 100;          // độ no đứng yên khi tắt game
    public int status;                    // CreatureHungerStatus
    public int careProgress;              // điểm chăm sóc 0..100 (CreatureCareManager)
    public string customName;
    public List<FoodAmountRecord> foods = new List<FoodAmountRecord>();
}

[Serializable]
public class FoodAmountRecord
{
    public string resourceId;
    public long amount;
}

/// <summary>
/// Tiến trình trên đảo không suy ra được từ thẻ đã đặt: điểm combo, mốc điểm đã thưởng,
/// lượt mở rộng đất còn lại và tâm các mảng đất đã mở.
/// </summary>
[Serializable]
public class WorldProgressData
{
    public bool saved;                    // false: bản lưu cũ, tính lại như trước
    public int bonusScore;                // điểm ngoài thẻ (combo thú)
    public List<int> grantedMilestoneThresholds = new List<int>(); // lưu theo ngưỡng điểm, không theo chỉ số
    public int nextEndlessThreshold = -1;
    public int nextRewardScore;
    public int expansionCharges;
    public List<HexPointRecord> clusterCenters = new List<HexPointRecord>();
}

[Serializable]
public class HexPointRecord
{
    public int q, r;
}

[Serializable]
public class TileRecord
{
    public int q, r;
    public string model;          // khóa prefab trong TileCatalog (GUID của asset)
    public string objectName;     // tên GameObject y hệt lúc lưu, thiên tai tìm ô theo tên
    public int terrainTypeIndex;  // -1 với ô đã tưới Rain
    public int terrainDataVersion; // 0: save cũ cần suy ra một lần từ prefab; 1: dùng dữ liệu môi trường bên dưới
    public bool hasLushEnvironment;
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
    public string connectionCooldownEndUtc; // ISO 8601 UTC lúc thú hết hồi combo; trống = không hồi
    public float connectionCooldownSeconds; // số giây còn lại lúc lưu, dùng khi đồng hồ máy bị lùi
}
