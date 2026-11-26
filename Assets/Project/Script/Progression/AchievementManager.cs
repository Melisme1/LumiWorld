using System;
using System.Collections.Generic;
using UnityEngine;
using LumiWorld.Acs;

[Serializable]
public class IslandMilestone
{
    public string islandId = "starter";
    public int index;
    public string id;
    public string title;
    public string shortCondition;
    public string description;
    public string rewardText;
    public int rewardCoins;
    public int rewardHarmony;
    public string rewardBonusInfo;
    public bool isCompleted;
    public bool isClaimed;
    public float currentProgress; // 0.0 to 1.0
}

[Serializable]
public class AchievementGoal
{
    public string islandId = "starter";
    public string id;
    public string title;
    public string description;
    public int targetCount;
    public int currentCount;
    public int rewardCoins;
    public int harmonyPoints;
    public bool isClaimed;

    public bool IsCompleted => currentCount >= targetCount;
}

/// <summary>
/// Quản lý dữ liệu Cột mốc Harmony, Tiến trình mở đảo mới và Hệ thống Thành tựu (Achievement System):
/// - Ghi nhận điểm Harmony vĩnh viễn (Milestone Score) để mở đảo.
/// - Theo dõi 4 Cột mốc Hệ Sinh Thái Đảo (Môi trường sống đầu tiên, Loài mới, Lên cấp, Câu chuyện đảo).
/// - Quản lý danh sách thành tựu với Claim ID duy nhất 1 lần chống nhận lặp.
/// </summary>
public class AchievementManager : MonoBehaviour
{
    private static AchievementManager instance;
    private static bool isApplicationQuitting = false;

    public static bool HasInstance => instance != null && !isApplicationQuitting;

