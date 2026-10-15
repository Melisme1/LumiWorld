using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lấy hộp thư từ server và áp vào bản lưu. Tự tạo khi game mở, không cần đặt trong scene.
/// Đồng bộ khi vào scene nào cũng được, khi đăng nhập xong, và mỗi syncInboxSeconds giây
/// (lấy từ server, mặc định 60) khi đã đăng nhập.
/// Mỗi mục chỉ được áp một lần: EconomySaveSystem ghi id vào bản lưu trước, rồi mới ack.
/// </summary>
public class InboxSync : MonoBehaviour
{
    private const int DefaultIntervalSeconds = 60;

    private static InboxSync _instance;

    private int intervalSeconds = DefaultIntervalSeconds;
    private bool configLoaded;
    private bool syncing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (_instance != null) return;

        var go = new GameObject("InboxSync");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<InboxSync>();
    }

    /// <summary>
    /// Đồng bộ ngay nếu đang đăng nhập. Gọi an toàn từ bất kỳ đâu; đang đồng bộ thì bỏ qua lần này.
    /// </summary>
    public static void RequestSync()
    {
        if (_instance != null) _instance.StartSync();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        StartSync();
        StartCoroutine(PeriodicSync());
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartSync();
    }

    private IEnumerator PeriodicSync()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(intervalSeconds);
            StartSync();
        }
    }

    private void StartSync()
    {
        if (syncing || !AuthSession.IsLoggedIn) return;

        StartCoroutine(SyncOnce());
    }

    private IEnumerator SyncOnce()
    {
        syncing = true;

        if (!configLoaded)
        {
            yield return ApiClient.Get<GameConfigDto>("/api/v1/config", false,
                config =>
                {
                    if (config != null && config.syncInboxSeconds > 0)
                        intervalSeconds = config.syncInboxSeconds;
                    configLoaded = true;
                },
                error => Debug.LogWarning($"[LumiWorld Inbox] Không đọc được config: {error}"));
        }

        InboxResponse inbox = null;
        ApiError fetchError = null;

        yield return ApiClient.Get<InboxResponse>("/api/v1/inbox", true,
            response => inbox = response,
            error => fetchError = error);

        if (fetchError != null)
        {
            // Mất mạng thì thôi, lần đồng bộ sau sẽ thử lại
            if (!fetchError.IsNetworkError)
                Debug.LogWarning($"[LumiWorld Inbox] Không tải được hộp thư: {fetchError}");

            syncing = false;
            yield break;
        }

        var ackIds = new List<string>();

        if (inbox != null && inbox.items != null)
        {
            foreach (InboxItemDto item in inbox.items)
            {
                if (string.IsNullOrEmpty(item.id)) continue;

                // Đã ghi vào bản lưu thì được ack, kể cả khi lần trước ack bị lỗi
                if (EconomySaveSystem.Instance.ApplyInboxItem(item.id, item.kind, item.payload))
                    ackIds.Add(item.id);
            }
        }

        if (ackIds.Count > 0)
        {
            var body = new InboxAckRequest { ids = ackIds };

            yield return ApiClient.Post<InboxAckRequest, InboxAckResponse>("/api/v1/inbox/ack", body, true,
                ack => Debug.Log($"[LumiWorld Inbox] Đã xác nhận {ackIds.Count} mục."),
                error =>
                {
                    // Mục vẫn còn trên server; lần sau nó đã có trong bản lưu nên chỉ ack lại
                    if (!error.IsNetworkError)
                        Debug.LogWarning($"[LumiWorld Inbox] Ack lỗi: {error}");
                });
        }

        syncing = false;
    }
}
