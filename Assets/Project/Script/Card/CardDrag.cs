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
    private RectTransform parentRect;
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

    // Độ lệch giữa con trỏ và tâm lá lúc bắt đầu kéo, đo theo ĐƠN VỊ CỦA LÁ (đã bỏ scale).
    // Vì card scale quanh tâm (pivot 0.5,0.5) và scale thay đổi khi kéo, nếu lưu offset
    // theo đơn vị tuyệt đối thì mỗi lần đổi scale lá sẽ trượt khỏi con trỏ -> cảm giác
    // "không nhất quán". Lưu theo tỉ lệ nội suy (localRatio) rồi nhân lại với scale hiện
    // tại mỗi frame để điểm nắm luôn nằm đúng dưới con trỏ.
    private Vector2 dragGrabOffset;

    // Important:
    // CardHover uses this to know whether
    // the card is currently being dragged.
    public bool IsDragging { get; private set; }
    public bool IsReturningToHand { get; private set; }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        parentRect = rectTransform.parent as RectTransform;
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
        IsReturningToHand = false;
        targetTilt = 0f;
        currentTilt = 0f;
        currentPointerPosition = eventData.position;

        // Ghi lại điểm "nắm" lá: khoảng cách từ con trỏ tới tâm lá tại thời điểm bắt đầu.
        // Chia cho scale lúc bắt đầu để quy về "đơn vị gốc" (scale 1). Nhờ vậy:
        //  - Lá không bị giật về tâm chuột (giữ đúng chỗ đã nắm).
        //  - Khi lá thu nhỏ/phóng to trong lúc kéo, OnDrag nhân lại với scale hiện tại
        //    để điểm nắm vẫn nằm đúng dưới con trỏ.
        float beginScale = Mathf.Max(0.0001f, transform.localScale.x);
        dragGrabOffset = (rectTransform.anchoredPosition - ScreenToCardParentPosition(eventData.position)) / beginScale;

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

        // Hover moves a card to the last sibling. Restore its normal hand
        // order before remembering the index used to return from dragging.
        CardHover cardHover = GetComponent<CardHover>();
        if (cardHover != null)
        {
            cardHover.EndHoverForDrag();
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

        // Đặt lá theo vị trí TUYỆT ĐỐI của con trỏ + độ lệch đã nắm.
        // Nhân offset với scale hiện tại vì lá scale quanh tâm: khi thu nhỏ, điểm nắm
        // phải co lại theo để vẫn nằm đúng dưới con trỏ (nếu không lá sẽ trượt khỏi chuột).
        float scaleFactor = transform.localScale.x;
        rectTransform.anchoredPosition = ScreenToCardParentPosition(eventData.position) + dragGrabOffset * scaleFactor;

        // Cập nhật góc nghiêng theo vận tốc kéo chuột sang trái/phải
        float deltaX = eventData.delta.x;
        targetTilt = Mathf.Clamp(-deltaX * tiltStrength, -maxTiltAngle, maxTiltAngle);

        if (singlePlacementController != null)
        {
            singlePlacementController.UpdatePreview(eventData.position);
        }
    }

    /// <summary>
    /// Đổi vị trí con trỏ (screen pixel) sang toạ độ anchoredPosition của LÁ BÀI.
    ///
    /// Phải dùng đúng parent RectTransform của lá (cardContainer) làm hệ quy chiếu,
    /// KHÔNG dùng RectTransform của Canvas: anchoredPosition của lá được đo so với
    /// pivot của parent, còn toạ độ tính từ Canvas sẽ lệch đúng bằng độ lệch của
    /// cardContainer so với tâm Canvas -> lá trôi khỏi con trỏ khi kéo.
    /// </summary>
    private Vector2 ScreenToCardParentPosition(Vector2 screenPosition)
    {
        RectTransform reference = parentRect != null ? parentRect : canvas?.transform as RectTransform;
        if (reference == null) return screenPosition;

        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = canvas.worldCamera;
        }

        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                reference,
                screenPosition,
                cam,
                out localPoint))
        {
            return localPoint;
        }

        return screenPosition / (canvas != null ? canvas.scaleFactor : 1f);
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

        bool placedSuccessfully = false;

        if (singlePlacementController != null && cardUI != null)
        {
            placedSuccessfully = singlePlacementController.TryConfirmPlacement(cardUI.CardData, out _);
        }

        // Keep the dragged card out of the UI raycast until placement checks the drop target.
        // Otherwise its own Image would be mistaken for a panel blocking the map.
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

        if (placedSuccessfully)
        {
            // Đặt thành công: khôi phục alpha/scale về bình thường trước khi rời tay.
            // Nếu chồng còn lá (slot được giữ lại), card phải trở về trạng thái rõ nét.
            RestoreVisualAfterPlacement();

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

    /// <summary>
    /// Khôi phục alpha và scale của card về trạng thái bình thường sau khi đặt thành công.
    /// CardDrag.Update chỉ chạy khi IsDragging nên nếu không làm bước này,
    /// alpha sẽ kẹt ở giá trị mờ (tileHoverAlpha / fieldDragAlpha) khi slot còn lá.
    /// </summary>
    private void RestoreVisualAfterPlacement()
    {
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }

    public void ReturnToHand()
    {
        if (handSlot == null) return;

        IsReturningToHand = true;
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

        // Alpha được hồi từ mức mờ lúc kéo -> 1.0. CardDrag là nguồn duy nhất ghi
        // canvasGroup.alpha trong giai đoạn này (CardAppear tự nhường quyền qua
        // IsDragOwned), nên alpha không bị animation khác đè ngược về trạng thái mờ.
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
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, t);
            }

            yield return null;
        }

        RestoreVisualAfterPlacement();

        transform.SetSiblingIndex(originalSiblingIndex);
        IsReturningToHand = false;
        returnCoroutine = null;

        // Thông báo cho CardHover biết card đã yên vị để nó đồng bộ lại target.
        CardHover cardHover = GetComponent<CardHover>();
        if (cardHover != null)
        {
            cardHover.SyncAfterReturn();
        }
    }
}
