using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Đại diện cho một đoạn suối nằm trên một ô lục giác.
/// Quản lý mô hình 3D lòng mương đá, hang grotto, hồ đọng và máng thác nước vượt tầng.
/// Toàn bộ mặt nước cuộn sóng do RiverNetworkManager quản lý bằng Unity Splines liên tục từ nguồn.
/// </summary>
public class RiverTileSegment : MonoBehaviour
{
    [Header("Coordinate & State")]
    public HexCoordinates placedHex;
    public bool isOrigin = false; // true = Đầu nguồn nước (luôn giữ mô hình SpringHead)
    public int flowStep = 0; // 0 = Đầu nguồn trên đỉnh núi cao nhất
    public RiverPieceType pieceType = RiverPieceType.PondBasin;
    public float currentRotationY = 0f;
    public int connectedPortsMask = 0;

    [Header("3D Bed & Accent Elements")]
    [SerializeField] private GameObject visualInstance;

    private readonly Dictionary<int, GameObject> _waterfallInstances = new Dictionary<int, GameObject>();
    private readonly HashSet<int> _doubleTierWaterfalls = new HashSet<int>();
    private readonly List<List<RiverWaypoint>> _cachedPaths = new List<List<RiverWaypoint>>();

    public GameObject VisualInstance => visualInstance;
    public IReadOnlyList<List<RiverWaypoint>> CachedPaths => _cachedPaths;

    /// <summary>
    /// Áp dụng giải pháp hình học (loại miếng, góc xoay) cho ô suối này.
    /// Sinh mô hình lòng mương đá và máng thác nước vượt tầng nếu chảy qua vách núi giật cấp.
    /// </summary>
    public void ApplySolution(RiverTileSolution solution, RiverConfig config, Vector3 baseWorldPos, HexWorldGenerator worldGen = null)
    {
        pieceType = solution.pieceType;
        currentRotationY = solution.rotationY;
        connectedPortsMask = solution.connectedPortsMask;

        float globalOffset = config != null ? config.globalRotationOffset : 0f;
        Vector3 scale = config != null ? config.modelScale : Vector3.one;

        transform.position = baseWorldPos;

        // 1. Sinh mô hình 3D lòng mương đá / hang grotto / hồ nước từ Prefab
        ApplyPrefabRiverbed(solution, config, baseWorldPos, globalOffset, scale);

        // 2. Tự động kiểm tra và sinh máng đá Thác nước vượt tầng nếu giật cấp địa hình
        UpdateWaterfalls(config, scale, worldGen);

        // 3. Cập nhật đường corridor để RiverPropHarmonizer dọn cây cản dòng nước
        UpdateCorridorPath(config);

        // 4. Tự động sinh các đoạn prefab mương nối (straightPrefab) lấp kín các khoảng hở giữa các ô
        UpdateConnectingTrenches(config, baseWorldPos, worldGen);
    }

