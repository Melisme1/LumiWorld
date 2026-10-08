using System;
using System.Collections.Generic;

namespace LumiWorld.Acs
{
    public sealed class ResourceCatalogValidationResult
    {
        public readonly List<string> errors = new List<string>();
        public readonly List<string> warnings = new List<string>();
        public bool IsValid => errors.Count == 0;
    }

    public static class ResourceCatalogValidator
    {
        // Catalogs may contain any number of resources. Complete-reference validation is opt-in.
        public static ResourceCatalogValidationResult Validate(ResourceProductionCatalog catalog, bool requireCompleteDesignReference = false)
        {
            var result = new ResourceCatalogValidationResult();
            if (catalog == null) { result.errors.Add("Chưa chọn Resource Production Catalog."); return result; }
            ValidateSpecies(catalog.speciesRegistry, result);
            ValidateHabitats(catalog.habitats, result);
            ValidateResources(catalog, result);
            if (catalog.balance == null) result.errors.Add("Catalog chưa gán Balance.");
            else if (!catalog.balance.approvedForProduction)
                result.warnings.Add("Balance asset chưa duyệt. Production runtime lấy Settings trên ResourceProductionRuntime " +
                    "trong Hierarchy; kiểm tra component để biết cấu hình chạy thực tế.");
            else if (!catalog.balance.TryValidateForProduction(out string reason))
                result.errors.Add("Balance: " + reason);
            ValidateDesignReference(catalog, result, requireCompleteDesignReference);
            return result;
        }

        private static void ValidateSpecies(SpeciesMappingRegistry registry, ResourceCatalogValidationResult result)
        {
            if (registry == null || registry.entries == null)
            { result.errors.Add("Thiếu Species Registry hoặc Entries."); return; }
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var aliases = new Dictionary<string, SpeciesMappingEntry>(StringComparer.OrdinalIgnoreCase);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var cardIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SpeciesMappingEntry entry in registry.entries)
            {
                if (entry == null) { result.errors.Add("Species Registry có entry rỗng."); continue; }
                string label = "Species " + entry.gddKey + ": ";
                if (!ValidId(entry.gddKey) || !keys.Add(entry.gddKey)) result.errors.Add(label + "Key trống/thừa khoảng trắng/trùng.");
                CheckAlias(entry.gddKey, entry, aliases, result);
                CheckAlias(entry.displayName, entry, aliases, result);
                HabitatIsland island = HabitatDataRules.GetIsland(entry.primaryAffinity);
                if (island == HabitatIsland.None) result.errors.Add(label + "Primary Affinity không hợp lệ.");
                if (entry.secondaryAffinity != BiomeAffinity.None &&
                    (entry.secondaryAffinity == entry.primaryAffinity || HabitatDataRules.GetIsland(entry.secondaryAffinity) != island))
                    result.errors.Add(label + "Secondary Affinity phải khác primary và cùng đảo.");
                if (!Enum.IsDefined(typeof(SpeciesIntegrationStatus), entry.integrationStatus))
                    result.errors.Add(label + "Integration Status không hợp lệ.");
                if (!entry.IsActive) continue;
                if (entry.species == null) { result.errors.Add(label + "Chưa gán Species."); continue; }
                if (!ValidId(entry.SpeciesId) || !ids.Add(entry.SpeciesId)) result.errors.Add(label + "Species ID trống/thừa khoảng trắng/trùng.");
                if (entry.SpeciesId == "cow" || entry.SpeciesId == "fox") result.errors.Add(label + "Cow/Fox là test, không nằm trong roster active.");
                if (entry.creatureCard == null) { result.errors.Add(label + "Chưa gán Creature Card."); continue; }
                if (entry.creatureCard.cardType != CardType.Creature || entry.creatureCard.animalSpeciesData != entry.species)
                    result.errors.Add(label + "Card phải là Creature và trỏ đúng Species asset.");
                if (!ValidId(entry.creatureCard.cardID) || !cardIds.Add(entry.creatureCard.cardID))
                    result.errors.Add(label + "Card ID trống/thừa khoảng trắng/trùng.");
                if (entry.creatureCard.prefabToPlace == null) result.errors.Add(label + "Creature Card chưa gán Prefab To Place.");
                if (entry.creatureCard.cardImage == null) result.warnings.Add(label + "Creature Card chưa gán Card Image.");
            }
        }

