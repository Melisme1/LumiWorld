using TMPro;
using UnityEngine;

public class ScoreUI : MonoBehaviour
{
    public static ScoreUI Instance;

    [SerializeField] private TMP_Text scoreText;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        UpdateScore(0);
    }

    public void UpdateScore(int score)
    {
        if (scoreText == null)
            return;

        scoreText.text = score.ToString();
    }
}