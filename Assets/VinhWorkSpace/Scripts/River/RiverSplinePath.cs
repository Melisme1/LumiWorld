using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Điểm nút trên dải suối uốn lượn (Spline Waypoint)
/// Chứa vị trí thế giới/cục bộ, hướng dòng chảy (tangent), pháp tuyến ngang (normal) và cự ly tích lũy (distance).
/// </summary>
[System.Serializable]
public struct RiverWaypoint
{
    public Vector3 position;
    public Vector3 tangent; // Hướng dòng chảy (Forward flow)
    public Vector3 normal;  // Pháp tuyến ngang vuông góc trong mặt phẳng XZ (Right normal)
    public float distance;  // Cự ly tích lũy dọc theo dòng suối (dùng trải UV cuộn sóng)
    public float width;     // Bề rộng tùy chỉnh tại điểm này (0 = dùng width mặc định)
    public float alpha;     // Độ mờ tùy chỉnh tại điểm này (-1 = tự động mờ dần ở mép cuối)

    public RiverWaypoint(Vector3 pos, Vector3 tan, Vector3 norm, float dist, float w = 0f, float a = -1f)
    {
        position = pos;
        tangent = tan.sqrMagnitude > 0.0001f ? tan.normalized : Vector3.forward;
        normal = norm.sqrMagnitude > 0.0001f ? norm.normalized : new Vector3(-tangent.z, 0f, tangent.x);
        distance = dist;
        width = w;
        alpha = a;
    }
}

/// <summary>
/// Bộ tạo đường cong dòng chảy sông suối trên lưới lục giác Pointed-Top.
/// Đảm bảo tất cả các cổng kết nối tại trung điểm mép ô hex khớp 100% về vị trí, pháp tuyến và độ rộng.
/// </summary>
public static class RiverSplinePath
{
    /// <summary>
    /// Lấy tọa độ trung điểm của cạnh lục giác theo hướng dir (0..5) trong hệ tọa độ cục bộ của ô
    /// Khoảng cách từ tâm ô tới trung điểm mép luôn bằng đúng HexMetrics.InnerRadius (1.0m)
    /// </summary>
    public static Vector3 GetEdgeMidpointLocal(int dir)
    {
        int index = ((dir % 6) + 6) % 6;
        HexCoordinates neighbor = HexCoordinates.Directions[index];
        Vector3 neighborWorldPos = HexMetrics.HexToWorldPosition(neighbor, 0f) - HexMetrics.HexToWorldPosition(new HexCoordinates(0, 0), 0f);
        return neighborWorldPos * 0.5f;
    }

    /// <summary>
    /// Lấy vector đơn vị chỉ hướng từ tâm ô ra trung điểm cạnh dir
    /// </summary>
    public static Vector3 GetEdgeNormalLocal(int dir)
    {
        return GetEdgeMidpointLocal(dir).normalized;
    }

    /// <summary>
    /// Tạo đường suối thượng nguồn: Nối từ miệng thoát nước của Non Bộ Grotto ra trung điểm mép hạ lưu
    /// </summary>
    /// <param name="outDir">Hướng ô hạ lưu (0..5)</param>
    /// <param name="mouthDistance">Cự ly từ tâm ô tới miệng tràn grotto (mặc định ~0.35m)</param>
    /// <param name="steps">Số bước chia mẫu để tạo đường cong mượt</param>
    public static List<RiverWaypoint> BuildSpringHeadPath(int outDir, float mouthDistance = 0.35f, int steps = 10)
    {
        Vector3 edgeMid = GetEdgeMidpointLocal(outDir);
        Vector3 edgeNorm = edgeMid.normalized;
        Vector3 mouthPos = edgeNorm * mouthDistance;

        List<Vector3> rawPoints = new List<Vector3>(steps);
        for (int i = 0; i < steps; i++)
        {
            float t = (float)i / (steps - 1);
            // Nội suy tuyến tính hoặc uốn lượn nhẹ từ miệng grotto ra mép
            Vector3 pos = Vector3.Lerp(mouthPos, edgeMid, t);
            rawPoints.Add(pos);
        }

        return GenerateWaypointsFromPoints(rawPoints);
    }

    /// <summary>
    /// Tạo đường suối xuyên ô: Nối từ cổng vào inDir sang cổng ra outDir với độ cong uốn lượn tự nhiên (Bézier Curve)
    /// </summary>
    public static List<RiverWaypoint> BuildThroughPath(int inDir, int outDir, float meanderOffset = 0.06f, int steps = 16)
    {
        Vector3 pIn = GetEdgeMidpointLocal(inDir);
        Vector3 pOut = GetEdgeMidpointLocal(outDir);

        Vector3 tanIn = -GetEdgeNormalLocal(inDir); // Hướng dòng chảy đi VÀO ô
        Vector3 tanOut = GetEdgeNormalLocal(outDir); // Hướng dòng chảy đi RA khỏi ô

        // Điểm điều khiển trung tâm:
        int diff = ((outDir - inDir) % 6 + 6) % 6;
        if (diff > 3) diff = 6 - diff;

        // Cua gắt 60° (diff == 1): Hai cổng kề nhau (ví dụ hướng 3 sang hướng 2)
        // Nối trực tiếp từ cổng vào sang cổng ra bằng một đường cong Bézier bậc 3 (Cubic Bézier)
        // Ôm quanh góc chung giữa 2 cạnh, KHÔNG đi qua tâm ô (0,0,0) vì sẽ tạo góc nhọn đâm vào giữa ô rồi giật ngược ra
        if (diff == 1)
        {
            float handleLength = 0.38f;
            Vector3 cp1 = pIn + tanIn * handleLength;
            Vector3 cp2 = pOut - tanOut * handleLength;

            List<Vector3> arcPoints = new List<Vector3>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 pos = EvaluateCubicBezier(pIn, cp1, cp2, pOut, t);
                arcPoints.Add(pos);
            }
            return GenerateWaypointsFromPoints(arcPoints);
        }

