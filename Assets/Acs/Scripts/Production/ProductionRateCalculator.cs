using System;
using System.Collections.Generic;

namespace LumiWorld.Acs
{
    // Configurable trial policy: share each individual's contribution over its eligible selections.
    // A capped buffer stays selected/eligible; reaching cap does not reallocate its contribution.
    public static class ProductionRateCalculator
    {
        private sealed class EligibleSlot
        {
            public ProductionSlotRate rate;
            public ResourceProductionDefinition definition;
            public Dictionary<string, AffinityRole> requiredSpecies = new Dictionary<string, AffinityRole>(StringComparer.Ordinal);
            public Dictionary<string, double> contributions = new Dictionary<string, double>(StringComparer.Ordinal);
        }

        public static List<ProductionSlotRate> Calculate(HabitatState habitat, ResourceProductionCatalog catalog,
            ProductionRuntimeSettings settings)
        {
            var result = new List<ProductionSlotRate>();
            if (habitat == null || habitat.slots == null) return result;
            string configReason = "Thiếu Settings.";
            bool configured = settings != null && settings.TryValidate(out configReason);
            var active = new List<EligibleSlot>();
            foreach (var slot in habitat.slots)
            {
                if (slot == null) continue;
                var rate = new ProductionSlotRate { tier = slot.tier, index = slot.index,
                    resourceId = slot.selectedResourceId, bufferCap = settings == null ? 0 :
                        slot.tier == ResourceTier.Tier1 ? settings.tier1BufferCap : settings.tier2BufferCap };
                result.Add(rate);
                var eligibility = HabitatEligibilityService.EvaluateSelection(habitat, slot.tier, slot.index,
                    slot.selectedResourceId, catalog);
                rate.reason = configured ? eligibility.Reason : configReason;
                if (!configured || !eligibility.IsEligible) continue;
                var eligible = new EligibleSlot { rate = rate,
                    definition = HabitatEligibilityService.FindDefinition(catalog, slot.selectedResourceId) };
                foreach (var requirement in eligible.definition.requirements)
                {
                    catalog.speciesRegistry.TryGetByGddKey(requirement.gddSpeciesKey, out var mapping);
                    eligible.requiredSpecies.Add(mapping.SpeciesId, requirement.role);
                    eligible.contributions.Add(mapping.SpeciesId, 0d);
                }
                active.Add(eligible);
            }
            if (!configured || active.Count == 0 || habitat.residents == null) return result;

            foreach (var resident in habitat.residents)
            {
                if (resident == null || string.IsNullOrEmpty(resident.speciesId) || !habitat.ContainsHex(resident.homeHex)) continue;
                var matching = active.FindAll(s => s.requiredSpecies.TryGetValue(resident.speciesId, out var role) && role == resident.role);
                if (matching.Count == 0) continue;
                double contribution = settings.GetContribution(resident.qualityRank);
                if (contribution <= 0)
                {
                    foreach (var rate in result) { rate.unitsPerMinute = 0; rate.reason = "Rank GDD của cư dân không hợp lệ."; }
                    return result;
                }
                double share = contribution / matching.Count;
                foreach (var match in matching) match.contributions[resident.speciesId] += share;
            }
            foreach (var slot in active)
            {
                double contribution = double.MaxValue;
                foreach (double speciesTotal in slot.contributions.Values) contribution = Math.Min(contribution, speciesTotal);
                slot.rate.unitsPerMinute = contribution * (slot.rate.tier == ResourceTier.Tier1 ?
                    settings.Tier1UnitsPerMinute : settings.Tier2UnitsPerMinute);
                slot.rate.reason = slot.rate.unitsPerMinute > 0 ? "Đang tích lũy." : "Chưa có contribution hợp lệ.";
            }
            return result;
        }
    }
}
