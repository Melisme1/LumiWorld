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
    private static bool _isQuitting = false;

    public static bool HasInstance => _instance != null && !_isQuitting;

    public static EconomySaveSystem Instance
    {
        get
        {
            if (_isQuitting) return null;

            if (_instance == null)
            {
                _instance = FindAnyObjectByType<EconomySaveSystem>();
                if (_instance == null)
                {
                    if (!Application.isPlaying || _isQuitting)
                        return null;

                    GameObject go = new GameObject("EconomySaveSystem");
                    _instance = go.AddComponent<EconomySaveSystem>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    private IEconomySaveStore store;
    private EconomySaveData data;
    private bool isDirty;
    private float nextSaveTime;

    // Adapters project pause-aware runtime timers to portable UTC deadlines before serialization.
    public event Action PreparingSave;

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

    /// <summary>
    /// Tiến trình của tài khoản: Harmony, cột mốc và thành tựu đã nhận (AchievementManager),
    /// bài trên tay (CardDeskController). Các hệ thống đó sửa trực tiếp trường của mình rồi gọi
    /// MarkDirty() (hoặc SaveNow() khi vừa trao thưởng).
    /// </summary>
    public EconomySaveData Progress
    {
        get
        {
            EnsureLoaded();
            return data;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _instance = null;
        _isQuitting = false;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }

        _instance = this;
        _isQuitting = false;
        DontDestroyOnLoad(gameObject);
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
    /// Đã áp mục hộp thư này vào bản lưu chưa. Dùng để bỏ qua mục đã nhận nhưng chưa được server xóa.
    /// </summary>
    public bool HasAppliedInboxItem(string itemId)
    {
        EnsureLoaded();
        return data.appliedInboxIds.Contains(itemId);
    }

    /// <summary>
    /// Áp một mục hộp thư rồi ghi xuống file ngay. Trả về true khi mục đã nằm trong bản lưu
    /// (vừa áp hoặc áp từ trước), lúc đó mới được ack cho server. Trả về false khi không ghi được
    /// hoặc loại mục chưa hỗ trợ; mục đó vẫn ở trên server để lần sau thử lại.
    /// </summary>
    public bool ApplyInboxItem(string itemId, string kind, string payload)
    {
        EnsureLoaded();
        if (data.appliedInboxIds.Contains(itemId)) return true;

        // Phần đã cộng vào ví hoặc kho. Nếu không ghi được file thì hoàn tác để lần sau áp lại đúng một lần
        Action undo = null;

        switch (kind)
        {
            case "System":
                // Mục thử nghiệm: không đổi Coins hay kho
                Debug.Log($"[LumiWorld Inbox] Nhận mục System {itemId}: {payload}");
                break;

            case "CoinsFromSale":
            {
                // Người bán nhận Coins sau khi trừ phí chợ
                var sale = JsonUtility.FromJson<CoinsFromSalePayload>(payload);
                if (sale == null || sale.coins < 0)
                {
                    Debug.LogWarning($"[LumiWorld Inbox] Mục CoinsFromSale {itemId} sai dữ liệu. Giữ lại trên server.");
                    return false;
                }

                CurrencyWallet wallet = CurrencyWallet.Instance;
                wallet.Add(sale.coins, CoinReason.MarketSale);
                undo = () => wallet.TrySpend(sale.coins, CoinReason.MarketSale);
                break;
            }

            case "ItemsBought":
            case "ItemsReturned":
            {
                // Hàng vừa mua hoặc hàng trả lại: cất đủ vào kho, không cất một phần
                var goods = JsonUtility.FromJson<ItemsPayload>(payload);

                // Thẻ thú mua được hoặc lô thẻ bị hủy/hết hạn: về tay bài
                if (goods != null && CardTradeIds.IsCardId(goods.resourceId))
                {
                    CardData card = CardTradeIds.Find(goods.resourceId);
                    if (card == null || goods.amount <= 0)
                    {
                        Debug.LogWarning($"[LumiWorld Inbox] Mục {kind} {itemId} có thẻ không tra được ({goods.resourceId}). Giữ lại trên server.");
                        return false;
                    }

                    // Đang thăm đảo người khác hoặc tay bài chưa nạp: giữ mục, về đảo mình mới nhận
                    CardDeskController desk = CardDeskController.Instance;
                    if (desk == null || !desk.HandReady) return false;

                    // Thẻ và id mục nằm trong cùng một lần ghi file bên dưới, nên không bao giờ cộng hai lần
                    desk.AddCardsWithoutSaving(card, goods.amount);
                    string cardId = card.cardID;
                    int cardCount = goods.amount;
                    undo = () => desk.TakeCardsWithoutSaving(cardId, cardCount);
                    break;
                }

                ResourceData resource = goods != null ? ResourceCatalog.Find(goods.resourceId) : null;
                if (resource == null || goods.amount <= 0)
                {
                    Debug.LogWarning($"[LumiWorld Inbox] Mục {kind} {itemId} sai dữ liệu. Giữ lại trên server.");
                    return false;
                }

                ResourceInventory inventory = ResourceInventory.Instance;
                if (inventory.GetFreeSpace(resource) < goods.amount)
                {
                    // Kho chưa đủ chỗ: để nguyên trong hộp thư, lần sau thử lại
                    Debug.Log($"[LumiWorld Inbox] Kho chưa đủ chỗ cho {goods.amount} {resource.resourceName}. Giữ mục {itemId}.");
                    return false;
                }

                inventory.AddFromAccount(resource, goods.amount);
                var stack = new ResourceStack { resourceId = resource.resourceID, amount = goods.amount };
                undo = () => inventory.TryRemoveFromAccount(new List<ResourceStack> { stack });
                break;
            }

            default:
                // Loại mới hơn bản client này: giữ lại trên server cho tới khi cập nhật
                Debug.LogWarning($"[LumiWorld Inbox] Loại mục chưa hỗ trợ: {kind}. Giữ lại trên server.");
                return false;
        }

        data.appliedInboxIds.Add(itemId);
        MarkDirty();

        if (SaveNow()) return true;

        // Không ghi được thì bỏ id và hoàn tác, để lần sau áp lại đúng một lần
        data.appliedInboxIds.Remove(itemId);
        undo?.Invoke();
        return false;
    }

    /// <summary>
    /// Các thao tác chợ đang chờ server (bản sao, sửa không ảnh hưởng bản lưu).
    /// </summary>
    public List<PendingMarketOp> PendingMarketOps
    {
        get
        {
            EnsureLoaded();
            return new List<PendingMarketOp>(data.pendingMarketOps);
        }
    }

    /// <summary>
    /// Ghi một thao tác chợ đang chờ xuống file ngay. Trả về false nếu ghi lỗi;
    /// lúc đó bên gọi phải hoàn tác hàng hoặc Coins đã trừ.
    /// </summary>
    public bool AddPendingMarketOp(PendingMarketOp op)
    {
        EnsureLoaded();
        data.pendingMarketOps.Add(op);
        MarkDirty();

        if (SaveNow()) return true;

        data.pendingMarketOps.Remove(op);
        return false;
    }

    /// <summary>
    /// Bỏ một thao tác chợ đã được server xử lý xong (thành công, hoặc bị từ chối và đã hoàn tác).
    /// </summary>
    public void RemovePendingMarketOp(string opKey)
    {
        EnsureLoaded();
        if (data.pendingMarketOps.RemoveAll(op => op.opKey == opKey) == 0) return;

        MarkDirty();
        SaveNow();
    }

    /// <summary>
    /// Ghi xuống file ngay, không đợi cuối frame. Trả về false nếu ghi lỗi.
    /// </summary>
    public bool SaveNow()
    {
        SaveIfDirty();
        return !isDirty;
    }

    private void EnsureLoaded()
    {
        if (data != null) return;

        store = new LocalEconomySaveStore(SaveSlot.PlayerId);
        data = store.Load();

        // Lần đầu đăng nhập mà tài khoản chưa có bản lưu thì lấy bản guest trên máy (nếu có),
        // để không mất tiến trình đã chơi trước khi có đăng nhập.
        if (data == null && SaveSlot.IsAccount)
        {
            data = new LocalEconomySaveStore(SaveSlot.GuestId).Load();
            if (data != null) isDirty = true;
        }

        data = data ?? new EconomySaveData();
        if (data.resources == null) data.resources = new List<ResourceStack>();
        if (data.openOrders == null) data.openOrders = new List<ActiveOrder>();
        if (data.orderRefills == null) data.orderRefills = new List<OrderSlotRefill>();
        if (data.appliedInboxIds == null) data.appliedInboxIds = new List<string>();
        if (data.pendingMarketOps == null) data.pendingMarketOps = new List<PendingMarketOp>();
        if (data.claimedMilestoneIds == null) data.claimedMilestoneIds = new List<string>();
        if (data.claimedAchievementIds == null) data.claimedAchievementIds = new List<string>();
        if (data.hand == null) data.hand = new List<HandCardStack>();
        if (data.craftingJobs == null) data.craftingJobs = new List<CraftingJobRecord>();
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
        if (paused) SaveIfDirty(true);
    }

    private void OnApplicationQuit()
    {
        _isQuitting = true;
        SaveIfDirty(true);
    }

    private void OnDestroy()
    {
        SaveIfDirty(true);
        if (_instance == this) _instance = null;
    }

    private void SaveIfDirty(bool force = false)
    {
        // Không ghi đè dữ liệu cũ trong lúc SaveSync đang áp dụng bản server.
        if (((!isDirty && !force) || data == null) || SaveSync.SavesSuspended) return;

        try { PreparingSave?.Invoke(); }
        catch (Exception exception)
        {
            isDirty = true;
            nextSaveTime = Time.unscaledTime + 2f;
            Debug.LogException(exception, this);
            return;
        }

        data.savedAtUtc = DateTime.UtcNow.ToString("o");
        if (store.Save(data))
        {
            isDirty = false;
            SaveSync.QueueUpload();
        }
        else
        {
            isDirty = true;
            nextSaveTime = Time.unscaledTime + 2f;
        }
    }
}
