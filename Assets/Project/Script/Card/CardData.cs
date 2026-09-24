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

    [Tooltip("Nếu tích chọn: lá bài này LUÔN chỉ cộng baseScore, không bao giờ được nhân hệ số nhóm (dùng cho card Special như Rain với baseScore = 1).")]
    public bool alwaysBaseScore = false;

    [Header("Placement")]
    [Tooltip("Prefab to instantiate on the hex tile when played")]
    public GameObject prefabToPlace;

    [Tooltip("Local offset relative to tile surface")]
    public Vector3 placementOffset = Vector3.zero;

    [Tooltip("Nếu tích chọn: khi đặt bài, khối lục giác tại ô đó được BIẾN ĐỔI thành Prefab To Place (thay vì đặt lên trên). Dùng cho card Special như Rain.")]
    public bool replacesTile = false;

    [Tooltip("Nếu tích chọn, các prop/vật phẩm đã đặt trên khối gốc sẽ bị xóa khi biến đổi (thường đúng cho Rain). Nếu bỏ trống, prop được giữ lại và chuyển sang khối mới.")]
    public bool clearPropsOnTransform = true;

    [Tooltip("Nếu tích chọn (mặc định), đặt lá bài này sẽ CHIẾM ô: các card khác không đặt lên được nữa. Bỏ tích với card biến đổi địa hình như Rain để khối sau biến đổi vẫn trống, cho phép đặt card khác (ví dụ Forest) lên.")]
    public bool occupiesTile = true;

    [Header("Tile Transformation Mapping (For Special Cards / Rain)")]
    [Tooltip("Danh sách các cặp biến đổi từ đất khô (Arid) sang đất tươi tốt (Lush) khi lá bài này được sử dụng")]
    public List<TileTransformPair> transformRules = new List<TileTransformPair>();

    [Header("Habitat Procedural Props (Preserve Style)")]
    [Tooltip("Tỉ lệ giới hạn vùng sinh bên trong ô lục giác: 0.85-0.88 cho rừng cây; 0.94-0.95 cho hoa cỏ phủ kín mặt phẳng mà không bị tràn vát mép; 1.0 là tràn ra tận mép ngoài cùng")]
    [Range(0.5f, 1.0f)]
    public float habitatSpawnMargin = 0.85f;

    [Tooltip("Danh sách các nhóm prop sẽ rải ngẫu nhiên khi đặt bài. Nếu danh sách này có dữ liệu, game sẽ tự động rải props thay vì chỉ đặt 1 prefabToPlace duy nhất.")]
    public List<HabitatPropRule> habitatProps = new List<HabitatPropRule>();

    /// <summary>
    /// Kiểm tra lá bài này có phải dạng biến đổi khối lục giác hay không (card Special như Rain).
    /// Cần bật replacesTile VÀ (có transformRules HOẶC có prefabToPlace làm khối đích).
    /// </summary>
    public bool IsTileTransformCard()
    {
        return replacesTile && ((transformRules != null && transformRules.Count > 0) || prefabToPlace != null);
    }

    /// <summary>
    /// Lấy tile tươi tốt (Lush) tương ứng với tile khô (Arid) hiện tại
    /// </summary>
    public GameObject GetTransformedPrefab(GameObject currentTileObj)
    {
        if (currentTileObj == null) return prefabToPlace;

        HexTileInfo tileInfo = currentTileObj.GetComponent<HexTileInfo>();
        GameObject sourcePrefab = tileInfo != null ? tileInfo.sourcePrefab : null;
        string cleanTileName = currentTileObj.name.Replace("(Clone)", "").Trim();

        if (transformRules != null && transformRules.Count > 0)
        {
            foreach (var rule in transformRules)
            {
                if (rule == null || rule.aridTilePrefab == null || rule.lushTilePrefab == null) continue;

                // 1. So khớp trực tiếp sourcePrefab
                if (sourcePrefab != null && sourcePrefab == rule.aridTilePrefab)
                {
                    return rule.lushTilePrefab;
                }

                // 2. So khớp theo tên sourcePrefab
                string cleanAridName = rule.aridTilePrefab.name.Replace("(Clone)", "").Trim();
                if (sourcePrefab != null)
                {
                    string cleanSourcePrefabName = sourcePrefab.name.Replace("(Clone)", "").Trim();
                    if (string.Equals(cleanSourcePrefabName, cleanAridName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return rule.lushTilePrefab;
                    }
                }

                // 3. So khớp theo tên GameObject tile trong map (ví dụ: Hex_0_0_[Tile_Bloomfield_Arid]_Type0)
                if (cleanTileName.IndexOf($"[{cleanAridName}]", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    cleanTileName.IndexOf(cleanAridName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return rule.lushTilePrefab;
                }
            }
        }

        return prefabToPlace;
    }

    /// <summary>
    /// Kiểm tra xem một tile có phải là tile khô nguồn hợp lệ trong transformRules không
    /// </summary>
    public bool IsTransformSourceTile(GameObject tileObj)
    {
        if (tileObj == null || transformRules == null || transformRules.Count == 0) return false;

        HexTileInfo tileInfo = tileObj.GetComponent<HexTileInfo>();
        GameObject sourcePrefab = tileInfo != null ? tileInfo.sourcePrefab : null;
        string cleanTileName = tileObj.name.Replace("(Clone)", "").Trim();

        foreach (var rule in transformRules)
        {
            if (rule == null || rule.aridTilePrefab == null) continue;

            if (sourcePrefab != null && sourcePrefab == rule.aridTilePrefab) return true;

            string cleanAridName = rule.aridTilePrefab.name.Replace("(Clone)", "").Trim();
            if (sourcePrefab != null)
            {
                string cleanSourcePrefabName = sourcePrefab.name.Replace("(Clone)", "").Trim();
                if (string.Equals(cleanSourcePrefabName, cleanAridName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (cleanTileName.IndexOf($"[{cleanAridName}]", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                cleanTileName.IndexOf(cleanAridName, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }


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
public class TileTransformPair
{
    [Tooltip("Khối lục giác khô trước khi tưới (Arid tile)")]
    public GameObject aridTilePrefab;

    [Tooltip("Khối lục giác tươi tốt sau khi tưới (Lush tile)")]
    public GameObject lushTilePrefab;
}

[System.Serializable]
public class HabitatPropRule
{
    public string groupName = "Trees";

    [Tooltip("Danh sách các model prefab biến thể để bốc ngẫu nhiên (Ví dụ: Tree_1, Tree_2, Tree_3...)")]
    public List<GameObject> prefabs = new List<GameObject>();

    [Tooltip("TÍCH CHỌN: Sinh thành CỤM/BẦY (countRange = Số Cụm trên ô hex).\nBỎ CHỌN: Rải ĐƠN LẺ cách đều (countRange = Số Cây đơn lẻ).")]
    public bool clusterAsPatch = false;

    [Tooltip("Số lượng xuất hiện trên ô hex (Min - Max):\n• Nếu KHÔNG tích cụm: Số lượng cây đơn lẻ.\n• Nếu CÓ tích cụm: Số lượng CỤM/BẦY trên ô hex.")]
    public Vector2Int countRange = new Vector2Int(1, 2);

    [Tooltip("Số lượng cây/bụi tụ lại bên trong MỖI CỤM (Min - Max). Chỉ có tác dụng khi tích chọn clusterAsPatch.")]
    public Vector2Int propsPerClusterRange = new Vector2Int(2, 3);

    [Tooltip("Bán kính cụm (mét): Bán kính quây tụ của các cây trong cụm. Càng nhỏ thì các cây trong cụm càng ôm sát nhau.")]
    [Range(0.12f, 0.45f)]
    public float clusterRadius = 0.25f;

    [Tooltip("Khoảng cách tối thiểu giữa các prop (hoặc giữa các cụm) để chống đè lấn / dính chùm")]
    public float minDistance = 0.35f;

    [Tooltip("Độ to nhỏ ngẫu nhiên (Min - Max scale)")]
    public Vector2 scaleRange = new Vector2(0.85f, 1.2f);

    [Tooltip("Góc nghiêng ngẫu nhiên nhẹ (độ) để cây cối trông tự nhiên")]
    public float maxTiltAngle = 2.5f;

    [Tooltip("Độ cắm sâu thêm xuống đất (mét). Mặc định = 0 (Hệ thống tự động: hoa dạng bó cắm sâu 0.30m để giấu chân cành và hạ độ cao ngang thảm cỏ).")]
    public float customGroundEmbed = 0f;

    [Tooltip("Nếu tích chọn, prop sẽ luôn được đặt ở tâm ô (0,0) kèm xoay ngẫu nhiên.")]
    public bool placeAtCenter = false;
}

    [Header("Placement Rules")]
    [Tooltip("Danh sách các Prefab khối lục giác hợp lệ mà lá bài này có thể đặt lên. Để trống = Cho phép đặt lên mọi ô.")]
    public List<GameObject> allowedTilePrefabs = new List<GameObject>();

    /// <summary>
    /// Kiểm tra xem khối lục giác này có hợp lệ với quy tắc đặt của lá bài không.
    /// CHỈ cho phép đặt lên đúng những hex prefab mà bạn kéo vào trong Inspector (allowedTilePrefabs / transformRules).
    /// </summary>
    public bool IsTileAllowed(GameObject tileObj, HexWorldGenerator worldGen = null)
    {
        if (tileObj == null) return false;

        // TRƯỜNG HỢP 1: THẺ BIẾN ĐỔI ĐỊA HÌNH (như Rain)
        // Bắt buộc ô này phải là ô khô nguồn hợp lệ (Arid) mà bạn đã kéo vào transformRules hoặc allowedTilePrefabs
        if (IsTileTransformCard())
        {
            bool hasTransformRules = transformRules != null && transformRules.Count > 0;
            bool hasAllowedPrefabs = allowedTilePrefabs != null && allowedTilePrefabs.Count > 0;

            if (hasTransformRules && !IsTransformSourceTile(tileObj))
            {
                return false;
            }

            if (hasAllowedPrefabs && !MatchesAnyAllowedPrefab(tileObj))
            {
                return false;
            }

            return hasTransformRules || hasAllowedPrefabs;
        }

        // TRƯỜNG HỢP 2: THẺ THƯỜNG (Forest, Mountain, Bloomfield, Props...)
        // Nếu không kéo prefab nào vào allowedTilePrefabs -> Cho phép đặt lên mọi ô
        if (allowedTilePrefabs == null || allowedTilePrefabs.Count == 0)
        {
            return true;
        }

        // Nếu có kéo prefab vào -> CHỈ cho phép đặt lên đúng các hex khớp với prefab đã kéo vào
        return MatchesAnyAllowedPrefab(tileObj);
    }

    /// <summary>
    /// Kiểm tra xem GameObject ô lục giác có khớp với bất kỳ prefab nào trong allowedTilePrefabs không
    /// (So khớp trực tiếp Prefab gốc hoặc tên Prefab, không phụ thuộc vào thứ tự tầng)
    /// </summary>
    public bool MatchesAnyAllowedPrefab(GameObject tileObj)
    {
        if (tileObj == null || allowedTilePrefabs == null || allowedTilePrefabs.Count == 0)
            return false;

        HexTileInfo tileInfo = tileObj.GetComponent<HexTileInfo>();
        GameObject sourcePrefab = tileInfo != null ? tileInfo.sourcePrefab : null;
        string cleanTileName = tileObj.name.Replace("(Clone)", "").Trim();

        foreach (var allowed in allowedTilePrefabs)
        {
            if (allowed == null) continue;

            // 1. So khớp trực tiếp Prefab gốc tham chiếu
            if (sourcePrefab != null && sourcePrefab == allowed)
            {
                return true;
            }

            string cleanAllowedName = allowed.name.Replace("(Clone)", "").Trim();

            // 2. So khớp theo tên sourcePrefab
            if (sourcePrefab != null)
            {
                string cleanSourcePrefabName = sourcePrefab.name.Replace("(Clone)", "").Trim();
                if (string.Equals(cleanSourcePrefabName, cleanAllowedName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // 3. So khớp theo tên GameObject trong scene (ví dụ: Hex_0_0_[Tile_Bloomfield_Arid]_Type0)
            if (cleanTileName.IndexOf($"[{cleanAllowedName}]", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                string.Equals(cleanTileName, cleanAllowedName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}