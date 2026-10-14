namespace LumiWorld.Acs.TimeSystem
{
    // UTC is for persistence/online reconciliation. Monotonic time measures this running session.
    // Implementations must use seconds and never move MonotonicSeconds backwards.
    public interface ITimeSource
    {
        double UtcSeconds { get; }
        double MonotonicSeconds { get; }
    }
}
