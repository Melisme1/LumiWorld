using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ScoreRewardMilestone
{
    [Min(0)] public int scoreThreshold;
    public List<CardData> rewardCards = new();

    [Tooltip("Đạt mốc này có được thưởng thêm 1 lượt nhấn Create để mở rộng đất vào map không?")]
    public bool grantLandExpansion = true;
}

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    public event Action<int> OnScoreChanged;
    public event Action<ScoreRewardMilestone> OnMilestoneReached;

    [Header("Current Score")]
    [SerializeField] private int currentScore = 0;


    private int scorePerReward = 10;
    private CardData rewardCardData;

    [Header("Milestone Rewards")]
    [Tooltip("Each milestone is granted once. The legacy Card Reward fields are used only when this list is empty.")]
    [SerializeField] private List<ScoreRewardMilestone> rewardMilestones = new();

    [Header("Endless / Repeatable Rewards")]
    [Tooltip("Khi vượt qua mốc điểm cao nhất trong danh sách, cứ sau mỗi bao nhiêu điểm sẽ tự động thưởng thêm 1 đợt bài?")]
    [SerializeField] private int endlessInterval = 30;

    [Tooltip("Thẻ Rain dùng để cấp định kỳ trong chế độ Endless")]
    [SerializeField] private CardData endlessRainCard;

    [Tooltip("Bể thẻ bài địa hình và sinh vật dùng để bốc ngẫu nhiên khi đạt mốc Endless")]
    [SerializeField] private List<CardData> endlessRewardPool = new();

    public int CurrentScore => currentScore;
    public int GrantedMilestoneCount => grantedMilestoneIndices.Count;

    private int nextRewardScore;
    private int nextEndlessThreshold = -1;
    private readonly HashSet<int> grantedMilestoneIndices = new();

    // Điểm không đến từ thẻ đã đặt (combo thú). Tổng điểm = điểm thẻ + bonusScore; lưu trong world_<id>.json
    private int bonusScore;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        currentScore = 0;
        grantedMilestoneIndices.Clear();
        nextEndlessThreshold = -1;

        nextRewardScore =
            scorePerReward;

        // Đảm bảo các mốc thưởng Milestone và Endless không bao giờ chứa hoặc cấp thẻ thú (Creature Cards)
        FilterOutCreatureCards();
    }

    private void OnValidate()
    {
        FilterOutCreatureCards();
    }

    /// <summary>
    /// Loại bỏ toàn bộ thẻ sinh vật (Creature Cards) khỏi các mốc thưởng Milestone và bể Endless.
    /// Sinh vật chỉ có thể nhận được thông qua cơ chế thuần hóa Thú Hoang (Wild Incident) hoặc tương tác gameplay.
    /// </summary>
    public void FilterOutCreatureCards()
    {
        if (rewardMilestones != null)
        {
            foreach (var m in rewardMilestones)
            {
                if (m != null && m.rewardCards != null)
                {
                    m.rewardCards.RemoveAll(c => c == null || c.cardType == CardType.Creature);
                }
            }
        }

        if (endlessRewardPool != null)
        {
            endlessRewardPool.RemoveAll(c => c == null || c.cardType == CardType.Creature);
        }
    }

    // =========================================
    // SET SCORE
    // =========================================

    public void SetScore(int newScore)
    {
        // Đảo đang xem của người khác không được cộng điểm hay mốc
        if (WorldLaunch.IsVisit) return;

        currentScore =
            Mathf.Max(0, newScore);

        UpdateUI();

        int grantedBefore = grantedMilestoneIndices.Count;
        int endlessBefore = nextEndlessThreshold;
        int rewardBefore = nextRewardScore;

        CheckCardReward();

        // Vừa trao thưởng mốc: ghi đảo để mốc đó không được trao lại sau khi mở lại game
        if (grantedMilestoneIndices.Count != grantedBefore || nextEndlessThreshold != endlessBefore || nextRewardScore != rewardBefore)
            WorldSaveSystem.MarkDirty();

        OnScoreChanged?.Invoke(currentScore);
    }

    /// <summary>Đặt điểm theo tổng điểm thẻ đang có trên đảo, giữ nguyên điểm combo.</summary>
    public void SetCardScore(int cardScore)
    {
        SetScore(cardScore + bonusScore);
    }

    /// <summary>Cộng điểm ngoài thẻ (combo thú). Được giữ lại khi tính lại điểm thẻ và khi mở lại game.</summary>
    public void AddBonusScore(int points)
    {
        if (WorldLaunch.IsVisit || points == 0) return;
        bonusScore = Mathf.Max(0, bonusScore + points);
        WorldSaveSystem.MarkDirty();
        SetScore(currentScore + points);
    }

    /// <summary>Ghi điểm combo, mốc đã thưởng và mốc Endless kế tiếp vào bản lưu đảo.</summary>
    public void CaptureProgress(WorldProgressData progress)
    {
        progress.bonusScore = bonusScore;
        progress.nextEndlessThreshold = nextEndlessThreshold;
        progress.nextRewardScore = nextRewardScore;
        progress.grantedMilestoneThresholds.Clear();
        foreach (int index in grantedMilestoneIndices)
        {
            if (index >= 0 && index < rewardMilestones.Count && rewardMilestones[index] != null)
                progress.grantedMilestoneThresholds.Add(rewardMilestones[index].scoreThreshold);
        }
    }

    /// <summary>
    /// Dựng lại điểm và các mốc đã thưởng từ bản lưu, không trao thưởng và không phát sự kiện.
    /// Mốc nằm giữa điểm cũ và điểm mới (nếu có) sẽ được trao ở lần đổi điểm kế tiếp.
    /// </summary>
    public void RestoreProgress(int savedCardScore, WorldProgressData progress)
    {
        bonusScore = Mathf.Max(0, progress.bonusScore);
        currentScore = Mathf.Max(0, savedCardScore + bonusScore);

        grantedMilestoneIndices.Clear();
        for (int i = 0; i < rewardMilestones.Count; i++)
        {
            ScoreRewardMilestone milestone = rewardMilestones[i];
            if (milestone != null && progress.grantedMilestoneThresholds.Contains(milestone.scoreThreshold))
                grantedMilestoneIndices.Add(i);
        }

        nextEndlessThreshold = progress.nextEndlessThreshold;
        nextRewardScore = progress.nextRewardScore > 0
            ? progress.nextRewardScore
            : (scorePerReward > 0 ? (currentScore / scorePerReward + 1) * scorePerReward : 0);
        UpdateUI();
    }

    /// <summary>Rebuilds displayed score from saved cards without granting milestone cards a second time.</summary>
    public void RestoreScoreWithoutRewards(int savedCardScore)
    {
        currentScore = Mathf.Max(0, savedCardScore);
        grantedMilestoneIndices.Clear();
        int maxThreshold = 0;
        for (int i = 0; i < rewardMilestones.Count; i++)
        {
            ScoreRewardMilestone milestone = rewardMilestones[i];
            if (milestone == null) continue;
            maxThreshold = Mathf.Max(maxThreshold, milestone.scoreThreshold);
            if (currentScore >= milestone.scoreThreshold) grantedMilestoneIndices.Add(i);
        }
        nextRewardScore = scorePerReward > 0 ? (currentScore / scorePerReward + 1) * scorePerReward : 0;
        nextEndlessThreshold = maxThreshold > 0 && endlessInterval > 0
            ? maxThreshold + (Mathf.Max(0, currentScore - maxThreshold) / endlessInterval + 1) * endlessInterval
            : -1;
        UpdateUI();
    }

    // =========================================
    // CHECK CARD REWARD
    // =========================================

    private void CheckCardReward()
    {
        if (rewardMilestones.Count > 0)
        {
            CheckMilestoneRewards();
            return;
        }

        if (scorePerReward <= 0)
            return;

        while (currentScore >= nextRewardScore)
        {
            GiveCardReward(rewardCardData, nextRewardScore);

            nextRewardScore +=
                scorePerReward;
        }
    }

    private void CheckMilestoneRewards()
    {
        List<int> milestoneIndices = new();
        int maxThreshold = 0;

        for (int i = 0; i < rewardMilestones.Count; i++)
        {
            if (rewardMilestones[i] != null)
            {
                milestoneIndices.Add(i);
                if (rewardMilestones[i].scoreThreshold > maxThreshold)
                {
                    maxThreshold = rewardMilestones[i].scoreThreshold;
                }
            }
        }

        milestoneIndices.Sort((left, right) =>
            rewardMilestones[left].scoreThreshold.CompareTo(
                rewardMilestones[right].scoreThreshold
            )
        );

        foreach (int index in milestoneIndices)
        {
            ScoreRewardMilestone milestone = rewardMilestones[index];

            if (currentScore < milestone.scoreThreshold ||
                !grantedMilestoneIndices.Add(index))
            {
                continue;
            }

            if (milestone.rewardCards != null)
            {
                foreach (CardData rewardCard in milestone.rewardCards)
                {
                    if (rewardCard != null && rewardCard.cardType != CardType.Creature)
                    {
                        GiveCardReward(rewardCard, milestone.scoreThreshold);
                    }
                }
            }

            // Thưởng 1 lượt mở rộng đất (nhấn Create) nếu mốc này kích hoạt
            if (milestone.grantLandExpansion && HexPlacementController.Instance != null)
            {
                HexPlacementController.Instance.AddExpansionCharge(1);
            }

            OnMilestoneReached?.Invoke(milestone);
        }

        // Kiểm tra phần thưởng Endless sau khi vượt qua mốc cao nhất
        CheckEndlessRewards(maxThreshold);
    }

    private void CheckEndlessRewards(int maxThreshold)
    {
        if (maxThreshold <= 0 || endlessInterval <= 0)
        {
            return;
        }

        if (nextEndlessThreshold < 0)
        {
            nextEndlessThreshold = maxThreshold + endlessInterval;
        }

        while (currentScore >= nextEndlessThreshold)
        {
            // 1. Thưởng 2 thẻ Rain để luôn có nước mở rộng đất
            if (endlessRainCard != null)
            {
                GiveCardReward(endlessRainCard, nextEndlessThreshold);
                GiveCardReward(endlessRainCard, nextEndlessThreshold);
            }

            // 2. Thưởng 2 thẻ ngẫu nhiên từ bể Endless (loại bỏ toàn bộ thẻ thú)
            if (endlessRewardPool != null && endlessRewardPool.Count > 0)
            {
                List<CardData> validPool = endlessRewardPool.FindAll(c => c != null && c.cardType != CardType.Creature);
                if (validPool.Count > 0)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        CardData randomCard = validPool[UnityEngine.Random.Range(0, validPool.Count)];
                        if (randomCard != null)
                        {
                            GiveCardReward(randomCard, nextEndlessThreshold);
                        }
                    }
                }
            }

            // 3. Thưởng 1 lượt mở rộng đất ở chế độ Endless
            if (HexPlacementController.Instance != null)
            {
                HexPlacementController.Instance.AddExpansionCharge(1);
            }

            nextEndlessThreshold += endlessInterval;
        }
    }

    // =========================================
    // GIVE CARD
    // =========================================

    private void GiveCardReward(CardData cardData, int rewardScore)
    {
        if (cardData == null)
        {
            Debug.LogWarning(
                "ScoreManager: Reward CardData chưa được gán!"
            );

            return;
        }

        // Tuyệt đối không thưởng thẻ sinh vật (Creature) từ các mốc Milestone/Điểm số
        if (cardData.cardType == CardType.Creature)
        {
            return;
        }

        if (CardDeskController.Instance == null)
        {
            Debug.LogWarning(
                "ScoreManager: Không tìm thấy CardDeskController!"
            );

            return;
        }

        CardDeskController.Instance.AddRewardCard(
            cardData
        );


    }

    // =========================================
    // RESET
    // =========================================

    public void ResetScore()
    {
        currentScore = 0;
        bonusScore = 0;

        nextRewardScore =
            scorePerReward;

        grantedMilestoneIndices.Clear();
        nextEndlessThreshold = -1;

        if (HexPlacementController.Instance != null)
        {
            HexPlacementController.Instance.ResetExpansionCharges();
        }

        UpdateUI();
        OnScoreChanged?.Invoke(0);
    }

    // =========================================
    // MILESTONE PROGRESS HELPER
    // =========================================

    /// <summary>
    /// Lấy tiến trình điểm số hướng tới mốc mở rộng đất (Land Expansion) kế tiếp.
    /// </summary>
    public void GetLandExpansionProgress(out int currentPointsInStep, out int targetPointsInStep, out float fillRatio, out int nextThreshold)
    {
        List<int> landThresholds = new();
        if (rewardMilestones != null)
        {
            foreach (var m in rewardMilestones)
            {
                if (m != null && m.grantLandExpansion && m.scoreThreshold > 0)
                {
                    if (!landThresholds.Contains(m.scoreThreshold))
                    {
                        landThresholds.Add(m.scoreThreshold);
                    }
                }
            }
        }

        landThresholds.Sort();

        // Nếu danh sách không có mốc nào có grantLandExpansion, fallback theo interval
        if (landThresholds.Count == 0)
        {
            int interval = Mathf.Max(10, endlessInterval > 0 ? endlessInterval : scorePerReward);
            int step = currentScore / interval;
            int prev = step * interval;
            nextThreshold = prev + interval;
            currentPointsInStep = currentScore - prev;
            targetPointsInStep = interval;
            fillRatio = Mathf.Clamp01((float)currentPointsInStep / targetPointsInStep);
            return;
        }

        int maxThreshold = landThresholds[^1];

        // Nếu đã vượt qua mốc cao nhất -> chuyển sang chế độ Endless
        if (currentScore >= maxThreshold)
        {
            int interval = Mathf.Max(10, endlessInterval);
            int stepsBeyond = (currentScore - maxThreshold) / interval;
            int prev = maxThreshold + stepsBeyond * interval;
            nextThreshold = prev + interval;
            currentPointsInStep = currentScore - prev;
            targetPointsInStep = interval;
            fillRatio = Mathf.Clamp01((float)currentPointsInStep / targetPointsInStep);
            return;
        }

        // Đang nằm giữa các mốc trong danh sách
        int prevThreshold = 0;
        nextThreshold = landThresholds[0];

        for (int i = 0; i < landThresholds.Count; i++)
        {
            if (currentScore < landThresholds[i])
            {
                nextThreshold = landThresholds[i];
                prevThreshold = (i > 0) ? landThresholds[i - 1] : 0;
                break;
            }
        }

        currentPointsInStep = Mathf.Max(0, currentScore - prevThreshold);
        targetPointsInStep = Mathf.Max(1, nextThreshold - prevThreshold);
        fillRatio = Mathf.Clamp01((float)currentPointsInStep / targetPointsInStep);
    }

    /// <summary>
    /// Lấy bộ 3 mốc mở rộng đất (Mốc vừa xong, Mốc đang nạp, Mốc tương lai) cùng tỉ lệ xanh của mốc đang nạp.
    /// </summary>
    public void GetMilestoneLeafTrio(out int completedThreshold, out int activeThreshold, out int upcomingThreshold, out float activeFillRatio)
    {
        List<int> landThresholds = new();
        if (rewardMilestones != null)
        {
            foreach (var m in rewardMilestones)
            {
                if (m != null && m.grantLandExpansion && m.scoreThreshold > 0)
                {
                    if (!landThresholds.Contains(m.scoreThreshold))
                    {
                        landThresholds.Add(m.scoreThreshold);
                    }
                }
            }
        }

        landThresholds.Sort();

        if (landThresholds.Count == 0)
        {
            int interval = Mathf.Max(10, endlessInterval > 0 ? endlessInterval : 30);
            int step = currentScore / interval;
            completedThreshold = Mathf.Max(0, (step - 1) * interval);
            activeThreshold = step * interval;
            upcomingThreshold = (step + 1) * interval;
            activeFillRatio = Mathf.Clamp01((float)(currentScore - completedThreshold) / interval);
            return;
        }

        int maxThreshold = landThresholds[^1];

        if (currentScore >= maxThreshold)
        {
            int interval = Mathf.Max(10, endlessInterval);
            int steps = (currentScore - maxThreshold) / interval;
            completedThreshold = maxThreshold + (steps - 1) * interval;
            activeThreshold = maxThreshold + steps * interval;
            upcomingThreshold = activeThreshold + interval;
            activeFillRatio = Mathf.Clamp01((float)(currentScore - completedThreshold) / interval);
            return;
        }

        // Tìm vị trí của mốc hiện tại
        int activeIdx = 0;
        for (int i = 0; i < landThresholds.Count; i++)
        {
            if (currentScore < landThresholds[i])
            {
                activeIdx = i;
                break;
            }
        }

        activeThreshold = landThresholds[activeIdx];
        completedThreshold = (activeIdx > 0) ? landThresholds[activeIdx - 1] : 0;
        upcomingThreshold = (activeIdx + 1 < landThresholds.Count)
            ? landThresholds[activeIdx + 1]
            : activeThreshold + Mathf.Max(10, endlessInterval);

        int curInStep = Mathf.Max(0, currentScore - completedThreshold);
        int targetInStep = Mathf.Max(1, activeThreshold - completedThreshold);
        activeFillRatio = Mathf.Clamp01((float)curInStep / targetInStep);
    }

    // =========================================
    // UI
    // =========================================

    private void UpdateUI()
    {
        if (ScoreUI.Instance != null)
        {
            ScoreUI.Instance.UpdateScore(
                currentScore
            );
        }
    }
}
