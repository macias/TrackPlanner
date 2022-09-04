using System;
using Geo;

namespace TrackPlanner.Data.Stored
{
    public sealed class ScheduleAnchor : IAnchor
    {
        public GeoPoint UserPoint { get; set; }
        public TimeSpan Break { get; set; }
        public string Label { get; set; }
        public bool IsPinned { get; set; }

        public ScheduleAnchor()
        {
            Label = "";
        }
    }
}