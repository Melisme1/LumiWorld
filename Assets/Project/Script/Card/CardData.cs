using System.Collections.Generic;
using UnityEngine;

public enum CardType
{
    Creature,
    Terrain,
    Building,
    Special
}

[CreateAssetMenu(
    fileName = "NewCard",
    menuName = "Card/Card Data"
)]
public class CardData : ScriptableObject
{
    [Header("Basic Information")]
    public string cardID;
    public string cardName;
    public Sprite cardImage;

    [Header("Type")]
    public CardType cardType;

    [Header("Placement")]
    [Tooltip("Prefab to instantiate on the hex tile when played")]
    public GameObject prefabToPlace;

    [Tooltip("Local offset relative to tile surface")]
    public Vector3 placementOffset = Vector3.zero;

    [Tooltip("If true, replaces the tile rather than placing on top")]
    public bool replacesTile = false;

    [Header("Habitat Procedural Props (Preserve Style)")]
    [Tooltip("Tỉ lệ giới hạn vùng sinh bên trong ô lục giác (0.7 - 0.85 giúp prop không bị lòi ra ngoài mép ô)")]
    [Range(0.5f, 0.92f)]
    public float habitatSpawnMargin = 0.8f;

    [Tooltip("Danh sách các nhóm prop sẽ rải ngẫu nhiên khi đặt bài. Nếu danh sách này có dữ liệu, game sẽ tự động rải props thay vì chỉ đặt 1 prefabToPlace duy nhất.")]
    public List<HabitatPropRule> habitatProps = new List<HabitatPropRule>();

    /// <summary>
    /// Kiểm tra xem lá bài này có cấu hình rải props hệ sinh thái hay không
    /// </summary>
    public bool HasHabitatProps()
    {
        if (habitatProps == null || habitatProps.Count == 0) return false;
        foreach (var rule in habitatProps)
        {
            if (rule != null && rule.prefabs != null && rule.prefabs.Count > 0 && rule.countRange.y > 0)
                return true;
        }
        return false;
    }

[System.Serializable]
public class HabitatPropRule
{
    public string groupName = "Trees";

    [Tooltip("Danh sách các model prefab biến thể để bốc ngẫu nhiên (Ví dụ: Tree_1, Tree_2, Tree_3...)")]
    public List<GameObject> prefabs = new List<GameObject>();

    [Tooltip("Số lượng prop loại này sẽ xuất hiện ngẫu nhiên (Min - Max)")]
    public Vector2Int countRange = new Vector2Int(2, 4);

    [Tooltip("Khoảng cách tối thiểu giữa các prop để chống đè lấn / dính chùm vào nhau")]
    public float minDistance = 0.35f;

    [Tooltip("Độ to nhỏ ngẫu nhiên (Min - Max scale)")]
    public Vector2 scaleRange = new Vector2(0.85f, 1.2f);

    [Tooltip("Góc nghiêng ngẫu nhiên nhẹ (độ) để cây cối trông tự nhiên")]
    public float maxTiltAngle = 2.5f;

    [Tooltip("Nếu tích chọn, prop sẽ luôn được đặt ở tâm ô (0,0) kèm xoay ngẫu nhiên. Thích hợp cho các cụm cây lớn dựng sẵn.")]
    public bool placeAtCenter = false;
}

    [Header("Placement Rules")]
    [Tooltip("Danh sách các Prefab khối lục giác hợp lệ mà lá bài này có thể đặt lên. Để trống = Cho phép đặt lên mọi ô.")]
    public List<GameObject> allowedTilePrefabs = new List<GameObject>();

    /// <summary>
    /// Kiểm tra xem khối lục giác này có hợp lệ với quy tắc đặt của lá bài không
    /// </summary>
    public bool IsTileAllowed(GameObject tileObj, HexWorldGenerator worldGen)
    {
        // 1. Nếu danh sách trống -> Được phép đặt lên bất kỳ ô nào
        if (allowedTilePrefabs == null || allowedTilePrefabs.Count == 0)
        {
            return true;
        }

        if (tileObj == null) return false;

        HexTileInfo tileInfo = tileObj.GetComponent<HexTileInfo>();

        foreach (var allowed in allowedTilePrefabs)
        {
            if (allowed == null) continue;

            string cleanAllowedName = allowed.name.Replace("(Clone)", "").Trim();

            // 2. So khớp chính xác qua HexTileInfo (Prefab gốc hoặc Tên chính xác)
            if (tileInfo != null)
            {
                if (tileInfo.sourcePrefab == allowed)
                {
                    return true;
                }

                if (tileInfo.sourcePrefab != null)
                {
                    string cleanSourcePrefabName = tileInfo.sourcePrefab.name.Replace("(Clone)", "").Trim();
                    if (string.Equals(cleanSourcePrefabName, cleanAllowedName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // 3. So khớp chính xác tên được đóng ngoặc vuông [prefabName] trong tên tileInstance (ví dụ: Hex_0_0_[hex_grass]_Type1)
            // Dùng dấu ngoặc [] để tránh nhầm lẫn "hex_grass" nằm bên trong "hex_grass_bottom"
            if (tileObj.name.IndexOf($"[{cleanAllowedName}]", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            // 4. So khớp theo TerrainPrefabs index từ HexWorldGenerator
            if (worldGen != null && worldGen.TerrainPrefabs != null)
            {
                for (int i = 0; i < worldGen.TerrainPrefabs.Length; i++)
                {
                    if (worldGen.TerrainPrefabs[i] == allowed)
                    {
                        if (tileInfo != null && tileInfo.terrainTypeIndex == i)
                        {
                            return true;
                        }

                        if (tileObj.name.Contains($"_Type{i}"))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
}