        private static void CheckAlias(string alias, SpeciesMappingEntry entry,
            Dictionary<string, SpeciesMappingEntry> aliases, ResourceCatalogValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(alias)) return;
            if (aliases.TryGetValue(alias, out SpeciesMappingEntry previous) && previous != entry)
                result.errors.Add("Key/display name '" + alias + "' resolve tới nhiều species.");
            else aliases[alias] = entry;
        }

        private static void ValidateHabitats(List<NatureHabitatDefinition> habitats, ResourceCatalogValidationResult result)
        {
            if (habitats == null) { result.errors.Add("Thiếu danh sách Habitats."); return; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (NatureHabitatDefinition habitat in habitats)
            {
                if (habitat == null) { result.errors.Add("Habitats có phần tử rỗng."); continue; }
                string label = "Habitat " + habitat.name + ": ";
                if (!ValidId(habitat.NatureCardId) || !ids.Add(habitat.NatureCardId))
                    result.errors.Add(label + "Nature Card ID trống/thừa khoảng trắng/trùng.");
                if (habitat.natureCard == null || habitat.natureCard.cardType != CardType.Terrain)
                    result.errors.Add(label + "Nature Card phải tham chiếu CardData Terrain.");
                if (habitat.natureCard != null && habitat.natureCard.maxBiomeTilesOverride > 6)
                    result.errors.Add(label + "Nature Card override vượt giới hạn 6 hex.");
                if (habitat.island == HabitatIsland.None || HabitatDataRules.GetIsland(habitat.affinity) != habitat.island)
                    result.errors.Add(label + "Affinity/Island không khớp GDD.");
            }
        }

        private static void ValidateResources(ResourceProductionCatalog catalog, ResourceCatalogValidationResult result)
        {
            if (catalog.resources == null) { result.errors.Add("Thiếu danh sách Resources."); return; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ResourceProductionDefinition definition in catalog.resources)
            {
                if (definition == null) { result.errors.Add("Resources có definition rỗng."); continue; }
                string label = "Resource " + definition.ResourceId + ": ";
                if (definition.resource == null) { result.errors.Add(label + "Chưa gán ResourceData."); continue; }
                if (!ValidId(definition.ResourceId) || !ids.Add(definition.ResourceId)) result.errors.Add(label + "Resource ID trống/thừa khoảng trắng/trùng.");
                if (string.IsNullOrWhiteSpace(definition.resource.resourceName)) result.errors.Add(label + "Thiếu Resource Name.");
                if (definition.resource.icon == null) result.errors.Add(label + "Thiếu Icon.");
                if (definition.resource.maxStackSize <= 0) result.errors.Add(label + "Max Stack Size phải lớn hơn 0.");
                if (definition.tier != ResourceTier.Tier1 && definition.tier != ResourceTier.Tier2) result.errors.Add(label + "Tier không hợp lệ.");
                if (HabitatDataRules.GetIsland(definition.affinity) == HabitatIsland.None) result.errors.Add(label + "Affinity không hợp lệ.");
                bool hasHabitat = catalog.habitats != null && catalog.habitats.Exists(h => h != null && h.affinity == definition.affinity);
                if (definition.isEnabled && !hasHabitat) result.errors.Add(label + "Chưa có habitat cùng affinity trong catalog.");
                if (definition.requirements == null || definition.requirements.Count == 0)
                { result.errors.Add(label + "Thiếu Requirements."); continue; }
                if (definition.tier == ResourceTier.Tier1 && definition.requirements.Count != 1)
                    result.errors.Add(label + "T1 cần đúng một primary producer.");
                var requiredIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (SpeciesProductionRequirement requirement in definition.requirements)
                {
                    if (requirement == null) { result.errors.Add(label + "Requirement rỗng."); continue; }
                    if (requirement.minimumCount < 1) result.errors.Add(label + "Minimum Count phải ít nhất 1.");
                    if (requirement.role != AffinityRole.Primary && requirement.role != AffinityRole.Secondary)
                        result.errors.Add(label + "Role phải là Primary hoặc Secondary.");
                    if (!ValidId(requirement.gddSpeciesKey) || catalog.speciesRegistry == null ||
                        !catalog.speciesRegistry.TryGetByGddKey(requirement.gddSpeciesKey, out SpeciesMappingEntry entry))
                    { result.errors.Add(label + "Không resolve được GDD key '" + requirement.gddSpeciesKey + "'."); continue; }
                    if (definition.isEnabled && !entry.IsActive) result.errors.Add(label + "Required species đang pending.");
                    if (entry.IsActive && (!ValidId(entry.SpeciesId) || !requiredIds.Add(entry.SpeciesId)))
                        result.errors.Add(label + "Requirement trùng species hoặc thiếu Species ID.");
                    AffinityRole actualRole = definition.affinity == entry.primaryAffinity ? AffinityRole.Primary :
                        definition.affinity != BiomeAffinity.None && definition.affinity == entry.secondaryAffinity ? AffinityRole.Secondary : AffinityRole.None;
                    if (actualRole != requirement.role) result.errors.Add(label + "Sai role/affinity cho '" + requirement.gddSpeciesKey + "'.");
                    if (definition.tier == ResourceTier.Tier1 && requirement.role != AffinityRole.Primary)
                        result.errors.Add(label + "T1 không dùng secondary producer.");
                }
            }
        }

        private static void ValidateDesignReference(ResourceProductionCatalog catalog, ResourceCatalogValidationResult result,
            bool requireCompleteReference)
        {
            foreach (GddResourceDesignReference.SpeciesSpec spec in GddResourceDesignReference.Species)
            {
                if (catalog.speciesRegistry == null || !catalog.speciesRegistry.TryGetByGddKey(spec.Key, out SpeciesMappingEntry entry))
                {
                    if (requireCompleteReference) result.errors.Add("Bộ đối chiếu GDD thiếu species key " + spec.Key + ".");
                    continue;
                }
                if (!entry.IsActive && !requireCompleteReference) continue;
                if (!entry.IsActive || entry.SpeciesId != spec.Id || entry.primaryAffinity != spec.Primary || entry.secondaryAffinity != spec.Secondary)
                    result.errors.Add("Mapping/affinity sai theo GDD: " + spec.Key + " phải dùng " + spec.Id + ".");
            }
            string[] natureIds = { "leafwood_001", "bloomfield_001", "windheath_001" };
            BiomeAffinity[] affinities = { BiomeAffinity.Leafwood, BiomeAffinity.Bloomfield, BiomeAffinity.Windheath };
            for (int i = 0; i < natureIds.Length; i++)
            {
                if (!catalog.TryGetHabitat(natureIds[i], out NatureHabitatDefinition habitat))
                {
                    if (requireCompleteReference) result.errors.Add("Bộ đối chiếu GDD thiếu Nature mapping " + natureIds[i] + ".");
                    continue;
                }
                if (habitat.affinity != affinities[i] || habitat.island != HabitatIsland.Starter)
                    result.errors.Add("Nature mapping sai theo GDD: " + natureIds[i] + ".");
            }
            foreach (GddResourceDesignReference.ResourceSpec spec in GddResourceDesignReference.Resources)
            {
                ResourceProductionDefinition definition = catalog.FindResource(spec.Id);
                if (definition == null)
                {
                    if (requireCompleteReference) result.errors.Add("Bộ đối chiếu GDD thiếu resource được bật " + spec.Id + ".");
                    continue;
                }
                if (definition.affinity != spec.Affinity || definition.tier != spec.Tier)
                    result.errors.Add(spec.Id + ": sai affinity/tier theo GDD.");
                var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string key in spec.Primary) expected.Add(key + ":Primary");
                foreach (string key in spec.Secondary) expected.Add(key + ":Secondary");
                var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (definition.requirements != null)
                    foreach (SpeciesProductionRequirement requirement in definition.requirements)
                    {
                        if (requirement == null) continue;
                        if (requirement.minimumCount != 1) result.errors.Add(spec.Id + ": mỗi required species cần Minimum Count = 1.");
                        if (catalog.speciesRegistry != null && catalog.speciesRegistry.TryGetByGddKey(requirement.gddSpeciesKey, out SpeciesMappingEntry entry))
                            actual.Add(entry.gddKey + ":" + requirement.role);
                    }
                if (!actual.SetEquals(expected)) result.errors.Add(spec.Id + ": combo species/role không khớp GDD (AND).");
                string[] parts = spec.Id.Split('.');
                string expectedIconName = "lumi_t" + (int)spec.Tier + "_" + parts[1] + "_" + parts[2];
                if (definition.resource.icon != null && definition.resource.icon.name != expectedIconName)
                    result.errors.Add(spec.Id + ": Icon phải là " + expectedIconName + ".png.");
            }
        }

        private static bool ValidId(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Trim() == value;
        }
    }
}
