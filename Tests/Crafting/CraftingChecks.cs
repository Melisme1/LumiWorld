using System;
using System.Collections.Generic;
using LumiWorld.Economy.Crafting;

internal static class CraftingChecks
{
    private static int checks, failed;

    private static void Assert(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        checks++;
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"{message}: expected {expected}, got {actual}");
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine("PASS " + name);
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine("FAIL " + name + ": " + ex.Message);
        }
    }

    public static int Main()
    {
        Run("All 15 standard recipes registered in CraftingCatalog", TestCatalogRegistration);
        Run("Category grouping matches expected counts", TestCategoryCounts);
        Run("Tier 1 direct raw material decomposition", TestTier1Decomposition);
        Run("Tier 2 intermediate multi-ingredient decomposition", TestTier2Decomposition);
        Run("Tier 3 WildBerryNutCake 5-raw-material tree decomposition", TestMasterCakeDecomposition);
        Run("Herbal and Paper multi-tier decomposition", TestHerbalAndPaperDecomposition);
        Run("Raw breakdown string formatting", TestFormatRawBreakdown);
        Run("Recipe and Resource icon resolution from Assets/Acs/Icons", TestRecipeAndResourceIcons);
        Run("Warehouse extra items provide 17 entries with custom biomes", TestWarehouseExtraItems);

        Console.WriteLine($"\n[Crafting Checks] {checks} assertions; {failed} failed groups.");
        return failed == 0 ? 0 : 1;
    }

    private static void TestRecipeAndResourceIcons()
    {
        CraftingCatalog.ResetCache();
        var all = CraftingCatalog.All;
        Equal(15, all.Count, "Total recipes for icon check");

        foreach (var recipe in all)
        {
            Assert(recipe.GetIcon() != null, $"Recipe '{recipe.recipeID}' must have a valid icon");
            Assert(recipe.outputResource != null, $"Recipe '{recipe.recipeID}' must have an output resource");
            Assert(recipe.outputResource.icon != null, $"Output resource '{recipe.outputResource.resourceID}' must have an icon");

            foreach (var input in recipe.inputs)
            {
                var res = CraftingCatalog.GetResource(input.resourceId);
                Assert(res != null, $"Ingredient resource '{input.resourceId}' must be resolvable");
                Assert(res.icon != null, $"Ingredient resource '{input.resourceId}' must have an icon");
            }
        }
    }

    private static void TestCatalogRegistration()
    {
        CraftingCatalog.ResetCache();
        var all = CraftingCatalog.All;
        Equal(15, all.Count, "Total recipe count");

        // Verify key recipes exist
        Assert(CraftingCatalog.Find("recipe.food.crispy_tender_leaves") != null, "Crispy leaves exists");
        Assert(CraftingCatalog.Find("recipe.food.wild_berry_nut_cake") != null, "Master cake exists");
        Assert(CraftingCatalog.Find("recipe.paper.bark_paper") != null, "Bark paper exists");
        Assert(CraftingCatalog.Find("recipe.refined.wheat_flour") != null, "Wheat flour exists");
    }

    private static void TestCategoryCounts()
    {
        CraftingCatalog.ResetCache();
        var food = CraftingCatalog.GetByCategory(CraftingCategory.Food);
        var refined = CraftingCatalog.GetByCategory(CraftingCategory.Refined);
        var herbal = CraftingCatalog.GetByCategory(CraftingCategory.Herbal);
        var paper = CraftingCatalog.GetByCategory(CraftingCategory.Paper);

        Equal(7, food.Count, "Food recipes count (edible)");
        Equal(3, refined.Count, "Refined intermediate count");
        Equal(2, herbal.Count, "Herbal recipes count");
        Equal(3, paper.Count, "Paper craft recipes count");

        // Check that all food recipes have isEdible = true
        foreach (var r in food)
        {
            Assert(r.isEdible, $"{r.recipeName} should be edible");
        }

        // Check that non-food are not edible
        foreach (var r in refined) Assert(!r.isEdible, $"{r.recipeName} should not be edible");
        foreach (var r in herbal) Assert(!r.isEdible, $"{r.recipeName} should not be edible");
        foreach (var r in paper) Assert(!r.isEdible, $"{r.recipeName} should not be edible");
    }

    private static void TestTier1Decomposition()
    {
        CraftingCatalog.ResetCache();
        var crispyLeaves = CraftingCatalog.Find("recipe.food.crispy_tender_leaves");
        var raw = CraftingCalculator.CalculateRawBreakdown(crispyLeaves);

        Equal(1, raw.Count, "Single raw ingredient");
        Assert(raw.ContainsKey("resource.leafwood.tender_leaves"), "Uses tender leaves");
        Equal(3, raw["resource.leafwood.tender_leaves"], "Needs 3 tender leaves");

        var flour = CraftingCatalog.Find("recipe.refined.wheat_flour");
        var flourRaw = CraftingCalculator.CalculateRawBreakdown(flour);
        Equal(1, flourRaw.Count, "Flour uses single raw ingredient");
        Assert(flourRaw.ContainsKey("resource.plain.wheat"), "Flour uses wheat");
        Equal(3, flourRaw["resource.plain.wheat"], "Needs 3 wheat");
    }

    private static void TestTier2Decomposition()
    {
        CraftingCatalog.ResetCache();

        // 1. Dried wild fruit mix = dried raspberries (2) + dried blueberries (2)
        var fruitMix = CraftingCatalog.Find("recipe.food.dried_wild_fruit_mix");
        var mixRaw = CraftingCalculator.CalculateRawBreakdown(fruitMix);
        Equal(2, mixRaw.Count, "Fruit mix decomposes into 2 raw ingredients");
        Assert(mixRaw.ContainsKey("resource.leafwood.raspberries"), "Needs raspberries");
        Equal(2, mixRaw["resource.leafwood.raspberries"], "2 raspberries");
        Assert(mixRaw.ContainsKey("resource.windheath.mountain_blueberries"), "Needs mountain blueberries");
        Equal(2, mixRaw["resource.windheath.mountain_blueberries"], "2 blueberries");

        // 2. Honey glazed acorns = 2 acorns + 1 nectar essence (from 2 nectar)
        var glazedAcorns = CraftingCatalog.Find("recipe.food.honey_glazed_acorns");
        var glazedRaw = CraftingCalculator.CalculateRawBreakdown(glazedAcorns);
        Equal(2, glazedRaw.Count, "Glazed acorns decomposes into 2 raw ingredients");
        Assert(glazedRaw.ContainsKey("resource.leafwood.acorns"), "Needs raw acorns");
        Equal(2, glazedRaw["resource.leafwood.acorns"], "2 acorns");
        Assert(glazedRaw.ContainsKey("resource.bloomfield.nectar"), "Needs raw nectar from essence");
        Equal(2, glazedRaw["resource.bloomfield.nectar"], "2 nectar");
    }

    private static void TestMasterCakeDecomposition()
    {
        CraftingCatalog.ResetCache();

        // Master Cake = HoneyGlazedAcorns (2 Acorn + 2 Nectar)
        //              + DriedWildFruitMix (2 Raspberries + 2 MountainBlueberries)
        //              + 2 WheatFlour (2 * 3 = 6 Wheat)
        // TOTAL = 5 distinct raw materials across 3 biomes + farm, total 14 items!
        var cake = CraftingCatalog.Find("recipe.food.wild_berry_nut_cake");
        var cakeRaw = CraftingCalculator.CalculateRawBreakdown(cake);

        Equal(5, cakeRaw.Count, "Cake decomposes into exactly 5 raw ingredients");
        Equal(2, cakeRaw["resource.leafwood.acorns"], "2 acorns");
        Equal(2, cakeRaw["resource.bloomfield.nectar"], "2 nectar");
        Equal(2, cakeRaw["resource.leafwood.raspberries"], "2 raspberries");
        Equal(2, cakeRaw["resource.windheath.mountain_blueberries"], "2 blueberries");
        Equal(6, cakeRaw["resource.plain.wheat"], "6 wheat");
    }

    private static void TestHerbalAndPaperDecomposition()
    {
        CraftingCatalog.ResetCache();

        // Herbal syrup = 1 nectar essence (2 nectar) + 2 rosemary + 2 juniper berries
        var syrup = CraftingCatalog.Find("recipe.herbal.forest_syrup");
        var syrupRaw = CraftingCalculator.CalculateRawBreakdown(syrup);
        Equal(3, syrupRaw.Count, "Syrup decomposes into 3 raw ingredients");
        Equal(2, syrupRaw["resource.bloomfield.nectar"], "2 nectar");
        Equal(2, syrupRaw["resource.windheath.rosemary"], "2 rosemary");
        Equal(2, syrupRaw["resource.windheath.juniper_berries"], "2 juniper");

        // Pressed flower paper = 1 bark paper (2 fallen bark + 1 oak wood) + 2 wild daisies
        var pressedPaper = CraftingCatalog.Find("recipe.paper.pressed_flower_paper");
        var paperRaw = CraftingCalculator.CalculateRawBreakdown(pressedPaper);
        Equal(3, paperRaw.Count, "Pressed paper decomposes into 3 raw ingredients");
        Equal(2, paperRaw["resource.leafwood.fallen_bark"], "2 fallen bark");
        Equal(1, paperRaw["resource.leafwood.oak_wood"], "1 oak wood");
        Equal(2, paperRaw["resource.bloomfield.wild_daisies"], "2 wild daisies");
    }

    private static void TestFormatRawBreakdown()
    {
        CraftingCatalog.ResetCache();
        var cake = CraftingCatalog.Find("recipe.food.wild_berry_nut_cake");
        string formatted = CraftingCalculator.FormatRawBreakdown(cake);
        Assert(!string.IsNullOrEmpty(formatted), "Formatted breakdown is not empty");
        Assert(formatted.Contains("6"), "Contains 6 wheat");
        Assert(formatted.Contains("2"), "Contains 2 acorns/nectar/berries");

        // Verify resource names fallback to Vietnamese
        Equal("Quả sồi ngào mật", CraftingCatalog.GetResourceName("resource.leafwood.honey_glazed_acorns"), "Honey glazed acorns name");
        Equal("Hỗn hợp trái cây rừng sấy khô", CraftingCatalog.GetResourceName("resource.food.dried_wild_berries_mix"), "Fruit mix name");
        Equal("Bột mì", CraftingCatalog.GetResourceName("resource.plain.wheat_flour"), "Wheat flour name");
        Equal("Lúa mì", CraftingCatalog.GetResourceName("resource.plain.wheat"), "Wheat raw name");
        Equal("Vỏ cây rụng", CraftingCatalog.GetResourceName("resource.leafwood.fallen_bark"), "Fallen bark name");

        // Raw breakdown contains Vietnamese 'Lúa mì' rather than technical id
        Assert(formatted.Contains("Lúa mì"), $"Formatted breakdown must contain 'Lúa mì', was: {formatted}");
    }

    private static void TestWarehouseExtraItems()
    {
        CraftingCatalog.ResetCache();
        var items = CraftingCatalog.GetWarehouseExtraItems();
        Equal(17, items.Count, "GetWarehouseExtraItems must have 17 items (2 raw + 15 products)");

        var wheat = items.Find(i => i.id == "resource.plain.wheat");
        Assert(wheat.id != null, "Contains wheat");
        Equal("Lúa mì", wheat.name, "Wheat name");
        Equal("Đồng cỏ", wheat.biome, "Wheat biome");
        Equal("Thô", wheat.tier, "Wheat tier");

        var bark = items.Find(i => i.id == "resource.leafwood.fallen_bark");
        Assert(bark.id != null, "Contains fallen bark");
        Equal("Vỏ cây rụng", bark.name, "Bark name");
        Equal("Leafwood", bark.biome, "Bark biome");
        Equal("Thô", bark.tier, "Bark tier");

        int productCount = 0;
        foreach (var item in items)
        {
            if (item.id != "resource.plain.wheat" && item.id != "resource.leafwood.fallen_bark")
            {
                Equal("Chế biến", item.biome, $"Product {item.id} biome must be 'Chế biến'");
                Assert(!string.IsNullOrEmpty(item.name), $"Product {item.id} name not empty");
                Assert(!string.IsNullOrEmpty(item.tier), $"Product {item.id} tier not empty");
                productCount++;
            }
        }
        Equal(15, productCount, "15 crafted products have biome 'Chế biến'");
    }
}
