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
        public SpeciesIntegrationStatus integrationStatus;

        public string SpeciesId => species != null ? species.speciesID : string.Empty;
        public bool IsActive => integrationStatus == SpeciesIntegrationStatus.Active;

        public AffinityRole GetRole(BiomeAffinity affinity)
        {
            if (!IsActive || affinity == BiomeAffinity.None) return AffinityRole.None;
            if (affinity == primaryAffinity) return AffinityRole.Primary;
            return affinity == secondaryAffinity ? AffinityRole.Secondary : AffinityRole.None;
        }
    }

    [CreateAssetMenu(fileName = "SpeciesMappingRegistry", menuName = "LumiWorld/Resources/Species Mapping Registry")]
    public sealed class SpeciesMappingRegistry : ScriptableObject
    {
        public List<SpeciesMappingEntry> entries = new List<SpeciesMappingEntry>();

        // Resolve only explicit GDD keys/names. Runtime species IDs use a separate lookup.
        public bool TryGetByGddKey(string key, out SpeciesMappingEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(key) || entries == null) return false;
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
            if (string.IsNullOrEmpty(speciesId) || entries == null) return false;
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
