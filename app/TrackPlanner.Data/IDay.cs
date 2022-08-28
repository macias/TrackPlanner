using System;
using System.Collections.Generic;
using MathUnit;

namespace TrackPlanner.Data
{
   
    public interface IReadOnlyDay
    {
        TimeSpan Start { get; }
        IReadOnlyList<IReadOnlyAnchor> Anchors { get; }
    }
  
    public interface IDay<TAnchor> : IReadOnlyDay
    where TAnchor : IAnchor
    {
        new TimeSpan Start { get; set; }
        new List<TAnchor> Anchors { get; }
    }
}