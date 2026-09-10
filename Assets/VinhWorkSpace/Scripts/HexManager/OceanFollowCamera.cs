using UnityEngine;

/// <summary>
/// Cho phép mặt phẳng biển (Ocean Plane) luôn trượt theo vị trí Camera trên trục XZ.
/// Nhờ tính toán theo World Position trong Shader, mặt nước và sóng vẫn đứng yên trong thế giới,
/// nhưng người chơi đi đến đâu thì biển luôn bao bọc tới đó, vô tận và không tốn thêm hiệu năng.
/// </summary>
public class OceanFollowCamera : MonoBehaviour
{
    [Tooltip("Camera chính để theo dõi (nếu để trống sẽ tự tìm Camera.main)")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Độ cao cố định của mặt biển")]
    [SerializeField] private float waterLevelY = -0.3f;

    [Tooltip("Khoảng cách bước nhảy tọa độ để tránh hiện tượng răng cưa vertex khi camera di chuyển")]
    [SerializeField] private float snapStep = 1.0f;

    private void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null) return;

        Vector3 camPos = targetCamera.transform.position;

        // Snap vị trí theo từng bước nhỏ (snapStep) để các đỉnh mesh không bị rung khi camera pan chậm
        float snappedX = Mathf.Floor(camPos.x / snapStep) * snapStep;
        float snappedZ = Mathf.Floor(camPos.z / snapStep) * snapStep;

        transform.position = new Vector3(snappedX, waterLevelY, snappedZ);
    }
}
