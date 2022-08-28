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

        public void Write(BinaryWriter writer)
        {
            writer.Write(this.roadSegments.Count);
            foreach (var elem in this.roadSegments)
                elem.Write(writer);
        }
        
        public static RoadGridCell Read(BinaryReader reader)
        {
            var count = reader.ReadInt32();
            var segments = new HashSet<RoadIndexLong>(capacity: count);
            for (int i = 0; i < count; ++i)
                segments.Add(RoadIndexLong.Read(reader));

            return new RoadGridCell(segments.ToList());
        }

        public static unsafe RoadGridCell Load(IReadOnlyList<BinaryReader> readers)
        {
            var counts = stackalloc int[readers.Count];
            int total_count = 0;
            for (int r=0;r<readers.Count;++r)
            {
                var c = readers[r].ReadInt32();
                counts[r] = c;
                total_count += c;
            }

            var segments = new HashSet<RoadIndexLong>(capacity: total_count);

            for (int r = 0; r < readers.Count; ++r)
            {
                for (int i = 0; i < counts[r]; ++i)
                    segments.Add(RoadIndexLong.Read(readers[r]));
            }

            return new RoadGridCell(segments.ToList());
        }
    }
}
