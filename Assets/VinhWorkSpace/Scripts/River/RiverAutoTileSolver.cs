using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cấu trúc kết quả tính toán tự động chọn loại miếng suối và góc xoay
/// </summary>
public struct RiverTileSolution
{
    public RiverPieceType pieceType;
    public float rotationY;       // Góc xoay trục Y (độ) để khớp với các cổng kết nối
    public int connectedPortsMask;// Bitmask các hướng (0..5) đã kết nối
    public int connectionCount;   // Số lượng nhánh suối kề bên kết nối
    public int primaryPortA;      // Cổng dòng chính A (Inlet hoặc cổng chính 1)
    public int primaryPortB;      // Cổng dòng chính B (Outlet hoặc cổng chính 2)

    public bool HasPort(int dir) => (connectedPortsMask & (1 << dir)) != 0;
}

/// <summary>
/// Thuật toán giải quyết Auto-Tiling thông minh cho mạng lưới sông suối trên lưới lục giác (Pointed-Top).
/// Hoàn toàn tách biệt khỏi MonoBehaviour / UI để dễ test và tái sử dụng.
/// </summary>
public static class RiverAutoTileSolver
{
    /// <summary>
    /// Lấy góc la bàn (độ) trong không gian thế giới Unity (trục Y) của hướng lục giác từ 0 đến 5.
    /// Hướng 5 = 30°, Hướng 0 = 90°, Hướng 1 = 150°, Hướng 2 = 210°, Hướng 3 = 270°, Hướng 4 = 330°.
    /// </summary>
    public static float GetDirectionAngle(int dir)
    {
        int index = ((dir % 6) + 6) % 6;
        HexCoordinates neighbor = HexCoordinates.Directions[index];
        Vector3 worldVec = HexMetrics.HexToWorldPosition(neighbor, 0) - HexMetrics.HexToWorldPosition(new HexCoordinates(0, 0), 0);
        return Mathf.Atan2(worldVec.x, worldVec.z) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// Tính toán miếng suối phù hợp nhất và góc xoay dựa trên danh sách các hướng láng giềng có suối,
    /// vị trí khởi nguồn (Origin ở đỉnh núi cao) và địa hình hạ lưu (Terminal pond).
    /// </summary>
    /// <param name="connectedDirections">Danh sách các hướng (0..5) có ô suối kề bên</param>
    /// <param name="isOrigin">Ô này có phải là Đầu nguồn ở đỉnh núi (Spring Head) hay không</param>
    /// <param name="downhillDir">Hướng nghiêng dốc xuống về thung lũng (khi chưa có nhánh kết nối)</param>
    /// <param name="isLowTierTerminal">Ô này có phải tầng thấp nhất (Tier 0) cuối dòng suối để đọng thành hồ không</param>
    /// <param name="upstreamDir">Hướng kề bên (0..5) dẫn về ô thượng nguồn (flowStep nhỏ hơn), giúp suối thẳng luôn quay đúng chiều dòng chảy</param>
    public static RiverTileSolution Solve(
        List<int> connectedDirections, 
        bool isOrigin = false, 
        int downhillDir = 0, 
        bool isLowTierTerminal = false,
        int upstreamDir = -1,
        int outflowDir = -1)
    {
        RiverTileSolution solution = new RiverTileSolution
        {
            connectedPortsMask = 0,
            connectionCount = connectedDirections != null ? connectedDirections.Count : 0,
            primaryPortA = -1,
            primaryPortB = -1
        };

        if (connectedDirections != null)
        {
            foreach (int d in connectedDirections)
            {
                solution.connectedPortsMask |= (1 << (((d % 6) + 6) % 6));
            }
        }

        // 1. Trường hợp Ô Thượng Nguồn (isOrigin == true):
        // Luôn luôn giữ nguyên bản chất là Hang Nguồn (SpringHead), TUYỆT ĐỐI không bao giờ bị biến dạng thành khúc cua hay suối thẳng!
        if (isOrigin)
        {
            solution.pieceType = RiverPieceType.SpringHead;
            if (solution.connectionCount == 0)
            {
                solution.rotationY = GetDirectionAngle(downhillDir);
            }
            else if (solution.connectionCount == 1)
            {
                // Miệng hang quay thẳng vào ô hạ lưu tiếp theo
                solution.rotationY = GetDirectionAngle(connectedDirections[0]);
                solution.primaryPortA = connectedDirections[0];
            }
            else
            {
                // Khi có 2+ nhánh hạ lưu kề bên:
                // Nếu có hướng đổ dốc xuống (outflowDir), ưu tiên quay theo hướng dốc/hạ lưu đó
                if (outflowDir >= 0 && connectedDirections.Contains(outflowDir))
                {
                    solution.rotationY = GetDirectionAngle(outflowDir);
                    solution.primaryPortA = outflowDir;
                }
                else
                {
                    float a0 = GetDirectionAngle(connectedDirections[0]);
                    float a1 = GetDirectionAngle(connectedDirections[1]);
                    solution.rotationY = Mathf.LerpAngle(a0, a1, 0.5f);
                    solution.primaryPortA = connectedDirections[0];
                    solution.primaryPortB = connectedDirections[1];
                }
            }
            return solution;
        }

        // 2. Trường hợp 0 kết nối (ô suối đứng biệt lập):
        if (solution.connectionCount == 0)
        {
            if (isOrigin)
            {
                solution.pieceType = RiverPieceType.SpringHead;
                solution.rotationY = GetDirectionAngle(downhillDir);
            }
            else
            {
                // Ở tầng thấp hoặc ô không phải origin: Biến thành Hồ nước (PondBasin), TUYỆT ĐỐI không sinh Hang Nguồn Đá ở tầng dưới
                solution.pieceType = RiverPieceType.PondBasin;
                solution.rotationY = GetDirectionAngle(downhillDir);
            }
            return solution;
        }

        // 3. Trường hợp 1 kết nối:
        if (solution.connectionCount == 1)
        {
            int dir = connectedDirections[0];
            // Là điểm cuối của dòng suối: Đọng thành HỒ (PondBasin)
            solution.pieceType = RiverPieceType.PondBasin;
            solution.rotationY = GetDirectionAngle(dir);
            solution.primaryPortA = dir;
            return solution;
        }

        // 4. Trường hợp 2+ kết nối (Dòng chảy liên tục, hỗ trợ phân nhánh từ dòng chính):
        // GIỮ NGUYÊN DÒNG CHÍNH (Straight / Curve120 / Curve60) xuyên suốt giữa 2 cổng chính.
        // Bất kỳ cổng thứ 3, thứ 4... sẽ được kết nối như nhánh phụ (Tributary) từ mép vào tâm!
        if (solution.connectionCount >= 2)
        {
            // Điểm hội tụ 3+ nhánh ở tầng trũng thấp nhất không có đường thoát -> Hồ chứa PondBasin
            if (solution.connectionCount >= 3 && outflowDir < 0 && isLowTierTerminal)
            {
                solution.pieceType = RiverPieceType.PondBasin;
                int primaryInlet = (upstreamDir >= 0) ? upstreamDir : connectedDirections[0];
                solution.rotationY = GetDirectionAngle(primaryInlet);
                solution.primaryPortA = primaryInlet;
                return solution;
            }

            int pA = -1;
            int pB = -1;

            if (solution.connectionCount == 2)
            {
                pA = connectedDirections[0];
                pB = connectedDirections[1];
            }
            else
            {
                // Chọn 2 cổng tạo nên DÒNG CHÍNH (Main Trunk):
                // Ưu tiên 1: Cặp upstreamDir và outflowDir nếu cả 2 đều thuộc connectedDirections
                if (upstreamDir >= 0 && outflowDir >= 0 &&
                    connectedDirections.Contains(upstreamDir) && connectedDirections.Contains(outflowDir))
                {
                    pA = upstreamDir;
                    pB = outflowDir;
                }
                // Ưu tiên 2: Có upstreamDir, chọn pB trong các cổng còn lại tạo góc duỗi thẳng nhất
                else if (upstreamDir >= 0 && connectedDirections.Contains(upstreamDir))
                {
                    pA = upstreamDir;
                    pB = FindBestOppositePort(pA, connectedDirections);
                }
                // Ưu tiên 3: Có outflowDir, chọn pA trong các cổng còn lại tạo góc duỗi thẳng nhất
                else if (outflowDir >= 0 && connectedDirections.Contains(outflowDir))
                {
                    pB = outflowDir;
                    pA = FindBestOppositePort(pB, connectedDirections);
                }
                // Ưu tiên 4: Tìm cặp 2 cổng trong danh sách tạo thành đường thẳng nhất (180° > 120° > 60°)
                else
                {
                    FindBestTrunkPair(connectedDirections, out pA, out pB);
                }
            }

            solution.primaryPortA = pA;
            solution.primaryPortB = pB;

            int diff = ((pB - pA) % 6 + 6) % 6;
            int d0 = pA;
            int d1 = pB;
            if (diff > 3)
            {
                d0 = pB;
                d1 = pA;
                diff = 6 - diff;
            }

            if (diff == 3)
            {
                // 180 độ: Suối thẳng xuyên tâm (Straight)
                solution.pieceType = RiverPieceType.Straight;
                int downstreamDir = (upstreamDir == d0) ? d1 : (upstreamDir == d1 ? d0 : d0);
                solution.rotationY = GetDirectionAngle(downstreamDir);
                return solution;
            }
            else
            {
                // Rẽ góc (120° mềm Bézier hoặc 60° cua)
                solution.pieceType = (diff == 2) ? RiverPieceType.Curve120 : RiverPieceType.Curve60;
                float a0 = GetDirectionAngle(d0);
                float a1 = GetDirectionAngle(d1);
                solution.rotationY = Mathf.LerpAngle(a0, a1, 0.5f);
                return solution;
            }
        }

        return solution;
    }

    /// <summary>
    /// Tìm cổng kết nối khác trong danh sách tạo góc duỗi thẳng nhất với referencePort (ưu tiên 180° diff=3, sau đó 120° diff=2)
    /// </summary>
    private static int FindBestOppositePort(int referencePort, List<int> candidates)
    {
        int bestPort = -1;
        int maxDiff = -1;
        foreach (int c in candidates)
        {
            if (c == referencePort) continue;
            int d = ((c - referencePort) % 6 + 6) % 6;
            if (d > 3) d = 6 - d;
            if (d > maxDiff)
            {
                maxDiff = d;
                bestPort = c;
            }
        }
        return (bestPort >= 0) ? bestPort : (candidates.Count > 0 ? candidates[0] : 0);
    }

    /// <summary>
    /// Tìm cặp 2 cổng trong danh sách tạo thành đường thẳng nhất (ưu tiên góc duỗi 180° > 120° > 60°)
    /// </summary>
    private static void FindBestTrunkPair(List<int> candidates, out int portA, out int portB)
    {
        portA = candidates[0];
        portB = candidates[1];
        int maxDiff = -1;

        for (int i = 0; i < candidates.Count; i++)
        {
            for (int j = i + 1; j < candidates.Count; j++)
            {
                int p0 = candidates[i];
                int p1 = candidates[j];
                int d = ((p1 - p0) % 6 + 6) % 6;
                if (d > 3) d = 6 - d;
                if (d > maxDiff)
                {
                    maxDiff = d;
                    portA = p0;
                    portB = p1;
                }
            }
        }
    }
}
