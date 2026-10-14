using System;
using System.Collections.Generic;
using UnityEngine;
using LumiWorld.Acs.TimeSystem;

namespace LumiWorld.Acs
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HabitatRuntimeManager))]
    [DefaultExecutionOrder(-90)]
    public sealed class ResourceProductionRuntime : MonoBehaviour
    {
        private static ResourceProductionRuntime instance;
        [SerializeField] private HabitatRuntimeManager habitatManager = null;
        [Tooltip("Balance chạy thực tế của map này. Chỉnh trên Hierarchy; component không ghi đè SO ProductionBalance.")]
        [SerializeField] private ProductionRuntimeSettings settings = new ProductionRuntimeSettings();
        [SerializeField] private CreatureCareSettings creatureCare = new CreatureCareSettings();
        [Tooltip("Chu kỳ cập nhật số hiển thị; sản lượng dùng đồng hồ gameplay chung, loại trừ quãng pause.")]
        [Min(0.1f)] [SerializeField] private float displayRefreshSeconds = 0.5f;
        [Tooltip("Kho dùng cho Collect. Có thể để trống để dùng ResourceInventory hiện có của game.")]
        [SerializeField] private ResourceInventory inventory = null;

        private ProductionLedger ledger;
        private CareProductionSimulation simulation;
        private GameTimeRuntime timeRuntime;
        private GameClock clock;
        private double ProductionSeconds => clock.Read().GameSeconds;
        private string settingsFingerprint;
        private string careFingerprint;
        private float nextDisplayRefresh;
        private bool subscribed;
        private bool applyingSettings;
        private bool collecting;
        private bool feeding;
        private readonly ResourceCollectionService collectionService = new ResourceCollectionService();
        // Keep a resolvable asset for withdrawing food after its diet/config entry is removed.
        private readonly Dictionary<string, ResourceData> knownFoodResources = new Dictionary<string, ResourceData>(StringComparer.Ordinal);
        public HabitatRuntimeManager HabitatManager => habitatManager;
        public ProductionRuntimeSettings Settings => settings;
        public CreatureCareSettings CareSettings => creatureCare;
        public ResourceInventory Inventory => inventory != null ? inventory : (inventory = ResourceInventory.Instance);
        public IReadOnlyList<HabitatProductionState> States => ledger != null ? ledger.States : Array.Empty<HabitatProductionState>();
        private string status = "Chưa Play.";
        public string Status
        {
            get => clock != null && clock.IsPaused ? "Game đang pause; giữ buffer, không tính quãng pause." : status;
            private set => status = value;
        }
        public event Action ProductionChanged;
        // Includes residency/status/progress changes; callers read snapshots rather than mutate state.
        public event Action ResidentsChanged;
        public ResourceCollectionReceipt LastCollection { get; private set; }
        public event Action<ResourceCollectionReceipt> ResourcesCollected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance() => instance = null;

        // Ownership suppresses seed production even when new settings are paused/invalid.
        public static bool ClaimsWorld
        {
            get
            {
                if (instance == null) instance = FindAnyObjectByType<ResourceProductionRuntime>();
                if (instance == null || !instance.isActiveAndEnabled) return false;
                var habitat = instance.habitatManager != null ? instance.habitatManager : instance.GetComponent<HabitatRuntimeManager>();
                return habitat != null;
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (instance != null && instance != this && instance.isActiveAndEnabled)
            { Debug.LogError("[LumiWorld Production] Chỉ dùng một ResourceProductionRuntime trong map.", this); enabled = false; return; }
            instance = this;
            timeRuntime = GameTimeRuntime.Instance;
            clock = timeRuntime.Clock;
            timeRuntime.PauseChanged += OnPauseChanged;
            if (habitatManager == null) habitatManager = GetComponent<HabitatRuntimeManager>();
            if (ledger == null) ledger = new ProductionLedger();
            if (simulation == null) simulation = new CareProductionSimulation(ledger, ProductionSeconds);
            simulation.AutoFeedingEnabled = !clock.IsPaused;
            if (habitatManager == null) return;
            habitatManager.BeforeStateChanged += BeforeHabitatChange;
            habitatManager.HabitatsChanged += Synchronize;
            subscribed = true;
            Synchronize();
        }

        private void Start()
        {
            if (habitatManager == null || !habitatManager.IsConfigured)
                Debug.LogError("[LumiWorld Production] Gán Habitat Manager có Catalog trước khi Play.", this);
            if (habitatManager != null) habitatManager.RefreshNow();
        }

        private void LateUpdate()
        {
            if (ledger == null || habitatManager == null) return;
            if (!habitatManager.isActiveAndEnabled || !habitatManager.IsConfigured)
            {
                simulation.Stop(ProductionSeconds);
                Status = "Habitat Manager đang tắt hoặc chưa cấu hình Catalog.";
                settingsFingerprint = null;
                return;
            }
            if (SettingsChanged()) ApplySettingsChange();
            if (Time.unscaledTime < nextDisplayRefresh) return;
            nextDisplayRefresh = Time.unscaledTime + Mathf.Max(0.1f, displayRefreshSeconds);
            SettleNow();
        }

        // Called by the custom Inspector immediately after editing runtime fields.
        public void ApplySettingsChange()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || applyingSettings || ledger == null) return;
            applyingSettings = true;
            try
            {
                simulation.AdvanceTo(ProductionSeconds); // Cached old rates/ranks until this instant.
                if (habitatManager != null) habitatManager.RefreshNow();
                Synchronize();
            }
            finally { applyingSettings = false; }
        }

        private bool SettingsChanged() => settingsFingerprint != JsonUtility.ToJson(settings) || careFingerprint != JsonUtility.ToJson(creatureCare);
        private void BeforeHabitatChange(IReadOnlyList<HabitatState> previous) => simulation?.AdvanceTo(ProductionSeconds);

        private void Synchronize()
        {
            if (ledger == null || habitatManager == null) return;
            string reason = "Thiếu Settings.";
            if (settings != null) settings.TryValidate(out reason);
            if (!habitatManager.IsConfigured || !habitatManager.isActiveAndEnabled)
            {
                simulation.Stop(ProductionSeconds);
                Status = "Habitat Manager đang tắt hoặc chưa cấu hình Catalog.";
                return;
            }
            Status = reason;
            if (creatureCare == null || !creatureCare.TryGetRules(out var careRules, out _))
            { simulation.Stop(ProductionSeconds); Status = "Cấu hình thời gian đói không hợp lệ; sửa Creature Care trong Inspector."; return; }
            if (!creatureCare.TryGetFoodRules(out var foodRules, out var foodReason))
            { simulation.Stop(ProductionSeconds); Status = "Cấu hình thức ăn: " + foodReason; return; }
            foreach (var diet in creatureCare.speciesDiets ?? new List<SpeciesDietSettings>())
                if (!habitatManager.Catalog.speciesRegistry.TryGetActiveBySpeciesId(diet.speciesId, out _))
                { simulation.Stop(ProductionSeconds); Status = "Species Diets dùng species ID chưa active: " + diet.speciesId; return; }
            foreach (var foodRule in foodRules.Foods)
            {
                var resource = ResolveResource(foodRule.ResourceId);
                if (resource == null)
                { simulation.Stop(ProductionSeconds); Status = "Thiếu ResourceData cho thức ăn " + foodRule.ResourceId; return; }
                knownFoodResources[foodRule.ResourceId] = resource;
            }
            // Immutable copy protects settlement from live Inspector edits before their boundary.
            var rateSettings = settings == null ? null : JsonUtility.FromJson<ProductionRuntimeSettings>(JsonUtility.ToJson(settings));
            simulation.Synchronize(habitatManager.Habitats, careRules,
                h => ProductionRateCalculator.Calculate(h, habitatManager.Catalog, rateSettings, simulation.Care.CanProduce), ProductionSeconds, foodRules);
            settingsFingerprint = JsonUtility.ToJson(settings);
            careFingerprint = JsonUtility.ToJson(creatureCare);
            PublishState();
        }

        [ContextMenu("Settle Production Now")]
        public void SettleNow()
        {
            if (!Application.isPlaying || ledger == null) return;
            simulation.AdvanceTo(ProductionSeconds);
            PublishState();
        }

        public HabitatProductionState FindState(string habitatId) => ledger?.Find(habitatId);

        public ResourceCollectionReceipt CollectHabitat(string habitatId) => string.IsNullOrWhiteSpace(habitatId) ?
            ResourceCollectionService.Failure(ResourceCollectionStatus.Unavailable, "Thiếu habitat ID.") : Collect(habitatId, null);
        public ResourceCollectionReceipt CollectResource(string habitatId, string resourceId) =>
            string.IsNullOrWhiteSpace(habitatId) || string.IsNullOrWhiteSpace(resourceId) ?
            ResourceCollectionService.Failure(ResourceCollectionStatus.Unavailable, "Thiếu habitat hoặc resource ID.") : Collect(habitatId, resourceId);
        public ResourceCollectionReceipt CollectAll() => Collect(null, null);
        public ResourceCollectionReceipt CollectRecovery() => Collect(null, null, true);

        // Void entry point for a future Unity Button; all callers share the same command.
        [ContextMenu("Collect All To Inventory")]
        public void CollectAllToInventory() => CollectAll();

        private ResourceCollectionReceipt Collect(string habitatId, string resourceId, bool recoveryOnly = false)
        {
            if (collecting || feeding) return ResourceCollectionService.Failure(ResourceCollectionStatus.Busy, "Đang xử lý thao tác trước.");
            if (!Application.isPlaying || ledger == null || !isActiveAndEnabled)
                return ResourceCollectionService.Failure(ResourceCollectionStatus.Unavailable, "Collect cần ResourceProductionRuntime đang bật trong Play Mode.");
            if (clock.IsPaused)
                return ResourceCollectionService.Failure(ResourceCollectionStatus.Unavailable, "Game đang pause. Resume trước khi thu.");
            collecting = true;
            try
            {
                // Observe edits/topology first. Cached old rates settle before each mutation.
                if (SettingsChanged()) ApplySettingsChange();
                if (habitatManager != null && habitatManager.isActiveAndEnabled && habitatManager.IsConfigured)
                    habitatManager.RefreshNow();
                else simulation.Stop(ProductionSeconds);
                simulation.AdvanceTo(ProductionSeconds);
                if (inventory == null) inventory = ResourceInventory.Instance;
                IReadOnlyList<HabitatProductionState> sources = States;
                if (recoveryOnly)
                {
                    var recovery = new List<HabitatProductionState>();
                    foreach (var state in States) if (state.isRecovery) recovery.Add(state);
                    sources = recovery;
                }
                LastCollection = collectionService.Collect(sources, inventory, ResolveResource, habitatId, resourceId);
                Debug.Log("[LumiWorld Collect] " + LastCollection.Message + " Nhận " + LastCollection.TotalTransferred +
                    "; còn chờ " + LastCollection.TotalRemaining + ". Action=" + LastCollection.ActionId, this);
                foreach (var item in LastCollection.Items)
                    Debug.Log("[LumiWorld Collect] " + item.ResourceName + ": +" + item.Transferred +
                        "; còn " + item.Remaining + "; " + item.Reason, this);
                // Events see both final stock and final source buffers.
                PublishState();
                var listeners = ResourcesCollected;
                if (listeners != null)
                    foreach (Action<ResourceCollectionReceipt> listener in listeners.GetInvocationList())
                        try { listener(LastCollection); }
                        catch (Exception exception) { Debug.LogException(exception, this); }
                return LastCollection;
            }
            finally { collecting = false; }
        }

        private ResourceData ResolveResource(string id)
        {
            if (creatureCare != null)
            {
                if (creatureCare.food != null && creatureCare.food.resourceID == id) return creatureCare.food;
                foreach (var food in creatureCare.additionalFoods ?? new List<CreatureFoodSettings>())
                    if (food?.resource != null && food.ResourceId == id) return food.resource;
            }
            var definition = HabitatEligibilityService.FindDefinition(habitatManager != null ? habitatManager.Catalog : null, id);
            var resource = definition != null && definition.resource != null ? definition.resource : ResourceCatalog.Find(id);
            if (resource == null && id != null) knownFoodResources.TryGetValue(id, out resource);
            return resource;
        }

        private void PublishState() { PublishSafely(ProductionChanged); PublishSafely(ResidentsChanged); }

        public CreatureCareView GetCareState(string individualId) => simulation?.Care.Read(individualId);

        public IReadOnlyList<CreatureResidentView> GetResidents(string habitatId)
        {
            var result = new List<CreatureResidentView>();
            var habitat = habitatManager != null ? habitatManager.FindHabitat(habitatId) : null;
            if (habitat == null || simulation == null) return result;
            foreach (var resident in habitat.residents)
            {
                SpeciesMappingEntry mapping = null;
                habitatManager.Catalog?.speciesRegistry?.TryGetActiveBySpeciesId(resident.speciesId, out mapping);
                result.Add(new CreatureResidentView { HabitatId = habitatId, IndividualId = resident.individualId,
                    SpeciesId = resident.speciesId, Name = mapping?.displayName ?? resident.speciesId,
                    Avatar = mapping?.creatureCard != null ? mapping.creatureCard.cardImage : null,
                    Role = resident.role, Care = simulation.Care.Read(resident.individualId),
                    IsWorking = !clock.IsPaused && simulation.IsWorking(resident.individualId) });
            }
            return result;
        }

        public CreatureFeedPreview PreviewFeed(string habitatId, string individualId)
        {
            var preview = simulation?.PreviewFeed(habitatId, individualId) ?? new CreatureFeedPreview();
            var habitat = habitatManager != null ? habitatManager.FindHabitat(habitatId) : null;
            if (!Application.isPlaying || !isActiveAndEnabled || simulation == null)
            { preview.CanFeed = false; preview.Reason = "Cần ResourceProductionRuntime đang hoạt động."; }
            else if (habitatManager == null || !habitatManager.isActiveAndEnabled || !habitatManager.IsConfigured)
            { preview.CanFeed = false; preview.Reason = "Habitat Manager đang tắt hoặc chưa cấu hình."; }
            else if (!simulation.IsRunning) { preview.CanFeed = false; preview.Reason = Status; }
            else if (creatureCare == null || !creatureCare.TryGetRules(out _, out _) || !creatureCare.TryGetFoodRules(out _, out _))
            { preview.CanFeed = false; preview.Reason = "Cấu hình độ no/thức ăn không hợp lệ."; }
            else if (clock.IsPaused) { preview.CanFeed = false; preview.Reason = "Game đang pause."; }
            else if (habitat == null || !habitat.residents.Exists(r => r.individualId == individualId))
            { preview.CanFeed = false; preview.Reason = "Cá thể không còn ở biome này."; }
            return preview;
        }

        public bool TryFeed(string habitatId, string individualId, out string reason)
        {
            reason = "Đang xử lý thao tác khác.";
            if (feeding || collecting || applyingSettings) return false;
            if (!Application.isPlaying || !isActiveAndEnabled || simulation == null || habitatManager == null)
            { reason = "Cần runtime đang hoạt động."; return false; }
            feeding = true;
            try
            {
                if (SettingsChanged()) ApplySettingsChange();
                habitatManager.RefreshNow(); simulation.AdvanceTo(ProductionSeconds);
                var preview = PreviewFeed(habitatId, individualId);
                reason = preview.Reason; if (!preview.CanFeed) return false;
                bool fed = simulation.TryFeed(habitatId, individualId, out reason);
                if (fed)
                {
                    simulation.RefreshRates(); PublishState();
                    Debug.Log("[LumiWorld Care] " + individualId + ": " + reason, this);
                }
                return fed;
            }
            finally { feeding = false; }
        }

        // Explicit creature names avoid silently accepting old habitat IDs as pantry owners.
        public IReadOnlyList<CreatureFoodStock> GetCreatureFoodStocks(string individualId)
            => string.IsNullOrWhiteSpace(individualId) ? Array.Empty<CreatureFoodStock>() :
                simulation?.Food.Read(individualId) ?? Array.Empty<CreatureFoodStock>();
        public IReadOnlyList<CreatureFoodStock> GetAllCreatureFoodStocks()
            => simulation?.Food.Read() ?? Array.Empty<CreatureFoodStock>();
        public long GetCreatureFoodAmount(string individualId, string resourceId)
            => simulation?.Food.GetAmount(individualId, resourceId) ?? 0;
        public IReadOnlyList<CreatureFoodRule> FoodTypes => simulation?.FoodRules?.Foods ?? Array.Empty<CreatureFoodRule>();

        // Deposit is all-or-nothing; withdrawal returns as much as Warehouse can accept.
        public bool DepositCreatureFood(string habitatId, string individualId, string resourceId, int amount, out string reason)
        {
            if (!PrepareFoodCommand(out reason)) return false;
            feeding = true;
            try
            {
                SettleFoodCommand();
                if (!simulation.IsRunning || creatureCare == null || !creatureCare.TryGetFoodRules(out _, out _) ||
                    !habitatManager.isActiveAndEnabled || habitatManager.FindHabitat(habitatId) == null)
                { reason = "Cấu hình thức ăn hoặc biome không hợp lệ."; return false; }
                bool done = CreatureFoodTransferService.Deposit(Inventory, simulation.Food, simulation.FoodRules,
                    habitatId, individualId, resourceId, amount, out reason);
                if (done) { simulation.RefreshActivity(); PublishState(); }
                return done;
            }
            finally { feeding = false; }
        }
        // Ownership is enough to withdraw, including stock whose creature has left the map.
        public int WithdrawCreatureFood(string individualId, string resourceId, int amount, out string reason)
        {
            if (!PrepareFoodCommand(out reason)) return 0;
            feeding = true;
            try
            {
                SettleFoodCommand();
                int transferred = CreatureFoodTransferService.Withdraw(Inventory, simulation.Food,
                    ResolveResource(resourceId), individualId, amount, out reason);
                if (transferred > 0) PublishState();
                return transferred;
            }
            finally { feeding = false; }
        }
        private bool PrepareFoodCommand(out string reason)
        {
            reason = "Đang xử lý thao tác khác.";
            if (feeding || collecting || applyingSettings) return false;
            if (!Application.isPlaying || !isActiveAndEnabled || simulation == null || habitatManager == null)
            { reason = "Cần runtime đang hoạt động."; return false; }
            if (clock.IsPaused) { reason = "Game đang pause."; return false; }
            return true;
        }
        private void SettleFoodCommand()
        {
            if (SettingsChanged()) ApplySettingsChange();
            habitatManager.RefreshNow(); simulation.AdvanceTo(ProductionSeconds);
        }

        private void PublishSafely(Action listeners)
        {
            if (listeners == null) return;
            foreach (Action listener in listeners.GetInvocationList())
                try { listener(); }
                catch (Exception exception) { Debug.LogException(exception, this); }
        }

        [ContextMenu("Log Production Summary")]
        public void LogSummary()
        {
            SettleNow();
            Debug.Log("[LumiWorld Production] " + Status, this);
            foreach (var state in States)
            {
                Debug.Log("[LumiWorld Production] Habitat " + state.habitatId + (state.isRecovery ? " (recovery)" : ""), this);
                foreach (var rate in state.rates)
                    Debug.Log("[LumiWorld Production] " + rate.resourceId + ": " + rate.unitsPerMinute.ToString("0.###") +
                        "/phút; " + rate.reason, this);
                foreach (var buffer in state.buffers)
                    Debug.Log("[LumiWorld Production] Buffer " + buffer.resourceId + ": " + buffer.amount +
                        "; phần lẻ=" + buffer.fractionalCarry.ToString("0.###"), this);
            }
        }

        private void OnApplicationPause(bool paused) { if (ledger != null) SettleNow(); }
        private void OnApplicationFocus(bool focused) { if (ledger != null) SettleNow(); }
        private void OnPauseChanged(bool paused)
        {
            // Settle the last active segment first, then prevent new bites while paused.
            SettleNow();
            if (simulation != null)
            {
                simulation.AutoFeedingEnabled = !paused;
                if (!paused) { simulation.RefreshActivity(); PublishState(); }
            }
        }

        private void OnDisable()
        {
            if (Application.isPlaying && clock != null) simulation?.Stop(ProductionSeconds);
            if (timeRuntime != null) timeRuntime.PauseChanged -= OnPauseChanged;
            if (subscribed && habitatManager != null)
            {
                habitatManager.BeforeStateChanged -= BeforeHabitatChange;
                habitatManager.HabitatsChanged -= Synchronize;
            }
            subscribed = false;
            if (instance == this) instance = null;
        }
    }
}
