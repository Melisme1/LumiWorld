using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Đại diện cho một ô lục giác định cư trong game.
/// Chuyên trách việc sinh ngẫu nhiên (Procedural Generation) nội dung công trình, nhà ở và chi tiết trang trí
/// trên ô theo vai trò (Role) và cấp độ (Tier) được giao.
/// </summary>
public class SettlementTile : MonoBehaviour
{
    [Header("Tile Identity")]
    public HexCoordinates Coordinates;
    public SettlementRole CurrentRole = SettlementRole.Residential;
    public SettlementTier CurrentTier = SettlementTier.Hamlet;

    [Header("Containers")]
    [SerializeField] private Transform contentRoot;

    private List<GameObject> _spawnedInstances = new List<GameObject>();

    public Transform ContentRoot => contentRoot != null ? contentRoot : transform;

    /// <summary>
    /// Tính toán độ cao bề mặt trên cùng của ô đất lục giác hiện tại
    /// </summary>
    public float GetTileSurfaceY()
    {
        var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(gameObject);
        if (topY > transform.position.y)
        {
            return topY;
        }
        return transform.position.y + HexMetrics.TileHeight;
    }

    private void Awake()
    {
        EnsureContentRoot();
    }

    private void EnsureContentRoot()
    {
        if (contentRoot == null)
        {
            Transform existing = transform.Find("Settlement_Content");
            if (existing != null)
            {
                contentRoot = existing;
            }
            else
            {
                GameObject rootGo = new GameObject("Settlement_Content");
                rootGo.transform.SetParent(transform, true);
                contentRoot = rootGo.transform;
            }
        }

        // Định vị ContentRoot nằm chuẩn xác trên bề mặt phía trên của ô đất
        float surfaceY = GetTileSurfaceY();
        contentRoot.position = new Vector3(transform.position.x, surfaceY, transform.position.z);
        contentRoot.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// Xóa toàn bộ nội dung nhà cửa cũ trên ô
    /// </summary>
    public void ClearContent()
    {
        EnsureContentRoot();
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = contentRoot.GetChild(i);
            if (child != null)
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }
        _spawnedInstances.Clear();
    }

