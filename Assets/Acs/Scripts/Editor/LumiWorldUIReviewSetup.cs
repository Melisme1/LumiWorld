using System.IO;
using System.Linq;
using LumiWorld.Acs.Review;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LumiWorld.Acs.Editor
{
    public static class LumiWorldUIReviewSetup
    {
        private const string Root = "Assets/Acs/UI/Review";
        [MenuItem("LumiWorld/UI Review/Build and Open Inventory + Crafting")]
        public static void Open()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode before opening UI review."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Create();
        }

        // Explicit batch entry point. Does not run on import or touch gameplay scenes.
        public static void Create()
        {
            Directory.CreateDirectory(Root);
            var sprite = CreateRoundedSprite();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Review Camera").AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.45f, .58f, .58f); camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
            var canvasObject = new GameObject("UI Review Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var host = new GameObject("Inventory and Crafting Review", typeof(RectTransform)); var rect = host.GetComponent<RectTransform>(); rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(1600, 900);
            var review = host.AddComponent<LumiWorldUIReview>(); review.rounded = sprite;
            review.font = LumiWorldUITheme.Body ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Acs/UI/Fonts/LumiWorldVietnameseSDF.asset");
            if (review.font == null) review.font = TMP_Settings.defaultFontAsset;
            review.items = new[] {
                Item("Oak Wood", "Ingredients", "Leafwood/lumi_t1_leafwood_oak_wood", 120),
                Item("Sticks", "Ingredients", "Leafwood/lumi_t1_leafwood_sticks", 36),
                Item("Tender Leaves", "Ingredients", "Leafwood/lumi_t1_leafwood_tender_leaves", 54),
                Item("Raspberries", "Ingredients", "Leafwood/lumi_t2_leafwood_raspberries", 28),
                Item("Acorns", "Ingredients", "Leafwood/lumi_t2_leafwood_acorns", 17),
                Item("Wild Daisies", "Ingredients", "Bloomfield/lumi_t1_bloomfield_wild_daisies", 32),
                Item("Blueberries", "Ingredients", "Windheath/lumi_t2_windheath_mountain_blueberries", 48),
                Item("Lavender", "Ingredients", "Bloomfield/lumi_t2_bloomfield_lavender_sprigs", 15),
                Item("Dandelions", "Ingredients", "Bloomfield/lumi_t1_bloomfield_dandelion_flowers", 26),
                Item("Nectar", "Ingredients", "Bloomfield/lumi_t2_bloomfield_nectar", 9),
                Item("Wheat", "Ingredients", "Bloomfield/lumi_t2_bloomfield_wheet", 60),
                Item("Rosemary", "Ingredients", "Windheath/lumi_t2_windheath_rosemary", 22),
                Item("Flax Stalks", "Ingredients", "Windheath/lumi_t2_windheath_flax_stalks", 37),
                Item("Lichen", "Ingredients", "Windheath/lumi_t1_windheath_lichen", 44),
                Item("Juniper Berries", "Ingredients", "Windheath/lumi_t2_windheath_juniper_berries", 39),
                Item("Foxtail Plumes", "Ingredients", "Windheath/lumi_t1_windheath_foxtail_plumes", 18),
                Item("Roasted Acorns", "Food", "Products/lumi_product_roasted_acorns", 12),
                Item("Berry Nut Cake", "Food", "Products/lumi_product_forest_berry_nut_cake", 8),
                Item("Dried Raspberries", "Food", "Products/lumi_product_dried_raspberries", 16),
                Item("Forest Herbal Syrup", "Medicine", "Products/lumi_product_forest_herbal_syrup", 9),
                Item("Bark Paper", "Products", "Products/lumi_product_bark_paper", 15),
                Item("Lavender Paper", "Products", "Products/lumi_product_lavender_scented_paper", 6)
            };
            new GameObject("Review EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            review.Build();
            EditorSceneManager.SaveScene(scene, Root + "/InventoryCraftingReview.unity");
            Capture(camera, "InventoryReview.png");
            review.crafting = true; review.Build(); Capture(camera, "CraftingReview.png");
            review.crafting = false; review.Build();
            EditorSceneManager.SaveScene(scene, Root + "/InventoryCraftingReview.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("UI_REVIEW_CREATED: " + Root + "/InventoryCraftingReview.unity");
        }

        private static LumiWorldUIReview.Item Item(string name, string category, string path, int quantity)
        {
            var source = "Assets/Acs/Icons/" + path + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(source);
            if (sprite == null)
            {
                // Import a review copy; preserve the production artwork and its existing importer.
                Directory.CreateDirectory(Root + "/Items"); var copy = Root + "/Items/" + Path.GetFileName(source);
                File.Copy(source, copy, true); AssetDatabase.ImportAsset(copy);
                var importer = (TextureImporter)AssetImporter.GetAtPath(copy); importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport();
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(copy);
            }
            return new LumiWorldUIReview.Item { name = name, category = category, art = sprite, quantity = quantity };
        }

        private static Sprite CreateRoundedSprite()
        {
            const int size = 96; const float radius = 24;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + .5f - size / 2f) - (size / 2f - radius), 0);
                float dy = Mathf.Max(Mathf.Abs(y + .5f - size / 2f) - (size / 2f - radius), 0);
                texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + .5f)));
            }
            texture.Apply(); string path = Root + "/RoundedSurface.png"; File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
            importer.spriteBorder = new Vector4(26, 26, 26, 26); importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void Capture(Camera camera, string name)
        {
            string output = Path.GetFullPath("../docs/LumiWorld_UI_Production_Pack/Review"); Directory.CreateDirectory(output);
            var target = new RenderTexture(1600, 900, 24); var previous = RenderTexture.active;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            foreach (var text in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None)) text.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
        }
    }
}
