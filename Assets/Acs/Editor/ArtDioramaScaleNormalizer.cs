#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LumiWorld.EditorTools
{
    /// <summary>
    /// Infers and normalizes world-space diorama scales (and off-center multi-mesh pivots)
    /// across Assets/Acs/Art so every prefab spawns at a harmonious size relative to
    /// LumiWorld hex clusters (1 hex = 2.0m, 3/6/10-hex = 4/6/8m) and real creatures
    /// (small 0.18-0.30m, medium median 0.36m, flight altitude 1.1-1.4m).
    ///
    /// Strategy:
    /// - Single-model prefabs (1-to-1 OBJ/FBX): folds root scale overrides + target scale
    ///   into ModelImporter.globalScale and normalizes prefab root localScale to (1,1,1)
    ///   (preserving intentional non-uniform aspect ratios on legacy Z-up FBXs).
    /// - Shared multi-mesh FBXs (e.g. ST-PaCK.fbx, Rocks.fbx): picks a geometric-mean
    ///   shared globalScale, applies per-prefab localScale, and auto-centers off-origin
    ///   mesh pivots so rocks spawn cleanly on the cursor resting at Y = 0.
    /// - Preserves 100% of .meta GUIDs and Prefab Variant links; idempotent on repeat runs.
    /// </summary>
    public static class ArtDioramaScaleNormalizer
    {
        // --- LumiWorld Real-World Diorama Anchors (verified against HexMetrics & CreatureModels) ---
        public const float HexFlatWidthMeters = 2.0f;
        public const float Cluster3HexWidthMeters = 4.0f;
        public const float Cluster6HexWidthMeters = 6.0f;
        public const float Cluster10HexWidthMeters = 8.0f;
        public const float SmallCreatureHeightMeters = 0.25f;   // BlossomFrog 0.27m, FishFrog 0.18m, Parrot 0.29m
        public const float MedianCreatureHeightMeters = 0.36f;  // Capybara 0.36m, Otter 0.35m, Deer 0.36m
        public const float FlightAltitudeMeters = 1.25f;

        private const float IdempotencyMinRatio = 0.94f;
        private const float IdempotencyMaxRatio = 1.06f;

        private static readonly HashSet<string> NonPropCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Ground",
            "UI",
            "Test",
            "Forest",
            "_StyleArchive"
        };

        private enum ScaleMetricAxis
        {
            HeightY,
            FootprintXZ,
            MaxDim
        }

        private readonly struct ArchetypeSpec
        {
            public readonly string Id;
            public readonly ScaleMetricAxis Axis;
            public readonly float TargetMin;
            public readonly float TargetMax;
            public readonly float CapH;
            public readonly float CapXZ;
            public readonly float FloorMaxDim;

            public ArchetypeSpec(
                string id,
                ScaleMetricAxis axis,
                float targetMin,
                float targetMax,
                float capH,
                float capXZ,
                float floorMaxDim)
            {
                Id = id;
                Axis = axis;
                TargetMin = targetMin;
                TargetMax = targetMax;
                CapH = capH;
                CapXZ = capXZ;
                FloorMaxDim = floorMaxDim;
            }
        }

        private static readonly Dictionary<string, ArchetypeSpec> Archetypes = new Dictionary<string, ArchetypeSpec>(StringComparer.OrdinalIgnoreCase)
        {
            // Ground cover / Grass (ankle-to-belly height of Capybara; never swallows small frogs)
            { "Grass_Mat",           new ArchetypeSpec("Grass_Mat",           ScaleMetricAxis.FootprintXZ, 0.42f, 0.78f, 0.36f, 0.85f, 0.26f) },
            { "Grass_Tuft",          new ArchetypeSpec("Grass_Tuft",          ScaleMetricAxis.HeightY,     0.18f, 0.32f, 0.38f, 0.62f, 0.20f) },
            { "Grass_TallClump",     new ArchetypeSpec("Grass_TallClump",     ScaleMetricAxis.HeightY,     0.35f, 0.56f, 0.62f, 0.82f, 0.32f) },

            // Lotus & Aquatic Pads (2-4 pads compose a natural 1.8m pond; blooms rise gently above water)
            { "Lotus_Pad",           new ArchetypeSpec("Lotus_Pad",           ScaleMetricAxis.FootprintXZ, 0.46f, 0.84f, 0.36f, 0.94f, 0.34f) },
            { "Lotus_Cluster",       new ArchetypeSpec("Lotus_Cluster",       ScaleMetricAxis.HeightY,     0.34f, 0.56f, 0.62f, 0.92f, 0.36f) },

            // Flowers (carpets, wildflowers at creature waist/back height, tall accent blooms)
            { "Flower_Carpet",       new ArchetypeSpec("Flower_Carpet",       ScaleMetricAxis.FootprintXZ, 0.48f, 0.78f, 0.32f, 0.88f, 0.32f) },
            { "Flower_Wild",         new ArchetypeSpec("Flower_Wild",         ScaleMetricAxis.HeightY,     0.22f, 0.42f, 0.46f, 0.58f, 0.18f) },
            { "Flower_Tall",         new ArchetypeSpec("Flower_Tall",         ScaleMetricAxis.HeightY,     0.40f, 0.60f, 0.66f, 0.78f, 0.32f) },

            // Ferns (half-hex readable understory; creatures weave around/behind leaves)
            { "Fern_Standard",       new ArchetypeSpec("Fern_Standard",       ScaleMetricAxis.FootprintXZ, 0.54f, 0.94f, 0.56f, 1.05f, 0.38f) },
            { "Fern_Tall",           new ArchetypeSpec("Fern_Tall",           ScaleMetricAxis.HeightY,     0.42f, 0.66f, 0.72f, 1.02f, 0.40f) },

            // Reeds & Cattails (vertical wetland accent 2-3x creature height, below flight altitude)
            { "Reed_Clump",          new ArchetypeSpec("Reed_Clump",          ScaleMetricAxis.HeightY,     0.72f, 1.15f, 1.24f, 1.05f, 0.55f) },

            // Tropical Understory Broadleaf & Jungle Shrubs
            { "Tropical_Sprout",     new ArchetypeSpec("Tropical_Sprout",     ScaleMetricAxis.FootprintXZ, 0.32f, 0.52f, 0.32f, 0.58f, 0.24f) },
            { "Tropical_Understory", new ArchetypeSpec("Tropical_Understory", ScaleMetricAxis.MaxDim,      0.54f, 0.94f, 0.82f, 1.04f, 0.42f) },
            { "Tropical_TallShrub",  new ArchetypeSpec("Tropical_TallShrub",  ScaleMetricAxis.HeightY,     0.72f, 1.12f, 1.20f, 1.22f, 0.56f) },

            // Vines (split hanging curtains vs horizontal creeping ledges)
            { "Vine_Hanging",        new ArchetypeSpec("Vine_Hanging",        ScaleMetricAxis.HeightY,     0.78f, 1.42f, 1.52f, 1.05f, 0.48f) },
            { "Vine_Creeping",       new ArchetypeSpec("Vine_Creeping",       ScaleMetricAxis.FootprintXZ, 0.65f, 1.10f, 0.52f, 1.18f, 0.45f) },

            // Trees & Palms (4 clear diorama canopy strata)
            { "Tree_DwarfPalm",      new ArchetypeSpec("Tree_DwarfPalm",      ScaleMetricAxis.FootprintXZ, 1.05f, 1.55f, 1.15f, 1.65f, 0.90f) },
            { "Tree_Sapling",        new ArchetypeSpec("Tree_Sapling",        ScaleMetricAxis.HeightY,     1.35f, 1.85f, 1.95f, 1.50f, 1.10f) },
            { "Tree_MidPalm",        new ArchetypeSpec("Tree_MidPalm",        ScaleMetricAxis.HeightY,     1.80f, 2.45f, 2.58f, 2.05f, 1.45f) },
            { "Tree_HeroCanopy",     new ArchetypeSpec("Tree_HeroCanopy",     ScaleMetricAxis.HeightY,     2.55f, 3.05f, 3.20f, 2.75f, 2.20f) },

            // Rocks, Boulders, Runic Stones & Headwater Cliffs
            { "Stone_Pebble",        new ArchetypeSpec("Stone_Pebble",        ScaleMetricAxis.FootprintXZ, 0.22f, 0.38f, 0.28f, 0.42f, 0.16f) },
            { "Stone_Medium",        new ArchetypeSpec("Stone_Medium",        ScaleMetricAxis.FootprintXZ, 0.42f, 0.72f, 0.56f, 0.80f, 0.30f) },
            { "Stone_Large",         new ArchetypeSpec("Stone_Large",         ScaleMetricAxis.MaxDim,      0.82f, 1.18f, 1.12f, 1.22f, 0.65f) },
            { "Stone_Cliff",         new ArchetypeSpec("Stone_Cliff",         ScaleMetricAxis.HeightY,     1.40f, 1.75f, 1.85f, 1.45f, 1.20f) },

            // Wood & Mushroom
            { "Wood_Log",            new ArchetypeSpec("Wood_Log",            ScaleMetricAxis.FootprintXZ, 1.48f, 1.70f, 0.68f, 1.78f, 1.20f) },
            { "Mushroom_Cluster",    new ArchetypeSpec("Mushroom_Cluster",    ScaleMetricAxis.FootprintXZ, 0.26f, 0.42f, 0.26f, 0.46f, 0.18f) },
        };

        private sealed class PrefabScaleRecord
        {
            public string Category;
            public string PrefabPath;
            public string PrefabName;
            public string SourceModelPath;
            public bool IsPrefabVariant;
            public bool IsSharedMultiMeshModel;

            public Vector3 CurrentBoundsSize;
            public Vector3 CurrentBoundsCenter;
            public float CurrentBoundsMinY;
            public Vector3 CurrentRootScale;
            public Vector3 CurrentRootPos;
            public Vector3 NativeModelRootScale;
            public float CurrentModelGlobalScale;

            public ArchetypeSpec Archetype;
            public string PackGroupKey;
            public float DesiredMultiplier;
            public Vector3 TargetBoundsSize;
            public bool NeedsScaleUpdate;
            public bool NeedsTransformCleanup;
            public bool NeedsPivotRecenter;
        }

        [MenuItem("LumiWorld/Assets/Art Normalizer/4. Normalize Diorama Scales & Pivots (All Assets/Acs/Art)")]
        public static void NormalizeAllArtDioramaScales()
        {
            RunScaleNormalization(ArtAssetNormalizer.DefaultArtRoot, selectedCategoryOnly: null, dryRun: false);
        }

        [MenuItem("LumiWorld/Assets/Art Normalizer/5. Normalize Diorama Scales (Selected Category Folder)")]
        public static void NormalizeSelectedCategoryDioramaScales()
        {
            string selected = GetSelectedCategoryFolder();
            if (string.IsNullOrEmpty(selected))
            {
                Debug.LogWarning("[ArtDioramaScaleNormalizer] Please select a category folder (or its Models/Prefabs subfolder) under Assets/Acs/Art.");
                return;
            }
            string catName = Path.GetFileName(selected);
            RunScaleNormalization(ArtAssetNormalizer.DefaultArtRoot, selectedCategoryOnly: catName, dryRun: false);
        }

        [MenuItem("LumiWorld/Assets/Art Normalizer/6. Preview Diorama Scale Report (Dry Run -> Logs CSV)")]
        public static void PreviewDioramaScaleReport()
        {
            RunScaleNormalization(ArtAssetNormalizer.DefaultArtRoot, selectedCategoryOnly: null, dryRun: true);
        }

        /// <summary>
        /// Checks a prefab's rendered bounds against its inferred LumiWorld diorama archetype tolerance.
        /// Used by ArtAssetNormalizer.AuditAllArtAssets().
        /// </summary>
        public static bool CheckAgainstDioramaTolerance(string category, string prefabName, Vector3 boundsSize, out string issue)
        {
            issue = null;
            float h = Mathf.Max(boundsSize.y, 0.0001f);
            float xz = Mathf.Max(boundsSize.x, boundsSize.z, 0.0001f);
            float maxDim = Mathf.Max(h, xz);

            ArchetypeSpec spec = ClassifyArchetype(category, prefabName, h, xz);

            if (maxDim < spec.FloorMaxDim * 0.65f)
            {
                issue = $"Too small for {spec.Id}: {boundsSize.x:F2}x{boundsSize.y:F2}x{boundsSize.z:F2}m (floor>={spec.FloorMaxDim:F2}m)";
                return false;
            }
            if (h > spec.CapH * 1.45f || xz > spec.CapXZ * 1.45f)
            {
                issue = $"Oversized for {spec.Id}: {boundsSize.x:F2}x{boundsSize.y:F2}x{boundsSize.z:F2}m (cap H<={spec.CapH:F2}m, XZ<={spec.CapXZ:F2}m)";
                return false;
            }
            return true;
        }

        public static void RunScaleNormalization(string artRoot, string selectedCategoryOnly, bool dryRun)
        {
            if (!AssetDatabase.IsValidFolder(artRoot))
            {
                Debug.LogWarning($"[ArtDioramaScaleNormalizer] Folder not found: {artRoot}");
                return;
            }

            List<PrefabScaleRecord> records = MeasureAllPrefabs(artRoot, selectedCategoryOnly);
            if (records.Count == 0)
            {
                Debug.LogWarning("[ArtDioramaScaleNormalizer] No valid prop prefabs found to analyze.");
                return;
            }

            InferTargetScales(records);
            string csvPath = WriteCsvReport(records, dryRun);

            if (dryRun)
            {
                int wouldChange = 0;
                foreach (var r in records)
                {
                    if (r.NeedsScaleUpdate || r.NeedsTransformCleanup || r.NeedsPivotRecenter) wouldChange++;
                }
                Debug.Log(
                    $"[ArtDioramaScaleNormalizer] DRY RUN complete: scanned {records.Count} prefabs, " +
                    $"{wouldChange} would be normalized (0 files modified). Full table written to: {csvPath}");
                return;
            }

            ApplyNormalizedScalesAndPivots(records, csvPath);
        }

        private static List<PrefabScaleRecord> MeasureAllPrefabs(string artRoot, string selectedCategoryOnly)
        {
            var records = new List<PrefabScaleRecord>(512);
            var modelUsageCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            string[] categoryFolders = AssetDatabase.GetSubFolders(artRoot);
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (string catFolder in categoryFolders)
                {
                    string catName = Path.GetFileName(catFolder);
                    if (NonPropCategories.Contains(catName)) continue;
                    if (!string.IsNullOrEmpty(selectedCategoryOnly) &&
                        !string.Equals(catName, selectedCategoryOnly, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string prefabRoot = catFolder + "/Prefabs";
                    if (!AssetDatabase.IsValidFolder(prefabRoot)) continue;

                    string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabRoot });
                    for (int i = 0; i < guids.Length; i++)
                    {
                        string prefabPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                        GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                        if (pf == null) continue;

                        if (i % 15 == 0)
                        {
                            EditorUtility.DisplayProgressBar(
                                "Diorama Scale Normalizer (1/3: Measuring)",
                                $"{catName}: {pf.name}",
                                (float)i / Mathf.Max(1, guids.Length));
                        }

                        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, preview);
                        if (inst == null) continue;

                        try
                        {
                            // Measure bounds at the prefab's own authored localPosition so we can also detect off-origin pivots
                            if (!TryComputePrimaryRenderBounds(inst, out Bounds bounds))
                            {
                                continue;
                            }

                            string sourceModelPath = ResolveSourceModelPath(pf, inst);
                            if (!string.IsNullOrEmpty(sourceModelPath))
                            {
                                modelUsageCounts[sourceModelPath] = modelUsageCounts.TryGetValue(sourceModelPath, out int c) ? c + 1 : 1;
                            }

                            float currentGlobalScale = 1.0f;
                            Vector3 nativeModelScale = Vector3.one;
                            if (!string.IsNullOrEmpty(sourceModelPath))
                            {
                                var mi = AssetImporter.GetAtPath(sourceModelPath) as ModelImporter;
                                if (mi != null) currentGlobalScale = mi.globalScale;

                                GameObject modelGo = AssetDatabase.LoadAssetAtPath<GameObject>(sourceModelPath);
                                if (modelGo != null) nativeModelScale = modelGo.transform.localScale;
                            }

                            var rec = new PrefabScaleRecord
                            {
                                Category = catName,
                                PrefabPath = prefabPath,
                                PrefabName = pf.name,
                                SourceModelPath = sourceModelPath,
                                IsPrefabVariant = PrefabUtility.IsPartOfVariantPrefab(pf),
                                CurrentBoundsSize = bounds.size,
                                CurrentBoundsCenter = bounds.center,
                                CurrentBoundsMinY = bounds.min.y,
                                CurrentRootScale = pf.transform.localScale,
                                CurrentRootPos = pf.transform.localPosition,
                                NativeModelRootScale = nativeModelScale,
                                CurrentModelGlobalScale = currentGlobalScale
                            };
                            records.Add(rec);
                        }
                        finally
                        {
                            UnityEngine.Object.DestroyImmediate(inst);
                        }
                    }
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                EditorUtility.ClearProgressBar();
            }

            // Detect shared multi-mesh models across the ENTIRE Art library (even when normalizing one category)
            if (!string.IsNullOrEmpty(selectedCategoryOnly))
            {
                var globalCounts = CountModelUsageAcrossRoot(artRoot);
                foreach (var r in records)
                {
                    if (!string.IsNullOrEmpty(r.SourceModelPath) &&
                        globalCounts.TryGetValue(r.SourceModelPath, out int cnt))
                    {
                        r.IsSharedMultiMeshModel = cnt > 1;
                    }
                }
            }
            else
            {
                foreach (var r in records)
                {
                    if (!string.IsNullOrEmpty(r.SourceModelPath) &&
                        modelUsageCounts.TryGetValue(r.SourceModelPath, out int cnt))
                    {
                        r.IsSharedMultiMeshModel = cnt > 1;
                    }
                }
            }

            return records;
        }

        private static Dictionary<string, int> CountModelUsageAcrossRoot(string artRoot)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { artRoot });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (pf == null) continue;
                string modelPath = ResolveSourceModelPath(pf, pf);
                if (!string.IsNullOrEmpty(modelPath))
                {
                    counts[modelPath] = counts.TryGetValue(modelPath, out int c) ? c + 1 : 1;
                }
            }
            return counts;
        }

        private static void InferTargetScales(List<PrefabScaleRecord> records)
        {
            var groups = new Dictionary<string, List<PrefabScaleRecord>>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in records)
            {
                float h = Mathf.Max(r.CurrentBoundsSize.y, 0.0001f);
                float xz = Mathf.Max(r.CurrentBoundsSize.x, r.CurrentBoundsSize.z, 0.0001f);
                r.Archetype = ClassifyArchetype(r.Category, r.PrefabName, h, xz);
                r.PackGroupKey = BuildPackGroupKey(r.Category, r.PrefabName) + "|" + r.Archetype.Id;

                if (!groups.TryGetValue(r.PackGroupKey, out var list))
                {
                    list = new List<PrefabScaleRecord>();
                    groups[r.PackGroupKey] = list;
                }
                list.Add(r);
            }

            foreach (var kv in groups)
            {
                List<PrefabScaleRecord> items = kv.Value;
                ArchetypeSpec spec = items[0].Archetype;

                float minMetric = float.MaxValue;
                float maxMetric = float.MinValue;
                for (int i = 0; i < items.Count; i++)
                {
                    float m = GetPrimaryMetric(items[i].CurrentBoundsSize, spec.Axis);
                    if (m < minMetric) minMetric = m;
                    if (m > maxMetric) maxMetric = m;
                }

                bool allInsidePrimaryBand = (minMetric >= spec.TargetMin && maxMetric <= spec.TargetMax);
                float geoMid = Mathf.Sqrt(spec.TargetMin * spec.TargetMax);

                for (int i = 0; i < items.Count; i++)
                {
                    PrefabScaleRecord r = items[i];
                    float h = Mathf.Max(r.CurrentBoundsSize.y, 0.0001f);
                    float xz = Mathf.Max(r.CurrentBoundsSize.x, r.CurrentBoundsSize.z, 0.0001f);
                    float curMetric = GetPrimaryMetric(r.CurrentBoundsSize, spec.Axis);

                    float targetMetric;
                    if (allInsidePrimaryBand)
                    {
                        targetMetric = curMetric;
                    }
                    else if (items.Count == 1 || maxMetric <= minMetric * 1.02f)
                    {
                        targetMetric = geoMid;
                    }
                    else
                    {
                        // Smooth log-space interpolation preserving natural intra-pack size variation
                        float logSpan = Mathf.Max(0.0001f, Mathf.Log(maxMetric) - Mathf.Log(minMetric));
                        float t = Mathf.Clamp01((Mathf.Log(curMetric) - Mathf.Log(minMetric)) / logSpan);

                        float naturalRatio = maxMetric / Mathf.Max(0.0001f, minMetric);
                        float targetBandRatio = spec.TargetMax / Mathf.Max(0.0001f, spec.TargetMin);
                        float effRatio = Mathf.Min(naturalRatio, targetBandRatio);

                        float effLo = Mathf.Max(spec.TargetMin, geoMid / Mathf.Sqrt(effRatio));
                        float effHi = Mathf.Min(spec.TargetMax, geoMid * Mathf.Sqrt(effRatio));
                        targetMetric = effLo * Mathf.Pow(effHi / Mathf.Max(0.0001f, effLo), t);
                    }

                    float mult = targetMetric / curMetric;

                    // 2D Diorama Envelope Guard (prevents wide mats or skinny stalks from exploding)
                    if (h * mult > spec.CapH)
                    {
                        mult = Mathf.Min(mult, spec.CapH / h);
                    }
                    if (xz * mult > spec.CapXZ)
                    {
                        mult = Mathf.Min(mult, spec.CapXZ / xz);
                    }

                    // Visibility Floor Guard (prevents tiny sprouts from vanishing)
                    float curMaxDim = Mathf.Max(h, xz);
                    if (curMaxDim * mult < spec.FloorMaxDim)
                    {
                        mult = spec.FloorMaxDim / curMaxDim;
                    }

                    // Check whether root transform needs cleanup (non-unit override scale or baked off-origin root position)
                    bool rootScaleDirty = !r.IsSharedMultiMeshModel && HasDirtyRootScaleOverride(r.CurrentRootScale, r.NativeModelRootScale);
                    bool rootPosDirty = r.CurrentRootPos.sqrMagnitude > 0.0004f;

                    bool insideDeadband = (mult >= IdempotencyMinRatio && mult <= IdempotencyMaxRatio);
                    if (insideDeadband && !rootScaleDirty)
                    {
                        mult = 1.0f;
                    }

                    r.DesiredMultiplier = mult;
                    r.TargetBoundsSize = r.CurrentBoundsSize * mult;
                    r.NeedsScaleUpdate = !Mathf.Approximately(mult, 1.0f);
                    r.NeedsTransformCleanup = rootScaleDirty || rootPosDirty;
                    r.NeedsPivotRecenter = rootPosDirty;
                }
            }
        }

        private static void ApplyNormalizedScalesAndPivots(List<PrefabScaleRecord> records, string csvPath)
        {
            var singleModelUpdates = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            var prefabScaleCleanup = new List<(PrefabScaleRecord rec, Vector3 targetRootScale, bool zeroPos)>();
            var sharedModelGroups = new Dictionary<string, List<PrefabScaleRecord>>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in records)
            {
                if (string.IsNullOrEmpty(r.SourceModelPath)) continue;

                if (r.IsSharedMultiMeshModel)
                {
                    if (!sharedModelGroups.TryGetValue(r.SourceModelPath, out var list))
                    {
                        list = new List<PrefabScaleRecord>();
                        sharedModelGroups[r.SourceModelPath] = list;
                    }
                    list.Add(r);
                    continue;
                }

                if (!r.NeedsScaleUpdate && !r.NeedsTransformCleanup) continue;

                // Fold root scale override (e.g. 0.1 on Grass7, or base 0.2 on Pine_Tree01) into ModelImporter.globalScale
                Vector3 ov = SafeDivide(r.CurrentRootScale, r.NativeModelRootScale);
                float baseUniform = Mathf.Max(Mathf.Abs(ov.x), Mathf.Max(Mathf.Abs(ov.y), Mathf.Abs(ov.z)));
                if (baseUniform < 0.0001f) baseUniform = 1.0f;

                float newGlobalScale = Mathf.Clamp(r.CurrentModelGlobalScale * baseUniform * r.DesiredMultiplier, 0.0005f, 1000.0f);
                singleModelUpdates[r.SourceModelPath] = newGlobalScale;

                Vector3 cleanedRootScale = new Vector3(
                    r.CurrentRootScale.x / baseUniform,
                    r.CurrentRootScale.y / baseUniform,
                    r.CurrentRootScale.z / baseUniform);
                // Snap near-1 components to exact 1.0f
                if (Mathf.Abs(cleanedRootScale.x - 1f) < 0.005f) cleanedRootScale.x = 1f;
                if (Mathf.Abs(cleanedRootScale.y - 1f) < 0.005f) cleanedRootScale.y = 1f;
                if (Mathf.Abs(cleanedRootScale.z - 1f) < 0.005f) cleanedRootScale.z = 1f;

                if (r.NeedsTransformCleanup || (r.CurrentRootScale - cleanedRootScale).sqrMagnitude > 0.00001f)
                {
                    prefabScaleCleanup.Add((r, cleanedRootScale, zeroPos: true));
                }
            }

            // Compute optimal shared globalScale for multi-mesh FBXs (ST-PaCK.fbx, Free Pack - Rocks Stylized.fbx)
            var sharedModelRatioMap = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in sharedModelGroups)
            {
                string modelPath = kv.Key;
                List<PrefabScaleRecord> items = kv.Value;

                bool anyChange = false;
                double logSum = 0.0;
                int cnt = 0;
                foreach (var r in items)
                {
                    if (r.NeedsScaleUpdate || r.NeedsPivotRecenter) anyChange = true;
                    float curRoot = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(r.CurrentRootScale.x), Mathf.Max(Mathf.Abs(r.CurrentRootScale.y), Mathf.Abs(r.CurrentRootScale.z))));
                    float idealTotalFromModel = curRoot * r.DesiredMultiplier;
                    logSum += Math.Log(Math.Max(0.0001, idealTotalFromModel));
                    cnt++;
                }

                if (!anyChange || cnt == 0) continue;

                float geoMeanFactor = (float)Math.Exp(logSum / cnt);
                float oldGlobal = items[0].CurrentModelGlobalScale;
                float newGlobal = Mathf.Clamp(oldGlobal * geoMeanFactor, 0.0005f, 1000.0f);
                float appliedModelRatio = newGlobal / Mathf.Max(0.00001f, oldGlobal);

                // Only reimport shared FBX if ratio is meaningfully different from 1.0
                if (appliedModelRatio < 0.96f || appliedModelRatio > 1.04f)
                {
                    singleModelUpdates[modelPath] = newGlobal;
                    sharedModelRatioMap[modelPath] = appliedModelRatio;
                }
                else
                {
                    sharedModelRatioMap[modelPath] = 1.0f;
                }
            }

            int updatedModels = 0;
            int updatedPrefabs = 0;

            try
            {
                // Step 1: Batch-update ModelImporter.globalScale across all models needing scale adjustment
                AssetDatabase.StartAssetEditing();
                try
                {
                    int idx = 0;
                    int total = singleModelUpdates.Count;
                    foreach (var kv in singleModelUpdates)
                    {
                        if (idx % 10 == 0)
                        {
                            EditorUtility.DisplayProgressBar(
                                "Diorama Scale Normalizer (2/3: Reimporting Models)",
                                $"Updating globalScale ({idx + 1}/{total}): {Path.GetFileName(kv.Key)}",
                                (float)idx / Mathf.Max(1, total));
                        }

                        var mi = AssetImporter.GetAtPath(kv.Key) as ModelImporter;
                        if (mi != null && Mathf.Abs(mi.globalScale - kv.Value) > 0.00005f)
                        {
                            mi.globalScale = kv.Value;
                            mi.SaveAndReimport();
                            updatedModels++;
                        }
                        idx++;
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                AssetDatabase.Refresh();

                // Step 2: Clean root Transform (localScale -> 1, localPosition -> 0) on single-model prefabs
                for (int i = 0; i < prefabScaleCleanup.Count; i++)
                {
                    var item = prefabScaleCleanup[i];
                    if (i % 10 == 0)
                    {
                        EditorUtility.DisplayProgressBar(
                            "Diorama Scale Normalizer (3/3: Updating Prefabs)",
                            $"Cleaning root transform ({i + 1}/{prefabScaleCleanup.Count}): {item.rec.PrefabName}",
                            (float)i / Mathf.Max(1, prefabScaleCleanup.Count));
                    }

                    GameObject contents = PrefabUtility.LoadPrefabContents(item.rec.PrefabPath);
                    if (contents == null) continue;
                    try
                    {
                        if (item.zeroPos) contents.transform.localPosition = Vector3.zero;
                        contents.transform.localScale = item.targetRootScale;
                        PrefabUtility.SaveAsPrefabAsset(contents, item.rec.PrefabPath);
                        updatedPrefabs++;
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(contents);
                    }
                }

                // Step 3: Update shared multi-mesh FBX prefabs (individual localScale + zero off-origin root position)
                foreach (var kv in sharedModelGroups)
                {
                    if (!sharedModelRatioMap.TryGetValue(kv.Key, out float modelRatio)) continue;
                    foreach (var r in kv.Value)
                    {
                        float prefabScaleFactor = r.DesiredMultiplier / Mathf.Max(0.0001f, modelRatio);
                        Vector3 targetScale = r.CurrentRootScale * prefabScaleFactor;
                        bool scaleDiff = (targetScale - r.CurrentRootScale).sqrMagnitude > 0.00001f;
                        if (!scaleDiff && !r.NeedsTransformCleanup) continue;

                        GameObject contents = PrefabUtility.LoadPrefabContents(r.PrefabPath);
                        if (contents == null) continue;
                        try
                        {
                            contents.transform.localPosition = Vector3.zero;
                            contents.transform.localScale = targetScale;
                            PrefabUtility.SaveAsPrefabAsset(contents, r.PrefabPath);
                            updatedPrefabs++;
                        }
                        finally
                        {
                            PrefabUtility.UnloadPrefabContents(contents);
                        }
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log(
                $"[ArtDioramaScaleNormalizer] Complete! Analyzed {records.Count} prefabs -> " +
                $"updated {updatedModels} ModelImporter globalScales and {updatedPrefabs} Prefab transforms/pivots. " +
                $"Detailed before/after report: {csvPath}");
        }

        private static ArchetypeSpec ClassifyArchetype(string category, string prefabName, float h, float xz)
        {
            string cat = (category ?? "").Trim();
            string nl = (prefabName ?? "").ToLowerInvariant();
            float aspect = h / Mathf.Max(xz, 0.0001f);

            if (cat.Equals("Grass", StringComparison.OrdinalIgnoreCase))
            {
                if (nl.Contains("bushfern") || nl.Contains("shoregrass") || nl.Contains("weep")) return Archetypes["Grass_TallClump"];
                if (nl.Contains("clover") || nl.Contains("grasspatch") || nl.Contains("sedge") || aspect < 0.38f) return Archetypes["Grass_Mat"];
                if (h > 0.70f && aspect >= 0.55f) return Archetypes["Grass_TallClump"];
                return Archetypes["Grass_Tuft"];
            }

            if (cat.Equals("Lotus", StringComparison.OrdinalIgnoreCase))
            {
                if (nl.Contains("lilypad") || nl.Contains("paleleaf") || nl.Contains("single") ||
                    nl.Contains("cloverpatch") || aspect < 0.36f ||
                    nl == "waterplant_4" || nl == "waterplant_5" || nl == "waterplant_6")
                {
                    return Archetypes["Lotus_Pad"];
                }
                return Archetypes["Lotus_Cluster"];
            }

            if (cat.Equals("Flower", StringComparison.OrdinalIgnoreCase))
            {
                if (nl.Contains("waterlilypad") || aspect < 0.35f)
                {
                    return Archetypes["Flower_Carpet"];
                }
                if (nl.Contains("sunflower") || nl.Contains("reedcattail") ||
                    nl.Contains("flowerbush") || nl.Contains("flower_bush") ||
                    nl.Contains("cloverpatch") || nl.Contains("iris"))
                {
                    return Archetypes["Flower_Tall"];
                }
                if (nl.StartsWith("paliya_flower_") && TryExtractTrailingInt(nl, out int paliyaIdx) && paliyaIdx >= 15)
                {
                    return Archetypes["Flower_Tall"];
                }
                return Archetypes["Flower_Wild"];
            }

            if (cat.Equals("Fern", StringComparison.OrdinalIgnoreCase))
            {
                if (nl.Contains("shuttlecock") || nl.Contains("ferntuft") || aspect >= 0.78f)
                {
                    return Archetypes["Fern_Tall"];
                }
                return Archetypes["Fern_Standard"];
            }

            if (cat.Equals("Reed", StringComparison.OrdinalIgnoreCase))
            {
                return Archetypes["Reed_Clump"];
            }

            if (cat.Equals("Tropical", StringComparison.OrdinalIgnoreCase))
            {
                if (nl.Contains("sprout") || nl == "plant1" || nl == "plant2")
                {
                    return Archetypes["Tropical_Sprout"];
                }
                if (nl.Contains("giantshrub") || nl.Contains("bananabush") || nl.Contains("tropicalpalm") ||
                    nl.Contains("jungleshrub") || nl.Contains("calathea") || nl.Contains("monstera") ||
                    nl.Contains("anthurium") || nl.Contains("ovalgreenlarge") ||
                    (aspect >= 0.85f && h > 1.2f))
                {
                    return Archetypes["Tropical_TallShrub"];
                }
                return Archetypes["Tropical_Understory"];
            }

            if (cat.Equals("Vine", StringComparison.OrdinalIgnoreCase))
            {
                return aspect < 0.55f ? Archetypes["Vine_Creeping"] : Archetypes["Vine_Hanging"];
            }

            if (cat.Equals("Tree", StringComparison.OrdinalIgnoreCase))
            {
                if (nl.Contains("ceiba") || nl.Contains("mangrove") || nl.Contains("hero"))
                {
                    return Archetypes["Tree_HeroCanopy"];
                }
                if (nl.Contains("tropicalpack") || aspect < 0.72f)
                {
                    return Archetypes["Tree_DwarfPalm"];
                }
                if (nl.Contains("pine") || nl.Contains("groundfern") || nl.Contains("settropic_fanpalm"))
                {
                    return Archetypes["Tree_Sapling"];
                }
                return Archetypes["Tree_MidPalm"];
            }

            if (cat.Equals("Stone", StringComparison.OrdinalIgnoreCase) ||
                cat.Equals("Rock", StringComparison.OrdinalIgnoreCase) ||
                cat.Equals("Rocks", StringComparison.OrdinalIgnoreCase))
            {
                if (nl.Contains("cliffrock") || nl.Contains("cliff")) return Archetypes["Stone_Cliff"];
                if (nl.Contains("big") || nl.Contains("runic")) return Archetypes["Stone_Large"];
                if (nl.Contains("small") || nl.Contains("pebble")) return Archetypes["Stone_Pebble"];
                if (nl.Contains("riverrock_p1") || nl.Contains("riverrock_p2")) return Archetypes["Stone_Pebble"];
                return Archetypes["Stone_Medium"];
            }

            if (cat.Equals("Wood", StringComparison.OrdinalIgnoreCase) ||
                cat.Equals("Log", StringComparison.OrdinalIgnoreCase))
            {
                return Archetypes["Wood_Log"];
            }

            if (cat.Equals("Mushroom", StringComparison.OrdinalIgnoreCase))
            {
                return Archetypes["Mushroom_Cluster"];
            }

            return Archetypes["Tropical_Understory"];
        }

        private static float GetPrimaryMetric(Vector3 size, ScaleMetricAxis axis)
        {
            switch (axis)
            {
                case ScaleMetricAxis.HeightY:
                    return Mathf.Max(size.y, 0.0001f);
                case ScaleMetricAxis.FootprintXZ:
                    return Mathf.Max(size.x, size.z, 0.0001f);
                default:
                    return Mathf.Max(size.x, Mathf.Max(size.y, size.z), 0.0001f);
            }
        }

        private static string BuildPackGroupKey(string category, string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return category;
            if (prefabName.Contains("_"))
            {
                string[] parts = prefabName.Split('_');
                if (parts.Length >= 2 &&
                    !Regex.IsMatch(parts[1], @"^\d+$") &&
                    !Regex.IsMatch(parts[1], @"^P\d+$", RegexOptions.IgnoreCase))
                {
                    return $"{category}:{parts[0]}_{parts[1]}";
                }
                return $"{category}:{parts[0]}";
            }
            return $"{category}:{Regex.Replace(prefabName, @"\d+$", "")}";
        }

        private static bool TryExtractTrailingInt(string s, out int value)
        {
            value = 0;
            Match m = Regex.Match(s, @"(\d+)$");
            return m.Success && int.TryParse(m.Groups[1].Value, out value);
        }

        private static bool TryComputePrimaryRenderBounds(GameObject root, out Bounds combined)
        {
            return LumiWorld.Acs.PrefabRenderMetrics.TryMeasureWorld(root, out combined);
        }

        private static string ResolveSourceModelPath(GameObject prefabAsset, GameObject instanceOrAsset)
        {
            UnityEngine.Object orig = PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefabAsset);
            if (orig != null)
            {
                string p = AssetDatabase.GetAssetPath(orig);
                if (IsModelFilePath(p)) return p;
            }

            MeshFilter[] filters = instanceOrAsset.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i] != null && filters[i].sharedMesh != null)
                {
                    string p = AssetDatabase.GetAssetPath(filters[i].sharedMesh);
                    if (IsModelFilePath(p)) return p;
                }
            }
            return "";
        }

        private static bool IsModelFilePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasDirtyRootScaleOverride(Vector3 curRootScale, Vector3 nativeModelScale)
        {
            Vector3 ov = SafeDivide(curRootScale, nativeModelScale);
            float maxAbs = Mathf.Max(Mathf.Abs(ov.x), Mathf.Max(Mathf.Abs(ov.y), Mathf.Abs(ov.z)));
            return Mathf.Abs(maxAbs - 1.0f) > 0.01f;
        }

        private static Vector3 SafeDivide(Vector3 a, Vector3 b)
        {
            return new Vector3(
                Mathf.Abs(b.x) > 0.0001f ? a.x / b.x : a.x,
                Mathf.Abs(b.y) > 0.0001f ? a.y / b.y : a.y,
                Mathf.Abs(b.z) > 0.0001f ? a.z / b.z : a.z);
        }

        private static string GetSelectedCategoryFolder()
        {
            UnityEngine.Object active = Selection.activeObject;
            if (active == null) return null;
            string path = AssetDatabase.GetAssetPath(active);
            if (string.IsNullOrEmpty(path)) return null;
            string folder = AssetDatabase.IsValidFolder(path) ? path : Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder)) return null;

            string leaf = Path.GetFileName(folder);
            if (leaf.Equals("Models", StringComparison.OrdinalIgnoreCase) ||
                leaf.Equals("Textures", StringComparison.OrdinalIgnoreCase) ||
                leaf.Equals("Materials", StringComparison.OrdinalIgnoreCase) ||
                leaf.Equals("Prefabs", StringComparison.OrdinalIgnoreCase))
            {
                folder = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            }

            if (string.IsNullOrEmpty(folder) ||
                !folder.StartsWith(ArtAssetNormalizer.DefaultArtRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            return folder;
        }

        private static string WriteCsvReport(List<PrefabScaleRecord> records, bool dryRun)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? ".";
            string logsDir = Path.Combine(projectRoot, "Logs");
            Directory.CreateDirectory(logsDir);
            string fileName = dryRun ? "art_scale_report_dryrun.csv" : "art_scale_report.csv";
            string fullPath = Path.Combine(logsDir, fileName);

            var sb = new StringBuilder(records.Count * 140);
            sb.AppendLine("Category,PrefabName,Archetype,SharedMultiMesh,BeforeW,BeforeH,BeforeD,Mult,AfterW,AfterH,AfterD,OldRootScale,OldGlobalScale,Changed");
            CultureInfo inv = CultureInfo.InvariantCulture;

            foreach (var r in records)
            {
                bool changed = r.NeedsScaleUpdate || r.NeedsTransformCleanup || r.NeedsPivotRecenter;
                sb.AppendFormat(
                    inv,
                    "{0},{1},{2},{3},{4:F3},{5:F3},{6:F3},{7:F3},{8:F3},{9:F3},{10:F3},{11:F3},{12:F4},{13}\n",
                    r.Category,
                    r.PrefabName,
                    r.Archetype.Id,
                    r.IsSharedMultiMeshModel ? "YES" : "NO",
                    r.CurrentBoundsSize.x,
                    r.CurrentBoundsSize.y,
                    r.CurrentBoundsSize.z,
                    r.DesiredMultiplier,
                    r.TargetBoundsSize.x,
                    r.TargetBoundsSize.y,
                    r.TargetBoundsSize.z,
                    r.CurrentRootScale.x,
                    r.CurrentModelGlobalScale,
                    changed ? "YES" : "NO");
            }

            File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
            return fullPath;
        }
    }
}
#endif
