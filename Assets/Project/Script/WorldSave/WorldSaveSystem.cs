using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Ghi và nạp world của người chơi. Đặt object này trong MapBuilding; DefaultExecutionOrder -300
/// để Start chạy trước HexWorldGenerator, nên ô đất được dựng lại trước khi generator kịp sinh đảo mới.
/// Các thay đổi gọi MarkDirty() hoặc RegisterPlacement(); file được ghi trong LateUpdate của frame đó.
/// Hiện đã nạp ô đất (bước 1). Thẻ đã lưu chưa dựng lại được nên giữ nguyên trong keptPlacements,
/// thẻ đặt thêm trong phiên này nối sau; bước 2 sẽ dựng lại thẻ và xóa keptPlacements.
/// Ở chế độ chỉ xem (WorldLaunch.IsVisit) không ghi gì; đảo được nạp từ file của chủ đảo.
/// </summary>
[DefaultExecutionOrder(-300)]
public class WorldSaveSystem : MonoBehaviour
{
    public static WorldSaveSystem Instance { get; private set; }

    [SerializeField] private TileCatalog tileCatalog;

    // Chỉ để thử: nhập mã người chơi để mở đảo của họ ở chế độ chỉ xem. Để trống thì mở đảo của mình.
    [SerializeField] private string devVisitWorldId = "";

    private IWorldSaveStore store;
    private readonly List<PlacedCard> placedCards = new List<PlacedCard>();
    private List<PlacementRecord> keptPlacements = new List<PlacementRecord>();
    private bool dirty;

    // Có file world nhưng dựng lại không đủ ô: không ghi đè file tốt (và không đẩy lên server) trong phiên này
    private bool savesBlocked;

    /// <summary>true khi ô đất đã được dựng lại từ file, HexWorldGenerator không sinh đảo mới.</summary>
    public bool HasRestoredTiles { get; private set; }

    private void Awake()
    {
        Instance = this;

        string visitId = devVisitWorldId == null ? "" : devVisitWorldId.Trim();
        if (VisitSession.Active) WorldLaunch.OpenVisit(VisitSession.OwnerId);
        else if (visitId.Length > 0 && visitId != SaveSlot.PlayerId) WorldLaunch.OpenVisit(visitId);
        else WorldLaunch.OpenOwn(SaveSlot.PlayerId);

        store = new LocalWorldSaveStore(WorldLaunch.WorldId);
    }

    private void Start()
    {
        // Đảo người khác tải từ server (VisitSession) thì dựng từ snapshot, không đọc file trên máy
        WorldSaveData data = VisitSession.Active ? VisitSession.ToSaveData() : store.Load();
        if (data == null)
        {
            if (WorldLaunch.IsVisit)
                Debug.LogWarning($"[LumiWorld World] Không có file world của {WorldLaunch.WorldId} trên máy. Đảo sinh ra chỉ để xem, không được lưu.");
            return;
        }

        keptPlacements = data.placements ?? new List<PlacementRecord>();

        HexWorldGenerator generator = FindFirstObjectByType<HexWorldGenerator>();
        if (generator == null || tileCatalog == null || data.tiles == null || data.tiles.Count == 0)
        {
            BlockSaves("Có file world nhưng chưa dựng được ô đất (thiếu HexWorldGenerator hoặc TileCatalog). Đảo hiện ra là đảo tạm.");
            return;
        }

        HasRestoredTiles = generator.RestoreTiles(data.tiles, tileCatalog);

        // Thiếu prefab trong TileCatalog: ô bị bỏ qua sẽ mất ở lần ghi sau, nên giữ nguyên file cũ
        var savedCoords = new HashSet<HexCoordinates>();
        foreach (TileRecord tile in data.tiles) savedCoords.Add(new HexCoordinates(tile.q, tile.r));
        if (generator.MapTiles.Count < savedCoords.Count)
            BlockSaves($"Chỉ dựng lại được {generator.MapTiles.Count}/{savedCoords.Count} ô đất (TileCatalog thiếu prefab). Bấm Tools > LumiWorld > Xây danh mục save rồi chạy lại.");
    }

