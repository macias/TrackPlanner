using System;
using System.Collections.Generic;
using TrackPlanner.Data;

namespace TrackPlanner.Logic.Tests.Data
{
    public class DummyDay : IDay<IAnchor>
    {
        public TimeSpan Start { get; set; }
        IReadOnlyList<IReadOnlyAnchor> IReadOnlyDay.Anchors => this.Anchors;
        public List<IAnchor> Anchors { get; set; } = default!;
    }
}