using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using LumiWorld.Acs;
using LumiWorld.Acs.Islands;

public static class SetupWetIsland
{
    // Asset names and serialized IDs are intentionally independent of the GDD display names.
    static readonly (string asset, string key, string speciesId, BiomeAffinity primary, BiomeAffinity secondary)[] WetSpecies = {
        ("Mudskipper", "reedle", "mudskipper", BiomeAffinity.Reedmarsh, BiomeAffinity.Streamstone),
        ("MossyCrab", "craggle", "mossycrab", BiomeAffinity.Reedmarsh, BiomeAffinity.Streamstone),
        ("LotusSwan", "lotelle", "lotusswan", BiomeAffinity.Lilywater, BiomeAffinity.Reedmarsh),
        ("Dragonfly", "iriwing", "dragonfly", BiomeAffinity.Lilywater, BiomeAffinity.Streamstone),
        ("Otter", "rivlet", "otter", BiomeAffinity.Streamstone, BiomeAffinity.Lilywater),
        ("Capybara", "pebbara", "capybara", BiomeAffinity.Streamstone, BiomeAffinity.Reedmarsh),
        ("GrassFox", "fernkin", "grassfox", BiomeAffinity.Rainleaf, BiomeAffinity.Streamstone),
        ("BlossomFrog", "puddleaf", "blossomfrog", BiomeAffinity.Rainleaf, BiomeAffinity.Reedmarsh),
        ("Parrot", "heliko", "parrot", BiomeAffinity.Rainleaf, BiomeAffinity.Streamstone)
    };
    static readonly HashSet<UnityEngine.Object> changedAssets = new HashSet<UnityEngine.Object>();
    static T Load<T>(string path) where T: UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path);
    static T Asset<T>(string path) where T: ScriptableObject
    {
        var result = Load<T>(path);
        if (result != null) return result;
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        AssetDatabase.Refresh();
        result = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(result, path);
        return result;
    }
    static void Dirty(UnityEngine.Object asset) { EditorUtility.SetDirty(asset); changedAssets.Add(asset); }

    static Sprite CardSprite(string path)
    {
        var sprite = Load<Sprite>(path);
        if (sprite != null) return sprite;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing card artwork: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.SaveAndReimport();
        return Load<Sprite>(path);
    }

    public static object ConfigureWetCardIcons()
    {
        foreach (string name in new[] { "Lilywater", "Reedmarsh", "Rainleaf", "Streamstone" })
        {
            var card = Load<CardData>($"Assets/Data/Cards/{name}.asset");
            card.cardImage = CardSprite($"Assets/Acs/UI Cards/Environment/{name}.png");
            EditorUtility.SetDirty(card);
            AssetDatabase.SaveAssetIfDirty(card);
        }
        return "Four Wet Nature icons assigned";
    }

    static List<CardData> ValidateCreatureAssets()
    {
        var cards = new List<CardData>();
        foreach (var row in WetSpecies)
        {
            var card = Load<CardData>($"Assets/Data/Cards/{row.asset}.asset");
            if (card == null || card.prefabToPlace == null || card.animalSpeciesData == null || card.cardImage == null ||
                card.cardID != "creature_" + row.speciesId || card.animalSpeciesData.speciesID != row.speciesId)
                throw new InvalidOperationException("Incomplete or mismatched Wet creature: " + row.asset);
            cards.Add(card);
        }
        return cards;
    }

    static void RegisterCreatures(SpeciesMappingRegistry registry, IReadOnlyList<CardData> cards)
    {
        for (int i = 0; i < WetSpecies.Length; i++)
        {
            var row = WetSpecies[i]; var card = cards[i];
            if (card.islandId != "wet") { card.islandId = "wet"; Dirty(card); }
            var matches = registry.entries.Where(e => e.gddKey == row.key || e.SpeciesId == row.speciesId || e.creatureCard == card).ToList();
            if (matches.Count > 1) throw new InvalidOperationException("Duplicate registry entries for " + row.key);
            var entry = matches.FirstOrDefault();
            if (entry == null) { entry = new SpeciesMappingEntry(); registry.entries.Add(entry); }
            entry.gddKey = row.key; entry.displayName = card.animalSpeciesData.speciesName;
            entry.species = card.animalSpeciesData; entry.creatureCard = card;
            entry.primaryAffinity = row.primary; entry.secondaryAffinity = row.secondary;
            entry.integrationStatus = SpeciesIntegrationStatus.Active;
        }
        Dirty(registry);
    }

    static void RegisterCreatureCatalogs(IReadOnlyList<CardData> cards)
    {
        var shop = Load<ShopCatalog>("Assets/Data/Resources/Shop/ShopCatalog.asset");
        var saveCatalog = Load<CardCatalog>("Assets/Data/Save/CardCatalog.asset");
        int starterPrice = shop.defaultPrices.First(p => p.cardType == CardType.Creature).shopPrice;
        int starterFee = shop.defaultPrices.First(p => p.cardType == CardType.Creature).placementFee;
        if (starterPrice <= 0) throw new InvalidOperationException("Configure a positive default Creature price.");
        int wetPrice = Mathf.RoundToInt(starterPrice * 1.5f);
        int wetFee = Mathf.RoundToInt(starterFee * 1.5f);
        foreach (var card in cards)
        {
            if (card.shopPrice != wetPrice) { card.shopPrice = wetPrice; Dirty(card); }
            if (card.placementFee != wetFee) { card.placementFee = wetFee; Dirty(card); }
            if (!shop.cards.Contains(card)) shop.cards.Add(card);
            if (!shop.testCards.Contains(card)) shop.testCards.Add(card);
        }
        saveCatalog.SetCards(saveCatalog.All.Concat(cards).Where(c => c != null).Distinct().ToList());
        Dirty(shop); Dirty(saveCatalog);
    }

    // Safe roster-only entry point: does not rebuild scene configuration or Nature assets.
    public static object RegisterWetCreatures()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before setup.");
        changedAssets.Clear();
        var cards = ValidateCreatureAssets();
        var registry = Load<SpeciesMappingRegistry>("Assets/Acs/Data/Definitions/SpeciesMappingRegistry.asset");
        RegisterCreatures(registry, cards);
        RegisterCreatureCatalogs(cards);
        foreach (var asset in changedAssets) AssetDatabase.SaveAssetIfDirty(asset);
        return new { wetSpecies = cards.Count, registryTotal = registry.entries.Count };
    }

    public static object Verify()
    {
        var cards = ValidateCreatureAssets();
        var production = Load<ResourceProductionCatalog>("Assets/Acs/Data/Definitions/ResourceProductionCatalog.asset");
        var savedCards = Load<CardCatalog>("Assets/Data/Save/CardCatalog.asset");
        var shop = Load<ShopCatalog>("Assets/Data/Resources/Shop/ShopCatalog.asset");
        var registry = production.speciesRegistry;
        for (int i = 0; i < WetSpecies.Length; i++)
        {
            var row = WetSpecies[i]; var card = cards[i];
            if (!registry.TryGetActiveBySpeciesId(row.speciesId, out var entry) || entry.gddKey != row.key ||
                entry.creatureCard != card || entry.primaryAffinity != row.primary || entry.secondaryAffinity != row.secondary ||
                registry.entries.Count(e => e.gddKey == row.key || e.SpeciesId == row.speciesId) != 1)
                throw new InvalidOperationException("Registry mismatch: " + row.key);
            if (card.islandId != "wet" || savedCards.Find(card.cardID) != card || savedCards.All.Count(c => c == card) != 1 ||
                shop.cards.Count(c => c == card) != 1 || shop.testCards.Count(c => c == card) != 1 ||
                card.shopPrice <= 0 || CardShop.GetPrice(card) <= 0 || !CardShop.Cards.Contains(card))
                throw new InvalidOperationException("Catalog/deck/shop mismatch: " + row.key);
            if (PrefabUtility.GetPrefabAssetType(card.prefabToPlace) == PrefabAssetType.MissingAsset ||
                card.prefabToPlace.GetComponentInChildren<SkinnedMeshRenderer>() == null)
                throw new InvalidOperationException("Missing model content: " + row.key);
            foreach (BiomeAffinity affinity in Enum.GetValues(typeof(BiomeAffinity)))
                if (entry.GetRole(affinity) != (affinity == row.primary ? AffinityRole.Primary : affinity == row.secondary ? AffinityRole.Secondary : AffinityRole.None))
                    throw new InvalidOperationException("Incorrect residency eligibility: " + row.key);
        }
        var active = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/MapBuilding.unity");
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene("Assets/Scenes/MapBuilding.unity", OpenSceneMode.Additive);
        try
        {
            var components = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c != null).ToArray();
            if (components.OfType<HabitatRuntimeManager>().Single().Catalog != production ||
                components.OfType<WorldSaveSystem>().Single().Cards != savedCards ||
                components.OfType<IslandRuntimeManager>().Single().Catalog == null ||
                components.OfType<WetBiomePresentation>().Count() != 1 || components.OfType<IslandNavigationUI>().Count() != 1)
                throw new InvalidOperationException("MapBuilding wiring mismatch.");
            return new { passed = true, wetSpecies = cards.Count, registryTotal = registry.entries.Count,
                affinityChecks = cards.Count * Enum.GetValues(typeof(BiomeAffinity)).Length, scene = scene.path,
                originalScene = active.path, originalSceneDirty = active.isDirty,
                mapping = registry.entries.Where(e => cards.Contains(e.creatureCard)).Select(e => new { e.gddKey,e.SpeciesId,cardId=e.creatureCard.cardID,primary=e.primaryAffinity.ToString(),secondary=e.secondaryAffinity.ToString() }).ToArray() };
        }
        finally { SceneManager.SetActiveScene(active); if(openedHere) EditorSceneManager.CloseScene(scene,true); }
    }
    public static object RegisterWetResources()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before setup.");
        changedAssets.Clear();
        var catalog = Load<ResourceProductionCatalog>("Assets/Acs/Data/Definitions/ResourceProductionCatalog.asset");
        var reference = typeof(ResourceProductionCatalog).Assembly.GetType("LumiWorld.Acs.GddResourceDesignReference");
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var specs = (Array)reference.GetField("Resources", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
        int added = 0;
        foreach (object spec in specs)
        {
            var type = spec.GetType();
            var affinity = (BiomeAffinity)type.GetField("Affinity", flags).GetValue(spec);
            if (HabitatDataRules.GetIsland(affinity) != HabitatIsland.Wet) continue;
            string id = (string)type.GetField("Id", flags).GetValue(spec);
            string item = id.Split('.')[2];
            string name = string.Concat(item.Split('_').Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1)));
            string resourcePath = $"Assets/Acs/Data/Resources/{affinity}/{name}.asset";
            bool isNew = Load<ResourceData>(resourcePath) == null;
            var resource = Asset<ResourceData>(resourcePath);
            resource.resourceID = id;
            if (isNew)
            {
                resource.resourceName = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(item.Replace('_', ' '));
                resource.description = $"Gathered from {affinity}.";
                resource.baseValue = 0; resource.maxStackSize = 999;
                resource.resourceColor = Color.white; resource.hexColor = "#FFFFFF";
            }
            if (resource.icon == null)
                resource.icon = Load<Sprite>($"Assets/Project/Resources/HabitatBadges/{affinity}.png");
            Dirty(resource);
            var definition = Asset<ResourceProductionDefinition>($"Assets/Acs/Data/Definitions/Production/Production_{name}.asset");
            definition.resource = resource; definition.affinity = affinity;
            definition.tier = (ResourceTier)type.GetField("Tier", flags).GetValue(spec);
            definition.isEnabled = true;
            definition.requirements.Clear();
            foreach (var role in new[] { AffinityRole.Primary, AffinityRole.Secondary })
                foreach (string key in (string[])type.GetField(role.ToString(), flags).GetValue(spec))
                    definition.requirements.Add(new SpeciesProductionRequirement { gddSpeciesKey=key, role=role, minimumCount=1 });
            if (!catalog.resources.Contains(definition)) catalog.resources.Add(definition);
            Dirty(definition); added++;
        }
        Dirty(catalog);
        var report = ResourceCatalogValidator.Validate(catalog);
        if (report.errors.Count > 0) throw new InvalidOperationException(string.Join("\\n", report.errors));
        foreach (var asset in changedAssets) AssetDatabase.SaveAssetIfDirty(asset);
        ResourceCatalog.ResetCache();
        return new { wetResources=added, total=catalog.resources.Count, errors=report.errors, warnings=report.warnings };
    }

    static GameObject Prefab(string name) => Load<GameObject>("Assets/Acs/Hex/WetIsland/Prefabs/" + name + ".prefab");
    static GameObject Biome(string name, int count) => Load<GameObject>($"Assets/Acs/Hex/WetIsland/Biomes/Prefabs/Biome_{name}_{count}Hex.prefab");

    public static object Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before setup.");
        changedAssets.Clear();
        var creatures = ValidateCreatureAssets();
        var active = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/MapBuilding.unity");
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene("Assets/Scenes/MapBuilding.unity", OpenSceneMode.Additive);
        else if (scene.isDirty) throw new InvalidOperationException("MapBuilding has unsaved edits; save it before running setup.");
        try
        {
            SceneManager.SetActiveScene(scene);
            var components = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c != null).ToArray();
            var world = components.OfType<HexWorldGenerator>().Single();
            var production = Load<ResourceProductionCatalog>("Assets/Acs/Data/Definitions/ResourceProductionCatalog.asset");
            var catalog = Asset<IslandCatalog>("Assets/Acs/Data/Islands/IslandCatalog.asset");
            var rain = Load<CardData>("Assets/Data/Cards/Rain.asset");
            var dry = new[]{Prefab("Normal-low"), Prefab("Normal-med"), Prefab("Normal-high")};
            var lush = new[]{Prefab("Lush-low"), Prefab("Lush-med"), Prefab("Lush-high")};
            if (dry.Concat(lush).Any(p => p == null)) throw new InvalidOperationException("Missing Wet ground prefabs.");
            var names = new[]{"Lilywater","Reedmarsh","Rainleaf","Streamstone"};
            var nature = new List<CardData>();
            catalog.biomeVisuals.Clear();
            foreach (string name in names)
            {
                var card = Asset<CardData>($"Assets/Data/Cards/{name}.asset");
                card.cardID = name.ToLowerInvariant() + "_001";
                card.cardName = name; card.cardType = CardType.Terrain; card.islandId = "wet";
                card.cardImage = CardSprite($"Assets/Acs/UI Cards/Environment/{name}.png");
                card.shopPrice = 30; card.placementFee = 15; card.occupiesTile = true;
                card.prefabToPlace = null; card.replacesTile = false;
                card.allowedTilePrefabs = lush.ToList();
                card.maxBiomeTilesOverride = 10;
                Dirty(card); nature.Add(card);
                var definition = Asset<NatureHabitatDefinition>($"Assets/Acs/Data/Definitions/Habitats/Habitat_{name}.asset");
                definition.natureCard = card; definition.affinity = (BiomeAffinity)Enum.Parse(typeof(BiomeAffinity), name); definition.island = HabitatIsland.Wet;
                if (!production.habitats.Contains(definition)) production.habitats.Add(definition);
                Dirty(definition);
                var visuals = new BiomeVisualSet { nature=card, single=Biome(name,1), three=Biome(name,3), six=Biome(name,6), ten=Biome(name,10) };
                if (visuals.single == null || visuals.three == null || visuals.six == null || visuals.ten == null) throw new InvalidOperationException("Missing biome prefab: " + name);
                catalog.biomeVisuals.Add(visuals);
            }
            var wetRain = Asset<CardData>("Assets/Data/Cards/RainWet.asset");
            EditorUtility.CopySerialized(rain, wetRain);
            wetRain.name = "RainWet"; wetRain.cardID = "Rain.Wet"; wetRain.cardName = "Rain"; wetRain.islandId = "wet";
            wetRain.rarity = CardRarity.Common; wetRain.isTriangleRain = false; wetRain.isTerraformTool = false;
            wetRain.shopPrice = 45; wetRain.placementFee = 15;
            wetRain.cardImage = CardSprite("Assets/Acs/UI Cards/Environment/Rain.png");
            wetRain.transformRules = new List<CardData.TileTransformPair>();
            for (int i=0;i<3;i++) wetRain.transformRules.Add(new CardData.TileTransformPair { aridTilePrefab=dry[i], lushTilePrefab=lush[i] });
            wetRain.allowedTilePrefabs = dry.ToList(); wetRain.prefabToPlace = null;
            Dirty(wetRain);
            var pouringRain = Asset<CardData>("Assets/Data/Cards/PouringRain.asset");
            EditorUtility.CopySerialized(wetRain, pouringRain);
            pouringRain.name = "PouringRain"; pouringRain.cardID = "Rain.Wet.Pouring"; pouringRain.cardName = "Pouring Rain"; pouringRain.islandId = "wet";
            pouringRain.reusable = false; pouringRain.isTriangleRain = true; pouringRain.isTerraformTool = false; pouringRain.elevationChange = 0;
            pouringRain.rarity = CardRarity.Epic; pouringRain.shopPrice = 50; pouringRain.placementFee = 30;
            pouringRain.baseScore = 1; pouringRain.alwaysBaseScore = true;
            pouringRain.cardImage = CardSprite("Assets/Acs/UI Cards/Environment/PouringRain.png");
            Dirty(pouringRain);
            var raiseCard = Asset<CardData>("Assets/Data/Cards/Raise.asset");
            raiseCard.cardID = "terraform.raise"; raiseCard.cardName = "Raise / Lower Land";
            raiseCard.cardType = CardType.Special; raiseCard.islandId = "wet"; raiseCard.reusable = true;
            raiseCard.isTerraformTool = true; raiseCard.isTriangleRain = false; raiseCard.elevationChange = 1;
            raiseCard.rarity = CardRarity.Legend; raiseCard.shopPrice = 60; raiseCard.placementFee = 10; raiseCard.occupiesTile = false;
            raiseCard.cardImage = CardSprite("Assets/Acs/UI Cards/Environment/RaiseLowerLand.png");
            Dirty(raiseCard);
            var lowerCard = Load<CardData>("Assets/Data/Cards/Lower.asset");
            if (lowerCard != null)
            {
                lowerCard.isTerraformTool = true; lowerCard.rarity = CardRarity.Legend;
                lowerCard.shopPrice = 0; lowerCard.placementFee = 10;
                lowerCard.cardImage = raiseCard.cardImage;
                Dirty(lowerCard);
            }
            var tools = new List<CardData> { raiseCard };
            catalog.islands = new List<IslandDefinition> {
                new IslandDefinition { id="starter", displayName="Starter", habitatIsland=HabitatIsland.Starter,
                    center=new HexCoordinates(0,0), expansionRadius=3, dryTiles=world.TerrainPrefabs, rainCard=rain },
                new IslandDefinition { id="wet", displayName="Wet Island", habitatIsland=HabitatIsland.Wet,
                    center=new HexCoordinates(45,0), expansionRadius=4, dryTiles=dry, lushTiles=lush, rainCard=wetRain,
                    startingNature=nature.ToArray(), startingRain=6, unlockCoins=200,
                    discoveryBiomes=new[]{BiomeAffinity.Leafwood,BiomeAffinity.Bloomfield,BiomeAffinity.Windheath}, discoveryCreatures=3 }
            };
            if (catalog.islands[0].CreateBoundary().Overlaps(catalog.islands[1].CreateBoundary())) throw new InvalidOperationException("Island margins overlap.");
            RegisterCreatures(production.speciesRegistry, creatures);
            RegisterCreatureCatalogs(creatures);
            Dirty(production); Dirty(catalog);
            var shop = Load<ShopCatalog>("Assets/Data/Resources/Shop/ShopCatalog.asset");
            if (lowerCard != null) { shop.cards.RemoveAll(c => c == lowerCard); shop.testCards.RemoveAll(c => c == lowerCard); }
            foreach (var card in nature.Concat(creatures).Concat(tools).Concat(new[]{wetRain, pouringRain}))
            {
                if(!shop.cards.Contains(card)) shop.cards.Add(card);
                if(!shop.testCards.Contains(card)) shop.testCards.Add(card);
            }
            Dirty(shop);
            var cardCatalog=Load<CardCatalog>("Assets/Data/Save/CardCatalog.asset");
            cardCatalog.SetCards(cardCatalog.All.Concat(AssetDatabase.FindAssets("t:CardData",new[]{"Assets/Data/Cards"})
                .Select(AssetDatabase.GUIDToAssetPath).Select(Load<CardData>)).Where(c => c != null).Distinct().ToList());
            Dirty(cardCatalog);
            var tileCatalog=Load<TileCatalog>("Assets/Data/Save/TileCatalog.asset");
            var so=new SerializedObject(tileCatalog); var entries=so.FindProperty("entries");
            var tileEntries=new List<TileCatalog.Entry>();
            for(int i=0;i<entries.arraySize;i++) { var entry=entries.GetArrayElementAtIndex(i); tileEntries.Add(new TileCatalog.Entry {key=entry.FindPropertyRelative("key").stringValue,prefab=entry.FindPropertyRelative("prefab").objectReferenceValue as GameObject}); }
            foreach(var prefab in dry.Concat(lush)) if(!tileEntries.Any(e=>e.prefab==prefab)) tileEntries.Add(new TileCatalog.Entry {key=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)),prefab=prefab});
            tileCatalog.SetEntries(tileEntries); Dirty(tileCatalog);
            var root=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="IslandSystem") ?? new GameObject("IslandSystem");
            var runtime=root.GetComponent<IslandRuntimeManager>() ?? root.AddComponent<IslandRuntimeManager>();
            var runtimeSo=new SerializedObject(runtime); runtimeSo.FindProperty("catalog").objectReferenceValue=catalog; runtimeSo.ApplyModifiedPropertiesWithoutUndo();
            if(root.GetComponent<IslandNavigationUI>()==null) root.AddComponent<IslandNavigationUI>();
            if(root.GetComponent<WetBiomePresentation>()==null) root.AddComponent<WetBiomePresentation>();
            EditorSceneManager.MarkSceneDirty(scene);
            foreach (var asset in changedAssets) AssetDatabase.SaveAssetIfDirty(asset);
            if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("MapBuilding save failed.");
            return new {nature=nature.Count,wetCreatures=creatures.Count,biomePrefabs=catalog.biomeVisuals.Count*4,
                starterClusters=catalog.islands[0].CreateBoundary().ClusterCapacity,wetClusters=catalog.islands[1].CreateBoundary().ClusterCapacity,
                scene=scene.path,originalSceneDirty=active.isDirty};
        }
        finally { SceneManager.SetActiveScene(active); if (openedHere) EditorSceneManager.CloseScene(scene,true); }
    }
}
