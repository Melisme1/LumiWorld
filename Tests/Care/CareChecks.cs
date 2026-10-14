using System;
using System.Collections.Generic;
using LumiWorld.Acs;
using LumiWorld.Acs.TimeSystem;

static class CareChecks
{
    const string Food = "resource.leafwood.raspberries";
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Near(double expected, double actual, string message)
        => Check(Math.Abs(expected - actual) < 1e-6, message + $" expected {expected}, got {actual}");
    static void Throws(Action action, string message)
    { try { action(); } catch (ArgumentException) { checks++; return; } throw new Exception(message); }
    sealed class Source : ITimeSource
    { public double now; public double MonotonicSeconds => now; public double UtcSeconds => 100000 + now; }

    sealed class World
    {
        public readonly ResourceProductionCatalog catalog = new ResourceProductionCatalog();
        public readonly HabitatState habitat = new HabitatState { habitatId = "h", islandId = "i", natureCardId = "leafwood", affinity = BiomeAffinity.Leafwood, island = HabitatIsland.Starter };
        public readonly ProductionRuntimeSettings settings = new ProductionRuntimeSettings { tier1BufferCap = 10000, tier2BufferCap = 10000 };
        public readonly CareProductionSimulation sim = new CareProductionSimulation(new ProductionLedger(), 0);
        public CreatureCareRules rules = new CreatureCareRules();
        public CreatureFoodRules foods = new CreatureFoodRules(new[] { new CreatureFoodRule(Food, 2) }, null);
        public World()
        {
            for (int i = 0; i < 6; i++) habitat.members.Add(new HabitatHexMember { placementId = "tile" + i, hex = new HexCoordinates(i) });
            habitat.EnsureSlots();
            foreach (string species in new[] { "a", "b" }) catalog.speciesRegistry.entries.Add(new SpeciesMappingEntry
            { gddKey = species, species = new AnimalSpeciesData { speciesID = species }, primaryAffinity = BiomeAffinity.Leafwood });
        }
        public void Add(string id, string species = "a", CreatureQualityRank rank = CreatureQualityRank.RankI)
            => habitat.residents.Add(new HabitatResident { individualId = id, speciesId = species, qualityRank = rank, role = AffinityRole.Primary, homeHex = new HexCoordinates(habitat.residents.Count) });
        public void Select(string resource, ResourceTier tier = ResourceTier.Tier1, int index = 0, params string[] species)
        {
            var definition = new ResourceProductionDefinition { resource = new ResourceData { resourceID = resource }, affinity = BiomeAffinity.Leafwood, tier = tier };
            foreach (string key in species.Length == 0 ? new[] { "a" } : species) definition.requirements.Add(new SpeciesProductionRequirement { gddSpeciesKey = key });
            catalog.resources.Add(definition); habitat.GetSlot(tier, index).selectedResourceId = resource;
        }
        public void Sync(double t = 0) => sim.Synchronize(new[] { habitat }, rules,
            h => ProductionRateCalculator.Calculate(h, catalog, settings, sim.Care.CanProduce), t, foods);
        public double Amount(string resource)
        { var b = sim.Ledger.Find("h").FindBuffer(resource); return b == null ? 0 : b.amount + b.fractionalCarry; }
        public void Collect(string resource) { sim.Ledger.Find("h").FindBuffer(resource).amount = 0; }
        public CreatureCareView View(string id = "one") => sim.Care.Read(id);
        public long Stock(string id = Food, string owner = "one") => sim.Food.GetAmount(owner, id);
        public void StockUp(int amount, string id = Food, string owner = "one")
        { Check(sim.Food.TryAdd(owner, id, amount), "Seed pantry"); sim.RefreshActivity(); }
    }
    static World Single(CreatureQualityRank rank = CreatureQualityRank.RankI)
    { var w = new World(); w.Add("one", rank: rank); w.Select("wood"); w.Sync(); return w; }

    static void Main()
    {
        DecayAndEligibility();
        EatingBoundaries();
        PreferencesAndScarcity();
        TransfersAndTopology();
        IdentityPauseAndConfig();
        Console.WriteLine($"PASS: {checks} care / satiety / feeding / pantry assertions. Managed stand-ins, not Unity Play Mode.");
    }

