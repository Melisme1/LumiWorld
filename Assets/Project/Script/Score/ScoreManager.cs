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

    private int nextRewardScore;
    private int nextEndlessThreshold = -1;
    private readonly HashSet<int> grantedMilestoneIndices = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        nextRewardScore =
            scorePerReward;
    }

    // =========================================
    // SET SCORE
    // =========================================

    public void SetScore(int newScore)
    {
        currentScore =
            Mathf.Max(0, newScore);

        UpdateUI();

        CheckCardReward();

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
                    GiveCardReward(rewardCard, milestone.scoreThreshold);
                }
            }

            // Thưởng 1 lượt mở rộng đất (nhấn Create) nếu mốc này kích hoạt
            if (milestone.grantLandExpansion && HexPlacementController.Instance != null)
            {
                HexPlacementController.Instance.AddExpansionCharge(1);
            }
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

            // 2. Thưởng 2 thẻ ngẫu nhiên từ bể Endless
            if (endlessRewardPool != null && endlessRewardPool.Count > 0)
            {
                for (int i = 0; i < 2; i++)
                {
                    CardData randomCard = endlessRewardPool[UnityEngine.Random.Range(0, endlessRewardPool.Count)];
                    if (randomCard != null)
                    {
                        GiveCardReward(randomCard, nextEndlessThreshold);
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

        nextRewardScore =
            scorePerReward;

        grantedMilestoneIndices.Clear();
        nextEndlessThreshold = -1;

        if (HexPlacementController.Instance != null)
        {
            HexPlacementController.Instance.ResetExpansionCharges();
        }

        UpdateUI();
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
