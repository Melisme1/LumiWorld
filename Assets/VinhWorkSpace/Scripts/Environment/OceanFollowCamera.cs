using UnityEngine;

/// <summary>
/// Cho phép mặt phẳng biển (Ocean Plane) luôn trượt theo vị trí Camera trên trục XZ.
/// Nhờ tính toán theo World Position trong Shader, mặt nước và sóng vẫn đứng yên trong thế giới,
/// nhưng người chơi đi đến đâu thì biển luôn bao bọc tới đó, vô tận và không tốn thêm hiệu năng.
/// </summary>
[DefaultExecutionOrder(100)]
public class OceanFollowCamera : MonoBehaviour
{
    [Tooltip("Camera chính để theo dõi (nếu để trống sẽ tự tìm Camera.main)")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Độ cao cố định của mặt biển")]
    [SerializeField] private float waterLevelY = -0.3f;

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
        // Đi theo camera hoàn toàn mượt mà, không giật nhảy theo từng bước
        transform.position = new Vector3(camPos.x, waterLevelY, camPos.z);
    }
}
