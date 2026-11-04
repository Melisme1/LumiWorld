using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the state of a single incubation chamber (test tube slot).
/// Tracks which parents are selected, incubation timer, and completion state.
/// Pure data/logic – no Unity UI knowledge.
/// </summary>
[Serializable]
public class BreedingChamberState
{
    public enum Status { Empty, Incubating, Done }

    public Status status = Status.Empty;
    public CardData parentA;
    public CardData parentB;
    public BreedingRecipe recipe;

    // Incubation progress (0–1)
    public float progress;
    public float totalSeconds;
    public float elapsedSeconds;

    public bool IsReady => status == Status.Done;
    public bool IsEmpty => status == Status.Empty;
    public bool IsBusy => status == Status.Incubating;

    public void StartIncubation(CardData a, CardData b, BreedingRecipe r)
    {
        parentA = a;
        parentB = b;
        recipe = r;
        totalSeconds = r != null ? r.incubationSeconds : 120f;
        elapsedSeconds = 0f;
        progress = 0f;
        status = Status.Incubating;
    }

    public void Tick(float deltaSeconds)
    {
        if (status != Status.Incubating) return;
        elapsedSeconds = Mathf.Min(elapsedSeconds + deltaSeconds, totalSeconds);
        progress = totalSeconds > 0f ? elapsedSeconds / totalSeconds : 1f;
        if (progress >= 1f) status = Status.Done;
    }

    public void SpeedUp(float secondsToSkip)
    {
        if (status != Status.Incubating) return;
        Tick(secondsToSkip);
    }

    public void Reset()
    {
        status = Status.Empty;
        parentA = null;
        parentB = null;
        recipe = null;
        progress = 0f;
        totalSeconds = 0f;
        elapsedSeconds = 0f;
    }

    public string FormatTimeRemaining()
    {
        float remaining = Mathf.Max(0f, totalSeconds - elapsedSeconds);
        int mins = Mathf.FloorToInt(remaining / 60f);
        int secs = Mathf.FloorToInt(remaining % 60f);
        if (mins > 0) return $"{mins}m {secs:D2}s";
        return $"{secs}s";
    }
}

/// <summary>
/// Runtime manager for the Creation Lab (Breeding System).
/// Governs 3 incubation chambers, recipe lookup, and reward delivery.
/// Unlocking chambers 2 & 3 is currently gated (future progression feature).
/// </summary>
public class BreedingManager : MonoBehaviour
{
    public static BreedingManager Instance { get; private set; }

    [Header("Recipes")]
    [Tooltip("All breeding recipes available in the game. Drag BreedingRecipe assets here.")]
    [SerializeField] private List<BreedingRecipe> recipes = new List<BreedingRecipe>();

    [Header("Chamber Settings")]
    [Tooltip("Total number of chambers. Currently only chamber 0 is unlocked.")]
    [SerializeField] private int totalChambers = 3;

    [Tooltip("How many chambers are unlocked at game start (1 = only first tube active).")]
    [SerializeField] private int unlockedChambers = 1;

    [Header("Speed-Up Cost")]
    [Tooltip("Coin cost to instantly complete one incubation.")]
    [SerializeField] private int speedUpCoinCost = 50;

    // ---- State ----
    private BreedingChamberState[] chambers;
    private static AnimalSpeciesData s_BraluffSpecies;
    private static CardData s_BraluffCard;

    public static AnimalSpeciesData GetOrCreateBraluffSpecies()
    {
        if (s_BraluffSpecies != null) return s_BraluffSpecies;
        s_BraluffSpecies = ScriptableObject.CreateInstance<AnimalSpeciesData>();
        s_BraluffSpecies.name = "BraluffSpecies";
        s_BraluffSpecies.speciesID = "braluff";
        s_BraluffSpecies.speciesName = "Braluff";
        s_BraluffSpecies.description = "Hybrid fairy deer creature thriving across Leafwood, Bloomfield, and Windheath.";
        s_BraluffSpecies.locomotionType = AnimalLocomotionType.Ground;
        s_BraluffSpecies.baseWalkSpeed = 0.52f;
        s_BraluffSpecies.baseRunSpeed = 1.1f;
        s_BraluffSpecies.personalRadius = 0.38f;
        s_BraluffSpecies.yOffset = 0f;
        s_BraluffSpecies.baseResourceAmount = 3;
        s_BraluffSpecies.baseProductionInterval = 20f;
        return s_BraluffSpecies;
    }

