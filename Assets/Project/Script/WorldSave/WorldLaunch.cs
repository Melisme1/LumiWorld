/// <summary>
/// Chế độ đang mở đảo. Own: đảo của mình, được ghi save. Visit: đảo người khác, chỉ đọc.
/// Ở Visit: không ghi world và economy, không lên server, không đặt thẻ, không thu phí,
/// không trao điểm hay mốc, không chạy thiên tai, không thu hoạch, không cộng tài nguyên.
/// Giai đoạn 3 sẽ lấy đảo từ server thay vì file trên máy.
/// </summary>
public static class WorldLaunch
{
    /// <summary>true khi đang xem đảo người khác (chỉ đọc).</summary>
    public static bool IsVisit { get; private set; }

    /// <summary>Mã người chơi sở hữu đảo đang mở.</summary>
    public static string WorldId { get; private set; } = "";

    public static void OpenOwn(string playerId)
    {
        IsVisit = false;
        WorldId = playerId ?? "";
    }

    public static void OpenVisit(string ownerId)
    {
        IsVisit = true;
        WorldId = ownerId ?? "";
    }
}
