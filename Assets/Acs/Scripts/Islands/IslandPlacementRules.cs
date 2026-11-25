using UnityEngine;

namespace LumiWorld.Acs.Islands
{
    public static class IslandPlacementRules
    {
        private static bool terraforming;
        public static bool IsWetNature(CardData card) => card != null && card.cardType == CardType.Terrain && card.islandId == "wet";

        public static bool CanUse(CardData card, HexCoordinates hex, GameObject tile, out string reason)
        {
            reason = string.Empty;
            var islands = IslandRuntimeManager.Instance;
            if (card == null || islands == null) return true;
            string region = islands.IslandAt(hex);
            if (region == null || !islands.IsUnlocked(region) || region != (string.IsNullOrEmpty(card.islandId) ? "starter" : card.islandId))
            { reason = "Use this card on its own unlocked island."; return false; }
            var info = tile.GetComponent<HexTileInfo>();
            if (card.elevationChange != 0)
            {
                if (region != "wet" || info == null || info.ElevationLevel < 0 || info.ElevationLevel + card.elevationChange < 0 || info.ElevationLevel + card.elevationChange > 2)
                { reason = "Elevation must stay between Low and High."; return false; }
                foreach (var placed in tile.GetComponentsInChildren<PlacedCard>(true))
                    if (placed.CardType != CardType.Special || placed.cardData.IsRiverCard())
                    { reason = "Raise or lower an empty tile first."; return false; }
                var disaster = tile.GetComponent<TileDisasterState>();
                if ((disaster != null && (disaster.HasAnyDisaster || disaster.isBurned || disaster.hasObstacle)) ||
                    (RiverNetworkManager.Instance != null && RiverNetworkManager.Instance.HasRiver(hex)))
                { reason = "Clear the tile before changing its elevation."; return false; }
            }
            if (IsWetNature(card) && HabitatRuntimeManager.TryGetActive(out var habitat) && habitat.Catalog.TryGetHabitat(card.cardID, out var definition))
            {
                if (info == null || !info.hasLushEnvironment)
                { reason = "Use Rain to prepare this tile first."; return false; }
                if (!WetBiomeFormation.AllowsElevation(definition.affinity, info.ElevationLevel))
                { reason = "This Nature needs a different elevation."; return false; }
            }
            return true;
        }

        public static bool TryTerraform(CardData card, HexCoordinates hex, HexWorldGenerator world)
        {
            if (terraforming || WorldLaunch.IsVisit || SaveSync.SavesSuspended ||
                WorldSaveSystem.Instance?.SavesBlocked == true || world == null ||
                card == null || Mathf.Abs(card.elevationChange) != 1 || !world.MapTiles.TryGetValue(hex, out var oldTile) ||
                !CanUse(card, hex, oldTile, out _) || CurrencyWallet.Instance == null) return false;
            var info = oldTile.GetComponent<HexTileInfo>();
            int level = info.ElevationLevel + card.elevationChange;
            var island = IslandRuntimeManager.Instance.Catalog.Find("wet");
            var prefab = (info.hasLushEnvironment ? island.lushTiles : island.dryTiles)[level];
            if (prefab == null) return false;
            terraforming = true;
            try
            {
                if (!CurrencyWallet.Instance.TrySpend(card.placementFee, CoinReason.PlacementFee)) return false;
                HabitatRuntimeManager.PrepareForWorldChange();
                world.MapTiles.Remove(hex);
                var tile = world.CreateTile(hex, prefab, level, info.hasLushEnvironment, oldTile.transform.rotation);
                if (tile == null)
                {
                    world.MapTiles[hex] = oldTile;
                    CurrencyWallet.Instance.Add(card.placementFee, CoinReason.PlacementFee);
                    return false;
                }
                var previous = oldTile.GetComponent<TileDisasterState>();
                if (previous != null)
                {
                    var state = TileDisasterState.Ensure(tile);
                    state.aridity = previous.aridity;
                    state.hasReceivedWater = previous.hasReceivedWater;
                    state.affinity = previous.affinity;
                }
                foreach (var record in oldTile.GetComponentsInChildren<PlacedCard>(true))
                    record.transform.SetParent(tile.transform, false);
                oldTile.SetActive(false);
                Object.Destroy(oldTile);
                HabitatRuntimeManager.NotifyWorldChanged();
                WorldSaveSystem.MarkDirty();
                return true;
            }
            finally { terraforming = false; }
        }
    }
}
