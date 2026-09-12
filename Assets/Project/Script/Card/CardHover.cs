using UnityEngine;
using UnityEngine.EventSystems;

public class CardHover : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("Hover")]
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float hoverOffsetY = 25f;
    [SerializeField] private float animationSpeed = 10f;

    private RectTransform rectTransform;
    private CardHandSlot handSlot;
    private CardDrag cardDrag;

    private Vector3 normalScale;

    private Vector3 targetScale;
    private Vector2 targetPosition;

    private int originalSiblingIndex;

    private bool isHovering;

    private void Awake()
    {
        rectTransform =
            GetComponent<RectTransform>();

        handSlot =
            GetComponent<CardHandSlot>();

        cardDrag =
            GetComponent<CardDrag>();

        normalScale = Vector3.one;
        targetScale = normalScale;

        targetPosition =
            GetHomePosition();
    }

    private void Update()
    {
        // =====================================
        // DO NOT INTERFERE WITH DRAG
        // =====================================

        if (cardDrag != null &&
            (cardDrag.IsDragging || cardDrag.IsReturningToHand))
        {
            return;
        }

        // =====================================
        // FOLLOW HOME POSITION
        // =====================================

        if (!isHovering)
        {
            targetPosition =
                GetHomePosition();
        }

        // =====================================
        // SCALE
        // =====================================

        if ((rectTransform.localScale - targetScale).sqrMagnitude > 0.0001f)
        {
            rectTransform.localScale =
                Vector3.Lerp(
                    rectTransform.localScale,
                    targetScale,
                    Time.deltaTime * animationSpeed
                );
        }
        else if (rectTransform.localScale != targetScale)
        {
            rectTransform.localScale = targetScale;
        }

        // =====================================
        // POSITION
        // =====================================

        if ((rectTransform.anchoredPosition - targetPosition).sqrMagnitude > 0.01f)
        {
            rectTransform.anchoredPosition =
                Vector2.Lerp(
                    rectTransform.anchoredPosition,
                    targetPosition,
                    Time.deltaTime * animationSpeed
                );
        }
        else if (rectTransform.anchoredPosition != targetPosition)
        {
            rectTransform.anchoredPosition = targetPosition;
        }
    }

    public void OnPointerEnter(
        PointerEventData eventData)
    {
        // Don't activate hover while dragging
        if (cardDrag != null &&
            (cardDrag.IsDragging || cardDrag.IsReturningToHand))
        {
            return;
        }

        isHovering = true;

        originalSiblingIndex =
            transform.GetSiblingIndex();

        // Bring card to front
        transform.SetAsLastSibling();

        targetScale =
            normalScale * hoverScale;

        targetPosition =
            GetHomePosition() +
            Vector2.up * hoverOffsetY;
    }

    public void OnPointerExit(
        PointerEventData eventData)
    {
        isHovering = false;

        targetScale =
            normalScale;

        targetPosition =
            GetHomePosition();

        transform.SetSiblingIndex(
            originalSiblingIndex
        );
    }

    // Called by CardDrag before it records the card's original sibling index.
    // Without this, a hovered card is recorded as the last sibling and stays
    // above every other card after a failed placement.
    public void EndHoverForDrag()
    {
        if (!isHovering)
        {
            return;
        }

        isHovering = false;
        targetScale = normalScale;
        targetPosition = GetHomePosition();
        transform.SetSiblingIndex(originalSiblingIndex);
    }

    private Vector2 GetHomePosition()
    {
        if (handSlot != null)
        {
            return handSlot.TargetPosition;
        }

        return rectTransform.anchoredPosition;
    }
}
