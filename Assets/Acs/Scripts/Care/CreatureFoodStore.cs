using System;
using System.Collections.Generic;

namespace LumiWorld.Acs
{
    public sealed class CreatureFoodStock
    {
        public string IndividualId { get; internal set; }
        // Current home, or last known home while the owner is absent.
        public string HabitatId { get; internal set; }
        public string ResourceId { get; internal set; }
        public long Amount { get; internal set; }
        public bool IsOrphan { get; internal set; }
    }

    // Session-owned food belonging to a stable creature ID, never to a habitat group.
    public sealed class CreatureFoodStore
    {
        private sealed class Pantry
        {
            public string individualId, habitatId;
            public bool orphan;
            public readonly Dictionary<string, long> amounts = new Dictionary<string, long>(StringComparer.Ordinal);
        }
        private readonly Dictionary<string, Pantry> pantries = new Dictionary<string, Pantry>(StringComparer.Ordinal);

        public bool IsActive(string individualId) => individualId != null &&
            pantries.TryGetValue(individualId, out var pantry) && !pantry.orphan;
        public bool IsResident(string habitatId, string individualId) => IsActive(individualId) &&
            !string.IsNullOrWhiteSpace(habitatId) && pantries[individualId].habitatId == habitatId;
        public long GetAmount(string individualId, string foodId) => individualId != null && foodId != null &&
            pantries.TryGetValue(individualId, out var pantry) && pantry.amounts.TryGetValue(foodId, out long amount) ? amount : 0;

        public bool TryAdd(string individualId, string foodId, int amount)
        {
            if (!IsActive(individualId) || string.IsNullOrWhiteSpace(foodId) || amount <= 0) return false;
            var pantry = pantries[individualId];
            pantry.amounts[foodId] = checked(GetAmount(individualId, foodId) + amount);
            return true;
        }
        // Orphan stock remains withdrawable; it is never assigned to a different creature.
        public bool TryTake(string individualId, string foodId, int amount)
        {
            if (amount <= 0 || GetAmount(individualId, foodId) < amount) return false;
            pantries[individualId].amounts[foodId] -= amount;
            return true;
        }
        public IReadOnlyList<CreatureFoodStock> Read(string individualId = null)
        {
            var result = new List<CreatureFoodStock>();
            foreach (var pantry in pantries.Values)
                if (individualId == null || pantry.individualId == individualId)
                    foreach (var food in pantry.amounts)
                        if (food.Value > 0) result.Add(new CreatureFoodStock
                        {
                            IndividualId = pantry.individualId, HabitatId = pantry.habitatId,
                            ResourceId = food.Key, Amount = food.Value, IsOrphan = pantry.orphan
                        });
            result.Sort((a, b) => a.IndividualId == b.IndividualId ? string.CompareOrdinal(a.ResourceId, b.ResourceId) :
                string.CompareOrdinal(a.IndividualId, b.IndividualId));
            return result.AsReadOnly();
        }

        // Merge/split/movement only updates the location of each owner. Validate the whole
        // membership snapshot before mutation so duplicate IDs cannot silently share a pantry.
        public void Synchronize(IReadOnlyList<HabitatState> habitats)
        {
            if (habitats == null) throw new ArgumentNullException(nameof(habitats));
            var homes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var habitat in habitats)
            {
                if (habitat == null || string.IsNullOrWhiteSpace(habitat.habitatId))
                    throw new ArgumentException("Habitat ID is required.");
                foreach (var resident in habitat.residents)
                {
                    if (resident == null || string.IsNullOrWhiteSpace(resident.individualId) ||
                        homes.ContainsKey(resident.individualId))
                        throw new ArgumentException("Resident ID missing or duplicated.");
                    homes.Add(resident.individualId, habitat.habitatId);
                }
            }
            foreach (var pantry in pantries.Values) pantry.orphan = true;
            foreach (var home in homes)
            {
                if (!pantries.TryGetValue(home.Key, out var pantry))
                {
                    pantry = new Pantry { individualId = home.Key };
                    pantries.Add(home.Key, pantry);
                }
                pantry.habitatId = home.Value;
                pantry.orphan = false;
            }
        }
    }
}
