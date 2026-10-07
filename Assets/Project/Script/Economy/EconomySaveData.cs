using System;
using System.Collections.Generic;

// Dữ liệu kinh tế của một người chơi được giữ lại giữa các lần chơi.
//
// Tên trường để camelCase có chủ ý: JsonUtility ánh xạ key JSON theo đúng tên
// trường, và server cũng dùng camelCase. Đổi tên trường sẽ làm hỏng bản lưu cũ
// mà không báo lỗi.

/// <summary>
/// Ví Coins, kho tài nguyên và các đơn hàng đang mở trên Bảng Đơn Hàng.
/// </summary>
[Serializable]
public class EconomySaveData
{
    public int version = 1;
    public int coins;
    public bool starterKitGranted;
    public List<ResourceStack> resources = new List<ResourceStack>();
    public List<ActiveOrder> openOrders = new List<ActiveOrder>();
    public List<OrderSlotRefill> orderRefills = new List<OrderSlotRefill>();

    // ISO 8601 (UTC), chỉ để dễ đọc file khi debug.
    public string savedAtUtc;
}

/// <summary>
/// Một loại tài nguyên kèm số lượng, ví dụ 12 wood.
/// </summary>
[Serializable]
public class ResourceStack
{
    public string resourceId;
    public int amount;
}

/// <summary>
/// Một đơn hàng đang nằm trên Bảng Đơn Hàng (OrderBoardSystem tạo và giao đơn).
/// Số lượng và thưởng được chốt lúc tạo đơn, nên chỉnh mẫu đơn không làm đổi đơn đang mở.
/// </summary>
[Serializable]
public class ActiveOrder
{
    public string orderId;
    public string templateId;
    public int slot;
    public List<ResourceStack> requirements = new List<ResourceStack>();
    public int rewardCoins;
    public string createdAtUtc;
}

/// <summary>
/// Ô đơn đang trống vì người chơi vừa bỏ đơn: tới refillAtUtc (ISO 8601, UTC) thì có đơn mới.
/// Tính theo giờ thật nên vẫn chạy khi tắt game.
/// </summary>
[Serializable]
public class OrderSlotRefill
{
    public int slot;
    public string refillAtUtc;
}
