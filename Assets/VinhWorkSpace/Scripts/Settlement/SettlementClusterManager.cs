using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tổng tài quản lý toàn bộ Quần thể Làng mạc & Đô thị (Settlement System).
/// Điều phối quá trình đặt thẻ Làng, tính toán mở rộng cụm, phân vai trò ô,
/// kích hoạt tiến hóa thị giác (Hamlet -> Town -> GrandCity / TradingAvenue)
/// và bảo đảm bất biến ranh giới khép kín 360 độ.
/// </summary>
public class SettlementClusterManager : MonoBehaviour
{
    private static SettlementClusterManager _instance;
    public static SettlementClusterManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<SettlementClusterManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("SettlementClusterManager");
                    _instance = go.AddComponent<SettlementClusterManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Configuration")]
    [SerializeField] private SettlementAssetConfig config;

    [Header("References")]
    [SerializeField] private HexWorldGenerator worldGenerator;

    // Quản lý các ô định cư đang tồn tại trên bản đồ
    private readonly Dictionary<HexCoordinates, SettlementTile> _settlementTiles =
        new Dictionary<HexCoordinates, SettlementTile>();

    // Ánh xạ từng ô tới ID cụm của nó
    private readonly Dictionary<HexCoordinates, int> _tileClusterMap =
        new Dictionary<HexCoordinates, int>();

    // Danh sách các ô trong từng cụm
    private readonly Dictionary<int, List<HexCoordinates>> _clusters =
        new Dictionary<int, List<HexCoordinates>>();

    // Quản lý container hạ tầng dùng chung (quảng trường, đường lát đá) của từng cụm
    private readonly Dictionary<int, GameObject> _sharedInfrastructureContainers =
        new Dictionary<int, GameObject>();

    private int _nextClusterId = 1;

    public sealed class BuildingClusterInfo
    {
        public string ClusterKey;
        public int ClusterId;
        public string BuildingTypeId;
        public string DisplayName;
        public BuildingUpgradeMethod UpgradeMethod;
        public BuildingFunctionKind FunctionKind;
        public int Level;
        public int MaxLevel;
        public int AuraRadius;
        public int UnlockedFoodSlots;
        public List<HexCoordinates> Hexes = new List<HexCoordinates>();
    }

    public static event System.Action<IReadOnlyList<string>, string> OnBuildingClusterMerged;
    public static event System.Action OnBuildingClustersChanged;

    public SettlementAssetConfig Config
    {
        get => config;
        set => config = value;
    }

    private void Awake()
    {
        if (_instance == null) _instance = this;

        if (worldGenerator == null)
        {
            worldGenerator = FindAnyObjectByType<HexWorldGenerator>();
        }

        // Tự động tìm config trong Resources nếu chưa gán thủ công
        if (config == null)
        {
            config = Resources.Load<SettlementAssetConfig>("SettlementAssetConfig");
            if (config == null)
            {
                Debug.LogWarning("<color=#F59E0B>[SettlementClusterManager] Chưa tìm thấy SettlementAssetConfig trong Resources. Hãy chọn menu 'LumiWorld > Settlement Setup > Auto Configure Settlement System & Card' trong Unity Editor để tạo tự động 1 chạm!</color>");
            }
        }
    }

    /// <summary>
    /// Kiểm tra xem tọa độ ô có chứa một ô Làng/Định cư hay không
    /// </summary>
    public bool HasSettlement(HexCoordinates coords)
    {
        return _settlementTiles.ContainsKey(coords);
    }

    public SettlementTile GetSettlementTile(HexCoordinates coords)
    {
        _settlementTiles.TryGetValue(coords, out SettlementTile tile);
        return tile;
    }

    public bool TryGetSettlementTile(HexCoordinates coords, out SettlementTile tile)
    {
        return _settlementTiles.TryGetValue(coords, out tile);
    }

    public bool IsSameBuildingCluster(HexCoordinates a, HexCoordinates b)
    {
        if (!_settlementTiles.TryGetValue(a, out var tileA) || tileA == null) return false;
        if (!_settlementTiles.TryGetValue(b, out var tileB) || tileB == null) return false;
        if (tileA.UpgradeMethod != BuildingUpgradeMethod.ClusterExpansion ||
            tileB.UpgradeMethod != BuildingUpgradeMethod.ClusterExpansion)
            return a == b;
        return string.Equals(tileA.BuildingTypeId, tileB.BuildingTypeId, System.StringComparison.OrdinalIgnoreCase);
    }

