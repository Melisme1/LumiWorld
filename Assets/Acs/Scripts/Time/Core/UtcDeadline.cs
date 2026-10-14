using System;
using System.Globalization;

namespace LumiWorld.Acs.TimeSystem
{
    // Boundary adapter for existing ISO-8601 saves. Runtime countdowns use GameSeconds instead.
    public static class UtcDeadline
    {
        public static double Remaining(string isoUtc, double nowUtcSeconds, double maximumSeconds)
        {
            TimeNumbers.NonNegative(nowUtcSeconds, nameof(nowUtcSeconds));
            TimeNumbers.NonNegative(maximumSeconds, nameof(maximumSeconds));
            if (!DateTimeOffset.TryParse(isoUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var deadline)) return 0;
            return Math.Min(maximumSeconds, Math.Max(0, deadline.ToUnixTimeMilliseconds() / 1000d - nowUtcSeconds));
        }

        public static string Format(double utcSeconds)
        {
            TimeNumbers.NonNegative(utcSeconds, nameof(utcSeconds));
            return DateTimeOffset.FromUnixTimeMilliseconds(checked((long)Math.Round(utcSeconds * 1000)))
                .UtcDateTime.ToString("o", CultureInfo.InvariantCulture);
        }
    }
}
