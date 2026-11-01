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
    // 2: thêm Harmony, cột mốc và thành tựu đã nhận. 3: thêm bài trên tay. 4: thêm hàng chờ chế biến.
    // Bản lưu cũ hơn vẫn đọc được (các trường mới để mặc định).
    public int version = 4;
    public int coins;
    public bool starterKitGranted;
    public List<ResourceStack> resources = new List<ResourceStack>();
    public List<ActiveOrder> openOrders = new List<ActiveOrder>();
    public List<OrderSlotRefill> orderRefills = new List<OrderSlotRefill>();

    // Id các mục hộp thư của server đã áp vào bản lưu này. Mục được áp và ghi file
    // trước khi ack, nên một mục không bao giờ được cộng hai lần.
    public List<string> appliedInboxIds = new List<string>();

    // Thao tác chợ đang chờ server xác nhận (đăng bán, mua, hủy). Xem PendingMarketOp.
    public List<PendingMarketOp> pendingMarketOps = new List<PendingMarketOp>();

    // Thao tác đổi thẻ thú đang chờ server (gửi, đồng ý, từ chối, hủy). Xem PendingTradeOp. Bản lưu cũ để trống.
    public List<PendingTradeOp> pendingTradeOps = new List<PendingTradeOp>();

    // Tiến trình của tài khoản (AchievementManager). harmonyInitialized = false ở bản lưu cũ:
    // khi đó dùng Harmony khởi điểm của AchievementManager rồi ghi lại.
    public bool harmonyInitialized;
    public int totalHarmony;
    public bool hasRankedUpAnyCreature;
    // Id cột mốc (M1_...) và thành tựu đã nhận thưởng; đã có trong danh sách thì không bao giờ nhận lại.
    public List<string> claimedMilestoneIds = new List<string>();
    public List<string> claimedAchievementIds = new List<string>();

    // Bài trên tay (CardDeskController), gộp theo cardID. starterHandGranted = false: chưa từng chia bài khởi đầu
    // (tài khoản mới, New Game hoặc bản lưu cũ), khi đó dùng bộ bài khởi đầu rồi ghi lại.
    public bool starterHandGranted;
    public List<HandCardStack> hand = new List<HandCardStack>();

    // Hàng chờ chế biến (CraftingQueueSystem). Nguyên liệu đã trừ khỏi kho lúc bắt đầu mẻ,
    // nên mẻ phải nằm cùng file với kho. Mẻ đã nhận thành phẩm bị xóa khỏi danh sách.
    public List<CraftingJobRecord> craftingJobs = new List<CraftingJobRecord>();

    // ISO 8601 (UTC), chỉ để dễ đọc file khi debug.
    public string savedAtUtc;
}

/// <summary>
/// Một loại thẻ trên tay kèm số lá, ví dụ Forest x3. cardID tra trong CardCatalog.
/// </summary>
[Serializable]
public class HandCardStack
{
    public string cardId;
    public int count;
}

/// <summary>
/// Một mẻ chế biến chưa nhận. Không lưu giờ của GameClock (đồng hồ này đếm lại từ 0 mỗi lần mở game),
/// chỉ lưu giờ UTC xong mẻ và số giây còn lại lúc ghi (dùng khi đồng hồ máy bị lùi).
/// </summary>
[Serializable]
public class CraftingJobRecord
{
    public string jobId;
    public string recipeId;
    public int quantity;
    public double durationSeconds;
    public string completeAtUtc;      // ISO 8601 UTC
    public double remainingSeconds;
    public bool isCompleted;
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
/// Vẫn chạy khi tắt game; OrderBoardSystem cập nhật deadline trước save để loại quãng pause trong phiên.
/// </summary>
[Serializable]
public class OrderSlotRefill
{
    public int slot;
    public string refillAtUtc;
}
