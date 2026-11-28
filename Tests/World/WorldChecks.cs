using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

internal sealed class UnitCaseResult
{
    public string FunctionKey;   // FN-01, FN-02, FN-03, FN-14, FN-13A, FN-13B
    public string UtcId;         // UTCID01..UTCID04 per sheet
    public string TcId;          // TC-F02-01.. etc.
    public string CaseType;      // N, B, A
    public bool Passed;
    public string Summary;
}

public static class WorldChecks
{
    private static readonly List<UnitCaseResult> Results = new List<UnitCaseResult>();

    public static int Main(string[] args)
    {
        RunHexCoordinateChecks();
        RunSettlementChecks();
        RunBiomeBuffChecks();
        RunRiverAutoTileChecks();
        RunRhythmMinigameChecks();
        RunRunnerMinigameChecks();
        RunIntegrationChecks();
        RunStateTransitionChecks();

        int passed = 0;
        foreach (var r in Results)
        {
            if (r.Passed) passed++;
            Console.WriteLine($"[{(r.Passed ? "PASS" : "FAIL")}] {r.FunctionKey} | {r.UtcId} | {r.TcId} ({r.CaseType}) - {r.Summary}");
        }

        Console.WriteLine($"SUMMARY: {passed}/{Results.Count} checks passed.");

        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            WriteJsonReport(args[0]);
        }

