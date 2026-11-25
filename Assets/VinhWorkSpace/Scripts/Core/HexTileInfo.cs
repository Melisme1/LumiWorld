using UnityEngine;

/// <summary>
/// Lưu trữ thông tin định danh của khối lục giác trên bản đồ để hỗ trợ kiểm tra quy tắc đặt bài chính xác 100%
/// </summary>
public class HexTileInfo : MonoBehaviour
{
    [System.NonSerialized] public LumiWorld.Acs.BiomeSurface biomeSurface;
    // Independent from terrainTypeIndex, which Rain sets to -1.
    public int elevationLevel = -1;
    public int ElevationLevel => elevationLevel >= 0 ? elevationLevel : terrainTypeIndex;
    [Tooltip("Prefab gốc được dùng để sinh ra ô này")]
    public GameObject sourcePrefab;

    [Tooltip("Chỉ số tầng địa hình trong HexWorldGenerator.TerrainPrefabs")]
    public int terrainTypeIndex;

    [Tooltip("Ô có môi trường Lush cho phép cháy rừng. Dữ liệu gameplay độc lập với model hiển thị.")]
    public bool hasLushEnvironment;

    [Tooltip("Tọa độ của ô trên bản đồ")]
    public HexCoordinates coordinates;

    [Tooltip("Trạng thái ô đã được đặt card / prop lên chưa")]
    public bool isOccupied;
}
