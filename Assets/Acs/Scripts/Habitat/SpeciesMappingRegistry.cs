using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs
{
    [Serializable]
    public sealed class SpeciesMappingEntry
    {
        [Tooltip("Key GDD viết thường, ví dụ frondle. Không điền animal alias leafbug.")]
        public string gddKey;
        public string displayName;
        public AnimalSpeciesData species;
        public CardData creatureCard;
        public BiomeAffinity primaryAffinity;
        public BiomeAffinity secondaryAffinity;
        public BiomeAffinity tertiaryAffinity;
        public SpeciesIntegrationStatus integrationStatus;

        public string SpeciesId => species != null ? species.speciesID : string.Empty;
        public bool IsActive => integrationStatus == SpeciesIntegrationStatus.Active;

        public AffinityRole GetRole(BiomeAffinity affinity)
        {
            if (!IsActive || affinity == BiomeAffinity.None) return AffinityRole.None;
            if (affinity == primaryAffinity) return AffinityRole.Primary;
            if (affinity == secondaryAffinity || affinity == tertiaryAffinity) return AffinityRole.Secondary;
            return AffinityRole.None;
        }
    }

    [CreateAssetMenu(fileName = "SpeciesMappingRegistry", menuName = "LumiWorld/Resources/Species Mapping Registry")]
    public sealed class SpeciesMappingRegistry : ScriptableObject
    {
        public List<SpeciesMappingEntry> entries = new List<SpeciesMappingEntry>();

        private static readonly (string key, string name, BiomeAffinity pri, BiomeAffinity sec, BiomeAffinity ter)[] s_HybridDefs = new[]
        {
            ("braluff",      "Braluff",      BiomeAffinity.Leafwood,    BiomeAffinity.Bloomfield,  BiomeAffinity.Windheath),
            ("bloomhorn",    "Bloomhorn",    BiomeAffinity.Bloomfield,  BiomeAffinity.Windheath,   BiomeAffinity.Leafwood),
            ("breezehorn",   "Breezehorn",   BiomeAffinity.Bloomfield,  BiomeAffinity.Windheath,   BiomeAffinity.Leafwood),
            ("featherglide", "Featherglide", BiomeAffinity.Windheath,   BiomeAffinity.Leafwood,    BiomeAffinity.Bloomfield),
            ("prismagale",   "Prismagale",   BiomeAffinity.Windheath,   BiomeAffinity.Leafwood,    BiomeAffinity.Bloomfield),
            ("fernbara",     "Fernbara",     BiomeAffinity.Rainleaf,    BiomeAffinity.Streamstone, BiomeAffinity.Reedmarsh),
            ("mossgarg",     "Mossgarg",     BiomeAffinity.Rainleaf,    BiomeAffinity.Streamstone, BiomeAffinity.Reedmarsh),
            ("lotriv",       "Lotriv",       BiomeAffinity.Lilywater,   BiomeAffinity.Reedmarsh,   BiomeAffinity.Streamstone),
            ("seraphin",     "Seraphin",     BiomeAffinity.Lilywater,   BiomeAffinity.Reedmarsh,   BiomeAffinity.Streamstone),
            ("raincroak",    "Raincroak",    BiomeAffinity.Rainleaf,    BiomeAffinity.Reedmarsh,   BiomeAffinity.Streamstone),
            ("quetzalcroak", "Quetzalcroak", BiomeAffinity.Rainleaf,    BiomeAffinity.Reedmarsh,   BiomeAffinity.Streamstone)
        };

        private SpeciesMappingEntry EnsureHybridEntry(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            if (entries == null) entries = new List<SpeciesMappingEntry>();

            string lk = key.ToLowerInvariant().Replace("creature_", "").Trim();
            foreach (var def in s_HybridDefs)
            {
                if (lk.Contains(def.key) || def.key.Contains(lk))
                {
                    foreach (var e in entries)
                    {
                        if (e != null && (string.Equals(e.gddKey, def.key, StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(e.SpeciesId, def.key, StringComparison.OrdinalIgnoreCase)))
                        {
                            e.primaryAffinity = def.pri;
                            e.secondaryAffinity = def.sec;
                            e.tertiaryAffinity = def.ter;
                            e.integrationStatus = SpeciesIntegrationStatus.Active;
                            if (e.species == null) e.species = BreedingManager.GetOrCreateHybridSpecies(def.key, def.name);
                            if (e.creatureCard == null) e.creatureCard = BreedingManager.ResolveCardSafe(def.name, def.key);
                            return e;
                        }
                    }

                    var newEntry = new SpeciesMappingEntry
                    {
                        gddKey = def.key,
                        displayName = def.name,
                        species = BreedingManager.GetOrCreateHybridSpecies(def.key, def.name),
                        creatureCard = BreedingManager.ResolveCardSafe(def.name, def.key),
                        primaryAffinity = def.pri,
                        secondaryAffinity = def.sec,
                        tertiaryAffinity = def.ter,
                        integrationStatus = SpeciesIntegrationStatus.Active
                    };
                    entries.Add(newEntry);
                    return newEntry;
                }
            }
            return null;
        }

        // Resolve only explicit GDD keys/names. Runtime species IDs use a separate lookup.
        public bool TryGetByGddKey(string key, out SpeciesMappingEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(key)) return false;
            var h = EnsureHybridEntry(key);
            if (h != null)
            {
                entry = h;
                return true;
            }
            if (entries == null) return false;
            foreach (SpeciesMappingEntry candidate in entries)
            {
                if (candidate != null &&
                    (string.Equals(candidate.gddKey, key, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(candidate.displayName, key, StringComparison.OrdinalIgnoreCase)))
                {
                    entry = candidate;
                    return true;
                }
            }
            return false;
        }

        public bool TryGetActiveBySpeciesId(string speciesId, out SpeciesMappingEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(speciesId)) return false;
            var h = EnsureHybridEntry(speciesId);
            if (h != null)
            {
                entry = h;
                return true;
            }
            if (entries == null) return false;
            foreach (SpeciesMappingEntry candidate in entries)
            {
                if (candidate != null && candidate.IsActive && candidate.SpeciesId == speciesId)
                {
                    entry = candidate;
                    return true;
                }
            }
            return false;
        }
    }
}
