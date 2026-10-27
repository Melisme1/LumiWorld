using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
        public static ResourceProductionRuntime Instance => instance != null ? instance : (instance = FindAnyObjectByType<ResourceProductionRuntime>());
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
        private string disasterFingerprint;
        private HexWorldGenerator worldGenerator;
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
            worldGenerator = FindAnyObjectByType<HexWorldGenerator>();
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

            string currentDisasterFingerprint = BuildDisasterFingerprint();
            if (!string.Equals(currentDisasterFingerprint, disasterFingerprint, StringComparison.Ordinal))
            {
                // Settle elapsed time at the previous rates before applying new disaster penalties.
                simulation.AdvanceTo(ProductionSeconds);
                Synchronize();
                disasterFingerprint = currentDisasterFingerprint;
                return;
            }

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
                h => ApplyDisasterProductivity(h,
                    ProductionRateCalculator.Calculate(h, habitatManager.Catalog, rateSettings, simulation.Care.CanProduce)),
                ProductionSeconds, foodRules);
            settingsFingerprint = JsonUtility.ToJson(settings);
            careFingerprint = JsonUtility.ToJson(creatureCare);
            disasterFingerprint = BuildDisasterFingerprint();
            PublishState();
        }

        private List<ProductionSlotRate> ApplyDisasterProductivity(HabitatState habitat, List<ProductionSlotRate> rates)
        {
            if (rates == null || habitat == null || habitat.members == null || worldGenerator == null ||
                !DisasterRuntimeManager.TryGetActive(out var disasterManager)) return rates;

            float multiplier = 1f;
            foreach (HabitatHexMember member in habitat.members)
            {
                if (member == null || !worldGenerator.MapTiles.TryGetValue(member.hex, out GameObject tile) || tile == null)
                    continue;

                multiplier = Mathf.Min(multiplier, disasterManager.GetHarvestMultiplier(tile));
            }

            if (multiplier >= 0.999f) return rates;
            foreach (ProductionSlotRate rate in rates)
            {
                if (rate == null) continue;
                rate.unitsPerMinute *= multiplier;
                if (rate.unitsPerMinute > 0)
                    rate.reason = "Cyclone làm giảm năng suất môi trường còn " +
                        (multiplier * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%.";
            }
            return rates;
        }

        private string BuildDisasterFingerprint()
        {
            if (habitatManager == null || worldGenerator == null ||
                !DisasterRuntimeManager.TryGetActive(out var disasterManager)) return string.Empty;

            var fingerprint = new StringBuilder();
            foreach (HabitatState habitat in habitatManager.Habitats)
            {
                if (habitat == null || habitat.members == null) continue;
                fingerprint.Append(habitat.habitatId).Append(':');
                foreach (HabitatHexMember member in habitat.members)
                {
                    if (member == null || !worldGenerator.MapTiles.TryGetValue(member.hex, out GameObject tile) || tile == null)
                        continue;
                    fingerprint.Append(member.hex.Q).Append(',').Append(member.hex.R).Append('=')
                        .Append(Mathf.RoundToInt(disasterManager.GetHarvestMultiplier(tile) * 1000f)).Append(';');
                }
            }
            return fingerprint.ToString();
        }

        [ContextMenu("Settle Production Now")]
        public void SettleNow()
        {
            if (!Application.isPlaying || ledger == null) return;
            simulation.AdvanceTo(ProductionSeconds);
            PublishState();
        }

        public HabitatProductionState FindState(string habitatId) => ledger?.Find(habitatId);

        // =========================================================
        // LƯU / NẠP (world_<id>.json, qua WorldSaveSystem)
        // =========================================================

        // Hàng vừa chuyển từ buffer (bản lưu đảo) sang kho (bản lưu kinh tế): ghi cả hai ngay trong frame này.
        // Ghi đảo trước: nếu game tắt giữa hai lần ghi thì chỉ mất lượt hàng đó, không bao giờ nhân đôi.
        private static void PersistAfterCollect()
        {
            WorldSaveSystem world = WorldSaveSystem.Instance;
            if (world != null) world.SaveNow();
            EconomySaveSystem economy = EconomySaveSystem.HasInstance ? EconomySaveSystem.Instance : null;
            if (economy != null) economy.SaveNow();
        }

        /// <summary>Sản lượng tắt game được cộng tối đa 8 giờ.</summary>
        public const double MaxOfflineProductionSeconds = 8 * 3600;

        /// <summary>Ghi hàng chưa thu của từng habitat và độ no, thức ăn của từng sinh vật (đã settle tới giờ hiện tại).</summary>
        public void CaptureForSave(List<ProductionSaveRecord> production, List<CreatureSaveRecord> creatures)
        {
            production.Clear();
            creatures.Clear();
            if (ledger == null || simulation == null) return;
            if (Application.isPlaying) simulation.AdvanceTo(ProductionSeconds);

            foreach (var state in ledger.States)
            {
                if (state == null || string.IsNullOrEmpty(state.habitatId)) continue;
                var record = new ProductionSaveRecord { habitatId = state.habitatId, islandId = state.islandId,
                    natureCardId = state.natureCardId, isRecovery = state.isRecovery,
                    memberIds = new List<string>(state.memberIds) };
                foreach (var buffer in state.buffers)
                    if (buffer != null && (buffer.amount > 0 || buffer.fractionalCarry > 0))
                        record.buffers.Add(new ProductionBufferRecord { resourceId = buffer.resourceId, tier = (int)buffer.tier,
                            amount = buffer.amount, fractionalCarry = buffer.fractionalCarry });
                production.Add(record);
            }

            if (habitatManager == null) return;
            foreach (var habitat in habitatManager.Habitats)
                foreach (var resident in habitat.residents)
                {
                    var view = simulation.Care.Read(resident.individualId);
                    var record = new CreatureSaveRecord { individualId = resident.individualId,
                        qualityRank = (int)resident.qualityRank,
                        satiety = view != null ? view.Satiety : 100, status = view != null ? (int)view.Status : 0 };
                    foreach (var stock in simulation.Food.Read(resident.individualId))
                        record.foods.Add(new FoodAmountRecord { resourceId = stock.ResourceId, amount = stock.Amount });
                    creatures.Add(record);
                }
        }

        /// <summary>
        /// Nạp hàng chưa thu, độ no và thức ăn đã lưu trước khi đảo dựng lại thẻ (trước lần đồng bộ habitat đầu tiên),
        /// để ledger giữ đúng buffer theo id habitat và sinh vật không bị reset về no 100.
        /// </summary>
        public void SeedFromSave(List<ProductionSaveRecord> production, List<CreatureSaveRecord> creatures)
        {
            if (ledger == null || simulation == null) return;
            double now = ProductionSeconds;
            if (production != null)
                foreach (var record in production)
                {
                    if (record == null || string.IsNullOrEmpty(record.habitatId)) continue;
                    var state = new HabitatProductionState { habitatId = record.habitatId, islandId = record.islandId,
                        natureCardId = record.natureCardId, isRecovery = record.isRecovery,
                        memberIds = record.memberIds != null ? new List<string>(record.memberIds) : new List<string>() };
                    foreach (var buffer in record.buffers)
                        if (buffer != null && !string.IsNullOrEmpty(buffer.resourceId))
                            state.buffers.Add(new ResourceProductionBuffer { resourceId = buffer.resourceId,
                                tier = (ResourceTier)buffer.tier, amount = Math.Max(0, buffer.amount),
                                fractionalCarry = Math.Max(0, buffer.fractionalCarry) });
                    ledger.RestoreState(state, now);
                }

            if (creatures == null || creatureCare == null || !creatureCare.TryGetRules(out var careRules, out _)) return;
            foreach (var record in creatures)
            {
                if (record == null || string.IsNullOrEmpty(record.individualId)) continue;
                var rank = Enum.IsDefined(typeof(CreatureQualityRank), record.qualityRank)
                    ? (CreatureQualityRank)record.qualityRank : CreatureQualityRank.RankI;
                var status = Enum.IsDefined(typeof(CreatureHungerStatus), record.status)
                    ? (CreatureHungerStatus)record.status : CreatureHungerStatus.Healthy;
                simulation.Care.RestoreState(record.individualId, rank, careRules, record.satiety, status);
                foreach (var food in record.foods)
                    if (food != null) simulation.Food.RestoreAmount(record.individualId, food.resourceId, food.amount);
            }
        }

        /// <summary>
        /// Cộng sản lượng cho quãng tắt game (giờ thật, tối đa 8 giờ) theo tỉ lệ sau khi đảo đã dựng xong.
        /// Độ no đứng yên khi tắt game, nên chỉ cộng hàng, không trừ no.
        /// </summary>
        public void AccrueOffline(double offlineSeconds)
        {
            if (ledger == null || simulation == null || !simulation.IsRunning) return;
            double seconds = Math.Min(MaxOfflineProductionSeconds, Math.Max(0, offlineSeconds));
            if (seconds <= 0) return;
            simulation.AdvanceTo(ProductionSeconds);
            ledger.AccrueOffline(seconds, ProductionSeconds);
            Debug.Log($"[LumiWorld Production] Đã cộng sản lượng cho {seconds / 60:0} phút tắt game.", this);
            PublishState();
        }

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
                if (LastCollection.TotalTransferred > 0) PersistAfterCollect();
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
            // Đảo đang xem của người khác: không cho thú của họ ăn bằng kho của người xem
            if (WorldLaunch.IsVisit)
            {
                reason = "Không thể cho sinh vật trên đảo người khác ăn.";
                return false;
            }
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
