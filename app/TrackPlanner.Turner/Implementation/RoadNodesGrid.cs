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
                var iter = roadBucket.RoadSnaps.GetEnumerator();
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


        private void writeSequence(List<TrackEval> enhanced, bool isWinning)
        {
            if (debugDirectory == null)
                return;

            TrackWriter.WriteLabeled(Helper.GetUniqueFileName(debugDirectory, $"seq-{enhanced.Last().Digest()}.kml"), null,
                enhanced.Select(it => (it.Point, $"{it.RoadIndexLong.RoadMapIndex} {it.TotalLength.Meters}_{it.TotalError.Meters}")));
        }

    }
}
