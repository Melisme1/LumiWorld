using System;
using System.Collections.Generic;
using System.Text.Json;
using LumiWorld.Acs;
using LumiWorld.Acs.TimeSystem;

internal sealed class FakeTimeSource : ITimeSource
{
    public double UtcSeconds { get; set; } = 1_800_000_000;
    public double MonotonicSeconds { get; set; }
    public void Advance(double seconds) { UtcSeconds += seconds; MonotonicSeconds += seconds; }
}

internal static class TimeChecks
{
    private static int checks, failed;
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
    private static void Equal(double expected, double actual, string message)
    {
        checks++;
        if (double.IsNaN(actual) || Math.Abs(expected - actual) > 1e-6)
            throw new Exception(message + ": expected " + expected + ", got " + actual);
    }
    private static void Assert(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static void Throws<T>(Action action, string message) where T : Exception
    {
        checks++;
        try { action(); } catch (T) { return; }
        throw new Exception(message);
    }
    private static void Run(string name, Action test)
    {
        try { test(); Console.WriteLine("PASS " + name); }
        catch (Exception exception) { failed++; Console.WriteLine("FAIL " + name + ": " + exception); }
    }

    private static int Main()
    {
        Run("Shared clock pause/focus gaps and wall-clock changes", Clock);
        Run("Countdown, suspension, cancellation and completion consumption", Countdown);
        Run("Intervals coalesce or count elapsed periods", Intervals);
        Run("Work time is explicitly credited, never gained offline", Work);
        Run("Snapshot round trip, offline policies and caps", Snapshots);
        Run("Bad snapshots never partially replace live timers", InvalidSnapshots);
        Run("Legacy order deadline survives pause/save/reload", LegacyOrder);
        Run("Production ledger uses paused gameplay time", Production);
        Run("Long and partitioned timer runs agree", Partitions);
        Console.WriteLine($"{checks} assertions; {failed} failed groups.");
        return failed == 0 ? 0 : 1;
    }

    private static void Clock()
    {
        var source = new FakeTimeSource(); var clock = new GameClock(source);
        Equal(0, clock.Read().GameSeconds, "starts at zero");
        source.Advance(10); Equal(10, clock.Read().GameSeconds, "elapsed before pause");
        Assert(clock.SetPaused(true), "first pause changes state");
        source.Advance(900); Equal(10, clock.Read().GameSeconds, "pause freezes game time");
        Assert(!clock.SetPaused(true), "second pause is idempotent");
        Assert(clock.SetPaused(false), "resume changes state");
        Equal(10, clock.Read().GameSeconds, "resume has no catch-up debt");
        source.Advance(5); Equal(15, clock.Read().GameSeconds, "resumed time");
        source.UtcSeconds -= 10000; source.MonotonicSeconds += 2;
        Equal(17, clock.Read().GameSeconds, "wall clock backwards does not freeze gameplay");
        source.UtcSeconds += 20000; source.MonotonicSeconds += 3;
        Equal(20, clock.Read().GameSeconds, "wall clock forwards does not produce windfall");
        source.Advance(600); Equal(620, clock.Read().GameSeconds, "no updates/focus gap still counts");
        source.MonotonicSeconds--;
        Throws<InvalidOperationException>(() => clock.Read(), "reject broken monotonic source");
        source.MonotonicSeconds++; source.UtcSeconds = double.NaN;
        Throws<ArgumentOutOfRangeException>(() => clock.Read(), "reject NaN UTC");
        Throws<ArgumentNullException>(() => new GameClock(null), "require source");
    }

    private static void Countdown()
    {
        var source = new FakeTimeSource(); var clock = new GameClock(source); var service = new TimerService(clock);
        service.StartCountdown("care", 10);
        source.Advance(3); Equal(7, service.Read("care").remainingSeconds, "three seconds consumed");
        clock.SetPaused(true); source.Advance(100); Equal(7, service.Read("care").remainingSeconds, "global pause");
        clock.SetPaused(false); service.SetSuspended("care", true);
        source.Advance(30); Equal(7, service.Read("care").remainingSeconds, "individual suspension");
        service.SetSuspended("care", false); source.Advance(7);
        Assert(service.Read("care").isCompleted, "deadline reached");
        Assert(service.TryConsumeSignals("care", out long count), "one completion"); Equal(1, count, "one signal");
        Assert(!service.TryConsumeSignals("care", out _), "no repeated completion");
        source.Advance(100); Assert(!service.TryConsumeSignals("care", out _), "remains consumed");
        var copy = service.Read("care"); copy.pendingSignals = 99;
        Assert(!service.TryConsumeSignals("care", out _), "read result cannot mutate timer");
        Throws<ArgumentException>(() => service.StartCountdown("care", 5), "duplicate ID rejected");
        Assert(service.Cancel("care"), "cancel existing"); Assert(!service.Cancel("care"), "cancel twice safe");
        service.StartCountdown("zero", 0); Assert(service.TryConsumeSignals("zero", out _), "zero completes immediately");
        Throws<ArgumentException>(() => service.StartCountdown(" ", 1), "empty ID rejected");
        Throws<ArgumentOutOfRangeException>(() => service.StartCountdown("bad", -1), "negative duration");
        Throws<ArgumentOutOfRangeException>(() => service.StartCountdown("bad", double.PositiveInfinity), "infinite duration");
        Throws<KeyNotFoundException>(() => service.Read("unknown"), "unknown ID explicit");
        service.Clear(); Equal(0, service.Count, "clear");
    }

    private static void Intervals()
    {
        var source = new FakeTimeSource(); var service = new TimerService(new GameClock(source));
        service.StartInterval("poll", 5); service.StartInterval("count", 5, IntervalCatchUpPolicy.CountElapsed);
        source.Advance(17);
        service.TryConsumeSignals("poll", out long once); Equal(1, once, "no replay storm");
        service.TryConsumeSignals("count", out long all); Equal(3, all, "count elapsed periods");
        Equal(3, service.Read("poll").remainingSeconds, "retains interval phase");
        Assert(!service.TryConsumeSignals("poll", out _), "does not duplicate on repeated polling");
        source.Advance(3); service.TryConsumeSignals("count", out all); Equal(1, all, "exact boundary");
        Equal(5, service.Read("count").remainingSeconds, "next interval positive");
        Throws<ArgumentOutOfRangeException>(() => service.StartInterval("bad", 0), "zero repeat interval rejected");
    }

    private static void Work()
    {
        var source = new FakeTimeSource(); var clock = new GameClock(source); var service = new TimerService(clock);
        service.StartWork("creature", 20); source.Advance(200);
        Equal(20, service.Read("creature").remainingSeconds, "wall time alone is not work");
        service.CreditWork("creature", 5); Equal(15, service.Read("creature").remainingSeconds, "credit actual work");
        clock.SetPaused(true); service.CreditWork("creature", 50);
        Equal(15, service.Read("creature").remainingSeconds, "no work while game paused");
        clock.SetPaused(false); service.SetSuspended("creature", true); service.CreditWork("creature", 50);
        Equal(15, service.Read("creature").remainingSeconds, "no work while individually suspended");
        service.SetSuspended("creature", false); service.CreditWork("creature", 15);
        Assert(service.TryConsumeSignals("creature", out _), "work threshold completes once");
        service.CreditWork("creature", 100); Assert(!service.TryConsumeSignals("creature", out _), "no duplicate work completion");
        service.StartCountdown("normal", 10);
        Throws<InvalidOperationException>(() => service.CreditWork("normal", 1), "do not manually credit deadlines");
        Throws<ArgumentOutOfRangeException>(() => service.CreditWork("creature", double.NaN), "reject invalid work");
    }

    private static TimerSnapshot RoundTrip(TimerSnapshot snapshot) => JsonSerializer.Deserialize<TimerSnapshot>(JsonSerializer.Serialize(snapshot, Json), Json);
    private static void Snapshots()
    {
        var source = new FakeTimeSource(); var clock = new GameClock(source); var service = new TimerService(clock);
        service.StartCountdown("recover", 100);
        service.StartCountdown("online", 100, OfflineTimerPolicy.Freeze);
        service.StartWork("work", 100); service.CreditWork("work", 10);
        service.StartInterval("random", 5); service.StartCountdown("held", 100); service.SetSuspended("held", true);
        service.StartCountdown("consumed", 0); service.TryConsumeSignals("consumed", out _);
        source.Advance(20); service.TryConsumeSignals("random", out _);
        var snapshot = RoundTrip(service.Capture());
        source.Advance(50); var reloaded = new TimerService(new GameClock(source));
        var result = reloaded.Restore(snapshot, 30);
        Equal(30, result.AppliedOfflineSeconds, "caller-supplied offline cap");
        Equal(50, reloaded.Read("recover").remainingSeconds, "advance only capped offline duration");
        Equal(80, reloaded.Read("online").remainingSeconds, "online timer freezes offline");
        Equal(90, reloaded.Read("work").remainingSeconds, "work never credited offline");
        Equal(100, reloaded.Read("held").remainingSeconds, "held timer freezes offline");
        Assert(!reloaded.TryConsumeSignals("random", out _), "no offline random backlog");
        Assert(!reloaded.TryConsumeSignals("consumed", out _), "consumed completion survives reload");
        var second = new TimerService(new GameClock(source)); second.Restore(RoundTrip(reloaded.Capture()), 30);
        Equal(50, second.Read("recover").remainingSeconds, "repeated save/restore does not reuse elapsed");
        var backwards = RoundTrip(second.Capture()); source.UtcSeconds -= 100;
        result = second.Restore(backwards, 1000);
        Assert(result.ClockMovedBackwards, "reports backwards UTC"); Equal(0, result.AppliedOfflineSeconds, "no negative offline accrual");
        source.Advance(50); Assert(second.TryConsumeSignals("recover", out _), "completed after resumed play");
        var unconsumed = new TimerService(new GameClock(source)); unconsumed.StartCountdown("x", 10);
        var old = unconsumed.Capture(); source.Advance(30); unconsumed.Restore(old, 100);
        Assert(unconsumed.TryConsumeSignals("x", out _), "offline completion is available to owner");
        var saved = unconsumed.Capture(); saved.timers[0].id = "mutated";
        Assert(unconsumed.Contains("x"), "snapshot is independent of live state");
        clock.SetPaused(true); var paused = service.Capture(); source.Advance(100);
        var afterPauseQuit = new TimerService(new GameClock(source)); afterPauseQuit.Restore(paused, 1000);
        Assert(afterPauseQuit.Read("recover").isCompleted, "manual pause is session-only, offline follows timer policy");
    }

    private static void InvalidSnapshots()
    {
        var source = new FakeTimeSource(); var service = new TimerService(new GameClock(source));
        service.StartCountdown("keep", 40);
        var bad = service.Capture(); bad.version = 999;
        Throws<NotSupportedException>(() => service.Restore(bad, 100), "reject unknown schema");
        bad = service.Capture(); bad.timers.Add(bad.timers[0]);
        Throws<ArgumentException>(() => service.Restore(bad, 100), "reject duplicate records");
        bad = service.Capture(); bad.timers[0].remainingSeconds = double.NaN;
        Throws<ArgumentOutOfRangeException>(() => service.Restore(bad, 100), "reject non-finite data");
        bad = service.Capture(); bad.timers[0].isCompleted = true;
        Throws<ArgumentException>(() => service.Restore(bad, 100), "reject inconsistent completed state");
        bad = service.Capture(); bad.timers[0].kind = TimerKind.Work; bad.timers[0].offlinePolicy = OfflineTimerPolicy.Advance;
        Throws<ArgumentException>(() => service.Restore(bad, 100), "reject work advancement offline");
        bad = service.Capture(); bad.timers[0].pendingSignals = -1;
        Throws<ArgumentException>(() => service.Restore(bad, 100), "reject negative signals");
        bad = service.Capture(); bad.timers[0].offlinePolicy = (OfflineTimerPolicy)9;
        Throws<ArgumentException>(() => service.Restore(bad, 100), "reject unknown policy");
        Throws<ArgumentOutOfRangeException>(() => service.Restore(service.Capture(), -1), "reject negative offline cap");
        Equal(40, service.Read("keep").remainingSeconds, "invalid import leaves live timers intact");
    }

    private static void LegacyOrder()
    {
        var source = new FakeTimeSource(); var clock = new GameClock(source); var service = new TimerService(clock);
        string saved = UtcDeadline.Format(source.UtcSeconds + 300);
        service.StartCountdown("refill", UtcDeadline.Remaining(saved, source.UtcSeconds, 300));
        source.Advance(40); clock.SetPaused(true); source.Advance(600);
        Equal(260, service.Read("refill").remainingSeconds, "long pause preserves refill");
        var snapshot = service.Capture(); saved = UtcDeadline.Format(snapshot.savedAtUtcSeconds + snapshot.timers[0].remainingSeconds);
        source.Advance(60); // App closed: deadline now counts offline after last save.
        var restored = new TimerService(new GameClock(source));
        restored.StartCountdown("refill", UtcDeadline.Remaining(saved, source.UtcSeconds, 300));
        Equal(200, restored.Read("refill").remainingSeconds, "save during pause + offline wait");
        Equal(0, UtcDeadline.Remaining("bad", source.UtcSeconds, 300), "bad legacy deadline is ready");
        Equal(300, UtcDeadline.Remaining(UtcDeadline.Format(source.UtcSeconds + 9999), source.UtcSeconds, 300), "legacy future time is capped");
        Equal(0, UtcDeadline.Remaining(UtcDeadline.Format(source.UtcSeconds - 1), source.UtcSeconds, 300), "expired legacy wait is zero");
        string timezone = DateTimeOffset.FromUnixTimeSeconds((long)source.UtcSeconds + 60).ToOffset(TimeSpan.FromHours(7)).ToString("o");
        Equal(60, UtcDeadline.Remaining(timezone, source.UtcSeconds, 300), "ISO timezone conversion");
    }

    private static void Production()
    {
        var source = new FakeTimeSource(); var clock = new GameClock(source); var ledger = new ProductionLedger();
        var habitat = new HabitatState { habitatId = "h", islandId = "i", natureCardId = "leafwood" };
        ledger.Synchronize(new[] { habitat }, _ => new List<ProductionSlotRate> {
            new ProductionSlotRate { resourceId = "wood", unitsPerMinute = 120, bufferCap = 100 }
        }, clock.Read().GameSeconds);
        source.Advance(10); ledger.Settle(clock.Read().GameSeconds);
        Equal(20, ledger.Find("h").FindBuffer("wood").amount, "T1 before pause");
        clock.SetPaused(true); source.Advance(1000); ledger.Settle(clock.Read().GameSeconds);
        Equal(20, ledger.Find("h").FindBuffer("wood").amount, "no production during pause");
        clock.SetPaused(false); ledger.Settle(clock.Read().GameSeconds);
        Equal(20, ledger.Find("h").FindBuffer("wood").amount, "resume has no production debt");
        source.Advance(.25); ledger.Settle(clock.Read().GameSeconds);
        Equal(.5, ledger.Find("h").FindBuffer("wood").fractionalCarry, "carry survives pause");
        source.UtcSeconds += 50000; ledger.Settle(clock.Read().GameSeconds);
        Equal(20, ledger.Find("h").FindBuffer("wood").amount, "changing wall time does not add production");
        source.Advance(100); ledger.Settle(clock.Read().GameSeconds);
        Equal(100, ledger.Find("h").FindBuffer("wood").amount, "buffer cap remains intact");
        ledger.Find("h").FindBuffer("wood").amount = 0;
        ledger.Settle(clock.Read().GameSeconds); Equal(0, ledger.Find("h").FindBuffer("wood").amount, "no cap debt after collect");
    }

    private static void Partitions()
    {
        var a = new FakeTimeSource(); var b = new FakeTimeSource();
        var longRun = new TimerService(new GameClock(a)); var shortRuns = new TimerService(new GameClock(b));
        longRun.StartInterval("x", 7, IntervalCatchUpPolicy.CountElapsed);
        shortRuns.StartInterval("x", 7, IntervalCatchUpPolicy.CountElapsed);
        a.Advance(1000); longRun.Tick();
        for (int i = 0; i < 4000; i++) { b.Advance(.25); shortRuns.Tick(); }
        Equal(longRun.Read("x").pendingSignals, shortRuns.Read("x").pendingSignals, "same interval count");
        Equal(longRun.Read("x").remainingSeconds, shortRuns.Read("x").remainingSeconds, "same remaining wait");
    }
}
