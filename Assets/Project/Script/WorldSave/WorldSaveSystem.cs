using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Ghi và nạp world của người chơi. Đặt object này trong MapBuilding; DefaultExecutionOrder -300
/// để Start chạy trước HexWorldGenerator, nên ô đất được dựng lại trước khi generator kịp sinh đảo mới.
/// Các thay đổi gọi MarkDirty() hoặc RegisterPlacement(); file được ghi trong LateUpdate của frame đó.
/// Dựng lại ô đất và thẻ đã đặt; ngăn ghi đè save nếu thiếu asset cần để khôi phục.
/// Ở chế độ chỉ xem (WorldLaunch.IsVisit) không ghi gì; đảo được nạp từ file của chủ đảo.
/// </summary>
[DefaultExecutionOrder(-300)]
public class WorldSaveSystem : MonoBehaviour
{
    public static WorldSaveSystem Instance { get; private set; }

    [SerializeField] private TileCatalog tileCatalog;
    [SerializeField] private CardCatalog cardCatalog;

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

    /// <summary>
    /// true khi Start đã chạy xong phần dựng lại đảo (kể cả khi không có file hoặc bị chặn ghi).
    /// Hệ thống tự đánh giá theo đảo (thành tựu...) đợi cờ này để không đọc đảo đang dựng dở.
    /// </summary>
    public bool IsWorldReady { get; private set; }

    /// <summary>
    /// Bản lưu đảo của mình vừa nạp trong Start (null khi đang thăm, đảo mới hoặc chưa có file).
    /// Hệ thống chạy sau (thiên tai, thứ tự -80) đọc phần của mình ở đây.
    /// </summary>
    public WorldSaveData RestoredData { get; private set; }

    /// <summary>Danh mục thẻ dùng để dựng lại thẻ đã đặt; bài trên tay cũng tra cardID ở đây.</summary>
    public CardCatalog Cards => cardCatalog;

    /// <summary>true khi phiên này không được ghi save (dựng lại đảo thiếu asset).</summary>
    public bool SavesBlocked => savesBlocked;

    /// <summary>
    /// Bỏ một thẻ đã đặt khỏi danh sách lưu ngay (trước khi Destroy, vì Destroy chỉ xóa object
    /// sau LateUpdate của frame này) rồi đánh dấu cần ghi. Dùng khi thẻ rời đảo ngoài luồng đặt bài,
    /// ví dụ cháy rừng trả thú về tay, để thú không vừa nằm trên đảo vừa nằm trên tay.
    /// </summary>
    public static void ForgetPlacement(PlacedCard card)
    {
        if (Instance == null || card == null || WorldLaunch.IsVisit) return;
        Instance.placedCards.Remove(card);
        Instance.dirty = true;
    }

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
        RestoreWorld();
        IsWorldReady = true;

