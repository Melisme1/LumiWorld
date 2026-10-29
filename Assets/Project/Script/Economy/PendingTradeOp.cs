using System;

/// <summary>
/// Một thao tác đổi thẻ thú đã làm trên máy nhưng server chưa xác nhận, lưu cùng bản lưu kinh tế.
/// Offer: thẻ offerCardId đã rời tay người gửi. Accept: thẻ requestCardId đã rời tay người nhận.
/// Decline và Cancel không trừ gì. Server từ chối hẳn thì trả thẻ lại; mất mạng thì gửi lại đúng opKey.
/// </summary>
[Serializable]
public class PendingTradeOp
{
    // Khóa Idempotency-Key
    public string opKey;

    // "Offer", "Accept", "Decline" hoặc "Cancel" (xem TradeSync)
    public string kind;

    // Rỗng với Offer
    public string tradeId;

    // Chỉ với Offer: chủ đảo nhận đề nghị
    public string toPlayerId;

    // Id dạng "card.<cardID>"
    public string offerCardId;
    public int offerCount;
    public string requestCardId;
    public int requestCount;
    public string createdAtUtc;
}
