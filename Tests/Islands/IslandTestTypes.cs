using System;
using System.Collections.Generic;
using LumiWorld.Acs.Islands;

public static class WorldLaunch
{
    public static bool IsVisit;
}

public enum CoinReason
{
    StarterKit,
    IslandUnlock
}

public sealed class CurrencyWallet
{
    public int Coins { get; private set; }

    public CurrencyWallet(int initialCoins)
    {
        Coins = initialCoins;
    }

    public bool CanAfford(int amount) => amount >= 0 && Coins >= amount;

    public bool TrySpend(int amount, CoinReason reason)
    {
        if (!CanAfford(amount)) return false;
        Coins -= amount;
        return true;
    }

    public void Add(int amount, CoinReason reason)
    {
        if (amount > 0) Coins += amount;
    }
}

public sealed class EconomySaveData
{
    public List<IslandAccessRecord> islandAccess = new List<IslandAccessRecord>();
}

public sealed class EconomySaveSystem
{
    public EconomySaveData Progress { get; } = new EconomySaveData();
    public bool Dirty { get; private set; }
    public bool ShouldFailSave { get; set; }
    public int SaveCalls { get; private set; }

    public void MarkDirty() => Dirty = true;

    public bool SaveNow()
    {
        SaveCalls++;
        if (ShouldFailSave) return false;
        Dirty = false;
        return true;
    }
}