    static void DecayAndEligibility()
    {
        foreach (var rank in new[] { CreatureQualityRank.RankI, CreatureQualityRank.RankII, CreatureQualityRank.RankIII })
        {
            var w = Single(rank); double threshold = 30 + 30 * (int)rank;
            w.sim.AdvanceTo(threshold / 2); Near(50, w.View().Satiety, "Linear rank-based decay");
            Check(w.View().CanProduce, "No food: can work below auto-feed threshold");
            w.sim.AdvanceTo(threshold - .01); Check(w.View().CanProduce, "Healthy before empty");
            w.sim.AdvanceTo(500); Check(w.View().Status == CreatureHungerStatus.Hungry, "Hungry at zero");
            Near(0, w.View().Satiety, "Clamped at zero");
            Near(threshold * 2 * w.settings.GetContribution(rank), w.Amount("wood"), "No output beyond hunger");
        }
        var capped = Single(); capped.settings.tier1BufferCap = 100; capped.Sync(); capped.sim.AdvanceTo(200);
        Near(100, capped.Amount("wood"), "Cap respected"); Near(100d/6, capped.View().Satiety, "Decay stops at cap");
        capped.Collect("wood"); capped.sim.AdvanceTo(210);
        Check(!capped.View().CanProduce, "Hungry after ten more seconds"); Near(20, capped.Amount("wood"), "Correct last interval");
        var multi = new World(); multi.Add("one"); multi.Select("wood"); multi.Select("leaves", index: 1); multi.Sync(); multi.sim.AdvanceTo(30);
        Near(50, multi.View().Satiety, "Two slots do not double decay");
        multi.sim.Ledger.Find("h").FindBuffer("wood").amount = 10000; multi.sim.AdvanceTo(60);
        Near(0, multi.View().Satiety, "One accepting slot keeps decay"); Near(60, multi.Amount("leaves"), "Full slot does not reallocate share");
        var combo = new World(); combo.Add("a1"); combo.Add("b1", "b", CreatureQualityRank.RankII);
        combo.Select("wood"); combo.Select("bloom", index: 1, species: new[] { "b" }); combo.Select("combo", ResourceTier.Tier2, 0, "a", "b");
        combo.Sync(); combo.sim.AdvanceTo(80);
        Near(60, combo.Amount("wood"), "Hungry A stops T1"); Near(150, combo.Amount("bloom"), "Healthy B continues");
        Near(5, combo.Amount("combo"), "Required A missing stops combo");
        Check(combo.habitat.GetSlot(ResourceTier.Tier2,0).selectedResourceId == "combo", "Keep selection");
        var replacement = new World(); replacement.Add("a1"); replacement.Add("a2", rank: CreatureQualityRank.RankII);
        replacement.Add("b1", "b", CreatureQualityRank.RankIII); replacement.Select("combo", ResourceTier.Tier2, 0, "a", "b"); replacement.Sync(); replacement.sim.AdvanceTo(70);
        Check(replacement.sim.Ledger.Find("h").FindRate(ResourceTier.Tier2,0).unitsPerMinute > 0, "Same species healthy replacement");
        replacement.catalog.resources[0].requirements[0].minimumCount = 2; replacement.Sync(70);
        Near(0, replacement.sim.Ledger.Find("h").FindRate(ResourceTier.Tier2,0).unitsPerMinute, "Healthy minimumCount");
        var unused = new World(); unused.Add("one"); unused.Sync(); unused.sim.AdvanceTo(500);
        Near(100, unused.View().Satiety, "No selected resource, no decay");
        var locked = Single(); locked.habitat.members.RemoveRange(2,4); locked.Sync(); locked.sim.AdvanceTo(100);
        Near(100, locked.View().Satiety, "Under three hex no decay");
        var disabled = Single(); disabled.settings.productionEnabled = false; disabled.Sync(); disabled.sim.AdvanceTo(100);
        Near(100, disabled.View().Satiety, "Disabled production no decay");
    }

