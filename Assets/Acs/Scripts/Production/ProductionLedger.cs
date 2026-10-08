using System;
using System.Collections.Generic;

namespace LumiWorld.Acs
{
    // Pure accrual over cached rates. Synchronize first settles the old rates, then installs new ones.
    // This class neither deposits into Inventory nor reads/writes a save file.
    public sealed class ProductionLedger
    {
        private readonly List<HabitatProductionState> states = new List<HabitatProductionState>();
        public IReadOnlyList<HabitatProductionState> States => states;
        public HabitatProductionState Find(string habitatId) => states.Find(s => s.habitatId == habitatId);

        public void Settle(double utcSeconds)
        {
            if (!Finite(utcSeconds)) throw new ArgumentOutOfRangeException(nameof(utcSeconds));
            foreach (var state in states)
            {
                if (!state.hasSettlementTime)
                { state.lastSettledUtc = utcSeconds; state.hasSettlementTime = true; continue; }
                // Keep a high-water mark when the device clock moves backwards.
                if (utcSeconds <= state.lastSettledUtc) continue;
                double elapsedMinutes = (utcSeconds - state.lastSettledUtc) / 60d;
                state.lastSettledUtc = utcSeconds;
                foreach (var rate in state.rates)
                {
                    if (rate.unitsPerMinute <= 0 || !Finite(rate.unitsPerMinute) || rate.bufferCap <= 0 ||
                        string.IsNullOrEmpty(rate.resourceId)) continue;
                    var buffer = state.FindBuffer(rate.resourceId);
                    if (buffer == null)
                    {
                        buffer = new ResourceProductionBuffer { resourceId = rate.resourceId, tier = rate.tier };
                        state.buffers.Add(buffer);
                    }
                    long room = rate.bufferCap - buffer.amount;
                    if (room <= 0) continue; // Preserve carry brought in by a merge or cap reduction.
                    double produced = buffer.fractionalCarry + elapsedMinutes * rate.unitsPerMinute;
                    if (produced >= room)
                    {
                        buffer.amount += room;
                        buffer.fractionalCarry = 0; // Discard excess, including fractions, at capacity.
                    }
                    else
                    {
                        long whole = (long)Math.Floor(produced + 1e-10d);
                        buffer.amount += whole;
                        buffer.fractionalCarry = Math.Max(0d, produced - whole);
                    }
                }
            }
        }

        public void Synchronize(IReadOnlyList<HabitatState> habitats,
            Func<HabitatState, List<ProductionSlotRate>> buildRates, double utcSeconds)
        {
            Settle(utcSeconds);
            var active = new Dictionary<string, HabitatState>(StringComparer.Ordinal);
            if (habitats != null)
                foreach (var habitat in habitats)
                {
                    if (habitat == null || string.IsNullOrEmpty(habitat.habitatId) || active.ContainsKey(habitat.habitatId))
                        throw new ArgumentException("Habitat production ID thiếu hoặc trùng.");
                    active.Add(habitat.habitatId, habitat);
                }

            foreach (var habitat in active.Values)
                if (Find(habitat.habitatId) == null)
                    states.Add(new HabitatProductionState { habitatId = habitat.habitatId,
                        hasSettlementTime = true, lastSettledUtc = utcSeconds });

            // A split keeps the entire old buffer with the surviving ID. Merged IDs transfer their
            // buffers to the group with greatest shared membership. No remaining home => recovery.
            foreach (var old in new List<HabitatProductionState>(states))
            {
                if (active.ContainsKey(old.habitatId) || old.isRecovery) continue;
                HabitatState destination = null;
                int bestOverlap = 0;
                foreach (var candidate in active.Values)
                {
                    if (candidate.islandId != old.islandId || candidate.natureCardId != old.natureCardId) continue;
                    int overlap = 0;
                    foreach (var member in candidate.members)
                        if (old.memberIds.Contains(member.placementId)) overlap++;
                    if (overlap > bestOverlap || (overlap > 0 && overlap == bestOverlap &&
                        string.CompareOrdinal(candidate.habitatId, destination.habitatId) < 0))
                    { destination = candidate; bestOverlap = overlap; }
                }
                if (destination != null)
                {
                    var target = Find(destination.habitatId);
                    target.lastSettledUtc = Math.Max(target.lastSettledUtc, old.lastSettledUtc);
                    foreach (var source in old.buffers) MergeBuffer(target, source);
                    states.Remove(old);
                }
                else
                {
                    old.isRecovery = true;
                    old.rates.Clear();
                }
            }

            foreach (var habitat in active.Values)
            {
                var state = Find(habitat.habitatId);
                state.isRecovery = false;
                state.islandId = habitat.islandId;
                state.natureCardId = habitat.natureCardId;
                state.memberIds.Clear();
                foreach (var member in habitat.members) state.memberIds.Add(member.placementId);
                state.rates = CloneRates(buildRates != null ? buildRates(habitat) : null);
            }
        }

        public void Stop(double utcSeconds)
        {
            Settle(utcSeconds);
            foreach (var state in states) state.rates.Clear();
        }

        private static void MergeBuffer(HabitatProductionState target, ResourceProductionBuffer source)
        {
            var buffer = target.FindBuffer(source.resourceId);
            if (buffer == null)
            {
                buffer = new ResourceProductionBuffer { resourceId = source.resourceId, tier = source.tier };
                target.buffers.Add(buffer);
            }
            double carry = buffer.fractionalCarry + source.fractionalCarry;
            long whole = (long)Math.Floor(carry + 1e-10d);
            buffer.amount = checked(buffer.amount + source.amount + whole);
            buffer.fractionalCarry = Math.Max(0d, carry - whole);
            // Existing goods may exceed the production cap after a merge; never destroy them.
        }

        private static List<ProductionSlotRate> CloneRates(List<ProductionSlotRate> rates)
        {
            var result = new List<ProductionSlotRate>();
            var resources = new HashSet<string>(StringComparer.Ordinal);
            if (rates != null)
                foreach (var rate in rates)
                {
                    if (rate == null) continue;
                    if (rate.unitsPerMinute > 0 && !resources.Add(rate.resourceId))
                        throw new ArgumentException("Duplicate active resource production rate.");
                    result.Add(new ProductionSlotRate { tier = rate.tier, index = rate.index,
                        resourceId = rate.resourceId, unitsPerMinute = rate.unitsPerMinute,
                        bufferCap = rate.bufferCap, reason = rate.reason });
                }
            return result;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
