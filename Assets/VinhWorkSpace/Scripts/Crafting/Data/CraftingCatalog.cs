using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LumiWorld.Economy.Crafting
{
    /// <summary>
    /// Danh mục quản lý tập trung toàn bộ công thức chế biến trong game.
    /// Tự động nạp từ thư mục Resources hoặc khởi tạo các công thức chuẩn khi chưa có file asset.
    /// </summary>
    public static class CraftingCatalog
    {
        private static List<CraftingRecipeData> all;
        private static readonly Dictionary<string, CraftingRecipeData> byId = new Dictionary<string, CraftingRecipeData>(StringComparer.Ordinal);
        private static readonly Dictionary<string, CraftingRecipeData> byOutputId = new Dictionary<string, CraftingRecipeData>(StringComparer.Ordinal);
        private static readonly Dictionary<string, ResourceData> fallbackResources = new Dictionary<string, ResourceData>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Sprite> loadedSprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        /// <summary>
        /// Bản đồ đường dẫn asset ảnh icon cho toàn bộ thành phẩm và nguyên liệu thô trong Assets/Acs/Icons.
        /// </summary>
        public static readonly Dictionary<string, string> IconAssetPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // --- 15 THÀNH PHẨM CHẾ BIẾN (Assets/Acs/Icons/Products) ---
            { "resource.leafwood.tender_leaves_crispy", "Assets/Acs/Icons/Products/lumi_product_crispy_roasted_leaves.png" },
            { "resource.leafwood.roasted_acorns", "Assets/Acs/Icons/Products/lumi_product_roasted_acorns.png" },
            { "resource.leafwood.dried_raspberries", "Assets/Acs/Icons/Products/lumi_product_dried_raspberries.png" },
            { "resource.windheath.dried_mountain_blueberries", "Assets/Acs/Icons/Products/lumi_product_dried_blueberries.png" },
            { "resource.bloomfield.dried_dandelion", "Assets/Acs/Icons/Products/lumi_product_dried_dandelion_flowers.png" },
            { "resource.bloomfield.nectar_essence", "Assets/Acs/Icons/Products/lumi_product_nectar_essence.png" },
            { "resource.plain.wheat_flour", "Assets/Acs/Icons/Products/lumi_product_wheat_flour.png" },
            { "resource.bloomfield.herbal_flower_powder", "Assets/Acs/Icons/Products/lumi_product_herbal_flower_powder.png" },
            { "resource.leafwood.bark_paper", "Assets/Acs/Icons/Products/lumi_product_bark_paper.png" },
            { "resource.food.dried_wild_berries_mix", "Assets/Acs/Icons/Products/lumi_product_dried_forest_berry_mix.png" },
            { "resource.leafwood.honey_glazed_acorns", "Assets/Acs/Icons/Products/lumi_product_nectar_glazed_acorns.png" },
            { "resource.forest.wild_herbal_syrup", "Assets/Acs/Icons/Products/lumi_product_forest_herbal_syrup.png" },
            { "resource.craft.pressed_flower_paper", "Assets/Acs/Icons/Products/lumi_product_pressed_daisy_paper.png" },
            { "resource.craft.scented_paper", "Assets/Acs/Icons/Products/lumi_product_lavender_scented_paper.png" },
            { "resource.food.wild_berry_nut_cake", "Assets/Acs/Icons/Products/lumi_product_forest_berry_nut_cake.png" },

            // --- NGUYÊN LIỆU THÔ LEAFWOOD (Assets/Acs/Icons/Leafwood) ---
            { "resource.leafwood.tender_leaves", "Assets/Acs/Icons/Leafwood/lumi_t1_leafwood_tender_leaves.png" },
            { "resource.leafwood.acorns", "Assets/Acs/Icons/Leafwood/lumi_t2_leafwood_acorns.png" },
            { "resource.leafwood.raspberries", "Assets/Acs/Icons/Leafwood/lumi_t2_leafwood_raspberries.png" },
            { "resource.leafwood.oak_wood", "Assets/Acs/Icons/Leafwood/lumi_t1_leafwood_oak_wood.png" },
            { "resource.leafwood.sticks", "Assets/Acs/Icons/Leafwood/lumi_t1_leafwood_sticks.png" },
            { "resource.leafwood.fallen_bark", "Assets/Acs/Icons/Leafwood/lumi_t1_leafwood_oak_wood.png" },

            // --- NGUYÊN LIỆU THÔ BLOOMFIELD (Assets/Acs/Icons/Bloomfield) ---
            { "resource.bloomfield.dandelion_flowers", "Assets/Acs/Icons/Bloomfield/lumi_t1_bloomfield_dandelion_flowers.png" },
            { "resource.bloomfield.wild_daisies", "Assets/Acs/Icons/Bloomfield/lumi_t1_bloomfield_wild_daisies.png" },
            { "resource.bloomfield.lavender_sprigs", "Assets/Acs/Icons/Bloomfield/lumi_t2_bloomfield_lavender_sprigs.png" },
            { "resource.bloomfield.nectar", "Assets/Acs/Icons/Bloomfield/lumi_t2_bloomfield_nectar.png" },

            // --- NGUYÊN LIỆU THÔ WINDHEATH / PLAIN (Assets/Acs/Icons/Windheath) ---
            { "resource.windheath.mountain_blueberries", "Assets/Acs/Icons/Windheath/lumi_t2_windheath_mountain_blueberries.png" },
            { "resource.windheath.rosemary", "Assets/Acs/Icons/Windheath/lumi_t2_windheath_rosemary.png" },
            { "resource.windheath.juniper_berries", "Assets/Acs/Icons/Windheath/lumi_t2_windheath_juniper_berries.png" },
            { "resource.windheath.foxtail_plumes", "Assets/Acs/Icons/Windheath/lumi_t1_windheath_foxtail_plumes.png" },
            { "resource.plain.wheat", "Assets/Acs/Icons/Windheath/lumi_t1_windheath_foxtail_plumes.png" },
            { "resource.windheath.flax_stalks", "Assets/Acs/Icons/Windheath/lumi_t2_windheath_flax_stalks.png" },
            { "resource.windheath.lichen", "Assets/Acs/Icons/Windheath/lumi_t1_windheath_lichen.png" }
        };

        public static IReadOnlyList<CraftingRecipeData> All
        {
            get
            {
                if (all == null) Load();
                return all;
            }
        }

        public static CraftingRecipeData Find(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return null;
            if (all == null) Load();
            byId.TryGetValue(recipeId, out CraftingRecipeData recipe);
            return recipe;
        }

        public static CraftingRecipeData FindByOutput(string outputResourceId)
        {
            if (string.IsNullOrEmpty(outputResourceId)) return null;
            if (all == null) Load();
            byOutputId.TryGetValue(outputResourceId, out CraftingRecipeData recipe);
            return recipe;
        }

        /// <summary>
        /// Tìm đường dẫn file icon PNG tương ứng với ID tài nguyên.
        /// </summary>
        public static string GetIconAssetPath(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId)) return null;
            IconAssetPaths.TryGetValue(resourceId, out string path);
            return path;
        }

        /// <summary>
        /// Nạp Sprite từ đường dẫn asset trong Unity Editor hoặc runtime fallback đọc file PNG.
        /// </summary>
        public static Sprite LoadSprite(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            if (loadedSprites.TryGetValue(assetPath, out Sprite cached) && cached != null)
            {
                return cached;
            }

#if UNITY_EDITOR
            Sprite edSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (edSprite != null)
            {
                loadedSprites[assetPath] = edSprite;
                return edSprite;
            }
#endif

            try
            {
                string relPath = assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                    ? assetPath.Substring(7)
                    : assetPath;
                string fullPath = Path.Combine(Application.dataPath, relPath);
                if (File.Exists(fullPath))
                {
                    byte[] fileData = File.ReadAllBytes(fullPath);
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(fileData))
                    {
                        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        if (sprite != null)
                        {
                            loadedSprites[assetPath] = sprite;
                            return sprite;
                        }
                    }
                }
            }
            catch
            {
                // Bỏ qua lỗi nạp file nếu môi trường không cho phép truy cập
            }

            return null;
        }

        /// <summary>
        /// Đảm bảo ResourceData có sprite icon từ danh mục Icons nếu hiện tại đang thiếu.
        /// </summary>
        public static void EnsureIcon(ResourceData res)
        {
            if (res == null) return;
            if (res.icon != null) return;

            string path = GetIconAssetPath(res.resourceID);
            if (!string.IsNullOrEmpty(path))
            {
                res.icon = LoadSprite(path);
            }
        }

        /// <summary>
        /// Đảm bảo CraftingRecipeData và outputResource của nó có sprite icon.
        /// </summary>
        public static void EnsureRecipeIcon(CraftingRecipeData recipe)
        {
            if (recipe == null) return;
            if (recipe.outputResource != null)
            {
                EnsureIcon(recipe.outputResource);
            }

            if (recipe.recipeIcon == null)
            {
                if (recipe.outputResource != null && recipe.outputResource.icon != null)
                {
                    recipe.recipeIcon = recipe.outputResource.icon;
                }
                else if (recipe.outputResource != null)
                {
                    string path = GetIconAssetPath(recipe.outputResource.resourceID);
                    if (!string.IsNullOrEmpty(path))
                    {
                        recipe.recipeIcon = LoadSprite(path);
                    }
                }
            }
        }

        private static readonly Dictionary<string, string> RawNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "resource.leafwood.tender_leaves", "Tender Leaves" },
            { "resource.leafwood.acorns", "Acorns" },
            { "resource.leafwood.raspberries", "Raspberries" },
            { "resource.leafwood.oak_wood", "Oak Wood" },
            { "resource.leafwood.sticks", "Sticks" },
            { "resource.leafwood.fallen_bark", "Fallen Bark" },
            { "resource.bloomfield.dandelion_flowers", "Dandelion Flowers" },
            { "resource.bloomfield.wild_daisies", "Wild Daisies" },
            { "resource.bloomfield.lavender_sprigs", "Lavender Sprigs" },
            { "resource.bloomfield.nectar", "Pure Nectar" },
            { "resource.windheath.mountain_blueberries", "Mountain Blueberries" },
            { "resource.windheath.rosemary", "Rosemary" },
            { "resource.windheath.juniper_berries", "Juniper Berries" },
            { "resource.windheath.foxtail_plumes", "Foxtail Plumes" },
            { "resource.plain.wheat", "Wheat" },
            { "resource.windheath.flax_stalks", "Flax Stalks" },
            { "resource.windheath.lichen", "Mountain Lichen" }
        };

        public static string GetFallbackRawName(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId)) return string.Empty;
            return RawNames.TryGetValue(resourceId, out string name) ? name : resourceId;
        }

        /// <summary>
        /// Tìm ResourceData của một tài nguyên: ưu tiên ResourceCatalog trên đĩa,
        /// nếu chưa có asset thì dùng bản fallback runtime từ công thức chế biến.
        /// Tự động nạp icon tương ứng từ Assets/Acs/Icons nếu asset thiếu icon.
        /// </summary>
        public static ResourceData GetResource(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId)) return null;

            // 1. Kiểm tra ResourceCatalog nếu đã có asset trên đĩa
            ResourceData res = ResourceCatalog.Find(resourceId);
            if (res != null)
            {
                EnsureIcon(res);
                return res;
            }

            // 2. Nạp catalog nếu chưa nạp
            if (all == null) Load();

            // 3. Tra cứu từ fallbackResources
            if (fallbackResources.TryGetValue(resourceId, out ResourceData fallback))
            {
                EnsureIcon(fallback);
                return fallback;
            }

            // 4. Tra cứu theo output của recipe
            if (byOutputId.TryGetValue(resourceId, out CraftingRecipeData recipe) && recipe.outputResource != null)
            {
                EnsureIcon(recipe.outputResource);
                return recipe.outputResource;
            }

            // 5. Nếu chưa có asset trên đĩa nhưng thuộc danh mục nguyên liệu gốc đã biết
            if (IconAssetPaths.TryGetValue(resourceId, out string iconPath))
            {
                ResourceData rawFallback = ScriptableObject.CreateInstance<ResourceData>();
                rawFallback.resourceID = resourceId;
                rawFallback.resourceName = GetFallbackRawName(resourceId);
                rawFallback.maxStackSize = 999;
                EnsureIcon(rawFallback);
                fallbackResources[resourceId] = rawFallback;
                return rawFallback;
            }

            return null;
        }

        /// <summary>
        /// Lấy tên tiếng Việt thân thiện của tài nguyên, tự động fallback nếu chưa có asset.
        /// </summary>
        public static string GetResourceName(string resourceId)
        {
            ResourceData res = GetResource(resourceId);
            string name = res != null && !string.IsNullOrEmpty(res.resourceName) ? res.resourceName : resourceId;
            return Acs.ResourceInventoryUIPresentation.LocalizeResourceName(resourceId, name);
        }

        /// <summary>
        /// Extended records for Resource Warehouse: raw materials and 15 crafted goods.
        /// </summary>
        public struct WarehouseExtraItem
        {
            public string id;
            public string name;
            public string biome;
            public string tier;
            public string description;
            public Sprite icon;
            public int capacity;
        }

        /// <summary>
        /// Provides list of extended warehouse items (Wheat, Fallen Bark, and 15 crafted products).
        /// </summary>
        public static List<WarehouseExtraItem> GetWarehouseExtraItems()
        {
            var list = new List<WarehouseExtraItem>();

            // 1. Two non-habitat raw materials (Wheat & Fallen Bark)
            var fallenBark = GetResource("resource.leafwood.fallen_bark");
            list.Add(new WarehouseExtraItem
            {
                id = "resource.leafwood.fallen_bark",
                name = fallenBark != null ? fallenBark.resourceName : "Fallen Bark",
                biome = "Leafwood",
                tier = "Raw",
                description = fallenBark != null ? fallenBark.description : "Sturdy oak bark peeled naturally from aged trunks in Leafwood, rich in fiber for papercraft.",
                icon = fallenBark?.icon,
                capacity = fallenBark != null && fallenBark.maxStackSize > 0 ? fallenBark.maxStackSize : 999
            });

            var wheat = GetResource("resource.plain.wheat");
            list.Add(new WarehouseExtraItem
            {
                id = "resource.plain.wheat",
                name = wheat != null ? wheat.resourceName : "Wheat",
                biome = "Meadows",
                tier = "Raw",
                description = wheat != null ? wheat.description : "Golden grain stalks brimming with nutritious starch.",
                icon = wheat?.icon,
                capacity = wheat != null && wheat.maxStackSize > 0 ? wheat.maxStackSize : 999
            });

            // 2. All 15 crafted goods
            foreach (var recipe in All)
            {
                if (recipe == null || recipe.outputResource == null) continue;
                var res = recipe.outputResource;
                EnsureIcon(res);
                string tierName;
                switch (recipe.category)
                {
                    case CraftingCategory.Food: tierName = "Food"; break;
                    case CraftingCategory.Refined: tierName = "Refined"; break;
                    case CraftingCategory.Herbal: tierName = "Herbal"; break;
                    case CraftingCategory.Paper: tierName = "Craft"; break;
                    default: tierName = "Crafting"; break;
                }

                list.Add(new WarehouseExtraItem
                {
                    id = res.resourceID,
                    name = res.resourceName,
                    biome = "Workshop",
                    tier = tierName,
                    description = res.description,
                    icon = recipe.GetIcon() ?? res.icon,
                    capacity = res.maxStackSize > 0 ? res.maxStackSize : 999
                });
            }

            return list;
        }

        public static List<CraftingRecipeData> GetByCategory(CraftingCategory category)
        {
            List<CraftingRecipeData> result = new List<CraftingRecipeData>();
            foreach (CraftingRecipeData recipe in All)
            {
                if (recipe.category == category)
                {
                    result.Add(recipe);
                }
            }
            return result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetCache()
        {
            all = null;
            byId.Clear();
            byOutputId.Clear();
            fallbackResources.Clear();
            loadedSprites.Clear();
        }

        private static void Load()
        {
            all = new List<CraftingRecipeData>();
            byId.Clear();
            byOutputId.Clear();

            // 1. Nạp từ các thư mục Resources
            foreach (CraftingRecipeData recipe in Resources.LoadAll<CraftingRecipeData>(string.Empty))
            {
                RegisterRecipe(recipe);
            }

            // 2. Nếu chưa có asset nào được tạo, khởi tạo fallback runtime definitions
            if (all.Count == 0)
            {
                RegisterDefaultRecipes();
            }

            Debug.Log($"[LumiWorld Chế Biến] Đã nạp {all.Count} công thức chế biến.");
        }

        public static void RegisterRecipe(CraftingRecipeData recipe)
        {
            if (recipe == null || string.IsNullOrEmpty(recipe.recipeID)) return;
            if (byId.ContainsKey(recipe.recipeID)) return;

            EnsureRecipeIcon(recipe);

            all.Add(recipe);
            byId[recipe.recipeID] = recipe;

            if (recipe.outputResource != null && !string.IsNullOrEmpty(recipe.outputResource.resourceID))
            {
                byOutputId[recipe.outputResource.resourceID] = recipe;
                fallbackResources[recipe.outputResource.resourceID] = recipe.outputResource;
            }
        }

        /// <summary>
        /// Tạo danh sách 15 công thức chuẩn từ GDD khi chưa có file ScriptableObject trên đĩa.
        /// </summary>
        private static void RegisterDefaultRecipes()
        {
            // Additional raw resources without asset files
            AddRawFallback("resource.leafwood.fallen_bark", "Fallen Bark",
                "Sturdy oak bark peeled naturally from aged trunks in Leafwood, rich in fiber for papercraft.",
                new Color(0.48f, 0.32f, 0.20f, 1f));

            AddRawFallback("resource.plain.wheat", "Wheat",
                "Golden grain stalks brimming with nutritious starch.",
                new Color(0.92f, 0.78f, 0.30f, 1f));

            // Tier 1 - Food
            AddFallback("recipe.food.crispy_tender_leaves", "Crispy Roasted Leaves",
                "Crispy, aromatic roasted leaves, a favorite crunchy snack for flying squirrels and forest creatures.",
                CraftingCategory.Food, "resource.leafwood.tender_leaves_crispy", 1, true, 5f, 30,
                new (string, int)[] { ("resource.leafwood.tender_leaves", 3) },
                new Color(0.35f, 0.75f, 0.30f, 1f));

            AddFallback("recipe.food.roasted_acorns", "Roasted Acorns",
                "Aromatic, toasted wild acorns rich in healthy fats and energizing nutrients for creatures.",
                CraftingCategory.Food, "resource.leafwood.roasted_acorns", 1, true, 5f, 35,
                new (string, int)[] { ("resource.leafwood.acorns", 2) },
                new Color(0.70f, 0.45f, 0.22f, 1f));

            AddFallback("recipe.food.dried_raspberries", "Dried Raspberries",
                "Sweet and tangy wild raspberries concentrated with nutrients through gentle breeze drying.",
                CraftingCategory.Food, "resource.leafwood.dried_raspberries", 1, true, 5f, 35,
                new (string, int)[] { ("resource.leafwood.raspberries", 2) },
                new Color(0.85f, 0.22f, 0.35f, 1f));

            AddFallback("recipe.food.dried_blueberries", "Dried Mountain Blueberries",
                "Tender, chewy dried highland blueberries that stimulate the appetite of winged creatures.",
                CraftingCategory.Food, "resource.windheath.dried_mountain_blueberries", 1, true, 5f, 35,
                new (string, int)[] { ("resource.windheath.mountain_blueberries", 2) },
                new Color(0.30f, 0.35f, 0.80f, 1f));

            // Tier 1 - Refined
            AddFallback("recipe.refined.dried_dandelion", "Dried Dandelion Flowers",
                "Sun-dried golden dandelion petals, preserving vibrant color and sweet floral pollen.",
                CraftingCategory.Refined, "resource.bloomfield.dried_dandelion", 1, false, 5f, 0,
                new (string, int)[] { ("resource.bloomfield.dandelion_flowers", 2) },
                new Color(0.95f, 0.85f, 0.20f, 1f));

            AddFallback("recipe.refined.nectar_essence", "Pure Nectar Essence",
                "Distilled floral nectar condensed into luminous, fragrant golden drops.",
                CraftingCategory.Refined, "resource.bloomfield.nectar_essence", 1, false, 8f, 0,
                new (string, int)[] { ("resource.bloomfield.nectar", 2) },
                new Color(1.00f, 0.70f, 0.10f, 1f));

            AddFallback("recipe.refined.wheat_flour", "Wheat Flour",
                "Finely milled golden wheat flour, the fundamental base for hearty baked goods.",
                CraftingCategory.Refined, "resource.plain.wheat_flour", 1, false, 6f, 0,
                new (string, int)[] { ("resource.plain.wheat", 3) },
                new Color(0.95f, 0.92f, 0.80f, 1f));

            // Tier 1 - Herbal & Paper
            AddFallback("recipe.herbal.flower_powder", "Herbal Flower Powder",
                "Finely ground blend of wild daisies and dandelions, soothing and calming for wild creatures.",
                CraftingCategory.Herbal, "resource.bloomfield.herbal_flower_powder", 1, false, 8f, 0,
                new (string, int)[] { ("resource.bloomfield.wild_daisies", 2), ("resource.bloomfield.dandelion_flowers", 2) },
                new Color(0.85f, 0.80f, 0.40f, 1f));

            AddFallback("recipe.paper.bark_paper", "Bark Paper",
                "Sturdy rustic parchment handcrafted from fallen oak bark and wood fibers.",
                CraftingCategory.Paper, "resource.leafwood.bark_paper", 1, false, 10f, 0,
                new (string, int)[] { ("resource.leafwood.fallen_bark", 2), ("resource.leafwood.oak_wood", 1) },
                new Color(0.80f, 0.72f, 0.58f, 1f));

            // Tier 2 - Food
            AddFallback("recipe.food.dried_wild_fruit_mix", "Dried Forest Fruit Mix",
                "A delightful blend of sweet wild raspberries and tangy highland blueberries.",
                CraftingCategory.Food, "resource.food.dried_wild_berries_mix", 1, true, 10f, 80,
                new (string, int)[] { ("resource.leafwood.dried_raspberries", 1), ("resource.windheath.dried_mountain_blueberries", 1) },
                new Color(0.60f, 0.25f, 0.65f, 1f));

            AddFallback("recipe.food.honey_glazed_acorns", "Honey-Glazed Acorns",
                "Crunchy roasted acorns glazed with sweet floral nectar essence, irresistible and wholesome.",
                CraftingCategory.Food, "resource.leafwood.honey_glazed_acorns", 1, true, 12f, 90,
                new (string, int)[] { ("resource.leafwood.acorns", 2), ("resource.bloomfield.nectar_essence", 1) },
                new Color(0.85f, 0.55f, 0.15f, 1f));

            // Tier 2 - Herbal & Paper
            AddFallback("recipe.herbal.forest_syrup", "Forest Herbal Syrup",
                "Concocted from nectar essence, aromatic rosemary, and juniper berries to restore vigor.",
                CraftingCategory.Herbal, "resource.forest.wild_herbal_syrup", 1, false, 12f, 0,
                new (string, int)[] { ("resource.bloomfield.nectar_essence", 1), ("resource.windheath.rosemary", 2), ("resource.windheath.juniper_berries", 2) },
                new Color(0.40f, 0.65f, 0.45f, 1f));

            AddFallback("recipe.paper.pressed_flower_paper", "Pressed Flower Paper",
                "Artisanal bark parchment delicately embedded with vibrant dried daisy petals.",
                CraftingCategory.Paper, "resource.craft.pressed_flower_paper", 1, false, 12f, 0,
                new (string, int)[] { ("resource.leafwood.bark_paper", 1), ("resource.bloomfield.wild_daisies", 2) },
                new Color(0.90f, 0.85f, 0.75f, 1f));

            AddFallback("recipe.paper.scented_paper", "Lavender Scented Paper",
                "Fine bark paper infused with soothing lavender blossoms, retaining a long-lasting floral fragrance.",
                CraftingCategory.Paper, "resource.craft.scented_paper", 1, false, 12f, 0,
                new (string, int)[] { ("resource.leafwood.bark_paper", 1), ("resource.bloomfield.lavender_sprigs", 2) },
                new Color(0.82f, 0.75f, 0.88f, 1f));

            // Tier 3 - Master Food
            AddFallback("recipe.food.wild_berry_nut_cake", "Forest Berry Nut Cake",
                "A masterwork delicacy uniting ingredients from all three biomes: glazed acorns, dried berries, and golden baked flour.",
                CraftingCategory.Food, "resource.food.wild_berry_nut_cake", 1, true, 20f, 250,
                new (string, int)[] {
                    ("resource.leafwood.honey_glazed_acorns", 1),
                    ("resource.food.dried_wild_berries_mix", 1),
                    ("resource.plain.wheat_flour", 2)
                },
                new Color(0.90f, 0.65f, 0.30f, 1f));
        }

        private static void AddRawFallback(string id, string name, string desc, Color color)
        {
            ResourceData res = ResourceCatalog.Find(id);
            if (res == null)
            {
                res = ScriptableObject.CreateInstance<ResourceData>();
                res.resourceID = id;
                res.resourceName = name;
                res.description = desc;
                res.resourceColor = color;
                res.maxStackSize = 999;
            }
            EnsureIcon(res);
            fallbackResources[id] = res;
        }

        private static void AddFallback(string id, string name, string desc, CraftingCategory category,
            string outputId, int outputCount, bool isEdible, float duration, int exp, (string id, int count)[] inputs,
            Color color)
        {
            CraftingRecipeData recipe = ScriptableObject.CreateInstance<CraftingRecipeData>();
            recipe.name = id;
            recipe.recipeID = id;
            recipe.recipeName = name;
            recipe.description = desc;
            recipe.category = category;
            recipe.outputAmount = outputCount;
            recipe.isEdible = isEdible;
            recipe.craftDurationSeconds = duration;
            recipe.feedingExpBonus = exp;

            ResourceData outRes = ResourceCatalog.Find(outputId);
            if (outRes == null)
            {
                outRes = ScriptableObject.CreateInstance<ResourceData>();
                outRes.resourceID = outputId;
                outRes.resourceName = name;
                outRes.description = desc;
                outRes.resourceColor = color;
                outRes.maxStackSize = 999;
            }
            EnsureIcon(outRes);
            recipe.outputResource = outRes;
            recipe.recipeIcon = outRes.icon;
            fallbackResources[outputId] = outRes;

            recipe.inputs = new List<ResourceStack>();
            foreach (var input in inputs)
            {
                recipe.inputs.Add(new ResourceStack { resourceId = input.id, amount = input.count });
            }

            RegisterRecipe(recipe);
        }
    }
}
