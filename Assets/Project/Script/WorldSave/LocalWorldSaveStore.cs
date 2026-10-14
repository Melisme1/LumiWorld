using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Lưu WorldSaveData thành file JSON trong Application.persistentDataPath, mỗi tài khoản một file:
/// world_{playerId}.json. Lần lưu trước giữ thành world_{playerId}.json.bak; file chính hỏng thì dùng bản này.
/// Chép theo LocalEconomySaveStore để hai file cùng cách xử lý lỗi.
/// </summary>
public class LocalWorldSaveStore : IWorldSaveStore
{
    private readonly string filePath;
    private readonly string backupPath;

    // File lưu có đó nhưng không mở được: lượt này không ghi đè lên nó
    private bool savingDisabled;

    public LocalWorldSaveStore(string playerId)
    {
        string owner = string.IsNullOrEmpty(playerId) ? SaveSlot.GuestId : playerId;
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            owner = owner.Replace(invalid, '_');
        }

        filePath = Path.Combine(Application.persistentDataPath, "world_" + owner + ".json");
        backupPath = filePath + ".bak";
    }

    public WorldSaveData Load()
    {
        if (!File.Exists(filePath)) return null;

        string json;
        try
        {
            json = File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            savingDisabled = true;
            Debug.LogError($"[LumiWorld World] Không mở được {filePath}: {ex.Message}. Lượt này không ghi world để khỏi đè bản lưu cũ.");
            return null;
        }

        WorldSaveData data = Parse(json);
        if (data != null) return data;

        // File hỏng: đổi tên thành .broken để còn kiểm tra, rồi dùng bản .bak.
        // Đổi tên chứ không để nguyên, vì lần lưu tới sẽ biến file chính thành .bak và đè mất bản tốt.
        string brokenCopy = filePath + ".broken";
        try
        {
            if (File.Exists(brokenCopy)) File.Delete(brokenCopy);
            File.Move(filePath, brokenCopy);
        }
        catch (Exception ex)
        {
            savingDisabled = true;
            Debug.LogError($"[LumiWorld World] File {filePath} hỏng và không đổi tên được: {ex.Message}.");
            return ReadBackup();
        }

        data = ReadBackup();
        Debug.LogWarning($"[LumiWorld World] File {filePath} hỏng, đã đổi tên thành {brokenCopy}. "
            + (data != null ? "Dùng bản .bak." : "Không có bản .bak, bắt đầu world mới."));
        return data;
    }

    public bool Save(WorldSaveData data)
    {
        if (savingDisabled) return true;

        try
        {
            // Ghi file tạm rồi mới thay file chính, để tắt game giữa chừng không làm hỏng bản lưu.
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
            Debug.LogWarning($"[LumiWorld World] Không lưu được {filePath}: {ex.Message}. Sẽ thử lại.");
            return false;
        }
    }

    private WorldSaveData ReadBackup()
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

    private static WorldSaveData Parse(string json)
    {
        try
        {
            return JsonUtility.FromJson<WorldSaveData>(json);
        }
        catch (ArgumentException)
        {
            // JsonUtility báo JSON sai cú pháp bằng ArgumentException
            return null;
        }
    }
}
