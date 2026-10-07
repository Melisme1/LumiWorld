using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nạp và lưu dữ liệu kinh tế của người chơi: ví Coins, kho tài nguyên và các đơn hàng đang mở.
/// CurrencyWallet và ResourceInventory tự gắn vào đây trong Awake. Mọi thay đổi được ghi
/// xuống cuối frame đó, khi game tạm dừng, khi đổi scene và khi thoát game.
///
/// Bây giờ dữ liệu nằm trong file JSON trên máy (LocalEconomySaveStore). Khi có server,
/// chỉ cần đổi store ở EnsureLoaded; ví, kho và Bảng Đơn Hàng giữ nguyên.
/// </summary>
public class EconomySaveSystem : MonoBehaviour
{
    private static EconomySaveSystem _instance;
    public static EconomySaveSystem Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<EconomySaveSystem>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("EconomySaveSystem");
                    _instance = go.AddComponent<EconomySaveSystem>();
                }
            }
            return _instance;
        }
    }

    private IEconomySaveStore store;
    private EconomySaveData data;
    private bool isDirty;
    private float nextSaveTime;

    /// <summary>
    /// Các đơn hàng đang mở đã lưu. Bảng Đơn Hàng sửa trực tiếp danh sách này rồi gọi MarkDirty().
    /// </summary>
    public List<ActiveOrder> OpenOrders
    {
        get
        {
            EnsureLoaded();
            return data.openOrders;
        }
    }

    /// <summary>
    /// Giờ có đơn mới của các ô vừa bị bỏ đơn. Bảng Đơn Hàng sửa trực tiếp danh sách này rồi gọi MarkDirty().
    /// </summary>
    public List<OrderSlotRefill> OrderRefills
    {
        get
        {
            EnsureLoaded();
            return data.orderRefills;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }

        _instance = this;
        EnsureLoaded();
    }

    /// <summary>
    /// Nạp số dư đã lưu vào ví. Người chơi mới nhận Starter Kit đúng một lần cho mỗi bản lưu.
    /// </summary>
    public void Attach(CurrencyWallet wallet)
    {
        EnsureLoaded();

        wallet.RestoreBalance(data.coins);
        wallet.OnBalanceChanged += HandleBalanceChanged;

        if (!data.starterKitGranted)
        {
            data.starterKitGranted = true;
            wallet.Add(wallet.StarterCoins, CoinReason.StarterKit);
            MarkDirty();
        }
    }

    /// <summary>
    /// Nạp số lượng tài nguyên đã lưu vào kho.
    /// </summary>
    public void Attach(ResourceInventory inventory)
    {
        EnsureLoaded();

        inventory.RestoreAmounts(data.resources);
        inventory.OnInventoryChanged += HandleInventoryChanged;
    }

    public void MarkDirty()
    {
        isDirty = true;
    }

    /// <summary>
    /// Id tài khoản đang chơi, dùng để chọn file lưu. Hiện game chưa có hệ thống đăng nhập
    /// nên luôn là "guest" (một bản lưu chung trên máy). Khi thêm đăng nhập, chỉ cần trả về
    /// id của tài khoản đang đăng nhập ở đây, phần còn lại của hệ thống lưu không phải sửa.
    /// </summary>
    private static string PlayerId => "guest";

    private void EnsureLoaded()
    {
        if (data != null) return;

        store = new LocalEconomySaveStore(PlayerId);
        data = store.Load() ?? new EconomySaveData();
        if (data.resources == null) data.resources = new List<ResourceStack>();
        if (data.openOrders == null) data.openOrders = new List<ActiveOrder>();
        if (data.orderRefills == null) data.orderRefills = new List<OrderSlotRefill>();
    }

    // Dữ liệu được cập nhật ngay khi ví/kho đổi (không đợi lúc lưu),
    // nên vẫn lưu đúng kể cả khi ví/kho bị hủy trước lúc đổi scene.
    private void HandleBalanceChanged(int balance, int delta, CoinReason reason)
    {
        data.coins = balance;
        MarkDirty();
    }

    private void HandleInventoryChanged(string resourceId, int amount, int delta)
    {
        ResourceStack stack = data.resources.Find(s => s.resourceId == resourceId);
        if (stack == null)
        {
            stack = new ResourceStack { resourceId = resourceId };
            data.resources.Add(stack);
        }

        stack.amount = amount;
        MarkDirty();
    }

    private void LateUpdate()
    {
        // Lần lưu trước bị lỗi thì đợi một lúc mới thử lại, không ghi file mỗi frame
        if (Time.unscaledTime >= nextSaveTime) SaveIfDirty();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveIfDirty();
    }

    private void OnApplicationQuit()
    {
        SaveIfDirty();
    }

    private void OnDestroy()
    {
        SaveIfDirty();
        if (_instance == this) _instance = null;
    }

    private void SaveIfDirty()
    {
        if (!isDirty || data == null) return;

        data.savedAtUtc = DateTime.UtcNow.ToString("o");
        if (store.Save(data))
        {
            isDirty = false;
        }
        else
        {
            nextSaveTime = Time.unscaledTime + 2f;
        }
    }
}
