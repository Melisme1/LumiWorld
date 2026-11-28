using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Số Gem của tài khoản. Gem chỉ có được qua nạp tiền và do server giữ: client không lưu, không cộng trừ,
/// chỉ đọc lại GET /api/v1/gems/me (lúc vào game, khi quay lại cửa sổ game sau khi thanh toán ở trình duyệt,
/// và khi hộp thư báo GemsPurchased). Tự tạo khi game mở, không cần đặt trong scene.
/// </summary>
public class GemWallet : MonoBehaviour
{
    private static GemWallet _instance;

    /// <summary>Số Gem đọc từ server lần gần nhất; -1 khi chưa đọc được (chưa đăng nhập, mất mạng).</summary>
    public static long Balance { get; private set; } = -1;

    /// <summary>Số dư mới và số Gem vừa tăng (0 khi chỉ là đọc lại).</summary>
    public static event Action<long, long> Changed;

    private bool refreshing;

    public static GemWallet Instance
    {
        get
        {
            if (_instance == null) Create();
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        Balance = -1;
        Changed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (_instance != null) return;
        var go = new GameObject("GemWallet");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<GemWallet>();
    }

    private void Start() => Refresh();

    // Người chơi thanh toán ở trình duyệt rồi quay lại game: đọc lại số Gem
    private void OnApplicationFocus(bool focused)
    {
        if (focused) Refresh();
    }

    /// <summary>Đọc lại số Gem từ server (bỏ qua nếu chưa đăng nhập hoặc đang đọc).</summary>
    public static void Refresh()
    {
        GemWallet wallet = Instance;
        if (wallet.refreshing || !AuthSession.IsLoggedIn) return;
        wallet.StartCoroutine(wallet.RefreshRoutine());
    }

    private IEnumerator RefreshRoutine()
    {
        refreshing = true;
        string account = AuthSession.PlayerId;
        GemMeResponse me = null;
        yield return ApiClient.Get<GemMeResponse>("/api/v1/gems/me", true, r => me = r, _ => { });
        refreshing = false;

        // Đổi tài khoản giữa chừng thì bỏ kết quả của tài khoản cũ
        if (me == null || AuthSession.PlayerId != account) yield break;
        // Lần đọc đầu (Balance = -1) chỉ hiện số, không tính là "vừa nhận"
        long gained = Balance >= 0 ? Math.Max(0, me.balance - Balance) : 0;
        Set(me.balance, gained);
    }

    /// <summary>Gọi từ EconomySaveSystem khi áp mục hộp thư GemsPurchased.</summary>
    public static void NotifyPurchased(GemsPurchasedPayload payload)
    {
        if (payload == null) return;
        // Số dư trong mục hộp thư là lúc cộng; đọc lại để chắc đúng số hiện tại
        if (payload.balance > Balance) Set(payload.balance, payload.gems);
        Refresh();
    }

    private static void Set(long balance, long gained)
    {
        Balance = balance;
        Changed?.Invoke(balance, gained);
    }
}
