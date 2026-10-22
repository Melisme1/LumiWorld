using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using LumiWorld.Economy.Crafting;

namespace LumiWorld.Acs
{
    public static class ResourceInventoryUIPresentation
    {
        public static string LocalizeResourceName(string id, string fallback = null)
        {
            if (string.IsNullOrEmpty(id)) return fallback ?? "";
            string lower = id.ToLowerInvariant();
            if (lower.Contains("oak_wood") || lower.Contains("go_soi") || lower.Contains("oak")) return "Oak Wood";
            if (lower.Contains("sticks") || lower.Contains("canh_cay")) return "Sticks";
            if (lower.Contains("tender_leaves") || lower.Contains("la_non")) return "Tender Leaves";
            if (lower.Contains("raspberries") || lower.Contains("mam_xoi")) return "Raspberries";
            if (lower.Contains("acorns") || lower.Contains("hat_de")) return "Acorns";
            if (lower.Contains("fallen_bark") || lower.Contains("vo_cay")) return "Fallen Bark";
            if (lower.Contains("wild_daisies") || lower.Contains("hoa_cuc")) return "Wild Daisies";
            if (lower.Contains("dandelion_flowers") || lower.Contains("bo_cong_anh") || lower.Contains("dandelion")) return "Dandelion Flowers";
            if (lower.Contains("nectar") || lower.Contains("mat_hoa")) return "Pure Nectar";
            if (lower.Contains("plain.wheat") || lower.Contains("lua_mi_dong_co")) return "Meadow Wheat";
            if (lower.Contains("wheat") || lower.Contains("lua_mi")) return "Wheat";
            if (lower.Contains("lavender_sprigs") || lower.Contains("oai_huong") || lower.Contains("lavender")) return "Lavender Sprigs";
            if (lower.Contains("foxtail_plumes") || lower.Contains("co_duoi_cao") || lower.Contains("foxtail")) return "Foxtail Plumes";
            if (lower.Contains("lichen") || lower.Contains("dia_y")) return "Mountain Lichen";
            if (lower.Contains("rosemary") || lower.Contains("huong_thao")) return "Rosemary";
            if (lower.Contains("juniper_berries") || lower.Contains("qua_bach_xu")) return "Juniper Berries";
            if (lower.Contains("flax_stalks") || lower.Contains("than_lanh") || lower.Contains("flax")) return "Flax Stalks";
            if (lower.Contains("mountain_blueberries") || lower.Contains("viet_quat") || lower.Contains("blueberry")) return "Mountain Blueberries";
            if (lower.Contains("flour") || lower.Contains("bot_mi")) return "Wheat Flour";
            if (lower.Contains("bread") || lower.Contains("banh_mi")) return "Warm Bread";
            if (lower.Contains("berry_jam") || lower.Contains("mut_dau") || lower.Contains("jam")) return "Berry Jam";
            if (lower.Contains("paper") || lower.Contains("giay")) return "Craft Paper";
            if (lower.Contains("cloth") || lower.Contains("vai")) return "Woven Linen";
            if (lower.Contains("plank") || lower.Contains("van_go")) return "Wood Planks";
            if (lower.Contains("rope") || lower.Contains("day_thung")) return "Sturdy Rope";
            if (lower.Contains("honey") || lower.Contains("mat_ong")) return "Jar of Honey";
            if (lower.Contains("oil") || lower.Contains("tinh_dau")) return "Essential Oil";
            if (lower.Contains("cookie") || lower.Contains("banh_quy")) return "Sweet Cookies";
            return !string.IsNullOrEmpty(fallback) ? fallback : id;
        }

