using System;
using System.Collections.Generic;

// Tên trường khớp JSON camelCase của server (JsonUtility so đúng tên trường).

/// <summary>
/// Một mục trong hộp thư. payload là chuỗi JSON của mục: đọc kind trước, rồi
/// JsonUtility.FromJson đúng lớp ứng với kind đó.
/// </summary>
[Serializable]
public class InboxItemDto
{
    public string id;
    public string kind;
    public string payload;
    public string createdAt;
}

[Serializable]
public class InboxResponse
{
    public List<InboxItemDto> items = new List<InboxItemDto>();
}

[Serializable]
public class InboxAckRequest
{
    public List<string> ids = new List<string>();
}

[Serializable]
public class InboxAckResponse
{
    public int acked;
}

[Serializable]
public class ObstacleInboxPayload
{
    public string obstacleId;
    public string kind;
    public int q;
    public int r;
    public double durationSeconds;
}

/// <summary>
/// Phần của GET /api/v1/config mà client đang dùng. Các trường khác server gửi kèm thì bỏ qua.
/// </summary>
[Serializable]
public class GameConfigDto
{
    public int syncInboxSeconds;

    public ObstacleConfigDto obstacle;
    // Flat fallback for server versions that expose obstacleFeeCoins directly.
    public int obstacleFeeCoins;

    // Chợ (xem MarketSync). Server trả đủ; client dùng để kiểm tra giá trước khi gửi.
    public int marketFeePercent;
    public int marketMaxActiveListings;
    public int marketListingHours;
    public float marketMinPriceFactor;
    public float marketMaxPriceFactor;
}

[Serializable]
public class ObstacleConfigDto
{
    public int feeCoins = 20;
}
