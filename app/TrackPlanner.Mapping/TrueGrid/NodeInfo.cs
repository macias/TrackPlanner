using MathUnit;
using System;
using System.Collections.Generic;
using TrackPlanner.Shared;
using TrackPlanner.Mapping.Data;
using TrackPlanner.Structures;

namespace TrackPlanner.Mapping
{

    internal readonly record struct NodeInfo
    {
        public GeoZPoint Point { get; }
        public IReadOnlyList<RoadIndexLong> Roads { get; }

        public NodeInfo(GeoZPoint point, IReadOnlyList<RoadIndexLong> roads)
        {
            Point = point;
            Roads = roads;
        }
    }
  
}