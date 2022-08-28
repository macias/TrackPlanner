using MathUnit;
using System;
using System.Collections.Generic;
using System.Linq;
using TrackPlanner.Data;
using TrackPlanner.Shared;
using TrackPlanner.DataExchange;
using TrackPlanner.LinqExtensions;
using TrackPlanner.Mapping;
using TrackPlanner.Mapping.Data;
using TrackPlanner.PathFinder;

namespace TrackPlanner.Turner.Implementation

{
    internal sealed class RoadNodesGrid //: IRoadNodesGrid
    {

        private readonly ILogger logger;
        private readonly IWorldMap map;
        //private readonly AStarRouteFinder finder;
        private readonly IGeoCalculator calc;
        //private readonly bool computeMidRoutes;
        private readonly string? debugDirectory;
        private readonly RoadGrid memory;

        public RoadNodesGrid(ILogger logger, IWorldMap map, IGeoCalculator calc,
            RoadGrid memoryGrid,
            // when two points are not connected should we simply get midpoint or compute route just between them
            //bool computeMidRoutes, 
            string? debugDirectory)
        {
            this.map = map;
            //this.finder = new AStarRouteFinder(mapBooster, calc);

            this.logger = logger;
            this.calc = calc;
            //this.computeMidRoutes = computeMidRoutes;
            this.debugDirectory = debugDirectory;

            this.memory = memoryGrid;
            /*this.memory = new RoadGridMemory(logger,
                new RoadGridMemoryBuilder(logger,map, calc, gridCellSize, debugDirectory).BuildCells(),
                map, calc, gridCellSize, debugDirectory, legacyGetNodeAllRoads: true);*/
        }



        public (IEnumerable<GeoZPoint> recreated, IEnumerable<TrackNode> nodes) GetTrackNodesForTurns(IReadOnlyList<GeoZPoint> userTrack, 
            Length initProximityLimit,Length finalProximityLimit)
        {
            IEnumerable<long> id_nodes = getRawTrackNodesForTurns(userTrack, initProximityLimit, finalProximityLimit, out GeoZPoint startPoint, out GeoZPoint endPoint);
            // remove same CONSECUTIVE (because the track could be in shape of "8") points
            id_nodes = id_nodes.ConsecutiveDistinct().ToList();
            List<TrackNode> track_nodes = id_nodes.Select(id => TrackNode.Create(map, id)).ToList();
            fillGaps(track_nodes);
            List<GeoZPoint> recreated = track_nodes.Select(it => it.Point).ToList();
            recreated.Insert(0, startPoint);
            recreated.Add(endPoint);
            return (recreated.ConsecutiveDistinctBy(it => (it.Latitude, it.Longitude)), track_nodes);
        }

        private void fillGaps(List<TrackNode> trackNodes)
        {
            for (int i = trackNodes.Count - 2; i >= 0; --i)
            {
                IReadOnlyDictionary<long, (ushort currentIndex, ushort nextIndex)> diff = trackNodes[i].ShortestSegmentsIntersection(trackNodes[i + 1]);
                // if we have road nodes that are directly "connected" we don't need to fill gaps even if from other roads perspective it looks this way
                // consider
                // ===*===============*========
                //     \-------------/
                //  above is primary road, which (typically) does not need any fillers, but for service roads there are gaps
                if (diff.Any(it => this.map.LEGACY_RoadSegmentsDistanceCount(it.Key, it.Value.currentIndex, it.Value.nextIndex) == 1))
                    continue;

                var roads_with_gaps = diff
                    .Where(it => this.map.LEGACY_RoadSegmentsDistanceCount(it.Key, it.Value.currentIndex, it.Value.nextIndex) > 1)
                    .ToDictionary(it => it.Key, it => (it.Value.currentIndex, it.Value.nextIndex));
                if (roads_with_gaps.Count == 0)
                    continue;
                else if (roads_with_gaps.Count > 1)
                    throw new NotSupportedException();

                long road_id = roads_with_gaps.Keys.Single();
                (int current_index, int next_index) = roads_with_gaps[road_id];
                logger.Info($"Adding gaps for road {road_id} between {current_index} and {next_index}");
                trackNodes.InsertRange(i + 1,
                    map.GetRoadIndices(new RoadIndexLong(road_id, current_index), new RoadIndexLong(road_id, next_index)).Skip(1).SkipLast(1).Select(gap =>
                    TrackNode.Create(this.map, map.GetNode(gap))));

            }
        }

