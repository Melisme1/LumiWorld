using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PropArchetype = HexClusterPropResolver.PropArchetype;
using NearbyPropEntry = HexClusterPropResolver.NearbyPropEntry;

/// <summary>
/// Quản lý việc kết nối các khối lục giác cùng Biome thành một cụm hoàn chỉnh (Preserve-style)
/// QUY TẮC CỐT LÕI:
/// - CHỈ KHI cả 2 ô kề nhau ĐÃ ĐƯỢC ĐẶT THẺ HABITAT (thẻ có Habitat Props) VÀ CÙNG LOẠI HABITAT thì mới sinh kết nối!
/// - Các ô đất thô (Arid), ô nước, ô chưa đặt thẻ Habitat: TUYỆT ĐỐI KHÔNG sinh kết nối.
/// </summary>
public class HexBiomeClusterConnector : MonoBehaviour
{
    private static HexBiomeClusterConnector _instance;
    public static HexBiomeClusterConnector Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<HexBiomeClusterConnector>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("HexBiomeClusterConnector");
                    _instance = go.AddComponent<HexBiomeClusterConnector>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Mesh Bridge Settings (Hàn rãnh mép vát & sườn ngoài)")]
    [Tooltip("Độ nâng Y cực nhỏ để chống z-fighting với bề mặt gốc (mặc định 0.0012f)")]
    [SerializeField] private float bridgeYOffset = 0.0012f;

    [Tooltip("Bán kính nắp hàn ngã ba nơi 3 ô chạm nhau")]
    [SerializeField] private float cornerFillerRadius = 0.12f;

    [Header("Vegetation Connection (Thảm thực vật liên kết ranh giới)")]
    [Tooltip("Tự động sinh hoa cỏ, bụi cây và thảm thực vật dọc theo đường nối giữa các ô trong cụm")]
    [SerializeField] private bool spawnConnectorProps = true;

    [Tooltip("Tỉ lệ xuất hiện Prop Trung (Ưu tiên Bụi cây xanh, Tảng đá rêu, Thân gỗ điểm xuyết) trên mỗi cạnh nối")]
    [Range(0f, 1f)]
    [SerializeField] private float mediumPropChance = 0.50f;

    [Tooltip("Tỉ lệ ưu tiên chọn Bụi cây thực vật / Tảng đá rêu thay vì Cây thân gỗ (0.90 = 90% là bụi cây/hoa/đá rêu, chỉ 10% cây thân gỗ)")]
    [Range(0f, 1f)]
    [SerializeField] private float vegetationPriority = 0.90f;

    [Tooltip("Số lượng prop nhỏ sinh rải rác trên mỗi cạnh nối")]
    [SerializeField] private Vector2Int propsPerEdgeRange = new Vector2Int(2, 4);

    [Tooltip("Khoảng cách tối thiểu từ Prop Trung tới các prop đã có trên 2 ô (chống đè lấn)")]
    [SerializeField] private float minMediumClearance = 0.24f;

    [Tooltip("Khoảng cách tối thiểu giữa các prop nhỏ")]
    [SerializeField] private float minSmallClearance = 0.11f;

    [Header("Seam Micro Grass (Mầm cỏ nhỏ viền nối giữa các ô cùng loại)")]
    [Tooltip("Tự động sinh mầm cỏ nhỏ (MicroGrassTuft) dọc theo viền nối giữa 2 ô cùng loại để kết nối mượt mà thảm cỏ")]
    [SerializeField] private bool spawnSeamMicroGrass = true;

    [Tooltip("Prefab mầm cỏ nhỏ (MicroGrassTuft). Nếu để trống sẽ tự động lấy từ HexFilledGroundSpawner hoặc Prefabs/Leafwood/05_FilledGround/MicroGrassTuft.prefab")]
    [SerializeField] private GameObject seamMicroGrassPrefab;

    [Tooltip("Số lượng mầm cỏ nhỏ sinh dọc theo mỗi viền nối giữa 2 ô")]
    [SerializeField] private Vector2Int seamMicroGrassCountRange = new Vector2Int(5, 8);

    [Tooltip("Số lượng mầm cỏ nhỏ sinh quanh nắp ngã ba 3 ô (Corner Filler)")]
    [SerializeField] private Vector2Int cornerMicroGrassCountRange = new Vector2Int(4, 7);

    [Tooltip("Khoảng scale ngẫu nhiên cho mầm cỏ nhỏ viền nối (1.10 - 1.80 đồng bộ với HexFilledGroundSpawner)")]
    [SerializeField] private Vector2 seamMicroGrassScaleRange = new Vector2(1.10f, 1.80f);

    [Tooltip("Độ cắm sâu tiếp đất cho mầm cỏ nhỏ viền nối (mặc định 0.005f = 0.5cm)")]
    [SerializeField] private float seamMicroGrassGroundEmbed = 0.005f;

    [Header("Corner Junction Hub (Cụm sinh thái ngã ba 3 ô)")]
    [Tooltip("Sinh thực vật tại ngã ba trung tâm nơi 3 ô gặp nhau")]
    [SerializeField] private bool spawnCornerProps = true;

    [Tooltip("Cho phép ngã ba 3 ô sinh Prop Trung (Bụi cây lớn, Tảng đá rêu, hoặc Thân gỗ/Cây)")]
    [SerializeField] private bool allowMediumPropAtCorner = true;

    [Tooltip("Tỉ lệ xuất hiện Prop Trung tại ngã ba 3 ô")]
    [Range(0f, 1f)]
    [SerializeField] private float cornerMediumPropChance = 0.50f;

    [Tooltip("Số lượng prop nhỏ tại ngã ba 3 ô")]
    [SerializeField] private Vector2Int cornerSmallPropsRange = new Vector2Int(2, 3);

    [Header("Celebration Effect (Hiệu ứng khi cụm hoàn chỉnh 3+ ô)")]
    [Tooltip("Nảy nhẹ đồng bộ toàn cụm khi đạt đủ điều kiện nhóm 3+ ô")]
    [SerializeField] private bool enableCelebration = true;

    [Tooltip("Độ nảy cao của hiệu ứng ăn mừng")]
    [SerializeField] private float celebrationBounceHeight = 0.065f;

    [Tooltip("Thời gian diễn ra hiệu ứng nảy")]
    [SerializeField] private float celebrationDuration = 0.35f;

