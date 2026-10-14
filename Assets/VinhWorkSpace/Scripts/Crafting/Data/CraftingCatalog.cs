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
            { "resource.leafwood.tender_leaves", "Lá non" },
            { "resource.leafwood.acorns", "Quả sồi" },
            { "resource.leafwood.raspberries", "Mâm xôi" },
            { "resource.leafwood.oak_wood", "Gỗ sồi" },
            { "resource.leafwood.sticks", "Cành cây" },
            { "resource.leafwood.fallen_bark", "Vỏ cây rụng" },
            { "resource.bloomfield.dandelion_flowers", "Hoa bồ công anh" },
            { "resource.bloomfield.wild_daisies", "Hoa cúc dại" },
            { "resource.bloomfield.lavender_sprigs", "Nhánh oải hương" },
            { "resource.bloomfield.nectar", "Mật hoa" },
            { "resource.windheath.mountain_blueberries", "Việt quất núi" },
            { "resource.windheath.rosemary", "Hương thảo" },
            { "resource.windheath.juniper_berries", "Quả bách xù" },
            { "resource.windheath.foxtail_plumes", "Bông cỏ" },
            { "resource.plain.wheat", "Lúa mì" },
            { "resource.windheath.flax_stalks", "Cây lanh" },
            { "resource.windheath.lichen", "Địa y" }
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
            return res != null && !string.IsNullOrEmpty(res.resourceName) ? res.resourceName : resourceId;
        }

        /// <summary>
        /// Bản ghi mở rộng cho Kho tài nguyên (Kho Acs): gồm 2 nguyên liệu ngoài sinh cảnh và 15 thành phẩm chế tạo.
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
        /// Cung cấp danh sách các mục mở rộng (Lúa mì, Vỏ cây rụng và 15 món chế biến) để hiển thị trong Kho tài nguyên (phím I).
        /// </summary>
        public static List<WarehouseExtraItem> GetWarehouseExtraItems()
        {
            var list = new List<WarehouseExtraItem>();

            // 1. Hai nguyên liệu thô ngoài sinh cảnh (Lúa mì & Vỏ cây rụng)
            var fallenBark = GetResource("resource.leafwood.fallen_bark");
            list.Add(new WarehouseExtraItem
            {
                id = "resource.leafwood.fallen_bark",
                name = fallenBark != null ? fallenBark.resourceName : "Vỏ cây rụng",
                biome = "Leafwood",
                tier = "Thô",
                description = fallenBark != null ? fallenBark.description : "Vỏ cây sồi già rơi rụng trong rừng Leafwood, chứa nhiều thớ sợi dai dùng làm giấy.",
                icon = fallenBark?.icon,
                capacity = fallenBark != null && fallenBark.maxStackSize > 0 ? fallenBark.maxStackSize : 999
            });

            var wheat = GetResource("resource.plain.wheat");
            list.Add(new WarehouseExtraItem
            {
                id = "resource.plain.wheat",
                name = wheat != null ? wheat.resourceName : "Lúa mì",
                biome = "Đồng cỏ",
                tier = "Thô",
                description = wheat != null ? wheat.description : "Bông lúa mì vàng óng thu hoạch từ các đồng cỏ, hạt mẩy giàu tinh bột.",
                icon = wheat?.icon,
                capacity = wheat != null && wheat.maxStackSize > 0 ? wheat.maxStackSize : 999
            });

            // 2. Toàn bộ 15 thành phẩm chế tạo (nhóm Biome hiển thị là "Chế biến")
            foreach (var recipe in All)
            {
                if (recipe == null || recipe.outputResource == null) continue;
                var res = recipe.outputResource;
                EnsureIcon(res);
                string tierName;
                switch (recipe.category)
                {
                    case CraftingCategory.Food: tierName = "Thức ăn"; break;
                    case CraftingCategory.Refined: tierName = "Tinh chế"; break;
                    case CraftingCategory.Herbal: tierName = "Dược liệu"; break;
                    case CraftingCategory.Paper: tierName = "Thủ công"; break;
                    default: tierName = "Chế biến"; break;
                }

                list.Add(new WarehouseExtraItem
                {
                    id = res.resourceID,
                    name = res.resourceName,
                    biome = "Chế biến",
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
            // Tài nguyên thô bổ sung chưa có file asset
            AddRawFallback("resource.leafwood.fallen_bark", "Vỏ cây rụng",
                "Vỏ cây sồi già rơi rụng trong rừng Leafwood, chứa nhiều thớ sợi dai dùng làm giấy.",
                new Color(0.48f, 0.32f, 0.20f, 1f));

            AddRawFallback("resource.plain.wheat", "Lúa mì",
                "Bông lúa mì vàng óng thu hoạch từ các đồng cỏ, hạt mẩy giàu tinh bột.",
                new Color(0.92f, 0.78f, 0.30f, 1f));

            // Cấp 1 - Food
            AddFallback("recipe.food.crispy_tender_leaves", "Lá non rang giòn",
                "Lá non rang giòn rụm thơm lừng, là món ăn vặt ưa thích của sóc bay và muông thú.",
                CraftingCategory.Food, "resource.leafwood.tender_leaves_crispy", 1, true, 5f, 30,
                new (string, int)[] { ("resource.leafwood.tender_leaves", 3) },
                new Color(0.35f, 0.75f, 0.30f, 1f));

            AddFallback("recipe.food.roasted_acorns", "Hạt sồi rang",
                "Hạt sồi sấy rang bùi béo, cung cấp dồi dào năng lượng cho sinh vật hệ Mộc.",
                CraftingCategory.Food, "resource.leafwood.roasted_acorns", 1, true, 5f, 35,
                new (string, int)[] { ("resource.leafwood.acorns", 2) },
                new Color(0.70f, 0.45f, 0.22f, 1f));

            AddFallback("recipe.food.dried_raspberries", "Mâm xôi sấy khô",
                "Mâm xôi ngọt chua thanh mát được cô đặc dinh dưỡng qua lò sấy gió.",
                CraftingCategory.Food, "resource.leafwood.dried_raspberries", 1, true, 5f, 35,
                new (string, int)[] { ("resource.leafwood.raspberries", 2) },
                new Color(0.85f, 0.22f, 0.35f, 1f));

            AddFallback("recipe.food.dried_blueberries", "Việt quất sấy khô",
                "Việt quất núi Windheath sấy khô dẻo thơm, kích thích vị giác của các loài thú bay.",
                CraftingCategory.Food, "resource.windheath.dried_mountain_blueberries", 1, true, 5f, 35,
                new (string, int)[] { ("resource.windheath.mountain_blueberries", 2) },
                new Color(0.30f, 0.35f, 0.80f, 1f));

            // Cấp 1 - Refined
            AddFallback("recipe.refined.dried_dandelion", "Hoa bồ công anh sấy khô",
                "Cánh hoa bồ công anh Bloomfield phơi khô, giữ trọn sắc vàng và hương phấn hoa.",
                CraftingCategory.Refined, "resource.bloomfield.dried_dandelion", 1, false, 5f, 0,
                new (string, int)[] { ("resource.bloomfield.dandelion_flowers", 2) },
                new Color(0.95f, 0.85f, 0.20f, 1f));

            AddFallback("recipe.refined.nectar_essence", "Tinh chất mật hoa",
                "Mật hoa Bloomfield được chưng cất thành giọt tinh chất đậm đặc và óng ả.",
                CraftingCategory.Refined, "resource.bloomfield.nectar_essence", 1, false, 8f, 0,
                new (string, int)[] { ("resource.bloomfield.nectar", 2) },
                new Color(1.00f, 0.70f, 0.10f, 1f));

            AddFallback("recipe.refined.wheat_flour", "Bột mì",
                "Lúa mì nghiền mịn tơi xốp, nguyên liệu căn bản của mọi món bánh nướng.",
                CraftingCategory.Refined, "resource.plain.wheat_flour", 1, false, 6f, 0,
                new (string, int)[] { ("resource.plain.wheat", 3) },
                new Color(0.95f, 0.92f, 0.80f, 1f));

            // Cấp 1 - Herbal & Paper
            AddFallback("recipe.herbal.flower_powder", "Bột hoa thảo mộc",
                "Hỗn hợp cúc dại và bồ công anh nghiền nhuyễn, có mùi thơm dịu nhẹ giúp xoa dịu tinh thần thú hoang.",
                CraftingCategory.Herbal, "resource.bloomfield.herbal_flower_powder", 1, false, 8f, 0,
                new (string, int)[] { ("resource.bloomfield.wild_daisies", 2), ("resource.bloomfield.dandelion_flowers", 2) },
                new Color(0.85f, 0.80f, 0.40f, 1f));

            AddFallback("recipe.paper.bark_paper", "Giấy vỏ cây",
                "Giấy thô mộc làm từ vỏ cây rụng và gỗ sồi, dùng làm giấy viết thư tín và sổ tay sinh cảnh.",
                CraftingCategory.Paper, "resource.leafwood.bark_paper", 1, false, 10f, 0,
                new (string, int)[] { ("resource.leafwood.fallen_bark", 2), ("resource.leafwood.oak_wood", 1) },
                new Color(0.80f, 0.72f, 0.58f, 1f));

            // Cấp 2 - Food
            AddFallback("recipe.food.dried_wild_fruit_mix", "Hỗn hợp trái cây rừng sấy khô",
                "Sự hòa quyện tuyệt hảo giữa mâm xôi ngọt lịm và việt quất chua thanh.",
                CraftingCategory.Food, "resource.food.dried_wild_berries_mix", 1, true, 10f, 80,
                new (string, int)[] { ("resource.leafwood.dried_raspberries", 1), ("resource.windheath.dried_mountain_blueberries", 1) },
                new Color(0.60f, 0.25f, 0.65f, 1f));

            AddFallback("recipe.food.honey_glazed_acorns", "Quả sồi ngào mật",
                "Quả sồi bùi béo phủ lớp tinh chất mật hoa óng ánh, giòn ngọt khó cưỡng.",
                CraftingCategory.Food, "resource.leafwood.honey_glazed_acorns", 1, true, 12f, 90,
                new (string, int)[] { ("resource.leafwood.acorns", 2), ("resource.bloomfield.nectar_essence", 1) },
                new Color(0.85f, 0.55f, 0.15f, 1f));

            // Cấp 2 - Herbal & Paper
            AddFallback("recipe.herbal.forest_syrup", "Siro thảo mộc rừng",
                "Pha chế từ tinh chất mật hoa, hương thảo và quả bách xù, giúp tăng cường sinh lực.",
                CraftingCategory.Herbal, "resource.forest.wild_herbal_syrup", 1, false, 12f, 0,
                new (string, int)[] { ("resource.bloomfield.nectar_essence", 1), ("resource.windheath.rosemary", 2), ("resource.windheath.juniper_berries", 2) },
                new Color(0.40f, 0.65f, 0.45f, 1f));

            AddFallback("recipe.paper.pressed_flower_paper", "Giấy hoa ép",
                "Tờ giấy vỏ cây mộc mạc được ép thêm cánh hoa cúc tươi tắn đầy nghệ thuật.",
                CraftingCategory.Paper, "resource.craft.pressed_flower_paper", 1, false, 12f, 0,
                new (string, int)[] { ("resource.leafwood.bark_paper", 1), ("resource.bloomfield.wild_daisies", 2) },
                new Color(0.90f, 0.85f, 0.75f, 1f));

            AddFallback("recipe.paper.scented_paper", "Giấy thơm",
                "Giấy vỏ cây ướp hương hoa oải hương thanh khiết, lưu giữ hương thơm bền lâu.",
                CraftingCategory.Paper, "resource.craft.scented_paper", 1, false, 12f, 0,
                new (string, int)[] { ("resource.leafwood.bark_paper", 1), ("resource.bloomfield.lavender_sprigs", 2) },
                new Color(0.82f, 0.75f, 0.88f, 1f));

            // Cấp 3 - Master Food (Bánh hạt quả rừng)
            AddFallback("recipe.food.wild_berry_nut_cake", "Bánh hạt quả rừng",
                "Tuyệt phẩm ẩm thực kết tinh từ cả 3 sinh cảnh: quả sồi ngào mật, hỗn hợp trái cây sấy và bột mì nướng vàng ươm.",
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
