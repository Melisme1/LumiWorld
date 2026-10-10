using System;
using System.Collections.Generic;

namespace LumiWorld.Acs.TimeSystem
{
    // A bounded set of timers owned by one feature. Pull results instead of persisting callbacks.
    // Completion signals are not resource transactions; persist consumption with the owner's state.
    public sealed class TimerService
    {
        private readonly GameClock clock;
        private Dictionary<string, TimerRecord> timers = new Dictionary<string, TimerRecord>(StringComparer.Ordinal);
        private double lastGameSeconds;
        public int Count => timers.Count;

        public TimerService(GameClock clock)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            lastGameSeconds = clock.Read().GameSeconds;
        }

        public void StartCountdown(string id, double seconds, OfflineTimerPolicy offline = OfflineTimerPolicy.Advance)
            => Start(id, TimerKind.Countdown, seconds, offline, IntervalCatchUpPolicy.Coalesce);
        public void StartInterval(string id, double seconds, IntervalCatchUpPolicy catchUp = IntervalCatchUpPolicy.Coalesce,
            OfflineTimerPolicy offline = OfflineTimerPolicy.Freeze)
            => Start(id, TimerKind.Interval, seconds, offline, catchUp);
        public void StartWork(string id, double targetSeconds)
            => Start(id, TimerKind.Work, targetSeconds, OfflineTimerPolicy.Freeze, IntervalCatchUpPolicy.Coalesce);

        private void Start(string id, TimerKind kind, double seconds, OfflineTimerPolicy offline, IntervalCatchUpPolicy catchUp)
        {
            var record = new TimerRecord { id = id, kind = kind, durationSeconds = seconds,
                remainingSeconds = seconds, offlinePolicy = offline, catchUpPolicy = catchUp };
            Validate(record);
            if (timers.ContainsKey(id)) throw new ArgumentException("Timer ID already exists; cancel it explicitly before restarting.", nameof(id));
            Tick();
            timers.Add(id, record);
            Advance(record, 0);
        }

        public void Tick() => Tick(clock.Read());
        private void Tick(TimeReading now)
        {
            double elapsed = now.GameSeconds - lastGameSeconds;
            foreach (var timer in timers.Values)
                if (timer.kind != TimerKind.Work) Advance(timer, elapsed);
            lastGameSeconds = now.GameSeconds;
        }

        public bool Contains(string id) => id != null && timers.ContainsKey(id);
        public bool Cancel(string id) => id != null && timers.Remove(id);
        public void Clear() { timers.Clear(); lastGameSeconds = clock.Read().GameSeconds; }

        // Copies prevent UI/callers from mutating the authoritative timer state.
        public TimerRecord Read(string id)
        {
            Tick();
            return Require(id).Copy();
        }

        public void SetSuspended(string id, bool suspended)
        {
            Tick();
            Require(id).isSuspended = suspended;
        }

        // Caller supplies actual work, already bounded by availability/capacity. No per-slot counting here.
        public void CreditWork(string id, double workedSeconds)
        {
            TimeNumbers.NonNegative(workedSeconds, nameof(workedSeconds));
            Tick();
            var timer = Require(id);
            if (timer.kind != TimerKind.Work) throw new InvalidOperationException("CreditWork requires a work timer.");
            if (!clock.IsPaused) Advance(timer, workedSeconds);
        }

        public bool TryConsumeSignals(string id, out long count)
        {
            Tick();
            var timer = Require(id);
            count = timer.pendingSignals;
            timer.pendingSignals = 0;
            return count > 0;
        }

        public TimerSnapshot Capture()
        {
            var now = clock.Read();
            Tick(now);
            var snapshot = new TimerSnapshot { savedAtUtcSeconds = now.UtcSeconds };
            foreach (var timer in timers.Values) snapshot.timers.Add(timer.Copy());
            snapshot.timers.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return snapshot;
        }