    private void BlockSaves(string reason)
    {
        // Không áp dụng khi chỉ xem: chế độ đó vốn không ghi gì
        if (WorldLaunch.IsVisit) return;

        savesBlocked = true;
        Debug.LogError("[LumiWorld World] " + reason + " Phiên này không ghi world để khỏi đè mất đảo đã lưu.");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        if (dirty) SaveNow();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && dirty) SaveNow();
    }

    private void OnApplicationQuit()
    {
        if (dirty) SaveNow();
    }

    /// <summary>Đánh dấu world đã đổi; frame này sẽ ghi file.</summary>
    public static void MarkDirty()
    {
        if (Instance != null && !WorldLaunch.IsVisit) Instance.dirty = true;
    }

    /// <summary>Ghi nhận một thẻ vừa đặt. Gọi cả khi card null (đặt không tạo PlacedCard) để vẫn ghi file.</summary>
    public static void RegisterPlacement(PlacedCard card)
    {
        if (Instance == null || WorldLaunch.IsVisit) return;

        if (card != null) Instance.placedCards.Add(card);
        Instance.dirty = true;
    }

    /// <summary>Chụp world từ object đang sống rồi ghi ra file. Trả về false nếu ghi lỗi (sẽ thử lại).</summary>
    public bool SaveNow()
    {
        // Chế độ chỉ xem không ghi file của người khác; đang tải bản server sau xung đột thì không ghi bản cũ đè lên
        if (store == null || WorldLaunch.IsVisit || SaveSync.SavesSuspended) return false;
        if (savesBlocked)
        {
            dirty = false;
            return false;
        }

        WorldSaveData data = Capture();
        if (!store.Save(data)) return false;

        dirty = false;
        SaveSync.QueueUpload();
        return true;
    }

    private WorldSaveData Capture()
    {
        placedCards.RemoveAll(card => card == null);

        var data = new WorldSaveData
        {
            playerId = SaveSlot.PlayerId,
            savedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
        };

        HexWorldGenerator generator = FindFirstObjectByType<HexWorldGenerator>();
        if (generator != null)
        {
            foreach (KeyValuePair<HexCoordinates, GameObject> pair in generator.MapTiles)
            {
                if (pair.Value == null) continue;

                TileRecord tile = CaptureTile(pair.Key, pair.Value);
                if (tile != null) data.tiles.Add(tile);
            }
        }

        // Thẻ đã lưu chưa dựng lại (keptPlacements) đứng trước, thẻ đặt trong phiên này đứng sau
        data.placements.AddRange(keptPlacements);
        foreach (PlacedCard card in placedCards)
        {
            if (card.cardData == null) continue;

            data.placements.Add(new PlacementRecord
            {
                cardID = card.CardID,
                q = card.placedHex.Q,
                r = card.placedHex.R,
                clusterId = card.clusterId,
                displayedScore = card.displayedScore,
            });
        }

        return data;
    }

    private TileRecord CaptureTile(HexCoordinates coords, GameObject tileObject)
    {
        HexTileInfo info = tileObject.GetComponent<HexTileInfo>();
        string model = info != null && info.sourcePrefab != null && tileCatalog != null ? tileCatalog.KeyOf(info.sourcePrefab) : "";
        if (string.IsNullOrEmpty(model))
        {
            Debug.LogError($"[LumiWorld World] Ô {tileObject.name} không có prefab trong TileCatalog, không lưu được. Bấm Tools > LumiWorld > Xây danh mục save.", tileObject);
            return null;
        }

        return new TileRecord
        {
            q = coords.Q,
            r = coords.R,
            model = model,
            objectName = tileObject.name,
            terrainTypeIndex = info.terrainTypeIndex,
            rotationStep = RotationStep(tileObject.transform.rotation),
            isOccupied = info.isOccupied,
        };
    }

    /// <summary>Góc xoay quanh trục Y đổi ra số bước 60 độ, 0..5.</summary>
    public static int RotationStep(Quaternion rotation)
    {
        int step = Mathf.RoundToInt(rotation.eulerAngles.y / 60f) % 6;
        return step < 0 ? step + 6 : step;
    }
}
