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
    [Tooltip("Tự động sinh hoa cỏ, bụi cây nhỏ dọc theo đường nối giữa các ô trong cụm")]
    [SerializeField] private bool spawnConnectorProps = true;

    [Tooltip("Số lượng prop nhỏ sinh dọc theo mỗi cạnh nối")]
    [SerializeField] private Vector2Int propsPerEdgeRange = new Vector2Int(1, 2);

    [Tooltip("Sinh hoa cỏ tại ngã ba trung tâm nơi 3 ô gặp nhau")]
    [SerializeField] private bool spawnCornerProps = true;

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

        // 1. Quét 6 hướng xung quanh centerHex
        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates neighborHex = centerHex.GetNeighbor(dir);

            if (worldGen.MapTiles.TryGetValue(neighborHex, out GameObject neighborTile) && neighborTile != null)
            {
                // Kiểm tra xem ô láng giềng có thẻ Habitat cùng loại không
                PlacedCard neighborCard = GetHabitatCardOnTile(neighborTile);

                if (neighborCard != null && AreHabitatsMatching(centerHabitatData, neighborCard.cardData))
                {
                    string edgeKey = GetEdgeKey(centerHex, neighborHex);
                    if (!_createdEdgeBridges.Contains(edgeKey))
                    {
                        _createdEdgeBridges.Add(edgeKey);
                        CreateEdgeSeamBridge(centerHex, centerTile, neighborHex, neighborTile, edgeKey, centerHabitatData, worldGen);

                        if (spawnConnectorProps)
                        {
                            SpawnEdgeConnectorProps(centerTile, neighborTile, centerHabitatData);
                        }
                    }
                }
            }
        }

        // 2. Quét các ngã ba 3 ô lục giác kề nhau ĐỀU ĐÃ ĐẶT CÙNG THẺ HABITAT
        ScanCornerJunctionsAround(centerHex, centerTile, centerHabitatData, worldGen);

        // 3. Kiểm tra ăn mừng cụm hoàn thành (>= 3 ô Habitat cùng loại kề nhau)
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

        // 1. Ưu tiên 1: Đọc trực tiếp tọa độ UV từ đỉnh cao nhất của Mesh khối lục giác
        MeshFilter tileMf = GetTileMeshFilter(tileObj);
        if (tileMf != null && tileMf.sharedMesh != null)
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
    /// Sinh hoa cỏ, bụi cây nhỏ dọc theo cạnh tiếp giáp giữa 2 ô Habitat
    /// </summary>
    private void SpawnEdgeConnectorProps(GameObject tileA, GameObject tileB, CardData cardData)
    {
        if (cardData == null || !cardData.HasHabitatProps()) return;

        GameObject propPrefab = GetSmallPropPrefab(cardData, out CardData.HabitatPropRule matchedRule);
        if (propPrefab == null) return;

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

        int count = UnityEngine.Random.Range(propsPerEdgeRange.x, propsPerEdgeRange.y + 1);

        for (int i = 0; i < count; i++)
        {
            float offsetAlongEdge = (count == 1)
                ? UnityEngine.Random.Range(-0.18f, 0.18f)
                : ((i == 0 ? -0.20f : 0.20f) + UnityEngine.Random.Range(-0.05f, 0.05f));

            float offsetAcrossEdge = UnityEngine.Random.Range(-0.03f, 0.03f);

            float randomScale = matchedRule != null
                ? UnityEngine.Random.Range(matchedRule.scaleRange.x, matchedRule.scaleRange.y) * 0.90f
                : UnityEngine.Random.Range(0.75f, 1.05f);

            // Tự động tính độ cắm đất chân đế để ôm sát mặt cỏ
            float customEmbed = matchedRule != null ? matchedRule.customGroundEmbed : 0f;
            float yOffset = HexHabitatSpawner.GetPrefabBottomYOffset(propPrefab, customEmbed) * randomScale;

            Vector3 spawnPos = midPoint + v * offsetAlongEdge + u * offsetAcrossEdge;
            spawnPos.y = topY + yOffset;

            // BẮT BUỘC nhân với propPrefab.transform.rotation để không bị lật góc xoay gốc của model 3D
            Quaternion rot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * propPrefab.transform.rotation;

            GameObject propObj = Instantiate(propPrefab, spawnPos, rot, ConnectionsContainer);
            propObj.name = $"ConnectorProp_{propPrefab.name}_{i}";
            propObj.transform.localScale = Vector3.zero;

            Collider[] colliders = propObj.GetComponentsInChildren<Collider>();
            for (int c = 0; c < colliders.Length; c++) colliders[c].enabled = false;

            Renderer[] renderers = propObj.GetComponentsInChildren<Renderer>();
            for (int r = 0; r < renderers.Length; r++) renderers[r].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Vector3 targetScale = propPrefab.transform.localScale * randomScale;
            StartCoroutine(AnimatePropPopIn(propObj.transform, targetScale, 0.04f * i));
        }
    }

    /// <summary>
    /// Sinh hoa cỏ hoặc bụi nhỏ ngay tâm ngã ba nơi 3 ô Habitat gặp nhau
    /// </summary>
    private void SpawnCornerConnectorProp(GameObject tileA, GameObject tileB, GameObject tileC, CardData cardData)
    {
        if (cardData == null || !cardData.HasHabitatProps()) return;

        GameObject propPrefab = GetSmallPropPrefab(cardData, out CardData.HabitatPropRule matchedRule);
        if (propPrefab == null) return;

        Vector3 center = (tileA.transform.position + tileB.transform.position + tileC.transform.position) / 3f;
        var (topYA, _, _) = GetTileHeightLevels(tileA);
        var (topYB, _, _) = GetTileHeightLevels(tileB);
        var (topYC, _, _) = GetTileHeightLevels(tileC);
        if (Mathf.Abs(topYA - topYB) > 0.15f || Mathf.Abs(topYA - topYC) > 0.15f) return;

        float topY = (topYA + topYB + topYC) / 3f;

        float randomScale = matchedRule != null
            ? UnityEngine.Random.Range(matchedRule.scaleRange.x, matchedRule.scaleRange.y) * 0.95f
            : UnityEngine.Random.Range(0.80f, 1.10f);

        float customEmbed = matchedRule != null ? matchedRule.customGroundEmbed : 0f;
        float yOffset = HexHabitatSpawner.GetPrefabBottomYOffset(propPrefab, customEmbed) * randomScale;
        center.y = topY + yOffset;

        // BẮT BUỘC nhân với propPrefab.transform.rotation
        Quaternion rot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * propPrefab.transform.rotation;

        GameObject propObj = Instantiate(propPrefab, center, rot, ConnectionsContainer);
        propObj.name = $"CornerProp_{propPrefab.name}";
        propObj.transform.localScale = Vector3.zero;

        Collider[] colliders = propObj.GetComponentsInChildren<Collider>();
        for (int c = 0; c < colliders.Length; c++) colliders[c].enabled = false;

        Renderer[] renderers = propObj.GetComponentsInChildren<Renderer>();
        for (int r = 0; r < renderers.Length; r++) renderers[r].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Vector3 targetScale = propPrefab.transform.localScale * randomScale;
        StartCoroutine(AnimatePropPopIn(propObj.transform, targetScale, 0.08f));
    }

    /// <summary>
    /// Lọc danh sách thảm thực vật tầng thấp (hoa, cỏ, dương xỉ, nấm, sỏi, khúc gỗ mục, hoa thạch nam),
    /// LOẠI TRỪ TUYỆT ĐỐI các loại cây to, cây cổ thụ và đỉnh núi lớn.
    /// Random công bằng giữa TẤT CẢ các loại prop nhỏ thuộc Biome đó.
    /// </summary>
    private GameObject GetSmallPropPrefab(CardData cardData, out CardData.HabitatPropRule matchedRule)
    {
        matchedRule = null;
        if (cardData == null || cardData.habitatProps == null || cardData.habitatProps.Count == 0)
            return null;

        List<(GameObject prefab, CardData.HabitatPropRule rule)> eligibleSmallProps = new List<(GameObject, CardData.HabitatPropRule)>();

        // 1. Quét qua tất cả rule của Biome để thu thập toàn bộ các prop tầng thấp
        foreach (var rule in cardData.habitatProps)
        {
            if (rule == null || rule.prefabs == null || rule.prefabs.Count == 0) continue;

            string gName = rule.groupName ?? "";

            // LOẠI TRỪ 100% CÂY TO VÀ ĐỈNH NÚI
            if (gName.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Birch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Oak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Pine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Cedar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Anchor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Sentinel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Peak", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            // Kiểm tra các từ khóa thảm thực vật tầng thấp
            bool isUndergrowth =
                gName.IndexOf("Floor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Fern", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Detail", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Flower", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Daisy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Clover", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Lavender", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Scatter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Bush", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Shrub", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Heather", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Tussock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Log", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Prairie", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Pebble", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Boulder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Rock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Carpet", StringComparison.OrdinalIgnoreCase) >= 0;

            // Nếu khớp từ khóa tầng thấp, hoặc kích thước prop nhỏ/thấp
            if (isUndergrowth || rule.scaleRange.y <= 1.25f)
            {
                foreach (var p in rule.prefabs)
                {
                    if (p != null)
                    {
                        // Kiểm tra thêm tên prefab để chắc chắn không lọt cây to/núi
                        string pName = p.name;
                        if (pName.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            pName.IndexOf("Oak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            pName.IndexOf("Pine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            pName.IndexOf("Birch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            pName.IndexOf("Peak", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            continue;
                        }

                        eligibleSmallProps.Add((p, rule));
                    }
                }
            }
        }

        // 2. Nếu tìm được danh sách prop tầng thấp hợp lệ, RANDOM đều trong danh sách này!
        if (eligibleSmallProps.Count > 0)
        {
            int randIdx = UnityEngine.Random.Range(0, eligibleSmallProps.Count);
            matchedRule = eligibleSmallProps[randIdx].rule;
            return eligibleSmallProps[randIdx].prefab;
        }

        // 3. Fallback: Nếu không tìm thấy theo tên, chọn rule có kích thước nhỏ nhất trong Biome (trừ cây to/núi to)
        foreach (var rule in cardData.habitatProps)
        {
            if (rule == null || rule.prefabs == null || rule.prefabs.Count == 0) continue;
            string gName = rule.groupName ?? "";
            if (gName.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Oak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Pine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Birch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Peak", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            matchedRule = rule;
            return rule.prefabs[UnityEngine.Random.Range(0, rule.prefabs.Count)];
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
