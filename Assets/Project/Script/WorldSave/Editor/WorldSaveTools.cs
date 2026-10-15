#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Công cụ Editor cho lưu world: xây danh mục thẻ và ô đất, và xóa save để thử lại từ đầu.
/// Người chơi không có các nút này.
/// </summary>
public static class WorldSaveTools
{
    private const string CatalogFolder = "Assets/Data/Save";
    private const string CardCatalogPath = CatalogFolder + "/CardCatalog.asset";
    private const string TileCatalogPath = CatalogFolder + "/TileCatalog.asset";

    [MenuItem("Tools/LumiWorld/Xây danh mục save")]
    public static void BuildCatalogs()
    {
        EnsureFolder();

        // Thẻ: mọi CardData trong project, kể cả các thẻ ShopCatalog chưa có
        var cards = new List<CardData>();
        foreach (string guid in AssetDatabase.FindAssets("t:CardData"))
        {
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
            if (card != null) cards.Add(card);
        }

        var cardCatalog = LoadOrCreate<CardCatalog>(CardCatalogPath);
        cardCatalog.SetCards(cards);
        EditorUtility.SetDirty(cardCatalog);

        // Ô đất: prefab sinh đảo (đang mở trong scene) và hai đầu của mọi luật biến đổi trên thẻ
        var tiles = new Dictionary<string, TileCatalog.Entry>();
        var generators = Object.FindObjectsByType<HexWorldGenerator>(FindObjectsSortMode.None);
        foreach (var generator in generators)
        {
            foreach (GameObject prefab in generator.TerrainPrefabs) AddTile(tiles, prefab);
        }
        foreach (CardData card in cards)
        {
            foreach (var pair in card.transformRules)
            {
                AddTile(tiles, pair.aridTilePrefab);
                AddTile(tiles, pair.lushTilePrefab);
            }

            // TransformTile dùng prefabToPlace làm ô mới khi luật biến đổi không khớp
            if (card.IsTileTransformCard()) AddTile(tiles, card.prefabToPlace);
        }

        var tileCatalog = LoadOrCreate<TileCatalog>(TileCatalogPath);
        tileCatalog.SetEntries(new List<TileCatalog.Entry>(tiles.Values));
        EditorUtility.SetDirty(tileCatalog);

        AssetDatabase.SaveAssets();
        Debug.Log($"[LumiWorld World] Danh mục: {cards.Count} thẻ, {tiles.Count} ô đất" +
            (generators.Length == 0 ? ". Chưa thấy HexWorldGenerator trong scene nên thiếu ô sinh đảo: mở MapBuilding rồi chạy lại." : "."));
    }

    /// <summary>
    /// Tạo scene VisitWorld từ MapBuilding (giai đoạn 3). Scene có cùng WorldSaveSystem và TileCatalog,
    /// nên đảo người khác dựng lại đúng như MapBuilding nhưng ở chế độ chỉ xem.
    /// </summary>
    [MenuItem("Tools/LumiWorld/Tạo scene VisitWorld (copy MapBuilding)")]
    public static void CreateVisitScene()
    {
        string[] guids = AssetDatabase.FindAssets("MapBuilding t:Scene");
        if (guids.Length == 0)
        {
            Debug.LogError("[LumiWorld Save] Không tìm thấy scene MapBuilding.");
            return;
        }

        string source = AssetDatabase.GUIDToAssetPath(guids[0]);
        string target = Path.Combine(Path.GetDirectoryName(source), "VisitWorld.unity").Replace('\\', '/');
        if (File.Exists(target))
        {
            Debug.Log("[LumiWorld Save] Đã có scene VisitWorld, bỏ qua.");
            return;
        }

        if (!AssetDatabase.CopyAsset(source, target))
        {
            Debug.LogError($"[LumiWorld Save] Không copy được {source} sang {target}.");
            return;
        }

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(scene => scene.path == target))
            scenes.Add(new EditorBuildSettingsScene(target, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[LumiWorld Save] Đã tạo {target} và thêm vào Build Settings.");
    }

    [MenuItem("Tools/LumiWorld/Xóa save của tôi (thử lại từ đầu)")]
    public static void DeleteMySave()
    {
        string owner = SaveSlot.PlayerId;
        string folder = Application.persistentDataPath;

        foreach (string prefix in new[] { "world_", "economy_" })
        {
            foreach (string suffix in new[] { ".json", ".json.bak", ".json.broken", ".json.tmp" })
            {
                string path = Path.Combine(folder, prefix + owner + suffix);
                if (File.Exists(path)) File.Delete(path);
            }
        }

        Debug.Log($"[LumiWorld World] Đã xóa save của '{owner}'. Lần vào game tới sẽ sinh đảo mới và nhận Starter Kit.");
    }

    private static void AddTile(Dictionary<string, TileCatalog.Entry> tiles, GameObject prefab)
    {
        if (prefab == null) return;

        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
        if (string.IsNullOrEmpty(guid) || tiles.ContainsKey(guid)) return;

        tiles.Add(guid, new TileCatalog.Entry { key = guid, prefab = prefab });
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(CatalogFolder)) AssetDatabase.CreateFolder("Assets/Data", "Save");
    }
}
#endif
