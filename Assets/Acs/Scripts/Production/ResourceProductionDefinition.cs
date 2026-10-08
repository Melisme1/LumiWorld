using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace LumiWorld.Acs
{
    [Serializable]
    public sealed class SpeciesProductionRequirement
    {
        [Tooltip("Key GDD trong registry: spriglide, frondle, petaluff... Không phải speciesID.")]
        public string gddSpeciesKey;
        public AffinityRole role = AffinityRole.Primary;
        [Min(1)] public int minimumCount = 1;
    }

    [CreateAssetMenu(fileName = "ResourceProduction", menuName = "LumiWorld/Resources/Resource Production Definition")]
    public sealed class ResourceProductionDefinition : ScriptableObject
    {
        public ResourceData resource;
        public ResourceTier tier = ResourceTier.Tier1;
        public BiomeAffinity affinity;
        [FormerlySerializedAs("availableInStage")]
        public bool isEnabled = true;
        [Tooltip("Tất cả requirements là AND; cùng habitat. Không tiêu hao creature.")]
        public List<SpeciesProductionRequirement> requirements = new List<SpeciesProductionRequirement>();

        public string ResourceId => resource != null ? resource.resourceID : string.Empty;
    }
}
