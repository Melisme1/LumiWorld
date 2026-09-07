using System.Collections.Generic;
using UnityEngine;

public class HexPreviewGhost : MonoBehaviour
{
    private struct GhostTileInfo
    {
        public MeshRenderer renderer;
        public int prefabIndex;
        public int heightLevel;
    }

    private List<GhostTileInfo> ghostTiles = new List<GhostTileInfo>();
    private MaterialPropertyBlock propBlock;
    private static readonly int ColorProperty = Shader.PropertyToID("_BaseColor");

    [Header("Màu Hologram Hợp Lệ theo Từng Loại Địa Hình")]
    [Tooltip("Tầng 0 / Nước: Xanh lam pha lê / ngọc bích")]
    [SerializeField] private Color waterValidColor = new Color(0.18f, 0.72f, 1.0f, 0.75f);

    [Tooltip("Tầng 1 / Cỏ: Xanh lá lục bảo tươi")]
    [SerializeField] private Color grassValidColor = new Color(0.28f, 0.95f, 0.40f, 0.75f);

    [Tooltip("Tầng 2 / Núi & Đá: Vàng cam hổ phách rực rỡ")]
    [SerializeField] private Color mountainValidColor = new Color(1.0f, 0.68f, 0.18f, 0.82f);

    [Tooltip("Màu mặc định nếu có thêm tầng khác")]
    [SerializeField] private Color defaultValidColor = new Color(0.85f, 0.40f, 1.0f, 0.75f);

    [Header("Màu Hologram Không Hợp Lệ (Bị Trùng Đè)")]
    [SerializeField] private Color invalidColor = new Color(0.95f, 0.22f, 0.22f, 0.75f); // Đỏ cảnh báo

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
        ghostTiles.Clear();

        if (clusterData == null || prefabs == null || prefabs.Length == 0) return;

        foreach (var tile in clusterData.tiles)
        {
            // Tọa độ đã qua phép xoay 60 độ
            HexCoordinates rotatedCoord = tile.relativeCoord.Rotate60Clockwise(rotationStep);

            int prefabIdx = Mathf.Clamp(tile.prefabIndex, 0, prefabs.Length - 1);
            GameObject prefab = prefabs[prefabIdx];

            Vector3 localPos = HexMetrics.HexToWorldPosition(rotatedCoord, 0f);

            // Xoay hướng mô hình 60 độ quanh trục thẳng đứng Y
            Quaternion localRot = Quaternion.Euler(0f, rotationStep * 60f, 0f);

            GameObject ghostPart = Instantiate(prefab, transform);
            ghostPart.name = $"Ghost_{rotatedCoord.Q}_{rotatedCoord.R}_Type{prefabIdx}";
            ghostPart.transform.localPosition = localPos;
            ghostPart.transform.localRotation = localRot;

            // Tắt Colliders của Ghost để không cản trở Raycast chuột
            Collider[] colliders = ghostPart.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false;
            }

            // Gán Material mô phỏng và lưu thông tin mảnh
            MeshRenderer[] mrList = ghostPart.GetComponentsInChildren<MeshRenderer>();
            foreach (var mr in mrList)
            {
                if (ghostMaterial != null)
                {
                    mr.material = ghostMaterial;
                }
                ghostTiles.Add(new GhostTileInfo
                {
                    renderer = mr,
                    prefabIndex = prefabIdx,
                    heightLevel = tile.heightLevel
                });
            }
        }
    }

    /// <summary>
    /// Thay đổi màu sắc thông qua MaterialPropertyBlock (Zero memory allocation)
    /// </summary>
    public void SetPlacementValidity(bool isValid)
    {
        for (int i = 0; i < ghostTiles.Count; i++)
        {
            var tileInfo = ghostTiles[i];
            if (tileInfo.renderer == null) continue;

            Color targetColor;
            if (isValid)
            {
                targetColor = GetBiomeValidColor(tileInfo.prefabIndex);
            }
            else
            {
                // Khi không hợp lệ: màu đỏ cảnh báo với sắc thái nhận diện từng tầng
                float intensity = 0.75f + (tileInfo.prefabIndex * 0.15f);
                targetColor = new Color(
                    invalidColor.r * intensity,
                    invalidColor.g * (0.5f + tileInfo.prefabIndex * 0.2f),
                    invalidColor.b * (0.5f + tileInfo.prefabIndex * 0.2f),
                    invalidColor.a
                );
            }

            tileInfo.renderer.GetPropertyBlock(propBlock);
            propBlock.SetColor(ColorProperty, targetColor);
            tileInfo.renderer.SetPropertyBlock(propBlock);
        }
    }

    private Color GetBiomeValidColor(int prefabIndex)
    {
        switch (prefabIndex)
        {
            case 0: return waterValidColor;      // Nước: Xanh lam pha lê
            case 1: return grassValidColor;      // Cỏ: Xanh lá tươi
            case 2: return mountainValidColor;   // Núi/Đá: Vàng hổ phách
            default: return defaultValidColor;   // Tầng khác
        }
    }
}
