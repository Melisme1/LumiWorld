using System;

namespace LumiWorld.Acs
{
    public interface IProductionClock
    {
        double UtcSeconds { get; }
    }

    // Production uses elapsed wall time, independently of frame rate and Time.timeScale.
    public sealed class SystemProductionClock : IProductionClock
    {
        public double UtcSeconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
    }
}
