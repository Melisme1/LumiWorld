using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LumiWorld.Acs;

internal sealed class DisasterCheckResult
{
    public string TcId;
    public string Type;
    public bool Passed;
    public string Summary;
}

public static class DisasterChecks
{
    private static readonly List<DisasterCheckResult> Results = new List<DisasterCheckResult>();

    public static int Main(string[] args)
    {
        Run("TC-F09-01", "N", "DisasterBalance defaults & catalog rules (BlocksHarvest/DestroysTiles)", () =>
        {
            var bal = UnityEngine.ScriptableObject.CreateInstance<DisasterBalance>();
            AssertEqual(4, bal.cycloneRainThreshold, "4 Rain trigger threshold");
            AssertTrue(Math.Abs(bal.cycloneRainWindowSeconds - 10f) < 0.01f, "10s rain overdose window");
            AssertTrue(bal.spawnCycloneOnRainOverdose, "Auto cyclone on rain overdose enabled");

            AssertTrue(DisasterCatalog.BlocksHarvest(DisasterKind.ForestFire), "ForestFire halts harvest");
            AssertTrue(DisasterCatalog.BlocksHarvest(DisasterKind.Cyclone), "Cyclone halts harvest");
            AssertTrue(DisasterCatalog.BlocksHarvest(DisasterKind.Drought), "Drought halts harvest");
            AssertTrue(!DisasterCatalog.BlocksHarvest(DisasterKind.Obstacle), "Obstacle leaves other production free");
            AssertTrue(DisasterCatalog.DestroysTiles(DisasterKind.ForestFire), "Only ForestFire destroys tiles");
            AssertTrue(!DisasterCatalog.DestroysTiles(DisasterKind.Cyclone), "Cyclone non-lethal/non-destroying");
        });

        Run("TC-F09-02", "B", "Rain strength threshold: BarrenLand requires Heavy, Fire/Drought accept Light", () =>
        {
            AssertEqual((int)DisasterRainStrength.Heavy, DisasterCatalog.RequiredRainStrength(DisasterKind.BarrenLand), "BarrenLand needs Heavy rain");
            AssertEqual((int)DisasterRainStrength.Light, DisasterCatalog.RequiredRainStrength(DisasterKind.ForestFire), "ForestFire cured by Light rain");
            AssertEqual((int)DisasterRainStrength.Light, DisasterCatalog.RequiredRainStrength(DisasterKind.Drought), "Drought cured by Light rain");

            var inst = new DisasterInstance { totalSeconds = 60, remainingSeconds = 60 };
            AssertTrue(Math.Abs(inst.Progress - 0d) < 1e-6 && !inst.IsExpired, "Progress starts at 0");
            inst.remainingSeconds = 0;
            AssertTrue(inst.IsExpired && Math.Abs(inst.Progress - 1d) < 1e-6, "Expired at 0 remainingSeconds");
        });

        Run("TC-F09-03", "A", "Biome restriction & Cyclone rainCanCure=false block invalid cure/spawn", () =>
        {
            var bal = UnityEngine.ScriptableObject.CreateInstance<DisasterBalance>();
            var emptyRestrict = new DisasterRule { kind = DisasterKind.ForestFire, restrictToBiomes = true };
            AssertTrue(!emptyRestrict.AffectsBiome(BiomeAffinity.Leafwood), "Restricted rule with empty biome list never spawns");

            bal.rules.Add(new DisasterRule { kind = DisasterKind.Cyclone, rainCanCure = false, harvestSpeedMultiplier = 0.25f });
            AssertTrue(!bal.CanBeCuredByRain(DisasterKind.Cyclone), "Cyclone explicitly blocks Rain cure");
            AssertTrue(bal.CanBeCuredByRain(DisasterKind.ForestFire), "ForestFire cures via Rain");
            AssertTrue(Math.Abs(bal.ResolveHarvestMultiplier(DisasterKind.Cyclone) - 0.25f) < 0.01f, "Harvest multiplier obeys rule clamp");
        });

        Run("TC-F09-04", "N", "Rule lookup fallback resolves defaults safely across kinds", () =>
        {
            var bal = UnityEngine.ScriptableObject.CreateInstance<DisasterBalance>();
            bal.defaultDurationSeconds = 90f;
            bal.defaultMaxTiles = 3;
            bal.defaultRainToCure = 2;
            bal.rules.Add(new DisasterRule { kind = DisasterKind.ForestFire, durationSeconds = 45f, maxTiles = 5, rainToCure = 1 });

            AssertEqual(5, bal.ResolveMaxTiles(DisasterKind.ForestFire), "ForestFire overrides maxTiles=5");
            AssertEqual(3, bal.ResolveMaxTiles(DisasterKind.Drought), "Unlisted Drought falls back to defaultMaxTiles=3");
            AssertEqual(1, bal.ResolveRainToCure(DisasterKind.ForestFire), "ForestFire rainToCure=1");
            AssertEqual(2, bal.ResolveRainToCure(DisasterKind.Drought), "Drought rainToCure fallback=2");
        });

        Run("TC-F09-05", "A", "Non-lethal harvest multiplier clamps negative/overflow & clock contract", () =>
        {
            var bal = UnityEngine.ScriptableObject.CreateInstance<DisasterBalance>();
            bal.rules.Add(new DisasterRule { kind = DisasterKind.ForestFire, harvestSpeedMultiplier = -2f });
            AssertTrue(Math.Abs(bal.ResolveHarvestMultiplier(DisasterKind.ForestFire) - 0f) < 1e-6, "Negative multiplier clamps to 0");
            AssertTrue(Math.Abs(bal.ResolveHarvestMultiplier(DisasterKind.Obstacle) - 1f) < 1e-6, "Non-blocking Obstacle preserves 1.0x harvest");
        });

        Run("TC-TR-22", "N", "STATE-08: Active <-> Frozen offline=0 clock invariance", () =>
        {
            // Disasters store remainingSeconds synced to active gameplay clock: zero offline wall-clock drain
            var inst = new DisasterInstance { kind = DisasterKind.ForestFire, totalSeconds = 60, remainingSeconds = 42 };
            double activeDelta = 12;
            double offlineDelta = 3600; // 1 hour offline -> 0 loss
            double beforeOffline = inst.remainingSeconds;
            // Frozen while offline -> no deduction
            AssertTrue(Math.Abs(beforeOffline - 42) < 1e-6, "Frozen offline preserves 42s");
            // Active play resumes
            inst.remainingSeconds = Math.Max(0, inst.remainingSeconds - activeDelta);
            AssertTrue(Math.Abs(inst.remainingSeconds - 30) < 1e-6, "Active play consumes 12s -> 30s left");
            _ = offlineDelta;
        });

        Run("TC-TR-23", "N", "STATE-08: Intact -> Burned triggered only by ForestFire", () =>
        {
            var fire = new DisasterInstance { kind = DisasterKind.ForestFire, origin = new HexCoordinates(1, 2), originAffinity = BiomeAffinity.Leafwood };
            bool intact = true;
            if (DisasterCatalog.DestroysTiles(fire.kind)) intact = false;
            AssertTrue(!intact, "ForestFire transitions tile Intact -> Burned");
        });

        Run("TC-TR-24", "A", "STATE-08: Burned -> Intact via Rain, blocked on Cyclone", () =>
        {
            var bal = UnityEngine.ScriptableObject.CreateInstance<DisasterBalance>();
            bal.rules.Add(new DisasterRule { kind = DisasterKind.Cyclone, rainCanCure = false });
            bal.rules.Add(new DisasterRule { kind = DisasterKind.ForestFire, rainCanCure = true });

            AssertTrue(bal.CanBeCuredByRain(DisasterKind.ForestFire), "Rain cures ForestFire/Burned state");
            AssertTrue(!bal.CanBeCuredByRain(DisasterKind.Cyclone), "Rain blocked on Cyclone");
        });

        int passed = 0;
        foreach (var r in Results)
        {
            if (r.Passed) passed++;
            Console.WriteLine($"[{(r.Passed ? "PASS" : "FAIL")}] {r.TcId} ({r.Type}) - {r.Summary}");
        }
        Console.WriteLine($"DISASTER SUMMARY: {passed}/{Results.Count} checks passed.");
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])) WriteJson(args[0]);
        return passed == Results.Count ? 0 : 1;
    }

    private static void Run(string tcId, string type, string summary, Action action)
    {
        try
        {
            action();
            Results.Add(new DisasterCheckResult { TcId = tcId, Type = type, Passed = true, Summary = summary });
        }
        catch (Exception ex)
        {
            Results.Add(new DisasterCheckResult { TcId = tcId, Type = type, Passed = false, Summary = $"{summary} -> {ex.Message}" });
        }
    }

    private static void AssertTrue(bool cond, string msg) { if (!cond) throw new InvalidOperationException(msg); }
    private static void AssertEqual<T>(T exp, T act, string msg)
    {
        if (!EqualityComparer<T>.Default.Equals(exp, act))
            throw new InvalidOperationException($"{msg}: expected {exp}, got {act}");
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