    /// <summary>
    /// Sinh và căn chỉnh mô hình 3D lòng mương đá phù hợp với ô lục giác
    /// </summary>
    private void ApplyPrefabRiverbed(
        RiverTileSolution solution, 
        RiverConfig config, 
        Vector3 baseWorldPos, 
        float globalOffset, 
        Vector3 defaultScale)
    {
        Quaternion targetRotation = Quaternion.Euler(0f, currentRotationY + globalOffset, 0f);
        float yElev = config != null ? config.GetYOffsetForPiece(pieceType) : 0.02f;
        Vector3 finalPos = baseWorldPos + new Vector3(0f, yElev, 0f);

        transform.position = finalPos;
        transform.rotation = targetRotation;

        GameObject prefab = config != null ? config.GetPrefabForPiece(pieceType) : null;
        if (prefab == null && pieceType != RiverPieceType.Straight && pieceType != RiverPieceType.Curve120)
        {
            Debug.LogError($"[RiverTileSegment] Không tìm thấy prefab cho pieceType: {pieceType} trong RiverConfig!");
            return;
        }

        // Suối thẳng Straight và khúc cua Curve120 rải các đoạn mương ghép qua UpdateConnectingTrenches
        if (pieceType == RiverPieceType.Straight || pieceType == RiverPieceType.Curve120)
        {
            if (visualInstance != null)
            {
                if (Application.isPlaying) Destroy(visualInstance);
                else DestroyImmediate(visualInstance);
                visualInstance = null;
            }
            return;
        }

        if (visualInstance == null || visualInstance.name != prefab.name)
        {
            if (visualInstance != null) Destroy(visualInstance);
            visualInstance = Instantiate(prefab, transform);
            visualInstance.name = prefab.name;
        }

        float pieceRotOffset = config != null ? config.GetRotationOffsetForPiece(pieceType) : 0f;
        Vector3 piecePosOffset = Vector3.zero;
        if (config != null)
        {
            if (pieceType == RiverPieceType.PondBasin) piecePosOffset = config.pondOffset;
            else if (pieceType == RiverPieceType.SpringHead) piecePosOffset = config.springHeadOffset;
            else if (pieceType == RiverPieceType.Curve60) piecePosOffset = config.curve60Offset;
        }
        visualInstance.transform.localPosition = piecePosOffset;
        visualInstance.transform.localRotation = Quaternion.Euler(0f, pieceRotOffset, 0f) * prefab.transform.localRotation;
        Vector3 pieceScale = config != null ? config.GetScaleForPiece(pieceType) : defaultScale;
        visualInstance.transform.localScale = Vector3.Scale(prefab.transform.localScale, pieceScale);
        visualInstance.SetActive(true);
    }