    /// <summary>
    /// Sinh bố cục ngẫu nhiên có kiểm soát (Procedural) cho ô lục giác
    /// Hỗ trợ cả chế độ đơn lẻ (Single Hamlet) và quy hoạch cụm đô thị liên kết (Cluster-Aware Town).
    /// </summary>
    public void BuildProcedural(
        SettlementAssetConfig config, 
        SettlementRole role, 
        SettlementTier tier,
        int clusterSize = 1,
        Vector3 clusterCenterWorld = default,
        List<int> internalDirs = null,
        List<int> externalDirs = null)
    {
        if (config == null) return;

        EnsureContentRoot();
        ClearContent();

        CurrentRole = role;
        CurrentTier = tier;

        // Cập nhật lại vị trí bề mặt theo độ cao thực tế của ô đất
        float surfaceY = GetTileSurfaceY();
        ContentRoot.position = new Vector3(transform.position.x, surfaceY, transform.position.z);

        // Tạo hạt giống ngẫu nhiên: Kết hợp cả tọa độ ô, vai trò và quy mô cụm (clusterSize)
        // để khi mở rộng thêm ô mới, toàn bộ cụm sẽ tự động tái quy hoạch và nâng cấp kiến trúc mới
        int clusterSeed = clusterSize * 104729;
        int seed = (Coordinates.Q * 73856093) ^ (Coordinates.R * 19349663) ^ ((int)role * 83492791) ^ clusterSeed;
        System.Random rng = new System.Random(seed);

        Vector3 rootPos = ContentRoot.position;

        if (internalDirs == null) internalDirs = new List<int>();
        if (externalDirs == null)
        {
            externalDirs = new List<int>();
            for (int d = 0; d < 6; d++)
            {
                if (!internalDirs.Contains(d)) externalDirs.Add(d);
            }
        }

        bool isMultiTile = clusterSize > 1;
        Vector3 toClusterCenter = clusterCenterWorld - rootPos;
        toClusterCenter.y = 0;
        bool hasClusterCenter = isMultiTile && toClusterCenter.sqrMagnitude > 0.05f;

        // Danh sách các hướng khả dụng để dựng công trình (nếu ô lõi không có cạnh biên thì dùng các hướng nội bộ)
        List<int> validBuildingDirs = (externalDirs != null && externalDirs.Count > 0)
            ? externalDirs
            : ((internalDirs != null && internalDirs.Count > 0) ? internalDirs : new List<int> { 0, 1, 2, 3, 4, 5 });

        bool hasRiver = RiverNetworkManager.Instance != null && RiverNetworkManager.Instance.HasRiver(Coordinates);
        RiverTileSegment riverSeg = hasRiver ? RiverNetworkManager.Instance.GetRiver(Coordinates) : null;
        List<(Vector2 pos, float radius)> riverZones = BuildRiverBlockedZones(riverSeg);
        List<(Vector2 pos, float radius)> occupiedZones = new List<(Vector2 pos, float radius)>();

        float spreadDist = Mathf.Clamp(Mathf.Max(config.GetHouseDistance(), 0.56f), 0.52f, 0.68f);

        // 1. SINH CÔNG TRÌNH THEO VAI TRÒ ĐẶC THÙ (DISTRICT ZONING)
        if (role == SettlementRole.CivicCore)
        {
            // Ô LÕI ĐẠI ĐÔ THỊ: Lâu Đài Hoàng Gia (Castle) hoặc Nhà Thờ Lớn (Church)
            GameObject corePrefab = config.GetRandomCivicBuilding(rng);
            if (corePrefab != null)
            {
                Vector3 centerPos = rootPos + new Vector3(0, config.buildingYOffset, 0);
                Quaternion rot = Quaternion.Euler(0, rng.Next(0, 6) * 60f, 0);

                // TÍCH HỢP HÀI HÒA NẾU Ô NÀY CÓ SÔNG / HANG THƯỢNG NGUỒN:
                if (riverSeg != null)
                {
                    if (riverSeg.pieceType == RiverPieceType.SpringHead)
                    {
                        int outDir = riverSeg.primaryPortA >= 0 ? riverSeg.primaryPortA : 0;
                        Vector3 fwd = RiverSplinePath.GetEdgeNormalLocal(outDir);
                        centerPos = rootPos - fwd * 0.36f + new Vector3(0, config.buildingYOffset, 0);
                        rot = Quaternion.LookRotation(fwd, Vector3.up);
                    }
                    else
                    {
                        int pA = riverSeg.primaryPortA >= 0 ? riverSeg.primaryPortA : 0;
                        Vector3 normA = RiverSplinePath.GetEdgeNormalLocal(pA);
                        Vector3 tangent = new Vector3(-normA.z, 0, normA.x);
                        centerPos = rootPos + tangent * 0.36f + new Vector3(0, config.buildingYOffset, 0);
                    }
                }

                Vector2 coreLocal2D = new Vector2(centerPos.x - rootPos.x, centerPos.z - rootPos.z);
                occupiedZones.Add((coreLocal2D, 0.38f));

                SpawnModel(corePrefab, centerPos, rot, config.GetScale(SettlementScaleType.Castle));
                SpawnBuildingVignette(centerPos, rot, SettlementRole.CivicCore, config, rng);
            }

            // Đại quảng trường hoàng gia lát đá bao quanh lâu đài
            SpawnCobblestonePlaza(config, rng, rootPos, 0.42f, 16);

            // Cắm cờ hoàng gia hai bên cửa chính (né vùng sông)
            if (config.villageCenterpieces != null)
            {
                GameObject flagPrefab = config.villageCenterpieces.Find(p => p != null && p.name.ToLower().Contains("flag"));
                if (flagPrefab != null)
                {
                    Vector2 f1Local = new Vector2(0.42f, 0.28f);
                    Vector2 f2Local = new Vector2(-0.42f, 0.28f);
                    if (!IsPointInZones(f1Local, 0.08f, riverZones))
                        SpawnModel(flagPrefab, rootPos + new Vector3(f1Local.x, 0, f1Local.y), Quaternion.identity, config.GetScale(SettlementScaleType.Centerpiece));
                    if (!IsPointInZones(f2Local, 0.08f, riverZones))
                        SpawnModel(flagPrefab, rootPos + new Vector3(f2Local.x, 0, f2Local.y), Quaternion.identity, config.GetScale(SettlementScaleType.Centerpiece));
                }
            }

            if (isMultiTile && validBuildingDirs.Count > 0)
            {
                SpawnClusterPerimeterHouses(config, rng, Mathf.Min(validBuildingDirs.Count, 2), tier, validBuildingDirs, clusterCenterWorld, null, occupiedZones, riverZones);
            }
        }
        else if (role == SettlementRole.MarketSquare)
        {
            // KHU CHỢ / BAZAAR SẦM UẤT (MARKET DISTRICT)
            List<int> spreadDirs = PickMaximallySeparatedDirs(validBuildingDirs, 2, rng);
            List<int> usedDirs = new List<int>();

            GameObject mainMarket = config.GetRandomCommercialBuilding(rng);
            if (mainMarket != null && spreadDirs.Count > 0)
            {
                int mDir = spreadDirs[0];
                usedDirs.Add(mDir);
                PlaceSpreadBuildingInDir(mainMarket, mDir, spreadDist, 0.26f, config, rng, rootPos, hasClusterCenter, clusterCenterWorld, occupiedZones, riverZones, true);
            }

            if (spreadDirs.Count > 1)
            {
                int tDir = spreadDirs[1];
                usedDirs.Add(tDir);
                GameObject secondComm = config.GetRandomCommercialBuilding(rng);
                if (secondComm == null) secondComm = config.GetRandomMediumHouse(rng);
                if (secondComm != null)
                {
                    PlaceSpreadBuildingInDir(secondComm, tDir, spreadDist, 0.26f, config, rng, rootPos, hasClusterCenter, clusterCenterWorld, occupiedZones, riverZones, true);
                }
            }

            if (isMultiTile)
            {
                int extraHouses = Mathf.Clamp(validBuildingDirs.Count - usedDirs.Count, 1, 2);
                SpawnClusterPerimeterHouses(config, rng, extraHouses, tier, validBuildingDirs, clusterCenterWorld, usedDirs, occupiedZones, riverZones);
            }
            else
            {
                SpawnPerimeterHouses(config, rng, 2, tier, occupiedZones, riverZones, riverSeg);
            }

            SpawnCobblestonePlaza(config, rng, rootPos, 0.36f, 14);
        }
        else if (role == SettlementRole.Craftsman)
        {
            // KHU XƯỞNG SẢN XUẤT & LÀNG NGHỀ (CRAFTSMEN DISTRICT)
            List<int> spreadDirs = PickMaximallySeparatedDirs(validBuildingDirs, 2, rng);
            List<int> usedDirs = new List<int>();

            GameObject mainCraft = config.GetRandomProductionBuilding(rng);
            if (mainCraft != null && spreadDirs.Count > 0)
            {
                int cDir = spreadDirs[0];
                usedDirs.Add(cDir);
                PlaceSpreadBuildingInDir(mainCraft, cDir, spreadDist, 0.26f, config, rng, rootPos, hasClusterCenter, clusterCenterWorld, occupiedZones, riverZones, false, SettlementRole.Craftsman);
            }

            if (spreadDirs.Count > 1)
            {
                int cDir2 = spreadDirs[1];
                usedDirs.Add(cDir2);
                GameObject secondCraft = config.GetRandomProductionBuilding(rng);
                if (secondCraft == null) secondCraft = config.GetRandomVillageCenterpiece(rng);
                if (secondCraft == null) secondCraft = config.GetRandomMediumHouse(rng);
                if (secondCraft != null)
                {
                    PlaceSpreadBuildingInDir(secondCraft, cDir2, spreadDist, 0.26f, config, rng, rootPos, hasClusterCenter, clusterCenterWorld, occupiedZones, riverZones, false, SettlementRole.Craftsman);
                }
            }

            if (isMultiTile)
            {
                int extraHouses = Mathf.Clamp(validBuildingDirs.Count - usedDirs.Count, 1, 2);
                SpawnClusterPerimeterHouses(config, rng, extraHouses, tier, validBuildingDirs, clusterCenterWorld, usedDirs, occupiedZones, riverZones);
            }
            else
            {
                SpawnPerimeterHouses(config, rng, 2, tier, occupiedZones, riverZones, riverSeg);
            }

            SpawnCobblestonePlaza(config, rng, rootPos, 0.32f, 10);
        }
        else if (role == SettlementRole.Military)
        {
            // KHU DOANH TRẠI & PHÒNG THỦ QUÂN SỰ (MILITARY GARRISON)
            List<int> spreadDirs = PickMaximallySeparatedDirs(validBuildingDirs, 2, rng);
            List<int> usedDirs = new List<int>();

            GameObject mainMil = config.GetRandomMilitaryBuilding(rng);
            if (mainMil != null && spreadDirs.Count > 0)
            {
                int mDir = spreadDirs[0];
                usedDirs.Add(mDir);
                PlaceSpreadBuildingInDir(mainMil, mDir, spreadDist, 0.26f, config, rng, rootPos, hasClusterCenter, clusterCenterWorld, occupiedZones, riverZones, false, SettlementRole.Military);
            }

            if (spreadDirs.Count > 1)
            {
                int mDir2 = spreadDirs[1];
                usedDirs.Add(mDir2);
                GameObject secondMil = config.GetRandomMilitaryBuilding(rng);
                if (secondMil == null) secondMil = config.GetRandomMediumHouse(rng);
                if (secondMil != null)
                {
                    PlaceSpreadBuildingInDir(secondMil, mDir2, spreadDist, 0.26f, config, rng, rootPos, hasClusterCenter, clusterCenterWorld, occupiedZones, riverZones, false, SettlementRole.Military);
                }
            }

            if (isMultiTile)
            {
                int extraHouses = Mathf.Clamp(validBuildingDirs.Count - usedDirs.Count, 1, 2);
                SpawnClusterPerimeterHouses(config, rng, extraHouses, tier, validBuildingDirs, clusterCenterWorld, usedDirs, occupiedZones, riverZones);
            }
            else
            {
                SpawnPerimeterHouses(config, rng, 2, tier, occupiedZones, riverZones, riverSeg);
            }

            SpawnCobblestonePlaza(config, rng, rootPos, 0.32f, 10);
        }
        else
        {
            // 2. Ô DÂN CƯ THÔNG THƯỜNG (RESIDENTIAL / HAMLET)
            if (!isMultiTile)
            {
                // Làng 1 ô (Hamlet): Chỉ đặt giếng/centerpiece ở giữa ô nếu KHÔNG có sông/đầu nguồn chiếm tâm ô!
                if (!hasRiver)
                {
                    GameObject centerPiece = config.GetRandomVillageCenterpiece(rng);
                    if (centerPiece != null)
                    {
                        Vector3 centerPos = rootPos + new Vector3(0, config.buildingYOffset, 0);
                        Quaternion centerRot = Quaternion.Euler(0, (float)(rng.NextDouble() * 360f), 0);
                        SpawnModel(centerPiece, centerPos, centerRot, config.GetScale(SettlementScaleType.Centerpiece));
                        occupiedZones.Add((Vector2.zero, 0.20f));
                    }
                }

                SpawnPerimeterHouses(config, rng, 3, tier, occupiedZones, riverZones, riverSeg);
                SpawnCobblestonePlaza(config, rng, rootPos, 0.28f, 10);
            }
            else
            {
                // Khu phố cổ dân cư trong đô thị:
                int perimeterCount = 2;
                SpawnClusterPerimeterHouses(config, rng, perimeterCount, tier, validBuildingDirs, clusterCenterWorld, null, occupiedZones, riverZones);

                // Căn nhà phố phụ ở khoảng thoáng (né sông và né nhà đã đặt)
                if (validBuildingDirs.Count > 0)
                {
                    int midDir = validBuildingDirs[rng.Next(0, validBuildingDirs.Count)];
                    float midAngle = RiverAutoTileSolver.GetDirectionAngle(midDir);
                    if (TryFindSpreadBuildingOffset(rng, midAngle, 0.34f, 0.56f, 0.24f, occupiedZones, riverZones, out Vector2 midLocal))
                    {
                        Vector3 midPos = rootPos + new Vector3(midLocal.x, config.buildingYOffset, midLocal.y);
                        Vector3 midFace = hasClusterCenter ? (clusterCenterWorld - midPos) : new Vector3(-midLocal.x, 0, -midLocal.y);
                        midFace.y = 0;
                        Quaternion midRot = midFace.sqrMagnitude > 0.001f
                            ? Quaternion.LookRotation(midFace.normalized, Vector3.up) * Quaternion.Euler(0, config.houseFacingOffset, 0)
                            : Quaternion.identity;

                        GameObject midHouse = config.GetRandomSmallHouse(rng);
                        if (midHouse != null)
                        {
                            occupiedZones.Add((midLocal, 0.24f));
                            SpawnModel(midHouse, midPos, midRot, config.GetScale(SettlementScaleType.Building));
                            SpawnBuildingVignette(midPos, midRot, SettlementRole.Residential, config, rng);
                            SpawnHouseFootpath(config, rng, midPos - rootPos);
                        }
                    }
                }

                SpawnCobblestonePlaza(config, rng, rootPos, 0.30f, 10);
            }
        }

        SpawnDistrictNatureFillers(config, rng, validBuildingDirs);
        SpawnStreetPropsOnInternalEdges(config, rng, internalDirs);

        // HÒA QUYỆN & GIẢI TỎA XUNG ĐỘT VỚI BIOME CŨ (Biến đá tảng thành đá cảnh, dọn sạch chân móng nhà & chân tường thành)
        HarmonizeBiomePropsForSettlement(config, externalDirs);
    }

