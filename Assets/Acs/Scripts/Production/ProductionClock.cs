using System;
using LumiWorld.Acs.TimeSystem;

namespace LumiWorld.Acs
{
    [Obsolete("Use GameClock for pause-aware gameplay or ITimeSource for UTC persistence.")]
    public interface IProductionClock
    {
        double UtcSeconds { get; }
    }

    // Compatibility for old callers only. ResourceProductionRuntime now uses GameTimeRuntime.Clock.
    [Obsolete("Use GameTimeRuntime.Clock. Raw UTC does not honor gameplay pause.")]
    public sealed class SystemProductionClock : IProductionClock
    {
        private readonly SystemTimeSource source = new SystemTimeSource();
        public double UtcSeconds => source.UtcSeconds;
    }
}
