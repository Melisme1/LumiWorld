#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using LumiWorld.Economy.Crafting;

namespace LumiWorld.Economy.Crafting.Editor
{
    /// <summary>
    /// Công cụ Editor tự động sinh toàn bộ ScriptableObject cho:
    /// - 2 Tài nguyên thô mới (Lúa mì, Vỏ cây rụng).
    /// - 15 Tài nguyên thành phẩm chế biến.
    /// - 15 Cấu hình công thức chế biến (CraftingRecipeData).
    /// </summary>
    public static class CraftingAssetSetup
    {
        private const string ResourceFolder = "Assets/Resources/Crafting/Items";
        private const string RecipeFolder = "Assets/Resources/Crafting/Recipes";

        [MenuItem("Tools/LumiWorld/Generate Crafting Recipes & Resources")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(ResourceFolder);
            Directory.CreateDirectory(RecipeFolder);

            // 1. Tạo 2 tài nguyên thô bổ sung
            var resFallenBark = GetOrCreateResource("resource.leafwood.fallen_bark", "Vỏ cây rụng",
                "Vỏ cây sồi già rơi rụng trong rừng Leafwood, chứa nhiều thớ sợi dai dùng làm giấy.",
                new Color(0.48f, 0.32f, 0.20f, 1f));

            var resWheat = GetOrCreateResource("resource.plain.wheat", "Lúa mì",
                "Bông lúa mì vàng óng thu hoạch từ các đồng cỏ, hạt mẩy giàu tinh bột.",
                new Color(0.92f, 0.78f, 0.30f, 1f));

            // 2. Tạo 15 tài nguyên thành phẩm
            var resCrispyLeaves = GetOrCreateResource("resource.leafwood.tender_leaves_crispy", "Lá non rang giòn",
                "Lá non rang giòn rụm thơm lừng, là món ăn vặt ưa thích của sóc bay và muông thú.",
                new Color(0.35f, 0.75f, 0.30f, 1f), 15);

            var resDriedDandelion = GetOrCreateResource("resource.bloomfield.dried_dandelion", "Hoa bồ công anh sấy khô",
                "Cánh hoa bồ công anh Bloomfield phơi khô, giữ trọn sắc vàng và hương phấn hoa.",
                new Color(0.95f, 0.85f, 0.20f, 1f), 12);

            var resRoastedAcorns = GetOrCreateResource("resource.leafwood.roasted_acorns", "Hạt sồi rang",
                "Hạt sồi sấy rang bùi béo, cung cấp dồi dào năng lượng cho sinh vật hệ Mộc.",
                new Color(0.70f, 0.45f, 0.22f, 1f), 18);

            var resDriedRaspberries = GetOrCreateResource("resource.leafwood.dried_raspberries", "Mâm xôi sấy khô",
                "Mâm xôi ngọt chua thanh mát được cô đặc dinh dưỡng qua lò sấy gió.",
                new Color(0.85f, 0.22f, 0.35f, 1f), 20);

            var resDriedBlueberries = GetOrCreateResource("resource.windheath.dried_mountain_blueberries", "Việt quất sấy khô",
                "Việt quất núi Windheath sấy khô dẻo thơm, kích thích vị giác của các loài thú bay.",
                new Color(0.30f, 0.35f, 0.80f, 1f), 20);

            var resWildBerriesMix = GetOrCreateResource("resource.food.dried_wild_berries_mix", "Hỗn hợp trái cây rừng sấy khô",
                "Sự hòa quyện tuyệt hảo giữa mâm xôi ngọt lịm và việt quất chua thanh.",
                new Color(0.60f, 0.25f, 0.65f, 1f), 45);

            var resNectarEssence = GetOrCreateResource("resource.bloomfield.nectar_essence", "Tinh chất mật hoa",
                "Mật hoa Bloomfield được chưng cất thành giọt tinh chất đậm đặc và óng ả.",
                new Color(1.00f, 0.70f, 0.10f, 1f), 25);

            var resHoneyGlazedAcorns = GetOrCreateResource("resource.leafwood.honey_glazed_acorns", "Quả sồi ngào mật",
                "Quả sồi bùi béo phủ lớp tinh chất mật hoa óng ánh, giòn ngọt khó cưỡng.",
                new Color(0.85f, 0.55f, 0.15f, 1f), 55);

            var resCake = GetOrCreateResource("resource.food.wild_berry_nut_cake", "Bánh hạt quả rừng",
                "Tuyệt phẩm ẩm thực kết tinh từ cả 3 sinh cảnh: quả sồi ngào mật, hỗn hợp trái cây sấy và bột mì nướng vàng ươm.",
                new Color(0.90f, 0.65f, 0.30f, 1f), 160);

            var resWheatFlour = GetOrCreateResource("resource.plain.wheat_flour", "Bột mì",
                "Lúa mì nghiền mịn tơi xốp, nguyên liệu căn bản của mọi món bánh nướng.",
                new Color(0.95f, 0.92f, 0.80f, 1f), 15);

            var resFlowerPowder = GetOrCreateResource("resource.bloomfield.herbal_flower_powder", "Bột hoa thảo mộc",
                "Hỗn hợp cúc dại và bồ công anh nghiền nhuyễn, có mùi thơm dịu nhẹ giúp xoa dịu tinh thần thú hoang.",
                new Color(0.85f, 0.80f, 0.40f, 1f), 30);

            var resForestSyrup = GetOrCreateResource("resource.forest.wild_herbal_syrup", "Siro thảo mộc rừng",
                "Pha chế từ tinh chất mật hoa, hương thảo và quả bách xù, giúp tăng cường sinh lực.",
                new Color(0.40f, 0.65f, 0.45f, 1f), 70);

            var resBarkPaper = GetOrCreateResource("resource.leafwood.bark_paper", "Giấy vỏ cây",
                "Giấy thô mộc làm từ vỏ cây rụng và gỗ sồi, dùng làm giấy viết thư tín và sổ tay sinh cảnh.",
                new Color(0.80f, 0.72f, 0.58f, 1f), 35);

            var resPressedPaper = GetOrCreateResource("resource.craft.pressed_flower_paper", "Giấy hoa ép",
                "Tờ giấy vỏ cây mộc mạc được ép thêm cánh hoa cúc tươi tắn đầy nghệ thuật.",
                new Color(0.90f, 0.85f, 0.75f, 1f), 65);

            var resScentedPaper = GetOrCreateResource("resource.craft.scented_paper", "Giấy thơm",
                "Giấy vỏ cây ướp hương hoa oải hương thanh khiết, lưu giữ hương thơm bền lâu.",
                new Color(0.82f, 0.75f, 0.88f, 1f), 65);

            // 3. Tạo 15 Recipe ScriptableObjects
            // Cấp 1 - Food
            CreateOrUpdateRecipe("recipe.food.crispy_tender_leaves", "Lá non rang giòn",
                "Lá non rang giòn rụm thơm lừng, là món ăn vặt ưa thích của sóc bay và muông thú.",
                CraftingCategory.Food, resCrispyLeaves, 1, true, 5f, 30,
                new (string, int)[] { ("resource.leafwood.tender_leaves", 1) });

            CreateOrUpdateRecipe("recipe.food.roasted_acorns", "Hạt sồi rang",
                "Hạt sồi sấy rang bùi béo, cung cấp dồi dào năng lượng cho sinh vật hệ Mộc.",
                CraftingCategory.Food, resRoastedAcorns, 1, true, 5f, 35,
                new (string, int)[] { ("resource.leafwood.acorns", 1) });

            CreateOrUpdateRecipe("recipe.food.dried_raspberries", "Mâm xôi sấy khô",
                "Mâm xôi ngọt chua thanh mát được cô đặc dinh dưỡng qua lò sấy gió.",
                CraftingCategory.Food, resDriedRaspberries, 1, true, 5f, 35,
                new (string, int)[] { ("resource.leafwood.raspberries", 1) });

            CreateOrUpdateRecipe("recipe.food.dried_blueberries", "Việt quất sấy khô",
                "Việt quất núi Windheath sấy khô dẻo thơm, kích thích vị giác của các loài thú bay.",
                CraftingCategory.Food, resDriedBlueberries, 1, true, 5f, 35,
                new (string, int)[] { ("resource.windheath.mountain_blueberries", 1) });

            // Cấp 1 - Refined
            CreateOrUpdateRecipe("recipe.refined.dried_dandelion", "Hoa bồ công anh sấy khô",
                "Cánh hoa bồ công anh Bloomfield phơi khô, giữ trọn sắc vàng và hương phấn hoa.",
                CraftingCategory.Refined, resDriedDandelion, 1, false, 5f, 0,
                new (string, int)[] { ("resource.bloomfield.dandelion_flowers", 1) });

            CreateOrUpdateRecipe("recipe.refined.nectar_essence", "Tinh chất mật hoa",
                "Mật hoa Bloomfield được chưng cất thành giọt tinh chất đậm đặc và óng ả.",
                CraftingCategory.Refined, resNectarEssence, 1, false, 8f, 0,
                new (string, int)[] { ("resource.bloomfield.nectar", 1) });

            CreateOrUpdateRecipe("recipe.refined.wheat_flour", "Bột mì",
                "Lúa mì nghiền mịn tơi xốp, nguyên liệu căn bản của mọi món bánh nướng.",
                CraftingCategory.Refined, resWheatFlour, 1, false, 6f, 0,
                new (string, int)[] { ("resource.plain.wheat", 1) });

            // Cấp 1 - Herbal & Paper
            CreateOrUpdateRecipe("recipe.herbal.flower_powder", "Bột hoa thảo mộc",
                "Hỗn hợp cúc dại và bồ công anh nghiền nhuyễn, có mùi thơm dịu nhẹ giúp xoa dịu tinh thần thú hoang.",
                CraftingCategory.Herbal, resFlowerPowder, 1, false, 8f, 0,
                new (string, int)[] { ("resource.bloomfield.wild_daisies", 1), ("resource.bloomfield.dandelion_flowers", 1) });

            CreateOrUpdateRecipe("recipe.paper.bark_paper", "Giấy vỏ cây",
                "Giấy thô mộc làm từ vỏ cây rụng và gỗ sồi, dùng làm giấy viết thư tín và sổ tay sinh cảnh.",
                CraftingCategory.Paper, resBarkPaper, 1, false, 10f, 0,
                new (string, int)[] { ("resource.leafwood.fallen_bark", 1), ("resource.leafwood.oak_wood", 1) });

            // Cấp 2 - Food
            CreateOrUpdateRecipe("recipe.food.dried_wild_fruit_mix", "Hỗn hợp trái cây rừng sấy khô",
                "Sự hòa quyện tuyệt hảo giữa mâm xôi ngọt lịm và việt quất chua thanh.",
                CraftingCategory.Food, resWildBerriesMix, 1, true, 10f, 80,
                new (string, int)[] { ("resource.leafwood.dried_raspberries", 1), ("resource.windheath.dried_mountain_blueberries", 1) });

            CreateOrUpdateRecipe("recipe.food.honey_glazed_acorns", "Quả sồi ngào mật",
                "Quả sồi bùi béo phủ lớp tinh chất mật hoa óng ánh, giòn ngọt khó cưỡng.",
                CraftingCategory.Food, resHoneyGlazedAcorns, 1, true, 12f, 90,
                new (string, int)[] { ("resource.leafwood.acorns", 1), ("resource.bloomfield.nectar_essence", 1) });

            // Cấp 2 - Herbal & Paper
            CreateOrUpdateRecipe("recipe.herbal.forest_syrup", "Siro thảo mộc rừng",
                "Pha chế từ tinh chất mật hoa, hương thảo và quả bách xù, giúp tăng cường sinh lực.",
                CraftingCategory.Herbal, resForestSyrup, 1, false, 12f, 0,
                new (string, int)[] { ("resource.bloomfield.nectar_essence", 1), ("resource.windheath.rosemary", 1), ("resource.windheath.juniper_berries", 1) });

            CreateOrUpdateRecipe("recipe.paper.pressed_flower_paper", "Giấy hoa ép",
                "Tờ giấy vỏ cây mộc mạc được ép thêm cánh hoa cúc tươi tắn đầy nghệ thuật.",
                CraftingCategory.Paper, resPressedPaper, 1, false, 12f, 0,
                new (string, int)[] { ("resource.leafwood.bark_paper", 1), ("resource.bloomfield.wild_daisies", 1) });

            CreateOrUpdateRecipe("recipe.paper.scented_paper", "Giấy thơm",
                "Giấy vỏ cây ướp hương hoa oải hương thanh khiết, lưu giữ hương thơm bền lâu.",
                CraftingCategory.Paper, resScentedPaper, 1, false, 12f, 0,
                new (string, int)[] { ("resource.leafwood.bark_paper", 1), ("resource.bloomfield.lavender_sprigs", 1) });

            // Cấp 3 - Master Food
            CreateOrUpdateRecipe("recipe.food.wild_berry_nut_cake", "Bánh hạt quả rừng",
                "Tuyệt phẩm ẩm thực kết tinh từ cả 3 sinh cảnh: quả sồi ngào mật, hỗn hợp trái cây sấy và bột mì nướng vàng ươm.",
                CraftingCategory.Food, resCake, 1, true, 20f, 250,
                new (string, int)[] {
                    ("resource.leafwood.honey_glazed_acorns", 1),
                    ("resource.food.dried_wild_berries_mix", 1),
                    ("resource.plain.wheat_flour", 1)
                });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            CraftingCatalog.ResetCache();
            ResourceCatalog.ResetCache();

            Debug.Log("[LumiWorld] Đã tạo/cập nhật thành công toàn bộ tài nguyên & công thức chế biến trong Assets/Resources/Crafting!");
            EditorUtility.DisplayDialog("LumiWorld", "Đã tạo thành công 17 ResourceData và 15 CraftingRecipeData!", "OK");
        }