    public static CardData GetOrCreateBraluffCard()
    {
        if (s_BraluffCard == null)
        {
            s_BraluffCard = ScriptableObject.CreateInstance<CardData>();
            s_BraluffCard.name = "Braluff";
        }
        EnsureBraluffCardConfigured(s_BraluffCard);
        return s_BraluffCard;
    }

    public static void EnsureBraluffCardConfigured(CardData card)
    {
        if (card == null) return;
        card.cardID = "creature_braluff";
        card.cardName = "Braluff";
        card.cardType = CardType.Creature;
        card.baseScore = 5;
        card.occupiesTile = true;

        if (card.animalSpeciesData == null)
        {
            card.animalSpeciesData = GetOrCreateBraluffSpecies();
        }
        else
        {
            card.animalSpeciesData.speciesID = "braluff";
            card.animalSpeciesData.speciesName = "Braluff";
        }

        if (card.prefabToPlace == null)
        {
            GameObject prefab = Resources.Load<GameObject>("CreatureModels/Braluff");
#if UNITY_EDITOR
            if (prefab == null)
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/conlai_laithu/Braluff.glb");
            if (prefab == null)
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Resources/CreatureModels/Braluff.glb");
#endif
            card.prefabToPlace = prefab;
        }

        Sprite braluffSprite = CreationLabUI.LoadSpriteSafe("Breeding/Braluff")
                            ?? CreationLabUI.LoadSpriteSafe("CreatureAvatars/17_Braluff")
                            ?? CreationLabUI.LoadSpriteSafe("UI_Cards/17_Braluff");
        if (braluffSprite != null)
        {
            card.cardImage = braluffSprite;
        }
    }

    // ---- Events ----
    public event Action<int> OnChamberStateChanged;   // chamber index
    public event Action<int, CardData> OnBreedingComplete; // chamber index, offspring

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        chambers = new BreedingChamberState[totalChambers];
        for (int i = 0; i < totalChambers; i++)
            chambers[i] = new BreedingChamberState();