    private void PlaceSpreadBuildingInDir(
        GameObject prefab,
        int dir,
        float baseDist,
        float footprintRadius,
        SettlementAssetConfig config,
        System.Random rng,
        Vector3 rootPos,
        bool hasClusterCenter,
        Vector3 clusterCenterWorld,
        List<(Vector2 pos, float radius)> occupiedZones,
        List<(Vector2 pos, float radius)> riverZones,
        bool isMarketStall = false,
        SettlementRole role = SettlementRole.MarketSquare)
    {
        if (prefab == null) return;

        Vector3 edgeMid = RiverSplinePath.GetEdgeMidpointLocal(dir);
        float preferredAngleDeg = Mathf.Atan2(edgeMid.z, edgeMid.x) * Mathf.Rad2Deg;
        float minR = Mathf.Max(0.46f, baseDist - 0.08f);
        float maxR = Mathf.Min(0.70f, baseDist + 0.08f);

        if (!TryFindSpreadBuildingOffset(rng, preferredAngleDeg, minR, maxR, footprintRadius, occupiedZones, riverZones, out Vector2 local2D))
        {
            return;
        }

        occupiedZones.Add((local2D, footprintRadius));
        Vector3 spawnPos = rootPos + new Vector3(local2D.x, config.buildingYOffset, local2D.y);

        Vector3 faceDir = hasClusterCenter ? (clusterCenterWorld - spawnPos) : new Vector3(-local2D.x, 0, -local2D.y);
        faceDir.y = 0;
        Quaternion rot = faceDir.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(faceDir.normalized, Vector3.up) * Quaternion.Euler(0, config.houseFacingOffset, 0)
            : Quaternion.identity;

        SpawnModel(prefab, spawnPos, rot, config.GetScale(SettlementScaleType.Building));
        if (isMarketStall) SpawnMarketStallVignette(spawnPos, rot, config, rng);
        else SpawnBuildingVignette(spawnPos, rot, role, config, rng);
        SpawnHouseFootpath(config, rng, new Vector3(local2D.x, 0, local2D.y));
    }