    public static AchievementManager Instance
    {
        get
        {
            if (isApplicationQuitting) return null;

            if (instance == null)
            {
                instance = FindAnyObjectByType<AchievementManager>();
                if (instance == null)
                {
                    if (!Application.isPlaying || isApplicationQuitting)
                        return null;

                    GameObject go = new GameObject("AchievementManager");
                    instance = go.AddComponent<AchievementManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    [Header("Harmony & Island Unlock Progression")]
    [Tooltip("Harmony khởi điểm của tài khoản mới; sau đó đọc từ bản lưu kinh tế (economy_<id>.json)")]
    [SerializeField] private int totalHarmony = 29;
    [SerializeField] private int nextIslandThreshold = 50;

    [Header("Island Unlock 3 Conditions")]
    [Tooltip("Điều kiện 1: Đạt đủ điểm Harmony (ví dụ 50 điểm)")]
    public bool condition1_HarmonyMet => condition2_MaxHabitatMet && condition3_CreaturesMet;

    [Tooltip("Điều kiện 2: Sở hữu ít nhất 1 cụm Habitat đạt 6 hex")]
    public bool condition2_MaxHabitatMet = false;

    [Tooltip("Điều kiện 3: Nuôi dưỡng ít nhất 3 cá thể sinh vật")]
    public bool condition3_CreaturesMet = false;

    [Header("4 Ecosystem Progression Milestones")]
    [SerializeField] private List<IslandMilestone> milestones = new List<IslandMilestone>();

    [Header("Legacy Achievements List")]
    [SerializeField] private List<AchievementGoal> achievements = new List<AchievementGoal>();

    private readonly HashSet<string> claimedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private bool hasRankedUpAnyCreature = false;
    private float lastEvalTime = 0f;

    public int TotalHarmony => totalHarmony;
    public int NextIslandThreshold => nextIslandThreshold;
    public IReadOnlyList<IslandMilestone> Milestones => milestones;
    public IReadOnlyList<AchievementGoal> Achievements => achievements;

    public event Action<int, int> OnHarmonyChanged;
    public event Action OnAchievementsUpdated;
    public event Action OnMilestonesChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
        isApplicationQuitting = false;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        isApplicationQuitting = false;
        DontDestroyOnLoad(gameObject);
        InitializeDefaultMilestones();
        InitializeDefaultAchievements();
        LoadProgress();
    }

    // =========================================================
    // LƯU THEO TÀI KHOẢN (economy_<id>.json, SaveSync đẩy lên server)
    // =========================================================

    private static readonly string[] LegacyClaimKeys =
    {
        "ACH_CLAIMED_M1_FIRST_HABITAT", "ACH_CLAIMED_M2_NEW_SPECIES", "ACH_CLAIMED_M3_RANK_UP", "ACH_CLAIMED_M4_ISLAND_STORY"
    };

    private static EconomySaveData SavedProgress
    {
        get
        {
            EconomySaveSystem saveSystem = EconomySaveSystem.Instance;
            return saveSystem != null ? saveSystem.Progress : null;
        }
    }

    /// <summary>
    /// Đọc Harmony, cột mốc và thành tựu đã nhận từ bản lưu kinh tế của tài khoản.
    /// Bản lưu cũ chưa có Harmony thì lấy giá trị khởi điểm rồi ghi lại.
    /// </summary>
    private void LoadProgress()
    {
        InitializeIslandChapters();
        // Khóa PlayerPrefs cũ chỉ theo máy và bị xóa mỗi lần mở game, không chuyển sang tài khoản
        bool removedLegacy = false;
        foreach (string key in LegacyClaimKeys)
        {
            if (!PlayerPrefs.HasKey(key)) continue;
            PlayerPrefs.DeleteKey(key);
            removedLegacy = true;
        }
        if (removedLegacy) PlayerPrefs.Save();

        EconomySaveData progress = SavedProgress;
        if (progress == null) return;

        if (!progress.harmonyInitialized)
        {
            progress.totalHarmony = totalHarmony;
            progress.harmonyInitialized = true;
            EconomySaveSystem.Instance.MarkDirty();
        }

        InitializeIslandChapters();
        totalHarmony = progress.totalHarmony;
        hasRankedUpAnyCreature = progress.hasRankedUpAnyCreature;

        foreach (IslandMilestone milestone in milestones)
            milestone.isClaimed = progress.claimedMilestoneIds.Contains(milestone.id);

        claimedIds.Clear();
        foreach (string id in progress.claimedAchievementIds) claimedIds.Add(id);
        foreach (AchievementGoal goal in achievements)
        {
            goal.isClaimed = claimedIds.Contains(goal.id);
            goal.currentCount = progress.achievementProgress.Find(r => r.id == goal.id)?.count ?? 0;
        }

        OnHarmonyChanged?.Invoke(totalHarmony, nextIslandThreshold);
    }

    // Chỉ đánh giá theo đảo của mình khi đảo đã dựng xong; đang xem đảo người khác hoặc đang
    // tải lại bản server sau xung đột thì giữ nguyên trạng thái cũ
    private static bool CanEvaluateWorld()
    {
        if (WorldLaunch.IsVisit || SaveSync.SavesSuspended) return false;
        WorldSaveSystem world = WorldSaveSystem.Instance;
        return world != null && world.IsWorldReady;
    }

    // Không trao thưởng khi đang xem đảo người khác hoặc đang tải lại bản server
    private static bool CanGrantRewards() => !WorldLaunch.IsVisit && !SaveSync.SavesSuspended;

    private void Start()
    {
        // Lắng nghe sự kiện nâng rank từ CreatureCareManager
        if (CreatureCareManager.Instance != null)
        {
            CreatureCareManager.Instance.OnRankUpgraded += HandleRankUpgraded;
        }

        EvaluateMilestones();
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (CreatureCareManager.HasInstance)
        {
            CreatureCareManager.Instance.OnRankUpgraded -= HandleRankUpgraded;
        }
    }

    private void Update()
    {
        // Định kỳ kiểm tra tiến độ mỗi 1.5s
        if (Time.unscaledTime - lastEvalTime > 1.5f)
        {
            lastEvalTime = Time.unscaledTime;
            EvaluateMilestones();
        }
    }

    private void HandleRankUpgraded(string creatureId, CreatureQualityRank rank)
    {
        if (WorldLaunch.IsVisit) return;
        string chapter = null;
        if (HabitatRuntimeManager.TryGetActive(out var manager))
            foreach (var h in manager.Habitats)
                if (h.residents.Exists(r => r.individualId == creatureId)) chapter = h.island == HabitatIsland.Wet ? "wet" : "starter";
        if (chapter == null) return;
        ReportProgress("ACH_FIRST_CARE", 1, chapter);
        if (chapter == "starter")
        {
            hasRankedUpAnyCreature = true;
            if (SavedProgress != null) SavedProgress.hasRankedUpAnyCreature = true;
        }
        EvaluateMilestones();
    }

    private static string GoalKey(string island, string id) => island == "starter" ? id : island + ":" + id;

    private void InitializeIslandChapters()
    {
        if (milestones.Count == 4)
            for (int i = 0; i < 4; i++)
            {
                var copy = JsonUtility.FromJson<IslandMilestone>(JsonUtility.ToJson(milestones[i]));
                copy.index = i + 4; copy.islandId = "wet"; copy.id = "wet:" + copy.id;
                copy.isClaimed = SavedProgress != null && SavedProgress.claimedMilestoneIds.Contains(copy.id);
                copy.isCompleted = false; copy.currentProgress = 0;
                milestones.Add(copy);
            }
        if (!achievements.Exists(g => g.islandId == "wet"))
            foreach (var goal in new List<AchievementGoal>(achievements))
            {
                var copy = JsonUtility.FromJson<AchievementGoal>(JsonUtility.ToJson(goal));
                copy.islandId = "wet"; copy.id = "wet:" + copy.id; copy.currentCount = 0;
                copy.isClaimed = SavedProgress != null && SavedProgress.claimedAchievementIds.Contains(copy.id);
                achievements.Add(copy);
            }
    }

    private void UpdateObservedGoal(string island, string id, int count)
    {
        var goal = achievements.Find(g => g.id == GoalKey(island, id));
        if (goal == null || count <= goal.currentCount) return;
        goal.currentCount = Mathf.Min(goal.targetCount, count);
        PersistGoal(goal);
    }

    private void PersistGoal(AchievementGoal goal)
    {
        var progress = SavedProgress;
        if (progress == null || !CanGrantRewards()) return;
        var record = progress.achievementProgress.Find(r => r.id == goal.id);
        if (record == null) { record = new AchievementProgressRecord { id = goal.id }; progress.achievementProgress.Add(record); }
        record.count = goal.currentCount;
        EconomySaveSystem.Instance.MarkDirty();
    }

    public void InitializeDefaultMilestones()
    {
        if (milestones.Count > 0) return;

        // Trạng thái đã nhận đọc từ bản lưu của tài khoản trong LoadProgress()
        milestones.Add(new IslandMilestone
        {
            index = 0,
            id = "M1_FIRST_HABITAT",
            title = "FIRST HABITAT",
            shortCondition = "Establish First Habitat",
            description = "Successfully create a compatible Habitat cluster with at least 1 resident creature settled on the island.",
            rewardText = "Coins & Harmony",
            rewardCoins = 30,
            rewardHarmony = 15,
            rewardBonusInfo = "",
            isCompleted = false,
            isClaimed = false,
            currentProgress = 0f
        });

        milestones.Add(new IslandMilestone
        {
            index = 1,
            id = "M2_NEW_SPECIES",
            title = "NEW SPECIES",
            shortCondition = "Deploy New Species",
            description = "Successfully introduce a new creature species adapted to the island ecosystem.",
            rewardText = "Coins & Terrain Card",
            rewardCoins = 40,
            rewardHarmony = 20,
            rewardBonusInfo = "Nature Card",
            isCompleted = false,
            isClaimed = false,
            currentProgress = 0f
        });

        milestones.Add(new IslandMilestone
        {
            index = 2,
            id = "M3_RANK_UP",
            title = "RANK UP",
            shortCondition = "Promote Creature Quality Rank",
            description = "Care for a creature to reach 100 Care points and promote its quality rank to Rank II or Rank III.",
            rewardText = "Coins & Harmony",
            rewardCoins = 50,
            rewardHarmony = 25,
            rewardBonusInfo = "",
            isCompleted = false,
            isClaimed = false,
            currentProgress = 0f
        });

        milestones.Add(new IslandMilestone
        {
            index = 3,
            id = "M4_ISLAND_STORY",
            title = "ISLAND STORY",
            shortCondition = "Island Ecosystem Milestone",
            description = "Expand a Habitat cluster to 6+ hexes, or nurture 3 or more creatures residing on the island.",
            rewardText = "Coins & Harmony",
            rewardCoins = 100,
            rewardHarmony = 35,
            rewardBonusInfo = "",
            isCompleted = false,
            isClaimed = false,
            currentProgress = 0f
        });
    }

    private void InitializeDefaultAchievements()
    {
        if (achievements.Count > 0) return;

        achievements.Add(new AchievementGoal
        {
            id = "ACH_FIRST_NATURE",
            title = "Pioneer",
            description = "Place your first Nature terrain card on the board",
            targetCount = 1,
            currentCount = 0,
            rewardCoins = 15,
            harmonyPoints = 10
        });

        achievements.Add(new AchievementGoal
        {
            id = "ACH_HABITAT_3",
            title = "Budding Ecology",
            description = "Expand a Biome cluster to 3 hexes (Unlocks first T1 resource slot)",
            targetCount = 3,
            currentCount = 0,
            rewardCoins = 25,
            harmonyPoints = 15
        });

        achievements.Add(new AchievementGoal
        {
            id = "ACH_HABITAT_6",
            title = "Sanctuary Haven",
            description = "Expand a Biome cluster to 6 hexes (Unlocks T2 resource slot)",
            targetCount = 6,
            currentCount = 0,
            rewardCoins = 50,
            harmonyPoints = 25
        });

        achievements.Add(new AchievementGoal
        {
            id = "ACH_FIRST_CREATURE",
            title = "Forest Companion",
            description = "Place a creature into a compatible Habitat",
            targetCount = 1,
            currentCount = 0,
            rewardCoins = 20,
            harmonyPoints = 10
        });

        achievements.Add(new AchievementGoal
        {
            id = "ACH_CONNECTION_DANCE",
            title = "Wild Harmony",
            description = "Trigger the 20s connection ring effect between creatures",
            targetCount = 1,
            currentCount = 0,
            rewardCoins = 35,
            harmonyPoints = 20
        });

        achievements.Add(new AchievementGoal
        {
            id = "ACH_FIRST_CARE",
            title = "Caring Hand",
            description = "Fill creature Care bar and promote creature to Rank II",
            targetCount = 1,
            currentCount = 0,
            rewardCoins = 30,
            harmonyPoints = 15
        });

        achievements.Add(new AchievementGoal
        {
            id = "ACH_HARVEST_10",
            title = "Bountiful Harvest",
            description = "Harvest 10 resource batches into the Warehouse",
            targetCount = 10,
            currentCount = 0,
            rewardCoins = 30,
            harmonyPoints = 15
        });
    }

    private bool isEvaluating = false;

    /// <summary>
    /// Đánh giá trạng thái thực tế của 4 cột mốc tiến trình
    /// </summary>
    public void EvaluateMilestones()
    {
        if (isEvaluating) return;
        isEvaluating = true;
        try
        {
            InitializeDefaultMilestones();
            if (milestones.Count < 4) return;
            if (!CanEvaluateWorld()) return;

        InitializeIslandChapters();
        if (!HabitatRuntimeManager.TryGetActive(out var habitatMgr)) return;
        foreach (string chapter in new[] { "starter", "wet" })
        {
            var island = chapter == "starter" ? HabitatIsland.Starter : HabitatIsland.Wet;
            var groups = new List<HabitatState>();
            foreach (var h in habitatMgr.Habitats) if (h.island == island) groups.Add(h);
            int natureCount = 0, maxCluster = 0, creatures = 0, ranked = 0;
            foreach (var h in groups)
            {
                natureCount += h.HexCount;
                maxCluster = Mathf.Max(maxCluster, h.HexCount);
                creatures += h.residents.Count;
                foreach (var resident in h.residents) if (resident.qualityRank >= CreatureQualityRank.RankII) ranked++;
            }
            int offset = chapter == "starter" ? 0 : 4;
            bool care = ranked > 0 || (chapter == "starter" && hasRankedUpAnyCreature) ||
                achievements.Find(g => g.id == GoalKey(chapter, "ACH_FIRST_CARE")).IsCompleted;
            bool[] complete = { creatures > 0, creatures > 0, care, maxCluster >= 6 || creatures >= 3 };
            float[] ratios = { creatures > 0 ? 1 : natureCount > 0 ? .5f : 0, creatures > 0 ? 1 : 0,
                care ? 1 : 0, Mathf.Max(maxCluster / 6f, creatures / 3f) };
            for (int i = 0; i < 4; i++)
            {
                milestones[offset + i].isCompleted = complete[i];
                milestones[offset + i].currentProgress = Mathf.Clamp01(ratios[i]);
            }
            UpdateObservedGoal(chapter, "ACH_FIRST_NATURE", natureCount);
            UpdateObservedGoal(chapter, "ACH_HABITAT_3", maxCluster);
            UpdateObservedGoal(chapter, "ACH_HABITAT_6", maxCluster);
            UpdateObservedGoal(chapter, "ACH_FIRST_CREATURE", creatures);
            UpdateObservedGoal(chapter, "ACH_FIRST_CARE", care ? 1 : 0);
            if (chapter == "starter")
            {
                condition2_MaxHabitatMet = groups.Exists(h => h.affinity == BiomeAffinity.Leafwood && h.HexCount >= 3) &&
                    groups.Exists(h => h.affinity == BiomeAffinity.Bloomfield && h.HexCount >= 3) &&
                    groups.Exists(h => h.affinity == BiomeAffinity.Windheath && h.HexCount >= 3);
                condition3_CreaturesMet = creatures >= 3;
            }
        }

        OnMilestonesChanged?.Invoke();
        OnAchievementsUpdated?.Invoke();
        }
        finally
        {
            isEvaluating = false;
        }
    }

    /// <summary>
    /// Kiểm tra xem có bất kỳ cột mốc nào đã hoàn thành nhưng chưa nhận thưởng không.
    /// Nếu có -> Nút Thành Tựu trên HUD sẽ nhấp nháy liên tục!
    /// </summary>
    public bool HasAnyUnclaimedMilestone()
    {
        InitializeDefaultMilestones();
        foreach (var m in milestones)
        {
            if (m.isCompleted && !m.isClaimed) return true;
        }
        return false;
    }

    /// <summary>
    /// Tính toán tỉ lệ lấp đầy thanh tiến trình (0.0 -> 1.0) kết nối giữa 4 cột mốc
    /// </summary>
    public float GetOverallProgressRatio()
    {
        InitializeDefaultMilestones();
        if (milestones.Count < 4) return 0f;

        float ratio = 0f;
        if (milestones[0].isCompleted)
        {
            ratio = 0.05f; // Đã đạt chạm node 1
            if (milestones[1].isCompleted)
            {
                ratio = 0.333f; // Đạt chạm node 2
                if (milestones[2].isCompleted)
                {
                    ratio = 0.667f; // Đạt chạm node 3
                    if (milestones[3].isCompleted)
                    {
                        ratio = 1.0f; // Đạt chạm node 4 hoàn thành
                    }
                    else
                    {
                        ratio += 0.333f * Mathf.Clamp01(milestones[3].currentProgress);
                    }
                }
                else
                {
                    ratio += 0.333f * Mathf.Clamp01(milestones[2].currentProgress);
                }
            }
            else
            {
                ratio += 0.283f * Mathf.Clamp01(milestones[1].currentProgress);
            }
        }
        else
        {
            ratio = 0.05f * Mathf.Clamp01(milestones[0].currentProgress);
        }
        return Mathf.Clamp01(ratio);
    }

    /// <summary>
    /// Nhận thưởng cho 1 trong 4 cột mốc hệ sinh thái
    /// </summary>
    public bool ClaimMilestone(int index, out string rewardInfo)
    {
        rewardInfo = string.Empty;
        if (!CanGrantRewards())
        {
            rewardInfo = "Không thể nhận thưởng lúc này.";
            return false;
        }
        EvaluateMilestones();

        if (index < 0 || index >= milestones.Count)
        {
            rewardInfo = "Milestone not found.";
            return false;
        }

        var m = milestones[index];
        if (m.isClaimed)
        {
            rewardInfo = "This milestone has already been claimed!";
            return false;
        }

        if (!m.isCompleted)
        {
            rewardInfo = "Milestone requirements not completed!";
            return false;
        }

        // Ghi id đã nhận vào bản lưu tài khoản; Coins và Harmony bên dưới cùng vào bản lưu đó,
        // rồi SaveNow ghi cả ba trong một lần (không có lúc đã cộng thưởng mà chưa đánh dấu đã nhận)
        m.isClaimed = true;
        EconomySaveData progress = SavedProgress;
        if (progress != null && !progress.claimedMilestoneIds.Contains(m.id))
            progress.claimedMilestoneIds.Add(m.id);

        // 1. Thưởng Coins
        if (m.rewardCoins > 0 && CurrencyWallet.Instance != null)
        {
            CurrencyWallet.Instance.Add(m.rewardCoins, CoinReason.Milestone);
        }

        // 2. Thưởng Harmony
        if (m.rewardHarmony > 0)
        {
            AddHarmony(m.rewardHarmony);
        }

        // 3. Cấp thẻ bài / Quà tặng thưởng vào tay người chơi (nếu có CardDeskController)
        TryGrantMilestoneCardReward(index);

        SaveProgressNow();

        rewardInfo = $"+{m.rewardCoins} Coins & +{m.rewardHarmony} Harmony" +
            (string.IsNullOrEmpty(m.rewardBonusInfo) ? "!" : $" & {m.rewardBonusInfo}!");
        OnMilestonesChanged?.Invoke();
        OnAchievementsUpdated?.Invoke();
        return true;
    }

    private void TryGrantMilestoneCardReward(int index)
    {
        if (CardDeskController.Instance == null) return;

        if (index % 4 != 1 || !HabitatRuntimeManager.TryGetActive(out var habitats)) return;
        var island = milestones[index].islandId == "wet" ? HabitatIsland.Wet : HabitatIsland.Starter;
        foreach (var habitat in habitats.Catalog.habitats)
            if (habitat != null && habitat.island == island && habitat.natureCard != null)
            {
                CardDeskController.Instance.AddCardsWithoutSaving(habitat.natureCard, 1);
                return;
            }
    }

    // Ghi bản lưu kinh tế ngay sau khi trao thưởng (id đã nhận, Coins, Harmony cùng một lần ghi)
    private static void SaveProgressNow()
    {
        EconomySaveSystem saveSystem = EconomySaveSystem.Instance;
        if (saveSystem == null) return;
        saveSystem.MarkDirty();
        if (!saveSystem.SaveNow())
            Debug.LogWarning("[LumiWorld Save] Chưa ghi được phần thưởng thành tựu; sẽ thử lại ở lần lưu kế tiếp.");
    }

    /// <summary>
    /// Cộng điểm Harmony cột mốc vĩnh viễn
    /// </summary>
    public void AddHarmony(int amount)
    {
        if (amount <= 0 || !CanGrantRewards()) return;
        totalHarmony += amount;

        EconomySaveData progress = SavedProgress;
        if (progress != null)
        {
            progress.totalHarmony = totalHarmony;
            progress.harmonyInitialized = true;
            EconomySaveSystem.Instance.MarkDirty();
        }

        OnHarmonyChanged?.Invoke(totalHarmony, nextIslandThreshold);
        OnAchievementsUpdated?.Invoke();
    }

    /// <summary>
    /// Cập nhật tiến độ của một thành tựu
    /// </summary>
    public void ReportProgress(string achievementId, int progressToAdd = 1, string islandId = "starter")
    {
        if (!CanGrantRewards() || progressToAdd <= 0) return;
        InitializeIslandChapters();
        var goal = achievements.Find(a => a.id.Equals(GoalKey(islandId, achievementId), StringComparison.OrdinalIgnoreCase));
        if (goal != null && !goal.isClaimed)
        {
            goal.currentCount = Mathf.Min(goal.targetCount, goal.currentCount + progressToAdd);
            PersistGoal(goal);
            OnAchievementsUpdated?.Invoke();
        }
    }

    public void ReportHarvest(ResourceCollectionReceipt receipt)
    {
        if (receipt == null || !HabitatRuntimeManager.TryGetActive(out var manager)) return;
        // Each transferred resource buffer is a batch. Empty/full/rejected transfers earn nothing.
        foreach (var item in receipt.Items)
        {
            if (item.Transferred <= 0) continue;
            var resource = manager.Catalog.FindResource(item.ResourceId);
            if (resource == null) continue;
            var island = HabitatDataRules.GetIsland(resource.affinity);
            if (island == HabitatIsland.Starter || island == HabitatIsland.Wet)
                ReportProgress("ACH_HARVEST_10", 1, island == HabitatIsland.Wet ? "wet" : "starter");
        }
    }

    public void ReportConnection(PlacedCard creature)
    {
        if (creature == null || creature.cardData == null) return;
        ReportProgress("ACH_CONNECTION_DANCE", 1, creature.cardData.islandId);
    }

    /// <summary>
    /// Nhận thưởng thành tựu (Claim Reward).
    /// Ghi nhận Claim ID duy nhất 1 lần để chống nhận lặp theo GDD!
    /// </summary>
    public bool ClaimAchievement(string achievementId, out string rewardInfo)
    {
        rewardInfo = string.Empty;
        if (!CanGrantRewards())
        {
            rewardInfo = "Không thể nhận thưởng lúc này.";
            return false;
        }

        EvaluateMilestones();
        var goal = achievements.Find(a => a.id.Equals(achievementId, StringComparison.OrdinalIgnoreCase));
        if (goal == null)
        {
            rewardInfo = "Achievement not found.";
            return false;
        }

        if (goal.isClaimed || claimedIds.Contains(goal.id))
        {
            rewardInfo = "This achievement has already been claimed!";
            return false;
        }

        if (!goal.IsCompleted)
        {
            rewardInfo = "Achievement requirements not met!";
            return false;
        }

        // 1. Đánh dấu đã nhận vĩnh viễn
        goal.isClaimed = true;
        claimedIds.Add(goal.id);
        EconomySaveData progress = SavedProgress;
        if (progress != null && !progress.claimedAchievementIds.Contains(goal.id))
            progress.claimedAchievementIds.Add(goal.id);

        // 2. Trao thưởng Coins
        if (goal.rewardCoins > 0 && CurrencyWallet.Instance != null)
        {
            CurrencyWallet.Instance.Add(goal.rewardCoins, CoinReason.Milestone);
        }

        // 3. Trao điểm Harmony vĩnh viễn
        if (goal.harmonyPoints > 0)
        {
            AddHarmony(goal.harmonyPoints);
        }

        SaveProgressNow();

        rewardInfo = $"+{goal.rewardCoins} Coins & +{goal.harmonyPoints} Harmony!";
        OnAchievementsUpdated?.Invoke();
        return true;
    }
}
