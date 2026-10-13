using UnityEngine;

namespace LumiWorld.Acs
{
    /// <summary>
    /// Quản lý hiệu ứng particle (khói + lửa) cho MỘT ô lục giác.
    ///
    /// Trách nhiệm:
    ///  - Giữ tham chiếu tới ParticleSystem khói và lửa đang chạy trên ô.
    ///  - Chuyển trạng thái theo đúng kịch bản thiên tai cháy rừng:
    ///      1. Ô "sắp cháy" (đang chờ bén lửa) -> CHỈ khói (báo hiệu trước).
    ///      2. Ô bén lửa thật (đã cháy)      -> khói + lửa.
    ///      3. Mưa dập / thiên tai kết thúc  -> tắt dần rồi tự hủy.
    ///
    /// AN TOÀN:
    ///  - Chỉ sinh/destroy GameObject particle do chính nó tạo, không đụng tới material/renderer
    ///    của ô lục giác (việc tô màu do TileDisasterVisualTint lo).
    ///  - Nếu thiếu prefab (do người dùng chưa gán) thì tự thoát êm, không log lỗi rác.
    ///
    /// GẮN Ở ĐÂU: tự động AddComponent bởi DisasterRuntimeManager qua Ensure(tileObj),
    /// nên không cần setup trong Scene — giống cách TileDisasterVisualTint hoạt động.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TileDisasterParticles : MonoBehaviour
    {
        [Tooltip("Hệ số nhân kích thước particle so với bề rộng ô (1 = khớp bán kính nội tiếp hex).")]
        [SerializeField] private float sizeMultiplier = 1f;

        [Tooltip("Khoảng nâng tâm particle tính từ MẶT TRÊN thật của khối hex (mét). " +
                 "Mặt trên mỗi tầng địa hình cao khác nhau (Thấp 0.50 / Trung 1.30 / Cao 2.10 " +
                 "ở world Y), nên offset này phải cộng lên độ cao đo được của ô, KHÔNG phải " +
                 "so với gốc/chân khối.")]
        [SerializeField] private float heightOffset = 0.12f;

        [Tooltip("Nếu bật: tự căn lại vị trí/scale particle theo bề rộng ô mỗi khi bật (ô đổi kích thước).")]
        [SerializeField] private bool autoFitOnEnable = true;

        private ParticleSystem smokeSystem;
        private ParticleSystem fireSystem;

        // Prefab truyền vào từ DisasterRuntimeManager (lấy từ DisasterBalance).
        private GameObject smokePrefab;
        private GameObject firePrefab;

        public bool HasSmoke => smokeSystem != null && smokeSystem.gameObject.activeSelf;
        public bool HasFire => fireSystem != null && fireSystem.gameObject.activeSelf;

        /// <summary>
        /// Lấy (hoặc tự gắn) component particle lên một ô. An toàn khi tileObj null.
        /// </summary>
        public static TileDisasterParticles Ensure(GameObject tileObj)
        {
            if (tileObj == null) return null;
            var fx = tileObj.GetComponent<TileDisasterParticles>();
            if (fx == null) fx = tileObj.AddComponent<TileDisasterParticles>();
            return fx;
        }

        /// <summary>
        /// Ghi nhớ prefab nguồn để dùng cho các lần bật sau (đổi khói <-> lửa).
        /// Gọi ngay sau Ensure, trước khi gọi bất kỳ API bật hiệu ứng nào.
        /// </summary>
        public void Configure(GameObject smoke, GameObject fire)
        {
            smokePrefab = smoke;
            firePrefab = fire;
        }

        /// <summary>
        /// CHỈ KHÓI — trạng thái "ô sắp cháy" (đang chờ bén lửa). Báo hiệu trước cho người chơi
        /// biết ô này sắp bốc cháy để kịp dập mưa. Không có lửa.
        /// </summary>
        public void ShowSmokeOnly()
        {
            EnsureSmoke();
            if (smokeSystem != null && !smokeSystem.gameObject.activeSelf)
            {
                smokeSystem.gameObject.SetActive(true);
                smokeSystem.Play(true);
            }
            StopFire();
        }

        /// <summary>
        /// LỬA — trạng thái ô đã bén lửa thật. Khói báo trước được dừng khi lửa xuất hiện.
        /// </summary>
        public void ShowSmokeAndFire()
        {
            if (smokeSystem != null)
            {
                smokeSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                Destroy(smokeSystem.gameObject);
                smokeSystem = null;
            }

            EnsureFire();
            if (fireSystem != null && !fireSystem.gameObject.activeSelf)
            {
                fireSystem.gameObject.SetActive(true);
                fireSystem.Play(true);
            }
        }

        /// <summary>Tắt và hủy sạch mọi particle của ô (khi mưa dập hoặc thiên tai kết thúc).</summary>
        public void ClearAll()
        {
            StopFire();
            if (smokeSystem != null)
            {
                smokeSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                Destroy(smokeSystem.gameObject);
                smokeSystem = null;
            }
        }

        private void StopFire()
        {
            if (fireSystem != null)
            {
                fireSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                Destroy(fireSystem.gameObject);
                fireSystem = null;
            }
        }

        private void EnsureSmoke()
        {
            if (smokeSystem != null) return;
            smokeSystem = SpawnPrefab(smokePrefab, "DisasterSmoke");
        }

        private void EnsureFire()
        {
            if (fireSystem != null) return;
            fireSystem = SpawnPrefab(firePrefab, "DisasterFire");
        }

        private ParticleSystem SpawnPrefab(GameObject prefab, string fallbackName)
        {
            if (prefab == null) return null;

            var go = Instantiate(prefab);
            go.name = fallbackName;

            // Neo vào tâm ô rồi nâng lên trên MẶT TRÊN thật của khối hex.
            // Mỗi tầng địa hình có mặt trên cao khác nhau (Thấp 1.00 / Trung 1.80 / Cao 2.60
            // tính từ gốc khối), nên phải hỏi độ cao thật của ô thay vì dùng hằng số.
            // Particle được sinh dưới transform của ô nên localPosition là hệ toạ độ ô.
            Vector3 localAnchor = new Vector3(0f, ResolveLocalSurfaceY() + heightOffset, 0f);
            go.transform.position = transform.TransformPoint(localAnchor);

            // Trục Y của particle phải thẳng đứng THEO THẾ GIỚI, không được kế thừa độ nghiêng
            // của ô hex cha. Ô hex do HexBiomeClusterConnector sinh ra có nghiêng ngẫu nhiên
            // tới +-3 độ ở trục X/Z (Quaternion.Euler(Random(-3,3), ..., Random(-3,3))), nên nếu
            // để localRotation = identity thì khói/lửa sẽ chúi theo hướng nghiêng của ô.
            // Bù ngược rotation của cha để particle luôn dựng thẳng đứng.
            go.transform.rotation = Quaternion.identity;
            go.transform.SetParent(transform, true);

            if (autoFitOnEnable) ApplyFitScale(go.transform);

            // Tắt collider (nếu prefab lỡ kèm) để không chặn raycast đặt thẻ.
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Clear(true);
                ps.Play(true);
            }
            return ps;
        }

