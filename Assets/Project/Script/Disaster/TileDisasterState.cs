using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs
{
    /// <summary>
    /// Gắn lên GameObject của một ô lục giác (HexTileInfo) để lưu trạng thái thiên tai/vật cản
    /// đang phủ lên ô đó. Là cầu nối duy nhất giữa DisasterRuntimeManager và các hệ thống khác
    /// (khai thác, đặt thẻ, UI highlight, tô màu) — nhờ vậy không phải sửa sâu vào từng hệ thống.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TileDisasterState : MonoBehaviour
    {
        private static readonly List<TileDisasterState> Active = new List<TileDisasterState>();

        [Tooltip("Môi trường (biome) của ô, dùng để thiên tai cháy/hạn lan đúng vùng.")]
        public BiomeAffinity affinity = BiomeAffinity.None;

        [Tooltip("Toạ độ lục giác của ô. Được điền lúc Ensure/Spawn và khi Rain biến đổi ô. " +
                 "Cần cho các thao tác theo vị trí (mưa dập lửa tại ô, hàng đợi chờ bén lửa).")]
        public HexCoordinates coordinates;

        [Tooltip("Vật cản tĩnh trên ô (đá, gỗ mục). Chặn khai thác, KHÔNG phá hủy ô.")]
        public bool hasObstacle;

        [Tooltip("Ô đã bị cháy rừng phá hủy môi trường. Chặn khai thác tới khi hồi phục.")]
        public bool isBurned;

        [Tooltip("Mức khô cằn 0..1. Ô chưa từng nhận nước nên mưa nhỏ không thấm.")]
        [Range(0f, 1f)] public float aridity;

        [Tooltip("Ô đã từng được tưới nước (áp dụng mưa nhỏ) hay chưa.")]
        public bool hasReceivedWater;

        // Mỗi loại thiên tai có thể cùng phủ lên 1 ô (cháy + hạn). Ta lưu tập kind -> instanceId.
        private readonly Dictionary<DisasterKind, string> activeDisasters = new Dictionary<DisasterKind, string>();

        public IReadOnlyDictionary<DisasterKind, string> ActiveDisasters => activeDisasters;
        public bool HasAnyDisaster => activeDisasters.Count > 0;
        public bool IsArid => aridity >= 0.5f && !hasReceivedWater;

        /// <summary>
        /// Ô có môi trường để cháy: terrain Lush được lưu trực tiếp trên HexTileInfo,
        /// hoặc có Habitat/ thẻ Terrain trồng trên ô.
        ///
        /// Ý nghĩa: CHỈ ô Lush mới có thứ để cháy. Ô đất khô Arid chưa cải tạo không có gì
        /// để cháy nên cháy rừng không bén vào và không lan qua.
        ///
        /// QUAN TRỌNG — vì sao KHÔNG đoán "mọi ô không chứa arid":
        ///  Bản cũ trả true cho MỌI ô không chứa "arid". Nhưng DisasterRuntimeManager tự gắn
        ///  TileDisasterState lên TẤT CẢ ô (EnsureTileDisasterStates), nên ô đất trống Arid cũng
        ///  bị coi là Lush. Khi lửa lan sang, ô đó bị Mark(ForestFire) -> isBurned = true -> các
        ///  nhịp lan sau IsLushTile trả false -> đám cháy ĐỨNG YÊN suốt duration dù chưa hết
        ///  maxTiles (triệu chứng "cháy câm").
        ///
        /// Ô đã cháy không thể làm nhiên liệu cho nhịp lan tiếp theo.
        /// </summary>
        public bool IsLushTile
        {
            get
            {
                if (isBurned) return false;
                return HasHabitatOnTile;
            }
        }

        /// <summary>
        /// Ô có môi trường Lush để cháy hay không. Nền terrain đọc từ HexTileInfo,
        /// còn Habitat/Terrain card trên ô cũng tạo nhiên liệu cháy.
        /// </summary>
        public bool HasHabitatOnTile
        {
            get
            {
                var info = GetComponent<HexTileInfo>();
                if (info != null && info.hasLushEnvironment) return true;

                // 1. Container props sinh từ thẻ Habitat/Terrain.
                var tr = transform;
                for (int i = 0; i < tr.childCount; i++)
                {
                    var child = tr.GetChild(i);
                    if (child != null && child.name.StartsWith("Habitat_", StringComparison.Ordinal))
                        return true;
                }

                // 2. Thẻ Terrain đã đặt lên ô (trường hợp props chưa kịp sinh).
                var cards = GetComponentsInChildren<PlacedCard>(true);
                for (int i = 0; i < cards.Length; i++)
                {
                    if (cards[i] != null && cards[i].CardType == CardType.Terrain) return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Khi tắt Domain Reload lúc vào Play Mode, biến static KHÔNG tự xóa: danh sách Active
        /// giữ lại component của lần chơi trước (đã bị hủy) và trạng thái thiên tai rò rỉ sang
        /// lần chơi mới. Reset tường minh ở đây để mỗi lần Play đều bắt đầu sạch.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        /// <summary>Tất cả ô đang có state (kể cả ô chưa có thiên tai).</summary>
        public static IReadOnlyList<TileDisasterState> All => Active;

        public static TileDisasterState Ensure(GameObject tileObj)
        {
            if (tileObj == null) return null;
            var state = tileObj.GetComponent<TileDisasterState>();
            if (state == null) state = tileObj.AddComponent<TileDisasterState>();
            return state;
        }

        public static bool TryGet(GameObject tileObj, out TileDisasterState state)
        {
            state = tileObj != null ? tileObj.GetComponent<TileDisasterState>() : null;
            return state != null;
        }

        public bool Has(DisasterKind kind) => activeDisasters.ContainsKey(kind);

        /// <summary>Ô này có đang bị ĐÚNG instance đó phủ không (không chỉ cùng loại).</summary>
        public bool HasInstance(DisasterKind kind, string instanceId)
        {
            return instanceId != null &&
                activeDisasters.TryGetValue(kind, out var held) &&
                held == instanceId;
        }

        public void Mark(DisasterKind kind, string instanceId)
        {
            activeDisasters[kind] = instanceId;
        }

        /// <summary>
        /// Gỡ thiên tai khỏi ô. Trả về true CHỈ KHI ô thực sự đang giữ instanceId truyền vào.
        ///
        /// QUAN TRỌNG: đây là điều kiện để bên gọi (EndDisaster) biết có được phép dọn các cờ
        /// phụ (isBurned / hasObstacle / aridity) hay không. Nếu gỡ "theo loại" mà không kiểm tra
        /// instance, việc một đám cháy kết thúc sẽ dập tắt luôn isBurned của đám cháy khác đang chạy.
        ///
        /// Truyền instanceId = null để gỡ bất kể instance nào (dùng cho dọn dẹp thủ công).
        /// </summary>
        public bool Clear(DisasterKind kind, string instanceId = null)
        {
            if (!activeDisasters.TryGetValue(kind, out var held)) return false;
            if (instanceId != null && held != instanceId) return false;

            activeDisasters.Remove(kind);
            return true;
        }

        public void ClearAll()
        {
            activeDisasters.Clear();
        }

        /// <summary>
        /// Hệ số tốc độ khai thác của ô = tích các hệ số của mọi thiên tai đang phủ.
        /// 0 = dừng khai thác hoàn toàn (hạn hán/bão/cháy), 1 = bình thường.
        ///
        /// Ô KHÔ CẰN (aridity cao và chưa từng nhận nước) cũng bị giảm tốc, dù nó không
        /// nằm trong activeDisasters. Trước đây chỉ có kind BarrenLand mới chặn, nên ô bị
        /// khô do cháy để lại (isBurned/aridity) hoàn toàn không ảnh hưởng sản xuất.
        /// </summary>
        public float HarvestSpeedMultiplier(DisasterBalance balance)
        {
            if (hasObstacle || isBurned) return 0f;

            float multiplier = 1f;
            if (balance == null) return multiplier;

            // Khô cằn dùng chung hệ số của rule BarrenLand (mặc định 0.5) — một nguồn số liệu duy nhất.
            if (IsArid) multiplier *= balance.ResolveHarvestMultiplier(DisasterKind.BarrenLand);

            foreach (var kind in activeDisasters.Keys)
            {
                multiplier *= balance.ResolveHarvestMultiplier(kind);
            }
            return multiplier;
        }

        public bool BlocksHarvest(DisasterBalance balance)
        {
            if (hasObstacle || isBurned) return true;

            // Khô cằn làm CHẬM khai thác chứ không chặn hẳn, nên KHÔNG đưa IsArid vào đây.
            // (Tham số balance giữ nguyên trong chữ ký cho tương thích API đã ship.)
            foreach (var kind in activeDisasters.Keys)
            {
                if (!DisasterCatalog.BlocksHarvest(kind)) continue;

                // A non-zero disaster productivity multiplier slows harvesting instead of blocking it.
                float multiplier = balance != null
                    ? balance.ResolveHarvestMultiplier(kind)
                    : (kind == DisasterKind.Cyclone ? 0.5f : 0f);
                if (multiplier <= 0f) return true;
            }
            return false;
        }

        /// <summary>Loại thiên tai ưu tiên hiển thị cảnh báo (số enum nhỏ nhất = ưu tiên cao).</summary>
        public DisasterKind DominantKind()
        {
            if (hasObstacle) return DisasterKind.Obstacle;

            DisasterKind best = DisasterKind.Obstacle;
            bool found = false;
            foreach (var kind in activeDisasters.Keys)
            {
                if (!found || (int)kind < (int)best)
                {
                    best = kind;
                    found = true;
                }
            }
            return best;
        }

        /// <summary>
        /// Đồng bộ lại lớp tô màu của ô theo trạng thái hiện tại.
        /// Tự gắn TileDisasterVisualTint nếu ô chưa có, nên không cần setup trong Scene.
        /// </summary>
        public void RefreshVisual()
        {
            var tint = GetComponent<TileDisasterVisualTint>();
            if (tint == null) tint = gameObject.AddComponent<TileDisasterVisualTint>();
            tint.Apply(this);
        }

        /// <summary>Gỡ sạch lớp tô màu của ô (dùng khi thiên tai kết thúc hoặc khi xóa hết).</summary>
        public void ClearVisual()
        {
            var tint = GetComponent<TileDisasterVisualTint>();
            if (tint != null) tint.Clear();
        }
    }
}
