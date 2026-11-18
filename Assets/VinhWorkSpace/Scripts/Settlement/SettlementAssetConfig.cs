using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cấu hình danh mục tài nguyên 3D (Model FBX hoặc Prefab) cho Hệ thống Làng mạc & Đô thị.
/// Tách rời 100% cấu hình khỏi logic code:
/// - Giai đoạn đầu: Kéo thả trực tiếp các file model FBX từ gói KayKit Medieval.
/// - Giai đoạn sau: Kéo đè các Prefab hoàn chỉnh (đã gắn VFX, ánh sáng, âm thanh) vào thay thế mà không cần sửa 1 dòng code.
/// </summary>
[CreateAssetMenu(fileName = "SettlementAssetConfig", menuName = "LumiWorld/Settlement/Asset Config")]
public class SettlementAssetConfig : ScriptableObject
{
    [Header("1. Nhà ở dân sự nhỏ (Small Cottages / Homes)")]
    [Tooltip("Danh sách các căn nhà nhỏ cho Làng (ví dụ: building_home_A, home_B các màu)")]
    public List<GameObject> smallHouses = new List<GameObject>();

    [Header("2. Nhà ở dân sự lớn / Nhà lầu (Medium / Urban Houses)")]
    [Tooltip("Danh sách các căn nhà quy mô lớn hơn khi lên cấp Đô thị")]
    public List<GameObject> mediumHouses = new List<GameObject>();

    [Header("3. Điểm nhấn trung tâm Làng (Village Centerpieces)")]
    [Tooltip("Vật thể tâm làng: Giếng nước (well), đụn rơm (grain), lều (tent), cột cờ (flag), sân khấu (stage), giàn giáo, tàn tích")]
    public List<GameObject> villageCenterpieces = new List<GameObject>();

    [Header("4. Công trình thương mại & Dịch vụ (Commercial: Market, Tavern, Stage)")]
    public List<GameObject> commercialBuildings = new List<GameObject>();

    [Header("5. Công trình sản xuất & Nghề thủ công (Production: Windmill, Watermill, Blacksmith, Lumbermill, Mine)")]
    public List<GameObject> productionBuildings = new List<GameObject>();

    [Header("6. Công trình quân sự & Quốc phòng (Military: Barracks, Archery Range, Catapult)")]
    public List<GameObject> militaryBuildings = new List<GameObject>();

    [Header("7. Trung tâm Đại Đô Thị (Grand City Civic Core: Castle, Church)")]
    public List<GameObject> civicBuildings = new List<GameObject>();

    [Header("Công trình đơn lẻ (Legacy References)")]
    [Tooltip("Khu chợ thương mại (building_market)")]
    public GameObject marketBuilding;

    [Tooltip("Cối xay gió (building_windmill)")]
    public GameObject windmillBuilding;

    [Tooltip("Lò rèn vũ khí / nông cụ (building_blacksmith)")]
    public GameObject blacksmithBuilding;

    [Tooltip("Quán trọ / Quán rượu (building_tavern)")]
    public GameObject tavernBuilding;

    [Tooltip("Lâu đài nguy nga cho ô lõi trung tâm thành phố (building_castle)")]
    public GameObject castleBuilding;

    [Tooltip("Nhà thờ lớn biểu tượng (building_church)")]
    public GameObject churchBuilding;

    [Header("8. Ranh giới Hàng rào Gỗ (Village Fence - Luôn khép kín)")]
    [Tooltip("Đoạn rào gỗ thẳng chạy dọc theo cạnh hex (fence_wood_straight)")]
    public GameObject fenceStraight;

    [Tooltip("Cổng rào gỗ mở ra ngoài (fence_wood_straight_gate)")]
    public GameObject fenceGate;

    [Header("9. Ranh giới Tường thành Đá (City Wall - Luôn khép kín)")]
    [Tooltip("Đoạn tường thành đá thẳng chạy dọc theo cạnh hex (wall_straight)")]
    public GameObject wallStraight;

    [Tooltip("Cổng thành đá đồ sộ (wall_straight_gate)")]
    public GameObject wallGate;

    [Tooltip("Đoạn tường bo góc ngoài (wall_corner_A_outside)")]
    public GameObject wallCorner;

    [Tooltip("Tháp canh phòng thủ cắm ở góc tường thành (tower_A hoặc tower_base)")]
    public GameObject wallTower;

    [Header("8. Phụ kiện đường phố & Trang trí (Street Props)")]
    [Tooltip("Thùng gỗ, bao tải, xe rùa, thùng rượu rải rác")]
    public List<GameObject> streetProps = new List<GameObject>();

