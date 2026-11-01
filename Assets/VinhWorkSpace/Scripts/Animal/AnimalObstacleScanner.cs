using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý việc quét, phân tích và lưu cache chướng ngại vật tĩnh (thân cây, tảng đá, gốc cây...)
/// trên từng ô lục giác cho hệ thống AI động vật LumiWorld.
/// </summary>
public static class AnimalObstacleScanner
{
    // Bán kính tiếp xúc vật lý của cơ thể con thú đối với chướng ngại vật tĩnh (thân cây, gốc cây, tảng đá)
    // Giữ ở mức 0.08m để thú có thể luồn lách qua các khe hở tự nhiên mà không bị kẹt hay giật hình
    public const float PropObstacleBodyRadius = 0.08f;

    // Cache chướng ngại vật tĩnh theo từng ô lục giác (tối ưu hóa 100%, không duyệt renderers mỗi frame)
    private static readonly Dictionary<HexCoordinates, List<AnimalMovementAI.TileObstacle>> TileObstaclesCache = new Dictionary<HexCoordinates, List<AnimalMovementAI.TileObstacle>>();
    private static readonly Dictionary<HexCoordinates, int> TileObstaclesChildCount = new Dictionary<HexCoordinates, int>();

    /// <summary>
    /// Xóa cache khi có ô lục giác bị thay đổi hoặc biến đổi địa hình
    /// </summary>
    public static void InvalidateObstacleCache(HexCoordinates hex)
    {
        TileObstaclesCache.Remove(hex);
        TileObstaclesChildCount.Remove(hex);
    }

    /// <summary>
    /// Xóa toàn bộ cache chướng ngại vật (ví dụ khi sinh đảo mới)
    /// </summary>
    public static void ClearAllObstacleCaches()
    {
        TileObstaclesCache.Clear();
        TileObstaclesChildCount.Clear();
    }

    /// <summary>
    /// Lấy danh sách chướng ngại vật trong phạm vi xung quanh tọa độ kiểm tra (từ ô hiện tại và các ô láng giềng)
    /// </summary>
    public static void GetNearbyObstacles(HexWorldGenerator worldGen, Vector3 center, float searchRadius, List<AnimalMovementAI.TileObstacle> results)
    {
        results.Clear();
        if (worldGen == null || worldGen.MapTiles == null) return;

        HexCoordinates centerHex = HexMetrics.WorldToHex(center);
        AddTileObstaclesIfValid(worldGen, centerHex, center, searchRadius, results);

        for (int d = 0; d < 6; d++)
        {
            HexCoordinates nHex = centerHex.GetNeighbor(d);
            AddTileObstaclesIfValid(worldGen, nHex, center, searchRadius, results);
        }
    }

    private static void AddTileObstaclesIfValid(HexWorldGenerator worldGen, HexCoordinates hex, Vector3 center, float searchRadius, List<AnimalMovementAI.TileObstacle> results)
    {
        List<AnimalMovementAI.TileObstacle> tileObs = GetOrUpdateTileObstacles(worldGen, hex);
        if (tileObs == null || tileObs.Count == 0) return;

        for (int i = 0; i < tileObs.Count; i++)
        {
            AnimalMovementAI.TileObstacle obs = tileObs[i];
            if (obs.transform == null) continue;

            float dx = obs.worldPos.x - center.x;
            float dz = obs.worldPos.z - center.z;
            float maxAllowedDist = searchRadius + obs.radius;
            if (dx * dx + dz * dz <= maxAllowedDist * maxAllowedDist)
            {
                results.Add(obs);
            }
        }
    }

    public static List<AnimalMovementAI.TileObstacle> GetOrUpdateTileObstacles(HexWorldGenerator worldGen, HexCoordinates hex)
    {
        if (worldGen == null || worldGen.MapTiles == null) return null;
        if (!worldGen.MapTiles.TryGetValue(hex, out GameObject tileObj) || tileObj == null)
        {
            TileObstaclesCache.Remove(hex);
            TileObstaclesChildCount.Remove(hex);
            return null;
        }

        int currentChildCount = tileObj.transform.childCount;
        for (int i = 0; i < tileObj.transform.childCount; i++)
        {
            Transform c = tileObj.transform.GetChild(i);
            if (c != null && c.name.StartsWith("Habitat_"))
            {
                currentChildCount += c.childCount;
            }
        }

        if (TileObstaclesCache.TryGetValue(hex, out List<AnimalMovementAI.TileObstacle> cachedList) &&
            TileObstaclesChildCount.TryGetValue(hex, out int cachedCount))
        {
            if (cachedCount == currentChildCount)
            {
                bool allAlive = true;
                for (int i = 0; i < cachedList.Count; i++)
                {
                    if (cachedList[i].transform == null)
                    {
                        allAlive = false;
                        break;
                    }
                }
                if (allAlive) return cachedList;
            }
        }

        List<AnimalMovementAI.TileObstacle> newList = TileObstaclesCache.TryGetValue(hex, out var existing) ? existing : new List<AnimalMovementAI.TileObstacle>();
        newList.Clear();

        ScanTransformForObstacles(tileObj.transform, newList);

        TileObstaclesCache[hex] = newList;
        TileObstaclesChildCount[hex] = currentChildCount;
        return newList;
    }