        private static ResourceData GetOrCreateResource(string resourceId, string name, string desc, Color color, int baseVal = 10)
        {
            string fileName = name.Replace(" ", "") + ".asset";
            string path = Path.Combine(ResourceFolder, fileName);

            ResourceData res = AssetDatabase.LoadAssetAtPath<ResourceData>(path);
            if (res == null)
            {
                res = ScriptableObject.CreateInstance<ResourceData>();
                AssetDatabase.CreateAsset(res, path);
            }

            res.resourceID = resourceId;
            res.resourceName = name;
            res.description = desc;
            res.resourceColor = color;
            res.hexColor = "#" + ColorUtility.ToHtmlStringRGB(color);
            res.baseValue = baseVal;
            res.maxStackSize = 999;

            EditorUtility.SetDirty(res);
            return res;
        }

        private static CraftingRecipeData CreateOrUpdateRecipe(string recipeId, string name, string desc,
            CraftingCategory category, ResourceData output, int outputCount, bool isEdible, float duration, int exp,
            (string id, int count)[] inputs)
        {
            string fileName = name.Replace(" ", "") + "Recipe.asset";
            string path = Path.Combine(RecipeFolder, fileName);

            CraftingRecipeData recipe = AssetDatabase.LoadAssetAtPath<CraftingRecipeData>(path);
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<CraftingRecipeData>();
                AssetDatabase.CreateAsset(recipe, path);
            }

            recipe.recipeID = recipeId;
            recipe.recipeName = name;
            recipe.description = desc;
            recipe.category = category;
            recipe.outputResource = output;
            recipe.outputAmount = outputCount;
            recipe.isEdible = isEdible;
            recipe.craftDurationSeconds = duration;
            recipe.feedingExpBonus = exp;
            recipe.description = desc;

            recipe.inputs = new List<ResourceStack>();
            foreach (var input in inputs)
            {
                recipe.inputs.Add(new ResourceStack { resourceId = input.id, amount = input.count });
            }

            EditorUtility.SetDirty(recipe);
            return recipe;
        }
    }
}
#endif
