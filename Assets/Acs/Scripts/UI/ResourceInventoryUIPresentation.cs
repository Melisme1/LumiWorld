using System;
using System.Collections.Generic;
using System.Text;

namespace LumiWorld.Acs
{
    public static class ResourceInventoryUIPresentation
    {
        public static List<ResourceUIItem> Warehouse(ResourceProductionCatalog catalog, Func<string, int> stock)
        {
            var items = new List<ResourceUIItem>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            if (catalog?.resources != null)
                foreach (var definition in catalog.resources)
                    if (definition != null && definition.isEnabled && definition.resource != null && seen.Add(definition.ResourceId))
                    {
                        var resource = definition.resource;
                        items.Add(new ResourceUIItem { id = definition.ResourceId, name = resource.resourceName,
                            icon = resource.icon, description = resource.description, biome = definition.affinity.ToString(),
                            tier = definition.tier == ResourceTier.Tier1 ? "T1" : "T2", capacity = resource.maxStackSize,
                            stock = stock != null ? stock(definition.ResourceId) : 0 });
                    }
            items.Sort((a,b) => { int biome = string.CompareOrdinal(a.biome, b.biome); return biome != 0 ? biome : string.CompareOrdinal(a.id,b.id); });
            return items;
        }

        public static ResourceUIHabitat Habitat(HabitatState habitat, HabitatProductionState state,
            ResourceProductionCatalog catalog, ProductionRuntimeSettings settings)
        {
            if (habitat == null) return null;
            var model = new ResourceUIHabitat { id = habitat.habitatId, title = habitat.affinity.ToString(),
                hexCount = habitat.HexCount, residentCount = habitat.residents.Count, capacity = habitat.CreatureCapacity };
            var names = new List<string>();
            foreach (var resident in habitat.residents)
            {
                string name = catalog.speciesRegistry.TryGetActiveBySpeciesId(resident.speciesId, out var mapping) ? mapping.displayName : resident.speciesId;
                names.Add(name + " · " + (resident.role == AffinityRole.Primary ? "Chính" : "Phụ") + " · " + resident.qualityRank.ToString().Replace("Rank", ""));
            }
            model.residents = names.Count == 0 ? "Chưa có cư dân." : string.Join("\n", names);
            model.hint = habitat.HexCount < 3 ? "Cần 3 hex để sản xuất. Hàng đã có vẫn thu được." : "T1: loài chính · T2: đủ combo cùng nhóm";
            foreach (var slot in habitat.slots)
            {
                var definition = HabitatEligibilityService.FindDefinition(catalog, slot.selectedResourceId);
                bool unlocked = HabitatEligibilityService.IsSlotUnlocked(habitat, slot.tier, slot.index);
                var rate = state?.FindRate(slot.tier, slot.index);
                var eligibility = HabitatEligibilityService.EvaluateSelection(habitat, slot.tier, slot.index, slot.selectedResourceId, catalog);
                model.slots.Add(new ResourceUISlot { tier = slot.tier, index = slot.index, unlocked = unlocked,
                    resourceId = slot.selectedResourceId, icon = definition?.resource?.icon,
                    name = definition?.resource?.resourceName ?? (string.IsNullOrEmpty(slot.selectedResourceId) ? "Chưa chọn" : slot.selectedResourceId),
                    rate = rate?.unitsPerMinute ?? 0,
                    reason = !unlocked ? "Mở ở " + ResourceInventoryUILayout.UnlockHexCount(slot.tier, slot.index) + " hex" :
                        rate?.reason ?? eligibility.Reason });
            }
            model.buffers = Buffers(state == null ? null : new[] { state }, catalog, settings);
            var key = new StringBuilder(model.id).Append('|').Append(model.hexCount).Append('|').Append(model.residents).Append('|').Append(model.hint);
            foreach (var slot in model.slots) key.Append('|').Append(slot.resourceId).Append('|').Append(slot.reason).Append('|').Append(slot.rate);
            foreach (var buffer in model.buffers) key.Append('|').Append(buffer.resourceId);
            model.structureKey = key.ToString(); return model;
        }

        public static List<ResourceUIOption> Options(HabitatState habitat, ResourceProductionCatalog catalog, ResourceTier tier, int index)
        {
            var result = new List<ResourceUIOption>(); if (habitat == null || catalog == null) return result;
            foreach (var definition in catalog.GetResources(habitat.affinity, tier))
            {
                var eligibility = HabitatEligibilityService.EvaluateSelection(habitat, tier, index, definition.ResourceId, catalog);
                result.Add(new ResourceUIOption { id = definition.ResourceId, name = definition.resource.resourceName,
                    icon = definition.resource.icon, requirements = Requirements(habitat, definition, catalog.speciesRegistry),
                    eligible = eligibility.IsEligible, reason = eligibility.Reason });
            }
            return result;
        }
        private static string Requirements(HabitatState habitat, ResourceProductionDefinition definition, SpeciesMappingRegistry registry)
        {
            var primary = new List<string>(); var secondary = new List<string>();
            foreach (var requirement in definition.requirements)
            {
                if (!registry.TryGetByGddKey(requirement.gddSpeciesKey, out var entry)) continue;
                var individuals = new HashSet<string>(); var homes = new HashSet<HexCoordinates>(); int count = 0;
                foreach (var resident in habitat.residents)
                    if (resident.speciesId == entry.SpeciesId && resident.role == requirement.role && habitat.ContainsHex(resident.homeHex) &&
                        !string.IsNullOrEmpty(resident.individualId) && individuals.Add(resident.individualId) && homes.Add(resident.homeHex)) count++;
                (requirement.role == AffinityRole.Primary ? primary : secondary).Add(entry.displayName + " " + count + "/" + requirement.minimumCount);
            }
            return "Chính: " + (primary.Count == 0 ? "—" : string.Join(", ", primary)) +
                "\nPhụ: " + (secondary.Count == 0 ? "—" : string.Join(", ", secondary));
        }

        public static List<ResourceUIBuffer> Buffers(IReadOnlyList<HabitatProductionState> states,
            ResourceProductionCatalog catalog, ProductionRuntimeSettings settings, bool recoveryOnly = false)
        {
            var result = new List<ResourceUIBuffer>(); if (states == null) return result;
            foreach (var state in states)
            {
                if (recoveryOnly && !state.isRecovery) continue;
                foreach (var buffer in state.buffers)
                {
                    var definition = HabitatEligibilityService.FindDefinition(catalog, buffer.resourceId);
                    result.Add(new ResourceUIBuffer { habitatId = state.habitatId, resourceId = buffer.resourceId,
                        name = definition?.resource?.resourceName ?? buffer.resourceId, icon = definition?.resource?.icon,
                        amount = buffer.amount, recovery = state.isRecovery,
                        cap = settings == null ? 0 : buffer.tier == ResourceTier.Tier1 ? settings.tier1BufferCap : settings.tier2BufferCap });
                }
            }
            return result;
        }
    }
}
