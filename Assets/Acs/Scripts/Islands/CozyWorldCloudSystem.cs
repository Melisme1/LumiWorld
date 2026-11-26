using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LumiWorld.Acs.Islands
{
    public enum WorldCloudStyle
    {
        Painterly25D = 0,
        Smooth3DSdf = 1,
        SoftMistOnly = 2
    }

    [Serializable]
    public sealed class CloudClearZone
    {
        public string id = "starter";
        public string displayName = "Starter";
        public Vector3 worldCenter = Vector3.zero;
        [Range(8f, 60f)] public float clearRadius = 22f;
        public bool isUnlocked = true;
        [Range(0f, 1f)] public float currentOpen = 1f;
    }

    /// <summary>
    /// World-Scale Dual-Layer Cozy Cloud Cover System:
    /// - Layer 1: Continuous misty/smoky cloud sea spanning all locked regions around islands,
    ///            organically parting around unlocked island clear zones.
    /// - Layer 2: Seamless overlapping billowing cloud bank rendered via GPU Instancing
    ///            supporting Painterly 2.5D sprites (recommended farm-game look) OR Smooth 3D SDF metaballs.
    /// Zero runtime GameObject churn when switching styles or unlocking/locking islands.
    /// </summary>
    [ExecuteAlways]
    public sealed class CozyWorldCloudSystem : MonoBehaviour
    {
        [Header("Style & Layers")]
        [SerializeField] private WorldCloudStyle style = WorldCloudStyle.Painterly25D;
        [SerializeField] private bool showSeaLayer = true;
        [SerializeField] private bool showBankLayer = true;

        [Header("World Coverage & Tuning")]
        [SerializeField] private Vector3 seaCenter = new Vector3(45f, 0f, 0f);
        [SerializeField] private float seaExtent = 420f;
        [SerializeField, Range(0.8f, 5.5f)] private float baseHeight = 3.0f;
        [SerializeField, Range(0.08f, 0.55f)] private float edgeSoftness = 0.30f;
        [SerializeField, Range(0.55f, 1.80f)] private float bankScale = 1.0f;
        [SerializeField, Range(0f, 2.5f)] private float driftSpeed = 1.0f;
        [SerializeField, Range(0.4f, 3.0f)] private float unlockDuration = 1.55f;

        [Header("Island Clear Zones")]
        [SerializeField] private List<CloudClearZone> zones = new List<CloudClearZone>();

        [Header("Materials")]
        [SerializeField] private Material worldCloudSeaMaterial;
        [SerializeField] private Material spriteBankMaterial;
        [SerializeField] private Material smoothSdfMaterial;

        private const string SeaChildName = "__CozyWorldCloudSea";
        private const int MaxShaderZones = 8;

        private Mesh seaPlaneMesh;
        private Mesh spriteQuadMesh;
        private Mesh sdfMeshA;
        private Mesh sdfMeshB;

        private MeshRenderer seaRenderer;
        private Transform seaTransform;
        private MaterialPropertyBlock seaMpb;

        private struct CloudBankNode
        {
            public Vector3 BasePos;
            public float BaseWidth;
            public float BaseHeight;
            public float Yaw;
            public float Phase;
            public float BobAmp;
            public float BobSpeed;
            public float DriftAmp;
            public int Variant; // 0..3 for 2x2 sprite atlas; variant & 1 for 3D SDF
            public bool FlipX;
            public Color Tint;
        }

        private struct RenderBatchItem
        {
            public Matrix4x4 Matrix;
            public Vector4 TileRect;
            public Vector4 TintAlpha;
            public float SortKey;
            public int MeshVariant;
        }

        private readonly List<CloudBankNode> bankNodes = new List<CloudBankNode>(512);
        private readonly List<RenderBatchItem> visibleItems = new List<RenderBatchItem>(512);
        private readonly Vector4[] shaderZones = new Vector4[MaxShaderZones];

        // Reusable instancing buffers (max 1023 per Unity Graphics.DrawMeshInstanced call)
        private Matrix4x4[] batchMatrices = new Matrix4x4[1023];
        private Vector4[] batchTileRects = new Vector4[1023];
        private Vector4[] batchTintAlphas = new Vector4[1023];
        private MaterialPropertyBlock bankMpb;

        private static readonly int[] IdClearZoneSlots =
        {
            Shader.PropertyToID("_ClearZone0"),
            Shader.PropertyToID("_ClearZone1"),
            Shader.PropertyToID("_ClearZone2"),
            Shader.PropertyToID("_ClearZone3"),
            Shader.PropertyToID("_ClearZone4"),
            Shader.PropertyToID("_ClearZone5"),
            Shader.PropertyToID("_ClearZone6"),
            Shader.PropertyToID("_ClearZone7")
        };
        private static readonly int IdClearZoneCount = Shader.PropertyToID("_ClearZoneCount");
        private static readonly int IdEdgeSoftness = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int IdDriftSpeed = Shader.PropertyToID("_DriftSpeed");
        private static readonly int IdWorldFadeStart = Shader.PropertyToID("_WorldFadeStart");
        private static readonly int IdWorldFadeEnd = Shader.PropertyToID("_WorldFadeEnd");
        private static readonly int IdTileRect = Shader.PropertyToID("_TileRect");
        private static readonly int IdTintAlpha = Shader.PropertyToID("_TintAlpha");

        public WorldCloudStyle Style => style;
        public bool ShowSeaLayer => showSeaLayer;
        public bool ShowBankLayer => showBankLayer;
        public float BaseHeight => baseHeight;
        public float EdgeSoftness => edgeSoftness;
        public float BankScale => bankScale;
        public float DriftSpeed => driftSpeed;
        public IReadOnlyList<CloudClearZone> Zones => zones;
        public int ActiveBankInstanceCount { get; private set; }

        private void OnEnable()
        {
            EnsureResources();
            RebuildBankLayout();
            SyncZoneInstantIfEditMode();
            UpdateSeaVisuals();
        }

        private void OnDestroy()
        {
            CleanupSeaChild(isDestroyingParent: true);
            DestroyResource(ref seaPlaneMesh);
            DestroyResource(ref spriteQuadMesh);
            DestroyResource(ref sdfMeshA);
            DestroyResource(ref sdfMeshB);
        }

        public void ConfigureSystem(
            Material seaMat,
            Material spriteMat,
            Material sdfMat,
            Vector3 center,
            float extent,
            List<CloudClearZone> initialZones)
        {
            float clampedExtent = Mathf.Max(160f, extent);
            bool extentChanged = !Mathf.Approximately(seaExtent, clampedExtent);

            if (seaMat != null) worldCloudSeaMaterial = seaMat;
            if (spriteMat != null) spriteBankMaterial = spriteMat;
            if (sdfMat != null) smoothSdfMaterial = sdfMat;
            seaCenter = center;
            seaExtent = clampedExtent;
            zones = initialZones ?? new List<CloudClearZone>();

            for (int i = 0; i < zones.Count; i++)
            {
                zones[i].currentOpen = zones[i].isUnlocked ? 1f : 0f;
            }

            if (extentChanged)
            {
                DestroyResource(ref seaPlaneMesh);
                CleanupSeaChild(isDestroyingParent: false);
            }

            EnsureResources();
            RebuildBankLayout();
            UpdateSeaVisuals();
        }

        public void SetStyle(WorldCloudStyle nextStyle)
        {
            style = nextStyle;
            UpdateSeaVisuals();
        }

        public void SetLayerVisibility(bool sea, bool bank)
        {
            showSeaLayer = sea;
            showBankLayer = bank;
            if (seaRenderer != null) seaRenderer.enabled = showSeaLayer;
        }

        public void SetLiveTuning(float height, float softness, float scale, float speed)
        {
            baseHeight = Mathf.Clamp(height, 0.8f, 5.5f);
            edgeSoftness = Mathf.Clamp(softness, 0.08f, 0.55f);
            bankScale = Mathf.Clamp(scale, 0.55f, 1.80f);
            driftSpeed = Mathf.Clamp(speed, 0f, 2.5f);
            UpdateSeaVisuals();
        }

        public CloudClearZone FindZone(string id)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                if (string.Equals(zones[i].id, id, StringComparison.OrdinalIgnoreCase))
                    return zones[i];
            }
            return null;
        }

        public bool IsZoneUnlocked(string id)
        {
            CloudClearZone z = FindZone(id);
            return z != null && z.isUnlocked;
        }

        public float GetZoneOpenProgress(string id)
        {
            CloudClearZone z = FindZone(id);
            return z != null ? z.currentOpen : 0f;
        }

        public void SetZoneUnlocked(string id, bool unlocked, bool instant = false)
        {
            CloudClearZone z = FindZone(id);
            if (z == null) return;
            z.isUnlocked = unlocked;
            if (instant || !Application.isPlaying)
            {
                z.currentOpen = unlocked ? 1f : 0f;
            }
            UpdateSeaVisuals();
        }

        public void ToggleZoneUnlocked(string id)
        {
            CloudClearZone z = FindZone(id);
            if (z == null) return;
            SetZoneUnlocked(id, !z.isUnlocked, instant: false);
        }

        public void SetZoneClearRadius(string id, float radius)
        {
            CloudClearZone z = FindZone(id);
            if (z == null) return;
            z.clearRadius = Mathf.Clamp(radius, 8f, 60f);
            UpdateSeaVisuals();
        }

        private void Update()
        {
            EnsureResources();

            if (Application.isPlaying)
            {
                float step = Time.deltaTime / Mathf.Max(0.25f, unlockDuration);
                for (int i = 0; i < zones.Count; i++)
                {
                    float target = zones[i].isUnlocked ? 1f : 0f;
                    if (!Mathf.Approximately(zones[i].currentOpen, target))
                    {
                        zones[i].currentOpen = Mathf.MoveTowards(zones[i].currentOpen, target, step);
                    }
                }
            }
            else
            {
                SyncZoneInstantIfEditMode();
            }

            UpdateSeaVisuals();
            RenderBankLayer();
        }

        private void SyncZoneInstantIfEditMode()
        {
            if (Application.isPlaying) return;
            for (int i = 0; i < zones.Count; i++)
            {
                zones[i].currentOpen = zones[i].isUnlocked ? 1f : 0f;
            }
        }

        private void EnsureResources()
        {
            if (seaMpb == null) seaMpb = new MaterialPropertyBlock();
            if (bankMpb == null) bankMpb = new MaterialPropertyBlock();
            if (seaPlaneMesh == null) seaPlaneMesh = CozyCloudSdfMeshBuilder.CreateSubdividedPlaneMesh(seaExtent, 96);
            if (spriteQuadMesh == null) spriteQuadMesh = CozyCloudSdfMeshBuilder.CreateBillboardQuadMesh();
            if (sdfMeshA == null) sdfMeshA = CozyCloudSdfMeshBuilder.CreateSmoothSdfCloudMesh(0);
            if (sdfMeshB == null) sdfMeshB = CozyCloudSdfMeshBuilder.CreateSmoothSdfCloudMesh(1);

            if (seaRenderer == null || seaTransform == null)
            {
                CleanupSeaChild(isDestroyingParent: false);
                var go = new GameObject(SeaChildName, typeof(MeshFilter), typeof(MeshRenderer))
                {
                    hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
                };
                seaTransform = go.transform;
                seaTransform.SetParent(transform, false);
                seaTransform.localRotation = Quaternion.identity;
                seaTransform.localScale = Vector3.one;

                var mf = go.GetComponent<MeshFilter>();
                mf.sharedMesh = seaPlaneMesh;

                seaRenderer = go.GetComponent<MeshRenderer>();
                seaRenderer.shadowCastingMode = ShadowCastingMode.Off;
                seaRenderer.receiveShadows = false;
                seaRenderer.sharedMaterial = worldCloudSeaMaterial;
                seaRenderer.enabled = showSeaLayer;
            }
            else if (seaRenderer.sharedMaterial != worldCloudSeaMaterial && worldCloudSeaMaterial != null)
            {
                seaRenderer.sharedMaterial = worldCloudSeaMaterial;
            }

            if (spriteBankMaterial != null && !spriteBankMaterial.enableInstancing)
                spriteBankMaterial.enableInstancing = true;
            if (smoothSdfMaterial != null && !smoothSdfMaterial.enableInstancing)
                smoothSdfMaterial.enableInstancing = true;
        }

        private void UpdateSeaVisuals()
        {
            if (seaTransform != null)
            {
                seaTransform.position = new Vector3(seaCenter.x, baseHeight, seaCenter.z);
            }

            if (seaRenderer == null) return;
            seaRenderer.enabled = showSeaLayer;

            int count = Mathf.Min(zones.Count, MaxShaderZones);
            for (int i = 0; i < MaxShaderZones; i++)
            {
                if (i < count)
                {
                    CloudClearZone z = zones[i];
                    float easedOpen = SmoothOpenCurve(z.currentOpen);
                    shaderZones[i] = new Vector4(z.worldCenter.x, z.worldCenter.z, z.clearRadius, easedOpen);
                }
                else
                {
                    shaderZones[i] = Vector4.zero;
                }
            }

            seaMpb.Clear();
            for (int i = 0; i < MaxShaderZones; i++)
            {
                seaMpb.SetVector(IdClearZoneSlots[i], shaderZones[i]);
            }
            seaMpb.SetFloat(IdClearZoneCount, count);
            seaMpb.SetFloat(IdEdgeSoftness, edgeSoftness);
            seaMpb.SetVector(IdDriftSpeed, new Vector4(0.14f * driftSpeed, 0.05f * driftSpeed, 0f, 0f));
            seaMpb.SetFloat(IdWorldFadeStart, seaExtent * 0.37f);
            seaMpb.SetFloat(IdWorldFadeEnd, seaExtent * 0.48f);
            seaRenderer.SetPropertyBlock(seaMpb);
        }

        /// <summary>
        /// Generates a rich, deterministic, organically jittered overlapping lattice of large fluffy cloud bank nodes
        /// spanning the entire locked sea around all islands (zero grid rows, zero isolated tiny dots).
        /// </summary>
        public void RebuildBankLayout()
        {
            bankNodes.Clear();
            var rng = new System.Random(4242);

            float halfX = seaExtent * 0.42f;
            float halfZ = seaExtent * 0.38f;
            const float spacingX = 15.0f;
            const float spacingZ = 13.2f;

            int rowIdx = 0;
            for (float dz = -halfZ; dz <= halfZ; dz += spacingZ)
            {
                float rowShift = (rowIdx % 2 == 0) ? 0f : spacingX * 0.5f;
                for (float dx = -halfX; dx <= halfX; dx += spacingX)
                {
                    float wx = seaCenter.x + dx + rowShift + (float)(rng.NextDouble() * 11.6 - 5.8);
                    float wz = seaCenter.z + dz + (float)(rng.NextDouble() * 10.0 - 5.0);

                    // Circular world clip inside the sea fade radius
                    float distFromSeaCenter = Vector2.Distance(new Vector2(wx, wz), new Vector2(seaCenter.x, seaCenter.z));
                    if (distFromSeaCenter > seaExtent * 0.45f) continue;

                    // Keep natural 1:1 aspect ratio matching each 1024x1024 atlas tile (zero horizontal stretch!)
                    // Higher texel density (16-27m instead of 22-40m) keeps close-up edges crisp
                    float width = 16.5f + (float)rng.NextDouble() * 10.5f;
                    float height = width * (0.94f + (float)rng.NextDouble() * 0.12f);
                    float yOffset = 0.25f + (float)rng.NextDouble() * 1.85f;

                    // Subtle warm/cool white variation so overlapping cloud banks read with rich depth
                    float warmth = (float)rng.NextDouble();
                    Color tint = Color.Lerp(
                        new Color(0.965f, 0.982f, 1.00f, 1f),
                        new Color(1.00f, 0.994f, 0.975f, 1f),
                        warmth);

                    bankNodes.Add(new CloudBankNode
                    {
                        BasePos = new Vector3(wx, yOffset, wz),
                        BaseWidth = width,
                        BaseHeight = height,
                        Yaw = (float)rng.NextDouble() * 360f,
                        Phase = (float)rng.NextDouble() * Mathf.PI * 2f,
                        BobAmp = 0.18f + (float)rng.NextDouble() * 0.24f,
                        BobSpeed = 0.28f + (float)rng.NextDouble() * 0.26f,
                        DriftAmp = 0.65f + (float)rng.NextDouble() * 0.85f,
                        Variant = rng.Next(0, 4),
                        FlipX = rng.Next(0, 2) == 1,
                        Tint = tint
                    });
                }
                rowIdx++;
            }
        }

        private void RenderBankLayer()
        {
            ActiveBankInstanceCount = 0;
            if (!showBankLayer || style == WorldCloudStyle.SoftMistOnly) return;

            bool useSprites = style == WorldCloudStyle.Painterly25D;
            Material mat = useSprites ? spriteBankMaterial : smoothSdfMaterial;
            if (mat == null) return;

            Camera cam = Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : new Vector3(0f, 30f, -30f);
            float t = Application.isPlaying ? Time.time : 0f;

            visibleItems.Clear();

            for (int i = 0; i < bankNodes.Count; i++)
            {
                CloudBankNode node = bankNodes[i];

                // Gentle organic breathing & drift
                float phase = t * node.BobSpeed * Mathf.Max(0.15f, driftSpeed) + node.Phase;
                float driftX = Mathf.Sin(phase * 0.7f) * node.DriftAmp * (driftSpeed > 0.01f ? 1f : 0f);
                float driftZ = Mathf.Cos(phase * 0.55f) * node.DriftAmp * (driftSpeed > 0.01f ? 1f : 0f);
                float bobY = Mathf.Sin(phase) * node.BobAmp;

                Vector3 pos = new Vector3(
                    node.BasePos.x + driftX,
                    baseHeight + node.BasePos.y + bobY,
                    node.BasePos.z + driftZ);

                // Evaluate combined unlock clearing + radial parting across all island zones
                float alpha = 1f;
                float scaleMul = 1f;
                Vector2 pushOffset = Vector2.zero;

                for (int z = 0; z < zones.Count; z++)
                {
                    CloudClearZone zone = zones[z];
                    float openT = SmoothOpenCurve(zone.currentOpen);
                    if (openT <= 0.001f || zone.clearRadius <= 0.01f) continue;

                    float dx = pos.x - zone.worldCenter.x;
                    float dz = pos.z - zone.worldCenter.z;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);

                    float targetR = zone.clearRadius * openT;
                    float fadeWidth = Mathf.Max(5.0f, zone.clearRadius * (edgeSoftness + 0.08f));
                    float puffFootprintPad = Mathf.Min(node.BaseWidth * 0.30f, 5.2f);
                    float clearStart = targetR + puffFootprintPad * 0.35f;
                    float clearEnd = targetR + fadeWidth + puffFootprintPad;

                    float zoneCov = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(clearStart, clearEnd, dist));
                    alpha = Mathf.Min(alpha, zoneCov);

                    // Gently part & shrink puffs near an opening island's perimeter
                    if (dist < clearEnd + 8f && dist > 0.01f)
                    {
                        float proximity = 1f - Mathf.Clamp01((dist - clearStart) / (clearEnd - clearStart + 8f));
                        float push = proximity * openT * 7.5f;
                        pushOffset += new Vector2(dx / dist, dz / dist) * push;
                        scaleMul = Mathf.Min(scaleMul, Mathf.Lerp(1f, 0.62f, proximity * openT));
                    }
                }

                // Soft horizon fade matching Layer 1 world edge
                float distWorld = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(seaCenter.x, seaCenter.z));
                float worldFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(seaExtent * 0.36f, seaExtent * 0.45f, distWorld));
                alpha *= worldFade;

                if (alpha <= 0.02f) continue;

                pos.x += pushOffset.x;
                pos.z += pushOffset.y;

                // Slightly dip clouds hugging an unlocked shoreline so they nestle seamlessly into the mist sea
                pos.y -= (1f - alpha) * 0.95f;

                float w = node.BaseWidth * bankScale * scaleMul;
                float h = node.BaseHeight * bankScale * scaleMul;

                Matrix4x4 mtx;
                Vector4 tileRect = new Vector4(0.5f, 0.5f, 0f, 0f);
                if (useSprites)
                {
                    mtx = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(w, h, 1f));
                    int col = node.Variant & 1;
                    int row = (node.Variant >> 1) & 1;
                    tileRect = new Vector4(node.FlipX ? -0.5f : 0.5f, 0.5f, col * 0.5f, row * 0.5f);
                }
                else
                {
                    // Smooth 3D SDF scale calibrated to overlap seamlessly like a lush 3D cloud carpet
                    float sdfW = w * 0.48f;
                    float sdfH = h * 0.32f;
                    float sdfD = w * 0.44f;
                    mtx = Matrix4x4.TRS(pos + Vector3.up * 0.15f, Quaternion.Euler(0f, node.Yaw + t * 1.2f * driftSpeed, 0f), new Vector3(sdfW, sdfH, sdfD));
                }

                float sqrDist = (pos - camPos).sqrMagnitude;
                visibleItems.Add(new RenderBatchItem
                {
                    Matrix = mtx,
                    TileRect = tileRect,
                    TintAlpha = new Vector4(node.Tint.r, node.Tint.g, node.Tint.b, alpha),
                    SortKey = sqrDist,
                    MeshVariant = node.Variant & 1
                });
            }

            if (visibleItems.Count == 0) return;

            // Back-to-front depth sort for silky artifact-free alpha compositing from every 360° camera angle
            visibleItems.Sort((a, b) => b.SortKey.CompareTo(a.SortKey));
            ActiveBankInstanceCount = visibleItems.Count;

            if (useSprites)
            {
                DrawInstancedBatch(spriteQuadMesh, mat, visibleItems, filterVariant: -1, includeTileRects: true);
            }
            else
            {
                DrawInstancedBatch(sdfMeshA, mat, visibleItems, filterVariant: 0, includeTileRects: false);
                DrawInstancedBatch(sdfMeshB, mat, visibleItems, filterVariant: 1, includeTileRects: false);
            }
        }

        private void DrawInstancedBatch(Mesh mesh, Material mat, List<RenderBatchItem> items, int filterVariant, bool includeTileRects)
        {
            if (mesh == null || mat == null) return;

            int count = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (filterVariant >= 0 && items[i].MeshVariant != filterVariant) continue;

                if (count >= batchMatrices.Length)
                {
                    FlushBatch(mesh, mat, count, includeTileRects);
                    count = 0;
                }

                batchMatrices[count] = items[i].Matrix;
                batchTileRects[count] = items[i].TileRect;
                batchTintAlphas[count] = items[i].TintAlpha;
                count++;
            }

            if (count > 0)
            {
                FlushBatch(mesh, mat, count, includeTileRects);
            }
        }

        private void FlushBatch(Mesh mesh, Material mat, int count, bool includeTileRects)
        {
            bankMpb.Clear();
            if (includeTileRects) bankMpb.SetVectorArray(IdTileRect, batchTileRects);
            bankMpb.SetVectorArray(IdTintAlpha, batchTintAlphas);
            Graphics.DrawMeshInstanced(mesh, 0, mat, batchMatrices, count, bankMpb, ShadowCastingMode.Off, false, gameObject.layer);
        }

        private static float SmoothOpenCurve(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private void CleanupSeaChild(bool isDestroyingParent)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name != SeaChildName) continue;

                child.gameObject.SetActive(false);
                if (!isDestroyingParent)
                {
                    child.name = "__CozyWorldCloudSea_PendingDestroy";
                    child.SetParent(null, false);
                }

                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
            seaTransform = null;
            seaRenderer = null;
        }

        private static void DestroyResource(ref Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
            mesh = null;
        }
    }
}
