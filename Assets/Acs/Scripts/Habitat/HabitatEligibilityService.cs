using System;
using System.Collections.Generic;

namespace LumiWorld.Acs
{
    public sealed class HabitatEligibilityResult
    {
        public bool IsEligible { get; }
        public string Reason { get; }
        public HabitatEligibilityResult(bool eligible, string reason) { IsEligible = eligible; Reason = reason; }
    }

    public static class HabitatEligibilityService
    {
        public static int GetSlotCount(int hexCount, ResourceTier tier)
        {
            if (hexCount < 3 || hexCount > HabitatTopologyService.MaximumHexCount) return 0;
            if (tier == ResourceTier.Tier1) return hexCount == 3 ? 1 : 2;
            return tier == ResourceTier.Tier2 && hexCount >= 6 ? 1 : 0;
        }

        public static bool IsSlotUnlocked(HabitatState habitat, ResourceTier tier, int index)
        {
            return habitat != null && index >= 0 && index < GetSlotCount(habitat.HexCount, tier);
        }

        public static HabitatEligibilityResult EvaluateResource(HabitatState habitat,
            ResourceProductionDefinition definition, SpeciesMappingRegistry registry, Func<string, bool> canProduce = null)
        {
            if (habitat == null || registry == null) return Reject("Missing habitat or Species Registry.");
            if (definition == null || definition.resource == null) return Reject("No resource selected or missing definition.");
            if (!definition.isEnabled) return Reject("Resource is disabled.");
            if (definition.affinity != habitat.affinity || HabitatDataRules.GetIsland(definition.affinity) != habitat.island)
                return Reject("Resource does not match habitat biome or island.");
            if (GetSlotCount(habitat.HexCount, definition.tier) == 0)
                return Reject(definition.tier == ResourceTier.Tier2 ? "T2 requires a cluster of 6+ hexes." : "T1 requires a cluster of 3 to 12 hexes.");
            if (definition.requirements == null || definition.requirements.Count == 0 ||
                (definition.tier == ResourceTier.Tier1 && definition.requirements.Count != 1))
                return Reject("Invalid requirements.");

            var residentIds = new HashSet<string>(StringComparer.Ordinal);
            var residentHomes = new HashSet<HexCoordinates>();
            if (habitat.residents != null)
                foreach (var resident in habitat.residents)
                    if (resident != null && habitat.ContainsHex(resident.homeHex) &&
                        (!residentIds.Add(resident.individualId) || !residentHomes.Add(resident.homeHex)))
                        return Reject("Duplicate resident ID or multiple creatures on same home hex.");

            var requiredSpecies = new HashSet<string>(StringComparer.Ordinal);
            var missing = new List<string>();
            var resting = new List<string>();
            foreach (var requirement in definition.requirements)
            {
                if (requirement == null || requirement.minimumCount < 1 ||
                    !registry.TryGetByGddKey(requirement.gddSpeciesKey, out var entry) || !entry.IsActive ||
                    string.IsNullOrEmpty(entry.SpeciesId)) return Reject("Required species missing mapping or pending.");
                if (!requiredSpecies.Add(entry.SpeciesId)) return Reject("Requirements contain duplicate species.");
                if ((requirement.role != AffinityRole.Primary && requirement.role != AffinityRole.Secondary) ||
                    entry.GetRole(habitat.affinity) != requirement.role ||
                    (definition.tier == ResourceTier.Tier1 && requirement.role != AffinityRole.Primary))
                    return Reject("Species role does not match biome.");
                int count = 0;
                int residentCount = 0;
                var individuals = new HashSet<string>(StringComparer.Ordinal);
                var homes = new HashSet<HexCoordinates>();
                if (habitat.residents != null)
                    foreach (var resident in habitat.residents)
                        if (resident != null && resident.speciesId == entry.SpeciesId && resident.role == requirement.role &&
                            !string.IsNullOrEmpty(resident.individualId) && habitat.ContainsHex(resident.homeHex) &&
                            individuals.Add(resident.individualId) && homes.Add(resident.homeHex))
                        {
                            residentCount++;
                            if (canProduce == null || canProduce(resident.individualId)) count++;
                        }
                if (count < requirement.minimumCount)
                {
                    string name = string.IsNullOrEmpty(entry.displayName) ? entry.gddKey : entry.displayName;
                    if (canProduce != null && residentCount >= requirement.minimumCount)
                        resting.Add(name + " " + count + "/" + requirement.minimumCount);
                    else
                        missing.Add(name + " " + residentCount + "/" + requirement.minimumCount);
                }
            }
            if (missing.Count == 0 && resting.Count == 0) return new HabitatEligibilityResult(true, "Eligible.");
            string reason = missing.Count > 0 ? "Missing biome resident: " + string.Join(", ", missing) + "." : "";
            if (resting.Count > 0)
                reason += (reason.Length > 0 ? " " : "") + "Resident hungry/eating: " + string.Join(", ", resting) + ".";
            return Reject(reason);
        }

        public static HabitatEligibilityResult EvaluateSelection(HabitatState habitat, ResourceTier tier, int index,
            string resourceId, ResourceProductionCatalog catalog, Func<string, bool> canProduce = null)
        {
            if (!IsSlotUnlocked(habitat, tier, index)) return Reject("Slot is locked by hex count.");
            if (string.IsNullOrEmpty(resourceId)) return Reject("No resource selected.");
            var definition = FindDefinition(catalog, resourceId);
            if (definition == null || definition.tier != tier) return Reject("Resource not in catalog or wrong tier.");
            if (tier == ResourceTier.Tier1 && habitat.slots != null &&
                habitat.slots.Exists(s => s != null && s.tier == tier && s.index != index && s.selectedResourceId == resourceId))
                return Reject("Both T1 slots cannot select the same resource.");
            return EvaluateResource(habitat, definition, catalog != null ? catalog.speciesRegistry : null, canProduce);
        }

        public static bool TrySetSelection(HabitatState habitat, ResourceTier tier, int index, string resourceId,
            ResourceProductionCatalog catalog, out string reason)
        {
            var slot = habitat != null ? habitat.GetSlot(tier, index) : null;
            if (slot == null) { reason = "Slot does not exist."; return false; }
            if (string.IsNullOrEmpty(resourceId))
            {
                slot.selectedResourceId = string.Empty;
                reason = "Selection cleared.";
                return true;
            }
            var eligibility = EvaluateSelection(habitat, tier, index, resourceId, catalog);
            reason = eligibility.Reason;
            if (!eligibility.IsEligible) return false;
            slot.selectedResourceId = resourceId;
            return true;
        }

        public static ResourceProductionDefinition FindDefinition(ResourceProductionCatalog catalog, string resourceId)
        {
            return catalog != null && catalog.resources != null ?
                catalog.resources.Find(d => d != null && d.ResourceId == resourceId) : null;
        }

        private static HabitatEligibilityResult Reject(string reason) => new HabitatEligibilityResult(false, reason);
    }
}
