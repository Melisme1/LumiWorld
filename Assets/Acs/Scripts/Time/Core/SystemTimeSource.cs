using System;
using System.Diagnostics;

namespace LumiWorld.Acs.TimeSystem
{
    public sealed class SystemTimeSource : ITimeSource
    {
        private readonly Stopwatch elapsed = Stopwatch.StartNew();
        public double UtcSeconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
        public double MonotonicSeconds => elapsed.Elapsed.TotalSeconds;
    }
}
