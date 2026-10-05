using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    // Quản lý các cầu nối và nắp góc đã tạo để không tạo trùng lặp
    private readonly HashSet<string> _createdEdgeBridges = new HashSet<string>();
    private readonly HashSet<string> _createdCornerFillers = new HashSet<string>();
    private readonly HashSet<string> _celebratedClusters = new HashSet<string>();

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

    // TUYỆT ĐỐI KHÔNG tự động quét toàn map trong Start() để tránh sinh dải nối trên các ô đất thô chưa đặt bài!

    /// <summary>
    /// Kích hoạt kết nối khi một lá bài Habitat vừa được người chơi đặt xuống thành công
    /// </summary>
    public void ConnectCluster(PlacedCard placedCard)
    {
        if (placedCard == null || placedCard.cardData == null) return;

        // Chỉ xử lý nếu lá bài này là thẻ Habitat (có hệ sinh thái props)
        if (!placedCard.cardData.HasHabitatProps()) return;

        ConnectAllNearby(placedCard.placedHex, placedCard.cardData);
    }

    /// <summary>
    /// Quét và kết nối ô tại centerHex với tất cả các ô láng giềng kề nó THỎA MÃN CÙNG HABITAT
    /// </summary>
    public void ConnectAllNearby(HexCoordinates centerHex, CardData centerHabitatData = null)
    {
        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen == null || worldGen.MapTiles == null) return;

        if (!worldGen.MapTiles.TryGetValue(centerHex, out GameObject centerTile) || centerTile == null)
            return;

        // Xác định thẻ Habitat trên ô trung tâm (duyệt qua toàn bộ PlacedCard trên ô, không bị che bởi thẻ Rain)
        if (centerHabitatData == null || !centerHabitatData.HasHabitatProps())
        {
            PlacedCard centerCard = GetHabitatCardOnTile(centerTile);
            if (centerCard == null || centerCard.cardData == null || !centerCard.cardData.HasHabitatProps())
                return;

            centerHabitatData = centerCard.cardData;
        }

        // 1. Quét tìm tất cả các ngã ba 3 ô (Corner Junctions) MỚI ĐƯỢC TẠO THÀNH bởi ô centerHex này
        List<(HexCoordinates hB, GameObject tileB, HexCoordinates hC, GameObject tileC, string cornerKey)> newCorners =
            new List<(HexCoordinates, GameObject, HexCoordinates, GameObject, string)>();
        HashSet<string> clusterEdgeKeys = new HashSet<string>();

        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates hB = centerHex.GetNeighbor(dir);
            HexCoordinates hC = centerHex.GetNeighbor((dir + 1) % 6);

            if (worldGen.MapTiles.TryGetValue(hB, out GameObject tileB) && tileB != null &&
                worldGen.MapTiles.TryGetValue(hC, out GameObject tileC) && tileC != null)
            {
                PlacedCard cardB = GetHabitatCardOnTile(tileB);
                PlacedCard cardC = GetHabitatCardOnTile(tileC);

                if (cardB != null && cardC != null &&
                    AreHabitatsMatching(centerHabitatData, cardB.cardData) &&
                    AreHabitatsMatching(centerHabitatData, cardC.cardData))
                {
                    string cornerKey = GetCornerKey(centerHex, hB, hC);
                    if (!_createdCornerFillers.Contains(cornerKey))
                    {
                        newCorners.Add((hB, tileB, hC, tileC, cornerKey));
                        // 2 cạnh mới nối từ centerHex sang 2 ô láng giềng thuộc cụm 3 ô này:
                        clusterEdgeKeys.Add(GetEdgeKey(centerHex, hB));
                        clusterEdgeKeys.Add(GetEdgeKey(centerHex, hC));
                    }
                }
            }
        }

        // 2. Quét 6 hướng xung quanh centerHex để hàn rãnh mesh và sinh props viền
        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates neighborHex = centerHex.GetNeighbor(dir);

            if (worldGen.MapTiles.TryGetValue(neighborHex, out GameObject neighborTile) && neighborTile != null)
            {
                PlacedCard neighborCard = GetHabitatCardOnTile(neighborTile);

                if (neighborCard != null && AreHabitatsMatching(centerHabitatData, neighborCard.cardData))
                {
                    string edgeKey = GetEdgeKey(centerHex, neighborHex);
                    if (!_createdEdgeBridges.Contains(edgeKey))
                    {
                        _createdEdgeBridges.Add(edgeKey);
                        CreateEdgeSeamBridge(centerHex, centerTile, neighborHex, neighborTile, edgeKey, centerHabitatData, worldGen);

                        // CHỈ sinh props theo quy tắc 2 ô liền nhau nếu cạnh này KHÔNG thuộc cụm 3 ô vừa tạo.
                        // (Cụm 3 ô sẽ sinh 1 cây trung ở tâm và tán props ra 2 cạnh viền này)
                        if (spawnConnectorProps && !clusterEdgeKeys.Contains(edgeKey))
                        {
                            SpawnEdgeConnectorProps(centerTile, neighborTile, centerHabitatData);
                        }
                    }
                }
            }
        }

        // 3. Xử lý các ngã ba 3 ô mới tạo thành:
        // - Tạo nắp phẳng hàn ngã ba
        // - Sinh 1 cây trung ở giữa tâm ngã ba và tán props ra 2 cạnh viền còn lại
        for (int i = 0; i < newCorners.Count; i++)
        {
            var corner = newCorners[i];
            _createdCornerFillers.Add(corner.cornerKey);
            CreateCornerFiller(centerTile, corner.tileB, corner.tileC, corner.cornerKey, centerHabitatData);

            if (spawnCornerProps)
            {
                SpawnClusterCornerProps(centerTile, corner.tileB, corner.tileC, centerHabitatData);
            }
        }

        // 4. Kiểm tra ăn mừng cụm hoàn thành (>= 3 ô Habitat cùng loại kề nhau)
        CheckAndCelebrateCluster(centerHex, centerHabitatData, worldGen);
    }

    /// <summary>
    /// Tìm PlacedCard chứa Habitat Props trên một GameObject Tile (hỗ trợ trường hợp có cả PlacedCard của Rain)
    /// </summary>
    public static PlacedCard GetHabitatCardOnTile(GameObject tileObj)
    {
        if (tileObj == null) return null;

        PlacedCard[] cards = tileObj.GetComponentsInChildren<PlacedCard>();
        if (cards != null)
        {
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

    /// <summary>
    /// So khớp xem 2 thẻ bài có cùng loại Habitat hay không
    /// </summary>
    public static bool AreHabitatsMatching(CardData a, CardData b)
    {
        if (a == null || b == null) return false;
        if (!a.HasHabitatProps() || !b.HasHabitatProps()) return false;

        // 1. So khớp trực tiếp ID hoặc Name
        if (a.cardID == b.cardID) return true;
        if (a.cardName == b.cardName) return true;

        // 2. So khớp nhóm Biome tương đương (ví dụ Forest và Leafwood đều là Rừng cây Leafwood)
        string gA = GetHabitatFamily(a.cardName);
        string gB = GetHabitatFamily(b.cardName);

        if (!string.IsNullOrEmpty(gA) && gA == gB) return true;

        return false;
    }

    private static string GetHabitatFamily(string cardName)
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

    /// <summary>
    /// Tạo cầu nối Seam Bridge khớp 100% hình học:
    /// - Mặt phẳng đỉnh (Top Flat Cap): chườm kín rãnh giữa 2 ô
    /// - Sườn dốc mép ngoài (Outer Bevel Skirts): vát dốc nối khít mép vát của 2 ô (xóa bỏ 100% góc khuyết chữ V)
    /// - Thành đứng bên ngoài (Vertical Wall Skirts): bịt kín khe nứt thành bên xuống đáy nước biển
    /// </summary>
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

        Vector3 u = dirXZ / distXZ;                 // Vector đơn vị từ A sang B
        Vector3 v = new Vector3(-u.z, 0f, u.x);     // Vector tiếp tuyến dọc theo cạnh chung
        Vector3 midPoint = (posA + posB) * 0.5f;    // Trung điểm cạnh chung

        float halfEdge = HexMetrics.OuterRadius * 0.5f; // 0.5773503f

        // Hai đỉnh đầu mút của cạnh tiếp giáp (tại tầng Rim của lục giác)
        Vector3 V1_world = midPoint - v * halfEdge;
        Vector3 V2_world = midPoint + v * halfEdge;

        // Lấy thông số độ cao chuẩn xác theo khối Tile thực tế (không dựa vào cardData)
        var (topYA, rimYA, bottomYA) = GetTileHeightLevels(tileA);
        var (topYB, rimYB, bottomYB) = GetTileHeightLevels(tileB);

        // Nếu 2 ô ở 2 tầng độ cao khác nhau (vách núi bậc thang), giữ nguyên bậc thang tự nhiên, không tạo cầu nối phẳng
        if (Mathf.Abs(topYA - topYB) > 0.15f) return;

        float topY = (topYA + topYB) * 0.5f;
        float rimY = (rimYA + rimYB) * 0.5f;
        float bottomY = Mathf.Min(bottomYA, bottomYB);
        float elevatedTopY = topY + bridgeYOffset;

        // Kiểm tra xem đầu mút V1 và V2 có phải là mép ngoài (Outer Rim) hay ngã ba nội bộ (Internal Corner)
        bool v1IsOuter = !HasThirdHabitatNeighbor(hexA, hexB, V1_world, habitatData, worldGen);
        bool v2IsOuter = !HasThirdHabitatNeighbor(hexA, hexB, V2_world, habitatData, worldGen);

        // Lấy UV tương ứng cho từng bề mặt
        var (topGrassUV, bevelUV, wallUV) = GetTilePaletteUVs(tileA, habitatData);
        Material tileMat = GetTileMaterial(tileA);

        // Xây dựng Mesh hoàn chỉnh trong Local Space quanh midPoint
        Mesh bridgeMesh = BuildSeamBridgeMeshLocal(
            u, v, halfEdge,
            elevatedTopY - midPoint.y,
            rimY - midPoint.y,
            bottomY - midPoint.y,
            v1IsOuter, v2IsOuter,
            topGrassUV, bevelUV, wallUV
        );

        // Tạo GameObject đại diện đặt đúng tại midPoint
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

        if (spawnSeamMicroGrass)
        {
            SpawnBorderMicroGrass(midPoint, u, v, halfEdge, elevatedTopY, habitatData);
        }
    }

    /// <summary>
    /// Xây dựng Mesh hình học chuẩn xác của Seam Bridge trong Local Space của midPoint:
    /// - Đoạn phẳng trên đỉnh kết thúc tại: 0.93 * halfEdge = 0.5369f
    /// - Sườn vát ngoài nghiêng từ 0.5369f ra 0.5774f, độ cao dốc từ localTopY xuống localRimY
    /// </summary>
    private Mesh BuildSeamBridgeMeshLocal(
        Vector3 u, Vector3 v, float halfEdge,
        float localTopY, float localRimY, float localBottomY,
        bool v1IsOuter, bool v2IsOuter,
        Vector2 topGrassUV, Vector2 bevelUV, Vector2 wallUV)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();

        Vector3 V1_loc = -v * halfEdge;
        Vector3 V2_loc = v * halfEdge;

        float flatHalfEdge = halfEdge * 0.93f; // 0.5369f
        float wTop = 0.10f;

        Vector3 startFlatV = -v * flatHalfEdge;
        Vector3 endFlatV = v * flatHalfEdge;

        // -------------------------------------------------------------
        // PHẦN 1: MẶT ĐỈNH PHẲNG (TOP FLAT CAP)
        // -------------------------------------------------------------
        int topStartIdx = vertices.Count;

        Vector3 t0 = startFlatV - u * wTop; t0.y = localTopY;
        Vector3 t1 = endFlatV - u * wTop;   t1.y = localTopY;
        Vector3 t2 = endFlatV + u * wTop;   t2.y = localTopY;
        Vector3 t3 = startFlatV + u * wTop; t3.y = localTopY;

        vertices.Add(t0); normals.Add(Vector3.up); uvs.Add(topGrassUV);
        vertices.Add(t1); normals.Add(Vector3.up); uvs.Add(topGrassUV);
        vertices.Add(t2); normals.Add(Vector3.up); uvs.Add(topGrassUV);
        vertices.Add(t3); normals.Add(Vector3.up); uvs.Add(topGrassUV);

        triangles.Add(topStartIdx + 0); triangles.Add(topStartIdx + 1); triangles.Add(topStartIdx + 2);
        triangles.Add(topStartIdx + 0); triangles.Add(topStartIdx + 2); triangles.Add(topStartIdx + 3);

        // -------------------------------------------------------------
        // PHẦN 2: SƯỜN VÁT MÉP NGOÀI TẠI V1 (OUTER BEVEL SKIRT V1)
        // Tam giác vát dốc nối trực tiếp từ cạnh (t0, t3) xuống đỉnh ngoài cùng V1_loc (tại localRimY)
        // -------------------------------------------------------------
        if (v1IsOuter)
        {
            Vector3 v1Rim = V1_loc; v1Rim.y = localRimY;
            Vector3 normBevel1 = (-v * 0.707f + Vector3.up * 0.707f).normalized;

            int bev1Idx = vertices.Count;
            vertices.Add(t0);       normals.Add(normBevel1); uvs.Add(bevelUV); // 0
            vertices.Add(t3);       normals.Add(normBevel1); uvs.Add(bevelUV); // 1
            vertices.Add(v1Rim);    normals.Add(normBevel1); uvs.Add(bevelUV); // 2

            // Tam giác dốc nghiêng từ t0, t3 xuống v1Rim (thuận kim đồng hồ khi nhìn từ ngoài)
            triangles.Add(bev1Idx + 0); triangles.Add(bev1Idx + 1); triangles.Add(bev1Idx + 2);

            // Bịt kín khe nứt thành bên từ v1Rim xuống localBottomY
            int wall1Idx = vertices.Count;
            Vector3 w0 = t0; w0.y = localRimY;
            Vector3 w1 = t3; w1.y = localRimY;
            Vector3 wBot0 = t0; wBot0.y = localBottomY;
            Vector3 wBot1 = t3; wBot1.y = localBottomY;

            vertices.Add(w0);    normals.Add(-v); uvs.Add(wallUV); // 0
            vertices.Add(w1);    normals.Add(-v); uvs.Add(wallUV); // 1
            vertices.Add(wBot1); normals.Add(-v); uvs.Add(wallUV); // 2
            vertices.Add(wBot0); normals.Add(-v); uvs.Add(wallUV); // 3

            triangles.Add(wall1Idx + 0); triangles.Add(wall1Idx + 1); triangles.Add(wall1Idx + 2);
            triangles.Add(wall1Idx + 0); triangles.Add(wall1Idx + 2); triangles.Add(wall1Idx + 3);
        }

        // -------------------------------------------------------------
        // PHẦN 3: SƯỜN VÁT MÉP NGOÀI TẠI V2 (OUTER BEVEL SKIRT V2)
        // -------------------------------------------------------------
        if (v2IsOuter)
        {
            Vector3 v2Rim = V2_loc; v2Rim.y = localRimY;
            Vector3 normBevel2 = (v * 0.707f + Vector3.up * 0.707f).normalized;

            int bev2Idx = vertices.Count;
            vertices.Add(t2);       normals.Add(normBevel2); uvs.Add(bevelUV); // 0
            vertices.Add(t1);       normals.Add(normBevel2); uvs.Add(bevelUV); // 1
            vertices.Add(v2Rim);    normals.Add(normBevel2); uvs.Add(bevelUV); // 2

            triangles.Add(bev2Idx + 0); triangles.Add(bev2Idx + 1); triangles.Add(bev2Idx + 2);

            // Bịt kín khe nứt thành bên từ v2Rim xuống localBottomY
            int wall2Idx = vertices.Count;
            Vector3 w2_0 = t2; w2_0.y = localRimY;
            Vector3 w2_1 = t1; w2_1.y = localRimY;
            Vector3 w2Bot0 = t2; w2Bot0.y = localBottomY;
            Vector3 w2Bot1 = t1; w2Bot1.y = localBottomY;

            vertices.Add(w2_0);    normals.Add(v); uvs.Add(wallUV); // 0
            vertices.Add(w2_1);    normals.Add(v); uvs.Add(wallUV); // 1
            vertices.Add(w2Bot1);  normals.Add(v); uvs.Add(wallUV); // 2
            vertices.Add(w2Bot0);  normals.Add(v); uvs.Add(wallUV); // 3

            triangles.Add(wall2Idx + 0); triangles.Add(wall2Idx + 1); triangles.Add(wall2Idx + 2);
            triangles.Add(wall2Idx + 0); triangles.Add(wall2Idx + 2); triangles.Add(wall2Idx + 3);
        }

        Mesh m = new Mesh();
        m.name = "Proc_SeamBridge";
        m.vertices = vertices.ToArray();
        m.triangles = triangles.ToArray();
        m.normals = normals.ToArray();
        m.uv = uvs.ToArray();
        m.RecalculateBounds();

        return m;
    }

    /// <summary>
    /// Quét các ngã ba 3 ô lục giác cùng Habitat tiếp giáp nhau để lấp phẳng tâm ngã ba
    /// </summary>
    private void ScanCornerJunctionsAround(
        HexCoordinates centerHex, GameObject centerTile, CardData centerHabitatData, HexWorldGenerator worldGen)
    {
        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates hB = centerHex.GetNeighbor(dir);
            HexCoordinates hC = centerHex.GetNeighbor((dir + 1) % 6);

            if (worldGen.MapTiles.TryGetValue(hB, out GameObject tileB) && tileB != null &&
                worldGen.MapTiles.TryGetValue(hC, out GameObject tileC) && tileC != null)
            {
                PlacedCard cardB = GetHabitatCardOnTile(tileB);
                PlacedCard cardC = GetHabitatCardOnTile(tileC);

                // CẢ 3 Ô BẮT BUỘC ĐỀU PHẢI ĐÃ ĐẶT THẺ HABITAT CÙNG LOẠI
                if (cardB != null && cardC != null &&
                    AreHabitatsMatching(centerHabitatData, cardB.cardData) &&
                    AreHabitatsMatching(centerHabitatData, cardC.cardData))
                {
                    string cornerKey = GetCornerKey(centerHex, hB, hC);
                    if (!_createdCornerFillers.Contains(cornerKey))
                    {
                        _createdCornerFillers.Add(cornerKey);
                        CreateCornerFiller(centerTile, tileB, tileC, cornerKey, centerHabitatData);

                        if (spawnCornerProps)
                        {
                            SpawnCornerConnectorProp(centerTile, tileB, tileC, centerHabitatData);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Tạo nắp mesh đa giác lấp phẳng điểm trũng ở ngã ba giao điểm giữa 3 ô
    /// </summary>
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

        int segments = 6;
        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];
        Vector3[] normals = new Vector3[segments + 1];
        Vector2[] uv = new Vector2[segments + 1];

        vertices[0] = Vector3.zero;
        normals[0] = Vector3.up;
        uv[0] = topGrassUV;

        float angleStep = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float rad = (i * angleStep) * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * cornerFillerRadius;
            normals[i + 1] = Vector3.up;
            uv[i + 1] = topGrassUV;

            int next = (i + 1) % segments + 1;
            triangles[i * 3 + 0] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = next;
        }

        Mesh cornerMesh = new Mesh();
        cornerMesh.name = $"Mesh_{cornerKey}";
        cornerMesh.vertices = vertices;
        cornerMesh.triangles = triangles;
        cornerMesh.normals = normals;
        cornerMesh.uv = uv;
        cornerMesh.RecalculateBounds();

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
            SpawnCornerMicroGrass(center, surfaceY, habitatData);
        }
    }

    /// <summary>
    /// Kiểm tra xem tại đỉnh V có ô Habitat thứ 3 cùng loại tiếp giáp không
    /// </summary>
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
                    // Ô thứ 3 BẮT BUỘC phải là ô đã đặt thẻ Habitat cùng loại
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

    /// <summary>
    /// Lấy độ cao chuẩn xác 100% của từng khối Tile thực tế trên Scene.
    /// Độ cao này BẮT BUỘC phải lấy từ bản thân khối đất (Mesh/Prefab của Tile),
    /// TUYỆT ĐỐI KHÔNG dùng tên lá bài (cardData) vì một lá bài (ví dụ Leafwood)
    /// có thể được đặt lên cả tầng thấp (1.0f), tầng trung (1.8f) hoặc tầng cao (2.6f).
    /// </summary>
    public static (float topY, float rimY, float bottomY) GetTileHeightLevels(GameObject tileObj, CardData cardData = null)
    {
        if (tileObj == null)
        {
            return (HexMetrics.DefaultTileY + 1.0f, HexMetrics.DefaultTileY + 0.91f, HexMetrics.DefaultTileY);
        }

        float baseY = tileObj.transform.position.y;
        float localTop = 1.0f; // Mặc định tầng thấp

        // 1. Ưu tiên 1: Đo từ MeshFilter gốc của khối lục giác
        MeshFilter tileMf = GetTileMeshFilter(tileObj);
        if (tileMf != null && tileMf.sharedMesh != null)
        {
            float scaleY = tileMf.transform.lossyScale.y;
            float meshMaxY = tileMf.sharedMesh.bounds.max.y * scaleY;

            // Mesh lục giác gốc luôn có gò nhô ở tâm cao hơn mép viền ngoài đúng ~0.10f:
            // - Tầng cao (Windheath): bounds.max.y ~ 2.716f -> mép viền phẳng chuẩn là 2.60f, rim là 2.51f
            // - Tầng trung (Leafwood): bounds.max.y ~ 1.90f  -> mép viền phẳng chuẩn là 1.80f, rim là 1.71f
            // - Tầng thấp (Bloomfield): bounds.max.y ~ 1.10f -> mép viền phẳng chuẩn là 1.00f, rim là 0.91f
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
            // 2. Ưu tiên 2: Xác định theo tên prefab hoặc thông số HexTileInfo của khối đất
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
                localTop = 1.00f; // Bloomfield / Tầng thấp
            }
        }

        float worldTop = baseY + localTop;
        float worldRim = worldTop - 0.09f;
        float worldBottom = baseY;

        return (worldTop, worldRim, worldBottom);
    }

    /// <summary>
    /// Tìm MeshFilter thuộc về khối đất nền (bỏ qua các MeshFilter thuộc về cây cối, props, cầu nối)
    /// </summary>
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

    /// <summary>
    /// Lấy tọa độ UV màu cỏ đỉnh, sườn vát và thành đá từ Preserve_Palette.png.
    /// BẮT BUỘC DỰA TRÊN BẢN THÂN KHỐI TILE THỰC TẾ (Mesh / Prefab của Tile)
    /// để màu sắc của cầu nối đồng nhất 100% với màu nền cỏ của khối lục giác bên dưới!
    /// </summary>
    public static (Vector2 topGrassUV, Vector2 bevelUV, Vector2 wallUV) GetTilePaletteUVs(GameObject tileObj, CardData cardData = null)
    {
        float grassY = 0.8125f; // Mặc định Bloomfield (tầng thấp)

        // 1. Ưu tiên 1: Đọc trực tiếp tọa độ UV từ đỉnh cao nhất của Mesh khối lục giác (nếu mesh cho phép Read/Write)
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

        // 2. Ưu tiên 2: Xác định theo tên prefab hoặc thông số HexTileInfo của khối lục giác
        string tileName = tileObj != null ? tileObj.name : "";
        HexTileInfo tileInfo = tileObj != null ? tileObj.GetComponent<HexTileInfo>() : null;
        if (tileInfo != null && tileInfo.sourcePrefab != null)
        {
            tileName = tileInfo.sourcePrefab.name;
        }

        if (tileName.IndexOf("Windheath", StringComparison.OrdinalIgnoreCase) >= 0 ||
            (tileInfo != null && tileInfo.terrainTypeIndex == 2))
        {
            grassY = 0.5625f; // Windheath (tầng cao)
        }
        else if (tileName.IndexOf("Leafwood", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 tileName.IndexOf("Forest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (tileInfo != null && tileInfo.terrainTypeIndex == 1))
        {
            grassY = 0.6875f; // Leafwood (tầng trung)
        }
        else
        {
            grassY = 0.8125f; // Bloomfield (tầng thấp)
        }

        Vector2 topGrassUV = new Vector2(0.4375f, grassY);
        Vector2 bevelUV = new Vector2(0.5625f, grassY);
        Vector2 wallUV = new Vector2(0.6875f, grassY);

        return (topGrassUV, bevelUV, wallUV);
    }

    private static Material GetTileMaterial(GameObject tileObj)
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

    /// <summary>
    /// Thu thập tọa độ XZ của tất cả các prop trang trí đã tồn tại gần tâm kiểm tra
    /// (bao gồm prop trên tileA, tileB, tileC và các prop viền đã sinh trước đó)
    /// </summary>
    public enum PropArchetype
    {
        FernBush,    // Dương xỉ, bụi lá, bụi oải hương, thạch nam
        Flower,      // Hoa chuông, hoa cúc, hoa dại
        Mushroom,    // Nấm rừng đốm đỏ
        RockAccent,  // Tảng đá rêu, phiến đá, sỏi hoa
        WoodAccent,  // Khúc gỗ mục, gốc cây rêu
        Tree,        // Cây thân gỗ phụ (birch, willow)
        Grass        // Cỏ khóm, thảm cỏ
    }

    public struct NearbyPropEntry
    {
        public Vector3 position;
        public string name;
        public PropArchetype archetype;
    }

    /// <summary>
    /// Phân loại hình thái của một prop dựa vào tên (hỗ trợ cả tên GameObject đã sinh và Prefab/Rule)
    /// </summary>
    public static PropArchetype GetArchetypeFromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return PropArchetype.FernBush;

        if (name.IndexOf("Log", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Stump", StringComparison.OrdinalIgnoreCase) >= 0)
            return PropArchetype.WoodAccent;

        if (name.IndexOf("Toadstool", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Mushroom", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Fungi", StringComparison.OrdinalIgnoreCase) >= 0)
            return PropArchetype.Mushroom;

        if (name.IndexOf("Bluebell", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Bellflower", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Flower", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Daisy", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Blossom", StringComparison.OrdinalIgnoreCase) >= 0)
            return PropArchetype.Flower;

        if (name.IndexOf("Boulder", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Slab", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Rock", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Pebble", StringComparison.OrdinalIgnoreCase) >= 0)
            return PropArchetype.RockAccent;

        if (name.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Tussock", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Carpet", StringComparison.OrdinalIgnoreCase) >= 0)
            return PropArchetype.Grass;

        if (name.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Birch", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Willow", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Oak", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Pine", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Sentinel", StringComparison.OrdinalIgnoreCase) >= 0)
            return PropArchetype.Tree;

        if (name.IndexOf("Fern", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Bush", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Hazel", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Shrub", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Lavender", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Heather", StringComparison.OrdinalIgnoreCase) >= 0)
            return PropArchetype.FernBush;

        return PropArchetype.FernBush;
    }

    public static PropArchetype GetPropArchetype(GameObject prefab, CardData.HabitatPropRule rule)
    {
        string combined = (prefab != null ? prefab.name : "") + " " + (rule != null ? rule.groupName : "");
        return GetArchetypeFromName(combined);
    }

    private static int CountArchetypeNearby(List<NearbyPropEntry> entries, PropArchetype arch)
    {
        if (entries == null) return 0;
        int count = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].archetype == arch) count++;
        }
        return count;
    }

    /// <summary>
    /// Thu thập danh sách prop đã tồn tại gần tâm kiểm tra kèm theo tên và chủng loại để phân tích đa dạng sinh thái
    /// </summary>
    private List<NearbyPropEntry> GetNearbyPropEntries(Vector3 checkCenter, float radius)
    {
        List<NearbyPropEntry> entries = new List<NearbyPropEntry>();
        float sqrRadius = radius * radius;

        if (_connectionsContainer != null)
        {
            for (int i = 0; i < _connectionsContainer.childCount; i++)
            {
                Transform child = _connectionsContainer.GetChild(i);
                if (child.name.StartsWith("Connector") || child.name.StartsWith("Corner"))
                {
                    Vector3 p = child.position;
                    float d2 = (p.x - checkCenter.x) * (p.x - checkCenter.x) + (p.z - checkCenter.z) * (p.z - checkCenter.z);
                    if (d2 <= sqrRadius)
                    {
                        entries.Add(new NearbyPropEntry
                        {
                            position = p,
                            name = child.name,
                            archetype = GetArchetypeFromName(child.name)
                        });
                    }
                }
            }
        }

        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen != null && worldGen.MapTiles != null)
        {
            foreach (var pair in worldGen.MapTiles)
            {
                GameObject tile = pair.Value;
                if (tile == null) continue;

                Vector3 tPos = tile.transform.position;
                float tileDist2 = (tPos.x - checkCenter.x) * (tPos.x - checkCenter.x) + (tPos.z - checkCenter.z) * (tPos.z - checkCenter.z);
                if (tileDist2 > 4.5f) continue;

                for (int i = 0; i < tile.transform.childCount; i++)
                {
                    Transform child = tile.transform.GetChild(i);
                    if (child.name.StartsWith("Habitat_") || child.name.StartsWith("PlacedCard_"))
                    {
                        for (int j = 0; j < child.childCount; j++)
                        {
                            Transform propTransform = child.GetChild(j);
                            Vector3 propPos = propTransform.position;
                            float d2 = (propPos.x - checkCenter.x) * (propPos.x - checkCenter.x) + (propPos.z - checkCenter.z) * (propPos.z - checkCenter.z);
                            if (d2 <= sqrRadius)
                            {
                                entries.Add(new NearbyPropEntry
                                {
                                    position = propPos,
                                    name = propTransform.name,
                                    archetype = GetArchetypeFromName(propTransform.name)
                                });
                            }
                        }
                    }
                }
            }
        }

        return entries;
    }

    private List<Vector3> GetNearbyPropPositions(Vector3 checkCenter, float radius)
    {
        var entries = GetNearbyPropEntries(checkCenter, radius);
        List<Vector3> pos = new List<Vector3>(entries.Count);
        for (int i = 0; i < entries.Count; i++) pos.Add(entries[i].position);
        return pos;
    }

    /// <summary>
    /// Helper thống nhất khởi tạo prop nối ranh giới, tự động xử lý độ cắm đất tiếp giáp cỏ Y,
    /// xoay ngẫu nhiên bảo toàn rotation gốc, tắt collider và bật/tắt bóng đổ.
    /// </summary>
    private GameObject SpawnConnectorPropInstance(
        GameObject prefab,
        Vector3 spawnPos,
        CardData.HabitatPropRule rule,
        float scaleMultiplier,
        bool isMedium,
        float animDelay,
        string namePrefix)
    {
        if (prefab == null) return null;

        float randomScale = (rule != null)
            ? UnityEngine.Random.Range(rule.scaleRange.x, rule.scaleRange.y) * scaleMultiplier
            : scaleMultiplier;

        float customEmbed = (rule != null) ? rule.customGroundEmbed : 0f;
        float yOffset = HexHabitatSpawner.GetPrefabBottomYOffset(prefab, customEmbed) * randomScale;
        spawnPos.y += yOffset;

        Quaternion rot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * prefab.transform.rotation;

        GameObject propObj = Instantiate(prefab, spawnPos, rot, ConnectionsContainer);
        propObj.name = $"{namePrefix}_{prefab.name}";
        propObj.transform.localScale = Vector3.zero;

        Collider[] colliders = propObj.GetComponentsInChildren<Collider>();
        for (int c = 0; c < colliders.Length; c++) colliders[c].enabled = false;

        Renderer[] renderers = propObj.GetComponentsInChildren<Renderer>();
        for (int r = 0; r < renderers.Length; r++)
        {
            renderers[r].shadowCastingMode = isMedium ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        Vector3 targetScale = prefab.transform.localScale * randomScale;
        StartCoroutine(AnimatePropPopIn(propObj.transform, targetScale, animDelay));
        return propObj;
    }

    /// <summary>
    /// Lấy Prefab mầm cỏ nhỏ (MicroGrassTuft) cho viền nối.
    /// Tự động fallback sang HexFilledGroundSpawner hoặc LoadAsset từ thư mục 05_FilledGround.
    /// </summary>
    public GameObject GetMicroGrassPrefab(CardData cardData = null)
    {
        if (seamMicroGrassPrefab != null) return seamMicroGrassPrefab;

        if (HexFilledGroundSpawner.Instance != null && HexFilledGroundSpawner.Instance.MicroGrassTuftPrefab != null)
        {
            seamMicroGrassPrefab = HexFilledGroundSpawner.Instance.MicroGrassTuftPrefab;
            return seamMicroGrassPrefab;
        }

        if (cardData != null && cardData.habitatProps != null)
        {
            foreach (var rule in cardData.habitatProps)
            {
                if (rule == null || rule.prefabs == null) continue;
                foreach (var p in rule.prefabs)
                {
                    if (p != null && p.name.IndexOf("MicroGrass", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        seamMicroGrassPrefab = p;
                        return seamMicroGrassPrefab;
                    }
                }
            }
        }

#if UNITY_EDITOR
        if (seamMicroGrassPrefab == null)
        {
            seamMicroGrassPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/VinhWorkSpace/Prefabs/Leafwood/05_FilledGround/MicroGrassTuft.prefab"
            );
        }
#endif

        return seamMicroGrassPrefab;
    }

    /// <summary>
    /// Kiểm tra xem Biome này có phù hợp để sinh mầm cỏ nhỏ ở viền nối không
    /// </summary>
    private bool ShouldSpawnSeamGrassForHabitat(CardData cardData)
    {
        if (!spawnSeamMicroGrass) return false;
        if (cardData == null) return false;

        if (HexFilledGroundSpawner.Instance != null && HexFilledGroundSpawner.Instance.HasFilledGround(cardData))
            return true;

        string family = GetHabitatFamily(cardData.cardName);
        if (string.Equals(family, "Leafwood", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(family, "Bloomfield", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (cardData.habitatProps != null)
        {
            foreach (var rule in cardData.habitatProps)
            {
                if (rule != null && !string.IsNullOrEmpty(rule.groupName))
                {
                    if (rule.groupName.IndexOf("FilledGround", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        rule.groupName.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Sinh một dải mầm cỏ nhỏ (MicroGrassTuft) vắt ngang viền nối giữa 2 ô cùng loại.
    /// Giúp che rãnh nối và liên kết thảm cỏ của 2 ô liền mạch như một cánh rừng tự nhiên.
    /// </summary>
    private void SpawnBorderMicroGrass(
        Vector3 midPoint, Vector3 u, Vector3 v, float halfEdge, float surfaceY, CardData habitatData)
    {
        if (!ShouldSpawnSeamGrassForHabitat(habitatData)) return;

        GameObject sproutPrefab = GetMicroGrassPrefab(habitatData);
        if (sproutPrefab == null) return;

        int count = UnityEngine.Random.Range(seamMicroGrassCountRange.x, seamMicroGrassCountRange.y + 1);
        if (count <= 0) return;

        // Đoạn phẳng trên đỉnh viền nối: ~0.42m mỗi bên (tổng chiều dài ~0.85m)
        float usableSpan = halfEdge * 0.75f;
        float step = (usableSpan * 2f) / count;
        float startV = -usableSpan + step * 0.5f;

        for (int i = 0; i < count; i++)
        {
            // Vị trí dọc theo viền tiếp giáp (v)
            float vOffset = startV + i * step + UnityEngine.Random.Range(-step * 0.35f, step * 0.35f);

            // Vị trí ngang viền tiếp giáp (u) - đan xen nhẹ sang 2 bên sườn ô
            float uOffset = UnityEngine.Random.Range(-0.065f, 0.065f);

            Vector3 spawnPos = midPoint + v * vOffset + u * uOffset;
            spawnPos.y = surfaceY;

            // Tính scale ngẫu nhiên
            float scale = UnityEngine.Random.Range(seamMicroGrassScaleRange.x, seamMicroGrassScaleRange.y);

            // Cắm sâu Y chuẩn xác dựa vào bounds của prefab
            float yOffset = HexHabitatSpawner.GetPrefabBottomYOffset(sproutPrefab, seamMicroGrassGroundEmbed) * scale;
            spawnPos.y += yOffset;

            // Xoay ngẫu nhiên 360 độ và nghiêng nhẹ tự nhiên
            Quaternion rot = Quaternion.Euler(
                UnityEngine.Random.Range(-3f, 3f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(-3f, 3f)
            ) * sproutPrefab.transform.rotation;

            GameObject sproutObj = Instantiate(sproutPrefab, spawnPos, rot, ConnectionsContainer);
            sproutObj.name = $"SeamSprout_{sproutPrefab.name}_{i}";
            sproutObj.transform.localScale = Vector3.zero;

            // Tắt collider để không cản trở raycast / chuột
            Collider[] colliders = sproutObj.GetComponentsInChildren<Collider>();
            for (int c = 0; c < colliders.Length; c++) colliders[c].enabled = false;

            // Tắt shadow casting mode để tối ưu hiệu năng
            Renderer[] renderers = sproutObj.GetComponentsInChildren<Renderer>();
            for (int r = 0; r < renderers.Length; r++)
            {
                renderers[r].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[r].receiveShadows = true;
            }

            // Diễn hoạt nảy mầm theo làn sóng nhẹ
            float delay = 0.04f + (float)i / count * 0.16f;
            Vector3 targetScale = sproutPrefab.transform.localScale * scale;
            StartCoroutine(AnimatePropPopIn(sproutObj.transform, targetScale, delay));
        }
    }

    /// <summary>
    /// Sinh cụm mầm cỏ nhỏ xoay quanh nắp ngã ba 3 ô (Corner Filler),
    /// tạo điểm xuyết mầm xanh ở tâm giao thoa giữa 3 khối lục giác.
    /// </summary>
    private void SpawnCornerMicroGrass(Vector3 center, float surfaceY, CardData habitatData)
    {
        if (!ShouldSpawnSeamGrassForHabitat(habitatData)) return;

        GameObject sproutPrefab = GetMicroGrassPrefab(habitatData);
        if (sproutPrefab == null) return;

        int count = UnityEngine.Random.Range(cornerMicroGrassCountRange.x, cornerMicroGrassCountRange.y + 1);
        if (count <= 0) return;

        float angleStep = 360f / count;

        for (int i = 0; i < count; i++)
        {
            float baseAngle = i * angleStep + UnityEngine.Random.Range(-15f, 15f);
            float rad = baseAngle * Mathf.Deg2Rad;
            float r = UnityEngine.Random.Range(0.04f, cornerFillerRadius * 1.15f);

            Vector3 spawnPos = center + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * r;
            spawnPos.y = surfaceY;

            float scale = UnityEngine.Random.Range(seamMicroGrassScaleRange.x, seamMicroGrassScaleRange.y);
            float yOffset = HexHabitatSpawner.GetPrefabBottomYOffset(sproutPrefab, seamMicroGrassGroundEmbed) * scale;
            spawnPos.y += yOffset;

            Quaternion rot = Quaternion.Euler(
                UnityEngine.Random.Range(-3f, 3f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(-3f, 3f)
            ) * sproutPrefab.transform.rotation;

            GameObject sproutObj = Instantiate(sproutPrefab, spawnPos, rot, ConnectionsContainer);
            sproutObj.name = $"CornerSprout_{sproutPrefab.name}_{i}";
            sproutObj.transform.localScale = Vector3.zero;

            Collider[] colliders = sproutObj.GetComponentsInChildren<Collider>();
            for (int c = 0; c < colliders.Length; c++) colliders[c].enabled = false;

            Renderer[] renderers = sproutObj.GetComponentsInChildren<Renderer>();
            for (int rend = 0; rend < renderers.Length; rend++)
            {
                renderers[rend].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[rend].receiveShadows = true;
            }

            float delay = 0.06f + (float)i / count * 0.16f;
            Vector3 targetScale = sproutPrefab.transform.localScale * scale;
            StartCoroutine(AnimatePropPopIn(sproutObj.transform, targetScale, delay));
        }
    }

    /// <summary>
    /// Sinh CỤM THẢM THỰC VẬT LIÊN KẾT ĐA DẠNG (Diversity-Driven Ecological Vignette):
    /// - Không bỏ hoàn toàn thân gỗ/khúc gỗ, nhưng ưu tiên các nhóm thực vật xanh, hoa và đá rêu
    /// - Thuật toán chấm điểm đa dạng (Dynamic Diversity Scoring): luân chuyển chủng loại, tuyệt đối không lặp lại prefab trên cùng đường biên
    /// - Phân tích tần suất xung quanh: Tự động kích hoạt các chủng loại đang hiếm trong cụm
    /// </summary>
    private void SpawnEdgeConnectorProps(GameObject tileA, GameObject tileB, CardData cardData)
    {
        if (cardData == null || !cardData.HasHabitatProps()) return;

        Vector3 posA = tileA.transform.position;
        Vector3 posB = tileB.transform.position;

        Vector3 dirXZ = new Vector3(posB.x - posA.x, 0f, posB.z - posA.z);
        Vector3 u = dirXZ.normalized;
        Vector3 v = new Vector3(-u.z, 0f, u.x);
        Vector3 midPoint = (posA + posB) * 0.5f;

        var (topYA, _, _) = GetTileHeightLevels(tileA);
        var (topYB, _, _) = GetTileHeightLevels(tileB);
        if (Mathf.Abs(topYA - topYB) > 0.15f) return;

        float topY = (topYA + topYB) * 0.5f;

        // Quét tất cả các prop đã có trên 2 đảo và xung quanh kèm theo tên và chủng loại để phân tích đa dạng
        List<NearbyPropEntry> nearbyEntries = GetNearbyPropEntries(midPoint, 1.25f);

        bool hasSpawnedMedium = false;
        Vector3 medSpawnPos = midPoint;
        float chosenMedV = 0f;
        GameObject spawnedMedPrefab = null;
        PropArchetype spawnedMedArchetype = PropArchetype.FernBush;

        // 1. THỬ SINH 1 PROP TRUNG ƯU TIÊN ĐỘ ĐA DẠNG VẮT QUA ĐƯỜNG BIÊN
        bool tryMedium = (UnityEngine.Random.value < mediumPropChance);
        if (tryMedium)
        {
            GameObject medPrefab = GetMediumPropPrefab(cardData, nearbyEntries, midPoint, out CardData.HabitatPropRule medRule, out PropArchetype medArch);
            if (medPrefab != null)
            {
                float[] candidateVOffsets = new float[] { -0.18f, 0.18f, -0.09f, 0.09f, 0f, -0.24f, 0.24f };
                float bestDist = -1f;
                Vector3 bestPos = midPoint;
                float bestV = 0f;

                for (int i = 0; i < candidateVOffsets.Length; i++)
                {
                    float candV = candidateVOffsets[i] + UnityEngine.Random.Range(-0.03f, 0.03f);
                    float candU = UnityEngine.Random.Range(-0.06f, 0.06f);
                    Vector3 testPos = midPoint + v * candV + u * candU;
                    testPos.y = topY;

                    float minDist = float.MaxValue;
                    for (int p = 0; p < nearbyEntries.Count; p++)
                    {
                        float d = Vector2.Distance(new Vector2(testPos.x, testPos.z), new Vector2(nearbyEntries[p].position.x, nearbyEntries[p].position.z));
                        if (d < minDist) minDist = d;
                    }

                    if (minDist > bestDist)
                    {
                        bestDist = minDist;
                        bestPos = testPos;
                        bestV = candV;
                    }
                }

                if (bestDist >= minMediumClearance)
                {
                    SpawnConnectorPropInstance(medPrefab, bestPos, medRule, 0.90f, true, 0.03f, "ConnectorMedProp");
                    medSpawnPos = bestPos;
                    chosenMedV = bestV;
                    hasSpawnedMedium = true;
                    spawnedMedPrefab = medPrefab;
                    spawnedMedArchetype = medArch;
                    nearbyEntries.Add(new NearbyPropEntry { position = bestPos, name = medPrefab.name, archetype = medArch });
                }
            }
        }

        // 2. SINH CÁC PROP NHỎ ĐA DẠNG (Hoa, Nấm, Dương xỉ, Đá rêu, Thân gỗ điểm xuyết, Cỏ)
        var eligibleSmall = GetEligibleSmallPropsList(cardData);
        if (eligibleSmall.Count == 0) return;

        int smallCount = UnityEngine.Random.Range(propsPerEdgeRange.x, propsPerEdgeRange.y + 1);
        HashSet<GameObject> usedPrefabsOnEdge = new HashSet<GameObject>();
        HashSet<PropArchetype> usedArchetypesOnEdge = new HashSet<PropArchetype>();

        if (hasSpawnedMedium && spawnedMedPrefab != null)
        {
            usedPrefabsOnEdge.Add(spawnedMedPrefab);
            usedArchetypesOnEdge.Add(spawnedMedArchetype);
        }

        for (int i = 0; i < smallCount; i++)
        {
            // THUẬT TOÁN ĐA DẠNG HÓA VƯỢT TRỘI CHO CÁC PROP NHỎ:
            // 1. Tuyệt đối không lặp lại Prefab đã dùng trên cùng một đường nối
            // 2. Luân chuyển chủng loại (Archetype) để không trùng loại cũ chừng nào còn chủng loại khác
            // 3. Đánh giá mật độ xung quanh: Ưu tiên tối đa các chủng loại đang hiếm trong cụm
            List<(GameObject prefab, CardData.HabitatPropRule rule, PropArchetype arch, float score)> scoredCandidates =
                new List<(GameObject, CardData.HabitatPropRule, PropArchetype, float)>();

            for (int c = 0; c < eligibleSmall.Count; c++)
            {
                var item = eligibleSmall[c];
                if (usedPrefabsOnEdge.Contains(item.prefab)) continue;

                float baseScore;
                switch (item.archetype)
                {
                    case PropArchetype.Flower: baseScore = 28f; break;
                    case PropArchetype.Mushroom: baseScore = 26f; break;
                    case PropArchetype.FernBush: baseScore = 24f; break;
                    case PropArchetype.RockAccent: baseScore = 22f; break;
                    case PropArchetype.Grass: baseScore = 20f; break;
                    case PropArchetype.WoodAccent: baseScore = 14f; break; // Không bỏ, cho phép xuất hiện làm điểm xuyết
                    default: baseScore = 20f; break;
                }

                // Nếu chủng loại này ĐÃ xuất hiện trên cạnh nối này -> phạt nặng trọng số để ép luân chuyển sang chủng loại khác
                if (usedArchetypesOnEdge.Contains(item.archetype))
                {
                    baseScore *= 0.05f;
                }

                // Đa dạng hóa theo mật độ xung quanh
                int archCount = CountArchetypeNearby(nearbyEntries, item.archetype);
                float diversityMult = 1.0f / (1.0f + archCount * 1.25f);
                if (archCount == 0) diversityMult *= 1.35f;

                float finalScore = baseScore * diversityMult;
                scoredCandidates.Add((item.prefab, item.rule, item.archetype, finalScore));
            }

            if (scoredCandidates.Count == 0)
            {
                // Fallback nếu đã hết ứng viên độc nhất
                for (int c = 0; c < eligibleSmall.Count; c++)
                {
                    var item = eligibleSmall[c];
                    scoredCandidates.Add((item.prefab, item.rule, item.archetype, 10f));
                }
            }

            // Chọn ngẫu nhiên theo phân phối trọng số đa dạng
            float totalScore = 0f;
            for (int s = 0; s < scoredCandidates.Count; s++) totalScore += scoredCandidates[s].score;

            float rVal = UnityEngine.Random.Range(0f, totalScore);
            float acc = 0f;
            var picked = scoredCandidates[scoredCandidates.Count - 1];
            for (int s = 0; s < scoredCandidates.Count; s++)
            {
                acc += scoredCandidates[s].score;
                if (rVal <= acc)
                {
                    picked = scoredCandidates[s];
                    break;
                }
            }

            GameObject smallPrefab = picked.prefab;
            CardData.HabitatPropRule smallRule = picked.rule;

            usedPrefabsOnEdge.Add(picked.prefab);
            usedArchetypesOnEdge.Add(picked.arch);

            Vector3 smallPos = midPoint;
            bool foundSpot = false;

            if (hasSpawnedMedium && i == 0)
            {
                // Prop nhỏ thứ nhất: Vệ tinh tự nhiên ôm chân Prop Trung
                for (int t = 0; t < 12; t++)
                {
                    float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float satDist = UnityEngine.Random.Range(0.12f, 0.20f);
                    Vector3 candSat = medSpawnPos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * satDist;
                    candSat.y = topY;

                    float dToMed = Vector2.Distance(new Vector2(candSat.x, candSat.z), new Vector2(medSpawnPos.x, medSpawnPos.z));
                    if (dToMed >= 0.10f)
                    {
                        smallPos = candSat;
                        foundSpot = true;
                        break;
                    }
                }
            }
            else
            {
                // Các prop nhỏ còn lại: Rải ở phía thoáng dọc theo cạnh ranh giới
                float bestDist = -1f;
                Vector3 bestCandidate = midPoint;

                for (int t = 0; t < 10; t++)
                {
                    float offsetV = (hasSpawnedMedium)
                        ? ((chosenMedV > 0f ? -1f : 1f) * UnityEngine.Random.Range(0.10f, 0.28f))
                        : UnityEngine.Random.Range(-0.25f, 0.25f);

                    float offsetU = UnityEngine.Random.Range(-0.05f, 0.05f);
                    Vector3 cand = midPoint + v * offsetV + u * offsetU;
                    cand.y = topY;

                    float minDist = float.MaxValue;
                    for (int p = 0; p < nearbyEntries.Count; p++)
                    {
                        float d = Vector2.Distance(new Vector2(cand.x, cand.z), new Vector2(nearbyEntries[p].position.x, nearbyEntries[p].position.z));
                        if (d < minDist) minDist = d;
                    }

                    if (minDist > bestDist)
                    {
                        bestDist = minDist;
                        bestCandidate = cand;
                    }
                }

                if (bestDist >= minSmallClearance)
                {
                    smallPos = bestCandidate;
                    foundSpot = true;
                }
            }

            if (foundSpot)
            {
                SpawnConnectorPropInstance(smallPrefab, smallPos, smallRule, 0.88f, false, 0.04f * (i + 1), $"ConnectorProp_{i}");
                nearbyEntries.Add(new NearbyPropEntry { position = smallPos, name = smallPrefab.name, archetype = picked.arch });
            }
        }
    }

    /// <summary>
    /// Sinh cụm sinh thái thực vật tại ngã ba khi đặt miếng thứ 3 tạo thành một cụm:
    /// - Sinh 1 cây trung (hoặc prop trung tâm) ngay tại tâm ngã ba 3 ô.
    /// - Từ cây trung, tán props nhỏ đa dạng ra 2 cạnh viền mới tạo (cạnh nối giữa ô thứ 3 với 2 ô trước).
    /// - Cạnh đầu tiên (nối 2 ô trước đó) đã được sinh ở trường hợp 2 khối liền nhau trước đó nên giữ nguyên, không can thiệp.
    /// </summary>
    private void SpawnClusterCornerProps(GameObject centerTile, GameObject tileB, GameObject tileC, CardData cardData)
    {
        if (cardData == null || !cardData.HasHabitatProps()) return;
        if (centerTile == null || tileB == null || tileC == null) return;

        Vector3 posA = centerTile.transform.position;
        Vector3 posB = tileB.transform.position;
        Vector3 posC = tileC.transform.position;

        var (topYA, _, _) = GetTileHeightLevels(centerTile);
        var (topYB, _, _) = GetTileHeightLevels(tileB);
        var (topYC, _, _) = GetTileHeightLevels(tileC);
        if (Mathf.Abs(topYA - topYB) > 0.15f || Mathf.Abs(topYA - topYC) > 0.15f) return;

        float topY = (topYA + topYB + topYC) / 3f;
        Vector3 center = (posA + posB + posC) / 3f;
        center.y = topY;

        List<NearbyPropEntry> nearbyEntries = GetNearbyPropEntries(center, 1.25f);

        // 1. SINH 1 CÂY TRUNG Ở GIỮA TÂM NGÃ BA
        GameObject centerTreePrefab = GetMediumTreeOrCenterpiecePrefab(cardData, nearbyEntries, center, out CardData.HabitatPropRule centerRule, out PropArchetype centerArch);

        if (centerTreePrefab != null)
        {
            SpawnConnectorPropInstance(centerTreePrefab, center, centerRule, 0.95f, true, 0.03f, "ClusterCenterTree");
            nearbyEntries.Add(new NearbyPropEntry { position = center, name = centerTreePrefab.name, archetype = centerArch });
        }

        // 2. TÁN PROPS RA 2 CẠNH VIỀN CÒN LẠI (Cạnh A-B và Cạnh A-C)
        var eligibleSmall = GetEligibleSmallPropsList(cardData);
        if (eligibleSmall.Count == 0) return;

        HashSet<GameObject> usedPrefabsInCluster = new HashSet<GameObject>();
        HashSet<PropArchetype> usedArchetypesInCluster = new HashSet<PropArchetype>();

        if (centerTreePrefab != null)
        {
            usedPrefabsInCluster.Add(centerTreePrefab);
            usedArchetypesInCluster.Add(centerArch);
        }

        // Định nghĩa 2 cạnh viền mới tạo:
        // Cạnh 1: từ tâm ngã ba hướng ra trung điểm A - B
        // Cạnh 2: từ tâm ngã ba hướng ra trung điểm A - C
        // (Cạnh B - C là cạnh đầu tiên đã sinh ở giai đoạn 2 khối liền nhau trước đó nên bỏ qua)
        Vector3[] edgeMidpoints = new Vector3[]
        {
            (posA + posB) * 0.5f,
            (posA + posC) * 0.5f
        };

        float delayTracker = 0.07f;

        for (int e = 0; e < edgeMidpoints.Length; e++)
        {
            Vector3 mid = edgeMidpoints[e];
            Vector3 dir = mid - center;
            dir.y = 0f;
            float edgeDist = dir.magnitude;
            if (edgeDist < 0.05f) continue;
            dir = dir.normalized;
            Vector3 perp = new Vector3(-dir.z, 0f, dir.x);

            // Mỗi cạnh viền tán ra từ 1 đến 2 prop nhỏ
            int propsOnThisEdge = UnityEngine.Random.Range(1, 3);

            for (int pIdx = 0; pIdx < propsOnThisEdge; pIdx++)
            {
                var picked = PickDiverseSmallProp(eligibleSmall, usedPrefabsInCluster, usedArchetypesInCluster, nearbyEntries);
                if (picked.prefab == null) continue;

                usedPrefabsInCluster.Add(picked.prefab);
                usedArchetypesInCluster.Add(picked.arch);

                // Vị trí tỏa ra dọc theo cạnh viền:
                // Prop thứ nhất ở gần chân cây trung (0.24m - 0.32m)
                // Prop thứ hai ở xa hơn dọc theo mép (0.42m - 0.52m)
                float dFromCenter = (pIdx == 0)
                    ? UnityEngine.Random.Range(0.24f, 0.32f)
                    : UnityEngine.Random.Range(0.42f, 0.52f);

                float lateralJitter = UnityEngine.Random.Range(-0.045f, 0.045f);
                Vector3 spawnPos = center + dir * dFromCenter + perp * lateralJitter;
                spawnPos.y = topY;

                // Kiểm tra khoảng cách vật lý với các prop đã có
                float minDist = float.MaxValue;
                for (int n = 0; n < nearbyEntries.Count; n++)
                {
                    float d = Vector2.Distance(
                        new Vector2(spawnPos.x, spawnPos.z),
                        new Vector2(nearbyEntries[n].position.x, nearbyEntries[n].position.z)
                    );
                    if (d < minDist) minDist = d;
                }

                if (minDist >= 0.12f)
                {
                    SpawnConnectorPropInstance(picked.prefab, spawnPos, picked.rule, 0.88f, false, delayTracker, $"ClusterEdgeProp_{e}_{pIdx}");
                    nearbyEntries.Add(new NearbyPropEntry { position = spawnPos, name = picked.prefab.name, archetype = picked.arch });
                    delayTracker += 0.04f;
                }
            }
        }
    }

    private void SpawnCornerConnectorProp(GameObject tileA, GameObject tileB, GameObject tileC, CardData cardData)
    {
        SpawnClusterCornerProps(tileA, tileB, tileC, cardData);
    }

    /// <summary>
    /// Tìm Cây Thân Gỗ Tầm Trung (Secondary/Supporting Tree) trong CardData để làm tâm điểm cho cụm 3 ô.
    /// Nếu Biome không có cây (như cánh đồng hoa Bloomfield), fallback sang Prop Trung (bụi cây lớn, tảng đá rêu).
    /// </summary>
    private GameObject GetMediumTreeOrCenterpiecePrefab(
        CardData cardData,
        List<NearbyPropEntry> nearbyEntries,
        Vector3 spawnTarget,
        out CardData.HabitatPropRule matchedRule,
        out PropArchetype chosenArchetype)
    {
        matchedRule = null;
        chosenArchetype = PropArchetype.Tree;

        if (cardData == null || cardData.habitatProps == null || cardData.habitatProps.Count == 0)
            return null;

        // 1. ƯU TIÊN 1: Tìm cây thân gỗ tầm trung (Secondary Trees / Supporting Trees) trong CardData
        List<(GameObject prefab, CardData.HabitatPropRule rule, PropArchetype arch)> treeCandidates =
            new List<(GameObject, CardData.HabitatPropRule, PropArchetype)>();

        foreach (var rule in cardData.habitatProps)
        {
            if (rule == null || rule.prefabs == null || rule.prefabs.Count == 0) continue;
            string gName = rule.groupName ?? "";

            // Bỏ qua Landmark đại thụ khổng lồ
            if (gName.IndexOf("Anchor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Peak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Colossus", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            foreach (var p in rule.prefabs)
            {
                if (p == null) continue;
                string pName = p.name;

                if (pName.IndexOf("Anchor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Peak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Micro", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                PropArchetype arch = GetPropArchetype(p, rule);
                if (arch == PropArchetype.Tree ||
                    gName.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Oak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Pine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Birch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Willow", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    treeCandidates.Add((p, rule, PropArchetype.Tree));
                }
            }
        }

        // Nếu tìm thấy cây tầm trung trong Biome:
        if (treeCandidates.Count > 0)
        {
            var bestCandidate = treeCandidates[0];
            float bestScore = -999f;

            for (int i = 0; i < treeCandidates.Count; i++)
            {
                var cand = treeCandidates[i];
                float score = 10f;
                if (nearbyEntries != null)
                {
                    for (int e = 0; e < nearbyEntries.Count; e++)
                    {
                        if (nearbyEntries[e].name.IndexOf(cand.prefab.name, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            score -= 6f;
                        }
                    }
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCandidate = cand;
                }
            }

            matchedRule = bestCandidate.rule;
            chosenArchetype = PropArchetype.Tree;
            return bestCandidate.prefab;
        }

        // 2. ƯU TIÊN 2: Nếu Biome không có cây, fallback sang Prop Trung (bụi cây lớn, tảng đá rêu)
        GameObject medPrefab = GetMediumPropPrefab(cardData, nearbyEntries, spawnTarget, out matchedRule, out chosenArchetype);
        if (medPrefab != null)
        {
            return medPrefab;
        }

        // 3. Fallback sang prop nhỏ bất kỳ để không bao giờ bị hổng
        return GetSmallPropPrefab(cardData, out matchedRule);
    }

    /// <summary>
    /// Chọn Prop nhỏ đa dạng dựa trên Inverse-Frequency và luân chuyển Archetype
    /// </summary>
    private (GameObject prefab, CardData.HabitatPropRule rule, PropArchetype arch) PickDiverseSmallProp(
        List<(GameObject prefab, CardData.HabitatPropRule rule, PropArchetype archetype)> eligibleSmall,
        HashSet<GameObject> usedPrefabs,
        HashSet<PropArchetype> usedArchetypes,
        List<NearbyPropEntry> nearbyEntries)
    {
        if (eligibleSmall == null || eligibleSmall.Count == 0)
            return (null, null, PropArchetype.FernBush);

        List<(GameObject prefab, CardData.HabitatPropRule rule, PropArchetype arch, float score)> scored =
            new List<(GameObject, CardData.HabitatPropRule, PropArchetype, float)>();

        for (int c = 0; c < eligibleSmall.Count; c++)
        {
            var item = eligibleSmall[c];
            if (usedPrefabs != null && usedPrefabs.Contains(item.prefab)) continue;

            float baseScore;
            switch (item.archetype)
            {
                case PropArchetype.Flower: baseScore = 28f; break;
                case PropArchetype.Mushroom: baseScore = 26f; break;
                case PropArchetype.FernBush: baseScore = 24f; break;
                case PropArchetype.RockAccent: baseScore = 22f; break;
                case PropArchetype.Grass: baseScore = 20f; break;
                case PropArchetype.WoodAccent: baseScore = 14f; break;
                default: baseScore = 20f; break;
            }

            if (usedArchetypes != null && usedArchetypes.Contains(item.archetype))
            {
                baseScore *= 0.08f;
            }

            int archCount = CountArchetypeNearby(nearbyEntries, item.archetype);
            float diversityMult = 1.0f / (1.0f + archCount * 1.25f);
            if (archCount == 0) diversityMult *= 1.35f;

            scored.Add((item.prefab, item.rule, item.archetype, baseScore * diversityMult));
        }

        if (scored.Count == 0)
        {
            for (int c = 0; c < eligibleSmall.Count; c++)
            {
                var item = eligibleSmall[c];
                scored.Add((item.prefab, item.rule, item.archetype, 10f));
            }
        }

        float totalScore = 0f;
        for (int s = 0; s < scored.Count; s++) totalScore += scored[s].score;

        float rVal = UnityEngine.Random.Range(0f, totalScore);
        float acc = 0f;
        var picked = scored[scored.Count - 1];
        for (int s = 0; s < scored.Count; s++)
        {
            acc += scored[s].score;
            if (rVal <= acc)
            {
                picked = scored[s];
                break;
            }
        }

        return (picked.prefab, picked.rule, picked.arch);
    }

    /// <summary>
    /// Chọn Prop tầm trung theo THUẬT TOÁN ĐA DẠNG HÓA SINH THÁI:
    /// - Không bỏ hoàn toàn thân gỗ (khúc gỗ, gốc cây, cây non phụ), nhưng ưu tiên các nhóm thực vật xanh và đá rêu.
    /// - Không hard-ban: Dùng trọng số nghịch đảo tần suất (Inverse-Frequency) để tự động ưu tiên các chủng loại đang hiếm trong khu vực.
    /// - Kiểm tra cự ly vật lý: Tránh sinh 2 khúc gỗ/thân cây sát sườn (< 0.55m) nhau.
    /// </summary>
    public GameObject GetMediumPropPrefab(
        CardData cardData,
        List<NearbyPropEntry> nearbyEntries,
        Vector3 spawnTarget,
        out CardData.HabitatPropRule matchedRule,
        out PropArchetype chosenArchetype)
    {
        matchedRule = null;
        chosenArchetype = PropArchetype.FernBush;
        if (cardData == null || cardData.habitatProps == null || cardData.habitatProps.Count == 0)
            return null;

        List<(GameObject prefab, CardData.HabitatPropRule rule, PropArchetype arch, float weight)> candidates =
            new List<(GameObject, CardData.HabitatPropRule, PropArchetype, float)>();

        foreach (var rule in cardData.habitatProps)
        {
            if (rule == null || rule.prefabs == null || rule.prefabs.Count == 0) continue;
            string gName = rule.groupName ?? "";

            // Loại trừ Landmark đại thụ trung tâm
            if (gName.IndexOf("Anchor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Peak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Colossus", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            foreach (var p in rule.prefabs)
            {
                if (p == null) continue;
                string pName = p.name;
                if (pName.IndexOf("Anchor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Peak", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                // Bỏ qua các prop quá nhỏ (nhỏ thì dành cho tầng thảm hoa cỏ)
                if (pName.IndexOf("Micro", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Toadstool", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Bluebell", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Daisy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Pebble", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                PropArchetype arch = GetPropArchetype(p, rule);

                // 1. Trọng số cơ sở: Ưu tiên thực vật hoa lá, đá rêu; thân gỗ/cây vẫn có cơ hội xuất hiện tự nhiên (KHÔNG BỎ HOÀN TOÀN)
                float baseWeight;
                switch (arch)
                {
                    case PropArchetype.FernBush: baseWeight = 30f; break;
                    case PropArchetype.RockAccent: baseWeight = 26f; break;
                    case PropArchetype.Flower: baseWeight = 22f; break;
                    case PropArchetype.Grass: baseWeight = 18f; break;
                    case PropArchetype.WoodAccent: baseWeight = 15f; break; // Khúc gỗ, gốc cây điểm xuyết tự nhiên
                    case PropArchetype.Tree: baseWeight = 12f; break; // Cây phụ / cây non kết nối
                    default: baseWeight = 15f; break;
                }

                // 2. Chống đè lấn / chụm cục:
                // Nếu đúng Prefab này đã có trong cự ly gần (< 0.50m) -> bỏ qua để không lặp lại 2 prop giống hệt nhau
                bool duplicateTooClose = false;
                if (nearbyEntries != null)
                {
                    for (int e = 0; e < nearbyEntries.Count; e++)
                    {
                        float d = Vector2.Distance(
                            new Vector2(spawnTarget.x, spawnTarget.z),
                            new Vector2(nearbyEntries[e].position.x, nearbyEntries[e].position.z)
                        );

                        if (d < 0.50f && nearbyEntries[e].name.IndexOf(p.name, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            duplicateTooClose = true;
                            break;
                        }

                        // Nếu là WoodAccent hoặc Tree, không đặt sát sườn (< 0.55m) một thân gỗ/cây khác
                        if (d < 0.55f && (arch == PropArchetype.WoodAccent || arch == PropArchetype.Tree))
                        {
                            if (nearbyEntries[e].archetype == PropArchetype.WoodAccent || nearbyEntries[e].archetype == PropArchetype.Tree)
                            {
                                duplicateTooClose = true;
                                break;
                            }
                        }
                    }
                }

                if (duplicateTooClose) continue;

                // 3. THUẬT TOÁN ĐA DẠNG HÓA SINH THÁI (Dynamic Inverse-Frequency):
                // Chủng loại nào càng ít xuất hiện xung quanh thì xác suất được chọn càng cao vượt trội
                int archetypeCount = CountArchetypeNearby(nearbyEntries, arch);
                float diversityFactor = 1.0f / (1.0f + archetypeCount * 1.35f);

                // Thưởng xác suất cho chủng loại chưa từng có mặt trong bán kính lân cận
                if (archetypeCount == 0)
                {
                    diversityFactor *= 1.30f;
                }

                float finalWeight = baseWeight * diversityFactor;
                if (finalWeight > 0.01f)
                {
                    candidates.Add((p, rule, arch, finalWeight));
                }
            }
        }

        if (candidates.Count > 0)
        {
            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++) totalWeight += candidates[i].weight;

            float r = UnityEngine.Random.Range(0f, totalWeight);
            float accum = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                accum += candidates[i].weight;
                if (r <= accum)
                {
                    matchedRule = candidates[i].rule;
                    chosenArchetype = candidates[i].arch;
                    return candidates[i].prefab;
                }
            }

            matchedRule = candidates[candidates.Count - 1].rule;
            chosenArchetype = candidates[candidates.Count - 1].arch;
            return candidates[candidates.Count - 1].prefab;
        }

        return null;
    }

    /// <summary>
    /// Thu thập danh sách toàn bộ các prop nhỏ tầng thấp trong Biome, kèm theo phân loại chủng loại
    /// </summary>
    public List<(GameObject prefab, CardData.HabitatPropRule rule, PropArchetype archetype)> GetEligibleSmallPropsList(CardData cardData)
    {
        List<(GameObject prefab, CardData.HabitatPropRule rule, PropArchetype archetype)> list =
            new List<(GameObject, CardData.HabitatPropRule, PropArchetype)>();

        if (cardData == null || cardData.habitatProps == null) return list;

        foreach (var rule in cardData.habitatProps)
        {
            if (rule == null || rule.prefabs == null || rule.prefabs.Count == 0) continue;
            string gName = rule.groupName ?? "";

            if (gName.IndexOf("Anchor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Peak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Colossus", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            foreach (var p in rule.prefabs)
            {
                if (p == null) continue;
                string pName = p.name;

                // Bỏ qua các cây thân gỗ cao lớn
                if (pName.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Oak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Pine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Birch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Willow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Peak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Anchor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pName.IndexOf("Sentinel", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                PropArchetype arch = GetPropArchetype(p, rule);
                // Prop nhỏ bao gồm: FernBush, Flower, Mushroom, RockAccent, WoodAccent, Grass
                // (Thân gỗ dạng khúc gỗ/gốc cây nhỏ vẫn có thể xuất hiện điểm xuyết, không bị loại bỏ hoàn toàn)
                if (arch == PropArchetype.FernBush ||
                    arch == PropArchetype.Flower ||
                    arch == PropArchetype.Mushroom ||
                    arch == PropArchetype.RockAccent ||
                    arch == PropArchetype.WoodAccent ||
                    arch == PropArchetype.Grass)
                {
                    list.Add((p, rule, arch));
                }
            }
        }

        return list;
    }

    /// <summary>
    /// Lọc prop nhỏ ngẫu nhiên (dùng làm fallback)
    /// </summary>
    public GameObject GetSmallPropPrefab(CardData cardData, out CardData.HabitatPropRule matchedRule)
    {
        matchedRule = null;
        var list = GetEligibleSmallPropsList(cardData);
        if (list.Count > 0)
        {
            var item = list[UnityEngine.Random.Range(0, list.Count)];
            matchedRule = item.rule;
            return item.prefab;
        }
        return null;
    }

    /// <summary>
    /// Kiểm tra nếu cụm đạt chuẩn 3+ ô Habitat cùng loại thì nảy ăn mừng
    /// </summary>
    private void CheckAndCelebrateCluster(HexCoordinates centerHex, CardData centerHabitatData, HexWorldGenerator worldGen)
    {
        if (!enableCelebration) return;

        List<GameObject> clusterTiles = new List<GameObject>();
        Queue<HexCoordinates> queue = new Queue<HexCoordinates>();
        HashSet<HexCoordinates> visited = new HashSet<HexCoordinates>();

        queue.Enqueue(centerHex);
        visited.Add(centerHex);

        GameObject centerTile = worldGen.MapTiles[centerHex];
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

    /// <summary>
    /// Quét lại toàn bộ các ô Habitat trong Scene để kết nối lại
    /// </summary>
    [ContextMenu("Scan And Connect All Habitat Clusters In Scene")]
    public void ScanAndConnectExisting()
    {
        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen == null || worldGen.MapTiles == null) return;

        foreach (var pair in worldGen.MapTiles)
        {
            PlacedCard card = GetHabitatCardOnTile(pair.Value);
            if (card != null && card.cardData != null && card.cardData.HasHabitatProps())
            {
                ConnectAllNearby(pair.Key, card.cardData);
            }
        }
    }

    /// <summary>
    /// Xóa toàn bộ các kết nối khi reset bản đồ
    /// </summary>
    [ContextMenu("Clear All Connections")]
    public void ClearAllConnections()
    {
        _createdEdgeBridges.Clear();
        _createdCornerFillers.Clear();
        _celebratedClusters.Clear();

        if (_connectionsContainer != null)
        {
            if (Application.isPlaying)
                Destroy(_connectionsContainer.gameObject);
            else
                DestroyImmediate(_connectionsContainer.gameObject);

            _connectionsContainer = null;
        }
    }

    // =========================================================================
    // HIỆU ỨNG DIỄN HOẠT (ANIMATIONS - EASEOUTBACK POP-IN & CELEBRATION BOUNCE)
    // =========================================================================

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
        if (target == null) yield break;

        if (delay > 0f) yield return new WaitForSeconds(delay);

        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (target == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float overshoot = 1f + c3 * Mathf.Pow(t - 1f, 3) + c1 * Mathf.Pow(t - 1f, 2);

            target.localScale = finalScale * overshoot;
            yield return null;
        }

        if (target != null) target.localScale = finalScale;
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
