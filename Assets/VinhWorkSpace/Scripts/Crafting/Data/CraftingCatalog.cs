using System;
using System.Collections.Generic;
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
        /// Tìm ResourceData của một tài nguyên: ưu tiên ResourceCatalog trên đĩa,
        /// nếu chưa có asset thì dùng bản fallback runtime từ công thức chế biến.
        /// </summary>
        public static ResourceData GetResource(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId)) return null;

            // 1. Kiểm tra ResourceCatalog nếu đã có asset trên đĩa
            ResourceData res = ResourceCatalog.Find(resourceId);
            if (res != null) return res;

            // 2. Nạp catalog nếu chưa nạp
            if (all == null) Load();

            // 3. Tra cứu từ fallbackResources
            if (fallbackResources.TryGetValue(resourceId, out ResourceData fallback))
            {
                return fallback;
            }

            // 4. Tra cứu theo output của recipe
            if (byOutputId.TryGetValue(resourceId, out CraftingRecipeData recipe) && recipe.outputResource != null)
            {
                return recipe.outputResource;
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
                new (string, int)[] { ("resource.leafwood.tender_leaves", 1) },
                new Color(0.35f, 0.75f, 0.30f, 1f));

            AddFallback("recipe.food.roasted_acorns", "Hạt sồi rang",
                "Hạt sồi sấy rang bùi béo, cung cấp dồi dào năng lượng cho sinh vật hệ Mộc.",
                CraftingCategory.Food, "resource.leafwood.roasted_acorns", 1, true, 5f, 35,
                new (string, int)[] { ("resource.leafwood.acorns", 1) },
                new Color(0.70f, 0.45f, 0.22f, 1f));

            AddFallback("recipe.food.dried_raspberries", "Mâm xôi sấy khô",
                "Mâm xôi ngọt chua thanh mát được cô đặc dinh dưỡng qua lò sấy gió.",
                CraftingCategory.Food, "resource.leafwood.dried_raspberries", 1, true, 5f, 35,
                new (string, int)[] { ("resource.leafwood.raspberries", 1) },
                new Color(0.85f, 0.22f, 0.35f, 1f));

            AddFallback("recipe.food.dried_blueberries", "Việt quất sấy khô",
                "Việt quất núi Windheath sấy khô dẻo thơm, kích thích vị giác của các loài thú bay.",
                CraftingCategory.Food, "resource.windheath.dried_mountain_blueberries", 1, true, 5f, 35,
                new (string, int)[] { ("resource.windheath.mountain_blueberries", 1) },
                new Color(0.30f, 0.35f, 0.80f, 1f));

            // Cấp 1 - Refined
            AddFallback("recipe.refined.dried_dandelion", "Hoa bồ công anh sấy khô",
                "Cánh hoa bồ công anh Bloomfield phơi khô, giữ trọn sắc vàng và hương phấn hoa.",
                CraftingCategory.Refined, "resource.bloomfield.dried_dandelion", 1, false, 5f, 0,
                new (string, int)[] { ("resource.bloomfield.dandelion_flowers", 1) },
                new Color(0.95f, 0.85f, 0.20f, 1f));

            AddFallback("recipe.refined.nectar_essence", "Tinh chất mật hoa",
                "Mật hoa Bloomfield được chưng cất thành giọt tinh chất đậm đặc và óng ả.",
                CraftingCategory.Refined, "resource.bloomfield.nectar_essence", 1, false, 8f, 0,
                new (string, int)[] { ("resource.bloomfield.nectar", 1) },
                new Color(1.00f, 0.70f, 0.10f, 1f));

            AddFallback("recipe.refined.wheat_flour", "Bột mì",
                "Lúa mì nghiền mịn tơi xốp, nguyên liệu căn bản của mọi món bánh nướng.",
                CraftingCategory.Refined, "resource.plain.wheat_flour", 1, false, 6f, 0,
                new (string, int)[] { ("resource.plain.wheat", 1) },
                new Color(0.95f, 0.92f, 0.80f, 1f));

            // Cấp 1 - Herbal & Paper
            AddFallback("recipe.herbal.flower_powder", "Bột hoa thảo mộc",
                "Hỗn hợp cúc dại và bồ công anh nghiền nhuyễn, có mùi thơm dịu nhẹ giúp xoa dịu tinh thần thú hoang.",
                CraftingCategory.Herbal, "resource.bloomfield.herbal_flower_powder", 1, false, 8f, 0,
                new (string, int)[] { ("resource.bloomfield.wild_daisies", 1), ("resource.bloomfield.dandelion_flowers", 1) },
                new Color(0.85f, 0.80f, 0.40f, 1f));

            AddFallback("recipe.paper.bark_paper", "Giấy vỏ cây",
                "Giấy thô mộc làm từ vỏ cây rụng và gỗ sồi, dùng làm giấy viết thư tín và sổ tay sinh cảnh.",
                CraftingCategory.Paper, "resource.leafwood.bark_paper", 1, false, 10f, 0,
                new (string, int)[] { ("resource.leafwood.fallen_bark", 1), ("resource.leafwood.oak_wood", 1) },
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
                new (string, int)[] { ("resource.leafwood.acorns", 1), ("resource.bloomfield.nectar_essence", 1) },
                new Color(0.85f, 0.55f, 0.15f, 1f));

            // Cấp 2 - Herbal & Paper
            AddFallback("recipe.herbal.forest_syrup", "Siro thảo mộc rừng",
                "Pha chế từ tinh chất mật hoa, hương thảo và quả bách xù, giúp tăng cường sinh lực.",
                CraftingCategory.Herbal, "resource.forest.wild_herbal_syrup", 1, false, 12f, 0,
                new (string, int)[] { ("resource.bloomfield.nectar_essence", 1), ("resource.windheath.rosemary", 1), ("resource.windheath.juniper_berries", 1) },
                new Color(0.40f, 0.65f, 0.45f, 1f));

            AddFallback("recipe.paper.pressed_flower_paper", "Giấy hoa ép",
                "Tờ giấy vỏ cây mộc mạc được ép thêm cánh hoa cúc tươi tắn đầy nghệ thuật.",
                CraftingCategory.Paper, "resource.craft.pressed_flower_paper", 1, false, 12f, 0,
                new (string, int)[] { ("resource.leafwood.bark_paper", 1), ("resource.bloomfield.wild_daisies", 1) },
                new Color(0.90f, 0.85f, 0.75f, 1f));

            AddFallback("recipe.paper.scented_paper", "Giấy thơm",
                "Giấy vỏ cây ướp hương hoa oải hương thanh khiết, lưu giữ hương thơm bền lâu.",
                CraftingCategory.Paper, "resource.craft.scented_paper", 1, false, 12f, 0,
                new (string, int)[] { ("resource.leafwood.bark_paper", 1), ("resource.bloomfield.lavender_sprigs", 1) },
                new Color(0.82f, 0.75f, 0.88f, 1f));

            // Cấp 3 - Master Food (Bánh hạt quả rừng)
            AddFallback("recipe.food.wild_berry_nut_cake", "Bánh hạt quả rừng",
                "Tuyệt phẩm ẩm thực kết tinh từ cả 3 sinh cảnh: quả sồi ngào mật, hỗn hợp trái cây sấy và bột mì nướng vàng ươm.",
                CraftingCategory.Food, "resource.food.wild_berry_nut_cake", 1, true, 20f, 250,
                new (string, int)[] {
                    ("resource.leafwood.honey_glazed_acorns", 1),
                    ("resource.food.dried_wild_berries_mix", 1),
                    ("resource.plain.wheat_flour", 1)
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
            recipe.outputResource = outRes;
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
