using System;
using System.Collections.Generic;
using UnityEngine;
using LumiWorld.Acs;

[Serializable]
public class IslandMilestone
{
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
    [SerializeField] private int totalHarmony = 29;
    [SerializeField] private int nextIslandThreshold = 50;

    [Header("Island Unlock 3 Conditions")]
    [Tooltip("Điều kiện 1: Đạt đủ điểm Harmony (ví dụ 50 điểm)")]
    public bool condition1_HarmonyMet => totalHarmony >= nextIslandThreshold;

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
    }

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
        hasRankedUpAnyCreature = true;
        EvaluateMilestones();
    }

    public void InitializeDefaultMilestones()
    {
        if (milestones.Count > 0) return;

        // Nếu người chơi bắt đầu phiên chơi mới (chưa có lưu GameSave), xóa sạch các flag claim test cũ
        bool isFreshSession = !PlayerPrefs.HasKey("GameSave");
        if (isFreshSession)
        {
            PlayerPrefs.DeleteKey("ACH_CLAIMED_M1_FIRST_HABITAT");
            PlayerPrefs.DeleteKey("ACH_CLAIMED_M2_NEW_SPECIES");
            PlayerPrefs.DeleteKey("ACH_CLAIMED_M3_RANK_UP");
            PlayerPrefs.DeleteKey("ACH_CLAIMED_M4_ISLAND_STORY");
            PlayerPrefs.Save();
        }

        milestones.Add(new IslandMilestone
        {
            index = 0,
            id = "M1_FIRST_HABITAT",
            title = "FIRST HABITAT",
            shortCondition = "Establish First Habitat",
            description = "Successfully create a compatible Habitat cluster with at least 1 resident creature settled on the island.",
            rewardText = "Coins & Creature Food",
            rewardCoins = 30,
            rewardHarmony = 15,
            rewardBonusInfo = "Creature Care Food Pack",
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
            rewardBonusInfo = "Blueprint / Terrain Card Pack",
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
            rewardText = "Coins & Supply Pack",
            rewardCoins = 50,
            rewardHarmony = 25,
            rewardBonusInfo = "Care Supply Pack & Upgrade Items",
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
            rewardText = "Title + Coins & Rare Creature Card",
            rewardCoins = 100,
            rewardHarmony = 35,
            rewardBonusInfo = "Title [Lumi Ecological Champion] & Ultra-Rare Creature Card",
            isCompleted = false,
            isClaimed = false,
            currentProgress = 0f
        });

        // Chỉ khôi phục trạng thái claimed nếu có lưu save và cột mốc đã hoàn thành
        if (!isFreshSession)
        {
            for (int i = 0; i < milestones.Count; i++)
            {
                milestones[i].isClaimed = PlayerPrefs.GetInt("ACH_CLAIMED_" + milestones[i].id, 0) == 1;
            }
        }
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
            currentCount = 1,
            rewardCoins = 15,
            harmonyPoints = 10
        });

        achievements.Add(new AchievementGoal
        {
            id = "ACH_HABITAT_3",
            title = "Budding Ecology",
            description = "Expand a Biome cluster to 3 hexes (Unlocks first T1 resource slot)",
            targetCount = 3,
            currentCount = 2,
            rewardCoins = 25,
            harmonyPoints = 15
        });

        achievements.Add(new AchievementGoal
        {
            id = "ACH_HABITAT_6",
            title = "Sanctuary Haven",
            description = "Expand a Biome cluster to 6 hexes (Unlocks T2 resource slot)",
            targetCount = 6,
            currentCount = 4,
            rewardCoins = 50,
            harmonyPoints = 25
        });

        achievements.Add(new AchievementGoal
        {
            id = "ACH_FIRST_CREATURE",
            title = "Forest Companion",
            description = "Place a creature into a compatible Habitat",
            targetCount = 1,
            currentCount = 1,
            rewardCoins = 20,
            harmonyPoints = 10
        });

        achievements.Add(new AchievementGoal
        {
            id = "ACH_CONNECTION_DANCE",
            title = "Wild Harmony",
            description = "Trigger the 20s connection ring effect between creatures",
            targetCount = 1,
            currentCount = 1,
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
            currentCount = 6,
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

        // 1. Kiểm tra Mốc 1: Habitat có cư dân sinh vật
        bool m1Met = false;
        float m1Prog = 0f;
        HabitatRuntimeManager habitatMgr = null;
        HabitatRuntimeManager.TryGetActive(out habitatMgr);
        if (habitatMgr != null && habitatMgr.Habitats != null)
        {
            foreach (var h in habitatMgr.Habitats)
            {
                if (h == null) continue;
                if (h.residents != null && h.residents.Count > 0)
                {
                    m1Met = true;
                    m1Prog = 1f;
                    break;
                }
                if (h.members != null && h.members.Count > 0)
                {
                    m1Prog = Mathf.Max(m1Prog, 0.5f);
                }
            }
        }
        var activeAnimals = FindObjectsByType<AnimalIndividual>(FindObjectsSortMode.None);
        if (!m1Met && activeAnimals != null && activeAnimals.Length > 0)
        {
            m1Met = true;
            m1Prog = 1f;
        }
        milestones[0].isCompleted = m1Met;
        milestones[0].currentProgress = m1Prog;

        // 2. Kiểm tra Mốc 2: Triển khai loài mới lên đảo
        bool m2Met = (activeAnimals != null && activeAnimals.Length > 0);
        milestones[1].isCompleted = m2Met;
        milestones[1].currentProgress = m2Met ? 1f : 0f;

        // 3. Kiểm tra Mốc 3: Nâng rank sinh vật
        bool m3Met = hasRankedUpAnyCreature;
        float maxCareRatio = 0f;
        if (activeAnimals != null)
        {
            foreach (var animal in activeAnimals)
            {
                if (animal == null) continue;
                if (animal.StarLevel >= 2 || animal.Rank > AnimalRank.Common)
                {
                    m3Met = true;
                    break;
                }
                if (CreatureCareManager.Instance != null && !string.IsNullOrEmpty(animal.UniqueID))
                {
                    int care = CreatureCareManager.Instance.GetCareProgress(animal.UniqueID);
                    maxCareRatio = Mathf.Max(maxCareRatio, care / 100f);
                }
            }
        }
        if (!m3Met)
        {
            var identities = FindObjectsByType<HabitatPlacementIdentity>(FindObjectsSortMode.None);
            if (identities != null)
            {
                foreach (var id in identities)
                {
                    if (id != null && id.QualityRank >= CreatureQualityRank.RankII)
                    {
                        m3Met = true;
                        break;
                    }
                }
            }
        }
        milestones[2].isCompleted = m3Met;
        milestones[2].currentProgress = m3Met ? 1f : maxCareRatio;

        // 4. Kiểm tra Mốc 4: Mở rộng cụm >= 6 hex hoặc >= 3 sinh vật
        int maxCluster = 0;
        if (habitatMgr != null && habitatMgr.Habitats != null)
        {
            foreach (var h in habitatMgr.Habitats)
            {
                if (h != null && h.members != null)
                {
                    maxCluster = Mathf.Max(maxCluster, h.members.Count);
                }
            }
        }
        int animalTotal = activeAnimals != null ? activeAnimals.Length : 0;
        bool m4Met = (maxCluster >= 6 || animalTotal >= 3);
        milestones[3].isCompleted = m4Met;
        milestones[3].currentProgress = m4Met ? 1f : Mathf.Max(maxCluster / 6f, animalTotal / 3f);

        // Đồng bộ lại 3 điều kiện mở đảo
        condition2_MaxHabitatMet = maxCluster >= 6;
        condition3_CreaturesMet = animalTotal >= 3;

        // Đảm bảo tính toàn vẹn: Không thể có trạng thái 'Đã nhận' nếu cột mốc chưa hoàn thành
        for (int i = 0; i < milestones.Count; i++)
        {
            var m = milestones[i];
            if (!m.isCompleted && m.isClaimed)
            {
                m.isClaimed = false;
                PlayerPrefs.DeleteKey("ACH_CLAIMED_" + m.id);
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

        m.isClaimed = true;
        PlayerPrefs.SetInt("ACH_CLAIMED_" + m.id, 1);
        PlayerPrefs.Save();

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

        rewardInfo = $"+{m.rewardCoins} Coins & +{m.rewardHarmony} Harmony & {m.rewardBonusInfo}!";
        OnMilestonesChanged?.Invoke();
        OnAchievementsUpdated?.Invoke();
        return true;
    }

    private void TryGrantMilestoneCardReward(int index)
    {
        if (CardDeskController.Instance == null) return;

        try
        {
            string cardName = null;
            if (index == 1) cardName = "Cards/Leafwood";
            else if (index == 3) cardName = "Cards/Fox";

            if (!string.IsNullOrEmpty(cardName))
            {
                var card = Resources.Load<CardData>(cardName);
                if (card != null)
                {
                    CardDeskController.Instance.AddRewardCard(card);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AchievementManager] Could not add reward card: {ex.Message}");
        }
    }

    /// <summary>
    /// Cộng điểm Harmony cột mốc vĩnh viễn
    /// </summary>
    public void AddHarmony(int amount)
    {
        if (amount <= 0) return;
        totalHarmony += amount;
        OnHarmonyChanged?.Invoke(totalHarmony, nextIslandThreshold);
        OnAchievementsUpdated?.Invoke();
    }

    /// <summary>
    /// Cập nhật tiến độ của một thành tựu
    /// </summary>
    public void ReportProgress(string achievementId, int progressToAdd = 1)
    {
        var goal = achievements.Find(a => a.id.Equals(achievementId, StringComparison.OrdinalIgnoreCase));
        if (goal != null && !goal.isClaimed)
        {
            goal.currentCount = Mathf.Min(goal.targetCount, goal.currentCount + progressToAdd);
            OnAchievementsUpdated?.Invoke();
        }
    }

    /// <summary>
    /// Nhận thưởng thành tựu (Claim Reward).
    /// Ghi nhận Claim ID duy nhất 1 lần để chống nhận lặp theo GDD!
    /// </summary>
    public bool ClaimAchievement(string achievementId, out string rewardInfo)
    {
        rewardInfo = string.Empty;

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

        rewardInfo = $"+{goal.rewardCoins} Coins & +{goal.harmonyPoints} Harmony!";
        OnAchievementsUpdated?.Invoke();
        return true;
    }
}
