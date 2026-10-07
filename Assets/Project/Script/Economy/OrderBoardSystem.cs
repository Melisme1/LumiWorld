using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// Bảng Đơn Hàng: các ô đơn của khách NPC. Người chơi giao tài nguyên trong kho để nhận Coins.
/// - Ô đầu tiên luôn là đơn cơ bản (mẫu có isBaseline) và không bỏ được, nên lúc nào cũng có đơn để làm.
/// - Đơn chỉ yêu cầu loại tài nguyên người chơi làm ra được: có cụm biome đang có thú, hoặc đang có trong kho.
/// - Giao đơn: trừ kho và cộng Coins cùng lúc (đủ hết mới trừ), rồi ô đó có ngay đơn mới.
/// - Bỏ đơn: miễn phí, ô trống có đơn mới sau refillSeconds (mặc định 5 phút, tính cả lúc tắt game).
/// Đơn đang mở và giờ có đơn mới được lưu cùng ví và kho trong EconomySaveData.
/// Mẫu đơn là các asset OrderTemplate trong thư mục Resources (Assets/Data/Resources/Orders).
/// </summary>
public class OrderBoardSystem : MonoBehaviour
{
    private static OrderBoardSystem _instance;
    public static OrderBoardSystem Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<OrderBoardSystem>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("OrderBoardSystem");
                    _instance = go.AddComponent<OrderBoardSystem>();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Ô của đơn cơ bản: luôn lấy mẫu có isBaseline và không bỏ được.
    /// </summary>
    public const int BaselineSlot = 0;

    [Tooltip("Số ô đơn trên bảng (game_config orderSlots)")]
    [Min(1)]
    [SerializeField] private int slotCount = 3;

    [Tooltip("Ô vừa bị bỏ đơn có đơn mới sau bao nhiêu giây (game_config orderRefillSeconds)")]
    [Min(0f)]
    [SerializeField] private float refillSeconds = 300f;

    private readonly List<OrderTemplate> templates = new List<OrderTemplate>();
    private readonly Dictionary<string, OrderTemplate> templatesById = new Dictionary<string, OrderTemplate>();
    private readonly HashSet<int> slotsWaitingForResources = new HashSet<int>();
    private List<ActiveOrder> orders;
    private List<OrderSlotRefill> refills;
    private float checkTimer;

    public int SlotCount => slotCount;

