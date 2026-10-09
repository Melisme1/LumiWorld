using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace LumiWorld.Acs.Editor
{
    [InitializeOnLoad]
    public static class ResourceInventoryUISetup
    {
        private const string Pack = "Assets/Acs/UI/LumiWorld_Resource_Inventory_UI_Assets";
        private const string SkinFolder = "Assets/Acs/UI/Skins/IvorySage";
        private const string SkinPath = "Assets/Acs/Data/Resources/UI/ResourceInventoryUISkin.asset";
        private const string PrefabPath = "Assets/Acs/Prefabs/UI/ResourceInventoryUIRoot.prefab";
        private const string FontPath = "Assets/Acs/UI/Fonts/LumiWorldVietnameseSDF.asset";
        public const string Vietnamese = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ·—‹›/+:,.()\n" +
            "ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝĂĐĨŨƠƯàáâãèéêìíòóôõùúýăđĩũơư" +
            "ẠẢẤẦẨẪẬẮẰẲẴẶẸẺẼẾỀỂỄỆỈỊỌỎỐỒỔỖỘỚỜỞỠỢỤỦỨỪỬỮỰỲỴỶỸ" +
            "ạảấầẩẫậắằẳẵặẹẻẽếềểễệỉịọỏốồổỗộớờởỡợụủứừửữựỳỵỷỹ";
        [Serializable] private sealed class Manifest { public Entry[] assets = Array.Empty<Entry>(); }
        [Serializable] private sealed class Coordinates { public float x = 0, y = 0, width = 0, height = 0; }
        [Serializable] private sealed class Border { public float left = 0, bottom = 0, right = 0, top = 0; }
        [Serializable] private sealed class Entry { public string key = null, file = null; public Coordinates sprite_rect_unity_px = new Coordinates(); public Border border_lbrt_px_recommended = new Border(); }

        static ResourceInventoryUISetup() { EditorApplication.delayCall += AutoSetup; }
        private static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (File.Exists(Pack + "/03_UI_SPRITE_IMPORT_SETTINGS.json") && !File.Exists(SkinPath)) Setup();
        }
        [MenuItem("Tools/LumiWorld/Resources/Setup Inventory UI Skin")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("[LumiWorld UI] Dừng Play trước khi setup skin/font/prefab."); return; }
            Directory.CreateDirectory(SkinFolder); Directory.CreateDirectory(Path.GetDirectoryName(SkinPath));
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath)); Directory.CreateDirectory(Path.GetDirectoryName(FontPath));
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Pack + "/03_UI_SPRITE_IMPORT_SETTINGS.json"));
            var skin = AssetDatabase.LoadAssetAtPath<ResourceInventoryUISkin>(SkinPath);
            if (skin == null) { skin = ScriptableObject.CreateInstance<ResourceInventoryUISkin>(); AssetDatabase.CreateAsset(skin, SkinPath); }
            foreach (var entry in manifest.assets)
            {
                string target = SkinFolder + "/" + Path.GetFileName(entry.file);
                if (!File.Exists(target)) File.Copy(Pack + "/" + entry.file, target);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(target);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritePixelsPerUnit = 100; importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteGenerateFallbackPhysicsShape = false;
                importer.SetTextureSettings(settings);
                foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" }) importer.ClearPlatformTextureSettings(platform);
                var factories = new SpriteDataProviderFactories(); factories.Init();
                var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
                var previous = provider.GetSpriteRects(); var id = previous.Length > 0 ? previous[0].spriteID : GUID.Generate();
                var rect = entry.sprite_rect_unity_px; var border = entry.border_lbrt_px_recommended;
                provider.SetSpriteRects(new[] { new SpriteRect { name = entry.key, spriteID = id,
                    rect = new Rect(rect.x,rect.y,rect.width,rect.height), alignment = SpriteAlignment.Center, pivot = new Vector2(.5f,.5f),
                    border = new Vector4(border.left,border.bottom,border.right,border.top) } });
                provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(entry.key,id) });
                provider.Apply(); importer.SaveAndReimport();
                var sprite = AssetDatabase.LoadAllAssetsAtPath(target).OfType<Sprite>().Single();
                var field = typeof(ResourceInventoryUISkin).GetField(ToField(entry.key)); field.SetValue(skin, sprite);
            }
            skin.font = CreateFont();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                var root = new GameObject("ResourceInventoryUIRoot", typeof(RectTransform), typeof(ResourceInventoryUIController));
                root.GetComponent<ResourceInventoryUIController>().Initialize(null, skin);
                skin.uiPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); UnityEngine.Object.DestroyImmediate(root);
            }
            else skin.uiPrefab = prefab;
            EditorUtility.SetDirty(skin); AssetDatabase.SaveAssets();
            Debug.Log("[LumiWorld UI] Skin 12 sprites, font tiếng Việt và prefab đã sẵn sàng. Play MapBuilding để dùng UI.");
        }
        private static TMP_FontAsset CreateFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
                font = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                font.name = "LumiWorld Vietnamese SDF"; AssetDatabase.CreateAsset(font, FontPath);
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var texture in font.atlasTextures) { texture.name = "Vietnamese Atlas"; AssetDatabase.AddObjectToAsset(texture, font); }
            }
            if (!font.TryAddCharacters(Vietnamese, out string missing)) Debug.LogWarning("[LumiWorld UI] Font thiếu glyph: " + missing);
            EditorUtility.SetDirty(font); return font;
        }
        private static string ToField(string key)
        {
            switch(key)
            {
                case "panel_habitat": return "panelHabitat"; case "window_main": return "windowMain"; case "surface_inset": return "surfaceInset";
                case "button_primary": return "buttonPrimary"; case "button_secondary": return "buttonSecondary"; case "slot_resource": return "slotResource";
                case "slot_selected": return "slotSelected"; case "slot_locked": return "slotLocked"; case "button_icon": return "buttonIcon";
                case "icon_warehouse": return "iconWarehouse"; case "icon_close": return "iconClose"; case "icon_lock": return "iconLock";
                default: throw new ArgumentException("Unknown UI sprite key: " + key);
            }
        }
    }
}
