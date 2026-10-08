using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kho tài nguyên của người chơi, đếm theo resourceID (wood, herb, flower...).
/// Mỗi loại chứa tối đa maxStackSize của ResourceData. Phần vượt sức chứa bị từ chối
/// để nơi gọi tự giữ lại (ví dụ bong bóng thu hoạch giữ phần dư).
/// Số lượng được EconomySaveSystem nạp khi vào game và tự lưu sau mỗi thay đổi.
/// </summary>
public class ResourceInventory : MonoBehaviour
{
    private static ResourceInventory _instance;
    public static ResourceInventory Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<ResourceInventory>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("ResourceInventory");
                    _instance = go.AddComponent<ResourceInventory>();
                }
            }
            return _instance;
        }
    }

    private readonly Dictionary<string, int> amounts = new Dictionary<string, int>();

    /// <summary>
    /// Bắn ra sau mỗi lần kho đổi: (resourceID, số lượng mới, chênh lệch).
    /// </summary>
    public event Action<string, int, int> OnInventoryChanged;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }

        _instance = this;
        EconomySaveSystem.Instance.Attach(this);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    public int GetAmount(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId)) return 0;
        return amounts.TryGetValue(resourceId, out int amount) ? amount : 0;
    }

    public int GetAmount(ResourceData resource)
    {
        return resource != null ? GetAmount(resource.resourceID) : 0;
    }

    /// <summary>
    /// Số lượng còn cất thêm được trước khi chạm sức chứa maxStackSize.
    /// </summary>
    public int GetFreeSpace(ResourceData resource)
    {
        if (resource == null) return 0;
        return Mathf.Max(0, resource.maxStackSize - GetAmount(resource));
    }

    /// <summary>
    /// Cất tối đa amount tài nguyên vào kho. Trả về số đã cất được,
    /// có thể ít hơn amount (hoặc bằng 0) khi kho gần đầy.
    /// </summary>
    public int Add(ResourceData resource, int amount)
    {
        if (resource == null || amount <= 0) return 0;

        if (string.IsNullOrEmpty(resource.resourceID))
        {
            Debug.LogError($"[LumiWorld Kho] ResourceData '{resource.name}' chưa có resourceID nên không cất vào kho được.", resource);
            return 0;
        }

        int stored = Mathf.Min(amount, GetFreeSpace(resource));
        if (stored <= 0) return 0;

        int newAmount = GetAmount(resource) + stored;
        amounts[resource.resourceID] = newAmount;
        OnInventoryChanged?.Invoke(resource.resourceID, newAmount, stored);
        return stored;
    }

    internal readonly struct Deposit
    {
        public ResourceData Resource { get; }
        public int Amount { get; }
        public Deposit(ResourceData resource, int amount) { Resource = resource; Amount = amount; }
    }

    /// <summary>
    /// Collection batch: validate capacity, update all stock and commit source buffers,
    /// then notify save/HUD. commitSource must only mutate the already validated sources.
    /// </summary>
    internal bool TryAddAll(IReadOnlyList<Deposit> deposits, Action commitSource)
    {
        if (deposits == null || deposits.Count == 0 || commitSource == null) return false;
        var deltas = new Dictionary<string, int>(StringComparer.Ordinal);
        var limits = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var deposit in deposits)
        {
            var resource = deposit.Resource;
            if (resource == null || string.IsNullOrWhiteSpace(resource.resourceID) || deposit.Amount <= 0) return false;
            string id = resource.resourceID;
            deltas.TryGetValue(id, out int delta);
            long combined = (long)delta + deposit.Amount;
            if (combined > int.MaxValue) return false;
            deltas[id] = (int)combined;
            limits[id] = limits.TryGetValue(id, out int limit) ? Math.Min(limit, resource.maxStackSize) : resource.maxStackSize;
        }
        var previous = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in deltas)
        {
            int old = GetAmount(pair.Key);
            if (old < 0 || (long)old + pair.Value > limits[pair.Key]) return false;
            previous[pair.Key] = old;
        }
        foreach (var pair in deltas) amounts[pair.Key] = previous[pair.Key] + pair.Value;
        try { commitSource(); }
        catch
        {
            foreach (var pair in previous) amounts[pair.Key] = pair.Value;
            throw;
        }
        // Isolate listeners: a presentation exception must not skip save or other resource notifications.
        foreach (var pair in deltas)
        {
            var listeners = OnInventoryChanged;
            if (listeners == null) continue;
            foreach (Action<string, int, int> listener in listeners.GetInvocationList())
                try { listener(pair.Key, previous[pair.Key] + pair.Value, pair.Value); }
                catch (Exception exception) { Debug.LogException(exception, this); }
        }
        return true;
    }

    /// <summary>
    /// Kho có đủ mọi dòng yêu cầu không (ví dụ yêu cầu của một đơn hàng). Các dòng trùng loại được cộng dồn.
    /// </summary>
    public bool HasAll(IList<ResourceStack> requirements)
    {
        foreach (KeyValuePair<string, int> need in SumByResource(requirements))
        {
            if (GetAmount(need.Key) < need.Value) return false;
        }
        return true;
    }

    /// <summary>
    /// Trừ đủ mọi dòng yêu cầu, hoặc không trừ gì nếu thiếu dù chỉ một loại. Trả về true khi đã trừ.
    /// </summary>
    public bool TryRemoveAll(IList<ResourceStack> requirements)
    {
        Dictionary<string, int> needs = SumByResource(requirements);
        foreach (KeyValuePair<string, int> need in needs)
        {
            if (GetAmount(need.Key) < need.Value) return false;
        }

        foreach (KeyValuePair<string, int> need in needs)
        {
            int newAmount = GetAmount(need.Key) - need.Value;
            amounts[need.Key] = newAmount;
            OnInventoryChanged?.Invoke(need.Key, newAmount, -need.Value);
        }
        return true;
    }

    private static Dictionary<string, int> SumByResource(IList<ResourceStack> stacks)
    {
        Dictionary<string, int> totals = new Dictionary<string, int>();
        if (stacks == null) return totals;

        foreach (ResourceStack stack in stacks)
        {
            if (stack == null || string.IsNullOrEmpty(stack.resourceId) || stack.amount <= 0) continue;

            totals.TryGetValue(stack.resourceId, out int total);
            totals[stack.resourceId] = total + stack.amount;
        }
        return totals;
    }

    /// <summary>
    /// Nạp lại kho đã lưu. Chỉ EconomySaveSystem gọi lúc vào game, không bắn sự kiện.
    /// </summary>
    internal void RestoreAmounts(List<ResourceStack> stacks)
    {
        amounts.Clear();
        if (stacks == null) return;

        foreach (ResourceStack stack in stacks)
        {
            if (stack == null || string.IsNullOrEmpty(stack.resourceId) || stack.amount <= 0) continue;
            amounts[stack.resourceId] = stack.amount;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Test: +10 mỗi loại tài nguyên")]
    private void DevAddEach()
    {
        foreach (ResourceData resource in ResourceCatalog.All)
        {
            Add(resource, 10);
        }
    }

    [ContextMenu("Test: trừ 5 mỗi loại tài nguyên")]
    private void DevRemoveEach()
    {
        List<ResourceStack> cost = new List<ResourceStack>();
        foreach (ResourceData resource in ResourceCatalog.All)
        {
            cost.Add(new ResourceStack { resourceId = resource.resourceID, amount = 5 });
        }

        if (!TryRemoveAll(cost))
        {
            Debug.LogWarning("[LumiWorld Kho] Có loại chưa đủ 5 nên không trừ gì.");
        }
    }
#endif
}
