using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs.Islands
{
    /// <summary>
    /// Dedicated adapter syncing IslandCatalog + IslandRuntimeManager (or HexWorldGenerator in World Visiting mode)
    /// into CozyWorldCloudSystem. Keeps cloud rendering completely decoupled from UI and save persistence.
    /// Automatically computes exact max land radii and world sea bounds for all islands defined in IslandCatalog (up to 8).
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class IslandWorldCloudBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private IslandCatalog catalog;
        [SerializeField] private CozyWorldCloudSystem cloudSystem;

        [Header("Coverage Calibration (Measured from IslandClusterBoundary)")]
        [Tooltip("Extra clear ocean margin beyond each island's outermost buildable hex so mists never obstruct max-expanded tiles.")]
        [SerializeField, Range(2f, 16f)] private float shorelinePadding = 7.5f;

        [Tooltip("Soft shoreline transition width ratio (0.22 at 38.5m radius = ~8.5m smooth fade, keeping locked neighboring islands 100% covered).")]
        [SerializeField, Range(0.12f, 0.40f)] private float worldEdgeSoftness = 0.22f;

        [Tooltip("Vertical elevation of the world cloud sea layer.")]
        [SerializeField, Range(1.0f, 5.0f)] private float cloudBaseHeight = 3.0f;

        [Tooltip("Horizontal cloud sea padding beyond the outermost island clear radius.")]
        [SerializeField, Range(90f, 220f)] private float outerSeaMargin = 140f;

        [Tooltip("Cloud bank visual style used in the main gameplay scene.")]
        [SerializeField] private WorldCloudStyle defaultStyle = WorldCloudStyle.Painterly25D;

        private IslandRuntimeManager runtimeManager;
        private readonly Dictionary<string, IslandClusterBoundary> islandBoundaries = new Dictionary<string, IslandClusterBoundary>();
        private bool subscribed;

        public IslandCatalog Catalog => catalog;
        public CozyWorldCloudSystem CloudSystem => cloudSystem;
        public float ShorelinePadding => shorelinePadding;

        public void Configure(IslandCatalog islandCatalog, CozyWorldCloudSystem system)
        {
            catalog = islandCatalog;
            cloudSystem = system;
        }

        /// <summary>
        /// Pure calculation of exact island clear zones and enclosing sea bounds from IslandCatalog.
        /// Used by both Editor scene wiring and runtime initialization so radii are never hardcoded.
        /// </summary>
        public static void BuildCatalogLayout(
            IslandCatalog islandCatalog,
            float padding,
            float seaMargin,
            out Vector3 seaCenter,
            out float seaExtent,
            out List<CloudClearZone> zones)
        {
            zones = new List<CloudClearZone>();
            if (islandCatalog == null || islandCatalog.islands == null || islandCatalog.islands.Count == 0)
            {
                seaCenter = Vector3.zero;
                seaExtent = 360f;
                return;
            }

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < islandCatalog.islands.Count; i++)
            {
                IslandDefinition def = islandCatalog.islands[i];
                if (def == null) continue;

                Vector3 center = HexMetrics.HexToWorldPosition(def.center, 0f);
                float maxLandRadius = MeasureMaxLandRadius(def, center);
                float clearRadius = maxLandRadius + Mathf.Max(0f, padding);
                bool isStarter = string.Equals(def.id, "starter", StringComparison.OrdinalIgnoreCase);

                zones.Add(new CloudClearZone
                {
                    id = def.id,
                    displayName = string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName,
                    worldCenter = center,
                    clearRadius = clearRadius,
                    isUnlocked = isStarter,
                    currentOpen = isStarter ? 1f : 0f
                });

                minX = Mathf.Min(minX, center.x - clearRadius);
                maxX = Mathf.Max(maxX, center.x + clearRadius);
                minZ = Mathf.Min(minZ, center.z - clearRadius);
                maxZ = Mathf.Max(maxZ, center.z + clearRadius);
            }

            seaCenter = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            float spanX = (maxX - minX) + seaMargin * 2f;
            float spanZ = (maxZ - minZ) + seaMargin * 2f;
            seaExtent = Mathf.Max(360f, Mathf.Max(spanX, spanZ));
        }

        public static float MeasureMaxLandRadius(IslandDefinition def, Vector3 centerWorld)
        {
            if (def == null) return 24f;
            IslandClusterBoundary boundary = def.CreateBoundary();
            float maxDist = 0f;
            foreach (HexCoordinates hex in boundary.LandHexes)
            {
                Vector3 hexWorld = HexMetrics.HexToWorldPosition(hex, 0f);
                float dist = Vector3.Distance(centerWorld, hexWorld) + HexMetrics.OuterRadius;
                if (dist > maxDist) maxDist = dist;
            }
            return maxDist > 0.1f ? maxDist : 24f;
        }

        private IEnumerator Start()
        {
            if (cloudSystem == null) cloudSystem = GetComponent<CozyWorldCloudSystem>();
            if (cloudSystem == null) cloudSystem = FindAnyObjectByType<CozyWorldCloudSystem>();
            if (cloudSystem == null) yield break;

            if (catalog == null)
            {
                IslandRuntimeManager mgr = IslandRuntimeManager.Instance ?? FindAnyObjectByType<IslandRuntimeManager>();
                if (mgr != null) catalog = mgr.Catalog;
            }
            if (catalog == null || catalog.islands == null || catalog.islands.Count == 0) yield break;

            // Rebuild exact boundaries & world layout from current IslandCatalog
            islandBoundaries.Clear();
            for (int i = 0; i < catalog.islands.Count; i++)
            {
                IslandDefinition def = catalog.islands[i];
                if (def != null && !string.IsNullOrEmpty(def.id))
                {
                    islandBoundaries[def.id] = def.CreateBoundary();
                }
            }

            BuildCatalogLayout(catalog, shorelinePadding, outerSeaMargin, out Vector3 seaCenter, out float seaExtent, out List<CloudClearZone> zones);
            cloudSystem.ConfigureSystem(null, null, null, seaCenter, seaExtent, zones);
            cloudSystem.SetLiveTuning(cloudBaseHeight, worldEdgeSoftness, cloudSystem.BankScale, cloudSystem.DriftSpeed);
            cloudSystem.SetStyle(defaultStyle);

            // Wait for saved world & economy state to finish restoring before locking in initial open/locked states
            while (WorldSaveSystem.Instance != null && !WorldSaveSystem.Instance.IsWorldReady)
            {
                yield return null;
            }

            if (WorldLaunch.IsVisit)
            {
                // One extra frame so HexWorldGenerator finishes populating restored tiles
                yield return null;
                SyncFromVisitedWorldTiles();
                yield break;
            }

            // Own world: wait for IslandRuntimeManager to bind IslandAccessService after IsWorldReady
            while (IslandRuntimeManager.Instance == null)
            {
                yield return null;
            }
            yield return null;

            runtimeManager = IslandRuntimeManager.Instance;
            SyncFromRuntimeManager(instant: true);

            runtimeManager.AccessChanged += OnIslandAccessChanged;
            subscribed = true;
        }

        private void OnDestroy()
        {
            if (subscribed && runtimeManager != null)
            {
                runtimeManager.AccessChanged -= OnIslandAccessChanged;
                subscribed = false;
            }
        }

        private void OnIslandAccessChanged(string islandId)
        {
            if (cloudSystem == null || runtimeManager == null || string.IsNullOrEmpty(islandId)) return;
            bool unlocked = runtimeManager.IsUnlocked(islandId);
            cloudSystem.SetZoneUnlocked(islandId, unlocked, instant: false);
        }

        public void SyncFromRuntimeManager(bool instant)
        {
            if (cloudSystem == null || runtimeManager == null || catalog == null) return;
            for (int i = 0; i < catalog.islands.Count; i++)
            {
                IslandDefinition def = catalog.islands[i];
                if (def == null) continue;
                cloudSystem.SetZoneUnlocked(def.id, runtimeManager.IsUnlocked(def.id), instant: instant);
            }
        }

        /// <summary>
        /// World Visiting mode (read-only): clear every island that actually contains land tiles in the visited save,
        /// and keep unbuilt/locked island regions seamlessly blanketed under the world cloud sea.
        /// </summary>
        public void SyncFromVisitedWorldTiles()
        {
            if (cloudSystem == null || catalog == null) return;
            HexWorldGenerator world = FindAnyObjectByType<HexWorldGenerator>();

            for (int i = 0; i < catalog.islands.Count; i++)
            {
                IslandDefinition def = catalog.islands[i];
                if (def == null) continue;

                bool hasLand = string.Equals(def.id, "starter", StringComparison.OrdinalIgnoreCase);
                if (!hasLand && world != null && world.MapTiles != null &&
                    islandBoundaries.TryGetValue(def.id, out IslandClusterBoundary boundary))
                {
                    foreach (HexCoordinates hex in world.MapTiles.Keys)
                    {
                        if (boundary.AllowsLandHex(hex))
                        {
                            hasLand = true;
                            break;
                        }
                    }
                }

                cloudSystem.SetZoneUnlocked(def.id, hasLand, instant: true);
            }
        }
    }
}