    static void EatingBoundaries()
    {
        var auto = Single(); auto.StockUp(100); auto.sim.AdvanceTo(29.9);
        Check(auto.View().CanProduce, "Before auto threshold still working"); Near(100, auto.Stock(), "Full creature does not eat");
        auto.sim.AdvanceTo(30); Check(auto.View().Status == CreatureHungerStatus.Eating, "Starts automatically at 50");
        Near(50, auto.View().Satiety, "No instant nutrition at bite start"); Near(99, auto.Stock(), "Reserve one whole unit only");
        Check(!auto.sim.TryFeed("h","one",out _), "Repeated feeding does not restart meal");
        auto.sim.AdvanceTo(30.05); Near(51, auto.View().Satiety, "Nutrition arrives gradually");
        auto.sim.AdvanceTo(32.5); Near(100, auto.View().Satiety, "50->100 needs 25 units and 2.5 seconds");
        Check(auto.View().CanProduce, "Resume at exactly full"); Near(75, auto.Stock(), "Consumes exactly 25");
        Near(60, auto.Amount("wood"), "No output while eating");
        auto.sim.AdvanceTo(33.5); Near(62, auto.Amount("wood"), "Continues below 100 after completed meal");
        var hungry = Single(); hungry.sim.AdvanceTo(60); hungry.StockUp(50); hungry.sim.AdvanceTo(65);
        Near(100, hungry.View().Satiety, "50 raspberries refill from zero in five seconds");
        Check(hungry.View().CanProduce, "Full meal completes"); Near(0, hungry.Stock(), "All 50 consumed");
        Near(120, hungry.Amount("wood"), "Production remains stopped during full meal");
        var partial = Single(); partial.sim.AdvanceTo(60); partial.StockUp(10); partial.sim.AdvanceTo(120);
        Near(20, partial.View().Satiety, "Insufficient food preserves partial satiety");
        Check(partial.View().WaitingForFood && !partial.View().CanProduce, "Wait until full, not resume at 20");
        Near(120, partial.Amount("wood"), "Waiting resident does not produce");
        partial.StockUp(40); partial.sim.AdvanceTo(124);
        Check(partial.View().CanProduce, "Adding remaining food finishes meal"); Near(100, partial.View().Satiety, "No lost progress");
        var manual = Single(); manual.sim.AdvanceTo(12); manual.StockUp(10);
        Check(manual.View().CanProduce, "Above threshold no auto eating");
        Check(manual.sim.TryFeed("h","one",out _), "Manual early feeding at 80");
        manual.sim.AdvanceTo(13); Near(100, manual.View().Satiety, "Early meal takes one second"); Near(24, manual.Amount("wood"), "Early eating stops output");
        Check(!manual.sim.TryFeed("h","one",out _), "Cannot feed full creature");
        Check(!manual.sim.TryFeed("different-habitat","one",out _), "Cannot feed through wrong biome");
        var slow = Single(); slow.StockUp(200); var fast = Single(); fast.StockUp(200);
        slow.sim.AdvanceTo(200);
        for (int i = 1; i <= 4000; i++) fast.sim.AdvanceTo(i / 20d);
        Near(slow.Amount("wood"), fast.Amount("wood"), "Long/partitioned output invariant with repeated meals");
        Near(slow.View().Satiety, fast.View().Satiety, "Long/partitioned satiety invariant");
        Near(slow.Stock(), fast.Stock(), "Long/partitioned consumption invariant");
    }

    static void PreferencesAndScarcity()
    {
        const string favorite = "favorite", other = "other";
        var rules = new CreatureFoodRules(new[] { new CreatureFoodRule(Food,2), new CreatureFoodRule(favorite,2), new CreatureFoodRule(other,20) },
            new[] { new SpeciesDietRule("a", new[] { Food, favorite }, new[] { favorite }, 2) });
        var w = Single(); w.foods = rules; w.Sync(); w.sim.AdvanceTo(60);
        w.sim.Food.TryAdd("one", Food,50); w.sim.Food.TryAdd("one",favorite,25); w.sim.Food.TryAdd("one",other,50); w.sim.RefreshActivity();
        Check(w.View().EatingResourceId == favorite, "Favorite chosen before ordinary");
        w.sim.AdvanceTo(62.5); Near(100,w.View().Satiety,"Favorite needs 25 units");
        Near(50,w.Stock(),"Ordinary preserved"); Near(0,w.Stock(favorite),"Favorite consumed first"); Near(50,w.Stock(other),"Forbidden item untouched");
        var fallback = Single(); fallback.foods = rules; fallback.Sync(); fallback.sim.AdvanceTo(60);
        fallback.sim.Food.TryAdd("one",favorite,1); fallback.sim.Food.TryAdd("one",Food,48); fallback.sim.RefreshActivity(); fallback.sim.AdvanceTo(64.9);
        Near(100,fallback.View().Satiety,"Favorite exhaustion falls back to allowed ordinary food"); Check(fallback.View().CanProduce,"Fallback meal complete");
        var wrong = Single(); wrong.foods = rules; wrong.Sync(); wrong.sim.AdvanceTo(60); wrong.StockUp(50,other); wrong.sim.AdvanceTo(100);
        Near(0,wrong.View().Satiety,"Unfit food cannot feed resident"); Near(50,wrong.Stock(other),"No wasted forbidden stock");
        var switchFood = Single(); switchFood.foods = rules; switchFood.Sync(); switchFood.sim.AdvanceTo(60); switchFood.StockUp(50);
        switchFood.sim.AdvanceTo(60.05); switchFood.StockUp(1,favorite);
        Check(switchFood.View().EatingResourceId == Food,"Keep already paid bite when favorite arrives");
        switchFood.sim.AdvanceTo(60.1); Check(switchFood.View().EatingResourceId == favorite,"Next bite switches to favorite");
        var pair = new World(); pair.Add("first"); pair.Add("second"); pair.Select("wood"); pair.Sync(); pair.sim.AdvanceTo(60);
        pair.StockUp(50, owner: "first"); pair.sim.AdvanceTo(65);
        Near(100, pair.View("first").Satiety + pair.View("second").Satiety, "Shared stock not duplicated across residents");
        Check(pair.View("first").CanProduce && !pair.View("second").CanProduce, "Only funded creature resumes");
        Near(0,pair.Stock(owner: "first"),"Personal pantry consumed");
        Throws(() => new CreatureFoodRules(new[] { new CreatureFoodRule(Food,2) },
            new[] { new SpeciesDietRule("a",new[] { Food },new[] { other },2) }), "Favorite must be allowed");
    }

