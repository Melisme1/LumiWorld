using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace LumiWorld.Acs.Editor
{
    [InitializeOnLoad]
    public static class LumiWorldUIProductionSetup
    {
        public const string Root = "Assets/Acs/Art/UI/Resources/UI/Production";
        private const string SkinPath = "Assets/Acs/Data/Resources/UI/ResourceInventoryUISkin.asset";
        private const string PrefabPath = "Assets/Acs/Prefabs/UI/ResourceInventoryUIRoot.prefab";

        static LumiWorldUIProductionSetup()
        {
            EditorApplication.delayCall += EnsureImported;
        }

        private static void EnsureImported()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += EnsureImported;
                return;
            }
            if (File.Exists(Root + "/Art/A-01.png") && !File.Exists(Root + "/Nunito-SemiBold SDF.asset"))
            {
                Setup();
                return;
            }

            // Ensure all glyph textures have smooth mipmaps and uncompressed RGBA
            var sampleGlyph = AssetImporter.GetAtPath(Root + "/Glyphs/G-01.png") as TextureImporter;
            if (sampleGlyph != null && (!sampleGlyph.mipmapEnabled || sampleGlyph.mipMapsPreserveCoverage))
            {
                if (Directory.Exists(Root + "/Glyphs"))
                {
                    foreach (var file in Directory.GetFiles(Root + "/Glyphs", "*.png"))
                    {
                        string assetPath = file.Replace('\\', '/');
                        Import(assetPath, 128, Vector4.zero, true);
                    }
                    AssetDatabase.SaveAssets();
                }
            }
        }

        [MenuItem("LumiWorld/UI/Import production assets and fonts")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before UI import.");

            var source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "LumiWorld/UI");
            if (!Directory.Exists(source) && !Directory.Exists(Root + "/Art"))
                throw new DirectoryNotFoundException(source);

            Directory.CreateDirectory(Root + "/Art");
            Directory.CreateDirectory(Root + "/Glyphs");
            Directory.CreateDirectory(Root + "/Surfaces");
            Directory.CreateDirectory(Root + "/Fonts");
            Directory.CreateDirectory(Path.GetDirectoryName(SkinPath));
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

            foreach (var file in Directory.Exists(source) ? Directory.GetFiles(source, "*.png") : Array.Empty<string>())
            {
                var id = Path.GetFileNameWithoutExtension(file);
                bool isGlyphAsset = id.StartsWith("G-") || id.StartsWith("I-");
                var target = Root + (isGlyphAsset ? "/Glyphs/" : "/Art/") + id + ".png";
                // Preserve pre-normalized pure-white glyphs in Glyphs/ if already present
                if (!File.Exists(target))
                    File.Copy(file, target, true);
                Import(target, isGlyphAsset ? 128 : 512, default, isGlyphAsset);
            }

            for (int i = 1; i <= 34; i++)
            {
                string path = Root + "/Glyphs/G-" + i.ToString("00") + ".png";
                if (!File.Exists(path)) Save(LumiWorldGlyphDrawing.Draw(i), path, Vector4.zero, true);
                else Import(path, 256, Vector4.zero, true);
            }

            if (!File.Exists(Root + "/Glyphs/Close.png")) Save(LumiWorldGlyphDrawing.Draw(100), Root + "/Glyphs/Close.png", Vector4.zero, true);
            else Import(Root + "/Glyphs/Close.png", 256, Vector4.zero, true);

            if (!File.Exists(Root + "/Glyphs/Lock.png")) Save(LumiWorldGlyphDrawing.Draw(101), Root + "/Glyphs/Lock.png", Vector4.zero, true);
            else Import(Root + "/Glyphs/Lock.png", 256, Vector4.zero, true);

            if (Directory.Exists(Root + "/Glyphs"))
            {
                foreach (var file in Directory.GetFiles(Root + "/Glyphs", "*.png"))
                    Import(file.Replace('\\', '/'), 128, Vector4.zero, true);
            }

            if (Directory.Exists(Root + "/Creatures"))
            {
                foreach (var file in Directory.GetFiles(Root + "/Creatures", "*.png"))
                    Import(file.Replace('\\', '/'), 512, Vector4.zero, false);
                string portraitsDir = Root + "/Creatures/Portraits";
                if (Directory.Exists(portraitsDir))
                {
                    foreach (var file in Directory.GetFiles(portraitsDir, "*.png"))
                        Import(file.Replace('\\', '/'), 256, Vector4.zero, false);
                }
            }

            Save(Surface(false), Root + "/Surfaces/Surface.png", new Vector4(24, 24, 24, 24), false);
            Save(Surface(true), Root + "/Surfaces/Border.png", new Vector4(24, 24, 24, 24), false);

            foreach (var fontName in new[] { "Nunito-SemiBold", "Nunito-ExtraBold" })
            {
                string srcTtf = "Assets/Acs/Art/UI/Fonts/" + fontName + ".ttf";
                string dstTtf = Root + "/Fonts/" + fontName + ".ttf";
                if (File.Exists(srcTtf) && !File.Exists(dstTtf))
                    File.Copy(srcTtf, dstTtf, true);
            }

            AssetDatabase.Refresh();

            var body = Font("Nunito-SemiBold");
            Font("Nunito-ExtraBold");

            var skin = AssetDatabase.LoadAssetAtPath<ResourceInventoryUISkin>(SkinPath);
            if (skin == null)
            {
                skin = ScriptableObject.CreateInstance<ResourceInventoryUISkin>();
                AssetDatabase.CreateAsset(skin, SkinPath);
            }

            skin.font = body;
            var surface = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Surfaces/Surface.png");
            skin.panelHabitat = skin.windowMain = skin.surfaceInset = surface;
            skin.buttonPrimary = skin.buttonSecondary = skin.buttonIcon = surface;
            skin.slotResource = skin.slotLocked = surface;
            skin.slotSelected = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Surfaces/Border.png");
            skin.iconWarehouse = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Glyphs/G-01.png");
            skin.iconClose = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Glyphs/Close.png");
            skin.iconLock = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Glyphs/Lock.png");
            skin.ivory = LumiWorldUITheme.Window;
            skin.forest = LumiWorldUITheme.Text;
            skin.sage = LumiWorldUITheme.Success;
            skin.muted = LumiWorldUITheme.Muted;
            skin.panelPPU = skin.windowPPU = skin.insetPPU = skin.buttonPPU = 1f;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                var rootGo = new GameObject("ResourceInventoryUIRoot", typeof(RectTransform), typeof(ResourceInventoryUIController));
                rootGo.GetComponent<ResourceInventoryUIController>().Initialize(null, skin);
                skin.uiPrefab = PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);
                UnityEngine.Object.DestroyImmediate(rootGo);
            }
            else
            {
                skin.uiPrefab = prefab;
            }

            EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssets();
            Debug.Log("UI_PRODUCTION_READY: 45 supplied assets, functional glyphs, surfaces and Nunito fonts imported.");
        }

        private static TMP_FontAsset Font(string name)
        {
            string path = Root + "/" + name + " SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;

            var source = AssetDatabase.LoadAssetAtPath<UnityEngine.Font>(Root + "/Fonts/" + name + ".ttf")
                      ?? AssetDatabase.LoadAssetAtPath<UnityEngine.Font>("Assets/Acs/Art/UI/Fonts/" + name + ".ttf");
            if (source == null) throw new InvalidOperationException("Missing font: " + name);

            var font = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic);
            font.name = name + " SDF";
            font.TryAddCharacters(LumiWorldUITheme.VietnameseChars, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning(name + " missing optional symbols: " + missing);
            AssetDatabase.CreateAsset(font, path);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            EditorUtility.SetDirty(font);
            return font;
        }

        private static void Import(string path, int maxSize, Vector4 border = default, bool isGlyph = false)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = isGlyph;
            importer.mipMapsPreserveCoverage = false;
            importer.filterMode = isGlyph ? FilterMode.Trilinear : FilterMode.Bilinear;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }

        private static void Save(Texture2D texture, string path, Vector4 border, bool isGlyph)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            Import(path, isGlyph ? 256 : 512, border, isGlyph);
        }

        private static Texture2D Surface(bool border)
        {
            var texture = new Texture2D(96, 96, TextureFormat.RGBA32, false);
            for (int y = 0; y < 96; y++)
            {
                for (int x = 0; x < 96; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - 48f) - 26f, 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - 48f) - 26f, 0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(22f - distance);
                    if (border) alpha *= Mathf.Clamp01(distance - 18.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();
            return texture;
        }
    }
}
