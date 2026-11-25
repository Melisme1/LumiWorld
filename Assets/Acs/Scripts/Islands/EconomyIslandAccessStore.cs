using System;
using System.Collections.Generic;

namespace LumiWorld.Acs.Islands
{
    /// <summary>Uses the existing account save and wallet; no separate currency or progress file.</summary>
    public sealed class EconomyIslandAccessStore : IIslandAccessStore
    {
        private readonly EconomySaveSystem save;
        private readonly CurrencyWallet wallet;
        private bool busy;

        public EconomyIslandAccessStore(EconomySaveSystem save, CurrencyWallet wallet)
        {
            this.save = save != null ? save : throw new ArgumentNullException(nameof(save));
            this.wallet = wallet != null ? wallet : throw new ArgumentNullException(nameof(wallet));
        }

        private List<IslandAccessRecord> Records => save.Progress.islandAccess ??
            (save.Progress.islandAccess = new List<IslandAccessRecord>());

        public IslandAccessStage Read(string islandId) =>
            Records.Find(r => r != null && r.islandId == islandId)?.stage ?? IslandAccessStage.Hidden;

        public bool TryDiscover(string islandId)
        {
            if (busy || WorldLaunch.IsVisit || Read(islandId) != IslandAccessStage.Hidden) return false;
            busy = true;
            var previous = Records.Find(r => r != null && r.islandId == islandId);
            var record = previous ?? new IslandAccessRecord { islandId = islandId };
            try
            {
                if (previous == null) Records.Add(record);
                record.stage = IslandAccessStage.Discovered;
                save.MarkDirty();
                if (save.SaveNow()) return true;
                record.stage = IslandAccessStage.Hidden;
                if (previous == null) Records.Remove(record);
                save.MarkDirty();
                return false;
            }
            finally { busy = false; }
        }

        public IslandUnlockResult TryPurchase(string islandId, int coins)
        {
            if (coins < 0) throw new ArgumentOutOfRangeException(nameof(coins));
            if (busy) return IslandUnlockResult.Busy;
            var stage = Read(islandId);
            if (stage == IslandAccessStage.Unlocked) return IslandUnlockResult.AlreadyUnlocked;
            if (WorldLaunch.IsVisit || stage != IslandAccessStage.Discovered) return IslandUnlockResult.NotDiscovered;
            if (!wallet.CanAfford(coins)) return IslandUnlockResult.InsufficientCoins;
            busy = true;
            var record = Records.Find(r => r != null && r.islandId == islandId);
            try
            {
                // Set access first so synchronous wallet/save listeners see a coherent transaction.
                record.stage = IslandAccessStage.Unlocked;
                if (!wallet.TrySpend(coins, CoinReason.IslandUnlock))
                {
                    record.stage = IslandAccessStage.Discovered;
                    return IslandUnlockResult.InsufficientCoins;
                }
                save.MarkDirty();
                if (save.SaveNow()) return IslandUnlockResult.Success;
                record.stage = IslandAccessStage.Discovered;
                wallet.Add(coins, CoinReason.IslandUnlock);
                save.MarkDirty();
                return IslandUnlockResult.SaveFailed;
            }
            finally { busy = false; }
        }
    }
}