    /// <summary>
    /// Tự động sinh máng đá Thác nước (Waterfall Ramp) ở các mép dốc xuống hạ lưu khi giật cấp tầng địa hình (>0.45m).
    /// Khi vượt 2 tầng (Cao -> Thấp trực tiếp, chênh lệch >= 1.2m), tự động spawn thêm máng thác thứ 2 xếp chồng lên trên.
    /// </summary>
    private void UpdateWaterfalls(RiverConfig config, Vector3 scale, HexWorldGenerator worldGen)
    {
        if (config == null || config.waterfallPrefab == null || worldGen == null || worldGen.MapTiles == null) return;
        if (!worldGen.MapTiles.TryGetValue(placedHex, out GameObject thisTile) || thisTile == null) return;

        float topY_this = HexBiomeClusterConnector.GetTileHeightLevels(thisTile).topY;

        for (int d = 0; d < 6; d++)
        {
            bool isPortConnected = HasPort(d);
            HexCoordinates nCoords = placedHex.GetNeighbor(d);
            bool isNeighborRiver = RiverNetworkManager.Instance != null && RiverNetworkManager.Instance.HasRiver(nCoords);

            bool needsWaterfall = false;
            float topY_n = 0f;

            if (isPortConnected && isNeighborRiver && worldGen.MapTiles.TryGetValue(nCoords, out GameObject nTile) && nTile != null)
            {
                topY_n = HexBiomeClusterConnector.GetTileHeightLevels(nTile).topY;
                if (topY_this - topY_n > 0.45f)
                {
                    needsWaterfall = true;
                }
            }

            if (needsWaterfall)
            {
                float heightDiff = topY_this - topY_n;
                bool isDoubleTier = config != null && config.IsDoubleTierWaterfall(heightDiff);

                Vector3 posThis = HexMetrics.HexToWorldPosition(placedHex, 0f);
                Vector3 posN = HexMetrics.HexToWorldPosition(nCoords, 0f);
                Vector3 edgePos = (posThis + posN) * 0.5f;

                Vector3 forwardVec = (posN - posThis).normalized;
                Vector3 rightVec = Vector3.Cross(Vector3.up, forwardVec).normalized;

                Vector3 wfOffset = config != null ? config.waterfallOffset : new Vector3(0f, 0f, 0.18f);
                float wfElev = config != null ? config.waterfallYOffset : 0.477f;
                float rotOffset = config != null ? (config.waterfallRotationOffset + config.globalRotationOffset) : 0f;

                // Tinh chỉnh vị trí theo hệ trục dòng chảy:
                // - X: Dịch ngang trái/phải vuông góc dòng chảy
                // - Y: Nâng/hạ cao độ (cộng dồn wfElev + wfOffset.y)
                // - Z: Đẩy tiến ra phía hạ lưu (Z > 0: đẩy ra xa vách núi, tránh bị đâm sâu vào vách núi/lòng đất)
                Vector3 finalPos = edgePos + rightVec * wfOffset.x + forwardVec * wfOffset.z;
                finalPos.y = topY_n + wfElev + wfOffset.y;

                float angleY = Mathf.Atan2(forwardVec.x, forwardVec.z) * Mathf.Rad2Deg + 180f + rotOffset;
                Quaternion rot = Quaternion.Euler(0f, angleY, 0f) * config.waterfallPrefab.transform.localRotation;
                Vector3 wfScale = config != null ? config.GetScaleForPiece(RiverPieceType.Waterfall) : scale;

                if (!_waterfallInstances.TryGetValue(d, out GameObject wf) || wf == null)
                {
                    wf = Instantiate(config.waterfallPrefab, finalPos, rot, transform);
                    wf.name = $"Waterfall_Dir{d}";
                    _waterfallInstances[d] = wf;
                }
                else
                {
                    wf.transform.position = finalPos;
                    wf.transform.rotation = rot;
                }

                wf.transform.localScale = Vector3.Scale(config.waterfallPrefab.transform.localScale, wfScale);

                // Quản lý máng thác thứ 2 (tầng trên) khi vượt tầng (Cao -> Thấp)
                const string upperWfName = "WaterfallCascadeRamp (1)";
                Transform upperTr = wf.transform.Find(upperWfName);

                if (isDoubleTier)
                {
                    _doubleTierWaterfalls.Add(d);
                    GameObject upperObj;
                    if (upperTr == null)
                    {
                        upperObj = Instantiate(config.waterfallPrefab, wf.transform);
                        upperObj.name = upperWfName;
                    }
                    else
                    {
                        upperObj = upperTr.gameObject;
                    }

                    Vector3 upPos = config != null ? config.upperWaterfallPosition : new Vector3(0f, 0.186f, -0.668f);
                    Vector3 upRot = config != null ? config.upperWaterfallRotation : new Vector3(-29.556f, 0f, 0f);
                    Vector3 upScale = config != null ? config.upperWaterfallScale : Vector3.one;

                    upperObj.transform.localPosition = upPos;
                    upperObj.transform.localRotation = Quaternion.Euler(upRot);
                    upperObj.transform.localScale = upScale;
                }
                else
                {
                    _doubleTierWaterfalls.Remove(d);
                    if (upperTr != null)
                    {
                        if (Application.isPlaying) Destroy(upperTr.gameObject);
                        else DestroyImmediate(upperTr.gameObject);
                    }
                }
            }
            else
            {
                _doubleTierWaterfalls.Remove(d);
                if (_waterfallInstances.TryGetValue(d, out GameObject oldWf))
                {
                    if (oldWf != null)
                    {
                        if (Application.isPlaying) Destroy(oldWf);
                        else DestroyImmediate(oldWf);
                    }
                    _waterfallInstances.Remove(d);
                }
            }
        }
    }

    /// <summary>
    /// Lấy vị trí tâm cục bộ của mặt nước hồ PondBasin và điểm nối suối trong không gian RiverTileSegment
    /// </summary>
    public Vector3 GetPondCenterLocal(RiverConfig config = null)
    {
        return (config != null) ? config.pondOffset : Vector3.zero;
    }