        // Auto-load recipes from Resources if list is empty
        if (recipes.Count == 0)
        {
            var loaded = Resources.LoadAll<BreedingRecipe>("Breeding");
            if (loaded != null && loaded.Length > 0)
            {
                recipes.AddRange(loaded);
            }
        }
        EnsureDefaultRecipes();
    }

    /// <summary>
    /// Guarantees that at least the default Bramblet + Petaluff = Braluff recipe exists.
    /// </summary>
    public void EnsureDefaultRecipes()
    {
        if (recipes == null) recipes = new List<BreedingRecipe>();

        bool hasBraluffRecipe = false;
        foreach (var r in recipes)
        {
            if (r != null && r.offspring != null && 
                (r.offspring.cardID == "creature_braluff" || r.offspring.cardName == "Braluff"))
            {
                hasBraluffRecipe = true;
                break;
            }
        }

        if (!hasBraluffRecipe)
        {
            CardData deerCard = Resources.Load<CardData>("Cards/Deer");
            CardData caterCard = Resources.Load<CardData>("Cards/Caterpillar");

            if (deerCard == null || caterCard == null)
            {
                var allCards = Resources.LoadAll<CardData>("");
                foreach (var c in allCards)
                {
                    if (c == null) continue;
                    string id = c.cardID?.ToLowerInvariant() ?? "";
                    string nm = c.cardName?.ToLowerInvariant() ?? "";
                    if (deerCard == null && (id.Contains("deer") || nm.Contains("nai") || id.Contains("bramblet"))) deerCard = c;
                    if (caterCard == null && (id.Contains("caterpillar") || nm.Contains("sâu") || nm.Contains("sau") || id.Contains("petaluff"))) caterCard = c;
                }
            }

            // Create runtime offspring CardData for Braluff if needed
            CardData braluffCard = GetOrCreateBraluffCard();

            var defaultRecipe = ScriptableObject.CreateInstance<BreedingRecipe>();
            defaultRecipe.name = "Recipe_Bramblet_Petaluff_Braluff";
            defaultRecipe.parentA = deerCard;
            defaultRecipe.parentB = caterCard;
            defaultRecipe.offspring = braluffCard;
            defaultRecipe.incubationSeconds = 120f;
            defaultRecipe.offspringBiomeTags = new List<string> { "Leafwood", "Bloomfield", "Windheath" };

            recipes.Add(defaultRecipe);
        }

        // Also ensure any existing Braluff cards in recipes or player hand have Species & Prefab configured
        foreach (var r in recipes)
        {
            if (r != null && r.offspring != null &&
                (r.offspring.cardID == "creature_braluff" || r.offspring.cardName == "Braluff"))
            {
                EnsureBraluffCardConfigured(r.offspring);
            }
        }
        UpgradeExistingBraluffCardsInHand();
    }

    public static void UpgradeExistingBraluffCardsInHand()
    {
        var cardUIs = FindObjectsByType<CardUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (cardUIs == null) return;
        foreach (var cui in cardUIs)
        {
            if (cui == null || cui.CardData == null) continue;
            string id = cui.CardData.cardID ?? "";
            string nm = cui.CardData.cardName ?? "";
            if (id.IndexOf("braluff", StringComparison.OrdinalIgnoreCase) >= 0 ||
                nm.IndexOf("braluff", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                EnsureBraluffCardConfigured(cui.CardData);
                cui.Refresh();
            }
        }
    }

    /// <summary>
    /// Finds a recipe that involves the specified parent card.
    /// Useful for auto-suggesting or previewing offspring when 1 parent is picked.
    /// </summary>
    public BreedingRecipe FindRecipeForParent(CardData parent)
    {
        if (parent == null) return null;
        EnsureDefaultRecipes();
        foreach (var r in recipes)
        {
            if (r != null && r.HasParent(parent)) return r;
        }
        return null;
    }

    private void Update()
    {
        for (int i = 0; i < totalChambers; i++)
        {
            var ch = chambers[i];
            if (!ch.IsBusy) continue;
            ch.Tick(Time.deltaTime);
            OnChamberStateChanged?.Invoke(i);
            if (ch.IsReady)
                NotifyComplete(i);
        }
    }

    // ---- Public API ----

    public int TotalChambers => totalChambers;
    public int UnlockedChambers => unlockedChambers;
    public BreedingChamberState GetChamber(int index) => (index >= 0 && index < chambers.Length) ? chambers[index] : null;

    public bool IsChamberUnlocked(int index) => index >= 0 && index < unlockedChambers;

    /// <summary>
    /// Looks up a recipe matching parentA + parentB.
    /// Returns null if no recipe is found.
    /// </summary>
    public BreedingRecipe FindRecipe(CardData a, CardData b)
    {
        foreach (var r in recipes)
            if (r != null && r.Matches(a, b)) return r;
        return null;
    }

    /// <summary>
    /// Starts incubation in the given chamber.
    /// Returns false if the chamber is locked, busy, or no recipe found.
    /// </summary>
    public bool StartBreeding(int chamberIndex, CardData parentA, CardData parentB)
    {
        if (!IsChamberUnlocked(chamberIndex))
        {
            Debug.LogWarning($"[BreedingManager] Chamber {chamberIndex} is locked.");
            return false;
        }
        var ch = chambers[chamberIndex];
        if (!ch.IsEmpty)
        {
            Debug.LogWarning($"[BreedingManager] Chamber {chamberIndex} is already in use.");
            return false;
        }
        var recipe = FindRecipe(parentA, parentB);
        if (recipe == null)
        {
            Debug.LogWarning($"[BreedingManager] No recipe found for {parentA?.cardName} + {parentB?.cardName}.");
            return false;
        }

        // Consume 1 copy of each parent card from player's hand upon starting breeding
        ConsumeCardFromHand(parentA);
        ConsumeCardFromHand(parentB);

        ch.StartIncubation(parentA, parentB, recipe);
        OnChamberStateChanged?.Invoke(chamberIndex);
        return true;
    }

    private static void ConsumeCardFromHand(CardData targetData)
    {
        if (targetData == null || CardDeskController.Instance == null) return;

        var cardUIs = FindObjectsByType<CardUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (cardUIs == null) return;

        bool matchDeer = CreationLabUI.IsDeer(targetData);
        bool matchCaterpillar = CreationLabUI.IsCaterpillar(targetData);

        foreach (var cui in cardUIs)
        {
            if (cui == null || cui.CardData == null || cui.Count <= 0) continue;

            bool isMatch = (cui.CardData == targetData)
                || (!string.IsNullOrEmpty(targetData.cardID) && string.Equals(cui.CardData.cardID, targetData.cardID, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrEmpty(targetData.cardName) && string.Equals(cui.CardData.cardName, targetData.cardName, StringComparison.OrdinalIgnoreCase))
                || (matchDeer && CreationLabUI.IsDeer(cui.CardData))
                || (matchCaterpillar && CreationLabUI.IsCaterpillar(cui.CardData));

            if (isMatch)
            {
                CardDeskController.Instance.RemoveCard(cui.gameObject);
                return;
            }
        }
    }

    /// <summary>
    /// Spends coins to finish incubation instantly.
    /// Returns false if not enough coins or chamber not incubating.
    /// </summary>
    public bool TrySpeedUpWithCoins(int chamberIndex)
    {
        var ch = GetChamber(chamberIndex);
        if (ch == null || !ch.IsBusy) return false;

        var wallet = CurrencyWallet.Instance;
        if (wallet == null || !wallet.CanAfford(speedUpCoinCost))
        {
            Debug.LogWarning("[BreedingManager] Not enough coins to speed up.");
            return false;
        }

        wallet.TrySpend(speedUpCoinCost, CoinReason.PlacementFee);
        ch.SpeedUp(ch.totalSeconds); // complete immediately
        OnChamberStateChanged?.Invoke(chamberIndex);
        if (ch.IsReady) NotifyComplete(chamberIndex);
        return true;
    }

    /// <summary>
    /// Simulates watching an ad to speed up (skips 30% of remaining time).
    /// Call this after the ad finishes in your ad integration.
    /// </summary>
    public void SpeedUpWithAd(int chamberIndex)
    {
        var ch = GetChamber(chamberIndex);
        if (ch == null || !ch.IsBusy) return;

        float skip = ch.totalSeconds * 0.3f;
        ch.SpeedUp(skip);
        OnChamberStateChanged?.Invoke(chamberIndex);
        if (ch.IsReady) NotifyComplete(chamberIndex);
    }

    /// <summary>
    /// Claims the completed offspring and grants it to the player's hand.
    /// </summary>
    public CardData ClaimOffspring(int chamberIndex)
    {
        var ch = GetChamber(chamberIndex);
        if (ch == null || !ch.IsReady) return null;

        CardData offspring = ch.recipe?.offspring;
        ch.Reset();
        OnChamberStateChanged?.Invoke(chamberIndex);

        if (offspring != null)
        {
            if (offspring.cardID == "creature_braluff" || string.Equals(offspring.cardName, "Braluff", StringComparison.OrdinalIgnoreCase))
            {
                EnsureBraluffCardConfigured(offspring);
            }
            CardDeskController.Instance?.AddRewardCard(offspring);
        }

        return offspring;
    }

    // ---- Helpers ----

    private void NotifyComplete(int chamberIndex)
    {
        var ch = chambers[chamberIndex];
        OnBreedingComplete?.Invoke(chamberIndex, ch.recipe?.offspring);
    }

    public List<BreedingRecipe> GetAllRecipes() => recipes;

    public void SetRecipes(IEnumerable<BreedingRecipe> list)
    {
        recipes.Clear();
        if (list != null) recipes.AddRange(list);
    }

    public void AddRecipe(BreedingRecipe r)
    {
        if (r != null && !recipes.Contains(r)) recipes.Add(r);
    }

    /// <summary>
    /// Returns all recipes that include the given card as a parent.
    /// Used by the selection panel to highlight compatible partners.
    /// </summary>
    public List<BreedingRecipe> GetRecipesFor(CardData card)
    {
        var result = new List<BreedingRecipe>();
        foreach (var r in recipes)
            if (r != null && r.HasParent(card)) result.Add(r);
        return result;
    }
}
