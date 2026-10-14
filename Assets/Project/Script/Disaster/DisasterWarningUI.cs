using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LumiWorld.Acs
{
    /// <summary>
    /// UI cảnh báo thiên tai/vật cản tự sinh vào scene (không cần tạo prefab hay sửa Scene).
    ///
    /// Gồm 3 phần phối hợp với nhau:
    ///  1. BIỂN CẢNH BÁO (banner) ở cạnh trên: khung + icon + tên thiên tai + thời gian + gợi ý cách dập.
    ///     Chỉ hiện khi có thiên tai đang hoạt động.
    ///  2. THẺ CHỈ HƯỚNG (tracker): khi vùng thiên tai ra ngoài khung nhìn camera, banner thu nhỏ
    ///     và trượt ra mép màn hình phía gần vùng đó nhất, kèm MŨI TÊN xoay đúng hướng và
    ///     KHOẢNG CÁCH (số ô) để dẫn người chơi tới. Khi vùng đã vào khung nhìn, thẻ trở lại
    ///     thành banner đầy đủ ở cạnh trên.
    ///  3. VÒNG SÁNG dưới chân mọi ô bị ảnh hưởng để thấy ngay vùng bị phủ.
    ///
    /// Chỉ ĐỌC dữ liệu từ DisasterRuntimeManager và vẽ UI riêng của mình, không đụng
    /// tới material của ô lục giác — việc tô ô do TileDisasterVisualTint lo.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    public sealed class DisasterWarningUI : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private DisasterRuntimeManager manager;
        [SerializeField] private HexWorldGenerator worldGenerator;
        [Tooltip("Camera dùng để chiếu vùng thiên tai lên màn hình. Trống = Camera.main.")]
        [SerializeField] private Camera targetCamera;

        [Header("Banner")]
        [SerializeField] private float bannerPixelHeight = 88f;
        [Tooltip("Khoảng cách thụt xuống so với cạnh trên màn hình.")]
        [SerializeField] private float bannerTopMargin = 8f;
        [Tooltip("Bề ngang banner (px). Thẻ chỉ hướng thu về đúng bề ngang này khi dạt ra mép.")]
        [SerializeField] private float bannerWidth = 860f;

        [Header("Thẻ chỉ hướng (khi vùng thiên tai ngoài khung nhìn)")]
        [Tooltip("Khoảng cách tối thiểu từ mép màn hình (px) để thẻ không bị cắt.")]
        [SerializeField] private float edgePadding = 16f;
        [Tooltip("Tốc độ trượt của thẻ khi dạt ra mép (đơn vị: hệ số làm mượt).")]
        [SerializeField] private float trackerLerpSpeed = 8f;
        [Tooltip("Hiện mũi tên chỉ hướng trên thẻ.")]
        [SerializeField] private bool showDirectionArrow = true;
        [Tooltip("Hiện khoảng cách (số ô) từ tâm màn hình tới vùng thiên tai.")]
        [SerializeField] private bool showDistance = true;

        [Header("Thẻ nổi tại chỗ (dán trên vùng thiên tai)")]
        [Tooltip("Bật để hiện thẻ nhãn NGAY TẠI vị trí đám cháy, không chỉ banner ở cạnh màn hình.")]
        [SerializeField] private bool showWorldTag = true;
        [Tooltip("Độ nâng của thẻ so với tâm ô (mét) để thẻ không đè lên khối hex.")]
        [SerializeField] private float worldTagHeightOffset = 1.6f;
        [SerializeField] private float worldTagWidth = 420f;
        [SerializeField] private float worldTagHeight = 64f;

        [Header("Vòng highlight")]
        [SerializeField] private float highlightRadius = 0.55f;
        [Tooltip("Độ cao vòng so với tâm ô. Tăng nếu vòng bị chìm dưới mặt khối hex.")]
        [SerializeField] private float highlightHeight = 0.08f;
        [Tooltip("Màu nền của vòng. Màu thiên tai được trộn lên trên màu này.")]
        [SerializeField] private Color highlightBaseColor = new Color(1f, 1f, 1f, 0.35f);
        [Tooltip("Bật để luôn vẽ vòng cho mọi ô bị ảnh hưởng (dễ quan sát khi phát triển).")]
        [SerializeField] private bool alwaysShowHighlights = true;

        private Canvas canvas;
        private RectTransform bannerRoot;
        private Image bannerBackground;
        private Image bannerAccentBar;
        private RectTransform bannerIconRoot;
        private Image iconGlyph;
        private Image iconDot;
        private TextMeshProUGUI bannerText;
        private RectTransform arrowRect;
        private Image arrowImage;
        private TextMeshProUGUI distanceText;

        // Thẻ nổi NGAY TẠI CHỖ bị thiên tai (world-space label): người chơi luôn thấy nó dán
        // trên vùng cháy, không cần đoán. Khi vùng đó ra ngoài khung nhìn thì ẩn thẻ này và
        // dùng banner ở mép màn hình để chỉ hướng.
        private RectTransform worldTagRoot;
        private Image worldTagBackground;
        private Image worldTagAccentBar;
        private TextMeshProUGUI worldTagText;

        private Material highlightMaterial;
        private Mesh highlightMesh;

        private readonly List<DisasterInstance> shown = new List<DisasterInstance>();
        private readonly List<Transform> highlightPool = new List<Transform>();

        /// <summary>Vị trí đích của banner (được làm mượt mỗi frame).</summary>
        private Vector2 bannerTargetPosition = Vector2.zero;
        /// <summary>Bề ngang đích của banner: rộng khi ở cạnh trên, thu nhỏ khi dạt ra mép.</summary>
        private float bannerTargetWidth;
        /// <summary>Có đang ở chế độ thẻ chỉ hướng (vùng thiên tai ngoài khung nhìn) hay không.</summary>
        private bool trackingOffscreen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Spawn();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Spawn();

        private static void Spawn()
        {
            if (FindAnyObjectByType<DisasterWarningUI>(FindObjectsInactive.Include) != null) return;
            var runtimeManager = FindAnyObjectByType<DisasterRuntimeManager>();
            if (runtimeManager == null) return; // Manager sẽ tự gọi lại qua AttachWhenReady.

            var go = new GameObject("DisasterWarningUI");
            var ui = go.AddComponent<DisasterWarningUI>();
            ui.manager = runtimeManager;
            ui.Build(runtimeManager);
        }

        /// <summary>Được DisasterRuntimeManager gọi khi nó sẵn sàng, tránh phụ thuộc thứ tự khởi tạo.</summary>
        public static void AttachWhenReady(DisasterRuntimeManager runtimeManager)
        {
            if (runtimeManager == null) return;
            if (FindAnyObjectByType<DisasterWarningUI>(FindObjectsInactive.Include) != null) return;

            var go = new GameObject("DisasterWarningUI");
            var ui = go.AddComponent<DisasterWarningUI>();
            ui.manager = runtimeManager;
            ui.Build(runtimeManager);
        }

        private void Build(DisasterRuntimeManager disasterManager)
        {
            worldGenerator = FindAnyObjectByType<HexWorldGenerator>();
            if (targetCamera == null) targetCamera = Camera.main;

            canvas = FindScreenCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("[LumiWorld Disaster UI] Không tìm thấy Canvas màn hình nên không vẽ được banner.");
                return;
            }

            BuildBanner();
            BuildWorldTag();

            if (disasterManager != null)
            {
                disasterManager.DisasterStarted += OnDisasterChanged;
                disasterManager.DisasterChanged += OnDisasterChanged;
                disasterManager.DisasterEnded += OnDisasterChanged;
            }

            Refresh();
        }

        /// <summary>
        /// Dựng biển cảnh báo: khung nền + thanh màu bên trái + icon + chữ + mũi tên + khoảng cách.
        ///
        /// SỬA LỖI QUAN TRỌNG: bản cũ neo banner ở cạnh trên nhưng đặt anchoredPosition = 0
        /// với pivot.y = 1, nghĩa là đỉnh banner TRÙNG cạnh trên màn hình -> banner thò ra
        /// NGOÀI khung nhìn và người chơi không thấy gì (dù object vẫn có trong Hierarchy).
        /// Nay đẩy xuống dưới cạnh trên đúng bannerTopMargin.
        /// </summary>
        private void BuildBanner()
        {
            bannerRoot = CreateRect("DisasterBanner", canvas.transform);

            // Neo giữa-cạnh-trên, kích thước CỐ ĐỊNH (không giãn hết bề ngang) để còn thu nhỏ
            // và trượt ra mép màn hình khi vùng thiên tai ở ngoài khung nhìn.
            bannerRoot.anchorMin = new Vector2(0.5f, 1f);
            bannerRoot.anchorMax = new Vector2(0.5f, 1f);
            bannerRoot.pivot = new Vector2(0.5f, 1f);
            bannerRoot.sizeDelta = new Vector2(bannerWidth, bannerPixelHeight);
            bannerRoot.anchoredPosition = new Vector2(0f, -bannerTopMargin);

            bannerTargetPosition = bannerRoot.anchoredPosition;
            bannerTargetWidth = bannerWidth;

            bannerBackground = bannerRoot.gameObject.AddComponent<Image>();
            bannerBackground.color = new Color(0.07f, 0.08f, 0.11f, 0.92f);
            bannerBackground.raycastTarget = false;

            // Thanh màu bên trái: dấu hiệu thị giác mạnh nhất cho biết loại thiên tai.
            var accentRect = CreateRect("Accent", bannerRoot);
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.sizeDelta = new Vector2(6f, 0f);
            accentRect.anchoredPosition = Vector2.zero;
            bannerAccentBar = accentRect.gameObject.AddComponent<Image>();
            bannerAccentBar.raycastTarget = false;

            // Icon cảnh báo: TAM GIÁC + dấu chấm than DỰNG BẰNG HÌNH HỌC, không dùng glyph.
            //
            // VÌ SAO: font mặc định LiberationSans SDF chỉ có bộ ký tự ASCII cơ bản. Các glyph
            // như \u26A0 (⚠) không có trong font nên TMP thay bằng \u25A1 (ô vuông) kèm warning
            // "character was not found in the font asset". Vẽ bằng Image thì không phụ thuộc font.
            bannerIconRoot = CreateRect("Icon", bannerRoot);
            bannerIconRoot.anchorMin = new Vector2(0f, 0f);
            bannerIconRoot.anchorMax = new Vector2(0f, 1f);
            bannerIconRoot.pivot = new Vector2(0.5f, 0.5f);
            bannerIconRoot.sizeDelta = new Vector2(28f, 0f);
            bannerIconRoot.anchoredPosition = new Vector2(26f, 0f);
            BuildWarningIcon(bannerIconRoot);

            // Chữ chính.
            bannerText = CreateLabel("Text", bannerRoot, 36f, FontStyles.Normal);
            bannerText.alignment = TextAlignmentOptions.Left;
            var textRect = (RectTransform)bannerText.transform;
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(48f, 0f);
            textRect.offsetMax = new Vector2(-96f, 0f);

            // Khoảng cách (chỉ hiện khi vùng thiên tai ở ngoài khung nhìn).
            distanceText = CreateLabel("Distance", bannerRoot, 30f, FontStyles.Bold);
            distanceText.alignment = TextAlignmentOptions.Right;
            var distRect = (RectTransform)distanceText.transform;
            distRect.anchorMin = new Vector2(1f, 0f);
            distRect.anchorMax = new Vector2(1f, 1f);
            distRect.pivot = new Vector2(1f, 0.5f);
            distRect.sizeDelta = new Vector2(80f, 0f);
            distRect.anchoredPosition = new Vector2(-8f, 0f);
            distanceText.gameObject.SetActive(false);

            // MŨI TÊN chỉ hướng: dựng bằng 2 Image (tam giác + đuôi) rồi xoay quanh trục Z.
            // Không dùng glyph \u27A4: font LiberationSans SDF không có ký tự này.
            arrowRect = CreateRect("Arrow", bannerRoot);
            arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.sizeDelta = new Vector2(26f, 26f);
            arrowRect.anchoredPosition = new Vector2(0f, -bannerPixelHeight * 0.5f - 20f);
            arrowImage = arrowRect.gameObject.AddComponent<Image>();
            arrowImage.raycastTarget = false;
            // Sprite gốc của Unity là hình VUÔNG -> dùng làm đầu mũi tên bằng cách để lại
            // phần "mũi" nhờ xoay 45° (xem BuildArrowShape).
            BuildArrowShape(arrowRect);
            arrowRect.gameObject.SetActive(false);

            ValidateTextSetup();

            bannerRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// Vẽ icon cảnh báo bằng HÌNH HỌC: một ô vuông xoay 45° (thành hình thoi ~ tam giác cảnh báo)
        /// + dấu chấm than nhỏ bằng 2 Image.
        ///
        /// VÌ SAO KHÔNG DÙNG GLYPH: font mặc định LiberationSans SDF chỉ có ASCII cơ bản, các ký tự
        /// như \u26A0 (⚠) / \u27A4 (➤) bị thay bằng \u25A1 (ô vuông) kèm warning
        /// "character was not found in the font asset". Vẽ bằng Image thì không phụ thuộc font.
        /// </summary>
        private void BuildWarningIcon(RectTransform parent)
        {
            // Khung bao (hình thoi xoay 45° -> nhìn như tam giác cảnh báo).
            var diamond = CreateRect("GlyphBg", parent);
            diamond.anchorMin = new Vector2(0.5f, 0.5f);
            diamond.anchorMax = new Vector2(0.5f, 0.5f);
            diamond.pivot = new Vector2(0.5f, 0.5f);
            diamond.sizeDelta = new Vector2(15f, 15f);
            diamond.anchoredPosition = new Vector2(0f, 1f);
            diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
            iconGlyph = diamond.gameObject.AddComponent<Image>();
            iconGlyph.raycastTarget = false;

            // Dấu chấm than: thân trên + chấm dưới.
            var stem = CreateRect("Stem", parent);
            stem.anchorMin = new Vector2(0.5f, 0.5f);
            stem.anchorMax = new Vector2(0.5f, 0.5f);
            stem.pivot = new Vector2(0.5f, 0.5f);
            stem.sizeDelta = new Vector2(2.5f, 8f);
            stem.anchoredPosition = new Vector2(0f, 2f);
            iconDot = stem.gameObject.AddComponent<Image>();
            iconDot.raycastTarget = false;

            var dot = CreateRect("Dot", parent);
            dot.anchorMin = new Vector2(0.5f, 0.5f);
            dot.anchorMax = new Vector2(0.5f, 0.5f);
            dot.pivot = new Vector2(0.5f, 0.5f);
            dot.sizeDelta = new Vector2(2.5f, 2.5f);
            dot.anchoredPosition = new Vector2(0f, -4f);
            dot.gameObject.AddComponent<Image>().raycastTarget = false;
        }

        /// <summary>Mũi tên = một tam giác (ô vuông xoay 45°, cắt còn nửa trên) + đuôi chữ nhật.</summary>
        private void BuildArrowShape(RectTransform parent)
        {
            var head = CreateRect("Head", parent);
            head.anchorMin = new Vector2(0.5f, 0.5f);
            head.anchorMax = new Vector2(0.5f, 0.5f);
            head.pivot = new Vector2(0.5f, 0.5f);
            head.sizeDelta = new Vector2(16f, 16f);
            head.anchoredPosition = new Vector2(6f, 0f);
            head.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var headImage = head.gameObject.AddComponent<Image>();
            headImage.raycastTarget = false;
            headImage.color = Color.white;

            var tail = CreateRect("Tail", parent);
            tail.anchorMin = new Vector2(0.5f, 0.5f);
            tail.anchorMax = new Vector2(0.5f, 0.5f);
            tail.pivot = new Vector2(0.5f, 0.5f);
            tail.sizeDelta = new Vector2(14f, 5f);
            tail.anchoredPosition = new Vector2(-4f, 0f);
            var tailImage = tail.gameObject.AddComponent<Image>();
            tailImage.raycastTarget = false;
            tailImage.color = Color.white;
        }

        /// <summary>Tạo TextMeshProUGUI đã cấu hình sẵn, gắn vào parent.</summary>
        private static TextMeshProUGUI CreateLabel(string name, Transform parent, float fontSize, FontStyles style)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);

            var label = obj.AddComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = Color.white;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        /// <summary>
        /// Thẻ nổi HIỆN NGAY TẠI CHỖ bị thiên tai (theo kiểu "world-space label" của game chiến thuật):
        /// mỗi frame thẻ được chiếu từ toạ độ 3D của vùng cháy lên màn hình rồi đặt đúng vị trí đó.
        ///
        /// Nhờ vậy người chơi KHÔNG phải đoán: thẻ luôn dán trên đám cháy, đi theo khi kéo camera.
        /// Khi vùng đó ra ngoài khung nhìn, thẻ này ẩn đi và nhường chỗ cho banner ở mép màn hình
        /// (kèm mũi tên + khoảng cách) để chỉ hướng.
        /// </summary>
        private void BuildWorldTag()
        {
            worldTagRoot = CreateRect("DisasterWorldTag", canvas.transform);
            worldTagRoot.anchorMin = new Vector2(0.5f, 0.5f);
            worldTagRoot.anchorMax = new Vector2(0.5f, 0.5f);
            worldTagRoot.pivot = new Vector2(0.5f, 0f); // móc ở đáy -> thẻ nằm TRÊN khối hex
            worldTagRoot.sizeDelta = new Vector2(210f, 34f);

            worldTagBackground = worldTagRoot.gameObject.AddComponent<Image>();
            worldTagBackground.raycastTarget = false;

            var accent = CreateRect("Accent", worldTagRoot);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.sizeDelta = new Vector2(4f, 0f);
            accent.anchoredPosition = Vector2.zero;
            worldTagAccentBar = accent.gameObject.AddComponent<Image>();
            worldTagAccentBar.raycastTarget = false;

            worldTagText = CreateLabel("Text", worldTagRoot, 28f, FontStyles.Bold);
            worldTagText.alignment = TextAlignmentOptions.Center;
            var textRect = (RectTransform)worldTagText.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 0f);
            textRect.offsetMax = new Vector2(-8f, 0f);

            worldTagRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// Đặt thẻ nổi tại toạ độ màn hình của tâm vùng thiên tai. Trả về false nếu vùng đó
        /// không nằm trong khung nhìn (lúc đó thẻ phải ẩn và dùng banner ở mép).
        /// </summary>
        private bool UpdateWorldTag(DisasterInstance top, Vector3 worldCenter)
        {
            if (worldTagRoot == null) return false;
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return false;

            Vector3 viewport = targetCamera.WorldToViewportPoint(worldCenter);
            bool visible = viewport.z > 0f &&
                viewport.x >= 0f && viewport.x <= 1f &&
                viewport.y >= 0f && viewport.y <= 1f;

            worldTagRoot.gameObject.SetActive(visible);
            if (!visible) return false;

            // Nhích lên trên vùng cháy một chút để thẻ không đè lên khối hex.
            Vector3 tagWorld = worldCenter + Vector3.up * worldTagHeightOffset;
            Vector3 tagViewport = targetCamera.WorldToViewportPoint(tagWorld);

            var canvasRect = (RectTransform)canvas.transform;
            worldTagRoot.anchoredPosition = new Vector2(
                (tagViewport.x - 0.5f) * canvasRect.rect.width,
                (tagViewport.y - 0.5f) * canvasRect.rect.height);

            worldTagRoot.sizeDelta = new Vector2(worldTagWidth, worldTagHeight);

            Color accent = DisasterCatalog.AlertColor(top.kind);
            worldTagBackground.color = new Color(0.07f, 0.08f, 0.11f, 0.92f);
            worldTagAccentBar.color = accent;
            worldTagText.color = Color.white;

            int remaining = Mathf.CeilToInt(Mathf.Max(0f, (float)top.remainingSeconds));
            worldTagText.text = $"{DisasterCatalog.DisplayName(top.kind)} · {remaining}s";
            return true;
        }

        /// <summary>
        /// Cảnh báo sớm khi thiếu TMP Essential Resources — đây là lỗi IM LẶNG: banner và chữ
        /// vẫn được tạo trong Hierarchy nhưng font null nên KHÔNG render ra gì trên màn hình.
        /// </summary>
        private void ValidateTextSetup()
        {
            if (bannerText == null || bannerText.font != null) return;

            Debug.LogError(
                "[LumiWorld Disaster UI] TextMeshPro CHƯA có font asset nên biển cảnh báo sẽ " +
                "KHÔNG hiện chữ. Mở Window > TextMeshPro > Import TMP Essential Resources.", this);
        }

        private static Canvas FindScreenCanvas()
        {
            // Ưu tiên Canvas Overlay: toạ độ UI trùng thẳng hệ màn hình, công thức trượt ra mép
            // màn hình ở UpdateTracker luôn đúng. Canvas ScreenSpaceCamera vẫn dùng được nhưng
            // Banner phải cùng "không gian" với camera đó — nên chỉ lấy khi không có Overlay.
            Canvas fallback = null;
            foreach (var found in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (!found.isRootCanvas || found.renderMode == RenderMode.WorldSpace) continue;
                if (found.renderMode == RenderMode.ScreenSpaceOverlay) return found;
                if (fallback == null) fallback = found;
            }
            if (fallback != null) return fallback;

            var go = new GameObject("DisasterUICanvas", typeof(RectTransform));
            var own = go.AddComponent<Canvas>();
            own.renderMode = RenderMode.ScreenSpaceOverlay;
            own.sortingOrder = 60;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            return own;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private void OnDisasterChanged(DisasterInstance instance) => Refresh();

        private void Update()
        {
            Refresh();
            AnimateBanner();
            SyncHighlights();
        }

        /// <summary>
        /// Làm mượt việc banner trượt ra mép màn hình / quay về cạnh trên.
        ///
        /// Tách khỏi Refresh(): Refresh() chỉ TÍNH đích (bannerTargetPosition/Width),
        /// còn ở đây mới nội suy từng frame — nhờ vậy thẻ "bay" mượt theo camera thay vì
        /// giật cục mỗi khi vùng thiên tai ra/vào khung nhìn.
        /// Dùng unscaledDeltaTime để không phụ thuộc Time.timeScale (game có thể pause).
        /// </summary>
        private void AnimateBanner()
        {
            if (bannerRoot == null || !bannerRoot.gameObject.activeSelf) return;

            float t = 1f - Mathf.Exp(-trackerLerpSpeed * Time.unscaledDeltaTime);
            bannerRoot.anchoredPosition = Vector2.Lerp(bannerRoot.anchoredPosition, bannerTargetPosition, t);

            Vector2 size = bannerRoot.sizeDelta;
            size.x = Mathf.Lerp(size.x, bannerTargetWidth, t);
            bannerRoot.sizeDelta = size;

            // Khi đang là thẻ chỉ hướng, mũi tên nằm sát dưới thẻ để không che chữ.
            if (arrowRect != null && arrowRect.gameObject.activeSelf)
            {
                float y = trackingOffscreen
                    ? -bannerPixelHeight * 0.5f - 6f
                    : -bannerPixelHeight * 0.5f - 20f;
                arrowRect.anchoredPosition = Vector2.Lerp(arrowRect.anchoredPosition, new Vector2(0f, y), t);
            }
        }

        // ------------------------------------------------------------------
        // Banner
        // ------------------------------------------------------------------

        private void Refresh()
        {
            if (bannerText == null) return;

            shown.Clear();
            if (manager == null) manager = FindAnyObjectByType<DisasterRuntimeManager>();
            if (manager != null) shown.AddRange(manager.ActiveDisasters);

            if (shown.Count == 0)
            {
                bannerRoot.gameObject.SetActive(false);
                arrowRect.gameObject.SetActive(false);
                distanceText.gameObject.SetActive(false);
                if (worldTagRoot != null) worldTagRoot.gameObject.SetActive(false);
                trackingOffscreen = false;
                return;
            }

            // Luôn cảnh báo thiên tai ưu tiên cao nhất (số enum nhỏ).
            DisasterInstance top = shown[0];
            for (int i = 1; i < shown.Count; i++)
            {
                if ((int)shown[i].kind < (int)top.kind) top = shown[i];
            }

            bannerRoot.gameObject.SetActive(true);

            Color accent = DisasterCatalog.AlertColor(top.kind);

            // Nền tối cố định + thanh màu accent bên trái: nhìn ra "biển cảnh báo" rõ hơn
            // là tô cả nền bằng màu nhạt (chữ dễ bị chìm, khó đọc).
            if (bannerBackground != null)
                bannerBackground.color = new Color(0.07f, 0.08f, 0.11f, 0.92f);
            if (bannerAccentBar != null)
                bannerAccentBar.color = accent;
            if (iconGlyph != null)
                iconGlyph.color = accent;
            if (iconDot != null)
                iconDot.color = accent;

            int remaining = Mathf.CeilToInt(Mathf.Max(0f, (float)top.remainingSeconds));
            bannerText.color = Color.white;
            bannerText.text = $"{DisasterCatalog.DisplayName(top.kind)}   ·   còn {remaining}s" +
                (shown.Count > 1 ? $"   (+{shown.Count - 1})" : string.Empty) +
                $"\n{AdviceFor(top.kind)}";

            UpdateTracker(top);
        }

        // ------------------------------------------------------------------
        // Thẻ chỉ hướng: dạt ra mép màn hình khi vùng thiên tai ngoài khung nhìn
        // ------------------------------------------------------------------

        /// <summary>
        /// Điều phối 2 chế độ hiển thị:
        ///
        ///  A. Vùng thiên tai TRONG khung nhìn -> hiện THẺ NỔI dán ngay tại chỗ đám cháy
        ///     (người chơi thấy trực tiếp, không phải đoán), banner thu về cạnh trên làm tóm tắt.
        ///
        ///  B. Vùng thiên tai NGOÀI khung nhìn -> ẩn thẻ nổi, banner dạt ra mép màn hình phía
        ///     gần vùng đó nhất, kèm mũi tên chỉ hướng + khoảng cách ("còn N ô").
        /// </summary>
        private void UpdateTracker(DisasterInstance top)
        {
            if (bannerRoot == null) return;
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null || worldGenerator == null) return;

            // Tâm vùng thiên tai (trung bình các ô đang bị ảnh hưởng) -> ổn định hơn lấy 1 ô.
            if (!TryGetAffectedCenter(top, out Vector3 worldCenter, out int tileCount))
            {
                SetBannerDockedAtTop();
                arrowRect.gameObject.SetActive(false);
                distanceText.gameObject.SetActive(false);
                if (worldTagRoot != null) worldTagRoot.gameObject.SetActive(false);
                return;
            }

            // Thẻ nổi hiện ngay tại chỗ khi vùng cháy nằm trong tầm nhìn.
            bool tagVisible = showWorldTag && UpdateWorldTag(top, worldCenter);

            Vector3 viewport = targetCamera.WorldToViewportPoint(worldCenter);
            bool inFrontOfCamera = viewport.z > 0f;
            bool onScreen = inFrontOfCamera &&
                viewport.x >= 0f && viewport.x <= 1f &&
                viewport.y >= 0f && viewport.y <= 1f;

            if (onScreen)
            {
                // Vùng thiên tai đã trong tầm mắt -> banner làm tóm tắt ở cạnh trên,
                // không cần mũi tên (thẻ nổi đã chỉ đúng chỗ).
                SetBannerDockedAtTop();
                arrowRect.gameObject.SetActive(false);
                distanceText.gameObject.SetActive(false);
                return;
            }

            trackingOffscreen = true;

            // Ra ngoài khung nhìn -> thu hồi thẻ nổi (UpdateWorldTag đã tự ẩn),
            // rồi đưa banner ra mép màn hình.
            if (tagVisible && worldTagRoot != null) worldTagRoot.gameObject.SetActive(false);

            // Kẹp vị trí lên mép màn hình.
            float cx = Mathf.Clamp(viewport.x, 0f, 1f);
            float cy = Mathf.Clamp(viewport.y, 0f, 1f);

            // Camera ở sau lưng vùng thiên tai: viewport lật dấu -> đẩy về đúng phía ngược lại.
            if (!inFrontOfCamera)
            {
                cx = viewport.x < 0.5f ? 1f : 0f;
                cy = 0f;
            }

            // Đổi sang toạ độ trong Canvas (rect của Canvas, không phải pixel màn hình —
            // nhờ vậy chạy đúng cả khi CanvasScaler dùng scale khác 1).
            var canvasRect = (RectTransform)canvas.transform;
            var edgeLocal = new Vector2(
                (cx - 0.5f) * canvasRect.rect.width,
                (cy - 0.5f) * canvasRect.rect.height);

            // neo banner ở TÂM canvas để công thức trượt ra mép luôn đúng mọi tỉ lệ màn hình.
            bannerRoot.anchorMin = new Vector2(0.5f, 0.5f);
            bannerRoot.anchorMax = new Vector2(0.5f, 0.5f);
            bannerRoot.pivot = new Vector2(0.5f, 0.5f);

            float halfW = bannerWidth * 0.5f;
            float halfH = bannerPixelHeight * 0.5f;
            float minX = -canvasRect.rect.width * 0.5f + halfW + edgePadding;
            float maxX = canvasRect.rect.width * 0.5f - halfW - edgePadding;
            float minY = -canvasRect.rect.height * 0.5f + halfH + edgePadding;
            float maxY = canvasRect.rect.height * 0.5f - halfH - edgePadding;

            bannerTargetPosition = new Vector2(
                Mathf.Clamp(edgeLocal.x, minX, maxX),
                Mathf.Clamp(edgeLocal.y, minY, maxY));

            // Thu nhỏ thẻ khi dạt ra mép cho gọn.
            bannerTargetWidth = Mathf.Min(bannerWidth, 330f);

            // Khoảng cách từ tâm vùng thiên tai tới tâm màn hình, quy đổi ra số ô hex.
            if (showDistance)
            {
                var screenCenterWorld = targetCamera.ViewportToWorldPoint(
                    new Vector3(0.5f, 0.5f, targetCamera.nearClipPlane));
                int hexDistance = HexCoordinates.Distance(
                    HexMetrics.WorldToHex(worldCenter),
                    HexMetrics.WorldToHex(screenCenterWorld));

                distanceText.gameObject.SetActive(true);
                distanceText.color = accentForTracker(top);
                distanceText.text = hexDistance <= 0 ? "tại đây" : $"{hexDistance} ô";
            }
            else
            {
                distanceText.gameObject.SetActive(false);
            }

            // Mũi tên chỉ hướng: quay về phía vùng thiên tai so với TÂM MÀN HÌNH.
            if (showDirectionArrow)
            {
                arrowRect.gameObject.SetActive(true);
                arrowImage.color = accentForTracker(top);
                // Đầu mũi tên và đuôi là con của arrowRect nên cùng đổi màu theo.
                for (int i = 0; i < arrowRect.childCount; i++)
                {
                    var img = arrowRect.GetChild(i).GetComponent<Image>();
                    if (img != null) img.color = accentForTracker(top);
                }

                var centerWorld = targetCamera.ViewportToWorldPoint(
                    new Vector3(0.5f, 0.5f, targetCamera.nearClipPlane));
                Vector3 dir = worldCenter - centerWorld;
                dir.y = 0f; // bản đồ nằm trên mặt phẳng XZ
                // atan2(x, z) cho góc quanh trục Y; màn hình Overlay dùng trục Z -> trừ 90°.
                float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                arrowRect.localRotation = Quaternion.Euler(0f, 0f, 90f - angle);
            }
            else
            {
                arrowRect.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Đưa banner về trạng thái "neo ở cạnh trên, bề ngang đầy đủ".
        ///
        /// BẮT BUỘC phải gọi hàm này (thay vì tự set bannerTargetPosition) vì UpdateTracker
        /// ở chế độ chỉ hướng đã ĐỔI anchor/pivot của banner sang tâm canvas (0.5, 0.5).
        /// Nếu không đổi lại về (0.5, 1), vị trí sẽ bị hiểu sai hệ quy chiếu -> banner nhảy
        /// lơ lửng giữa màn hình (đúng hiện tượng đã gặp trong ảnh).
        /// </summary>
        private void SetBannerDockedAtTop()
        {
            trackingOffscreen = false;
            bannerRoot.anchorMin = new Vector2(0.5f, 1f);
            bannerRoot.anchorMax = new Vector2(0.5f, 1f);
            bannerRoot.pivot = new Vector2(0.5f, 1f);
            bannerTargetPosition = new Vector2(0f, -bannerTopMargin);
            bannerTargetWidth = bannerWidth;
        }

        /// <summary>Tính tâm (trung bình cộng) các ô đang bị thiên tai phủ.</summary>
        private bool TryGetAffectedCenter(DisasterInstance instance, out Vector3 center, out int tileCount)
        {
            center = Vector3.zero;
            tileCount = 0;
            if (instance == null || manager == null) return false;

            var tiles = manager.GetAffectedTiles(instance);
            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                if (tile == null) continue;
                center += tile.transform.position;
                tileCount++;
            }

            if (tileCount == 0) return false;
            center /= tileCount;
            return true;
        }

        private static Color accentForTracker(DisasterInstance instance)
        {
            Color accent = DisasterCatalog.AlertColor(instance != null ? instance.kind : DisasterKind.Obstacle);
            accent.a = 1f;
            return accent;
        }

        private static string AdviceFor(DisasterKind kind)
        {
            switch (kind)
            {
                case DisasterKind.ForestFire: return "Dùng thẻ MƯA dập lửa để cứu ô trước khi nó lan rộng hơn";
                case DisasterKind.Cyclone: return "";
                case DisasterKind.Drought: return "Dùng mưa để chấm dứt hạn hán";
                case DisasterKind.BarrenLand: return "Cần MƯA LỚN (mưa nhỏ không thấm)";
                case DisasterKind.Obstacle: return "Vật cản: dọn hoặc dùng mưa";
                default: return string.Empty;
            }
        }

        // ------------------------------------------------------------------
        // Vòng highlight ô bị ảnh hưởng
        // ------------------------------------------------------------------

        private void SyncHighlights()
        {
            if (manager == null) return;
            if (worldGenerator == null) worldGenerator = FindAnyObjectByType<HexWorldGenerator>();
            if (worldGenerator == null) return;

            int used = 0;

            foreach (var instance in manager.ActiveDisasters)
            {
                if (instance == null) continue;
                if (!alwaysShowHighlights &&
                    Time.unscaledTime - instance.startedAtUnscaledTime > 3f) continue;

                var tiles = manager.GetAffectedTiles(instance);
                foreach (var tile in tiles)
                {
                    if (tile == null) continue;

                    Transform marker = GetHighlight(used++);
                    marker.gameObject.SetActive(true);
                    marker.position = new Vector3(tile.transform.position.x,
                        tile.transform.position.y + highlightHeight,
                        tile.transform.position.z);
                    marker.localScale = Vector3.one * (highlightRadius * 2f);

                    var renderer = marker.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        Color accent = DisasterCatalog.AccentColor(instance.kind);
                        accent.a = 1f;
                        Color color = Color.Lerp(highlightBaseColor, accent, 0.75f);
                        color.a = Mathf.Max(0.6f, highlightBaseColor.a);
                        renderer.material.color = color;
                    }
                }
            }

            for (int i = used; i < highlightPool.Count; i++)
                highlightPool[i].gameObject.SetActive(false);
        }

        private Transform GetHighlight(int index)
        {
            while (highlightPool.Count <= index)
            {
                var go = new GameObject("DisasterHighlight", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);

                go.GetComponent<MeshFilter>().sharedMesh = GetHighlightMesh();

                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = GetHighlightMaterial();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.allowOcclusionWhenDynamic = false;

                highlightPool.Add(go.transform);
            }
            return highlightPool[index];
        }

        private Mesh GetHighlightMesh()
        {
            if (highlightMesh != null) return highlightMesh;

            const int segments = 24;
            highlightMesh = new Mesh { name = "DisasterHighlightDisc" };

            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;

            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f);
            }
            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }

            highlightMesh.vertices = vertices;
            highlightMesh.triangles = triangles;
            highlightMesh.RecalculateNormals();
            highlightMesh.RecalculateBounds();
            return highlightMesh;
        }

        private Material GetHighlightMaterial()
        {
            if (highlightMaterial != null) return highlightMaterial;

            // Material RIÊNG của UI này (không phải material của ô hex) nên được phép tạo và set màu.
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            highlightMaterial = new Material(shader) { name = "DisasterHighlightMat" };
            highlightMaterial.renderQueue = 3000; // Vẽ sau mặt đất để không bị khối hex che.
            return highlightMaterial;
        }

        private void OnDestroy()
        {
            if (manager == null) return;
            manager.DisasterStarted -= OnDisasterChanged;
            manager.DisasterChanged -= OnDisasterChanged;
            manager.DisasterEnded -= OnDisasterChanged;
        }
    }
}
