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

    /// <summary>
    /// Trả về false nếu chưa lưu được, để lần sau thử lại.
    /// </summary>
    bool Save(EconomySaveData data);
}

/// <summary>
/// Lưu EconomySaveData thành file JSON trong Application.persistentDataPath, mỗi tài khoản một file:
/// economy_{playerId}.json, chưa đăng nhập thì economy_guest.json. Lần lưu trước được giữ lại thành
/// economy_{playerId}.json.bak, file chính hỏng thì game tự dùng bản này.
/// Xóa các file này để chơi lại từ đầu (nhận lại Starter Kit).
/// </summary>
public class LocalEconomySaveStore : IEconomySaveStore
{
    private readonly string filePath;
    private readonly string backupPath;

    // File lưu có đó nhưng không mở được (máy khác đang giữ, thiếu quyền): lượt này không ghi đè lên nó
    private bool savingDisabled;

    public LocalEconomySaveStore(string playerId)
    {
        string owner = string.IsNullOrEmpty(playerId) ? "guest" : playerId;
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            owner = owner.Replace(invalid, '_');
        }

        filePath = Path.Combine(Application.persistentDataPath, "economy_" + owner + ".json");
        backupPath = filePath + ".bak";
    }

    public EconomySaveData Load()
    {
        Debug.Log($"[LumiWorld Save] Dữ liệu Coins và kho: {filePath}");

        if (!File.Exists(filePath)) return null;

        string json;
        try
        {
            json = File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            savingDisabled = true;
            Debug.LogError($"[LumiWorld Save] Không mở được {filePath}: {ex.Message}. Lượt này không lưu Coins và kho để khỏi ghi đè bản lưu cũ.");
            return null;
        }

        EconomySaveData data = Parse(json);
        if (data != null) return data;

        // File hỏng (ví dụ sửa tay bị sai cú pháp): đổi tên thành .broken để còn kiểm tra, rồi dùng bản lưu trước đó.
        // Đổi tên chứ không để nguyên, vì lần lưu tới sẽ biến file chính thành .bak và đè mất bản lưu tốt.
        string brokenCopy = filePath + ".broken";
        try
        {
            if (File.Exists(brokenCopy)) File.Delete(brokenCopy);
            File.Move(filePath, brokenCopy);
        }
        catch (Exception ex)
        {
            savingDisabled = true;
            Debug.LogError($"[LumiWorld Save] File {filePath} bị hỏng và không đổi tên được: {ex.Message}. Lượt này không lưu Coins và kho.");
            return ReadBackup();
        }

        data = ReadBackup();
        Debug.LogWarning($"[LumiWorld Save] File {filePath} bị hỏng, đã đổi tên thành {brokenCopy}. "
            + (data != null ? "Dùng bản lưu trước đó (.bak)." : "Không có bản lưu trước đó, bắt đầu bản lưu mới."));
        return data;
    }

    public bool Save(EconomySaveData data)
    {
        if (savingDisabled) return true;

        try
        {
            // Ghi ra file tạm rồi mới thay file chính, để tắt game giữa chừng không làm hỏng bản lưu.
            // File chính cũ thành bản .bak.
            string tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));

            if (File.Exists(filePath))
            {
                File.Replace(tempPath, filePath, backupPath);
            }
            else
            {
                File.Move(tempPath, filePath);
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LumiWorld Save] Không lưu được {filePath}: {ex.Message}. Sẽ thử lại.");
            return false;
        }
    }

    private EconomySaveData ReadBackup()
    {
        try
        {
            return File.Exists(backupPath) ? Parse(File.ReadAllText(backupPath)) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static EconomySaveData Parse(string json)
    {
        try
        {
            return JsonUtility.FromJson<EconomySaveData>(json);
        }
        catch (ArgumentException)
        {
            // JsonUtility báo JSON sai cú pháp bằng ArgumentException
            return null;
        }
    }
}
