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

    [Header("Score")]
    public int baseScore = 1;

    [Header("Placement")]
    [Tooltip("Prefab to instantiate on the hex tile when played")]
    public GameObject prefabToPlace;

    [Tooltip("Local offset relative to tile surface")]
    public Vector3 placementOffset = Vector3.zero;

    [Tooltip("If true, replaces the tile rather than placing on top")]
    public bool replacesTile = false;

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