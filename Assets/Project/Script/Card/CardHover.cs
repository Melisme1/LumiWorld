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
            cardDrag.IsDragging)
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

        rectTransform.localScale =
            Vector3.Lerp(
                rectTransform.localScale,
                targetScale,
                Time.deltaTime * animationSpeed
            );

        // =====================================
        // POSITION
        // =====================================

        rectTransform.anchoredPosition =
            Vector2.Lerp(
                rectTransform.anchoredPosition,
                targetPosition,
                Time.deltaTime * animationSpeed
            );
    }

    public void OnPointerEnter(
        PointerEventData eventData)
    {
        // Don't activate hover while dragging
        if (cardDrag != null &&
            cardDrag.IsDragging)
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

    private Vector2 GetHomePosition()
    {
        if (handSlot != null)
        {
            return handSlot.TargetPosition;
        }

        return rectTransform.anchoredPosition;
    }
}