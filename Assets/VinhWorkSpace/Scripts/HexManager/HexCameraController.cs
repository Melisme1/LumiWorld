using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class HexCameraController : MonoBehaviour
{
    [Header("Tốc độ & Độ nhạy")]
    [SerializeField] private float moveSpeed = 25f;
    [SerializeField] private float boostMultiplier = 2.5f;   // Giữ Shift để tăng tốc
    [SerializeField] private float panSensitivity = 0.10f;   // Kéo rê mặt phẳng bằng Chuột trái / Chuột giữa (nhạy và mượt)
    [SerializeField] private float orbitSensitivity = 0.25f; // Xoay quanh tâm (Orbit)
    [SerializeField] private float lookSensitivity = 0.2f;   // Xoay góc nhìn tự do (Look around)
    [SerializeField] private float zoomSensitivity = 0.05f;  // Zoom bằng Alt + Chuột phải
    [SerializeField] private float scrollZoomSpeed = 4f;     // Zoom bằng con lăn chuột

    [Header("Giới hạn Độ cao Zoom")]
    [SerializeField] private float minHeight = 2f;
    [SerializeField] private float maxHeight = 80f;

    [Header("Độ mượt mà (Damping)")]
    [SerializeField] private float smoothTime = 16f;

    private Vector3 targetPosition;
    private Vector3 targetEulerRotation;
    private Vector3 orbitPivotPoint;

    private HexPlacementController placementController;

    private void Start()
    {
        targetPosition = transform.position;
        targetEulerRotation = transform.eulerAngles;
        UpdateOrbitPivot();

        placementController = FindAnyObjectByType<HexPlacementController>();
    }

    private void LateUpdate()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (mouse == null) return;

        bool isAltHeld = keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
        Vector2 mouseDelta = mouse.delta.ReadValue();

        bool isOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        bool isInPlacement = placementController != null && placementController.IsInPlacementMode;

        // 1. ĐIỀU KHIỂN CHUỘT
        if (isAltHeld)
        {
            // Alt + Chuột trái: Xoay quanh tâm nhìn (Orbit quanh tâm đảo)
            if (mouse.leftButton.isPressed)
            {
                HandleOrbit(mouseDelta);
            }
            // Alt + Chuột phải: Zoom mượt mà theo chuyển động kéo chuột
            else if (mouse.rightButton.isPressed)
            {
                HandleAltZoom(mouseDelta);
            }
            // Alt + Chuột giữa: Kéo rê màn hình (Pan)
            else if (mouse.middleButton.isPressed)
            {
                HandlePan(mouseDelta);
            }
        }
        else
        {
            // Chuột giữa: Luôn luôn kéo rê (Pan)
            if (mouse.middleButton.isPressed)
            {
                HandlePan(mouseDelta);
            }
            // Chuột phải: Xoay góc nhìn tự do (Free Look)
            else if (mouse.rightButton.isPressed && !isInPlacement)
            {
                HandleFreeLook(mouseDelta);
            }
            // Chuột trái: Kéo ngang qua lại (Pan) khi KHÔNG trong chế độ đặt và không bấm UI
            else if (mouse.leftButton.isPressed && !isInPlacement && !isOverUI)
            {
                HandlePan(mouseDelta);
            }
        }

        // 2. DI CHUYỂN BẰNG PHÍM (WASD / Mũi tên)
        HandleKeyboardMovement(keyboard);

        // 3. ZOOM BẰNG CON LĂN CHUỘT (Mouse Scroll Wheel)
        HandleScrollZoom(mouse, isInPlacement, isAltHeld);

        // 4. XOAY BẰNG PHÍM Q / E (Khi không trong Placement Mode)
        if (!isInPlacement)
        {
            HandleKeyboardRotation(keyboard);
        }

        // 5. CẬP NHẬT VỊ TRÍ & GÓC XOAY MƯỢT MÀ
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(targetEulerRotation), Time.deltaTime * smoothTime);
    }

    private void HandleOrbit(Vector2 delta)
    {
        targetEulerRotation.y += delta.x * orbitSensitivity;
        targetEulerRotation.x = Mathf.Clamp(targetEulerRotation.x - delta.y * orbitSensitivity, 10f, 85f);

        Quaternion rot = Quaternion.Euler(targetEulerRotation);
        float distance = Vector3.Distance(targetPosition, orbitPivotPoint);
        targetPosition = orbitPivotPoint - (rot * Vector3.forward * distance);
    }

    private void HandleFreeLook(Vector2 delta)
    {
        targetEulerRotation.y += delta.x * lookSensitivity;
        targetEulerRotation.x = Mathf.Clamp(targetEulerRotation.x - delta.y * lookSensitivity, 10f, 85f);
        UpdateOrbitPivot();
    }

    private void HandlePan(Vector2 delta)
    {
        // 1. Tính vector mặt đất phẳng (XZ) theo góc xoay ngang của camera
        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        // 2. Tự động thích ứng tỉ lệ theo độ cao zoom hiện tại
        float heightScale = Mathf.Clamp(targetPosition.y * 0.08f, 0.5f, 4.0f);

        // 3. Kéo chuột trái/giữa: dịch chuyển mượt mà trên mặt phẳng đất
        Vector3 move = (-right * delta.x - forward * delta.y) * (panSensitivity * heightScale);

        targetPosition += move;
        orbitPivotPoint += move;
    }

    private void HandleAltZoom(Vector2 delta)
    {
        float zoomDelta = (delta.x + delta.y) * zoomSensitivity;
        ApplyZoom(zoomDelta);
    }

    private void HandleScrollZoom(Mouse mouse, bool isInPlacement, bool isAltHeld)
    {
        float scrollY = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scrollY) > 0.001f)
        {
            // Nếu đang trong chế độ đặt, con lăn chuột ưu tiên dùng để xoay khối lục giác (trừ khi giữ phím Alt)
            if (!isInPlacement || isAltHeld)
            {
                float notches = Mathf.Sign(scrollY) * (Mathf.Abs(scrollY) >= 10f ? Mathf.Abs(scrollY) / 120f : 1f);
                float zoomAmount = notches * scrollZoomSpeed;
                ApplyZoom(zoomAmount);
            }
        }
    }

    private void ApplyZoom(float amount)
    {
        Vector3 newPos = targetPosition + transform.forward * amount;
        newPos.y = Mathf.Clamp(newPos.y, minHeight, maxHeight);
        targetPosition = newPos;
    }

    private void HandleKeyboardMovement(Keyboard keyboard)
    {
        if (keyboard == null) return;

        Vector3 moveDir = Vector3.zero;
        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        fwd.Normalize();

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveDir += fwd;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveDir -= fwd;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveDir += right;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveDir -= right;

        if (moveDir.sqrMagnitude > 0.001f)
        {
            float speed = moveSpeed;
            if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
            {
                speed *= boostMultiplier;
            }

            Vector3 move = moveDir.normalized * (speed * Time.deltaTime);
            targetPosition += move;
            orbitPivotPoint += move;
        }
    }

    private void HandleKeyboardRotation(Keyboard keyboard)
    {
        if (keyboard == null) return;

        float rot = 0f;
        if (keyboard.qKey.isPressed) rot -= 1f;
        if (keyboard.eKey.isPressed) rot += 1f;

        if (rot != 0f)
        {
            targetEulerRotation.y += rot * 75f * Time.deltaTime;
            UpdateOrbitPivot();
        }
    }

    private void UpdateOrbitPivot()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float enter))
        {
            orbitPivotPoint = ray.GetPoint(enter);
        }
        else
        {
            orbitPivotPoint = transform.position + transform.forward * 20f;
        }
    }
}
