using UnityEngine;
using UnityEngine.EventSystems;

public class CardDrag : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;

    private Vector2 originalPosition;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Lưu vị trí ban đầu
        originalPosition = rectTransform.anchoredPosition;

        // Cho Card nằm trên các Card khác
        transform.SetAsLastSibling();

        // Cho phép Raycast xuyên qua Card
        // để sau này Raycast xuống Hex
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Di chuyển Card theo chuột
        rectTransform.anchoredPosition +=
            eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Bật Raycast lại
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        // Tạm thời trả Card về vị trí cũ
        rectTransform.anchoredPosition = originalPosition;
    }
}