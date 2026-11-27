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
        public int elevationLevel = -1;
    }

    [Serializable]
    public sealed class HabitatResident
    {
        public string individualId;
        public string speciesId;
        public string customName;
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
        public bool confirmedFormation;
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

        /// <summary>
        /// True only when this cluster has formed an actual biome (Wet: explicitly confirmed 3/6/10; Starter: 3+ hexes).
        /// Unformed tiles (&lt;3 hex or unconfirmed Wet placements) must not behave as biomes or open Biome HUD.
        /// </summary>
        public bool IsFormedBiome => island == HabitatIsland.Wet
            ? (confirmedFormation && HexCount >= 3)
            : HexCount >= 3;

        public static int CalculateCreatureCapacity(int hexCount)
        {
            if (hexCount < 3) return 0;
            return hexCount < 6 ? 2 : hexCount < 10 ? 4 : 6;
        }

        public int CreatureCapacity => IsFormedBiome ? CalculateCreatureCapacity(HexCount) : 0;

        public void EnsureSlots()
        {
            if (slots == null) slots = new List<HabitatResourceSlot>();
            EnsureSlot(ResourceTier.Tier1, 0);
            EnsureSlot(ResourceTier.Tier1, 1);
            EnsureSlot(ResourceTier.Tier2, 0);
            EnsureSlot(ResourceTier.Tier2, 1);
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
