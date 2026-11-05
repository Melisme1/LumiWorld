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

        [Header("Thẻ nổi tại chỗ (dán trên vùng thiên tai)")]
        [Tooltip("Độ nâng của thẻ so với tâm ô (mét) để thẻ không đè lên khối hex.")]
        [SerializeField] private float worldTagHeightOffset = 1.6f;
        [SerializeField] private float worldTagWidth = 300f;
        [SerializeField] private float worldTagHeight = 64f;

        [Header("Chỉ hướng khi nằm ngoài camera")]
        [SerializeField] private float edgePadding = 16f;
        [SerializeField] private float trackerLerpSpeed = 8f;
        [SerializeField] private bool showDirectionArrow = true;
        [SerializeField] private bool showDistance = true;

        [Header("Vòng highlight")]
        [SerializeField] private float highlightRadius = 0.55f;
        [Tooltip("Độ cao vòng so với tâm ô. Tăng nếu vòng bị chìm dưới mặt khối hex.")]
        [SerializeField] private float highlightHeight = 0.08f;
        [Tooltip("Màu nền của vòng. Màu thiên tai được trộn lên trên màu này.")]
        [SerializeField] private Color highlightBaseColor = new Color(1f, 1f, 1f, 0.35f);
        [Tooltip("Bật để luôn vẽ vòng cho mọi ô bị ảnh hưởng (dễ quan sát khi phát triển).")]
        [SerializeField] private bool alwaysShowHighlights = true;

        private Canvas canvas;

        // Thẻ nổi NGAY TẠI CHỖ bị thiên tai (world-space label): người chơi luôn thấy nó dán
        // trên vùng cháy, không cần đoán. Khi vùng đó ra ngoài khung nhìn thì ẩn thẻ này và
        // dùng banner ở mép màn hình để chỉ hướng.
        private RectTransform worldTagRoot;
        private Image worldTagBackground;
        private Image worldTagAccentBar;
        private TextMeshProUGUI worldTagText;
        private RectTransform arrowRoot;
        private TextMeshProUGUI distanceText;
        private Vector2 tagTargetPosition;
        private float tagTargetWidth;

        private Material highlightMaterial;
        private Mesh highlightMesh;



        private readonly List<DisasterInstance> shown = new List<DisasterInstance>();
        private readonly List<GameObject> tileBuffer = new List<GameObject>();
        private readonly List<Transform> highlightPool = new List<Transform>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Spawn();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var runtimeManager = FindAnyObjectByType<DisasterRuntimeManager>();
            if (runtimeManager == null)
            {
                // A warning belongs to the world scene. Never leave it visible on World Map/menu scenes.
                var stale = FindAnyObjectByType<DisasterWarningUI>(FindObjectsInactive.Include);
                if (stale != null) Destroy(stale.gameObject);
                return;
            }
            Spawn();
        }

        private static void Spawn()
        {
            var runtimeManager = FindAnyObjectByType<DisasterRuntimeManager>();
            if (runtimeManager == null) return; // Manager sẽ tự gọi lại qua AttachWhenReady.

            var existing = FindAnyObjectByType<DisasterWarningUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (existing.manager == runtimeManager) return;
                existing.gameObject.SetActive(false);
                Destroy(existing.gameObject);
            }

            var go = new GameObject("DisasterWarningUI");
            var ui = go.AddComponent<DisasterWarningUI>();
            ui.manager = runtimeManager;
            ui.Build(runtimeManager);
        }

        /// <summary>Được DisasterRuntimeManager gọi khi nó sẵn sàng, tránh phụ thuộc thứ tự khởi tạo.</summary>
        public static void AttachWhenReady(DisasterRuntimeManager runtimeManager)
        {
            if (runtimeManager == null) return;
            var existing = FindAnyObjectByType<DisasterWarningUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (existing.manager == runtimeManager) return;
                existing.gameObject.SetActive(false);
                Destroy(existing.gameObject);
            }

            var go = new GameObject("DisasterWarningUI");
            var ui = go.AddComponent<DisasterWarningUI>();
            ui.manager = runtimeManager;
            ui.Build(runtimeManager);
        }

        private void Build(DisasterRuntimeManager disasterManager)
        {
            manager = disasterManager;
            worldGenerator = FindAnyObjectByType<HexWorldGenerator>();
            if (targetCamera == null) targetCamera = Camera.main;

            canvas = FindScreenCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("[LumiWorld Disaster UI] Không tìm thấy Canvas màn hình nên không vẽ được banner.");
                return;
            }

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
        private void BuildWorldTag()
        {
            worldTagRoot = CreateRect("DisasterWarningTag", canvas.transform);
            worldTagRoot.anchorMin = worldTagRoot.anchorMax = new Vector2(0.5f, 0.5f);
            worldTagRoot.pivot = new Vector2(0.5f, 0.5f);
            worldTagRoot.sizeDelta = new Vector2(worldTagWidth, worldTagHeight);
            worldTagBackground = worldTagRoot.gameObject.AddComponent<Image>();
            worldTagBackground.color = new Color(0.07f, 0.08f, 0.11f, 0.92f);
            worldTagBackground.raycastTarget = false;

            var accent = CreateRect("Accent", worldTagRoot);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.sizeDelta = new Vector2(4f, 0f);
            worldTagAccentBar = accent.gameObject.AddComponent<Image>();
            worldTagAccentBar.raycastTarget = false;

            worldTagText = CreateLabel("Text", worldTagRoot, 28f, FontStyles.Bold);
            worldTagText.alignment = TextAlignmentOptions.Center;
            var textRect = (RectTransform)worldTagText.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 0f);
            textRect.offsetMax = new Vector2(-8f, 0f);

            arrowRoot = CreateRect("DirectionArrow", worldTagRoot);
            arrowRoot.anchorMin = arrowRoot.anchorMax = new Vector2(0.5f, 0.5f);
            arrowRoot.pivot = new Vector2(0.5f, 0.5f);
            arrowRoot.sizeDelta = new Vector2(28f, 28f);
            BuildArrowShape(arrowRoot);
            arrowRoot.gameObject.SetActive(false);

            distanceText = CreateLabel("DisasterDistance", worldTagRoot, 22f, FontStyles.Bold);
            distanceText.alignment = TextAlignmentOptions.Center;
            distanceText.rectTransform.anchorMin = distanceText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            distanceText.rectTransform.sizeDelta = new Vector2(100f, 28f);
            distanceText.gameObject.SetActive(false);
            worldTagRoot.gameObject.SetActive(false);
        }

        private void BuildArrowShape(RectTransform parent)
        {
            var head = CreateRect("Head", parent);
            head.anchorMin = head.anchorMax = new Vector2(0.5f, 0.5f);
            head.sizeDelta = new Vector2(14f, 14f);
            head.anchoredPosition = new Vector2(0f, 5f);
            head.localRotation = Quaternion.Euler(0f, 0f, 45f);
            head.gameObject.AddComponent<Image>().raycastTarget = false;

            var tail = CreateRect("Tail", parent);
            tail.anchorMin = tail.anchorMax = new Vector2(0.5f, 0.5f);
            tail.sizeDelta = new Vector2(5f, 14f);
            tail.anchoredPosition = new Vector2(0f, -4f);
            tail.gameObject.AddComponent<Image>().raycastTarget = false;
        }

        private bool UpdateWorldTag(DisasterInstance top, Vector3 worldCenter)
        {
            if (worldTagRoot == null) return false;
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return false;

            Vector3 viewport = targetCamera.WorldToViewportPoint(worldCenter);
            bool onScreen = viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f &&
                            viewport.y >= 0f && viewport.y <= 1f;
            RectTransform canvasRect = (RectTransform)canvas.transform;
            Color accent = DisasterCatalog.AlertColor(top.kind);
            worldTagBackground.color = new Color(0.07f, 0.08f, 0.11f, 0.92f);
            worldTagAccentBar.color = accent;
            worldTagText.color = Color.white;
            worldTagText.text = $"{DisasterCatalog.DisplayName(top.kind)} · {Mathf.CeilToInt(Mathf.Max(0f, (float)top.remainingSeconds))}s";

            float halfW = Mathf.Min(worldTagWidth, canvasRect.rect.width - edgePadding * 2f) * 0.5f;
            float halfH = worldTagHeight * 0.5f;
            Vector2 direction;
            if (onScreen)
            {
                Vector3 tagViewport = targetCamera.WorldToViewportPoint(worldCenter + Vector3.up * worldTagHeightOffset);
                tagTargetPosition = new Vector2((tagViewport.x - 0.5f) * canvasRect.rect.width,
                                                (tagViewport.y - 0.5f) * canvasRect.rect.height);
                tagTargetWidth = worldTagWidth;
                direction = Vector2.zero;
                arrowRoot.gameObject.SetActive(false);
                distanceText.gameObject.SetActive(false);
            }
            else
            {
                float x = viewport.x - 0.5f;
                float y = viewport.y - 0.5f;
                if (viewport.z <= 0f) { x = -x; y = -y; }
                direction = new Vector2(x, y);
                if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
                direction.Normalize();

                // Distance from center to the safe screen edge, accounting for the tag size.
                // Using halfW/halfH here would place the target near the center of the screen.
                float safeHalfWidth = Mathf.Max(0f, canvasRect.rect.width * 0.5f - halfW - edgePadding);
                float safeHalfHeight = Mathf.Max(0f, canvasRect.rect.height * 0.5f - halfH - edgePadding);
                float tx = Mathf.Abs(direction.x) < 0.0001f ? float.PositiveInfinity : safeHalfWidth / Mathf.Abs(direction.x);
                float ty = Mathf.Abs(direction.y) < 0.0001f ? float.PositiveInfinity : safeHalfHeight / Mathf.Abs(direction.y);
                float scale = Mathf.Min(tx, ty);
                tagTargetPosition = direction * scale;
                tagTargetPosition.x = Mathf.Clamp(tagTargetPosition.x, -canvasRect.rect.width * 0.5f + halfW + edgePadding,
                    canvasRect.rect.width * 0.5f - halfW - edgePadding);
                tagTargetPosition.y = Mathf.Clamp(tagTargetPosition.y, -canvasRect.rect.height * 0.5f + halfH + edgePadding,
                    canvasRect.rect.height * 0.5f - halfH - edgePadding);
                tagTargetWidth = worldTagWidth;

                arrowRoot.gameObject.SetActive(showDirectionArrow);
                arrowRoot.anchoredPosition = direction * (halfH + 18f);
                arrowRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
                foreach (Transform child in arrowRoot)
                    if (child.TryGetComponent(out Image image)) image.color = accent;

                if (showDistance)
                {
                    Vector3 centerWorld = targetCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, targetCamera.nearClipPlane));
                    int hexDistance = HexCoordinates.Distance(HexMetrics.WorldToHex(worldCenter), HexMetrics.WorldToHex(centerWorld));
                    distanceText.text = $"{hexDistance} ô";
                    distanceText.color = accent;
                    distanceText.rectTransform.anchoredPosition = -direction * (halfH + 18f);
                    distanceText.gameObject.SetActive(true);
                }
                else distanceText.gameObject.SetActive(false);
            }

            if (!worldTagRoot.gameObject.activeSelf)
            {
                worldTagRoot.anchoredPosition = tagTargetPosition;
                Vector2 initialSize = worldTagRoot.sizeDelta;
                initialSize.x = tagTargetWidth;
                worldTagRoot.sizeDelta = initialSize;
            }
            worldTagRoot.gameObject.SetActive(true);
            return true;
        }

        private void AnimateTag()
        {
            if (worldTagRoot == null || !worldTagRoot.gameObject.activeSelf) return;
            float t = 1f - Mathf.Exp(-trackerLerpSpeed * Time.unscaledDeltaTime);
            worldTagRoot.anchoredPosition = Vector2.Lerp(worldTagRoot.anchoredPosition, tagTargetPosition, t);
            Vector2 size = worldTagRoot.sizeDelta;
            size.x = Mathf.Lerp(size.x, tagTargetWidth, t);
            worldTagRoot.sizeDelta = size;
        }

        private static Canvas FindScreenCanvas()
        {
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

        private static TextMeshProUGUI CreateLabel(string name, Transform parent, float fontSize, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = Color.white;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private void OnDisasterChanged(DisasterInstance instance) => Refresh();

        private void Update()
        {
            Refresh();
            AnimateTag();
            SyncHighlights();
        }

        private void HideTag()
        {
            if (worldTagRoot != null) worldTagRoot.gameObject.SetActive(false);
            if (arrowRoot != null) arrowRoot.gameObject.SetActive(false);
            if (distanceText != null) distanceText.gameObject.SetActive(false);
        }

        private void Refresh()
        {
            // Build() có thể đã thoát sớm (không có Canvas) -> các tham chiếu này còn null.
            if (worldTagRoot == null) return;

            shown.Clear();
            if (manager == null) manager = FindAnyObjectByType<DisasterRuntimeManager>();
            if (manager != null) shown.AddRange(manager.ActiveDisasters);
            if (shown.Count == 0)
            {
                HideTag();
                return;
            }

            DisasterInstance top = shown[0];
            for (int i = 1; i < shown.Count; i++)
                if ((int)shown[i].kind < (int)top.kind) top = shown[i];

            if (!TryGetAffectedCenter(top, out Vector3 worldCenter, out _))
            {
                HideTag();
                return;
            }
            UpdateWorldTag(top, worldCenter);
        }

        private bool TryGetAffectedCenter(DisasterInstance instance, out Vector3 center, out int tileCount)
        {
            center = Vector3.zero;
            tileCount = 0;
            if (instance == null || manager == null) return false;
            manager.FillAffectedTiles(instance, tileBuffer);
            for (int i = 0; i < tileBuffer.Count; i++)
            {
                var tile = tileBuffer[i];
                if (tile == null) continue;
                center += tile.transform.position;
                tileCount++;
            }
            if (tileCount == 0) return false;
            center /= tileCount;
            return true;
        }
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

                manager.FillAffectedTiles(instance, tileBuffer);
                foreach (var tile in tileBuffer)
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

            // Pool luôn được dùng theo thứ tự 0..used-1 nên mọi vòng từ used trở đi là vòng KHÔNG
            // còn ô nào dùng trong frame này -> phải tắt. Đây chính là chỗ giấu vòng khi thiên tai
            // kết thúc (used về 0) hoặc khi số ô bị ảnh hưởng giảm.
            for (int i = used; i < highlightPool.Count; i++)
            {
                var marker = highlightPool[i];
                if (marker != null) marker.gameObject.SetActive(false);
            }
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
            if (worldTagRoot != null) Destroy(worldTagRoot.gameObject);

            if (manager != null)
            {
                manager.DisasterStarted -= OnDisasterChanged;
                manager.DisasterChanged -= OnDisasterChanged;
                manager.DisasterEnded -= OnDisasterChanged;
            }

            if (highlightMaterial != null) Destroy(highlightMaterial);
            if (highlightMesh != null) Destroy(highlightMesh);
        }
    }
}
