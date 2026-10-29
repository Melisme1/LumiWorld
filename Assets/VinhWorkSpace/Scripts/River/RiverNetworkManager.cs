using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quáº£n lÃ½ toÃ n bá»™ máº¡ng lÆ°á»›i sÃ´ng suá»‘i trÃªn báº£n Ä‘á»“ (Preserve / Dorfromantik style).
/// - Quáº£n lÃ½ Ä‘Äƒng kÃ½ cÃ¡c Ä‘oáº¡n suá»‘i (RiverTileSegment).
/// - Tá»± Ä‘á»™ng káº¿t ná»‘i, xoay vÃ  nÃ¢ng cáº¥p mÃ´ hÃ¬nh khi cÃ¡c Ä‘oáº¡n suá»‘i cháº¡m nhau.
/// - CÆ¡ cháº¿ tÃ­nh Ä‘iá»ƒm pháº£n á»©ng hai chiá»u (Bi-directional Reactive Scoring) giá»¯a Suá»‘i, Biome vÃ  ThÃº (Báº¿n nÆ°á»›c muÃ´n loÃ i).
/// </summary>
public class RiverNetworkManager : MonoBehaviour
{
    private static RiverNetworkManager _instance;
    public static RiverNetworkManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<RiverNetworkManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("RiverNetworkManager");
                    _instance = go.AddComponent<RiverNetworkManager>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Configuration")]
    [SerializeField] private RiverConfig riverConfig;

    public RiverConfig Config
    {
        get => riverConfig;
        set => riverConfig = value;
    }

    // Báº£n Ä‘á»“ lÆ°u trá»¯ cÃ¡c Ã´ lá»¥c giÃ¡c Ä‘Ã£ cÃ³ suá»‘i
    private readonly Dictionary<HexCoordinates, RiverTileSegment> _riverMap = new Dictionary<HexCoordinates, RiverTileSegment>();

    // Quáº£n lÃ½ cÃ¡c tuyáº¿n sÃ´ng liÃªn tá»¥c tá»« nguá»“n báº±ng Unity Splines
    private readonly Dictionary<HexCoordinates, GameObject> _continuousRiverObjects = new Dictionary<HexCoordinates, GameObject>();

