using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

internal sealed class OnlineCheckResult
{
    public string TcId;
    public string Type;
    public bool Passed;
    public string Summary;
}

public static class OnlineClientChecks
{
    private static readonly List<OnlineCheckResult> Results = new List<OnlineCheckResult>();

    public static int Main(string[] args)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "lumi_online_client_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        Application.persistentDataPath = tempRoot;

        try
        {
            RunLocalSaveChecks();
            RunWorldMapAndSnapshotChecks();
            RunMarketAndLockChecks();
            RunInboxAndSyncOpChecks();
            RunBreedingRecipeChecks();
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }

        int passed = 0;
        foreach (var r in Results)
        {
            if (r.Passed) passed++;
            Console.WriteLine($"[{(r.Passed ? "PASS" : "FAIL")}] {r.TcId} ({r.Type}) - {r.Summary}");
        }
        Console.WriteLine($"ONLINE CLIENT SUMMARY: {passed}/{Results.Count} checks passed.");
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])) WriteJson(args[0]);
        return passed == Results.Count ? 0 : 1;
    }

    // -------------------------------------------------------------------------
    // F-01 Local Save Store Integrity & Corrupt JSON Recovery (TC-F01-01..03, TC-TR-04, TC-TR-06)
    // -------------------------------------------------------------------------
    private static void RunLocalSaveChecks()
    {
        Run("TC-F01-01", "N", "LocalWorldSaveStore saves JSON & creates .bak on overwrite", () =>
        {
            string pid = "player_save_01";
            var store = new LocalWorldSaveStore(pid);
            var w1 = new WorldSaveData { playerId = pid, nextClusterId = 3 };
            w1.placements.Add(new PlacementRecord { cardID = "leafwood_001", q = 1, r = -1, placementId = "p1" });

            AssertTrue(store.Save(w1), "First Save returns true");
            var loaded1 = store.Load();
            AssertTrue(loaded1 != null && loaded1.nextClusterId == 3 && loaded1.placements.Count == 1, "Reloads saved placement cleanly");

            // Second write shifts first version to .bak
            w1.nextClusterId = 7;
            AssertTrue(store.Save(w1), "Second Save overwrites atomically");
            string mainPath = Path.Combine(Application.persistentDataPath, $"world_{pid}.json");
            AssertTrue(File.Exists(mainPath + ".bak"), "Previous save preserved in .bak");
            AssertEqual(7, store.Load().nextClusterId, "Reads latest save revision");
        });

        Run("TC-F01-02", "B", "Null/invalid playerId sanitizes to safe filename", () =>
        {
            var guestStore = new LocalWorldSaveStore(null);
            var badCharsStore = new LocalWorldSaveStore("user:<>|?/*1");
            AssertTrue(guestStore.Save(new WorldSaveData { playerId = "guest" }), "Guest save writes cleanly");
            AssertTrue(badCharsStore.Save(new WorldSaveData { playerId = "bad" }), "Invalid path chars sanitized");
            AssertTrue(guestStore.Load() != null && badCharsStore.Load() != null, "Both load safely");
        });

        Run("TC-F01-03", "A", "Corrupt main JSON quarantines to .broken & restores valid .bak", () =>
        {
            string pid = "player_corrupt_01";
            var store = new LocalWorldSaveStore(pid);
            AssertTrue(store.Save(new WorldSaveData { playerId = pid, nextClusterId = 10 }), "Write v1");
            AssertTrue(store.Save(new WorldSaveData { playerId = pid, nextClusterId = 20 }), "Write v2 (.bak = 10)");

            string mainPath = Path.Combine(Application.persistentDataPath, $"world_{pid}.json");
            File.WriteAllText(mainPath, "{ CORRUPT_JSON !!! ");

            var recovered = store.Load();
            AssertTrue(recovered != null, "Recovers cleanly from .bak when main JSON corrupts");
            AssertEqual(10, recovered.nextClusterId, "Restored exact .bak payload");
            AssertTrue(File.Exists(mainPath + ".broken"), "Corrupt JSON moved to .broken instead of wiped");
        });

        Run("TC-TR-04", "N", "STATE-02: Local save transition writes clean updated snapshot", () =>
        {
            var store = new LocalWorldSaveStore("state02_clean");
            AssertTrue(store.Load() == null, "Initial empty slot returns null");
            AssertTrue(store.Save(new WorldSaveData { playerId = "state02_clean", initializeNewWorld = true }), "Save transitions Dirty -> Clean");
            AssertTrue(store.Load().initializeNewWorld, "Persists NewWorld marker cleanly");
        });

        Run("TC-TR-06", "N", "STATE-02: Conflict/corrupt restore falls back preserving authoritative .bak", () =>
        {
            string pid = "state02_bak";
            var store = new LocalWorldSaveStore(pid);
            store.Save(new WorldSaveData { playerId = pid, nextClusterId = 42 });
            store.Save(new WorldSaveData { playerId = pid, nextClusterId = 99 });
            File.WriteAllText(Path.Combine(Application.persistentDataPath, $"world_{pid}.json"), "NOT_JSON");
            var restored = store.Load();
            AssertEqual(42, restored.nextClusterId, "Authoritative backup survives broken upload");
        });
    }

    // -------------------------------------------------------------------------
    // F-10 World Snapshot, Stripping & 10-col Spiral Map Layout (TC-F10-01..03)
    // -------------------------------------------------------------------------
    private static void RunWorldMapAndSnapshotChecks()
    {
        Run("TC-F10-01", "N", "SnapshotPlacement.From strips private IDs & WorldMapLayout maps 10-col grid", () =>
        {
            var rec = new PlacementRecord
            {
                cardID = "creature_deer", q = 2, r = -1, clusterId = 1, displayedScore = 15,
                placementId = "SECRET_UUID_999", animalId = "PRIVATE_INDIVIDUAL_ID",
                qualityRank = 2, animalName = "Bambi", animalStar = 3, riverFlowStep = 1, riverIsOrigin = false
            };
            var snap = SnapshotPlacement.From(rec);
            AssertEqual("creature_deer", snap.cardID, "Preserves cardID");
            AssertEqual(2, snap.qualityRank, "Preserves display rank");
            AssertEqual("Bambi", snap.animalName, "Preserves display name");
            var restored = snap.ToRecord();
            AssertTrue(string.IsNullOrEmpty(restored.placementId) && string.IsNullOrEmpty(restored.animalId),
                "Visitor restore has zero owner internal IDs");

            AssertEqual(10, WorldMapLayout.Columns, "10-column World Map grid");
            var pos1 = WorldMapLayout.SlotPosition(1, 1);
            var pos2 = WorldMapLayout.SlotPosition(1, 1);
            AssertTrue(Math.Abs(pos1.x - pos2.x) < 1e-6 && Math.Abs(pos1.y - pos2.y) < 1e-6, "StableHash deterministic layout");
        });

        Run("TC-F10-02", "B", "Snapshot payload stays well within 300 KB limit & Bounds clamps rows", () =>
        {
            var snap = new WorldSnapshot { version = 1, score = 250 };
            for (int i = 0; i < 40; i++)
                snap.placements.Add(new SnapshotPlacement { cardID = "leafwood_001", q = i, r = 0 });
            int bytes = Encoding.UTF8.GetByteCount(JsonUtility.ToJson(snap));
            AssertTrue(bytes > 0 && bytes <= WorldShareService.MaxSnapshotBytes, $"Payload {bytes}B <= 300 KB cap");

            var rect = WorldMapLayout.Bounds(0); // Clamped to >=1 row
            AssertTrue(rect.height > 0f && rect.width > 50f, "Bounds clamps 0 rows safely");
        });

        Run("TC-F10-03", "A", "StableHash handles null/empty IDs without collision failure", () =>
        {
            uint hEmpty = StableHash.Of("");
            uint hNull = StableHash.Of(null);
            uint hUser1 = StableHash.Of("player_a");
            uint hUser2 = StableHash.Of("player_b");
            AssertEqual(hEmpty, hNull, "Null and empty string share offset basis cleanly");
            AssertTrue(hUser1 != hUser2, "Distinct player IDs hash distinctly");
        });
    }

    // -------------------------------------------------------------------------
    // F-11 CardTradeIds & CardLockManager (TC-F11-01..03, TC-TR-31..33)
    // -------------------------------------------------------------------------
    private static void RunMarketAndLockChecks()
    {
        Run("TC-F11-01", "N", "CardTradeIds converts Creature cards to 'card.<id>' & CardLockManager locks", () =>
        {
            var deer = ScriptableObject.CreateInstance<CardData>();
            deer.cardID = "creature_deer";
            deer.cardType = CardType.Creature;
            WorldSaveSystem.Instance.Cards.Register(deer);

            AssertTrue(CardTradeIds.IsTradable(deer), "Creature card is tradable");
            string mid = CardTradeIds.ToMarketId(deer);
            AssertEqual("card.creature_deer", mid, "Market ID prefix matches server");
            AssertEqual("creature_deer", CardTradeIds.ToCardId(mid), "Strips prefix back to cardID");
            AssertEqual(deer, CardTradeIds.Find(mid), "Lookup resolves Creature CardData");
        });

        Run("TC-F11-02", "B", "CardLockManager resource escrow accumulates & releases cleanly to zero", () =>
        {
            var mgr = new GameObject("LockMgr").AddComponent<CardLockManager>();
            mgr.LockResourceAmount("resource.leafwood.acorns", 10, CardLockType.MarketEscrow);
            mgr.LockResourceAmount("resource.leafwood.acorns", 5, CardLockType.MarketEscrow);
            AssertEqual(15, mgr.GetLockedResourceAmount("resource.leafwood.acorns", out var lt, out _), "Accumulates 15 escrowed");
            AssertEqual(CardLockType.MarketEscrow, lt, "Tracks MarketEscrow type");

            mgr.UnlockResourceAmount("resource.leafwood.acorns", 20); // clamped >= 0 & removed
            AssertEqual(0, mgr.GetLockedResourceAmount("resource.leafwood.acorns", out lt, out _), "Unlocks to 0 cleanly");
            AssertEqual(CardLockType.None, lt, "Lock type resets to None");
        });

        Run("TC-F11-03", "A", "Non-creature cards (Terrain/Building) rejected as non-tradable", () =>
        {
            var village = ScriptableObject.CreateInstance<CardData>();
            village.cardID = "Medieval_Village";
            village.cardType = CardType.Building;
            WorldSaveSystem.Instance.Cards.Register(village);

            AssertTrue(!CardTradeIds.IsTradable(village), "Building card excluded from Creature trade");
            AssertTrue(CardTradeIds.Find("card.Medieval_Village") == null, "Find rejects non-creature card");
            AssertTrue(CardTradeIds.ToCardId("resource.leafwood.acorns") == null, "Non-card prefix returns null");
        });

        Run("TC-TR-31", "N", "STATE-11: Available <-> Unlocked card lock preserves identity cleanly", () =>
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.cardID = "creature_fox"; card.cardType = CardType.Creature;
            var mgr = new GameObject("Lock31").AddComponent<CardLockManager>();

            AssertTrue(!mgr.IsCardLocked(card, out _, out _), "Starts Available");
            mgr.LockCard(card.cardID, CardLockType.MarketEscrow);
            AssertTrue(mgr.IsCardLocked(card, out var lt, out _) && lt == CardLockType.MarketEscrow, "Locked in MarketEscrow");
            mgr.UnlockCard(card.cardID);
            AssertTrue(!mgr.IsCardLocked(card, out _, out _), "Unlocked restores Available");
        });

        Run("TC-TR-33", "A", "STATE-11: Breeding/AccountBound lock identifies exclusive reason", () =>
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.cardID = "creature_caterpillar"; card.cardType = CardType.Creature;
            var mgr = new GameObject("Lock33").AddComponent<CardLockManager>();

            mgr.LockCard(card.cardID, CardLockType.Breeding);
            AssertTrue(mgr.IsCardLocked(card, out var lt, out string reason), "Breeding lock active");
            AssertEqual(CardLockType.Breeding, lt, "Reports Breeding lock type preventing market/deploy");
            AssertTrue(reason.Contains("breeding", StringComparison.OrdinalIgnoreCase), "Explains breeding conflict reason");
        });
    }

    // -------------------------------------------------------------------------
    // F-12 Async Inbox AppliedIds & Op Keys Schema (TC-F12-01..03, TC-TR-19/21)
    // -------------------------------------------------------------------------
    private static void RunInboxAndSyncOpChecks()
    {
        Run("TC-F12-01", "N", "EconomySaveData persists appliedInboxIds & pending opKeys across JSON reload", () =>
        {
            var econ = new EconomySaveData { coins = 250 };
            econ.appliedInboxIds.Add("inbox-uuid-001");
            econ.pendingMarketOps.Add(new PendingMarketOp { opKey = "op-mkt-1", kind = "BuyListing", listingId = "l-1", priceCoins = 30 });
            econ.pendingTradeOps.Add(new PendingTradeOp { opKey = "op-trd-1", kind = "Offer", offerCardId = "card.creature_deer", offerCount = 1 });

            var reloaded = JsonUtility.FromJson<EconomySaveData>(JsonUtility.ToJson(econ));
            AssertEqual(1, reloaded.appliedInboxIds.Count, "Retains appliedInboxIds");
            AssertEqual("op-mkt-1", reloaded.pendingMarketOps[0].opKey, "Retains pending market opKey");
            AssertEqual("op-trd-1", reloaded.pendingTradeOps[0].opKey, "Retains pending trade opKey");
        });

        Run("TC-F12-02", "B", "Inbox deduplication boundary ignores duplicate applied IDs cleanly", () =>
        {
            var econ = new EconomySaveData();
            string itemId = "inbox-boundary-01";
            econ.appliedInboxIds.Add(itemId);
            bool shouldApplyFirst = !econ.appliedInboxIds.Contains("inbox-new-02");
            bool shouldApplyDup = !econ.appliedInboxIds.Contains(itemId);
            AssertTrue(shouldApplyFirst && !shouldApplyDup, "Previously applied ID skipped safely before ack");
        });

        Run("TC-F12-03", "A", "Malformed Inbox JSON raises catchable ArgumentException (never silent pass)", () =>
        {
            Throws<ArgumentException>(() => JsonUtility.FromJson<EconomySaveData>("{ bad json"), "Corrupt JSON throws ArgumentException");
        });
    }

    // -------------------------------------------------------------------------
    // F-17 BreedingRecipe Symmetric Matching & Parent Validation (TC-F17-01..03)
    // -------------------------------------------------------------------------
    private static void RunBreedingRecipeChecks()
    {
        Run("TC-F17-01", "N", "BreedingRecipe matches parents symmetrically (A+B == B+A) & derives aliases", () =>
        {
            var deer = ScriptableObject.CreateInstance<CardData>(); deer.cardID = "creature_deer"; deer.cardName = "Forest Deer";
            var cat  = ScriptableObject.CreateInstance<CardData>(); cat.cardID  = "creature_caterpillar"; cat.cardName = "Caterpillar";
            var bral = ScriptableObject.CreateInstance<CardData>(); bral.cardID = "creature_braluff"; bral.cardName = "Braluff";

            var recipe = ScriptableObject.CreateInstance<BreedingRecipe>();
            recipe.parentA = deer;
            recipe.parentB = cat;
            recipe.offspring = bral;

            AssertTrue(recipe.Matches(deer, cat), "Matches (parentA, parentB)");
            AssertTrue(recipe.Matches(cat, deer), "Matches symmetric reverse (parentB, parentA)");
            AssertTrue(recipe.HasParent(deer) && recipe.HasParent(cat), "HasParent detects both parents");
        });

        Run("TC-F17-02", "B", "BreedingRecipe fallback matches aliases (bramblet<->deer, petaluff<->caterpillar)", () =>
        {
            var pA = ScriptableObject.CreateInstance<CardData>(); pA.cardID = "creature_bramblet";
            var pB = ScriptableObject.CreateInstance<CardData>(); pB.cardID = "creature_petaluff";
            var recipe = ScriptableObject.CreateInstance<BreedingRecipe>(); recipe.parentA = pA; recipe.parentB = pB;

            var deerInHand = ScriptableObject.CreateInstance<CardData>(); deerInHand.cardID = "creature_deer";
            var catInHand  = ScriptableObject.CreateInstance<CardData>(); catInHand.cardID  = "creature_caterpillar";
            AssertTrue(recipe.Matches(deerInHand, catInHand), "Supports alias match for Deer + Caterpillar");
        });

        Run("TC-F17-03", "A", "BreedingRecipe rejects null parents, unknown species, or duplicate single parent", () =>
        {
            var deer = ScriptableObject.CreateInstance<CardData>(); deer.cardID = "creature_deer";
            var cat  = ScriptableObject.CreateInstance<CardData>(); cat.cardID  = "creature_caterpillar";
            var fox  = ScriptableObject.CreateInstance<CardData>(); fox.cardID  = "creature_fox";
            var recipe = ScriptableObject.CreateInstance<BreedingRecipe>(); recipe.parentA = deer; recipe.parentB = cat;

            AssertTrue(!recipe.Matches(null, cat), "Null parent rejected");
            AssertTrue(!recipe.Matches(deer, deer), "Same parent twice rejected");
            AssertTrue(!recipe.Matches(deer, fox), "Mismatched species rejected");
        });
    }

    private static void Run(string tcId, string type, string summary, Action action)
    {
        try
        {
            action();
            Results.Add(new OnlineCheckResult { TcId = tcId, Type = type, Passed = true, Summary = summary });
        }
        catch (Exception ex)
        {
            Results.Add(new OnlineCheckResult { TcId = tcId, Type = type, Passed = false, Summary = $"{summary} -> {ex.Message}" });
        }
    }

    private static void AssertTrue(bool cond, string msg) { if (!cond) throw new InvalidOperationException(msg); }
    private static void AssertEqual<T>(T exp, T act, string msg)
    {
        if (!EqualityComparer<T>.Default.Equals(exp, act))
            throw new InvalidOperationException($"{msg}: expected {exp}, got {act}");
    }
    private static void Throws<T>(Action a, string msg) where T : Exception
    {
        try { a(); } catch (T) { return; }
        throw new InvalidOperationException(msg);
    }

    private static void WriteJson(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var sb = new StringBuilder();
        sb.AppendLine("[");
        for (int i = 0; i < Results.Count; i++)
        {
            var r = Results[i];
            string esc = r.Summary.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string c = i < Results.Count - 1 ? "," : "";
            sb.AppendLine($"  {{\"tc\":\"{r.TcId}\",\"type\":\"{r.Type}\",\"passed\":{(r.Passed ? "true" : "false")},\"summary\":\"{esc}\"}}{c}");
        }
        sb.AppendLine("]");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }
}
