using System;

/// <summary>
/// Các cấp độ quy mô phát triển của một Quần thể Định cư (Settlement Tier)
/// </summary>
public enum SettlementTier
{
    /// <summary>
    /// 1 ô: Làng nhỏ / Ấp sơ khai (2-3 căn nhà nhỏ, giếng nước, hàng rào gỗ bao quanh)
    /// </summary>
    Hamlet = 1,

    /// <summary>
    /// 2-4 ô: Thị trấn đang phát triển (các vách ngăn bên trong hạ xuống, xuất hiện Chợ, Lò rèn, Cối xay gió)
    /// </summary>
    Town = 2,

    /// <summary>
    /// 5+ ô (dạng tụ lại có ô lõi): Đại Đô Thị / Pháo Đài kiên cố (Lâu đài ở trung tâm, Tường thành đá & Tháp canh khép kín)
    /// </summary>
    GrandCity = 3,

    /// <summary>
    /// 5+ ô (dạng kéo dài thành chuỗi thẳng/rắn): Tuyến Phố Thương Mại (Trục đường dài, Quán trọ, Chợ hai bên đường)
    /// </summary>
    TradingAvenue = 4
}

/// <summary>
/// Vai trò chức năng của từng ô lục giác trong quần thể định cư
/// </summary>
public enum SettlementRole
{
    /// <summary>
    /// Ô dân cư thông thường (nhà ở + giếng/kho phụ trợ)
    /// </summary>
    Residential = 0,

    /// <summary>
    /// Ô thương mại dịch vụ (Khu chợ nhộn nhịp + quán trọ)
    /// </summary>
    MarketSquare = 1,

    /// <summary>
    /// Ô tiểu thủ công nghiệp (Lò rèn / Cối xay gió / Nông trại)
    /// </summary>
    Craftsman = 2,

    /// <summary>
    /// Ô trung tâm hành chính / chính trị của Đại đô thị (Lâu đài uy nghiêm hoặc Nhà thờ lớn)
    /// </summary>
    CivicCore = 3,

    /// <summary>
    /// Ô quân sự / phòng thủ (Doanh trại, trường bắn cung, tháp máy bắn)
    /// </summary>
    Military = 4
}

/// <summary>
/// Bí danh tương thích cho SettlementRole
/// </summary>
public enum SettlementTileRole
{
    Residential = 0,
    MarketSquare = 1,
    Craftsman = 2,
    CivicCore = 3,
    Military = 4
}

/// <summary>
/// Phân loại đối tượng 3D trong Settlement để tính toán tỉ lệ hiển thị thống nhất
/// </summary>
public enum SettlementScaleType
{
    /// <summary> Nhà ở dân sự, xưởng nghề, quán trọ, khu chợ </summary>
    Building = 0,

    /// <summary> Vật thể tâm làng: Giếng nước, đụn rơm, lều cắm trại </summary>
    Centerpiece = 1,

    /// <summary> Công trình kỳ vĩ: Lâu đài trung tâm, Nhà thờ lớn, Tháp canh góc </summary>
    Castle = 2,

    /// <summary> Ranh giới bao quanh: Hàng rào gỗ, Tường thành đá </summary>
    Border = 3,

    /// <summary> Đạo cụ đường phố: Thùng gỗ, bao tải, xe rùa </summary>
    Prop = 4,

    /// <summary> Hoa cỏ nhỏ, mầm cỏ, đám cỏ ba lá viền lối đi và chân tường </summary>
    Flora = 5,

    /// <summary> Đá nhỏ lát đường, lối mòn lát đá kết nối làng </summary>
    SteppingStone = 6,

    /// <summary> Cây bóng mát làng quê (Village Shade Trees) </summary>
    Tree = 7,

    /// <summary> Bụi cây rậm rạp, dương xỉ, hàng rào xanh (Bushes & Shrubs) </summary>
    Bush = 8
}

