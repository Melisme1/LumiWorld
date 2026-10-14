using System;
using System.Collections.Generic;

namespace LumiWorld.Acs.TimeSystem
{
    public enum TimerKind { Countdown, Interval, Work }
    public enum OfflineTimerPolicy { Freeze, Advance }
    // Coalesce emits one signal after a long gap; CountElapsed reports how many intervals elapsed.
    public enum IntervalCatchUpPolicy { Coalesce, CountElapsed }

    [Serializable]
    public sealed class TimerRecord
    {
        public string id;
        public TimerKind kind;
        public double durationSeconds;
        public double remainingSeconds;
        public OfflineTimerPolicy offlinePolicy;
        public IntervalCatchUpPolicy catchUpPolicy;
        public bool isSuspended;
        public bool isCompleted;
        public long pendingSignals;

        internal TimerRecord Copy() => (TimerRecord)MemberwiseClone();
    }

    // Transport data only: no callbacks, Unity references, file paths, or session clock values.
    [Serializable]
    public sealed class TimerSnapshot
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public double savedAtUtcSeconds;
        public List<TimerRecord> timers = new List<TimerRecord>();
    }

    public readonly struct TimerRestoreResult
    {
        public double AppliedOfflineSeconds { get; }
        public bool ClockMovedBackwards { get; }
        public TimerRestoreResult(double seconds, bool backwards)
        { AppliedOfflineSeconds = seconds; ClockMovedBackwards = backwards; }
    }
}
