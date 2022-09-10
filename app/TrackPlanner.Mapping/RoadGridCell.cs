using MathUnit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrackPlanner.Shared;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Mapping
{
  

    public sealed class RoadGridCell
    {
        // segment from given road index to (implicit) next road index
        private readonly List<RoadIndexLong> roadSegments; // when working, we don't need hashset, so let's keep it list for lower memory

        public int Count => this.roadSegments.Count;
        public IEnumerable<RoadIndexLong> Segments => this.roadSegments;

        public RoadGridCell(List<RoadIndexLong>? segmets = null)
        {
            this.roadSegments = segmets ?? new List<RoadIndexLong>();
        }

        internal void Add(RoadIndexLong roadIdx)
        {
            roadSegments.Add(roadIdx);
        }

        public IEnumerable<RoadSnapInfo> GetSnaps(IWorldMap map, IGeoCalculator calc,GeoZPoint point, Length snapLimit,Func<RoadInfo,bool>? predicate)
        {
            foreach (var idx in roadSegments)
            {
                if (predicate != null && !predicate(map.Roads[idx.RoadMapIndex]))
                    continue;

                // because we basically look for points on mapped ways, we expect the difference to be so small that we can use plane/euclidian distance
                var start = map.GetPoint(idx);
                var end = map.GetPoint(idx.Next());
                (var snap_distance, var cx, Length distance_along_segment) = calc.GetDistanceToArcSegment(point, start, end);
                if (snap_distance <= snapLimit)
                {
                    yield return new RoadSnapInfo(idx, snap_distance, cx, distance_along_segment, shortestNextDistance: Length.Zero);
                    yield return new RoadSnapInfo(idx.Next(), snap_distance, cx, calc.GetDistance(start, end) - distance_along_segment, shortestNextDistance: Length.Zero);
                }
            }
        }
    }
}
