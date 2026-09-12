using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CardDrag : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [Header("Return Animation")]
    [SerializeField] private float returnDuration = 0.25f;

    [Header("Drag Scaling & Visibility")]
    [Tooltip("Tỉ lệ thu nhỏ khi kéo bài ra ngoài bản đồ (mặc định 0.38x để không che khuất map)")]
    [SerializeField] private float fieldDragScale = 0.38f;

    [Tooltip("Tỉ lệ thu nhỏ siêu gọn khi đang nhắm vào 1 ô lục giác (mặc định 0.28x)")]
    [SerializeField] private float tileHoverScale = 0.28f;

    [Tooltip("Độ trong suốt khi đang nhắm vào ô để nhìn xuyên thấu địa hình 3D (0.1 - 1.0)")]
    [Range(0.1f, 1f)]
    [SerializeField] private float tileHoverAlpha = 0.40f;

    [Tooltip("Độ trong suốt khi đang lơ lửng trên bản đồ")]
    [Range(0.1f, 1f)]
    [SerializeField] private float fieldDragAlpha = 0.75f;

    [Header("Drag Feel (Juice & Motion)")]
    [Tooltip("Độ nghiêng lá bài theo quán tính khi di chuột qua lại")]
    [SerializeField] private float tiltStrength = 0.8f;
    [SerializeField] private float maxTiltAngle = 18f;

    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;

    private CardHandSlot handSlot;

    private int originalSiblingIndex;

    private Coroutine returnCoroutine;
    private HexCameraController cameraController;
    private HexSinglePlacementController singlePlacementController;
    private CardUI cardUI;
    private CardDeskController deskController;

    private float currentTilt = 0f;
    private float targetTilt = 0f;
    private Vector2 currentPointerPosition;

    // Important:
    // CardHover uses this to know whether
    // the card is currently being dragged.
    public bool IsDragging { get; private set; }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        handSlot = GetComponent<CardHandSlot>();
        cardUI = GetComponent<CardUI>();
        deskController = GetComponentInParent<CardDeskController>();
        cameraController = FindAnyObjectByType<HexCameraController>();
        singlePlacementController = FindAnyObjectByType<HexSinglePlacementController>();
    }

    private void Update()
    {
        if (!IsDragging) return;

        // 1. Xác định vị trí chuột bằng Input System (tương thích hoàn toàn với New Input System)
        Vector2 mousePos = currentPointerPosition;
        if (Mouse.current != null)
        {
            mousePos = Mouse.current.position.ReadValue();
        }

        bool isOverBoard = (mousePos.y > Screen.height * 0.20f);

        float targetScale = 1.0f;
        float targetAlpha = 0.95f;

        if (isOverBoard)
        {
            bool isOverTile = (singlePlacementController != null && singlePlacementController.IsHoveringValidTile);

            if (isOverTile)
            {
                // Khi đang nhắm trúng ô lục giác: thu nhỏ cực gọn + trong suốt cao để ngắm trọn ô 3D
                targetScale = tileHoverScale;
                targetAlpha = tileHoverAlpha;
            }
            else
            {
                // Khi đang lướt trên bản đồ
                targetScale = fieldDragScale;
                targetAlpha = fieldDragAlpha;
            }
        }
        else
        {
            // Khi kéo gần về vùng cầm bài dưới đáy màn hình
            targetScale = 1.0f;
            targetAlpha = 0.95f;
        }

        // 2. Nội suy mượt mà Kích thước (Scale)
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, Time.deltaTime * 15f);

        // 3. Nội suy mượt mà Độ trong suốt (Alpha)
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * 15f);
        }

        // 4. Nội suy mượt mà Góc nghiêng quán tính (Tilt)
        targetTilt = Mathf.Lerp(targetTilt, 0f, Time.deltaTime * 6f);
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * 15f);
        transform.localRotation = Quaternion.Euler(0f, 0f, currentTilt);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        IsDragging = true;
        targetTilt = 0f;
        currentTilt = 0f;
        currentPointerPosition = eventData.position;

        if (cameraController != null)
        {
            cameraController.SetCardDragging(true);
        }

        if (deskController == null)
        {
            deskController = FindAnyObjectByType<CardDeskController>();
        }

        if (singlePlacementController == null)
        {
            singlePlacementController = HexSinglePlacementController.Instance != null
                ? HexSinglePlacementController.Instance
                : FindAnyObjectByType<HexSinglePlacementController>();
        }

        if (singlePlacementController != null && cardUI != null)
        {
            singlePlacementController.StartPreview(cardUI.CardData);
        }

        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        originalSiblingIndex = transform.GetSiblingIndex();
        transform.SetAsLastSibling();

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (canvas == null) return;

        currentPointerPosition = eventData.position;
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;

        // Cập nhật góc nghiêng theo vận tốc kéo chuột sang trái/phải
        float deltaX = eventData.delta.x;
        targetTilt = Mathf.Clamp(-deltaX * tiltStrength, -maxTiltAngle, maxTiltAngle);

        if (singlePlacementController != null)
        {
            singlePlacementController.UpdatePreview(eventData.position);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        IsDragging = false;
        targetTilt = 0f;
        currentTilt = 0f;

        if (cameraController != null)
        {
            cameraController.SetCardDragging(false);
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        bool placedSuccessfully = false;

        if (singlePlacementController != null && cardUI != null)
        {
            placedSuccessfully = singlePlacementController.TryConfirmPlacement(cardUI.CardData, out _);
        }

        if (placedSuccessfully)
        {
            if (deskController != null)
            {
                deskController.RemoveCard(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        else
        {
            ReturnToHand();
        }
    }

    public void ReturnToHand()
    {
        if (handSlot == null) return;

        Vector2 targetPosition = handSlot.TargetPosition;

        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
        }

        returnCoroutine = StartCoroutine(ReturnAnimation(targetPosition));
    }

    private IEnumerator ReturnAnimation(Vector2 targetPosition)
    {
        Vector2 startPosition = rectTransform.anchoredPosition;
        Vector3 startScale = transform.localScale;
        Quaternion startRotation = transform.localRotation;
        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;

        float elapsed = 0f;

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / returnDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            rectTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
            transform.localScale = Vector3.Lerp(startScale, Vector3.one, t);
            transform.localRotation = Quaternion.Lerp(startRotation, Quaternion.identity, t);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1.0f, t);
            }

            yield return null;
        }

        rectTransform.anchoredPosition = targetPosition;
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1.0f;
        }

        transform.SetSiblingIndex(originalSiblingIndex);
        returnCoroutine = null;
    }
}