    [Header("9. Thảm thực vật nhỏ (Village Micro Flora: Grass, Flowers, Clovers)")]
    [Tooltip("Danh sách các bụi cỏ nhỏ, hoa dại, đám cỏ ba lá từ LumiWorld")]
    public List<GameObject> villageFlora = new List<GameObject>();

    [Header("10. Đá lát đường & Lối đi (Stepping Stones & Cobblestone Path)")]
    [Tooltip("Danh sách các phiến đá nhỏ để lát đường từ cổng vào làng và giữa các công trình")]
    public List<GameObject> pathSteppingStones = new List<GameObject>();

    [Header("11. Cây bóng mát Làng quê (Village Shade Trees)")]
    [Tooltip("Cây bóng mát, cây liễu, thông làng quê, cây phong phủ bóng mát che lấp khoảng trống")]
    public List<GameObject> villageTrees = new List<GameObject>();

    [Header("12. Bụi cây xanh & Dương xỉ (Village Bushes & Shrubs)")]
    [Tooltip("Bụi rậm, dương xỉ xanh, bụi cây cảnh ốp chân tường nhà và hàng rào")]
    public List<GameObject> villageBushes = new List<GameObject>();

    [Header("13. Cầu thang & Kết nối Đa tầng (Terrace Stairs & Connectors)")]
    [Tooltip("Bật/Tắt dựng thang gỗ nối dọc vách đá giữa các tầng trong cụm đô thị (mặc định tắt để giữ vách đá gọn sạch)")]
    public bool spawnTerraceLadders = false;

    [Tooltip("Cầu thang đá kết nối giữa các tầng vách đá cho Đại Đô Thị (Meshy Stone Stairway hoặc bậc đá)")]
    public GameObject stoneStairsPrefab;

    [Tooltip("Cầu thang gỗ / thang dây kết nối giữa các tầng cho Làng quê & Thị trấn")]
    public GameObject woodStairsPrefab;

    [Tooltip("Lan can đá thấp trên ban công / vọng cảnh tầng trên (fence_stone_straight)")]
    public GameObject terraceBalustrade;

    [Header("★ Tùy biến Mở Rộng của Bạn (Custom User Assets)")]
    [Tooltip("Danh sách công trình kiến trúc / nhà ở bạn tự tạo hoặc muốn đưa thêm vào làng")]
    public List<GameObject> customBuildings = new List<GameObject>();

    [Tooltip("Danh sách hoa, cỏ, cây xanh đặc biệt bạn muốn tự kéo thả thêm")]
    public List<GameObject> customFlora = new List<GameObject>();

    [Tooltip("Danh sách phụ kiện, tượng, xe rùa, đồ trang trí bạn muốn tự kéo thả thêm")]
    public List<GameObject> customProps = new List<GameObject>();

    [Tooltip("Danh sách phiến đá, sỏi hoặc gạch lát đường bạn muốn tự kéo thả thêm")]
    public List<GameObject> customSteppingStones = new List<GameObject>();

    [Header("11. Tinh chỉnh Tọa độ & Tỉ lệ (Transforms & Scale)")]
    [Tooltip("Hệ số scale tổng thể")]
    public float masterScale = 1.0f;

    [Tooltip("Khoảng cách từ tâm ô ra vị trí các căn nhà")]
    public float houseDistance = 0.45f;

    [Tooltip("Độ nâng trục Y cho tường thành đá")]
    public float wallYOffset = 0.0f;

    [Tooltip("Độ nâng trục Y cho hàng rào gỗ")]
    public float fenceYOffset = 0.0f;

    [Tooltip("Độ nâng trục Y cho các công trình nhà ở")]
    public float buildingYOffset = 0.0f;

    [Tooltip("Góc xoay bù trừ cho nhà dân để cửa chính hướng vào tâm làng (mặc định 180 độ cho model KayKit)")]
    public float houseFacingOffset = 180.0f;

    [Tooltip("Góc xoay bù trừ cho hàng rào và tường thành (mặc định 0 độ để thân tường chạy dọc theo cạnh hex)")]
    public float borderRotationOffset = 0.0f;

    [Tooltip("Góc xoay bù trừ cho cầu thang đá (mặc định 90 độ để bậc thang hướng dọc theo sườn dốc leo lên)")]
    public float stairRotationOffset = 90.0f;

    [Header("Tỉ lệ tương đối giữa các loại công trình (Multiplier)")]
    [Tooltip("Tỉ lệ nhà ở")]
    public float buildingMultiplier = 0.33f;

    [Tooltip("Tỉ lệ vật thể trung tâm (giếng, rơm)")]
    public float centerpieceMultiplier = 0.36f;

    [Tooltip("Tỉ lệ lâu đài / tháp canh")]
    public float castleMultiplier = 0.42f;

