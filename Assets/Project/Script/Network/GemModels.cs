using System;
using System.Collections.Generic;

// Gem và nạp Gem (/api/v1/gems, /api/v1/payments). Tên trường camelCase khớp JSON của server.

[Serializable]
public class GemPackageDto
{
    public string id;
    public string name;
    public int gems;
    public int bonusGems;
    public int totalGems;
    public int priceVnd;
}

[Serializable]
public class GemPackagesResponse
{
    public string currency;
    public List<GemPackageDto> items = new List<GemPackageDto>();
}

[Serializable]
public class GemMeResponse
{
    public long balance;
}

[Serializable]
public class PaymentProviderDto
{
    public string id;
    public string name;
    public bool available;
}

[Serializable]
public class PaymentProvidersResponse
{
    public List<PaymentProviderDto> items = new List<PaymentProviderDto>();
}

[Serializable]
public class CreateGemOrderRequest
{
    public string packageId;
    public string provider;
}

[Serializable]
public class GemOrderDto
{
    public string id;
    public string orderCode;
    public int gems;
    public int amountVnd;
    public string provider;
    public string status;
    public string payUrl;   // chỉ có khi vừa tạo đơn
}

/// <summary>Payload mục hộp thư GemsPurchased: server đã cộng Gem cho một đơn nạp.</summary>
[Serializable]
public class GemsPurchasedPayload
{
    public string orderId;
    public string orderCode;
    public int gems;
    public long balance;
}
