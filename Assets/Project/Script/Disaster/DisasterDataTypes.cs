using System;
using UnityEngine;

namespace LumiWorld.Acs
{
    /// <summary>
    /// Loại thiên tai/vật cản. Thứ tự số là thứ tự ưu tiên hiển thị cảnh báo trên UI
    /// (số nhỏ = ưu tiên cao hơn), nên KHÔNG được đổi giá trị đã ship.
    /// </summary>
    public enum DisasterKind
    {
        Obstacle = 0,    // Vật cản tĩnh: chặn 1 ô, không phá hủy
        BarrenLand = 1,  // Khô cằn: ô chưa từng nhận nước, mưa nhỏ không thấm
        Drought = 2,     // Hạn hán: dừng khai thác 1 vùng môi trường
        Cyclone = 3,     // Lốc xoáy/bão: lan theo bán kính, dừng khai thác nhiều ô
        ForestFire = 4   // Cháy rừng: lan theo môi trường, PHÁ HỦY ô (trả thẻ Thú)
    }

    /// <summary>
    /// Cường độ thẻ mưa. Khô cằn phân biệt rõ: mưa nhỏ đặt xuống không thấm (bị từ chối),
    /// phải dùng mưa lớn mới cải tạo được ô.
    /// </summary>
    public enum DisasterRainStrength
    {
        Light = 0,
        Heavy = 1
    }

    /// <summary>
    /// Đồng hồ dùng cho vòng đời thiên tai. Tách riêng khỏi IProductionClock để sau này
    /// ghép vào time system của game (day/season) mà không phải sửa logic thiên tai.
    /// Mặc định: wall-clock thực (UTC).
    /// </summary>
    public interface IDisasterClock
    {
        double NowSeconds { get; }
    }

    public sealed class SystemDisasterClock : IDisasterClock
    {
        public double NowSeconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
    }

    // Một tai họa đang diễn ra trên bản đồ. Mọi hệ thống (ô, môi trường, UI) đọc chung dữ liệu này.
    [Serializable]
    public sealed class DisasterInstance
    {
        [Tooltip("ID phiên của lần xuất hiện, không đổi trong suốt thời gian tồn tại.")]
        public string instanceId;
        public DisasterKind kind;
        [Tooltip("Ô bắt nguồn. Cháy rừng lan theo môi trường; bão lan theo bán kính.")]
        public HexCoordinates origin;
        [Tooltip("Môi trường CHỐT tại thời điểm sinh (chỉ dùng cho cháy rừng). Lấy một lần ở " +
                 "SpawnDisaster rồi giữ nguyên: nếu đọc lại từ ô gốc mỗi nhịp lan, ô gốc đã cháy sẽ " +
                 "mất dấu vết biome (props bị ẩn) khiến mọi ô kề bị chặn -> lửa không lan được.")]
        public BiomeAffinity originAffinity;
        [Tooltip("Số giây còn lại trước khi tự tắt. Lấy mẫu MỘT LẦN lúc tạo, không đọc lại balance.")]
        public double remainingSeconds;
        [Tooltip("Tổng thời lượng ban đầu, dùng để vẽ vòng tiến độ.")]
        public double totalSeconds;
        [Tooltip("Số lần thẻ mưa đã được dùng lên thiên tai này.")]
        public int rainApplied;
        [Tooltip("Số ô đã bị phá hủy (chỉ cháy rừng phá ô).")]
        public int destroyedCount;
        [Tooltip("Mốc thời gian thực để UI tính thời lượng highlight, không phải nguồn gameplay.")]
        public float startedAtUnscaledTime;

        public double Progress => totalSeconds <= 0 ? 1 : 1 - Math.Max(0, remainingSeconds) / totalSeconds;
        public bool IsExpired => remainingSeconds <= 0;
    }

    /// <summary>
    /// Bảng tra cứu nhanh theo loại thiên tai: tên hiển thị, màu, quy tắc chặn khai thác
    /// và cường độ mưa tối thiểu. Đây là balance, gom một chỗ.
    /// </summary>
    public static class DisasterCatalog
    {
        public static string DisplayName(DisasterKind kind)
        {
            switch (kind)
            {
                case DisasterKind.Obstacle: return "Obstacle";
                case DisasterKind.ForestFire: return "Forest Fire";
                case DisasterKind.Cyclone: return "Cyclone";
                case DisasterKind.Drought: return "Drought";
                case DisasterKind.BarrenLand: return "Barren Land";
                default: return "Disaster";
            }
        }

