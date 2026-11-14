using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// QuÃ¡ÂºÂ£n lÃƒÂ½ toÃƒÂ n bÃ¡Â»â„¢ mÃ¡ÂºÂ¡ng lÃ†Â°Ã¡Â»â€ºi sÃƒÂ´ng suÃ¡Â»â€˜i trÃƒÂªn bÃ¡ÂºÂ£n Ã„â€˜Ã¡Â»â€œ (Preserve / Dorfromantik style).
/// - QuÃ¡ÂºÂ£n lÃƒÂ½ Ã„â€˜Ã„Æ’ng kÃƒÂ½ cÃƒÂ¡c Ã„â€˜oÃ¡ÂºÂ¡n suÃ¡Â»â€˜i (RiverTileSegment).
/// - TÃ¡Â»Â± Ã„â€˜Ã¡Â»â„¢ng kÃ¡ÂºÂ¿t nÃ¡Â»â€˜i, xoay vÃƒÂ  nÃƒÂ¢ng cÃ¡ÂºÂ¥p mÃƒÂ´ hÃƒÂ¬nh khi cÃƒÂ¡c Ã„â€˜oÃ¡ÂºÂ¡n suÃ¡Â»â€˜i chÃ¡ÂºÂ¡m nhau.
/// - CÃ†Â¡ chÃ¡ÂºÂ¿ tÃƒÂ­nh Ã„â€˜iÃ¡Â»Æ’m phÃ¡ÂºÂ£n Ã¡Â»Â©ng hai chiÃ¡Â»Âu (Bi-directional Reactive Scoring) giÃ¡Â»Â¯a SuÃ¡Â»â€˜i, Biome vÃƒÂ  ThÃƒÂº (BÃ¡ÂºÂ¿n nÃ†Â°Ã¡Â»â€ºc muÃƒÂ´n loÃƒÂ i).
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

    // BÃ¡ÂºÂ£n Ã„â€˜Ã¡Â»â€œ lÃ†Â°u trÃ¡Â»Â¯ cÃƒÂ¡c ÃƒÂ´ lÃ¡Â»Â¥c giÃƒÂ¡c Ã„â€˜ÃƒÂ£ cÃƒÂ³ suÃ¡Â»â€˜i
    private readonly Dictionary<HexCoordinates, RiverTileSegment> _riverMap = new Dictionary<HexCoordinates, RiverTileSegment>();

    // QuÃ¡ÂºÂ£n lÃƒÂ½ cÃƒÂ¡c tuyÃ¡ÂºÂ¿n sÃƒÂ´ng liÃƒÂªn tÃ¡Â»Â¥c tÃ¡Â»Â« nguÃ¡Â»â€œn bÃ¡ÂºÂ±ng Unity Splines
    private readonly Dictionary<HexCoordinates, GameObject> _continuousRiverObjects = new Dictionary<HexCoordinates, GameObject>();
    private int _placementCounter = 0;

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
    /// Ã„ÂÃ¡ÂºÂ£m bÃ¡ÂºÂ£o luÃƒÂ´n cÃƒÂ³ RiverConfig hÃ¡Â»Â£p lÃ¡Â»â€¡ (tÃ¡Â»Â± tÃƒÂ¬m trong Resources hoÃ¡ÂºÂ·c gÃƒÂ¡n fallback)
    /// </summary>
    private void EnsureConfig()
    {
        if (riverConfig == null)
        {
            riverConfig = Resources.Load<RiverConfig>("RiverConfig");
            if (riverConfig == null)
            {
                // TÃ¡Â»Â± sinh 1 ScriptableObject runtime nÃ¡ÂºÂ¿u chÃ†Â°a cÃƒÂ³ asset
                riverConfig = ScriptableObject.CreateInstance<RiverConfig>();
            }
        }
    }

    /// <summary>
    /// QuÃƒÂ©t toÃƒÂ n bÃ¡Â»â„¢ Scene khi khÃ¡Â»Å¸i Ã„â€˜Ã¡Â»â„¢ng Ã„â€˜Ã¡Â»Æ’ thu thÃ¡ÂºÂ­p cÃƒÂ¡c ÃƒÂ´ suÃ¡Â»â€˜i Ã„â€˜ÃƒÂ£ Ã„â€˜Ã†Â°Ã¡Â»Â£c nÃ¡ÂºÂ¡p sÃ¡ÂºÂµn tÃ¡Â»Â« file Save
    /// </summary>
    public void IndexExistingRivers()
    {
        _riverMap.Clear();
        RiverTileSegment[] existingSegments = FindObjectsByType<RiverTileSegment>(FindObjectsInactive.Exclude);
        foreach (var seg in existingSegments)
        {
            if (seg != null && !_riverMap.ContainsKey(seg.placedHex))
            {
                if (seg.approvedPortsMask == 0 && seg.connectedPortsMask != 0) seg.approvedPortsMask = seg.connectedPortsMask;
                _riverMap.Add(seg.placedHex, seg);
            }
        }
    }

    /// <summary>
    /// KiÃ¡Â»Æ’m tra xem ÃƒÂ´ lÃ¡Â»Â¥c giÃƒÂ¡c Ã„â€˜ÃƒÂ£ cÃƒÂ³ Ã„â€˜oÃ¡ÂºÂ¡n suÃ¡Â»â€˜i nÃƒÂ o chÃ¡ÂºÂ£y qua chÃ†Â°a
    /// </summary>
    public bool HasRiver(HexCoordinates coords)
    {
        return _riverMap.ContainsKey(coords);
    }

    /// <summary>
    /// LÃ¡ÂºÂ¥y Ã„â€˜oÃ¡ÂºÂ¡n suÃ¡Â»â€˜i tÃ¡ÂºÂ¡i tÃ¡Â»Âa Ã„â€˜Ã¡Â»â„¢ chÃ¡Â»â€° Ã„â€˜Ã¡Â»â€¹nh
    /// </summary>
    public RiverTileSegment GetRiver(HexCoordinates coords)
    {
        _riverMap.TryGetValue(coords, out RiverTileSegment seg);
        return seg;
    }

    // =========================================================================
    // Ã„ÂÃ¡ÂºÂ¶T BÃƒâ‚¬I SÃƒâ€NG SUÃ¡Â»ÂI (RIVER PLACEMENT & AUTO-TILING)
    // =========================================================================

    /// <summary>
    /// DÃ¡Â»Ân dÃ¡ÂºÂ¹p vÃƒÂ  Ã„â€˜iÃ¡Â»Âu chÃ¡Â»â€°nh cÃƒÂ¢y cÃ¡Â»â€˜i/props trÃƒÂªn ÃƒÂ´ khi Ã„â€˜Ã¡ÂºÂ·t suÃ¡Â»â€˜i xuÃ¡Â»â€˜ng Ã„â€˜Ã¡Â»Æ’ nÃ†Â°Ã¡Â»â€ºc khÃƒÂ´ng bÃ¡Â»â€¹ Ã„â€˜ÃƒÂ¨ xuyÃƒÂªn qua cÃƒÂ¢y,
    /// Ã„â€˜Ã¡Â»â€œng thÃ¡Â»Âi dÃ¡ÂºÂ¡t cÃƒÂ¡c bÃ¡Â»Â¥i hoa vÃƒÂ  thÃ¡Â»Â±c vÃ¡ÂºÂ­t nhÃ¡Â»Â ra hai bÃƒÂªn bÃ¡Â»Â suÃ¡Â»â€˜i/hÃ¡Â»â€œ theo thuÃ¡ÂºÂ­t toÃƒÂ¡n RiverPropHarmonizer.
    /// </summary>
    public static void ClearConflictingPropsForRiver(GameObject tileObj)
    {
        if (tileObj == null) return;
        RiverPropHarmonizer.HarmonizePropsForRiver(tileObj, RiverPieceType.SpringHead, 0f, true, null);
    }

    /// <summary>
    /// Äáº·t má»™t Ä‘oáº¡n suá»‘i lÃªn Ã´ lá»¥c giÃ¡c, tá»± Ä‘á»™ng uá»‘n lÆ°á»£n ná»‘i vá»›i cÃ¡c Ã´ suá»‘i ká» bÃªn vÃ  tÃ­nh Ä‘iá»ƒm
    /// </summary>
    /// <param name="restoring">true khi WorldSaveSystem dá»±ng láº¡i tá»« báº£n lÆ°u: khÃ´ng tÃ­nh Ä‘iá»ƒm thÆ°á»Ÿng tá»©c thÃ¬, khÃ´ng hiá»‡n popup, khÃ´ng tÃ­nh láº¡i tá»•ng Ä‘iá»ƒm.</param>
    public PlacedCard PlaceRiver(HexCoordinates coords, GameObject targetTileObj, CardData cardData, Vector3? explicitSpawnPos = null, bool restoring = false)
    {
        EnsureConfig();
        if (targetTileObj == null) return null;

        // 1. XÃ¡c Ä‘á»‹nh Ä‘á»™ cao bá» máº·t trÃªn cÃ¹ng cá»§a Ã´ (topY)
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

        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();

        // TÃ­nh bÆ°á»›c dÃ²ng cháº£y (flowStep): Äáº§u nguá»“n = 0, Ã´ tiáº¿p theo = neighbor.flowStep + 1
        int minStep = int.MaxValue;
        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates neighbor = coords.GetNeighbor(dir);
            if (_riverMap.TryGetValue(neighbor, out RiverTileSegment nSeg) && nSeg != null)
            {
                // LUáº¬T TRá»ŒNG Lá»°C: NÆ°á»›c khÃ´ng thá»ƒ cháº£y ngÆ°á»£c dá»‘c tá»« Ã´ tháº¥p hÆ¡n lÃªn Ã´ má»›i cao hÆ¡n!
                float nY = GetTileTopY(neighbor, worldGen);
                if (nY < surfaceY - 0.35f)
                {
                    continue; // Ã” lÃ¡ng giá»ng tháº¥p hÆ¡n khÃ´ng thá»ƒ cáº¥p nÆ°á»›c leo dá»‘c lÃªn Ã´ cao má»›i Ä‘áº·t!
                }

                // KhÃ´ng nháº­n flowStep tá»« Ã´ lÃ¡ng giá»ng náº¿u vi pháº¡m luáº­t ná»‘i suá»‘i/thÃ¡c
                if (!ShouldConnectRivers(coords, segment, neighbor, nSeg, worldGen))
                {
                    continue;
                }

                if (nSeg.flowStep < minStep) minStep = nSeg.flowStep;
            }
        }
        bool isNewOrigin = (minStep == int.MaxValue) && (surfaceY >= 1.35f);
        segment.isOrigin = isNewOrigin;
        segment.flowStep = isNewOrigin ? 0 : (minStep + 1);
        segment.placementOrder = ++_placementCounter;
        _riverMap[coords] = segment;
        AssignApprovedConnectionsForNewTile(coords, segment, worldGen);

        // 3. GÃ¡ÂºÂ¯n PlacedCard Ã„â€˜Ã¡Â»Æ’ hÃ¡Â»â€¡ thÃ¡Â»â€˜ng Save/Load vÃƒÂ  Ã„â€˜iÃ¡Â»Æ’m sÃ¡Â»â€˜ nhÃ¡ÂºÂ­n diÃ¡Â»â€¡n
        PlacedCard placedCard = riverObj.AddComponent<PlacedCard>();
        placedCard.cardData = cardData;
        placedCard.placedHex = coords;

        // 4. CÃ¡ÂºÂ­p nhÃ¡ÂºÂ­t Auto-Tiling cho ÃƒÂ´ nÃƒÂ y vÃƒÂ  toÃƒÂ n bÃ¡Â»â„¢ cÃƒÂ¡c ÃƒÂ´ lÃƒÂ¡ng giÃ¡Â»Âng kÃ¡Â»Â sÃƒÂ¡t
        RefreshTileAndNeighbors(coords, baseSurfacePos);

        // Äang náº¡p báº£n lÆ°u: Ä‘iá»ƒm hiá»ƒn thá»‹ láº¥y tá»« record, tá»•ng Ä‘iá»ƒm WorldSaveSystem tÃ­nh láº¡i má»™t láº§n á»Ÿ cuá»‘i
        if (restoring) return placedCard;

        // 5. TÃƒÂ­nh toÃƒÂ¡n Ã„â€˜iÃ¡Â»Æ’m thÃ†Â°Ã¡Â»Å¸ng nÃ¡Â»â€¢ tÃ¡Â»Â©c thÃƒÂ¬ khi Ã„â€˜Ã¡ÂºÂ·t suÃ¡Â»â€˜i (Burst Score)
        int burstScore = CalculateBurstScoreForNewRiver(coords);
        if (burstScore > 0)
        {
            placedCard.displayedScore = burstScore;
            Vector3 popupPos = HexMetrics.HexToWorldPosition(coords, surfaceY + 0.35f);
            ScorePopupManager.ShowScore(burstScore, popupPos);
        }

        // 6. CÃ¡ÂºÂ­p nhÃ¡ÂºÂ­t tÃ¡Â»â€¢ng Ã„â€˜iÃ¡Â»Æ’m
        if (ScoreCalculator.Instance != null)
        {
            ScoreCalculator.Instance.RecalculateScore();
        }

        return placedCard;
    }

    /// <summary>
    /// LÃƒÂ m mÃ¡Â»â€ºi loÃ¡ÂºÂ¡i hÃƒÂ¬nh hÃ¡Â»Âc (Straight, Curve, Fork...) vÃƒÂ  gÃƒÂ³c xoay cÃ¡Â»Â§a ÃƒÂ´ chÃ¡Â»â€° Ã„â€˜Ã¡Â»â€¹nh vÃƒÂ  cÃƒÂ¡c ÃƒÂ´ suÃ¡Â»â€˜i kÃ¡Â»Â bÃƒÂªn
    /// </summary>
    public void RefreshTileAndNeighbors(HexCoordinates centerCoords, Vector3 centerWorldPos, bool includeNeighbors = true)
    {
        List<HexCoordinates> toRefresh = new List<HexCoordinates> { centerCoords };

        for (int dir = 0; dir < 6 && includeNeighbors; dir++)
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
                // XÃ¡c Ä‘á»‹nh Ä‘á»™ cao bá» máº·t cá»§a Ã´ hiá»‡n táº¡i vÃ  kiá»ƒm tra táº§ng tháº¥p nháº¥t
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
                        if (!seg.HasApprovedPort(d) || !nSeg.HasApprovedPort((d + 3) % 6))
                        {
                            continue;
                        }
                        if (!ShouldConnectRivers(hex, seg, n, nSeg, worldGen))
                        {
                            continue;
                        }

                        connectedDirs.Add(d);
                        float nTopY = GetTileTopY(n, worldGen);

                        // Æ¯u tiÃªn xÃ¡c Ä‘á»‹nh hÆ°á»›ng dÃ²ng cháº£y dá»±a trÃªn Äá»˜ CAO THá»°C Táº¾:
                        if (Mathf.Abs(nTopY - hexSurfaceY) > 0.35f)
                        {
                            if (nTopY > hexSurfaceY)
                            {
                                if (upstreamDir < 0 || d == seg.primaryPortA) upstreamDir = d; // NÆ°á»›c tá»« Ã´ cao hÆ¡n Ä‘á»• xuá»‘ng Ã´ nÃ y
                            }
                            else
                            {
                                if (outflowDir < 0 || d == seg.primaryPortB) outflowDir = d;  // NÆ°á»›c tá»« Ã´ nÃ y Ä‘á»• xuá»‘ng Ã´ tháº¥p hÆ¡n
                            }
                        }
                        else
                        {
                            // CÃ¹ng Ä‘á»™ cao báº±ng pháº³ng: So sÃ¡nh theo bÆ°á»›c dÃ²ng cháº£y flowStep
                            if (nSeg.flowStep < seg.flowStep)
                            {
                                if (upstreamDir < 0 || d == seg.primaryPortA)
                                {
                                    upstreamDir = d;
                                }
                                else if (upstreamDir != seg.primaryPortA)
                                {
                                    var curSeg = _riverMap.TryGetValue(hex.GetNeighbor(upstreamDir), out var curUp) ? curUp : null;
                                    if (curSeg != null && nSeg.placementOrder < curSeg.placementOrder)
                                    {
                                        upstreamDir = d;
                                    }
                                }
                            }
                            else if (nSeg.flowStep > seg.flowStep)
                            {
                                if (outflowDir < 0 || d == seg.primaryPortB)
                                {
                                    outflowDir = d;
                                }
                                else if (outflowDir != seg.primaryPortB)
                                {
                                    var curSeg = _riverMap.TryGetValue(hex.GetNeighbor(outflowDir), out var curOut) ? curOut : null;
                                    if (curSeg != null && nSeg.placementOrder < curSeg.placementOrder)
                                    {
                                        outflowDir = d;
                                    }
                                }
                            }
                        }
                    }
                }

                // Cáº­p nháº­t tráº¡ng thÃ¡i nguá»“n: Má»™t Ã´ chá»‰ lÃ  Äáº§u nguá»“n (SpringHead) náº¿u KHÃ”NG cÃ³ nÆ°á»›c tá»« Ã´ khÃ¡c Ä‘á»• vÃ o
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

                // TÃ¬m hÆ°á»›ng dá»‘c xuá»‘ng cho Ã´ Ä‘áº§u nguá»“n náº¿u chÆ°a cÃ³ káº¿t ná»‘i nÃ o
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

                RiverTileSolution solution = RiverAutoTileSolver.Solve(connectedDirs, isOrigin, downhillDir, isLowTierTerminal, upstreamDir, outflowDir, seg.primaryPortA, seg.primaryPortB);

                Debug.Log($"<color=#38BDF8>[RiverAutoTile]</color> Hex {hex} (isOrigin={isOrigin}, conns={solution.connectionCount}) -> Piece: <b>{solution.pieceType}</b>, RotY: <b>{solution.rotationY:F1}Â°</b>");

                Vector3 hexPos = HexMetrics.HexToWorldPosition(hex, hexSurfaceY);
                seg.ApplySolution(solution, riverConfig, hexPos, worldGen);

                // KÃ­ch hoáº¡t thuáº­t toÃ¡n ThÃ­ch á»©ng vÃ  TÃ¡i phÃ¢n bá»‘ Props sinh thÃ¡i (RiverPropHarmonizer):
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

                // Náº¿u Ã´ nÃ y Ä‘Ã£ cÃ³ Settlement, cáº­p nháº­t láº¡i quy hoáº¡ch Ä‘á»ƒ building tá»± nÃ© dÃ²ng suá»‘i má»›i
                if (SettlementClusterManager.Instance != null && SettlementClusterManager.Instance.HasSettlement(hex))
                {
                    SettlementClusterManager.Instance.RefreshSettlementAt(hex);
                }
            }
        }
    }

    /// <summary>
    /// Dá»±ng láº¡i má»™t Ã´ sÃ´ng tá»« báº£n lÆ°u. CÃ³ flowStep Ä‘Ã£ lÆ°u (>= 0) thÃ¬ chá»‰ gáº¯n dá»¯ liá»‡u, chÆ°a chá»n hÃ¬nh:
    /// gá»i RefreshAllRivers sau khi náº¡p xong má»i Ã´ Ä‘á»ƒ káº¿t quáº£ khÃ´ng phá»¥ thuá»™c thá»© tá»± náº¡p.
    /// Báº£n lÆ°u cÅ© (savedFlowStep = -1) thÃ¬ Ä‘áº·t láº¡i nhÆ° lÃºc chÆ¡i, khÃ´ng tÃ­nh Ä‘iá»ƒm.
    /// </summary>
    public PlacedCard RestoreRiver(HexCoordinates coords, GameObject targetTileObj, CardData cardData, int savedFlowStep, bool savedIsOrigin)
    {
        if (savedFlowStep < 0) return PlaceRiver(coords, targetTileObj, cardData, null, restoring: true);

        EnsureConfig();
        if (targetTileObj == null) return null;

        float surfaceY = HexBiomeClusterConnector.GetTileHeightLevels(targetTileObj).topY;
        GameObject riverObj = new GameObject($"River_{coords.Q}_{coords.R}");
        riverObj.transform.SetParent(targetTileObj.transform, true);
        riverObj.transform.position = HexMetrics.HexToWorldPosition(coords, surfaceY);

        RiverTileSegment segment = riverObj.AddComponent<RiverTileSegment>();
        segment.placedHex = coords;
        segment.flowStep = savedFlowStep;
        segment.isOrigin = savedIsOrigin;
        segment.placementOrder = ++_placementCounter;
        _riverMap[coords] = segment;
        // restored segment will be connected by RefreshAllRivers()

        PlacedCard placedCard = riverObj.AddComponent<PlacedCard>();
        placedCard.cardData = cardData;
        placedCard.placedHex = coords;
        return placedCard;
    }

    /// <summary>
    /// Chá»n láº¡i hÃ¬nh vÃ  gÃ³c xoay cho má»i Ã´ sÃ´ng, má»—i Ã´ má»™t láº§n, Ä‘i tá»« Ä‘áº§u nguá»“n xuá»‘ng (flowStep tÄƒng dáº§n).
    /// </summary>
    [ContextMenu("Refresh All Rivers")]
    public void RefreshAllRivers()
    {
        List<RiverTileSegment> segments = new List<RiverTileSegment>();
        foreach (RiverTileSegment seg in _riverMap.Values)
            if (seg != null) segments.Add(seg);
        segments.Sort((a, b) =>
        {
            int byStep = a.flowStep.CompareTo(b.flowStep);
            if (byStep != 0) return byStep;
            int byQ = a.placedHex.Q.CompareTo(b.placedHex.Q);
            return byQ != 0 ? byQ : a.placedHex.R.CompareTo(b.placedHex.R);
        });
        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();
        foreach (RiverTileSegment seg in segments)
            seg.approvedPortsMask = 0;
        foreach (RiverTileSegment seg in segments)
            AssignApprovedConnectionsForNewTile(seg.placedHex, seg, worldGen);
        foreach (RiverTileSegment seg in segments)
            RefreshTileAndNeighbors(seg.placedHex, seg.transform.position, includeNeighbors: false);
    }

    private struct RiverCandidateEdge
    {
        public int dir;
        public HexCoordinates neighbor;
        public RiverTileSegment nSeg;
        public int priorityTier;
        public int placementOrder;
    }

    /// <summary>
    /// Chá»n vÃ  duyá»‡t cÃ¡c káº¿t ná»‘i há»£p lá»‡ cho Ã´ suá»‘i má»›i Ä‘áº·t theo thá»© tá»± Æ°u tiÃªn rÃµ rÃ ng,
    /// Ä‘á»“ng thá»i NGÄ‚N CHáº¶N TUYá»†T Äá»I viá»‡c ná»‘i thÃ nh vÃ²ng khÃ©p kÃ­n (Cycle Prevention).
    /// </summary>
    /// <summary>
    /// Kiểm tra trước khi đặt: liệu ô tại coords có thể nối hợp lệ với ít nhất 1 ô suối láng giềng hay không
    /// (dùng cho Validator để tránh đặt ra một hồ cô lập ở tầng thấp rồi thành 2 hồ nối nhau).
    /// </summary>
    public bool CanFormAnyConnection(HexCoordinates coords, float myY, HexWorldGenerator worldGen = null)
    {
        if (worldGen == null) worldGen = FindAnyObjectByType<HexWorldGenerator>();

        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates neighbor = coords.GetNeighbor(dir);
            if (!_riverMap.TryGetValue(neighbor, out RiverTileSegment nSeg) || nSeg == null) continue;

            float nY = GetTileTopY(neighbor, worldGen);
            if (nY < myY - 0.35f)
            {
                // Nước không chảy ngược từ ô thấp hơn lên ô cao hơn
                continue;
            }

            if (nY > myY + 0.35f)
            {
                // Chỉ nhận thác nếu neighbor là nguồn tốt nhất từ tầng trên và không tạo thác kép cạnh nhau
                HexCoordinates bestUp = GetBestUpstreamFeeder(coords, worldGen);
                if (bestUp != neighbor && bestUp != coords) continue;
                bool hasParallelWaterfall = false;
                for (int d = 0; d < 6; d++)
                {
                    HexCoordinates hC = coords.GetNeighbor(d);
                    if (_riverMap.TryGetValue(hC, out RiverTileSegment segC) && segC != null && neighbor.DistanceTo(hC) == 1)
                    {
                        float yC = GetTileTopY(hC, worldGen);
                        if (Mathf.Abs(yC - myY) <= 0.35f)
                        {
                            hasParallelWaterfall = true;
                            break;
                        }
                    }
                }
                if (hasParallelWaterfall) continue;
            }

            return true;
        }

        return false;
    }

    private void AssignApprovedConnectionsForNewTile(HexCoordinates coords, RiverTileSegment segment, HexWorldGenerator worldGen)
    {
        if (segment == null) return;

        float myY = GetTileTopY(coords, worldGen);
        List<RiverCandidateEdge> candidates = new List<RiverCandidateEdge>();

        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates neighbor = coords.GetNeighbor(dir);
            if (!_riverMap.TryGetValue(neighbor, out RiverTileSegment nSeg) || nSeg == null) continue;

            float nY = GetTileTopY(neighbor, worldGen);

            if (nY > myY + 0.35f)
            {
                if (GetBestUpstreamFeeder(coords, worldGen) != neighbor) continue;
                if (!ShouldConnectRivers(coords, segment, neighbor, nSeg, worldGen)) continue;
                candidates.Add(new RiverCandidateEdge
                {
                    dir = dir,
                    neighbor = neighbor,
                    nSeg = nSeg,
                    priorityTier = 0,
                    placementOrder = nSeg.placementOrder
                });
            }
            else if (nY < myY - 0.35f)
            {
                if (GetBestUpstreamFeeder(neighbor, worldGen) != coords) continue;
                if (!ShouldConnectRivers(coords, segment, neighbor, nSeg, worldGen)) continue;
                candidates.Add(new RiverCandidateEdge
                {
                    dir = dir,
                    neighbor = neighbor,
                    nSeg = nSeg,
                    priorityTier = 3,
                    placementOrder = nSeg.placementOrder
                });
            }
            else
            {
                if (!ShouldConnectRivers(coords, segment, neighbor, nSeg, worldGen)) continue;

                int approvedCount = CountBits(nSeg.approvedPortsMask);
                bool isOpenTip = (nSeg.pieceType == RiverPieceType.PondBasin && approvedCount <= 1) ||
                                 (nSeg.pieceType == RiverPieceType.SpringHead && approvedCount == 0);

                candidates.Add(new RiverCandidateEdge
                {
                    dir = dir,
                    neighbor = neighbor,
                    nSeg = nSeg,
                    priorityTier = isOpenTip ? 1 : 2,
                    placementOrder = nSeg.placementOrder
                });
            }
        }

        candidates.Sort((a, b) =>
        {
            int byTier = a.priorityTier.CompareTo(b.priorityTier);
            if (byTier != 0) return byTier;
            return b.placementOrder.CompareTo(a.placementOrder);
        });

        foreach (var cand in candidates)
        {
            if (segment.HasApprovedPort(cand.dir)) continue;
            if (AreInSameRiverComponent(coords, cand.neighbor)) continue;

            int oppDir = (cand.dir + 3) % 6;
            segment.approvedPortsMask |= (1 << cand.dir);
            cand.nSeg.approvedPortsMask |= (1 << oppDir);
        }
    }

    private bool AreInSameRiverComponent(HexCoordinates start, HexCoordinates target)
    {
        if (start == target) return true;
        Queue<HexCoordinates> queue = new Queue<HexCoordinates>();
        HashSet<HexCoordinates> visited = new HashSet<HexCoordinates>();
        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            HexCoordinates cur = queue.Dequeue();
            if (!_riverMap.TryGetValue(cur, out RiverTileSegment curSeg) || curSeg == null) continue;

            for (int d = 0; d < 6; d++)
            {
                if (!curSeg.HasApprovedPort(d)) continue;
                HexCoordinates next = cur.GetNeighbor(d);
                if (next == target) return true;
                if (!visited.Contains(next) && _riverMap.TryGetValue(next, out RiverTileSegment nextSeg) && nextSeg != null && nextSeg.HasApprovedPort((d + 3) % 6))
                {
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
        }
        return false;
    }

    private static int CountBits(int mask)
    {
        int c = 0;
        while (mask != 0)
        {
            c += (mask & 1);
            mask >>= 1;
        }
        return c;
    }

    /// <summary>
    /// Láº¥y Ä‘á»™ cao bá» máº·t thá»±c táº¿ cá»§a Ã´ lá»¥c giÃ¡c
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
    /// Kiá»ƒm tra xem cáº¡nh giá»¯a h1 vÃ  h2 cÃ³ pháº£i lÃ  cáº¡nh cáº§n ngáº¯t trong báº¥t ká»³ tam giÃ¡c suá»‘i 3 Ã´ nÃ o khÃ´ng.
    /// Má»™t tam giÃ¡c suá»‘i (3 Ã´ ká» nhau) luÃ´n táº¡o thÃ nh vÃ²ng láº·p / máº¡ng nhá»‡n máº¥t tá»± nhiÃªn,
    /// do Ä‘Ã³ báº¯t buá»™c pháº£i ngáº¯t 1 cáº¡nh Ä‘á»ƒ chá»‰ giá»¯ láº¡i dÃ²ng cháº£y 1 lÃ n duy nháº¥t (Clean Flow).
    /// </summary>
    /// <summary>
    /// Kiá»ƒm tra xem 2 Ã´ lá»¥c giÃ¡c lÃ¡ng giá»ng ká» sÃ¡t cÃ³ káº¿t ná»‘i suá»‘i há»£p lá»‡ khÃ´ng.
    /// TuÃ¢n thá»§ quy luáº­t dÃ²ng cháº£y tá»± nhiÃªn (Clean Flow & Gravity):
    /// 1. NÆ°á»›c luÃ´n cháº£y xuÃ´i dá»‘c tá»« táº§ng cao xuá»‘ng táº§ng tháº¥p.
    /// 2. CÃ¹ng má»™t táº§ng pháº³ng:
    ///    - Hai Ã´ KHÃ”NG ná»‘i ngang vá»›i nhau náº¿u cáº£ 2 Ä‘á»u Ä‘Ã£ nháº­n nÆ°á»›c tá»« táº§ng trÃªn cao Ä‘á»• xuá»‘ng (2 nhÃ¡nh táº½ riÃªng biá»‡t).
    ///    - Hai Ã´ KHÃ”NG ná»‘i ngang vá»›i nhau náº¿u cáº£ 2 Ä‘á»u cÃ¹ng Ä‘á»• xuá»‘ng chung má»™t Ã´ trÅ©ng bÃªn dÆ°á»›i (2 nhÃ¡nh há»£p lÆ°u riÃªng biá»‡t).
    ///    - Trong tam giÃ¡c 3 Ã´ cÃ¹ng táº§ng: Ngáº¯t cáº¡nh trá»±c tiáº¿p giá»¯a Ä‘iá»ƒm Ä‘áº§u vÃ  Ä‘iá»ƒm cuá»‘i Ä‘á»ƒ dÃ²ng suá»‘i Ä‘i 1 lÃ n A -> B -> C.
    /// </summary>
    private bool ShouldConnectRivers(HexCoordinates hA, RiverTileSegment segA, HexCoordinates hB, RiverTileSegment segB, HexWorldGenerator worldGen)
    {
        if (segA == null || segB == null) return false;

        float yA = GetTileTopY(hA, worldGen);
        float yB = GetTileTopY(hB, worldGen);

        // TrÃ†Â°Ã¡Â»Âng hÃ¡Â»Â£p 1: ChÃƒÂªnh lÃ¡Â»â€¡ch Ã„â€˜Ã¡Â»â„¢ cao giÃ¡Â»Â¯a 2 tÃ¡ÂºÂ§ng (DÃ¡Â»â€˜c / ThÃƒÂ¡c nÃ†Â°Ã¡Â»â€ºc)
        if (Mathf.Abs(yA - yB) > 0.35f)
        {
            HexCoordinates upperHex = (yA > yB) ? hA : hB;
            HexCoordinates lowerHex = (yA > yB) ? hB : hA;
            RiverTileSegment upperSeg = (yA > yB) ? segA : segB;
            RiverTileSegment lowerSeg = (yA > yB) ? segB : segA;
            float lowerY = Mathf.Min(yA, yB);

            // Ãƒâ€ Ã¡Â»Å¸ tÃ¡ÂºÂ§ng thÃ¡ÂºÂ¥p hÃ†Â¡n CHÃ¡Â»Ë† Ã„ÂÃ†Â¯Ã¡Â»Â¢C PHÃƒâ€°P nhÃ¡ÂºÂ­n nÃ†Â°Ã¡Â»â€ºc tÃ¡Â»Â« Ã„ÂÃƒÅ¡NG 1 Ãƒâ€ Ã¡Â»Å¸ tÃ¡ÂºÂ§ng cao hÃ†Â¡n:
            if (GetBestUpstreamFeeder(lowerHex, worldGen) != upperHex)
            {
                return false;
            }

            // QUY TÃ¡ÂºÂ®C CHÃ¡Â»ÂNG THÃƒÂC NÃ†Â¯Ã¡Â»Å¡C KÃƒâ€°P CÃ¡ÂºÂ NH NHAU (Anti-Parallel Waterfalls):
            // Trong tam giÃƒÂ¡c {upperHex, lowerHex, hC}: NÃ¡ÂºÂ¿u tÃ¡Â»â€œn tÃ¡ÂºÂ¡i ÃƒÂ´ hC Ã¡Â»Å¸ cÃƒÂ¹ng tÃ¡ÂºÂ§ng vÃ¡Â»â€ºi lowerHex,
            // cÃ…Â©ng kÃ¡Â»Â vÃ¡Â»â€ºi upperHex vÃƒÂ  lowerHex: upperHex CHÃ¡Â»Ë† Ã„ÂÃ†Â¯Ã¡Â»Â¢C Ã„â€˜Ã¡Â»â€¢ 1 thÃƒÂ¡c nÃ†Â°Ã¡Â»â€ºc duy nhÃ¡ÂºÂ¥t xuÃ¡Â»â€˜ng 1 trong 2 ÃƒÂ´!
            // Ãƒâ€ cÃƒÂ²n lÃ¡ÂºÂ¡i sÃ¡ÂºÂ½ nhÃ¡ÂºÂ­n dÃƒÂ²ng nÃ†Â°Ã¡Â»â€ºc chÃ¡ÂºÂ£y ngang tÃ¡Â»Â« ÃƒÂ´ nhÃ¡ÂºÂ­n thÃƒÂ¡c Ã„â€˜Ã¡Â»Æ’ suÃ¡Â»â€˜i Ã„â€˜i tuÃ¡ÂºÂ§n tÃ¡Â»Â±, khÃƒÂ´ng tÃ¡ÂºÂ¡o vÃƒÂ²ng lÃ¡ÂºÂ·p.
            for (int d = 0; d < 6; d++)
            {
                HexCoordinates hC = lowerHex.GetNeighbor(d);
                if (_riverMap.TryGetValue(hC, out RiverTileSegment segC) && segC != null && upperHex.DistanceTo(hC) == 1)
                {
                    float yC = GetTileTopY(hC, worldGen);
                    if (Mathf.Abs(yC - lowerY) <= 0.35f)
                    {
                        // BÃ¡ÂºÂ¢O VÃ¡Â»â€  LÃ¡Â»Å CH SÃ¡Â»Â¬ Ã„ÂÃ¡ÂºÂ¶T Ãƒâ€: Ãƒâ€ Ã„â€˜Ã¡ÂºÂ·t trÃ†Â°Ã¡Â»â€ºc (placementOrder nhÃ¡Â»Â hÃ†Â¡n) LUÃƒâ€N GIÃ¡Â»Â® NGUYÃƒÅ N THÃƒÂC!
                        // Ãƒâ€ mÃ¡Â»â€ºi Ã„â€˜Ã¡ÂºÂ·t sau tuyÃ¡Â»â€¡t Ã„â€˜Ã¡Â»â€˜i khÃƒÂ´ng Ã„â€˜Ã†Â°Ã¡Â»Â£c cÃ†Â°Ã¡Â»â€ºp thÃƒÂ¡c cÃ¡Â»Â§a ÃƒÂ´ Ã„â€˜ÃƒÂ£ tÃ¡Â»â€œn tÃ¡ÂºÂ¡i!
                        if (segC.placementOrder < lowerSeg.placementOrder) return false;
                        if (segC.placementOrder > lowerSeg.placementOrder) continue;

                        int rankLower = lowerSeg.isOrigin ? -1 : lowerSeg.flowStep;
                        int rankC = segC.isOrigin ? -1 : segC.flowStep;

                        if (rankC < rankLower)
                        {
                            return false; // NhÃ†Â°Ã¡Â»Âng thÃƒÂ¡c nÃ†Â°Ã¡Â»â€ºc cho hC; lowerHex sÃ¡ÂºÂ½ nhÃ¡ÂºÂ­n suÃ¡Â»â€˜i chÃ¡ÂºÂ£y ngang tÃ¡Â»Â« hC
                        }
                        else if (rankC == rankLower)
                        {
                            // HÃƒÂ²a Ã„â€˜iÃ¡Â»Æ’m: DÃƒÂ¹ng tÃ¡Â»Âa Ã„â€˜Ã¡Â»â„¢ phÃƒÂ¡ vÃ¡Â»Â¡ thÃ¡ÂºÂ¿ hÃƒÂ²a Ã„â€˜Ã¡Â»Æ’ chÃ¡Â»â€° duy nhÃ¡ÂºÂ¥t 1 ÃƒÂ´ nhÃ¡ÂºÂ­n thÃƒÂ¡c
                            if (segC.placementOrder < lowerSeg.placementOrder) return false;
                            if (segC.placementOrder > lowerSeg.placementOrder) continue;
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

            return true; // NÃ†Â°Ã¡Â»â€ºc chÃ¡ÂºÂ£y xuÃƒÂ´i tÃ¡Â»Â« ÃƒÂ´ cao xuÃ¡Â»â€˜ng ÃƒÂ´ thÃ¡ÂºÂ¥p theo nhÃƒÂ¡nh hÃ¡Â»Â£p lÃ¡Â»â€¡ duy nhÃ¡ÂºÂ¥t
        }

        // TrÃ†Â°Ã¡Â»Âng hÃ¡Â»Â£p 2: Hai ÃƒÂ´ Ã¡Â»Å¸ CÃƒâ„¢NG MÃ¡Â»ËœT TÃ¡ÂºÂ¦NG Ã„ÂÃ¡Â»Ëœ CAO
        // Quy tÃ¡ÂºÂ¯c A: Tam giÃƒÂ¡c vÃ¡Â»â€ºi ÃƒÂ´ trÃƒÂªn cao {hUpper, hA, hB}:
        // NÃ¡ÂºÂ¿u cÃ¡ÂºÂ£ 2 ÃƒÂ´ Ã„â€˜Ã¡Â»Âu Ã„â€˜ang nhÃ¡ÂºÂ­n thÃƒÂ¡c nÃ†Â°Ã¡Â»â€ºc tÃ¡Â»Â« tÃ¡ÂºÂ§ng trÃƒÂªn Ã„â€˜Ã¡Â»â€¢ xuÃ¡Â»â€˜ng, KHÃƒâ€NG nÃ¡Â»â€˜i ngang Ã„â€˜Ã¡Â»Æ’ trÃƒÂ¡nh tÃ¡ÂºÂ¡o vÃƒÂ²ng lÃ¡ÂºÂ·p tam giÃƒÂ¡c!
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
                                return false; // CÃ¡ÂºÂ£ hai Ã„â€˜Ã¡Â»Âu cÃƒÂ³ thÃƒÂ¡c tÃ¡Â»Â« hUpper Ã„â€˜Ã¡Â»â€¢ xuÃ¡Â»â€˜ng -> TuyÃ¡Â»â€¡t Ã„â€˜Ã¡Â»â€˜i khÃƒÂ´ng nÃ¡Â»â€˜i ngang!
                            }
                        }
                    }
                }
            }
        }

        // Quy tÃ¡ÂºÂ¯c C: Tam giÃƒÂ¡c 3 ÃƒÂ´ cÃƒÂ¹ng tÃ¡ÂºÂ§ng phÃ¡ÂºÂ³ng {hA, hB, hC}
        // NgÃ¡ÂºÂ¯t cÃ¡ÂºÂ¡nh trÃ¡Â»Â±c tiÃ¡ÂºÂ¿p giÃ¡Â»Â¯a ÃƒÂ´ cÃƒÂ³ flowStep nhÃ¡Â»Â nhÃ¡ÂºÂ¥t vÃƒÂ  lÃ¡Â»â€ºn nhÃ¡ÂºÂ¥t Ã„â€˜Ã¡Â»Æ’ dÃƒÂ²ng Ã„â€˜i 1 lÃƒÂ n A -> B -> C
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

                    if (!segA.isOrigin && segA.flowStep == 0 && segA.placementOrder > segC.placementOrder)
                    {
                        rA = segC.flowStep + 1;
                    }
                    if (!segB.isOrigin && segB.flowStep == 0 && segB.placementOrder > segC.placementOrder)
                    {
                        rB = segC.flowStep + 1;
                    }

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
    /// TÃ¬m Ã´ thÆ°á»£ng nguá»“n tá»‘t nháº¥t (gáº§n nguá»“n nháº¥t - min flowStep) trong sá»‘ cÃ¡c Ã´ lÃ¡ng giá»ng á»Ÿ táº§ng cao hÆ¡n.
    /// GiÃºp Ã´ táº§ng dÆ°á»›i chá»‰ nháº­n Ä‘Ãºng 1 nguá»“n cáº¥p nÆ°á»›c tá»« Ã´ gáº§n nguá»“n nháº¥t á»Ÿ táº§ng trÃªn.
    /// </summary>
    private HexCoordinates GetBestUpstreamFeeder(HexCoordinates hex, HexWorldGenerator worldGen)
    {
        float myY = GetTileTopY(hex, worldGen);
        HexCoordinates bestFeeder = hex;
        int bestRank = int.MaxValue;
        int bestOrder = int.MaxValue;

        for (int d = 0; d < 6; d++)
        {
            HexCoordinates nHex = hex.GetNeighbor(d);
            if (_riverMap.TryGetValue(nHex, out RiverTileSegment nSeg) && nSeg != null)
            {
                float nY = GetTileTopY(nHex, worldGen);
                if (nY > myY + 0.35f)
                {
                    // isOrigin Æ°u tiÃªn cao nháº¥t (-1), sau Ä‘Ã³ Ä‘áº¿n flowStep nhá» nháº¥t
                    int rank = nSeg.isOrigin ? -1 : nSeg.flowStep;
                    if (rank < bestRank || (rank == bestRank && nSeg.placementOrder < bestOrder))
                    {
                        bestRank = rank; bestOrder = nSeg.placementOrder;
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
    // Ã„ÂIÃ¡Â»â€šM PHÃ¡ÂºÂ¢N Ã¡Â»Â¨NG HAI CHIÃ¡Â»â‚¬U (REACTIVE SCORING)
    // =========================================================================

    /// <summary>
    /// TÃƒÂ­nh Ã„â€˜iÃ¡Â»Æ’m thÃ†Â°Ã¡Â»Å¸ng tÃ¡Â»Â©c thÃƒÂ¬ khi vÃ¡Â»Â«a Ã„â€˜Ã¡ÂºÂ·t mÃ¡Â»â„¢t thÃ¡ÂºÂ» SuÃ¡Â»â€˜i mÃ¡Â»â€ºi xuÃ¡Â»â€˜ng:
    /// - Ã„ÂiÃ¡Â»Æ’m bÃ¡ÂºÂ£n thÃƒÂ¢n Ã„â€˜oÃ¡ÂºÂ¡n suÃ¡Â»â€˜i (Base River points).
    /// - Ã„ÂiÃ¡Â»Æ’m cÃ¡Â»â„¢ng hÃ†Â°Ã¡Â»Å¸ng nÃ¡ÂºÂ¿u ÃƒÂ´ nÃƒÂ y Ã„â€˜ÃƒÂ£ cÃƒÂ³ sÃ¡ÂºÂµn Biome.
    /// - Ã„ÂiÃ¡Â»Æ’m sinh thÃƒÂ¡i nÃ¡ÂºÂ¿u cÃƒÂ¡c ÃƒÂ´ lÃƒÂ¡ng giÃ¡Â»Âng xung quanh Ã„â€˜ÃƒÂ£ cÃƒÂ³ Biome.
    /// - Ã„ÂiÃ¡Â»Æ’m BÃ¡ÂºÂ¿n NÃ†Â°Ã¡Â»â€ºc MuÃƒÂ´n LoÃƒÂ i (Watering Hole) nÃ¡ÂºÂ¿u cÃƒÂ¡c ÃƒÂ´ lÃƒÂ¡ng giÃ¡Â»Âng Ã„â€˜ÃƒÂ£ cÃƒÂ³ ThÃƒÂº.
    /// </summary>
    private int CalculateBurstScoreForNewRiver(HexCoordinates riverHex)
    {
        int score = riverConfig != null ? riverConfig.pointsPerRiverTile : 3;

        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen == null || worldGen.MapTiles == null) return score;

        // 1. KiÃ¡Â»Æ’m tra ÃƒÂ´ tÃ¡ÂºÂ¡i chÃ¡Â»â€” (Co-located Biome)
        if (worldGen.MapTiles.TryGetValue(riverHex, out GameObject centerTileObj) && centerTileObj != null)
        {
            if (HasBiomeOnTile(centerTileObj))
            {
                score += riverConfig != null ? riverConfig.pointsPerCoLocatedBiome : 2;
            }

            // ThÃ†Â°Ã¡Â»Å¸ng BÃ¡ÂºÂ¿n nÃ†Â°Ã¡Â»â€ºc nÃ¡ÂºÂ¿u suÃ¡Â»â€˜i chÃ¡ÂºÂ£y qua ÃƒÂ´ Ã„â€˜ang cÃƒÂ³ thÃƒÂº sinh sÃ¡Â»â€˜ng
            if (HasCreatureOnTile(centerTileObj, riverHex))
            {
                score += riverConfig != null ? riverConfig.pointsPerAdjacentCreature : 5;
            }
        }

        // 2. QuÃƒÂ©t 6 ÃƒÂ´ lÃƒÂ¡ng giÃ¡Â»Âng xung quanh
        int adjacentCreatureCount = 0;
        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates nHex = riverHex.GetNeighbor(dir);
            if (worldGen.MapTiles.TryGetValue(nHex, out GameObject nTileObj) && nTileObj != null)
            {
                // ThÃ†Â°Ã¡Â»Å¸ng Biome lÃƒÂ¡ng giÃ¡Â»Âng ven suÃ¡Â»â€˜i
                if (HasBiomeOnTile(nTileObj))
                {
                    score += riverConfig != null ? riverConfig.pointsPerAdjacentBiome : 1;
                }

                // ThÃ†Â°Ã¡Â»Å¸ng BÃ¡ÂºÂ¿n nÃ†Â°Ã¡Â»â€ºc muÃƒÂ´n loÃƒÂ i nÃ¡ÂºÂ¿u cÃƒÂ³ thÃƒÂº sinh sÃ¡Â»â€˜ng
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
    /// KÃƒÂ­ch hoÃ¡ÂºÂ¡t khi ngÃ†Â°Ã¡Â»Âi chÃ†Â¡i vÃ¡Â»Â«a Ã„â€˜Ã¡ÂºÂ·t mÃ¡Â»â„¢t thÃ¡ÂºÂ» khÃƒÂ¡c (Biome hoÃ¡ÂºÂ·c Creature) lÃƒÂªn bÃ¡ÂºÂ£n Ã„â€˜Ã¡Â»â€œ.
    /// NÃ¡ÂºÂ¿u thÃ¡ÂºÂ» nÃƒÂ y Ã„â€˜Ã†Â°Ã¡Â»Â£c Ã„â€˜Ã¡ÂºÂ·t kÃ¡Â»Â sÃƒÂ¡t (hoÃ¡ÂºÂ·c Ã„â€˜ÃƒÂ¨ lÃƒÂªn) mÃ¡Â»â„¢t Ã„â€˜oÃ¡ÂºÂ¡n suÃ¡Â»â€˜i Ã„â€˜ÃƒÂ£ cÃƒÂ³ sÃ¡ÂºÂµn Ã¢Å¾â€ KÃƒÂ­ch hoÃ¡ÂºÂ¡t Ã„â€˜iÃ¡Â»Æ’m thÃ†Â°Ã¡Â»Å¸ng nhÃ¡Â»Â giÃ¡Â»Ât (Incremental Score)!
    /// </summary>
    public void OnNeighborCardPlaced(HexCoordinates placedHex, CardData placedCard)
    {
        if (placedCard == null || riverConfig == null) return;

        EnsureConfig();
        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();
        int bonusScore = 0;

        // TRÃ†Â¯Ã¡Â»Å“NG HÃ¡Â»Â¢P 1: Ã„ÂÃ¡ÂºÂ¶T THÃ¡ÂºÂº THÃƒÅ¡ (CREATURE) SÃƒÂT BÃ¡Â»Å“ SUÃ¡Â»ÂI
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
                // Ã„ÂiÃ¡Â»Æ’m bÃ¡ÂºÂ¿n nÃ†Â°Ã¡Â»â€ºc muÃƒÂ´n loÃƒÂ i
                bonusScore += riverConfig.pointsPerAdjacentCreature;

                // ThÃ†Â°Ã¡Â»Å¸ng thÃƒÂªm "BÃƒÂ¡n Ã„â€˜Ã¡ÂºÂ£o ÃƒÂªm Ã„â€˜Ã¡Â»Âm" nÃ¡ÂºÂ¿u Ã„â€˜Ã†Â°Ã¡Â»Â£c suÃ¡Â»â€˜i ÃƒÂ´m tÃ¡Â»Â« 2 phÃƒÂ­a trÃ¡Â»Å¸ lÃƒÂªn
                if (touchingRiverCount >= 2)
                {
                    bonusScore += riverConfig.pointsPeninsulaBonus;
                }
            }
        }
        // TRÃ†Â¯Ã¡Â»Å“NG HÃ¡Â»Â¢P 2: Ã„ÂÃ¡ÂºÂ¶T THÃ¡ÂºÂº Ã„ÂÃ¡Â»Å A HÃƒÅ’NH (TERRAIN / BIOME) TRÃƒÅ N HOÃ¡ÂºÂ¶C SÃƒÂT SUÃ¡Â»ÂI
        else if (placedCard.cardType == CardType.Terrain)
        {
            // NÃ¡ÂºÂ¿u Ã„â€˜Ã¡ÂºÂ·t trÃ¡Â»Â±c tiÃ¡ÂºÂ¿p lÃƒÂªn ÃƒÂ´ Ã„â€˜ÃƒÂ£ cÃƒÂ³ suÃ¡Â»â€˜i tÃ¡Â»Â« trÃ†Â°Ã¡Â»â€ºc
            if (_riverMap.ContainsKey(placedHex))
            {
                bonusScore += riverConfig.pointsPerCoLocatedBiome;
            }

            // Ã„ÂiÃ¡Â»Æ’m thÃ†Â°Ã¡Â»Å¸ng vÃ¡Â»â€ºi cÃƒÂ¡c ÃƒÂ´ suÃ¡Â»â€˜i lÃƒÂ¡ng giÃ¡Â»Âng kÃ¡Â»Â sÃƒÂ¡t
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
    // TÃƒÂNH TOÃƒâ‚¬N BÃ¡Â»Ëœ Ã„ÂIÃ¡Â»â€šM SÃƒâ€NG SUÃ¡Â»ÂI (TOÃƒâ‚¬N CÃ¡ÂºÂ¢NH CHO SCORECALCULATOR)
    // =========================================================================

    /// <summary>
    /// TÃƒÂ­nh tÃ¡Â»â€¢ng Ã„â€˜iÃ¡Â»Æ’m toÃƒÂ n bÃ¡Â»â„¢ mÃ¡ÂºÂ¡ng lÃ†Â°Ã¡Â»â€ºi suÃ¡Â»â€˜i trÃƒÂªn toÃƒÂ n bÃ¡ÂºÂ£n Ã„â€˜Ã¡Â»â€œ:
    /// Ã„ÂÃ¡ÂºÂ£m bÃ¡ÂºÂ£o kÃ¡ÂºÂ¿t quÃ¡ÂºÂ£ LUÃƒâ€N NHÃ¡ÂºÂ¤T QUÃƒÂN dÃƒÂ¹ ngÃ†Â°Ã¡Â»Âi chÃ†Â¡i Ã„â€˜Ã¡ÂºÂ·t SuÃ¡Â»â€˜i trÃ†Â°Ã¡Â»â€ºc hay Biome/ThÃƒÂº trÃ†Â°Ã¡Â»â€ºc!
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

        // 1. Ã„ÂiÃ¡Â»Æ’m cÃ†Â¡ bÃ¡ÂºÂ£n cho mÃ¡Â»â€”i ÃƒÂ´ suÃ¡Â»â€˜i
        totalScore += _riverMap.Count * (riverConfig != null ? riverConfig.pointsPerRiverTile : 3);

        // 2. Ã„ÂiÃ¡Â»Æ’m Biome tÃ¡ÂºÂ¡i chÃ¡Â»â€” vÃƒÂ  Biome ven sÃƒÂ´ng
        HashSet<string> countedRiverBiomeEdges = new HashSet<string>();

        foreach (var kvp in _riverMap)
        {
            HexCoordinates riverHex = kvp.Key;

            // KiÃ¡Â»Æ’m tra Biome tÃ¡ÂºÂ¡i chÃ¡Â»â€”
            if (worldGen.MapTiles.TryGetValue(riverHex, out GameObject centerTileObj) && centerTileObj != null)
            {
                if (HasBiomeOnTile(centerTileObj))
                {
                    totalScore += riverConfig.pointsPerCoLocatedBiome;
                }
            }

            // KiÃ¡Â»Æ’m tra Biome lÃƒÂ¡ng giÃ¡Â»Âng
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

        // 3. Ã„ÂiÃ¡Â»Æ’m BÃ¡ÂºÂ¿n nÃ†Â°Ã¡Â»â€ºc muÃƒÂ´n loÃƒÂ i (Creature Watering Hole)
        // QuÃƒÂ©t tÃ¡ÂºÂ¥t cÃ¡ÂºÂ£ thÃƒÂº trÃƒÂªn bÃ¡ÂºÂ£n Ã„â€˜Ã¡Â»â€œ, xem mÃ¡Â»â€”i con thÃƒÂº tiÃ¡ÂºÂ¿p giÃƒÂ¡p bao nhiÃƒÂªu ÃƒÂ´ suÃ¡Â»â€˜i
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
    // TIÃ¡Â»â€ N ÃƒÂCH KIÃ¡Â»â€šM TRA Ã„ÂÃ¡Â»Å A HÃƒÅ’NH & THÃƒÅ¡
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

    // Stub tÃ†Â°Ã†Â¡ng thÃƒÂ­ch ngÃ†Â°Ã¡Â»Â£c cho cÃƒÂ¡c cÃƒÂ´ng cÃ¡Â»Â¥ Inspector/Debug cÃ…Â©
    public void RebuildAllRiverSplines() { }
}

