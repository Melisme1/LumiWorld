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

        private SpeciesMappingEntry EnsureBraluffEntry()
        {
            if (entries == null) entries = new List<SpeciesMappingEntry>();
            foreach (var e in entries)
            {
                if (e != null && (string.Equals(e.gddKey, "braluff", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(e.SpeciesId, "braluff", StringComparison.OrdinalIgnoreCase)))
                {
                    e.primaryAffinity = BiomeAffinity.Leafwood;
                    e.secondaryAffinity = BiomeAffinity.Bloomfield;
                    e.tertiaryAffinity = BiomeAffinity.Windheath;
                    e.integrationStatus = SpeciesIntegrationStatus.Active;
                    if (e.species == null) e.species = BreedingManager.GetOrCreateBraluffSpecies();
                    if (e.creatureCard == null) e.creatureCard = BreedingManager.GetOrCreateBraluffCard();
                    return e;
                }
            }

            var braluffEntry = new SpeciesMappingEntry
            {
                gddKey = "braluff",
                displayName = "Braluff",
                species = BreedingManager.GetOrCreateBraluffSpecies(),
                creatureCard = BreedingManager.GetOrCreateBraluffCard(),
                primaryAffinity = BiomeAffinity.Leafwood,
                secondaryAffinity = BiomeAffinity.Bloomfield,
                tertiaryAffinity = BiomeAffinity.Windheath,
                integrationStatus = SpeciesIntegrationStatus.Active
            };
            entries.Add(braluffEntry);
            return braluffEntry;
        }

        // Resolve only explicit GDD keys/names. Runtime species IDs use a separate lookup.
        public bool TryGetByGddKey(string key, out SpeciesMappingEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(key)) return false;
            if (key.IndexOf("braluff", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                entry = EnsureBraluffEntry();
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
            if (speciesId.IndexOf("braluff", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                entry = EnsureBraluffEntry();
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
