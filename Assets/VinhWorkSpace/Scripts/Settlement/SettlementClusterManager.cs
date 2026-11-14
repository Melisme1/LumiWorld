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

    /// <summary>
    /// Kích hoạt đặt một ô Khu Dân Cư / Làng Mạc từ thẻ bài
    /// </summary>
    public PlacedCard PlaceSettlement(
        HexCoordinates coords, 
        GameObject targetTileObj, 
        CardData cardData, 
        Vector3 spawnWorldPos)
    {
        if (targetTileObj == null || cardData == null) return null;

        if (worldGenerator == null) worldGenerator = FindAnyObjectByType<HexWorldGenerator>();
        if (config == null) config = Resources.Load<SettlementAssetConfig>("SettlementAssetConfig");

        // 1. Gắn hoặc lấy component SettlementTile trên ô hex
        SettlementTile settlementTile = targetTileObj.GetComponent<SettlementTile>();
        if (settlementTile == null)
        {
            settlementTile = targetTileObj.AddComponent<SettlementTile>();
        }
        settlementTile.Coordinates = coords;

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

        // 3. TÍNH TOÁN CỤM & CẬP NHẬT TIẾN HÓA LÃNH THỔ
        UpdateSettlementCluster(coords, placedCardInstance);

        return placedCardInstance;
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
        // Tìm toàn bộ các ô liên thông trực tiếp với ô vừa đặt
        List<HexCoordinates> clusterHexes = SettlementClusterCalculator.FindConnectedCluster(
            placedHex,
            hex => _settlementTiles.ContainsKey(hex));

        if (clusterHexes.Count == 0) return;

        // Cấp phát hoặc tái sử dụng Cluster ID
        int clusterId = _nextClusterId++;
        foreach (var hex in clusterHexes)
        {
            if (_tileClusterMap.TryGetValue(hex, out int existingId) && existingId > 0)
            {
                clusterId = existingId;
                break;
            }
        }

        // Cập nhật lại bản đồ cụm
        _clusters[clusterId] = clusterHexes;
        foreach (var hex in clusterHexes)
        {
            _tileClusterMap[hex] = clusterId;
            if (_settlementTiles.TryGetValue(hex, out SettlementTile tile))
            {
                // Cập nhật clusterId cho PlacedCard tương ứng
                PlacedCard pc = tile.GetComponentInChildren<PlacedCard>();
                if (pc != null) pc.clusterId = clusterId;
            }
        }

        if (placedCardInstance != null)
        {
            placedCardInstance.clusterId = clusterId;
        }

        // 4. XÁC ĐỊNH CẤP ĐỘ TIẾN HÓA (TIER)
        SettlementTier tier = SettlementClusterCalculator.DetermineTier(clusterHexes);
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

        // 5. PHÂN BỔ VAI TRÒ (ROLE) VÀ SINH NỘI DUNG PROCEDURAL CHO TỪNG Ô
        for (int i = 0; i < clusterHexes.Count; i++)
        {
            HexCoordinates h = clusterHexes[i];
            if (!_settlementTiles.TryGetValue(h, out SettlementTile sTile)) continue;

            SettlementRole assignedRole = SettlementRole.Residential;

            if (tier == SettlementTier.GrandCity)
            {
                // Ô lõi trung tâm: Mọc Lâu Đài hoặc Nhà Thờ lớn
                if (h == coreHex)
                {
                    assignedRole = SettlementRole.CivicCore;
                }
                else if (i % 4 == 1)
                {
                    assignedRole = SettlementRole.MarketSquare;
                }
                else if (i % 4 == 2)
                {
                    assignedRole = SettlementRole.Craftsman;
                }
                else if (i % 4 == 3)
                {
                    assignedRole = SettlementRole.Military;
                }
            }
            else if (tier == SettlementTier.Town)
            {
                // Thị trấn: 1 ô làm chợ, 1 ô làm thủ công/sản xuất, 1 ô quân sự phòng thủ (nếu từ 4 ô trở lên), còn lại là nhà dân
                if (h == coreHex)
                {
                    assignedRole = SettlementRole.MarketSquare;
                }
                else if (i == 1)
                {
                    assignedRole = SettlementRole.Craftsman;
                }
                else if (i == 2 && clusterHexes.Count >= 4)
                {
                    assignedRole = SettlementRole.Military;
                }
            }
            else if (tier == SettlementTier.TradingAvenue)
            {
                // Dải phố dài: Xen kẽ nhà dân và chợ / quán trọ
                if (i % 2 == 1) assignedRole = SettlementRole.MarketSquare;
                else assignedRole = SettlementRole.Residential;
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
                assignedRole, 
                tier, 
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
            tier, 
            config, 
            worldGenerator,
            _settlementTiles);

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

        BuildTownInfrastructure(clusterId, clusterHexes, tier, config, clusterCenter, gateWorldPos);

        // 6.5. HÀN GẮN VIỀN NỐI ĐẤT LIỀN MẠCH VÀ NẮP GÓC NGÃ BA GIỮA CÁC Ô TRONG CỤM (SEAM BRIDGES & CORNER FILLERS)
        if (HexBiomeClusterConnector.Instance != null)
        {
            foreach (var h in clusterHexes)
            {
                HexBiomeClusterConnector.Instance.ConnectAllNearby(h);
            }
        }

        // 7. THÔNG BÁO VÀ PHẢN HỒI THỊ GIÁC (FEEDBACK)
        AnnounceSettlementProgress(tier, clusterHexes.Count);
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
        SettlementTier tier,
        SettlementAssetConfig config,
        Vector3 clusterCenter,
        Vector3 gateWorldPos)
    {
        if (config == null || clusterHexes == null || clusterHexes.Count == 0) return;

        // Dọn dẹp container hạ tầng cũ của cụm
        string containerName = $"Settlement_Infrastructure_Cluster_{clusterId}";
        Transform oldRoot = transform.Find(containerName);
        if (oldRoot != null)
        {
            if (Application.isPlaying) Destroy(oldRoot.gameObject);
            else DestroyImmediate(oldRoot.gameObject);
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

            // Nếu là Đại Đô Thị: Cắm thêm 2 lá cờ hoàng gia hai bên lối vào quảng trường (Props)
            if (tier == SettlementTier.GrandCity && config.villageCenterpieces != null)
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

    private void AnnounceSettlementProgress(SettlementTier tier, int count)
    {
        string title = "";
        string desc = "";

        switch (tier)
        {
            case SettlementTier.Hamlet:
                title = "🌿 Làng Sơ Khai";
                desc = $"Đã thành lập một ấp nhỏ bình yên ({count} ô).";
                break;
            case SettlementTier.Town:
                title = "🏘️ Thị Trấn Mở Rộng";
                desc = $"Khu định cư đã mở rộng thành Thị trấn với Chợ và Xưởng rèn ({count} ô)!";
                break;
            case SettlementTier.GrandCity:
                title = "🏰 ĐẠI ĐÔ THỊ PHÁO ĐÀI";
                desc = $"Chúc mừng! Đô thị đã hoàn thiện với Lâu Đài trung tâm và Tường Thành Đá kiên cố ({count} ô)!";
                break;
            case SettlementTier.TradingAvenue:
                title = "🛒 Tuyến Phố Thương Mại";
                desc = $"Đã hình thành trục giao thương sầm uất kéo dài ({count} ô)!";
                break;
        }

        Debug.Log($"<color=#10B981>[LumiWorld Settlement] {title} - {desc}</color>");

        if (HexPlacementPreviewHUD.Instance != null)
        {
            HexPlacementPreviewHUD.ShowFeedbackToast($"{title}: {desc}");
        }
    }
}
