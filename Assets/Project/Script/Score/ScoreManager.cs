using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [Header("Current Score")]
    [SerializeField] private int currentScore = 0;

    public int CurrentScore => currentScore;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void SetScore(int newScore)
    {
        currentScore = Mathf.Max(0, newScore);

        UpdateUI();
    }

    public void ResetScore()
    {
        currentScore = 0;

        UpdateUI();
    }

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