        /// <summary>
        /// Độ cao MẶT TRÊN thật của khối hex, quy về hệ toạ độ cục bộ của ô (mét từ gốc ô).
        /// Dùng HexBiomeClusterConnector.GetTileHeightLevels để đo đúng theo mesh thực tế
        /// (Thấp 1.00 / Trung 1.80 / Cao 2.60). Nếu không đo được, dùng mặc định 1.00.
        /// </summary>
        private float ResolveLocalSurfaceY()
        {
            var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(gameObject);
            float localSurfaceY = topY - transform.position.y;

            // Chắn an toàn: nếu vì lý do nào đó độ cao đo được vô lý (âm hoặc quá lớn),
            // quay về mặt trên của tầng thấp để hiệu ứng không bị chìm dưới đất.
            if (localSurfaceY < 0.2f || localSurfaceY > 10f) localSurfaceY = 1.0f;
            return localSurfaceY;
        }

        /// <summary>
        /// Chỉnh scale particle cho phù hợp bề rộng khối hex hiện tại của ô.
        /// Ưu tiên đo bề rộng thực từ Renderer của ô (nếu có); nếu không thì dùng
        /// bán kính nội tiếp chuẩn của hex (HexMetrics.InnerRadius = 1.0f).
        ///
        /// Lý do nhân theo bề rộng: prefab particle (Smoke/Fire) được tác giả dựng ở
        /// tỉ lệ riêng. Nhân với bán kính nội tiếp giúp hiệu ứng phủ vừa vặn một ô,
        /// không tràn sang ô kề (2 tâm ô cách nhau 2.0f).
        /// </summary>
        private void ApplyFitScale(Transform particleTransform)
        {
            if (particleTransform == null) return;

            float fit = Mathf.Max(0.05f, sizeMultiplier);
            particleTransform.localScale = new Vector3(fit, fit, fit);

            // Giữ vị trí neo đúng trên mặt khối hex mỗi lần căn lại.
            Vector3 localAnchor = new Vector3(0f, ResolveLocalSurfaceY() + heightOffset, 0f);
            particleTransform.position = transform.TransformPoint(localAnchor);

            // Ép thẳng đứng theo thế giới (bù rotation nghiêng của ô hex cha) sau mỗi lần căn lại.
            particleTransform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        }

        private void OnEnable()
        {
            if (autoFitOnEnable)
            {
                if (smokeSystem != null) ApplyFitScale(smokeSystem.transform);
                if (fireSystem != null) ApplyFitScale(fireSystem.transform);
            }
        }
    }
}
