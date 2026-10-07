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
}