        public static string LocalizeResourceDescription(string id, string fallback = null)
        {
            if (string.IsNullOrEmpty(id)) return fallback ?? "";
            string lower = id.ToLowerInvariant();
            if (lower.Contains("oak_wood")) return "Sturdy oak timber harvested from ancient Leafwood trees.";
            if (lower.Contains("sticks")) return "Fallen branches gathered from the forest canopy, useful for crafting.";
            if (lower.Contains("tender_leaves")) return "Fresh, nutrient-rich young leaves favored by forest creatures.";
            if (lower.Contains("raspberries")) return "Juicy wild berries growing in quiet forest clearings.";
            if (lower.Contains("acorns")) return "Nutritious wild acorns dropped from towering oak crowns.";
            if (lower.Contains("fallen_bark")) return "Sturdy oak bark peeled naturally from aged trunks, rich in fiber.";
            if (lower.Contains("wild_daisies")) return "Charming blooming daisies from vibrant flower fields.";
            if (lower.Contains("dandelion_flowers")) return "Bright golden dandelion blooms rich in pollen.";
            if (lower.Contains("nectar")) return "Sweet aromatic floral nectar harvested by gentle breezes.";
            if (lower.Contains("wheat")) return "Golden grain stalks brimming with nutritious starch.";
            if (lower.Contains("lavender_sprigs")) return "Fragrant purple blossoms carrying a soothing scent.";
            if (lower.Contains("foxtail_plumes")) return "Silky grass plumes dancing across windswept plateaus.";
            if (lower.Contains("lichen")) return "Hardy mountain lichen clinging to rocky ridges.";
            if (lower.Contains("rosemary")) return "Fragrant aromatic herb thriving in breezy highlands.";
            if (lower.Contains("juniper_berries")) return "Crisp tart berries picked from highland shrubs.";
            if (lower.Contains("flax_stalks")) return "Fibrous stalks ideal for spinning thread and weaving cloth.";
            if (lower.Contains("mountain_blueberries")) return "Plump dark blueberries nurtured by cool mountain air.";
            return !string.IsNullOrEmpty(fallback) ? fallback : "Natural resources harvested from the Lumi ecosystem.";
        }

        public static string LocalizeBiomeName(string biome)
        {
            if (string.IsNullOrEmpty(biome)) return "Natural Land";
            string lower = biome.ToLowerInvariant();
            if (lower.Contains("leafwood")) return "Leafwood";
            if (lower.Contains("bloomfield")) return "Bloomfield";
            if (lower.Contains("windheath")) return "Windheath";
            if (lower.Contains("reedmarsh")) return "Reedmarsh";
            if (lower.Contains("lilywater")) return "Lilywater";
            if (lower.Contains("streamstone")) return "Streamstone";
            if (lower.Contains("rainleaf")) return "Rainleaf";
            if (lower.Contains("dong co") || lower.Contains("đồng cỏ") || lower.Contains("meadow") || lower.Contains("plain")) return "Meadows";
            if (lower.Contains("che bien") || lower.Contains("chế biến") || lower.Contains("craft") || lower.Contains("workshop")) return "Crafting";
            return biome;
        }

