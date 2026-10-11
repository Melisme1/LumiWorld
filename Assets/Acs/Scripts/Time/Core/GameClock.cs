using System;

namespace LumiWorld.Acs.TimeSystem
{
    public readonly struct TimeReading
    {
        public double UtcSeconds { get; }
        public double GameSeconds { get; }
        public bool IsPaused { get; }
        public TimeReading(double utcSeconds, double gameSeconds, bool isPaused)
        { UtcSeconds = utcSeconds; GameSeconds = gameSeconds; IsPaused = isPaused; }
    }

    // Main-thread/session scoped. Focus and timeScale do not change this clock; pause is explicit.
    // GameSeconds is NOT an epoch timestamp and must never be persisted as a UTC deadline.
    public sealed class GameClock
    {
        private readonly ITimeSource source;
        private double lastMonotonic, gameSeconds;
        public bool IsPaused { get; private set; }

        public GameClock(ITimeSource source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            lastMonotonic = TimeNumbers.NonNegative(source.MonotonicSeconds, nameof(source));
        }

        public TimeReading Read()
        {
            double monotonic = TimeNumbers.NonNegative(source.MonotonicSeconds, nameof(source));
            double utc = TimeNumbers.NonNegative(source.UtcSeconds, nameof(source));
            if (monotonic < lastMonotonic) throw new InvalidOperationException("Monotonic time moved backwards.");
            double next = IsPaused ? gameSeconds : TimeNumbers.Add(gameSeconds, monotonic - lastMonotonic);
            lastMonotonic = monotonic;
            gameSeconds = next;
            return new TimeReading(utc, gameSeconds, IsPaused);
        }

        public bool SetPaused(bool paused)
        {
            Read(); // Account for the old state before changing it, even when no Update ran.
            if (IsPaused == paused) return false;
            IsPaused = paused;
            return true;
        }
    }

    internal static class TimeNumbers
    {
        internal static double NonNegative(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(name, "Time must be finite and non-negative.");
            return value;
        }
        internal static double Positive(double value, string name)
        {
            NonNegative(value, name);
            if (value == 0) throw new ArgumentOutOfRangeException(name, "Interval must be positive.");
            return value;
        }
        internal static double Add(double a, double b) => NonNegative(a + b, "time sum");
    }
}