    public static void ScanTransformForObstacles(Transform root, List<AnimalMovementAI.TileObstacle> results)
    {
        if (root == null) return;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null) continue;

            if (child.name.StartsWith("Habitat_"))
            {
                for (int p = 0; p < child.childCount; p++)
                {
                    Transform prop = child.GetChild(p);
                    if (prop != null)
                    {
                        EvaluateAndAddObstacle(prop, results);
                    }
                }
                continue;
            }

            if (child.GetComponent<AnimalMovementAI>() != null || child.GetComponent<PlacedCard>() != null)
            {
                continue;
            }

            EvaluateAndAddObstacle(child, results);
        }
    }

    public static void EvaluateAndAddObstacle(Transform prop, List<AnimalMovementAI.TileObstacle> results)
    {
        if (prop == null) return;
        string name = prop.name;

        // 1. Kiểm tra cấu hình tường minh trên HexPropConfig nếu có
        HexPropConfig config = prop.GetComponent<HexPropConfig>();
        if (config == null) config = prop.GetComponentInChildren<HexPropConfig>();

        if (config != null)
        {
            if (config.ObstacleMode == PropObstacleMode.Passable)
            {
                return;
            }

            if (config.ObstacleMode == PropObstacleMode.SolidObstacle)
            {
                float r = config.CustomObstacleRadius > 0.01f ? config.CustomObstacleRadius : ComputeDefaultRadius(prop);
                results.Add(new AnimalMovementAI.TileObstacle
                {
                    worldPos = prop.position,
                    radius = Mathf.Clamp(r, 0.15f, 0.50f),
                    name = prop.name,
                    transform = prop
                });
                return;
            }
        }

        // 2. Chế độ AutoDetect: Bỏ qua các loại cỏ nền, mầm cỏ, hoa mềm, thảm rêu
        if (IsPassableFlora(name))
        {
            return;
        }

        // 3. Phân loại theo tên mô hình và kích thước thực tế (Chỉ các vật thể cứng thực sự: Thân cây, Gốc cây, Tảng đá, Khúc gỗ)
        float obstacleRadius = -1f;

        // Gốc cây (Stump) - vd: MossySproutStump
        if (name.IndexOf("Stump", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            obstacleRadius = Mathf.Clamp(0.10f * (prop.localScale.x / 0.35f), 0.08f, 0.15f);
        }
        // Tảng đá / Khối đá (Boulder, Rock, Slab) - vd: MossyForestBoulders, HighlandBoulderSlabs
        else if (name.IndexOf("Boulder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Rock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Slab", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            obstacleRadius = Mathf.Clamp(0.14f * (prop.localScale.x / 0.35f), 0.10f, 0.20f);
        }
        // Khúc gỗ mục / Thân cây nằm (Log) - vd: HollowMossyLog
        else if (name.IndexOf("Log", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            obstacleRadius = Mathf.Clamp(0.12f * (prop.localScale.x / 0.35f), 0.09f, 0.18f);
        }
        // Thân đại thụ & Cây lớn - vd: HeroOak, Beech, Pine, Sentinel, Hero
        else if (name.IndexOf("Oak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("GreatOak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Sentinel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Beech", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Pine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            obstacleRadius = Mathf.Clamp(0.18f * (prop.localScale.x / 1.0f), 0.14f, 0.26f);
        }
        // Cây thân vừa / nhỏ - vd: Birch, Willow, DwarfBirch, Tree
        else if (name.IndexOf("Birch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Willow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            obstacleRadius = Mathf.Clamp(0.12f * (prop.localScale.x / 1.0f), 0.09f, 0.16f);
        }

        if (obstacleRadius > 0.05f)
        {
            results.Add(new AnimalMovementAI.TileObstacle
            {
                worldPos = prop.position,
                radius = obstacleRadius,
                name = prop.name,
                transform = prop
            });
        }
    }

    public static float ComputeDefaultRadius(Transform prop)
    {
        Renderer rend = prop.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            Bounds b = rend.bounds;
            return Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.z) * 0.40f, 0.14f, 0.35f);
        }
        return 0.20f;
    }

    public static bool IsPassableFlora(string name)
    {
        if (string.IsNullOrEmpty(name)) return true;

        return name.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Tuft", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("FilledGround", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Clover", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Daisy", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Lavender", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("BellFlower", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Wildflower", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Flower", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Flora", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Pebble", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Toadstool", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Mushroom", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Bluebell", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("PrairieFlower", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Patch", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Carpet", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Tussock", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Scatter", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Fern", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Undergrowth", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Heather", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Shrub", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Bush", StringComparison.OrdinalIgnoreCase) >= 0 ||
               (name.IndexOf("Sprout", StringComparison.OrdinalIgnoreCase) >= 0 && name.IndexOf("Stump", StringComparison.OrdinalIgnoreCase) < 0);
    }
}