        // Dựng lại thất bại: đảo đang thấy chỉ là đảo tạm và không được lưu. Báo cho người chơi biết,
        // để không chơi tiếp trên đảo tạm rồi tưởng đảo bị reset khi vào lại (chi tiết lỗi nằm trong Console).
        if (savesBlocked)
            GameDialogUI.ShowNotice("Không mở được đảo đã lưu, đảo đang thấy là đảo tạm và sẽ KHÔNG được lưu. "
                + "Đảo cũ vẫn còn nguyên trong bản lưu. Hãy báo nhóm phát triển và đừng xây tiếp trên đảo này.");
    }

    private void RestoreWorld()
    {
        // Đảo người khác tải từ server (VisitSession) thì dựng từ snapshot, không đọc file trên máy
        WorldSaveData data = VisitSession.Active ? VisitSession.ToSaveData() : store.Load();
        if (data == null)
        {
            if (WorldLaunch.IsVisit)
                Debug.LogWarning($"[LumiWorld World] Không có file world của {WorldLaunch.WorldId} trên máy. Đảo sinh ra chỉ để xem, không được lưu.");
            return;
        }

        // New Game writes an intentionally empty world to server and disk first. The
        // generator will create its starting island and save it in its own Start().
        if (!WorldLaunch.IsVisit && data.initializeNewWorld && data.tiles != null && data.tiles.Count == 0 &&
            (data.placements == null || data.placements.Count == 0)) return;

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

        if (savesBlocked || !HasRestoredTiles) return;
        if (!WorldLaunch.IsVisit) RestoredData = data;
        SeedHabitatsAndCreatures(data);
        RestorePlacements(data, generator);
        RestoreProgress(data, generator);
        AccrueOfflineProduction(data);
    }

    // Trước khi dựng thẻ: habitat (id, tài nguyên đã chọn), hàng chưa thu, độ no, thức ăn, điểm chăm sóc, tên.
    // Lần dựng habitat đầu tiên (trong RestorePlacements) sẽ khớp theo các id này thay vì tạo mới.
    private void SeedHabitatsAndCreatures(WorldSaveData data)
    {
        if (WorldLaunch.IsVisit) return;

        if (LumiWorld.Acs.HabitatRuntimeManager.TryGetActive(out var habitatManager))
            habitatManager.SeedFromSave(data.habitats);

        var production = LumiWorld.Acs.ResourceProductionRuntime.Instance;
        if (production != null) production.SeedFromSave(data.production, data.creatures);

        CreatureCareManager care = CreatureCareManager.Instance;
        if (care != null && data.creatures != null)
            foreach (CreatureSaveRecord creature in data.creatures)
                if (creature != null) care.RestoreCreature(creature.individualId, creature.careProgress, creature.customName);
    }

    // Sau khi đảo dựng xong: cộng sản lượng cho quãng tắt game (giờ thật, tối đa 8 giờ).
    // Đồng hồ máy bị lùi thì không cộng.
    private static void AccrueOfflineProduction(WorldSaveData data)
    {
        if (WorldLaunch.IsVisit) return;
        if (!DateTime.TryParse(data.savedAtUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime savedAtUtc)) return;
        double offline = (DateTime.UtcNow - savedAtUtc).TotalSeconds;
        if (offline <= 0) return;

        var production = LumiWorld.Acs.ResourceProductionRuntime.Instance;
        if (production != null) production.AccrueOffline(offline);
    }

    // Điểm combo, mốc điểm đã thưởng, lượt mở rộng và tâm mảng đất. Chạy trong Start (order -300),
    // trước Start của ScoreUI, WildPackIncidentManager và HexPlacementController.
    // Đang xem đảo người khác thì không dựng tiến trình (snapshot không có phần này).
    private void RestoreProgress(WorldSaveData data, HexWorldGenerator generator)
    {
        if (WorldLaunch.IsVisit || savesBlocked) return;
        WorldProgressData progress = data.progress;

        if (progress != null && progress.saved && ScoreManager.Instance != null)
        {
            int cardScore = ScoreCalculator.Instance != null ? ScoreCalculator.Instance.CalculateTotalScore() : 0;
            ScoreManager.Instance.RestoreProgress(cardScore, progress);
        }

        if (HexPlacementController.Instance != null)
            HexPlacementController.Instance.RestoreExpansion(progress, generator);
    }

    // Giờ hết hồi combo của thú: trừ thời gian đã trôi khi game tắt. Đồng hồ máy bị lùi thì giữ số giây đã lưu.
    private static void RestoreConnectionCooldown(PlacementRecord record, GameObject placedObject, DateTime savedAtUtc)
    {
        if (string.IsNullOrEmpty(record.connectionCooldownEndUtc) || placedObject == null) return;
        FarmAnimalAI ai = placedObject.GetComponentInChildren<FarmAnimalAI>(true);
        if (ai == null) return;

        float remaining = record.connectionCooldownSeconds;
        DateTime now = DateTime.UtcNow;
        if (now >= savedAtUtc &&
            DateTime.TryParse(record.connectionCooldownEndUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime endUtc))
        {
            remaining = Mathf.Min(record.connectionCooldownSeconds, (float)(endUtc - now).TotalSeconds);
        }
        ai.RestoreConnectionCooldown(remaining);
    }

    private void RestorePlacements(WorldSaveData data, HexWorldGenerator generator)
    {
        if (keptPlacements.Count == 0)
        {
            ReconcileOccupancy(generator);
            return;
        }
        if (cardCatalog == null)
        {
            BlockSaves("Chưa gán CardCatalog trên WorldSaveSystem; không thể dựng lại thẻ đã đặt.");
            return;
        }

        // Validate the entire saved set before creating anything. Never silently lose an unresolved card.
        foreach (PlacementRecord record in keptPlacements)
        {
            if (record == null || !generator.MapTiles.ContainsKey(new HexCoordinates(record.q, record.r)))
            {
                BlockSaves("Bản lưu chứa thẻ trên ô đất không khôi phục được.");
                return;
            }
            CardData card = cardCatalog.Find(record.cardID);
            if (card == null || (card.cardType == CardType.Creature && card.prefabToPlace == null))
            {
                BlockSaves($"CardCatalog thiếu thẻ/prefab '{record.cardID}' tại ({record.q}, {record.r}). Chạy Tools > LumiWorld > Xây danh mục save và kiểm tra asset.");
                return;
            }
        }

        int duplicateCount = RemoveDuplicatePlacements();

        int maxClusterId = Mathf.Max(0, data.nextClusterId - 1);
        foreach (PlacementRecord record in keptPlacements)
            maxClusterId = Mathf.Max(maxClusterId, record.clusterId);
        HexBiomeClusterConnector connector = HexBiomeClusterConnector.Instance;
        if (connector != null) connector.ReserveClusterIdsThrough(maxClusterId);

        // Giờ ghi bản lưu, để biết đồng hồ máy có bị lùi không khi tính thời gian hồi combo còn lại
        if (!DateTime.TryParse(data.savedAtUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime savedAtUtc))
            savedAtUtc = DateTime.UtcNow;

        // Terrain first: creatures need their Nature card to bind to a home biome.
        int restored = 0;
        bool upgradedLegacyIds = false;
        for (int phase = 0; phase < 2; phase++)
        {
            foreach (PlacementRecord record in keptPlacements)
            {
                CardData card = cardCatalog.Find(record.cardID);
                if ((card.cardType == CardType.Creature) != (phase == 1)) continue;
                try
                {
                    PlacedCard placed = RestorePlacement(record, card, generator);
                    placedCards.Add(placed);
                    if (card.cardType == CardType.Creature) RestoreConnectionCooldown(record, placed.gameObject, savedAtUtc);
                    restored++;
                    if ((card.cardType == CardType.Terrain || card.cardType == CardType.Creature) &&
                        string.IsNullOrEmpty(record.placementId)) upgradedLegacyIds = true;
                    if (card.cardType == CardType.Creature && record.animalStar <= 0) upgradedLegacyIds = true;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                    BlockSaves($"Không dựng được thẻ '{record.cardID}' tại ({record.q}, {record.r}).");
                    return;
                }
            }
        }

        if (connector != null)
        {
            try
            {
                foreach (PlacedCard placed in placedCards)
                    if (placed != null && placed.cardData != null && placed.cardData.cardType == CardType.Terrain)
                        connector.ConnectCluster(placed);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                BlockSaves("Không nối được các cụm Nature sau khi khôi phục.");
                return;
            }
        }

        keptPlacements.Clear();
        if (upgradedLegacyIds || duplicateCount > 0) MarkDirty();
        ReconcileOccupancy(generator);
        LumiWorld.Acs.HabitatRuntimeManager.NotifyWorldChanged();
        if (LumiWorld.Acs.HabitatRuntimeManager.TryGetActive(out var habitat)) habitat.RefreshNow();
        if (ScoreCalculator.Instance != null && ScoreManager.Instance != null && HexGroupDetector.Instance != null)
            ScoreManager.Instance.RestoreScoreWithoutRewards(ScoreCalculator.Instance.CalculateTotalScore());
        Debug.Log($"[LumiWorld World] Đã dựng lại {restored} thẻ đã đặt và cư dân từ bản lưu.", this);
    }

    private int RemoveDuplicatePlacements()
    {
        // Earlier tile-only loads retained old records and appended newly placed Nature on top.
        // One home hex can have one Nature and one creature; use the newest record of each kind.
        var lastIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < keptPlacements.Count; i++)
        {
            PlacementRecord record = keptPlacements[i];
            CardData card = cardCatalog.Find(record.cardID);
            string kind = card.cardType == CardType.Terrain ? "terrain" :
                card.cardType == CardType.Creature ? "creature" :
                card.IsTileTransformCard() ? "transform" : null;
            if (kind != null) lastIndex[$"{record.q},{record.r}:{kind}"] = i;
        }

        var unique = new List<PlacementRecord>(keptPlacements.Count);
        for (int i = 0; i < keptPlacements.Count; i++)
        {
            PlacementRecord record = keptPlacements[i];
            CardData card = cardCatalog.Find(record.cardID);
            string kind = card.cardType == CardType.Terrain ? "terrain" :
                card.cardType == CardType.Creature ? "creature" :
                card.IsTileTransformCard() ? "transform" : null;
            if (kind == null || lastIndex[$"{record.q},{record.r}:{kind}"] == i)
                unique.Add(record);
        }
        int removed = keptPlacements.Count - unique.Count;
        if (removed > 0)
        {
            keptPlacements = unique;
            Debug.LogWarning($"[LumiWorld World] Bỏ {removed} record Nature/creature/Rain trùng trên cùng ô; giữ thẻ mới nhất. Bản lưu gốc còn trong .bak sau lần ghi kế tiếp.", this);
        }
        return removed;
    }

    private static PlacedCard RestorePlacement(PlacementRecord record, CardData card, HexWorldGenerator generator)
    {
        HexCoordinates coords = new HexCoordinates(record.q, record.r);
        GameObject tile = generator.MapTiles[coords];
        var (surfaceY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tile);
        Vector3 position = HexMetrics.HexToWorldPosition(coords, surfaceY) + card.placementOffset;
        GameObject objectToPlace;
        if (card.IsTileTransformCard())
        {
            // TileRecord already stores the transformed tile. Replaying Rain would replace it twice.
            objectToPlace = new GameObject($"PlacedCard_{card.cardName}");
            objectToPlace.transform.SetParent(tile.transform, false);
        }
        else if (card.HasHabitatProps())
        {
            HexHabitatSpawner.Instance.SpawnHabitat(card, tile.transform, position);
            objectToPlace = new GameObject($"PlacedCard_{card.cardName}");
            objectToPlace.transform.SetParent(tile.transform, false);
        }
        else if (card.prefabToPlace != null)
        {
            objectToPlace = Instantiate(card.prefabToPlace, position, Quaternion.identity, tile.transform);
        }
        else
        {
            objectToPlace = new GameObject($"PlacedCard_{card.cardName}");
            objectToPlace.transform.SetParent(tile.transform, false);
        }

        PlacedCard placed = objectToPlace.GetComponent<PlacedCard>();
        if (placed == null) placed = objectToPlace.AddComponent<PlacedCard>();
        placed.cardData = card;
        placed.placedHex = coords;
        placed.clusterId = record.clusterId;
        placed.displayedScore = record.displayedScore;

        if (card.cardType == CardType.Creature && card.animalSpeciesData != null)
        {
            AnimalIndividual animal = objectToPlace.GetComponent<AnimalIndividual>();
            if (animal == null) animal = objectToPlace.AddComponent<AnimalIndividual>();
            AnimalTrait? trait = record.animalStar > 0 && Enum.IsDefined(typeof(AnimalTrait), record.animalTrait)
                ? (AnimalTrait?)record.animalTrait : null;
            animal.RestoreSavedState(card.animalSpeciesData, tile, record.animalId,
                record.animalName, record.animalStar, trait);
        }
        if (card.cardType == CardType.Terrain || card.cardType == CardType.Creature)
        {
            var identity = LumiWorld.Acs.HabitatPlacementIdentity.BindPlacedCard(placed);
            identity.Restore(record.placementId, (LumiWorld.Acs.CreatureQualityRank)record.qualityRank, placed);
        }
        if (card.occupiesTile)
        {
            HexTileInfo info = tile.GetComponent<HexTileInfo>();
            if (info != null) info.isOccupied = true;
        }
        return placed;
    }

    private void ReconcileOccupancy(HexWorldGenerator generator)
    {
        foreach (var pair in generator.MapTiles)
        {
            if (pair.Value == null) continue;
            HexTileInfo info = pair.Value.GetComponent<HexTileInfo>();
            if (info == null || !info.isOccupied) continue;
            bool occupiedByCard = false;
            foreach (PlacedCard placed in pair.Value.GetComponentsInChildren<PlacedCard>(true))
                if (placed != null && placed.cardData != null && placed.cardData.occupiesTile)
                { occupiedByCard = true; break; }
            if (occupiedByCard) continue;
            info.isOccupied = false;
            MarkDirty();
            Debug.LogWarning($"[LumiWorld World] Ô {pair.Key} có cờ chiếm dụng cũ nhưng không có thẻ tương ứng; đã giải phóng ô.", pair.Value);
        }
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
        if (HexBiomeClusterConnector.Instance != null)
            data.nextClusterId = HexBiomeClusterConnector.Instance.NextClusterId;

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

        // Unresolved records remain only when restore failed; SaveNow is blocked in that case.
        data.placements.AddRange(keptPlacements);
        foreach (PlacedCard card in placedCards)
        {
            if (card.cardData == null) continue;

            var identity = card.GetComponent<LumiWorld.Acs.HabitatPlacementIdentity>();
            var animal = card.GetComponent<AnimalIndividual>();
            var ai = card.GetComponentInChildren<FarmAnimalAI>(true);
            float cooldown = ai != null ? Mathf.Max(0f, ai.cooldownRemainingSeconds) : 0f;
            data.placements.Add(new PlacementRecord
            {
                connectionCooldownSeconds = cooldown,
                connectionCooldownEndUtc = cooldown > 0f
                    ? DateTime.UtcNow.AddSeconds(cooldown).ToString("o", CultureInfo.InvariantCulture)
                    : null,
                cardID = card.CardID,
                q = card.placedHex.Q,
                r = card.placedHex.R,
                clusterId = card.clusterId,
                displayedScore = card.displayedScore,
                placementId = identity != null ? identity.PlacementId : null,
                qualityRank = identity != null ? (int)identity.QualityRank : 0,
                animalId = animal != null ? animal.UniqueID : null,
                animalName = animal != null ? animal.IndividualName : null,
                animalStar = animal != null ? animal.StarLevel : 0,
                animalTrait = animal != null ? (int)animal.Trait : 0,
            });
        }

        // Tiến trình không suy ra được từ thẻ: điểm combo, mốc đã thưởng, lượt mở rộng, tâm mảng đất
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.CaptureProgress(data.progress);
            if (HexPlacementController.Instance != null) HexPlacementController.Instance.CaptureExpansion(data.progress);
            data.progress.saved = true;
        }

        CaptureHabitatsAndCreatures(data);
        if (LumiWorld.Acs.DisasterRuntimeManager.TryGetActive(out var disasters)) disasters.CaptureForSave(data.disasters);
        return data;
    }

    // Habitat, hàng chưa thu, độ no, thức ăn, điểm chăm sóc và tên của sinh vật.
    // Tên đặt cũng ghi vào PlacementRecord.animalName để người thăm đảo thấy qua snapshot.
    private static void CaptureHabitatsAndCreatures(WorldSaveData data)
    {
        if (LumiWorld.Acs.HabitatRuntimeManager.TryGetActive(out var habitatManager))
            habitatManager.CaptureForSave(data.habitats);

        var production = LumiWorld.Acs.ResourceProductionRuntime.Instance;
        if (production != null) production.CaptureForSave(data.production, data.creatures);

        CreatureCareManager care = CreatureCareManager.HasInstance ? CreatureCareManager.Instance : null;
        if (care == null) return;
        foreach (CreatureSaveRecord creature in data.creatures)
        {
            creature.careProgress = care.GetCareProgress(creature.individualId);
            creature.customName = care.GetCustomName(creature.individualId);
        }
        foreach (PlacementRecord placement in data.placements)
        {
            string customName = care.GetCustomName(placement.placementId);
            if (!string.IsNullOrEmpty(customName)) placement.animalName = customName;
        }
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