        private IEnumerable<long> getRawTrackNodesForTurns(IReadOnlyList<GeoZPoint> userTrack, Length initProximityLimit,
            Length finalProximityLimit,
            out GeoZPoint startPoint, out GeoZPoint endPoint)
        {
            // const double proximityLimitConversion = 0.000000000002;
            // double effective_proximity = proximityLimitConversion * proximityLimit.Meters;

            List<RoadBucket> buckets = this.memory.GetRoadBuckets(userTrack.Select(it => new RequestPoint(it.Convert(),false)).ToArray(), 
                initProximityLimit,finalProximityLimit, requireAllHits: false, singleMiddleSnaps:false);

            logger.Verbose($"Buckets: count {buckets.Count}, volume {buckets.Sum(it => it.Count)}, max {buckets.Max(it => it.Count)}");

            fillGapsBetweenBuckets(buckets, initProximityLimit,finalProximityLimit);

            //easyCompressBuckets(buckets);
            //hardCompressBuckets(buckets, useRisky: !use_graph);

            if (debugDirectory != null)
            {
                visualizeBucket(Helper.GetUniqueFileName(debugDirectory, $"bucket-first.kml"), buckets.First());
                visualizeBucket(Helper.GetUniqueFileName(debugDirectory, $"bucket-last.kml"), buckets.Last());
            }

            logger.Verbose($"Cleaned Buckets: count {buckets.Count}, volume {buckets.Sum(it => it.Count)}, max {buckets.Max(it => it.Count)}");

            if (false)
            {
                if (debugDirectory != null)
                {
                    for (int i = 0; i < buckets.Count; ++i)
                        TrackWriter.WriteLabeled(Helper.GetUniqueFileName(debugDirectory, $"bucket-{buckets[i].DEBUG_TrackIndex}-{i}.kml"), null,
                            buckets[i].Select(it => (map.GetPoint(it.RoadIdx), $"{it.RoadIdx}")));
                }

                logBuckets(buckets);
            }

            var finder = new SolutionGraphFinder(logger, map, calc, debugDirectory);
            return finder.GetSolution(userTrack.First(), userTrack.Last(), buckets, out startPoint, out endPoint);
        }

        private void visualizeBucket(string filename, RoadBucket roadBucket)
        {
            IEnumerable<(RoadIndexLong idx, GeoZPoint cx, Length snap)> get_segments()
            {
                var iter = roadBucket.Values.GetEnumerator();
                if (!iter.MoveNext())
                    yield break;

                var last = iter.Current;

                while (iter.MoveNext())
                {
                    var curr = iter.Current;

                    if (last.RoadIdx.Next() == curr.RoadIdx)
                    {
                        yield return (last.RoadIdx, last.TrackCrosspoint, last.TrackSnapDistance);
                        if (last.TrackCrosspoint != curr.TrackCrosspoint)
                            yield return (last.RoadIdx, curr.TrackCrosspoint, curr.TrackSnapDistance);
                    }

                    last = curr;
                }
            }

            var segments = get_segments().ZipIndex().Select(it => (new[] { map.GetPoint(it.item.idx), map.GetPoint(it.item.idx.Next()) }.ToList().ReadOnlyList(), $"{it.index} {it.item.idx}"));
            var cxx = get_segments().ZipIndex().Select(it => (it.item.cx, it.index + " " + it.item.snap.ToString("0.##"), PointIcon.DotIcon));
            TrackWriter.BuildMultiple(tracks: segments, cxx).Save(filename);
        }


