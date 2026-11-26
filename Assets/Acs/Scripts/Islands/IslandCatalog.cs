using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs.Islands
{
    [Serializable]
    public sealed class IslandDefinition
    {
        public string id;
        public string displayName;
        public HabitatIsland habitatIsland;
        public HexCoordinates center;
        public int expansionRadius;
        public int clusterRadius = 2;
        public int unlockCoins = 200;
        public string prerequisiteIsland = "starter";
        public BiomeAffinity[] discoveryBiomes;
        public int discoveryCreatures = 3;
        public CardData[] startingNature;
        public CardData rainCard;
        public int startingRain = 6;
        public GameObject[] dryTiles;
        public GameObject[] lushTiles;

        public IslandClusterBoundary CreateBoundary() => new IslandClusterBoundary(center, expansionRadius,
            clusterRadius, HexPlacementController.GetSuperHexOffsets(clusterRadius));
    }

    [Serializable]
    public sealed class BiomeVisualSet
    {
        public CardData nature;
        public GameObject single, three, six, ten;
        public GameObject ForSize(int count) => count == 1 ? single : count == 3 ? three : count == 6 ? six : count == 10 ? ten : null;
    }

    [CreateAssetMenu(menuName = "LumiWorld/Islands/Island Catalog")]
    public sealed class IslandCatalog : ScriptableObject
    {
        public List<IslandDefinition> islands = new List<IslandDefinition>();
        public List<BiomeVisualSet> biomeVisuals = new List<BiomeVisualSet>();
        public IslandDefinition Find(string id) => islands.Find(i => i.id == id);
        public BiomeVisualSet Visuals(string cardId) => biomeVisuals.Find(v => v.nature != null && v.nature.cardID == cardId);
    }
}
