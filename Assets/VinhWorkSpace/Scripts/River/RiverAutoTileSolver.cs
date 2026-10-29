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
            connectionCount = connectedDirections != null ? connectedDirections.Count : 0
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
            }
            else
            {
                // Khi có 2+ nhánh hạ lưu kề bên:
                // Nếu có hướng đổ dốc xuống (outflowDir), ưu tiên quay theo hướng dốc/hạ lưu đó
                if (outflowDir >= 0 && connectedDirections.Contains(outflowDir))
                {
                    solution.rotationY = GetDirectionAngle(outflowDir);
                }
                else
                {
                    float a0 = GetDirectionAngle(connectedDirections[0]);
                    float a1 = GetDirectionAngle(connectedDirections[1]);
                    solution.rotationY = Mathf.LerpAngle(a0, a1, 0.5f);
                }
            }
            return solution;
        }

        // 2. Trường hợp 0 kết nối (ô suối đơn lẻ đứng biệt lập không phải origin):
        // BẤT ĐẮC DĨ KHÔNG CÓ ĐƯỜNG NỐI -> Sinh Hang Nguồn (SpringHead) quay miệng theo hướng dốc
        if (solution.connectionCount == 0)
        {
            solution.pieceType = RiverPieceType.SpringHead;
            solution.rotationY = GetDirectionAngle(downhillDir);
            return solution;
        }

        // 3. Trường hợp 1 kết nối:
        if (solution.connectionCount == 1)
        {
            int dir = connectedDirections[0];
            // Là điểm cuối của dòng suối: Đọng thành HỒ (PondBasin)
            solution.pieceType = RiverPieceType.PondBasin;
            solution.rotationY = GetDirectionAngle(dir);
            return solution;
        }

        // 4. Trường hợp 2 kết nối: ƯU TIÊN NỐI DÒNG CHẢY (Straight hoặc Curve)
        if (solution.connectionCount == 2)
        {
            int d0 = connectedDirections[0];
            int d1 = connectedDirections[1];

            int diff = ((d1 - d0) % 6 + 6) % 6;
            if (diff > 3)
            {
                int temp = d0;
                d0 = d1;
                d1 = temp;
                diff = 6 - diff;
            }

            if (diff == 3)
            {
                // 180 độ: Suối thẳng xuyên tâm (nối 2 hướng đối diện)
                solution.pieceType = RiverPieceType.Straight;
                int downstreamDir = (upstreamDir == d0) ? d1 : (upstreamDir == d1 ? d0 : d0);
                solution.rotationY = GetDirectionAngle(downstreamDir);
                return solution;
            }
            else
            {
                // Rẽ góc sang ô khác (120° mềm Bézier hoặc 60° cua gắt)
                solution.pieceType = (diff == 2) ? RiverPieceType.Curve120 : RiverPieceType.Curve60;
                float a0 = GetDirectionAngle(d0);
                float a1 = GetDirectionAngle(d1);
                solution.rotationY = Mathf.LerpAngle(a0, a1, 0.5f);
                return solution;
            }
        }

        // 5. ĐIỂM HỘI TỤ 3+ NHÁNH KHÔNG CÓ ĐƯỜNG RA:
        // CHỈ KHI ở tầng trũng thấp nhất (isLowTierTerminal) VÀ không có nhánh thoát (outflowDir < 0) mới là HỒ HỘI TỤ
        if (outflowDir < 0 && isLowTierTerminal)
        {
            solution.pieceType = RiverPieceType.PondBasin;
            int primaryInlet = (upstreamDir >= 0) ? upstreamDir : connectedDirections[0];
            solution.rotationY = GetDirectionAngle(primaryInlet);
            return solution;
        }

        // 6. GIAO LỘ HỢP LƯU (3 kết nối):
        if (solution.connectionCount == 3)
        {
            List<int> sorted = new List<int>(connectedDirections);
            sorted.Sort();

            int step0 = sorted[1] - sorted[0];
            int step1 = sorted[2] - sorted[1];
            int step2 = 6 - (sorted[2] - sorted[0]);

            int[] steps = new int[] { step0, step1, step2 };
            Array.Sort(steps);

            int targetDir = (outflowDir >= 0) ? outflowDir : connectedDirections[0];

            if (steps[0] == 2 && steps[1] == 2 && steps[2] == 2)
            {
                // Dạng đối xứng đều 120° - 120° - 120° (HexFork3WayCrossroad)
                solution.pieceType = RiverPieceType.Fork3Way;
                solution.rotationY = GetDirectionAngle(targetDir);
                return solution;
            }
            else
            {
                // Mọi dạng 3 nhánh bất đối xứng: Ưu tiên Fork3waysame (Fork3WayAsymmetric)
                solution.pieceType = RiverPieceType.Fork3WayAsymmetric;
                solution.rotationY = GetDirectionAngle(targetDir);
                return solution;
            }
        }

        // 6. Giao lộ 4+ kết nối: Dùng cơ chế Hub-and-Spoke với Fork3Way, rải mương nối tới mọi cổng kết nối
        solution.pieceType = RiverPieceType.Fork3Way;
        int targetForkDir = (outflowDir >= 0) ? outflowDir : connectedDirections[0];
        solution.rotationY = GetDirectionAngle(targetForkDir);
        return solution;
    }
}
