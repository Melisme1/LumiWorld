#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tiện ích Editor một chạm (One-Click Setup) để tự động thiết lập Hệ thống Làng mạc & Đô thị:
/// 1. Tự động quét toàn bộ model FBX trong gói KayKit Medieval và gán vào SettlementAssetConfig.asset (trong Assets/Resources/).
/// 2. Tạo thẻ bài Medieval_Village.asset (CardData) trong Assets/Data/Cards/.
/// 3. Đăng ký thẻ bài vào CardCatalog.asset để lưu và nạp save mượt mà.
/// </summary>
public static class SettlementSetupUtility
{
    public const string KayKitUnityFbxFolder = "Assets/KayKit_Medieval_Hexagon_Pack_1.0_FREE/KayKit_Medieval_Hexagon_Pack_1.0_FREE/Assets/fbx(unity)";

    [InitializeOnLoadMethod]
    private static void AutoRunOnEditorLoad()
    {
        EditorApplication.delayCall += () =>
        {
            string configPath = "Assets/Resources/SettlementAssetConfig.asset";
            string cardPath = "Assets/Data/Cards/Medieval_Village.asset";

            if (!File.Exists(configPath) || !File.Exists(cardPath))
            {
                SetupSettlementSystem(forceRefreshFbx: true);
            }
            else
            {
                SettlementAssetConfig config = AssetDatabase.LoadAssetAtPath<SettlementAssetConfig>(configPath);
                if (config != null && (NeedsUnityFbxUpgrade(config) || config.villageTrees == null || config.villageTrees.Count == 0 || config.villageBushes == null || config.villageBushes.Count == 0))
                {
                    Debug.Log("<color=#38BDF8>[Settlement Setup] Tự động cập nhật cây bóng mát, bụi rậm và hạ tầng đường xá mới...</color>");
                    SetupSettlementSystem(forceRefreshFbx: false);
                }
            }
        };
    }

    [MenuItem("LumiWorld/Settlement Setup/Auto Configure Settlement System & Card", priority = 101)]
    public static void AutoConfigureMenu()
    {
        SetupSettlementSystem(forceRefreshFbx: false);
    }

    [MenuItem("LumiWorld/Settlement Setup/Force Re-scan All Assets from fbx(unity) Folder", priority = 102)]
    public static void ForceRescanUnityFbxMenu()
    {
        SetupSettlementSystem(forceRefreshFbx: true);
    }

