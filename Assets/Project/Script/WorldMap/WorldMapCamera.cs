using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Camera trực giao của WorldMap: kéo chuột trái hoặc WASD / phím mũi tên để di chuyển, cuộn chuột để zoom,
/// không ra khỏi khung bản đồ. Một cú bấm không kéo (dưới DragThreshold pixel) được báo lên để chọn đảo.
/// Project chỉ bật Input System mới, nên dùng Mouse.current / Keyboard.current.
/// </summary>
[RequireComponent(typeof(Camera))]
public class WorldMapCamera : MonoBehaviour
{
    public const float DragThreshold = 8f;

    [SerializeField] private float minSize = 5f;
    [SerializeField] private float maxSize = 16f;
    [SerializeField] private float zoomStep = 1.2f;
    [SerializeField] private float keyPanSpeed = 14f;
    [SerializeField] private float flySeconds = 0.35f;

    public Camera Camera { get; private set; }

    /// <summary>Bấm và nhả chuột trái mà không kéo, không trúng UI: vị trí trên màn hình.</summary>
    public event System.Action<Vector2> Clicked;

    private Rect bounds = new Rect(-5f, -40f, 60f, 45f);
    private bool pressing;
    private bool dragging;
    private Vector2 pressScreen;
    private Vector3 pressWorld;
    private Vector3? flyTarget;
    private float flyVelocityX;
    private float flyVelocityY;

    private void Awake()
    {
        Camera = GetComponent<Camera>();
        Camera.orthographic = true;
        Camera.orthographicSize = Mathf.Clamp(Camera.orthographicSize, minSize, maxSize);
    }

    public void SetBounds(Rect value)
    {
        bounds = value;
        Clamp();
    }

    /// <summary>Bay tới một điểm trên biển (tâm đảo khi chọn từ ô tìm kiếm).</summary>
    public void FlyTo(Vector3 target) => flyTarget = target;

    /// <summary>Nhìn rộng nhất (nút World).</summary>
    public void ZoomOut() => SetSize(maxSize);

    /// <summary>Nhìn gần một đảo (nút Island).</summary>
    public void ZoomIn() => SetSize(minSize);

    private void SetSize(float size)
    {
        Camera.orthographicSize = Mathf.Clamp(size, minSize, maxSize);
        Clamp();
    }

    public void JumpTo(Vector3 target)
    {
        flyTarget = null;
        transform.position = new Vector3(target.x, target.y, transform.position.z);
        Clamp();
    }

    /// <summary>Hàng trên cùng và dưới cùng đang lọt trong khung nhìn.</summary>
    public void VisibleRows(out int top, out int bottom)
    {
        float half = Camera.orthographicSize;
        top = WorldMapLayout.RowAt(transform.position.y + half);
        bottom = WorldMapLayout.RowAt(transform.position.y - half);
    }

    private void Update()
    {
        // Đang gõ trong ô tìm kiếm thì không di chuyển bằng phím
        bool typing = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null;

        HandleMouse();
        if (!typing) HandleKeys();
        HandleFly();
        Clamp();
    }

    private void HandleMouse()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 screen = mouse.position.ReadValue();
        bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (mouse.leftButton.wasPressedThisFrame && !overUi)
        {
            pressing = true;
            dragging = false;
            pressScreen = screen;
            pressWorld = Camera.ScreenToWorldPoint(screen);
        }

        if (pressing && mouse.leftButton.isPressed)
        {
            if (!dragging && (screen - pressScreen).magnitude > DragThreshold) dragging = true;
            if (dragging)
            {
                // Giữ đúng điểm biển dưới con trỏ: camera dịch ngược với chuột
                flyTarget = null;
                Vector3 now = Camera.ScreenToWorldPoint(screen);
                transform.position += pressWorld - now;
            }
        }

        if (pressing && mouse.leftButton.wasReleasedThisFrame)
        {
            pressing = false;
            if (!dragging) Clicked?.Invoke(screen);
            dragging = false;
        }

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f && !overUi)
        {
            // Zoom giữ điểm dưới con trỏ đứng yên
            Vector3 before = Camera.ScreenToWorldPoint(screen);
            float size = scroll > 0 ? Camera.orthographicSize / zoomStep : Camera.orthographicSize * zoomStep;
            Camera.orthographicSize = Mathf.Clamp(size, minSize, maxSize);
            Vector3 after = Camera.ScreenToWorldPoint(screen);
            transform.position += before - after;
        }
    }

    private void HandleKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        Vector2 move = Vector2.zero;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
        if (move == Vector2.zero) return;

        flyTarget = null;
        float speed = keyPanSpeed * Camera.orthographicSize / maxSize;
        transform.position += (Vector3)(move.normalized * speed * Time.unscaledDeltaTime);
    }

    private void HandleFly()
    {
        if (flyTarget == null) return;

        Vector3 target = flyTarget.Value;
        Vector3 p = transform.position;
        p.x = Mathf.SmoothDamp(p.x, target.x, ref flyVelocityX, flySeconds, Mathf.Infinity, Time.unscaledDeltaTime);
        p.y = Mathf.SmoothDamp(p.y, target.y, ref flyVelocityY, flySeconds, Mathf.Infinity, Time.unscaledDeltaTime);
        transform.position = p;

        if (Vector2.Distance(p, target) < 0.02f) flyTarget = null;
    }

    // Tâm camera không ra ngoài khung; khung hẹp hơn màn hình thì đứng giữa
    private void Clamp()
    {
        if (Camera == null) return;

        float halfH = Camera.orthographicSize;
        float halfW = halfH * Camera.aspect;
        Vector3 p = transform.position;

        p.x = bounds.width <= halfW * 2f ? bounds.center.x : Mathf.Clamp(p.x, bounds.xMin + halfW, bounds.xMax - halfW);
        p.y = bounds.height <= halfH * 2f ? bounds.center.y : Mathf.Clamp(p.y, bounds.yMin + halfH, bounds.yMax - halfH);
        transform.position = p;
    }
}
