using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs.Islands
{
    public enum CloudCoverStyle
    {
        FluffyTwoLayer = 0,
        SoftAiryMist = 1,
        KayKitLowPolyPlusBlanket = 2
    }

    /// <summary>
    /// Dual-layer Cozy Island Cloud Cover:
    /// Layer 1: Seamless organic billow blanket spanning well beyond the island perimeter (guarantees zero holes).
    /// Layer 2: Soft-edged 3D cumulus puffs floating & breathing above the blanket for playful diorama depth.
    /// Supports animated center-outward unlock reveal via MaterialPropertyBlock (never dirties shared assets).
    /// </summary>
    [ExecuteAlways]
    public sealed class CozyIslandCloudCover : MonoBehaviour
    {
        [Header("Style & Layers")]
        [SerializeField] private CloudCoverStyle style = CloudCoverStyle.FluffyTwoLayer;
        [SerializeField] private bool showBlanketLayer = true;
        [SerializeField] private bool showPuffLayer = true;

        [Header("Coverage & Shape")]
        [SerializeField, Range(6f, 32f)] private float coverRadius = 13.0f;
        [SerializeField, Range(0.2f, 5f)] private float baseHeight = 2.05f;
        [SerializeField, Range(0f, 3f)] private float domeLift = 0.85f;
        [SerializeField, Range(4, 36)] private int puffCount = 11;
        [SerializeField, Range(0f, 3f)] private float driftSpeed = 1.0f;
        [SerializeField, Range(0.08f, 0.48f)] private float edgeSoftness = 0.28f;

        [Header("Reveal Animation")]
        [SerializeField, Range(0f, 1f)] private float dissolve = 0f;

        [Header("Materials & Meshes")]
        [SerializeField] private Material blanketFluffyMaterial;
        [SerializeField] private Material blanketMistMaterial;
        [SerializeField] private Material puffFluffyMaterial;
        [SerializeField] private Material puffKayKitMaterial;
        [SerializeField] private Mesh kayKitCloudBigMesh;
        [SerializeField] private Mesh kayKitCloudSmallMesh;

        private const string RuntimeRootName = "__CozyCloudRuntime";

        private Transform runtimeRoot;
        private MeshRenderer blanketRenderer;
        private MeshFilter blanketFilter;
        private Mesh blanketMesh;
        private Mesh sculptedPuffMesh;

        private readonly List<PuffInstance> puffs = new List<PuffInstance>();
        private MaterialPropertyBlock blanketMpb;
        private MaterialPropertyBlock puffMpb;
        private Coroutine revealRoutine;

        private struct PuffInstance
        {
            public Transform Transform;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Vector3 BaseLocalPos;
            public Vector3 RadialDir;
            public Vector3 BaseScale;
            public float BaseYaw;
            public float Phase;
            public float BobAmp;
            public float BobSpeed;
            public float OrbitSpeed;
            public float ClearBias;
        }

        public CloudCoverStyle Style => style;
        public bool ShowBlanketLayer => showBlanketLayer;
        public bool ShowPuffLayer => showPuffLayer;
        public float CoverRadius => coverRadius;
        public float BaseHeight => baseHeight;
        public float EdgeSoftness => edgeSoftness;
        public float DriftSpeed => driftSpeed;
        public float Dissolve => dissolve;
        public bool IsRevealed => dissolve >= 0.99f;

        private static readonly int IdDissolve = Shader.PropertyToID("_Dissolve");
        private static readonly int IdEdgeSoftness = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int IdDriftSpeed = Shader.PropertyToID("_DriftSpeed");

        private void OnEnable()
        {
            EnsurePropertyBlocks();
            RebuildAll();
        }

        private void OnValidate()
        {
            coverRadius = Mathf.Clamp(coverRadius, 6f, 32f);
            puffCount = Mathf.Clamp(puffCount, 4, 36);
            if (!isActiveAndEnabled) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled)
                {
                    RebuildAll();
                }
            };
#endif
        }

        private void OnDisable()
        {
            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
                revealRoutine = null;
            }
        }

        private void OnDestroy()
        {
            CleanupRuntimeObjectsAndMeshes();
        }

        public void AssignAssets(
            Material fluffyBlanket,
            Material mistBlanket,
            Material fluffyPuff,
            Material kayKitPuff,
            Mesh bigKayKitMesh,
            Mesh smallKayKitMesh)
        {
            blanketFluffyMaterial = fluffyBlanket;
            blanketMistMaterial = mistBlanket;
            puffFluffyMaterial = fluffyPuff;
            puffKayKitMaterial = kayKitPuff;
            kayKitCloudBigMesh = bigKayKitMesh;
            kayKitCloudSmallMesh = smallKayKitMesh;
            RebuildAll();
        }

        public void ConfigureForRadius(float radius, float height = 1.95f)
        {
            coverRadius = Mathf.Clamp(radius, 6f, 32f);
            baseHeight = height;
            RebuildAll();
        }

        public void SetStyle(CloudCoverStyle nextStyle)
        {
            if (style == nextStyle && runtimeRoot != null)
            {
                ApplyDynamicProperties();
                return;
            }
            style = nextStyle;
            edgeSoftness = nextStyle == CloudCoverStyle.SoftAiryMist ? 0.36f : 0.26f;
            RebuildAll();
        }

        public void SetLayerVisibility(bool blanket, bool puffsVisible)
        {
            showBlanketLayer = blanket;
            showPuffLayer = puffsVisible;
            if (blanketRenderer != null) blanketRenderer.enabled = showBlanketLayer;
            for (int i = 0; i < puffs.Count; i++)
            {
                if (puffs[i].Renderer != null) puffs[i].Renderer.enabled = showPuffLayer && style != CloudCoverStyle.SoftAiryMist;
            }
        }

        public void SetLiveTuning(float radius, float height, float softness, float speed)
        {
            bool geometryChanged = !Mathf.Approximately(coverRadius, radius) || !Mathf.Approximately(baseHeight, height);
            coverRadius = Mathf.Clamp(radius, 6f, 32f);
            baseHeight = Mathf.Clamp(height, 0.2f, 5f);
            edgeSoftness = Mathf.Clamp(softness, 0.08f, 0.48f);
            driftSpeed = Mathf.Clamp(speed, 0f, 3f);

            if (geometryChanged)
            {
                RebuildAll();
            }
            else
            {
                ApplyDynamicProperties();
            }
        }

        public void SetDissolve(float value)
        {
            dissolve = Mathf.Clamp01(value);
            ApplyDynamicProperties();
        }

        public void ResetCovered()
        {
            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
                revealRoutine = null;
            }
            SetDissolve(0f);
        }

        public void PlayReveal(float duration = 1.4f, Action onComplete = null)
        {
            if (!Application.isPlaying)
            {
                SetDissolve(1f);
                onComplete?.Invoke();
                return;
            }

            if (revealRoutine != null) StopCoroutine(revealRoutine);
            revealRoutine = StartCoroutine(RevealRoutine(Mathf.Max(0.25f, duration), onComplete));
        }

        private IEnumerator RevealRoutine(float duration, Action onComplete)
        {
            float elapsed = 0f;
            float startDissolve = dissolve;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - u) * (1f - u) * (1f - u);
                SetDissolve(Mathf.Lerp(startDissolve, 1f, eased));
                yield return null;
            }

            SetDissolve(1f);
            revealRoutine = null;
            onComplete?.Invoke();
        }

        private void Update()
        {
            if (!Application.isPlaying || runtimeRoot == null) return;
            UpdatePuffTransforms(Time.time);
        }

        [ContextMenu("Rebuild Cloud Cover")]
        public void RebuildAll()
        {
            EnsurePropertyBlocks();
            CleanupRuntimeObjectsOnly();

            GameObject rootGo = new GameObject(RuntimeRootName)
            {
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
            runtimeRoot = rootGo.transform;
            runtimeRoot.SetParent(transform, false);
            runtimeRoot.localPosition = Vector3.zero;
            runtimeRoot.localRotation = Quaternion.identity;
            runtimeRoot.localScale = Vector3.one;

            BuildOrGetBlanketMesh();
            BuildOrGetSculptedPuffMesh();

            CreateBlanketObject();
            CreatePuffObjects();
            ApplyDynamicProperties();
        }

        private void CreateBlanketObject()
        {
            GameObject go = new GameObject("Layer1_CloudBlanket", typeof(MeshFilter), typeof(MeshRenderer))
            {
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
            go.transform.SetParent(runtimeRoot, false);
            go.transform.localPosition = new Vector3(0f, baseHeight, 0f);
            go.transform.localScale = new Vector3(coverRadius, 1f, coverRadius);

            blanketFilter = go.GetComponent<MeshFilter>();
            blanketFilter.sharedMesh = blanketMesh;

            blanketRenderer = go.GetComponent<MeshRenderer>();
            blanketRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            blanketRenderer.receiveShadows = false;
            blanketRenderer.sharedMaterial = style == CloudCoverStyle.SoftAiryMist && blanketMistMaterial != null
                ? blanketMistMaterial
                : blanketFluffyMaterial;
            blanketRenderer.enabled = showBlanketLayer;
        }

        private void CreatePuffObjects()
        {
            puffs.Clear();

            bool useKayKit = style == CloudCoverStyle.KayKitLowPolyPlusBlanket &&
                             (kayKitCloudBigMesh != null || kayKitCloudSmallMesh != null);
            Material puffMat = useKayKit && puffKayKitMaterial != null ? puffKayKitMaterial : puffFluffyMaterial;

            // Deterministic golden-angle sunflower distribution nestled inside Layer 1's opaque core (zero gaps or edge hangovers)
            var rng = new System.Random(1337);
            const float goldenAngle = 2.39996323f;

            int activeCount = puffCount;
            for (int i = 0; i < activeCount; i++)
            {
                float frac = (i + 0.35f) / activeCount;
                float rFrac = Mathf.Sqrt(frac) * 0.50f;
                float theta = i * goldenAngle + (float)(rng.NextDouble() * 0.35 - 0.175);

                float worldR = rFrac * coverRadius;
                float px = Mathf.Cos(theta) * worldR;
                float pz = Mathf.Sin(theta) * worldR;

                // Nestle bottom bellies right into Layer 1 Blanket so crests rise organically from the cloud sea
                float archY = baseHeight + domeLift * (1f - rFrac * rFrac) - 0.08f + (float)rng.NextDouble() * 0.22f;

                Mesh meshToUse = sculptedPuffMesh;
                float radialT = Mathf.Clamp01(rFrac / 0.50f);
                float sizeScale = Mathf.Lerp(2.15f, 1.38f, radialT) * (0.90f + (float)rng.NextDouble() * 0.22f);

                if (useKayKit)
                {
                    bool pickBig = (i % 3 != 2) && kayKitCloudBigMesh != null;
                    Mesh candidate = pickBig ? kayKitCloudBigMesh : (kayKitCloudSmallMesh != null ? kayKitCloudSmallMesh : kayKitCloudBigMesh);
                    if (candidate != null)
                    {
                        meshToUse = candidate;
                        float meshWidth = Mathf.Max(0.25f, candidate.bounds.size.x);
                        float targetWidth = (pickBig ? 6.4f : 4.6f) * Mathf.Lerp(1.15f, 0.85f, radialT);
                        sizeScale = targetWidth / meshWidth;
                    }
                }

                GameObject go = new GameObject($"Layer2_CloudPuff_{i:00}", typeof(MeshFilter), typeof(MeshRenderer))
                {
                    hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
                };
                go.transform.SetParent(runtimeRoot, false);

                MeshFilter mf = go.GetComponent<MeshFilter>();
                mf.sharedMesh = meshToUse;

                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = puffMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = showPuffLayer && style != CloudCoverStyle.SoftAiryMist;

                Vector3 basePos = new Vector3(px, archY, pz);
                Vector3 radial = new Vector3(px, 0f, pz);
                radial = radial.sqrMagnitude > 0.001f ? radial.normalized : Vector3.forward;

                float scaleY = useKayKit ? sizeScale : sizeScale * (0.62f + (float)rng.NextDouble() * 0.14f);
                var entry = new PuffInstance
                {
                    Transform = go.transform,
                    Filter = mf,
                    Renderer = mr,
                    BaseLocalPos = basePos,
                    RadialDir = radial,
                    BaseScale = new Vector3(sizeScale, scaleY, sizeScale * (0.86f + (float)rng.NextDouble() * 0.22f)),
                    BaseYaw = (float)rng.NextDouble() * 360f,
                    Phase = (float)rng.NextDouble() * Mathf.PI * 2f,
                    BobAmp = 0.07f + (float)rng.NextDouble() * 0.08f,
                    BobSpeed = 0.45f + (float)rng.NextDouble() * 0.35f,
                    OrbitSpeed = (i % 2 == 0 ? 1f : -1f) * (0.6f + (float)rng.NextDouble() * 0.7f),
                    ClearBias = 0.85f + (1f - radialT) * 0.55f
                };

                go.transform.localPosition = basePos;
                go.transform.localRotation = Quaternion.Euler(0f, entry.BaseYaw, 0f);
                go.transform.localScale = entry.BaseScale;
                puffs.Add(entry);
            }
        }

        private void ApplyDynamicProperties()
        {
            EnsurePropertyBlocks();

            if (blanketRenderer != null)
            {
                blanketRenderer.enabled = showBlanketLayer;
                blanketMpb.Clear();
                blanketMpb.SetFloat(IdDissolve, dissolve);
                blanketMpb.SetFloat(IdEdgeSoftness, edgeSoftness);
                blanketMpb.SetVector(IdDriftSpeed, new Vector4(0.14f * driftSpeed, 0.05f * driftSpeed, 0f, 0f));
                blanketRenderer.SetPropertyBlock(blanketMpb);
            }

            bool showPuffsNow = showPuffLayer && style != CloudCoverStyle.SoftAiryMist;
            float tNow = Application.isPlaying ? Time.time : 0f;
            UpdatePuffTransforms(tNow);

            float globalShrink = 1f - Mathf.SmoothStep(0.05f, 0.72f, dissolve);
            for (int i = 0; i < puffs.Count; i++)
            {
                PuffInstance p = puffs[i];
                if (p.Renderer == null) continue;
                p.Renderer.enabled = showPuffsNow && globalShrink > 0.03f;

                float puffDissolve = Mathf.Clamp01((dissolve - (1f - p.ClearBias) * 0.2f) * 1.45f);
                puffMpb.Clear();
                puffMpb.SetFloat(IdDissolve, puffDissolve);
                p.Renderer.SetPropertyBlock(puffMpb);
            }
        }

        private void UpdatePuffTransforms(float timeSec)
        {
            float swirl = timeSec * 1.15f * driftSpeed;
            float shrink = 1f - Mathf.SmoothStep(0.05f, 0.72f, dissolve);
            float sinkY = -Mathf.SmoothStep(0f, 0.72f, dissolve) * 1.15f;

            for (int i = 0; i < puffs.Count; i++)
            {
                PuffInstance p = puffs[i];
                if (p.Transform == null) continue;

                float bob = Mathf.Sin(timeSec * p.BobSpeed * Mathf.Max(0.2f, driftSpeed) + p.Phase) * p.BobAmp;
                float partOffset = dissolve * 3.8f * p.ClearBias;
                Vector3 pos = p.BaseLocalPos + p.RadialDir * partOffset + Vector3.up * (bob + sinkY);
                if (swirl != 0f)
                {
                    pos = Quaternion.Euler(0f, swirl * (0.25f * p.OrbitSpeed), 0f) * pos;
                }

                p.Transform.localPosition = pos;
                p.Transform.localRotation = Quaternion.Euler(0f, p.BaseYaw + swirl * p.OrbitSpeed, 0f);
                p.Transform.localScale = p.BaseScale * Mathf.Max(0.01f, shrink);
            }
        }

        private void EnsurePropertyBlocks()
        {
            if (blanketMpb == null) blanketMpb = new MaterialPropertyBlock();
            if (puffMpb == null) puffMpb = new MaterialPropertyBlock();
        }

        private void CleanupRuntimeObjectsOnly(bool isDestroyingParent = false)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name != RuntimeRootName) continue;

                if (child == runtimeRoot) runtimeRoot = null;
                child.gameObject.SetActive(false);
                if (!isDestroyingParent)
                {
                    child.name = "__CozyCloudRuntime_PendingDestroy";
                    child.SetParent(null, false);
                }

                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            if (runtimeRoot != null)
            {
                runtimeRoot.gameObject.SetActive(false);
                if (!isDestroyingParent)
                {
                    runtimeRoot.name = "__CozyCloudRuntime_PendingDestroy";
                    runtimeRoot.SetParent(null, false);
                }

                if (Application.isPlaying) Destroy(runtimeRoot.gameObject);
                else DestroyImmediate(runtimeRoot.gameObject);
            }

            runtimeRoot = null;
            blanketRenderer = null;
            blanketFilter = null;
            puffs.Clear();
        }

        private void CleanupRuntimeObjectsAndMeshes()
        {
            CleanupRuntimeObjectsOnly(isDestroyingParent: true);
            if (blanketMesh != null)
            {
                if (Application.isPlaying) Destroy(blanketMesh);
                else DestroyImmediate(blanketMesh);
                blanketMesh = null;
            }
            if (sculptedPuffMesh != null)
            {
                if (Application.isPlaying) Destroy(sculptedPuffMesh);
                else DestroyImmediate(sculptedPuffMesh);
                sculptedPuffMesh = null;
            }
        }

        private void BuildOrGetBlanketMesh()
        {
            if (blanketMesh != null) return;

            const int segments = 48;
            const int rings = 22;
            var verts = new List<Vector3>(1 + segments * rings);
            var norms = new List<Vector3>(1 + segments * rings);
            var uvs = new List<Vector2>(1 + segments * rings);
            var tris = new List<int>(segments * rings * 6);

            verts.Add(new Vector3(0f, domeLift, 0f));
            norms.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int r = 1; r <= rings; r++)
            {
                float radius = (float)r / rings;
                // Gentle dome peaking at center (clears elevated Wet hexes) and sloping smoothly down toward water at rim
                float y = domeLift * (1f - radius * radius) - Mathf.Pow(radius, 3f) * 0.45f;
                for (int s = 0; s < segments; s++)
                {
                    float ang = (float)s / segments * Mathf.PI * 2f;
                    float x = Mathf.Cos(ang) * radius;
                    float z = Mathf.Sin(ang) * radius;
                    verts.Add(new Vector3(x, y, z));
                    norms.Add(new Vector3(x * 0.18f, 1f, z * 0.18f).normalized);
                    uvs.Add(new Vector2(x * 0.5f + 0.5f, z * 0.5f + 0.5f));
                }
            }

            // Center fan
            for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                tris.Add(0);
                tris.Add(1 + next);
                tris.Add(1 + s);
            }

            // Concentric quads
            for (int r = 1; r < rings; r++)
            {
                int ringStart = 1 + (r - 1) * segments;
                int nextRingStart = 1 + r * segments;
                for (int s = 0; s < segments; s++)
                {
                    int next = (s + 1) % segments;
                    int a = ringStart + s;
                    int b = ringStart + next;
                    int c = nextRingStart + s;
                    int d = nextRingStart + next;

                    tris.Add(a); tris.Add(b); tris.Add(d);
                    tris.Add(a); tris.Add(d); tris.Add(c);
                }
            }

            blanketMesh = new Mesh { name = "CozyCloudBlanketMesh", hideFlags = HideFlags.DontSave };
            blanketMesh.SetVertices(verts);
            blanketMesh.SetNormals(norms);
            blanketMesh.SetUVs(0, uvs);
            blanketMesh.SetTriangles(tris, 0);
            blanketMesh.RecalculateBounds();
        }

        private void BuildOrGetSculptedPuffMesh()
        {
            if (sculptedPuffMesh != null) return;

            var lobeSpecs = new[]
            {
                (offset: new Vector3( 0.00f,  0.34f,  0.00f), scale: new Vector3(1.18f, 0.85f, 1.08f)),
                (offset: new Vector3(-0.78f,  0.14f,  0.18f), scale: new Vector3(0.92f, 0.68f, 0.88f)),
                (offset: new Vector3( 0.82f,  0.12f, -0.14f), scale: new Vector3(0.88f, 0.65f, 0.84f)),
                (offset: new Vector3( 0.16f,  0.10f,  0.72f), scale: new Vector3(0.84f, 0.62f, 0.80f)),
                (offset: new Vector3(-0.22f,  0.08f, -0.68f), scale: new Vector3(0.80f, 0.60f, 0.78f)),
                (offset: new Vector3(-0.46f,  0.02f, -0.46f), scale: new Vector3(0.68f, 0.50f, 0.68f)),
                (offset: new Vector3( 0.56f,  0.04f,  0.46f), scale: new Vector3(0.70f, 0.52f, 0.68f))
            };

            BuildUnitIcosphere(2, out Vector3[] unitVerts, out int[] unitTris);

            var allVerts = new List<Vector3>(lobeSpecs.Length * unitVerts.Length);
            var allNorms = new List<Vector3>(lobeSpecs.Length * unitVerts.Length);
            var allTris = new List<int>(lobeSpecs.Length * unitTris.Length);

            Vector3 clusterCenter = new Vector3(0f, 0.12f, 0f);
            for (int l = 0; l < lobeSpecs.Length; l++)
            {
                int baseIdx = allVerts.Count;
                var spec = lobeSpecs[l];

                for (int v = 0; v < unitVerts.Length; v++)
                {
                    Vector3 n = unitVerts[v];
                    // Soft flat-bottom cloud shaping
                    Vector3 p = new Vector3(n.x * spec.scale.x, n.y * spec.scale.y, n.z * spec.scale.z);
                    if (p.y < -0.12f)
                    {
                        p.y = -0.12f + (p.y + 0.12f) * 0.28f;
                    }
                    p += spec.offset;
                    allVerts.Add(p);

                    // Stylized Ghibli normal blending: preserve gentle lobe convexity (35% cluster outward, 65% local lobe) so sunlit highlights + cool crevices both read clearly
                    Vector3 clusterNorm = (p - clusterCenter);
                    clusterNorm = clusterNorm.sqrMagnitude > 0.0001f ? clusterNorm.normalized : Vector3.up;
                    Vector3 blended = Vector3.Slerp(n, (clusterNorm + Vector3.up * 0.28f).normalized, 0.36f).normalized;
                    allNorms.Add(blended);
                }

                for (int t = 0; t < unitTris.Length; t++)
                {
                    allTris.Add(baseIdx + unitTris[t]);
                }
            }

            sculptedPuffMesh = new Mesh { name = "CozySculptedCumulusMesh", hideFlags = HideFlags.DontSave };
            sculptedPuffMesh.SetVertices(allVerts);
            sculptedPuffMesh.SetNormals(allNorms);
            sculptedPuffMesh.SetTriangles(allTris, 0);
            sculptedPuffMesh.RecalculateBounds();
        }

        private static void BuildUnitIcosphere(int subdivisions, out Vector3[] vertices, out int[] triangles)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var verts = new List<Vector3>
            {
                new Vector3(-1,  t,  0).normalized, new Vector3( 1,  t,  0).normalized,
                new Vector3(-1, -t,  0).normalized, new Vector3( 1, -t,  0).normalized,
                new Vector3( 0, -1,  t).normalized, new Vector3( 0,  1,  t).normalized,
                new Vector3( 0, -1, -t).normalized, new Vector3( 0,  1, -t).normalized,
                new Vector3( t,  0, -1).normalized, new Vector3( t,  0,  1).normalized,
                new Vector3(-t,  0, -1).normalized, new Vector3(-t,  0,  1).normalized
            };

            var tris = new List<int>
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
                1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
                4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };

            var midpointCache = new Dictionary<long, int>();
            int GetMidpoint(int i1, int i2)
            {
                long a = Math.Min(i1, i2);
                long b = Math.Max(i1, i2);
                long key = (a << 32) | b;
                if (midpointCache.TryGetValue(key, out int idx)) return idx;
                Vector3 mid = ((verts[i1] + verts[i2]) * 0.5f).normalized;
                idx = verts.Count;
                verts.Add(mid);
                midpointCache[key] = idx;
                return idx;
            }

            for (int s = 0; s < subdivisions; s++)
            {
                var nextTris = new List<int>(tris.Count * 4);
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int v1 = tris[i];
                    int v2 = tris[i + 1];
                    int v3 = tris[i + 2];
                    int a = GetMidpoint(v1, v2);
                    int b = GetMidpoint(v2, v3);
                    int c = GetMidpoint(v3, v1);

                    nextTris.Add(v1); nextTris.Add(a); nextTris.Add(c);
                    nextTris.Add(v2); nextTris.Add(b); nextTris.Add(a);
                    nextTris.Add(v3); nextTris.Add(c); nextTris.Add(b);
                    nextTris.Add(a);  nextTris.Add(b); nextTris.Add(c);
                }
                tris = nextTris;
            }

            vertices = verts.ToArray();
            triangles = tris.ToArray();
        }
    }
}
