using UnityEngine;

public class CardHandSlot : MonoBehaviour
{
    private RectTransform rectTransform;

    private Vector2 targetPosition;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        targetPosition = rectTransform.anchoredPosition;
    }

    public Vector2 TargetPosition => targetPosition;

    public void SetTargetPosition(Vector2 position)
    {
        targetPosition = position;
    }
}