    public List<BuildingClusterInfo> GetAllBuildingClusters()
    {
        var result = new List<BuildingClusterInfo>();
        var grouped = new Dictionary<string, BuildingClusterInfo>(System.StringComparer.Ordinal);
        foreach (var kvp in _settlementTiles)
        {
            SettlementTile tile = kvp.Value;
            if (tile == null) continue;
            string key = !string.IsNullOrEmpty(tile.ClusterKey)
                ? tile.ClusterKey
                : BuildingRules.BuildClusterKey(tile.BuildingTypeId, new[] { tile.Coordinates });

            if (!grouped.TryGetValue(key, out var info))
            {
                _tileClusterMap.TryGetValue(tile.Coordinates, out int cid);
                info = new BuildingClusterInfo
                {
                    ClusterKey = key,
                    ClusterId = cid,
                    BuildingTypeId = tile.BuildingTypeId,
                    DisplayName = tile.BuildingDisplayName,
                    UpgradeMethod = tile.UpgradeMethod,
                    FunctionKind = tile.FunctionKind,
                    Level = Mathf.Max(1, tile.BuildingLevel),
                    MaxLevel = Mathf.Max(1, tile.MaxLevel),
                    AuraRadius = BuildingRules.GetAuraRadius(tile.BuildingTypeId, tile.BuildingLevel),
                    UnlockedFoodSlots = BuildingRules.GetUnlockedFoodSlots(tile.BuildingTypeId, tile.BuildingLevel)
                };
                grouped[key] = info;
                result.Add(info);
            }
            info.Hexes.Add(tile.Coordinates);
        }

        result.Sort((a, b) =>
        {
            int cmp = a.ClusterId.CompareTo(b.ClusterId);
            return cmp != 0 ? cmp : string.CompareOrdinal(a.ClusterKey, b.ClusterKey);
        });
        return result;
    }

    public List<BuildingClusterInfo> GetGranaryPostClusters()
    {
        var all = GetAllBuildingClusters();
        all.RemoveAll(c => c.FunctionKind != BuildingFunctionKind.AutoFeeding);
        return all;
    }

    public BuildingClusterInfo GetClusterInfo(HexCoordinates coords)
    {
        if (!_settlementTiles.TryGetValue(coords, out var tile) || tile == null) return null;
        string key = tile.ClusterKey;
        foreach (var info in GetAllBuildingClusters())
        {
            if (string.Equals(info.ClusterKey, key, System.StringComparison.Ordinal) || info.Hexes.Contains(coords))
                return info;
        }
        return null;
    }

    /// <summary>
    /// Kích hoạt đặt một ô Building từ thẻ bài (ClusterExpansion chỉ gộp cùng BuildingTypeId; DirectUpgrade đứng độc lập).
    /// </summary>
    public PlacedCard PlaceSettlement(
        HexCoordinates coords,
        GameObject targetTileObj,
        CardData cardData,
        Vector3 spawnWorldPos,
        int restoredLevel = 1)
    {
        if (targetTileObj == null || cardData == null) return null;

        if (worldGenerator == null) worldGenerator = FindAnyObjectByType<HexWorldGenerator>();
        if (config == null) config = Resources.Load<SettlementAssetConfig>("SettlementAssetConfig");

        // 1. Gắn hoặc lấy component SettlementTile trên ô hex và cấu hình Building Metadata
        SettlementTile settlementTile = targetTileObj.GetComponent<SettlementTile>();
        if (settlementTile == null)
        {
            settlementTile = targetTileObj.AddComponent<SettlementTile>();
        }
        settlementTile.Coordinates = coords;
        settlementTile.BuildingTypeId = cardData.GetBuildingTypeId();
        settlementTile.BuildingDisplayName = cardData.GetBuildingDisplayName();
        settlementTile.UpgradeMethod = cardData.GetBuildingUpgradeMethod();
        settlementTile.FunctionKind = cardData.GetBuildingFunctionKind();
        settlementTile.MaxLevel = cardData.GetBuildingMaxLevel();
        settlementTile.BuildingLevel = Mathf.Clamp(restoredLevel, 1, settlementTile.MaxLevel);

        // 2. Tạo đối tượng ghi nhận PlacedCard để đồng bộ với hệ thống điểm và bản lưu của game
        GameObject cardRecord = new GameObject($"PlacedSettlement_{cardData.cardName}_{coords.Q}_{coords.R}");
        cardRecord.transform.SetParent(targetTileObj.transform, false);
        PlacedCard placedCardInstance = cardRecord.AddComponent<PlacedCard>();
        placedCardInstance.cardData = cardData;
        placedCardInstance.placedHex = coords;

        // Đánh dấu ô đã bị chiếm dụng
        HexTileInfo tileInfo = targetTileObj.GetComponent<HexTileInfo>();
        if (tileInfo != null)
        {
            tileInfo.isOccupied = true;
        }

        _settlementTiles[coords] = settlementTile;

        // 2.5. Sinh cảnh quan thuần visual tương ứng biome của ô đất Lush (không gắn PlacedCard Terrain)
        EnsureSettlementBiomeDecor(targetTileObj, settlementTile);

        // 3. TÍNH TOÁN CỤM & CẬP NHẬT LEVEL / VISUAL
        UpdateSettlementCluster(coords, placedCardInstance);

        return placedCardInstance;
    }

