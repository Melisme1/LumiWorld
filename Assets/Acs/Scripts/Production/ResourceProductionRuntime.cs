using System;
using System.Collections.Generic;
using UnityEngine;

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
        [Tooltip("Chu kỳ cập nhật số hiển thị; sản lượng dùng UTC elapsed, không dùng khoảng này làm timer.")]
        [Min(0.1f)] [SerializeField] private float displayRefreshSeconds = 0.5f;
        [Tooltip("Kho dùng cho Collect. Có thể để trống để dùng ResourceInventory hiện có của game.")]
        [SerializeField] private ResourceInventory inventory = null;

        private ProductionLedger ledger;
        private IProductionClock clock = new SystemProductionClock();
        private string settingsFingerprint;
        private float nextDisplayRefresh;
        private bool subscribed;
        private bool applyingSettings;
        private bool collecting;
        private readonly ResourceCollectionService collectionService = new ResourceCollectionService();
        public HabitatRuntimeManager HabitatManager => habitatManager;
        public ProductionRuntimeSettings Settings => settings;
        public ResourceInventory Inventory => inventory != null ? inventory : (inventory = ResourceInventory.Instance);
        public IReadOnlyList<HabitatProductionState> States => ledger != null ? ledger.States : Array.Empty<HabitatProductionState>();
        public string Status { get; private set; } = "Chưa Play.";
        public event Action ProductionChanged;
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
            if (habitatManager == null) habitatManager = GetComponent<HabitatRuntimeManager>();
            if (ledger == null) ledger = new ProductionLedger();
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
                ledger.Stop(clock.UtcSeconds);
                Status = "Habitat Manager đang tắt hoặc chưa cấu hình Catalog.";
                settingsFingerprint = null;
                return;
            }
            if (settingsFingerprint != JsonUtility.ToJson(settings)) ApplySettingsChange();
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
                ledger.Settle(clock.UtcSeconds); // Cached old rates, unaffected by edited fields.
                if (habitatManager != null) habitatManager.RefreshNow();
                Synchronize();
            }
            finally { applyingSettings = false; }
        }

        private void BeforeHabitatChange(IReadOnlyList<HabitatState> previous) => ledger?.Settle(clock.UtcSeconds);

        private void Synchronize()
        {
            if (ledger == null || habitatManager == null) return;
            string reason = "Thiếu Settings.";
            if (settings != null) settings.TryValidate(out reason);
            if (!habitatManager.IsConfigured || !habitatManager.isActiveAndEnabled)
            {
                ledger.Stop(clock.UtcSeconds);
                Status = "Habitat Manager đang tắt hoặc chưa cấu hình Catalog.";
                return;
            }
            Status = reason;
            ledger.Synchronize(habitatManager.Habitats,
                h => ProductionRateCalculator.Calculate(h, habitatManager.Catalog, settings), clock.UtcSeconds);
            settingsFingerprint = JsonUtility.ToJson(settings);
            ProductionChanged?.Invoke();
        }

        [ContextMenu("Settle Production Now")]
        public void SettleNow()
        {
            if (!Application.isPlaying || ledger == null) return;
            ledger.Settle(clock.UtcSeconds);
            ProductionChanged?.Invoke();
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
            if (collecting) return ResourceCollectionService.Failure(ResourceCollectionStatus.Busy, "Đang xử lý lần thu trước.");
            if (!Application.isPlaying || ledger == null || !isActiveAndEnabled)
                return ResourceCollectionService.Failure(ResourceCollectionStatus.Unavailable, "Collect cần ResourceProductionRuntime đang bật trong Play Mode.");
            collecting = true;
            try
            {
                // Observe edits/topology first. Cached old rates settle before each mutation.
                if (settingsFingerprint != JsonUtility.ToJson(settings)) ApplySettingsChange();
                if (habitatManager != null && habitatManager.isActiveAndEnabled && habitatManager.IsConfigured)
                    habitatManager.RefreshNow();
                else ledger.Stop(clock.UtcSeconds);
                ledger.Settle(clock.UtcSeconds);
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
                PublishSafely(ProductionChanged);
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
            var definition = HabitatEligibilityService.FindDefinition(habitatManager != null ? habitatManager.Catalog : null, id);
            return definition != null && definition.resource != null ? definition.resource : ResourceCatalog.Find(id);
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

        private void OnDisable()
        {
            if (Application.isPlaying) ledger?.Stop(clock.UtcSeconds);
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
