using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs
{
    [CreateAssetMenu(fileName = "ResourceProductionCatalog", menuName = "LumiWorld/Resources/Resource Production Catalog")]
    public sealed class ResourceProductionCatalog : ScriptableObject
    {
        public SpeciesMappingRegistry speciesRegistry;
        public ProductionBalanceConfig balance;
        public List<NatureHabitatDefinition> habitats = new List<NatureHabitatDefinition>();
        public List<ResourceProductionDefinition> resources = new List<ResourceProductionDefinition>();

        public bool TryGetHabitat(string exactNatureCardId, out NatureHabitatDefinition habitat)
        {
            habitat = null;
            if (string.IsNullOrEmpty(exactNatureCardId) || habitats == null) return false;
            foreach (NatureHabitatDefinition candidate in habitats)
            {
                if (candidate != null && string.Equals(candidate.NatureCardId, exactNatureCardId, StringComparison.Ordinal))
                { habitat = candidate; return true; }
            }
            return false;
        }

        public ResourceProductionDefinition FindResource(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId) || resources == null) return null;
            foreach (ResourceProductionDefinition definition in resources)
            {
                if (definition != null && definition.isEnabled && definition.ResourceId == resourceId)
                    return definition;
            }
            return null;
        }

        public IEnumerable<ResourceProductionDefinition> GetResources(BiomeAffinity affinity, ResourceTier tier)
        {
            if (resources == null) yield break;
            foreach (ResourceProductionDefinition definition in resources)
            {
                if (definition != null && definition.isEnabled && definition.affinity == affinity && definition.tier == tier)
                    yield return definition;
            }
        }
    }
}