    public bool TryDirectUpgrade(HexCoordinates coords, out string reason)
    {
        reason = string.Empty;
        if (!_settlementTiles.TryGetValue(coords, out var tile) || tile == null)
        {
            reason = "Building not found.";
            return false;
        }
        if (tile.UpgradeMethod != BuildingUpgradeMethod.DirectUpgrade)
        {
            reason = "Expand this building by placing adjacent cards of the same type.";
            return false;
        }
        if (tile.BuildingLevel >= tile.MaxLevel)
        {
            reason = "Building is already at Max Level.";
            return false;
        }

        tile.BuildingLevel++;
        PlacedCard pc = tile.GetComponentInChildren<PlacedCard>();
        UpdateSettlementCluster(coords, pc);
        WorldSaveSystem.MarkDirty();
        return true;
    }

    public void EnsureSettlementBiomeDecor(GameObject targetTileObj, SettlementTile settlementTile = null)
    {
        if (targetTileObj == null) return;
        if (settlementTile == null) settlementTile = targetTileObj.GetComponent<SettlementTile>();

        for (int i = 0; i < targetTileObj.transform.childCount; i++)
        {
            Transform child = targetTileObj.transform.GetChild(i);
            if (child != null && (child.name.StartsWith("SettlementBiomeDecor_", System.StringComparison.Ordinal) ||
                                  child.name.StartsWith("Habitat_", System.StringComparison.Ordinal)))
            {
                return;
            }
        }

        CardData natureCard = ResolveNatureCardForTile(targetTileObj);
        if (natureCard == null || !natureCard.HasHabitatProps()) return;

        float surfaceY = settlementTile != null
            ? settlementTile.GetTileSurfaceY()
            : HexMetrics.TileHeight;
        Vector3 centerWorldPos = new Vector3(targetTileObj.transform.position.x, surfaceY, targetTileObj.transform.position.z);
        HexHabitatSpawner.Instance.SpawnSettlementDecor(natureCard, targetTileObj.transform, centerWorldPos, () =>
        {
            if (settlementTile != null) settlementTile.ReharmonizeBiomeDecor();
        });
    }

    private static LumiWorld.Acs.BiomeAffinity ResolveTileAffinity(GameObject tileObj)
    {
        if (tileObj == null) return LumiWorld.Acs.BiomeAffinity.None;

        var aff = LumiWorld.Acs.DisasterRuntimeManager.ResolveAffinity(tileObj);
        if (aff != LumiWorld.Acs.BiomeAffinity.None) return aff;

        var info = tileObj.GetComponent<HexTileInfo>();
        string probe = (tileObj.name + " " + (info != null && info.sourcePrefab != null ? info.sourcePrefab.name : "")).ToLowerInvariant();
        if (probe.Contains("reedmarsh")) return LumiWorld.Acs.BiomeAffinity.Reedmarsh;
        if (probe.Contains("lilywater")) return LumiWorld.Acs.BiomeAffinity.Lilywater;
        if (probe.Contains("streamstone")) return LumiWorld.Acs.BiomeAffinity.Streamstone;
        if (probe.Contains("rainleaf")) return LumiWorld.Acs.BiomeAffinity.Rainleaf;
        if (probe.Contains("bloomfield")) return LumiWorld.Acs.BiomeAffinity.Bloomfield;
        if (probe.Contains("windheath")) return LumiWorld.Acs.BiomeAffinity.Windheath;
        if (probe.Contains("leafwood") || probe.Contains("forest")) return LumiWorld.Acs.BiomeAffinity.Leafwood;
        return LumiWorld.Acs.BiomeAffinity.Leafwood;
    }

