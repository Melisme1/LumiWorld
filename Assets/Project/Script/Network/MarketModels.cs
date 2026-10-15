using System;
using System.Collections.Generic;

// Tên trường camelCase khớp JSON của server (JsonUtility so đúng tên trường).

/// <summary>
/// Một lô trên chợ. pricePerUnit là số Coins mỗi đơn vị, server tính sẵn.
/// </summary>
[Serializable]
public class MarketListingDto
{
    public string id;
    public string sellerId;
    public string sellerName;
    public string resourceId;
    public int amount;
    public int priceCoins;
    public float pricePerUnit;
    public string status;
    public string createdAt;
    public string expiresAt;
}

/// <summary>
/// Kết quả của GET /market/listings (có phân trang) và GET /market/listings/mine (không có phân trang).
/// </summary>
[Serializable]
public class MarketListingsResponse
{
    public List<MarketListingDto> items = new List<MarketListingDto>();
    public int page;
    public int pageSize;
    public bool hasMore;
}

[Serializable]
public class CreateListingRequest
{
    public string resourceId;
    public int amount;
    public int priceCoins;
}

/// <summary>
/// Body rỗng cho các POST không cần dữ liệu (mua, hủy).
/// </summary>
[Serializable]
public class EmptyRequest
{
}

[Serializable]
public class MarketPurchaseResponse
{
    public string listingId;
    public string resourceId;
    public int amount;
    public int paidCoins;
}

[Serializable]
public class MarketCancelResponse
{
    public string listingId;
    public string status;
}

/// <summary>
/// Payload của mục hộp thư CoinsFromSale: người bán nhận coins (đã trừ phí).
/// </summary>
[Serializable]
public class CoinsFromSalePayload
{
    public string listingId;
    public string resourceId;
    public int amount;
    public int coins;
    public int fee;
}

/// <summary>
/// Payload của mục ItemsBought (hàng vừa mua) và ItemsReturned (hàng trả lại vì hủy hoặc hết hạn).
/// reason chỉ có ở ItemsReturned: "cancelled" hoặc "expired".
/// </summary>
[Serializable]
public class ItemsPayload
{
    public string listingId;
    public string resourceId;
    public int amount;
    public string reason;
}
