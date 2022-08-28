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
            var node_point = map.Nodes[nodeId];
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
        private readonly Dictionary<long, List<RoadSnapInfo>> roads;

        public IEnumerable<RoadSnapInfo> Values => this.roads.Values.SelectMany(x => x);
        public IEnumerable<long> Roads => this.roads.Keys;
        public int Count => this.roads.Count;

        public int DEBUG_TrackIndex { get; }
        public GeoZPoint UserPoint { get; }
        public IReadOnlySet<long> ReachableNodes { get; }
        public bool IsFinal { get; }
        public bool AllowSmoothing { get; }
        public Length NextBucketDistance { get; private set; }


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
                .Select(it => it.Value).ToList());
        }

        public RoadBucket RebuildWithSingleSnap(GeoZPoint snapCrosspoint, long snapNodeId)
        {
            var snap = this.roads.SelectMany(it => it.Value).Single(it => this.map.GetNode(it.RoadIdx) == snapNodeId && it.TrackCrosspoint == snapCrosspoint);
            return new RoadBucket(DEBUG_TrackIndex,this.map,this.nodeId,UserPoint,calc,new Dictionary<RoadIndexLong, RoadSnapInfo>()
            {
                [snap.RoadIdx] = snap
            },ReachableNodes,UsedProximityLimit,IsFinal,AllowSmoothing);
        }


        public IEnumerator<RoadSnapInfo> GetEnumerator()
        {
            return this.Values.GetEnumerator();
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

        public bool IsSwitcher(RoadIndexLong idx)
        {
            long node_id = map.GetNode(idx);
            foreach (var list in this.roads.Where(it => it.Key != idx.RoadMapIndex).Select(it => it.Value))
            {
                if (list.Select(it => map.GetNode(it.RoadIdx)).Contains(node_id))
                    return true;
            }

            return false;
        }

        public IEnumerable<(RoadIndexLong index, Length snapDistance)> GetAtNode(in RoadIndexLong idx)
        {
            return GetAtNode(map.GetNode(idx));
        }

        public IEnumerable<(RoadIndexLong index, Length snapDistance)> GetAtNode(long nodeId)
        {
            IEnumerable<RoadSnapInfo> get_entries()
            {
                foreach (var entry in this.Values)
                    if (map.GetNode(entry.RoadIdx) == nodeId)
                        yield return entry;
            }

            return get_entries().OrderBy(it => it.LEGACY_ShortestNextDistance).Select(it => (Idx: it.RoadIdx, it.TrackSnapDistance));
        }

        public void ComputeEndingDistances()
        {
            Length total_min_dist = Length.MaxValue;

            foreach (var list in this.roads.Values)
            {
                for (int i = 0; i < list.Count; ++i)
                {
                    Length end_dist = calc.GetDistance(map.GetPoint(list[i].RoadIdx), list[i].TrackCrosspoint);

                    list[i] = new RoadSnapInfo(list[i].RoadIdx, list[i].TrackSnapDistance, list[i].TrackCrosspoint, list[i].DistanceAlongRoad, end_dist);

                    total_min_dist = total_min_dist.Min(end_dist);
                }
            }

            this.NextBucketDistance = total_min_dist;
        }

        public void ComputeDistancesTo(RoadBucket dest)
        {
            Length total_min_dist = Length.MaxValue;

            foreach (var curr_entry in this.Values.ToArray())
            {
                Length min_dist = Length.MaxValue;
                foreach (var dest_entry in dest.Values)
                {
                    if (curr_entry.RoadIdx == dest_entry.RoadIdx)
                        min_dist = Length.Zero;
                    else
                        min_dist = min_dist.Min(calc.GetDistance(map.GetPoint(curr_entry.RoadIdx), map.GetPoint(dest_entry.RoadIdx)));

                    if (min_dist == Length.Zero)
                        break;
                }

                List<RoadSnapInfo> list = this.roads[curr_entry.RoadIdx.RoadMapIndex];
                int index_of = list.IndexOf(it => it.RoadIdx == curr_entry.RoadIdx);
                list[index_of] = new RoadSnapInfo(curr_entry.RoadIdx, curr_entry.TrackSnapDistance, curr_entry.TrackCrosspoint, curr_entry.DistanceAlongRoad, min_dist);

                total_min_dist = total_min_dist.Min(min_dist);
            }

            this.NextBucketDistance = total_min_dist;
        }

        public void Remove(RoadIndexLong idx)
        {
            if (this.roads.TryGetValue(idx.RoadMapIndex, out var list))
            {
                list.RemoveAll(it => it.RoadIdx == idx);
                if (list.Count == 0)
                    this.roads.Remove(idx.RoadMapIndex);
            }
        }

        public void RemoveRoads(long roadId)
        {
            this.roads.Remove(roadId);
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

        // returns -1 if we have smaller indices, or +1 if we have greater ones
        public int GetExistingRoadSide(RoadIndexLong idx)
        {
            if (!this.roads.TryGetValue(idx.RoadMapIndex, out var list))
                return 0;

            foreach (var entry in list)
            {
                int cmp = entry.RoadIdx.IndexAlongRoad.CompareTo(idx.IndexAlongRoad);
                if (cmp != 0)
                    return cmp;
            }

            return 0;
        }

        // returns true if it finds we have smaller/greater indices for given road
        public bool HasRoadSide(RoadIndexLong idx, int side)
        {
            if (side == 0)
                return false;

            if (!this.roads.TryGetValue(idx.RoadMapIndex, out var list))
                return false;

            foreach (var entry in list)
            {
                int cmp = entry.RoadIdx.IndexAlongRoad.CompareTo(idx.IndexAlongRoad);
                if (cmp == side)
                    return true;
            }

            return false;
        }

        public bool TryMerge(RoadSnapInfo entry)
        {
            if (!this.roads.TryGetValue(entry.RoadIdx.RoadMapIndex, out List<RoadSnapInfo>? list))
                return false;
            int index_of = list.IndexOf(it => it.RoadIdx == entry.RoadIdx);
            if (index_of == -1)
                return false;

            if (entry.LEGACY_ShortestNextDistance != Length.Zero || list[index_of].LEGACY_ShortestNextDistance != Length.Zero)
                throw new Exception();

            if (list[index_of].TrackSnapDistance > entry.TrackSnapDistance)
                list[index_of] = new RoadSnapInfo(entry.RoadIdx, entry.TrackSnapDistance, entry.TrackCrosspoint, entry.DistanceAlongRoad, Length.Zero);

            return true;
        }


        public void Clear()
        {
            this.roads.Clear();
        }

        public override string ToString()
        {
            if (this.nodeId == null)
                return this.UserPoint.ToString();
            else
                return $"n#{this.nodeId}@{UserPoint}";
        }

        public int GetRoadSwitchesCount()
        {
            return this.roads.Values
                .SelectMany(it => it)
                // group by map nodes
                .GroupBy(it => map.GetNode(it.RoadIdx))
                // there is a road switch if there are at least two roads sharing the same node
                .Where(it => it.Count() > 1)
                // count such switches
                .Count();
        }

    }
}
