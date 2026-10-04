using System;

namespace Template.Core.Time
{
    /// <summary>
    /// Inject this instead of calling DateTime.UtcNow, so daily rewards, lives and event timers
    /// can be tested with a fake clock.
    /// </summary>
    public interface IClock
    {
        DateTime UtcNow { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
