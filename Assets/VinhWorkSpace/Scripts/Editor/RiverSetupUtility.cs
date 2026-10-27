#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tiện ích Editor một chạm (One-Click Setup) để tự động cấu hình Hệ thống Sông Suối:
/// 1. Tự động tìm và gán 6-7 model FBX của Meshy AI vào RiverConfig.
/// 2. Tạo asset RiverConfig trong Assets/Resources/.
/// 3. Tạo thẻ bài River.asset (CardData) trong Assets/Data/Cards/.
/// 4. Đăng ký thẻ bài River vào ShopCatalog để người chơi có thể mua và rút bài.
/// </summary>
public static class RiverSetupUtility
{
    [MenuItem("LumiWorld/River Setup/Auto Configure River System & Card", priority = 100)]
    public static void SetupRiverSystem()
    {
        // 1. Tạo thư mục Resources nếu chưa có
        string resourcesDir = "Assets/Resources";
        if (!AssetDatabase.IsValidFolder(resourcesDir))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        // 2. Tìm hoặc tạo RiverConfig.asset
        string configPath = "Assets/Resources/RiverConfig.asset";
        RiverConfig config = AssetDatabase.LoadAssetAtPath<RiverConfig>(configPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<RiverConfig>();
            AssetDatabase.CreateAsset(config, configPath);
            Debug.Log("<color=#38BDF8>[River Setup] Đã tạo mới asset RiverConfig.asset trong Assets/Resources/</color>");
        }

        // 3. Quét tự động các file FBX trong Assets/VinhWorkSpace/Props/Waterfall
        config.straightPrefab = FindFbx("Stream_Path");
        config.fork3WayPrefab = FindFbx("Stone_Crossroad");
        config.springHeadPrefab = FindFbx("Stone_Hollow");
        config.pondBasinPrefab = FindFbx("Meadow_Pond");
        config.waterfallPrefab = FindFbx("Stone_Stairw");

        config.modelScale = Vector3.one;
        config.yElevationOffset = 0.02f;
        config.springHeadYOffset = 0.285f;
        config.springHeadOffset = Vector3.zero;
        config.pondBasinYOffset = 0.125f;
        config.pondOffset = Vector3.zero;
        config.pondTextureOffset = Vector2.zero;
        config.straightYOffset = 0.095f;
        config.curve120YOffset = 0.135f;
        config.curve60YOffset = 0.135f;
        config.fork3WayYOffset = 0.080f;
        config.waterfallYOffset = 0.477f;
        config.waterfallOffset = new Vector3(0f, 0f, 0.18f);
        config.waterfallRotationOffset = 0f;
        config.globalRotationOffset = 0f;

        config.pointsPerRiverTile = 3;
        config.pointsPerCoLocatedBiome = 2;
        config.pointsPerAdjacentBiome = 1;
        config.pointsPerAdjacentCreature = 5;
        config.pointsPeninsulaBonus = 5;

        EditorUtility.SetDirty(config);
        Debug.Log("<color=#10B981>[River Setup] Đã gán thành công 6 mô hình FBX vào RiverConfig!</color>");

        // 4. Tìm hoặc tạo CardData River.asset
        string cardsDir = "Assets/Data/Cards";
        if (!AssetDatabase.IsValidFolder(cardsDir))
        {
            Directory.CreateDirectory(cardsDir);
            AssetDatabase.Refresh();
        }

        string riverCardPath = "Assets/Data/Cards/River.asset";
        CardData riverCard = AssetDatabase.LoadAssetAtPath<CardData>(riverCardPath);
        if (riverCard == null)
        {
            riverCard = ScriptableObject.CreateInstance<CardData>();
            AssetDatabase.CreateAsset(riverCard, riverCardPath);
            Debug.Log("<color=#38BDF8>[River Setup] Đã tạo mới thẻ bài River.asset trong Assets/Data/Cards/</color>");
        }

        riverCard.cardID = "River";
        riverCard.cardName = "River";
        riverCard.cardType = CardType.Special;
        riverCard.isRiverCard = true;
        riverCard.occupiesTile = false;
        riverCard.replacesTile = false;
        riverCard.baseScore = 3;
        riverCard.alwaysBaseScore = false;
        riverCard.shopPrice = 30;
        riverCard.placementFee = 5;

        // Gán sprite đại diện nếu tìm thấy Streamstone hoặc Lilywater
        Sprite riverSprite = FindSprite("Streamstone");
        if (riverSprite == null) riverSprite = FindSprite("Lilywater");
        if (riverSprite != null)
        {
            riverCard.cardImage = riverSprite;
        }

        EditorUtility.SetDirty(riverCard);

        // 5. Đăng ký vào ShopCatalog nếu có
        string[] shopGuids = AssetDatabase.FindAssets("t:ShopCatalog");
        foreach (string guid in shopGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ShopCatalog shop = AssetDatabase.LoadAssetAtPath<ShopCatalog>(path);
            if (shop != null && !shop.cards.Contains(riverCard))
            {
                shop.cards.Add(riverCard);
                EditorUtility.SetDirty(shop);
                Debug.Log($"<color=#10B981>[River Setup] Đã thêm thẻ River vào ShopCatalog: {path}</color>");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Cấu Hình Sông Suối Hoàn Tất",
            "Hệ thống Sông Suối đã được cấu hình thành công!\n\n" +
            "- RiverConfig: Đã gán 6 mô hình suối uốn lượn\n" +
            "- CardData: Đã tạo thẻ bài 'River' (Special, occupiesTile = false)\n" +
            "- Cửa hàng: Đã đăng ký River vào ShopCatalog\n" +
            "- Cơ chế điểm: Đã tích hợp điểm suối + Biome + Thú (Bến nước muôn loài)",
            "OK"
        );
    }

    private static GameObject FindFbx(string keyword)
    {
        string[] guids = AssetDatabase.FindAssets($"{keyword} t:Model");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (obj != null) return obj;
            }
        }
        return null;
    }

    private static Sprite FindSprite(string keyword)
    {
        string[] guids = AssetDatabase.FindAssets($"{keyword} t:Sprite");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        return null;
    }
}
#endif
