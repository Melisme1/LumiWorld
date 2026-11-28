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
    public CardData rolledOffspring;

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
        if (r != null)
        {
            if (r.incubationSeconds > 0f && r.incubationSeconds < 600f)
            {
                r.incubationSeconds *= 60f;
            }
            int h = Mathf.FloorToInt(r.incubationSeconds / 3600f);
            int m = Mathf.FloorToInt((r.incubationSeconds % 3600f) / 60f);
            r.durationDisplay = $"{h:D2}h {m:D2}m";
            totalSeconds = r.incubationSeconds;
        }
        else
        {
            totalSeconds = 6300f; // 01h 45m default
        }
        elapsedSeconds = 0f;
        progress = 0f;
        status = Status.Incubating;
        rolledOffspring = r != null ? r.RollOffspring() : null;
    }

    public void Tick(float deltaSeconds)
    {
        if (status != Status.Incubating) return;

        // Auto-heal active incubation if it was started with legacy test seconds (< 600s)
        if (totalSeconds > 0f && totalSeconds < 600f)
        {
            if (recipe != null)
            {
                if (recipe.incubationSeconds < 600f) recipe.incubationSeconds *= 60f;
                totalSeconds = recipe.incubationSeconds;
            }
            else
            {
                totalSeconds = 6300f; // 01h 45m default
            }
            elapsedSeconds = progress * totalSeconds;
        }

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
        rolledOffspring = null;
        progress = 0f;
        totalSeconds = 0f;
        elapsedSeconds = 0f;
    }

    public string FormatTimeRemaining()
    {
        float remaining = Mathf.Max(0f, totalSeconds - elapsedSeconds);
        int hours = Mathf.FloorToInt(remaining / 3600f);
        int mins = Mathf.FloorToInt((remaining % 3600f) / 60f);
        int secs = Mathf.FloorToInt(remaining % 60f);
        if (hours > 0) return $"{hours:D2}h {mins:D2}m {secs:D2}s";
        if (mins > 0) return $"{mins:D2}m {secs:D2}s";
        return $"{secs:D2}s";
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

    [Tooltip("How many chambers are unlocked at game start (3 = all three tubes active).")]
    [SerializeField] private int unlockedChambers = 3;

    [Header("Speed-Up Cost")]
    [Tooltip("Coin cost to instantly complete one incubation.")]
    [SerializeField] private int speedUpCoinCost = 300;

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
        card.islandId = "starter";
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

    public static GameObject LoadPrefabSafe(string path)
    {
        var go = Resources.Load<GameObject>(path);
#if UNITY_EDITOR
        if (go == null) go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#endif
        return go;
    }

    private static readonly Dictionary<string, AnimalSpeciesData> s_HybridSpeciesCache = new Dictionary<string, AnimalSpeciesData>(StringComparer.OrdinalIgnoreCase);

    public static AnimalSpeciesData GetOrCreateHybridSpecies(string speciesKey, string displayName, AnimalLocomotionType loco = AnimalLocomotionType.Ground)
    {
        if (string.IsNullOrEmpty(speciesKey)) return null;
        string lk = speciesKey.ToLowerInvariant().Replace("creature_", "").Trim();
        if (s_HybridSpeciesCache.TryGetValue(lk, out var existing) && existing != null)
            return existing;

        var sp = ScriptableObject.CreateInstance<AnimalSpeciesData>();
        sp.name = displayName ?? speciesKey;
        sp.speciesID = lk;
        sp.speciesName = displayName ?? speciesKey;
        sp.description = $"{displayName} hybrid creature.";
        sp.locomotionType = loco;
        sp.baseWalkSpeed = 0.52f;
        sp.baseRunSpeed = 1.1f;
        sp.personalRadius = 0.38f;
        s_HybridSpeciesCache[lk] = sp;
        return sp;
    }

    public static void EnsureCardFullyConfigured(CardData card)
    {
        if (card == null) return;
        string id = (card.cardID ?? "").ToLowerInvariant();
        string nm = (card.cardName ?? card.name ?? "").ToLowerInvariant();

        if (id.Contains("braluff") || nm.Contains("braluff"))
        {
            EnsureBraluffCardConfigured(card);
            return;
        }

        card.cardType = CardType.Creature;
        card.occupiesTile = true;
        if (card.allowedTilePrefabs == null) card.allowedTilePrefabs = new List<GameObject>();

        if (id.Contains("fernbara") || nm.Contains("fernbara"))
        {
            card.cardID = "creature_fernbara";
            card.cardName = "Fernbara";
            card.rarity = CardRarity.Common;
            card.islandId = "wet";
            card.baseScore = 6;
            if (card.animalSpeciesData == null) card.animalSpeciesData = GetOrCreateHybridSpecies("fernbara", "Fernbara");
            if (card.cardImage == null) card.cardImage = CreationLabUI.LoadSpriteSafe("UI_Breeding/Cards/fernbara_good_card") ?? CreationLabUI.LoadSpriteSafe("CreatureAvatars/Avatar_Fernbara");
            if (card.prefabToPlace == null) card.prefabToPlace = LoadPrefabSafe("Assets/thulai_thuong/Animated/Fernbara_anm.glb");
        }
        else if (id.Contains("lotriv") || nm.Contains("lotriv"))
        {
            card.cardID = "creature_lotriv";
            card.cardName = "Lotriv";
            card.rarity = CardRarity.Common;
            card.islandId = "wet";
            card.baseScore = 6;
            if (card.animalSpeciesData == null) card.animalSpeciesData = GetOrCreateHybridSpecies("lotriv", "Lotriv");
            if (card.cardImage == null) card.cardImage = CreationLabUI.LoadSpriteSafe("UI_Breeding/Cards/lotriv_good_card") ?? CreationLabUI.LoadSpriteSafe("CreatureAvatars/Avatar_Lotriv");
            if (card.prefabToPlace == null) card.prefabToPlace = LoadPrefabSafe("Assets/thulai_thuong/Animated/Lotriv_anm.glb");
        }
        else if (id.Contains("bloomhorn") || nm.Contains("bloomhorn"))
        {
            card.cardID = "creature_bloomhorn";
            card.cardName = "Bloomhorn";
            card.rarity = CardRarity.Common;
            card.islandId = "starter";
            card.baseScore = 6;
            if (card.animalSpeciesData == null) card.animalSpeciesData = GetOrCreateHybridSpecies("bloomhorn", "Bloomhorn");
            if (card.cardImage == null) card.cardImage = CreationLabUI.LoadSpriteSafe("UI_Breeding/Cards/bloomhorn_good_card") ?? CreationLabUI.LoadSpriteSafe("CreatureAvatars/Avatar_Bloomhorn");
            if (card.prefabToPlace == null) card.prefabToPlace = LoadPrefabSafe("Assets/thulai_thuong/Animated/Bloomhorn_anm.glb");
        }
        else if (id.Contains("raincroak") || nm.Contains("raincroak"))
        {
            card.cardID = "creature_raincroak";
            card.cardName = "Raincroak";
            card.rarity = CardRarity.Common;
            card.islandId = "wet";
            card.baseScore = 6;
            if (card.animalSpeciesData == null) card.animalSpeciesData = GetOrCreateHybridSpecies("raincroak", "Raincroak", AnimalLocomotionType.Amphibian);
            if (card.cardImage == null) card.cardImage = CreationLabUI.LoadSpriteSafe("UI_Breeding/Cards/raincroak_good_card") ?? CreationLabUI.LoadSpriteSafe("CreatureAvatars/Avatar_Raincroak");
            if (card.prefabToPlace == null) card.prefabToPlace = LoadPrefabSafe("Assets/thulai_thuong/Animated/Raincroak_anm.glb");
        }
        else if (id.Contains("featherglide") || nm.Contains("featherglide"))
        {
            card.cardID = "creature_featherglide";
            card.cardName = "Featherglide";
            card.rarity = CardRarity.Common;
            card.islandId = "starter";
            card.baseScore = 6;
            if (card.animalSpeciesData == null) card.animalSpeciesData = GetOrCreateHybridSpecies("featherglide", "Featherglide", AnimalLocomotionType.Flying);
            if (card.cardImage == null) card.cardImage = CreationLabUI.LoadSpriteSafe("UI_Breeding/Cards/featherglide_good_card") ?? CreationLabUI.LoadSpriteSafe("CreatureAvatars/Avatar_Featherglide");
            if (card.prefabToPlace == null) card.prefabToPlace = LoadPrefabSafe("Assets/thulai_thuong/Animated/Featherglide_anm.glb");
        }
        else if (id.Contains("mossgarg") || nm.Contains("mossgarg"))
        {
            card.cardID = "creature_mossgarg";
            card.cardName = "Mossgarg";
            card.rarity = CardRarity.Epic;
            card.islandId = "wet";
            card.baseScore = 8;
            if (card.animalSpeciesData == null) card.animalSpeciesData = GetOrCreateHybridSpecies("mossgarg", "Mossgarg");
            if (card.cardImage == null) card.cardImage = CreationLabUI.LoadSpriteSafe("UI_Breeding/Cards/mossgarg_card") ?? CreationLabUI.LoadSpriteSafe("CreatureAvatars/Avatar_Mossgarg");
            if (card.prefabToPlace == null) card.prefabToPlace = LoadPrefabSafe("Assets/model_thulai_caocap/Animated/Mossgarg_anm.glb");
        }
        else if (id.Contains("seraphin") || nm.Contains("seraphin"))
        {
            card.cardID = "creature_seraphin";
            card.cardName = "Seraphin";
            card.rarity = CardRarity.Legend;
            card.islandId = "wet";
            card.baseScore = 10;
            if (card.animalSpeciesData == null) card.animalSpeciesData = GetOrCreateHybridSpecies("seraphin", "Seraphin", AnimalLocomotionType.Flying);
            if (card.cardImage == null) card.cardImage = CreationLabUI.LoadSpriteSafe("UI_Breeding/Cards/seraphin_card") ?? CreationLabUI.LoadSpriteSafe("CreatureAvatars/Avatar_Seraphin");
            if (card.prefabToPlace == null) card.prefabToPlace = LoadPrefabSafe("Assets/model_thulai_caocap/Animated/Seraphin_anm.glb");
        }
        else if (id.Contains("breezehorn") || nm.Contains("breezehorn"))
        {
            card.cardID = "creature_breezehorn";
            card.cardName = "Breezehorn";
            card.rarity = CardRarity.Legend;
            card.islandId = "starter";
            card.baseScore = 10;
            if (card.animalSpeciesData == null) card.animalSpeciesData = GetOrCreateHybridSpecies("breezehorn", "Breezehorn");
            if (card.cardImage == null) card.cardImage = CreationLabUI.LoadSpriteSafe("UI_Breeding/Cards/breezehorn_card") ?? CreationLabUI.LoadSpriteSafe("CreatureAvatars/Avatar_Breezehorn");
            if (card.prefabToPlace == null) card.prefabToPlace = LoadPrefabSafe("Assets/model_thulai_caocap/Animated/Breezehorn_anm.glb");
        }
        else if (id.Contains("prismagale") || nm.Contains("prismagale"))
        {
            card.cardID = "creature_prismagale";
            card.cardName = "Prismagale";
            card.rarity = CardRarity.Legend;
            card.islandId = "starter";
            card.baseScore = 10;
            if (card.animalSpeciesData == null) card.animalSpeciesData = GetOrCreateHybridSpecies("prismagale", "Prismagale", AnimalLocomotionType.Flying);
            if (card.cardImage == null) card.cardImage = CreationLabUI.LoadSpriteSafe("UI_Breeding/Cards/prismagale_card") ?? CreationLabUI.LoadSpriteSafe("CreatureAvatars/Avatar_Prismagale");
            if (card.prefabToPlace == null) card.prefabToPlace = LoadPrefabSafe("Assets/model_thulai_caocap/Animated/Prismagale_anm.glb");
        }
        else if (id.Contains("quetzalcroak") || nm.Contains("quetzalcroak"))
        {
            card.cardID = "creature_quetzalcroak";
            card.cardName = "Quetzalcroak";
            card.rarity = CardRarity.Epic;
            card.islandId = "wet";
            card.baseScore = 8;
            if (card.animalSpeciesData == null) card.animalSpeciesData = GetOrCreateHybridSpecies("quetzalcroak", "Quetzalcroak", AnimalLocomotionType.Amphibian);
            if (card.cardImage == null) card.cardImage = CreationLabUI.LoadSpriteSafe("UI_Breeding/Cards/quetzalcroak_card") ?? CreationLabUI.LoadSpriteSafe("CreatureAvatars/Avatar_Quetzalcroak");
            if (card.prefabToPlace == null) card.prefabToPlace = LoadPrefabSafe("Assets/model_thulai_caocap/Animated/Quetzalcroak_anm.glb");
        }

        if (card.animalSpeciesData == null && !string.IsNullOrEmpty(card.cardName))
        {
            card.animalSpeciesData = GetOrCreateHybridSpecies(card.cardName.ToLowerInvariant(), card.cardName);
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

    public static CardData ResolveCardSafe(string cardName, params string[] searchKeys)
    {
        if (string.IsNullOrEmpty(cardName)) return null;

        // Try main catalog first
        CardData fromCat = FindCardFromMainCatalog(cardName, searchKeys);
        if (fromCat != null) return fromCat;

        CardData c = Resources.Load<CardData>($"Cards/{cardName}");
        if (c != null) return c;
        c = Resources.Load<CardData>($"Cards/Creature/{cardName}");
        if (c != null) return c;

        var all = Resources.LoadAll<CardData>("");
        string targetLower = cardName.ToLowerInvariant();

        // Pass 1: Exact matches
        foreach (var item in all)
        {
            if (item == null) continue;
            string id = item.cardID?.ToLowerInvariant() ?? "";
            string nm = item.cardName?.ToLowerInvariant() ?? "";
            string fn = item.name.ToLowerInvariant();
            if (fn == targetLower || nm == targetLower || id == targetLower || id == $"creature_{targetLower}")
                return item;
        }

        // Pass 2: Search keys match (strictly never match Fox for Fernkin)
        foreach (var item in all)
        {
            if (item == null) continue;
            string id = item.cardID?.ToLowerInvariant() ?? "";
            string nm = item.cardName?.ToLowerInvariant() ?? "";
            string fn = item.name.ToLowerInvariant();

            if (targetLower.Contains("fernkin") && (fn == "fox" || nm == "fox" || id == "creature_fox"))
                continue;

            foreach (var k in searchKeys)
            {
                if (string.IsNullOrEmpty(k)) continue;
                string lk = k.ToLowerInvariant();
                if (lk == "fox" && targetLower.Contains("fernkin")) continue;

                if (id.Contains(lk) || nm.Contains(lk) || fn.Contains(lk))
                    return item;
            }
        }
        return null;
    }

    /// <summary>
    /// Guarantees that the 5 multi-tier hybrid recipes and classic Braluff recipe exist.
    /// </summary>
    public void EnsureDefaultRecipes()
    {
        if (recipes == null) recipes = new List<BreedingRecipe>();

        // Remove any obsolete or conflicting recipes
        recipes.RemoveAll(r => r != null && (
            r.name.Contains("Capybara_Deer") ||
            r.name.Contains("BlossomFrog_HighlandCow") ||
            r.name.Contains("BlossomFrog_Caterpillar") ||
            (r.name.Contains("Dandrift") && !r.name.Contains("Heathorn")) ||
            (r.name.Contains("Peacock") && !r.name.Contains("Squirrel") && !r.name.Contains("Spriglide")) ||
            (r.name.Contains("Galume") && !r.name.Contains("Squirrel") && !r.name.Contains("Spriglide"))
        ));

        // Resolve all 10 Parent Cards (Strict search keys, never include wild Fox!)
        CardData dandriftCard   = ResolveCardSafe("Dandrift", "creature_flowerdog", "flowerdog", "dandrift");
        CardData heathornCard   = ResolveCardSafe("Heathorn", "creature_highlandcow", "highlandcow", "heathorn");
        CardData galumeCard     = ResolveCardSafe("Galume", "creature_peacock", "peacock", "galume");
        CardData spriglideCard  = ResolveCardSafe("Spriglide", "creature_flyingsquirrel", "flyingsquirrel", "spriglide");
        CardData fernkinCard    = ResolveCardSafe("Fernkin", "creature_grassfox", "grassfox", "fernkin");
        CardData pebbaraCard    = ResolveCardSafe("Pebbara", "creature_capybara", "capybara", "pebbara");
        CardData lotelleCard    = ResolveCardSafe("Lotelle", "creature_swan", "lotelle", "swan");
        CardData rivletCard     = ResolveCardSafe("Rivlet", "creature_mudskipper", "rivlet", "mudskipper", "otter");
        CardData puddleafCard   = ResolveCardSafe("Puddleaf", "creature_blossomfrog", "puddleaf", "blossomfrog");
        CardData helikoCard     = ResolveCardSafe("Heliko", "creature_parrot", "heliko", "parrot");

        // Resolve all 10 Offspring Cards
        CardData bloomhornCard    = ResolveCardSafe("Bloomhorn", "bloomhorn");
        CardData breezehornCard   = ResolveCardSafe("Breezehorn", "breezehorn");
        CardData featherglideCard = ResolveCardSafe("Featherglide", "featherglide");
        CardData prismagaleCard   = ResolveCardSafe("Prismagale", "prismagale");
        CardData fernbaraCard     = ResolveCardSafe("Fernbara", "fernbara");
        CardData mossgargCard     = ResolveCardSafe("Mossgarg", "mossgarg");
        CardData lotrivCard       = ResolveCardSafe("Lotriv", "lotriv");
        CardData seraphinCard     = ResolveCardSafe("Seraphin", "seraphin");
        CardData raincroakCard    = ResolveCardSafe("Raincroak", "raincroak");
        CardData quetzalcroakCard = ResolveCardSafe("Quetzalcroak", "quetzalcroak");

        // Helper to check and add recipe
        System.Action<string, CardData, CardData, CardData, CardData, CardData, float, float, float, float, int, string> ensureRecipe =
            (recName, pA, pB, goodOff, epicOff, legOff, gRate, eRate, lRate, sec, fee, dur) =>
        {
            if (pA == null || pB == null) return;
            var existing = recipes.Find(r => r != null && r.name == recName);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<BreedingRecipe>();
                existing.name = recName;
                recipes.Add(existing);
            }
            existing.parentA = pA;
            existing.parentB = pB;
            existing.offspring = goodOff;
            existing.epicOffspring = epicOff;
            existing.legendOffspring = legOff;
            existing.goodRate = gRate;
            existing.epicRate = eRate;
            existing.legendRate = lRate;
            existing.incubationSeconds = sec;
            existing.feeCoins = fee;
            existing.durationDisplay = dur;
        };

        // Pair 1: Dandrift + Heathorn -> Bloomhorn (~95%/98%), Breezehorn (~0.5%/1.5% Legendary)
        ensureRecipe("Recipe_Dandrift_Heathorn_Bloomhorn", dandriftCard, heathornCard,
            bloomhornCard, mossgargCard, breezehornCard, 0.98f, 0.015f, 0.005f, 6300f, 55, "01h 45m");

        // Pair 2: Galume + Spriglide -> Featherglide (~95%/98%), Prismagale (~0.5%/1.5% Legendary)
        ensureRecipe("Recipe_Galume_Spriglide_Featherglide", galumeCard, spriglideCard,
            featherglideCard, breezehornCard, prismagaleCard, 0.98f, 0.015f, 0.005f, 4800f, 50, "01h 20m");

        // Pair 3: Fernkin + Pebbara -> Fernbara (~95%/98%), Mossgarg (~0.5%/1.5% Epic)
        ensureRecipe("Recipe_Fernkin_Pebbara_Fernbara", fernkinCard, pebbaraCard,
            fernbaraCard, mossgargCard, prismagaleCard, 0.98f, 0.015f, 0.005f, 8100f, 65, "02h 15m");

        // Pair 4: Lotelle + Rivlet -> Lotriv (~95%/98%), Seraphin (~0.5%/1.5% Legendary)
        ensureRecipe("Recipe_Lotelle_Rivlet_Lotriv", lotelleCard, rivletCard,
            lotrivCard, mossgargCard, seraphinCard, 0.98f, 0.015f, 0.005f, 9900f, 75, "02h 45m");

        // Pair 5: Puddleaf + Heliko -> Raincroak (~95%/98%), Quetzalcroak (~0.5%/1.5% Epic)
        ensureRecipe("Recipe_Puddleaf_Heliko_Raincroak", puddleafCard, helikoCard,
            raincroakCard, quetzalcroakCard, seraphinCard, 0.98f, 0.015f, 0.005f, 7200f, 60, "02h 00m");

        // Recipe 0: Starter (Bramblet + Petaluff -> 98% Petaluff)
        CardData deerCard = ResolveCardSafe("Bramblet", "creature_deer", "deer", "bramblet");
        CardData caterCard = ResolveCardSafe("Petaluff", "creature_caterpillar", "caterpillar", "petaluff");
        CardData targetOffspring = caterCard != null ? caterCard : GetOrCreateBraluffCard();
        if (targetOffspring != null)
        {
            targetOffspring.cardName = "Petaluff";
        }
        if (deerCard != null && caterCard != null)
        {
            ensureRecipe("Recipe_Bramblet_Petaluff_Braluff", deerCard, caterCard,
                targetOffspring, null, null, 0.98f, 0f, 0f, 3600f, 40, "01h 00m");
        }

        // Ensure all hybrid cards and Braluff are fully configured
        foreach (var r in recipes)
        {
            if (r != null)
            {
                if (r.offspring != null) EnsureCardFullyConfigured(r.offspring);
                if (r.epicOffspring != null) EnsureCardFullyConfigured(r.epicOffspring);
                if (r.legendOffspring != null) EnsureCardFullyConfigured(r.legendOffspring);
            }
        }
        UpgradeExistingBraluffCardsInHand();
    }

    private static CardData FindCardFromMainCatalog(string exactId, params string[] keywords)
    {
        CardCatalog catalog = WorldSaveSystem.Instance != null ? WorldSaveSystem.Instance.Cards : null;
        if (catalog == null) return null;

        if (!string.IsNullOrEmpty(exactId))
        {
            CardData exact = catalog.Find(exactId);
            if (exact != null) return exact;
        }

        var all = catalog.AllCards;
        if (all == null || keywords == null || keywords.Length == 0) return null;

        for (int i = 0; i < all.Count; i++)
        {
            CardData c = all[i];
            if (c == null) continue;
            string id = c.cardID?.ToLowerInvariant() ?? "";
            string nm = c.cardName?.ToLowerInvariant() ?? "";
            for (int k = 0; k < keywords.Length; k++)
            {
                string kw = keywords[k];
                if (!string.IsNullOrEmpty(kw) && (id.Contains(kw) || nm.Contains(kw)))
                    return c;
            }
        }

        return null;
    }

    /// <summary>Returns true if the card can be used as a parent in any active recipe.</summary>
    public bool HasRecipeWithParent(CardData card)
    {
        if (card == null) return false;
        EnsureDefaultRecipes();
        foreach (var r in recipes)
        {
            if (r != null && r.HasParent(card)) return true;
        }
        return false;
    }

    public static void UpgradeExistingBraluffCardsInHand()
    {
        var cardUIs = FindObjectsByType<CardUI>(FindObjectsInactive.Include);
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

        var cardUIs = FindObjectsByType<CardUI>(FindObjectsInactive.Include);
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

        if (!string.IsNullOrEmpty(targetData.cardID) && CardDeskController.Instance.TakeCardsWithoutSaving(targetData.cardID, 1)) return;
        CardDeskController.Instance.TakeCardsWithoutSaving(targetData.name, 1);
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

        CardData offspring = ch.rolledOffspring ?? ch.recipe?.offspring;
        ch.Reset();
        OnChamberStateChanged?.Invoke(chamberIndex);

        if (offspring != null)
        {
            EnsureCardFullyConfigured(offspring);
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
