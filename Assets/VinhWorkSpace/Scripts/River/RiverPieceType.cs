using System;

/// <summary>
/// Các biến thể hình học của đoạn suối trong lòng ô lục giác (Preserve / Dorfromantik style)
/// </summary>
public enum RiverPieceType
{
    /// <summary>Dải suối thẳng xuyên tâm (nối 2 cổng đối diện 180° - ví dụ hướng 0 và 3)</summary>
    Straight,

    /// <summary>Dải suối rẽ cua 120° (nối 2 cổng lệch 2 bước - ví dụ hướng 0 và 2)</summary>
    Curve120,

    /// <summary>Dải suối rẽ cua gắt 60° (nối 2 cổng kề nhau - ví dụ hướng 0 và 1)</summary>
    Curve60,

    /// <summary>Đầu nguồn suối / Cụt dòng (chỉ mở 1 cổng nối - ví dụ hang đá non bộ hay suối cạn)</summary>
    SpringHead,

    /// <summary>Ngã ba hợp lưu suối hình chữ Y (nối 3 cổng - ví dụ hướng 0, 2, 4)</summary>
    Fork3Way,

    /// <summary>Hồ / Ao nước cuối nguồn (1 cổng vào hồ hoặc hồ độc lập giữa lòng ô)</summary>
    PondBasin,

    /// <summary>Thác nước bậc thang kết nối chênh lệch độ cao giữa 2 tầng hex</summary>
    Waterfall
}
