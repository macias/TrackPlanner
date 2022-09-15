using System;
using TrackPlanner.Data;

namespace TrackPlanner.Logic.Tests.Data
{
    public class DummyAnchor : IAnchor
    {
        public TimeSpan Break { get; set; }
        public string Label { get; set; } = default!;
        public bool IsPinned { get; set; }
    }
}