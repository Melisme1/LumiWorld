using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LumiWorld.Acs.Islands;

internal sealed class IslandCheckResult
{
    public string TcId;
    public string Type;
    public bool Passed;
    public string Summary;
}

public static class IslandChecks
{
    private static readonly List<IslandCheckResult> Results = new List<IslandCheckResult>();

    public static int Main(string[] args)
    {
        Run("TC-F08-01", "N", "Discover Wet Island and unlock atomically for 200 Coins", () =>
        {
            WorldLaunch.IsVisit = false;
            var save = new EconomySaveSystem();
            var wallet = new CurrencyWallet(500);
            var store = new EconomyIslandAccessStore(save, wallet);
            var svc = new IslandAccessService(store);

            var events = new List<(string id, IslandAccessStage stage)>();
            svc.Changed += (id, st) => events.Add((id, st));

            AssertEqual(IslandAccessStage.Hidden, svc.GetStage("wet"), "Starts Hidden");
            AssertTrue(svc.Discover("wet"), "Discover succeeds on Hidden island");
            AssertEqual(IslandAccessStage.Discovered, svc.GetStage("wet"), "Stage becomes Discovered");
            AssertTrue(!svc.Discover("wet"), "Duplicate Discover is idempotent no-op");

            var unlock = svc.Unlock("wet", 200);
            AssertEqual(IslandUnlockResult.Success, unlock, "Unlock succeeds with 200 Coins");
            AssertEqual(IslandAccessStage.Unlocked, svc.GetStage("wet"), "Stage becomes Unlocked");
            AssertEqual(300, wallet.Coins, "Deducts exact 200 Coins");
            AssertEqual(2, events.Count, "Emits 2 stage change notifications");
        });

        Run("TC-F08-02", "B", "Unlock boundary: 199 Coins fails cleanly, 200 Coins exact succeeds", () =>
        {
            WorldLaunch.IsVisit = false;
            var save = new EconomySaveSystem();
            var wallet = new CurrencyWallet(199);
            var svc = new IslandAccessService(new EconomyIslandAccessStore(save, wallet));
            svc.Discover("wet");

            AssertEqual(IslandUnlockResult.InsufficientCoins, svc.Unlock("wet", 200), "199 Coins rejected");
            AssertEqual(199, wallet.Coins, "Wallet untouched on rejection");
            AssertEqual(IslandAccessStage.Discovered, svc.GetStage("wet"), "Stage stays Discovered");

            wallet.Add(1, CoinReason.StarterKit); // exactly 200 Coins
            AssertEqual(IslandUnlockResult.Success, svc.Unlock("wet", 200), "Exact 200 Coins boundary passes");
            AssertEqual(0, wallet.Coins, "Wallet drains to 0 cleanly");
        });

        Run("TC-F08-03", "A", "Reject unlock when Hidden, AlreadyUnlocked, negative cost, or blank ID", () =>
        {
            WorldLaunch.IsVisit = false;
            var save = new EconomySaveSystem();
            var wallet = new CurrencyWallet(400);
            var svc = new IslandAccessService(new EconomyIslandAccessStore(save, wallet));

            AssertEqual(IslandUnlockResult.NotDiscovered, svc.Unlock("wet", 200), "Hidden unlock rejected");
            AssertEqual(400, wallet.Coins, "No Coins charged when Hidden");

            svc.Discover("wet");
            AssertEqual(IslandUnlockResult.Success, svc.Unlock("wet", 200), "First unlock succeeds");
            AssertEqual(IslandUnlockResult.AlreadyUnlocked, svc.Unlock("wet", 200), "Double unlock returns AlreadyUnlocked");
            AssertEqual(200, wallet.Coins, "Double unlock never double-charges");

            Throws<ArgumentOutOfRangeException>(() => svc.Unlock("wet", -1), "Negative coin cost rejected");
            Throws<ArgumentException>(() => svc.GetStage("  "), "Blank islandId rejected");
        });

        Run("TC-F08-04", "N", "Save failure atomically rolls back stage & refunds Coins", () =>
        {
            WorldLaunch.IsVisit = false;
            var save = new EconomySaveSystem();
            var wallet = new CurrencyWallet(300);
            var svc = new IslandAccessService(new EconomyIslandAccessStore(save, wallet));
            svc.Discover("wet");

            save.ShouldFailSave = true;
            var res = svc.Unlock("wet", 200);
            AssertEqual(IslandUnlockResult.SaveFailed, res, "Save fail returns SaveFailed");
            AssertEqual(IslandAccessStage.Discovered, svc.GetStage("wet"), "Stage rolled back to Discovered");
            AssertEqual(300, wallet.Coins, "200 Coins refunded cleanly");
        });

        Run("TC-F08-05", "A", "Read-only Visit mode blocks island Discover & Unlock mutations", () =>
        {
            WorldLaunch.IsVisit = false;
            var save = new EconomySaveSystem();
            var wallet = new CurrencyWallet(500);
            var svc = new IslandAccessService(new EconomyIslandAccessStore(save, wallet));

            WorldLaunch.IsVisit = true;
            AssertTrue(!svc.Discover("wet"), "Discover blocked in Visit mode");
            AssertEqual(IslandAccessStage.Hidden, svc.GetStage("wet"), "Remains Hidden");
            AssertEqual(IslandUnlockResult.NotDiscovered, svc.Unlock("wet", 200), "Unlock blocked in Visit mode");
            AssertEqual(500, wallet.Coins, "Wallet untouched during Visit");
            WorldLaunch.IsVisit = false;
        });

        int passed = 0;
        foreach (var r in Results)
        {
            if (r.Passed) passed++;
            Console.WriteLine($"[{(r.Passed ? "PASS" : "FAIL")}] {r.TcId} ({r.Type}) - {r.Summary}");
        }
        Console.WriteLine($"ISLAND SUMMARY: {passed}/{Results.Count} checks passed.");
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])) WriteJson(args[0]);
        return passed == Results.Count ? 0 : 1;
    }

    private static void Run(string tcId, string type, string summary, Action action)
    {
        try
        {
            action();
            Results.Add(new IslandCheckResult { TcId = tcId, Type = type, Passed = true, Summary = summary });
        }
        catch (Exception ex)
        {
            Results.Add(new IslandCheckResult { TcId = tcId, Type = type, Passed = false, Summary = $"{summary} -> {ex.Message}" });
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
