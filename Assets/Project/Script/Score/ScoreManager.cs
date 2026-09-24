using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ScoreRewardMilestone
{
    [Min(0)] public int scoreThreshold;
    public List<CardData> rewardCards = new();
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

    public int CurrentScore => currentScore;

    private int nextRewardScore;
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

        for (int i = 0; i < rewardMilestones.Count; i++)
        {
            if (rewardMilestones[i] != null)
            {
                milestoneIndices.Add(i);
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

            if (milestone.rewardCards == null)
            {
                continue;
            }

            foreach (CardData rewardCard in milestone.rewardCards)
            {
                GiveCardReward(rewardCard, milestone.scoreThreshold);
            }
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
