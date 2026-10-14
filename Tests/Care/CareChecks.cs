using System;
using System.Collections.Generic;
using LumiWorld.Acs;
using LumiWorld.Acs.TimeSystem;

static class CareChecks
{
    const string Food = "resource.leafwood.raspberries";
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Near(double expected, double actual, string message) => Check(Math.Abs(expected - actual) < 1e-6, message + $" expected {expected}, got {actual}");
    sealed class Source : ITimeSource { public double now; public double MonotonicSeconds => now; public double UtcSeconds => 100000 + now; }
    sealed class World
    {
        public readonly ResourceProductionCatalog catalog = new ResourceProductionCatalog();
        public readonly HabitatState habitat = new HabitatState { habitatId = "h", islandId = "i", natureCardId = "leafwood", affinity = BiomeAffinity.Leafwood, island = HabitatIsland.Starter };
        public readonly ProductionRuntimeSettings settings = new ProductionRuntimeSettings { tier1BufferCap = 10000, tier2BufferCap = 10000 };
        public readonly CareProductionSimulation sim = new CareProductionSimulation(new ProductionLedger(), 0);
        public CreatureCareRules rules = new CreatureCareRules();
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
        public void Sync(double t = 0) => sim.Synchronize(new[] { habitat }, rules, h => ProductionRateCalculator.Calculate(h, catalog, settings, sim.Care.CanProduce), t);
        public double Amount(string resource) { var b = sim.Ledger.Find("h").FindBuffer(resource); return b == null ? 0 : b.amount + b.fractionalCarry; }
        public void Collect(string resource) { var b = sim.Ledger.Find("h").FindBuffer(resource); b.amount = 0; }
    }
    static World Single(CreatureQualityRank rank = CreatureQualityRank.RankI)
    { var w = new World(); w.Add("one", rank: rank); w.Select("wood"); w.Sync(); return w; }
    static void Main()
    {
        foreach (var rank in new[] { CreatureQualityRank.RankI, CreatureQualityRank.RankII, CreatureQualityRank.RankIII })
        {
            var w = Single(rank); double threshold = 30 + 30 * (int)rank;
            w.sim.AdvanceTo(threshold - .01); Check(w.sim.Care.CanProduce("one"), "Healthy before rank boundary");
            w.sim.AdvanceTo(500); Check(w.sim.Care.Read("one").Status == CreatureHungerStatus.Hungry, "Hungry on boundary");
            Near(threshold, w.sim.Care.Read("one").WorkSeconds, "Exact hunger work");
            Near(threshold * 2 * w.settings.GetContribution(rank), w.Amount("wood"), "Large gap cannot overproduce beyond hunger");
        }
        var capped = Single(); capped.settings.tier1BufferCap = 100; capped.Sync(); capped.sim.AdvanceTo(200);
        Near(100, capped.Amount("wood"), "Capacity respected"); Near(50, capped.sim.Care.Read("one").WorkSeconds, "No work while full");
        Check(capped.sim.Care.CanProduce("one"), "Full buffer creature still healthy"); capped.Collect("wood"); capped.sim.AdvanceTo(210);
        Check(!capped.sim.Care.CanProduce("one"), "Hungry after collect and ten more seconds"); Near(20, capped.Amount("wood"), "Remaining ten seconds output");

        var multi = new World(); multi.Add("one"); multi.Select("wood"); multi.Select("leaves", index: 1); multi.Sync(); multi.sim.AdvanceTo(60);
        Near(60, multi.sim.Care.Read("one").WorkSeconds, "Two slots do not double work"); Near(60, multi.Amount("wood"), "Contribution split first slot"); Near(60, multi.Amount("leaves"), "Contribution split second slot");
        var partlyFull = new World(); partlyFull.Add("one"); partlyFull.Select("wood"); partlyFull.Select("leaves", index: 1); partlyFull.settings.tier1BufferCap = 100; partlyFull.Sync();
        partlyFull.sim.AdvanceTo(1); partlyFull.sim.Ledger.Find("h").FindBuffer("wood").amount = 100; partlyFull.sim.AdvanceTo(60);
        Check(!partlyFull.sim.Care.CanProduce("one"), "One accepting slot is enough to count work"); Near(60, partlyFull.Amount("leaves"), "Full slot does not reallocate share");

        var combo = new World(); combo.Add("a1"); combo.Add("b1", "b", CreatureQualityRank.RankII); combo.Select("wood"); combo.Select("bloom", index: 1, species: new[] { "b" }); combo.Select("combo", ResourceTier.Tier2, 0, "a", "b"); combo.Sync(); combo.sim.AdvanceTo(80);
        Near(60, combo.Amount("wood"), "Hungry A stops T1"); Near(150, combo.Amount("bloom"), "Healthy B continues with reallocated contribution"); Near(5, combo.Amount("combo"), "T2 stops when required A hungry");
        Check(combo.sim.Ledger.Find("h").FindRate(ResourceTier.Tier2, 0).unitsPerMinute == 0, "T2 zero after lost healthy species");
        Check(combo.habitat.GetSlot(ResourceTier.Tier2, 0).selectedResourceId == "combo", "Selection preserved");
        var replacement = new World(); replacement.Add("a1"); replacement.Add("a2", rank: CreatureQualityRank.RankII); replacement.Add("b1", "b", CreatureQualityRank.RankIII); replacement.Select("combo", ResourceTier.Tier2, 0, "a", "b"); replacement.Sync(); replacement.sim.AdvanceTo(70);
        Check(replacement.sim.Ledger.Find("h").FindRate(ResourceTier.Tier2, 0).unitsPerMinute > 0, "Healthy same-species resident preserves combo");
        replacement.catalog.resources[0].requirements[0].minimumCount = 2; replacement.Sync(70);
        Near(0, replacement.sim.Ledger.Find("h").FindRate(ResourceTier.Tier2, 0).unitsPerMinute, "Combo minimumCount uses healthy residents");

        var unused = new World(); unused.Add("one"); unused.Sync(); unused.sim.AdvanceTo(500);
        Near(0, unused.sim.Care.Read("one").WorkSeconds, "No selection means no work");
        var shortGroup = Single(); shortGroup.habitat.members.RemoveRange(2,4); shortGroup.Sync(); shortGroup.sim.AdvanceTo(100);
        Near(0, shortGroup.sim.Care.Read("one").WorkSeconds, "Locked slots under three hex cause no hunger work");
        var disabled = Single(); disabled.settings.productionEnabled = false; disabled.Sync(); disabled.sim.AdvanceTo(100);
        Near(0, disabled.sim.Care.Read("one").WorkSeconds, "Disabled production stops work");

        var fed = Single(); fed.sim.AdvanceTo(60);
        var inventory = new ResourceInventory(); var recipe = new CreatureFeedRecipe(Food, 1, 5);
        Check(!CreatureFeedingService.TryFeed(fed.sim.Care, "one", recipe, inventory, out _), "Insufficient food rejected");
        var food = new ResourceData { resourceID = Food }; inventory.Add(food, 3);
        bool coherent = false; bool doubleFeed = true;
        inventory.OnInventoryChanged += (id, amount, delta) => { if (delta >= 0) return; coherent = fed.sim.Care.Read("one").Status == CreatureHungerStatus.Recovering && amount == inventory.GetAmount(Food); doubleFeed = CreatureFeedingService.TryFeed(fed.sim.Care, "one", recipe, inventory, out _); };
        inventory.OnInventoryChanged += (id, amount, delta) => throw new Exception("Simulated view failure");
        int afterFailure = 0; inventory.OnInventoryChanged += (id, amount, delta) => afterFailure++;
        Check(CreatureFeedingService.TryFeed(fed.sim.Care, "one", recipe, inventory, out _), "Feed starts recovery");
        Check(coherent && !doubleFeed, "Inventory listeners see committed recovery and cannot feed twice");
        Near(2, inventory.GetAmount(Food), "Exactly one food consumed"); Check(afterFailure == 1 && UnityEngine.Debug.Exceptions == 1, "Failing view does not skip later save listeners");
        fed.sim.RefreshRates(); fed.sim.AdvanceTo(64); Near(120, fed.Amount("wood"), "No output during recovery"); Near(1, fed.sim.Care.Read("one").RecoverySeconds, "Recovery countdown");
        fed.sim.AdvanceTo(75); Near(140, fed.Amount("wood"), "Long gap splits recovery and ten seconds of new work"); Near(10, fed.sim.Care.Read("one").WorkSeconds, "Fresh work cycle after feed");
        Check(!CreatureFeedingService.TryFeed(fed.sim.Care, "one", recipe, inventory, out _), "Healthy feed rejected"); Near(2, inventory.GetAmount(Food), "Rejected feed unchanged inventory");
        Check(!inventory.TryConsume(Food, 1, () => false), "Failed care commit rejected"); Near(2, inventory.GetAmount(Food), "Failed care commit rolls back stock");
        try { inventory.TryConsume(Food, 1, () => throw new Exception("commit")); } catch (Exception) { }
        Near(2, inventory.GetAmount(Food), "Throwing commit rolls back stock");

        var identity = Single(); identity.sim.AdvanceTo(40); identity.Sync(40); Near(40, identity.sim.Care.Read("one").WorkSeconds, "Topology rebuild keeps work");
        identity.habitat.residents[0].qualityRank = CreatureQualityRank.RankII; identity.Sync(40); Near(40, identity.sim.Care.Read("one").WorkSeconds, "Rank edit preserves work");
        identity.sim.AdvanceTo(90); Check(!identity.sim.Care.CanProduce("one"), "Rank II hungry at total 90");
        identity.habitat.residents[0].qualityRank = CreatureQualityRank.RankIII; identity.Sync(90); Check(!identity.sim.Care.CanProduce("one"), "Rank increase cannot cure hunger");
        var resident = identity.habitat.residents[0]; identity.habitat.residents.Clear(); identity.Sync(90); identity.sim.AdvanceTo(300); identity.habitat.residents.Add(resident); identity.Sync(300);
        Check(!identity.sim.Care.CanProduce("one"), "Rebind same individual remains hungry");
        var stop = Single(); stop.sim.AdvanceTo(60); stop.sim.Care.TryBeginRecovery("one", 5); stop.sim.Stop(60); stop.sim.AdvanceTo(80);
        Check(stop.sim.Care.CanProduce("one"), "Recovery finishes while production stopped"); Near(0, stop.sim.Care.Read("one").WorkSeconds, "Stopped simulation cannot restart work at recovery"); Near(120, stop.Amount("wood"), "Stopped output unchanged");
        stop.Sync(80); stop.sim.AdvanceTo(85); Near(10, stop.Amount("wood") - 120, "Enable resumes without disabled-time output");
        var source = new Source(); var clock = new GameClock(source); var paused = Single(); source.now = 20; clock.SetPaused(true); paused.sim.AdvanceTo(clock.Read().GameSeconds); source.now = 200; paused.sim.AdvanceTo(clock.Read().GameSeconds);
        Near(20, paused.sim.Care.Read("one").WorkSeconds, "Pause excludes gameplay elapsed"); clock.SetPaused(false); source.now = 210; paused.sim.AdvanceTo(clock.Read().GameSeconds); Near(30, paused.sim.Care.Read("one").WorkSeconds, "Resume does not catch up paused time");
        source.now = 240; paused.sim.AdvanceTo(clock.Read().GameSeconds); Check(!paused.sim.Care.CanProduce("one"), "Focus gap advances shared gameplay time");
        var slow = Single(); var fast = Single(); slow.sim.AdvanceTo(200); for (int i = 1; i <= 2000; i++) fast.sim.AdvanceTo(i / 10d);
        Near(slow.Amount("wood"), fast.Amount("wood"), "Frame partition invariant production"); Near(slow.sim.Care.Read("one").WorkSeconds, fast.sim.Care.Read("one").WorkSeconds, "Frame partition invariant work");
        var config = new CreatureCareSettings(); Check(config.FoodResourceId == Food && config.foodAmount == 1 && config.recoverySeconds == 5, "Inspector defaults"); config.rankIHungerSeconds = 0; Check(!config.TryGetRules(out _, out _), "Invalid hunger config rejected");
        Console.WriteLine($"PASS: {checks} creature care / production / inventory assertions. Managed stand-ins, not Unity Play Mode.");
    }
}
