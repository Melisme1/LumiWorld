using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chuyên trách việc dựng vành đai tường thành / hàng rào KHÉP KÍN 360 ĐỘ cho Quần thể định cư.
/// Đảm bảo nguyên tắc bất biến: Dù 1 ô hay 10 ô, ranh giới luôn luôn được hàn kín 100%,
/// tự động tháo dỡ các vách ngăn bên trong khi cụm mở rộng.
/// </summary>
public class SettlementBorderBuilder : MonoBehaviour
{
    private static SettlementBorderBuilder _instance;
    public static SettlementBorderBuilder Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<SettlementBorderBuilder>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("SettlementBorderBuilder");
                    _instance = go.AddComponent<SettlementBorderBuilder>();
                }
            }
            return _instance;
        }
    }

    private Transform _bordersParent;

    private void Awake()
    {
        if (_instance == null) _instance = this;
        EnsureBordersParent();
    }

    private void EnsureBordersParent()
    {
        if (_bordersParent == null)
        {
            GameObject parentGo = new GameObject("Settlement_Borders_Container");
            _bordersParent = parentGo.transform;
        }
    }

    /// <summary>
    /// Kiểm tra xem prefab có phải là dạng KayKit Hex-Centered (pivot tại tâm ô hex, mesh bên trong lệch sẵn ra mép ở X = -1.0)
    /// </summary>
    public static bool IsHexCenteredModel(GameObject prefab)
    {
        if (prefab == null) return false;
        MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            return mf.sharedMesh.bounds.center.x < -0.5f;
        }
        return false;
    }

    /// <summary>
    /// Tính góc xoay Yaw thuần túy trên mặt phẳng XZ (quanh trục Y thẳng đứng) từ vector hướng gốc sang vector đích.
    /// Đảm bảo góc luôn nằm ngang 100%, tuyệt đối không bao giờ bị lộn ngược trục Y (pitch/roll).
    /// </summary>
    public static Quaternion GetHorizontalRotation(Vector3 fromDir, Vector3 toDir)
    {
        fromDir.y = 0;
        toDir.y = 0;
        if (fromDir.sqrMagnitude < 0.0001f || toDir.sqrMagnitude < 0.0001f)
            return Quaternion.identity;

        float angle = Vector2.SignedAngle(
            new Vector2(fromDir.x, fromDir.z),
            new Vector2(toDir.x, toDir.z)
        );

        return Quaternion.Euler(0, -angle, 0);
    }

    /// <summary>
    /// Xây dựng lại toàn bộ ranh giới khép kín cho một cụm ô định cư
    /// </summary>
    public GameObject BuildClosedBorders(
        int clusterId,
        List<HexCoordinates> clusterHexes,
        SettlementTier tier,
        SettlementAssetConfig config,
        HexWorldGenerator worldGen,
        Dictionary<HexCoordinates, SettlementTile> settlementTiles = null)
    {
        if (config == null || clusterHexes == null || clusterHexes.Count == 0) return null;

        EnsureBordersParent();

        // 1. Tìm hoặc dọn sạch container viền cũ của cụm này
        string containerName = $"Borders_Cluster_{clusterId}";
        Transform oldContainer = _bordersParent.Find(containerName);
        if (oldContainer != null)
        {
            if (Application.isPlaying) Destroy(oldContainer.gameObject);
            else DestroyImmediate(oldContainer.gameObject);
        }

        GameObject borderContainer = new GameObject(containerName);
        borderContainer.transform.SetParent(_bordersParent, false);

        // 2. Tính toán toàn bộ các cạnh biên giới ngoài cùng của cụm
        List<SettlementClusterCalculator.PerimeterEdge> perimeterEdges =
            SettlementClusterCalculator.CalculatePerimeterEdges(clusterHexes);

        HashSet<HexCoordinates> clusterSet = new HashSet<HexCoordinates>(clusterHexes);


        // Thu thập các cạnh vách đá nội bộ giữa các tầng cao thấp (Terrace Cliff Edges):
        // Tuyệt đối KHÔNG đưa vào perimeterEdges để tầng trên không bị tường thành bịt kín cô lập!
        // Thay vào đó, dùng cliffEdges để dựng cầu thang bộ kết nối tầng và ban công vọng cảnh.
        List<(HexCoordinates upperHex, HexCoordinates lowerHex, int dir, float heightDiff)> cliffEdges = 
            new List<(HexCoordinates, HexCoordinates, int, float)>();

        if (settlementTiles != null)
        {
            foreach (var hex in clusterHexes)
            {
                if (!settlementTiles.TryGetValue(hex, out SettlementTile upperTile) || upperTile == null) continue;
                float upperY = upperTile.GetTileSurfaceY();

                for (int dir = 0; dir < 6; dir++)
                {
                    HexCoordinates neighbor = hex.GetNeighbor(dir);
                    if (!clusterSet.Contains(neighbor)) continue;

                    if (settlementTiles.TryGetValue(neighbor, out SettlementTile lowerTile) && lowerTile != null)
                    {
                        float lowerY = lowerTile.GetTileSurfaceY();
                        float diff = upperY - lowerY;
                        if (diff > 0.35f)
                        {
                            cliffEdges.Add((hex, neighbor, dir, diff));
                        }
                    }
                }
            }
        }

        if (perimeterEdges.Count == 0) return borderContainer;

        // 3. Chọn bộ model (Tường đá cho GrandCity, Rào gỗ cho Làng & Thị trấn)
        bool isStoneWall = (tier == SettlementTier.GrandCity);
        GameObject straightPrefab = isStoneWall ? config.wallStraight : config.fenceStraight;
        GameObject gatePrefab = isStoneWall ? config.wallGate : config.fenceGate;
        float yOffset = isStoneWall ? config.wallYOffset : config.fenceYOffset;

        if (straightPrefab == null && isStoneWall) straightPrefab = config.fenceStraight;
        if (gatePrefab == null && isStoneWall) gatePrefab = config.fenceGate;


        // Bán kính đặt rào an toàn trên bề mặt ô lục giác:
        // - Rào gỗ (Làng & Thị trấn) mỏng nhẹ (~0.05m): đặt tại 0.93f
        // - Tường đá (Đại Đô Thị) có bề dày thực tế ~0.22m: đặt tại 0.925f để mép ngoài tường
        //   nằm khít khao viền trên của ô lục giác, che phủ mép bevel, hoàn toàn vững chãi, 0% trôi ra vách đá hay rơi xuống biển.
        float targetEdgeDistance = isStoneWall ? (HexMetrics.InnerRadius * 0.925f) : (HexMetrics.InnerRadius * 0.93f);
        float borderScale = config.GetScale(SettlementScaleType.Border);

        // 4. Dựng các đoạn tường/rào thẳng và cổng dọc theo từng cạnh biên giới
        foreach (var edge in perimeterEdges)
        {
            GameObject tileObj = null;
            if (settlementTiles != null && settlementTiles.TryGetValue(edge.Hex, out SettlementTile sTile) && sTile != null)
            {
                tileObj = sTile.gameObject;
            }
            else if (worldGen != null && worldGen.MapTiles != null)
            {
                worldGen.MapTiles.TryGetValue(edge.Hex, out tileObj);
            }

            float surfaceY = HexMetrics.TileHeight;
            Vector3 tileWorldCenter;
            if (tileObj != null)
            {
                var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tileObj);
                surfaceY = topY > tileObj.transform.position.y ? topY : tileObj.transform.position.y + HexMetrics.TileHeight;
                tileWorldCenter = new Vector3(tileObj.transform.position.x, surfaceY, tileObj.transform.position.z);
            }
            else
            {
                tileWorldCenter = HexMetrics.HexToWorldPosition(edge.Hex, surfaceY);
            }

            // Tọa độ trung điểm của cạnh hex theo hướng dir (0..5)
            Vector3 edgeLocal = RiverSplinePath.GetEdgeMidpointLocal(edge.Direction);
            Vector3 outwardNormal = edgeLocal.normalized;
            outwardNormal.y = 0;

            GameObject prefabToSpawn = edge.IsGate && gatePrefab != null ? gatePrefab : straightPrefab;
            if (!SettlementAssetConfig.IsValidPrefab(prefabToSpawn)) continue;

            Vector3 spawnWorldPos;
            Quaternion rot;

            if (IsHexCenteredModel(prefabToSpawn))
            {
                // Model dạng KayKit Hex-Centered (fence_wood_straight, fence_stone_straight):
                // Pivot vốn nằm ở tâm ô hex (0,0,0) và mesh đã lệch sẵn ở X = -1.0.
                // Để đảm bảo khoảng cách luôn chuẩn targetEdgeDistance bất kể scale người dùng chỉnh:
                Vector3 radialOffset = outwardNormal * (targetEdgeDistance - 1.0f * borderScale);
                spawnWorldPos = tileWorldCenter + radialOffset + new Vector3(0, yOffset, 0);

                // Xoay vector (-1, 0, 0) về hướng outwardNormal thuần túy quanh trục Y để thân rào chạy dọc viền cạnh
                rot = GetHorizontalRotation(Vector3.left, outwardNormal);

                if (Mathf.Abs(config.borderRotationOffset) > 0.001f)
                {
                    rot *= Quaternion.Euler(0, config.borderRotationOffset, 0);
                }

                GameObject borderPiece = Instantiate(prefabToSpawn, spawnWorldPos, rot, borderContainer.transform);
                float heightMult = config.borderHeightMultiplier > 0.01f ? config.borderHeightMultiplier : 1.0f;
                borderPiece.transform.localScale = new Vector3(borderScale, borderScale * heightMult, borderScale);
                borderPiece.name = $"{(edge.IsGate ? "Gate" : "Wall")}_{edge.Hex.Q}_{edge.Hex.R}_Dir{edge.Direction}";
            }
            else
            {
                // Model chuẩn thông thường (wall_straight, wall_straight_gate):
                // Pivot ở giữa thân tường. Kích thước gốc trong FBX: Dài 2.0m (trục X), Bề dày 0.8m (trục Z), Chiều cao 1.1m (trục Y).
                spawnWorldPos = tileWorldCenter + outwardNormal * targetEdgeDistance + new Vector3(0, yOffset, 0);

                rot = outwardNormal.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(outwardNormal, Vector3.up) * Quaternion.Euler(0, config.borderRotationOffset, 0)
                    : Quaternion.Euler(0, config.borderRotationOffset, 0);

                GameObject borderPiece = Instantiate(prefabToSpawn, spawnWorldPos, rot, borderContainer.transform);

                // Scale chuẩn xác để tường đá khớp hoàn mỹ với cạnh lục giác (cạnh dài 1.155m, bán kính trong 1.0m):
                // - Chiều dài (X): scale 0.58f -> chiều dài thực tế = 2.0m * 0.58 = 1.16m (khớp chính xác với độ dài cạnh 1.155m, không thừa thãi xuyên thủng góc lõm)
                // - Bề dày (Z): scale 0.28f -> bề dày thực tế = 0.8m * 0.28 = 0.224m (độ dày tường thành đá kiên cố, vừa vặn nằm trên mặt phẳng)
                // - Chiều cao (Y): scale theo borderHeightMultiplier -> tầm 0.50m - 0.55m
                float heightRatio = config.borderHeightMultiplier > 0.01f ? config.borderHeightMultiplier * 0.88f : 0.48f;
                float sx = borderScale * 0.58f;
                float sy = borderScale * heightRatio;
                float sz = borderScale * 0.28f;
                borderPiece.transform.localScale = new Vector3(sx, sy, sz);
                borderPiece.name = $"{(edge.IsGate ? "Gate" : "Wall")}_{edge.Hex.Q}_{edge.Hex.R}_Dir{edge.Direction}";
            }
        }

        // 4.5. Dựng Cầu thang bộ kết nối tầng (chỉ khi bật spawnTerraceLadders trong config)
        if (config.spawnTerraceLadders && cliffEdges.Count > 0)
        {
            BuildTerraceTransitions(borderContainer.transform, cliffEdges, isStoneWall, borderScale, yOffset, settlementTiles, config);
        }

        // 5. Cắm Tháp canh (Towers) hoặc Trụ góc tại các góc cua lồi nếu là Đại Đô Thị
        if (isStoneWall && config.wallTower != null)
        {
            SpawnCornerTowers(borderContainer.transform, clusterHexes, clusterSet, config, worldGen, yOffset, settlementTiles);
        }

        return borderContainer;
    }

    /// <summary>
    /// Xây dựng các điểm nối bậc tam cấp / cầu thang và lan can ban công vọng cảnh giữa các tầng cao thấp,
    /// tạo nên một tổng thể thành phố đa tầng gắn kết, liền mạch và sống động (Terraced Citadel) như Townscaper/Tiny Glade.
    /// </summary>
    private void BuildTerraceTransitions(
        Transform parent,
        List<(HexCoordinates upperHex, HexCoordinates lowerHex, int dir, float heightDiff)> cliffEdges,
        bool isStoneWall,
        float borderScale,
        float yOffset,
        Dictionary<HexCoordinates, SettlementTile> settlementTiles,
        SettlementAssetConfig config)
    {
        if (cliffEdges == null || cliffEdges.Count == 0 || settlementTiles == null || config == null) return;

        // Dùng mô hình thang chuẩn KayKit Medieval (ladder.fbx)
        GameObject stairPrefab = config.woodStairsPrefab;
        if (stairPrefab == null && config.streetProps != null)
        {
            stairPrefab = config.streetProps.Find(p => SettlementAssetConfig.IsValidPrefab(p) && p.name.ToLower().Contains("ladder"));
        }

        HashSet<string> processedEdges = new HashSet<string>();

        foreach (var cliff in cliffEdges)
        {
            string edgeKey = cliff.upperHex.Q < cliff.lowerHex.Q || (cliff.upperHex.Q == cliff.lowerHex.Q && cliff.upperHex.R < cliff.lowerHex.R)
                ? $"{cliff.upperHex.Q}_{cliff.upperHex.R}_to_{cliff.lowerHex.Q}_{cliff.lowerHex.R}"
                : $"{cliff.lowerHex.Q}_{cliff.lowerHex.R}_to_{cliff.upperHex.Q}_{cliff.upperHex.R}";

            if (processedEdges.Contains(edgeKey)) continue;
            processedEdges.Add(edgeKey);

            if (!settlementTiles.TryGetValue(cliff.upperHex, out SettlementTile uTile) ||
                !settlementTiles.TryGetValue(cliff.lowerHex, out SettlementTile lTile) ||
                uTile == null || lTile == null) continue;

            // Kiểm tra xem trên cạnh này đã có thác nước (Waterfall) của sông hay chưa
            bool hasWaterfall = false;
            RiverTileSegment uRiver = uTile.GetComponentInChildren<RiverTileSegment>();
            RiverTileSegment lRiver = lTile.GetComponentInChildren<RiverTileSegment>();
            int dirToLower = -1;
            for (int d = 0; d < 6; d++)
            {
                if (cliff.upperHex.GetNeighbor(d) == cliff.lowerHex)
                {
                    dirToLower = d;
                    break;
                }
            }

            if (dirToLower >= 0)
            {
                int dirToUpper = (dirToLower + 3) % 6;
                if (uRiver != null && (uRiver.HasPort(dirToLower) || uRiver.transform.Find($"Waterfall_Dir{dirToLower}") != null))
                {
                    hasWaterfall = true;
                }
                else if (lRiver != null && lRiver.HasPort(dirToUpper))
                {
                    hasWaterfall = true;
                }
            }

            Vector3 uPos = uTile.transform.position;
            Vector3 lPos = lTile.transform.position;
            float uY = uTile.GetTileSurfaceY();
            float lY = lTile.GetTileSurfaceY();

            Vector3 midXZ = (uPos + lPos) * 0.5f;
            Vector3 toUpper = (uPos - lPos).normalized;
            toUpper.y = 0;

            // DỰNG THANG BẮC VÁCH NÚI KẾT NỐI TẦNG (Nếu chưa có thác nước)
            if (!hasWaterfall && SettlementAssetConfig.IsValidPrefab(stairPrefab))
            {
                // Chân thang tựa sát vào vách đá tầng dưới
                Vector3 ladderPos = midXZ - toUpper * 0.05f;
                ladderPos.y = lY + yOffset;

                // Xoay thang nghiêng tự nhiên 12 độ tựa vào vách núi
                Quaternion ladderRot = Quaternion.LookRotation(toUpper, Vector3.up) * Quaternion.Euler(12f, 0, 0);

                GameObject stairObj = Instantiate(stairPrefab, ladderPos, ladderRot, parent);
                if (stairObj != null)
                {
                    // Scale thang theo chiều cao vách đá chuẩn xác, không bị biến dạng hay phình to
                    float heightScale = Mathf.Clamp(cliff.heightDiff / 1.0f, 0.8f, 1.4f);
                    stairObj.transform.localScale = Vector3.Scale(
                        stairPrefab.transform.localScale, 
                        new Vector3(borderScale * 0.45f, borderScale * heightScale, borderScale * 0.45f)
                    );
                    stairObj.name = $"Cliff_Ladder_{cliff.upperHex.Q}_{cliff.upperHex.R}_to_{cliff.lowerHex.Q}_{cliff.lowerHex.R}";
                }
            }
        }
    }

    private static bool IsEdgeBorderOrUpperCliff(
        HexCoordinates hex, 
        int dir, 
        HashSet<HexCoordinates> clusterSet, 
        Dictionary<HexCoordinates, SettlementTile> settlementTiles)
    {
        HexCoordinates neighbor = hex.GetNeighbor(dir);
        // Chỉ là đường biên ngoài cùng nếu ô hàng xóm KHÔNG nằm trong cụm đô thị!
        return !clusterSet.Contains(neighbor);
    }

    private struct CornerCandidate
    {
        public Vector3 worldPos;
        public Quaternion rotation;
        public float distToClusterCenter;
        public HexCoordinates hex;
        public int dir;
    }

    /// <summary>
    /// Cắm tháp canh tại các góc cua phòng thủ xung yếu (Bastions) cho Đại Đô Thị.
    /// Khắc phục triệt để lỗi tràn viền và lỗi nhím:
    /// 1. Tọa độ tháp được kéo vào trong (0.83f) để 100% đế tháp đứng vững chãi trên bề mặt ô đất, không bị chìa ra biển.
    /// 2. Giới hạn số lượng 3 - 4 tháp tại các góc nhô xa nhất (bastions), cấm spam tháp dày đặc tại mọi góc cua.
    /// 3. Khoảng cách giữa 2 tháp tối thiểu ~2.1m để giữa các tháp luôn có tường thành đá chạy dài uy nghi.
    /// </summary>
    private void SpawnCornerTowers(
        Transform parent,
        List<HexCoordinates> clusterHexes,
        HashSet<HexCoordinates> clusterSet,
        SettlementAssetConfig config,
        HexWorldGenerator worldGen,
        float yOffset,
        Dictionary<HexCoordinates, SettlementTile> settlementTiles = null)
    {
        if (clusterHexes == null || clusterHexes.Count == 0) return;

        // 1. Tính tâm hình học của toàn cụm Đại Đô Thị
        Vector3 clusterCenter = Vector3.zero;
        foreach (var h in clusterHexes)
        {
            clusterCenter += HexMetrics.HexToWorldPosition(h);
        }
        clusterCenter /= clusterHexes.Count;

        // 2. Thu thập toàn bộ các góc lồi (120 độ) trên đường viền ngoài
        List<CornerCandidate> candidates = new List<CornerCandidate>();

        foreach (var hex in clusterHexes)
        {
            for (int dir = 0; dir < 6; dir++)
            {
                int nextDir = (dir + 1) % 6;
                bool isDirBorder = IsEdgeBorderOrUpperCliff(hex, dir, clusterSet, settlementTiles);
                bool isNextBorder = IsEdgeBorderOrUpperCliff(hex, nextDir, clusterSet, settlementTiles);

                // Nếu cả 2 cạnh kề nhau đều là cạnh biên giới -> Đây là một góc cua nhọn 120 độ ở ngoài!
                if (isDirBorder && isNextBorder)
                {
                    Vector3 midA = RiverSplinePath.GetEdgeMidpointLocal(dir);
                    Vector3 midB = RiverSplinePath.GetEdgeMidpointLocal(nextDir);
                    Vector3 cornerDir = (midA + midB).normalized;
                    cornerDir.y = 0;

                    GameObject tileObj = null;
                    if (settlementTiles != null && settlementTiles.TryGetValue(hex, out SettlementTile sTile) && sTile != null)
                    {
                        tileObj = sTile.gameObject;
                    }
                    else if (worldGen != null && worldGen.MapTiles != null)
                    {
                        worldGen.MapTiles.TryGetValue(hex, out tileObj);
                    }

                    float surfaceY = HexMetrics.TileHeight;
                    Vector3 tileCenter;
                    if (tileObj != null)
                    {
                        var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tileObj);
                        surfaceY = topY > tileObj.transform.position.y ? topY : tileObj.transform.position.y + HexMetrics.TileHeight;
                        tileCenter = new Vector3(tileObj.transform.position.x, surfaceY, tileObj.transform.position.z);
                    }
                    else
                    {
                        tileCenter = HexMetrics.HexToWorldPosition(hex, surfaceY);
                    }

                    // Tọa độ góc lồi: Đặt tại giao điểm góc của 2 bức tường đá:
                    float cornerRadius = (HexMetrics.InnerRadius * 0.925f) / 0.8660254f;
                    Vector3 cornerWorldPos = tileCenter + cornerDir * cornerRadius + new Vector3(0, yOffset, 0);
                    Quaternion towerRot = Quaternion.LookRotation(cornerDir, Vector3.up);

                    // Tránh thêm các góc bị trùng vị trí (< 0.4m)
                    bool isDuplicate = false;
                    foreach (var c in candidates)
                    {
                        if (Vector3.Distance(c.worldPos, cornerWorldPos) < 0.4f)
                        {
                            isDuplicate = true;
                            break;
                        }
                    }

                    if (!isDuplicate)
                    {
                        float distToCenter = Vector2.Distance(
                            new Vector2(cornerWorldPos.x, cornerWorldPos.z),
                            new Vector2(clusterCenter.x, clusterCenter.z)
                        );

                        candidates.Add(new CornerCandidate
                        {
                            worldPos = cornerWorldPos,
                            rotation = towerRot,
                            distToClusterCenter = distToCenter,
                            hex = hex,
                            dir = dir
                        });
                    }
                }
            }
        }

        // 3. Thu thập các góc lõm (Concave Corners - nơi 2 ô trong cụm gặp 1 khoảng trống ngoài, góc 240 độ)
        List<CornerCandidate> concaveCandidates = new List<CornerCandidate>();
        foreach (var hex in clusterHexes)
        {
            GameObject tileObj = null;
            if (settlementTiles != null && settlementTiles.TryGetValue(hex, out SettlementTile sTile) && sTile != null)
                tileObj = sTile.gameObject;
            else if (worldGen != null && worldGen.MapTiles != null)
                worldGen.MapTiles.TryGetValue(hex, out tileObj);

            float surfaceY = HexMetrics.TileHeight;
            Vector3 tileCenter;
            if (tileObj != null)
            {
                var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tileObj);
                surfaceY = topY > tileObj.transform.position.y ? topY : tileObj.transform.position.y + HexMetrics.TileHeight;
                tileCenter = new Vector3(tileObj.transform.position.x, surfaceY, tileObj.transform.position.z);
            }
            else
            {
                tileCenter = HexMetrics.HexToWorldPosition(hex, surfaceY);
            }

            for (int dir = 0; dir < 6; dir++)
            {
                int nextDir = (dir + 1) % 6;
                bool isDirBorder = IsEdgeBorderOrUpperCliff(hex, dir, clusterSet, settlementTiles);
                bool isNextBorder = IsEdgeBorderOrUpperCliff(hex, nextDir, clusterSet, settlementTiles);

                // Góc lõm: một cạnh là biên giới, cạnh kề là nội bộ nối sang ô láng giềng trong cụm
                if (isDirBorder && !isNextBorder)
                {
                    HexCoordinates neighborInCluster = hex.GetNeighbor(nextDir);
                    // Kiểm tra đỉnh chung giữa dir và nextDir có tiếp giáp khoảng trống ngoài
                    Vector3 midA = RiverSplinePath.GetEdgeMidpointLocal(dir);
                    Vector3 midB = RiverSplinePath.GetEdgeMidpointLocal(nextDir);
                    Vector3 cornerLocal = (midA + midB) * (2f / 3f);
                    Vector3 cornerWorldPos = tileCenter + cornerLocal * 0.95f + new Vector3(0, yOffset, 0);

                    bool isDup = false;
                    foreach (var c in concaveCandidates)
                    {
                        if (Vector3.Distance(c.worldPos, cornerWorldPos) < 0.4f)
                        {
                            isDup = true;
                            break;
                        }
                    }

                    if (!isDup)
                    {
                        Vector3 facingDir = (tileCenter - cornerWorldPos).normalized;
                        facingDir.y = 0;
                        concaveCandidates.Add(new CornerCandidate
                        {
                            worldPos = cornerWorldPos,
                            rotation = Quaternion.LookRotation(facingDir, Vector3.up),
                            distToClusterCenter = Vector2.Distance(new Vector2(cornerWorldPos.x, cornerWorldPos.z), new Vector2(clusterCenter.x, clusterCenter.z)),
                            hex = hex,
                            dir = dir
                        });
                    }
                }
            }
        }

        // 4. Sắp xếp các góc lồi theo thứ tự xa tâm cụm nhất -> Đây chính là các pháo đài tiền tiêu (Bastions)
        candidates.Sort((a, b) => b.distToClusterCenter.CompareTo(a.distToClusterCenter));

        // Tuyển chọn các góc bastion với khoảng cách an toàn, tạo dáng thành trì hùng vĩ
        int maxTowers = clusterHexes.Count <= 2 ? 3 : (clusterHexes.Count + 4);
        float minDistanceBetweenTowers = 0.95f; // Đảm bảo mỗi góc cua/mép vực đều có thể cắm tháp, không bị nuốt tháp ở góc vực

        List<Vector3> placedTowerPositions = new List<Vector3>();
        float towerScale = config.GetScale(SettlementScaleType.Castle) * 0.70f;

        foreach (var cand in candidates)
        {
            if (placedTowerPositions.Count >= maxTowers) break;

            bool tooClose = false;
            foreach (var pos in placedTowerPositions)
            {
                if (Vector3.Distance(pos, cand.worldPos) < minDistanceBetweenTowers)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose && SettlementAssetConfig.IsValidPrefab(config.wallTower))
            {
                placedTowerPositions.Add(cand.worldPos);
                GameObject tower = Instantiate(config.wallTower, cand.worldPos, cand.rotation, parent);
                if (tower != null)
                {
                    tower.transform.localScale = Vector3.one * towerScale;
                    tower.name = $"Tower_Bastion_{cand.hex.Q}_{cand.hex.R}_Dir{cand.dir}";
                }
            }
        }

        // 5. Cắm trụ thành / tháp góc nhỏ tại các góc lõm (Concave Bastions) để hàn gắn mép tường, triệt tiêu khe hở
        if (SettlementAssetConfig.IsValidPrefab(config.wallTower))
        {
            float innerTowerScale = towerScale * 0.65f;
            foreach (var conc in concaveCandidates)
            {
                GameObject tower = Instantiate(config.wallTower, conc.worldPos, conc.rotation, parent);
                if (tower != null)
                {
                    tower.transform.localScale = Vector3.one * innerTowerScale;
                    tower.name = $"Tower_InnerBastion_{conc.hex.Q}_{conc.hex.R}_Dir{conc.dir}";
                }
            }
        }
    }
}