    [Header("Biome Cluster Limit (Giới hạn quy mô Cụm Biome)")]
    [Tooltip("Bật/tắt giới hạn kích thước Cụm Biome. Khi bật, một cụm chỉ được tối đa maxTilesPerCluster ô.")]
    [SerializeField] private bool enableBiomeSizeLimit = true;
    public bool EnableBiomeSizeLimit
    {
        get => enableBiomeSizeLimit;
        set => enableBiomeSizeLimit = value;
    }

    [Tooltip("Số ô lục giác tối đa của một Cụm Biome (Mặc định: 9). Khi cụm đã đủ 9 ô, các ô đặt thêm sau đó sẽ tách thành cụm độc lập, không tạo viền nối.")]
    [SerializeField] private int maxTilesPerCluster = 9;
    public int MaxTilesPerCluster
    {
        get => maxTilesPerCluster;
        set => maxTilesPerCluster = value;
    }

    private int _nextClusterId = 1;
    public int NextClusterId => _nextClusterId;

    public void ReserveClusterIdsThrough(int lastUsedId)
    {
        if (lastUsedId > 0 && lastUsedId < int.MaxValue)
            _nextClusterId = Mathf.Max(_nextClusterId, lastUsedId + 1);
    }

    // Quản lý các cầu nối và nắp góc đã tạo để không tạo trùng lặp
    private readonly HashSet<string> _createdEdgeBridges = new HashSet<string>();
    private readonly HashSet<string> _createdCornerFillers = new HashSet<string>();
    private readonly HashSet<string> _celebratedClusters = new HashSet<string>();
    private readonly HashSet<Mesh> _ownedConnectionMeshes = new HashSet<Mesh>();

    private Transform _connectionsContainer;
    public Transform ConnectionsContainer
    {
        get
        {
            if (_connectionsContainer == null)
            {
                GameObject go = new GameObject("BiomeConnectionsContainer");
                go.transform.SetParent(transform, true);
                _connectionsContainer = go.transform;
            }
            return _connectionsContainer;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        AutoLoadDefaultMicroGrassIfNeeded();
    }

    private void Reset()
    {
        AutoLoadDefaultMicroGrassIfNeeded();
    }

    private void OnValidate()
    {
        AutoLoadDefaultMicroGrassIfNeeded();
    }

    public void AutoLoadDefaultMicroGrassIfNeeded()
    {
#if UNITY_EDITOR
        if (seamMicroGrassPrefab == null)
        {
            seamMicroGrassPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/VinhWorkSpace/Prefabs/Leafwood/05_FilledGround/MicroGrassTuft.prefab"
            );
        }
#endif
    }

    public int AssignCluster(PlacedCard placedCard)
    {
        if (placedCard == null || placedCard.cardData == null) return -1;
        if (!placedCard.cardData.HasHabitatProps() && placedCard.cardData.cardType != CardType.Terrain) return -1;

        if (placedCard.clusterId > 0)
        {
            return placedCard.clusterId;
        }

        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen == null || worldGen.MapTiles == null)
        {
            placedCard.clusterId = _nextClusterId++;
            return placedCard.clusterId;
        }

        int limit = (enableBiomeSizeLimit)
            ? ((placedCard.cardData.maxBiomeTilesOverride > 0) ? placedCard.cardData.maxBiomeTilesOverride : maxTilesPerCluster)
            : int.MaxValue;
        if (limit <= 0) limit = int.MaxValue;
        if (LumiWorld.Acs.HabitatRuntimeManager.TryGetActive(out var habitatManager) &&
            habitatManager.Catalog.TryGetHabitat(placedCard.CardID, out _)) limit = LumiWorld.Acs.HabitatTopologyService.MaximumHexCount;

        PlacedCard[] allCards = FindObjectsByType<PlacedCard>(FindObjectsInactive.Include);
        Dictionary<int, int> clusterSizes = new Dictionary<int, int>();
        foreach (var c in allCards)
        {
            if (c != null && c != placedCard && c.clusterId > 0 && AreNatureCardsMatching(placedCard.cardData, c.cardData))
            {
                if (!clusterSizes.ContainsKey(c.clusterId)) clusterSizes[c.clusterId] = 0;
                clusterSizes[c.clusterId]++;
            }
        }

