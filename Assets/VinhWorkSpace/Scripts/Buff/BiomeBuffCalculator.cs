using System;
using System.Collections.Generic;

public enum RiverBuffTier
{
    None = 0,
    Riverside = 1,
    Peninsula = 2
}

public enum BuildingBuffTier
{
    None = 0,
    Town = 1,
    Grand = 2
}

public struct BiomeBuffState
{
    public RiverBuffTier RiverTier;
    public BuildingBuffTier BuildingTier;

    public bool HasRiverBuff => RiverTier != RiverBuffTier.None;
    public bool HasBuildingBuff => BuildingTier != BuildingBuffTier.None;
    public bool HasAnyBuff => HasRiverBuff || HasBuildingBuff;
}

public sealed class BiomeClusterBuffInfo
{
    public string ClusterKey;
    public string TerrainCardId;
    public List<HexCoordinates> Hexes = new List<HexCoordinates>();
    public BiomeBuffState BuffState;
}

/// <summary>
/// Thuật toán thuần C# (không phụ thuộc MonoBehaviour/VFX) để tính toán vùng ảnh hưởng
/// và trạng thái buff Sông / Building cho từng cụm Biome (theo luật chạm 1 ô = cả Biome nhận buff).
/// </summary>
public static class BiomeBuffCalculator
{
    /// <summary>
    /// Quy tắc tầm ảnh hưởng của cụm Building theo Phương án A (Granary Post Lv.1..5):
    /// - Lv.1–2 (1–2 ô): bán kính 1 ô
    /// - Lv.3–4 (3–4 ô): bán kính 2 ô
    /// - Lv.5 (5+ ô): bán kính 3 ô
    /// </summary>
    public static int GetBuildingAuraRadius(int clusterTileCount)
    {
        if (clusterTileCount <= 0) return 0;
        int level = BuildingRules.GetLevelForClusterSize(clusterTileCount, BuildingRules.GranaryPostMaxLevel);
        return BuildingRules.GetAuraRadius(BuildingRules.GranaryPostTypeId, level);
    }

    public static BuildingBuffTier GetBuildingBuffTier(int clusterTileCount)
    {
        if (clusterTileCount <= 0) return BuildingBuffTier.None;
        return clusterTileCount >= 5 ? BuildingBuffTier.Grand : BuildingBuffTier.Town;
    }

    /// <summary>
    /// Tìm cụm Building dự kiến khi đặt thêm một Building tại previewHex và trả về toàn bộ ô nằm trong vùng ảnh hưởng.
    /// </summary>
    public static HashSet<HexCoordinates> CollectBuildingAuraHexes(
        HexCoordinates previewHex,
        Func<HexCoordinates, bool> isExistingBuilding,
        out int prospectiveClusterSize,
        out int auraRadius)
    {
        return CollectBuildingAuraHexes(
            previewHex,
            BuildingRules.GranaryPostTypeId,
            BuildingUpgradeMethod.ClusterExpansion,
            BuildingRules.GranaryPostMaxLevel,
            1,
            isExistingBuilding,
            out prospectiveClusterSize,
            out auraRadius);
    }

    public static HashSet<HexCoordinates> CollectBuildingAuraHexes(
        HexCoordinates previewHex,
        string buildingTypeId,
        BuildingUpgradeMethod upgradeMethod,
        int maxLevel,
        int fixedLevel,
        Func<HexCoordinates, bool> isSameClusterBuilding,
        out int prospectiveClusterSize,
        out int auraRadius)
    {
        List<HexCoordinates> cluster = upgradeMethod == BuildingUpgradeMethod.ClusterExpansion
            ? CollectConnectedHexes(
                previewHex,
                h => h == previewHex || (isSameClusterBuilding != null && isSameClusterBuilding(h)))
            : new List<HexCoordinates> { previewHex };

        prospectiveClusterSize = cluster.Count;
        int level = upgradeMethod == BuildingUpgradeMethod.ClusterExpansion
            ? BuildingRules.GetLevelForClusterSize(prospectiveClusterSize, maxLevel)
            : Math.Max(1, fixedLevel);
        auraRadius = BuildingRules.GetAuraRadius(buildingTypeId, level);

        return CollectAuraHexesAroundCluster(cluster, auraRadius);
    }

    public static HashSet<HexCoordinates> CollectAuraHexesAroundCluster(IReadOnlyList<HexCoordinates> cluster, int auraRadius)
    {
        HashSet<HexCoordinates> auraHexes = new HashSet<HexCoordinates>();
        if (cluster == null || cluster.Count == 0 || auraRadius <= 0) return auraHexes;

        for (int i = 0; i < cluster.Count; i++)
        {
            AddHexesWithinRadius(cluster[i], auraRadius, auraHexes);
        }

        // Không tô aura đè lên chính các ô Building trong cụm để giữ vùng nhìn sạch sẽ
        for (int i = 0; i < cluster.Count; i++)
        {
            auraHexes.Remove(cluster[i]);
        }

        return auraHexes;
    }

