using MathUnit;
using System;
using System.Runtime.InteropServices;

namespace TrackPlanner.PathFinder
{
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct BacktrackInfo
    {
        public Placement Source { get; }
        public long? IncomingRoadId { get; }
        public RoadCondition? IncomingCondition { get; }
        public Length RunningRouteDistance { get; }
        public TimeSpan RunningTime { get; }

        public BacktrackInfo(Placement source,
            long? incomingRoadId, RoadCondition? incomingCondition, Length runningRouteDistance, TimeSpan runningTime)
        {
            IncomingCondition = incomingCondition;
            Source = source;
            IncomingRoadId = incomingRoadId;
            RunningRouteDistance = runningRouteDistance;
            this.RunningTime = runningTime;
        }
    }

   

}