    private static List<int> PickMaximallySeparatedDirs(List<int> availableDirs, int count, System.Random rng)
    {
        List<int> chosen = new List<int>();
        if (availableDirs == null || availableDirs.Count == 0 || count <= 0) return chosen;

        if (availableDirs.Count <= count)
        {
            chosen.AddRange(availableDirs);
            return chosen;
        }

        if (count == 1)
        {
            chosen.Add(availableDirs[rng.Next(0, availableDirs.Count)]);
            return chosen;
        }

        if (count == 2)
        {
            int bestD1 = availableDirs[0];
            int bestD2 = availableDirs[availableDirs.Count - 1];
            int maxDiff = -1;
            int startIdx = rng.Next(0, availableDirs.Count);
            for (int a = 0; a < availableDirs.Count; a++)
            {
                int idxA = (startIdx + a) % availableDirs.Count;
                for (int b = a + 1; b < availableDirs.Count; b++)
                {
                    int idxB = (startIdx + b) % availableDirs.Count;
                    int diff = Mathf.Abs(availableDirs[idxA] - availableDirs[idxB]);
                    if (diff > 3) diff = 6 - diff;
                    if (diff > maxDiff)
                    {
                        maxDiff = diff;
                        bestD1 = availableDirs[idxA];
                        bestD2 = availableDirs[idxB];
                    }
                }
            }
            chosen.Add(bestD1);
            chosen.Add(bestD2);
            return chosen;
        }

        List<int> sorted = new List<int>(availableDirs);
        sorted.Sort();
        float step = (float)(sorted.Count - 1) / Mathf.Max(1, count - 1);
        for (int i = 0; i < count; i++)
        {
            int idx = Mathf.Clamp(Mathf.RoundToInt(i * step), 0, sorted.Count - 1);
            if (!chosen.Contains(sorted[idx])) chosen.Add(sorted[idx]);
        }
        for (int i = 0; i < sorted.Count && chosen.Count < count; i++)
        {
            if (!chosen.Contains(sorted[i])) chosen.Add(sorted[i]);
        }
        return chosen;
    }

    private List<(Vector2 pos, float radius)> BuildRiverBlockedZones(RiverTileSegment riverSeg)
    {
        List<(Vector2 pos, float radius)> zones = new List<(Vector2, float)>();
        if (riverSeg == null) return zones;

        if (riverSeg.pieceType == RiverPieceType.SpringHead)
        {
            zones.Add((Vector2.zero, 0.42f));
        }
        else if (riverSeg.pieceType == RiverPieceType.PondBasin)
        {
            zones.Add((Vector2.zero, 0.44f));
        }

        Vector3 rootPos = ContentRoot.position;
        if (riverSeg.CachedPaths != null && riverSeg.CachedPaths.Count > 0)
        {
            foreach (var path in riverSeg.CachedPaths)
            {
                if (path == null) continue;
                for (int i = 0; i < path.Count; i++)
                {
                    Vector3 wp = path[i].position;
                    zones.Add((new Vector2(wp.x - rootPos.x, wp.z - rootPos.z), 0.25f));
                }
            }
        }
        else
        {
            for (int d = 0; d < 6; d++)
            {
                if (!riverSeg.HasPort(d)) continue;
                Vector3 norm = RiverSplinePath.GetEdgeNormalLocal(d);
                for (float dist = 0.20f; dist <= 0.95f; dist += 0.20f)
                {
                    Vector3 p = norm * dist;
                    zones.Add((new Vector2(p.x, p.z), 0.24f));
                }
            }
        }

        return zones;
    }

    private static bool IsPointInZones(Vector2 pt, float radius, List<(Vector2 pos, float radius)> zones)
    {
        if (zones == null) return false;
        for (int i = 0; i < zones.Count; i++)
        {
            if (Vector2.Distance(pt, zones[i].pos) < radius + zones[i].radius)
                return true;
        }
        return false;
    }

    private static float GetMinClearance(Vector2 pt, float radius, List<(Vector2 pos, float radius)> occupiedZones, List<(Vector2 pos, float radius)> riverZones)
    {
        float minC = float.MaxValue;
        if (riverZones != null)
        {
            for (int i = 0; i < riverZones.Count; i++)
            {
                float c = Vector2.Distance(pt, riverZones[i].pos) - (radius + riverZones[i].radius);
                if (c < minC) minC = c;
            }
        }
        if (occupiedZones != null)
        {
            for (int i = 0; i < occupiedZones.Count; i++)
            {
                float c = Vector2.Distance(pt, occupiedZones[i].pos) - (radius + occupiedZones[i].radius);
                if (c < minC) minC = c;
            }
        }
        return minC;
    }

    private static bool TryFindSpreadBuildingOffset(
        System.Random rng,
        float preferredAngleDeg,
        float minRadius,
        float maxRadius,
        float buildingRadius,
        List<(Vector2 pos, float radius)> occupiedZones,
        List<(Vector2 pos, float radius)> riverZones,
        out Vector2 chosenLocal)
    {
        float[] angleOffsets = { 0f, 25f, -25f, 50f, -50f, 75f, -75f, 105f, -105f, 135f, -135f, 160f, -160f, 180f };
        float preferredR = Mathf.Lerp(minRadius, maxRadius, (float)rng.NextDouble());
        float[] radii = { preferredR, maxRadius, (minRadius + maxRadius) * 0.5f, minRadius };

        Vector2 bestFallback = Vector2.zero;
        float bestFallbackClearance = float.MinValue;

        for (int a = 0; a < angleOffsets.Length; a++)
        {
            float angRad = (preferredAngleDeg + angleOffsets[a]) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angRad), Mathf.Sin(angRad));

