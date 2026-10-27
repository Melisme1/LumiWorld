using UnityEngine;

/// <summary>
/// Cấu hình trực quan trực tiếp trên từng Prefab mô hình trang trí (Habitat Prop).
/// Cho phép điều chỉnh độ cắm sâu xuống đất (groundEmbed) độc lập cho từng Prefab ngay trong Inspector,
/// thay thế hoàn toàn việc so khớp chuỗi tên (string matching) trong mã nguồn.
/// </summary>
[DisallowMultipleComponent]
public class HexPropConfig : MonoBehaviour
{
    [Header("Ground Placement")]
    [Tooltip("Độ cắm sâu thêm xuống đất (mét) tính từ đáy mô hình.\n" +
             "• 0.00: Đáy chạm vừa khít mặt cỏ (không chìm)\n" +
             "• 0.01 - 0.03: Cắm nhẹ 1 - 3cm (hoa, bụi cỏ, cụm sỏi nhỏ để giấu chân đế)\n" +
             "• 0.04 - 0.08: Cắm 4 - 8cm (tảng đá, khúc gỗ, gốc cây)\n" +
             "• 0.10 - 0.20: Cắm sâu cho đại thụ rễ bạnh to")]
    [SerializeField] private float groundEmbed = 0.02f;

    [Tooltip("Giới hạn tỉ lệ chìm tối đa theo chiều cao mô hình (0.05 - 0.50 tương đương 5% - 50%). Tránh chìm mất hút.")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float maxEmbedHeightRatio = 0.25f;

    public float GroundEmbed
    {
        get => groundEmbed;
        set => groundEmbed = Mathf.Max(0f, value);
    }

    public float MaxEmbedHeightRatio
    {
        get => maxEmbedHeightRatio;
        set => maxEmbedHeightRatio = Mathf.Clamp(value, 0.05f, 0.5f);
    }

    [Header("Obstacle Navigation (AI Thú Né Tránh)")]
    [Tooltip("Chế độ vật cản cho AI động vật:\n" +
             "• AutoDetect: Tự động phân loại theo tên/kích thước prefab (gốc cây, thân cây, tảng đá, khúc gỗ)\n" +
             "• SolidObstacle: Luôn coi là vật cản cứng, thú tuyệt đối không đi xuyên qua\n" +
             "• Passable: Luôn coi là thực vật mềm, thú có thể dẫm lướt qua tự nhiên")]
    [SerializeField] private PropObstacleMode obstacleMode = PropObstacleMode.AutoDetect;

    [Tooltip("Bán kính cản va chạm tùy chỉnh (mét). Để 0 sẽ tự động tính từ kích thước thực tế của mô hình.")]
    [SerializeField] private float customObstacleRadius = 0f;

    public PropObstacleMode ObstacleMode
    {
        get => obstacleMode;
        set => obstacleMode = value;
    }

    public float CustomObstacleRadius
    {
        get => customObstacleRadius;
        set => customObstacleRadius = Mathf.Max(0f, value);
    }
}

public enum PropObstacleMode
{
    AutoDetect = 0,
    SolidObstacle = 1,
    Passable = 2
}