    static void TransfersAndTopology()
    {
        var w = Single(); var warehouse = new ResourceInventory(); var food = new ResourceData { resourceID = Food };
        warehouse.Add(food,100);
        bool coherent = false; int observerAfterFailure = 0;
        warehouse.OnInventoryChanged += (id,amount,delta) => coherent = warehouse.GetAmount(Food) + w.Stock() == 100;
        warehouse.OnInventoryChanged += (id,amount,delta) => throw new Exception("view");
        warehouse.OnInventoryChanged += (id,amount,delta) => observerAfterFailure++;
        Check(CreatureFoodTransferService.Deposit(warehouse,w.sim.Food,w.foods,"h","one",Food,50,out _),"Deposit");
        Check(coherent && observerAfterFailure == 1,"Deposit observers see both stores committed, errors isolated");
        Near(50,warehouse.GetAmount(Food),"Warehouse debited"); Near(50,w.Stock(),"Pantry credited");
        Check(!CreatureFoodTransferService.Deposit(warehouse,w.sim.Food,w.foods,"h","one",Food,51,out _),"Reject insufficient deposit atomically");
        Near(50,w.Stock(),"Rejected deposit unchanged");
        Check(!CreatureFoodTransferService.Deposit(warehouse,w.sim.Food,w.foods,"h","one","wood",1,out _),"Reject non-food deposit");
        food.maxStackSize = 60;
        Near(10,CreatureFoodTransferService.Withdraw(warehouse,w.sim.Food,food,"one",50,out _),"Withdraw up to Warehouse cap");
        Near(40,w.Stock(),"Keep unwithdrawn stock"); Near(60,warehouse.GetAmount(Food),"Warehouse cap");
        Check(coherent,"Withdrawal observer sees source already debited");
        Check(!warehouse.TryConsume(Food,1,() => false),"Failed owner commit"); Near(60,warehouse.GetAmount(Food),"Rollback stock");
        try { warehouse.TryConsume(Food,1,() => throw new Exception("commit")); } catch (Exception) { }
        Near(60,warehouse.GetAmount(Food),"Exception rollback");
        var store = new CreatureFoodStore();
        var a = new HabitatState { habitatId="a" };
        var b = new HabitatState { habitatId="b" };
        var one = new HabitatResident { individualId="one" };
        var two = new HabitatResident { individualId="two" };
        a.residents.Add(one); b.residents.Add(two);
        store.Synchronize(new[] { a,b }); store.TryAdd("one",Food,30); store.TryAdd("two",Food,20);
        a.residents.Add(two); b.residents.Clear(); store.Synchronize(new[] { a });
        Near(30,store.GetAmount("one",Food),"Merge preserves first owner");
        Near(20,store.GetAmount("two",Food),"Merge preserves second owner");
        Check(store.IsResident("a","two"),"Merge updates location");
        a.residents.Remove(two); b.residents.Add(two); store.Synchronize(new[] { a,b });
        Near(20,store.GetAmount("two",Food),"Split keeps personal stock");
        Check(!CreatureFoodTransferService.Deposit(warehouse,store,w.foods,"a","two",Food,1,out _),"Reject stale habitat");
        store.Synchronize(Array.Empty<HabitatState>());
        Check(store.Read()[0].IsOrphan,"Absent creature keeps recoverable stock");
        Check(!store.TryAdd("one",Food,1),"Absent owner cannot receive deposits");
        var emptyWarehouse = new ResourceInventory(); food.maxStackSize=100;
        Near(30,CreatureFoodTransferService.Withdraw(emptyWarehouse,store,food,"one",50,out _),"Withdraw absent owner's stock");
        store.Synchronize(new[] { b }); Near(20,store.GetAmount("two",Food),"Rebind retains personal stock");
        a.residents.Add(two);
        Throws(() => store.Synchronize(new[] { a,b }),"Reject duplicate individual IDs");
        Near(20,store.GetAmount("two",Food),"Rejected sync preserves stock");
    }
    static void IdentityPauseAndConfig()
    {
        var w = Single(); w.sim.AdvanceTo(30); w.Sync(30); Near(50,w.View().Satiety,"Refresh preserves satiety");
        w.habitat.residents[0].qualityRank = CreatureQualityRank.RankII; w.Sync(30);
        Near(50,w.View().Satiety,"Rank changes future decay, never current satiety");
        w.sim.AdvanceTo(75); Check(!w.View().CanProduce,"Half of new 90 seconds remains");
        w.habitat.residents[0].qualityRank = CreatureQualityRank.RankIII; w.Sync(75);
        Check(!w.View().CanProduce,"Rank up cannot cure hunger");
        var resident = w.habitat.residents[0]; w.habitat.residents.Clear(); w.Sync(75); w.sim.AdvanceTo(100);
        w.habitat.residents.Add(resident); w.Sync(100); Near(0,w.View().Satiety,"Rebind same ID preserves hunger");
        var source = new Source(); var clock = new GameClock(source); var paused = Single(); paused.StockUp(50);
        source.now=30; paused.sim.AdvanceTo(clock.Read().GameSeconds); clock.SetPaused(true); paused.sim.AutoFeedingEnabled=false;
        long stock = paused.Stock(); source.now=300; paused.sim.AdvanceTo(clock.Read().GameSeconds);
        Near(50,paused.View().Satiety,"Pause freezes nutrition too"); Near(stock,paused.Stock(),"Pause consumes no new food");
        clock.SetPaused(false); paused.sim.AutoFeedingEnabled=true; source.now=302.5; paused.sim.AdvanceTo(clock.Read().GameSeconds);
        Near(100,paused.View().Satiety,"Resume without paused-time eating");
        var stop = Single(); stop.sim.AdvanceTo(60); stop.StockUp(50); stop.sim.Stop(60); stop.sim.AdvanceTo(100);
        Near(2,stop.View().Satiety,"Stopped runtime finishes only its prepaid bite"); Near(49,stop.Stock(),"Stopped runtime starts no new bites");
        stop.Sync(100); stop.sim.AdvanceTo(104.9); Near(100,stop.View().Satiety,"Enable resumes meal without resetting");
        var edited = Single(); edited.sim.AdvanceTo(60); edited.StockUp(50); edited.sim.AdvanceTo(60.05);
        edited.rules = new CreatureCareRules(foodUnitsPerSecond:5);
        edited.foods = new CreatureFoodRules(new[] { new CreatureFoodRule(Food,4) },null);
        edited.Sync(60.05); edited.sim.AdvanceTo(60.1);
        Near(2,edited.View().Satiety,"Config edits keep nutrition/duration of prepaid bite");
        edited.sim.AdvanceTo(60.2); Near(4,edited.View().Satiety,"Next bite takes edited nutrition/speed");
        var pausedStart = Single(); pausedStart.sim.AdvanceTo(60); pausedStart.sim.AutoFeedingEnabled=false;
        pausedStart.StockUp(50); pausedStart.Sync(60);
        Check(!pausedStart.View().IsTakingBite && pausedStart.Stock()==50,"Config/topology sync while paused starts no bite");
        pausedStart.sim.AutoFeedingEnabled=true; pausedStart.sim.RefreshActivity();
        Check(pausedStart.View().IsTakingBite && pausedStart.Stock()==49,"Resume starts pending automatic meal");
        var config = new CreatureCareSettings();
        Check(config.TryGetRules(out var rules,out _) && rules.AutoFeedAtSatiety == 50 && rules.FoodUnitsPerSecond == 10,"Inspector defaults");
        Check(config.TryGetFoodRules(out var foods,out _),"Seed food config");
        Near(2,foods.Choose("any",_ => 50).SatietyPerUnit,"Seed has 2 satiety per unit, no favorites assigned");
        config.rankIHungerSeconds = 0; Check(!config.TryGetRules(out _,out _),"Reject invalid rank timing");
        Throws(() => new CreatureCareRules(autoFeedAtSatiety:100),"Threshold must not create infinite feeding");
        Throws(() => new CreatureFoodRule(Food,double.NaN),"Reject invalid nutrition");
    }
}
