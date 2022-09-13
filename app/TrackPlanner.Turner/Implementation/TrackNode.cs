using System;
using System.Collections.Generic;
using System.Linq;
using TrackPlanner.Shared;
using TrackPlanner.LinqExtensions;
using TrackPlanner.Mapping;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Turner.Implementation

{
    internal sealed class TrackNode 
    {
        public static TrackNode Create(IWorldMap map, long nodeId)
        {
            return new TrackNode(map,nodeId, map.GetRoadsAtNode(nodeId)
                .GroupBy(it => it.RoadMapIndex)
                .ToDictionary(it => it.Key, it => it.Select(it => it.IndexAlongRoad).ToList().ReadOnlyList()));
        }

        private readonly IWorldMap map;

        // road id -> indices along road nodes (1 or -- in case of roundabout start/end -- 2)
        private readonly IReadOnlyDictionary<long, IReadOnlyList<ushort>> dict;
        public long? RoundaboutId { get; }


        public long NodeId { get; }
        public GeoZPoint Point { get; }

        public int Count => this.dict.Count;

        public bool CycleWayExit { get; set; }
        public bool ForwardCycleWayCorrected { get; set; }
        public bool BackwardCycleWayCorrected { get; set; }
        public bool CyclewaySwitch { get; set; }
        public bool BackwardCyclewayUncertain { get; set; }
        public bool ForwardCyclewayUncertain { get; set; }
        public DirectionalArray<RoadIndexLong> Segment { get; }

        private TrackNode(IWorldMap map,long nodeId,
            // road id -> indices along road
            IReadOnlyDictionary<long, IReadOnlyList<ushort>> dict)
        {
            this.map = map;
            // remove all point-roads (like crossings)
            this.dict = dict
                .Where(it => map.GetRoad(it.Key).Nodes.Count > 1)
                .ToDictionary(it => it.Key, it => it.Value);

            if (this.dict.Values.Any(it => it.Count < 1 || it.Count > 2))
                throw new ArgumentException();

            NodeId = nodeId;
            
            this.Point = map.GetPoint(NodeId);

            foreach (var entry in this.dict)
            {
                if (map.GetRoad(entry.Key).IsRoundabout)
                {
                    RoundaboutId = entry.Key;
                    break;
                }
            }

            this.Segment = new DirectionalArray<RoadIndexLong>();
        }

        public IReadOnlyDictionary<long, (ushort currentIndex, ushort nextIndex)> ShortestSegmentsIntersection(TrackNode other)
        {
            var intersect = Linqer.Intersect(this.dict, other.dict, (a, b) => (a, b));
            // this is naive, we assume the highest index from current node and the lowest index from the next node are the closest ones
            // but roads can have loops and knots so this is VERY shaky

            // todo: compute all permutations and return the actual closests one, by computing segment distance
            // todo: this is wrong also because we cannot assume we are along (and not in reverse) of given road
            return intersect.ToDictionary(it => it.Key, it => (it.Value.a.OrderByDescending(x => x).First(), it.Value.b.OrderBy(x => x).First()));
        }

        internal bool IsDirectionAllowed(long roadId, in RoadIndexLong dest)
        {
            return map.IsDirectionAllowed(new RoadIndexLong(roadId, dict[roadId].First()), dest);
        }

    }
}
