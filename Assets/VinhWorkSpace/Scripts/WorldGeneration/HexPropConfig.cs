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
}