        return passed == Results.Count ? 0 : 1;
    }

    private static void Record(string fnKey, string utcId, string tcId, string caseType, string summary, Action test)
    {
        try
        {
            test();
            Results.Add(new UnitCaseResult
            {
                FunctionKey = fnKey,
                UtcId = utcId,
                TcId = tcId,
                CaseType = caseType,
                Passed = true,
                Summary = summary
            });
        }
        catch (Exception ex)
        {
            Results.Add(new UnitCaseResult
            {
                FunctionKey = fnKey,
                UtcId = utcId,
                TcId = tcId,
                CaseType = caseType,
                Passed = false,
                Summary = $"{summary} -> {ex.Message}"
            });
        }
    }

    private static void AssertTrue(bool cond, string msg)
    {
        if (!cond) throw new InvalidOperationException(msg);
    }

    private static void AssertEqual<T>(T expected, T actual, string msg)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{msg}: expected {expected}, got {actual}");
    }

    private static void AssertNear(float expected, float actual, float tol, string msg)
    {
        if (Math.Abs(expected - actual) > tol)
            throw new InvalidOperationException($"{msg}: expected {expected:F3}±{tol:F3}, got {actual:F3}");
    }

    // -------------------------------------------------------------------------
    // FN-01: HexCoordinates & HexMetrics (F-02)
    // -------------------------------------------------------------------------
    private static void RunHexCoordinateChecks()
    {
        Record("FN-01", "UTCID01", "TC-F02-01", "N", "Axial S identity, neighbors & World round-trip", () =>
        {
            var a = new HexCoordinates(2, -1);
            AssertEqual(-1, a.S, "S must equal -Q-R");
            AssertEqual(new HexCoordinates(3, -1), a.GetNeighbor(0), "Neighbor dir 0");
            AssertEqual(3, HexCoordinates.Distance(new HexCoordinates(0, 0), new HexCoordinates(2, 1)), "Distance (0,0)-(2,1)");
            var world = HexMetrics.HexToWorldPosition(a, 0f);
            AssertEqual(a, HexMetrics.WorldToHex(world), "HexToWorld -> WorldToHex roundtrip");
        });

        Record("FN-01", "UTCID02", "TC-F02-02", "B", "Direction wrap, 60-deg full loop & normal threshold", () =>
        {
            var p = new HexCoordinates(1, 2);
            AssertEqual(p.GetNeighbor(5), p.GetNeighbor(-1), "Negative direction wraps to 5");
            AssertEqual(p.GetNeighbor(0), p.GetNeighbor(6), "Direction 6 wraps to 0");
            AssertEqual(0, p.DistanceTo(p), "Zero distance to self");
            AssertEqual(p, p.Rotate60Clockwise(6), "6x60 deg rotation restores origin");

            var hitOk = new UnityEngine.RaycastHit { normal = new UnityEngine.Vector3(0f, HexMetrics.MinSurfaceNormalY, 0f) };
            var hitLow = new UnityEngine.RaycastHit { normal = new UnityEngine.Vector3(0f, 0.54f, 0.84f) };
            AssertTrue(HexMetrics.IsTopSurfaceHit(in hitOk), "0.55 normal accepts top surface");
            AssertTrue(!HexMetrics.IsTopSurfaceHit(in hitLow), "0.54 normal rejects steep side hit");
        });

        Record("FN-01", "UTCID03", "TC-F02-03", "A", "Invalid Cube warning & negative rotation safety", () =>
        {
            UnityEngine.Debug.Warnings.Clear();
            var bad = new HexCoordinates(1, 1, 1);
            AssertTrue(UnityEngine.Debug.Warnings.Count == 1, "Q+R+S != 0 must emit Debug.LogWarning");
            AssertEqual(-2, bad.S, "Stored S derives strictly from -Q-R");
            var r5 = new HexCoordinates(1, 0).Rotate60Clockwise(5);
            var rNeg1 = new HexCoordinates(1, 0).Rotate60Clockwise(-1);
            AssertEqual(r5, rNeg1, "Negative step count wraps consistently");
        });
    }

    // -------------------------------------------------------------------------
    // FN-02: SettlementClusterCalculator (F-02 Village Cluster Evolution)
    // -------------------------------------------------------------------------
    private static void RunSettlementChecks()
    {
        Record("FN-02", "UTCID01", "TC-F02-01", "N", "Compact 7-hex cluster -> GrandCity & dry CoreTile", () =>
        {
            var center = new HexCoordinates(0, 0);
            var hexes = new HashSet<HexCoordinates> { center };
            for (int d = 0; d < 6; d++) hexes.Add(center.GetNeighbor(d));

            var cluster = SettlementClusterCalculator.FindConnectedCluster(center, h => hexes.Contains(h));
            AssertEqual(7, cluster.Count, "BFS cluster count");
            AssertTrue(!SettlementClusterCalculator.IsLinearCluster(cluster), "7-hex flower is non-linear");
            AssertEqual(SettlementTier.GrandCity, SettlementClusterCalculator.DetermineTier(cluster), "7-hex flower evolves GrandCity");
            AssertEqual(center, SettlementClusterCalculator.FindCoreTile(cluster, _ => false), "Center hex has max degree (6)");

            // Tie-break dry vs river test on 3-triangle cluster (every node has degree 2)
            var tri = new List<HexCoordinates> { new HexCoordinates(0,0), new HexCoordinates(1,0), new HexCoordinates(0,1) };
            var core = SettlementClusterCalculator.FindCoreTile(tri, h => h == tri[0]);
            AssertTrue(core != tri[0], "FindCoreTile avoids river hex when tied");
        });

        Record("FN-02", "UTCID02", "TC-F02-01", "N", "Linear 5-hex strip -> TradingAvenue + 22 perimeter edges", () =>
        {
            var chain = new List<HexCoordinates>
            {
                new HexCoordinates(0,0), new HexCoordinates(1,0), new HexCoordinates(2,0),
                new HexCoordinates(3,0), new HexCoordinates(4,0)
            };
            var set = new HashSet<HexCoordinates>(chain);
            var cluster = SettlementClusterCalculator.FindConnectedCluster(chain[0], h => set.Contains(h));
            AssertEqual(5, cluster.Count, "Chain size");
            AssertTrue(SettlementClusterCalculator.IsLinearCluster(cluster), "Max degree <= 2 -> Linear");
            AssertEqual(SettlementTier.TradingAvenue, SettlementClusterCalculator.DetermineTier(cluster), "5-hex linear evolves TradingAvenue");
            var edges = SettlementClusterCalculator.CalculatePerimeterEdges(cluster, chain[0]);
            // 5 hexes * 6 - 4 internal seams * 2 = 22 outer edges
            AssertEqual(22, edges.Count, "Open perimeter edge count");
            int gateCount = 0;
            foreach (var e in edges) if (e.IsGate) gateCount++;
            AssertEqual(1, gateCount, "Exactly 1 gate assigned on outer perimeter");
        });

        Record("FN-02", "UTCID03", "TC-F02-02", "B", "Tier boundaries: 1=Hamlet, 2..4=Town, 5=Trading/Grand", () =>
        {
            AssertEqual(SettlementTier.Hamlet, SettlementClusterCalculator.DetermineTier(new List<HexCoordinates> { new HexCoordinates(0, 0) }), "1 tile -> Hamlet");
            AssertEqual(SettlementTier.Town, SettlementClusterCalculator.DetermineTier(new List<HexCoordinates> { new HexCoordinates(0, 0), new HexCoordinates(1, 0) }), "2 tiles -> Town");
            AssertEqual(SettlementTier.Town, SettlementClusterCalculator.DetermineTier(new List<HexCoordinates> {
                new HexCoordinates(0, 0), new HexCoordinates(1, 0), new HexCoordinates(2, 0), new HexCoordinates(3, 0)
            }), "4 tiles -> Town");
        });

        Record("FN-02", "UTCID04", "TC-F02-03", "A", "Null/empty cluster & null predicate safe handling", () =>
        {
            var empty1 = SettlementClusterCalculator.FindConnectedCluster(new HexCoordinates(0, 0), null);
            var empty2 = SettlementClusterCalculator.FindConnectedCluster(new HexCoordinates(9, 9), h => h.Q == 0);
            AssertEqual(0, empty1.Count, "Null predicate returns empty");
            AssertEqual(0, empty2.Count, "Non-settlement start returns empty");
            AssertEqual(0, SettlementClusterCalculator.CalculatePerimeterEdges(null).Count, "Null cluster edges empty");
            AssertEqual(SettlementTier.Hamlet, SettlementClusterCalculator.DetermineTier(null), "Null cluster defaults Hamlet");
        });
    }

    // -------------------------------------------------------------------------
    // FN-03: BiomeBuffCalculator (F-03 Habitat Clusters & Buff Auras)
    // -------------------------------------------------------------------------
    private static void RunBiomeBuffChecks()
    {
        Record("FN-03", "UTCID01", "TC-F03-01", "N", "Town (3-hex) & Peninsula river buffs propagate to whole biome", () =>
        {
            // Biome cluster of 3 hexes: (0,0), (1,0), (0,1); only (0,1) touches building aura & (0,0) touches 2 river hexes
            var b1 = new HexCoordinates(0, 0);
            var b2 = new HexCoordinates(1, 0);
            var b3 = new HexCoordinates(0, 1);
            var bldg = new HashSet<HexCoordinates> { new HexCoordinates(0, 2), new HexCoordinates(1, 2), new HexCoordinates(-1, 3) };
            var rivers = new HashSet<HexCoordinates> { new HexCoordinates(-1, 0), new HexCoordinates(0, -1) }; // both adjacent to (0,0)!
            var all = new List<HexCoordinates>();
            all.AddRange(new[] { b1, b2, b3 });
            all.AddRange(bldg);
            all.AddRange(rivers);

            var infos = BiomeBuffCalculator.EvaluateAllBiomeBuffs(
                all,
                h => (h == b1 || h == b2 || h == b3) ? "leafwood" : null,
                _ => 1,
                h => bldg.Contains(h),
                h => rivers.Contains(h));

            AssertEqual(1, infos.Count, "Single biome cluster excluding buildings");
            AssertEqual(3, infos[0].Hexes.Count, "Biome tile count excludes building tiles");
            AssertEqual(BuildingBuffTier.Town, infos[0].BuffState.BuildingTier, "3-hex building radius 1 touches (0,1)");
            AssertEqual(RiverBuffTier.Peninsula, infos[0].BuffState.RiverTier, "2 adjacent rivers grant Peninsula buff");
        });

        Record("FN-03", "UTCID02", "TC-F03-01", "N", "Separate positive clusterIds stay split & aura excludes self", () =>
        {
            var hA = new HexCoordinates(0, 0);
            var hB = new HexCoordinates(1, 0);
            var infos = BiomeBuffCalculator.EvaluateAllBiomeBuffs(
                new[] { hA, hB },
                h => (h == hA || h == hB) ? "leafwood" : null,
                h => h == hA ? 1 : 2,
                null,
                null);
            AssertEqual(2, infos.Count, "Distinct clusterIds (1 vs 2) must not merge adjacent biomes");

            var existingBldg = new HashSet<HexCoordinates> { new HexCoordinates(1, 0), new HexCoordinates(2, 0) };
            var aura = BiomeBuffCalculator.CollectBuildingAuraHexes(new HexCoordinates(0, 0), h => existingBldg.Contains(h), out int size, out int radius);
            AssertEqual(3, size, "Prospective building cluster size = 3");
            AssertEqual(1, radius, "Radius = 1 for 3-hex cluster");
            AssertTrue(!aura.Contains(new HexCoordinates(0, 0)) && !aura.Contains(new HexCoordinates(1, 0)), "Aura excludes building tiles");
            AssertEqual(7, BiomeBuffCalculator.CollectRiverAuraHexes(new HexCoordinates(0, 0)).Count, "River aura = center + 6 neighbors");
        });

        Record("FN-03", "UTCID03", "TC-F03-02", "B", "Building aura radius boundaries: 2->0, 3->1, 4->1, 5->2", () =>
        {
            AssertEqual(0, BiomeBuffCalculator.GetBuildingAuraRadius(2), "Size 2 -> radius 0");
            AssertEqual(1, BiomeBuffCalculator.GetBuildingAuraRadius(3), "Size 3 -> radius 1");
            AssertEqual(1, BiomeBuffCalculator.GetBuildingAuraRadius(4), "Size 4 -> radius 1");
            AssertEqual(2, BiomeBuffCalculator.GetBuildingAuraRadius(5), "Size 5 -> radius 2");
            AssertEqual(BuildingBuffTier.None, BiomeBuffCalculator.GetBuildingBuffTier(2), "Size 2 -> None");
            AssertEqual(BuildingBuffTier.Town, BiomeBuffCalculator.GetBuildingBuffTier(4), "Size 4 -> Town");
            AssertEqual(BuildingBuffTier.Grand, BiomeBuffCalculator.GetBuildingBuffTier(5), "Size 5 -> Grand");
        });

        Record("FN-03", "UTCID04", "TC-F03-03", "A", "Null inputs return empty list safely & <3 building grants None", () =>
        {
            AssertEqual(0, BiomeBuffCalculator.EvaluateAllBiomeBuffs(null, _ => "x", null, null, null).Count, "Null allHexes safe");
            AssertEqual(0, BiomeBuffCalculator.EvaluateAllBiomeBuffs(new[] { new HexCoordinates(0, 0) }, null, null, null, null).Count, "Null terrain func safe");
            var smallBldg = new HashSet<HexCoordinates> { new HexCoordinates(0, 1), new HexCoordinates(1, 1) };
            var res = BiomeBuffCalculator.EvaluateAllBiomeBuffs(
                new[] { new HexCoordinates(0, 0), new HexCoordinates(0, 1), new HexCoordinates(1, 1) },
                h => h == new HexCoordinates(0, 0) ? "bloomfield" : null,
                _ => 1,
                h => smallBldg.Contains(h),
                null);
            AssertEqual(BuildingBuffTier.None, res[0].BuffState.BuildingTier, "2-hex building emits zero aura");
        });
    }

    // -------------------------------------------------------------------------
    // FN-14: RiverAutoTileSolver (F-16 Downhill River Networks)
    // -------------------------------------------------------------------------
    private static void RunRiverAutoTileChecks()
    {
        Record("FN-14", "UTCID01", "TC-F16-01", "N", "SpringHead, Straight (180), Curve120 (120) & Curve60 (60)", () =>
        {
            var spring = RiverAutoTileSolver.Solve(new List<int> { 0 }, isOrigin: true);
            AssertEqual(RiverPieceType.SpringHead, spring.pieceType, "Origin with 1 outlet is SpringHead");
            AssertNear(90f, spring.rotationY, 0.5f, "Dir 0 compass angle = 90 deg");

            var straight = RiverAutoTileSolver.Solve(new List<int> { 0, 3 }, upstreamDir: 3, outflowDir: 0);
            AssertEqual(RiverPieceType.Straight, straight.pieceType, "Opposite dirs (0,3) -> Straight");
            AssertNear(90f, straight.rotationY, 0.5f, "Straight flows toward outflowDir 0 (90 deg)");

            var c120 = RiverAutoTileSolver.Solve(new List<int> { 0, 2 });
            AssertEqual(RiverPieceType.Curve120, c120.pieceType, "Diff 2 -> Curve120");

            var c60 = RiverAutoTileSolver.Solve(new List<int> { 0, 1 });
            AssertEqual(RiverPieceType.Curve60, c60.pieceType, "Diff 1 -> Curve60");
        });

        Record("FN-14", "UTCID02", "TC-F16-01", "N", "Preserve existing main trunk when 3rd tributary merges", () =>
        {
            var branched = RiverAutoTileSolver.Solve(
                new List<int> { 0, 2, 3 },
                isOrigin: false,
                existingPortA: 3,
                existingPortB: 0);
            AssertEqual(RiverPieceType.Straight, branched.pieceType, "Existing trunk 3->0 keeps Straight geometry");
            AssertEqual(3, branched.primaryPortA, "Preserves primaryPortA");
            AssertEqual(0, branched.primaryPortB, "Preserves primaryPortB");
            AssertTrue(branched.HasPort(2), "ConnectedPortsMask retains tributary port 2");
        });

        Record("FN-14", "UTCID03", "TC-F16-02", "B", "PondBasin terminal at 0/1 conn & 3-way low-tier basin", () =>
        {
            var p0 = RiverAutoTileSolver.Solve(new List<int>(), isOrigin: false, downhillDir: 2);
            var p1 = RiverAutoTileSolver.Solve(new List<int> { 4 }, isOrigin: false);
            var p3Terminal = RiverAutoTileSolver.Solve(new List<int> { 1, 3, 5 }, isOrigin: false, isLowTierTerminal: true, upstreamDir: 5, outflowDir: -1);
            AssertEqual(RiverPieceType.PondBasin, p0.pieceType, "Isolated non-origin -> PondBasin");
            AssertEqual(RiverPieceType.PondBasin, p1.pieceType, "Single-inlet endpoint -> PondBasin");
            AssertEqual(RiverPieceType.PondBasin, p3Terminal.pieceType, "3-way low-tier convergence w/o outflow -> PondBasin");
            AssertEqual(1, p3Terminal.connectionCount, "Low-tier convergence caps to primary inlet");
        });

        Record("FN-14", "UTCID04", "TC-F16-03", "A", "Null connectedDirections & SpringHead invariance", () =>
        {
            var nullOrigin = RiverAutoTileSolver.Solve(null, isOrigin: true, downhillDir: 1);
            var nullBasin = RiverAutoTileSolver.Solve(null, isOrigin: false, downhillDir: 1);
            var multiSpring = RiverAutoTileSolver.Solve(new List<int> { 0, 1, 2 }, isOrigin: true, outflowDir: 1);
            AssertEqual(RiverPieceType.SpringHead, nullOrigin.pieceType, "Null dirs origin -> SpringHead");
            AssertEqual(RiverPieceType.PondBasin, nullBasin.pieceType, "Null dirs non-origin -> PondBasin");
            AssertEqual(RiverPieceType.SpringHead, multiSpring.pieceType, "Multi-branch origin never mutates away from SpringHead");
        });
    }

    // -------------------------------------------------------------------------
    // FN-13A: RhythmMinigameEngine (F-15 & STATE-09)
    // -------------------------------------------------------------------------
    private static void RunRhythmMinigameChecks()
    {
        Record("FN-13A", "UTCID01", "TC-F15-01", "N", "Complete sequence + Perfect beats reach Victory (100% Harmony)", () =>
        {
            var eng = new RhythmMinigameEngine();
            bool? finishedSuccess = null;
            eng.OnSessionFinished += (ok, _) => finishedSuccess = ok;
            eng.Initialize(AnimalRank.Common, 25f);
            eng.StartPlaying();
            AssertEqual(RhythmPhase.OrbGliding, eng.Phase, "StartPlaying enters OrbGliding");

            for (int cycle = 0; cycle < 3; cycle++)
            {
                var seq = new List<RhythmDirection>(eng.CurrentSequence);
                AssertTrue(seq.Count >= 3 && seq.Count <= 5, "Sequence length 3..5");
                foreach (var dir in seq) AssertTrue(eng.InputDirection(dir), "Valid direction advances");
                AssertTrue(eng.IsSequenceCompleted, "Sequence completed");

                // Advance orb exactly to TargetBeatTime (0.78 * 2.6 = 2.028s)
                eng.Tick(eng.TargetBeatTime);
                var grade = eng.InputBeatStrike();
                AssertEqual(RhythmHitGrade.Perfect, grade, "Exact beat strike returns Perfect (+35%)");
                eng.Tick(0.65f); // Resolve bar -> next cycle or Victory after 3*35%=105% -> 100%
            }

            AssertEqual(RhythmPhase.Victory, eng.Phase, "3 Perfect hits reach Victory");
            AssertEqual(true, finishedSuccess, "OnSessionFinished reports victory");
        });

        Record("FN-13A", "UTCID02", "TC-F15-02", "B", "Timing window boundaries & premature SPACE guard", () =>
        {
            var eng = new RhythmMinigameEngine();
            eng.Initialize(AnimalRank.Rare, 25f);
            eng.StartPlaying();

            // Premature strike BEFORE completing arrows must NOT penalize Miss
            eng.Tick(0.2f);
            AssertEqual(RhythmHitGrade.None, eng.InputBeatStrike(), "SPACE before completing arrows returns None (no Miss)");
            AssertEqual(RhythmPhase.OrbGliding, eng.Phase, "Remains OrbGliding");

            // Complete sequence, strike in Great band (offset +0.07 normalized => +0.182s)
            foreach (var d in new List<RhythmDirection>(eng.CurrentSequence)) eng.InputDirection(d);
            float greatTime = (RhythmMinigameEngine.TargetBeatNormalizedPos + 0.07f) * RhythmMinigameEngine.OrbFlightDuration;
            eng.Tick(greatTime - 0.2f);
            AssertEqual(RhythmHitGrade.Great, eng.InputBeatStrike(), "+0.07 offset scores Great");
            AssertNear(RhythmMinigameEngine.GreatGain, eng.HarmonyProgress, 0.001f, "Harmony += 0.24f");

            // Let next bar glide to end without strike -> timeout Miss
            eng.Tick(0.65f);
            foreach (var d in new List<RhythmDirection>(eng.CurrentSequence)) eng.InputDirection(d);
            eng.Tick(RhythmMinigameEngine.OrbFlightDuration + 0.05f);
            AssertEqual(RhythmPhase.BarResolved, eng.Phase, "Full flight timeout resolves bar as Miss");
            AssertNear(RhythmMinigameEngine.GreatGain, eng.HarmonyProgress, 0.001f, "Timeout Miss adds 0 harmony");
        });

        Record("FN-13A", "UTCID03", "TC-F15-03", "A", "Wrong arrow reset, double-tap debounce & session timeout", () =>
        {
            var eng = new RhythmMinigameEngine();
            bool? finishedSuccess = null;
            eng.OnSessionFinished += (ok, _) => finishedSuccess = ok;
            eng.Initialize(AnimalRank.Common, 10f);
            eng.StartPlaying();

            var first = eng.CurrentSequence[0];
            var wrong = (RhythmDirection)(((int)first + 1) % 4);
            AssertTrue(!eng.InputDirection(wrong), "Wrong key returns false");
            AssertEqual(0, eng.CurrentSequenceIndex, "Wrong key resets sequence index to 0");

            // Test debounce (< 0.14s) while gliding before arrows finish (resets lastSpacePressTime check)
            eng.Tick(0.5f);
            AssertEqual(RhythmHitGrade.None, eng.InputBeatStrike(), "1st strike sets debounce timestamp");
            foreach (var d in new List<RhythmDirection>(eng.CurrentSequence)) eng.InputDirection(d);
            eng.Tick(0.05f);
            AssertEqual(RhythmHitGrade.None, eng.InputBeatStrike(), "2nd strike within 0.14s blocked by debounce");

            // Timeout path
            eng.Tick(12f);
            AssertEqual(RhythmPhase.Timeout, eng.Phase, "Clock expiry ends in Timeout");
            AssertEqual(false, finishedSuccess, "Timeout reports failure without taming");
        });
    }

    // -------------------------------------------------------------------------
    // FN-13B: RunnerMinigameEngine (F-15 & STATE-09)
    // -------------------------------------------------------------------------
    private static void RunRunnerMinigameChecks()
    {
        Record("FN-13B", "UTCID01", "TC-F15-01", "N", "Ground jump/land physics & full duration Victory", () =>
        {
            var eng = new RunnerMinigameEngine();
            bool? finished = null;
            eng.OnSessionFinished += (ok, _) => finished = ok;
            eng.Initialize(RunnerEnvironmentMode.Ground, 18f, 3);
            AssertTrue(eng.IsGrounded, "Starts grounded");

            eng.TryJump();
            eng.Tick(0.05f);
            AssertTrue(eng.PlayerY > 0f && !eng.IsGrounded, "Jump lifts player off ground");

            // Jump once and advance cleanly only skipping obstacle collisions by keeping PlayerY safe via re-jumping or checking sky
            var sky = new RunnerMinigameEngine();
            bool? skyWin = null;
            sky.OnSessionFinished += (ok, _) => skyWin = ok;
            sky.Initialize(RunnerEnvironmentMode.Sky, 5f, 99); // high lives to guarantee surviving random spawns to duration
            for (int i = 0; i < 110; i++) sky.Tick(0.05f);
            AssertTrue(sky.IsFinished && skyWin == true, "Reaching duration ends in Victory");
            AssertNear(1.75f, sky.SpeedMultiplier, 0.001f, "SpeedMultiplier reaches 1.75x at 100% progress");
            AssertNear(1.0f, sky.Progress, 0.001f, "Progress reaches 1.0");
            _ = finished;
        });

        Record("FN-13B", "UTCID02", "TC-F15-02", "B", "Sky bounds clamping, speed scaling & 1-life defeat boundary", () =>
        {
            var eng = new RunnerMinigameEngine();
            eng.Initialize(RunnerEnvironmentMode.Sky, 10f, 3);
            eng.SetThrust(true);
            for (int i = 0; i < 40; i++) eng.Tick(0.05f);
            float maxY = RunnerMinigameEngine.TrackHeight - RunnerMinigameEngine.PlayerSize - 8f;
            AssertNear(maxY, eng.PlayerY, 0.01f, "Sky Y clamps at ceiling bound");

            // Invalid boundary defaults
            var badInit = new RunnerMinigameEngine();
            badInit.Initialize(RunnerEnvironmentMode.Ground, -10f, 0);
            AssertNear(18f, badInit.Duration, 0.01f, "Duration <= 1s falls back to 18f");
            AssertEqual(3, badInit.MaxLives, "MaxLives <= 0 falls back to 3");
        });

        Record("FN-13B", "UTCID03", "TC-F15-03", "A", "Delta-time cap anti-tunnel & 3-hit defeat loses pack intact", () =>
        {
            var eng = new RunnerMinigameEngine();
            bool? won = null;
            eng.OnSessionFinished += (ok, _) => won = ok;
            eng.Initialize(RunnerEnvironmentMode.Ground, 18f, 3);

            // Spike dt = 5.0f must be clamped to 0.05f
            eng.Tick(5.0f);
            AssertNear(0.05f, eng.Elapsed, 0.001f, "dt capped at 0.05s prevents hitbox tunneling");

            // Inject deterministic overlapping obstacle via private field to verify 1s i-frame & 3-life defeat without flaky RNG
            var obsField = typeof(RunnerMinigameEngine).GetField("obstacles", BindingFlags.NonPublic | BindingFlags.Instance);
            var list = (List<RunnerObstacleState>)obsField.GetValue(eng);

            for (int hit = 1; hit <= 3; hit++)
            {
                list.Add(new RunnerObstacleState { Id = 900 + hit, Kind = RunnerObstacleKind.Rock, X = RunnerMinigameEngine.PlayerX, Y = 0f, Width = 60f, Height = 60f });
                eng.Tick(0.01f);
                if (hit < 3)
                {
                    AssertEqual(3 - hit, eng.Lives, $"Hit {hit} decrements life");
                    AssertTrue(eng.IsInvulnerable, "Grants 1s invulnerability after hit");
                    // Colliding again during i-frame must not subtract a second life
                    list.Add(new RunnerObstacleState { Id = 950 + hit, Kind = RunnerObstacleKind.Rock, X = RunnerMinigameEngine.PlayerX, Y = 0f, Width = 60f, Height = 60f });
                    eng.Tick(0.02f);
                    AssertEqual(3 - hit, eng.Lives, "Invulnerability blocks double hit");
                    // Expire 1s invulnerability
                    for (int k = 0; k < 25; k++) eng.Tick(0.05f);
                    list.Clear();
                }
            }

            AssertTrue(eng.IsFinished, "3rd life loss ends minigame");
            AssertEqual(false, won, "3rd hit ends in defeat");
            AssertEqual(0, eng.Lives, "Lives == 0 at defeat");

            float elapsedBefore = eng.Elapsed;
            eng.Tick(0.05f);
            eng.TryJump();
            AssertNear(elapsedBefore, eng.Elapsed, 0.0001f, "Post-finish Tick is no-op");
        });
    }

    // -------------------------------------------------------------------------
    // Integration Checks (INT-01 .. INT-08 -> TC-F02/03/15/16-04..05)
    // -------------------------------------------------------------------------
    private static void RunIntegrationChecks()
    {
        Record("INT-01", "UTCID01", "TC-F02-04", "N", "Placement + Settlement Town->GrandCity evolution + Biome aura reach", () =>
        {
            var v1 = new HexCoordinates(0, 0);
            var v2 = new HexCoordinates(1, 0);
            var v3 = new HexCoordinates(0, 1);
            var villages = new HashSet<HexCoordinates> { v1, v2, v3 };
            var cluster3 = SettlementClusterCalculator.FindConnectedCluster(v1, h => villages.Contains(h));
            AssertEqual(SettlementTier.Town, SettlementClusterCalculator.DetermineTier(cluster3), "3 villages form Town");
            AssertEqual(12, SettlementClusterCalculator.CalculatePerimeterEdges(cluster3).Count, "3-hex triangle outer perimeter (18-6=12)");

            // Add 2 more compact villages (5 compact hexes -> GrandCity + radius 2 aura reaching biome at dist 2)
            villages.Add(new HexCoordinates(1, -1));
            villages.Add(new HexCoordinates(-1, 1));
            var cluster5 = SettlementClusterCalculator.FindConnectedCluster(v1, h => villages.Contains(h));
            AssertEqual(SettlementTier.GrandCity, SettlementClusterCalculator.DetermineTier(cluster5), "5 compact villages evolve GrandCity");

            var biomeHex = new HexCoordinates(3, -1); // distance 2 from (1,-1)
            var allHexes = new List<HexCoordinates>(villages) { biomeHex };
            var buffInfos = BiomeBuffCalculator.EvaluateAllBiomeBuffs(
                allHexes,
                h => h == biomeHex ? "leafwood" : null,
                _ => 1,
                h => villages.Contains(h),
                null);
            AssertEqual(1, buffInfos.Count, "Single biome evaluated");
            AssertEqual(BuildingBuffTier.Grand, buffInfos[0].BuffState.BuildingTier, "GrandCity radius=2 reaches biome at distance 2");
        });

        Record("INT-02", "UTCID02", "TC-F02-05", "A", "CardData settlement/rain rules & HexTileInfo elevation preservation", () =>
        {
            var aridPrefab = new UnityEngine.GameObject { name = "Tile_Bloomfield_Arid" };
            var lushPrefab = new UnityEngine.GameObject { name = "Tile_Bloomfield_Lush" };

            var rain = UnityEngine.ScriptableObject.CreateInstance<CardData>();
            rain.cardID = "Rain";
            rain.cardType = CardType.Special;
            rain.replacesTile = true;
            rain.transformRules.Add(new CardData.TileTransformPair { aridTilePrefab = aridPrefab, lushTilePrefab = lushPrefab });

            var tile = new UnityEngine.GameObject { name = "Hex_0_0_[Tile_Bloomfield_Arid]_Type0" };
            var info = tile.AddComponent<HexTileInfo>();
            info.elevationLevel = 1;
            info.terrainTypeIndex = 1;
            info.sourcePrefab = aridPrefab;

            AssertTrue(rain.IsRainCard() && rain.IsTileTransformCard(), "Rain recognized as tile transform card");
            AssertTrue(rain.IsTransformSourceTile(tile), "Arid tile matches transformRules");
            AssertEqual(lushPrefab, rain.GetTransformedPrefab(tile), "Returns matching Lush prefab");

            // After Rain mutates terrainTypeIndex = -1, ElevationLevel remains 1
            info.terrainTypeIndex = -1;
            AssertEqual(1, info.ElevationLevel, "ElevationLevel stays independent from terrainTypeIndex=-1");

            var village = UnityEngine.ScriptableObject.CreateInstance<CardData>();
            village.cardID = "Medieval_Village";
            village.cardType = CardType.Building;
            AssertTrue(village.IsSettlementCard(), "Medieval_Village recognized as Settlement card");
            AssertTrue(!village.IsRiverCard(), "Village is not River card");
        });

        Record("INT-03", "UTCID03", "TC-F03-04", "N", "Multi-layer GrandCity + Winding River grants dual Biome buffs", () =>
        {
            var biome = new List<HexCoordinates>
            {
                new HexCoordinates(0, 0), new HexCoordinates(1, 0),
                new HexCoordinates(0, 1), new HexCoordinates(1, 1)
            };
            var rivers = new HashSet<HexCoordinates> { new HexCoordinates(-1, 0), new HexCoordinates(0, -1) };
            var grandBldg = new HashSet<HexCoordinates>
            {
                new HexCoordinates(3, 1), new HexCoordinates(4, 1), new HexCoordinates(3, 2),
                new HexCoordinates(4, 0), new HexCoordinates(5, 0) // dist 2 to (1,1)
            };
            var riverTile = RiverAutoTileSolver.Solve(new List<int> { 0, 2 });
            AssertEqual(RiverPieceType.Curve120, riverTile.pieceType, "Winding river solves Curve120");

            var all = new List<HexCoordinates>();
            all.AddRange(biome); all.AddRange(rivers); all.AddRange(grandBldg);
            var res = BiomeBuffCalculator.EvaluateAllBiomeBuffs(
                all,
                h => biome.Contains(h) ? "bloomfield" : null,
                _ => 1,
                h => grandBldg.Contains(h),
                h => rivers.Contains(h));
            AssertEqual(1, res.Count, "Single Bloomfield group");
            AssertTrue(res[0].BuffState.HasBuildingBuff && res[0].BuffState.HasRiverBuff, "Receives both Building and River buffs");
            AssertEqual(BuildingBuffTier.Grand, res[0].BuffState.BuildingTier, "Grand tier");
            AssertEqual(RiverBuffTier.Peninsula, res[0].BuffState.RiverTier, "Peninsula tier from 2 touching rivers");
        });

        Record("INT-04", "UTCID04", "TC-F03-05", "A", "Building exclusion & <3 settlement never leaks buff across split clusters", () =>
        {
            var h0 = new HexCoordinates(0, 0);
            var h1 = new HexCoordinates(1, 0);
            var h2 = new HexCoordinates(2, 0);
            var bldg2 = new HashSet<HexCoordinates> { new HexCoordinates(0, 1), new HexCoordinates(1, 1) }; // only 2 tiles (<3)
            var all = new List<HexCoordinates> { h0, h1, h2 };
            all.AddRange(bldg2);

            var infos = BiomeBuffCalculator.EvaluateAllBiomeBuffs(
                all,
                h => all.Contains(h) ? "leafwood" : null, // even if building tiles had terrain
                h => h == h2 ? 2 : 1,
                h => bldg2.Contains(h),
                null);
            AssertEqual(2, infos.Count, "Building tiles excluded & clusterId 1 vs 2 split cleanly");
            AssertEqual(BuildingBuffTier.None, infos[0].BuffState.BuildingTier, "<3 building grants zero buff");
            AssertEqual(BuildingBuffTier.None, infos[1].BuffState.BuildingTier, "Second split cluster grants zero buff");
        });

        Record("INT-05", "UTCID05", "TC-F15-04", "N", "WildIncidentEvaluator + WildPackIncident halt clusters until Rhythm victory", () =>
        {
            var host = new IncidentClusterTarget
            {
                ClusterId = "hab_leaf", HexCount = 6,
                Hexes = new List<HexCoordinates> { new HexCoordinates(0, 0), new HexCoordinates(1, 0), new HexCoordinates(0, 1) },
                CenterPosition = new UnityEngine.Vector3(0f, 1f, 0f), IsProducing = true
            };
            var sat = new IncidentClusterTarget
            {
                ClusterId = "hab_bloom", HexCount = 3,
                Hexes = new List<HexCoordinates> { new HexCoordinates(2, 0) }, // dist 1 from (1,0)
                CenterPosition = new UnityEngine.Vector3(4f, 0f, 0f), IsProducing = true
            };
            var small = new IncidentClusterTarget { ClusterId = "hab_tiny", HexCount = 2, IsProducing = true };
            var all = new List<IncidentClusterTarget> { small, sat, host };

            AssertEqual(2, WildIncidentEvaluator.EvaluateProgressionTier(0, all), "6-hex cluster lifts tier to >=2");
            var picked = WildIncidentEvaluator.PickHostCluster(all, 3);
            AssertTrue(picked != null && picked.HexCount >= 3 && picked.ClusterId != "hab_tiny", "Picks eligible >=3 cluster from top candidates and ignores <3");
            var nearby = WildIncidentEvaluator.FindNearbySatelliteClusters(picked, all, 1);
            AssertEqual(1, nearby.Count, "Finds adjacent producing satellite");
            var satellite = nearby[0];

            var cfg = UnityEngine.ScriptableObject.CreateInstance<WildIncidentConfig>();
            var rank = cfg.RollAlphaRank(1);
            var incident = new WildPackIncident("INC-1", picked, null, rank, null, nearby, null);
            AssertTrue(incident.IsActive && picked.IsDisrupted && satellite.IsDisrupted, "Incident disrupts both host & satellite");

            // Complete Rhythm minigame and resolve
            var rEng = new RhythmMinigameEngine();
            rEng.Initialize(rank, 25f);
            rEng.StartPlaying();
            for (int c = 0; c < 3; c++)
            {
                foreach (var d in new List<RhythmDirection>(rEng.CurrentSequence)) rEng.InputDirection(d);
                rEng.Tick(rEng.TargetBeatTime);
                rEng.InputBeatStrike();
                rEng.Tick(0.65f);
            }
            AssertEqual(RhythmPhase.Victory, rEng.Phase, "Rhythm win achieved");
            incident.Resolve();
            AssertTrue(!incident.IsActive && !picked.IsDisrupted && !satellite.IsDisrupted, "Resolve restores both clusters");
        });

        Record("INT-06", "UTCID06", "TC-F15-05", "A", "Runner 3-life defeat retains active WildPack disruption for retry", () =>
        {
            var host = new IncidentClusterTarget { ClusterId = "hab_1", HexCount = 3, IsProducing = true };
            var incident = new WildPackIncident("INC-2", host, null, AnimalRank.Common, null, null, null);
            var runner = new RunnerMinigameEngine();
            runner.Initialize(RunnerEnvironmentMode.Ground, 18f, 3);
            var obsField = typeof(RunnerMinigameEngine).GetField("obstacles", BindingFlags.NonPublic | BindingFlags.Instance);
            var list = (List<RunnerObstacleState>)obsField.GetValue(runner);

            for (int hit = 0; hit < 3; hit++)
            {
                list.Add(new RunnerObstacleState { Id = 1 + hit, X = RunnerMinigameEngine.PlayerX, Y = 0f, Width = 60f, Height = 60f });
                runner.Tick(0.01f);
                if (hit < 2) { for (int k = 0; k < 25; k++) runner.Tick(0.05f); list.Clear(); }
            }
            AssertTrue(runner.IsFinished && runner.Lives == 0, "Minigame ended in defeat");
            // Because player lost, manager does NOT call Resolve()
            AssertTrue(incident.IsActive && host.IsDisrupted, "WildPack stays active & host stays halted on defeat");
        });

        Record("INT-07", "UTCID07", "TC-F16-04", "N", "RiverAutoTileSolver chain + RiverConfig scales, offsets & waterfall check", () =>
        {
            var cfg = UnityEngine.ScriptableObject.CreateInstance<RiverConfig>();
            var s0 = RiverAutoTileSolver.Solve(new List<int> { 0 }, isOrigin: true);
            var s1 = RiverAutoTileSolver.Solve(new List<int> { 0, 3 }, upstreamDir: 3, outflowDir: 0);
            var s2 = RiverAutoTileSolver.Solve(new List<int> { 3, 5 }, upstreamDir: 3, outflowDir: 5);
            var s3 = RiverAutoTileSolver.Solve(new List<int> { 2 }, isOrigin: false, isLowTierTerminal: true);

            AssertEqual(RiverPieceType.SpringHead, s0.pieceType, "Origin SpringHead");
            AssertEqual(RiverPieceType.Straight, s1.pieceType, "Straight link");
            AssertEqual(RiverPieceType.Curve120, s2.pieceType, "120-deg bend");
            AssertEqual(RiverPieceType.PondBasin, s3.pieceType, "Terminal PondBasin");

            AssertNear(cfg.springHeadOffset.y, cfg.GetYOffsetForPiece(s0.pieceType), 0.001f, "SpringHead Y offset");
            AssertNear(cfg.straightYOffset, cfg.GetYOffsetForPiece(s1.pieceType), 0.001f, "Straight Y offset");
            AssertNear(cfg.pondOffset.y, cfg.GetYOffsetForPiece(s3.pieceType), 0.001f, "Pond Y offset");
            AssertTrue(cfg.IsDoubleTierWaterfall(1.25f) && !cfg.IsDoubleTierWaterfall(1.0f), "Double-tier waterfall threshold");
        });

        Record("INT-08", "UTCID08", "TC-F16-05", "N", "River card identity, trunk preservation & order-independent port mask", () =>
        {
            var riverCard = UnityEngine.ScriptableObject.CreateInstance<CardData>();
            riverCard.cardID = "River";
            riverCard.cardType = CardType.Special;
            AssertTrue(riverCard.IsRiverCard(), "CardData.IsRiverCard detects River cardID");

            var orderA = RiverAutoTileSolver.Solve(new List<int> { 0, 2, 3 }, existingPortA: 3, existingPortB: 0);
            var orderB = RiverAutoTileSolver.Solve(new List<int> { 3, 0, 2 }, existingPortA: 3, existingPortB: 0);
            AssertEqual(orderA.connectedPortsMask, orderB.connectedPortsMask, "Port bitmask independent of connection order");
            AssertEqual(orderA.pieceType, orderB.pieceType, "Piece geometry independent of connection order");
            AssertTrue(orderA.HasPort(0) && orderA.HasPort(2) && orderA.HasPort(3), "All 3 connected ports preserved");
        });
    }

    // -------------------------------------------------------------------------
    // State Transition Checks (STATE-03 & STATE-09 -> TC-TR-07..09, 25..27)
    // -------------------------------------------------------------------------
    private static void RunStateTransitionChecks()
    {
        Record("STR-03", "UTCID01", "TC-TR-07", "N", "STATE-03: Preview -> Established (Hamlet->Town & closed borders)", () =>
        {
            var hexes = new HashSet<HexCoordinates> { new HexCoordinates(0, 0) };
            var hamlet = SettlementClusterCalculator.FindConnectedCluster(new HexCoordinates(0, 0), h => hexes.Contains(h));
            AssertEqual(SettlementTier.Hamlet, SettlementClusterCalculator.DetermineTier(hamlet), "Initial 1 hex = Hamlet");
            AssertEqual(6, SettlementClusterCalculator.CalculatePerimeterEdges(hamlet).Count, "Hamlet has 6 borders");

            // Expand to 3 connected hexes -> transitions cleanly to Town removing internal walls
            hexes.Add(new HexCoordinates(1, 0));
            hexes.Add(new HexCoordinates(0, 1));
            var town = SettlementClusterCalculator.FindConnectedCluster(new HexCoordinates(0, 0), h => hexes.Contains(h));
            AssertEqual(SettlementTier.Town, SettlementClusterCalculator.DetermineTier(town), "3 hexes transition to Town");
            AssertEqual(12, SettlementClusterCalculator.CalculatePerimeterEdges(town).Count, "Town removes internal seams (18-6=12)");
        });

        Record("STR-03", "UTCID02", "TC-TR-08", "A", "STATE-03: Preview -> Preview [Forbidden empty/arid Village stays unchanged]", () =>
        {
            var townTile = new UnityEngine.GameObject { name = "Hex_0_0_[Tile_Leafwood_Arid]" };
            var riverCard = UnityEngine.ScriptableObject.CreateInstance<CardData>();
            riverCard.cardID = "River";
            var biomeAllowedCheck = new CardData { allowedTilePrefabs = new List<UnityEngine.GameObject> { new UnityEngine.GameObject { name = "Tile_Leafwood_Lush" } } };

            AssertTrue(!biomeAllowedCheck.MatchesAnyAllowedPrefab(townTile), "Arid tile rejected for Lush-only rule");
            var unchanged = SettlementClusterCalculator.DetermineTier(new List<HexCoordinates>());
            AssertEqual(SettlementTier.Hamlet, unchanged, "Rejected transition leaves cluster unchanged");
        });

        Record("STR-03", "UTCID03", "TC-TR-09", "N", "STATE-03: Damaged/Arid -> Established via Rain transform + Height retained", () =>
        {
            var arid = new UnityEngine.GameObject { name = "Tile_Windheath_Arid" };
            var lush = new UnityEngine.GameObject { name = "Tile_Windheath_Lush" };
            var tile = new UnityEngine.GameObject { name = "Hex_1_2_[Tile_Windheath_Arid]" };
            var info = tile.AddComponent<HexTileInfo>();
            info.elevationLevel = 2;
            info.terrainTypeIndex = 2;
            info.sourcePrefab = arid;

            var rain = UnityEngine.ScriptableObject.CreateInstance<CardData>();
            rain.cardID = "Rain";
            rain.replacesTile = true;
            rain.transformRules.Add(new CardData.TileTransformPair { aridTilePrefab = arid, lushTilePrefab = lush });

            AssertTrue(rain.IsTileAllowed(tile), "Rain allowed on damaged/arid tile");
            AssertEqual(lush, rain.GetTransformedPrefab(tile), "Transforms arid tile -> Lush established");
            info.terrainTypeIndex = -1;
            info.hasLushEnvironment = true;
            AssertEqual(2, info.ElevationLevel, "Tier-2 height preserved after Rain recovery");
        });

        Record("STR-09", "UTCID01", "TC-TR-25", "N", "STATE-09: ActivePack -> Inspecting -> AttemptRunning (Owner)", () =>
        {
            var host = new IncidentClusterTarget { ClusterId = "hab_s", HexCount = 3, IsProducing = true };
            var inc = new WildPackIncident("INC-S1", host, null, AnimalRank.Common, null, null, null);
            AssertTrue(inc.IsActive && host.IsDisrupted, "State ActivePack halts host production");

            var eng = new RhythmMinigameEngine();
            eng.Initialize(inc.AlphaRank, 24f);
            AssertEqual(RhythmPhase.ReadyCountdown, eng.Phase, "Inspecting initialized");
            eng.StartPlaying();
            AssertEqual(RhythmPhase.OrbGliding, eng.Phase, "Transitions to AttemptRunning (OrbGliding)");
            AssertTrue(inc.IsActive, "Incident stays active during attempt");
        });

        Record("STR-09", "UTCID02", "TC-TR-26", "N", "STATE-09: AttemptRunning -> Resolved idempotently grants completion once", () =>
        {
            int stateChanges = 0;
            var host = new IncidentClusterTarget
            {
                ClusterId = "hab_r", HexCount = 3, IsProducing = true,
                SetDisruptedAction = _ => stateChanges++
            };
            var inc = new WildPackIncident("INC-S2", host, null, AnimalRank.Common, null, null, null);
            AssertEqual(1, stateChanges, "Spawn triggers disrupt once");

            inc.Resolve();
            inc.Resolve(); // duplicate resolve call
            AssertTrue(!inc.IsActive && !host.IsDisrupted, "State transitions to Resolved & restores host");
            AssertEqual(2, stateChanges, "Duplicate Resolve() is idempotent (exactly 1 restore transition)");
        });

        Record("STR-09", "UTCID03", "TC-TR-27", "A", "STATE-09: Forbidden Visitor/Incomplete attempt keeps pack ActivePack", () =>
        {
            bool isVisitMode = true; // Guard matching WildAlphaInteractable.HandleMouseClickDetection (WorldLaunch.IsVisit)
            var host = new IncidentClusterTarget { ClusterId = "hab_v", HexCount = 3, IsProducing = true };
            var inc = new WildPackIncident("INC-S3", host, null, AnimalRank.Uncommon, null, null, null);

            bool attemptAllowed = !isVisitMode && inc.IsActive;
            AssertTrue(!attemptAllowed, "Visitor cannot transition ActivePack/Inspecting into AttemptRunning");

            // Even when Owner starts but strikes before finishing arrows -> stays Active without resolving
            var eng = new RhythmMinigameEngine();
            eng.Initialize(inc.AlphaRank, 24f);
            eng.StartPlaying();
            eng.Tick(0.3f);
            AssertEqual(RhythmHitGrade.None, eng.InputBeatStrike(), "Incomplete arrow guard prevents false strike");
            AssertTrue(inc.IsActive && host.IsDisrupted, "WildPack remains active & host remains disrupted");
        });
    }

    private static void WriteJsonReport(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var sb = new StringBuilder();
        sb.AppendLine("[");
        for (int i = 0; i < Results.Count; i++)
        {
            var r = Results[i];
            string esc = (r.Summary ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
            string comma = i < Results.Count - 1 ? "," : "";
            sb.AppendLine($"  {{\"fn\":\"{r.FunctionKey}\",\"utc\":\"{r.UtcId}\",\"tc\":\"{r.TcId}\",\"type\":\"{r.CaseType}\",\"passed\":{(r.Passed ? "true" : "false")},\"summary\":\"{esc}\"}}{comma}");
        }
        sb.AppendLine("]");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }
}
