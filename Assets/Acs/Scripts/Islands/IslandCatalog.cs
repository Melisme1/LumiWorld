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
        [Tooltip("Allow 1-hex Rain cards on this island. Disable on volcanic, ocean, or other rainless islands.")]
        public bool allowsRain = true;
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

        public bool AllowsRain(string islandId)
        {
            string key = string.IsNullOrEmpty(islandId) ? "starter" : islandId;
            IslandDefinition def = Find(key);
            return def != null && def.allowsRain;
        }

        public GameObject TryResolveRainLushTile(GameObject tileObj)
        {
            if (tileObj == null || islands == null) return null;

            HexTileInfo tileInfo = tileObj.GetComponent<HexTileInfo>();
            GameObject sourcePrefab = tileInfo != null ? tileInfo.sourcePrefab : null;
            string cleanSourceName = sourcePrefab != null ? sourcePrefab.name.Replace("(Clone)", "").Trim() : null;
            string cleanTileName = tileObj.name.Replace("(Clone)", "").Trim();

            for (int i = 0; i < islands.Count; i++)
            {
                IslandDefinition island = islands[i];
                if (island == null || !island.allowsRain || island.dryTiles == null || island.lushTiles == null) continue;

                int pairCount = Mathf.Min(island.dryTiles.Length, island.lushTiles.Length);
                for (int j = 0; j < pairCount; j++)
                {
                    GameObject dryPrefab = island.dryTiles[j];
                    GameObject lushPrefab = island.lushTiles[j];
                    if (dryPrefab == null || lushPrefab == null) continue;

                    if (sourcePrefab != null && sourcePrefab == dryPrefab)
                        return lushPrefab;

                    string cleanDryName = dryPrefab.name.Replace("(Clone)", "").Trim();
                    if (!string.IsNullOrEmpty(cleanSourceName) &&
                        string.Equals(cleanSourceName, cleanDryName, StringComparison.OrdinalIgnoreCase))
                    {
                        return lushPrefab;
                    }

                    if (cleanTileName.IndexOf($"[{cleanDryName}]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cleanTileName.IndexOf(cleanDryName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return lushPrefab;
                    }
                }
            }

            return null;
        }

        public bool IsRainTransformableDryTile(GameObject tileObj) => TryResolveRainLushTile(tileObj) != null;
    }
}
