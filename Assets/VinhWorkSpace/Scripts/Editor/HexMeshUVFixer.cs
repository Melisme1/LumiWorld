#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tiện ích Editor một chạm: Chiếu lại tọa độ UV (Planar & Triplanar UV Mapping) cho khối Hexagon.
/// Khắc phục triệt để lỗi mesh dùng Color Palette làm mất vân ảnh texture (Base Map, Normal Map).
/// </summary>
public static class HexMeshUVFixer
{
    private const float UV_SCALE = 1.0f; // 1 mét thế giới = 1 chu kỳ texture (khớp với Cube)

    [MenuItem("LumiWorld/Fix Hex UVs (Chiếu UV cho khối đang chọn)", priority = 200)]
    public static void FixSelectedHexUVs()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("Lỗi", "Vui lòng bấm chọn một hoặc nhiều khối Hexagon trong Hierarchy hoặc Scene trước!", "OK");
            return;
        }

        string saveDir = "Assets/VinhWorkSpace/Meshes";
        if (!AssetDatabase.IsValidFolder("Assets/VinhWorkSpace"))
        {
            AssetDatabase.CreateFolder("Assets", "VinhWorkSpace");
        }
        if (!AssetDatabase.IsValidFolder(saveDir))
        {
            AssetDatabase.CreateFolder("Assets/VinhWorkSpace", "Meshes");
        }

        int fixedCount = 0;

        foreach (GameObject go in selectedObjects)
        {
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                // Thử tìm trong các con của GameObject
                MeshFilter[] childMfs = go.GetComponentsInChildren<MeshFilter>();
                foreach (var childMf in childMfs)
                {
                    if (childMf.sharedMesh != null)
                    {
                        FixMeshFilterUV(childMf, saveDir);
                        fixedCount++;
                    }
                }
                continue;
            }

            FixMeshFilterUV(mf, saveDir);
            fixedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (fixedCount > 0)
        {
            EditorUtility.DisplayDialog("Thành công", $"Đã chiếu lại UV thành công cho {fixedCount} Mesh! Vân texture cỏ/đất giờ đây đã hiển thị sắc nét.", "Tuyệt vời!");
        }
        else
        {
            EditorUtility.DisplayDialog("Thông báo", "Không tìm thấy MeshFilter nào trên các đối tượng được chọn.", "OK");
        }
    }

    private static void FixMeshFilterUV(MeshFilter mf, string saveDir)
    {
        Mesh original = mf.sharedMesh;
        Undo.RecordObject(mf, "Fix Hex UVs");

        // Tạo bản sao mesh để không làm hỏng file gốc
        Mesh newMesh = Object.Instantiate(original);
        newMesh.name = original.name + "_UVFixed";

        Vector3[] vertices = newMesh.vertices;
        Vector3[] normals = newMesh.normals;
        if (normals == null || normals.Length != vertices.Length)
        {
            newMesh.RecalculateNormals();
            normals = newMesh.normals;
        }

        Vector2[] uvs = new Vector2[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            Vector3 n = normals[i];

            // 1. Mặt trên đỉnh và gờ vát dốc xuống (hướng trục Y lên trên):
            // Chiếu phẳng từ trên xuống (Top-down Planar Projection X-Z)
            if (n.y > 0.25f)
            {
                uvs[i] = new Vector2(v.x * UV_SCALE, v.z * UV_SCALE);
            }
            // 2. Vách thẳng đứng bên hông (Vách đất / Vách đá):
            // Chiếu theo trục chiếu đứng tương ứng để tránh bị kéo dãn sọc dọc
            else if (Mathf.Abs(n.x) > Mathf.Abs(n.z))
            {
                uvs[i] = new Vector2(v.z * UV_SCALE, v.y * UV_SCALE);
            }
            else
            {
                uvs[i] = new Vector2(v.x * UV_SCALE, v.y * UV_SCALE);
            }
        }

        newMesh.uv = uvs;
        newMesh.RecalculateBounds();

        // Lưu mesh mới vĩnh viễn vào Assets/VinhWorkSpace/Meshes
        string assetPath = $"{saveDir}/{newMesh.name}.asset";
        // Nếu đã có file cũ thì overwrite
        AssetDatabase.CreateAsset(newMesh, assetPath);

        mf.sharedMesh = newMesh;
        EditorUtility.SetDirty(mf.gameObject);

        Debug.Log($"<color=#38BDF8>[HexMeshUVFixer]</color> Đã chiếu lại UV cho <b>{mf.gameObject.name}</b> ➔ Đã lưu mesh tại: <color=#4ADE80>{assetPath}</color>");
    }
}
#endif
