using System.Collections.Generic;
using UnityEngine;

public class ScoreCalculator : MonoBehaviour
{
    public static ScoreCalculator Instance;

    [Header("Group Rules")]
    [SerializeField] private int groupRequired = 3;
    [SerializeField] private int groupMultiplier = 2;

    public int GroupRequired => groupRequired;
    public int GroupMultiplier => groupMultiplier;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // =========================================================
    // TÍNH TỔNG ĐIỂM
    // =========================================================

    public int CalculateTotalScore()
    {
        int totalScore = 0;

        PlacedCard[] allCards =
            FindObjectsByType<PlacedCard>(
                FindObjectsSortMode.None
            );

        HashSet<PlacedCard> processed =
            new HashSet<PlacedCard>();

        foreach (PlacedCard card in allCards)
        {
            if (card == null || card.cardData == null)
                continue;

            if (processed.Contains(card))
                continue;

            // Tìm group của card này
            List<PlacedCard> group =
                HexGroupDetector.Instance.FindGroup(card);

            if (group == null || group.Count == 0)
                continue;

            // Đánh dấu toàn bộ group đã xử lý
            foreach (PlacedCard groupCard in group)
            {
                processed.Add(groupCard);
            }

            // =================================================
            // GROUP >= 3
            // =================================================

            if (group.Count >= groupRequired)
            {
                foreach (PlacedCard groupCard in group)
                {
                    // Card đánh dấu alwaysBaseScore (Rain) luôn chỉ cộng baseScore, không nhân hệ số nhóm
                    if (groupCard.cardData.alwaysBaseScore)
                    {
                        totalScore +=
                            groupCard.cardData.baseScore;
                    }
                    else
                    {
                        totalScore +=
                            groupCard.cardData.baseScore
                            * groupMultiplier;
                    }
                }
            }
            else
            {
                // =================================================
                // GROUP < 3
                // =================================================

                foreach (PlacedCard groupCard in group)
                {
                    totalScore +=
                        groupCard.cardData.baseScore;
                }
            }
        }

        return totalScore;
    }

    // =========================================================
    // RECALCULATE SCORE
    // =========================================================

    public void RecalculateScore()
    {
        int totalScore = CalculateTotalScore();

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.SetScore(totalScore);
        }
    }
}