        private void fillGapsBetweenBuckets(List<RoadBucket> buckets, Length initProximityLimit, Length finalProximityLimit)
        {
            bool is_connected_to_next(int i)
            {
                HashSet<long> curr_nodes = buckets[i].Select(it => map.GetNode(it.RoadIdx)).ToHashSet();
                var next_nodes = buckets[i + 1].Select(it => map.GetNode(it.RoadIdx)).ToHashSet();
                curr_nodes.IntersectWith(next_nodes);
                if (curr_nodes.Any())
                    return true;

                foreach (var node in next_nodes)
                {
                    var curr_roads = buckets[i].GetAtNode(node).Select(it => it.index.RoadMapIndex).ToHashSet();
                    var next_roads = buckets[i + 1].GetAtNode(node).Select(it => it.index.RoadMapIndex).ToHashSet();
                    curr_roads.IntersectWith(next_roads);
                    if (curr_roads.Any())
                        return true;
                }

                return false;
            }

            // we need buckets that each is connected to the other
            for (int i = 0; i < buckets.Count - 1;)
            {
                if (is_connected_to_next(i))
                {
                    ++i;
                    continue;
                }
                RoadBucket mid_bucket;
                /*if (this.computeMidRoutes)
                {
                    long start_node = mapBooster.Map.GetNode(buckets[i].Values.First().Idx);
                    long end_node = mapBooster.Map.GetNode(buckets[i+1].Values.First().Idx);
                    if (!finder.TryFindPath(start_node,end_node,out List<long> path_nodes,out _))
                        throw new Exception();

                    path_nodes.RemoveAt(0);
                    path_nodes.RemoveAt(path_nodes.Count - 1);
                    path_nodes.Reverse();
                    foreach (long node_id in path_nodes)
                    {
                        var mid = map.Nodes[node_id];
                        mid_bucket = createBucket(-buckets.Count, mid, proximityLimit, automatic: true, closestPerRoad: false);
                        logger.Warning($"Filling bucket gap {i + 1} at {mid} from roads {(String.Join(", ", buckets[i].Select(it => it.Idx.RoadId).Distinct()))} to {(String.Join(", ", buckets[i + 1].Select(it => it.Idx.RoadId).Distinct()))} with {(String.Join(", ", mid_bucket.Select(it => it.Idx.RoadId).Distinct()))}");
                        buckets.Insert(i + 1, mid_bucket);
                    }
                }
                else*/
                {
                    // simply get mid point
                    var mid = calc.GetMidPoint(buckets[i].UserPoint, buckets[i + 1].UserPoint);
                    mid_bucket = this.memory.CreateBucket(-buckets.Count, mid, initProximityLimit, finalProximityLimit, automatic: true, singleSnap: false, isFinal: false,
                        allowSmoothing:buckets[i].AllowSmoothing || buckets[i+1].AllowSmoothing);
                    logger.Warning($"Filling bucket gap {i + 1} at {mid} from roads {(String.Join(", ", buckets[i].Select(it => it.RoadIdx.RoadMapIndex).Distinct()))} to {(String.Join(", ", buckets[i + 1].Select(it => it.RoadIdx.RoadMapIndex).Distinct()))} with {(String.Join(", ", mid_bucket.Select(it => it.RoadIdx.RoadMapIndex).Distinct()))}");
                    buckets.Insert(i + 1, mid_bucket);
                }
            }
        }

        private void logBuckets(IReadOnlyList<RoadBucket> buckets)
        {
            for (int i = 0; i < buckets.Count; ++i)
            {
                logger.Verbose($"Bucket {buckets[i].DEBUG_TrackIndex}:{i}, roads {(string.Join(", ", buckets[i].Select(it => $"{it.RoadIdx}({map.GetNode(it.RoadIdx)})")))}");
            }
            logger.Flush();

        }


