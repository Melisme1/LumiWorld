using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Giữ save trên server khớp với máy (giai đoạn 2, bước 8). Server giữ một save mỗi tài khoản.
/// - Trước khi vào MapBuilding (PullBeforePlay): nếu server có bản khác với bản máy đã đồng bộ lần cuối
///   thì tải về ghi đè file trên máy. Server thắng.
/// - Khi save đổi (QueueUpload): gửi lên sau khi yên tĩnh 10 giây. Lỗi mạng thì thử lại sau 30 giây.
///   Nếu thoát game trước khi gửi, cờ "chưa gửi" còn trong PlayerPrefs và được gửi ở lần mở sau.
/// - Gặp 409 SAVE_CONFLICT (máy khác đã lưu sau): tải bản server về rồi tải lại scene.
/// Tài khoản guest không đồng bộ. Tự tạo khi game mở, không cần đặt trong scene.
/// </summary>
public class SaveSync : MonoBehaviour
{
    private const float DebounceSeconds = 10f;
    private const float RetrySeconds = 30f;
    private const string SavesPath = "/api/v1/saves/me";

    private static SaveSync instance;

    private bool pending;
    private bool uploading;
    private bool resetting;
    private float lastChangeTime;
    private float nextRetryTime;

    /// <summary>
    /// true từ lúc ghi bản server xuống máy (sau 409) cho tới khi scene tải lại xong. Trong lúc này
    /// WorldSaveSystem và EconomySaveSystem không ghi trạng thái cũ còn trong bộ nhớ đè lên file vừa tải về.
    /// </summary>
    public static bool SavesSuspended { get; private set; }

    [Serializable]
    private class SaveHeader
    {
        public long revision;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;

        var go = new GameObject("SaveSync");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SaveSync>();
        SceneManager.sceneLoaded += (scene, mode) => SavesSuspended = false;
    }

