using System;
using System.Collections.Generic;

// Đổi thẻ thú (/api/v1/trades). Tên trường camelCase khớp JSON của server.

[Serializable]
public class CreateTradeRequest
{
    public string toPlayerId;
    public string offerCardId;
    public int offerCount;
    public string requestCardId;
    public int requestCount;
}

[Serializable]
public class TradeDto
{
    public string id;
    public string fromPlayerId;
    public string fromName;
    public string toPlayerId;
    public string toName;
    public string offerCardId;
    public int offerCount;
    public string requestCardId;
    public int requestCount;
    public string status;
    public string createdAt;
    public string expiresAt;
}

[Serializable]
public class TradeListResponse
{
    public List<TradeDto> items = new List<TradeDto>();
}

[Serializable]
public class TradeResolveResponse
{
    public string tradeId;
    public string status;
}

/// <summary>Payload của mục hộp thư TradeOffered: chỉ để báo, không mang thẻ.</summary>
[Serializable]
public class TradeOfferedPayload
{
    public string tradeId;
    public string fromName;
    public string offerCardId;
    public int offerCount;
    public string requestCardId;
    public int requestCount;
}