    public static List<HexCoordinates> CollectConnectedBuildingCluster(
        HexCoordinates seedHex,
        Func<HexCoordinates, bool> isBuildingTile)
    {
        return CollectConnectedHexes(seedHex, isBuildingTile);
    }

    public static List<HexCoordinates> CollectConnectedRiverCluster(
        HexCoordinates seedHex,
        Func<HexCoordinates, bool> isRiverTile)
    {
        return CollectConnectedHexes(seedHex, isRiverTile);
    }

    public static HashSet<HexCoordinates> CollectRiverNetworkAuraHexes(IEnumerable<HexCoordinates> riverHexes)
    {
        HashSet<HexCoordinates> auraHexes = new HashSet<HexCoordinates>();
        if (riverHexes == null) return auraHexes;
        foreach (var rHex in riverHexes)
        {
            auraHexes.Add(rHex);
            for (int dir = 0; dir < 6; dir++)
            {
                auraHexes.Add(rHex.GetNeighbor(dir));
            }
        }
        return auraHexes;
    }

    public static RiverBuffTier EvaluateRiverBuffForCluster(
        List<HexCoordinates> clusterHexes,
        Func<HexCoordinates, bool> isRiverTile,
        HashSet<HexCoordinates> restrictToRivers = null)
    {
        if (isRiverTile == null) return RiverBuffTier.None;

        HashSet<HexCoordinates> touchingRivers = new HashSet<HexCoordinates>();
        for (int i = 0; i < clusterHexes.Count; i++)
        {
            HexCoordinates hex = clusterHexes[i];
            if (isRiverTile(hex) && (restrictToRivers == null || restrictToRivers.Contains(hex)))
                touchingRivers.Add(hex);

            int localAdjacentRivers = 0;
            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates nHex = hex.GetNeighbor(dir);
                if (isRiverTile(nHex) && (restrictToRivers == null || restrictToRivers.Contains(nHex)))
                {
                    touchingRivers.Add(nHex);
                    localAdjacentRivers++;
                }
            }

            if (localAdjacentRivers >= 2)
            {
                return RiverBuffTier.Peninsula;
            }
        }