        private void hardCompressBuckets(List<RoadBucket> buckets, bool useRisky)
        {
            while (true)
            {
                bool changed = false;

                for (int i = buckets.Count - 1; i >= 0; --i)
                {
                    foreach (var entry in buckets[i].ToArray())
                    {
                        for (int adj = i - 1; adj <= i + 1; adj += 2)
                        {
                            if (adj < 0 || adj >= buckets.Count)
                                continue;

                            // given road is not follow-up from the previous bucket 
                            // and we cannot switch to it by node within current bucket, so it is a dead entry
                            if (!buckets[adj].TryGetSameRoad(entry.RoadIdx.RoadMapIndex).Any()
                                && !buckets[i].IsSwitcher(entry.RoadIdx))
                            {
                                buckets[i].Remove(entry.RoadIdx);
                                changed = true;
                            }
                        }
                    }

                    if (i > 0 && i < buckets.Count - 1)
                    {
                        for (int adj = i - 1; adj <= i + 1; adj += 2)
                        {
                            // if current bucket is subset of adjacent bucket, then merge into adjacent
                            if (buckets[i].All(it => buckets[adj].TryGetEntry(it.RoadIdx, out _)))
                            {
                                buckets[i].Clear();
                                changed = true;
                            }
                        }

                        // if we have only single road in given bucket we know it won't switch roads here, 
                        // so we can potentially split this bucket and move roads to adjacent buckets
                        // only if those roads already exists there
                        if (buckets[i].GetRoadSwitchesCount() == 0
                            && buckets[i].All(it => buckets[i - 1].TryGetEntry(it.RoadIdx, out _) || buckets[i + 1].TryGetEntry(it.RoadIdx, out _)))
                        {
                            buckets[i].Clear();
                            changed = true;
                        }

                        // if given road has only one switch it has to have connections to prev/next buckets
                        // or in other words if it has connection to only one bucket and has no switches 
                        // we remove it 
                        foreach (var road_id in buckets[i].Roads.ToArray())
                        {
                            int required_switchers = 1;
                            // for connecting to the adjacent bucket the limit on required switchers is tougher
                            if (buckets[i - 1].TryGetSameRoad(road_id).Any())
                                --required_switchers;
                            if (buckets[i + 1].TryGetSameRoad(road_id).Any())
                                --required_switchers;

                            if (buckets[i].TryGetSameRoad(road_id).Count(it => buckets[i].IsSwitcher(it.index)) <= required_switchers)
                            {
                                buckets[i].RemoveRoads(road_id);
                                changed = true;
                            }
                        }

                        // WARNING -- unlike other rules this "should work in practice", so there is chance it will corrupt the flow
                        if (useRisky)
                        {
                            foreach (var road_id in buckets[i].Roads.ToArray())
                            {
                                // if the given road is only connector within the bucket
                                if (!buckets[i - 1].TryGetSameRoad(road_id).Any() && !buckets[i + 1].TryGetSameRoad(road_id).Any()
                                    // and for the same nodes the alternatate roads are simply pass-through, that it looks
                                    // like we don't need such road within bucket (again -- RISKY theory)
                                    && buckets[i].TryGetSameRoad(road_id).SelectMany(it => buckets[i].GetAtNode(it.index))
                                        .Where(it => it.index.RoadMapIndex != road_id)
                                        .All(it => buckets[i - 1].TryGetSameRoad(it.index.RoadMapIndex).Any() && buckets[i + 1].TryGetSameRoad(it.index.RoadMapIndex).Any()))
                                {
                                    buckets[i].RemoveRoads(road_id);
                                    changed = true;
                                }
                            }
                        }

                        foreach (var entry in buckets[i].ToArray())
                        {
                            // if the entry is not a switcher, it means we can use it as moving via given road
                            // but if there is are no entries in adjacent buckets, this mean moving would no longer be distance-zero
                            // so we need to check if we have other entries for this road, and if yes, we can remove it
                            // because using it does not have any benefit
                            if (!buckets[i].IsSwitcher(entry.RoadIdx))
                            {
                                // we don't have non-zero travel using this entry
                                bool previous_has = buckets[i - 1].TryGetEntry(entry.RoadIdx, out _);
                                bool next_has = buckets[i + 1].TryGetEntry(entry.RoadIdx, out _);

                                if (!previous_has && !next_has
                                // we have other entries for this road, so we can stil travel in/out using it
                                   && buckets[i].TryGetSameRoad(entry.RoadIdx.RoadMapIndex).Count() > 1)
                                {
                                    buckets[i].Remove(entry.RoadIdx);
                                    changed = true;
                                }
                                else
                                {
                                    int previous_side = buckets[i - 1].GetExistingRoadSide(entry.RoadIdx);
                                    int next_side = buckets[i + 1].GetExistingRoadSide(entry.RoadIdx);

                                    // this is another look at the same idea -- let's say we don't have same entry in previous
                                    // bucket, so we check if we have smaller/greater indices in current and previous bucket
                                    // if this is true, than we don't need this entry, because we move through smaller/greater entry

                                    // consider some road with indices
                                    // 6 7 8
                                    //   7 8  <--- current bucket
                                    //       9
                                    // we cannot remove "8" because we maybe move 8-8-9 and removing it would force us to move
                                    // 8-7-9, but 7 is OK to remove, because if we move 8-8-9 it makes no difference, 7-7-9
                                    // can be replaced by 7-8-9, and similarly 6-7-9, with 6-8-9
                                    if ((!previous_has && buckets[i].HasRoadSide(entry.RoadIdx, previous_side))
                                        || (!next_has && buckets[i].HasRoadSide(entry.RoadIdx, next_side)))
                                    {
                                        buckets[i].Remove(entry.RoadIdx);
                                        changed = true;
                                    }
                                }

                            }

                        }

                        // bucket with only one entry is just a filler created because we have track point there
                        if (buckets[i].Values.Count() == 1)
                        {
                            buckets[i].Clear();
                            changed = true;
                        }
                        // if we cannot switch within bucket, it means it just just pass-through, filler, we can remove it
                        if (buckets[i].All(it => !buckets[i].IsSwitcher(it.RoadIdx)))
                        {
                            buckets[i].Clear();
                            changed = true;
                        }

                    }

                    if (buckets[i].Count == 0)
                    {
                        if (i == 0 || i == buckets.Count - 1)
                            throw new Exception($"No nodes founds for start/end user points {i}/{buckets.Count}");
                        buckets.RemoveAt(i);
                    }

                }

                if (!changed)
                    break;
            }
        }

