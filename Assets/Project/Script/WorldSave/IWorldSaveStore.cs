/// <summary>
/// Nơi đọc và ghi WorldSaveData. Hiện là file JSON trên máy; khi có server, bước 8 thêm một store
/// gọi API mà không phải sửa WorldSaveSystem.
/// </summary>
public interface IWorldSaveStore
{
    /// <summary>Trả về dữ liệu đã lưu, hoặc null nếu chưa có bản lưu.</summary>
    WorldSaveData Load();

    /// <summary>Trả về false nếu chưa ghi được, để lần sau thử lại.</summary>
    bool Save(WorldSaveData data);
}
