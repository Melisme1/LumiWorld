using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chuyên trách tính toán hình học và sinh Mesh thủ tục (Procedural Mesh Generation)
/// cho hệ thống kết nối cụm Biome lục giác:
/// 1. Cầu nối rãnh mép vát (Seam Bridge): Mặt phẳng đỉnh, sườn vát mép ngoài, thành đứng bịt nứt.
/// 2. Nắp hàn ngã ba (Corner Filler): Nắp đa giác lấp phẳng điểm giao thoa 3 ô lục giác.
/// </summary>
public static class HexClusterMeshBuilder
{
    /// <summary>
    /// Xây dựng Mesh hình học chuẩn xác của Seam Bridge trong Local Space của midPoint:
    /// - Đoạn phẳng trên đỉnh kết thúc tại: 0.93 * halfEdge = 0.5369f
    /// - Sườn vát ngoài nghiêng từ 0.5369f ra 0.5774f, độ cao dốc từ localTopY xuống localRimY
    /// - Hỗ trợ cả Color Palette UV lẫn Planar UV Mapping (chiếu phẳng vân texture cỏ hoa liền mạch từ 2 ô).
    /// </summary>
    public static Mesh BuildSeamBridgeMeshLocal(
        Vector3 u, Vector3 v, float halfEdge,
        float localTopY, float localRimY, float localBottomY,
        bool v1IsOuter, bool v2IsOuter,
        Vector2 topGrassUV, Vector2 bevelUV, Vector2 wallUV,
        Vector3 midPoint = default, Vector3 posA = default, Vector3 posB = default, bool usePlanarUV = false)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();

        Vector3 V1_loc = -v * halfEdge;
        Vector3 V2_loc = v * halfEdge;

        float flatHalfEdge = halfEdge * 0.93f; // 0.5369f
        float wTop = 0.10f;

        Vector3 startFlatV = -v * flatHalfEdge;
        Vector3 endFlatV = v * flatHalfEdge;

        // -------------------------------------------------------------
        // PHẦN 1: MẶT ĐỈNH PHẲNG (TOP FLAT CAP)
        // -------------------------------------------------------------
        int topStartIdx = vertices.Count;

        Vector3 t0 = startFlatV - u * wTop; t0.y = localTopY; // Phía ô A
        Vector3 t1 = endFlatV - u * wTop;   t1.y = localTopY; // Phía ô A
        Vector3 t2 = endFlatV + u * wTop;   t2.y = localTopY; // Phía ô B
        Vector3 t3 = startFlatV + u * wTop; t3.y = localTopY; // Phía ô B

        Vector2 uv0 = topGrassUV;
        Vector2 uv1 = topGrassUV;
        Vector2 uv2 = topGrassUV;
        Vector2 uv3 = topGrassUV;

        if (usePlanarUV)
        {
            Vector3 p0 = midPoint + t0 - posA; uv0 = new Vector2(p0.x, p0.z);
            Vector3 p1 = midPoint + t1 - posA; uv1 = new Vector2(p1.x, p1.z);
            Vector3 p2 = midPoint + t2 - posB; uv2 = new Vector2(p2.x, p2.z);
            Vector3 p3 = midPoint + t3 - posB; uv3 = new Vector2(p3.x, p3.z);
        }

        vertices.Add(t0); normals.Add(Vector3.up); uvs.Add(uv0);
        vertices.Add(t1); normals.Add(Vector3.up); uvs.Add(uv1);
        vertices.Add(t2); normals.Add(Vector3.up); uvs.Add(uv2);
        vertices.Add(t3); normals.Add(Vector3.up); uvs.Add(uv3);

        triangles.Add(topStartIdx + 0); triangles.Add(topStartIdx + 1); triangles.Add(topStartIdx + 2);
        triangles.Add(topStartIdx + 0); triangles.Add(topStartIdx + 2); triangles.Add(topStartIdx + 3);

