using System.Collections.Generic;
using UnityEngine;

public class HexPreviewGhost : MonoBehaviour
{
    private List<MeshRenderer> renderers = new List<MeshRenderer>();
    private MaterialPropertyBlock propBlock;
    private static readonly int ColorProperty = Shader.PropertyToID("_BaseColor");

    [Header("Màu sắc báo hiệu")]
    [SerializeField] private Color validColor = new Color(0.2f, 0.9f, 0.4f, 0.6f);   // Xanh lá (Hợp lệ)
    [SerializeField] private Color invalidColor = new Color(0.95f, 0.2f, 0.2f, 0.6f); // Đỏ (Không hợp lệ)

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
    }

    /// <summary>
    /// Tạo các phần tử Mesh mô phỏng cho cụm dựa theo góc xoay hiện tại
    /// </summary>
    public void Initialize(GameObject[] prefabs, HexClusterData clusterData, int rotationStep, Material ghostMaterial, float stepHeight)
    {
        // Xóa các mesh cũ
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        renderers.Clear();

        if (clusterData == null || prefabs == null || prefabs.Length == 0) return;

        foreach (var tile in clusterData.tiles)
        {
            // Tọa độ đã qua phép xoay 60 độ
            HexCoordinates rotatedCoord = tile.relativeCoord.Rotate60Clockwise(rotationStep);

            int prefabIdx = Mathf.Clamp(tile.prefabIndex, 0, prefabs.Length - 1);
            GameObject prefab = prefabs[prefabIdx];

            // Căn chỉnh bù trừ Pivot mặt phẳng trên cùng
            float topSurfaceOffset = 0f;
            MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                topSurfaceOffset = mf.sharedMesh.bounds.center.y + (mf.sharedMesh.bounds.size.y / 2f);
            }

            float targetTopY = tile.heightLevel * stepHeight;
            float localY = targetTopY - topSurfaceOffset;
            Vector3 localPos = HexMetrics.HexToWorldPosition(rotatedCoord, localY);

            // Xoay hướng mô hình 60 độ quanh trục thẳng đứng Y
            Quaternion localRot = Quaternion.Euler(0f, rotationStep * 60f, 0f);

            GameObject ghostPart = Instantiate(prefab, transform);
            ghostPart.transform.localPosition = localPos;
            ghostPart.transform.localRotation = localRot;

            // Tắt Colliders của Ghost để không cản trở Raycast chuột
            Collider[] colliders = ghostPart.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false;
            }

            // Gán Material mô phỏng
            MeshRenderer[] mrList = ghostPart.GetComponentsInChildren<MeshRenderer>();
            foreach (var mr in mrList)
            {
                if (ghostMaterial != null)
                {
                    mr.material = ghostMaterial;
                }
                renderers.Add(mr);
            }
        }
    }

    /// <summary>
    /// Thay đổi màu sắc thông qua MaterialPropertyBlock (Zero memory allocation)
    /// </summary>
    public void SetPlacementValidity(bool isValid)
    {
        Color targetColor = isValid ? validColor : invalidColor;

        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].GetPropertyBlock(propBlock);
                propBlock.SetColor(ColorProperty, targetColor);
                renderers[i].SetPropertyBlock(propBlock);
            }
        }
    }
}
