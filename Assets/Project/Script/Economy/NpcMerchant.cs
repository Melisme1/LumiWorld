using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thương nhân NPC mua tài nguyên dư của người chơi với giá sàn bằng baseValue (game_config npcSaleRate = 1.0).
/// Giá này luôn thấp hơn thưởng đơn hàng (hệ số 1,5 trở lên), nên giao đơn luôn lợi hơn bán lẻ.
/// </summary>
public static class NpcMerchant
{
    /// <summary>
    /// Hệ số giá mua so với baseValue. Khi có server (giai đoạn 4) sẽ đọc từ game_config.
    /// </summary>
    public const float PriceRate = 1f;

    /// <summary>
    /// Số Coins thương nhân trả cho một đơn vị tài nguyên.
    /// </summary>
    public static int GetUnitPrice(ResourceData resource)
    {
        if (resource == null) return 0;
        return Mathf.Max(1, Mathf.RoundToInt(resource.baseValue * PriceRate));
    }

    /// <summary>
    /// Bán amount tài nguyên: trừ kho và cộng Coins cùng lúc. Kho không đủ thì không bán gì và trả về false.
    /// </summary>
    public static bool TrySell(ResourceData resource, int amount)
    {
        if (resource == null || amount <= 0 || string.IsNullOrEmpty(resource.resourceID)) return false;

        List<ResourceStack> sold = new List<ResourceStack>
        {
            new ResourceStack { resourceId = resource.resourceID, amount = amount }
        };
        if (!ResourceInventory.Instance.TryRemoveAll(sold)) return false;

        CurrencyWallet.Instance.Add(amount * GetUnitPrice(resource), CoinReason.NpcSale);
        return true;
    }
}
