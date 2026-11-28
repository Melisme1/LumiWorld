using System;
using System.Collections.Generic;
using UnityEngine;

public enum BuildingUpgradeMethod
{
    ClusterExpansion = 0,
    DirectUpgrade = 1
}

public enum BuildingFunctionKind
{
    None = 0,
    AutoFeeding = 1
}

/// <summary>
/// Internal 3D layout preset for multi-tile / leveled Building procedural meshes.
/// Visual-only; does not affect gameplay rules or UI display.
/// </summary>
public enum BuildingVisualPreset
{
    Standard = 0,
    Market = 1,
    Production = 2,
    LandmarkCore = 3,
    WatchPost = 4
}

/// <summary>
/// Pure C# rules for the unified Building system (BuildingTypeId + UpgradeMethod + Level).
/// Shared across placement, cluster management, aura calculation, Auto-Feed, and UI.
/// </summary>
public static class BuildingRules
{
    public const string GranaryPostTypeId = "granary_post";
    public const string GranaryPostDisplayName = "Granary Post";
    public const int GranaryPostMaxLevel = 5;

    private static readonly int[] GranaryAuraByLevel = { 1, 1, 2, 2, 3 };
    private static readonly int[] GranaryFoodSlotsByLevel = { 1, 2, 2, 3, 3 };

    public static int GetLevelForClusterSize(int tileCount, int maxLevel)
    {
        int cap = Mathf.Max(1, maxLevel);
        return Mathf.Clamp(Mathf.Max(1, tileCount), 1, cap);
    }

    public static int GetAuraRadius(string buildingTypeId, int level)
    {
        if (string.Equals(buildingTypeId, GranaryPostTypeId, StringComparison.OrdinalIgnoreCase))
        {
            int idx = Mathf.Clamp(level, 1, GranaryAuraByLevel.Length) - 1;
            return GranaryAuraByLevel[idx];
        }

        // Default fallback for future building types until their own curve is configured
        return Mathf.Clamp(level, 1, 3);
    }

    public static int GetUnlockedFoodSlots(string buildingTypeId, int level)
    {
        if (string.Equals(buildingTypeId, GranaryPostTypeId, StringComparison.OrdinalIgnoreCase))
        {
            int idx = Mathf.Clamp(level, 1, GranaryFoodSlotsByLevel.Length) - 1;
            return GranaryFoodSlotsByLevel[idx];
        }

        return 0;
    }

    public static int GetRequiredLevelForFoodSlot(int slotIndex)
    {
        switch (slotIndex)
        {
            case 0: return 1;
            case 1: return 2;
            case 2: return 4;
            default: return 1;
        }
    }

    /// <summary>
    /// Builds a deterministic cluster key from the building type and the minimum (Q, R) hex in the cluster.
    /// </summary>
    public static string BuildClusterKey(string buildingTypeId, IReadOnlyList<HexCoordinates> hexes)
    {
        string typeKey = string.IsNullOrWhiteSpace(buildingTypeId) ? GranaryPostTypeId : buildingTypeId.Trim().ToLowerInvariant();
        if (hexes == null || hexes.Count == 0) return typeKey + ":0,0";

        HexCoordinates anchor = hexes[0];
        for (int i = 1; i < hexes.Count; i++)
        {
            HexCoordinates h = hexes[i];
            if (h.Q < anchor.Q || (h.Q == anchor.Q && h.R < anchor.R))
            {
                anchor = h;
            }
        }

        return $"{typeKey}:{anchor.Q},{anchor.R}";
    }
}