        public static string ShortLabel(DisasterKind kind)
        {
            switch (kind)
            {
                case DisasterKind.Obstacle: return "OBSTACLE";
                case DisasterKind.ForestFire: return "FIRE";
                case DisasterKind.Cyclone: return "CYCLONE";
                case DisasterKind.Drought: return "DROUGHT";
                case DisasterKind.BarrenLand: return "BARREN";
                default: return "!";
            }
        }

        /// <summary>Màu accent dùng cho vòng highlight và màu tô ô.</summary>
        public static Color AccentColor(DisasterKind kind)
        {
            switch (kind)
            {
                case DisasterKind.Obstacle: return new Color(0.42f, 0.45f, 0.52f, 1f);
                case DisasterKind.ForestFire: return new Color(1f, 0.42f, 0.12f, 1f);
                case DisasterKind.Cyclone: return new Color(0.38f, 0.72f, 1f, 1f);
                case DisasterKind.Drought: return new Color(0.95f, 0.72f, 0.26f, 1f);
                case DisasterKind.BarrenLand: return new Color(0.74f, 0.56f, 0.34f, 1f);
                default: return Color.white;
            }
        }

        /// <summary>Màu chữ/nền banner cảnh báo.</summary>
        public static Color AlertColor(DisasterKind kind)
        {
            switch (kind)
            {
                case DisasterKind.ForestFire: return new Color(0.98f, 0.32f, 0.18f, 1f);
                case DisasterKind.Cyclone: return new Color(0.30f, 0.62f, 0.96f, 1f);
                case DisasterKind.Drought: return new Color(0.96f, 0.72f, 0.22f, 1f);
                case DisasterKind.BarrenLand: return new Color(0.72f, 0.54f, 0.32f, 1f);
                default: return new Color(0.70f, 0.72f, 0.78f, 1f);
            }
        }

        /// <summary>
        /// Hệ số nhân màu dùng để TÔ ô (không phải màu phẳng).
        /// Nhân vào màu gốc nên giữ được texture và độ tương phản của từng loại đất.
        /// </summary>
        public static Color TintMultiplier(DisasterKind kind)
        {
            switch (kind)
            {
                case DisasterKind.ForestFire: return new Color(0.28f, 0.22f, 0.18f, 1f); // ô cháy: xỉn, ngả than
                case DisasterKind.BarrenLand: return new Color(1.00f, 0.80f, 0.52f, 1f); // khô cằn: ngả vàng nâu
                case DisasterKind.Drought: return new Color(1.00f, 0.88f, 0.62f, 1f);    // hạn hán: vàng nhạt
                case DisasterKind.Cyclone: return new Color(0.78f, 0.86f, 1.00f, 1f);    // bão: ngả xanh lạnh
                default: return Color.white;
            }
        }

        /// <summary>Thiên tai nào phải dập bằng thẻ mưa trước khi khai thác lại được.</summary>
        public static bool BlocksHarvest(DisasterKind kind)
        {
            return kind == DisasterKind.ForestFire || kind == DisasterKind.Cyclone ||
                kind == DisasterKind.Drought || kind == DisasterKind.BarrenLand;
        }

        /// <summary>Chỉ cháy rừng mới phá hủy ô (trả thẻ Thú, mất môi trường).</summary>
        public static bool DestroysTiles(DisasterKind kind) => kind == DisasterKind.ForestFire;

        /// <summary>
        /// Cường độ mưa tối thiểu để dập. Khô cằn (ô chưa có nước) BẮT BUỘC mưa lớn,
        /// các loại khác mưa nhỏ là đủ.
        /// </summary>
        public static int RequiredRainStrength(DisasterKind kind)
        {
            return kind == DisasterKind.BarrenLand ? (int)DisasterRainStrength.Heavy : (int)DisasterRainStrength.Light;
        }
    }
}
