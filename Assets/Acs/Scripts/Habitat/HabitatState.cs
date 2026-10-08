using System;
using System.Collections.Generic;

namespace LumiWorld.Acs
{
    [Serializable]
    public sealed class HabitatHexMember
    {
        public string placementId;
        public HexCoordinates hex;
        public string islandId;
        public string natureCardId;
        public BiomeAffinity affinity;
        public HabitatIsland island;
        public int connectionClusterId;
    }

    [Serializable]
    public sealed class HabitatResident
    {
        public string individualId;
        public string speciesId;
        public HexCoordinates homeHex;
        public AffinityRole role;
        // GDD rank is independent of the legacy 1–5 stars/traits.
        public CreatureQualityRank qualityRank = CreatureQualityRank.RankI;
    }

    [Serializable]
    public sealed class HabitatResourceSlot
    {
        public ResourceTier tier;
        public int index;
        public string selectedResourceId = string.Empty;
    }

    [Serializable]
    public sealed class HabitatState
    {
        public string habitatId;
        public string islandId;
        public string natureCardId;
        public BiomeAffinity affinity;
        public HabitatIsland island;
        public List<HabitatHexMember> members = new List<HabitatHexMember>();
        public List<HabitatResident> residents = new List<HabitatResident>();
        // Keep locked slots as well, so shrinking a habitat does not erase its selections.
        public List<HabitatResourceSlot> slots = new List<HabitatResourceSlot>();

        public int HexCount => members != null ? members.Count : 0;
        public int CreatureCapacity => HexCount;

        public void EnsureSlots()
        {
            if (slots == null) slots = new List<HabitatResourceSlot>();
            EnsureSlot(ResourceTier.Tier1, 0);
            EnsureSlot(ResourceTier.Tier1, 1);
            EnsureSlot(ResourceTier.Tier2, 0);
        }

        private void EnsureSlot(ResourceTier tier, int index)
        {
            if (GetSlot(tier, index) == null) slots.Add(new HabitatResourceSlot { tier = tier, index = index });
        }

        public HabitatResourceSlot GetSlot(ResourceTier tier, int index)
        {
            return slots != null ? slots.Find(s => s != null && s.tier == tier && s.index == index) : null;
        }

        public bool ContainsHex(HexCoordinates hex)
        {
            return members != null && members.Exists(m => m != null && m.hex == hex);
        }
    }
}
