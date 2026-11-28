using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;

namespace LumiWorld.Acs
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class HabitatRuntimeManager : MonoBehaviour
    {
        private static HabitatRuntimeManager instance;

        [Header("References")]
        [SerializeField] private ResourceProductionCatalog catalog = null;
        [SerializeField] private HexWorldGenerator worldGenerator;
        [Tooltip("Stable world key. Keep the same value when saving/restoring this map.")]
        [SerializeField] private string worldId = "mapbuilding";
        [Header("Observation")]
        [Tooltip("Fallback scan also detects manual removals and inactive models. Not a production timer.")]
        [Min(0.1f)] [SerializeField] private float refreshInterval = 0.5f;
        [SerializeField, HideInInspector] private List<HabitatState> habitats = new List<HabitatState>();
        [SerializeField, HideInInspector] private List<string> diagnostics = new List<string>();

        private bool dirty = true;
        private float nextRefresh;
        private string fingerprint;
        public ResourceProductionCatalog Catalog => catalog;
        public IReadOnlyList<HabitatState> Habitats => habitats;
        public IReadOnlyList<string> Diagnostics => diagnostics;
        public bool IsConfigured => catalog != null && catalog.speciesRegistry != null && !string.IsNullOrWhiteSpace(worldId);

        // Accrual settles its cached old rates before selection/topology mutations.
        public event Action<IReadOnlyList<HabitatState>> BeforeStateChanged;
        public event Action HabitatsChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance() => instance = null;

        public static bool TryGetActive(out HabitatRuntimeManager manager)
        {
            if (instance == null) instance = FindAnyObjectByType<HabitatRuntimeManager>();
            manager = instance;
            return manager != null && manager.isActiveAndEnabled && manager.IsConfigured;
        }

        public static void NotifyWorldChanged()
        {
            if (TryGetActive(out var manager)) manager.dirty = true;
        }

        public static void PrepareForWorldChange()
        {
            if (TryGetActive(out var manager))
            {
                manager.RefreshNow();
                manager.BeforeStateChanged?.Invoke(manager.habitats);
            }
        }

        private void OnEnable()
        {
            if (instance != null && instance != this && instance.isActiveAndEnabled)
            {
                Debug.LogError("[LumiWorld Habitat] Chỉ dùng một HabitatRuntimeManager cho map này.", this);
                enabled = false;
                return;
            }
            instance = this;
            dirty = true;
        }

        private void Start()
        {
            if (!IsConfigured) Debug.LogError("[LumiWorld Habitat] Gán ResourceProductionCatalog và World Id trước khi Play.", this);
            RefreshNow();
        }

        private void LateUpdate()
        {
            if (!dirty && Time.unscaledTime < nextRefresh) return;
            RefreshFromWorld(false);
            nextRefresh = Time.unscaledTime + Mathf.Max(0.1f, refreshInterval);
            dirty = false;
        }

        private void OnDisable() { if (instance == this) instance = null; }

        [ContextMenu("Refresh Habitats")]
        public void RefreshNow()
        {
            if (!Application.isPlaying) return;
            RefreshFromWorld(true);
        }

        private static void EnsureHybridCardConfigured(CardData card)
        {
            if (card == null) return;
            BreedingManager.EnsureCardFullyConfigured(card);
        }

        private void RefreshFromWorld(bool force)
        {
            if (!IsConfigured) return;
            if (worldGenerator == null) worldGenerator = FindAnyObjectByType<HexWorldGenerator>();
            diagnostics.Clear();
            if (worldGenerator == null || worldGenerator.MapTiles == null)
            {
                diagnostics.Add("Chưa tìm thấy HexWorldGenerator. Gán MapGenerator vào World Generator.");
                return;
            }

            var records = new List<PlacedCard>();
            var seenRecords = new HashSet<PlacedCard>();
            foreach (var pair in worldGenerator.MapTiles)
                if (pair.Value != null)
                    foreach (var record in pair.Value.GetComponentsInChildren<PlacedCard>(true))
                        if (record != null && record.cardData != null && seenRecords.Add(record)) records.Add(record);
            records.Sort((a, b) =>
            {
                int order = a.placedHex.Q.CompareTo(b.placedHex.Q);
                if (order == 0) order = a.placedHex.R.CompareTo(b.placedHex.R);
                return order == 0 ? string.CompareOrdinal(a.GetEntityId().ToString(), b.GetEntityId().ToString()) : order;
            });

            var identities = new Dictionary<PlacedCard, HabitatPlacementIdentity>();
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                if (record.CardType != CardType.Terrain && record.CardType != CardType.Creature) continue;
                var identity = HabitatPlacementIdentity.BindPlacedCard(record);
                while (!uniqueIds.Add(identity.PlacementId)) identity.RegenerateId();
                identities[record] = identity;
            }

            var members = new List<HabitatHexMember>();
            var terrainByHex = new Dictionary<HexCoordinates, HabitatHexMember>();
            foreach (var record in records)
            {
                if (record.CardType != CardType.Terrain || !catalog.TryGetHabitat(record.CardID, out var definition)) continue;
                if (record.GetComponentInParent<SettlementTile>() != null) continue;
                var identity = identities[record];
                if (terrainByHex.ContainsKey(identity.HomeHex))
                { diagnostics.Add("Hai Nature cards tại " + identity.HomeHex + "; kiểm tra placement."); continue; }
                if (record.clusterId <= 0) HexBiomeClusterConnector.Instance.AssignCluster(record);
                var member = new HabitatHexMember { placementId = identity.PlacementId, hex = identity.HomeHex,
                    islandId = worldId + ":" + (int)definition.island, natureCardId = record.CardID,
                    affinity = definition.affinity, island = definition.island, connectionClusterId = record.clusterId };
                if (worldGenerator.MapTiles.TryGetValue(identity.HomeHex, out var ground) && ground != null)
                    member.elevationLevel = ground.GetComponent<HexTileInfo>()?.ElevationLevel ?? -1;
                terrainByHex.Add(member.hex, member);
                members.Add(member);
            }

            var residents = new List<HabitatResident>();
            foreach (var record in records)
            {
                if (record.CardType != CardType.Creature) continue;
                EnsureHybridCardConfigured(record.cardData);
                var identity = identities[record];
                string speciesId = record.cardData.animalSpeciesData != null ? record.cardData.animalSpeciesData.speciesID : string.Empty;
                if (!terrainByHex.TryGetValue(identity.HomeHex, out var home) ||
                    !catalog.speciesRegistry.TryGetActiveBySpeciesId(speciesId, out var mapping) || mapping.GetRole(home.affinity) == AffinityRole.None)
                { diagnostics.Add(record.cardData.cardName + " tại " + identity.HomeHex + ": thiếu habitat/mapping hoặc sai affinity."); continue; }
                residents.Add(new HabitatResident { individualId = identity.PlacementId, speciesId = speciesId,
                    homeHex = identity.HomeHex, role = mapping.GetRole(home.affinity), qualityRank = identity.QualityRank });
            }

            string nextFingerprint = BuildFingerprint(members, residents);
            if (!force && nextFingerprint == fingerprint) return;
            BeforeStateChanged?.Invoke(habitats);
            var rebuilt = HabitatTopologyService.Rebuild(members, habitats);
            foreach (var habitat in rebuilt)
                foreach (var resident in residents)
                    if (habitat.ContainsHex(resident.homeHex)) habitat.residents.Add(resident);
            habitats = rebuilt;
            fingerprint = nextFingerprint;
            HabitatsChanged?.Invoke();
        }

        private static string BuildFingerprint(List<HabitatHexMember> members, List<HabitatResident> residents)
        {
            var value = new StringBuilder();
            foreach (var member in members)
                value.Append(member.placementId).Append('|').Append(member.hex.Q).Append(',').Append(member.hex.R)
                    .Append('|').Append(member.islandId).Append('|').Append(member.natureCardId).Append('|')
                    .Append((int)member.affinity).Append('|').Append(member.connectionClusterId)
                    .Append('|').Append(member.elevationLevel).Append(';');
            value.Append("Residents:");
            foreach (var resident in residents)
                value.Append(resident.individualId).Append('|').Append(resident.speciesId).Append('|')
                    .Append(resident.homeHex.Q).Append(',').Append(resident.homeHex.R).Append('|').Append((int)resident.role)
                    .Append('|').Append((int)resident.qualityRank).Append(';');
            return value.ToString();
        }

        public HabitatState FindHabitat(string habitatId) => habitats.Find(h => h != null && h.habitatId == habitatId);

        public List<BiomeFormationCandidate> GetFormationCandidates()
        {
            return WetBiomeFormation.FindCandidates(habitats.SelectMany(h => h.members), habitats);
        }

        public bool ConfirmFormation(string candidateKey)
        {
            if (WorldLaunch.IsVisit || SaveSync.SavesSuspended || WorldSaveSystem.Instance?.SavesBlocked == true) return false;
            RefreshNow();
            if (!WetBiomeFormation.TryConfirm(candidateKey, habitats, out var confirmed)) return false;
            BeforeStateChanged?.Invoke(habitats);
            habitats = confirmed;
            HabitatsChanged?.Invoke();
            WorldSaveSystem.MarkDirty();
            return true;
        }

        public HabitatState FindHabitatByHex(HexCoordinates hex) => habitats.Find(h => h != null && h.ContainsHex(hex));

        public bool HasCreatureAtHome(HexCoordinates hex)
        {
            foreach (var habitat in habitats)
                if (habitat.residents.Exists(r => r != null && r.homeHex == hex)) return true;
            // Also reserve homes of inactive/unmapped existing creatures before the next observer scan.
            if (worldGenerator != null && worldGenerator.MapTiles.TryGetValue(hex, out var tile) && tile != null)
                foreach (var record in tile.GetComponentsInChildren<PlacedCard>(true))
                    if (record != null && record.CardType == CardType.Creature)
                    {
                        var identity = record.GetComponent<HabitatPlacementIdentity>();
                        if ((identity != null ? identity.HomeHex : record.placedHex) == hex) return true;
                    }
            return false;
        }

        public bool CanPlaceCreature(CardData creature, CardData nature, HexCoordinates homeHex, out string reason)
        {
            if (!IsConfigured) { reason = "Creature placement is currently unavailable."; return false; }
            EnsureHybridCardConfigured(creature);
            if (creature == null || creature.cardType != CardType.Creature || creature.animalSpeciesData == null)
            { reason = "This creature card is not ready to be placed yet."; return false; }
            if (nature == null || !catalog.TryGetHabitat(nature.cardID, out var habitat))
            { reason = "Creatures cannot live on this type of land."; return false; }
            if (!catalog.speciesRegistry.TryGetActiveBySpeciesId(creature.animalSpeciesData.speciesID, out var mapping))
            { reason = "This creature cannot be placed on the island yet."; return false; }
            if (mapping.GetRole(habitat.affinity) == AffinityRole.None)
            { reason = $"This creature cannot live in {habitat.affinity} biomes."; return false; }
            var homeHabitat = FindHabitatByHex(homeHex);
            if (homeHabitat == null) { RefreshFromWorld(false); homeHabitat = FindHabitatByHex(homeHex); }
            if (homeHabitat != null && homeHabitat.IsFormedBiome)
            {
                int maxAllowed = homeHabitat.CreatureCapacity;
                if (homeHabitat.residents.Count >= maxAllowed)
                {
                    int nextMilestone = homeHabitat.HexCount < 3 ? 3 : (homeHabitat.HexCount < 6 ? 6 : 10);
                    reason = maxAllowed >= 6 ? "This biome is full (max 6 creatures)." : $"Expand this biome to {nextMilestone} connected tiles to add more creatures.";
                    return false;
                }
            }
            else
            {
                reason = habitat.island == HabitatIsland.Wet
                    ? "Form a 3, 6, or 10-hex biome triangle first to house a creature."
                    : "Connect at least 3 matching biome tiles to house a creature.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public bool TrySelectResource(string habitatId, ResourceTier tier, int index, string resourceId, out string reason)
        {
            RefreshNow();
            var habitat = FindHabitat(habitatId);
            if (habitat == null) { reason = "Habitat does not exist."; return false; }
            var eligibility = HabitatEligibilityService.EvaluateSelection(habitat, tier, index, resourceId, catalog);
            if (!string.IsNullOrEmpty(resourceId) && !eligibility.IsEligible) { reason = eligibility.Reason; return false; }
            BeforeStateChanged?.Invoke(habitats);
            if (!HabitatEligibilityService.TrySetSelection(habitat, tier, index, resourceId, catalog, out reason)) return false;
            HabitatsChanged?.Invoke();
            // Tài nguyên đã chọn nằm trong bản lưu đảo
            WorldSaveSystem.MarkDirty();
            return true;
        }

        // =========================================================
        // LƯU / NẠP (world_<id>.json, qua WorldSaveSystem)
        // =========================================================

        /// <summary>Ghi id habitat, thành viên và tài nguyên đã chọn của từng ô slot.</summary>
        public void CaptureForSave(List<HabitatSaveRecord> records)
        {
            records.Clear();
            foreach (var habitat in habitats)
            {
                if (habitat == null || string.IsNullOrEmpty(habitat.habitatId)) continue;
                var record = new HabitatSaveRecord { habitatId = habitat.habitatId, islandId = habitat.islandId,
                    confirmedFormation = habitat.confirmedFormation,
                    natureCardId = habitat.natureCardId, affinity = (int)habitat.affinity, island = (int)habitat.island };
                foreach (var member in habitat.members) if (member != null) record.memberIds.Add(member.placementId);
                foreach (var slot in habitat.slots)
                    if (slot != null) record.slots.Add(new HabitatSlotRecord { tier = (int)slot.tier, index = slot.index,
                        resourceId = slot.selectedResourceId });
                records.Add(record);
            }
        }

        /// <summary>
        /// Đặt habitat đã lưu làm "danh sách trước" trước lần dựng đầu tiên, để HabitatTopologyService.Rebuild
        /// giữ đúng id và tài nguyên đã chọn (khớp theo placementId thành viên) thay vì tạo id mới với slot trống.
        /// Gọi trước khi WorldSaveSystem dựng lại thẻ đã đặt.
        /// </summary>
        public void SeedFromSave(List<HabitatSaveRecord> records)
        {
            if (records == null || records.Count == 0) return;
            var seeded = new List<HabitatState>();
            foreach (var record in records)
            {
                if (record == null || string.IsNullOrEmpty(record.habitatId)) continue;
                var state = new HabitatState { habitatId = record.habitatId, islandId = record.islandId,
                    confirmedFormation = record.confirmedFormation,
                    natureCardId = record.natureCardId, affinity = (BiomeAffinity)record.affinity, island = (HabitatIsland)record.island };
                foreach (string id in record.memberIds ?? new List<string>())
                    if (!string.IsNullOrEmpty(id)) state.members.Add(new HabitatHexMember { placementId = id,
                        islandId = record.islandId, natureCardId = record.natureCardId,
                        affinity = state.affinity, island = state.island });
                foreach (var slot in record.slots ?? new List<HabitatSlotRecord>())
                    if (slot != null) state.slots.Add(new HabitatResourceSlot { tier = (ResourceTier)slot.tier, index = slot.index,
                        selectedResourceId = slot.resourceId ?? string.Empty });
                seeded.Add(state);
            }
            habitats = seeded;
            fingerprint = null;
            dirty = true;
        }

        [ContextMenu("Log Habitat Summary")]
        public void LogSummary()
        {
            foreach (var habitat in habitats)
                Debug.Log("[LumiWorld Habitat] " + habitat.affinity + " | " + habitat.HexCount + " hex | " +
                    habitat.residents.Count + "/" + habitat.CreatureCapacity + " creature | T1=" +
                    HabitatEligibilityService.GetSlotCount(habitat.HexCount, ResourceTier.Tier1) + " T2=" +
                    HabitatEligibilityService.GetSlotCount(habitat.HexCount, ResourceTier.Tier2) + " | ID=" + habitat.habitatId, this);
            foreach (string diagnostic in diagnostics) Debug.LogWarning("[LumiWorld Habitat] " + diagnostic, this);
        }
    }
}