        // -------------------------------------------------------------
        // PHẦN 2: SƯỜN VÁT MÉP NGOÀI TẠI V1 (OUTER BEVEL SKIRT V1)
        // Tam giác vát dốc nối trực tiếp từ cạnh (t0, t3) xuống đỉnh ngoài cùng V1_loc (tại localRimY)
        // -------------------------------------------------------------
        if (v1IsOuter)
        {
            Vector3 v1Rim = V1_loc; v1Rim.y = localRimY;
            Vector3 normBevel1 = (-v * 0.707f + Vector3.up * 0.707f).normalized;

            Vector2 uvBev0 = bevelUV;
            Vector2 uvBev3 = bevelUV;
            Vector2 uvBevRim = bevelUV;
            if (usePlanarUV)
            {
                Vector3 pb0 = midPoint + t0 - posA;       uvBev0 = new Vector2(pb0.x, pb0.z);
                Vector3 pb3 = midPoint + t3 - posB;       uvBev3 = new Vector2(pb3.x, pb3.z);
                Vector3 pbRim = midPoint + v1Rim - posA;  uvBevRim = new Vector2(pbRim.x, pbRim.z);
            }

            int bev1Idx = vertices.Count;
            vertices.Add(t0);       normals.Add(normBevel1); uvs.Add(uvBev0); // 0
            vertices.Add(t3);       normals.Add(normBevel1); uvs.Add(uvBev3); // 1
            vertices.Add(v1Rim);    normals.Add(normBevel1); uvs.Add(uvBevRim); // 2

            // Tam giác dốc nghiêng từ t0, t3 xuống v1Rim (thuận kim đồng hồ khi nhìn từ ngoài)
            triangles.Add(bev1Idx + 0); triangles.Add(bev1Idx + 1); triangles.Add(bev1Idx + 2);

            // Bịt kín khe nứt thành bên từ v1Rim xuống localBottomY
            int wall1Idx = vertices.Count;
            Vector3 w0 = t0; w0.y = localRimY;
            Vector3 w1 = t3; w1.y = localRimY;
            Vector3 wBot0 = t0; wBot0.y = localBottomY;
            Vector3 wBot1 = t3; wBot1.y = localBottomY;

            Vector2 uvW0 = wallUV;
            Vector2 uvW1 = wallUV;
            Vector2 uvWBot1 = wallUV;
            Vector2 uvWBot0 = wallUV;
            if (usePlanarUV)
            {
                uvW0 = new Vector2((midPoint + w0 - posA).x, w0.y);
                uvW1 = new Vector2((midPoint + w1 - posB).x, w1.y);
                uvWBot1 = new Vector2((midPoint + wBot1 - posB).x, wBot1.y);
                uvWBot0 = new Vector2((midPoint + wBot0 - posA).x, wBot0.y);
            }

            vertices.Add(w0);    normals.Add(-v); uvs.Add(uvW0); // 0
            vertices.Add(w1);    normals.Add(-v); uvs.Add(uvW1); // 1
            vertices.Add(wBot1); normals.Add(-v); uvs.Add(uvWBot1); // 2
            vertices.Add(wBot0); normals.Add(-v); uvs.Add(uvWBot0); // 3

            triangles.Add(wall1Idx + 0); triangles.Add(wall1Idx + 1); triangles.Add(wall1Idx + 2);
            triangles.Add(wall1Idx + 0); triangles.Add(wall1Idx + 2); triangles.Add(wall1Idx + 3);
        }

