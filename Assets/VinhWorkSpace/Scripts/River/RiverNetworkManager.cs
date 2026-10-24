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
    /// Äáº·t má»™t Ä‘oáº¡n suá»‘i lÃªn Ã´ lá»¥c giÃ¡c, tá»± Ä‘á»™ng uá»‘n lÆ°á»£n ná»‘i vá»›i cÃ¡c Ã´ suá»‘i ká» bÃªn vÃ  tÃ­nh Ä‘iá»ƒm
    /// </summary>
    public PlacedCard PlaceRiver(HexCoordinates coords, GameObject targetTileObj, CardData cardData, Vector3? explicitSpawnPos = null)
    {
        EnsureConfig();
        if (targetTileObj == null) return null;

        // 1. XÃ¡c Ä‘á»‹nh Ä‘á»™ cao bá» máº·t trÃªn cÃ¹ng cá»§a Ã´ (topY) Ä‘á»ƒ suá»‘i khÃ´ng bá»‹ chÃ¬m xuá»‘ng dÆ°á»›i Ä‘áº¥t
        float surfaceY = HexBiomeClusterConnector.GetTileHeightLevels(targetTileObj).topY;
        Vector3 baseSurfacePos = explicitSpawnPos.HasValue 
            ? explicitSpawnPos.Value 
            : HexMetrics.HexToWorldPosition(coords, surfaceY);

        // 2. Táº¡o container cho Ä‘oáº¡n suá»‘i
        GameObject riverObj = new GameObject($"River_{coords.Q}_{coords.R}");
        riverObj.transform.SetParent(targetTileObj.transform, true);
        riverObj.transform.position = baseSurfacePos;

        RiverTileSegment segment = riverObj.AddComponent<RiverTileSegment>();
        segment.placedHex = coords;

        // TÃ­nh bÆ°á»›c dÃ²ng cháº£y (flowStep): Äáº§u nguá»“n = 0, Ã´ tiáº¿p theo = neighbor.flowStep + 1
        int minStep = int.MaxValue;
        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates neighbor = coords.GetNeighbor(dir);
            if (_riverMap.TryGetValue(neighbor, out RiverTileSegment nSeg) && nSeg != null)
            {
                // Nếu nSeg là Nguồn và nSeg đã có một láng giềng khác tạo thành tam giác với coords:
                // Theo yêu cầu dòng chảy 1 làn (A -> B -> C): coords sẽ nhận flowStep từ ô B thay vì từ nguồn A
                if (nSeg.isOrigin)
                {
                    bool sourceAlreadyFeedsTriangle = false;
                    for (int d2 = 0; d2 < 6; d2++)
                    {
                        HexCoordinates other = neighbor.GetNeighbor(d2);
                        if (other != coords && _riverMap.ContainsKey(other) && coords.DistanceTo(other) == 1)
                        {
                            sourceAlreadyFeedsTriangle = true;
                            break;
                        }
                    }
                    if (sourceAlreadyFeedsTriangle) continue;
                }

                if (nSeg.flowStep < minStep) minStep = nSeg.flowStep;
            }
        }
        bool isNewOrigin = (minStep == int.MaxValue);
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
                List<int> connectedDirs = new List<int>();
                int upstreamDir = -1;
                int outflowDir = -1;
                for (int d = 0; d < 6; d++)
                {
                    HexCoordinates n = hex.GetNeighbor(d);
                    if (_riverMap.TryGetValue(n, out RiverTileSegment nSeg) && nSeg != null)
                    {
                        // Kiểm tra ngắt cạnh trực tiếp giữa Nguồn và ô cuối trong tam giác 1 Nguồn + 2 Thường để giữ dòng 1 làn:
                        if (IsTriangleSourceLoopEdge(hex, seg, n, nSeg))
                        {
                            continue;
                        }

                        connectedDirs.Add(d);
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

                bool isOrigin = seg.isOrigin || (seg.flowStep == 0);

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

                // Kiểm tra xem ô này có phải tầng thấp nhất (Tier 0 / Bloomfield / Plain)
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

                RiverTileSolution solution = RiverAutoTileSolver.Solve(connectedDirs, isOrigin, downhillDir, isLowTierTerminal, upstreamDir, outflowDir);

                Debug.Log($"<color=#38BDF8>[RiverAutoTile]</color> Hex {hex} (isOrigin={isOrigin}, conns={solution.connectionCount}) -> Piece: <b>{solution.pieceType}</b>, RotY: <b>{solution.rotationY:F1}Â°</b>");

                Vector3 hexPos = HexMetrics.HexToWorldPosition(hex, hexSurfaceY);
                seg.ApplySolution(solution, riverConfig, hexPos, worldGen);

                // KÃ­ch hoáº¡t thuáº­t toÃ¡n ThÃ­ch á»©ng vÃ  TÃ¡i phÃ¢n bá»‘ Props sinh thÃ¡i (RiverPropHarmonizer):
                // Tá»± Ä‘á»™ng Ä‘áº©y dáº¡t bá»¥i hoa/cÃ¢y con ra hai bÃªn bá» suá»‘i/há»“ vÃ  thu nhá» dá»n sáº¡ch cÃ¢y cáº£n dÃ²ng nÆ°á»›c
                if (tileObj != null)
                {
                    bool isInitialSpring = isOrigin && (solution.connectionCount == 0);
                    float actualRotY = solution.rotationY + (riverConfig != null ? riverConfig.GetRotationOffsetForPiece(solution.pieceType) : 0f);
                    RiverPropHarmonizer.HarmonizePropsForRiver(tileObj, solution.pieceType, actualRotY, isInitialSpring, this, seg);
                }

                // LuÃ´n cáº­p nháº­t má»‘i ná»‘i SeamBridge kÃ­n rÃ£nh vá»›i cÃ¡c Ã´ lÃ¡ng giá»ng ká» sÃ¡t
                if (HexBiomeClusterConnector.Instance != null)
                {
                    HexBiomeClusterConnector.Instance.ConnectAllNearby(hex);
                }
            }
        }
    }

    /// <summary>
    /// Kiểm tra xem cạnh giữa h1 và h2 có phải là cạnh cần ngắt trong tam giác 1 Nguồn + 2 Thường không.
    /// Trường hợp 2: Khi có 1 Nguồn (A) và 2 ô thường (B, C) tạo thành tam giác:
    /// Dòng chảy đi 1 làn duy nhất (A -> B -> C). Cạnh trực tiếp giữa Nguồn A và C sẽ ngắt kết nối.
    /// </summary>
    private bool IsTriangleSourceLoopEdge(HexCoordinates h1, RiverTileSegment s1, HexCoordinates h2, RiverTileSegment s2)
    {
        if (s1 == null || s2 == null) return false;

        int dir1to2 = -1;
        for (int d = 0; d < 6; d++)
        {
            if (h1.GetNeighbor(d) == h2) { dir1to2 = d; break; }
        }
        if (dir1to2 == -1) return false;

        HexCoordinates[] thirdCandidates = new HexCoordinates[]
        {
            h1.GetNeighbor((dir1to2 + 1) % 6),
            h1.GetNeighbor((dir1to2 + 5) % 6)
        };

        foreach (var h3 in thirdCandidates)
        {
            if (_riverMap.TryGetValue(h3, out RiverTileSegment s3) && s3 != null)
            {
                int originCount = (s1.isOrigin ? 1 : 0) + (s2.isOrigin ? 1 : 0) + (s3.isOrigin ? 1 : 0);
                if (originCount == 1)
                {
                    if (s1.isOrigin)
                    {
                        if (s3.flowStep <= s2.flowStep) return true;
                    }
                    else if (s2.isOrigin)
                    {
                        if (s3.flowStep <= s1.flowStep) return true;
                    }
                }
            }
        }

        return false;
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
            int touchingRiverCount = 0;
            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates nHex = placedHex.GetNeighbor(dir);
                if (_riverMap.ContainsKey(nHex))
                {
                    touchingRiverCount++;
                }
            }

            if (touchingRiverCount > 0)
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

            int touchingRivers = 0;
            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates nHex = card.placedHex.GetNeighbor(dir);
                if (_riverMap.ContainsKey(nHex))
                {
                    touchingRivers++;
                }
            }

            if (touchingRivers > 0)
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

    private bool HasCreatureOnTile(GameObject tileObj)
    {
        if (tileObj == null) return false;
        PlacedCard[] cards = tileObj.GetComponentsInChildren<PlacedCard>();
        foreach (var c in cards)
        {
            if (c != null && c.cardData != null && c.cardData.cardType == CardType.Creature)
                return true;
        }
        return false;
    }

    // Stub tÆ°Æ¡ng thÃ­ch ngÆ°á»£c cho cÃ¡c cÃ´ng cá»¥ Inspector/Debug cÅ©
    public void RebuildAllRiverSplines() { }
}