        private void easyCompressBuckets(List<RoadBucket> buckets)
        {
            while (true)
            {
                bool changed = false;

                for (int i = buckets.Count - 1; i >= 0; --i)
                {
                    /*foreach (var entry in buckets[i].ToArray())
                    {
                        for (int adj = i - 1; adj <= i + 1; adj += 2)
                        {
                            if (adj < 0 || adj >= buckets.Count)
                                continue;

                            // given road is not follow-up from the previous bucket 
                            // and we cannot switch to it by node within current bucket, so it is a dead entry
                            if (!buckets[adj].TryGetSameRoad(entry.Idx.RoadId).Any()
                                && !buckets[i].IsSwitcher(entry.Idx))
                            {
                                buckets[i].Remove(entry.Idx);
                                changed = true;
                            }
                        }
                    }*/

                    if (i > 0 && i < buckets.Count - 1)
                    {
                        for (int adj = i - 1; adj <= i + 1; adj += 2)
                        {
                            // if current bucket is subset of adjacent bucket, then merge into adjacent
                            if (buckets[i].All(it => buckets[adj].TryGetEntry(it.RoadIdx, out _)))
                            {
                                buckets[i].Clear();
                                changed = true;
                            }
                        }

                        // if we have only single road in given bucket we know it won't switch roads here, 
                        // so we can potentially split this bucket and move roads to adjacent buckets
                        // only if those roads already exists there
                        /*if (buckets[i].GetRoadSwitchesCount() == 0
                            && buckets[i].All(it => buckets[i - 1].TryGetEntry(it.Idx, out _) || buckets[i + 1].TryGetEntry(it.Idx, out _)))
                        {
                            buckets[i].Clear();
                            changed = true;
                        }*/

                        // if given road has only one switch it has to have connections to prev/next buckets
                        // or in other words if it has connection to only one bucket and has no switches 
                        // we remove it 
                        /*foreach (var road_id in buckets[i].Roads.ToArray())
                        {
                            int required_switchers = 1;
                            // for connecting to the adjacent bucket the limit on required switchers is tougher
                            if (buckets[i - 1].TryGetSameRoad(road_id).Any())
                                --required_switchers;
                            if (buckets[i + 1].TryGetSameRoad(road_id).Any())
                                --required_switchers;

                            if (buckets[i].TryGetSameRoad(road_id).Count(it => buckets[i].IsSwitcher(it.index)) <= required_switchers)
                            {
                                buckets[i].RemoveRoads(road_id);
                                changed = true;
                            }
                        }*/

                        /*foreach (var entry in buckets[i].ToArray())
                        {
                            // if the entry is not a switcher, it means we can use it as moving via given road
                            // but if there is are no entries in adjacent buckets, this mean moving would no longer be distance-zero
                            // so we need to check if we have other entries for this road, and if yes, we can remove it
                            // because using it does not have any benefit
                            if (!buckets[i].IsSwitcher(entry.Idx))
                            {
                                // we don't have non-zero travel using this entry
                                bool previous_has = buckets[i - 1].TryGetEntry(entry.Idx, out _);
                                bool next_has = buckets[i + 1].TryGetEntry(entry.Idx, out _);

                                if (!previous_has && !next_has
                                // we have other entries for this road, so we can stil travel in/out using it
                                   && buckets[i].TryGetSameRoad(entry.Idx.RoadId).Count() > 1)
                                {
                                    buckets[i].Remove(entry.Idx);
                                    changed = true;
                                }
                                else
                                {
                                    int previous_side = buckets[i - 1].GetExistingRoadSide(entry.Idx);
                                    int next_side = buckets[i + 1].GetExistingRoadSide(entry.Idx);

                                    // this is another look at the same idea -- let's say we don't have same entry in previous
                                    // bucket, so we check if we have smaller/greater indices in current and previous bucket
                                    // if this is true, than we don't need this entry, because we move through smaller/greater entry

                                    // consider some road with indices
                                    // 6 7 8
                                    //   7 8  <--- current bucket
                                    //       9
                                    // we cannot remove "8" because we maybe move 8-8-9 and removing it would force us to move
                                    // 8-7-9, but 7 is OK to remove, because if we move 8-8-9 it makes no difference, 7-7-9
                                    // can be replaced by 7-8-9, and similarly 6-7-9, with 6-8-9
                                    if ((!previous_has && buckets[i].HasRoadSide(entry.Idx, previous_side))
                                        || (!next_has && buckets[i].HasRoadSide(entry.Idx, next_side)))
                                    {
                                        buckets[i].Remove(entry.Idx);
                                        changed = true;
                                    }
                                }

                            }

                        }*/
                        /*
                        // bucket with only one entry is just a filler created because we have track point there
                        if (buckets[i].Values.Count() == 1)
                        {
                            buckets[i].Clear();
                            changed = true;
                        }
                        // if we cannot switch within bucket, it means it just just pass-through, filler, we can remove it
                        if (buckets[i].All(it => !buckets[i].IsSwitcher(it.Idx)))
                        {
                            buckets[i].Clear();
                            changed = true;
                        }
                        */
                    }

                    if (buckets[i].Count == 0)
                    {
                        if (i == 0 || i == buckets.Count - 1)
                            throw new Exception($"Start and end serves as anchors {i}/{buckets.Count}");
                        buckets.RemoveAt(i);
                    }

                }

                if (!changed)
                    break;
            }
        }

        private void writeSequence(List<TrackEval> enhanced, bool isWinning)
        {
            if (debugDirectory == null)
                return;

            TrackWriter.WriteLabeled(Helper.GetUniqueFileName(debugDirectory, $"seq-{enhanced.Last().Digest()}.kml"), null,
                enhanced.Select(it => (it.Point, $"{it.RoadIndexLong.RoadMapIndex} {it.TotalLength.Meters}_{it.TotalError.Meters}")));
        }

    }
}
