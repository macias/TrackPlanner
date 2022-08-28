using MathUnit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using TrackRadar.Collections;
using TrackPlanner.Mapping;
using TrackPlanner.LinqExtensions;
using TrackPlanner.DataExchange;
using TrackPlanner.PathFinder;
using TrackPlanner.Shared;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Turner.Implementation

{
    internal sealed class SolutionGraphFinder
    {
        private readonly ILogger logger;
        private readonly IWorldMap map;
        private readonly IGeoCalculator calc;
        private readonly string? debugDirectory;

        public SolutionGraphFinder(ILogger logger, IWorldMap map, IGeoCalculator calc, string? debugDirectory)
        {
            this.logger = logger;
            this.map = map;
            this.calc = calc;
            this.debugDirectory = debugDirectory;
        }

        /*
          the path weight comes from two sources -- the weight of the bubble itself, and the weight of the segment which is travelled
          
          the weight of root and final bubbles are zero
          
          the weight from the source to target is the sum of weights including source but EXCLUDING target (this works fine for entire path because root/final
            are zero so they can be excluded). This works fine for addiition, because when we move forward our target becomes source, we can now add source safely
            because before target was not included
        */

        public IEnumerable<long> GetSolution(in GeoZPoint userStartPoint, in GeoZPoint userEndPoint,
            List<RoadBucket> buckets,
             out GeoZPoint mapStartPoint, out GeoZPoint mapEndPoint)
        {
            HashSet<GraphBubble> DEBUG_remaining = buildGraph(userStartPoint, userEndPoint, buckets, out GraphBubble root_bubble, out GraphBubble final_bubble);
            if (debugDirectory!=null)
            {
                System.IO.File.WriteAllLines(Helper.GetUniqueFileName(debugDirectory, $"graph.txt"), 
                    DEBUG_remaining.Select(it => $"ID:{it.DEBUG_ID} = "+String.Join(", ",it.RoadIndices.Select(it => it.RoadMapIndex))+Environment.NewLine));
            }

            var DEBUG_travelled = new HashSet<GraphBubble>();

            var bubble_debug_targets = new Dictionary<GraphBubble, List<(GraphFutureSegment segment, GraphPathWeight targetWeight, GraphPathWeight futureWeight)>>();

            var simplified_heap_history = new Dictionary<GraphBubble, (bool hot, int order, GraphFutureSegment segment)>();
            // bubble -> insertion index
            var used = new GraphSegmentUsed();
            // target -> source
            var backtrace = new Dictionary<GraphFutureSegment, (GraphPathWeight currentWeight, GraphPathWeight futureWeight, List<GraphBubble> source, long? incomingRoadId)>(new GraphFutureSegmentComparer());
            // key: minimum future weight (not current one!)
            var bubbles_heap = MappedPairingHeap.Create<GraphFutureSegment,GraphPathWeight,ValueTuple >(new GraphFutureSegmentComparer());// new GraphBubbleWeightedByBubbleComparer());
            {
                var current_root_weight = new GraphPathWeight();
                foreach ((GraphPathWeight next_weight, GraphFutureSegment next_segment) in computeFutureWeights(root_bubble, final_bubble, source: null, null, current: root_bubble))
                {
                    var future_root_weight = current_root_weight.Add(next_weight);
                    if (bubbles_heap.TryAddOrUpdate(next_segment,future_root_weight,new ValueTuple() ))
                    {
                        List<GraphBubble> sources = new List<GraphBubble>();
                        next_segment.DEBUG_SetSources(sources);
                        backtrace[next_segment] = (current_root_weight, future_root_weight, sources, null);
                    }
                }

            }

            var winner = new List<GraphBubble>();

            using (var debug_heap_writer = createTextWriter("heap-trace.txt"))
            {
                while (true)
                {
                    //logger.Verbose($"Heap size {bubbles_heap.Count} / {used.Count} ");

                    if (!bubbles_heap.TryPop(out GraphFutureSegment top_segment,out GraphPathWeight top_future_weight, out _))
                    {
                        if (this.debugDirectory != null)
                        {
                            TrackWriter.WriteLabeled(Helper.GetUniqueFileName(debugDirectory, "nodes-cut.kml"), null,
                                DEBUG_travelled.Select(it => (it.Point, $"USED ID:{it.DEBUG_ID}")).Concat(DEBUG_remaining.Select(it => (it.Point, $"REM ID:{it.DEBUG_ID}"))));
                        }
                        logger.Flush();
                        throw new NotSupportedException("Something wrong, heap is empty");
                    }

                    top_segment.DEBUG_ValidateSourcesAlongPath(backtrace);

                    (GraphBubble current, GraphBubble target) = top_segment;

                    debug_heap_writer?.WriteLine($"ID:{current.DEBUG_ID} [{used.Count}]" + Environment.NewLine
                        + $"    CURR {backtrace[top_segment].currentWeight}" + Environment.NewLine
                        + $"    NEXT {top_future_weight}");
                    used.Add(top_segment);

                    DEBUG_travelled.Add(current);
                    DEBUG_remaining.Remove(current);

                    if (current == final_bubble)
                    {
                        winner = backtrace[top_segment].source.ToList();
                        if (winner.First() != root_bubble || winner.Last() == final_bubble)
                            throw new Exception();
                        winner.Add(final_bubble);
                        //winner.Reverse();

                        if (debugDirectory != null)
                        {
                            TrackWriter.WriteLabeled(Helper.GetUniqueFileName(debugDirectory, "winner-raw.kml"), null,
                                winner.ZipIndex().Select(it => (it.item.Point, $"[{it.index}] ID:{it.item.DEBUG_ID} rd {getRoadIdTo(winner, it.index)}")));
                        }

                        GraphBubble curr_bubble = winner.First();
                        foreach (GraphBubble next in winner.Skip(1))
                        {
                            var curr_segment = new GraphFutureSegment(curr_bubble, next);
                            int curr_order = used[curr_segment];
                            simplified_heap_history.Add(curr_bubble, (hot: true, curr_order, curr_segment));
                            curr_bubble = next;
                        }

                        foreach (var group in used.Segments.Where(it => !simplified_heap_history.ContainsKey(it.Key.Current))
                            .GroupBy(it => it.Key.Current))
                        {
                            var with_min_weight = group.OrderBy(it => backtrace[it.Key].currentWeight.TotalWeight).First();
                            simplified_heap_history.Add(group.Key, (hot: false, with_min_weight.Value, with_min_weight.Key));
                        }

                        break;
                    }

                    (GraphPathWeight current_weight, _, List<GraphBubble> source, long? incoming_road) = backtrace[top_segment];

                    (_, long? current_road_id, Length dist) = current.Targets.Single(it => it.bubble == target);
                    {
                        var target_weight = current_weight.Add(computeWeight(root_bubble, final_bubble, source.LastOrDefault(), incoming_road, current: current, current_road_id, target, dist));
                        IEnumerable<(GraphPathWeight weight, GraphFutureSegment segment)> futures = target == final_bubble
                            ? new[] { (new GraphPathWeight(), new GraphFutureSegment(final_bubble, final_bubble)) }
                            : computeFutureWeights(root_bubble, final_bubble, source: current, current_road_id, current: target);
                        foreach ((GraphPathWeight next_weight, GraphFutureSegment next_segment) in futures)
                        {
                            //if (source.Contains(next_segment.Target) || source.Contains(next_segment.Current)) // disallow  loops, we can compute them of course, but they will disrupt backtraces
                            //  continue;

                            var future_weight = target_weight.Add(next_weight);

                            if (debugDirectory != null)
                            {
                                //logger.Verbose($"New target from ID:{current.DEBUG_ID} to ID:{target.DEBUG_ID} -> FUTURE {future_weight}");
                                bubble_debug_targets.TryAdd(current, new List<(GraphFutureSegment, GraphPathWeight targetWeight, GraphPathWeight futureWeight)>());
                                bubble_debug_targets[current].Add((next_segment, target_weight, future_weight));
                            }

                            if (used.ContainsKey(next_segment))
                                continue;

                            var src_extension = source.ToList();
                            src_extension.Add(current);

                            if (bubbles_heap.TryAddOrUpdate(next_segment,future_weight, new ValueTuple()))
                            {
                                next_segment.DEBUG_SetSources(src_extension);
                                backtrace[next_segment] = (target_weight, future_weight, src_extension, current_road_id);
                            }
                        }
                    }
                }
            }

            if (debugDirectory != null)
            {
                int? get_order(GraphBubble? bubble) => bubble == null ? null : simplified_heap_history[bubble].order;

                TrackWriter.WriteLabeled(Helper.GetUniqueFileName(debugDirectory, "heap-trace.kml"), null,
                    simplified_heap_history.OrderBy(it => it.Value.order)
                    .Select(it => (it.Key.Point, $"{it.Key.DEBUG_ID} {(it.Value.hot ? "!" : "")} [{it.Value.order}]{it.Key.KindLabel} {(backtrace[it.Value.segment].currentWeight.TotalWeight.ToString("0.#"))}/{(backtrace[it.Value.segment].futureWeight.TotalWeight.ToString("0.#"))} from {get_order(backtrace[it.Value.segment].source.LastOrDefault())}")));

                static IEnumerable<string> create_target_info(KeyValuePair<GraphBubble, List<(GraphFutureSegment segment, GraphPathWeight targetWeight, GraphPathWeight futureWeight)>> targets)
                {
                    yield return $"Roads {(String.Join(",", targets.Key.RoadIndices.Select(it => it.RoadMapIndex)))}";
                    yield return Environment.NewLine;

                    foreach (var it in targets.Value)
                    {
                        yield return $"ID:{it.segment.Current.DEBUG_ID}->{it.segment.Target.DEBUG_ID}" + Environment.NewLine + $"    TARGET {it.targetWeight}" + Environment.NewLine + $"    FUTURE {it.futureWeight}";
                    }
                }

                if (bubble_debug_targets.Any())
                {
                    foreach (var entry in bubble_debug_targets)
                    {
                        TrackWriter.WriteLabeled(Helper.GetUniqueFileName(debugDirectory, $"bubble_{entry.Key.DEBUG_ID}-targets.kml"), null, entry.Value.Select(it => (it.segment.Current.Point, $"ID:{it.segment.Current.DEBUG_ID}")),
                            PointIcon.StarIcon, new SharpKml.Base.Color32(0xff, 0x18, 0xc2, 0x5b));
                        System.IO.File.WriteAllLines(Helper.GetUniqueFileName(debugDirectory, $"bubble_{entry.Key.DEBUG_ID}-targets.txt"), create_target_info(entry));
                    }
                }
            }


            // removing user points
            winner.RemoveAt(0);
            winner.RemoveAt(winner.Count - 1);

            mapStartPoint = winner.First().Point;
            mapEndPoint = winner.Last().Point;

            // removing cross points
            winner.RemoveAt(0);
            winner.RemoveAt(winner.Count - 1);

            var result = winner.Select(it => map.GetNode(it.RoadIndices.First())).ToList();

            /*            for (int i=0;i<result.Count;++i)
                        {
                            logger.Verbose($"win {winner[i].debugBucketIndex}: {(String.Join(", ",result[i].Select(idx => $"{idx} [{map.GetNode(idx)}]")))}");
                        }
            */
            return result;

        }

        private long? getRoadIdTo(List<GraphBubble> winner, int index)
        {
            if (index == 0)
                return null;
            else
                return winner[index - 1].Targets.Single(it => it.bubble == winner[index]).roadId;
        }

        private IEnumerable<(GraphPathWeight weight, GraphFutureSegment segment)> computeFutureWeights(GraphBubble rootBubble, GraphBubble finalBubble,
            GraphBubble? source, long? incomingRoadId, GraphBubble current)
        {
            foreach ((GraphBubble target, long? outgoing_road_id, Length dist) in current.Targets)
            {
                var weight = computeWeight(rootBubble, finalBubble, source, incomingRoadId, current, outgoing_road_id, target, dist);
                yield return (weight, new GraphFutureSegment(current, target));
            }
        }


        private GraphPathWeight computeWeight(GraphBubble rootBubble, GraphBubble finalBubble,
                GraphBubble? source, long? incomingRoadId, GraphBubble current, long? outgoingRoadId, GraphBubble target,
                Length distanceToTarget)
        {
            // computes the weight of the current bubble + outgoing segment (but excluding the weight of the target bubble)

            Angle angle = incomingRoadId == null || outgoingRoadId == null ? Angle.Zero : calc.AngleDistance(center: current.Point, source!.Point, target.Point);

            bool road_switch = /*top*/incomingRoadId == null || outgoingRoadId == null
                ? false : !map.IsRoadContinuation(/*top*/incomingRoadId.Value, outgoingRoadId.Value);
            int road_diff_levels = 0;
            if (road_switch)
                road_diff_levels = new RoadRank(map.Roads[incomingRoadId!.Value]).DifferenceLevel(new RoadRank(map.Roads[outgoingRoadId!.Value]));
            bool is_outgoing_motor_road = //top.Target.Point.Convert() == target.Point.Convert()
                outgoingRoadId == null ? false : map.IsMotorRoad(outgoingRoadId.Value);
            //logger.Verbose($"Adding to heap target {target}");

            // if current road id is null (meaning in-place "movement") use the road used previously
            int cycle_crossings = 0;
            if (outgoingRoadId != null && !is_outgoing_motor_road
                && current.BubbleKind == GraphBubble.Kind.Internal && target.BubbleKind == GraphBubble.Kind.Internal)
            {
                // here we count target as well
                foreach (var idx in getRoadIndices(current.GetRoadIndices(outgoingRoadId.Value), target.GetRoadIndices(outgoingRoadId.Value)).Distinct())
                {
                    if (isMotorCycleCrossing(idx))
                    {
                        // count all the crossings along the current-target segment
                        ++cycle_crossings;
                    }
                }

                // and here if we detect this is continuation of the cycleway we decrement the cyclecrossing for current node, because it was already counted for before
                if (incomingRoadId != null && !map.IsMotorRoad(incomingRoadId.Value)
                    && source!.BubbleKind == GraphBubble.Kind.Internal)
                {
                    foreach (var idx in current.GetRoadIndices(incomingRoadId.Value).Distinct())
                    {
                        if (isMotorCycleCrossing(idx))
                        {
                            // count all the crossings at the current node
                            --cycle_crossings;
                        }
                    }
                }
            }


            var bubble_weight = new GraphPathWeight(isAnchor: current.BubbleKind == GraphBubble.Kind.Crosspoint,// source == rootBubble || target == finalBubble,
                                        snapDistance: current.TrackSnapDistance, travelDistance: distanceToTarget, angle,
                                                                isMotorRoad: is_outgoing_motor_road, isRoadSwitch: road_switch, roadDiffLevels: road_diff_levels, cycleCrossings: cycle_crossings, $" {incomingRoadId} -> {outgoingRoadId}");

            return bubble_weight;
        }

        private long getNode(GraphBubble bubble)
        {
            if (bubble.RoadIndices.Any())
                return map.GetNode(bubble.RoadIndices.First());
            else
                return -1;
        }

        private StreamWriter? createTextWriter(string filename)
        {
            if (debugDirectory == null)
                return null;

            return new StreamWriter(Helper.GetUniqueFileName(debugDirectory, filename));
        }

        private bool isMotorCycleCrossing(RoadIndexLong idx)
        {
            IEnumerable<RoadIndexLong> roads = map.GetRoads(map.GetNode(idx));
            return roads.Any(it => map.IsSignificantMotorRoad(it.RoadMapIndex))  // do not count path-cycleways as crossings
                && roads.Any(it => map.IsCycleWay(it.RoadMapIndex));
        }

        private IEnumerable<RoadIndexLong> getRoadIndices(IEnumerable<RoadIndexLong> starts, IEnumerable<RoadIndexLong> ends)
        {
            // todo: improve this, i.e. handle roads knots better
            foreach (var start in starts)
                foreach (var end in ends)
                    foreach (var idx in map.GetRoadIndices(start, end))
                        yield return idx;

        }

        private HashSet<GraphBubble> buildGraph(in GeoZPoint userStartPoint, in GeoZPoint userEndPoint, IReadOnlyList<RoadBucket> buckets, out GraphBubble root, out GraphBubble final)
        {
            var result = new HashSet<GraphBubble>();
            // start and end-point have to be on some road, so they are crosspoints at the worse case, but the actual user points

            root = new GraphBubble(0, userStartPoint, trackSnapDistance: Length.Zero, GraphBubble.Kind.Final, "root");
            final = new GraphBubble(buckets.Count - 1, userEndPoint, trackSnapDistance: Length.Zero, GraphBubble.Kind.Final, "final");

            result.Add(root);
            result.Add(final);

            int debug_total_count = 2;

            var current_bubbles = new Dictionary<long, GraphBubble>();
            foreach (RoadSnapInfo entry in buckets.First())
            {
                // add cross point to root...
                var cross_point_bubble = new GraphBubble(0, entry.TrackCrosspoint, trackSnapDistance: entry.TrackSnapDistance, GraphBubble.Kind.Crosspoint, "start-cx");
                result.Add(cross_point_bubble);
                cross_point_bubble.AddRoad(entry.RoadIdx.RoadMapIndex);
                root.AddTarget(cross_point_bubble, entry.RoadIdx.RoadMapIndex, onRoadTravelDistance: Length.Zero);// calc.GetDistance(root.Point, entry.TrackCrosspoint));
                ++debug_total_count;

                // ... then add actual map node next to cross point
                GeoZPoint entry_point = map.GetPoint(entry.RoadIdx);
                long entry_node = map.GetNode(entry.RoadIdx);

                if (!current_bubbles.TryGetValue(entry_node, out GraphBubble? adjacent))
                {
                    adjacent = new GraphBubble(0, entry_point, entry.TrackSnapDistance, GraphBubble.Kind.Internal);
                    result.Add(adjacent);
                    current_bubbles.Add(entry_node, adjacent);
                }
                adjacent.AddRoadIndex(entry.RoadIdx);
                cross_point_bubble.AddTarget(adjacent, entry.RoadIdx.RoadMapIndex, calc.GetDistance(entry_point, cross_point_bubble.Point));
            }

            for (int i = 0; i < buckets.Count; ++i)
            {
                addBubbleInnerConnections(i, buckets[i], current_bubbles);
                result.AddRange(current_bubbles.Values);

                //logger.Verbose($"Layer {i}: graph bubbles {current_bubbles.Count}");
                debug_total_count += current_bubbles.Count;

                if (i == buckets.Count - 1)
                    break;

                current_bubbles = connectToNextBucket2(i + 1, buckets[i + 1], current_bubbles);
            }

            foreach (RoadSnapInfo entry in buckets.Last())
            {
                GraphBubble current = current_bubbles[map.GetNode(entry.RoadIdx)];

                var cross_point_bubble = new GraphBubble(buckets.Count - 1, entry.TrackCrosspoint, trackSnapDistance: entry.TrackSnapDistance, GraphBubble.Kind.Crosspoint, "end-cx");
                result.Add(cross_point_bubble);
                cross_point_bubble.AddRoad(entry.RoadIdx.RoadMapIndex);
                current.AddTarget(cross_point_bubble, entry.RoadIdx.RoadMapIndex, calc.GetDistance(current.Point, entry.TrackCrosspoint));
                ++debug_total_count;

                cross_point_bubble.AddTarget(final, entry.RoadIdx.RoadMapIndex, Length.Zero);// entry.TrackSnapDistance);
            }

            //logger.Verbose($"Graph total count {debug_total_count}");
            return result;
        }

        private Dictionary<long, GraphBubble> connectToNextBucket2(int debugBucketIndex, RoadBucket nextBucket, Dictionary<long, GraphBubble> currentBubbles)
        {
            var next_bubbles = new Dictionary<long, GraphBubble>();

            foreach (GraphBubble current in currentBubbles.Values)
            {
                foreach (RoadIndexLong curr_idx in current.RoadIndices.ToArray())
                {
                    long curr_node = map.GetNode(curr_idx);

                    //foreach ((RoadIndex adj, _) in buckets[i].GetRoadNeighbourhood(curr_entry.Idx).Where(it => it.index != curr_entry.Idx))
                    //  current.AddAdjacent(current_bubbles[adj], map.GetRoadDistance(calc, curr_entry.Idx, adj));

                    foreach ((RoadIndexLong dest, Length snap_dist) in nextBucket.GetRoadNeighbourhood(curr_idx).Concat(nextBucket.GetAtNode(curr_idx)))
                    {
                        long dest_node = map.GetNode(dest);
                        GraphBubble? dest_bubble;
                        if (currentBubbles.TryGetValue(dest_node, out dest_bubble))
                        {
                            next_bubbles.TryAdd(dest_node, dest_bubble);
                        }
                        else
                        {
                            if (!next_bubbles.TryGetValue(dest_node, out dest_bubble))
                            {
                                dest_bubble = new GraphBubble(debugBucketIndex, map.GetPoint(dest), snap_dist, GraphBubble.Kind.Internal);
                                next_bubbles.Add(dest_node, dest_bubble);
                            }

                            current.AddTarget(dest_bubble, dest_node == curr_node ? null : dest.RoadMapIndex, map.GetRoadDistance(calc, curr_idx, dest));
                        }

                        dest_bubble.AddRoadIndex(dest);


                        /*if (current_bubbles.TryGetValue(adj, out GraphBubble? curr_next))
                        {
                            next_bubbles.Add(adj, curr_next);
                        }
                        else
                        {
                            Length distance = map.GetRoadDistance(calc, curr_entry.Idx, adj);
                            if (next_bubbles.TryGetValue(adj, out GraphBubble? next_next))
                            {
                                current.AddAdjacent(next_next, distance);
                            }
                            else
                            {
                                next_bubbles.Add(adj, current.AddAdjacent(adj, snap_dist, distance));
                            }
                        }*/
                    }
                }
            }

            return next_bubbles;
        }

        private void addBubbleInnerConnections(int debugBucketIndex, RoadBucket currentBucket, Dictionary<long, GraphBubble> currentBubbles)
        {
            foreach (RoadSnapInfo road_info in currentBucket)
            {
                long node = map.GetNode(road_info.RoadIdx);
                if (!currentBubbles.TryGetValue(node, out GraphBubble? bubble))
                {
                    bubble = new GraphBubble(debugBucketIndex, map.Nodes[node], road_info.TrackSnapDistance, GraphBubble.Kind.Internal);
                    currentBubbles.Add(node, bubble);
                }

                bubble.AddRoadIndex(road_info.RoadIdx);
            }

            foreach (RoadSnapInfo road_info in currentBucket)
            {
                long node = map.GetNode(road_info.RoadIdx);
                GraphBubble current = currentBubbles[node];

                // we would like to avoid double computation (and numeric instability with road length) so we take only right neighbours, because left will take us
                foreach (RoadIndexLong dst_idx in currentBucket.GetRoadNeighbourhood(road_info.RoadIdx).Select(it => it.index)
                    //.Where(it => it!=road_info.Idx))
                    .Where(it => it.IndexAlongRoad > road_info.RoadIdx.IndexAlongRoad))
                {
                    GraphBubble target = currentBubbles[map.GetNode(dst_idx)];

                    Length distance = map.GetRoadDistance(calc, road_info.RoadIdx, dst_idx);
                    current.AddTarget(target, dst_idx.RoadMapIndex, distance);
                    target.AddTarget(current, dst_idx.RoadMapIndex, distance);
                }

            }
        }
    }
}