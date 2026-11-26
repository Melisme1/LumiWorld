using System;
using System.Collections.Generic;

/// <summary>
/// Thuật toán thuần C# (Pure Logic, không phụ thuộc MonoBehaviour/Unity)
/// chuyên trách tính toán tô-pô học, phân vai ô, xác định cấp độ và tìm vành đai ranh giới khép kín 360 độ.
/// </summary>
public static class SettlementClusterCalculator
{
    /// <summary>
    /// Cạnh biên giới ngoài cùng của cụm (Perimeter Edge)
    /// </summary>
    public struct PerimeterEdge
    {
        public HexCoordinates Hex;
        public int Direction; // Hướng 0 đến 5
        public bool IsGate;    // Có phải là vị trí đặt Cổng ra vào hay không
    }

    /// <summary>
    /// Thuật toán BFS tìm toàn bộ các ô lục giác định cư liên thông với ô bắt đầu
    /// </summary>
    public static List<HexCoordinates> FindConnectedCluster(
        HexCoordinates startHex,
        Func<HexCoordinates, bool> isSettlementPredicate)
    {
        List<HexCoordinates> cluster = new List<HexCoordinates>();
        if (isSettlementPredicate == null || !isSettlementPredicate(startHex)) return cluster;

        HashSet<HexCoordinates> visited = new HashSet<HexCoordinates>();
        Queue<HexCoordinates> queue = new Queue<HexCoordinates>();

        queue.Enqueue(startHex);
        visited.Add(startHex);

        while (queue.Count > 0)
        {
            HexCoordinates current = queue.Dequeue();
            cluster.Add(current);

            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates neighbor = current.GetNeighbor(dir);
                if (!visited.Contains(neighbor) && isSettlementPredicate(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        return cluster;
    }

    /// <summary>
    /// Kiểm tra cụm có đang phát triển theo hình thái chuỗi kéo dài (Linear / Snake) hay không.
    /// Một cụm là Linear nếu không có bất kỳ ô nào có nhiều hơn 2 hàng xóm nội bộ.
    /// </summary>
    public static bool IsLinearCluster(List<HexCoordinates> cluster)
    {
        if (cluster == null || cluster.Count < 3) return false;
        HashSet<HexCoordinates> clusterSet = new HashSet<HexCoordinates>(cluster);

        int maxDegree = 0;
        foreach (var hex in cluster)
        {
            int degree = 0;
            for (int dir = 0; dir < 6; dir++)
            {
                if (clusterSet.Contains(hex.GetNeighbor(dir))) degree++;
            }
            if (degree > maxDegree) maxDegree = degree;
        }

        // Nếu số hàng xóm lớn nhất của mọi ô chỉ <= 2 thì chắc chắn là một dải chuỗi dài
        return maxDegree <= 2;
    }

    /// <summary>
    /// Tìm ô trung tâm (Core Tile) của cụm: Là ô có nhiều hàng xóm nội bộ nhất (nằm sâu bên trong nhất).
    /// Nếu có nhiều ô bằng nhau, ưu tiên ô đất khô (không có sông) để dựng Lâu Đài hoàng gia.
    /// </summary>
    public static HexCoordinates FindCoreTile(List<HexCoordinates> cluster, Func<HexCoordinates, bool> hasRiverPredicate = null)
    {
        if (cluster == null || cluster.Count == 0) return default;
        if (cluster.Count == 1) return cluster[0];

        HashSet<HexCoordinates> clusterSet = new HashSet<HexCoordinates>(cluster);
        HexCoordinates bestTile = cluster[0];
        int maxNeighbors = -1;
        bool bestHasRiver = true;

        foreach (var hex in cluster)
        {
            int neighborCount = 0;
            for (int dir = 0; dir < 6; dir++)
            {
                if (clusterSet.Contains(hex.GetNeighbor(dir))) neighborCount++;
            }

            bool hasRiver = hasRiverPredicate != null && hasRiverPredicate(hex);

            if (neighborCount > maxNeighbors || (neighborCount == maxNeighbors && bestHasRiver && !hasRiver))
            {
                maxNeighbors = neighborCount;
                bestTile = hex;
                bestHasRiver = hasRiver;
            }
        }

        return bestTile;
    }

    /// <summary>
    /// Xác định cấp độ quy mô của Quần thể định cư dựa trên số ô và hình thái cụm
    /// </summary>
    public static SettlementTier DetermineTier(List<HexCoordinates> cluster)
    {
        if (cluster == null || cluster.Count <= 1) return SettlementTier.Hamlet;
        if (cluster.Count < 5) return SettlementTier.Town;

        // Nếu từ 5 ô trở lên: phân biệt dạng Dải Dài (TradingAvenue) vs Dạng Tụ Lại (GrandCity)
        if (IsLinearCluster(cluster))
        {
            return SettlementTier.TradingAvenue;
        }

        return SettlementTier.GrandCity;
    }

    /// <summary>
    /// Tính toán chính xác toàn bộ các cạnh biên giới ngoài cùng để tạo VÀNH ĐAI KHÉP KÍN 360 ĐỘ.
    /// Bất biến toán học:
    /// - Cạnh nào tiếp giáp với ô ngoài cụm -> Đánh dấu là Cạnh Biên Giới Ngoài (Perimeter Edge).
    /// - Cạnh nào tiếp giáp với ô bên trong cụm -> Bỏ qua (không sinh tường, thông suốt nội bộ).
    /// </summary>
    public static List<PerimeterEdge> CalculatePerimeterEdges(
        List<HexCoordinates> cluster, 
        HexCoordinates? preferredGateHex = null)
    {
        List<PerimeterEdge> edges = new List<PerimeterEdge>();
        if (cluster == null || cluster.Count == 0) return edges;

        HashSet<HexCoordinates> clusterSet = new HashSet<HexCoordinates>(cluster);

        // Xác định ô ưu tiên đặt Cổng (thường là ô ngoài rìa được chỉ định, hoặc ô đầu tiên)
        HexCoordinates gateTargetHex = preferredGateHex.HasValue && clusterSet.Contains(preferredGateHex.Value)
            ? preferredGateHex.Value
            : cluster[0];

        bool gateAssigned = false;

        foreach (var hex in cluster)
        {
            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates neighbor = hex.GetNeighbor(dir);

                // Nếu ô láng giềng theo hướng này KHÔNG thuộc cụm -> Đây chính là mép ngoài cùng!
                if (!clusterSet.Contains(neighbor))
                {
                    bool isGate = false;
                    if (!gateAssigned && hex == gateTargetHex)
                    {
                        isGate = true;
                        gateAssigned = true;
                    }

                    edges.Add(new PerimeterEdge
                    {
                        Hex = hex,
                        Direction = dir,
                        IsGate = isGate
                    });
                }
            }
        }

        return edges;
    }
}