    public IReadOnlyDictionary<HexCoordinates, RiverTileSegment> RiverMap => _riverMap;
    public IReadOnlyDictionary<HexCoordinates, GameObject> ContinuousRiverObjects => _continuousRiverObjects;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        EnsureConfig();
        IndexExistingRivers();
    }

    /// <summary>
    /// Äáº£m báº£o luÃ´n cÃ³ RiverConfig há»£p lá»‡ (tá»± tÃ¬m trong Resources hoáº·c gÃ¡n fallback)
    /// </summary>
    private void EnsureConfig()
    {
        if (riverConfig == null)
        {
            riverConfig = Resources.Load<RiverConfig>("RiverConfig");
            if (riverConfig == null)
            {
                // Tá»± sinh 1 ScriptableObject runtime náº¿u chÆ°a cÃ³ asset
                riverConfig = ScriptableObject.CreateInstance<RiverConfig>();
            }
        }
    }

    /// <summary>
    /// QuÃ©t toÃ n bá»™ Scene khi khá»Ÿi Ä‘á»™ng Ä‘á»ƒ thu tháº­p cÃ¡c Ã´ suá»‘i Ä‘Ã£ Ä‘Æ°á»£c náº¡p sáºµn tá»« file Save
    /// </summary>
    public void IndexExistingRivers()
    {
        _riverMap.Clear();
        RiverTileSegment[] existingSegments = FindObjectsByType<RiverTileSegment>(FindObjectsInactive.Exclude);
        foreach (var seg in existingSegments)
        {
            if (seg != null && !_riverMap.ContainsKey(seg.placedHex))
            {
                _riverMap.Add(seg.placedHex, seg);
            }
        }
    }

    /// <summary>
    /// Kiá»ƒm tra xem Ã´ lá»¥c giÃ¡c Ä‘Ã£ cÃ³ Ä‘oáº¡n suá»‘i nÃ o cháº£y qua chÆ°a
    /// </summary>
    public bool HasRiver(HexCoordinates coords)
    {
        return _riverMap.ContainsKey(coords);
    }

    /// <summary>
    /// Láº¥y Ä‘oáº¡n suá»‘i táº¡i tá»a Ä‘á»™ chá»‰ Ä‘á»‹nh
    /// </summary>
    public RiverTileSegment GetRiver(HexCoordinates coords)
    {
        _riverMap.TryGetValue(coords, out RiverTileSegment seg);
        return seg;
    }

    // =========================================================================
    // Äáº¶T BÃ€I SÃ”NG SUá»I (RIVER PLACEMENT & AUTO-TILING)
    // =========================================================================

    /// <summary>
    /// Dá»n dáº¹p vÃ  Ä‘iá»u chá»‰nh cÃ¢y cá»‘i/props trÃªn Ã´ khi Ä‘áº·t suá»‘i xuá»‘ng Ä‘á»ƒ nÆ°á»›c khÃ´ng bá»‹ Ä‘Ã¨ xuyÃªn qua cÃ¢y,
    /// Ä‘á»“ng thá»i dáº¡t cÃ¡c bá»¥i hoa vÃ  thá»±c váº­t nhá» ra hai bÃªn bá» suá»‘i/há»“ theo thuáº­t toÃ¡n RiverPropHarmonizer.
    /// </summary>
    public static void ClearConflictingPropsForRiver(GameObject tileObj)
    {
        if (tileObj == null) return;
        RiverPropHarmonizer.HarmonizePropsForRiver(tileObj, RiverPieceType.SpringHead, 0f, true, null);
    }

    /// <summary>
    /// Đặt một đoạn suối lên ô lục giác, tự động uốn lượn nối với các ô suối kề bên và tính điểm
    /// </summary>
    public PlacedCard PlaceRiver(HexCoordinates coords, GameObject targetTileObj, CardData cardData, Vector3? explicitSpawnPos = null)
    {
        EnsureConfig();
        if (targetTileObj == null) return null;

        // 1. Xác định độ cao bề mặt trên cùng của ô (topY)
        float surfaceY = HexBiomeClusterConnector.GetTileHeightLevels(targetTileObj).topY;
        Vector3 baseSurfacePos = explicitSpawnPos.HasValue 
            ? explicitSpawnPos.Value 
            : HexMetrics.HexToWorldPosition(coords, surfaceY);

        // 2. Tạo container cho đoạn suối
        GameObject riverObj = new GameObject($"River_{coords.Q}_{coords.R}");
        riverObj.transform.SetParent(targetTileObj.transform, true);
        riverObj.transform.position = baseSurfacePos;

        RiverTileSegment segment = riverObj.AddComponent<RiverTileSegment>();
        segment.placedHex = coords;

        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();

        // Tính bước dòng chảy (flowStep): Đầu nguồn = 0, ô tiếp theo = neighbor.flowStep + 1
        int minStep = int.MaxValue;
        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates neighbor = coords.GetNeighbor(dir);
            if (_riverMap.TryGetValue(neighbor, out RiverTileSegment nSeg) && nSeg != null)
            {
                // LUẬT TRỌNG LỰC: Nước không thể chảy ngược dốc từ ô thấp hơn lên ô mới cao hơn!
                float nY = GetTileTopY(neighbor, worldGen);
                if (nY < surfaceY - 0.35f)
                {
                    continue; // Ô láng giềng thấp hơn không thể cấp nước leo dốc lên ô cao mới đặt!
                }

                if (nSeg.flowStep < minStep) minStep = nSeg.flowStep;
            }
        }
        bool isNewOrigin = (minStep == int.MaxValue) && (surfaceY >= 1.35f);
        segment.isOrigin = isNewOrigin;
        segment.flowStep = isNewOrigin ? 0 : (minStep + 1);
        _riverMap[coords] = segment;

        // 3. Gáº¯n PlacedCard Ä‘á»ƒ há»‡ thá»‘ng Save/Load vÃ  Ä‘iá»ƒm sá»‘ nháº­n diá»‡n
        PlacedCard placedCard = riverObj.AddComponent<PlacedCard>();
        placedCard.cardData = cardData;
        placedCard.placedHex = coords;

        // 4. Cáº­p nháº­t Auto-Tiling cho Ã´ nÃ y vÃ  toÃ n bá»™ cÃ¡c Ã´ lÃ¡ng giá»ng ká» sÃ¡t
        RefreshTileAndNeighbors(coords, baseSurfacePos);

        // 5. TÃ­nh toÃ¡n Ä‘iá»ƒm thÆ°á»Ÿng ná»• tá»©c thÃ¬ khi Ä‘áº·t suá»‘i (Burst Score)
        int burstScore = CalculateBurstScoreForNewRiver(coords);
        if (burstScore > 0)
        {
            placedCard.displayedScore = burstScore;
            Vector3 popupPos = HexMetrics.HexToWorldPosition(coords, surfaceY + 0.35f);
            ScorePopupManager.ShowScore(burstScore, popupPos);
        }

        // 6. Cáº­p nháº­t tá»•ng Ä‘iá»ƒm
        if (ScoreCalculator.Instance != null)
        {
            ScoreCalculator.Instance.RecalculateScore();
        }

        return placedCard;
    }

    /// <summary>
    /// LÃ m má»›i loáº¡i hÃ¬nh há»c (Straight, Curve, Fork...) vÃ  gÃ³c xoay cá»§a Ã´ chá»‰ Ä‘á»‹nh vÃ  cÃ¡c Ã´ suá»‘i ká» bÃªn
    /// </summary>
    public void RefreshTileAndNeighbors(HexCoordinates centerCoords, Vector3 centerWorldPos)
    {
        List<HexCoordinates> toRefresh = new List<HexCoordinates> { centerCoords };

        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates neighbor = centerCoords.GetNeighbor(dir);
            if (_riverMap.ContainsKey(neighbor))
            {
                toRefresh.Add(neighbor);
            }
        }

        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();

        foreach (var hex in toRefresh)
        {
            if (_riverMap.TryGetValue(hex, out RiverTileSegment seg) && seg != null)
            {
                // Xác định độ cao bề mặt của ô hiện tại và kiểm tra tầng thấp nhất
                bool isLowTierTerminal = false;
                float hexSurfaceY = HexMetrics.TileHeight;
                GameObject tileObj = null;
                if (worldGen != null && worldGen.MapTiles != null && worldGen.MapTiles.TryGetValue(hex, out tileObj) && tileObj != null)
                {
                    var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tileObj);
                    hexSurfaceY = topY;
                    HexTileInfo tInfo = tileObj.GetComponent<HexTileInfo>();
                    isLowTierTerminal = (tInfo != null && tInfo.terrainTypeIndex == 0) || (topY < 0.9f);
                }

                List<int> connectedDirs = new List<int>();
                int upstreamDir = -1;
                int outflowDir = -1;
                for (int d = 0; d < 6; d++)
                {
                    HexCoordinates n = hex.GetNeighbor(d);
                    if (_riverMap.TryGetValue(n, out RiverTileSegment nSeg) && nSeg != null)
                    {
                        if (!ShouldConnectRivers(hex, seg, n, nSeg, worldGen))
                        {
                            continue;
                        }

                        connectedDirs.Add(d);
                        float nTopY = GetTileTopY(n, worldGen);

                        // Ưu tiên xác định hướng dòng chảy dựa trên ĐỘ CAO THỰC TẾ:
                        if (Mathf.Abs(nTopY - hexSurfaceY) > 0.35f)
                        {
                            if (nTopY > hexSurfaceY)
                            {
                                upstreamDir = d; // Nước từ ô cao hơn đổ xuống ô này
                            }
                            else
                            {
                                outflowDir = d;  // Nước từ ô này đổ xuống ô thấp hơn
                            }
                        }
                        else
                        {
                            // Cùng độ cao bằng phẳng: So sánh theo bước dòng chảy flowStep
                            if (nSeg.flowStep < seg.flowStep)
                            {
                                upstreamDir = d;
                            }
                            else if (nSeg.flowStep > seg.flowStep)
                            {
                                outflowDir = d;
                            }
                        }
                    }
                }

                // Cập nhật trạng thái nguồn: Một ô chỉ là Đầu nguồn (SpringHead) nếu KHÔNG có nước từ ô khác đổ vào
                bool isOrigin = (upstreamDir < 0) && (hexSurfaceY >= 1.35f) && !isLowTierTerminal;
                if (isOrigin)
                {
                    seg.isOrigin = true;
                    seg.flowStep = 0;
                }
                else
                {
                    seg.isOrigin = false;
                    if (upstreamDir >= 0)
                    {
                        HexCoordinates upHex = hex.GetNeighbor(upstreamDir);
                        if (_riverMap.TryGetValue(upHex, out RiverTileSegment upSeg) && upSeg != null)
                        {
                            seg.flowStep = upSeg.flowStep + 1;
                        }
                    }
                    else
                    {
                        seg.flowStep = 10;
                    }
                }

                // Tìm hướng dốc xuống cho ô đầu nguồn nếu chưa có kết nối nào
                int downhillDir = 0;
                float lowestNeighborY = float.MaxValue;
                for (int d = 0; d < 6; d++)
                {
                    HexCoordinates n = hex.GetNeighbor(d);
                    if (worldGen != null && worldGen.MapTiles.TryGetValue(n, out GameObject nTile) && nTile != null)
                    {
                        float nY = HexBiomeClusterConnector.GetTileHeightLevels(nTile).topY;
                        if (nY < lowestNeighborY)
                        {
                            lowestNeighborY = nY;
                            downhillDir = d;
                        }
                    }
                }

                RiverTileSolution solution = RiverAutoTileSolver.Solve(connectedDirs, isOrigin, downhillDir, isLowTierTerminal, upstreamDir, outflowDir);

                Debug.Log($"<color=#38BDF8>[RiverAutoTile]</color> Hex {hex} (isOrigin={isOrigin}, conns={solution.connectionCount}) -> Piece: <b>{solution.pieceType}</b>, RotY: <b>{solution.rotationY:F1}°</b>");

                Vector3 hexPos = HexMetrics.HexToWorldPosition(hex, hexSurfaceY);
                seg.ApplySolution(solution, riverConfig, hexPos, worldGen);

                // Kích hoạt thuật toán Thích ứng và Tái phân bố Props sinh thái (RiverPropHarmonizer):
                if (tileObj != null)
                {
                    bool isInitialSpring = isOrigin && (solution.connectionCount == 0);
                    float actualRotY = solution.rotationY + (riverConfig != null ? riverConfig.GetRotationOffsetForPiece(solution.pieceType) : 0f);
                    RiverPropHarmonizer.HarmonizePropsForRiver(tileObj, solution.pieceType, actualRotY, isInitialSpring, this, seg);
                }

                // Luôn cập nhật mối nối SeamBridge kín rãnh với các ô láng giềng kề sát
                if (HexBiomeClusterConnector.Instance != null)
                {
                    HexBiomeClusterConnector.Instance.ConnectAllNearby(hex);
                }
            }
        }
    }

    /// <summary>
    /// Lấy độ cao bề mặt thực tế của ô lục giác
    /// </summary>
    private float GetTileTopY(HexCoordinates coords, HexWorldGenerator worldGen)
    {
        if (worldGen != null && worldGen.MapTiles != null && worldGen.MapTiles.TryGetValue(coords, out GameObject tObj) && tObj != null)
        {
            return HexBiomeClusterConnector.GetTileHeightLevels(tObj).topY;
        }
        return HexMetrics.TileHeight;
    }

    /// <summary>
    /// Kiểm tra xem cạnh giữa h1 và h2 có phải là cạnh cần ngắt trong bất kỳ tam giác suối 3 ô nào không.
    /// Một tam giác suối (3 ô kề nhau) luôn tạo thành vòng lặp / mạng nhện mất tự nhiên,
    /// do đó bắt buộc phải ngắt 1 cạnh để chỉ giữ lại dòng chảy 1 làn duy nhất (Clean Flow).
    /// </summary>
    /// <summary>
    /// Kiểm tra xem 2 ô lục giác láng giềng kề sát có kết nối suối hợp lệ không.
    /// Tuân thủ quy luật dòng chảy tự nhiên (Clean Flow & Gravity):
    /// 1. Nước luôn chảy xuôi dốc từ tầng cao xuống tầng thấp.
    /// 2. Cùng một tầng phẳng:
    ///    - Hai ô KHÔNG nối ngang với nhau nếu cả 2 đều đã nhận nước từ tầng trên cao đổ xuống (2 nhánh tẽ riêng biệt).
    ///    - Hai ô KHÔNG nối ngang với nhau nếu cả 2 đều cùng đổ xuống chung một ô trũng bên dưới (2 nhánh hợp lưu riêng biệt).
    ///    - Trong tam giác 3 ô cùng tầng: Ngắt cạnh trực tiếp giữa điểm đầu và điểm cuối để dòng suối đi 1 làn A -> B -> C.
    /// </summary>
    private bool ShouldConnectRivers(HexCoordinates hA, RiverTileSegment segA, HexCoordinates hB, RiverTileSegment segB, HexWorldGenerator worldGen)
    {
        if (segA == null || segB == null) return false;

        float yA = GetTileTopY(hA, worldGen);
        float yB = GetTileTopY(hB, worldGen);

        // TrÆ°á»ng há»£p 1: ChÃªnh lá»‡ch Ä‘á»™ cao giá»¯a 2 táº§ng (Dá»‘c / ThÃ¡c nÆ°á»›c)
        if (Mathf.Abs(yA - yB) > 0.35f)
        {
            HexCoordinates upperHex = (yA > yB) ? hA : hB;
            HexCoordinates lowerHex = (yA > yB) ? hB : hA;
            RiverTileSegment upperSeg = (yA > yB) ? segA : segB;
            RiverTileSegment lowerSeg = (yA > yB) ? segB : segA;
            float lowerY = Mathf.Min(yA, yB);

            // Ã” á»Ÿ táº§ng tháº¥p hÆ¡n CHá»ˆ ÄÆ¯á»¢C PHÃ‰P nháº­n nÆ°á»›c tá»« ÄÃšNG 1 Ã” á»Ÿ táº§ng cao hÆ¡n:
            if (GetBestUpstreamFeeder(lowerHex, worldGen) != upperHex)
            {
                return false;
            }

            // QUY Táº®C CHá»NG THÃC NÆ¯á»šC KÃ‰P Cáº NH NHAU (Anti-Parallel Waterfalls):
            // Trong tam giÃ¡c {upperHex, lowerHex, hC}: Náº¿u tá»“n táº¡i Ã´ hC á»Ÿ cÃ¹ng táº§ng vá»›i lowerHex,
            // cÅ©ng ká» vá»›i upperHex vÃ  lowerHex: upperHex CHá»ˆ ÄÆ¯á»¢C Ä‘á»• 1 thÃ¡c nÆ°á»›c duy nháº¥t xuá»‘ng 1 trong 2 Ã´!
            // Ã” cÃ²n láº¡i sáº½ nháº­n dÃ²ng nÆ°á»›c cháº£y ngang tá»« Ã´ nháº­n thÃ¡c Ä‘á»ƒ suá»‘i Ä‘i tuáº§n tá»±, khÃ´ng táº¡o vÃ²ng láº·p.
            for (int d = 0; d < 6; d++)
            {
                HexCoordinates hC = lowerHex.GetNeighbor(d);
                if (_riverMap.TryGetValue(hC, out RiverTileSegment segC) && segC != null && upperHex.DistanceTo(hC) == 1)
                {
                    float yC = GetTileTopY(hC, worldGen);
                    if (Mathf.Abs(yC - lowerY) <= 0.35f)
                    {
                        int rankLower = lowerSeg.isOrigin ? -1 : lowerSeg.flowStep;
                        int rankC = segC.isOrigin ? -1 : segC.flowStep;

                        if (rankC < rankLower)
                        {
                            return false; // NhÆ°á»ng thÃ¡c nÆ°á»›c cho hC; lowerHex sáº½ nháº­n suá»‘i cháº£y ngang tá»« hC
                        }
                        else if (rankC == rankLower)
                        {
                            // HÃ²a Ä‘iá»ƒm: DÃ¹ng tá»a Ä‘á»™ phÃ¡ vá»¡ tháº¿ hÃ²a Ä‘á»ƒ chá»‰ duy nháº¥t 1 Ã´ nháº­n thÃ¡c
                            long idLower = ((long)lowerHex.Q << 32) ^ (long)lowerHex.R;
                            long idC = ((long)hC.Q << 32) ^ (long)hC.R;
                            if (idC < idLower)
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            return true; // NÆ°á»›c cháº£y xuÃ´i tá»« Ã´ cao xuá»‘ng Ã´ tháº¥p theo nhÃ¡nh há»£p lá»‡ duy nháº¥t
        }

        // TrÆ°á»ng há»£p 2: Hai Ã´ á»Ÿ CÃ™NG Má»˜T Táº¦NG Äá»˜ CAO
        // Quy táº¯c A: Tam giÃ¡c vá»›i Ã´ trÃªn cao {hUpper, hA, hB}:
        // Náº¿u cáº£ 2 Ã´ Ä‘á»u Ä‘ang nháº­n thÃ¡c nÆ°á»›c tá»« táº§ng trÃªn Ä‘á»• xuá»‘ng, KHÃ”NG ná»‘i ngang Ä‘á»ƒ trÃ¡nh táº¡o vÃ²ng láº·p tam giÃ¡c!
        for (int d = 0; d < 6; d++)
        {
            HexCoordinates hUpper = hA.GetNeighbor(d);
            if (_riverMap.TryGetValue(hUpper, out RiverTileSegment segUp) && segUp != null && hB.DistanceTo(hUpper) == 1)
            {
                float yUp = GetTileTopY(hUpper, worldGen);
                if (yUp > yA + 0.35f)
                {
                    if (segA.HasPort(d))
                    {
                        for (int d2 = 0; d2 < 6; d2++)
                        {
                            if (hB.GetNeighbor(d2) == hUpper && segB.HasPort(d2))
                            {
                                return false; // Cáº£ hai Ä‘á»u cÃ³ thÃ¡c tá»« hUpper Ä‘á»• xuá»‘ng -> Tuyá»‡t Ä‘á»‘i khÃ´ng ná»‘i ngang!
                            }
                        }
                    }
                }
            }
        }

        // Quy táº¯c C: Tam giÃ¡c 3 Ã´ cÃ¹ng táº§ng pháº³ng {hA, hB, hC}
        // Ngáº¯t cáº¡nh trá»±c tiáº¿p giá»¯a Ã´ cÃ³ flowStep nhá» nháº¥t vÃ  lá»›n nháº¥t Ä‘á»ƒ dÃ²ng Ä‘i 1 lÃ n A -> B -> C
        for (int d = 0; d < 6; d++)
        {
            HexCoordinates hC = hA.GetNeighbor(d);
            if (_riverMap.TryGetValue(hC, out RiverTileSegment segC) && segC != null && hB.DistanceTo(hC) == 1)
            {
                float yC = GetTileTopY(hC, worldGen);
                if (Mathf.Abs(yC - yA) <= 0.35f)
                {
                    int rA = segA.isOrigin ? -1 : segA.flowStep;
                    int rB = segB.isOrigin ? -1 : segB.flowStep;
                    int rC = segC.isOrigin ? -1 : segC.flowStep;

                    int minR = Mathf.Min(rA, Mathf.Min(rB, rC));
                    int maxR = Mathf.Max(rA, Mathf.Max(rB, rC));

                    if ((rA == minR && rB == maxR) || (rB == minR && rA == maxR))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Tìm ô thượng nguồn tốt nhất (gần nguồn nhất - min flowStep) trong số các ô láng giềng ở tầng cao hơn.
    /// Giúp ô tầng dưới chỉ nhận đúng 1 nguồn cấp nước từ ô gần nguồn nhất ở tầng trên.
    /// </summary>
    private HexCoordinates GetBestUpstreamFeeder(HexCoordinates hex, HexWorldGenerator worldGen)
    {
        float myY = GetTileTopY(hex, worldGen);
        HexCoordinates bestFeeder = hex;
        int bestRank = int.MaxValue;

        for (int d = 0; d < 6; d++)
        {
            HexCoordinates nHex = hex.GetNeighbor(d);
            if (_riverMap.TryGetValue(nHex, out RiverTileSegment nSeg) && nSeg != null)
            {
                float nY = GetTileTopY(nHex, worldGen);
                if (nY > myY + 0.35f)
                {
                    // isOrigin ưu tiên cao nhất (-1), sau đó đến flowStep nhỏ nhất
                    int rank = nSeg.isOrigin ? -1 : nSeg.flowStep;
                    if (rank < bestRank)
                    {
                        bestRank = rank;
                        bestFeeder = nHex;
                    }
                }
            }
        }

        return bestFeeder;
    }

    private bool HasHigherNeighborFeed(HexCoordinates hex, HexWorldGenerator worldGen)
    {
        return GetBestUpstreamFeeder(hex, worldGen) != hex;
    }

    // =========================================================================
    // ÄIá»‚M PHáº¢N á»¨NG HAI CHIá»€U (REACTIVE SCORING)
    // =========================================================================

    /// <summary>
    /// TÃ­nh Ä‘iá»ƒm thÆ°á»Ÿng tá»©c thÃ¬ khi vá»«a Ä‘áº·t má»™t tháº» Suá»‘i má»›i xuá»‘ng:
    /// - Äiá»ƒm báº£n thÃ¢n Ä‘oáº¡n suá»‘i (Base River points).
    /// - Äiá»ƒm cá»™ng hÆ°á»Ÿng náº¿u Ã´ nÃ y Ä‘Ã£ cÃ³ sáºµn Biome.
    /// - Äiá»ƒm sinh thÃ¡i náº¿u cÃ¡c Ã´ lÃ¡ng giá»ng xung quanh Ä‘Ã£ cÃ³ Biome.
    /// - Äiá»ƒm Báº¿n NÆ°á»›c MuÃ´n LoÃ i (Watering Hole) náº¿u cÃ¡c Ã´ lÃ¡ng giá»ng Ä‘Ã£ cÃ³ ThÃº.
    /// </summary>
    private int CalculateBurstScoreForNewRiver(HexCoordinates riverHex)
    {
        int score = riverConfig != null ? riverConfig.pointsPerRiverTile : 3;

        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen == null || worldGen.MapTiles == null) return score;

        // 1. Kiá»ƒm tra Ã´ táº¡i chá»— (Co-located Biome)
        if (worldGen.MapTiles.TryGetValue(riverHex, out GameObject centerTileObj) && centerTileObj != null)
        {
            if (HasBiomeOnTile(centerTileObj))
            {
                score += riverConfig != null ? riverConfig.pointsPerCoLocatedBiome : 2;
            }

            // ThÆ°á»Ÿng Báº¿n nÆ°á»›c náº¿u suá»‘i cháº£y qua Ã´ Ä‘ang cÃ³ thÃº sinh sá»‘ng
            if (HasCreatureOnTile(centerTileObj, riverHex))
            {
                score += riverConfig != null ? riverConfig.pointsPerAdjacentCreature : 5;
            }
        }

        // 2. QuÃ©t 6 Ã´ lÃ¡ng giá»ng xung quanh
        int adjacentCreatureCount = 0;
        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates nHex = riverHex.GetNeighbor(dir);
            if (worldGen.MapTiles.TryGetValue(nHex, out GameObject nTileObj) && nTileObj != null)
            {
                // ThÆ°á»Ÿng Biome lÃ¡ng giá»ng ven suá»‘i
                if (HasBiomeOnTile(nTileObj))
                {
                    score += riverConfig != null ? riverConfig.pointsPerAdjacentBiome : 1;
                }

                // ThÆ°á»Ÿng Báº¿n nÆ°á»›c muÃ´n loÃ i náº¿u cÃ³ thÃº sinh sá»‘ng
                if (HasCreatureOnTile(nTileObj))
                {
                    adjacentCreatureCount++;
                    score += riverConfig != null ? riverConfig.pointsPerAdjacentCreature : 5;
                }
            }
        }

        return score;
    }

    /// <summary>
    /// KÃ­ch hoáº¡t khi ngÆ°á»i chÆ¡i vá»«a Ä‘áº·t má»™t tháº» khÃ¡c (Biome hoáº·c Creature) lÃªn báº£n Ä‘á»“.
    /// Náº¿u tháº» nÃ y Ä‘Æ°á»£c Ä‘áº·t ká» sÃ¡t (hoáº·c Ä‘Ã¨ lÃªn) má»™t Ä‘oáº¡n suá»‘i Ä‘Ã£ cÃ³ sáºµn âž” KÃ­ch hoáº¡t Ä‘iá»ƒm thÆ°á»Ÿng nhá» giá»t (Incremental Score)!
    /// </summary>
    public void OnNeighborCardPlaced(HexCoordinates placedHex, CardData placedCard)
    {
        if (placedCard == null || riverConfig == null) return;

        EnsureConfig();
        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();
        int bonusScore = 0;

        // TRÆ¯á»œNG Há»¢P 1: Äáº¶T THáºº THÃš (CREATURE) SÃT Bá»œ SUá»I
        if (placedCard.cardType == CardType.Creature)
        {
            bool isOnRiver = _riverMap.ContainsKey(placedHex);
            int touchingRiverCount = 0;
            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates nHex = placedHex.GetNeighbor(dir);
                if (_riverMap.ContainsKey(nHex))
                {
                    touchingRiverCount++;
                }
            }

            if (isOnRiver || touchingRiverCount > 0)
            {
                // Äiá»ƒm báº¿n nÆ°á»›c muÃ´n loÃ i
                bonusScore += riverConfig.pointsPerAdjacentCreature;

                // ThÆ°á»Ÿng thÃªm "BÃ¡n Ä‘áº£o Ãªm Ä‘á»m" náº¿u Ä‘Æ°á»£c suá»‘i Ã´m tá»« 2 phÃ­a trá»Ÿ lÃªn
                if (touchingRiverCount >= 2)
                {
                    bonusScore += riverConfig.pointsPeninsulaBonus;
                }
            }
        }
        // TRÆ¯á»œNG Há»¢P 2: Äáº¶T THáºº Äá»ŠA HÃŒNH (TERRAIN / BIOME) TRÃŠN HOáº¶C SÃT SUá»I
        else if (placedCard.cardType == CardType.Terrain)
        {
            // Náº¿u Ä‘áº·t trá»±c tiáº¿p lÃªn Ã´ Ä‘Ã£ cÃ³ suá»‘i tá»« trÆ°á»›c
            if (_riverMap.ContainsKey(placedHex))
            {
                bonusScore += riverConfig.pointsPerCoLocatedBiome;
            }

            // Äiá»ƒm thÆ°á»Ÿng vá»›i cÃ¡c Ã´ suá»‘i lÃ¡ng giá»ng ká» sÃ¡t
            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates nHex = placedHex.GetNeighbor(dir);
                if (_riverMap.ContainsKey(nHex))
                {
                    bonusScore += riverConfig.pointsPerAdjacentBiome;
                }
            }
        }

        if (bonusScore > 0)
        {
            Vector3 popupPos = HexMetrics.HexToWorldPosition(placedHex, HexMetrics.TileHeight + 0.35f);
            ScorePopupManager.ShowScore(bonusScore, popupPos);

            if (ScoreCalculator.Instance != null)
            {
                ScoreCalculator.Instance.RecalculateScore();
            }
        }
    }

    // =========================================================================
    // TÃNH TOÃ€N Bá»˜ ÄIá»‚M SÃ”NG SUá»I (TOÃ€N Cáº¢NH CHO SCORECALCULATOR)
    // =========================================================================

    /// <summary>
    /// TÃ­nh tá»•ng Ä‘iá»ƒm toÃ n bá»™ máº¡ng lÆ°á»›i suá»‘i trÃªn toÃ n báº£n Ä‘á»“:
    /// Äáº£m báº£o káº¿t quáº£ LUÃ”N NHáº¤T QUÃN dÃ¹ ngÆ°á»i chÆ¡i Ä‘áº·t Suá»‘i trÆ°á»›c hay Biome/ThÃº trÆ°á»›c!
    /// </summary>
    public int CalculateTotalRiverScore()
    {
        if (riverConfig == null) EnsureConfig();
        if (_riverMap.Count == 0) return 0;

        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen == null || worldGen.MapTiles == null)
        {
            return _riverMap.Count * (riverConfig != null ? riverConfig.pointsPerRiverTile : 3);
        }

        int totalScore = 0;

        // 1. Äiá»ƒm cÆ¡ báº£n cho má»—i Ã´ suá»‘i
        totalScore += _riverMap.Count * (riverConfig != null ? riverConfig.pointsPerRiverTile : 3);

        // 2. Äiá»ƒm Biome táº¡i chá»— vÃ  Biome ven sÃ´ng
        HashSet<string> countedRiverBiomeEdges = new HashSet<string>();

        foreach (var kvp in _riverMap)
        {
            HexCoordinates riverHex = kvp.Key;

            // Kiá»ƒm tra Biome táº¡i chá»—
            if (worldGen.MapTiles.TryGetValue(riverHex, out GameObject centerTileObj) && centerTileObj != null)
            {
                if (HasBiomeOnTile(centerTileObj))
                {
                    totalScore += riverConfig.pointsPerCoLocatedBiome;
                }
            }

            // Kiá»ƒm tra Biome lÃ¡ng giá»ng
            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates nHex = riverHex.GetNeighbor(dir);
                if (worldGen.MapTiles.TryGetValue(nHex, out GameObject nTileObj) && nTileObj != null)
                {
                    if (HasBiomeOnTile(nTileObj))
                    {
                        string edgeKey = $"{riverHex.Q}_{riverHex.R}_{nHex.Q}_{nHex.R}";
                        if (!countedRiverBiomeEdges.Contains(edgeKey))
                        {
                            countedRiverBiomeEdges.Add(edgeKey);
                            totalScore += riverConfig.pointsPerAdjacentBiome;
                        }
                    }
                }
            }
        }

        // 3. Äiá»ƒm Báº¿n nÆ°á»›c muÃ´n loÃ i (Creature Watering Hole)
        // QuÃ©t táº¥t cáº£ thÃº trÃªn báº£n Ä‘á»“, xem má»—i con thÃº tiáº¿p giÃ¡p bao nhiÃªu Ã´ suá»‘i
        PlacedCard[] allCards = FindObjectsByType<PlacedCard>(FindObjectsInactive.Exclude);
        foreach (var card in allCards)
        {
            if (card == null || card.cardData == null || card.cardData.cardType != CardType.Creature)
                continue;
            bool isOnRiver = _riverMap.ContainsKey(card.placedHex);

            int touchingRivers = 0;
            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates nHex = card.placedHex.GetNeighbor(dir);
                if (_riverMap.ContainsKey(nHex))
                {
                    touchingRivers++;
                }
            }

            if (isOnRiver || touchingRivers > 0)
            {
                totalScore += riverConfig.pointsPerAdjacentCreature;
                if (touchingRivers >= 2)
                {
                    totalScore += riverConfig.pointsPeninsulaBonus;
                }
            }
        }

        return totalScore;
    }

    // =========================================================================
    // TIá»†N ÃCH KIá»‚M TRA Äá»ŠA HÃŒNH & THÃš
    // =========================================================================

    private bool HasBiomeOnTile(GameObject tileObj)
    {
        if (tileObj == null) return false;
        PlacedCard[] cards = tileObj.GetComponentsInChildren<PlacedCard>();
        foreach (var c in cards)
        {
            if (c != null && c.cardData != null && c.cardData.cardType == CardType.Terrain)
                return true;
        }
        return false;
    }

    private bool HasCreatureOnTile(GameObject tileObj, HexCoordinates? hex = null)
    {
        if (tileObj == null) return false;
        PlacedCard[] cards = tileObj.GetComponentsInChildren<PlacedCard>();
        foreach (var c in cards)
        {
            if (c != null && c.cardData != null && c.cardData.cardType == CardType.Creature)
                return true;
        }

        if (hex.HasValue && LumiWorld.Acs.HabitatRuntimeManager.TryGetActive(out var hm))
        {
            if (hm.HasCreatureAtHome(hex.Value)) return true;
        }
        return false;
    }

    // Stub tÆ°Æ¡ng thÃ­ch ngÆ°á»£c cho cÃ¡c cÃ´ng cá»¥ Inspector/Debug cÅ©
    public void RebuildAllRiverSplines() { }
}