    [Tooltip("Tỉ lệ rào gỗ & tường đá")]
    public float borderMultiplier = 1.0f;

    [Tooltip("Hệ số chiều cao riêng cho hàng rào/tường thành (trục Y). Giúp giảm chiều cao rào về tầm hông/ngực (ví dụ 0.5 - 0.6) mà không làm hở mép nối ngang.")]
    [Range(0.2f, 1.5f)]
    public float borderHeightMultiplier = 0.55f;

    [Tooltip("Tỉ lệ phụ kiện đường phố (thùng gỗ, xe rùa)")]
    public float propMultiplier = 0.33f;

    [Tooltip("Tỉ lệ hoa cỏ trang trí (mầm cỏ, hoa dại)")]
    public float floraMultiplier = 0.35f;

    [Tooltip("Tỉ lệ cây bóng mát làng quê")]
    public float treeMultiplier = 0.45f;

    [Tooltip("Tỉ lệ bụi cây rậm rạp & dương xỉ")]
    public float bushMultiplier = 0.40f;

    [Tooltip("Tỉ lệ đá lát đường (đủ to rõ để nhìn rõ trục phố đi bộ từ camera trên cao)")]
    public float stoneMultiplier = 0.42f;

    /// <summary>
    /// Nơi quản lý và cung cấp Scale theo đúng 100% giá trị người dùng cài đặt trong Inspector
    /// </summary>
    public float GetScale(SettlementScaleType type = SettlementScaleType.Building)
    {
        switch (type)
        {
            case SettlementScaleType.Centerpiece:
                return masterScale * centerpieceMultiplier;
            case SettlementScaleType.Castle:
                return masterScale * castleMultiplier;
            case SettlementScaleType.Border:
                return masterScale * borderMultiplier;
            case SettlementScaleType.Prop:
                return masterScale * propMultiplier;
            case SettlementScaleType.Flora:
                return masterScale * floraMultiplier;
            case SettlementScaleType.Tree:
                return masterScale * (treeMultiplier > 0 ? treeMultiplier : 0.45f);
            case SettlementScaleType.Bush:
                return masterScale * (bushMultiplier > 0 ? bushMultiplier : 0.40f);
            case SettlementScaleType.SteppingStone:
                return masterScale * stoneMultiplier;
            case SettlementScaleType.Building:
            default:
                return masterScale * buildingMultiplier;
        }
    }

    /// <summary>
    /// Cự ly đặt nhà tính từ tâm ô theo đúng giá trị người dùng cài đặt trong Inspector
    /// </summary>
    public float GetHouseDistance()
    {
        return houseDistance;
    }

    private static SettlementAssetConfig _activeConfig;
    public static SettlementAssetConfig ActiveConfig
    {
        get
        {
            if (_activeConfig == null)
            {
                if (SettlementClusterManager.Instance != null && SettlementClusterManager.Instance.Config != null)
                {
                    _activeConfig = SettlementClusterManager.Instance.Config;
                }
                else
                {
                    _activeConfig = Resources.Load<SettlementAssetConfig>("SettlementAssetConfig");
                }
            }
            return _activeConfig;
        }
        set => _activeConfig = value;
    }

    /// <summary>
    /// Truy xuất scale chuẩn ở bất cứ đâu (kể cả UI / Ghost Preview) chỉ với 1 dòng code duy nhất
    /// </summary>
    public static float GetGlobalScale(SettlementScaleType type = SettlementScaleType.Building)
    {
        return ActiveConfig != null ? ActiveConfig.GetScale(type) : 1.0f;
    }


    // --- Helper Methods & Defensive Pickers ---

    public static bool IsValidPrefab(GameObject go)
    {
        if (go == null) return false;
        try
        {
            if (!go) return false;
            if (go.transform == null) return false;
            return !string.IsNullOrEmpty(go.name);
        }
        catch
        {
            return false;
        }
    }

    public static GameObject PickRandomValid(List<GameObject> list, System.Random rng)
    {
        if (list == null || list.Count == 0) return null;
        int count = list.Count;
        int startIndex = rng.Next(0, count);
        for (int i = 0; i < count; i++)
        {
            int idx = (startIndex + i) % count;
            GameObject item = list[idx];
            if (IsValidPrefab(item))
            {
                return item;
            }
        }
        return null;
    }

    public GameObject GetRandomSmallHouse(System.Random rng)
    {
        // Ưu tiên xen kẽ asset kiến trúc tùy chỉnh do người dùng thêm vào
        if (customBuildings != null && customBuildings.Count > 0 && (smallHouses == null || smallHouses.Count == 0 || rng.NextDouble() < 0.35))
        {
            var custom = PickRandomValid(customBuildings, rng);
            if (custom != null) return custom;
        }

        var house = PickRandomValid(smallHouses, rng);
        if (house != null) return house;

        return PickRandomValid(customBuildings, rng);
    }

