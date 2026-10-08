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
            return tier == ResourceTier.Tier2 && hexCount == 6 ? 1 : 0;
        }

        public static bool IsSlotUnlocked(HabitatState habitat, ResourceTier tier, int index)
        {
            return habitat != null && index >= 0 && index < GetSlotCount(habitat.HexCount, tier);
        }

        public static HabitatEligibilityResult EvaluateResource(HabitatState habitat,
            ResourceProductionDefinition definition, SpeciesMappingRegistry registry)
        {
            if (habitat == null || registry == null) return Reject("Thiếu habitat hoặc Species Registry.");
            if (definition == null || definition.resource == null) return Reject("Chưa chọn tài nguyên hoặc thiếu definition.");
            if (!definition.isEnabled) return Reject("Tài nguyên đang tắt.");
            if (definition.affinity != habitat.affinity || HabitatDataRules.GetIsland(definition.affinity) != habitat.island)
                return Reject("Tài nguyên không thuộc biome/đảo của nhóm.");
            if (GetSlotCount(habitat.HexCount, definition.tier) == 0)
                return Reject(definition.tier == ResourceTier.Tier2 ? "T2 cần nhóm đủ 6 hex." : "T1 cần nhóm từ 3 đến 6 hex.");
            if (definition.requirements == null || definition.requirements.Count == 0 ||
                (definition.tier == ResourceTier.Tier1 && definition.requirements.Count != 1))
                return Reject("Requirements không hợp lệ.");

            var residentIds = new HashSet<string>(StringComparer.Ordinal);
            var residentHomes = new HashSet<HexCoordinates>();
            if (habitat.residents != null)
                foreach (var resident in habitat.residents)
                    if (resident != null && habitat.ContainsHex(resident.homeHex) &&
                        (!residentIds.Add(resident.individualId) || !residentHomes.Add(resident.homeHex)))
                        return Reject("Cư dân trùng individual ID hoặc nhiều creature cùng home hex.");

            var requiredSpecies = new HashSet<string>(StringComparer.Ordinal);
            var missing = new List<string>();
            foreach (var requirement in definition.requirements)
            {
                if (requirement == null || requirement.minimumCount < 1 ||
                    !registry.TryGetByGddKey(requirement.gddSpeciesKey, out var entry) || !entry.IsActive ||
                    string.IsNullOrEmpty(entry.SpeciesId)) return Reject("Required species thiếu mapping hoặc đang pending.");
                if (!requiredSpecies.Add(entry.SpeciesId)) return Reject("Requirements trùng species.");
                if ((requirement.role != AffinityRole.Primary && requirement.role != AffinityRole.Secondary) ||
                    entry.GetRole(habitat.affinity) != requirement.role ||
                    (definition.tier == ResourceTier.Tier1 && requirement.role != AffinityRole.Primary))
                    return Reject("Species/primary/secondary không khớp biome.");
                int count = 0;
                var individuals = new HashSet<string>(StringComparer.Ordinal);
                var homes = new HashSet<HexCoordinates>();
                if (habitat.residents != null)
                    foreach (var resident in habitat.residents)
                        if (resident != null && resident.speciesId == entry.SpeciesId && resident.role == requirement.role &&
                            !string.IsNullOrEmpty(resident.individualId) && habitat.ContainsHex(resident.homeHex) &&
                            individuals.Add(resident.individualId) && homes.Add(resident.homeHex)) count++;
                if (count < requirement.minimumCount)
                    missing.Add((string.IsNullOrEmpty(entry.displayName) ? entry.gddKey : entry.displayName) +
                        " " + count + "/" + requirement.minimumCount);
            }
            return missing.Count == 0 ? new HabitatEligibilityResult(true, "Đủ điều kiện.") :
                Reject("Thiếu trong cùng nhóm: " + string.Join(", ", missing) + ".");
        }

        public static HabitatEligibilityResult EvaluateSelection(HabitatState habitat, ResourceTier tier, int index,
            string resourceId, ResourceProductionCatalog catalog)
        {
            if (!IsSlotUnlocked(habitat, tier, index)) return Reject("Slot đang khóa theo số hex.");
            if (string.IsNullOrEmpty(resourceId)) return Reject("Chưa chọn tài nguyên.");
            var definition = FindDefinition(catalog, resourceId);
            if (definition == null || definition.tier != tier) return Reject("Tài nguyên không có trong catalog hoặc sai tier.");
            if (tier == ResourceTier.Tier1 && habitat.slots != null &&
                habitat.slots.Exists(s => s != null && s.tier == tier && s.index != index && s.selectedResourceId == resourceId))
                return Reject("Hai slot T1 không được chọn cùng tài nguyên.");
            return EvaluateResource(habitat, definition, catalog != null ? catalog.speciesRegistry : null);
        }

        public static bool TrySetSelection(HabitatState habitat, ResourceTier tier, int index, string resourceId,
            ResourceProductionCatalog catalog, out string reason)
        {
            var slot = habitat != null ? habitat.GetSlot(tier, index) : null;
            if (slot == null) { reason = "Slot không tồn tại."; return false; }
            if (string.IsNullOrEmpty(resourceId))
            {
                slot.selectedResourceId = string.Empty;
                reason = "Đã bỏ lựa chọn.";
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
