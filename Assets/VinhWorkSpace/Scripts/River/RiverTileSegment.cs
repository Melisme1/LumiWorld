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
    public int placementOrder = 0; // Thứ tự đặt ô (ô đặt trước có giá trị nhỏ hơn, ưu tiên giữ thác nước)
    public RiverPieceType pieceType = RiverPieceType.PondBasin;
    public float currentRotationY = 0f;
    public int connectedPortsMask = 0;
    public int primaryPortA = -1; // Cổng dòng chính A (Thượng nguồn / Inflow)
    public int primaryPortB = -1; // Cổng dòng chính B (Hạ lưu / Outflow)

    [Header("3D Bed & Accent Elements")]
    [SerializeField] private GameObject visualInstance;

    private readonly Dictionary<int, GameObject> _waterfallInstances = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, GameObject> _upperWaterfallInstances = new Dictionary<int, GameObject>();
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
        primaryPortA = solution.primaryPortA;
        primaryPortB = solution.primaryPortB;

        float globalOffset = config != null ? config.globalRotationOffset : 0f;
        Vector3 scale = config != null ? config.modelScale : Vector3.one;

        transform.position = baseWorldPos;

        // 1. Sinh mô hình 3D lòng mương đá / hang grotto / hồ nước từ Prefab
        ApplyPrefabRiverbed(solution, config, baseWorldPos, globalOffset, scale);

        // 2. Tự động kiểm tra và sinh máng đá Thác nước vượt tầng nếu giật cấp địa hình
        UpdateWaterfalls(config, scale, worldGen);

        // 3. Cập nhật đường corridor để RiverPropHarmonizer dọn cây cản dòng nước
        UpdateCorridorPath(config, worldGen);

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
    /// <summary>
    /// NƠI DUY NHẤT TÍNH TOÁN TOÀN BỘ VỊ TRÍ & GÓC XOAY CHO CẢ 2 MÁNG THÁC NƯỚC:
    /// Đọc trực tiếp 100% từ RiverConfig, tập trung tuyệt đối tại 1 chỗ, không phân mảnh, không cộng trừ ẩn.
    /// </summary>
    private void ComputeWaterfallTransforms(
        Vector3 edgePos, Vector3 forwardVec, Vector3 rightVec, float topY_n,
        RiverConfig config,
        out Vector3 lowerPos, out Quaternion lowerRot,
        out Vector3 upperPos, out Quaternion upperRot)
    {
        float angleY = Mathf.Atan2(forwardVec.x, forwardVec.z) * Mathf.Rad2Deg + 180f;
        Vector3 lowerOffset = new Vector3(0f, 0.427f, 0.15f);
        float lowerRotOffset = 0f;
        Vector3 upperOffset = new Vector3(0f, 0.8f, -0.16f);
        Vector3 upperRotEuler = new Vector3(-36.5f, 0f, 0f);

        if (config != null)
        {
            config.GetWaterfallTransformOffsets(
                out lowerOffset, out lowerRotOffset,
                out upperOffset, out upperRotEuler);
        }

        // 1. Máng chân vách (Lower Waterfall):
        lowerPos = edgePos + rightVec * lowerOffset.x + forwardVec * lowerOffset.z;
        lowerPos.y = topY_n + lowerOffset.y;
        Quaternion baseRot = config != null && config.waterfallPrefab != null 
            ? config.waterfallPrefab.transform.localRotation 
            : Quaternion.identity;
        lowerRot = Quaternion.Euler(0f, angleY + lowerRotOffset, 0f) * baseRot;

        // 2. Máng đỉnh vách (Upper Waterfall):
        upperPos = lowerPos + Vector3.up * upperOffset.y + forwardVec * upperOffset.z + rightVec * upperOffset.x;
        upperRot = lowerRot * Quaternion.Euler(upperRotEuler);
    }

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

                // 1 NƠI DUY NHẤT TÍNH TOÁN TỌA ĐỘ & GÓC XOAY CHO CẢ 2 MÁNG THÁC NƯỚC
                ComputeWaterfallTransforms(
                    edgePos, forwardVec, rightVec, topY_n, config,
                    out Vector3 lowerPos, out Quaternion lowerRot,
                    out Vector3 upperPos, out Quaternion upperRot);

                Vector3 wfScale = config != null ? config.GetScaleForPiece(RiverPieceType.Waterfall) : scale;

                // Máng tầng dưới (Chân vách):
                if (!_waterfallInstances.TryGetValue(d, out GameObject wf) || wf == null)
                {
                    wf = Instantiate(config.waterfallPrefab, lowerPos, lowerRot, transform);
                    wf.name = $"Waterfall_Dir{d}";
                    _waterfallInstances[d] = wf;
                }
                else
                {
                    wf.transform.position = lowerPos;
                    wf.transform.rotation = lowerRot;
                }
                wf.transform.localScale = Vector3.Scale(config.waterfallPrefab.transform.localScale, wfScale);

                // Dọn dẹp child cũ nếu còn sót từ phiên bản trước
                Transform oldChildUpper = wf.transform.Find("WaterfallCascadeRamp (1)");
                if (oldChildUpper != null)
                {
                    if (Application.isPlaying) Destroy(oldChildUpper.gameObject);
                    else DestroyImmediate(oldChildUpper.gameObject);
                }

                // Máng tầng trên (Đỉnh vách 2 tầng) - Độc lập, không làm con của wf:
                if (isDoubleTier)
                {
                    _doubleTierWaterfalls.Add(d);

                    if (!_upperWaterfallInstances.TryGetValue(d, out GameObject upperObj) || upperObj == null)
                    {
                        upperObj = Instantiate(config.waterfallPrefab, upperPos, upperRot, transform);
                        upperObj.name = $"Waterfall_Upper_Dir{d}";
                        _upperWaterfallInstances[d] = upperObj;
                    }
                    else
                    {
                        upperObj.transform.position = upperPos;
                        upperObj.transform.rotation = upperRot;
                    }

                    upperObj.transform.localScale = Vector3.Scale(config.waterfallPrefab.transform.localScale, wfScale);
                    upperObj.SetActive(true);
                }                else
                {
                    _doubleTierWaterfalls.Remove(d);
                    if (_upperWaterfallInstances.TryGetValue(d, out GameObject oldUpper))
                    {
                        if (oldUpper != null)
                        {
                            if (Application.isPlaying) Destroy(oldUpper);
                            else DestroyImmediate(oldUpper);
                        }
                        _upperWaterfallInstances.Remove(d);
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
                if (_upperWaterfallInstances.TryGetValue(d, out GameObject oldUpper))
                {
                    if (oldUpper != null)
                    {
                        if (Application.isPlaying) Destroy(oldUpper);
                        else DestroyImmediate(oldUpper);
                    }
                    _upperWaterfallInstances.Remove(d);
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
    private void UpdateCorridorPath(RiverConfig config, HexWorldGenerator worldGen = null)
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
                    float mouthDist = 0.35f;
                    var pPath = RiverSplinePath.BuildSpringHeadPath(p, mouthDist, 8);
                    if (pPath.Count > 0) _cachedPaths.Add(pPath);
                }
            }
            else
            {
                int inPort = primaryPortA >= 0 ? primaryPortA : ports[0];
                int outPort = primaryPortB >= 0 ? primaryPortB : ports[1];
                EnsureUpstreamDownstreamOrder(ref inPort, ref outPort, worldGen);

                float inOffset = IsWaterfallInflow(inPort, worldGen) 
                    ? (config != null ? config.waterfallBottomOffset : 0.38f) 
                    : 0f;
                float outOffset = GetWaterfallInwardOffset(outPort, config);

                var path = RiverSplinePath.BuildThroughPath(inPort, outPort, 0f, 10, inOffset, outOffset);
                if (path.Count > 0) _cachedPaths.Add(path);

                // Bổ sung corridor cho các nhánh rẽ phụ (Tributaries):
                foreach (int p in ports)
                {
                    if (p == inPort || p == outPort) continue;
                    Vector3 edgeMid = RiverSplinePath.GetEdgeMidpointLocal(p);
                    Vector3 confluencePoint = GetClosestPointOnPath(path, edgeMid);
                    var branchPath = RiverSplinePath.BuildLinePath(edgeMid, confluencePoint, 8);
                    if (branchPath.Count > 0) _cachedPaths.Add(branchPath);
                }
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
                    int p0 = primaryPortA >= 0 ? primaryPortA : connectedPorts[0];
                    int p1 = primaryPortB >= 0 ? primaryPortB : connectedPorts[1];
                    EnsureUpstreamDownstreamOrder(ref p0, ref p1, worldGen);

                    Vector3 norm0 = RiverSplinePath.GetEdgeNormalLocal(p0);
                    float in0 = IsWaterfallInflow(p0, worldGen) 
                        ? (config != null ? config.waterfallBottomOffset : 0.38f) 
                        : GetWaterfallInwardOffset(p0, config);
                    Vector3 edgeA = RiverSplinePath.GetEdgeMidpointLocal(p0) - norm0 * in0;

                    Vector3 norm1 = RiverSplinePath.GetEdgeNormalLocal(p1);
                    float in1 = GetWaterfallInwardOffset(p1, config);
                    Vector3 edgeB = RiverSplinePath.GetEdgeMidpointLocal(p1) - norm1 * in1;

                    SpawnTrenchLine(edgeA, edgeB, _trenchContainer, config, baseWorldPos);
                }
                break;
            }

            case RiverPieceType.Curve120:
            case RiverPieceType.Curve60:
            {
                // Khúc cua cong 120 độ hoặc 60 độ: Uốn lượn mượt mà theo đường cong Bezier
                if (connectedPorts.Count >= 2)
                {
                    int inPort = primaryPortA >= 0 ? primaryPortA : connectedPorts[0];
                    int outPort = primaryPortB >= 0 ? primaryPortB : connectedPorts[1];
                    EnsureUpstreamDownstreamOrder(ref inPort, ref outPort, worldGen);

                    float inOffset = IsWaterfallInflow(inPort, worldGen) 
                        ? (config != null ? config.waterfallBottomOffset : 0.38f) 
                        : 0f;
                    float outOffset = GetWaterfallInwardOffset(outPort, config);

                    var curvePath = RiverSplinePath.BuildThroughPath(inPort, outPort, 0f, 20, inOffset, outOffset);
                    SpawnTrenchCurve(curvePath, _trenchContainer, config, baseWorldPos, pieceType);
                }
                break;
            }
        }

        // TỰ ĐỘNG SINH CÁC NHÁNH RẼ PHỤ (Tributary Branches):
        // Giữ nguyên dòng chính (Straight/Curve120/Curve60), bất kỳ cổng kết nối thứ 3, thứ 4...
        // đều được nối bằng một đoạn mương từ mép ô cắm vào tâm giao lộ!
        if (pieceType == RiverPieceType.Straight || pieceType == RiverPieceType.Curve120 || pieceType == RiverPieceType.Curve60)
        {
            int pMainA = primaryPortA >= 0 ? primaryPortA : (connectedPorts.Count > 0 ? connectedPorts[0] : -1);
            int pMainB = primaryPortB >= 0 ? primaryPortB : (connectedPorts.Count > 1 ? connectedPorts[1] : -1);

            List<RiverWaypoint> mainPath = (_cachedPaths != null && _cachedPaths.Count > 0) ? _cachedPaths[0] : null;

            foreach (int p in connectedPorts)
            {
                if (p == pMainA || p == pMainB) continue;

                Vector3 norm = RiverSplinePath.GetEdgeNormalLocal(p);
                float inw = IsWaterfallInflow(p, worldGen) 
                    ? (config != null ? config.waterfallBottomOffset : 0.38f) 
                    : GetWaterfallInwardOffset(p, config);
                Vector3 edgeMid = RiverSplinePath.GetEdgeMidpointLocal(p) - norm * inw;
                Vector3 junctionPoint = Vector3.zero;
                if (mainPath != null && mainPath.Count > 0)
                {
                    junctionPoint = GetClosestPointOnPath(mainPath, edgeMid);
                }
                else
                {
                    Vector3 midA = RiverSplinePath.GetEdgeMidpointLocal(pMainA);
                    Vector3 midB = RiverSplinePath.GetEdgeMidpointLocal(pMainB);
                    junctionPoint = ProjectPointOnSegment(edgeMid, midA, midB);
                }

                Vector3 toJunction = junctionPoint - edgeMid;
                float branchDist = toJunction.magnitude;
                float bankOffset = 0.16f;
                Vector3 branchEnd = (branchDist > bankOffset + 0.05f)
                    ? edgeMid + toJunction.normalized * (branchDist - bankOffset)
                    : junctionPoint;

                bool isBranchInflow = false;
                if (RiverNetworkManager.Instance != null)
                {
                    var nSeg = RiverNetworkManager.Instance.GetRiver(placedHex.GetNeighbor(p));
                    if (nSeg != null && nSeg.flowStep < flowStep)
                    {
                        isBranchInflow = true;
                    }
                }

                if (isBranchInflow)
                {
                    SpawnTrenchLine(edgeMid, branchEnd, _trenchContainer, config, baseWorldPos);
                }
                else
                {
                    SpawnTrenchLine(branchEnd, edgeMid, _trenchContainer, config, baseWorldPos);
                }
            }
        }
    }

    /// <summary>
    /// Luôn đảm bảo inPort là cổng thượng nguồn (nhận nước từ ô có flowStep nhỏ hơn hoặc tầng cao hơn)
    /// và outPort là cổng hạ lưu thoát nước.
    /// </summary>
    private void EnsureUpstreamDownstreamOrder(ref int inPort, ref int outPort, HexWorldGenerator worldGen)
    {
        if (RiverNetworkManager.Instance == null) return;
        if (worldGen == null) worldGen = FindAnyObjectByType<HexWorldGenerator>();

        var nIn = RiverNetworkManager.Instance.GetRiver(placedHex.GetNeighbor(inPort));
        var nOut = RiverNetworkManager.Instance.GetRiver(placedHex.GetNeighbor(outPort));

        float yIn = (nIn != null && worldGen != null && worldGen.MapTiles != null && worldGen.MapTiles.TryGetValue(nIn.placedHex, out var tIn) && tIn != null)
            ? HexBiomeClusterConnector.GetTileHeightLevels(tIn).topY : float.MinValue;
        float yOut = (nOut != null && worldGen != null && worldGen.MapTiles != null && worldGen.MapTiles.TryGetValue(nOut.placedHex, out var tOut) && tOut != null)
            ? HexBiomeClusterConnector.GetTileHeightLevels(tOut).topY : float.MinValue;

        bool shouldSwap = false;
        // Nếu cổng out nằm ở tầng cao hơn cổng in (nước chảy ngược dốc):
        if (yOut > yIn + 0.35f)
        {
            shouldSwap = true;
        }
        // Nếu cùng tầng cao, cổng nào có flowStep nhỏ hơn (gần nguồn hơn) là cổng in:
        else if (Mathf.Abs(yOut - yIn) <= 0.35f && nIn != null && nOut != null && nOut.flowStep < nIn.flowStep)
        {
            shouldSwap = true;
        }

        if (shouldSwap)
        {
            int temp = inPort;
            inPort = outPort;
            outPort = temp;
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
        float yOffset = config != null ? config.straightYOffset : 0.056f;

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
        float yOffset = config != null ? config.straightYOffset : 0.056f;
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
        foreach (var kvp in _upperWaterfallInstances)
        {
            if (kvp.Value != null) Destroy(kvp.Value);
        }
        _upperWaterfallInstances.Clear();
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

    /// <summary>
    /// TÃ¬m Ä‘iá»ƒm trÃªn Ä‘Æ°á»ng cong dÃ²ng suá»‘i gáº§n nháº¥t vá»›i Ä‘iá»ƒm cho trÆ°á»›c (dÃ¹ng tÃ­nh Ä‘iá»ƒm hÃ²a dÃ²ng)
    /// </summary>
    private static Vector3 GetClosestPointOnPath(List<RiverWaypoint> path, Vector3 point)
    {
        if (path == null || path.Count == 0) return Vector3.zero;
        if (path.Count == 1) return path[0].position;

        Vector3 closest = path[0].position;
        float minDistSq = float.MaxValue;

        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 segA = path[i].position;
            Vector3 segB = path[i + 1].position;
            Vector3 p = ProjectPointOnSegment(point, segA, segB);
            float dSq = (point - p).sqrMagnitude;
            if (dSq < minDistSq)
            {
                minDistSq = dSq;
                closest = p;
            }
        }
        return closest;
    }

    /// <summary>
    /// HÃ¬nh chiáº¿u vuÃ´ng gÃ³c cá»§a má»™t Ä‘iá»ƒm lÃªn má»™t Ä‘oáº¡n tháº³ng trong khÃ´ng gian XZ
    /// </summary>
    private static Vector3 ProjectPointOnSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float abSqr = ab.sqrMagnitude;
        if (abSqr < 0.0001f) return a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / abSqr);
        return a + ab * t;
    }
}