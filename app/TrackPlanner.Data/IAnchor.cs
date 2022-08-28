using System;
using System.Collections.Generic;
using MathUnit;

namespace TrackPlanner.Data
{
    public interface IReadOnlyAnchor
    {
        TimeSpan Break { get; }
        string Label { get; }
        bool IsPinned { get; }
    }

    public interface IAnchor : IReadOnlyAnchor
    {
        new TimeSpan Break { get; set; }
        new string Label { get; set; }
        new bool IsPinned { get; set; }
    }
 
}