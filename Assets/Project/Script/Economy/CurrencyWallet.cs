using System;
using UnityEngine;

/// <summary>
/// Lý do Coins thay đổi. Mỗi lần cộng/trừ đều mang theo lý do để ghi log,
/// sau này server dùng để thống kê Coins vào/ra theo từng nguồn.
/// </summary>
public enum CoinReason
{
    StarterKit,
    OrderReward,
    NpcSale,
    Milestone,
    CardPurchase,
    PlacementFee,
    IslandUnlock,
    DevCheat
}

/// <summary>
/// Ví Lumi Coin của người chơi: nơi duy nhất được cộng/trừ Coins.
/// Coins là tiền ảo trong game, không nạp bằng tiền thật. Số dư không bao giờ âm.
/// Số dư được EconomySaveSystem nạp khi vào game và tự lưu sau mỗi thay đổi.
/// </summary>
public class CurrencyWallet : MonoBehaviour
{
    private static CurrencyWallet _instance;
    public static CurrencyWallet Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<CurrencyWallet>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("CurrencyWallet");
                    _instance = go.AddComponent<CurrencyWallet>();
                }
            }
            return _instance;
        }
    }

    [Tooltip("Số Coins người chơi mới nhận một lần duy nhất (Starter Kit)")]
    [Min(0)]
    [SerializeField] private int starterCoins = 200;

    [Tooltip("Số dư hiện tại, chỉ để xem khi đang chạy game. Muốn đổi số dư khi test hãy dùng menu ⋮ của component.")]
    [SerializeField] private int balance;

    public int Balance => balance;
    public int StarterCoins => starterCoins;

    /// <summary>
    /// Coins được tặng ngay lúc nạp ví (Starter Kit). Khoản này có trước khi CoinHUD kịp nghe OnBalanceChanged,
    /// nên CoinHUD đọc số này để vẫn hiện số bay.
    /// </summary>
    public int GrantedOnLoad { get; private set; }

    private int restoredBalance;

    /// <summary>
    /// Bắn ra sau mỗi lần số dư đổi: (số dư mới, chênh lệch, lý do).
    /// </summary>
    public event Action<int, int, CoinReason> OnBalanceChanged;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }

        _instance = this;
        EconomySaveSystem.Instance.Attach(this);
        GrantedOnLoad = balance - restoredBalance;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    public bool CanAfford(int amount)
    {
        return amount <= 0 || balance >= amount;
    }

    /// <summary>
    /// Trừ Coins nếu đủ. Không đủ thì không trừ gì và trả về false.
    /// </summary>
    public bool TrySpend(int amount, CoinReason reason)
    {
        if (amount <= 0) return true;
        if (balance < amount) return false;

        balance -= amount;
        NotifyChanged(-amount, reason);
        return true;
    }

    public void Add(int amount, CoinReason reason)
    {
        if (amount <= 0) return;

        balance += amount;
        NotifyChanged(amount, reason);
    }

    /// <summary>
    /// Nạp lại số dư đã lưu. Chỉ EconomySaveSystem gọi lúc vào game, không tính là giao dịch nên không bắn sự kiện.
    /// </summary>
    internal void RestoreBalance(int savedBalance)
    {
        balance = Mathf.Max(0, savedBalance);
        restoredBalance = balance;
    }

    private void NotifyChanged(int delta, CoinReason reason)
    {
        Debug.Log($"<color=#FACC15>🪙 [LumiWorld Coins] {delta:+#;-#;0} ({reason}) → số dư {balance}</color>");
        OnBalanceChanged?.Invoke(balance, delta, reason);
    }

#if UNITY_EDITOR
    [ContextMenu("Test: +100 Coins")]
    private void DevAddCoins()
    {
        Add(100, CoinReason.DevCheat);
    }

    [ContextMenu("Test: tiêu hết Coins")]
    private void DevSpendAllCoins()
    {
        TrySpend(balance, CoinReason.DevCheat);
    }
#endif
}