    /// <summary>
    /// Cập nhật đường corridor để các hệ thống phụ trợ (Prop Harmonizer) nhận diện luồng suối để dọn cây
    /// </summary>
    private void UpdateCorridorPath(RiverConfig config)
    {
        _cachedPaths.Clear();

        List<int> ports = new List<int>();
        for (int d = 0; d < 6; d++)
        {
            if (HasPort(d)) ports.Add(d);
        }

        if (ports.Count >= 2)
        {
            if (pieceType == RiverPieceType.SpringHead)
            {
                foreach (int p in ports)
                {
                    float mouthDist = (pieceType == RiverPieceType.Curve60 && config != null) ? config.curve60MouthDistance : 0.35f;
                    var pPath = RiverSplinePath.BuildSpringHeadPath(p, mouthDist, 8);
                    if (pPath.Count > 0) _cachedPaths.Add(pPath);
                }
            }
            else
            {
                int inPort = ports[0];
                int outPort = ports[1];
                if (RiverNetworkManager.Instance != null)
                {
                    var n0 = RiverNetworkManager.Instance.GetRiver(placedHex.GetNeighbor(ports[0]));
                    var n1 = RiverNetworkManager.Instance.GetRiver(placedHex.GetNeighbor(ports[1]));
                    if (n0 != null && n1 != null && n1.flowStep < n0.flowStep)
                    {
                        inPort = ports[1];
                        outPort = ports[0];
                    }
                }
                var path = RiverSplinePath.BuildThroughPath(inPort, outPort, 0f, 10);
                if (path.Count > 0) _cachedPaths.Add(path);
            }
        }
        else if (ports.Count == 1)
        {
            if (pieceType == RiverPieceType.SpringHead)
            {
                var path = RiverSplinePath.BuildSpringHeadPath(ports[0], 0.35f, 8);
                if (path.Count > 0) _cachedPaths.Add(path);
            }
            else if (pieceType == RiverPieceType.PondBasin)
            {
                var path = RiverSplinePath.BuildPondInletPath(ports[0], 0.32f, 8);
                if (path.Count > 0) _cachedPaths.Add(path);
            }
            else
            {
                var path = RiverSplinePath.BuildThroughPath(ports[0], (ports[0] + 3) % 6, 0f, 8);
                if (path.Count > 0) _cachedPaths.Add(path);
            }
        }
    }

    /// <summary>
    /// Kiểm tra cổng kết nối theo hướng dir (0..5)
    /// </summary>
    public bool HasPort(int dir)
    {
        return (connectedPortsMask & (1 << (((dir % 6) + 6) % 6))) != 0;
    }

    private Transform _trenchContainer;

