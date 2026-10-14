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

        Console.WriteLine($"\n[Crafting Checks] {checks} assertions; {failed} failed groups.");
        return failed == 0 ? 0 : 1;
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
        Equal(1, raw["resource.leafwood.tender_leaves"], "Needs 1 tender leaves");

        var flour = CraftingCatalog.Find("recipe.refined.wheat_flour");
        var flourRaw = CraftingCalculator.CalculateRawBreakdown(flour);
        Equal(1, flourRaw.Count, "Flour uses single raw ingredient");
        Assert(flourRaw.ContainsKey("resource.plain.wheat"), "Flour uses wheat");
        Equal(1, flourRaw["resource.plain.wheat"], "Needs 1 wheat");
    }

    private static void TestTier2Decomposition()
    {
        CraftingCatalog.ResetCache();

        // 1. Dried wild fruit mix = dried raspberries + dried blueberries
        var fruitMix = CraftingCatalog.Find("recipe.food.dried_wild_fruit_mix");
        var mixRaw = CraftingCalculator.CalculateRawBreakdown(fruitMix);
        Equal(2, mixRaw.Count, "Fruit mix decomposes into 2 raw ingredients");
        Assert(mixRaw.ContainsKey("resource.leafwood.raspberries"), "Needs raspberries");
        Equal(1, mixRaw["resource.leafwood.raspberries"], "1 raspberries");
        Assert(mixRaw.ContainsKey("resource.windheath.mountain_blueberries"), "Needs mountain blueberries");
        Equal(1, mixRaw["resource.windheath.mountain_blueberries"], "1 blueberries");

        // 2. Honey glazed acorns = acorns + nectar essence (from nectar)
        var glazedAcorns = CraftingCatalog.Find("recipe.food.honey_glazed_acorns");
        var glazedRaw = CraftingCalculator.CalculateRawBreakdown(glazedAcorns);
        Equal(2, glazedRaw.Count, "Glazed acorns decomposes into 2 raw ingredients");
        Assert(glazedRaw.ContainsKey("resource.leafwood.acorns"), "Needs raw acorns");
        Equal(1, glazedRaw["resource.leafwood.acorns"], "1 acorns");
        Assert(glazedRaw.ContainsKey("resource.bloomfield.nectar"), "Needs raw nectar from essence");
        Equal(1, glazedRaw["resource.bloomfield.nectar"], "1 nectar");
    }

    private static void TestMasterCakeDecomposition()
    {
        CraftingCatalog.ResetCache();

        // Master Cake = HoneyGlazedAcorns (Acorn + Nectar)
        //              + DriedWildFruitMix (Raspberries + MountainBlueberries)
        //              + WheatFlour (Wheat)
        // TOTAL = 5 distinct raw materials across 3 biomes + farm!
        var cake = CraftingCatalog.Find("recipe.food.wild_berry_nut_cake");
        var cakeRaw = CraftingCalculator.CalculateRawBreakdown(cake);

        Equal(5, cakeRaw.Count, "Cake decomposes into exactly 5 raw ingredients");
        Equal(1, cakeRaw["resource.leafwood.acorns"], "1 acorns");
        Equal(1, cakeRaw["resource.bloomfield.nectar"], "1 nectar");
        Equal(1, cakeRaw["resource.leafwood.raspberries"], "1 raspberries");
        Equal(1, cakeRaw["resource.windheath.mountain_blueberries"], "1 blueberries");
        Equal(1, cakeRaw["resource.plain.wheat"], "1 wheat");
    }

    private static void TestHerbalAndPaperDecomposition()
    {
        CraftingCatalog.ResetCache();

        // Herbal syrup = nectar essence (nectar) + rosemary + juniper berries
        var syrup = CraftingCatalog.Find("recipe.herbal.forest_syrup");
        var syrupRaw = CraftingCalculator.CalculateRawBreakdown(syrup);
        Equal(3, syrupRaw.Count, "Syrup decomposes into 3 raw ingredients");
        Equal(1, syrupRaw["resource.bloomfield.nectar"], "1 nectar");
        Equal(1, syrupRaw["resource.windheath.rosemary"], "1 rosemary");
        Equal(1, syrupRaw["resource.windheath.juniper_berries"], "1 juniper");

        // Pressed flower paper = bark paper (fallen bark + oak wood) + wild daisies
        var pressedPaper = CraftingCatalog.Find("recipe.paper.pressed_flower_paper");
        var paperRaw = CraftingCalculator.CalculateRawBreakdown(pressedPaper);
        Equal(3, paperRaw.Count, "Pressed paper decomposes into 3 raw ingredients");
        Equal(1, paperRaw["resource.leafwood.fallen_bark"], "1 fallen bark");
        Equal(1, paperRaw["resource.leafwood.oak_wood"], "1 oak wood");
        Equal(1, paperRaw["resource.bloomfield.wild_daisies"], "1 wild daisies");
    }

    private static void TestFormatRawBreakdown()
    {
        CraftingCatalog.ResetCache();
        var cake = CraftingCatalog.Find("recipe.food.wild_berry_nut_cake");
        string formatted = CraftingCalculator.FormatRawBreakdown(cake);
        Assert(!string.IsNullOrEmpty(formatted), "Formatted breakdown is not empty");
        Assert(formatted.Contains("1"), "Contains quantities");

        // Verify resource names fallback to Vietnamese
        Equal("Quả sồi ngào mật", CraftingCatalog.GetResourceName("resource.leafwood.honey_glazed_acorns"), "Honey glazed acorns name");
        Equal("Hỗn hợp trái cây rừng sấy khô", CraftingCatalog.GetResourceName("resource.food.dried_wild_berries_mix"), "Fruit mix name");
        Equal("Bột mì", CraftingCatalog.GetResourceName("resource.plain.wheat_flour"), "Wheat flour name");
        Equal("Lúa mì", CraftingCatalog.GetResourceName("resource.plain.wheat"), "Wheat raw name");
        Equal("Vỏ cây rụng", CraftingCatalog.GetResourceName("resource.leafwood.fallen_bark"), "Fallen bark name");

        // Raw breakdown contains Vietnamese 'Lúa mì' rather than technical id
        Assert(formatted.Contains("Lúa mì"), $"Formatted breakdown must contain 'Lúa mì', was: {formatted}");
    }
}
