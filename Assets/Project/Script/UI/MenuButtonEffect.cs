using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hiệu ứng Hover / Press cho các nút ở Main Menu.
///
/// - Hover: phóng to nhẹ, sáng lên, nâng nhẹ lên trên.
/// - Press: thu nhỏ lại, tối đi, hạ xuống — cảm giác "được nhấn".
///
/// Gắn script này lên chính GameObject của Button (hoặc lên parent chứa visual
/// của nút). Script tự tìm Selectable ở component hoặc ở parent gần nhất để biết
/// khi nào nút bị disable (interactable = false).
/// </summary>
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class MenuButtonEffect : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler
{
    // ========================================
    // HOVER
    // ========================================

    [Header("Hover")]
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float hoverOffsetY = 8f;
    [SerializeField] private float hoverBrightness = 1.08f;


    // ========================================
    // PRESS
    // ========================================

    [Header("Press")]
    [SerializeField] private float pressScale = 0.95f;
    [SerializeField] private float pressOffsetY = -6f;
    [SerializeField] private float pressBrightness = 0.9f;


    // ========================================
    // MOTION
    // ========================================

    [Header("Motion")]
    [SerializeField] private float animationSpeed = 14f;
    [Tooltip("Cho phép trạng thái Keyboard/Gamepad focus cũng kích hoạt hiệu ứng hover.")]
    [SerializeField] private bool hoverOnSelect = true;


    // ========================================

    private RectTransform rectTransform;
    private Graphic targetGraphic;
    private Selectable selectable;

    private Vector3 baseScale;
    private Vector2 basePosition;
    private Color baseColor;

    private Vector3 targetScale;
    private Vector2 targetPosition;
    private float targetBrightness = 1f;

    private bool isHovering;
    private bool isPressed;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        selectable = GetComponent<Selectable>();

        if (selectable == null)
        {
            selectable = GetComponentInParent<Selectable>();
        }

        // Lấy graphic để đổi độ sáng. Ưu tiên Image của chính nút,
        // nếu là parent của Button thì lấy Image của Button con.
        targetGraphic = GetComponent<Graphic>();

        if (targetGraphic == null && selectable != null)
        {
            targetGraphic = selectable.targetGraphic;
        }

        if (targetGraphic == null)
        {
            targetGraphic = GetComponentInChildren<Graphic>();
        }

        baseScale = rectTransform.localScale;
        basePosition = rectTransform.anchoredPosition;

        if (targetGraphic != null)
        {
            baseColor = targetGraphic.color;
        }

        targetScale = baseScale;
        targetPosition = basePosition;
    }

    private void OnEnable()
    {
        // Trở về trạng thái nghỉ khi được bật lại, tránh dính hiệu ứng cũ.
        isHovering = false;
        isPressed = false;
        ApplyTargets();
    }

    private void Update()
    {
        if (!IsInteractable())
        {
            // Nút bị disable (ví dụ Continue khi chưa có save) -> luôn về trạng thái nghỉ.
            if (isHovering || isPressed)
            {
                isHovering = false;
                isPressed = false;
                ApplyTargets();
            }
        }

        // =====================================
        // SCALE
        // =====================================

        if ((rectTransform.localScale - targetScale).sqrMagnitude > 0.000001f)
        {
            rectTransform.localScale = Vector3.Lerp(
                rectTransform.localScale,
                targetScale,
                Time.unscaledDeltaTime * animationSpeed
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
            rectTransform.anchoredPosition = Vector2.Lerp(
                rectTransform.anchoredPosition,
                targetPosition,
                Time.unscaledDeltaTime * animationSpeed
            );
        }
        else if (rectTransform.anchoredPosition != targetPosition)
        {
            rectTransform.anchoredPosition = targetPosition;
        }

        // =====================================
        // BRIGHTNESS
        // =====================================

        if (targetGraphic != null)
        {
            Color current = targetGraphic.color;

            if (Mathf.Abs(current.r - baseColor.r * targetBrightness) > 0.001f ||
                Mathf.Abs(current.g - baseColor.g * targetBrightness) > 0.001f ||
                Mathf.Abs(current.b - baseColor.b * targetBrightness) > 0.001f)
            {
                Color wanted = new Color(
                    baseColor.r * targetBrightness,
                    baseColor.g * targetBrightness,
                    baseColor.b * targetBrightness,
                    baseColor.a
                );

                targetGraphic.color = Color.Lerp(
                    current,
                    wanted,
                    Time.unscaledDeltaTime * animationSpeed
                );
            }
            else if (current != baseColor)
            {
                targetGraphic.color = new Color(
                    baseColor.r * targetBrightness,
                    baseColor.g * targetBrightness,
                    baseColor.b * targetBrightness,
                    baseColor.a
                );
            }
        }
    }

    // ========================================
    // POINTER EVENTS
    // ========================================

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsInteractable())
            return;

        isHovering = true;
        ApplyTargets();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        isPressed = false;
        ApplyTargets();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsInteractable())
            return;

        isPressed = true;
        ApplyTargets();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;

        // Chuột có thể đã rời khỏi nút khi nhả -> cập nhật lại hover đúng trạng thái.
        if (!RectTransformUtility.RectangleContainsScreenPoint(
                rectTransform,
                eventData.position,
                eventData.pressEventCamera))
        {
            isHovering = false;
        }

        ApplyTargets();
    }

    // ========================================
    // KEYBOARD / GAMEPAD FOCUS
    // ========================================

    public void OnSelect(BaseEventData eventData)
    {
        if (!hoverOnSelect || !IsInteractable())
            return;

        isHovering = true;
        ApplyTargets();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isHovering = false;
        isPressed = false;
        ApplyTargets();
    }

    // ========================================

    private void ApplyTargets()
    {
        if (isPressed)
        {
            targetScale = baseScale * pressScale;
            targetPosition = basePosition + Vector2.up * pressOffsetY;
            targetBrightness = pressBrightness;
            return;
        }

        if (isHovering)
        {
            targetScale = baseScale * hoverScale;
            targetPosition = basePosition + Vector2.up * hoverOffsetY;
            targetBrightness = hoverBrightness;
            return;
        }

        targetScale = baseScale;
        targetPosition = basePosition;
        targetBrightness = 1f;
    }

    private bool IsInteractable()
    {
        return selectable == null || selectable.IsInteractable();
    }

    /// <summary>
    /// Cho phép gọi khi trạng thái interactable thay đổi runtime
    /// (ví dụ Continue button vừa được bật) để reset về trạng thái nghỉ.
    /// </summary>
    public void ResetVisual()
    {
        isHovering = false;
        isPressed = false;
        ApplyTargets();

        rectTransform.localScale = targetScale;
        rectTransform.anchoredPosition = targetPosition;

        if (targetGraphic != null)
        {
            targetGraphic.color = baseColor;
        }
    }
}
