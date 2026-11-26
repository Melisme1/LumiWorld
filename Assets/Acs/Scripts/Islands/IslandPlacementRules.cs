using UnityEngine;

namespace LumiWorld.Acs.Islands
{
    public static class IslandPlacementRules
    {
        private static bool terraforming;
        public static bool IsWetNature(CardData card) => card != null && card.cardType == CardType.Terrain && card.islandId == "wet";

        public static bool IsEmptyWetTerraformableTile(CardData card, HexCoordinates hex, GameObject tile, out int elevationLevel, out string reason)
        {
            elevationLevel = -1;
            reason = string.Empty;
            var islands = IslandRuntimeManager.Instance;
            if (card == null || tile == null)
            {
                reason = "Invalid tile.";
                return false;
            }
            if (islands != null)
            {
                string region = islands.IslandAt(hex);
                if (region == null || !islands.IsUnlocked(region) || region != "wet")
                {
                    reason = "Use Raise / Lower Land on an unlocked Wet Island tile.";
                    return false;
                }
            }

            var info = tile.GetComponent<HexTileInfo>();
            if (info == null || info.ElevationLevel < 0 || info.ElevationLevel > 2)
            {
                reason = "This tile cannot be raised or lowered.";
                return false;
            }

            foreach (var placed in tile.GetComponentsInChildren<PlacedCard>(true))
            {
                if (placed.CardType != CardType.Special || placed.cardData.IsRiverCard())
                {
                    reason = "Raise or lower an empty tile first.";
                    return false;
                }
            }

            var disaster = tile.GetComponent<TileDisasterState>();
            if ((disaster != null && (disaster.HasAnyDisaster || disaster.isBurned || disaster.hasObstacle)) ||
                (RiverNetworkManager.Instance != null && RiverNetworkManager.Instance.HasRiver(hex)))
            {
                reason = "Clear the tile before changing its elevation.";
                return false;
            }

            elevationLevel = info.ElevationLevel;
            return true;
        }

        public static bool CanTerraformTile(CardData card, HexCoordinates hex, GameObject tile, int direction, out string reason)
        {
            if (Mathf.Abs(direction) != 1)
            {
                reason = "Invalid terraform direction.";
                return false;
            }

            if (!IsEmptyWetTerraformableTile(card, hex, tile, out int elevationLevel, out reason))
            {
                return false;
            }

            int nextLevel = elevationLevel + direction;
            if (nextLevel < 0 || nextLevel > 2)
            {
                reason = direction > 0
                    ? "Tile is already at High — switch to Lower."
                    : "Tile is already at Low — switch to Raise.";
                return false;
            }

            return true;
        }

        public static bool CanUse(CardData card, HexCoordinates hex, GameObject tile, out string reason)
        {
            reason = string.Empty;
            var islands = IslandRuntimeManager.Instance;
            if (card == null || islands == null) return true;
            string region = islands.IslandAt(hex);
            if (region == null || !islands.IsUnlocked(region) || region != (string.IsNullOrEmpty(card.islandId) ? "starter" : card.islandId))
            { reason = "Use this card on its own unlocked island."; return false; }
            var info = tile.GetComponent<HexTileInfo>();
            if (card.IsTerraformTool() && card.elevationChange != 0)
            {
                if (!CanTerraformTile(card, hex, tile, card.elevationChange, out reason)) return false;
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
            int dir = card != null ? card.elevationChange : 0;
            return TryTerraform(card, hex, world, dir);
        }

        public static bool TryTerraform(CardData card, HexCoordinates hex, HexWorldGenerator world, int directionOverride)
        {
            if (terraforming || WorldLaunch.IsVisit || SaveSync.SavesSuspended ||
                WorldSaveSystem.Instance?.SavesBlocked == true || world == null ||
                card == null || Mathf.Abs(directionOverride) != 1 || !world.MapTiles.TryGetValue(hex, out var oldTile) ||
                !CanTerraformTile(card, hex, oldTile, directionOverride, out _) || CurrencyWallet.Instance == null) return false;
            var info = oldTile.GetComponent<HexTileInfo>();
            int level = info.ElevationLevel + directionOverride;
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