        return touchingRivers.Count > 0 ? RiverBuffTier.Riverside : RiverBuffTier.None;
    }

    /// <summary>
    /// Lấy toàn bộ ô nằm trong vùng ảnh hưởng của một ô Sông (chính ô đó + 6 ô láng giềng).
    /// </summary>
    public static HashSet<HexCoordinates> CollectRiverAuraHexes(HexCoordinates riverHex)
    {
        HashSet<HexCoordinates> auraHexes = new HashSet<HexCoordinates> { riverHex };
        for (int dir = 0; dir < 6; dir++)
        {
            auraHexes.Add(riverHex.GetNeighbor(dir));
        }
        return auraHexes;
    }

    /// <summary>
    /// Đánh giá trạng thái buff cho toàn bộ các cụm Biome hiện có trên bản đồ.
    /// Ô đã có Building/Settlement không được tính vào cụm Biome.
    /// Các ô cùng Terrain nhưng khác clusterId (ví dụ đã tách cụm khi vượt max 10 ô) sẽ không bị gộp chung.
    /// </summary>
    public static List<BiomeClusterBuffInfo> EvaluateAllBiomeBuffs(
        IEnumerable<HexCoordinates> allHexes,
        Func<HexCoordinates, string> getBiomeTerrainId,
        Func<HexCoordinates, int> getBiomeClusterId,
        Func<HexCoordinates, bool> isBuildingTile,
        Func<HexCoordinates, bool> isRiverTile,
        Func<HexCoordinates, HexCoordinates, bool> areSameBuildingCluster = null,
        Func<HexCoordinates, int, int> getBuildingClusterAuraRadius = null)
    {
        List<BiomeClusterBuffInfo> results = new List<BiomeClusterBuffInfo>();
        if (allHexes == null || getBiomeTerrainId == null) return results;

        List<HexCoordinates> hexList = new List<HexCoordinates>(allHexes);
        hexList.Sort(CompareHex);

        // 1. Gom toàn bộ cụm Building (chỉ gộp khi cùng loại ClusterExpansion) và tầm ảnh hưởng tương ứng
        List<(List<HexCoordinates> hexes, int radius, BuildingBuffTier tier)> buildingAuras =
            new List<(List<HexCoordinates>, int, BuildingBuffTier)>();
        if (isBuildingTile != null)
        {
            HashSet<HexCoordinates> visitedBuildings = new HashSet<HexCoordinates>();
            for (int i = 0; i < hexList.Count; i++)
            {
                HexCoordinates seed = hexList[i];
                if (visitedBuildings.Contains(seed) || !isBuildingTile(seed)) continue;

                List<HexCoordinates> bCluster = CollectConnectedHexes(
                    seed,
                    h => isBuildingTile(h) && (areSameBuildingCluster == null || areSameBuildingCluster(seed, h)),
                    visitedBuildings);
                int radius = getBuildingClusterAuraRadius != null
                    ? getBuildingClusterAuraRadius(seed, bCluster.Count)
                    : GetBuildingAuraRadius(bCluster.Count);
                if (radius > 0)
                {
                    buildingAuras.Add((bCluster, radius, GetBuildingBuffTier(bCluster.Count)));
                }
            }
        }

        // 2. Gom từng cụm Biome (loại bỏ ô Building, tôn trọng clusterId) và xét chạm Sông + chạm Aura Building
        HashSet<HexCoordinates> visitedBiome = new HashSet<HexCoordinates>();
        for (int i = 0; i < hexList.Count; i++)
        {
            HexCoordinates seed = hexList[i];
            if (visitedBiome.Contains(seed)) continue;
            if (isBuildingTile != null && isBuildingTile(seed)) continue;

            string terrainId = getBiomeTerrainId(seed);
            if (string.IsNullOrEmpty(terrainId)) continue;
            int seedClusterId = getBiomeClusterId != null ? getBiomeClusterId(seed) : 0;

            List<HexCoordinates> clusterHexes = CollectConnectedHexes(
                seed,
                h => (isBuildingTile == null || !isBuildingTile(h)) &&
                     string.Equals(getBiomeTerrainId(h), terrainId, StringComparison.Ordinal) &&
                     AreClusterIdsCompatible(seedClusterId, getBiomeClusterId != null ? getBiomeClusterId(h) : 0),
                visitedBiome);

            clusterHexes.Sort(CompareHex);

            RiverBuffTier riverTier = EvaluateRiverBuffForCluster(clusterHexes, isRiverTile);
            BuildingBuffTier buildingTier = EvaluateBuildingBuffForCluster(clusterHexes, buildingAuras);

            results.Add(new BiomeClusterBuffInfo
            {
                ClusterKey = $"{terrainId}:{seedClusterId}:{clusterHexes[0].Q},{clusterHexes[0].R}",
                TerrainCardId = terrainId,
                Hexes = clusterHexes,
                BuffState = new BiomeBuffState
                {
                    RiverTier = riverTier,
                    BuildingTier = buildingTier
                }
            });
        }

        return results;
    }

    private static bool AreClusterIdsCompatible(int idA, int idB)
    {
        if (idA <= 0 || idB <= 0) return true;
        return idA == idB;
    }

    private static RiverBuffTier EvaluateRiverBuffForCluster(
        List<HexCoordinates> clusterHexes,
        Func<HexCoordinates, bool> isRiverTile)
    {
        return EvaluateRiverBuffForCluster(clusterHexes, isRiverTile, null);
    }

    private static BuildingBuffTier EvaluateBuildingBuffForCluster(
        List<HexCoordinates> clusterHexes,
        List<(List<HexCoordinates> hexes, int radius, BuildingBuffTier tier)> buildingAuras)
    {
        BuildingBuffTier bestTier = BuildingBuffTier.None;

        for (int b = 0; b < buildingAuras.Count; b++)
        {
            var aura = buildingAuras[b];
            if (aura.tier <= bestTier) continue;

            if (IsAnyHexWithinRadius(clusterHexes, aura.hexes, aura.radius))
            {
                bestTier = aura.tier;
                if (bestTier == BuildingBuffTier.Grand) break;
            }
        }

        return bestTier;
    }

    private static bool IsAnyHexWithinRadius(
        List<HexCoordinates> clusterHexes,
        List<HexCoordinates> sourceHexes,
        int radius)
    {
        for (int i = 0; i < clusterHexes.Count; i++)
        {
            HexCoordinates cHex = clusterHexes[i];
            for (int j = 0; j < sourceHexes.Count; j++)
            {
                if (HexCoordinates.Distance(cHex, sourceHexes[j]) <= radius)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static void AddHexesWithinRadius(HexCoordinates center, int radius, HashSet<HexCoordinates> target)
    {
        for (int dq = -radius; dq <= radius; dq++)
        {
            int rMin = Math.Max(-radius, -dq - radius);
            int rMax = Math.Min(radius, -dq + radius);
            for (int dr = rMin; dr <= rMax; dr++)
            {
                target.Add(new HexCoordinates(center.Q + dq, center.R + dr));
            }
        }
    }

    private static List<HexCoordinates> CollectConnectedHexes(
        HexCoordinates seed,
        Func<HexCoordinates, bool> predicate,
        HashSet<HexCoordinates> externalVisited = null)
    {
        List<HexCoordinates> cluster = new List<HexCoordinates>();
        if (predicate == null || !predicate(seed)) return cluster;

        HashSet<HexCoordinates> visited = externalVisited ?? new HashSet<HexCoordinates>();
        Queue<HexCoordinates> queue = new Queue<HexCoordinates>();

        if (!visited.Add(seed)) return cluster;
        queue.Enqueue(seed);

        while (queue.Count > 0)
        {
            HexCoordinates current = queue.Dequeue();
            cluster.Add(current);

            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates neighbor = current.GetNeighbor(dir);
                if (!visited.Contains(neighbor) && predicate(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        return cluster;
    }

    private static int CompareHex(HexCoordinates a, HexCoordinates b)
    {
        int qCompare = a.Q.CompareTo(b.Q);
        return qCompare != 0 ? qCompare : a.R.CompareTo(b.R);
    }
}