    private static CardData ResolveNatureCardForTile(GameObject tileObj)
    {
        LumiWorld.Acs.BiomeAffinity affinity = ResolveTileAffinity(tileObj);
        if (LumiWorld.Acs.HabitatRuntimeManager.TryGetActive(out var hm) && hm.Catalog != null && hm.Catalog.habitats != null)
        {
            foreach (var def in hm.Catalog.habitats)
            {
                if (def != null && def.affinity == affinity && def.natureCard != null && def.natureCard.HasHabitatProps())
                {
                    return def.natureCard;
                }
            }
        }

        string affName = affinity.ToString();
        CardData[] cards = Resources.LoadAll<CardData>("Cards");
        if (cards != null)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                CardData c = cards[i];
                if (c == null || c.cardType != CardType.Terrain || !c.HasHabitatProps()) continue;
                string id = c.cardID ?? "";
                string nm = c.cardName ?? "";
                if (id.IndexOf(affName, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    nm.IndexOf(affName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return c;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Cập nhật lại quy hoạch cụm khi có thay đổi địa hình/sông suối trên ô định cư
    /// </summary>
    public void RefreshSettlementAt(HexCoordinates coords)
    {
        if (!_settlementTiles.TryGetValue(coords, out SettlementTile tile) || tile == null) return;
        PlacedCard pc = tile.GetComponentInChildren<PlacedCard>();
        UpdateSettlementCluster(coords, pc);
    }

    /// <summary>
    /// Thuật toán quét gom cụm, phân vai trò ô và xây dựng lại vành đai khép kín 360 độ
    /// </summary>
    private void UpdateSettlementCluster(HexCoordinates placedHex, PlacedCard placedCardInstance)
    {
        if (HexBiomeClusterConnector.Instance != null)
        {
            HexBiomeClusterConnector.Instance.DetachTerrainClusterFromHex(placedHex);
        }

        if (!_settlementTiles.TryGetValue(placedHex, out SettlementTile placedTile) || placedTile == null)
            return;

        // Chỉ các ô dùng ClusterExpansion và có CÙNG BuildingTypeId mới gộp cụm với nhau.
        // Building khác loại hoặc dùng DirectUpgrade luôn đứng riêng biệt, không gộp tường/cụm.
        List<HexCoordinates> clusterHexes;
        if (placedTile.UpgradeMethod == BuildingUpgradeMethod.ClusterExpansion)
        {
            string targetTypeId = placedTile.BuildingTypeId;
            clusterHexes = SettlementClusterCalculator.FindConnectedCluster(
                placedHex,
                hex => _settlementTiles.TryGetValue(hex, out var st) &&
                       st != null &&
                       st.UpgradeMethod == BuildingUpgradeMethod.ClusterExpansion &&
                       string.Equals(st.BuildingTypeId, targetTypeId, System.StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            clusterHexes = new List<HexCoordinates> { placedHex };
        }

        if (clusterHexes.Count == 0) return;

        // Thu thập các ClusterKey cũ trước khi cập nhật để migrate cấu hình Auto-Feed nếu có gộp cụm
        var previousKeys = new List<string>();
        foreach (var hex in clusterHexes)
        {
            if (_settlementTiles.TryGetValue(hex, out var st) && st != null &&
                !string.IsNullOrEmpty(st.ClusterKey) && !previousKeys.Contains(st.ClusterKey))
            {
                previousKeys.Add(st.ClusterKey);
            }
        }

        string clusterKey = BuildingRules.BuildClusterKey(placedTile.BuildingTypeId, clusterHexes);
        int resolvedLevel = placedTile.UpgradeMethod == BuildingUpgradeMethod.ClusterExpansion
            ? BuildingRules.GetLevelForClusterSize(clusterHexes.Count, placedTile.MaxLevel)
            : Mathf.Clamp(placedTile.BuildingLevel, 1, placedTile.MaxLevel);

        // Cấp phát hoặc tái sử dụng Cluster ID nhỏ nhất (giữ nguyên thứ tự xây dựng cũ #1, #2...)
        var mergedOldIds = new HashSet<int>();
        int clusterId = int.MaxValue;
        foreach (var hex in clusterHexes)
        {
            if (_tileClusterMap.TryGetValue(hex, out int existingId) && existingId > 0)
            {
                mergedOldIds.Add(existingId);
                if (existingId < clusterId)
                {
                    clusterId = existingId;
                }
            }
        }
        if (clusterId == int.MaxValue)
        {
            clusterId = _nextClusterId++;
        }

        // Dọn sạch tường rào & hạ tầng cũ của các cụm phụ đã bị gộp vào clusterId
        foreach (int oldId in mergedOldIds)
        {
            if (oldId == clusterId) continue;
            _clusters.Remove(oldId);
            SettlementBorderBuilder.Instance.DestroyClusterBorders(oldId);

            if (_sharedInfrastructureContainers.TryGetValue(oldId, out GameObject oldInfra) && oldInfra != null)
            {
                oldInfra.SetActive(false);
                oldInfra.transform.SetParent(null, false);
                if (Application.isPlaying) Destroy(oldInfra);
                else DestroyImmediate(oldInfra);
            }
            _sharedInfrastructureContainers.Remove(oldId);
        }

        // Cập nhật lại bản đồ cụm và Level đồng bộ trên toàn bộ các ô trong cụm
        _clusters[clusterId] = clusterHexes;
        foreach (var hex in clusterHexes)
        {
            _tileClusterMap[hex] = clusterId;
            if (_settlementTiles.TryGetValue(hex, out SettlementTile tile) && tile != null)
            {
                tile.BuildingLevel = resolvedLevel;
                tile.ClusterKey = clusterKey;
                PlacedCard pc = tile.GetComponentInChildren<PlacedCard>();
                if (pc != null) pc.clusterId = clusterId;
            }
        }

        if (placedCardInstance != null)
        {
            placedCardInstance.clusterId = clusterId;
        }

        if (previousKeys.Count > 0)
        {
            OnBuildingClusterMerged?.Invoke(previousKeys, clusterKey);
        }

        // 4. XÁC ĐỊNH HÌNH THÁI VISUAL (Lv.5+ luôn dùng tường đá thành trì; bố cục bên trong chọn theo cụm compact/linear)
        bool isLinearCluster = placedTile.UpgradeMethod == BuildingUpgradeMethod.ClusterExpansion &&
                               SettlementClusterCalculator.IsLinearCluster(clusterHexes);
        bool isCompactHighLevel = resolvedLevel >= 5 && !isLinearCluster;
        bool useStoneWalls = resolvedLevel >= 5;
        HexCoordinates coreHex = SettlementClusterCalculator.FindCoreTile(
            clusterHexes, 
            hex => RiverNetworkManager.Instance != null && RiverNetworkManager.Instance.HasRiver(hex));

        // Tính toán tâm hình học và cao độ bề mặt của toàn cụm (Cluster Center)
        // Xử lý thông minh cho cụm phân tầng (Multi-tier Terraces):
        // Tuyệt đối không cộng trung bình cao độ giữa 2 tầng khác nhau (gây trôi dạt lơ lửng giữa trời).
        // Thay vào đó, chọn tầng chính (Anchor Terrace) có nhiều ô nhất hoặc chứa coreHex,
        // và neo clusterCenter vững chãi trên bề mặt đất của tầng đó!
        Dictionary<int, List<HexCoordinates>> elevationGroups = new Dictionary<int, List<HexCoordinates>>();
        foreach (var h in clusterHexes)
        {
            if (_settlementTiles.TryGetValue(h, out SettlementTile st) && st != null)
            {
                int elevKey = Mathf.RoundToInt(st.GetTileSurfaceY() * 2f);
                if (!elevationGroups.TryGetValue(elevKey, out var list))
                {
                    list = new List<HexCoordinates>();
                    elevationGroups[elevKey] = list;
                }
                list.Add(h);
            }
        }

        List<HexCoordinates> primaryTerrace = null;
        int coreElevKey = 0;
        if (_settlementTiles.TryGetValue(coreHex, out SettlementTile ct) && ct != null)
        {
            coreElevKey = Mathf.RoundToInt(ct.GetTileSurfaceY() * 2f);
        }

        if (elevationGroups.TryGetValue(coreElevKey, out var coreList) && coreList.Count >= 2)
        {
            primaryTerrace = coreList;
        }
        else
        {
            int maxC = -1;
            foreach (var kvp in elevationGroups)
            {
                if (kvp.Value.Count > maxC)
                {
                    maxC = kvp.Value.Count;
                    primaryTerrace = kvp.Value;
                }
            }
        }

        if (primaryTerrace == null || primaryTerrace.Count == 0) primaryTerrace = clusterHexes;

        Vector3 clusterCenter = Vector3.zero;
        int validTileCount = 0;
        float terraceSurfaceY = 0f;
        foreach (var h in primaryTerrace)
        {
            if (_settlementTiles.TryGetValue(h, out SettlementTile st) && st != null)
            {
                terraceSurfaceY = st.GetTileSurfaceY();
                clusterCenter += new Vector3(st.transform.position.x, terraceSurfaceY, st.transform.position.z);
                validTileCount++;
            }
        }
        if (validTileCount > 0)
        {
            clusterCenter /= validTileCount;
            // Bất biến: Cao độ Y của clusterCenter LUÔN LUÔN là cao độ chuẩn của tầng đất được chọn,
            // không bao giờ bị trung bình cộng lơ lửng giữa vực thẳm!
            clusterCenter.y = terraceSurfaceY;
        }

        HashSet<HexCoordinates> clusterSet = new HashSet<HexCoordinates>(clusterHexes);

        // 5. PHÂN BỔ BỐ CỤC VISUAL VÀ SINH NỘI DUNG PROCEDURAL CHO TỪNG Ô
        for (int i = 0; i < clusterHexes.Count; i++)
        {
            HexCoordinates h = clusterHexes[i];
            if (!_settlementTiles.TryGetValue(h, out SettlementTile sTile)) continue;

            BuildingVisualPreset visualPreset = BuildingVisualPreset.Standard;

            if (isCompactHighLevel)
            {
                if (h == coreHex)
                {
                    visualPreset = BuildingVisualPreset.LandmarkCore;
                }
                else if (i % 4 == 1)
                {
                    visualPreset = BuildingVisualPreset.Market;
                }
                else if (i % 4 == 2)
                {
                    visualPreset = BuildingVisualPreset.Production;
                }
                else if (i % 4 == 3)
                {
                    visualPreset = BuildingVisualPreset.WatchPost;
                }
            }
            else if (isLinearCluster && clusterHexes.Count >= 5)
            {
                if (i % 2 == 1) visualPreset = BuildingVisualPreset.Market;
                else visualPreset = BuildingVisualPreset.Standard;
            }
            else if (resolvedLevel >= 2)
            {
                if (h == coreHex)
                {
                    visualPreset = BuildingVisualPreset.Market;
                }
                else if (i == 1)
                {
                    visualPreset = BuildingVisualPreset.Production;
                }
                else if (i == 2 && clusterHexes.Count >= 4)
                {
                    visualPreset = BuildingVisualPreset.WatchPost;
                }
            }

            // Phân loại các hướng láng giềng: Nội bộ (thông sang ô khác) vs Biên ngoài (có hàng rào / mép vách đá)
            List<int> internalDirs = new List<int>();
            List<int> externalDirs = new List<int>();
            float hSurfaceY = sTile.GetTileSurfaceY();
            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates neighbor = h.GetNeighbor(dir);
                if (!clusterSet.Contains(neighbor))
                {
                    externalDirs.Add(dir);
                }
                else
                {
                    float nSurfaceY = hSurfaceY;
                    if (_settlementTiles.TryGetValue(neighbor, out SettlementTile nTile) && nTile != null)
                    {
                        nSurfaceY = nTile.GetTileSurfaceY();
                    }

                    float deltaY = hSurfaceY - nSurfaceY;
                    if (Mathf.Abs(deltaY) > 0.35f)
                    {
                        // Chênh lệch cao độ vách đá (> 0.35m):
                        // Ô trên cao nhìn xuống mép vực: Đưa vào externalDirs để dựng tường bảo vệ mép vực & tháp bastion!
                        if (deltaY > 0.35f)
                        {
                            externalDirs.Add(dir);
                        }
                        // Tuyệt đối không đưa vào internalDirs của cả 2 ô để cấm tiệt rải prop đường phố rơi ra vực
                    }
                    else
                    {
                        internalDirs.Add(dir);
                    }
                }
            }

            // Sinh hoặc cập nhật kiến trúc trên ô theo quy hoạch thống nhất toàn cụm
            sTile.BuildProcedural(
                config, 
                visualPreset, 
                resolvedLevel, 
                clusterHexes.Count, 
                clusterCenter, 
                internalDirs, 
                externalDirs);
        }

        // 6. XÂY DỰNG LẠI VÀNH ĐAI KHÉP KÍN 360 ĐỘ (SEALED PERIMETER)
        // Bất biến: Tường ngăn bên trong bị dỡ bỏ, viền ngoài cùng được hàn kín 100%
        SettlementBorderBuilder.Instance.BuildClosedBorders(
            clusterId, 
            clusterHexes, 
            useStoneWalls, 
            config, 
            worldGenerator,
            _settlementTiles);
        SettlementBorderBuilder.Instance.DestroyOrphanedBorders(new HashSet<int>(_clusters.Keys));

        // 6.2. TÌM VỊ TRÍ CỔNG LÀNG VÀ XÂY DỰNG CƠ SỞ HẠ TẦNG (QUẢNG TRƯỜNG & ĐƯỜNG LÁT ĐÁ)
        List<SettlementClusterCalculator.PerimeterEdge> perimeterEdges =
            SettlementClusterCalculator.CalculatePerimeterEdges(clusterHexes);
        Vector3 gateWorldPos = Vector3.zero;
        foreach (var edge in perimeterEdges)
        {
            if (edge.IsGate && _settlementTiles.TryGetValue(edge.Hex, out SettlementTile gt))
            {
                Vector3 edgeMid = RiverSplinePath.GetEdgeMidpointLocal(edge.Direction);
                float surfaceY = gt.GetTileSurfaceY();
                gateWorldPos = new Vector3(gt.transform.position.x + edgeMid.x, surfaceY, gt.transform.position.z + edgeMid.z);
                break;
            }
        }

        BuildTownInfrastructure(clusterId, clusterHexes, isCompactHighLevel, config, clusterCenter, gateWorldPos);

        // 6.5. HÀN GẮN VIỀN NỐI ĐẤT LIỀN MẠCH VÀ NẮP GÓC NGÃ BA GIỮA CÁC Ô TRONG CỤM (SEAM BRIDGES & CORNER FILLERS)
        if (HexBiomeClusterConnector.Instance != null)
        {
            foreach (var h in clusterHexes)
            {
                HexBiomeClusterConnector.Instance.ConnectAllNearby(h);
            }
        }

        // 7. THÔNG BÁO VÀ PHẢN HỒI THỊ GIÁC (FEEDBACK)
        AnnounceBuildingProgress(placedTile.BuildingDisplayName, resolvedLevel, placedTile.MaxLevel, clusterHexes.Count);
        OnBuildingClustersChanged?.Invoke();
    }

    /// <summary>
    /// Xây dựng cơ sở hạ tầng dùng chung cho toàn bộ Cụm Đô thị:
    /// 1. Quảng trường trung tâm (Town Plaza) tại clusterCenter: Giếng làng, ghế đá, thùng hàng, bồn hoa.
    /// 2. Đại lộ lát đá (Main Boulevard) dẫn thẳng từ Cổng Làng vào Quảng trường Trung tâm.
    /// 3. Hệ thống lối đi lát đá (Inter-District Walkways) tỏa nhánh từ Quảng trường đến từng phân khu.
    /// 4. Thảm thực vật viền lối đi (cỏ mầm, hoa dại, sỏi nhỏ).
    /// </summary>
    private void BuildTownInfrastructure(
        int clusterId,
        List<HexCoordinates> clusterHexes,
        bool isCompactHighLevel,
        SettlementAssetConfig config,
        Vector3 clusterCenter,
        Vector3 gateWorldPos)
    {
        if (config == null || clusterHexes == null || clusterHexes.Count == 0) return;

        // Dọn dẹp toàn bộ container hạ tầng cũ của cụm ngay trong frame
        string containerName = $"Settlement_Infrastructure_Cluster_{clusterId}";
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform ch = transform.GetChild(i);
            if (ch == null || !string.Equals(ch.name, containerName, System.StringComparison.Ordinal)) continue;
            ch.gameObject.SetActive(false);
            ch.SetParent(null, false);
            if (Application.isPlaying) Destroy(ch.gameObject);
            else DestroyImmediate(ch.gameObject);
        }

        GameObject infraRoot = new GameObject(containerName);
        infraRoot.transform.SetParent(transform, true);
        _sharedInfrastructureContainers[clusterId] = infraRoot;

        System.Random rng = new System.Random(clusterId * 31337 + clusterHexes.Count * 7919);

        float stoneScale = config.GetScale(SettlementScaleType.SteppingStone);
        float floraScale = config.GetScale(SettlementScaleType.Flora);
        float propScale = config.GetScale(SettlementScaleType.Prop);
        float treeScale = config.GetScale(SettlementScaleType.Tree);
        float bushScale = config.GetScale(SettlementScaleType.Bush);

        // 1. QUẢNG TRƯỜNG TRUNG TÂM (CHỈ KHI CỤM TỪ 2 Ô TRỞ LÊN)
        if (clusterHexes.Count >= 2)
        {
            // Điểm nhấn quảng trường: Giếng nước (Well) hoặc Đài kỷ niệm trung tâm
            GameObject centerpiece = config.GetRandomVillageCenterpiece(rng);
            if (SettlementAssetConfig.IsValidPrefab(centerpiece))
            {
                Vector3 centerPos = clusterCenter + new Vector3(0, config.buildingYOffset, 0);
                Quaternion centerRot = Quaternion.Euler(0, (float)(rng.NextDouble() * 360f), 0) * centerpiece.transform.rotation;
                GameObject cpInst = Instantiate(centerpiece, centerPos, centerRot, infraRoot.transform);
                if (cpInst != null)
                {
                    cpInst.transform.localScale = Vector3.Scale(centerpiece.transform.localScale, Vector3.one * config.GetScale(SettlementScaleType.Centerpiece));
                }
            }

            // Tiểu cảnh bám quanh giếng nước: 2 xô nước đặt ngay chân giếng (Props)
            Vector3 bucketOffset1 = clusterCenter + new Vector3(0.20f, config.buildingYOffset, 0.10f);
            Vector3 bucketOffset2 = clusterCenter + new Vector3(-0.18f, config.buildingYOffset, 0.16f);
            if (config.streetProps != null && config.streetProps.Count > 0)
            {
                GameObject bucketPrefab = config.streetProps.Find(p => SettlementAssetConfig.IsValidPrefab(p) && p.name.ToLower().Contains("bucket"));
                if (bucketPrefab == null) bucketPrefab = config.GetRandomStreetProp(rng);
                if (SettlementAssetConfig.IsValidPrefab(bucketPrefab))
                {
                    var b1 = Instantiate(bucketPrefab, bucketOffset1, Quaternion.Euler(0, (float)(rng.NextDouble() * 360f), 0), infraRoot.transform);
                    if (b1 != null) b1.transform.localScale = Vector3.Scale(bucketPrefab.transform.localScale, Vector3.one * propScale);

                    var b2 = Instantiate(bucketPrefab, bucketOffset2, Quaternion.Euler(0, (float)(rng.NextDouble() * 360f), 0), infraRoot.transform);
                    if (b2 != null) b2.transform.localScale = Vector3.Scale(bucketPrefab.transform.localScale, Vector3.one * propScale);
                }
            }

            // Nếu là cụm cấp cao tụ cụm: Cắm thêm 2 lá cờ hoàng gia hai bên lối vào quảng trường (Props)
            if (isCompactHighLevel && config.villageCenterpieces != null)
            {
                GameObject flagPrefab = config.villageCenterpieces.Find(p => SettlementAssetConfig.IsValidPrefab(p) && p.name.ToLower().Contains("flag"));
                if (SettlementAssetConfig.IsValidPrefab(flagPrefab))
                {
                    Instantiate(flagPrefab, clusterCenter + new Vector3(0.42f, 0, 0.25f), Quaternion.identity, infraRoot.transform);
                    Instantiate(flagPrefab, clusterCenter + new Vector3(-0.42f, 0, 0.25f), Quaternion.identity, infraRoot.transform);
                }
            }
        }

        // Tạm thời bỏ đi đường đi (stepping stones) và thực vật (plants) theo chỉ thị thiết kế
    }

    private void SpawnInfrastructureSteppingStone(
        GameObject stonePrefab, 
        Vector3 worldPos, 
        System.Random rng, 
        float stoneScale, 
        Transform parent)
    {
        if (!SettlementAssetConfig.IsValidPrefab(stonePrefab)) return;

        try
        {
            // Giữ nguyên góc xoay gốc của prefab và chỉ quay ngẫu nhiên quanh trục đứng Y thế giới
            Quaternion rot = Quaternion.AngleAxis((float)(rng.NextDouble() * 360f), Vector3.up) * stonePrefab.transform.rotation;
            worldPos.y = Mathf.Max(worldPos.y, 0.006f);

            GameObject sInst = Instantiate(stonePrefab, worldPos, rot, parent);
            if (sInst == null) return;

            // Scale chuẩn tỉ lệ theo transform gốc của prefab nhân với hệ số stoneMultiplier
            // Giữ nguyên tỉ lệ 3D tự nhiên, tuyệt đối không bóp méo trục Y/Z
            Vector3 targetScale = Vector3.Scale(stonePrefab.transform.localScale, Vector3.one * stoneScale);
            sInst.transform.localScale = targetScale;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SettlementClusterManager] Stepping stone spawn failed: {ex.Message}");
        }
    }

    private void AnnounceBuildingProgress(string displayName, int level, int maxLevel, int tileCount)
    {
        string name = string.IsNullOrEmpty(displayName) ? BuildingRules.GranaryPostDisplayName : displayName;
        string msg = $"{name} · Lv. {level}/{maxLevel} ({tileCount} {(tileCount == 1 ? "tile" : "tiles")})";
        Debug.Log($"<color=#10B981>[LumiWorld Building] {msg}</color>");

        if (HexPlacementPreviewHUD.Instance != null)
        {
            HexPlacementPreviewHUD.ShowFeedbackToast(msg);
        }
    }
}
