using UnityEngine;

/// <summary>
/// Quản lý hiệu ứng cộng điểm "+X" (Preserve style).
///
/// CÁCH DÙNG:
/// 1. Tạo 1 GameObject trống trong Scene, gán script này vào.
/// 2. Gán 3 sprite (+1, +2, +10) từ Assets/Project/Art/poitn.png vào các ô tương ứng.
/// 3. Hệ thống sẽ tự động hiển thị hiệu ứng khi đặt bài xuống khối.
///
/// Script này cũng tự chuyển sprite cho ScorePopup qua các static field,
/// nên không cần gán thủ công ở ScorePopup.
/// </summary>
public class ScorePopupManager : MonoBehaviour
{
    public static ScorePopupManager Instance;

    [Header("Sprites (từ poitn.png)")]
    [Tooltip("Sprite '+1' cho điểm đặt bài thường")]
    [SerializeField] private Sprite spritePlus1;

    [Tooltip("Sprite '+2' cho điểm đặt bài ghép cặp")]
    [SerializeField] private Sprite spritePlus2;

    [Tooltip("Sprite '+10' cho điểm hoàn thành nhóm (3+ bài)")]
    [SerializeField] private Sprite spritePlus10;

    [Header("Spawn Settings")]
    [Tooltip("Độ cao so với đỉnh khối nơi hiệu ứng xuất hiện")]
    [SerializeField] private float spawnHeightOffset = 0.35f;

    [Tooltip("Tỉ lệ kích thước hiệu ứng")]
    [SerializeField] private float popupScale = 0.6f;

    private void Awake()
    {
        Instance = this;
        ApplySprites();

        if (spritePlus1 == null && spritePlus2 == null && spritePlus10 == null)
        {
            Debug.LogWarning(
                "ScorePopupManager: Chưa gán sprite nào! Hiệu ứng +X sẽ không hiển thị. " +
                "Hãy kéo các sprite (+1, +2, +10) từ Assets/Project/Art/poitn.png vào Inspector.",
                this
            );
        }
    }

    private void OnValidate()
    {
        ApplySprites();
    }

    /// <summary>
    /// Đồng bộ sprite vào ScorePopup để dùng ở mọi nơi.
    /// </summary>
    private void ApplySprites()
    {
        ScorePopup.SpritePlus1 = spritePlus1;
        ScorePopup.SpritePlus2 = spritePlus2;
        ScorePopup.SpritePlus10 = spritePlus10;
    }

    /// <summary>
    /// Hiển thị hiệu ứng "+score" tại vị trí world position của khối.
    /// Nếu không tìm thấy ScorePopupManager trong scene, vẫn gọi ScorePopup trực tiếp
    /// (ScorePopup sẽ tự tìm sprite dự phòng).
    /// </summary>
    public static void ShowScore(int score, Vector3 worldPosition)
    {
        if (score <= 0) return;

        float scale = Instance != null ? Instance.popupScale : 0.6f;
        float offset = Instance != null ? Instance.spawnHeightOffset : 0.35f;

        if (Instance == null)
        {
            Debug.LogWarning(
                "ScorePopupManager: Không tìm thấy ScorePopupManager trong Scene. " +
                "Hãy tạo 1 GameObject và gắn script ScorePopupManager để hiệu ứng +X hoạt động."
            );
            return;
        }

        // Đồng bộ lại sprite (an toàn khi Awake thứ tự khác nhau)
        Instance.ApplySprites();

        ScorePopup.Spawn(score, worldPosition + Vector3.up * offset, scale);
    }
}
