using System;
using System.Collections.Generic;

namespace LumiWorld.Acs.Islands
{
    public enum IslandAccessStage { Hidden, Discovered, Unlocked }
    public enum IslandUnlockResult { Success, AlreadyUnlocked, NotDiscovered, InsufficientCoins, SaveFailed, Busy }

    [Serializable]
    public sealed class IslandAccessRecord
    {
        public string islandId;
        public IslandAccessStage stage;
        public string startingNatureId;
        public bool initialHandGranted;
        public bool initialLandCreated;
    }

    // The adapter must commit access and coins together, or leave both unchanged.
    public interface IIslandAccessStore
    {
        IslandAccessStage Read(string islandId);
        bool TryDiscover(string islandId);
        IslandUnlockResult TryPurchase(string islandId, int coins);
    }

    /// <summary>Discovery is permanent; purchase is explicit and never triggered by the camera.</summary>
    public sealed class IslandAccessService
    {
        private readonly IIslandAccessStore store;
        private readonly HashSet<string> operations = new HashSet<string>(StringComparer.Ordinal);
        public event Action<string, IslandAccessStage> Changed;

        public IslandAccessService(IIslandAccessStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public IslandAccessStage GetStage(string islandId)
        {
            RequireId(islandId);
            return store.Read(islandId);
        }

        public bool Discover(string islandId)
        {
            RequireId(islandId);
            if (!operations.Add(islandId)) return false;
            bool changed;
            try { changed = store.Read(islandId) == IslandAccessStage.Hidden && store.TryDiscover(islandId); }
            finally { operations.Remove(islandId); }
            if (changed) Changed?.Invoke(islandId, IslandAccessStage.Discovered);
            return changed;
        }

        public IslandUnlockResult Unlock(string islandId, int coins)
        {
            RequireId(islandId);
            if (coins < 0) throw new ArgumentOutOfRangeException(nameof(coins));
            if (!operations.Add(islandId)) return IslandUnlockResult.Busy;
            IslandUnlockResult result;
            try
            {
                var stage = store.Read(islandId);
                result = stage == IslandAccessStage.Unlocked ? IslandUnlockResult.AlreadyUnlocked
                    : stage == IslandAccessStage.Hidden ? IslandUnlockResult.NotDiscovered
                    : store.TryPurchase(islandId, coins);
            }
            finally { operations.Remove(islandId); }
            if (result == IslandUnlockResult.Success) Changed?.Invoke(islandId, IslandAccessStage.Unlocked);
            return result;
        }

        private static void RequireId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An island needs a stable ID.", nameof(id));
        }
    }
}