        public static List<ResourceUIItem> Warehouse(ResourceProductionCatalog catalog, Func<string, int> stock, bool onlyCollected = true)
        {
            var items = new List<ResourceUIItem>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            if (catalog?.resources != null)
                foreach (var definition in catalog.resources)
                    if (definition != null && definition.isEnabled && definition.resource != null && seen.Add(definition.ResourceId))
                    {
                        var resource = definition.resource;
                        int currentStock = stock != null ? stock(definition.ResourceId) : 0;
                        if (onlyCollected && currentStock <= 0) continue;
                        items.Add(new ResourceUIItem { id = definition.ResourceId, name = LocalizeResourceName(definition.ResourceId, resource.resourceName),
                            icon = resource.icon, description = LocalizeResourceDescription(definition.ResourceId, resource.description), biome = LocalizeBiomeName(definition.affinity.ToString()),
                            tier = definition.tier == ResourceTier.Tier1 ? "T1" : "T2", category = "Resource", capacity = resource.maxStackSize,
                            stock = currentStock, isUnlocked = currentStock > 0 });
                    }

            // Gộp thêm nguyên liệu thô ngoài sinh cảnh (Lúa mì, Vỏ cây rụng) và 15 thành phẩm chế tạo từ CraftingCatalog
            try
            {
                var extraItems = CraftingCatalog.GetWarehouseExtraItems();
                if (extraItems != null)
                {
                    foreach (var extra in extraItems)
                    {
                        if (seen.Add(extra.id))
                        {
                            int currentStock = stock != null ? stock(extra.id) : 0;
                            if (onlyCollected && currentStock <= 0) continue;

                            string category = "Resource";
                            var recipe = CraftingCatalog.FindByOutput(extra.id);
                            if (recipe != null)
                            {
                                if (recipe.category == CraftingCategory.Food) category = "Food";
                                else if (recipe.category == CraftingCategory.Herbal) category = "Medicine";
                                else category = "Resource";
                            }
                            else if (extra.tier == "Thức ăn" || extra.tier == "Food" || extra.id.Contains(".food."))
                            {
                                category = "Food";
                            }
                            else if (extra.tier == "Dược liệu" || extra.tier == "Herbal" || extra.tier == "Medicine" || extra.id.Contains(".herbal."))
                            {
                                category = "Medicine";
                            }

                            string biomeName = (category == "Food" || category == "Medicine" || extra.biome == "Chế biến" || extra.biome == "Crafting") ? "Crafting" : LocalizeBiomeName(extra.biome);

                            items.Add(new ResourceUIItem
                            {
                                id = extra.id,
                                name = LocalizeResourceName(extra.id, extra.name),
                                icon = extra.icon,
                                description = LocalizeResourceDescription(extra.id, extra.description),
                                biome = biomeName,
                                tier = extra.tier,
                                category = category,
                                capacity = extra.capacity,
                                stock = currentStock,
                                isUnlocked = currentStock > 0
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[LumiWorld Kho] Không thể nạp danh mục phụ chế tạo vào Kho: " + ex.Message);
            }

            int GetBiomeSortOrder(string biome)
            {
                if (string.Equals(biome, "Leafwood", StringComparison.OrdinalIgnoreCase)) return 1;
                if (string.Equals(biome, "Bloomfield", StringComparison.OrdinalIgnoreCase)) return 2;
                if (string.Equals(biome, "Windheath", StringComparison.OrdinalIgnoreCase)) return 3;
                if (string.Equals(biome, "Meadows", StringComparison.OrdinalIgnoreCase) || string.Equals(biome, "Đồng cỏ", StringComparison.OrdinalIgnoreCase)) return 4;
                if (string.Equals(biome, "Workshop", StringComparison.OrdinalIgnoreCase) || string.Equals(biome, "Chế biến", StringComparison.OrdinalIgnoreCase)) return 5;
                return 6;
            }

            items.Sort((a, b) =>
            {
                int orderA = GetBiomeSortOrder(a.biome);
                int orderB = GetBiomeSortOrder(b.biome);
                if (orderA != orderB) return orderA.CompareTo(orderB);
                int biomeComp = string.CompareOrdinal(a.biome, b.biome);
                return biomeComp != 0 ? biomeComp : string.CompareOrdinal(a.id, b.id);
            });
            return items;
        }

        public static List<ResourceUIItem> Collection(ResourceProductionCatalog catalog, Func<string, int> stock)
        {
            return Warehouse(catalog, stock, onlyCollected: false);
        }

        public static ResourceUIHabitat Habitat(HabitatState habitat, HabitatProductionState state,
            ResourceProductionCatalog catalog, ProductionRuntimeSettings settings, Func<string, CreatureCareView> readCare = null)
        {
            if (habitat == null) return null;
            var model = new ResourceUIHabitat { id = habitat.habitatId, title = LocalizeBiomeName(habitat.affinity.ToString()),
                hexCount = habitat.HexCount, residentCount = habitat.residents.Count, capacity = habitat.CreatureCapacity };
            var names = new List<string>();
            foreach (var resident in habitat.residents)
            {
                string name = catalog.speciesRegistry.TryGetActiveBySpeciesId(resident.speciesId, out var mapping) ? mapping.displayName : resident.speciesId;
                var care = readCare?.Invoke(resident.individualId);
                string condition = care == null ? "" : care.Status == CreatureHungerStatus.Hungry ? " · Hungry" :
                    care.Status == CreatureHungerStatus.Eating ? care.WaitingForFood ? " · Waiting for Food" : " · Eating" : " · Healthy";
                names.Add(name + " · " + (resident.role == AffinityRole.Primary ? "Primary" : "Secondary") + " · " + resident.qualityRank.ToString().Replace("Rank", "") + condition);
            }
            model.residents = names.Count == 0 ? "No residents." : string.Join("\n", names);
            model.hint = habitat.HexCount < 3 ? "Requires 3 hexes to produce. Existing goods can still be collected." : "T1: Primary species · T2: Group combo";
            foreach (var slot in habitat.slots)
            {
                var definition = HabitatEligibilityService.FindDefinition(catalog, slot.selectedResourceId);
                bool unlocked = HabitatEligibilityService.IsSlotUnlocked(habitat, slot.tier, slot.index);
                var rate = state?.FindRate(slot.tier, slot.index);
                var eligibility = HabitatEligibilityService.EvaluateSelection(habitat, slot.tier, slot.index, slot.selectedResourceId, catalog);
                model.slots.Add(new ResourceUISlot { tier = slot.tier, index = slot.index, unlocked = unlocked,
                    resourceId = slot.selectedResourceId, icon = definition?.resource?.icon,
                    name = LocalizeResourceName(slot.selectedResourceId, definition?.resource?.resourceName ?? (string.IsNullOrEmpty(slot.selectedResourceId) ? "Unselected" : slot.selectedResourceId)),
                    rate = rate?.unitsPerMinute ?? 0,
                    reason = !unlocked ? "Unlocks at " + ResourceInventoryUILayout.UnlockHexCount(slot.tier, slot.index) + " Hexes" :
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
            float baseDuration = tier == ResourceTier.Tier1 ? 10f : 30f;
            foreach (var definition in catalog.GetResources(habitat.affinity, tier))
            {
                var eligibility = HabitatEligibilityService.EvaluateSelection(habitat, tier, index, definition.ResourceId, catalog);
                float dur = baseDuration;
                try
                {
                    var recipe = LumiWorld.Economy.Crafting.CraftingCatalog.FindByOutput(definition.ResourceId);
                    if (recipe != null && recipe.craftDurationSeconds > 0)
                        dur = recipe.craftDurationSeconds;
                }
                catch { }

                string timeStr = FormatDuration(dur);
                string desc = (definition.resource != null) ? LocalizeResourceDescription(definition.ResourceId, definition.resource.description) : "";
                result.Add(new ResourceUIOption {
                    id = definition.ResourceId,
                    name = LocalizeResourceName(definition.ResourceId, definition.resource.resourceName),
                    icon = definition.resource.icon,
                    requirements = Requirements(habitat, definition, catalog.speciesRegistry),
                    eligible = eligibility.IsEligible,
                    reason = eligibility.Reason,
                    timeText = timeStr,
                    affinity = definition.affinity,
                    description = desc,
                    tier = definition.tier
                });
            }
            return result;
        }

        private static string FormatDuration(float seconds)
        {
            if (seconds <= 0) return "Instant";
            int total = Mathf.RoundToInt(seconds);
            int m = total / 60;
            int s = total % 60;
            if (m > 0)
            {
                return s > 0 ? $"{m}m {s:00}s" : $"{m}m";
            }
            return $"{s}s";
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
            return "Primary: " + (primary.Count == 0 ? "—" : string.Join(", ", primary)) +
                "\nSecondary: " + (secondary.Count == 0 ? "—" : string.Join(", ", secondary));
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