    /// <summary>
    /// Tự động sinh các đoạn prefab mương nối (straightPrefab) lấp kín khoảng trống giữa tâm ô và các mép ô
    /// </summary>
    private void UpdateConnectingTrenches(RiverConfig config, Vector3 baseWorldPos, HexWorldGenerator worldGen = null)
    {
        if (_trenchContainer == null)
        {
            Transform existing = transform.Find("ConnectingTrenches");
            if (existing != null) _trenchContainer = existing;
            else
            {
                GameObject trenchObj = new GameObject("ConnectingTrenches");
                trenchObj.transform.SetParent(transform, false);
                _trenchContainer = trenchObj.transform;
            }
        }

        // Đảm bảo container nằm tại gốc thế giới không bị ảnh hưởng bởi góc xoay của mô hình trung tâm
        _trenchContainer.position = baseWorldPos;
        _trenchContainer.rotation = Quaternion.identity;
        _trenchContainer.localScale = Vector3.one;

        // Xóa sạch các đoạn mương cũ
        for (int i = _trenchContainer.childCount - 1; i >= 0; i--)
        {
            GameObject child = _trenchContainer.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }

        if (config == null || config.straightPrefab == null) return;

        List<int> connectedPorts = new List<int>();
        for (int d = 0; d < 6; d++)
        {
            if (HasPort(d)) connectedPorts.Add(d);
        }

        if (connectedPorts.Count == 0) return;

        switch (pieceType)
        {
            case RiverPieceType.SpringHead:
            {
                // Hang nguồn: Rải từ miệng hang ra tất cả các cổng kết nối hạ lưu
                float mouthDist = config != null ? config.springHeadMouthDistance : 0.20f;
                foreach (int outDir in connectedPorts)
                {
                    Vector3 mouthLocal = RiverSplinePath.GetEdgeNormalLocal(outDir) * mouthDist;
                    Vector3 edgeNormal = RiverSplinePath.GetEdgeNormalLocal(outDir);
                    float inward = GetWaterfallInwardOffset(outDir, config);
                    Vector3 edgeLocal = RiverSplinePath.GetEdgeMidpointLocal(outDir) - edgeNormal * inward;

                    SpawnTrenchLine(mouthLocal, edgeLocal, _trenchContainer, config, baseWorldPos);
                }
                break;
            }

            case RiverPieceType.PondBasin:
            {
                // Hồ chứa PondBasin (hồ cuối nguồn hoặc hồ hội tụ đón nhiều nhánh suối đổ về):
                // Duyệt qua TẤT CẢ các cổng kết nối (connectedPorts)
                float inletDist = config != null ? config.pondInletDistance : 0.48f;
                foreach (int inDir in connectedPorts)
                {
                    if (IsWaterfallInflow(inDir, worldGen))
                    {
                        // Thác nước từ trên cao đổ trực tiếp vào hồ (UpdateWaterfalls đã xử lý thác)
                        continue;
                    }

                    Vector3 edgeNormal = RiverSplinePath.GetEdgeNormalLocal(inDir);
                    Vector3 edgeLocal = RiverSplinePath.GetEdgeMidpointLocal(inDir);
                    Vector3 pondInletLocal = edgeNormal * inletDist;
                    SpawnTrenchLine(edgeLocal, pondInletLocal, _trenchContainer, config, baseWorldPos);
                }
                break;
            }

            case RiverPieceType.Straight:
            {
                // Suối thẳng: Rải từ mép cổng vào xuyên qua tâm sang mép cổng ra
                if (connectedPorts.Count >= 2)
                {
                    int p0 = connectedPorts[0];
                    int p1 = connectedPorts[1];

                    Vector3 norm0 = RiverSplinePath.GetEdgeNormalLocal(p0);
                    float in0 = GetWaterfallInwardOffset(p0, config);
                    Vector3 edgeA = RiverSplinePath.GetEdgeMidpointLocal(p0) - norm0 * in0;

                    Vector3 norm1 = RiverSplinePath.GetEdgeNormalLocal(p1);
                    float in1 = GetWaterfallInwardOffset(p1, config);
                    Vector3 edgeB = RiverSplinePath.GetEdgeMidpointLocal(p1) - norm1 * in1;

                    SpawnTrenchLine(edgeA, edgeB, _trenchContainer, config, baseWorldPos);
                }
                break;
            }

            case RiverPieceType.Curve120:
            {
                // Khuc cua cong 120 do: Uon luon muot ma theo duong cong Bezier, kich thuoc dong bo
                if (connectedPorts.Count >= 2)
                {
                    int p0 = connectedPorts[0];
                    int p1 = connectedPorts[1];

                    int inPort = p0;
                    int outPort = p1;
                    if (RiverNetworkManager.Instance != null)
                    {
                        var n0 = RiverNetworkManager.Instance.GetRiver(placedHex.GetNeighbor(p0));
                        var n1 = RiverNetworkManager.Instance.GetRiver(placedHex.GetNeighbor(p1));
                        if (n0 != null && n1 != null && n1.flowStep < n0.flowStep)
                        {
                            inPort = p1;
                            outPort = p0;
                        }
                    }

                    var curvePath = RiverSplinePath.BuildThroughPath(inPort, outPort, 0f, 20);
                    SpawnTrenchCurve(curvePath, _trenchContainer, config, baseWorldPos, pieceType);
                }
                break;
            }

            case RiverPieceType.Curve60:
            {
                // Khuc cua 60 do: Su dung model Curved60 (Curved60.prefab) o tam o va rai 2 doan muong thang noi tu 2 mep o vao mieng Curved60
                float mouthDist = config != null ? config.curve60MouthDistance : 0.35f;

                int inPort = connectedPorts[0];
                int outPort = (connectedPorts.Count > 1) ? connectedPorts[1] : connectedPorts[0];
                if (connectedPorts.Count >= 2 && RiverNetworkManager.Instance != null)
                {
                    var n0 = RiverNetworkManager.Instance.GetRiver(placedHex.GetNeighbor(connectedPorts[0]));
                    var n1 = RiverNetworkManager.Instance.GetRiver(placedHex.GetNeighbor(connectedPorts[1]));
                    if (n0 != null && n1 != null && n1.flowStep < n0.flowStep)
                    {
                        inPort = connectedPorts[1];
                        outPort = connectedPorts[0];
                    }
                }

                // Nhanh 1: Cong vao (Inflow) -> Muong thang tu mep o VAO mieng Curved60
                {
                    Vector3 normIn = RiverSplinePath.GetEdgeNormalLocal(inPort);
                    float inwIn = GetWaterfallInwardOffset(inPort, config);
                    Vector3 edgeIn = RiverSplinePath.GetEdgeMidpointLocal(inPort) - normIn * inwIn;
                    Vector3 mouthIn = normIn * mouthDist;
                    SpawnTrenchLine(edgeIn, mouthIn, _trenchContainer, config, baseWorldPos);
                }

                // Nhanh 2: Cong ra (Outflow) -> Muong thang tu mieng Curved60 RA mep o / thac nuoc
                if (connectedPorts.Count >= 2)
                {
                    Vector3 normOut = RiverSplinePath.GetEdgeNormalLocal(outPort);
                    float inwOut = GetWaterfallInwardOffset(outPort, config);
                    Vector3 edgeOut = RiverSplinePath.GetEdgeMidpointLocal(outPort) - normOut * inwOut;
                    Vector3 mouthOut = normOut * mouthDist;
                    SpawnTrenchLine(mouthOut, edgeOut, _trenchContainer, config, baseWorldPos);
                }
                break;
            }

            case RiverPieceType.Fork3Way:
            case RiverPieceType.Fork3WayAsymmetric:
            {
                // Ngã ba hoặc giao lộ nhiều nhánh: Model ở tâm ô xử lý giao lộ (bán kính ~0.35m)
                // Rải mương nối từ mỗi mép ô kết nối vào tâm ngã ba (kể cả cổng thứ 4, thứ 5 nếu có)
                foreach (int p in connectedPorts)
                {
                    Vector3 norm = RiverSplinePath.GetEdgeNormalLocal(p);
                    float inw = GetWaterfallInwardOffset(p, config);
                    Vector3 edgeMid = RiverSplinePath.GetEdgeMidpointLocal(p) - norm * inw;
                    Vector3 forkEnd = norm * 0.35f;
                    SpawnTrenchLine(edgeMid, forkEnd, _trenchContainer, config, baseWorldPos);
                }
                break;
            }
        }
    }

