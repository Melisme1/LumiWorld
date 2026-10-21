using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Các lý do khiến thẻ bài, sinh vật hoặc tài nguyên bị khóa theo quy định GDD
/// </summary>
public enum CardLockType
{
    None = 0,
    MarketEscrow = 1,  // Đang niêm yết trên Marketplace (Escrow) - không thể đem ra dùng, triển khai hay giao dịch khác
    Breeding = 2,      // Đang lai tạo (Sinh vật bố mẹ bị khóa, không thể sản xuất hay trade)
    AccountBound = 3   // Khóa theo tài khoản (Emergency supplies, starter kit, quà story/achievement - không thể giao dịch trên Marketplace)
}

[Serializable]
public class LockedCardEntry
{
    public string cardID;
    public CardLockType lockType;
    public string reason;
}

[Serializable]
public class LockedResourceEntry
{
    public string resourceID;
    public int lockedAmount;
    public CardLockType lockType;
    public string reason;
}

/// <summary>
/// Quản lý trạng thái Khóa (Lock / Escrow) của thẻ bài, sinh vật và tài nguyên trong kho:
/// - Phân biệt rõ Inventory (thẻ & sinh vật) và Warehouse (kho tài nguyên thu hoạch).
/// - Khóa tài sản khi niêm yết Marketplace (Escrow).
/// - Khóa sinh vật bố mẹ khi Breeding.
/// - Khóa thẻ/vật phẩm theo tài khoản (Account-bound / Trade-locked).
/// </summary>
public class CardLockManager : MonoBehaviour
{
    private static CardLockManager instance;
    private static bool isApplicationQuitting = false;

    public static bool HasInstance => instance != null && !isApplicationQuitting;

    public static CardLockManager Instance
    {
        get
        {
            if (isApplicationQuitting) return null;

            if (instance == null)
            {
                instance = FindAnyObjectByType<CardLockManager>();
                if (instance == null)
                {
                    if (!Application.isPlaying || isApplicationQuitting)
                        return null;

                    GameObject go = new GameObject("CardLockManager");
                    instance = go.AddComponent<CardLockManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    [Header("Runtime Lock Entries")]
    [SerializeField] private List<LockedCardEntry> lockedCards = new List<LockedCardEntry>();
    [SerializeField] private List<LockedResourceEntry> lockedResources = new List<LockedResourceEntry>();

    private readonly Dictionary<string, LockedCardEntry> cardLockMap = new Dictionary<string, LockedCardEntry>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LockedResourceEntry> resourceLockMap = new Dictionary<string, LockedResourceEntry>(StringComparer.OrdinalIgnoreCase);

    public event Action OnLockStateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
        isApplicationQuitting = false;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        isApplicationQuitting = false;
        DontDestroyOnLoad(gameObject);
        RebuildMaps();
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void RebuildMaps()
    {
        cardLockMap.Clear();
        foreach (var c in lockedCards)
        {
            if (c != null && !string.IsNullOrEmpty(c.cardID))
            {
                cardLockMap[c.cardID] = c;
            }
        }

        resourceLockMap.Clear();
        foreach (var r in lockedResources)
        {
            if (r != null && !string.IsNullOrEmpty(r.resourceID))
            {
                resourceLockMap[r.resourceID] = r;
            }
        }
    }

    // =========================================================================
    // 1. QUẢN LÝ KHÓA THẺ BÀI / CÁ THỂ SINH VẬT (INVENTORY)
    // =========================================================================

    public bool IsCardLocked(CardData card, out CardLockType lockType, out string reason)
    {
        lockType = CardLockType.None;
        reason = string.Empty;

        if (card == null) return false;

        // 1. Kiểm tra trong danh sách khóa động
        if (cardLockMap.TryGetValue(card.cardID, out var entry) || cardLockMap.TryGetValue(card.name, out entry))
        {
            lockType = entry.lockType;
            reason = entry.reason;
            return true;
        }

        return false;
    }

    public void LockCard(string cardId, CardLockType lockType, string customReason = "")
    {
        if (string.IsNullOrEmpty(cardId)) return;

        string defaultReason = lockType switch
        {
            CardLockType.MarketEscrow => "Listed on Marketplace (Escrow). Cannot deploy or use.",
            CardLockType.Breeding => "Currently breeding. Cannot produce or trade.",
            CardLockType.AccountBound => "Account-bound. Cannot trade on Marketplace.",
            _ => "Card is locked."
        };

        var entry = new LockedCardEntry
        {
            cardID = cardId,
            lockType = lockType,
            reason = !string.IsNullOrEmpty(customReason) ? customReason : defaultReason
        };

        cardLockMap[cardId] = entry;

        lockedCards.RemoveAll(c => c.cardID.Equals(cardId, StringComparison.OrdinalIgnoreCase));
        lockedCards.Add(entry);

        OnLockStateChanged?.Invoke();
    }

    public void UnlockCard(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return;

        cardLockMap.Remove(cardId);
        lockedCards.RemoveAll(c => c.cardID.Equals(cardId, StringComparison.OrdinalIgnoreCase));

        OnLockStateChanged?.Invoke();
    }

    // =========================================================================
    // 2. QUẢN LÝ KHÓA TÀI NGUYÊN (WAREHOUSE)
    // =========================================================================

    public int GetLockedResourceAmount(string resourceId, out CardLockType lockType, out string reason)
    {
        lockType = CardLockType.None;
        reason = string.Empty;

        if (string.IsNullOrEmpty(resourceId)) return 0;

        if (resourceLockMap.TryGetValue(resourceId, out var entry))
        {
            lockType = entry.lockType;
            reason = entry.reason;
            return entry.lockedAmount;
        }

        return 0;
    }

    public void LockResourceAmount(string resourceId, int amount, CardLockType lockType, string customReason = "")
    {
        if (string.IsNullOrEmpty(resourceId) || amount <= 0) return;

        if (!resourceLockMap.TryGetValue(resourceId, out var entry))
        {
            entry = new LockedResourceEntry
            {
                resourceID = resourceId,
                lockedAmount = 0,
                lockType = lockType,
                reason = !string.IsNullOrEmpty(customReason) ? customReason : "Listed on Marketplace (Escrow)"
            };
            resourceLockMap[resourceId] = entry;
            lockedResources.Add(entry);
        }

        entry.lockedAmount += amount;
        entry.lockType = lockType;
        OnLockStateChanged?.Invoke();
    }

    public void UnlockResourceAmount(string resourceId, int amount)
    {
        if (string.IsNullOrEmpty(resourceId) || amount <= 0) return;

        if (resourceLockMap.TryGetValue(resourceId, out var entry))
        {
            entry.lockedAmount = Mathf.Max(0, entry.lockedAmount - amount);
            if (entry.lockedAmount == 0)
            {
                resourceLockMap.Remove(resourceId);
                lockedResources.Remove(entry);
            }
            OnLockStateChanged?.Invoke();
        }
    }
}