        Vector3 centerPos = Vector3.zero;

        if (diff == 3)
        {
            // Suối thẳng 180 độ: Thêm độ võng uốn lượn tự nhiên hình chữ S nhỏ (Meandering S-curve)
            // Vector ngang vuông góc với trục dòng chảy
            Vector3 streamAxis = (pOut - pIn).normalized;
            Vector3 lateralDir = new Vector3(-streamAxis.z, 0f, streamAxis.x);
            centerPos += lateralDir * meanderOffset;
        }

        // Tạo 4 điểm điều khiển cho Cubic Bézier:
        float handleLen = 0.38f;
        Vector3 cpA = pIn + tanIn * handleLen;
        Vector3 cpB = pOut - tanOut * handleLen;

        // Nếu có độ võng trung tâm, ta kết hợp 2 đoạn Bézier (In -> Center -> Out) để uốn lượn mềm như dải lụa
        List<Vector3> rawPoints = new List<Vector3>(steps);
        int halfSteps = steps / 2;

        // Đoạn 1: từ pIn tới centerPos
        Vector3 tanCenter = (pOut - pIn).normalized;
        for (int i = 0; i < halfSteps; i++)
        {
            float t = (float)i / halfSteps;
            Vector3 pos = EvaluateCubicBezier(pIn, pIn + tanIn * 0.25f, centerPos - tanCenter * 0.20f, centerPos, t);
            rawPoints.Add(pos);
        }

        // Đoạn 2: từ centerPos tới pOut
        for (int i = 0; i <= halfSteps; i++)
        {
            float t = (float)i / halfSteps;
            Vector3 pos = EvaluateCubicBezier(centerPos, centerPos + tanCenter * 0.20f, pOut - tanOut * 0.25f, pOut, t);
            rawPoints.Add(pos);
        }

        return GenerateWaypointsFromPoints(rawPoints);
    }

    /// <summary>
    /// Tạo đường suối dẫn vào hồ cuối nguồn: Nối từ cổng inDir vào mép hồ đọng
    /// </summary>
    public static List<RiverWaypoint> BuildPondInletPath(int inDir, float pondInletRadius = 0.32f, int steps = 10)
    {
        Vector3 pIn = GetEdgeMidpointLocal(inDir);
        Vector3 edgeNorm = GetEdgeNormalLocal(inDir);
        Vector3 pondInletPos = edgeNorm * pondInletRadius;

        List<Vector3> rawPoints = new List<Vector3>(steps);
        for (int i = 0; i < steps; i++)
        {
            float t = (float)i / (steps - 1);
            Vector3 pos = Vector3.Lerp(pIn, pondInletPos, t);
            rawPoints.Add(pos);
        }

        return GenerateWaypointsFromPoints(rawPoints);
    }

    /// <summary>
    /// Tạo nhánh suối cho Ngã ba (Fork 3-way): từ cổng inDir chảy vào tâm hồ ngã ba
    /// </summary>
    public static List<RiverWaypoint> BuildForkBranchPath(int inDir, float junctionRadius = 0.20f, int steps = 8)
    {
        Vector3 pIn = GetEdgeMidpointLocal(inDir);
        Vector3 edgeNorm = GetEdgeNormalLocal(inDir);
        Vector3 centerJunction = edgeNorm * junctionRadius;

        List<Vector3> rawPoints = new List<Vector3>(steps);
        for (int i = 0; i < steps; i++)
        {
            float t = (float)i / (steps - 1);
            Vector3 pos = Vector3.Lerp(pIn, centerJunction, t);
            rawPoints.Add(pos);
        }

        return GenerateWaypointsFromPoints(rawPoints);
    }

    /// <summary>
    /// Tính toán đầy đủ Tangent, Normal và Distance tích lũy từ danh sách tọa độ
    /// </summary>
    public static List<RiverWaypoint> GenerateWaypointsFromPoints(List<Vector3> points)
    {
        List<RiverWaypoint> waypoints = new List<RiverWaypoint>(points.Count);
        if (points == null || points.Count < 2) return waypoints;

        float accumDist = 0f;

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 cur = points[i];
            Vector3 forward;

            if (i == 0)
            {
                forward = (points[1] - cur).normalized;
            }
            else if (i == points.Count - 1)
            {
                forward = (cur - points[i - 1]).normalized;
                accumDist += Vector3.Distance(points[i - 1], cur);
            }
            else
            {
                forward = ((points[i + 1] - cur).normalized + (cur - points[i - 1]).normalized) * 0.5f;
                accumDist += Vector3.Distance(points[i - 1], cur);
            }

            Vector3 normal = new Vector3(-forward.z, 0f, forward.x).normalized;
            waypoints.Add(new RiverWaypoint(cur, forward, normal, accumDist));
        }

        return waypoints;
    }

    /// <summary>
    /// Tính điểm trên đường cong Cubic Bézier
    /// </summary>
    public static Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        t = Mathf.Clamp01(t);
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector3 p = uuu * p0;
        p += 3f * uu * t * p1;
        p += 3f * u * tt * p2;
        p += ttt * p3;
        return p;
    }
}
