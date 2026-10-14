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
    /// Đánh giá cấp độ tiến trình toàn diện của người chơi kết hợp giữa:
    /// 1. Mốc phần thưởng / mở rộng đất (ScoreManager.GrantedMilestoneCount).
    /// 2. Quy mô cụm sinh thái thực tế trên đảo (Cụm 3 - 6 - 9 ô).
    /// 3. Số lượng các cụm đất đang có trên toàn bản đồ.
    /// Giúp Rank thú hoang phản hồi chân thực theo sự trù phú của thế giới game.
    /// </summary>
    public static int EvaluateProgressionTier(int scoreMilestoneCount, IReadOnlyList<IncidentClusterTarget> clusters)
    {
        int tier = scoreMilestoneCount;

        if (clusters != null && clusters.Count > 0)
        {
            int maxClusterSize = 0;
            int validClusters = 0;
            foreach (var c in clusters)
            {
                if (c == null) continue;
                if (c.HexCount > maxClusterSize) maxClusterSize = c.HexCount;
                if (c.HexCount >= 3) validClusters++;
            }

            // Nếu người chơi đã xây dựng Cụm 9+ ô (Kỳ quan sinh thái) -> Cấp độ tối thiểu Tier 3 (có thể xuất hiện Epic 4★)
            if (maxClusterSize >= 9) tier = Math.Max(tier, 3);
            // Nếu người chơi đã xây dựng Cụm 6+ ô (Cụm trưởng thành) -> Cấp độ tối thiểu Tier 2 (xuất hiện Rare 3★)
            else if (maxClusterSize >= 6) tier = Math.Max(tier, 2);

            // Nếu có từ 3 cụm đất riêng biệt trở lên -> Nâng thêm bậc trù phú
            if (validClusters >= 3) tier = Math.Max(tier, 2);
        }

        return tier;
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
