using System;

/// <summary>
/// Một thao tác chợ đã làm trên máy nhưng server chưa xác nhận. Hàng hoặc Coins đã được trừ trên máy
/// và thao tác này được lưu cùng bản lưu. Server từ chối thì trả lại; mất mạng thì gửi lại đúng opKey.
/// </summary>
[Serializable]
public class PendingMarketOp
{
    // Khóa Idempotency-Key: gửi lại cùng khóa thì server không làm lại
    public string opKey;

    // "CreateListing", "BuyListing" hoặc "CancelListing" (xem MarketSync)
    public string kind;

    // Rỗng với CreateListing
    public string listingId;

    public string resourceId;
    public int amount;
    public int priceCoins;
    public string createdAtUtc;
}
