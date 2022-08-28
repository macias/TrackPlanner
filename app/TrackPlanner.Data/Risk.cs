using System;

namespace TrackPlanner.Data
{
    [Flags]
    public enum Risk
    {
        None = 0,
        Dangerous = 1,
        Suppressed = 2,
        Uncomfortable = 4,
        HighTrafficBikeLane = 8,
    }
}