    public static void SetupSettlementSystem(bool forceRefreshFbx = false)
    {
        // 1. Tạo thư mục Resources nếu chưa có
        string resourcesDir = "Assets/Resources";
        if (!AssetDatabase.IsValidFolder(resourcesDir))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        // 2. Tìm hoặc tạo SettlementAssetConfig.asset
        string configPath = "Assets/Resources/SettlementAssetConfig.asset";
        SettlementAssetConfig config = AssetDatabase.LoadAssetAtPath<SettlementAssetConfig>(configPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<SettlementAssetConfig>();
            AssetDatabase.CreateAsset(config, configPath);
            Debug.Log("<color=#38BDF8>[Settlement Setup] Đã tạo mới asset SettlementAssetConfig.asset trong Assets/Resources/</color>");
        }

        // 3. Tự động tìm và gán các model chuẩn từ thư mục fbx(unity) của KayKit
        PopulateModelsFromKayKit(config, forceRefreshFbx);
        EditorUtility.SetDirty(config);

        // 4. Tìm hoặc tạo CardData Medieval_Village.asset
        string cardsDir = "Assets/Data/Cards";
        if (!AssetDatabase.IsValidFolder(cardsDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            AssetDatabase.CreateFolder("Assets/Data", "Cards");
        }

        string cardPath = $"{cardsDir}/Medieval_Village.asset";
        CardData villageCard = AssetDatabase.LoadAssetAtPath<CardData>(cardPath);
        if (villageCard == null)
        {
            villageCard = ScriptableObject.CreateInstance<CardData>();
            AssetDatabase.CreateAsset(villageCard, cardPath);
            Debug.Log("<color=#38BDF8>[Settlement Setup] Đã tạo mới CardData Medieval_Village.asset trong Assets/Data/Cards/</color>");
        }

        // Cấu hình thông số cho thẻ Làng
        villageCard.cardID = "Medieval_Village";
        villageCard.cardName = "Làng Trung Cổ";
        villageCard.cardType = CardType.Building;
        villageCard.isSettlementCard = true;
        villageCard.occupiesTile = true;
        villageCard.baseScore = 5;
        villageCard.placementFee = 0;
        villageCard.shopPrice = 25;

        // Thử tìm sprite icon phù hợp trong UI_Icon_card
        if (villageCard.cardImage == null)
        {
            string[] iconGuids = AssetDatabase.FindAssets("t:Sprite", new string[] { "Assets/UI_Icon_card" });
            if (iconGuids != null && iconGuids.Length > 0)
            {
                villageCard.cardImage = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(iconGuids[0]));
            }
        }

        EditorUtility.SetDirty(villageCard);

        // 5. Đăng ký vào CardCatalog nếu có
        string catalogPath = "Assets/Data/Save/CardCatalog.asset";
        CardCatalog catalog = AssetDatabase.LoadAssetAtPath<CardCatalog>(catalogPath);
        if (catalog != null)
        {
            var field = typeof(CardCatalog).GetField("cards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                var cardList = field.GetValue(catalog) as List<CardData>;
                if (cardList != null && !cardList.Contains(villageCard))
                {
                    cardList.Add(villageCard);
                    EditorUtility.SetDirty(catalog);
                    Debug.Log("<color=#10B981>[Settlement Setup] Đã đăng ký thẻ Medieval_Village vào CardCatalog.asset thành công!</color>");
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=#10B981><b>[Settlement Setup] HOÀN TẤT THIẾT LẬP HỆ THỐNG LÀNG MẠC & ĐÔ THỊ!</b></color>\n" +
                  "• Đã cấu hình đầy đủ Model FBX vào SettlementAssetConfig.\n" +
                  "• Đã tạo Thẻ Bài Medieval_Village.asset sẵn sàng kéo thả trong game!");
    }

    private static void PopulateModelsFromKayKit(SettlementAssetConfig config, bool forceRefresh = false)
    {
        if (forceRefresh || NeedsUnityFbxUpgrade(config))
        {
            if (config.smallHouses != null) config.smallHouses.Clear();
            if (config.mediumHouses != null) config.mediumHouses.Clear();
            if (config.villageCenterpieces != null) config.villageCenterpieces.Clear();
            if (config.commercialBuildings != null) config.commercialBuildings.Clear();
            if (config.productionBuildings != null) config.productionBuildings.Clear();
            if (config.militaryBuildings != null) config.militaryBuildings.Clear();
            if (config.civicBuildings != null) config.civicBuildings.Clear();
            if (config.streetProps != null) config.streetProps.Clear();
            if (config.villageTrees != null) config.villageTrees.Clear();
            if (config.villageBushes != null) config.villageBushes.Clear();

            config.marketBuilding = null;
            config.windmillBuilding = null;
            config.blacksmithBuilding = null;
            config.tavernBuilding = null;
            config.castleBuilding = null;
            config.churchBuilding = null;
            config.fenceStraight = null;
            config.fenceGate = null;
            config.wallStraight = null;
            config.wallGate = null;
            config.wallCorner = null;
            config.wallTower = null;
        }

        // 1. Nhà nhỏ đa dạng màu sắc (Small Cottages)
        if (config.smallHouses == null) config.smallHouses = new List<GameObject>();
        if (config.smallHouses.Count == 0)
        {
            string[] colors = { "green", "blue", "yellow", "red" };
            foreach (var c in colors)
            {
                AddIfFound(config.smallHouses, $"building_home_A_{c}");
                AddIfFound(config.smallHouses, $"building_home_B_{c}");
            }
        }

        // 2. Nhà lớn / Nhà phố (Medium / Urban Houses)
        if (config.mediumHouses == null) config.mediumHouses = new List<GameObject>();
        if (config.mediumHouses.Count == 0)
        {
            AddIfFound(config.mediumHouses, "building_home_A_yellow");
            AddIfFound(config.mediumHouses, "building_home_B_yellow");
            AddIfFound(config.mediumHouses, "building_home_A_red");
            AddIfFound(config.mediumHouses, "building_home_B_red");
            AddIfFound(config.mediumHouses, "building_home_A_blue");
            AddIfFound(config.mediumHouses, "building_home_B_blue");
        }

        // 3. Vật thể tâm làng (Village Centerpieces)
        if (config.villageCenterpieces == null) config.villageCenterpieces = new List<GameObject>();
        if (config.villageCenterpieces.Count == 0)
        {
            AddIfFound(config.villageCenterpieces, "building_well_green");
            AddIfFound(config.villageCenterpieces, "building_well_blue");
            AddIfFound(config.villageCenterpieces, "building_well_yellow");
            AddIfFound(config.villageCenterpieces, "building_well_red");
            AddIfFound(config.villageCenterpieces, "tent");
            AddIfFound(config.villageCenterpieces, "building_grain");
            AddIfFound(config.villageCenterpieces, "building_stage_A");
            AddIfFound(config.villageCenterpieces, "building_stage_B");
            AddIfFound(config.villageCenterpieces, "building_stage_C");
            AddIfFound(config.villageCenterpieces, "building_scaffolding");
            AddIfFound(config.villageCenterpieces, "building_destroyed");
            AddIfFound(config.villageCenterpieces, "flag_green");
            AddIfFound(config.villageCenterpieces, "flag_blue");
            AddIfFound(config.villageCenterpieces, "flag_yellow");
            AddIfFound(config.villageCenterpieces, "flag_red");
        }

        // 4. Công trình thương mại & Dịch vụ (Commercial: Market, Tavern, Stage)
        if (config.commercialBuildings == null) config.commercialBuildings = new List<GameObject>();
        if (config.commercialBuildings.Count == 0)
        {
            string[] colors = { "yellow", "green", "blue", "red" };
            foreach (var c in colors)
            {
                AddIfFound(config.commercialBuildings, $"building_market_{c}");
                AddIfFound(config.commercialBuildings, $"building_tavern_{c}");
            }
            AddIfFound(config.commercialBuildings, "building_stage_A");
            AddIfFound(config.commercialBuildings, "building_stage_B");
            AddIfFound(config.commercialBuildings, "building_stage_C");
        }

        // 5. Công trình sản xuất & Nghề thủ công (Production: Windmill, Watermill, Blacksmith, Lumbermill, Mine)
        if (config.productionBuildings == null) config.productionBuildings = new List<GameObject>();
        if (config.productionBuildings.Count == 0)
        {
            string[] colors = { "green", "red", "yellow", "blue" };
            foreach (var c in colors)
            {
                AddIfFound(config.productionBuildings, $"building_blacksmith_{c}");
                AddIfFound(config.productionBuildings, $"building_windmill_{c}");
                AddIfFound(config.productionBuildings, $"building_lumbermill_{c}");
                AddIfFound(config.productionBuildings, $"building_mine_{c}");
                AddIfFound(config.productionBuildings, $"building_watermill_{c}");
            }
        }

        // 6. Công trình quân sự & Quốc phòng (Military: Barracks, Archery, Catapult)
        if (config.militaryBuildings == null) config.militaryBuildings = new List<GameObject>();
        if (config.militaryBuildings.Count == 0)
        {
            string[] colors = { "red", "blue", "green", "yellow" };
            foreach (var c in colors)
            {
                AddIfFound(config.militaryBuildings, $"building_barracks_{c}");
                AddIfFound(config.militaryBuildings, $"building_archeryrange_{c}");
                AddIfFound(config.militaryBuildings, $"building_tower_catapult_{c}");
            }
        }

        // 7. Lõi biểu tượng Đại Đô Thị (Civic Core: Castle, Church)
        if (config.civicBuildings == null) config.civicBuildings = new List<GameObject>();
        if (config.civicBuildings.Count == 0)
        {
            string[] colors = { "blue", "red", "green", "yellow" };
            foreach (var c in colors)
            {
                AddIfFound(config.civicBuildings, $"building_castle_{c}");
                AddIfFound(config.civicBuildings, $"building_church_{c}");
            }
        }

        // Legacy direct references
        if (config.marketBuilding == null) config.marketBuilding = FindFbx("building_market_yellow");
        if (config.windmillBuilding == null) config.windmillBuilding = FindFbx("building_windmill_green");
        if (config.blacksmithBuilding == null) config.blacksmithBuilding = FindFbx("building_blacksmith_red");
        if (config.tavernBuilding == null) config.tavernBuilding = FindFbx("building_tavern_yellow");
        if (config.castleBuilding == null) config.castleBuilding = FindFbx("building_castle_blue");
        if (config.churchBuilding == null) config.churchBuilding = FindFbx("building_church_blue");

        // 8. Hàng rào gỗ
        if (config.fenceStraight == null) config.fenceStraight = FindFbx("fence_wood_straight");
        if (config.fenceGate == null) config.fenceGate = FindFbx("fence_wood_straight_gate");

        // 9. Tường thành đá
        if (config.wallStraight == null) config.wallStraight = FindFbx("wall_straight");
        if (config.wallGate == null) config.wallGate = FindFbx("wall_straight_gate");
        if (config.wallCorner == null) config.wallCorner = FindFbx("wall_corner_A_outside");
        if (config.wallTower == null) config.wallTower = FindFbx("building_tower_A_blue");

        // 10. Props đường phố phong phú (LƯU Ý: Bỏ qua hoàn toàn folder decoration/nature)
        if (config.streetProps == null) config.streetProps = new List<GameObject>();
        if (config.streetProps.Count == 0)
        {
            // Thùng kiện hàng
            AddIfFound(config.streetProps, "barrel");
            AddIfFound(config.streetProps, "crate_A_small");
            AddIfFound(config.streetProps, "crate_A_big");
            AddIfFound(config.streetProps, "crate_B_small");
            AddIfFound(config.streetProps, "crate_B_big");
            AddIfFound(config.streetProps, "crate_long_A");
            AddIfFound(config.streetProps, "crate_long_B");
            AddIfFound(config.streetProps, "crate_long_C");
            AddIfFound(config.streetProps, "crate_open");

            // Dụng cụ & phương tiện lao động
            AddIfFound(config.streetProps, "wheelbarrow");
            AddIfFound(config.streetProps, "sack");
            AddIfFound(config.streetProps, "ladder");
            AddIfFound(config.streetProps, "pallet");

            // Đống tài nguyên
            AddIfFound(config.streetProps, "resource_lumber");
            AddIfFound(config.streetProps, "resource_stone");

            // Quân dụng & tập bắn
            AddIfFound(config.streetProps, "weaponrack");
            AddIfFound(config.streetProps, "target");
            AddIfFound(config.streetProps, "bucket_arrows");

            // Xô chậu
            AddIfFound(config.streetProps, "bucket_water");
            AddIfFound(config.streetProps, "bucket_empty");
        }

        // 10. Thảm thực vật nhỏ (LumiWorld Flora: Micro grass, Clover, Wildflowers - CHỈ DÙNG PREFAB MỚI)
        if (config.villageFlora == null) config.villageFlora = new List<GameObject>();
        if (config.villageFlora.Count == 0 || forceRefresh)
        {
            config.villageFlora.Clear();
            // Ground Scatter từ Bloomfield mới
            AddPrefabIfFound(config.villageFlora, "Assets/VinhWorkSpace/Prefabs/Bloomfield/03_GroundScatter/CloverDaisyClump.prefab");
            AddPrefabIfFound(config.villageFlora, "Assets/VinhWorkSpace/Prefabs/Bloomfield/03_GroundScatter/PebbleFlowerScatter.prefab");

            // Mầm cỏ và bụi cây nhỏ nền đất
            for (int i = 1; i <= 5; i++)
            {
                AddPrefabIfFound(config.villageFlora, $"Assets/Acs/Art/Flower/Prefabs/Grass_{i}.prefab");
                AddPrefabIfFound(config.villageFlora, $"Assets/Acs/Art/Flower/Prefabs/Plant_{i}.prefab");
            }

            // Hoa nhỏ xinh xắn quanh nhà (chỉ chọn các loài hoa nhỏ, bỏ qua hoa hướng dương hoặc hoa cuống quá cao)
            int[] smallFlowerIndices = { 1, 2, 4, 5, 7, 8, 10, 12 };
            foreach (int fIdx in smallFlowerIndices)
            {
                AddPrefabIfFound(config.villageFlora, $"Assets/Acs/Art/Flower/Prefabs/Flower_{fIdx}.prefab");
            }
        }

        // 11. Đá nhỏ lát đường (Stepping Stones)
        if (config.pathSteppingStones == null) config.pathSteppingStones = new List<GameObject>();
        if (config.pathSteppingStones.Count == 0 || forceRefresh)
        {
            config.pathSteppingStones.Clear();
            // Các viên sỏi nhỏ làm đá lát đường (bỏ Small_7 vì quá to và p1, p2 vì lệch tâm)
            int[] smallStoneIndices = { 1, 2, 3, 4, 5, 6, 8 };
            foreach (int sIdx in smallStoneIndices)
            {
                AddPrefabIfFound(config.pathSteppingStones, $"Assets/Acs/Art/Stone/Prefabs/Stones/Small_{sIdx}.prefab");
            }
        }

        // 12. Cây bóng mát làng quê (Village Shade Trees)
        if (config.villageTrees == null) config.villageTrees = new List<GameObject>();
        if (config.villageTrees.Count == 0 || forceRefresh)
        {
            config.villageTrees.Clear();
            AddIfFound(config.villageTrees, "tree_single_A");
            AddIfFound(config.villageTrees, "tree_single_B");
            AddIfFound(config.villageTrees, "trees_A_small");
            AddIfFound(config.villageTrees, "trees_B_small");
            AddPrefabIfFound(config.villageTrees, "Assets/Acs/Art/Forest/Prefabs/Pine_Tree01.prefab");
            AddPrefabIfFound(config.villageTrees, "Assets/Acs/Art/Forest/Prefabs/Pine_Tree02.prefab");
        }

        // 13. Bụi cây rậm & Dương xỉ (Village Bushes & Shrubs)
        if (config.villageBushes == null) config.villageBushes = new List<GameObject>();
        if (config.villageBushes.Count == 0 || forceRefresh)
        {
            config.villageBushes.Clear();
            AddPrefabIfFound(config.villageBushes, "Assets/Acs/Art/Forest/Prefabs/Flower_Bush.prefab");
            AddPrefabIfFound(config.villageBushes, "Assets/VinhWorkSpace/Prefabs/Bloomfield/02_TallFlora/LavenderSpikeBush.prefab");
            for (int b = 1; b <= 5; b++)
            {
                AddPrefabIfFound(config.villageBushes, $"Assets/Acs/Art/Flower/Prefabs/Broadleaf_{b}.prefab");
                AddPrefabIfFound(config.villageBushes, $"Assets/Acs/Art/Flower/Prefabs/Fern_{b}.prefab");
                AddPrefabIfFound(config.villageBushes, $"Assets/Acs/Art/Flower/Prefabs/Plant_{b}.prefab");
            }
        }

        // 14. Thang kết nối Đa tầng (Terrace Cliff Ladder)
        if (config.woodStairsPrefab == null || forceRefresh)
        {
            config.woodStairsPrefab = FindFbx("ladder");
        }

        // Tự động làm sạch các tham chiếu rỗng hoặc hỏng (Clean Missing References)
        CleanList(config.smallHouses);
        CleanList(config.mediumHouses);
        CleanList(config.villageCenterpieces);
        CleanList(config.commercialBuildings);
        CleanList(config.productionBuildings);
        CleanList(config.militaryBuildings);
        CleanList(config.civicBuildings);
        CleanList(config.streetProps);
        CleanList(config.villageFlora);
        CleanList(config.pathSteppingStones);
        CleanList(config.villageTrees);
        CleanList(config.villageBushes);
        CleanList(config.customBuildings);
        CleanList(config.customFlora);
        CleanList(config.customProps);
        CleanList(config.customSteppingStones);

        // Cập nhật thông số scale mặc định hoàn mỹ
        config.buildingMultiplier = 0.30f; // Scale nhỏ lại một chút để cụm nhà vừa vặn, không bị chen chúc
        config.stoneMultiplier = 0.42f;
        config.treeMultiplier = 0.45f;
        config.bushMultiplier = 0.40f;

        // Đảm bảo các danh sách tùy biến của người dùng luôn được khởi tạo sẵn sàng
        if (config.customBuildings == null) config.customBuildings = new List<GameObject>();
        if (config.customFlora == null) config.customFlora = new List<GameObject>();
        if (config.customProps == null) config.customProps = new List<GameObject>();
        if (config.customSteppingStones == null) config.customSteppingStones = new List<GameObject>();
    }

    private static void CleanList(List<GameObject> list)
    {
        if (list == null) return;
        list.RemoveAll(item => item == null || !SettlementAssetConfig.IsValidPrefab(item));
    }

    private static void AddPrefabIfFound(List<GameObject> list, string assetPath)
    {
        if (list == null) return;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab != null && !list.Contains(prefab))
        {
            list.Add(prefab);
        }
    }

    private static bool NeedsUnityFbxUpgrade(SettlementAssetConfig config)
    {
        if (config == null) return true;

        if (config.smallHouses != null && config.smallHouses.Count > 0)
        {
            foreach (var go in config.smallHouses)
            {
                if (go != null && !IsFromUnityFbx(go)) return true;
            }
        }
        else return true;

        if (config.castleBuilding != null && !IsFromUnityFbx(config.castleBuilding)) return true;
        if (config.fenceStraight != null && !IsFromUnityFbx(config.fenceStraight)) return true;
        if (config.wallStraight != null && !IsFromUnityFbx(config.wallStraight)) return true;
        if (config.villageFlora == null || config.villageFlora.Count == 0) return true;
        if (config.pathSteppingStones == null || config.pathSteppingStones.Count == 0) return true;

        return false;
    }

    private static bool IsFromUnityFbx(GameObject go)
    {
        if (go == null) return false;
        string path = AssetDatabase.GetAssetPath(go);
        return !string.IsNullOrEmpty(path) && path.IndexOf("fbx(unity)", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void AddIfFound(List<GameObject> list, string modelName)
    {
        GameObject go = FindFbx(modelName);
        if (go != null && !list.Contains(go))
        {
            list.Add(go);
        }
    }

    private static GameObject FindFbx(string modelName)
    {
        // 1. Ưu tiên tuyệt đối tìm trực tiếp trong thư mục chuẩn hóa riêng cho Unity: fbx(unity)
        if (AssetDatabase.IsValidFolder(KayKitUnityFbxFolder))
        {
            string[] unityGuids = AssetDatabase.FindAssets($"{modelName} t:Model", new string[] { KayKitUnityFbxFolder });
            if (unityGuids == null || unityGuids.Length == 0)
            {
                unityGuids = AssetDatabase.FindAssets($"{modelName} t:GameObject", new string[] { KayKitUnityFbxFolder });
            }

            if (unityGuids != null && unityGuids.Length > 0)
            {
                foreach (var guid in unityGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    {
                        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (model != null) return model;
                    }
                }
            }
        }

        // 2. Tìm kiếm dự phòng trên toàn bộ gói KayKit, lọc và ưu tiên path có chứa "fbx(unity)"
        string[] allGuids = AssetDatabase.FindAssets($"{modelName} t:Model", new string[] { "Assets/KayKit_Medieval_Hexagon_Pack_1.0_FREE" });
        if (allGuids == null || allGuids.Length == 0)
        {
            allGuids = AssetDatabase.FindAssets($"{modelName} t:GameObject", new string[] { "Assets/KayKit_Medieval_Hexagon_Pack_1.0_FREE" });
        }

        if (allGuids != null && allGuids.Length > 0)
        {
            // Lượt 1: Bắt buộc fbx(unity)
            foreach (var guid in allGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) &&
                    path.IndexOf("fbx(unity)", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (model != null) return model;
                }
            }

            // Lượt 2: Fallback nếu không có trong fbx(unity)
            foreach (var guid in allGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (model != null) return model;
                }
            }
        }

        return null;
    }
}
#endif
