using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LumiWorld.Acs
{
    /// <summary>
    /// UI cảnh báo thiên tai/vật cản tự sinh vào scene (không cần tạo prefab hay sửa Scene):
    ///  - Banner góc trên: loại thiên tai ưu tiên cao nhất, thời gian còn lại, gợi ý cách dập.
    ///  - Vòng sáng dưới chân mọi ô bị ảnh hưởng để thấy ngay vùng bị phủ.
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

        [Header("Banner")]
        [SerializeField] private float bannerPixelHeight = 34f;

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
        private TextMeshProUGUI bannerText;

        private Material highlightMaterial;
        private Mesh highlightMesh;

        private readonly List<DisasterInstance> shown = new List<DisasterInstance>();
        private readonly List<Transform> highlightPool = new List<Transform>();

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

            canvas = FindScreenCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("[LumiWorld Disaster UI] Không tìm thấy Canvas màn hình nên không vẽ được banner.");
                return;
            }

            bannerRoot = CreateRect("DisasterBanner", canvas.transform);
            bannerRoot.anchorMin = new Vector2(0f, 1f);
            bannerRoot.anchorMax = new Vector2(1f, 1f);
            bannerRoot.pivot = new Vector2(0.5f, 1f);
            bannerRoot.anchoredPosition = Vector2.zero;
            bannerRoot.sizeDelta = new Vector2(0f, bannerPixelHeight);

            bannerBackground = bannerRoot.gameObject.AddComponent<Image>();
            bannerBackground.color = new Color(0.08f, 0.09f, 0.12f, 0.82f);
            bannerBackground.raycastTarget = false;

            GameObject textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(bannerRoot, false);
            bannerText = textObj.AddComponent<TextMeshProUGUI>();
            bannerText.alignment = TextAlignmentOptions.Center;
            bannerText.fontSize = 18f;
            bannerText.color = Color.white;
            bannerText.raycastTarget = false;

            RectTransform textRect = (RectTransform)textObj.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 0f);
            textRect.offsetMax = new Vector2(-8f, 0f);

            if (disasterManager != null)
            {
                disasterManager.DisasterStarted += OnDisasterChanged;
                disasterManager.DisasterChanged += OnDisasterChanged;
                disasterManager.DisasterEnded += OnDisasterChanged;
            }

            Refresh();
        }

        private static Canvas FindScreenCanvas()
        {
            foreach (var found in FindObjectsByType<Canvas>())
            {
                if (found.isRootCanvas && found.renderMode != RenderMode.WorldSpace) return found;
            }

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
            SyncHighlights();
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
                if (bannerBackground != null) bannerBackground.color = new Color(0f, 0f, 0f, 0f);
                bannerText.text = string.Empty;
                return;
            }

            // Luôn cảnh báo thiên tai ưu tiên cao nhất (số enum nhỏ).
            DisasterInstance top = shown[0];
            for (int i = 1; i < shown.Count; i++)
            {
                if ((int)shown[i].kind < (int)top.kind) top = shown[i];
            }

            if (bannerBackground != null)
            {
                Color accent = DisasterCatalog.AlertColor(top.kind);
                accent.a = 0.30f;
                bannerBackground.color = accent;
            }

            int remaining = Mathf.CeilToInt(Mathf.Max(0f, (float)top.remainingSeconds));
            bannerText.color = DisasterCatalog.AlertColor(top.kind);
            bannerText.text = $"{DisasterCatalog.ShortLabel(top.kind)} — {DisasterCatalog.DisplayName(top.kind)}" +
                $"   còn {remaining}s   {AdviceFor(top.kind)}" +
                (shown.Count > 1 ? $"   (+{shown.Count - 1})" : string.Empty);
        }

        private static string AdviceFor(DisasterKind kind)
        {
            switch (kind)
            {
                case DisasterKind.ForestFire: return "→ Dùng thẻ MƯA dập lửa để cứu ô";
                case DisasterKind.Cyclone: return "→ Mưa lớn nhiều ô để dập bão";
                case DisasterKind.Drought: return "→ Dùng mưa để chấm dứt hạn hán";
                case DisasterKind.BarrenLand: return "→ Cần MƯA LỚN (mưa nhỏ không thấm)";
                case DisasterKind.Obstacle: return "→ Vật cản: dọn hoặc dùng mưa";
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
