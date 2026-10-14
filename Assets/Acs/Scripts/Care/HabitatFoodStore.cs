using System;
using System.Collections.Generic;

namespace LumiWorld.Acs
{
    public sealed class HabitatFoodStock
    {
        public string HabitatId { get; internal set; }
        public string ResourceId { get; internal set; }
        public long Amount { get; internal set; }
        public bool IsOrphan { get; internal set; }
    }

    // Session-owned biome pantry, separate from Warehouse and production buffers.
    public sealed class HabitatFoodStore
    {
        private sealed class Pantry
        {
            public string id, island, nature;
            public bool orphan;
            public readonly HashSet<string> members = new HashSet<string>(StringComparer.Ordinal);
            public readonly Dictionary<string, long> amounts = new Dictionary<string, long>(StringComparer.Ordinal);
        }
        private readonly Dictionary<string, Pantry> pantries = new Dictionary<string, Pantry>(StringComparer.Ordinal);
        public bool IsActive(string habitatId) => habitatId != null &&
            pantries.TryGetValue(habitatId, out var pantry) && !pantry.orphan;
        public long GetAmount(string habitatId, string foodId) => habitatId != null && foodId != null &&
            pantries.TryGetValue(habitatId, out var pantry) && pantry.amounts.TryGetValue(foodId, out long amount) ? amount : 0;
        public bool TryAdd(string habitatId, string foodId, int amount)
        {
            if (!IsActive(habitatId) || string.IsNullOrWhiteSpace(foodId) || amount <= 0) return false;
            var pantry = pantries[habitatId];
            pantry.amounts[foodId] = checked(GetAmount(habitatId, foodId) + amount);
            return true;
        }
        public bool TryTake(string habitatId, string foodId, int amount)
        {
            if (amount <= 0 || GetAmount(habitatId, foodId) < amount) return false;
            pantries[habitatId].amounts[foodId] -= amount; return true;
        }
        public IReadOnlyList<HabitatFoodStock> Read(string habitatId = null)
        {
            var result = new List<HabitatFoodStock>();
            foreach (var pantry in pantries.Values)
                if (habitatId == null || pantry.id == habitatId)
                    foreach (var food in pantry.amounts)
                        if (food.Value > 0) result.Add(new HabitatFoodStock
                        { HabitatId = pantry.id, ResourceId = food.Key, Amount = food.Value, IsOrphan = pantry.orphan });
            result.Sort((a, b) => a.HabitatId == b.HabitatId ? string.CompareOrdinal(a.ResourceId, b.ResourceId) :
                string.CompareOrdinal(a.HabitatId, b.HabitatId));
            return result.AsReadOnly();
        }

        // Match existing buffer ownership: a split retains the surviving ID's stock, a merge
        // moves removed IDs to greatest shared membership. Removed worlds keep withdrawable stock.
        public void Synchronize(IReadOnlyList<HabitatState> habitats)
        {
            var active = new Dictionary<string, HabitatState>(StringComparer.Ordinal);
            foreach (var habitat in habitats)
            {
                active.Add(habitat.habitatId, habitat);
                if (!pantries.ContainsKey(habitat.habitatId))
                    pantries.Add(habitat.habitatId, new Pantry { id = habitat.habitatId });
            }
            foreach (var old in new List<Pantry>(pantries.Values))
            {
                if (active.ContainsKey(old.id) || old.orphan) continue;
                HabitatState destination = null;
                int overlap = 0;
                foreach (var candidate in active.Values)
                {
                    if (old.island != candidate.islandId || old.nature != candidate.natureCardId) continue;
                    int shared = 0;
                    foreach (var member in candidate.members) if (old.members.Contains(member.placementId)) shared++;
                    if (shared > overlap || shared > 0 && shared == overlap &&
                        string.CompareOrdinal(candidate.habitatId, destination.habitatId) < 0)
                    { destination = candidate; overlap = shared; }
                }
                if (destination == null) { old.orphan = true; continue; }
                var target = pantries[destination.habitatId];
                foreach (var food in old.amounts)
                {
                    target.amounts.TryGetValue(food.Key, out long previous);
                    target.amounts[food.Key] = checked(previous + food.Value);
                }
                pantries.Remove(old.id);
            }
            foreach (var habitat in active.Values)
            {
                var pantry = pantries[habitat.habitatId];
                pantry.orphan = false; pantry.island = habitat.islandId; pantry.nature = habitat.natureCardId;
                pantry.members.Clear();
                foreach (var member in habitat.members) pantry.members.Add(member.placementId);
            }
        }
    }
}
