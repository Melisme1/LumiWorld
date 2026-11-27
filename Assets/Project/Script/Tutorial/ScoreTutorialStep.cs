using UnityEngine;

public enum ScoreTutorialPointerMode
{
    None,
    Click,
    Drag
}

/// <summary>
/// One score gated tutorial step configured from the Scene Inspector.
/// </summary>
[System.Serializable]
public class ScoreTutorialStep
{
    [Min(0)] public int scoreThreshold;
    [TextArea(4, 10)] public string purpose;
    [Tooltip("Scene RectTransform that defines the position and size of the tutorial hole.")]
    public RectTransform customHighlightArea;
    [Min(0)] public float holePadding = 12f;
    public ScoreTutorialPointerMode pointerMode;
    [Tooltip("For Drag mode, pointer starts this many canvas units from the target center.")]
    public Vector2 dragStartOffset = new Vector2(-100f, 0f);
}
