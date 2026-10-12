using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Logic C# thuần túy (Pure Logic) phục vụ việc đánh giá, chọn lọc Cụm Biome và tính toán bầy đàn thú hoang.
/// Hoạt động độc lập với hệ thống cụ thể bằng cách sử dụng IncidentClusterTarget abstraction.
/// </summary>
public static class WildIncidentEvaluator
{
    /// <summary>
    /// Kiểm tra người chơi đã đạt mốc Milestone cần thiết để mở tính năng thú hoang hay chưa
    /// </summary>
    public static bool IsFeatureUnlocked(int currentMilestoneIndex, int requiredUnlockMilestone)
    {
        return currentMilestoneIndex >= requiredUnlockMilestone;
    }

    /// <summary>
    /// Thuật toán Cluster-First: Lựa chọn Cụm Đầu Não (Host Cluster) để Thú Đầu Đàn (Alpha) chiếm cứ.
    /// Ưu tiên các cụm có quy mô lớn (mốc 3-6-9) hoặc giá trị cao.
    /// </summary>
    public static IncidentClusterTarget PickHostCluster(IReadOnlyList<IncidentClusterTarget> clusters, int minSizeToHost)
    {
        if (clusters == null || clusters.Count == 0) return null;

        var eligibleClusters = new List<IncidentClusterTarget>();

        foreach (var c in clusters)
        {
            if (c == null) continue;
            // Bỏ qua cụm đang bị chiếm hoặc chưa đủ mốc kích thước tối thiểu (3 ô)
            if (c.IsDisrupted || c.HexCount < minSizeToHost) continue;

            eligibleClusters.Add(c);
        }

        if (eligibleClusters.Count == 0) return null;

        // Ưu tiên cụm có số ô lớn (đạt chuẩn sinh thái cao)
        eligibleClusters.Sort((a, b) => b.HexCount.CompareTo(a.HexCount));
        return eligibleClusters[Random.Range(0, Math.Min(3, eligibleClusters.Count))];
    }

    /// <summary>
    /// Tìm các Cụm Vệ Tinh lân cận gần Cụm Đầu Não nhất để thú con (Minions) tản sang quấy phá
    /// </summary>
    public static List<IncidentClusterTarget> FindNearbySatelliteClusters(
        IncidentClusterTarget hostCluster,
        IReadOnlyList<IncidentClusterTarget> allClusters,
        int count)
    {
        var result = new List<IncidentClusterTarget>();
        if (hostCluster == null || allClusters == null || count <= 0) return result;

        var candidates = new List<(IncidentClusterTarget cluster, float distance)>();

        foreach (var c in allClusters)
        {
            if (c == null || c == hostCluster || c.IsDisrupted) continue;

            float dist = Vector3.Distance(hostCluster.CenterPosition, c.CenterPosition);
            candidates.Add((c, dist));
        }

        // Sắp xếp theo khoảng cách từ gần đến xa
        candidates.Sort((a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < count && i < candidates.Count; i++)
        {
            result.Add(candidates[i].cluster);
        }

        return result;
    }
}