            for (int r = 0; r < radii.Length; r++)
            {
                float rad = Mathf.Clamp(radii[r], 0.30f, 0.72f);
                Vector2 candidate = dir * rad;
                float clearance = GetMinClearance(candidate, buildingRadius, occupiedZones, riverZones);

                if (clearance >= 0f)
                {
                    chosenLocal = candidate;
                    return true;
                }

                if (clearance > bestFallbackClearance)
                {
                    bestFallbackClearance = clearance;
                    bestFallback = candidate;
                }
            }
        }

        // Chỉ chấp nhận fallback nếu không đè sâu vào sông/SpringHead
        if (bestFallbackClearance > -0.10f)
        {
            chosenLocal = bestFallback;
            return true;
        }

        chosenLocal = Vector2.zero;
        return false;
    }

    /// <summary>
    /// Sinh các căn nhà xếp dàn trải quanh ô một cách tự nhiên, né sông suối và né va chạm nhau
    /// </summary>
    private void SpawnPerimeterHouses(
        SettlementAssetConfig config,
        System.Random rng,
        int count,
        SettlementTier tier,
        List<(Vector2 pos, float radius)> occupiedZones = null,
        List<(Vector2 pos, float radius)> riverZones = null,
        RiverTileSegment riverSeg = null)
    {
        if (count <= 0) return;

        float houseBaseDist = Mathf.Clamp(Mathf.Max(config.GetHouseDistance(), 0.56f), 0.52f, 0.68f);
        float houseScale = config.GetScale(SettlementScaleType.Building);

        // Nếu có sông/đầu nguồn, ưu tiên góc bắt đầu vuông góc với hướng dòng chảy để nhà nằm trên 2 bờ khô
        float startAngle = (float)(rng.NextDouble() * 360f);
        if (riverSeg != null && riverSeg.primaryPortA >= 0)
        {
            Vector3 flowNorm = RiverSplinePath.GetEdgeNormalLocal(riverSeg.primaryPortA);
            float flowDeg = Mathf.Atan2(flowNorm.z, flowNorm.x) * Mathf.Rad2Deg;
            startAngle = flowDeg + 90f + (float)(rng.NextDouble() * 30f - 15f);
        }

        List<float> angles = new List<float>();
        if (count == 1)
        {
            angles.Add(startAngle);
        }
        else if (count == 2)
        {
            angles.Add(startAngle);
            float gap = (float)(rng.NextDouble() * 30f + 150f);
            angles.Add(startAngle + gap);
        }
        else
        {
            float jitter1 = (float)(rng.NextDouble() * 20f - 10f);
            float jitter2 = (float)(rng.NextDouble() * 20f - 10f);
            angles.Add(startAngle);
            angles.Add(startAngle + 120f + jitter1);
            angles.Add(startAngle + 240f + jitter2);
        }

        for (int i = 0; i < count && i < angles.Count; i++)
        {
            float minR = Mathf.Max(0.48f, houseBaseDist - 0.08f);
            float maxR = Mathf.Min(0.70f, houseBaseDist + 0.08f);

            Vector2 chosenLocal;
            if (occupiedZones != null || riverZones != null)
            {
                if (!TryFindSpreadBuildingOffset(rng, angles[i], minR, maxR, 0.24f, occupiedZones, riverZones, out chosenLocal))
                {
                    continue;
                }
                occupiedZones?.Add((chosenLocal, 0.24f));
            }
            else
            {
                float angleRad = angles[i] * Mathf.Deg2Rad;
                float currentDist = Mathf.Lerp(minR, maxR, (float)rng.NextDouble());
                chosenLocal = new Vector2(Mathf.Cos(angleRad) * currentDist, Mathf.Sin(angleRad) * currentDist);
            }

            Vector3 localOffset = new Vector3(chosenLocal.x, config.buildingYOffset, chosenLocal.y);
            Vector3 spawnWorldPos = ContentRoot.position + localOffset;

            Vector3 lookDir = -localOffset;
            lookDir.y = 0;
            Quaternion lookRot = lookDir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(lookDir.normalized, Vector3.up)
                : Quaternion.identity;

            float rotJitter = (float)(rng.NextDouble() * 30f - 15f);
            lookRot *= Quaternion.Euler(0, config.houseFacingOffset + rotJitter, 0);

            GameObject housePrefab = (tier >= SettlementTier.Town && rng.NextDouble() > 0.45)
                ? config.GetRandomMediumHouse(rng)
                : config.GetRandomSmallHouse(rng);

            if (housePrefab != null)
            {
                SpawnModel(housePrefab, spawnWorldPos, lookRot, houseScale);
                SpawnHouseFootpath(config, rng, localOffset);
                SpawnBuildingVignette(spawnWorldPos, lookRot, SettlementRole.Residential, config, rng);
            }
        }
    }

    /// <summary>
    /// Sinh nhà ở cho ô nằm trong cụm đô thị lớn:
    /// Đặt nhà dàn trải gần các hướng biên khả dụng, né sông suối và tránh đè nhau.
    /// </summary>
    private void SpawnClusterPerimeterHouses(
        SettlementAssetConfig config, 
        System.Random rng, 
        int count, 
        SettlementTier tier, 
        List<int> externalDirs, 
        Vector3 clusterCenterWorld,
        List<int> excludedDirs = null,
        List<(Vector2 pos, float radius)> occupiedZones = null,
        List<(Vector2 pos, float radius)> riverZones = null)
    {
        if (count <= 0) return;

        float houseBaseDist = Mathf.Clamp(Mathf.Max(config.GetHouseDistance(), 0.56f), 0.52f, 0.68f);
        float houseScale = config.GetScale(SettlementScaleType.Building);

        List<int> availableDirs = (externalDirs != null && externalDirs.Count > 0)
            ? new List<int>(externalDirs)
            : new List<int> { 0, 1, 2, 3, 4, 5 };

        if (excludedDirs != null)
        {
            foreach (int exc in excludedDirs) availableDirs.Remove(exc);
            if (availableDirs.Count == 0 && externalDirs != null && externalDirs.Count > 0)
            {
                availableDirs.AddRange(externalDirs);
            }
        }

        List<int> chosenDirs = PickMaximallySeparatedDirs(availableDirs, count, rng);

        for (int i = 0; i < chosenDirs.Count; i++)
        {
            int dir = chosenDirs[i];
            Vector3 edgeMid = RiverSplinePath.GetEdgeMidpointLocal(dir);
            float preferredAngleDeg = Mathf.Atan2(edgeMid.z, edgeMid.x) * Mathf.Rad2Deg;
            float minR = Mathf.Max(0.48f, houseBaseDist - 0.08f);
            float maxR = Mathf.Min(0.70f, houseBaseDist + 0.08f);

            Vector2 chosenLocal;
            if (occupiedZones != null || riverZones != null)
            {
                if (!TryFindSpreadBuildingOffset(rng, preferredAngleDeg, minR, maxR, 0.24f, occupiedZones, riverZones, out chosenLocal))
                {
                    continue;
                }
                occupiedZones?.Add((chosenLocal, 0.24f));
            }
            else
            {
                Vector3 dirNorm = edgeMid.normalized;
                float currentDist = Mathf.Lerp(minR, maxR, (float)rng.NextDouble());
                Vector3 tangent = new Vector3(-dirNorm.z, 0, dirNorm.x);
                float tangentJitter = (float)(rng.NextDouble() * 0.12f - 0.06f);
                Vector3 lo = dirNorm * currentDist + tangent * tangentJitter;
                chosenLocal = new Vector2(lo.x, lo.z);
            }

            Vector3 localOffset = new Vector3(chosenLocal.x, config.buildingYOffset, chosenLocal.y);
            Vector3 spawnWorldPos = ContentRoot.position + localOffset;

            Vector3 lookDir = (clusterCenterWorld - spawnWorldPos);
            lookDir.y = 0;
            if (lookDir.sqrMagnitude < 0.1f)
            {
                lookDir = -localOffset;
                lookDir.y = 0;
            }

            Quaternion lookRot = lookDir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(lookDir.normalized, Vector3.up)
                : Quaternion.identity;

            float rotJitter = (float)(rng.NextDouble() * 24f - 12f);
            lookRot *= Quaternion.Euler(0, config.houseFacingOffset + rotJitter, 0);

            GameObject housePrefab = (tier >= SettlementTier.Town && rng.NextDouble() > 0.45)
                ? config.GetRandomMediumHouse(rng)
                : config.GetRandomSmallHouse(rng);

            if (housePrefab != null)
            {
                SpawnModel(housePrefab, spawnWorldPos, lookRot, houseScale);
                SpawnHouseFootpath(config, rng, localOffset);
                SpawnBuildingVignette(spawnWorldPos, lookRot, SettlementRole.Residential, config, rng);
            }
        }
    }

    /// <summary>
    /// Tiểu cảnh gắn liền công trình: Chỉ sinh các Props phố thị (thùng gỗ, xe cút kít, đống gỗ xẻ, đe thợ rèn)
    /// Tạm thời bỏ đi thực vật / cây cối / hoa cỏ theo chỉ thị thiết kế
    /// </summary>
    private void SpawnBuildingVignette(
        Vector3 buildingPos, 
        Quaternion buildingRot, 
        SettlementRole role, 
        SettlementAssetConfig config, 
        System.Random rng)
    {
        if (config == null) return;
        float propScale = config.GetScale(SettlementScaleType.Prop);

        Vector3 forward = buildingRot * Vector3.forward;
        Vector3 right = buildingRot * Vector3.right;

        // TIỂU CẢNH BÊN HÔNG NHÀ (Side Yard Prop Vignette)
        // Dựa củi, đống đá, xe rùa, thùng gỗ sát vào vách tường hông
        float sideSign = rng.Next(0, 2) == 0 ? 1f : -1f;
        Vector3 sidePos = buildingPos + right * (0.19f * sideSign) + forward * 0.03f;
        sidePos.y = config.buildingYOffset;

        GameObject sideProp = null;
        if (role == SettlementRole.Craftsman)
        {
            sideProp = FindPropMatching(config, "lumber", "stone", "barrel");
        }
        else if (role == SettlementRole.MarketSquare)
        {
            sideProp = FindPropMatching(config, "crate", "barrel", "sack");
        }
        else if (role == SettlementRole.Military)
        {
            sideProp = FindPropMatching(config, "weaponrack", "target", "crate");
        }
        else
        {
            sideProp = FindPropMatching(config, "lumber", "wheelbarrow", "barrel", "sack");
        }

        if (sideProp == null) sideProp = config.GetRandomStreetProp(rng);
        if (sideProp != null)
        {
            Quaternion pRot = Quaternion.Euler(0, (float)(rng.NextDouble() * 360f), 0);
            SpawnModel(sideProp, sidePos, pRot, propScale);
        }
    }

    /// <summary>
    /// Tiểu cảnh đặc thù sạp hàng chợ: Bày thùng hoa quả, sọt cá, bao ngũ cốc, thùng rượu ngay chân sạp
    /// </summary>
    private void SpawnMarketStallVignette(Vector3 stallPos, Quaternion stallRot, SettlementAssetConfig config, System.Random rng)
    {
        if (config == null) return;
        float propScale = config.GetScale(SettlementScaleType.Prop);
        Vector3 forward = stallRot * Vector3.forward;
        Vector3 right = stallRot * Vector3.right;

        // 3 kiện hàng / thùng / bao tải bày so le trước và hai bên quầy hàng
        string[] marketProps = { "crate", "barrel", "sack", "pallet" };
        for (int i = 0; i < 3; i++)
        {
            GameObject prop = FindPropMatching(config, marketProps[i % marketProps.Length]);
            if (prop == null) prop = config.GetRandomStreetProp(rng);
            if (prop != null)
            {
                float sideOff = (i == 0 ? -0.18f : (i == 1 ? 0.18f : 0f));
                float fwdOff = (i == 2 ? 0.16f : 0.06f);
                Vector3 pos = stallPos + right * sideOff + forward * fwdOff;
                pos.y = config.buildingYOffset;
                Quaternion rot = Quaternion.Euler(0, (float)(rng.NextDouble() * 360f), 0);
                SpawnModel(prop, pos, rot, propScale);
            }
        }
    }

    private GameObject FindPropMatching(SettlementAssetConfig config, params string[] keywords)
    {
        if (config == null || config.streetProps == null || config.streetProps.Count == 0) return null;
        foreach (var kw in keywords)
        {
            GameObject match = config.streetProps.Find(p => p != null && p.name.ToLower().Contains(kw));
            if (match != null) return match;
        }
        return null;
    }

    /// <summary>
    /// Sinh thảm thực vật đa tầng (Tạm thời bỏ đi thực vật / cây cối / hoa dại theo chỉ thị thiết kế)
    /// </summary>
    private void SpawnDistrictNatureFillers(SettlementAssetConfig config, System.Random rng, List<int> externalDirs)
    {
        // Tạm thời bỏ đi cây cối / hoa cỏ để tập trung vào công trình và props
    }

    /// <summary>
    /// Lối vào nhà (Tạm thời bỏ đi đường đi lát đá theo chỉ thị thiết kế)
    /// </summary>
    private void SpawnHouseFootpath(SettlementAssetConfig config, System.Random rng, Vector3 houseLocalPos)
    {
    }

    private GameObject SpawnSteppingStone(GameObject stonePrefab, Vector3 worldPos, System.Random rng, float stoneScale)
    {
        return null;
    }

    /// <summary>
    /// Quảng trường lát đá (Tạm thời bỏ đi đường đi lát đá theo chỉ thị thiết kế)
    /// </summary>
    private void SpawnCobblestonePlaza(
        SettlementAssetConfig config, 
        System.Random rng, 
        Vector3 centerWorldPos, 
        float radius, 
        int count)
    {
    }

    /// <summary>
    /// Rải phụ kiện phố thị (thùng gỗ, xe cút kít, sạp hàng nhỏ, bao tải) dọc theo ranh giới ô nội bộ
    /// </summary>
    private void SpawnStreetPropsOnInternalEdges(
        SettlementAssetConfig config, 
        System.Random rng, 
        List<int> internalDirs)
    {
        if (internalDirs == null || internalDirs.Count == 0 || config == null) return;

        float propScale = config.GetScale(SettlementScaleType.Prop);

        foreach (int dir in internalDirs)
        {
            // Kiểm tra an toàn: Nếu ô láng giềng theo hướng này có chênh lệch cao độ (> 0.35m)
            // thì đây là mép vách đá, tuyệt đối không rải prop kẻo rơi lơ lửng ngoài vực!
            HexCoordinates neighbor = Coordinates.GetNeighbor(dir);
            if (SettlementClusterManager.Instance != null &&
                SettlementClusterManager.Instance.TryGetSettlementTile(neighbor, out SettlementTile nTile) && nTile != null)
            {
                if (Mathf.Abs(nTile.GetTileSurfaceY() - GetTileSurfaceY()) > 0.35f)
                {
                    continue;
                }
            }

            Vector3 edgeMid = RiverSplinePath.GetEdgeMidpointLocal(dir);
            Vector3 dirNorm = edgeMid.normalized;
            Vector3 tangent = new Vector3(-dirNorm.z, 0, dirNorm.x);

            // Đặt 1 phụ kiện ven đường phố (thùng gỗ, bao tải) dạt sát lề đường
            GameObject propModel = FindPropMatching(config, "barrel", "crate", "sack");
            if (propModel == null) propModel = config.GetRandomStreetProp(rng);
            if (propModel != null)
            {
                float streetDist = (float)(rng.NextDouble() * 0.15f + 0.42f);
                float sideSign = rng.Next(0, 2) == 0 ? 1f : -1f;
                float sideOffset = (float)(rng.NextDouble() * 0.08f + 0.14f) * sideSign;

                Vector3 propOffset = dirNorm * streetDist + tangent * sideOffset;
                propOffset.y = config.buildingYOffset;

                Quaternion propRot = Quaternion.Euler(0, (float)(rng.NextDouble() * 360f), 0);
                SpawnModel(propModel, ContentRoot.position + propOffset, propRot, propScale);
            }
        }
    }

    private GameObject SpawnModel(GameObject prefab, Vector3 worldPos, Quaternion rot, float scale)
    {
        if (!SettlementAssetConfig.IsValidPrefab(prefab)) return null;

        try
        {
            Quaternion prefabRot = Quaternion.identity;
            Vector3 prefabScale = Vector3.one;
            try
            {
                if (prefab.transform != null)
                {
                    prefabRot = prefab.transform.rotation;
                    prefabScale = prefab.transform.localScale;
                }
            }
            catch
            {
                return null;
            }

            // Nhân góc xoay và scale theo đúng chuẩn thiết kế gốc của prefab (giống hệ thống Biome)
            Quaternion targetRot = rot * prefabRot;
            Vector3 targetScale = Vector3.Scale(prefabScale, Vector3.one * scale);

            GameObject instance = Instantiate(prefab, worldPos, targetRot, ContentRoot);
            if (instance != null)
            {
                instance.transform.localScale = targetScale;
                _spawnedInstances.Add(instance);
            }
            return instance;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SettlementTile] SpawnModel failed for {(prefab != null ? prefab.name : "null")}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Trung hòa & tái phân phối thảm thực vật / đá tảng Biome khi ô có Building (hoặc cả Building + Biome + River):
    /// 1. Giữ nguyên thảm thực vật nhỏ (cỏ, hoa, nấm, bụi) ở mọi khoảng trống hợp lệ; chỉ đẩy dạt hoặc ẩn nếu đè khít chân móng nhà, lòng suối hoặc đúng cạnh có tường thành.
    /// 2. Với cây lớn / tảng đá bị đè vào nhà, suối hoặc tường thành: thử tìm vị trí trống khác trên ô (góc sân, bãi trống) để phân phối lại đều đặn thay vì xóa trắng.
    /// 3. Cân bằng tỉ lệ đá/cây vừa phải để Biome, Building và Sông suối đều nổi bật hài hòa, không cái nào lấn át hay làm lu mờ cái nào.
    /// </summary>
    public void HarmonizeBiomePropsForSettlement(SettlementAssetConfig config, List<int> externalDirs)
    {
        Vector2 tileCenter2D = new Vector2(transform.position.x, transform.position.z);

        // 1. Thu thập footprint thực tế của các công trình chính (bỏ qua phụ kiện nhỏ như thùng gỗ/bao tải/cờ)
        List<(Vector2 localPos, float radius)> buildingZones = new List<(Vector2, float)>();
        foreach (var inst in _spawnedInstances)
        {
            if (inst == null) continue;
            string instName = inst.name.ToLowerInvariant();

            float radius;
            if (instName.Contains("castle") || instName.Contains("church")) radius = 0.36f;
            else if (instName.Contains("building_") || instName.Contains("tower") || instName.Contains("market") || instName.Contains("blacksmith") || instName.Contains("tavern") || instName.Contains("windmill") || instName.Contains("barracks")) radius = 0.25f;
            else if (instName.Contains("well") || instName.Contains("tent") || instName.Contains("stage") || instName.Contains("scaffold")) radius = 0.18f;
            else continue; // Phụ kiện nhỏ ven đường không xóa thảm thực vật

            Vector2 local2D = new Vector2(inst.transform.position.x - tileCenter2D.x, inst.transform.position.z - tileCenter2D.y);
            buildingZones.Add((local2D, radius));
        }

        // 2. Thu thập vùng mặt nước nếu ô có sông/đầu nguồn
        RiverTileSegment riverSeg = (RiverNetworkManager.Instance != null && RiverNetworkManager.Instance.HasRiver(Coordinates))
            ? RiverNetworkManager.Instance.GetRiver(Coordinates)
            : null;
        List<(Vector2 localPos, float radius)> riverZones = BuildRiverBlockedZones(riverSeg);

        // 3. Thu thập các cạnh có tường thành/hàng rào thực tế
        List<Vector2> wallNormals = new List<Vector2>();
        if (externalDirs != null)
        {
            foreach (int dir in externalDirs)
            {
                Vector3 n = RiverSplinePath.GetEdgeNormalLocal(dir);
                wallNormals.Add(new Vector2(n.x, n.z).normalized);
            }
        }

        // Danh sách các prop lớn đã giữ chỗ để tránh dồn 2 cây/đá vào cùng 1 điểm khi tái phân phối
        List<(Vector2 localPos, float radius)> placedLargeProps = new List<(Vector2, float)>();

        int seed = (Coordinates.Q * 48611) ^ (Coordinates.R * 96179) ^ 1337;
        System.Random rng = new System.Random(seed);

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform container = transform.GetChild(i);
            if (container == null || container == contentRoot) continue;

            string cName = container.name;
            if (cName.StartsWith("River_") || cName.StartsWith("Water_") || cName.StartsWith("Waterfall_")) continue;
            if (cName.StartsWith("PlacedSettlement_") || cName.StartsWith("ConnectingTrenches")) continue;

            List<Transform> props = new List<Transform>();
            CollectBiomePropsDirect(container, props);

            foreach (var prop in props)
            {
                if (prop == null) continue;
                Vector2 propLocal = new Vector2(prop.position.x - tileCenter2D.x, prop.position.z - tileCenter2D.y);
                string pName = prop.name.ToLowerInvariant();

                bool isRock = pName.Contains("stone") || pName.Contains("rock") || pName.Contains("boulder");
                bool isTree = pName.Contains("tree") || pName.Contains("pine") || pName.Contains("oak") || pName.Contains("willow");
                bool isBush = pName.Contains("bush") || pName.Contains("shrub") || pName.Contains("fern");

                float propRadius = isTree ? 0.14f : (isRock ? 0.12f : (isBush ? 0.07f : 0.03f));

                bool hasConflict = IsBiomePropConflicting(propLocal, propRadius, isTree || isRock, buildingZones, riverZones, wallNormals);

                if (hasConflict)
                {
                    // Thử đẩy dạt / tái phân phối vào khoảng trống hợp lệ trên ô (giữa nhà, tường và suối)
                    if (TryFindOpenBiomePropSpot(rng, propLocal, propRadius, isTree || isRock, buildingZones, riverZones, wallNormals, placedLargeProps, out Vector2 newLocal))
                    {
                        prop.position = new Vector3(tileCenter2D.x + newLocal.x, prop.position.y, tileCenter2D.y + newLocal.y);
                        propLocal = newLocal;
                        hasConflict = false;
                    }
                }

                if (hasConflict)
                {
                    prop.gameObject.SetActive(false);
                }
                else
                {
                    prop.gameObject.SetActive(true);
                    if (isTree || isRock)
                    {
                        placedLargeProps.Add((propLocal, propRadius));
                    }

                    // Cân bằng tỉ lệ nhẹ để Biome không che khuất Building/Suối nhưng vẫn giữ rõ nét đặc trưng
                    if (!prop.name.EndsWith("_scaled"))
                    {
                        if (isRock)
                        {
                            prop.localScale = prop.localScale * 0.60f;
                            prop.name = prop.name + "_scaled";
                        }
                        else if (isTree)
                        {
                            prop.localScale = prop.localScale * 0.82f;
                            prop.name = prop.name + "_scaled";
                        }
                    }
                }
            }
        }
    }

    private static bool IsBiomePropConflicting(
        Vector2 localPos,
        float propRadius,
        bool isLargeProp,
        List<(Vector2 localPos, float radius)> buildingZones,
        List<(Vector2 localPos, float radius)> riverZones,
        List<Vector2> wallNormals)
    {
        for (int i = 0; i < buildingZones.Count; i++)
        {
            float d = Vector2.Distance(localPos, buildingZones[i].localPos);
            if (d < buildingZones[i].radius + propRadius)
                return true;
        }

        for (int i = 0; i < riverZones.Count; i++)
        {
            float d = Vector2.Distance(localPos, riverZones[i].localPos);
            if (d < riverZones[i].radius + propRadius)
                return true;
        }

        // Chỉ dọn dọc đúng các cạnh thực sự có tường/rào (không xóa 360 độ)
        float wallClearLimit = HexMetrics.InnerRadius * (isLargeProp ? 0.76f : 0.83f);
        for (int i = 0; i < wallNormals.Count; i++)
        {
            float proj = Vector2.Dot(localPos, wallNormals[i]);
            if (proj > wallClearLimit)
                return true;
        }

        return false;
    }

    private static bool TryFindOpenBiomePropSpot(
        System.Random rng,
        Vector2 originalLocal,
        float propRadius,
        bool isLargeProp,
        List<(Vector2 localPos, float radius)> buildingZones,
        List<(Vector2 localPos, float radius)> riverZones,
        List<Vector2> wallNormals,
        List<(Vector2 localPos, float radius)> placedLargeProps,
        out Vector2 chosenLocal)
    {
        float baseAngle = Mathf.Atan2(originalLocal.y, originalLocal.x) * Mathf.Rad2Deg;
        float[] angleSteps = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f, 150f, -150f, 180f };
        float[] radii = isLargeProp
            ? new float[] { 0.72f, 0.62f, 0.50f, 0.78f, 0.36f }
            : new float[] { 0.58f, 0.70f, 0.44f, 0.78f, 0.30f };

        for (int r = 0; r < radii.Length; r++)
        {
            float rad = radii[r] + (float)(rng.NextDouble() * 0.06f - 0.03f);
            for (int a = 0; a < angleSteps.Length; a++)
            {
                float ang = (baseAngle + angleSteps[a]) * Mathf.Deg2Rad;
                Vector2 cand = new Vector2(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad);

                if (cand.magnitude > 0.86f) continue;
                if (IsBiomePropConflicting(cand, propRadius, isLargeProp, buildingZones, riverZones, wallNormals)) continue;

                if (isLargeProp)
                {
                    bool overlapsLarge = false;
                    for (int p = 0; p < placedLargeProps.Count; p++)
                    {
                        if (Vector2.Distance(cand, placedLargeProps[p].localPos) < propRadius + placedLargeProps[p].radius + 0.06f)
                        {
                            overlapsLarge = true;
                            break;
                        }
                    }
                    if (overlapsLarge) continue;
                }

                chosenLocal = cand;
                return true;
            }
        }

        chosenLocal = originalLocal;
        return false;
    }

    private void CollectBiomePropsDirect(Transform container, List<Transform> result)
    {
        if (container == null) return;
        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);
            if (child != null)
            {
                result.Add(child);
            }
        }
    }
}