    /// <summary>
    /// Bắn ra mỗi khi bảng đổi: có đơn mới, vừa giao đơn hoặc vừa bỏ đơn.
    /// </summary>
    public event Action OnBoardChanged;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }

        _instance = this;
        LoadTemplates();

        // Sửa thẳng danh sách trong bản lưu, giống cách ví và kho cập nhật EconomySaveData
        orders = EconomySaveSystem.Instance.OpenOrders;
        refills = EconomySaveSystem.Instance.OrderRefills;
        DropInvalidSavedOrders();
    }

    private void Start()
    {
        FillEmptySlots();
    }

    private void Update()
    {
        // Mỗi giây xem lại các ô trống: hết giờ chờ, hoặc người chơi vừa làm ra loại tài nguyên mới
        checkTimer -= Time.unscaledDeltaTime;
        if (checkTimer > 0f) return;

        checkTimer = 1f;
        FillEmptySlots();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // =========================================================
    // ĐỌC TRẠNG THÁI BẢNG
    // =========================================================

    /// <summary>
    /// Đơn đang nằm ở ô slot, hoặc null khi ô trống.
    /// </summary>
    public ActiveOrder GetOrder(int slot)
    {
        foreach (ActiveOrder order in orders)
        {
            if (order.slot == slot) return order;
        }
        return null;
    }

    /// <summary>
    /// Mẫu đơn theo templateId (để lấy tên và ảnh khách), hoặc null nếu mẫu đã bị xóa.
    /// </summary>
    public OrderTemplate FindTemplate(string templateId)
    {
        if (string.IsNullOrEmpty(templateId)) return null;
        return templatesById.TryGetValue(templateId, out OrderTemplate template) ? template : null;
    }

    /// <summary>
    /// Kho có đủ hàng cho đơn ở ô slot chưa.
    /// </summary>
    public bool CanFulfill(int slot)
    {
        ActiveOrder order = GetOrder(slot);
        return order != null && ResourceInventory.Instance.HasAll(order.requirements);
    }

    /// <summary>
    /// Số đơn đang đủ hàng để giao ngay.
    /// </summary>
    public int CountFulfillable()
    {
        int count = 0;
        foreach (ActiveOrder order in orders)
        {
            if (ResourceInventory.Instance.HasAll(order.requirements)) count++;
        }
        return count;
    }

    /// <summary>
    /// Thời gian còn lại tới khi ô trống (vừa bị bỏ đơn) có đơn mới, TimeSpan.Zero nếu không phải chờ.
    /// </summary>
    public TimeSpan GetTimeUntilRefill(int slot)
    {
        TimeSpan left = GetRefillTime(slot) - DateTime.UtcNow;
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    /// <summary>
    /// Ô trống đã hết giờ chờ nhưng chưa có mẫu đơn nào hợp với loại tài nguyên người chơi đang làm ra.
    /// </summary>
    public bool IsWaitingForResources(int slot)
    {
        return slotsWaitingForResources.Contains(slot);
    }

    // =========================================================
    // GIAO ĐƠN VÀ BỎ ĐƠN
    // =========================================================

    /// <summary>
    /// Giao đơn ở ô slot: trừ đủ hàng trong kho và cộng thưởng Coins cùng lúc, rồi ô đó có ngay đơn mới.
    /// Thiếu dù chỉ một loại thì không trừ gì và trả về false.
    /// </summary>
    public bool TryFulfill(int slot)
    {
        ActiveOrder order = GetOrder(slot);
        if (order == null) return false;

        // Trừ kho và cộng Coins trong cùng một frame, nên chúng luôn được lưu cùng nhau
        if (!ResourceInventory.Instance.TryRemoveAll(order.requirements)) return false;
        CurrencyWallet.Instance.Add(order.rewardCoins, CoinReason.OrderReward);

        orders.Remove(order);
        Debug.Log($"<color=#38BDF8>📋 [LumiWorld Đơn hàng] Đã giao {Describe(order)} cho {CustomerName(order)}, nhận {order.rewardCoins} Coins.</color>");

        TryCreateOrder(slot, GetAvailableResourceIds());
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// Bỏ đơn ở ô slot (miễn phí). Ô trống có đơn mới sau refillSeconds. Đơn cơ bản không bỏ được.
    /// </summary>
    public bool Discard(int slot)
    {
        if (slot == BaselineSlot) return false;

        ActiveOrder order = GetOrder(slot);
        if (order == null) return false;

        orders.Remove(order);
        SetRefillTime(slot, DateTime.UtcNow.AddSeconds(refillSeconds));
        Debug.Log($"<color=#38BDF8>📋 [LumiWorld Đơn hàng] Đã bỏ đơn {Describe(order)} của {CustomerName(order)}. Ô {slot + 1} có đơn mới sau {refillSeconds:0} giây.</color>");

        NotifyChanged();
        return true;
    }

    // =========================================================
    // SINH ĐƠN MỚI
    // =========================================================

    private void FillEmptySlots()
    {
        if (orders == null) return;

        HashSet<string> available = null;
        bool changed = false;
        DateTime now = DateTime.UtcNow;

        for (int slot = 0; slot < slotCount; slot++)
        {
            if (GetOrder(slot) != null || GetRefillTime(slot) > now) continue;

            if (available == null) available = GetAvailableResourceIds();
            if (TryCreateOrder(slot, available)) changed = true;
        }

        if (changed) NotifyChanged();
    }

    private bool TryCreateOrder(int slot, HashSet<string> available)
    {
        OrderTemplate template = PickTemplate(slot, available);
        if (template == null)
        {
            slotsWaitingForResources.Add(slot);
            return false;
        }

        ActiveOrder order = new ActiveOrder
        {
            orderId = Guid.NewGuid().ToString("N"),
            templateId = template.templateId,
            slot = slot,
            createdAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
        };

        // Chốt số lượng trong khoảng của mẫu; thưởng = tổng (số lượng × baseValue) × hệ số
        int baseValueTotal = 0;
        foreach (OrderRequirement requirement in template.requirements)
        {
            if (requirement == null || requirement.resource == null) continue;

            int min = Mathf.Max(1, requirement.minAmount);
            int amount = UnityEngine.Random.Range(min, Mathf.Max(min, requirement.maxAmount) + 1);
            AddRequirement(order.requirements, requirement.resource.resourceID, amount);
            baseValueTotal += amount * Mathf.Max(0, requirement.resource.baseValue);
        }

        order.rewardCoins = Mathf.Max(1, Mathf.RoundToInt(baseValueTotal * template.rewardMultiplier));
        orders.Add(order);
        refills.RemoveAll(refill => refill.slot == slot);
        slotsWaitingForResources.Remove(slot);

        Debug.Log($"<color=#38BDF8>📋 [LumiWorld Đơn hàng] Ô {slot + 1}: {CustomerName(order)} cần {Describe(order)}, thưởng {order.rewardCoins} Coins.</color>");
        return true;
    }

    private OrderTemplate PickTemplate(int slot, HashSet<string> available)
    {
        List<OrderTemplate> candidates = new List<OrderTemplate>();
        List<OrderTemplate> notOnBoard = new List<OrderTemplate>();

        foreach (OrderTemplate template in templates)
        {
            if (slot == BaselineSlot && !template.isBaseline) continue;
            if (!UsesOnly(template, available)) continue;

            candidates.Add(template);
            if (!IsOnBoard(template.templateId)) notOnBoard.Add(template);
        }

        // Ưu tiên mẫu chưa có trên bảng để các đơn khác nhau; hết mẫu khác thì mới lặp lại
        List<OrderTemplate> pool = notOnBoard.Count > 0 ? notOnBoard : candidates;
        return pool.Count > 0 ? pool[UnityEngine.Random.Range(0, pool.Count)] : null;
    }

    private bool IsOnBoard(string templateId)
    {
        foreach (ActiveOrder order in orders)
        {
            if (order.templateId == templateId) return true;
        }
        return false;
    }

    private static bool UsesOnly(OrderTemplate template, HashSet<string> available)
    {
        foreach (OrderRequirement requirement in template.requirements)
        {
            if (requirement == null || requirement.resource == null) continue;
            if (!available.Contains(requirement.resource.resourceID)) return false;
        }
        return true;
    }

    /// <summary>
    /// Loại tài nguyên người chơi làm ra được: cụm biome đang có thú sản xuất, hoặc đang có trong kho.
    /// </summary>
    private static HashSet<string> GetAvailableResourceIds()
    {
        HashSet<string> ids = new HashSet<string>();

        // Tìm chứ không gọi BiomeHarvestManager.Instance, để không tự tạo manager ở scene không có bản đồ
        BiomeHarvestManager harvest = FindAnyObjectByType<BiomeHarvestManager>();
        if (harvest != null)
        {
            foreach (BiomeHarvestCluster cluster in harvest.Clusters)
            {
                if (cluster != null && cluster.ResourceData != null && cluster.Animals.Count > 0)
                {
                    ids.Add(cluster.ResourceData.resourceID);
                }
            }
        }

        ResourceInventory inventory = ResourceInventory.Instance;
        foreach (ResourceData resource in ResourceCatalog.All)
        {
            if (inventory.GetAmount(resource) > 0) ids.Add(resource.resourceID);
        }
        return ids;
    }

    private static void AddRequirement(List<ResourceStack> requirements, string resourceId, int amount)
    {
        // Mẫu có hai dòng cùng loại thì gộp lại thành một dòng
        foreach (ResourceStack stack in requirements)
        {
            if (stack.resourceId != resourceId) continue;
            stack.amount += amount;
            return;
        }

        requirements.Add(new ResourceStack { resourceId = resourceId, amount = amount });
    }

    // =========================================================
    // GIỜ CÓ ĐƠN MỚI CỦA Ô TRỐNG
    // =========================================================

    private DateTime GetRefillTime(int slot)
    {
        foreach (OrderSlotRefill refill in refills)
        {
            if (refill.slot != slot) continue;

            return DateTime.TryParse(refill.refillAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime time)
                ? time.ToUniversalTime()
                : DateTime.MinValue;
        }
        return DateTime.MinValue;
    }

    private void SetRefillTime(int slot, DateTime timeUtc)
    {
        refills.RemoveAll(refill => refill.slot == slot);
        refills.Add(new OrderSlotRefill { slot = slot, refillAtUtc = timeUtc.ToString("o", CultureInfo.InvariantCulture) });
    }

    // =========================================================
    // NẠP MẪU ĐƠN VÀ KIỂM TRA BẢN LƯU
    // =========================================================

    private void LoadTemplates()
    {
        templates.Clear();
        templatesById.Clear();

        foreach (OrderTemplate template in Resources.LoadAll<OrderTemplate>(string.Empty))
        {
            if (string.IsNullOrEmpty(template.templateId))
            {
                Debug.LogWarning($"[LumiWorld Đơn hàng] Mẫu đơn '{template.name}' chưa có templateId nên bị bỏ qua.", template);
                continue;
            }

            if (templatesById.ContainsKey(template.templateId))
            {
                Debug.LogWarning($"[LumiWorld Đơn hàng] Trùng templateId '{template.templateId}': bỏ qua '{template.name}'.", template);
                continue;
            }

            if (template.requirements == null || !template.requirements.Exists(requirement => requirement != null && requirement.resource != null))
            {
                Debug.LogWarning($"[LumiWorld Đơn hàng] Mẫu đơn '{template.name}' chưa có dòng yêu cầu nào có tài nguyên nên bị bỏ qua.", template);
                continue;
            }

            templates.Add(template);
            templatesById[template.templateId] = template;
        }

        templates.Sort((a, b) => string.CompareOrdinal(a.templateId, b.templateId));

        if (templates.Count == 0)
        {
            Debug.LogWarning("[LumiWorld Đơn hàng] Không tìm thấy OrderTemplate nào trong thư mục Resources (Assets/Data/Resources/Orders), bảng sẽ không có đơn.");
        }
        else if (!templates.Exists(template => template.isBaseline))
        {
            Debug.LogWarning("[LumiWorld Đơn hàng] Chưa có mẫu đơn cơ bản (isBaseline), ô đơn đầu tiên sẽ luôn trống.");
        }
    }

    private void DropInvalidSavedOrders()
    {
        // Bỏ đơn hỏng trong bản lưu: ô ngoài phạm vi, hai đơn chung một ô, hoặc cần loại tài nguyên không còn tồn tại
        HashSet<int> usedSlots = new HashSet<int>();
        int removed = orders.RemoveAll(order =>
            order == null
            || order.slot < 0
            || order.slot >= slotCount
            || order.requirements == null
            || order.requirements.Count == 0
            || order.requirements.Exists(stack => stack == null || ResourceCatalog.Find(stack.resourceId) == null)
            || !usedSlots.Add(order.slot));
        removed += refills.RemoveAll(refill => refill == null || refill.slot < 0 || refill.slot >= slotCount);

        if (removed > 0)
        {
            Debug.LogWarning($"[LumiWorld Đơn hàng] Bỏ {removed} mục hỏng trong bản lưu Bảng Đơn Hàng.");
            EconomySaveSystem.Instance.MarkDirty();
        }
    }

    private void NotifyChanged()
    {
        EconomySaveSystem.Instance.MarkDirty();
        OnBoardChanged?.Invoke();
    }

    private string CustomerName(ActiveOrder order)
    {
        OrderTemplate template = FindTemplate(order.templateId);
        return template != null ? template.customerName : order.templateId;
    }

    private static string Describe(ActiveOrder order)
    {
        StringBuilder text = new StringBuilder();
        foreach (ResourceStack stack in order.requirements)
        {
            if (text.Length > 0) text.Append(" + ");
            ResourceData resource = ResourceCatalog.Find(stack.resourceId);
            text.Append(stack.amount).Append(' ').Append(resource != null ? resource.resourceName : stack.resourceId);
        }
        return text.ToString();
    }

#if UNITY_EDITOR
    [ContextMenu("Test: có đơn mới ngay cho ô trống")]
    private void DevSkipRefillWait()
    {
        refills.Clear();
        EconomySaveSystem.Instance.MarkDirty();
        FillEmptySlots();
    }

    [ContextMenu("Test: làm mới cả bảng")]
    private void DevRerollBoard()
    {
        orders.Clear();
        refills.Clear();
        NotifyChanged();
        FillEmptySlots();
    }
#endif
}
