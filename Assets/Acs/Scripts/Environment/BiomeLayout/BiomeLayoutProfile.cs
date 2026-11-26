using System;
using UnityEngine;
namespace LumiWorld.Acs
{
    [Serializable]
    public sealed class BiomePropTier
    {
        public bool enabled = true;
        [Min(0)] public int count;
        public GameObject[] assets = Array.Empty<GameObject>();
        public DioramaSizeLimits size = new DioramaSizeLimits();
    }

    [Serializable]
    public sealed class BiomePropLayer : DioramaRule
    {
        public GameObject[] pool;
        public bool useTierPresets;
        public BiomePropTier hex1 = new BiomePropTier();
        public BiomePropTier hex3 = new BiomePropTier();
        public BiomePropTier hex6 = new BiomePropTier();
        public BiomePropTier hex10 = new BiomePropTier();

        public BiomePropTier GetTier(int hexCount)
        {
            switch (hexCount)
            {
                case 1: return hex1;
                case 3: return hex3;
                case 6: return hex6;
                case 10: return hex10;
                default: throw new ArgumentOutOfRangeException(nameof(hexCount), "Expected 1, 3, 6 or 10 hex.");
            }
        }
    }
    [CreateAssetMenu(menuName="LumiWorld/Biome Layout Profile")]
    public sealed class BiomeLayoutProfile : ScriptableObject
    {
        public string biomeId;
        [TextArea] public string designNotes;
        public Texture2D waterMask1Hex,waterMask3Hex,waterMask6Hex,waterMask10Hex;
        public HexBiomeSurfaceSettings surfaceSettings;
        public float minSwimWaterDepthMeters=.06f, walkableSampleSpacing=.65f, swimmableSampleSpacing=.48f, creatureAgentRadius=.32f;
        public BiomePropLayer[] layers;
        [Min(0)] public float dryCorridorWidth;
        [HideInInspector] public int sizingVersion;
    }
}