    /// <summary>Đánh dấu save đã đổi. Gọi được từ bất kỳ đâu; chỉ có tác dụng khi đăng nhập bằng tài khoản.</summary>
    public static void QueueUpload()
    {
        if (instance == null || !SaveSlot.IsAccount || SavesSuspended) return;

        instance.pending = true;
        instance.lastChangeTime = Time.unscaledTime;

        // Ghi nhớ còn bản chưa gửi, phòng khi thoát game trước khi kịp gửi
        string key = PendingKey(SaveSlot.PlayerId);
        if (PlayerPrefs.GetInt(key, 0) == 0)
        {
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Gọi trước khi chuyển sang MapBuilding. Gọi done khi xong, kể cả khi server không trả lời
    /// (khi đó chơi bằng bản trên máy).
    /// </summary>
    public static IEnumerator PullBeforePlay(Action done)
    {
        if (!SaveSlot.IsAccount)
        {
            done?.Invoke();
            yield break;
        }

        string id = SaveSlot.PlayerId;
        string body = null;
        ApiError error = null;

        yield return ApiClient.GetText(SavesPath, true, text => body = text, err => error = err);

        if (body != null)
        {
            // Server ghi đè thì các thay đổi chưa gửi trên máy bị bỏ, đúng quy tắc server thắng
            if (ApplySafely(id, body, false)) ClearPending(id);
        }
        else if (error != null && error.StatusCode == 404)
        {
            Debug.Log("[LumiWorld Save] Server chưa có save của tài khoản này. Sẽ dùng bản trên máy, hoặc sinh đảo mới nếu máy cũng chưa có.");
        }
        else
        {
            Debug.LogWarning($"[LumiWorld Save] Chưa tải được save từ server ({error}). Chơi bằng bản trên máy.");
        }

        // Còn bản chưa gửi từ lần chơi trước thì gửi lại trong game
        if (instance != null && PlayerPrefs.GetInt(PendingKey(id), 0) == 1)
        {
            instance.pending = true;
            instance.lastChangeTime = Time.unscaledTime;
        }

        done?.Invoke();
    }

    /// <summary>
    /// Replace the account's single save with a fresh world/economy pair. An empty world is
    /// intentional: HexWorldGenerator creates the first island when MapBuilding opens.
    /// Do not enter the game if the server cannot accept the replacement, or the next pull
    /// would silently bring the previous world back.
    /// </summary>
    public static IEnumerator StartNewGame(Action<bool, string> done)
    {
        if (instance == null || !SaveSlot.IsAccount)
        {
            done?.Invoke(false, "Cần đăng nhập trước khi tạo world mới.");
            yield break;
        }

        if (instance.resetting)
        {
            done?.Invoke(false, "New Game đang được xử lý.");
            yield break;
        }
        instance.resetting = true;
        // A previous save must finish before we read the revision to replace.
        while (instance.uploading) yield return null;
        string id = SaveSlot.PlayerId;
        string current = null;
        ApiError readError = null;
        yield return ApiClient.GetText(SavesPath, true, text => current = text, error => readError = error);
        if (current == null && (readError == null || readError.StatusCode != 404))
        {
            instance.resetting = false;
            done?.Invoke(false, "Không kiểm tra được save trên server. World cũ vẫn được giữ nguyên.");
            yield break;
        }

        long revision = 0;
        if (current != null)
        {
            try { revision = JsonUtility.FromJson<SaveHeader>(current).revision; }
            catch (Exception exception)
            {
                Debug.LogError("[LumiWorld Save] Không đọc được revision trước New Game: " + exception.Message);
                instance.resetting = false;
                done?.Invoke(false, "Không đọc được save trên server. World cũ vẫn được giữ nguyên.");
                yield break;
            }
        }

        string world = JsonUtility.ToJson(new WorldSaveData { playerId = id, initializeNewWorld = true });
        string economy = JsonUtility.ToJson(new EconomySaveData());
        string request = "{\"baseRevision\":" + revision.ToString(CultureInfo.InvariantCulture) +
            ",\"world\":" + world + ",\"economy\":" + economy + "}";
        string ack = null;
        ApiError writeError = null;
        yield return ApiClient.PutRaw(SavesPath, request, true, text => ack = text, error => writeError = error);
        if (ack == null)
        {
            Debug.LogWarning("[LumiWorld Save] New Game bị server từ chối: " + writeError);
            instance.resetting = false;
            done?.Invoke(false, "Không tạo được world mới trên server. World cũ vẫn được giữ nguyên.");
            yield break;
        }

        long newRevision;
        try { newRevision = JsonUtility.FromJson<SaveHeader>(ack).revision; }
        catch (Exception exception)
        {
            Debug.LogError("[LumiWorld Save] New Game không đọc được xác nhận: " + exception.Message);
            instance.resetting = false;
            done?.Invoke(false, "Server đã nhận yêu cầu nhưng chưa xác nhận revision. Hãy mở lại game để đồng bộ.");
            yield break;
        }

        SavesSuspended = true;
        instance.pending = false;
        instance.nextRetryTime = 0f;
        try
        {
            // Retain a recoverable copy before replacing either local file.
            string suffix = ".pre-newgame-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            foreach (string prefix in new[] { "world_", "economy_" })
            {
                string path = FilePath(prefix, id);
                if (File.Exists(path)) File.Copy(path, path + suffix, true);
            }
            WriteAtomic(FilePath("world_", id), world);
            WriteAtomic(FilePath("economy_", id), economy);
            SetLocalRevision(id, newRevision);
            ClearPending(id);
        }
        catch (Exception exception)
        {
            Debug.LogError("[LumiWorld Save] Server đã reset nhưng không ghi được save mới trên máy: " + exception);
            SavesSuspended = false;
            instance.resetting = false;
            done?.Invoke(false, "Server đã tạo world mới nhưng máy chưa ghi được. Hãy mở lại game để đồng bộ.");
            yield break;
        }

        // These services survive scene changes and otherwise retain the old economy in memory.
        DestroyRuntimeStateForNewGame();
        instance.resetting = false;
        done?.Invoke(true, null);
    }

    private static void DestroyRuntimeStateForNewGame()
    {
        if (OrderBoardSystem.HasInstance) Destroy(OrderBoardSystem.Instance.gameObject);
        if (ResourceInventory.HasInstance) Destroy(ResourceInventory.Instance.gameObject);
        if (CurrencyWallet.HasInstance) Destroy(CurrencyWallet.Instance.gameObject);
        if (EconomySaveSystem.HasInstance) Destroy(EconomySaveSystem.Instance.gameObject);
        if (AchievementManager.HasInstance) Destroy(AchievementManager.Instance.gameObject);
        if (CardLockManager.HasInstance) Destroy(CardLockManager.Instance.gameObject);
        if (CreatureCareManager.HasInstance) Destroy(CreatureCareManager.Instance.gameObject);
    }

    private void Update()
    {
        if (!pending || uploading || resetting) return;
        if (Time.unscaledTime - lastChangeTime < DebounceSeconds) return;
        if (Time.unscaledTime < nextRetryTime) return;
        if (!AuthSession.IsLoggedIn) return;

        StartCoroutine(Upload());
    }

    private IEnumerator Upload()
    {
        string id = SaveSlot.PlayerId;
        string worldPath = FilePath("world_", id);
        string economyPath = FilePath("economy_", id);
        if (!File.Exists(worldPath) || !File.Exists(economyPath))
        {
            pending = false;
            yield break;
        }

        string world;
        string economy;
        try
        {
            world = File.ReadAllText(worldPath);
            economy = File.ReadAllText(economyPath);
        }
        catch (IOException ex)
        {
            // File đang bị khóa (đang ghi, phần mềm diệt virus...): thử lại sau, không dừng hẳn việc gửi
            nextRetryTime = Time.unscaledTime + RetrySeconds;
            Debug.LogWarning($"[LumiWorld Save] Chưa đọc được file save ({ex.Message}). Sẽ thử lại sau {RetrySeconds} giây.");
            yield break;
        }

        uploading = true;
        pending = false;
        long baseRevision = Math.Max(LocalRevision(id), 0);

        // Gửi nguyên văn hai file. Thay đổi trong lúc gửi sẽ làm pending = true và được gửi ở lượt sau
        string body = "{\"baseRevision\":" + baseRevision + ",\"world\":" + world + ",\"economy\":" + economy + "}";

        string ack = null;
        ApiError error = null;
        yield return ApiClient.PutRaw(SavesPath, body, true, text => ack = text, err => error = err);

        uploading = false;

        if (ack != null)
        {
            SaveHeader result = JsonUtility.FromJson<SaveHeader>(ack);
            SetLocalRevision(id, result.revision);
            if (!pending) ClearPending(id);
            Debug.Log($"[LumiWorld Save] Đã lưu lên server, revision {result.revision}.");
            yield break;
        }

        if (error != null && error.Code == "SAVE_CONFLICT")
        {
            Debug.LogWarning("[LumiWorld Save] Máy khác đã lưu sau. Dùng bản trên server và tải lại scene.");
            yield return ReloadFromServer(id);
            yield break;
        }

        pending = true;
        nextRetryTime = Time.unscaledTime + RetrySeconds;
        Debug.LogWarning($"[LumiWorld Save] Chưa gửi được save lên server ({error}). Sẽ thử lại sau {RetrySeconds} giây.");
    }

    private IEnumerator ReloadFromServer(string id)
    {
        string body = null;
        ApiError error = null;
        yield return ApiClient.GetText(SavesPath, true, text => body = text, err => error = err);

        if (body == null && error != null && error.StatusCode == 404)
        {
            // Server không còn save (database bị xoá hoặc reset): gửi lại bản máy như lần lưu đầu tiên
            Debug.LogWarning("[LumiWorld Save] Server không còn save của tài khoản này. Gửi lại bản trên máy.");
            PlayerPrefs.DeleteKey(RevisionKey(id));
            PlayerPrefs.Save();
            pending = true;
            nextRetryTime = Time.unscaledTime + RetrySeconds;
            yield break;
        }

        if (body == null)
        {
            // Giữ bản máy và thử gửi lại sau, không mất thay đổi
            Debug.LogWarning($"[LumiWorld Save] Không tải được bản server ({error}). Giữ nguyên bản trên máy.");
            pending = true;
            nextRetryTime = Time.unscaledTime + RetrySeconds;
            yield break;
        }

        // Bản server thắng nên thay đổi chưa gửi trên máy bị bỏ.
        // Chặn ghi trước khi ghi file: scene chỉ tải lại ở frame sau, LateUpdate và OnDestroy frame này không được ghi đè.
        SavesSuspended = true;
        if (!ApplySafely(id, body, true))
        {
            SavesSuspended = false;
            pending = true;
            nextRetryTime = Time.unscaledTime + RetrySeconds;
            yield break;
        }

        ClearPending(id);
        pending = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Ghi file từ server; lỗi định dạng chỉ ghi log để game vẫn chạy bằng bản trên máy.
    // Trả về true nếu đã ghi file.
    private static bool ApplySafely(string id, string body, bool force)
    {
        try
        {
            return ApplyServer(id, body, force);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LumiWorld Save] Save từ server không đọc được: {ex.Message}. Giữ nguyên bản trên máy.");
            return false;
        }
    }

    /// <summary>Ghi world và economy từ server nếu cần. Trả về false nếu bản máy đã khớp với server.</summary>
    private static bool ApplyServer(string id, string body, bool force)
    {
        SaveHeader header = JsonUtility.FromJson<SaveHeader>(body);
        bool hasLocal = File.Exists(FilePath("world_", id));
        if (!force && hasLocal && LocalRevision(id) == header.revision) return false;

        string world = JsonSlice.ObjectField(body, "world");
        string economy = JsonSlice.ObjectField(body, "economy");
        if (world == null || economy == null)
            throw new InvalidDataException("thiếu world hoặc economy");

        WriteAtomic(FilePath("world_", id), world);
        WriteAtomic(FilePath("economy_", id), economy);
        SetLocalRevision(id, header.revision);

        Debug.Log($"[LumiWorld Save] Đã tải save revision {header.revision} từ server về máy.");
        return true;
    }

    private static void WriteAtomic(string path, string text)
    {
        // Ghi file tạm rồi mới thay file chính, như các store lưu khác
        string temp = path + ".tmp";
        File.WriteAllText(temp, text);

        if (File.Exists(path))
            File.Replace(temp, path, path + ".bak");
        else
            File.Move(temp, path);
    }

    private static string FilePath(string prefix, string playerId)
    {
        string owner = string.IsNullOrEmpty(playerId) ? SaveSlot.GuestId : playerId;
        foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
        {
            owner = owner.Replace(invalid, '_');
        }

        return System.IO.Path.Combine(Application.persistentDataPath, prefix + owner + ".json");
    }

    // Revision của bản đã đồng bộ lần cuối trên máy này, -1 nếu chưa đồng bộ lần nào
    private static long LocalRevision(string id)
    {
        string text = PlayerPrefs.GetString(RevisionKey(id), "");
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long revision) ? revision : -1;
    }

    private static void SetLocalRevision(string id, long revision)
    {
        PlayerPrefs.SetString(RevisionKey(id), revision.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    private static void ClearPending(string id)
    {
        if (PlayerPrefs.GetInt(PendingKey(id), 0) == 0) return;

        PlayerPrefs.DeleteKey(PendingKey(id));
        PlayerPrefs.Save();
    }

    private static string RevisionKey(string id) => "SaveRevision_" + id;
    private static string PendingKey(string id) => "SavePending_" + id;
}
