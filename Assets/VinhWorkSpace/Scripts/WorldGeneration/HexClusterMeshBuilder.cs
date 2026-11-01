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
    /// </summary>
    public static Mesh BuildSeamBridgeMeshLocal(
        Vector3 u, Vector3 v, float halfEdge,
        float localTopY, float localRimY, float localBottomY,
        bool v1IsOuter, bool v2IsOuter,
        Vector2 topGrassUV, Vector2 bevelUV, Vector2 wallUV)
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

        Vector3 t0 = startFlatV - u * wTop; t0.y = localTopY;
        Vector3 t1 = endFlatV - u * wTop;   t1.y = localTopY;
        Vector3 t2 = endFlatV + u * wTop;   t2.y = localTopY;
        Vector3 t3 = startFlatV + u * wTop; t3.y = localTopY;

        vertices.Add(t0); normals.Add(Vector3.up); uvs.Add(topGrassUV);
        vertices.Add(t1); normals.Add(Vector3.up); uvs.Add(topGrassUV);
        vertices.Add(t2); normals.Add(Vector3.up); uvs.Add(topGrassUV);
        vertices.Add(t3); normals.Add(Vector3.up); uvs.Add(topGrassUV);

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

            int bev1Idx = vertices.Count;
            vertices.Add(t0);       normals.Add(normBevel1); uvs.Add(bevelUV); // 0
            vertices.Add(t3);       normals.Add(normBevel1); uvs.Add(bevelUV); // 1
            vertices.Add(v1Rim);    normals.Add(normBevel1); uvs.Add(bevelUV); // 2

            // Tam giác dốc nghiêng từ t0, t3 xuống v1Rim (thuận kim đồng hồ khi nhìn từ ngoài)
            triangles.Add(bev1Idx + 0); triangles.Add(bev1Idx + 1); triangles.Add(bev1Idx + 2);

            // Bịt kín khe nứt thành bên từ v1Rim xuống localBottomY
            int wall1Idx = vertices.Count;
            Vector3 w0 = t0; w0.y = localRimY;
            Vector3 w1 = t3; w1.y = localRimY;
            Vector3 wBot0 = t0; wBot0.y = localBottomY;
            Vector3 wBot1 = t3; wBot1.y = localBottomY;

            vertices.Add(w0);    normals.Add(-v); uvs.Add(wallUV); // 0
            vertices.Add(w1);    normals.Add(-v); uvs.Add(wallUV); // 1
            vertices.Add(wBot1); normals.Add(-v); uvs.Add(wallUV); // 2
            vertices.Add(wBot0); normals.Add(-v); uvs.Add(wallUV); // 3

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

            int bev2Idx = vertices.Count;
            vertices.Add(t2);       normals.Add(normBevel2); uvs.Add(bevelUV); // 0
            vertices.Add(t1);       normals.Add(normBevel2); uvs.Add(bevelUV); // 1
            vertices.Add(v2Rim);    normals.Add(normBevel2); uvs.Add(bevelUV); // 2

            triangles.Add(bev2Idx + 0); triangles.Add(bev2Idx + 1); triangles.Add(bev2Idx + 2);

            // Bịt kín khe nứt thành bên từ v2Rim xuống localBottomY
            int wall2Idx = vertices.Count;
            Vector3 w2_0 = t2; w2_0.y = localRimY;
            Vector3 w2_1 = t1; w2_1.y = localRimY;
            Vector3 w2Bot0 = t2; w2Bot0.y = localBottomY;
            Vector3 w2Bot1 = t1; w2Bot1.y = localBottomY;

            vertices.Add(w2_0);    normals.Add(v); uvs.Add(wallUV); // 0
            vertices.Add(w2_1);    normals.Add(v); uvs.Add(wallUV); // 1
            vertices.Add(w2Bot1);  normals.Add(v); uvs.Add(wallUV); // 2
            vertices.Add(w2Bot0);  normals.Add(v); uvs.Add(wallUV); // 3

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
    public static Mesh BuildCornerFillerMeshLocal(float radius, Vector2 topGrassUV, string cornerKey, int segments = 6)
    {
        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];
        Vector3[] normals = new Vector3[segments + 1];
        Vector2[] uv = new Vector2[segments + 1];

        vertices[0] = Vector3.zero;
        normals[0] = Vector3.up;
        uv[0] = topGrassUV;

        float angleStep = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float rad = (i * angleStep) * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius;
            normals[i + 1] = Vector3.up;
            uv[i + 1] = topGrassUV;

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

