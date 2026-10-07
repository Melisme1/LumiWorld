using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Nơi đọc/ghi EconomySaveData. Bây giờ là file JSON trên máy; khi có server sẽ thêm
/// một store gọi API mà không phải sửa EconomySaveSystem hay các lớp dùng nó.
/// </summary>
public interface IEconomySaveStore
{
    /// <summary>
    /// Trả về dữ liệu đã lưu, hoặc null nếu người chơi chưa có bản lưu.
    /// </summary>
    EconomySaveData Load();

    void Save(EconomySaveData data);
}

/// <summary>
/// Lưu EconomySaveData thành file JSON trong Application.persistentDataPath, mỗi tài khoản một file:
/// economy_{playerId}.json, chưa đăng nhập thì economy_guest.json.
/// Xóa file này để chơi lại từ đầu (nhận lại Starter Kit).
/// </summary>
public class LocalEconomySaveStore : IEconomySaveStore
{
    private readonly string filePath;

    public LocalEconomySaveStore(string playerId)
    {
        string owner = string.IsNullOrEmpty(playerId) ? "guest" : playerId;
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            owner = owner.Replace(invalid, '_');
        }

        filePath = Path.Combine(Application.persistentDataPath, "economy_" + owner + ".json");
    }

    public EconomySaveData Load()
    {
        Debug.Log($"[LumiWorld Save] Dữ liệu Coins và kho: {filePath}");

        if (!File.Exists(filePath)) return null;

        try
        {
            return JsonUtility.FromJson<EconomySaveData>(File.ReadAllText(filePath));
        }
        catch (Exception ex)
        {
            // File hỏng: chép ra một bản để còn kiểm tra, rồi bắt đầu bản lưu mới thay vì làm hỏng game.
            string brokenCopy = filePath + ".broken";
            try
            {
                File.Copy(filePath, brokenCopy, true);
            }
            catch (Exception)
            {
                brokenCopy = "(không chép được)";
            }

            Debug.LogWarning($"[LumiWorld Save] Không đọc được {filePath}: {ex.Message}. Bản hỏng: {brokenCopy}. Bắt đầu bản lưu mới.");
            return null;
        }
    }

    public void Save(EconomySaveData data)
    {
        try
        {
            // Ghi ra file tạm rồi mới thay file chính, để tắt game giữa chừng không làm hỏng bản lưu.
            string tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));

            if (File.Exists(filePath))
            {
                File.Replace(tempPath, filePath, null);
            }
            else
            {
                File.Move(tempPath, filePath);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LumiWorld Save] Không lưu được {filePath}: {ex.Message}");
        }
    }
}
