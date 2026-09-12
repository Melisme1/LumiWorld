using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [Header("Current Score")]
    [SerializeField] private int currentScore = 0;

    [Header("Card Reward")]
    [Tooltip("Cứ đạt bao nhiêu điểm thì nhận Card")]
    [SerializeField] private int scorePerReward = 10;

    [Tooltip("CardData sẽ được thưởng")]
    [SerializeField] private CardData rewardCardData;

    public int CurrentScore => currentScore;

    private int nextRewardScore;

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

        Debug.Log(
            $"Score = {currentScore}"
        );
    }

    // =========================================
    // CHECK CARD REWARD
    // =========================================

    private void CheckCardReward()
    {
        if (scorePerReward <= 0)
            return;

        while (currentScore >= nextRewardScore)
        {
            GiveCardReward();

            nextRewardScore +=
                scorePerReward;
        }
    }

    // =========================================
    // GIVE CARD
    // =========================================

    private void GiveCardReward()
    {
        if (rewardCardData == null)
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
            rewardCardData
        );

        Debug.Log(
            $"Đạt {nextRewardScore} điểm → +1 Card: {rewardCardData.cardName}"
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