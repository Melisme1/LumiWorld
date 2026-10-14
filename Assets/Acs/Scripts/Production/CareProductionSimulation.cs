using System;
using System.Collections.Generic;

namespace LumiWorld.Acs
{
    // Coordinates existing ledger and time-based care at the exact next gameplay boundary.
    // Unity supplies GameClock.GameSeconds. No UI, Unity clock or inventory mutation here.
    public sealed class CareProductionSimulation
    {
        public ProductionLedger Ledger { get; }
        public CreatureCareService Care { get; }
        public CreatureFoodStore Food { get; } = new CreatureFoodStore();
        public CreatureFoodRules FoodRules { get; private set; }
        public bool AutoFeedingEnabled { get; set; } = true;
        private CreatureCareRules rules;
        private List<HabitatState> habitats = new List<HabitatState>();
        private Func<HabitatState, List<ProductionSlotRate>> buildRates;
        private bool producing;
        public bool IsRunning => producing;
        public CareProductionSimulation(ProductionLedger ledger, double gameSeconds)
        { Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger)); Care = new CreatureCareService(gameSeconds); }

        public void Synchronize(IReadOnlyList<HabitatState> current, CreatureCareRules rules,
            Func<HabitatState, List<ProductionSlotRate>> rateFactory, double gameSeconds, CreatureFoodRules foods = null)
        {
            AdvanceTo(gameSeconds); // Previous topology, ranks, config and cached rates.
            habitats = CopyHabitats(current);
            this.rules = rules;
            FoodRules = foods;
            Food.Synchronize(habitats);
            foreach (var habitat in habitats)
                foreach (var resident in habitat.residents) Care.Register(resident.individualId, resident.qualityRank, rules);
            buildRates = rateFactory; producing = true;
            PrepareEating(); RefreshRates();
        }
        public void Stop(double gameSeconds)
        { AdvanceTo(gameSeconds); producing = false; Ledger.Stop(gameSeconds); }
        public void RefreshRates()
        {
            if (producing) Ledger.Synchronize(habitats, buildRates, Care.GameSeconds);
            else Ledger.Stop(Care.GameSeconds);
        }
        public bool IsWorking(string id) => WorkingIndividuals(out _).Contains(id);

        public CreatureFeedPreview PreviewFeed(string habitatId, string individualId)
        {
            var resident = FindResident(habitatId, individualId);
            var choice = resident == null ? null : ChooseFood(resident);
            return CreatureFeedingService.Preview(resident == null ? null : Care.Read(individualId), choice,
                choice == null ? 0 : Food.GetAmount(individualId, choice.ResourceId), rules?.FoodUnitsPerSecond ?? 10);
        }
        public bool TryFeed(string habitatId, string individualId, out string reason)
        {
            var preview = PreviewFeed(habitatId, individualId); reason = preview.Reason;
            if (!producing || !AutoFeedingEnabled)
            { reason = "Hệ thống cho ăn đang dừng."; return false; }
            if (!preview.CanFeed) return false;
            if (!Care.TryStartEating(individualId)) return false;
            RefreshActivity(); reason = "Đã bắt đầu ăn từ ngăn thức ăn riêng."; return true;
        }
        public void RefreshActivity()
        {
            long revision = Care.Revision;
            PrepareEating();
            // Explicit commands may already have changed a resident's status before this call.
            if (producing || revision != Care.Revision) RefreshRates();
        }
        private HabitatResident FindResident(string habitatId, string id)
        { return habitats.Find(h => h.habitatId == habitatId)?.residents.Find(r => r.individualId == id); }
        private CreatureFoodChoice ChooseFood(HabitatResident resident)
            => FoodRules?.Choose(resident.speciesId, id => Food.GetAmount(resident.individualId, id));

        private void PrepareEating()
        {
            if (!producing || !AutoFeedingEnabled || FoodRules == null) return;
            foreach (var habitat in habitats)
            {
                foreach (var resident in habitat.residents)
                {
                    var state = Care.Read(resident.individualId);
                    if (state.IsTakingBite || state.Satiety >= 100) continue;
                    if (state.Status == CreatureHungerStatus.Healthy && state.Satiety > rules.AutoFeedAtSatiety + 1e-8) continue;
                    var food = ChooseFood(resident);
                    if (food == null) continue; // A healthy resident without food can work until empty.
                    if (state.Status != CreatureHungerStatus.Eating) Care.TryStartEating(resident.individualId);
                    if (!Food.TryTake(resident.individualId, food.ResourceId, 1)) continue;
                    try
                    {
                        if (!Care.TryBeginBite(resident.individualId, food.ResourceId,
                            food.SatietyPerUnit, 1 / rules.FoodUnitsPerSecond))
                            Food.TryAdd(resident.individualId, food.ResourceId, 1);
                    }
                    catch { Food.TryAdd(resident.individualId, food.ResourceId, 1); throw; }
                }
            }
        }

        private double SecondsToAutoFeed(ISet<string> workers)
        {
            double result = double.PositiveInfinity;
            if (!producing || !AutoFeedingEnabled || FoodRules == null) return result;
            foreach (var habitat in habitats)
                foreach (var resident in habitat.residents)
                {
                    if (!workers.Contains(resident.individualId) || ChooseFood(resident) == null) continue;
                    var state = Care.Read(resident.individualId);
                    if (state.Status == CreatureHungerStatus.Healthy && state.Satiety > rules.AutoFeedAtSatiety)
                        result = Math.Min(result, (state.Satiety - rules.AutoFeedAtSatiety) * state.HungerThresholdSeconds / 100);
                }
            return result;
        }

        public void AdvanceTo(double gameSeconds)
        {
            if (double.IsNaN(gameSeconds) || double.IsInfinity(gameSeconds) || gameSeconds < Care.GameSeconds)
                throw new ArgumentOutOfRangeException(nameof(gameSeconds));
            long initialRevision = Care.Revision;
            PrepareEating();
            if (Care.Revision != initialRevision) RefreshRates();
            while (Care.GameSeconds < gameSeconds)
            {
                var workers = WorkingIndividuals(out double toCapacity);
                double seconds = Math.Min(gameSeconds - Care.GameSeconds,
                    Math.Min(SecondsToAutoFeed(workers), Math.Min(toCapacity, Care.SecondsToNextBoundary(workers))));
                // Numerical tolerance only; prevents a sub-ulp boundary from stalling the loop.
                double next = Math.Min(gameSeconds, Care.GameSeconds + Math.Max(seconds, 1e-8));
                long revision = Care.Revision;
                Ledger.Settle(next); // Accrue old rates BEFORE applying hunger/recovery.
                Care.AdvanceTo(next, workers);
                PrepareEating();
                if (Care.Revision != revision) RefreshRates();
            }
        }
        private HashSet<string> WorkingIndividuals(out double toCapacity)
        {
            var workers = new HashSet<string>(StringComparer.Ordinal);
            toCapacity = double.PositiveInfinity;
            foreach (var state in Ledger.States)
            {
                if (state.isRecovery) continue;
                foreach (var rate in state.rates)
                {
                    if (rate.unitsPerMinute <= 0 || rate.bufferCap <= 0) continue;
                    var buffer = state.FindBuffer(rate.resourceId);
                    double room = rate.bufferCap - (buffer?.amount ?? 0) - (buffer?.fractionalCarry ?? 0);
                    if (room <= 0) continue;
                    toCapacity = Math.Min(toCapacity, room * 60 / rate.unitsPerMinute);
                    foreach (string id in rate.contributingIndividualIds)
                        if (Care.CanProduce(id)) workers.Add(id);
                }
            }
            return workers;
        }
        private static List<HabitatState> CopyHabitats(IReadOnlyList<HabitatState> source)
        {
            var result = new List<HabitatState>();
            if (source == null) return result;
            foreach (var h in source)
            {
                var copy = new HabitatState { habitatId = h.habitatId, islandId = h.islandId,
                    natureCardId = h.natureCardId, affinity = h.affinity, island = h.island };
                foreach (var m in h.members) copy.members.Add(new HabitatHexMember { placementId = m.placementId,
                    hex = m.hex, islandId = m.islandId, natureCardId = m.natureCardId, affinity = m.affinity,
                    island = m.island, connectionClusterId = m.connectionClusterId });
                foreach (var r in h.residents) copy.residents.Add(new HabitatResident { individualId = r.individualId,
                    speciesId = r.speciesId, homeHex = r.homeHex, role = r.role, qualityRank = r.qualityRank });
                foreach (var s in h.slots) copy.slots.Add(new HabitatResourceSlot { tier = s.tier, index = s.index, selectedResourceId = s.selectedResourceId });
                result.Add(copy);
            }
            return result;
        }
    }
}