    public GameObject GetRandomMediumHouse(System.Random rng)
    {
        if (customBuildings != null && customBuildings.Count > 0 && rng.NextDouble() < 0.35)
        {
            var custom = PickRandomValid(customBuildings, rng);
            if (custom != null) return custom;
        }

        var house = PickRandomValid(mediumHouses, rng);
        if (house != null) return house;

        return GetRandomSmallHouse(rng);
    }

    public GameObject GetRandomVillageCenterpiece(System.Random rng)
    {
        var center = PickRandomValid(villageCenterpieces, rng);
        if (center != null) return center;
        return IsValidPrefab(marketBuilding) ? marketBuilding : null;
    }

    public GameObject GetRandomCommercialBuilding(System.Random rng)
    {
        var b = PickRandomValid(commercialBuildings, rng);
        if (b != null) return b;
        if (IsValidPrefab(marketBuilding)) return marketBuilding;
        if (IsValidPrefab(tavernBuilding)) return tavernBuilding;
        return GetRandomMediumHouse(rng);
    }

    public GameObject GetRandomProductionBuilding(System.Random rng)
    {
        var b = PickRandomValid(productionBuildings, rng);
        if (b != null) return b;
        if (IsValidPrefab(blacksmithBuilding)) return blacksmithBuilding;
        if (IsValidPrefab(windmillBuilding)) return windmillBuilding;
        return GetRandomMediumHouse(rng);
    }

    public GameObject GetRandomMilitaryBuilding(System.Random rng)
    {
        var b = PickRandomValid(militaryBuildings, rng);
        if (b != null) return b;
        if (IsValidPrefab(wallTower)) return wallTower;
        return GetRandomMediumHouse(rng);
    }

    public GameObject GetRandomCivicBuilding(System.Random rng)
    {
        var b = PickRandomValid(civicBuildings, rng);
        if (b != null) return b;
        if (IsValidPrefab(castleBuilding)) return castleBuilding;
        if (IsValidPrefab(churchBuilding)) return churchBuilding;
        return GetRandomMediumHouse(rng);
    }

    public GameObject GetRandomStreetProp(System.Random rng)
    {
        // Ưu tiên cao đồ trang trí tùy chỉnh do người dùng thêm vào
        if (customProps != null && customProps.Count > 0 && (streetProps == null || streetProps.Count == 0 || rng.NextDouble() < 0.75))
        {
            var custom = PickRandomValid(customProps, rng);
            if (custom != null) return custom;
        }

        var prop = PickRandomValid(streetProps, rng);
        if (prop != null) return prop;

        return PickRandomValid(customProps, rng);
    }

    public GameObject GetRandomFlora(System.Random rng)
    {
        // Ưu tiên cao thực vật / hoa cỏ tùy chỉnh do người dùng thêm vào
        if (customFlora != null && customFlora.Count > 0 && (villageFlora == null || villageFlora.Count == 0 || rng.NextDouble() < 0.75))
        {
            var custom = PickRandomValid(customFlora, rng);
            if (custom != null) return custom;
        }

        var f = PickRandomValid(villageFlora, rng);
        if (f != null) return f;

        return PickRandomValid(customFlora, rng);
    }

    public GameObject GetRandomSteppingStone(System.Random rng)
    {
        // Ưu tiên cao phiến đá lát lối đi tùy chỉnh do người dùng thêm vào
        if (customSteppingStones != null && customSteppingStones.Count > 0 && (pathSteppingStones == null || pathSteppingStones.Count == 0 || rng.NextDouble() < 0.75))
        {
            var custom = PickRandomValid(customSteppingStones, rng);
            if (custom != null) return custom;
        }

        var s = PickRandomValid(pathSteppingStones, rng);
        if (s != null) return s;

        return PickRandomValid(customSteppingStones, rng);
    }

    public GameObject GetRandomTree(System.Random rng)
    {
        if (customFlora != null && customFlora.Count > 0 && rng.Next(0, 100) < 35)
        {
            var match = PickRandomValid(customFlora, rng);
            if (match != null) return match;
        }

        var tree = PickRandomValid(villageTrees, rng);
        if (tree != null) return tree;

        return GetRandomBush(rng);
    }

    public GameObject GetRandomBush(System.Random rng)
    {
        if (customFlora != null && customFlora.Count > 0 && rng.Next(0, 100) < 30)
        {
            var match = PickRandomValid(customFlora, rng);
            if (match != null) return match;
        }

        var bush = PickRandomValid(villageBushes, rng);
        if (bush != null) return bush;

        return GetRandomFlora(rng);
    }
}
