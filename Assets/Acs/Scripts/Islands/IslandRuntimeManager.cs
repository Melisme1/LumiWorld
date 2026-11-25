using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LumiWorld.Acs.Islands
{
    /// <summary>Owns island access and region selection; terrain, cards and dialogs retain their own owners.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class IslandRuntimeManager : MonoBehaviour
    {
        public static IslandRuntimeManager Instance { get; private set; }
        [SerializeField] private IslandCatalog catalog;
        private HexWorldGenerator world;
        private HexCameraController cameraController;
        private HabitatRuntimeManager habitats;
        private IslandAccessService access;
        private readonly Dictionary<string, IslandClusterBoundary> boundaries = new Dictionary<string, IslandClusterBoundary>();
        private float nextCheck;
        private bool ready;
        private bool grantingInitialContent;
        private string pendingDiscovery;
        public string ActiveIslandId { get; private set; } = "starter";
        public IslandCatalog Catalog => catalog;
        public event Action<string> ActiveIslandChanged;
        public event Action<string> AccessChanged;

        private void Awake()
        {
            Instance = this;
            foreach (var island in catalog.islands) boundaries.Add(island.id, island.CreateBoundary());
        }

        private IEnumerator Start()
        {
            world = FindAnyObjectByType<HexWorldGenerator>();
            cameraController = FindAnyObjectByType<HexCameraController>();
            while (WorldSaveSystem.Instance != null && !WorldSaveSystem.Instance.IsWorldReady) yield return null;
            if (WorldLaunch.IsVisit || world == null) yield break;
            HabitatRuntimeManager.TryGetActive(out habitats);
            access = new IslandAccessService(new EconomyIslandAccessStore(EconomySaveSystem.Instance, CurrencyWallet.Instance));
            access.Changed += OnAccessChanged;
            if (cameraController != null) cameraController.FocusFinished += OnFocusFinished;
            ready = true;
        }

        private void OnDestroy()
        {
            if (access != null) access.Changed -= OnAccessChanged;
            if (cameraController != null) cameraController.FocusFinished -= OnFocusFinished;
            if (Instance == this) Instance = null;
        }

        public bool IsUnlocked(string id) => id == "starter" || (access != null && access.GetStage(id) == IslandAccessStage.Unlocked);
        public IslandAccessStage Stage(string id) => id == "starter" ? IslandAccessStage.Unlocked : access?.GetStage(id) ?? IslandAccessStage.Hidden;
        public string IslandAt(HexCoordinates hex)
        {
            foreach (var pair in boundaries) if (pair.Value.AllowsLandHex(hex)) return pair.Key;
            return null;
        }
        public bool AllowsExpansion(HexCoordinates center)
        {
            return IsUnlocked(ActiveIslandId) && boundaries[ActiveIslandId].AllowsClusterCenter(center);
        }
        public GameObject[] ActiveTerrain => catalog.Find(ActiveIslandId)?.dryTiles;

        private void Update()
        {
            if (!ready || SaveSync.SavesSuspended || (WorldSaveSystem.Instance != null && WorldSaveSystem.Instance.SavesBlocked)) return;
            if (cameraController != null && !cameraController.IsCardDragging && !cameraController.IsFocusing &&
                !(HexPlacementController.Instance != null && HexPlacementController.Instance.IsInPlacementMode) &&
                cameraController.TryGetGroundFocus(out var focus))
            {
                string region = IslandAt(HexMetrics.WorldToHex(focus));
                // Keep the current deck across the sea; switch only inside the next island's land boundary.
                if (region != null && region != ActiveIslandId)
                {
                    ActiveIslandId = region;
                    CardDeskController.Instance?.SelectIsland(region);
                    ActiveIslandChanged?.Invoke(region);
                }
            }
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + .5f;
            foreach (var island in catalog.islands)
            {
                if (island.id == "starter") continue;
                if (IsUnlocked(island.id)) EnsureInitialContent(island);
                else if (Stage(island.id) == IslandAccessStage.Hidden && CanDiscover(island) &&
                    !GameDialogUI.IsAnyOpen && !EconomyHUD.IsWindowOpen &&
                    !(cameraController != null && (cameraController.IsCardDragging || cameraController.IsFocusing)))
                    access.Discover(island.id);
            }
        }

        public bool CanDiscover(IslandDefinition island)
        {
            if (habitats == null) return false;
            var predecessor = catalog.Find(island.prerequisiteIsland);
            if (predecessor == null || !IsUnlocked(predecessor.id)) return false;
            var groups = habitats.Habitats.Where(h => h.island == predecessor.habitatIsland).ToList();
            return island.discoveryBiomes != null && island.discoveryBiomes.All(a => groups.Any(h => h.affinity == a && h.HexCount >= 3)) &&
                groups.Sum(h => h.residents.Count) >= island.discoveryCreatures;
        }

        private void OnAccessChanged(string id, IslandAccessStage stage)
        {
            AccessChanged?.Invoke(id);
            if (stage != IslandAccessStage.Discovered) return;
            pendingDiscovery = id;
            FocusIsland(id);
        }

        public void FocusIsland(string id)
        {
            var island = catalog.Find(id);
            if (island == null || Stage(id) == IslandAccessStage.Hidden ||
                (cameraController != null && cameraController.IsCardDragging)) return;
            if (pendingDiscovery != id) pendingDiscovery = null;
            if (cameraController != null) cameraController.FocusOnTarget(HexMetrics.HexToWorldPosition(island.center, 0), 12f, 1.5f);
            else OnFocusFinished(true);
        }

        private void OnFocusFinished(bool arrived)
        {
            string id = pendingDiscovery;
            pendingDiscovery = null;
            if (arrived && id != null) RequestUnlock(id);
        }

        public void RequestUnlock(string id)
        {
            var island = catalog.Find(id);
            if (!ready || island == null || Stage(id) != IslandAccessStage.Discovered || GameDialogUI.IsAnyOpen) return;
            GameDialogUI.ShowConfirm($"Open {island.displayName} for {island.unlockCoins} Coins?\nYou can return to Starter and open it later.",
                () => {
                    var result = access.Unlock(id, island.unlockCoins);
                    if (result == IslandUnlockResult.Success) EnsureInitialContent(island);
                    else if (result != IslandUnlockResult.AlreadyUnlocked)
                        GameDialogUI.ShowNotice(result == IslandUnlockResult.InsufficientCoins ? "Not enough Coins. Your island remains discovered." : "Unable to save the purchase. Please try again.");
                }, title: island.displayName, confirmText: $"Open · {island.unlockCoins} Coins", cancelText: "Later");
        }

        private void EnsureInitialContent(IslandDefinition island)
        {
            if (grantingInitialContent) return;
            grantingInitialContent = true;
            try { GrantInitialContent(island); }
            finally { grantingInitialContent = false; }
        }

        private void GrantInitialContent(IslandDefinition island)
        {
            var hand = CardDeskController.Instance;
            if (hand == null || !hand.HandReady || island.startingNature == null || island.startingNature.Length == 0) return;
            var save = EconomySaveSystem.Instance;
            var record = save.Progress.islandAccess.Find(r => r.islandId == island.id);
            if (string.IsNullOrEmpty(record.startingNatureId))
            {
                record.startingNatureId = island.startingNature[UnityEngine.Random.Range(0, island.startingNature.Length)].cardID;
                save.MarkDirty();
                if (!save.SaveNow()) { record.startingNatureId = null; save.MarkDirty(); return; }
            }
            var nature = island.startingNature.FirstOrDefault(c => c != null && c.cardID == record.startingNatureId);
            if (nature == null) return;
            if (!record.initialLandCreated)
            {
                int elevation = 0;
                if (habitats.Catalog.TryGetHabitat(nature.cardID, out var definition))
                    elevation = definition.affinity == BiomeAffinity.Streamstone ? 2 : definition.affinity == BiomeAffinity.Rainleaf ? 1 : 0;
                world.CreateIslandCenter(island.center, island.clusterRadius, island.dryTiles, elevation);
                HexPlacementController.Instance?.RegisterClusterCenter(island.center);
                WorldSaveSystem.MarkDirty();
                // Persist land before marking completion; replay creates only missing hexes.
                if (WorldSaveSystem.Instance != null && !WorldSaveSystem.Instance.SaveNow()) return;
                record.initialLandCreated = true;
                save.MarkDirty();
            }
            if (record.initialHandGranted) return;
            int oldNature = hand.GetHeldCount(nature.cardID);
            int oldRain = hand.GetHeldCount(island.rainCard.cardID);
            hand.AddCardsWithoutSaving(nature, 3);
            hand.AddCardsWithoutSaving(island.rainCard, island.startingRain);
            record.initialHandGranted = hand.GetHeldCount(nature.cardID) == oldNature + 3 &&
                hand.GetHeldCount(island.rainCard.cardID) == oldRain + island.startingRain;
            save.MarkDirty();
            if (!record.initialHandGranted || !save.SaveNow())
            {
                record.initialHandGranted = false;
                hand.TakeCardsWithoutSaving(nature.cardID, hand.GetHeldCount(nature.cardID) - oldNature);
                hand.TakeCardsWithoutSaving(island.rainCard.cardID, hand.GetHeldCount(island.rainCard.cardID) - oldRain);
            }
        }
    }
}
