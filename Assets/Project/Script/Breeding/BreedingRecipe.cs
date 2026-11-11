using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines a breeding recipe: two parent CardData that produce a specific offspring CardData.
/// Offspring inherits affinities from both parents (union of their biome tags).
/// Created as a ScriptableObject so designers can configure parent/offspring in Inspector.
/// </summary>
[CreateAssetMenu(fileName = "NewBreedingRecipe", menuName = "LumiWorld/Breeding/Breeding Recipe")]
public class BreedingRecipe : ScriptableObject
{
    [Header("Parents (order does not matter)")]
    [Tooltip("First parent creature card")]
    public CardData parentA;

    [Tooltip("Second parent creature card")]
    public CardData parentB;

    [Header("Offspring")]
    [Tooltip("Resulting hybrid creature card")]
    public CardData offspring;

    [Tooltip("Base incubation duration in seconds (displayed as days/hours in UI)")]
    public float incubationSeconds = 120f;

    [Header("Biome Tags (auto-derived but can override)")]
    [Tooltip("Biome affinity icons to display on the offspring card. Leave empty to auto-derive from parents.")]
    public List<string> offspringBiomeTags = new List<string>();

    /// <summary>Returns true if the two provided cards match this recipe (either order).</summary>
    public bool Matches(CardData a, CardData b)
    {
        if (a == null || b == null) return false;
        if ((parentA == a && parentB == b) || (parentA == b && parentB == a)) return true;

        // Fallback match by cardID or cardName
        string idA = a.cardID?.ToLowerInvariant() ?? a.cardName?.ToLowerInvariant() ?? "";
        string idB = b.cardID?.ToLowerInvariant() ?? b.cardName?.ToLowerInvariant() ?? "";
        string pA = parentA?.cardID?.ToLowerInvariant() ?? parentA?.cardName?.ToLowerInvariant() ?? "";
        string pB = parentB?.cardID?.ToLowerInvariant() ?? parentB?.cardName?.ToLowerInvariant() ?? "";

        if (string.IsNullOrEmpty(pA) || string.IsNullOrEmpty(pB)) return false;

        bool matchDirect = (IsCardMatch(idA, pA) && IsCardMatch(idB, pB)) ||
                           (IsCardMatch(idA, pB) && IsCardMatch(idB, pA));
        return matchDirect;
    }

    /// <summary>Returns true if a card is one of this recipe's parents.</summary>
    public bool HasParent(CardData card)
    {
        if (card == null) return false;
        if (parentA == card || parentB == card) return true;

        string id = card.cardID?.ToLowerInvariant() ?? card.cardName?.ToLowerInvariant() ?? "";
        string pA = parentA?.cardID?.ToLowerInvariant() ?? parentA?.cardName?.ToLowerInvariant() ?? "";
        string pB = parentB?.cardID?.ToLowerInvariant() ?? parentB?.cardName?.ToLowerInvariant() ?? "";

        return IsCardMatch(id, pA) || IsCardMatch(id, pB);
    }

    private static bool IsCardMatch(string id, string pattern)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pattern)) return false;
        if (id == pattern) return true;
        if (id.Contains(pattern) || pattern.Contains(id)) return true;
        // Check common aliases (e.g., deer <-> bramblet, caterpillar <-> petaluff)
        if ((id.Contains("deer") || id.Contains("nai")) && (pattern.Contains("deer") || pattern.Contains("bramblet") || pattern.Contains("nai"))) return true;
        if ((id.Contains("caterpillar") || id.Contains("sau") || id.Contains("buom")) && (pattern.Contains("caterpillar") || pattern.Contains("petaluff") || pattern.Contains("sau"))) return true;
        return false;
    }
}