    /// <summary>
    /// Rải các đoạn prefab mương thẳng nối tiếp nhau giữa 2 điểm (tọa độ lệch so với baseWorldPos)
    /// </summary>
    private void SpawnTrenchLine(Vector3 startOffset, Vector3 endOffset, Transform parent, RiverConfig config, Vector3 baseWorldPos)
    {
        Vector3 dir = endOffset - startOffset;
        float dist = dir.magnitude;
        if (dist < 0.05f) return;

        Vector3 startWorld = baseWorldPos + startOffset;
        Vector3 endWorld = baseWorldPos + endOffset;
        Vector3 worldFlow = (endWorld - startWorld);
        worldFlow.y = 0f;
        if (worldFlow.sqrMagnitude > 0.0001f) worldFlow.Normalize();
        else worldFlow = Vector3.forward;

        float rotOffset = config != null ? (config.straightRotationOffset + config.globalRotationOffset) : 0f;
        Quaternion rot = Quaternion.LookRotation(worldFlow, Vector3.up) *
                         Quaternion.Euler(0f, rotOffset, 0f) *
                         config.straightPrefab.transform.localRotation;

        Vector3 pieceScale = config != null ? config.GetScaleForPiece(RiverPieceType.Straight) : new Vector3(0.28f, 0.42f, 0.28f);
        float yOffset = config != null ? config.GetYOffsetForPiece(RiverPieceType.Straight) : 0.045f;

        // Trục X của StraightStreamTrench là trục dọc theo dòng chảy (Length)
        float unitLength = Mathf.Max(0.05f, pieceScale.x);

        // Trường hợp 1: Nếu chiều dài đoạn mương được cấu hình đủ dài để phủ toàn bộ khoảng cách (hoặc gần bằng)
        // -> Chỉ cần 1 đoạn duy nhất vừa khít, tránh lặp lại nhiều tảng đá hoa văn
        if (unitLength >= dist * 0.85f)
        {
            Vector3 midOffset = (startOffset + endOffset) * 0.5f;
            Vector3 worldPos = baseWorldPos + midOffset;
            worldPos.y = baseWorldPos.y + yOffset;

            GameObject trench = Instantiate(config.straightPrefab, worldPos, rot, parent);
            trench.name = "Trench_SingleFit";

            // Co giãn chiều dài đúng bằng khoảng cách dist để 2 đầu chạm khít miệng hang và mép ô
            Vector3 fitScale = pieceScale;
            fitScale.x = dist;
            trench.transform.localScale = Vector3.Scale(config.straightPrefab.transform.localScale, fitScale);
            trench.SetActive(true);
            return;
        }

        // Trường hợp 2: Ghép chuỗi các đoạn mương nhỏ (Modular Chaining với độ gối đầu 15%)
        float stepDist = unitLength * 0.85f;
        int count = Mathf.Max(2, Mathf.CeilToInt(dist / stepDist));

        Vector3 flowDir = dir.normalized;
        // Tâm của đoạn đầu tiên chạm đúng mép startOffset
        Vector3 firstCenter = startOffset + flowDir * (unitLength * 0.5f);
        // Tâm của đoạn cuối cùng chạm đúng mép endOffset
        Vector3 lastCenter = endOffset - flowDir * (unitLength * 0.5f);

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            Vector3 offset = Vector3.Lerp(firstCenter, lastCenter, t);
            Vector3 worldPos = baseWorldPos + offset;
            worldPos.y = baseWorldPos.y + yOffset;

            GameObject trench = Instantiate(config.straightPrefab, worldPos, rot, parent);
            trench.name = $"Trench_{i}";
            trench.transform.localScale = Vector3.Scale(config.straightPrefab.transform.localScale, pieceScale);
            trench.SetActive(true);
        }
    }

    /// <summary>
    /// Rải các đoạn mương dọc theo đường cong Bézier (uốn lượn mềm mại theo tiếp tuyến dòng chảy)
    /// </summary>
    private void SpawnTrenchCurve(List<RiverWaypoint> path, Transform parent, RiverConfig config, Vector3 baseWorldPos, RiverPieceType pieceType = RiverPieceType.Curve120)
    {
        if (path == null || path.Count < 2 || config == null || config.straightPrefab == null) return;

        float totalDist = path[path.Count - 1].distance;
        if (totalDist < 0.1f) return;

        Vector3 pieceScale = config.GetScaleForPiece(pieceType);
        float yOffset = config.GetYOffsetForPiece(pieceType);
        float rotOffset = config.straightRotationOffset + config.globalRotationOffset;

        float unitLength = Mathf.Max(0.05f, pieceScale.x);
        float stepDist = unitLength * 0.85f;
        int count = Mathf.Max(2, Mathf.CeilToInt(totalDist / stepDist));

        float firstDist = unitLength * 0.5f;
        float lastDist = Mathf.Max(firstDist, totalDist - unitLength * 0.5f);

        for (int i = 0; i < count; i++)
        {
            float t = (count > 1) ? (float)i / (count - 1) : 0.5f;
            float sampleDist = Mathf.Lerp(firstDist, lastDist, t);
            var (posLocal, tanLocal) = SamplePathAtDistance(path, sampleDist);

            Vector3 worldPos = baseWorldPos + posLocal;
            // Bù độ cao gò đất ở tâm ô hex:
            // Mép ngoài (r ~ InnerRadius): mặt đất ở baseWorldPos.y -> bù 0 để khớp mép phẳng và đỉnh thác
            // Tâm ô (r ~ 0): gò đất lồi cao hơn mép viền +0.10m -> nâng mương lên để không bị đất cỏ nuốt chìm
            float distFromCenter = new Vector2(posLocal.x, posLocal.z).magnitude;
            float domeLift = Mathf.Clamp01(1f - (distFromCenter / HexMetrics.InnerRadius)) * 0.08f;
            worldPos.y = baseWorldPos.y + yOffset + domeLift;

            Quaternion rot = Quaternion.LookRotation(tanLocal, Vector3.up) *
                             Quaternion.Euler(0f, rotOffset, 0f) *
                             config.straightPrefab.transform.localRotation;

            GameObject trench = Instantiate(config.straightPrefab, worldPos, rot, parent);
            trench.name = $"Trench_Curve_{i}";
            trench.transform.localScale = Vector3.Scale(config.straightPrefab.transform.localScale, pieceScale);
            trench.SetActive(true);
        }
    }

    private static (Vector3 pos, Vector3 tangent) SamplePathAtDistance(List<RiverWaypoint> path, float targetDist)
    {
        if (path == null || path.Count == 0) return (Vector3.zero, Vector3.forward);
        if (path.Count == 1 || targetDist <= path[0].distance) return (path[0].position, path[0].tangent);
        if (targetDist >= path[path.Count - 1].distance) return (path[path.Count - 1].position, path[path.Count - 1].tangent);

        for (int i = 0; i < path.Count - 1; i++)
        {
            if (targetDist >= path[i].distance && targetDist <= path[i + 1].distance)
            {
                float segmentLen = path[i + 1].distance - path[i].distance;
                float t = segmentLen > 0.0001f ? (targetDist - path[i].distance) / segmentLen : 0f;
                Vector3 pos = Vector3.Lerp(path[i].position, path[i + 1].position, t);
                Vector3 tan = Vector3.Lerp(path[i].tangent, path[i + 1].tangent, t);
                if (tan.sqrMagnitude > 0.0001f) tan.Normalize();
                else tan = Vector3.forward;
                return (pos, tan);
            }
        }
        return (path[path.Count - 1].position, path[path.Count - 1].tangent);
    }

    /// <summary>
    /// Kiểm tra xem cổng inDir có nhận nước từ tầng trên đổ xuống qua Thác nước hay không
    /// </summary>
    private bool IsWaterfallInflow(int inDir, HexWorldGenerator worldGen)
    {
        if (worldGen == null) worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen == null || worldGen.MapTiles == null) return false;

        HexCoordinates upstreamCoords = placedHex.GetNeighbor(inDir);
        if (worldGen.MapTiles.TryGetValue(placedHex, out GameObject thisTile) && thisTile != null &&
            worldGen.MapTiles.TryGetValue(upstreamCoords, out GameObject upTile) && upTile != null)
        {
            float topY_this = HexBiomeClusterConnector.GetTileHeightLevels(thisTile).topY;
            float topY_up = HexBiomeClusterConnector.GetTileHeightLevels(upTile).topY;
            return (topY_up - topY_this > 0.45f);
        }
        return false;
    }

    private void OnDestroy()
    {
        if (visualInstance != null)
        {
            Destroy(visualInstance);
            visualInstance = null;
        }

        if (_trenchContainer != null)
        {
            Destroy(_trenchContainer.gameObject);
            _trenchContainer = null;
        }

        foreach (var kvp in _waterfallInstances)
        {
            if (kvp.Value != null) Destroy(kvp.Value);
        }
        _waterfallInstances.Clear();
        _doubleTierWaterfalls.Clear();
    }

    /// <summary>
    /// Lấy độ thụt lùi mương nối tại đỉnh thác nước theo loại thác (1 tầng hay 2 tầng vượt tầng)
    /// </summary>
    private float GetWaterfallInwardOffset(int dir, RiverConfig config)
    {
        if (!_waterfallInstances.ContainsKey(dir) || config == null) return 0f;
        return _doubleTierWaterfalls.Contains(dir) 
            ? config.doubleWaterfallInwardOffset 
            : config.waterfallInwardOffset;
    }
}
