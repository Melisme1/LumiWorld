using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardDrag : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [Header("Return Animation")]
    [SerializeField] private float returnDuration = 0.25f;

    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;

    private CardHandSlot handSlot;

    private int originalSiblingIndex;

    private Coroutine returnCoroutine;

    // Important:
    // CardHover uses this to know whether
    // the card is currently being dragged.
    public bool IsDragging { get; private set; }

    private void Awake()
    {
        rectTransform =
            GetComponent<RectTransform>();

        canvas =
            GetComponentInParent<Canvas>();

        canvasGroup =
            GetComponent<CanvasGroup>();

        handSlot =
            GetComponent<CardHandSlot>();
    }

    public void OnBeginDrag(
        PointerEventData eventData)
    {
        IsDragging = true;

        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        originalSiblingIndex =
            transform.GetSiblingIndex();

        // Put card above other cards
        transform.SetAsLastSibling();

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void OnDrag(
        PointerEventData eventData)
    {
        if (canvas == null)
        {
            return;
        }

        rectTransform.anchoredPosition +=
            eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(
        PointerEventData eventData)
    {
        IsDragging = false;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        ReturnToHand();
    }

    public void ReturnToHand()
    {
        if (handSlot == null)
        {
            return;
        }

        Vector2 targetPosition =
            handSlot.TargetPosition;

        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
        }

        returnCoroutine =
            StartCoroutine(
                ReturnAnimation(
                    targetPosition
                )
            );
    }

    private IEnumerator ReturnAnimation(
        Vector2 targetPosition
    )
    {
        Vector2 startPosition =
            rectTransform.anchoredPosition;

        float elapsed = 0f;

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                elapsed / returnDuration;

            t = Mathf.SmoothStep(
                0f,
                1f,
                t
            );

            rectTransform.anchoredPosition =
                Vector2.Lerp(
                    startPosition,
                    targetPosition,
                    t
                );

            yield return null;
        }

        rectTransform.anchoredPosition =
            targetPosition;

        transform.SetSiblingIndex(
            originalSiblingIndex
        );

        returnCoroutine = null;
    }
}