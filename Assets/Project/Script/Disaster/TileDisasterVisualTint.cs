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
        [Tooltip("Vật cản: hơi xám lạnh để phân biệt với đất trống.")]
        [SerializeField] private Color obstacleTint = new Color(0.72f, 0.74f, 0.78f, 1f);

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

        /// <summary>Chỉ cảnh báo "không tô được renderer nào" một lần cho mỗi lần tô, tránh spam log mỗi frame.</summary>
        private bool diagnosticsLogged;

        /// <summary>Tô lại ô theo trạng thái thiên tai hiện tại. Gọi sau khi state đã cập nhật.</summary>
        public void Apply(TileDisasterState state)
        {
            if (state == null) { Clear(); return; }
            if (!enableTint) { Clear(); return; }

            Color? tint = ResolveTint(state);
            if (!tint.HasValue)
            {
                // Không có gì để tô -> gỡ lớp tô cũ.
                Clear();
                return;
            }

            CacheRenderers();
            _pendingTint = tint.Value;
            _hasPendingTint = true;
            _pendingTintSince = Time.unscaledTime;

            ApplyPendingTintTo(tinted);
        }

        /// <summary>
        /// Áp màu đang chờ lên một tập renderer. Tách riêng khỏi Apply() để có thể gọi lại
        /// từ LateUpdate nếu tập renderer phát sinh thêm sau khi Apply (ví dụ các GameObject
        /// con/props được sinh trong cùng khung hình sau lệnh tô).
        /// </summary>
        private void ApplyPendingTintTo(List<Renderer> targets)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var r = targets[i];
                if (r == null) continue;
                SetTint(r, _pendingTint);

                // CHẨN ĐOÁN: shader của renderer này KHÔNG có cả _BaseColor lẫn _Color
                // (ví dụ shader dùng _MainColor hoặc ghi màu qua texture) -> property block
                // không có tác dụng dù đã đặt đúng.
                Material shared = r.sharedMaterial;
                if (shared != null &&
                    !shared.HasProperty(BaseColorId) && !shared.HasProperty(ColorId))
                {
                    _diagShaderMismatch++;
                    if (!diagnosticsLogged)
                    {
                        Debug.LogWarning(
                            $"[LumiWorld Disaster Tint] Renderer '{r.name}' (ô '{name}') dùng shader " +
                            $"'{(shared.shader != null ? shared.shader.name : "?")}' KHÔNG có property " +
                            $"_BaseColor / _Color -> MaterialPropertyBlock không đổi được màu ô.\n" +
                            $"  • Cần shader nhận _BaseColor (URP Lit/Unlit) hoặc bật tint bằng cách khác.", this);
                    }
                }
            }
        }

        /// <summary>
        /// Lớp tô màu hiện tại của ô. Được lưu lại để:
        ///  - Không bị gỡ nhầm khi có hệ thống khác xoá property block trên renderer dùng chung.
        ///  - Tự phục hồi ở LateUpdate nếu lớp tô bị mất trong frame.
        /// </summary>
        private Color _pendingTint = Color.white;
        private bool _hasPendingTint;
        private float _pendingTintSince;
        private int _diagShaderMismatch;

        /// <summary>Khôi phục lớp tô nếu nó bị mất trong frame (một hệ thống khác gọi SetPropertyBlock).</summary>
        private void LateUpdate()
        {
            if (!_hasPendingTint) return;
            if (Time.unscaledTime - _pendingTintSince < 0.1f) return; // vừa tô xong

            // Chỉ áp lại khi thực sự mất, tránh ghi lại mỗi frame.
            for (int i = 0; i < tinted.Count; i++)
            {
                var r = tinted[i];
                if (r == null) continue;
                if (!r.HasPropertyBlock()) SetTint(r, _pendingTint);
            }
        }

        /// <summary>Gỡ lớp tô màu, trả ô về nguyên trạng material dùng chung.</summary>
        public void Clear()
        {
            _hasPendingTint = false;
            for (int i = 0; i < tinted.Count; i++)
            {
                var renderer = tinted[i];
                if (renderer == null) continue;
                renderer.SetPropertyBlock(null);
            }
            tinted.Clear();
            blocks.Clear();
            diagnosticsLogged = false; // lần tô sau được phép báo lỗi lại
        }

        /// <summary>
        /// Trộn màu của MỌI thiên tai đang phủ ô (ô có thể vừa cháy vừa hạn), theo thứ tự
        /// nặng → nhẹ: cháy → khô cằn → hạn hán → bão → vật cản.
        ///
        /// Trước đây hàm này return ngay ở ô cháy nên bất kỳ trạng thái nào khác bị "che";
        /// đồng thời ô chỉ có vật cản (không cháy/khô/hạn/bão) rơi vào nhánh default của
        /// DisasterCatalog.TintMultiplier = màu trắng -> KHÔNG BAO GIỜ đổi màu.
        /// Trả null chỉ khi ô thực sự không có gì để tô.
        /// </summary>
        private Color? ResolveTint(TileDisasterState state)
        {
            bool any = state.hasObstacle || state.isBurned || state.IsArid || state.HasAnyDisaster;
            if (!any) return null;

            Color tint = Color.white;
            if (state.isBurned) tint *= burnedTint;
            if (state.IsArid) tint *= aridTint;
            if (state.Has(DisasterKind.Drought)) tint *= droughtTint;
            if (state.Has(DisasterKind.Cyclone)) tint *= cycloneTint;
            if (state.hasObstacle) tint *= obstacleTint;

            tint.a = 1f; // giữ alpha đục để không làm ô trong suốt tốn công vô ích
            return tint;
        }

        /// <summary>
        /// Gom renderer của thân ô. Kết quả được GIỮ LẠI giữa các lần gọi vì:
        ///  - GetComponentsInChildren cấp phát mảng mới mỗi lần (cháy lan gọi liên tục).
        ///  - Nhiều prefab tile có cấu trúc: GameObject gốc (script gắn ở đây)
        ///    → GameObject con chứa MeshRenderer. Nếu không quét cả cây con thì
        ///    tinted.Count == 0 và ô sẽ KHÔNG BAO GIỜ đổi màu.
        ///  - Nếu cây con không có renderer nào, đi lên cấp cha để tìm (trường hợp
        ///    TileDisasterState bị gắn vào một GameObject con thay vì gốc khối hex).
        /// </summary>
        private void CacheRenderers()
        {
            if (tinted.Count > 0) return; // đã cache ở lần Apply trước

            tinted.Clear();
            CollectRenderers(transform, tinted);
            if (tinted.Count == 0 && transform.parent != null)
                CollectRenderers(transform.parent, tinted);
        }

        /// <summary>Thêm mọi Renderer của root (kể cả đang tắt) vào danh sách, bỏ qua prop của PlacedCard.</summary>
        private void CollectRenderers(Transform root, List<Renderer> into)
        {
            if (root == null) return;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                if (into.Contains(renderer)) continue;
                if (skipPlacedCardRenderers && renderer.GetComponentInParent<PlacedCard>() != null) continue;
                into.Add(renderer);
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