        // -------------------------------------------------------------
        // PHẦN 3: SƯỜN VÁT MÉP NGOÀI TẠI V2 (OUTER BEVEL SKIRT V2)
        // -------------------------------------------------------------
        if (v2IsOuter)
        {
            Vector3 v2Rim = V2_loc; v2Rim.y = localRimY;
            Vector3 normBevel2 = (v * 0.707f + Vector3.up * 0.707f).normalized;

            Vector2 uvBev2 = bevelUV;
            Vector2 uvBev1 = bevelUV;
            Vector2 uvBevRim = bevelUV;
            if (usePlanarUV)
            {
                Vector3 pb2 = midPoint + t2 - posB;       uvBev2 = new Vector2(pb2.x, pb2.z);
                Vector3 pb1 = midPoint + t1 - posA;       uvBev1 = new Vector2(pb1.x, pb1.z);
                Vector3 pbRim2 = midPoint + v2Rim - posA; uvBevRim = new Vector2(pbRim2.x, pbRim2.z);
            }

            int bev2Idx = vertices.Count;
            vertices.Add(t2);       normals.Add(normBevel2); uvs.Add(uvBev2); // 0
            vertices.Add(t1);       normals.Add(normBevel2); uvs.Add(uvBev1); // 1
            vertices.Add(v2Rim);    normals.Add(normBevel2); uvs.Add(uvBevRim); // 2

            triangles.Add(bev2Idx + 0); triangles.Add(bev2Idx + 1); triangles.Add(bev2Idx + 2);

            // Bịt kín khe nứt thành bên từ v2Rim xuống localBottomY
            int wall2Idx = vertices.Count;
            Vector3 w2_0 = t2; w2_0.y = localRimY;
            Vector3 w2_1 = t1; w2_1.y = localRimY;
            Vector3 w2Bot0 = t2; w2Bot0.y = localBottomY;
            Vector3 w2Bot1 = t1; w2Bot1.y = localBottomY;

            Vector2 uvW2_0 = wallUV;
            Vector2 uvW2_1 = wallUV;
            Vector2 uvW2Bot1 = wallUV;
            Vector2 uvW2Bot0 = wallUV;
            if (usePlanarUV)
            {
                uvW2_0 = new Vector2((midPoint + w2_0 - posB).x, w2_0.y);
                uvW2_1 = new Vector2((midPoint + w2_1 - posA).x, w2_1.y);
                uvW2Bot1 = new Vector2((midPoint + w2Bot1 - posA).x, w2Bot1.y);
                uvW2Bot0 = new Vector2((midPoint + w2Bot0 - posB).x, w2Bot0.y);
            }

            vertices.Add(w2_0);    normals.Add(v); uvs.Add(uvW2_0); // 0
            vertices.Add(w2_1);    normals.Add(v); uvs.Add(uvW2_1); // 1
            vertices.Add(w2Bot1);  normals.Add(v); uvs.Add(uvW2Bot1); // 2
            vertices.Add(w2Bot0);  normals.Add(v); uvs.Add(uvW2Bot0); // 3

            triangles.Add(wall2Idx + 0); triangles.Add(wall2Idx + 1); triangles.Add(wall2Idx + 2);
            triangles.Add(wall2Idx + 0); triangles.Add(wall2Idx + 2); triangles.Add(wall2Idx + 3);
        }

        Mesh m = new Mesh();
        m.name = "Proc_SeamBridge";
        m.vertices = vertices.ToArray();
        m.triangles = triangles.ToArray();
        m.normals = normals.ToArray();
        m.uv = uvs.ToArray();
        m.RecalculateBounds();

        return m;
    }

    /// <summary>
    /// Xây dựng Mesh đa giác nắp lấp phẳng ngã ba 3 ô lục giác
    /// </summary>
    public static Mesh BuildCornerFillerMeshLocal(
        float radius, Vector2 topGrassUV, string cornerKey, int segments = 6,
        Vector3 center = default, Vector3 posA = default, Vector3 posB = default, Vector3 posC = default, bool usePlanarUV = false)
    {
        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];
        Vector3[] normals = new Vector3[segments + 1];
        Vector2[] uv = new Vector2[segments + 1];

        vertices[0] = Vector3.zero;
        normals[0] = Vector3.up;
        uv[0] = usePlanarUV ? new Vector2((center - posA).x, (center - posA).z) : topGrassUV;

        float angleStep = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float rad = (i * angleStep) * Mathf.Deg2Rad;
            Vector3 vPos = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius;
            vertices[i + 1] = vPos;
            normals[i + 1] = Vector3.up;

            if (usePlanarUV)
            {
                Vector3 worldV = center + vPos;
                float dA = (worldV - posA).sqrMagnitude;
                float dB = (worldV - posB).sqrMagnitude;
                float dC = (worldV - posC).sqrMagnitude;
                Vector3 closest = (dA <= dB && dA <= dC) ? posA : (dB <= dC ? posB : posC);
                uv[i + 1] = new Vector2((worldV - closest).x, (worldV - closest).z);
            }
            else
            {
                uv[i + 1] = topGrassUV;
            }

            int next = (i + 1) % segments + 1;
            triangles[i * 3 + 0] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = next;
        }

        Mesh cornerMesh = new Mesh();
        cornerMesh.name = $"Mesh_{cornerKey}";
        cornerMesh.vertices = vertices;
        cornerMesh.triangles = triangles;
        cornerMesh.normals = normals;
        cornerMesh.uv = uv;
        cornerMesh.RecalculateBounds();

        return cornerMesh;
    }
}

