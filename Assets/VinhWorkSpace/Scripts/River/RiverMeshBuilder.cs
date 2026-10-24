using System;
using UnityEngine;

/// <summary>
/// Trình tạo Mesh hình học phụ trợ cho suối (Procedural River Helper).
/// Sau khi chuyển đổi 100% dòng nước sang Unity Splines & SplineExtrude,
/// lớp này chỉ còn giữ lại chức năng tạo mặt ao tròn phẳng lặng (Pond Surface Mesh) cho hồ cuối nguồn.
/// Toàn bộ các hàm sinh mesh nước/lòng suối thủ công từng ô cũ đã được loại bỏ hoàn toàn.
/// </summary>
public static class RiverMeshBuilder
{
    /// <summary>
    /// Xây dựng Mesh Mặt ao / Mặt hồ đa giác hình elip hoặc hình tròn (Pond Surface Mesh)
    /// Dùng cho các tầng hồ giật cấp hoặc lòng chảo ao nước đọng phẳng lặng (PondBasin)
    /// </summary>
    public static Mesh BuildPondMesh(Vector3 center, float radiusX, float radiusZ, int segments = 24, float alpha = 1.0f, Vector2 uvOffset = default)
    {
        if (segments < 3) segments = 16;
        Mesh mesh = new Mesh { name = "Procedural_Pond" };

        int vertCount = segments + 1;
        Vector3[] vertices = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        Color[] colors = new Color[vertCount];
        int[] triangles = new int[segments * 3];

        vertices[0] = center;
        normals[0] = Vector3.up;
        uvs[0] = new Vector2(0.5f, 0.5f) + uvOffset;
        colors[0] = new Color(1f, 1f, 1f, alpha);

        float angleStep = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float rad = Mathf.Deg2Rad * (i * angleStep);
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            vertices[i + 1] = center + new Vector3(cos * radiusX, 0f, sin * radiusZ);
            normals[i + 1] = Vector3.up;
            uvs[i + 1] = new Vector2((cos + 1f) * 0.5f, (sin + 1f) * 0.5f) + uvOffset;
            colors[i + 1] = new Color(1f, 1f, 1f, alpha);

            int triIdx = i * 3;
            triangles[triIdx + 0] = 0;
            triangles[triIdx + 1] = i + 1;
            triangles[triIdx + 2] = (i + 1) % segments + 1;
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }
}
