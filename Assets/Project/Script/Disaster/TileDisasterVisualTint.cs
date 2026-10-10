using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs
{
    /// <summary>
    /// TÔ MÀU Ô LỤC GIÁC KHI BỊ THIÊN TAI PHỦ — AN TOÀN TUYỆT ĐỐI VỚI ASSET.
    ///
    /// ============================ CẢNH BÁO QUAN TRỌNG ============================
    /// TUYỆT ĐỐI KHÔNG gán màu qua renderer.sharedMaterial hoặc material.color!
    ///
    /// Các ô lục giác trong project này DÙNG CHUNG một material
    /// (xem HexBiomeClusterConnector.GetTileMaterial -> trả về r.sharedMaterial).
    /// Vì vậy sửa material là sửa material CỦA MỌI Ô trên bản đồ cùng lúc, và vì đó là
    /// asset thật trên đĩa, Unity LƯU THAY ĐỔI ĐÓ XUỐNG FILE .mat. Kết quả:
    ///   - Toàn bộ bản đồ đen dần theo số ô bị phủ (0.28^n -> đen tuyền).
    ///   - Vết đen KHÔNG mất khi bấm xóa thiên tai, vì nó đã nằm trong file trên đĩa.
    ///   - Play lại vẫn đen, vì material hỏng được nạp lại từ asset.
    ///
    /// Cách DUY NHẤT đúng: MaterialPropertyBlock — áp màu cho RIÊNG một Renderer,
    /// không đụng material gốc, không ghi gì xuống đĩa. Gỡ bằng SetPropertyBlock(null).
    /// =============================================================================
    ///
    /// Lớp này là nơi DUY NHẤT được phép đổi màu ô. Mọi hệ thống khác muốn tô ô
    /// hãy gọi qua đây, đừng tự đụng vào Renderer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TileDisasterVisualTint : MonoBehaviour
    {
        // Property màu của URP Lit ("_BaseColor") và Built-in Lit/Unlit ("_Color").
        // Project dùng URP (HexGhostVisual tham chiếu "_BaseColor"), nhưng vẫn set cả hai
        // để hoạt động với mọi shader được gán vào ô.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Bật/Tắt")]
        [Tooltip("Tô màu ô khi bị thiên tai phủ để nhìn thấy hiệu quả bằng mắt. Tắt = chỉ dùng vòng highlight của UI.")]
        [SerializeField] private bool enableTint = true;

        [Header("Hệ số màu khi ô BỊ PHỦ")]
        [Tooltip("Hệ số NHÂN vào màu gốc của ô (không phải màu phẳng). Giữ texture và tông đất gốc.")]
        [SerializeField] private Color burnedTint = new Color(0.28f, 0.22f, 0.18f, 1f);
        [SerializeField] private Color aridTint = new Color(1.00f, 0.80f, 0.52f, 1f);
        [SerializeField] private Color droughtTint = new Color(1.00f, 0.88f, 0.62f, 1f);
        [SerializeField] private Color cycloneTint = new Color(0.78f, 0.86f, 1.00f, 1f);

        [Header("An toàn")]
        [Tooltip("Bỏ qua renderer thuộc PlacedCard (vật cản, cây cối) — chỉ tô thân ô lục giác.")]
        [SerializeField] private bool skipPlacedCardRenderers = true;

        /// <summary>
        /// Renderer đang bị tô của MỖI ô. Lưu theo từng ô để một ô có thể gỡ sạch
        /// mà không ảnh hưởng ô khác (material dùng chung, nhưng property block thì riêng).
        /// </summary>
        private readonly List<Renderer> tinted = new List<Renderer>();
        private readonly Dictionary<Renderer, MaterialPropertyBlock> blocks =
            new Dictionary<Renderer, MaterialPropertyBlock>();

        /// <summary>Tô lại ô theo trạng thái thiên tai hiện tại. Gọi sau khi state đã cập nhật.</summary>
        public void Apply(TileDisasterState state)
        {
            if (state == null) { Clear(); return; }
            if (!enableTint) { Clear(); return; }

            Color? tint = ResolveTint(state);
            if (!tint.HasValue) { Clear(); return; }

            CacheRenderers();
            for (int i = 0; i < tinted.Count; i++)
            {
                SetTint(tinted[i], tint.Value);
            }
        }

        /// <summary>Gỡ lớp tô màu, trả ô về nguyên trạng material dùng chung.</summary>
        public void Clear()
        {
            for (int i = 0; i < tinted.Count; i++)
            {
                var renderer = tinted[i];
                if (renderer == null) continue;
                renderer.SetPropertyBlock(null);
            }
            tinted.Clear();
            blocks.Clear();
        }

        /// <summary>
        /// Ưu tiên hiển thị: cháy > khô cằn > hạn hán > bão (nặng nhất trước).
        /// Trả null nếu ô không có gì đáng tô.
        /// </summary>
        private Color? ResolveTint(TileDisasterState state)
        {
            if (state.isBurned) return burnedTint;
            if (state.IsArid) return aridTint;
            if (state.Has(DisasterKind.Drought)) return droughtTint;
            if (state.Has(DisasterKind.Cyclone)) return cycloneTint;
            return null;
        }

        private void CacheRenderers()
        {
            tinted.Clear();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                if (skipPlacedCardRenderers && renderer.GetComponentInParent<PlacedCard>() != null) continue;
                tinted.Add(renderer);
            }
        }

        /// <summary>
        /// Áp màu bằng MaterialPropertyBlock — KHÔNG tạo material instance, KHÔNG đụng asset.
        /// Property block được LƯU LẠI cho từng renderer để lần sau chỉ cập nhật màu.
        /// </summary>
        private void SetTint(Renderer renderer, Color color)
        {
            if (renderer == null) return;

            if (!blocks.TryGetValue(renderer, out var block))
            {
                block = new MaterialPropertyBlock();
                blocks[renderer] = block;
            }

            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }

        private void OnDisable()
        {
            // Ô bị ẩn/hủy (ví dụ SuppressHabitatOnTile hoặc TransformTile) không được
            // để lại property block treo trên renderer dùng chung.
            Clear();
        }
    }
}
