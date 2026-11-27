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

    [Header("Offspring & Multi-tier Probabilities")]
    [Tooltip("Primary Good Hybrid offspring card (e.g. Fernbara)")]
    public CardData offspring;

    [Tooltip("Probability for Good hybrid (e.g. 0.98 = 98%)")]
    [Range(0f, 1f)]
    public float goodRate = 0.98f;

    [Tooltip("Optional Epic offspring card (e.g. Mossgarg - Silver frame)")]
    public CardData epicOffspring;

    [Tooltip("Probability for Epic hybrid (e.g. 0.015 = 1.5%)")]
    [Range(0f, 1f)]
    public float epicRate = 0.015f;

    [Tooltip("Optional Legend offspring card (e.g. Prismagale - Gold frame)")]
    public CardData legendOffspring;

    [Tooltip("Probability for Legend hybrid (e.g. 0.005 = 0.5%)")]
    [Range(0f, 1f)]
    public float legendRate = 0.005f;

    [Tooltip("Base incubation duration in seconds (displayed as days/hours in UI)")]
    public float incubationSeconds = 6300f;

    [Header("Breeding Stats")]
    [Tooltip("Fixed success probability for this recipe (fallback if multi-tier not set)")]
    [Range(0f, 1f)]
    public float successRate = 0.85f;

    [Tooltip("Breeding fee in coins (e.g. 50 coins)")]
    public int feeCoins = 55;

    [Tooltip("Formatted duration text to display in Setup view (e.g. '01h 45m')")]
    public string durationDisplay = "01h 45m";

    public void SyncDuration()
    {
        if (incubationSeconds > 0f && incubationSeconds < 600f)
        {
            incubationSeconds *= 60f;
        }
        int h = Mathf.FloorToInt(incubationSeconds / 3600f);
        int m = Mathf.FloorToInt((incubationSeconds % 3600f) / 60f);
        durationDisplay = $"{h:D2}h {m:D2}m";
    }

    [Header("Biome Tags (auto-derived but can override)")]
    [Tooltip("Biome affinity icons to display on the offspring card. Leave empty to auto-derive from parents.")]
    public List<string> offspringBiomeTags = new List<string>();

    /// <summary>Rolls an offspring based on the multi-tier probabilities.</summary>
    public CardData RollOffspring()
    {
        float roll = Random.value;
        if (legendOffspring != null && roll >= (1f - legendRate))
            return legendOffspring;
        if (epicOffspring != null && roll >= (1f - legendRate - epicRate))
            return epicOffspring;
        return offspring != null ? offspring : (epicOffspring ?? legendOffspring);
    }

    /// <summary>Formats the success rate display string for Setup view (e.g. '85%' or '98% Featherglide').</summary>
    public string GetSuccessRateSummary()
    {
        string goodName = offspring != null ? CreationLabUI.GetCreatureDisplayName(offspring) : "Good";
        float pct = goodRate > 0f ? goodRate * 100f : successRate * 100f;
        if (!string.IsNullOrEmpty(goodName) && goodName != "Good" && goodName != "Braluff")
            return $"<b>{pct:0.#}%</b> {goodName}";
        return $"<b>{pct:0.#}%</b>";
    }

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

        // Common aliases
        if ((id.Contains("capybara") || id.Contains("pebbara")) && (pattern.Contains("capybara") || pattern.Contains("pebbara"))) return true;
        if ((id.Contains("deer") || id.Contains("bramblet") || id.Contains("nai")) && (pattern.Contains("deer") || pattern.Contains("bramblet") || pattern.Contains("nai"))) return true;
        if ((id.Contains("swan") || id.Contains("lotelle") || id.Contains("thien nga")) && (pattern.Contains("swan") || pattern.Contains("lotelle") || pattern.Contains("thien nga"))) return true;
        if ((id.Contains("mudskipper") || id.Contains("rivlet") || id.Contains("thoi loi")) && (pattern.Contains("mudskipper") || pattern.Contains("rivlet") || pattern.Contains("thoi loi"))) return true;
        if ((id.Contains("frog") || id.Contains("reedle") || id.Contains("ech")) && (pattern.Contains("frog") || pattern.Contains("reedle") || pattern.Contains("ech"))) return true;
        if ((id.Contains("highlandcow") || id.Contains("heathorn") || id.Contains("bo long dai")) && (pattern.Contains("highlandcow") || pattern.Contains("heathorn") || pattern.Contains("bo long dai"))) return true;
        if ((id.Contains("caterpillar") || id.Contains("petaluff") || id.Contains("sau") || id.Contains("buom")) && (pattern.Contains("caterpillar") || pattern.Contains("petaluff") || pattern.Contains("sau") || pattern.Contains("buom"))) return true;
        if ((id.Contains("squirrel") || id.Contains("spriglide") || id.Contains("soc")) && (pattern.Contains("squirrel") || pattern.Contains("spriglide") || pattern.Contains("soc"))) return true;
        if ((id.Contains("peacock") || id.Contains("galume") || id.Contains("cong")) && (pattern.Contains("peacock") || pattern.Contains("galume") || pattern.Contains("cong"))) return true;
        if ((id.Contains("dandrift") || id.Contains("flowerdog") || id.Contains("cho hoa") || id.Contains("chó hoa")) && (pattern.Contains("dandrift") || pattern.Contains("flowerdog") || pattern.Contains("cho hoa") || pattern.Contains("chó hoa"))) return true;
        return false;
    }
}
