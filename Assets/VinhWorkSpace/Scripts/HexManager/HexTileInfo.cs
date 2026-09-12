using UnityEngine;

/// <summary>
/// Lưu trữ thông tin định danh của khối lục giác trên bản đồ để hỗ trợ kiểm tra quy tắc đặt bài chính xác 100%
/// </summary>
public class HexTileInfo : MonoBehaviour
{
    [Tooltip("Prefab gốc được dùng để sinh ra ô này")]
    public GameObject sourcePrefab;

    [Tooltip("Chỉ số tầng địa hình trong HexWorldGenerator.TerrainPrefabs")]
    public int terrainTypeIndex;

    [Tooltip("Tọa độ của ô trên bản đồ")]
    public HexCoordinates coordinates;
}
