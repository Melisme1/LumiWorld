using System;
using UnityEngine;

namespace LumiWorld.Acs
{
    /// <summary>
    /// Attaches to a multi-hex biome cluster root to hold its BiomeLayoutProfile,
    /// deterministic layout seed, and trigger ecological prop + BiomeActivityZones regeneration.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BiomeLayoutController : MonoBehaviour
    {
        public const string DefaultPropsContainerName = "BiomeProps_Generated";

        [Header("Layout Configuration")]
        [SerializeField] private BiomeLayoutProfile profile;
        [SerializeField] private int clusterSize = 6;
        [SerializeField] private int layoutSeed = 1001;

        [Header("Managed References")]
        [SerializeField] private Transform propsContainer;
        [SerializeField] private BiomeActivityZones activityZones;

        [Header("Last Build Summary")]
        [SerializeField] private int lastPlacedProps;
        [SerializeField] private int lastLockedProps;
        [SerializeField, HideInInspector] private string sourcePrefabAssetPath = "";

        public BiomeLayoutProfile Profile
        {
            get => profile;
            set => profile = value;
        }

        public int ClusterSize
        {
            get => clusterSize;
            set => clusterSize = value;
        }

        public int LayoutSeed
        {
            get => layoutSeed;
            set => layoutSeed = value;
        }

        public Transform PropsContainer => EnsurePropsContainer();
        public BiomeActivityZones ActivityZones => EnsureActivityZones();
        public int LastPlacedProps => lastPlacedProps;
        public int CreatureCapacity => clusterSize < 3 ? 0 : clusterSize <= 3 ? 2 : clusterSize <= 6 ? 4 : 6;
        public int LastLockedProps => lastLockedProps;
        public string SourcePrefabAssetPath
        {
            get => sourcePrefabAssetPath;
            set => sourcePrefabAssetPath = value;
        }

        public void Configure(BiomeLayoutProfile layoutProfile, int size, int seed)
        {
            profile = layoutProfile;
            clusterSize = size;
            layoutSeed = seed;
            EnsurePropsContainer();
            EnsureActivityZones();
        }

        public BiomeLayoutBuildResult Regenerate(
            bool preserveLocked,
            Func<GameObject, Transform, GameObject> instantiator = null,
            BiomeLayoutEditActions editActions = null)
        {
            Transform container = EnsurePropsContainer();
            BiomeActivityZones zones = EnsureActivityZones();

            BiomeLayoutBuildResult result = BiomeLayoutGenerator.Generate(
                biomeRoot: transform,
                propsContainer: container,
                profile: profile,
                clusterSize: clusterSize,
                seed: layoutSeed,
                preserveLocked: preserveLocked,
                activityZones: zones,
                instantiator: instantiator,
                editActions: editActions);

            lastPlacedProps = result.placedPropCount;
            lastLockedProps = result.preservedLockedCount;
            return result;
        }

        private Transform EnsurePropsContainer()
        {
            if (propsContainer == null)
            {
                Transform existing = transform.Find(DefaultPropsContainerName);
                if (existing != null)
                {
                    propsContainer = existing;
                }
                else
                {
                    GameObject go = new GameObject(DefaultPropsContainerName);
                    go.transform.SetParent(transform, false);
                    propsContainer = go.transform;
                }
            }
            return propsContainer;
        }

        private BiomeActivityZones EnsureActivityZones()
        {
            if (activityZones == null)
            {
                activityZones = GetComponent<BiomeActivityZones>();
                if (activityZones == null)
                {
                    activityZones = gameObject.AddComponent<BiomeActivityZones>();
                }
            }
            return activityZones;
        }
    }
}
