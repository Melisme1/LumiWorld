using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LumiWorld.Acs.TimeSystem;

/// <summary>
/// Quản lý Runtime của Hệ thống Thú Hoang Quậy Phá (Wild Pack Incident System):
/// - Tự động nhận diện cụm Biome từ HabitatRuntimeManager (GDD Acs) hoặc PlacedCard.
/// - Áp dụng quy tắc Cluster-First: Chọn Cụm Đầu Não (mốc 3-6-9) ➔ Chọn Loài tương thích ➔ Roll Rank theo Tiến trình (Milestone & Quy mô sinh thái).
/// - Sinh mô hình Alpha Leader tại Cụm Đầu Não (khóa sản xuất) và Minions tại các Cụm Vệ Tinh (làm chậm).
/// - Quản lý chu kỳ ngẫu nhiên bằng Time System lõi của dự án (LumiWorld.Acs.TimeSystem.GameTimeRuntime).
/// - Xử lý khi người chơi click tương tác để thu phục con Alpha và dẹp yên toàn bộ đàn thú trên bản đồ.
/// </summary>
public class WildPackIncidentManager : MonoBehaviour
{
    public const string IncidentSpawnTimerId = "wild_incident_spawn_timer";

    private static WildPackIncidentManager _instance;
    public static WildPackIncidentManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<WildPackIncidentManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("WildPackIncidentManager");
                    _instance = go.AddComponent<WildPackIncidentManager>();
                }
            }
            return _instance;
        }
    }

    [Header("1. Cấu hình trung tâm (Config)")]
    [SerializeField] private WildIncidentConfig config;

    [Header("2. Giám sát trạng thái sự cố (Time System)")]
    [SerializeField] private bool enableIncidents = true;
    [SerializeField] private float currentIntervalDuration = 240f;
    [SerializeField] private float internalTimer = 0f; // Fallback khi scene không có GameTimeRuntime
    [SerializeField] private int activeIncidentCount = 0;

    public bool AutoSpawnEnabled { get => enableIncidents; set => enableIncidents = value; }
    public float IntervalSeconds => currentIntervalDuration;

    public float RemainingCooldown
    {
        get
        {
            if (GameTimeRuntime.Instance != null && GameTimeRuntime.Instance.Timers != null)
            {
                var timers = GameTimeRuntime.Instance.Timers;
                if (timers.Contains(IncidentSpawnTimerId))
                {
                    return (float)timers.Read(IncidentSpawnTimerId).remainingSeconds;
                }
            }
            return Mathf.Max(0f, currentIntervalDuration - internalTimer);
        }
    }

    public float CurrentTimer => Mathf.Max(0f, currentIntervalDuration - RemainingCooldown);

    public float CooldownProgress
    {
        get
        {
            if (currentIntervalDuration <= 0f) return 0f;
            return Mathf.Clamp01(1f - (RemainingCooldown / currentIntervalDuration));
        }
    }

    public WildIncidentConfig Config => config;

    private readonly List<WildPackIncident> activeIncidents = new List<WildPackIncident>();
    public IReadOnlyList<WildPackIncident> ActiveIncidents => activeIncidents;

    private readonly HashSet<string> disruptedHabitatIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public event Action<WildPackIncident> OnIncidentSpawned;
    public event Action<WildPackIncident, bool> OnIncidentResolved;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        _ = Instance;
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (config == null)
        {
            config = Resources.Load<WildIncidentConfig>("WildIncidentConfig");
        }
    }

    private void Start()
    {
        // Khởi động chu kỳ ngẫu nhiên đầu tiên khi bắt đầu màn chơi
        StartNextSpawnCooldown();
    }

    /// <summary>
    /// Khởi động chu kỳ đếm giờ ngẫu nhiên mới bằng Time System của dự án.
    /// Thời gian ngẫu nhiên cân bằng: không quá nhanh (>= 3 phút) cũng không quá lâu (<= 5 phút).
    /// </summary>
    public void StartNextSpawnCooldown()
    {
        currentIntervalDuration = config != null ? config.GetRandomInterval() : 240f;
        internalTimer = 0f;

        if (GameTimeRuntime.Instance != null && GameTimeRuntime.Instance.Timers != null)
        {
            var timers = GameTimeRuntime.Instance.Timers;
            if (timers.Contains(IncidentSpawnTimerId))
            {
                timers.Cancel(IncidentSpawnTimerId);
            }
            // Sử dụng OfflineTimerPolicy.Freeze để không bị trừ lẹm khi pause/đóng game
            timers.StartCountdown(IncidentSpawnTimerId, currentIntervalDuration, OfflineTimerPolicy.Freeze);
        }
    }

    /// <summary>
    /// Đặt một chu kỳ chờ ngắn (retry) khi đồng hồ đã điểm nhưng bản đồ chưa đủ điều kiện (ví dụ chưa có cụm 3 ô).
    /// </summary>
    private void StartRetryCooldown(float retrySeconds)
    {
        currentIntervalDuration = retrySeconds;
        internalTimer = 0f;

        if (GameTimeRuntime.Instance != null && GameTimeRuntime.Instance.Timers != null)
        {
            var timers = GameTimeRuntime.Instance.Timers;
            if (timers.Contains(IncidentSpawnTimerId))
            {
                timers.Cancel(IncidentSpawnTimerId);
            }
            timers.StartCountdown(IncidentSpawnTimerId, retrySeconds, OfflineTimerPolicy.Freeze);
        }
    }

    private void Update()
    {
        // Đảo đang xem của người khác: không sinh đàn thú hoang
        if (WorldLaunch.IsVisit) return;

        if (!enableIncidents || config == null) return;

        // 1. Không đếm giờ sinh thêm nếu trên bản đồ đã có đàn thú đang quậy phá (chờ thu phục)
        if (activeIncidents.Count >= config.maxConcurrentIncidents)
        {
            return;
        }

        // 2. Kiểm tra tín hiệu thời gian từ Time System (hoặc Fallback)
        bool timerExpired = false;

        if (GameTimeRuntime.Instance != null && GameTimeRuntime.Instance.Timers != null)
        {
            var timers = GameTimeRuntime.Instance.Timers;
            if (!timers.Contains(IncidentSpawnTimerId))
            {
                StartNextSpawnCooldown();
            }
            else if (timers.TryConsumeSignals(IncidentSpawnTimerId, out _))
            {
                timerExpired = true;
            }
        }
        else
        {
            internalTimer += Time.deltaTime;
            if (internalTimer >= currentIntervalDuration)
            {
                internalTimer = 0f;
                timerExpired = true;
            }
        }

        // 3. Khi đồng hồ thời gian điểm -> Đánh giá điều kiện gameplay
        if (timerExpired)
        {
            if (CanSpawnOrganicIncident(out _))
            {
                TryTriggerOrganicIncident(out _, out _);
                // Lưu ý: Sau khi đàn thú xuất hiện, chu kỳ tiếp theo sẽ tự động được khởi tạo khi đàn thú này được giải quyết (OnIncidentResolved)
            }
            else
            {
                // Chưa đủ điều kiện (chưa mở khóa hoặc chưa có cụm 3 ô): Đợi 30 giây rồi thử lại
                StartRetryCooldown(30f);
            }
        }

        activeIncidentCount = activeIncidents.Count;
    }

    /// <summary>
    /// Thu thập danh sách toàn bộ các Cụm Biome / Habitat hiện có trên bản đồ:
    /// Hỗ trợ cả HabitatRuntimeManager (GDD Acs) hoặc PlacedCard.
    /// </summary>
    public List<IncidentClusterTarget> GetAllActiveClusters()
    {
        var result = new List<IncidentClusterTarget>();

        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();

        // 1. Kiểm tra HabitatRuntimeManager (Hệ thống GDD Acs chính quy trong scene)
        if (LumiWorld.Acs.HabitatRuntimeManager.TryGetActive(out var habitatManager))
        {
            if (habitatManager.Habitats != null && habitatManager.Habitats.Count > 0)
            {
                foreach (var habitat in habitatManager.Habitats)
                {
                    if (habitat == null) continue;

                    Vector3 sumPos = Vector3.zero;
                    int count = 0;
                    if (habitat.members != null)
                    {
                        foreach (var m in habitat.members)
                        {
                            if (m != null)
                            {
                                float tileSurfaceY = HexMetrics.TileHeight;
                                if (worldGen != null && worldGen.MapTiles.TryGetValue(m.hex, out GameObject tileObj) && tileObj != null)
                                {
                                    var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tileObj);
                                    tileSurfaceY = topY;
                                }
                                sumPos += HexMetrics.HexToWorldPosition(m.hex, tileSurfaceY);
                                count++;
                            }
                        }
                    }

                    Vector3 center = count > 0 ? sumPos / count : Vector3.zero;
                    string natureKey = habitat.affinity.ToString();
                    CardData terrain = FindMatchingTerrainCard(natureKey);

                    var target = new IncidentClusterTarget
                    {
                        ClusterId = habitat.habitatId,
                        BiomeName = habitat.affinity.ToString(),
                        HexCount = habitat.HexCount,
                        CenterPosition = center,
                        TerrainCard = terrain,
                        NatureKey = natureKey,
                        IsDisrupted = disruptedHabitatIds.Contains(habitat.habitatId),
                        SetDisruptedAction = (disrupted) => {
                            if (disrupted) disruptedHabitatIds.Add(habitat.habitatId);
                            else disruptedHabitatIds.Remove(habitat.habitatId);
                        },
                        SetSpeedMultiplierAction = (mult) => { }
                    };

                    result.Add(target);
                }

                if (result.Count > 0) return result;
            }
        }

        // 2. Fallback an toàn: Quét trực tiếp các PlacedCard trên scene
        var allPlaced = FindObjectsByType<PlacedCard>();
        if (allPlaced != null && allPlaced.Length > 0)
        {
            var groups = new Dictionary<string, List<PlacedCard>>(StringComparer.OrdinalIgnoreCase);
            foreach (var card in allPlaced)
            {
                if (card == null || card.cardData == null || card.CardType != CardType.Terrain) continue;
                string key = string.IsNullOrEmpty(card.cardData.cardID) ? card.cardData.cardName : card.cardData.cardID;
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<PlacedCard>();
                    groups[key] = list;
                }
                list.Add(card);
            }

            foreach (var kvp in groups)
            {
                var list = kvp.Value;
                Vector3 sumPos = Vector3.zero;
                foreach (var p in list)
                {
                    if (p != null)
                    {
                        float tileSurfaceY = p.transform.position.y;
                        if (worldGen != null && worldGen.MapTiles.TryGetValue(p.placedHex, out GameObject tileObj) && tileObj != null)
                        {
                            var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tileObj);
                            tileSurfaceY = topY;
                        }
                        sumPos += new Vector3(p.transform.position.x, tileSurfaceY, p.transform.position.z);
                    }
                }
                Vector3 center = list.Count > 0 ? sumPos / list.Count : Vector3.zero;

                var firstCard = list[0].cardData;
                var target = new IncidentClusterTarget
                {
                    ClusterId = kvp.Key,
                    BiomeName = firstCard.cardName,
                    HexCount = list.Count,
                    CenterPosition = center,
                    TerrainCard = firstCard,
                    NatureKey = firstCard.cardName,
                    SetDisruptedAction = (disrupted) => { },
                    SetSpeedMultiplierAction = (mult) => { }
                };
                result.Add(target);
            }
        }

        return result;
    }

    /// <summary>
    /// Kiểm tra xem thế giới game hiện tại có đủ điều kiện phát sinh sự cố tự nhiên theo Game Loop hay chưa.
    /// </summary>
    public bool CanSpawnOrganicIncident(out string reason)
    {
        if (!enableIncidents)
        {
            reason = "Cơ chế tự động sinh sự cố đang bị tắt (AutoSpawn = false).";
            return false;
        }

        if (config == null)
        {
            config = Resources.Load<WildIncidentConfig>("WildIncidentConfig");
            if (config == null)
            {
                reason = "Không tìm thấy cấu hình WildIncidentConfig.";
                return false;
            }
        }

        int currentMilestone = ScoreManager.Instance != null ? ScoreManager.Instance.GrantedMilestoneCount : 0;
        if (!WildIncidentEvaluator.IsFeatureUnlocked(currentMilestone, config.requiredUnlockMilestone))
        {
            reason = $"Chưa mở khóa tính năng (Yêu cầu Milestone {config.requiredUnlockMilestone}, hiện tại: {currentMilestone}).";
            return false;
        }

        if (activeIncidents.Count >= config.maxConcurrentIncidents)
        {
            reason = $"Đã đạt số lượng đàn thú hoang tối đa trên bản đồ ({activeIncidents.Count}/{config.maxConcurrentIncidents}).";
            return false;
        }

        var clusters = GetAllActiveClusters();
        if (clusters == null || clusters.Count == 0)
        {
            reason = "Chưa tìm thấy Cụm Biome nào trên map (Cần đặt ít nhất 3 ô cùng loại).";
            return false;
        }

        var host = WildIncidentEvaluator.PickHostCluster(clusters, config.minClusterSizeToHost);
        if (host == null)
        {
            reason = $"Chưa có Cụm Biome nào đạt mốc tối thiểu {config.minClusterSizeToHost} ô.";
            return false;
        }

        reason = "Đủ điều kiện xuất hiện sự cố thú hoang.";
        return true;
    }

    /// <summary>
    /// [GAMEPLAY GỐC] Kích hoạt sự cố thú hoang theo tiến trình gameplay tự nhiên.
    /// Kiểm tra đầy đủ Milestone, giới hạn số lượng và quy tắc cụm đầu não.
    /// </summary>
    public bool TryTriggerOrganicIncident(out WildPackIncident incident, out string failureReason)
    {
        incident = null;
        if (!CanSpawnOrganicIncident(out failureReason))
        {
            return false;
        }

        var clusters = GetAllActiveClusters();
        var hostCluster = WildIncidentEvaluator.PickHostCluster(clusters, config.minClusterSizeToHost);
        CardData creatureCard = config.GetCompatibleCreature(hostCluster.TerrainCard, hostCluster.NatureKey);

        int scoreMilestone = ScoreManager.Instance != null ? ScoreManager.Instance.GrantedMilestoneCount : 0;
        int progressionTier = WildIncidentEvaluator.EvaluateProgressionTier(scoreMilestone, clusters);
        AnimalRank alphaRank = config.RollAlphaRank(progressionTier);

        incident = ExecuteSpawn(hostCluster, creatureCard, alphaRank, clusters, isDebugForce: false);
        failureReason = incident != null ? null : "Lỗi khi sinh mô hình sinh vật vào scene.";
        return incident != null;
    }

    /// <summary>
    /// [KIỂM THỬ / TEST TOOL] Ép sinh sự cố thú hoang ngay lập tức cho Tester / QA.
    /// Cho phép chỉ định Cụm, Loài, hoặc Rank tùy chọn, bỏ qua giới hạn Milestone và Cooldown.
    /// </summary>
    public bool ForceSpawnIncident(IncidentClusterTarget targetCluster, CardData forceCreature, AnimalRank? forceRank, out WildPackIncident incident, out string failureReason)
    {
        incident = null;
        if (config == null)
        {
            config = Resources.Load<WildIncidentConfig>("WildIncidentConfig");
        }

        var clusters = GetAllActiveClusters();
        if (clusters == null || clusters.Count == 0)
        {
            failureReason = "Không có bất kỳ cụm đất hoặc ô Nature nào trên bản đồ để spawn.";
            return false;
        }

        IncidentClusterTarget host = targetCluster;
        if (host == null)
        {
            host = WildIncidentEvaluator.PickHostCluster(clusters, config != null ? config.minClusterSizeToHost : 3);
            if (host == null)
            {
                // Nếu không có cụm nào đạt chuẩn 3 ô, test tool ưu tiên chọn cụm lớn nhất hiện có để tester có thể thử nghiệm
                clusters.Sort((a, b) => b.HexCount.CompareTo(a.HexCount));
                host = clusters[0];
            }
        }

        CardData creature = forceCreature;
        if (creature == null && config != null)
        {
            creature = config.GetCompatibleCreature(host.TerrainCard, host.NatureKey);
        }

        int scoreMilestone = ScoreManager.Instance != null ? ScoreManager.Instance.GrantedMilestoneCount : 0;
        int progressionTier = WildIncidentEvaluator.EvaluateProgressionTier(scoreMilestone, clusters);
        AnimalRank rank = forceRank ?? (config != null ? config.RollAlphaRank(progressionTier) : AnimalRank.Rare);

        incident = ExecuteSpawn(host, creature, rank, clusters, isDebugForce: true);
        failureReason = incident != null ? null : "Lỗi khi sinh mô hình sinh vật.";
        return incident != null;
    }

    /// <summary>
    /// Wrapper tương thích ngược cho Editor và các lời gọi cũ.
    /// </summary>
    public bool TriggerNewIncident()
    {
        return TryTriggerOrganicIncident(out _, out _) || ForceSpawnIncident(null, null, null, out _, out _);
    }

    public void ResetCooldownTimer()
    {
        StartNextSpawnCooldown();
    }

    public void SetCooldownTimer(float seconds)
    {
        currentIntervalDuration = Mathf.Max(0f, seconds);
        internalTimer = 0f;
        if (GameTimeRuntime.Instance != null && GameTimeRuntime.Instance.Timers != null)
        {
            var timers = GameTimeRuntime.Instance.Timers;
            if (timers.Contains(IncidentSpawnTimerId))
            {
                timers.Cancel(IncidentSpawnTimerId);
            }
            timers.StartCountdown(IncidentSpawnTimerId, currentIntervalDuration, OfflineTimerPolicy.Freeze);
        }
    }

    /// <summary>
    /// Thực hiện việc sinh mô hình Alpha + Minions, gắn tương tác và áp dụng phong tỏa.
    /// </summary>
    private WildPackIncident ExecuteSpawn(IncidentClusterTarget hostCluster, CardData creatureCard, AnimalRank alphaRank, List<IncidentClusterTarget> allClusters, bool isDebugForce)
    {
        if (hostCluster == null) return null;

        int minionCount = config != null ? config.CalculateMinionCount(hostCluster.HexCount) : 1;
        var satelliteClusters = WildIncidentEvaluator.FindNearbySatelliteClusters(hostCluster, allClusters, minionCount);

        // 1. Sinh mô hình Thú Đầu Đàn (Alpha)
        GameObject alphaInstance = SpawnCreatureModel(creatureCard, hostCluster.CenterPosition, isAlpha: true, alphaRank);
        if (alphaInstance != null)
        {
            FocusCameraOnAlpha(alphaInstance.transform.position);
        }

        // 2. Sinh mô hình Thú Con (Minions) tại các Cụm Vệ Tinh
        var minionInstances = new List<GameObject>();
        foreach (var sat in satelliteClusters)
        {
            if (sat != null)
            {
                GameObject minionObj = SpawnCreatureModel(creatureCard, sat.CenterPosition, isAlpha: false, AnimalRank.Common);
                if (minionObj != null)
                {
                    minionInstances.Add(minionObj);
                }
            }
        }

        // 3. Khởi tạo đối tượng sự cố và phong tỏa
        string incidentId = Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
        float debuff = config != null ? config.satelliteSpeedDebuff : 0.7f;
        var incident = new WildPackIncident(
            incidentId,
            hostCluster,
            creatureCard,
            alphaRank,
            alphaInstance,
            satelliteClusters,
            minionInstances,
            debuff
        );

        if (alphaInstance != null)
        {
            var interactable = alphaInstance.AddComponent<WildAlphaInteractable>();
            interactable.Initialize(incident);
        }

        activeIncidents.Add(incident);
        OnIncidentSpawned?.Invoke(incident);

        string sourceTag = isDebugForce
            ? "<color=#38bdf8><b>[Test Tool - Thú Hoang]</b></color>"
            : "<color=#FFA500><b>[Gameplay - Thú Hoang]</b></color>";

        Debug.Log($"{sourceTag} Đàn thú hoang [Alpha {alphaRank} - {creatureCard?.cardName ?? "Linh Thú"}] đã chiếm cứ cụm <b>{hostCluster.BiomeName}</b> ({hostCluster.HexCount} ô)! {satelliteClusters.Count} cụm vệ tinh bị quấy phá.");
        return incident;
    }

    /// <summary>
    /// Xử lý khi người chơi nhấp vào con Alpha: Thu phục thành công và giải phóng toàn bộ các cụm bị ảnh hưởng!
    /// </summary>
    public void OnAlphaInteracted(WildPackIncident incident)
    {
        if (incident == null || !activeIncidents.Contains(incident)) return;

        // 1. Thưởng Thẻ Linh Thú chất lượng cao cho người chơi
        if (incident.AlphaCreatureCard != null && CardDeskController.Instance != null)
        {
            CardDeskController.Instance.AddRewardCard(incident.AlphaCreatureCard);
            Debug.Log($"<color=#50C878><b>[Thu phục thành công!]</b></color> Nhận Thẻ Linh Thú: <b>{incident.AlphaCreatureCard.cardName}</b> [{incident.AlphaRank}] bay vào tay!");
        }

        // 2. Dọn dẹp sự cố và giải phóng hòn đảo
        ResolveIncident(incident, rewarded: true);
    }

    /// <summary>
    /// Xóa sự cố và khôi phục hoạt động cho các cụm (kèm hiệu ứng rút lui êm ái)
    /// </summary>
    public void ResolveIncident(WildPackIncident incident, bool rewarded)
    {
        if (incident == null) return;
        StartCoroutine(ResolveIncidentRoutine(incident, rewarded));
    }

    private IEnumerator ResolveIncidentRoutine(WildPackIncident incident, bool rewarded)
    {
        // 1. Giải phóng cụm đất ngay lập tức
        if (incident.HostCluster != null) incident.HostCluster.SetDisrupted(false);
        foreach (var sat in incident.SatelliteClusters)
        {
            if (sat != null) sat.SetSpeedMultiplier(1.0f);
        }

        // 2. Thu thập danh sách mô hình thú đang có mặt
        List<GameObject> allCreatures = new List<GameObject>();
        if (incident.AlphaObject != null) allCreatures.Add(incident.AlphaObject);
        if (incident.MinionObjects != null)
        {
            foreach (var m in incident.MinionObjects)
            {
                if (m != null) allCreatures.Add(m);
            }
        }

        Dictionary<GameObject, Vector3> startScales = new Dictionary<GameObject, Vector3>();
        Dictionary<GameObject, Vector3> startPositions = new Dictionary<GameObject, Vector3>();
        foreach (var obj in allCreatures)
        {
            if (obj != null)
            {
                var moveAI = obj.GetComponent<AnimalMovementAI>();
                if (moveAI != null) moveAI.enabled = false;
                startScales[obj] = obj.transform.localScale;
                startPositions[obj] = obj.transform.position;
            }
        }

        // 3. Animation nhảy cẫng vui sướng và co nhỏ dần bay vào bộ bài (~0.65s)
        float elapsed = 0f;
        float duration = 0.65f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scaleFactor = 1.0f - Mathf.SmoothStep(0f, 1f, t);
            float hopY = Mathf.Sin(t * Mathf.PI) * 0.45f;

            foreach (var obj in allCreatures)
            {
                if (obj != null && startScales.TryGetValue(obj, out Vector3 baseScale) && startPositions.TryGetValue(obj, out Vector3 basePos))
                {
                    obj.transform.localScale = baseScale * scaleFactor;
                    obj.transform.position = basePos + Vector3.up * hopY;
                }
            }
            yield return null;
        }

        incident.Resolve();
        activeIncidents.Remove(incident);
        StartNextSpawnCooldown(); // Khởi tạo chu kỳ đếm giờ ngẫu nhiên mới qua Time System (180s - 300s)

        OnIncidentResolved?.Invoke(incident, rewarded);
        Debug.Log($"<color=#50C878><b>[Hệ sinh thái an toàn!]</b></color> Đàn thú hoang đã được thuần hóa / rút lui trong hòa bình.");
    }

    [ContextMenu("Resolve All Incidents")]
    public void ResolveAllIncidents()
    {
        var list = new List<WildPackIncident>(activeIncidents);
        foreach (var inc in list)
        {
            ResolveIncident(inc, rewarded: false);
        }
    }

    private CardData FindMatchingTerrainCard(string key)
    {
        if (config == null || config.biomeSpeciesMappings == null || string.IsNullOrEmpty(key)) return null;

        foreach (var m in config.biomeSpeciesMappings)
        {
            if (m != null && m.terrainCard != null)
            {
                if (m.terrainCard.cardName.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.terrainCard.cardID.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return m.terrainCard;
                }
            }
        }
        return null;
    }

    private GameObject SpawnCreatureModel(CardData cardData, Vector3 position, bool isAlpha, AnimalRank rank)
    {
        // 1. Xác định bề mặt cỏ chuẩn (Surface Y) tại vị trí spawn
        float surfaceY = position.y;
        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();
        HexCoordinates hex = HexMetrics.WorldToHex(position);
        if (worldGen != null && worldGen.MapTiles.TryGetValue(hex, out GameObject tileObj) && tileObj != null)
        {
            var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tileObj);
            surfaceY = topY;
        }

        // Raycast từ trên cao để bắt chính xác bề mặt lưới địa hình thực tế
        if (Physics.Raycast(new Vector3(position.x, 30f, position.z), Vector3.down, out RaycastHit hit, 50f))
        {
            if (hit.point.y > 0.1f)
            {
                surfaceY = Mathf.Max(surfaceY, hit.point.y);
            }
        }

        GameObject prefab = cardData != null ? cardData.prefabToPlace : null;
        GameObject instance = null;

        Vector3 spawnBasePos = new Vector3(position.x, surfaceY, position.z);

        if (prefab != null)
        {
            instance = Instantiate(prefab, spawnBasePos, Quaternion.identity);
        }
        else
        {
            // Placeholder nếu chưa có prefab
            instance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            instance.transform.position = spawnBasePos;
        }

        if (instance != null)
        {
            // Alpha to lớn uy nghi (scale 1.3), Minions nhỏ bé (scale 0.7)
            float scaleMultiplier = isAlpha ? 1.3f : 0.7f;
            instance.transform.localScale = instance.transform.localScale * scaleMultiplier;

            // 2. Tính toán chính xác độ chìm của chân (Hooves/Feet) để đặt con thú đứng phẳng phiu trên mặt cỏ:
            // Đa số model 3D động vật có pivot ở giữa bụng (~0.35m - 0.50m so với lòng bàn chân).
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            float bottomY = float.MaxValue;
            foreach (var r in renderers)
            {
                if (r is SpriteRenderer) continue;
                if (r.bounds.min.y < bottomY)
                {
                    bottomY = r.bounds.min.y;
                }
            }

            float pivotToFeet = 0.45f * scaleMultiplier;
            if (bottomY < float.MaxValue)
            {
                float calculatedOffset = instance.transform.position.y - bottomY;
                if (calculatedOffset > 0.05f)
                {
                    pivotToFeet = calculatedOffset;
                }
            }

            // Nâng pivot lên đúng bằng khoảng cách từ pivot đến bàn chân -> Chân chạm thẳng mặt cỏ!
            float finalPivotY = surfaceY + pivotToFeet;
            instance.transform.position = new Vector3(position.x, finalPivotY, position.z);

            // Gắn collider để bắt click chuột raycast
            Collider col = instance.GetComponentInChildren<Collider>();
            if (col == null)
            {
                SphereCollider sc = instance.AddComponent<SphereCollider>();
                sc.center = new Vector3(0f, 0.45f * scaleMultiplier, 0f);
                sc.radius = 0.65f * scaleMultiplier;
            }

            // Gắn hiệu ứng thị giác phân biệt thú hoang: Vòng tròn đỏ dưới chân + texture đỏ nổi bật + viền đỏ rực rỡ
            var visuals = instance.AddComponent<WildCreatureVisuals>();
            visuals.Initialize(isAlpha, surfaceY);

            // Hiệu ứng xuất hiện nhún nhảy (Spawn pop-in bounce animation)
            StartCoroutine(SpawnEntranceRoutine(instance.transform, instance.transform.localScale));
        }

        return instance;
    }

    /// <summary>
    /// Hiệu ứng xuất hiện nhún nhảy vui tươi khi đàn thú xuất hiện trên đảo
    /// </summary>
    private IEnumerator SpawnEntranceRoutine(Transform targetTransform, Vector3 targetScale)
    {
        if (targetTransform == null) yield break;

        targetTransform.localScale = Vector3.zero;
        float elapsed = 0f;
        float duration = 0.45f;

        while (elapsed < duration)
        {
            if (targetTransform == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Elastic bounce effect: overshoot up to 1.15 then settle smoothly
            float scaleMult = t < 0.7f ? (t / 0.7f) * 1.15f : Mathf.Lerp(1.15f, 1.0f, (t - 0.7f) / 0.3f);
            targetTransform.localScale = targetScale * scaleMult;
            yield return null;
        }

        if (targetTransform != null)
        {
            targetTransform.localScale = targetScale;
        }
    }

    /// <summary>
    /// Di chuyển và zoom camera mượt mà vào Thú Đầu Đàn (Alpha) khi nó xuất hiện
    /// </summary>
    public void FocusCameraOnAlpha(Vector3 alphaPosition)
    {
        var camController = HexCameraController.Instance;
        if (camController == null)
        {
            camController = FindAnyObjectByType<HexCameraController>();
        }

        if (camController != null)
        {
            camController.FocusOnTarget(alphaPosition, targetDistance: 5.2f, duration: 1.3f);
        }
    }
}
