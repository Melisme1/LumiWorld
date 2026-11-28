using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs score gated tutorial steps and controls the tutorial presentation.
/// Scene objects and step content are assigned through the Inspector.
/// </summary>
public class ScoreTutorialController : MonoBehaviour
{
    [Header("Progress")]
    [SerializeField] private string progressKey = "LumiWorld.ScoreTutorial.v1";
    [SerializeField] private List<ScoreTutorialStep> steps = new();
    [SerializeField] private bool replayCompletedSteps;

    [Header("Scene UI")]
    [SerializeField] private ScoreTutorialOverlay overlay;

    private int nextStepIndex;
    private ScoreManager subscribedScoreManager;

    private void Awake()
    {
        if (overlay == null)
            overlay = GetComponentInChildren<ScoreTutorialOverlay>(true);

        nextStepIndex = replayCompletedSteps
            ? 0
            : Mathf.Clamp(PlayerPrefs.GetInt(progressKey, 0), 0, steps.Count);

        if (overlay != null)
        {
            overlay.ContinueRequested += HandleContinue;
            overlay.Hide();
        }
    }

    private void OnEnable()
    {
        TrySubscribeToScoreManager();
    }

    private void Start()
    {
        TrySubscribeToScoreManager();
    }

    private void Update()
    {
        // ScoreManager may be created or enabled after this controller during world startup.
        if (subscribedScoreManager == null)
            TrySubscribeToScoreManager();

        // Also reconcile current state so score changes during New Game/world restore cannot be missed.
        if (subscribedScoreManager != null)
            HandleScoreChanged(subscribedScoreManager.CurrentScore);
    }

    private void OnDisable()
    {
        UnsubscribeFromScoreManager();
    }

    private void OnDestroy()
    {
        if (overlay != null)
            overlay.ContinueRequested -= HandleContinue;
    }

    private void HandleScoreChanged(int score)
    {
        if (overlay == null || nextStepIndex >= steps.Count || overlay.IsShowing)
            return;

        ScoreTutorialStep step = steps[nextStepIndex];
        if (step == null || score < step.scoreThreshold)
            return;

        overlay.Show(step);
    }

    private void TrySubscribeToScoreManager()
    {
        ScoreManager current = ScoreManager.Instance;
        if (current == null || current == subscribedScoreManager)
            return;

        UnsubscribeFromScoreManager();
        subscribedScoreManager = current;
        subscribedScoreManager.OnScoreChanged += HandleScoreChanged;
        HandleScoreChanged(subscribedScoreManager.CurrentScore);
    }

    private void UnsubscribeFromScoreManager()
    {
        if (subscribedScoreManager != null)
            subscribedScoreManager.OnScoreChanged -= HandleScoreChanged;

        subscribedScoreManager = null;
    }

    private void HandleContinue()
    {
        nextStepIndex = Mathf.Min(nextStepIndex + 1, steps.Count);
        PlayerPrefs.SetInt(progressKey, nextStepIndex);
        PlayerPrefs.Save();

        if (overlay != null)
            overlay.Hide();

        if (nextStepIndex < steps.Count && ScoreManager.Instance != null)
            HandleScoreChanged(ScoreManager.Instance.CurrentScore);
    }
}
