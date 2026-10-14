using System;
using System.Reflection;
using LumiWorld.Acs.TimeSystem;

// Executes the actual GameTimeRuntime, OrderBoardSystem and EconomySaveSystem methods
// against fake time and in-memory storage, without opening the developer's economy save.
internal static class IntegrationChecks
{
    private static int checks;
    private sealed class Source : ITimeSource
    {
        public double UtcSeconds { get; set; } = 1_800_000_000;
        public double MonotonicSeconds { get; set; }
        public void Advance(double seconds) { UtcSeconds += seconds; MonotonicSeconds += seconds; }
    }
    private static void Set(object target, string name, object value)
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Call(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void Assert(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static void Equal(double expected, double actual, string message)
        => Assert(!double.IsNaN(actual) && Math.Abs(expected - actual) < 1e-5, message + ": " + actual);
    private static double Wait(OrderBoardSystem board) => board.GetTimeUntilRefill(1).TotalSeconds;
    private static double SavedWait(MemoryEconomyStore store, Source source)
        => UtcDeadline.Remaining(store.Saved.orderRefills.Find(r => r.slot == 1).refillAtUtc, source.UtcSeconds, 300);

    private static int Main()
    {
        try { Run(); Console.WriteLine($"PASS runtime/order/save adapters: {checks} assertions."); return 0; }
        catch (Exception exception) { Console.WriteLine("FAIL " + exception); return 1; }
    }

    private static void Run()
    {
        var source = new Source();
        var runtime = new GameTimeRuntime(); var clock = new GameClock(source);
        Set(runtime, "clock", clock); Set(runtime, "timers", new TimerService(clock)); Call(runtime, "Awake");
        Assert(UnityEngine.Application.runInBackground, "runtime enables background ticking");
        var store = new MemoryEconomyStore(); var save = NewSave(new EconomySaveData(), store);
        var template = new OrderTemplate(); template.requirements.Add(new OrderRequirement { resource = ResourceCatalog.Wood });
        UnityEngine.Resources.Assets = new UnityEngine.Object[] { template };
        var board = NewBoard();
        Assert(board.GetOrder(0) != null && board.GetOrder(1) != null, "normal order creation");
        Assert(!board.Discard(0), "baseline remains non-discardable");
        Assert(board.Discard(1), "discard starts timer"); Equal(300, Wait(board), "new refill duration");
        source.Advance(40); Equal(260, Wait(board), "elapsed refill");

        int observed = 0;
        Action<bool> broken = _ => throw new Exception("Intentional subscriber failure");
        Action<bool> observer = _ => observed++;
        runtime.PauseChanged += broken; runtime.PauseChanged += observer;
        UnityEngine.Time.timeScale = .5f;
        runtime.Pause(); runtime.Pause();
        Equal(1, observed, "pause is idempotent even with failed listener");
        Equal(0, UnityEngine.Time.timeScale, "pause also freezes Unity simulation");
        Call(save, "LateUpdate"); Equal(260, SavedWait(store, source), "save at pause boundary");
        source.Advance(600); Call(runtime, "Update"); Call(board, "Update");
        Equal(260, Wait(board), "long pause never completes refill");
        Assert(board.GetOrder(1) == null, "paused board does not create order");
        int before = ResourceInventory.Instance.Amount;
        Assert(!board.TryFulfill(0) && !board.Discard(2), "paused order mutations blocked");
        Equal(before, ResourceInventory.Instance.Amount, "paused fulfillment consumes no inventory");

        // No dirty state after previous save: quitting must still project the latest remaining wait.
        Call(save, "OnApplicationQuit"); Equal(260, SavedWait(store, source), "forced save includes long manual pause");
        Assert(store.Saved.version == 1 && store.Saved.orderRefills.Count == 1, "legacy save schema retained");
        runtime.Resume(); Equal(.5, UnityEngine.Time.timeScale, "restores previous Unity speed");
        Equal(260, Wait(board), "resume excludes paused debt");
        runtime.PauseChanged -= broken; runtime.PauseChanged -= observer;
        Equal(2, UnityEngine.Debug.ExceptionCount, "listener failures isolated for pause and resume");

        source.UtcSeconds += 86400; Equal(260, Wait(board), "wall-clock edit does not skip live refill");
        source.Advance(259); Call(board, "Update"); Assert(board.GetOrder(1) == null, "still waiting before deadline");
        source.Advance(1); Call(board, "Update"); Assert(board.GetOrder(1) != null, "order created on deadline");
        Assert(board.Discard(1), "second countdown");
        source.Advance(600); Call(board, "Update"); Assert(board.GetOrder(1) != null, "background gap is counted");

        // Close while manually paused, then start another session 60 seconds later.
        board.Discard(1); source.Advance(40); runtime.Pause(); source.Advance(900);
        Call(save, "OnApplicationQuit"); var saved = store.Saved;
        Equal(260, SavedWait(store, source), "snapshot excludes pause before close");
        Call(board, "OnDestroy"); Call(save, "OnDestroy"); Call(runtime, "OnDestroy");
        source.Advance(60);
        runtime = new GameTimeRuntime(); clock = new GameClock(source);
        Set(runtime, "clock", clock); Set(runtime, "timers", new TimerService(clock)); Call(runtime, "Awake");
        store = new MemoryEconomyStore(); save = NewSave(saved, store); board = NewBoard();
        Assert(!runtime.IsPaused, "manual pause belongs to the old session");
        Equal(200, Wait(board), "offline refill is restored from legacy UTC");
        source.Advance(200); Call(board, "Update"); Assert(board.GetOrder(1) != null, "restored countdown creates one order");
        Equal(3, save.OpenOrders.Count, "reload does not duplicate orders");

        // Forced write failure must remain dirty and retry, even when nothing else changes.
        Call(save, "LateUpdate"); int writes = store.Writes;
        store.Fail = true; Call(save, "OnApplicationQuit"); Equal(writes + 1, store.Writes, "forced save attempted");
        store.Fail = false; Call(save, "LateUpdate"); Equal(writes + 1, store.Writes, "retry waits for backoff");
        UnityEngine.Time.unscaledTime += 2; Call(save, "LateUpdate"); Equal(writes + 2, store.Writes, "failed forced save retries");
        Action failPrepare = () => throw new Exception("Invalid snapshot");
        save.PreparingSave += failPrepare; save.MarkDirty(); Call(save, "LateUpdate");
        Equal(writes + 2, store.Writes, "failed preparation never writes stale state");
        save.PreparingSave -= failPrepare; UnityEngine.Time.unscaledTime += 2; Call(save, "LateUpdate");
        Equal(writes + 3, store.Writes, "prepare failure recovers");

        // A work owner can settle its pre-pause segment before the clock is frozen.
        runtime.Timers.StartWork("work", 10);
        double start = clock.Read().GameSeconds;
        runtime.PauseChanging += pausing => { if (pausing) runtime.Timers.CreditWork("work", clock.Read().GameSeconds - start); };
        source.Advance(3); runtime.Pause(); Equal(7, runtime.Timers.Read("work").remainingSeconds, "work settles before pause");
        source.Advance(30); runtime.Timers.CreditWork("work", 30);
        Equal(7, runtime.Timers.Read("work").remainingSeconds, "paused work cannot accrue");
        runtime.Resume();
        Call(board, "OnDestroy"); Call(save, "OnDestroy"); Call(runtime, "OnDestroy");
    }

    private static EconomySaveSystem NewSave(EconomySaveData data, MemoryEconomyStore store)
    {
        var save = new EconomySaveSystem(); Set(save, "data", data); Set(save, "store", store); Call(save, "Awake"); return save;
    }
    private static OrderBoardSystem NewBoard()
    { var board = new OrderBoardSystem(); Call(board, "Awake"); Call(board, "Start"); return board; }
}