        // Restores a feature's entire timer set atomically. The caller chooses the offline cap.
        // A manual app pause is session-only. Offline advancement follows each timer's saved policy.
        public TimerRestoreResult Restore(TimerSnapshot snapshot, double maxOfflineSeconds)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.version != TimerSnapshot.CurrentVersion) throw new NotSupportedException("Unsupported timer snapshot version.");
            TimeNumbers.NonNegative(maxOfflineSeconds, nameof(maxOfflineSeconds));
            TimeNumbers.NonNegative(snapshot.savedAtUtcSeconds, nameof(snapshot.savedAtUtcSeconds));
            if (snapshot.timers == null) throw new ArgumentException("Missing timer records.");
            var now = clock.Read();
            double offline = Math.Min(maxOfflineSeconds, Math.Max(0, now.UtcSeconds - snapshot.savedAtUtcSeconds));
            var restored = new Dictionary<string, TimerRecord>(StringComparer.Ordinal);
            foreach (var item in snapshot.timers)
            {
                Validate(item);
                var copy = item.Copy();
                if (restored.ContainsKey(copy.id)) throw new ArgumentException("Duplicate timer ID in snapshot.");
                if (copy.offlinePolicy == OfflineTimerPolicy.Advance) Advance(copy, offline);
                restored.Add(copy.id, copy);
            }
            timers = restored;
            lastGameSeconds = now.GameSeconds;
            return new TimerRestoreResult(offline, now.UtcSeconds < snapshot.savedAtUtcSeconds);
        }

        private TimerRecord Require(string id)
        {
            if (id == null || !timers.TryGetValue(id, out var timer)) throw new KeyNotFoundException("Unknown timer: " + id);
            return timer;
        }

        private static void Advance(TimerRecord timer, double elapsed)
        {
            if (timer.isSuspended || timer.isCompleted) return;
            if (elapsed < timer.remainingSeconds) { timer.remainingSeconds -= elapsed; return; }
            if (timer.kind != TimerKind.Interval)
            { timer.remainingSeconds = 0; timer.isCompleted = true; timer.pendingSignals = 1; return; }
            double overdue = elapsed - timer.remainingSeconds;
            double occurrences = 1 + Math.Floor(overdue / timer.durationSeconds);
            timer.remainingSeconds = timer.durationSeconds - overdue % timer.durationSeconds;
            if (timer.catchUpPolicy == IntervalCatchUpPolicy.Coalesce) timer.pendingSignals = 1;
            else
            {
                double pending = timer.pendingSignals + occurrences;
                timer.pendingSignals = pending >= long.MaxValue ? long.MaxValue : (long)pending;
            }
        }

        private static void Validate(TimerRecord timer)
        {
            if (timer == null || string.IsNullOrWhiteSpace(timer.id)) throw new ArgumentException("Timer ID is required.");
            if (!Enum.IsDefined(typeof(TimerKind), timer.kind) || !Enum.IsDefined(typeof(OfflineTimerPolicy), timer.offlinePolicy) ||
                !Enum.IsDefined(typeof(IntervalCatchUpPolicy), timer.catchUpPolicy)) throw new ArgumentException("Unknown timer policy.");
            TimeNumbers.NonNegative(timer.durationSeconds, nameof(timer.durationSeconds));
            TimeNumbers.NonNegative(timer.remainingSeconds, nameof(timer.remainingSeconds));
            if (timer.kind == TimerKind.Interval) TimeNumbers.Positive(timer.durationSeconds, nameof(timer.durationSeconds));
            if (timer.remainingSeconds > timer.durationSeconds || timer.pendingSignals < 0 ||
                (timer.isCompleted && timer.remainingSeconds != 0) ||
                (timer.kind == TimerKind.Interval && (timer.isCompleted || timer.remainingSeconds == 0)) ||
                (timer.kind == TimerKind.Work && timer.offlinePolicy != OfflineTimerPolicy.Freeze) ||
                (timer.kind != TimerKind.Interval && (timer.pendingSignals > 1 || (!timer.isCompleted && timer.pendingSignals > 0))))
                throw new ArgumentException("Inconsistent timer state.");
        }
    }
}