        Dictionary<int, List<PlacedCard>> eligibleNeighborClusters = new Dictionary<int, List<PlacedCard>>();

        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates neighborHex = placedCard.placedHex.GetNeighbor(dir);
            if (worldGen.MapTiles.TryGetValue(neighborHex, out GameObject neighborTile) && neighborTile != null)
            {
                PlacedCard neighborCard = GetHabitatCardOnTile(neighborTile);
                if (neighborCard != null && neighborCard != placedCard && AreNatureCardsMatching(placedCard.cardData, neighborCard.cardData))
                {
                    if (neighborCard.clusterId <= 0)
                    {
                        neighborCard.clusterId = _nextClusterId++;
                        clusterSizes[neighborCard.clusterId] = 1;
                    }

                    int cId = neighborCard.clusterId;
                    int curSize = clusterSizes.ContainsKey(cId) ? clusterSizes[cId] : 1;

                    if (curSize < limit)
                    {
                        if (!eligibleNeighborClusters.ContainsKey(cId))
                        {
                            eligibleNeighborClusters[cId] = new List<PlacedCard>();
                        }
                        eligibleNeighborClusters[cId].Add(neighborCard);
                    }
                }
            }
        }

        if (eligibleNeighborClusters.Count == 0)
        {
            placedCard.clusterId = _nextClusterId++;
        }
        else if (eligibleNeighborClusters.Count == 1)
        {
            foreach (var kvp in eligibleNeighborClusters)
            {
                placedCard.clusterId = kvp.Key;
                break;
            }
        }
        else
        {
            List<int> candidateClusterIds = new List<int>(eligibleNeighborClusters.Keys);
            candidateClusterIds.Sort();
            int primaryClusterId = candidateClusterIds[0];
            int totalMergedSize = 1;

            foreach (var cId in candidateClusterIds)
            {
                totalMergedSize += clusterSizes.ContainsKey(cId) ? clusterSizes[cId] : 1;
            }

            if (totalMergedSize <= limit)
            {
                placedCard.clusterId = primaryClusterId;
                for (int i = 1; i < candidateClusterIds.Count; i++)
                {
                    int otherId = candidateClusterIds[i];
                    foreach (var c in allCards)
                    {
                        if (c != null && c.clusterId == otherId)
                        {
                            c.clusterId = primaryClusterId;
                        }
                    }
                }
            }
            else
            {
                int bestClusterId = primaryClusterId;
                int maxAdjCount = -1;

                foreach (var kvp in eligibleNeighborClusters)
                {
                    int adjCount = kvp.Value.Count;
                    if (adjCount > maxAdjCount)
                    {
                        maxAdjCount = adjCount;
                        bestClusterId = kvp.Key;
                    }
                }

                placedCard.clusterId = bestClusterId;
            }
        }

        return placedCard.clusterId;
    }

    public void ConnectCluster(PlacedCard placedCard)
    {
        if (placedCard == null || placedCard.cardData == null) return;

        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen != null && worldGen.MapTiles != null &&
            worldGen.MapTiles.TryGetValue(placedCard.placedHex, out GameObject placedTile) && IsBurnedTile(placedTile))
            return;

        if (!placedCard.cardData.HasHabitatProps() && placedCard.cardData.cardType != CardType.Terrain) return;

        AssignCluster(placedCard);

        if (!placedCard.cardData.HasHabitatProps()) return;

        ConnectAllNearby(placedCard.placedHex, placedCard.cardData);
    }

    public void ConnectAllNearby(HexCoordinates centerHex, CardData centerHabitatData = null)
    {
        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen == null || worldGen.MapTiles == null) return;

        if (!worldGen.MapTiles.TryGetValue(centerHex, out GameObject centerTile) || centerTile == null)
            return;
        if (IsBurnedTile(centerTile)) return;

        PlacedCard centerCard = GetHabitatCardOnTile(centerTile);
        bool centerHasRiver = (RiverNetworkManager.Instance != null && RiverNetworkManager.Instance.HasRiver(centerHex));
        if (centerHabitatData == null || !centerHabitatData.HasHabitatProps())
        {
            if (centerCard != null && centerCard.cardData != null && centerCard.cardData.HasHabitatProps())
            {
                centerHabitatData = centerCard.cardData;
            }
            else if (!centerHasRiver)
            {
                return;
            }
        }

        if (centerCard != null && centerCard.clusterId <= 0)
        {
            AssignCluster(centerCard);
        }

        List<(HexCoordinates hB, GameObject tileB, HexCoordinates hC, GameObject tileC, string cornerKey)> newCorners =
            new List<(HexCoordinates, GameObject, HexCoordinates, GameObject, string)>();
        HashSet<string> clusterEdgeKeys = new HashSet<string>();

        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates hB = centerHex.GetNeighbor(dir);
            HexCoordinates hC = centerHex.GetNeighbor((dir + 1) % 6);

            if (worldGen.MapTiles.TryGetValue(hB, out GameObject tileB) && tileB != null && !IsBurnedTile(tileB) &&
                worldGen.MapTiles.TryGetValue(hC, out GameObject tileC) && tileC != null && !IsBurnedTile(tileC))
            {
                PlacedCard cardB = GetHabitatCardOnTile(tileB);
                PlacedCard cardC = GetHabitatCardOnTile(tileC);

                if (cardB != null && cardC != null &&
                    AreHabitatsMatching(centerHabitatData, cardB.cardData) &&
                    AreHabitatsMatching(centerHabitatData, cardC.cardData))
                {
                    if (enableBiomeSizeLimit)
                    {
                        if (centerCard == null || centerCard.clusterId <= 0 ||
                            cardB.clusterId <= 0 || cardC.clusterId <= 0 ||
                            cardB.clusterId != centerCard.clusterId ||
                            cardC.clusterId != centerCard.clusterId)
                        {
                            continue;
                        }
                    }

                    string cornerKey = GetCornerKey(centerHex, hB, hC);
                    if (!_createdCornerFillers.Contains(cornerKey))
                    {
                        newCorners.Add((hB, tileB, hC, tileC, cornerKey));
                        clusterEdgeKeys.Add(GetEdgeKey(centerHex, hB));
                        clusterEdgeKeys.Add(GetEdgeKey(centerHex, hC));
                    }
                }
            }
        }

        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates neighborHex = centerHex.GetNeighbor(dir);

            if (worldGen.MapTiles.TryGetValue(neighborHex, out GameObject neighborTile) &&
                neighborTile != null && !IsBurnedTile(neighborTile))
            {
                PlacedCard neighborCard = GetHabitatCardOnTile(neighborTile);

                bool riverCrossesEdge = false;
                if (RiverNetworkManager.Instance != null)
                {
                    RiverTileSegment segCenter = RiverNetworkManager.Instance.GetRiver(centerHex);
                    RiverTileSegment segNeighbor = RiverNetworkManager.Instance.GetRiver(neighborHex);
                    if (segCenter != null && segCenter.HasPort(dir)) riverCrossesEdge = true;
                    if (segNeighbor != null)
                    {
                        int oppositeDir = (dir + 3) % 6;
                        if (segNeighbor.HasPort(oppositeDir)) riverCrossesEdge = true;
                    }
                }

                bool habitatsMatch = (neighborCard != null && AreHabitatsMatching(centerHabitatData, neighborCard.cardData));

                if (habitatsMatch || riverCrossesEdge)
                {
                    if (!riverCrossesEdge && enableBiomeSizeLimit)
                    {
                        if (centerCard == null || centerCard.clusterId <= 0 ||
                            neighborCard == null || neighborCard.clusterId <= 0 ||
                            neighborCard.clusterId != centerCard.clusterId)
                        {
                            continue;
                        }
                    }

                    string edgeKey = GetEdgeKey(centerHex, neighborHex);
                    if (!_createdEdgeBridges.Contains(edgeKey))
                    {
                        _createdEdgeBridges.Add(edgeKey);
                        CardData bridgeHabitatData = centerHabitatData != null ? centerHabitatData : (neighborCard != null ? neighborCard.cardData : null);
                        CreateEdgeSeamBridge(centerHex, centerTile, neighborHex, neighborTile, edgeKey, bridgeHabitatData, worldGen);

                        if (!riverCrossesEdge && spawnConnectorProps && !clusterEdgeKeys.Contains(edgeKey))
                        {
                            HexClusterVegetationSpawner.SpawnEdgeConnectorProps(this, ConnectionsContainer, centerTile, neighborTile, bridgeHabitatData, GetVegetationConfig());
                        }
                    }
                }
            }
        }

        for (int i = 0; i < newCorners.Count; i++)
        {
            var corner = newCorners[i];
            _createdCornerFillers.Add(corner.cornerKey);
            CreateCornerFiller(centerTile, corner.tileB, corner.tileC, corner.cornerKey, centerHabitatData);

            if (spawnCornerProps)
            {
                HexClusterVegetationSpawner.SpawnClusterCornerProps(this, ConnectionsContainer, centerTile, corner.tileB, corner.tileC, centerHabitatData, GetVegetationConfig());
            }
        }

        CheckAndCelebrateCluster(centerHex, centerHabitatData, worldGen);
    }

    public static PlacedCard GetHabitatCardOnTile(GameObject tileObj)
    {
        if (tileObj == null) return null;

        PlacedCard[] cards = tileObj.GetComponentsInChildren<PlacedCard>(true);
        if (cards != null)
        {
            foreach (var c in cards)
                if (c != null && c.CardType == CardType.Terrain) return c;
            foreach (var c in cards)
            {
                if (c != null && c.cardData != null && c.cardData.HasHabitatProps())
                {
                    return c;
                }
            }
        }

        return null;
    }

    private static bool IsBurnedTile(GameObject tileObj)
    {
        return LumiWorld.Acs.TileDisasterState.TryGet(tileObj, out var state) && state.isBurned;
    }

    public static bool AreNatureCardsMatching(CardData a, CardData b)
    {
        return a != null && b != null && a.cardType == CardType.Terrain && b.cardType == CardType.Terrain &&
            !string.IsNullOrEmpty(a.cardID) && string.Equals(a.cardID, b.cardID, StringComparison.Ordinal);
    }

    public static bool AreHabitatsMatching(CardData a, CardData b)
    {
        if (a == null || b == null) return false;
        if (!a.HasHabitatProps() || !b.HasHabitatProps()) return false;

        if (a.cardID == b.cardID) return true;
        if (a.cardName == b.cardName) return true;

        string gA = GetHabitatFamily(a.cardName);
        string gB = GetHabitatFamily(b.cardName);

        if (!string.IsNullOrEmpty(gA) && gA == gB) return true;

        return false;
    }

    public static string GetHabitatFamily(string cardName)
    {
        if (string.IsNullOrEmpty(cardName)) return "";
        if (cardName.IndexOf("Forest", StringComparison.OrdinalIgnoreCase) >= 0 ||
            cardName.IndexOf("Leafwood", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Leafwood";

        if (cardName.IndexOf("Bloomfield", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Bloomfield";

        if (cardName.IndexOf("Windheath", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Windheath";

        if (cardName.IndexOf("Mountain", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Mountain";

        return cardName;
    }

    private void CreateEdgeSeamBridge(
        HexCoordinates hexA, GameObject tileA,
        HexCoordinates hexB, GameObject tileB,
        string edgeKey, CardData habitatData, HexWorldGenerator worldGen)
    {
        if (tileA == null || tileB == null) return;

        Vector3 posA = tileA.transform.position;
        Vector3 posB = tileB.transform.position;

        Vector3 dirXZ = new Vector3(posB.x - posA.x, 0f, posB.z - posA.z);
        float distXZ = dirXZ.magnitude;
        if (distXZ < 0.001f) return;

        Vector3 u = dirXZ / distXZ;
        Vector3 v = new Vector3(-u.z, 0f, u.x);
        Vector3 midPoint = (posA + posB) * 0.5f;

        float halfEdge = HexMetrics.OuterRadius * 0.5f;

        Vector3 V1_world = midPoint - v * halfEdge;
        Vector3 V2_world = midPoint + v * halfEdge;

        var (topYA, rimYA, bottomYA) = GetTileHeightLevels(tileA);
        var (topYB, rimYB, bottomYB) = GetTileHeightLevels(tileB);

        if (Mathf.Abs(topYA - topYB) > 0.15f) return;

        float topY = (topYA + topYB) * 0.5f;
        float rimY = (rimYA + rimYB) * 0.5f;
        float bottomY = Mathf.Min(bottomYA, bottomYB);
        float elevatedTopY = topY + bridgeYOffset;

        bool v1IsOuter = !HasThirdHabitatNeighbor(hexA, hexB, V1_world, habitatData, worldGen);
        bool v2IsOuter = !HasThirdHabitatNeighbor(hexA, hexB, V2_world, habitatData, worldGen);

        var (topGrassUV, bevelUV, wallUV) = GetTilePaletteUVs(tileA, habitatData);
        Material tileMat = GetTileMaterial(tileA);

        Mesh bridgeMesh = HexClusterMeshBuilder.BuildSeamBridgeMeshLocal(
            u, v, halfEdge,
            elevatedTopY - midPoint.y,
            rimY - midPoint.y,
            bottomY - midPoint.y,
            v1IsOuter, v2IsOuter,
            topGrassUV, bevelUV, wallUV
        );
        if (bridgeMesh != null)
            _ownedConnectionMeshes.Add(bridgeMesh);

        GameObject bridgeGo = new GameObject($"SeamBridge_{edgeKey}");
        bridgeGo.transform.SetParent(ConnectionsContainer, false);
        bridgeGo.transform.position = new Vector3(midPoint.x, midPoint.y, midPoint.z);

        MeshFilter mf = bridgeGo.AddComponent<MeshFilter>();
        mf.sharedMesh = bridgeMesh;

        MeshRenderer mr = bridgeGo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = tileMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = true;

        StartCoroutine(AnimateBridgePopIn(bridgeGo.transform));

        if (spawnSeamMicroGrass && !IsPositionNearRiver(midPoint, 0.40f))
        {
            HexClusterVegetationSpawner.SpawnBorderMicroGrass(this, ConnectionsContainer, midPoint, u, v, halfEdge, elevatedTopY, habitatData, GetVegetationConfig());
        }
    }

    private void CreateCornerFiller(
        GameObject tileA, GameObject tileB, GameObject tileC, string cornerKey, CardData habitatData)
    {
        if (tileA == null || tileB == null || tileC == null) return;

        Vector3 posA = tileA.transform.position;
        Vector3 posB = tileB.transform.position;
        Vector3 posC = tileC.transform.position;

        Vector3 center = (posA + posB + posC) / 3f;

        var (topYA, _, _) = GetTileHeightLevels(tileA);
        var (topYB, _, _) = GetTileHeightLevels(tileB);
        var (topYC, _, _) = GetTileHeightLevels(tileC);

        if (Mathf.Abs(topYA - topYB) > 0.15f || Mathf.Abs(topYA - topYC) > 0.15f) return;

        float topY = (topYA + topYB + topYC) / 3f;
        float surfaceY = topY + bridgeYOffset + 0.0005f;
        center.y = surfaceY;

        var (topGrassUV, _, _) = GetTilePaletteUVs(tileA, habitatData);
        Material tileMat = GetTileMaterial(tileA);

        Mesh cornerMesh = HexClusterMeshBuilder.BuildCornerFillerMeshLocal(cornerFillerRadius, topGrassUV, cornerKey);
        if (cornerMesh != null)
            _ownedConnectionMeshes.Add(cornerMesh);

        GameObject cornerGo = new GameObject($"CornerFiller_{cornerKey}");
        cornerGo.transform.SetParent(ConnectionsContainer, false);
        cornerGo.transform.position = center;

        MeshFilter mf = cornerGo.AddComponent<MeshFilter>();
        mf.sharedMesh = cornerMesh;

        MeshRenderer mr = cornerGo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = tileMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = true;

        StartCoroutine(AnimateBridgePopIn(cornerGo.transform));

        if (spawnSeamMicroGrass)
        {
            HexClusterVegetationSpawner.SpawnCornerMicroGrass(this, ConnectionsContainer, center, surfaceY, habitatData, GetVegetationConfig());
        }
    }

    private bool HasThirdHabitatNeighbor(
        HexCoordinates hexA, HexCoordinates hexB,
        Vector3 V_world, CardData habitatData, HexWorldGenerator worldGen)
    {
        if (worldGen == null || worldGen.MapTiles == null) return false;

        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates cand = hexA.GetNeighbor(dir);
            if (cand == hexB) continue;

            if (cand.DistanceTo(hexB) == 1)
            {
                if (worldGen.MapTiles.TryGetValue(cand, out GameObject candTile) && candTile != null)
                {
                    PlacedCard candCard = GetHabitatCardOnTile(candTile);
                    if (candCard != null && AreHabitatsMatching(habitatData, candCard.cardData))
                    {
                        float dist = Vector3.Distance(
                            new Vector3(candTile.transform.position.x, 0f, candTile.transform.position.z),
                            new Vector3(V_world.x, 0f, V_world.z)
                        );

                        if (dist < HexMetrics.OuterRadius * 1.15f)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    public static (float topY, float rimY, float bottomY) GetTileHeightLevels(GameObject tileObj, CardData cardData = null)
    {
        if (tileObj == null)
        {
            return (HexMetrics.DefaultTileY + 1.0f, HexMetrics.DefaultTileY + 0.91f, HexMetrics.DefaultTileY);
        }

        float baseY = tileObj.transform.position.y;
        float localTop = 1.0f;

        MeshFilter tileMf = GetTileMeshFilter(tileObj);
        if (tileMf != null && tileMf.sharedMesh != null)
        {
            float scaleY = tileMf.transform.lossyScale.y;
            float meshMaxY = tileMf.sharedMesh.bounds.max.y * scaleY;

            if (meshMaxY > 2.3f * scaleY)
            {
                localTop = 2.60f * scaleY;
            }
            else if (meshMaxY > 1.5f * scaleY)
            {
                localTop = 1.80f * scaleY;
            }
            else if (meshMaxY > 0.5f * scaleY)
            {
                localTop = 1.00f * scaleY;
            }
            else
            {
                localTop = meshMaxY;
            }
        }
        else
        {
            string tileName = tileObj.name;
            HexTileInfo tileInfo = tileObj.GetComponent<HexTileInfo>();
            if (tileInfo != null && tileInfo.sourcePrefab != null)
            {
                tileName = tileInfo.sourcePrefab.name;
            }

            if (tileName.IndexOf("Windheath", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (tileInfo != null && tileInfo.terrainTypeIndex == 2))
            {
                localTop = 2.60f;
            }
            else if (tileName.IndexOf("Leafwood", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     tileName.IndexOf("Forest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     (tileInfo != null && tileInfo.terrainTypeIndex == 1))
            {
                localTop = 1.80f;
            }
            else
            {
                localTop = 1.00f;
            }
        }

        float worldTop = baseY + localTop;
        float worldRim = worldTop - 0.09f;
        float worldBottom = baseY;

        return (worldTop, worldRim, worldBottom);
    }

    public static MeshFilter GetTileMeshFilter(GameObject tileObj)
    {
        if (tileObj == null) return null;

        MeshFilter[] mfs = tileObj.GetComponentsInChildren<MeshFilter>();
        if (mfs != null)
        {
            foreach (var mf in mfs)
            {
                if (mf != null && mf.sharedMesh != null && !IsPartOfPlacedProp(mf.transform, tileObj.transform))
                {
                    return mf;
                }
            }
        }
        return null;
    }

    private static bool IsPartOfPlacedProp(Transform t, Transform tileTransform)
    {
        Transform curr = t;
        while (curr != null && curr != tileTransform)
        {
            string n = curr.name;
            if (curr.GetComponent<PlacedCard>() != null ||
                n.StartsWith("Habitat_") ||
                n.StartsWith("PlacedCard_") ||
                n.StartsWith("ConnectorProp_") ||
                n.StartsWith("CornerProp_") ||
                n.StartsWith("SeamBridge_") ||
                n.StartsWith("CornerJunction_"))
            {
                return true;
            }
            curr = curr.parent;
        }
        return false;
    }

    public static (Vector2 topGrassUV, Vector2 bevelUV, Vector2 wallUV) GetTilePaletteUVs(GameObject tileObj, CardData cardData = null)
    {
        float grassY = 0.8125f;

        MeshFilter tileMf = GetTileMeshFilter(tileObj);
        if (tileMf != null && tileMf.sharedMesh != null && tileMf.sharedMesh.isReadable)
        {
            Mesh mesh = tileMf.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;

            if (vertices != null && uvs != null && vertices.Length == uvs.Length && vertices.Length > 0)
            {
                int highestIdx = 0;
                float maxY = float.MinValue;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (vertices[i].y > maxY)
                    {
                        maxY = vertices[i].y;
                        highestIdx = i;
                    }
                }

                if (uvs[highestIdx].y > 0.01f)
                {
                    grassY = uvs[highestIdx].y;
                    return (new Vector2(0.4375f, grassY), new Vector2(0.5625f, grassY), new Vector2(0.6875f, grassY));
                }
            }
        }

        string tileName = tileObj != null ? tileObj.name : "";
        HexTileInfo tileInfo = tileObj != null ? tileObj.GetComponent<HexTileInfo>() : null;
        if (tileInfo != null && tileInfo.sourcePrefab != null)
        {
            tileName = tileInfo.sourcePrefab.name;
        }

        if (tileName.IndexOf("Windheath", StringComparison.OrdinalIgnoreCase) >= 0 ||
            (tileInfo != null && tileInfo.terrainTypeIndex == 2))
        {
            grassY = 0.5625f;
        }
        else if (tileName.IndexOf("Leafwood", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 tileName.IndexOf("Forest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (tileInfo != null && tileInfo.terrainTypeIndex == 1))
        {
            grassY = 0.6875f;
        }
        else
        {
            grassY = 0.8125f;
        }

        Vector2 topGrassUV = new Vector2(0.4375f, grassY);
        Vector2 bevelUV = new Vector2(0.5625f, grassY);
        Vector2 wallUV = new Vector2(0.6875f, grassY);

        return (topGrassUV, bevelUV, wallUV);
    }

    public static Material GetTileMaterial(GameObject tileObj)
    {
        if (tileObj != null)
        {
            Renderer[] rends = tileObj.GetComponentsInChildren<Renderer>();
            if (rends != null)
            {
                foreach (var r in rends)
                {
                    if (r != null && r.sharedMaterial != null && !IsPartOfPlacedProp(r.transform, tileObj.transform))
                    {
                        return r.sharedMaterial;
                    }
                }
            }
        }
        return null;
    }

    // Forwarding helpers to HexClusterPropResolver for 100% backward compatibility
    public static PropArchetype GetArchetypeFromName(string name) => HexClusterPropResolver.GetArchetypeFromName(name);
    public static PropArchetype GetPropArchetype(GameObject prefab, CardData.HabitatPropRule rule) => HexClusterPropResolver.GetPropArchetype(prefab, rule);
    public static int CountArchetypeNearby(List<NearbyPropEntry> entries, PropArchetype arch) => HexClusterPropResolver.CountArchetypeNearby(entries, arch);

    public List<NearbyPropEntry> GetNearbyPropEntries(Vector3 checkCenter, float radius)
    {
        return HexClusterPropResolver.GetNearbyPropEntries(_connectionsContainer, checkCenter, radius);
    }

    public List<Vector3> GetNearbyPropPositions(Vector3 checkCenter, float radius)
    {
        var entries = GetNearbyPropEntries(checkCenter, radius);
        List<Vector3> pos = new List<Vector3>(entries.Count);
        for (int i = 0; i < entries.Count; i++) pos.Add(entries[i].position);
        return pos;
    }

    public HexClusterVegetationConfig GetVegetationConfig() => new HexClusterVegetationConfig
    {
        spawnConnectorProps = spawnConnectorProps,
        mediumPropChance = mediumPropChance,
        propsPerEdgeRange = propsPerEdgeRange,
        minMediumClearance = minMediumClearance,
        minSmallClearance = minSmallClearance,
        spawnSeamMicroGrass = spawnSeamMicroGrass,
        seamMicroGrassPrefab = seamMicroGrassPrefab,
        seamMicroGrassCountRange = seamMicroGrassCountRange,
        cornerMicroGrassCountRange = cornerMicroGrassCountRange,
        seamMicroGrassScaleRange = seamMicroGrassScaleRange,
        seamMicroGrassGroundEmbed = seamMicroGrassGroundEmbed,
        cornerFillerRadius = cornerFillerRadius
    };

    // Forwarding helpers for 100% backward compatibility
    public GameObject GetMicroGrassPrefab(CardData cardData = null) =>
        HexClusterVegetationSpawner.GetMicroGrassPrefab(cardData, ref seamMicroGrassPrefab);

    public GameObject GetMediumTreeOrCenterpiecePrefab(
        CardData cardData,
        List<NearbyPropEntry> nearbyEntries,
        Vector3 spawnTarget,
        out CardData.HabitatPropRule matchedRule,
        out PropArchetype chosenArchetype) =>
        HexClusterPropResolver.GetMediumTreeOrCenterpiecePrefab(cardData, nearbyEntries, spawnTarget, out matchedRule, out chosenArchetype);

    public GameObject GetMediumPropPrefab(
        CardData cardData,
        List<NearbyPropEntry> nearbyEntries,
        Vector3 spawnTarget,
        out CardData.HabitatPropRule matchedRule,
        out PropArchetype chosenArchetype) =>
        HexClusterPropResolver.GetMediumPropPrefab(cardData, nearbyEntries, spawnTarget, out matchedRule, out chosenArchetype);

    public List<(GameObject prefab, CardData.HabitatPropRule rule, PropArchetype archetype)> GetEligibleSmallPropsList(CardData cardData) =>
        HexClusterPropResolver.GetEligibleSmallPropsList(cardData);

    public GameObject GetSmallPropPrefab(CardData cardData, out CardData.HabitatPropRule matchedRule) =>
        HexClusterPropResolver.GetSmallPropPrefab(cardData, out matchedRule);

    private void CheckAndCelebrateCluster(HexCoordinates centerHex, CardData centerHabitatData, HexWorldGenerator worldGen)
    {
        if (!enableCelebration) return;

        List<GameObject> clusterTiles = new List<GameObject>();
        Queue<HexCoordinates> queue = new Queue<HexCoordinates>();
        HashSet<HexCoordinates> visited = new HashSet<HexCoordinates>();

        queue.Enqueue(centerHex);
        visited.Add(centerHex);

        GameObject centerTile = worldGen.MapTiles[centerHex];
        PlacedCard centerCard = GetHabitatCardOnTile(centerTile);
        clusterTiles.Add(centerTile);

        while (queue.Count > 0)
        {
            HexCoordinates cur = queue.Dequeue();

            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates nHex = cur.GetNeighbor(dir);
                if (visited.Contains(nHex)) continue;

                if (worldGen.MapTiles.TryGetValue(nHex, out GameObject nTile) && nTile != null)
                {
                    PlacedCard nCard = GetHabitatCardOnTile(nTile);
                    if (nCard != null && AreHabitatsMatching(centerHabitatData, nCard.cardData))
                    {
                        if (enableBiomeSizeLimit && centerCard != null && (nCard.clusterId <= 0 || nCard.clusterId != centerCard.clusterId))
                        {
                            continue;
                        }

                        visited.Add(nHex);
                        queue.Enqueue(nHex);
                        clusterTiles.Add(nTile);
                    }
                }
            }
        }

        if (clusterTiles.Count >= 3)
        {
            string signature = GetClusterSignature(new List<HexCoordinates>(visited));
            if (!_celebratedClusters.Contains(signature))
            {
                _celebratedClusters.Add(signature);
                StartCoroutine(CelebrateClusterRoutine(clusterTiles));
            }
        }
    }

    [ContextMenu("Scan And Connect All Habitat Clusters In Scene")]
    public void ScanAndConnectExisting()
    {
        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen == null || worldGen.MapTiles == null) return;

        ClearAllConnections();

        foreach (var pair in worldGen.MapTiles)
        {
            PlacedCard card = GetHabitatCardOnTile(pair.Value);
            if (card != null && card.cardData != null && card.cardData.HasHabitatProps())
            {
                card.clusterId = -1;
            }
        }

        foreach (var pair in worldGen.MapTiles)
        {
            PlacedCard card = GetHabitatCardOnTile(pair.Value);
            if (card != null && card.cardData != null && card.cardData.HasHabitatProps())
            {
                ConnectCluster(card);
            }
        }
    }

    [ContextMenu("Clear All Connections")]
    public void ClearAllConnections()
    {
        _createdEdgeBridges.Clear();
        _createdCornerFillers.Clear();
        _celebratedClusters.Clear();
        _nextClusterId = 1;

        DestroyOwnedConnectionMeshes();

        if (_connectionsContainer != null)
        {
            if (Application.isPlaying)
                Destroy(_connectionsContainer.gameObject);
            else
                DestroyImmediate(_connectionsContainer.gameObject);

            _connectionsContainer = null;
        }
    }

    public void RemoveConnectionsForHex(HexCoordinates hex)
    {
        if (_connectionsContainer == null) return;

        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();

        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates neighbor = hex.GetNeighbor(dir);
            string edgeKey = GetEdgeKey(hex, neighbor);
            _createdEdgeBridges.Remove(edgeKey);
            DestroyConnectionObject($"SeamBridge_{edgeKey}");

            HexCoordinates nextNeighbor = hex.GetNeighbor((dir + 1) % 6);
            string cornerKey = GetCornerKey(hex, neighbor, nextNeighbor);
            _createdCornerFillers.Remove(cornerKey);
            DestroyConnectionObject($"CornerFiller_{cornerKey}");

            if (worldGen != null && worldGen.MapTiles != null &&
                worldGen.MapTiles.TryGetValue(hex, out GameObject tileA) && tileA != null &&
                worldGen.MapTiles.TryGetValue(neighbor, out GameObject tileB) && tileB != null &&
                worldGen.MapTiles.TryGetValue(nextNeighbor, out GameObject tileC) && tileC != null)
            {
                Vector3 junction = (tileA.transform.position + tileB.transform.position + tileC.transform.position) / 3f;
                DestroyConnectionObjectsNear(junction, 0.68f);
            }
        }
    }

    public void ClearPropsNearRiver(Vector3 center, float radius)
    {
        if (_connectionsContainer == null) return;
        float radiusSqr = radius * radius;
        for (int i = _connectionsContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = _connectionsContainer.GetChild(i);
            if (child == null) continue;

            string cName = child.name;
            if (cName.StartsWith("SeamBridge_") || 
                cName.StartsWith("CornerFiller_") || 
                cName.StartsWith("EdgeBridge_"))
            {
                continue;
            }

            Vector3 delta = child.position - center;
            if (delta.x * delta.x + delta.z * delta.z > radiusSqr) continue;
            DestroyConnectionObject(child);
        }
    }

    public void RestoreMissingBridges()
    {
        if (ConnectionsContainer == null) return;
        _createdEdgeBridges.RemoveWhere(k => _connectionsContainer.Find($"SeamBridge_{k}") == null);
        _createdCornerFillers.RemoveWhere(k => _connectionsContainer.Find($"CornerFiller_{k}") == null);

        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen == null || worldGen.MapTiles == null) return;

        if (_connectionsContainer != null)
        {
            for (int i = _connectionsContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = _connectionsContainer.GetChild(i);
                if (child == null) continue;
                string n = child.name;
                if (n.StartsWith("SeamBridge_"))
                {
                    string key = n.Substring("SeamBridge_".Length);
                    string[] parts = key.Split('_');
                    if (parts.Length == 4 &&
                        int.TryParse(parts[0], out int q1) && int.TryParse(parts[1], out int r1) &&
                        int.TryParse(parts[2], out int q2) && int.TryParse(parts[3], out int r2))
                    {
                        HexCoordinates hA = new HexCoordinates(q1, r1);
                        HexCoordinates hB = new HexCoordinates(q2, r2);
                        PlacedCard cA = worldGen.MapTiles.TryGetValue(hA, out GameObject tA) ? GetHabitatCardOnTile(tA) : null;
                        PlacedCard cB = worldGen.MapTiles.TryGetValue(hB, out GameObject tB) ? GetHabitatCardOnTile(tB) : null;

                        bool riverCrosses = false;
                        if (RiverNetworkManager.Instance != null)
                        {
                            RiverTileSegment segA = RiverNetworkManager.Instance.GetRiver(hA);
                            RiverTileSegment segB = RiverNetworkManager.Instance.GetRiver(hB);
                            for (int d = 0; d < 6; d++)
                            {
                                if (segA != null && hA.GetNeighbor(d) == hB && segA.HasPort(d)) riverCrosses = true;
                                if (segB != null && hB.GetNeighbor(d) == hA && segB.HasPort(d)) riverCrosses = true;
                            }
                        }

                        if (!riverCrosses && (cA == null || cB == null || !AreNatureCardsMatching(cA.cardData, cB.cardData)))
                        {
                            _createdEdgeBridges.Remove(key);
                            DestroyConnectionObject(child);
                        }
                    }
                }
            }
        }

        foreach (var pair in worldGen.MapTiles)
        {
            PlacedCard card = GetHabitatCardOnTile(pair.Value);
            bool hasRiver = RiverNetworkManager.Instance != null && RiverNetworkManager.Instance.HasRiver(pair.Key);
            if ((card != null && card.cardData != null && card.cardData.HasHabitatProps()) || hasRiver)
            {
                ConnectAllNearby(pair.Key, card != null ? card.cardData : null);
            }
        }
    }

    public static bool IsPositionNearRiver(Vector3 worldPos, float radius)
    {
        if (RiverNetworkManager.Instance == null || RiverNetworkManager.Instance.RiverMap == null) return false;
        float radiusSqr = radius * radius;

        foreach (var pair in RiverNetworkManager.Instance.RiverMap)
        {
            RiverTileSegment seg = pair.Value;
            if (seg == null) continue;

            Vector3 segPos = seg.transform.position;

            if (seg.CachedPaths != null && seg.CachedPaths.Count > 0)
            {
                for (int p = 0; p < seg.CachedPaths.Count; p++)
                {
                    var path = seg.CachedPaths[p];
                    for (int w = 0; w < path.Count; w++)
                    {
                        Vector3 wpWorld = segPos + path[w].position;
                        float dx = worldPos.x - wpWorld.x;
                        float dz = worldPos.z - wpWorld.z;
                        if (dx * dx + dz * dz < radiusSqr) return true;
                    }
                }
            }
            else
            {
                for (int d = 0; d < 6; d++)
                {
                    if (seg.HasPort(d))
                    {
                        Vector3 portWorld = segPos + RiverSplinePath.GetEdgeMidpointLocal(d);
                        float dx = worldPos.x - portWorld.x;
                        float dz = worldPos.z - portWorld.z;
                        if (dx * dx + dz * dz < radiusSqr) return true;
                    }
                }

                float dxCenter = worldPos.x - segPos.x;
                float dzCenter = worldPos.z - segPos.z;
                if (dxCenter * dxCenter + dzCenter * dzCenter < radiusSqr) return true;
            }
        }
        return false;
    }

    private void DestroyConnectionObjectsNear(Vector3 center, float radius)
    {
        if (_connectionsContainer == null) return;
        float radiusSqr = radius * radius;
        for (int i = _connectionsContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = _connectionsContainer.GetChild(i);
            if (child == null) continue;
            Vector3 delta = child.position - center;
            if (delta.x * delta.x + delta.z * delta.z > radiusSqr) continue;
            DestroyConnectionObject(child);
        }
    }

    private void DestroyConnectionObject(string objectName)
    {
        if (_connectionsContainer == null) return;
        Transform connection = _connectionsContainer.Find(objectName);
        if (connection == null) return;

        DestroyConnectionObject(connection);
    }

    private void DestroyConnectionObject(Transform connection)
    {
        if (connection == null) return;

        MeshFilter meshFilter = connection.GetComponent<MeshFilter>();
        Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
        if (mesh != null && _ownedConnectionMeshes.Remove(mesh))
        {
            meshFilter.sharedMesh = null;
            DestroyOwnedMesh(mesh);
        }

        if (Application.isPlaying) Destroy(connection.gameObject);
        else DestroyImmediate(connection.gameObject);
    }

    private void DestroyOwnedConnectionMeshes()
    {
        if (_ownedConnectionMeshes.Count == 0) return;

        foreach (Mesh mesh in _ownedConnectionMeshes)
            DestroyOwnedMesh(mesh);

        _ownedConnectionMeshes.Clear();
    }

    private static void DestroyOwnedMesh(Mesh mesh)
    {
        if (mesh == null) return;

        if (Application.isPlaying) Destroy(mesh);
        else DestroyImmediate(mesh);
    }

    private IEnumerator AnimateBridgePopIn(Transform target)
    {
        if (target == null) yield break;

        float duration = 0.24f;
        float elapsed = 0f;

        Vector3 finalScale = Vector3.one;
        target.localScale = new Vector3(0.2f, 0f, 0.2f);

        while (elapsed < duration)
        {
            if (target == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float overshoot = 1f + c3 * Mathf.Pow(t - 1f, 3) + c1 * Mathf.Pow(t - 1f, 2);

            target.localScale = new Vector3(
                Mathf.Clamp01(overshoot),
                Mathf.Clamp01(overshoot),
                Mathf.Clamp01(overshoot)
            );

            yield return null;
        }

        if (target != null)
        {
            target.localScale = finalScale;
        }
    }

    private IEnumerator AnimatePropPopIn(Transform target, Vector3 finalScale, float delay)
    {
        return HexClusterVegetationSpawner.AnimatePropPopIn(target, finalScale, delay);
    }

    private IEnumerator CelebrateClusterRoutine(List<GameObject> tiles)
    {
        yield return new WaitForSeconds(0.12f);

        List<Transform> tileTransforms = new List<Transform>();
        List<Vector3> initialPositions = new List<Vector3>();

        foreach (var t in tiles)
        {
            if (t != null)
            {
                tileTransforms.Add(t.transform);
                initialPositions.Add(t.transform.position);
            }
        }

        float duration = celebrationDuration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float bounceY = Mathf.Sin(t * Mathf.PI) * celebrationBounceHeight;

            for (int i = 0; i < tileTransforms.Count; i++)
            {
                if (tileTransforms[i] != null)
                {
                    tileTransforms[i].position = initialPositions[i] + Vector3.up * bounceY;
                }
            }

            yield return null;
        }

        for (int i = 0; i < tileTransforms.Count; i++)
        {
            if (tileTransforms[i] != null)
            {
                tileTransforms[i].position = initialPositions[i];
            }
        }
    }

    private string GetEdgeKey(HexCoordinates a, HexCoordinates b)
    {
        if (a.Q < b.Q || (a.Q == b.Q && a.R < b.R))
            return $"{a.Q}_{a.R}__{b.Q}_{b.R}";
        else
            return $"{b.Q}_{b.R}__{a.Q}_{a.R}";
    }

    private string GetCornerKey(HexCoordinates a, HexCoordinates b, HexCoordinates c)
    {
        List<HexCoordinates> list = new List<HexCoordinates> { a, b, c };
        list.Sort((h1, h2) => {
            int cmp = h1.Q.CompareTo(h2.Q);
            return cmp != 0 ? cmp : h1.R.CompareTo(h2.R);
        });
        return $"C_{list[0].Q}_{list[0].R}__{list[1].Q}_{list[1].R}__{list[2].Q}_{list[2].R}";
    }

    private string GetClusterSignature(List<HexCoordinates> hexList)
    {
        List<string> strList = new List<string>();
        foreach (var h in hexList) strList.Add($"{h.Q}_{h.R}");
        strList.Sort();
        return string.Join("|", strList);
    }
}
