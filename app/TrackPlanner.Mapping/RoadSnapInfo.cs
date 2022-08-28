using System.Runtime.InteropServices;
using MathUnit;
using TrackPlanner.Shared;
using TrackPlanner.Mapping;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Mapping
{
    [StructLayout(LayoutKind.Auto)]
    public readonly struct RoadSnapInfo
    {
        public RoadIndexLong RoadIdx { get; }
        public Length TrackSnapDistance { get; }
        public GeoZPoint TrackCrosspoint { get; }
        // distance from crosspoint to the given node
        public Length DistanceAlongRoad { get; }
        
        public Length LEGACY_ShortestNextDistance { get; }

        public RoadSnapInfo(RoadIndexLong roadIdx, Length trackSnapDistance, in GeoZPoint trackCrosspoint, Length distanceAlongRoad, Length shortestNextDistance)
        {
            this.RoadIdx = roadIdx;
            this.TrackSnapDistance = trackSnapDistance;
            TrackCrosspoint = trackCrosspoint;
            DistanceAlongRoad = distanceAlongRoad;
            this.LEGACY_ShortestNextDistance = shortestNextDistance;
        }

        public void Deconstruct(out RoadIndexLong idx, out Length trackSnapDistance,out GeoZPoint trackCrosspoint, out Length distanceAlongRoad, out Length shortestNextDistance)
        {
            idx = this.RoadIdx;
            trackSnapDistance = this.TrackSnapDistance;
            shortestNextDistance = this.LEGACY_ShortestNextDistance;
            distanceAlongRoad = this.DistanceAlongRoad;
            trackCrosspoint = this.TrackCrosspoint;
        }

    }
  }
