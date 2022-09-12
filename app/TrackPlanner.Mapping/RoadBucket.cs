using MathUnit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TrackPlanner.Mapping;
using TrackPlanner.LinqExtensions;
using TrackPlanner.Shared;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Mapping
{
    public sealed class RoadBucket : IEnumerable<RoadSnapInfo>
    {
        public static List<RoadBucket> GetRoadBuckets(IReadOnlyList<long> mapNodes,IWorldMap map, IGeoCalculator calc,bool allowSmoothing)
        {
            return mapNodes.ZipIndex().Select(it =>
            {
                return CreateBucket(it.index,it.item,map,calc, isFinal:it.index == 0 || it.index == mapNodes.Count - 1,allowSmoothing:allowSmoothing);
            }).ToList();
        }

        public static RoadBucket CreateBucket(int index,long nodeId,IWorldMap map, IGeoCalculator calc,bool isFinal,bool allowSmoothing)
        {
            var node_point = map.GetPoint(nodeId);
            Dictionary<RoadIndexLong, RoadSnapInfo> snaps = map.GetRoads(nodeId)
                .ToDictionary(idx => idx,
                    idx => new RoadSnapInfo(idx, trackSnapDistance: Length.Zero, trackCrosspoint: node_point, distanceAlongRoad: Length.Zero, shortestNextDistance: Length.Zero));
            return new RoadBucket(DEBUG_trackIndex: index, map, nodeId:nodeId, node_point, calc, snaps, reachableNodes: new HashSet<long>(), Length.Zero,  isFinal: isFinal,allowSmoothing);
        }

        private readonly IWorldMap map;
        private readonly long? nodeId;
        private readonly IGeoCalculator calc;
        public Length UsedProximityLimit { get; }

        // IMPORTANT: for given OSM node this type does not keep all roads (you have to fetch them from the map)
        // this is because we get the nodes by hitting segments in nearby, if some segment is too far
        // we won't register it

        // key: road id
        private readonly IReadOnlyDictionary<long, IReadOnlyList<RoadSnapInfo>> roads;

        public IEnumerable<RoadSnapInfo> RoadSnaps => this.roads.Values.SelectMany(x => x);
        public int Count => this.roads.Count;

        public int DEBUG_TrackIndex { get; }
        public GeoZPoint UserPoint { get; }
        public IReadOnlySet<long> ReachableNodes { get; }
        public bool IsFinal { get; }
        public bool AllowSmoothing { get; }

        //public IEnumerable<long> Nodes => this.Values.Select(it => map.GetNode(it.Idx)).Distinct();

        public RoadBucket(int DEBUG_trackIndex, IWorldMap map, long? nodeId, GeoZPoint userPoint, IGeoCalculator calculator,
            IReadOnlyDictionary<RoadIndexLong, RoadSnapInfo> snaps,
            IReadOnlySet<long> reachableNodes,
            Length usedProximityLimit, bool isFinal,bool allowSmoothing)
        {
            DEBUG_TrackIndex = DEBUG_trackIndex;
            this.map = map;
            this.nodeId = nodeId;
            this.UserPoint = userPoint;
            ReachableNodes = reachableNodes;
            this.calc = calculator;
            this.UsedProximityLimit = usedProximityLimit;
            IsFinal = isFinal;
            AllowSmoothing = allowSmoothing;
            this.roads = snaps
                // here we remove "lone islands", i.e. nodes which does not have connections (within given assigment)
                //.GroupBy(it => map.GetNode(it.Key))
                //.Where(it => it.Count()>1)                
                //.SelectMany(x => x)

                .GroupBy(it => it.Key.RoadMapIndex)
                .ToDictionary(it => it.Key, it => it.OrderBy(it => it.Key.IndexAlongRoad)
                .Select(it => it.Value).ToList().ReadOnlyList());
        }

        public RoadBucket RebuildWithSingleSnap(GeoZPoint snapCrosspoint, long snapNodeId)
        {
            var snap = RoadSnaps
                .Where(it => this.map.GetNode(it.RoadIdx) == snapNodeId && it.TrackCrosspoint == snapCrosspoint)
                .SingleOrNone();

            if (!snap.HasValue)
            {
                throw new Exception($"Cannot find single entry {snapNodeId}, cx {snapCrosspoint} in the bucket:"
                                    + Environment.NewLine
                                    + String.Join(Environment.NewLine, RoadSnaps.Select(it => $"node {this.map.GetNode(it.RoadIdx)}, cx {it.TrackCrosspoint}")));
            }

            return new RoadBucket(DEBUG_TrackIndex,this.map,this.nodeId,UserPoint,calc,new Dictionary<RoadIndexLong, RoadSnapInfo>()
            {
                [snap.Value.RoadIdx] = snap.Value
            },ReachableNodes,UsedProximityLimit,IsFinal,AllowSmoothing);
        }


        public IEnumerator<RoadSnapInfo> GetEnumerator()
        {
            return this.RoadSnaps.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }


        public IEnumerable<(RoadIndexLong index, Length snapDistance, GeoZPoint crosspoint)> TryGetSameRoad(long roadId)
        {
            if (this.roads.TryGetValue(roadId, out var list))
                return list.Select(it => (Idx: it.RoadIdx, it.TrackSnapDistance, it.TrackCrosspoint));
            else
                return Enumerable.Empty<(RoadIndexLong index, Length snapDistance, GeoZPoint crosspoint)>();
        }

        public IEnumerable<(RoadIndexLong index, Length snapDistance)> GetRoadNeighbourhood(RoadIndexLong idx)
        {
            if (this.roads.TryGetValue(idx.RoadMapIndex, out var list))
            {
                var left = list.Where(it => it.RoadIdx.IndexAlongRoad < idx.IndexAlongRoad).Select(it => (ass: it, exist: true))
                    .LastOrDefault();
                var curr = list.Where(it => it.RoadIdx.IndexAlongRoad == idx.IndexAlongRoad).Select(it => (ass: it, exist: true))
                    .SingleOrDefault();
                var right = list.Where(it => it.RoadIdx.IndexAlongRoad > idx.IndexAlongRoad).Select(it => (ass: it, exist: true))
                    .FirstOrDefault();


                if (left.exist)
                    yield return (left.ass.RoadIdx, left.ass.TrackSnapDistance);
                if (curr.exist)
                    yield return (curr.ass.RoadIdx, curr.ass.TrackSnapDistance);
                if (right.exist)
                    yield return (right.ass.RoadIdx, right.ass.TrackSnapDistance);
            }
        }

        public IEnumerable<(RoadIndexLong index, Length snapDistance)> GetAtNode(in RoadIndexLong idx)
        {
            return GetAtNode(map.GetNode(idx));
        }

        public IEnumerable<(RoadIndexLong index, Length snapDistance)> GetAtNode(long nodeId)
        {
            IEnumerable<RoadSnapInfo> get_entries()
            {
                foreach (var entry in this.RoadSnaps)
                    if (map.GetNode(entry.RoadIdx) == nodeId)
                        yield return entry;
            }

            return get_entries().OrderBy(it => it.LEGACY_ShortestNextDistance).Select(it => (Idx: it.RoadIdx, it.TrackSnapDistance));
        }

        public bool TryGetEntry(RoadIndexLong idx, out RoadSnapInfo info)
        {
            if (!this.roads.TryGetValue(idx.RoadMapIndex, out var list))
            {
                info = default;
                return false;
            }

            int index_of = list.IndexOf(it => it.RoadIdx.IndexAlongRoad == idx.IndexAlongRoad);
            if (index_of == -1)
            {
                info = default;
                return false;
            }

            info = list[index_of];
            return true;
        }

        public bool Contains(RoadIndexLong idx)
        {
            return TryGetEntry(idx, out _);
        }

        public override string ToString()
        {
            if (this.nodeId == null)
                return this.UserPoint.ToString();
            else
                return $"n#{this.nodeId}@{UserPoint}";
        }

    }
}
