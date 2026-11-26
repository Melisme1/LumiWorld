using UnityEngine;

/// <summary>
/// One score gated tutorial step configured from the Scene Inspector.
/// </summary>
[System.Serializable]
public class ScoreTutorialStep
{
    [Min(0)] public int scoreThreshold;
    public string title;
    [TextArea(2, 4)] public string purpose;
    [TextArea(3, 6)] public string instructions;
    [Tooltip("Optional highlight image created and positioned by you in the Scene.")]
    public GameObject highlightImage;
}
