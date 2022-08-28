using MathUnit;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using TrackPlanner.Data;
using TrackPlanner.Settings;
using TrackPlanner.Shared;
using TrackRadar.Collections;
using TrackPlanner.DataExchange;
using TrackPlanner.LinqExtensions;
using TrackPlanner.Mapping;

namespace TrackPlanner.PathFinder
{
    // A* algorithm with pairing heap
    // https://en.wikipedia.org/wiki/A*_search_algorithm
    // https://brilliant.org/wiki/pairing-heap/
    // https://en.wikipedia.org/wiki/Pairing_heap
    // http://www.brouter.de/brouter/algorithm.html

    public sealed class RouteFinder
    {
        public static bool TryFindPath(ILogger logger,Navigator navigator, IWorldMap map, RoadGrid grid, SystemConfiguration sysConfig,
            UserPlannerPreferences userPlannerConfig, IReadOnlyList<RequestPoint> userPoints,
            CancellationToken cancellationToken,
            [MaybeNullWhen(false)] out List<LegRun> pathSteps)
        {
            var buckets = grid.GetRoadBuckets(userPoints, sysConfig.InitSnapProximityLimit, sysConfig.FinalSnapProximityLimit, requireAllHits: true, singleMiddleSnaps: true);
            var finder = new RouteFinder(logger,navigator, map, grid, new Shortcuts(), sysConfig, userPlannerConfig, buckets, new PathConstraints(), cancellationToken);
            return finder.tryFindPath(buckets, out pathSteps);
        }

        public static bool TryFindPath(ILogger logger,Navigator navigator, IWorldMap map, RoadGrid grid, SystemConfiguration sysConfig,
            UserPlannerPreferences userPlannerConfig, IReadOnlyList<NodePoint> userPoints,
            bool allowSmoothing,
            CancellationToken cancellationToken,
            [MaybeNullWhen(false)] out List<LegRun> pathSteps)
        {
            var buckets = new List<RoadBucket>();
            for (int i = 0; i < userPoints.Count; ++i)
            {
                var is_final = i == 0 || i == userPoints.Count - 1;

                if (userPoints[i].Point.HasValue)
                    buckets.Add(grid.GetRoadBucket(i, userPoints[i].Point!.Value, sysConfig.InitSnapProximityLimit, sysConfig.FinalSnapProximityLimit,
                        requireAllHits: true,
                        singleSnap: !is_final,
                        isFinal: is_final,
                        allowSmoothing:allowSmoothing)!);
                else
                    buckets.Add(RoadBucket.CreateBucket(i, userPoints[i].NodeId!.Value, map, grid.Calc, isFinal: is_final,allowSmoothing:allowSmoothing));
            }

            var finder = new RouteFinder(logger,navigator, map, grid, new Shortcuts(), sysConfig, userPlannerConfig, buckets, new PathConstraints(), cancellationToken);
            return finder.tryFindPath(buckets, out pathSteps);
        }

        public static bool TryFindPath(ILogger logger,Navigator navigator, IWorldMap map, RoadGrid grid,
            Shortcuts shortcuts,
            SystemConfiguration sysConfig,
            UserPlannerPreferences userPlannerConfig, IReadOnlyList<long> mapNodes,
            in PathConstraints constraints,
            bool allowSmoothing,
            CancellationToken cancellationToken,
            [MaybeNullWhen(false)] out List<LegRun> pathSteps)
        {
            List<RoadBucket> buckets = RoadBucket.GetRoadBuckets(mapNodes, map, grid.Calc,allowSmoothing:allowSmoothing);

            var finder = new RouteFinder(logger,navigator, map, grid, shortcuts, sysConfig, userPlannerConfig, buckets, constraints, cancellationToken);
            return finder.tryFindPath(buckets, out pathSteps);
        }



        private readonly ILogger logger;
        private readonly Navigator navigator;
        private readonly IWorldMap map;
        private IGeoCalculator calc => this.grid.Calc;
        private readonly SystemConfiguration sysConfig;
        private readonly UserPlannerPreferences userPlannerConfig;
        private readonly IReadOnlySet<long> suppressedHighTraffic;
        private readonly PathConstraints constraints;
        private readonly CancellationToken cancellationToken;
        private readonly RoadGrid grid;
        private readonly Shortcuts shortcuts;

        private string? debugDirectory => this.navigator.GetDebug(this.sysConfig.EnableDebugDumping);

        private readonly HashSet<long> DEBUG_lowCostNodes;
        private readonly Dictionary<long, string> DEBUG_suppressToFar;
        private readonly Dictionary<long, string> DEBUG_suppressInRange;
        private readonly HashSet<long> DEBUG_dangerousNodes;
        private readonly Dictionary<long, string> DEBUG_hotNodes;

        private readonly RouteLogic logic;

        private readonly Speed slowest;

        private readonly Speed fastest;
        //private readonly DistancePredictor predictor;

        private RouteFinder(ILogger logger, Navigator navigator, IWorldMap map, RoadGrid grid, Shortcuts shortcuts, SystemConfiguration sysConfig, UserPlannerPreferences userPlannerConfig,
            IReadOnlyList<RoadBucket> buckets,
            in PathConstraints constraints, CancellationToken cancellationToken)
        {
            this.logger = logger;
            this.navigator = navigator;
            this.map = map;
            this.sysConfig = sysConfig;
            this.userPlannerConfig = userPlannerConfig;
            userPlannerConfig.Speeds.Values.MinMax(out this.slowest, out this.fastest);
            this.constraints = constraints;
            this.cancellationToken = cancellationToken;
            this.grid = grid;
            this.shortcuts = shortcuts;

            this.DEBUG_lowCostNodes = new HashSet<long>();
            this.DEBUG_suppressToFar = new Dictionary<long, string>();
            this.DEBUG_suppressInRange = new Dictionary<long, string>();
            this.DEBUG_dangerousNodes = new HashSet<long>();
            this.DEBUG_hotNodes = new Dictionary<long, string>()
            {
                [1373734399] = "pierwszy",
                [4511042524] = "na drodze"
            };

            this.suppressedHighTraffic = suppressTrafficCost(buckets, userPlannerConfig.TrafficSuppression);

            this.logic = new RouteLogic(map, calc, userPlannerConfig, this.fastest, this.suppressedHighTraffic);
        }

        private bool tryFindPath(List<RoadBucket> buckets, [MaybeNullWhen(false)] out List<LegRun> pathLegs)
        {
            if (buckets.Count < 2)
            {
                logger.Error($"Insufficient number of anchors were given: {buckets.Count}");
                pathLegs = default;
                return false;
            }

            var fast_comparer = new WeightComparer(runningScale: 0.5, estimatedScale: 1.0, estimatedSpeed: this.fastest, useStableRoads: this.userPlannerConfig.UseStableRoads);
            var precise_comparer = new WeightComparer(runningScale: 1.0, estimatedScale: 1.0, estimatedSpeed: this.fastest, useStableRoads: this.userPlannerConfig.UseStableRoads);

            pathLegs = new List<LegRun>();
            for (int pt_index = 1; pt_index < buckets.Count; ++pt_index)
            {
                TravelCost cost_limit = TravelCost.MaxValue;
                var estimate_ratio = this.sysConfig.DefaultEstimateRatio;

                CostPath? best_path = null;

                if (this.sysConfig.DoublePass)
                {
                    var collector = new DistanceCollector();
                    using (var forwardDebugHistory = this.sysConfig.DumpProgress
                               ? new DebugFinderHistory(this.logger, this.map, "fast-fwd",
                                   this.debugDirectory)
                               : null)
                    using (var backwardDebugHistory = this.sysConfig.DumpProgress
                               ? new DebugFinderHistory(this.logger, this.map, "fast-bwd",
                                   this.debugDirectory)
                               : null)
                    {
                        if (tryFindLegPath(collector,
                                forwardDebugHistory,
                                backwardDebugHistory,
                                estimate_ratio, fast_comparer, cost_limit, buckets[pt_index - 1], buckets[pt_index],
                                modeLabel: "fast-find",
                                out CompStatistics fast_stats, out CostPath fast_path))
                        {
                            best_path = fast_path;
                            cost_limit = fast_path.RunCost;
                        }
                        else
                        {
                            pathLegs = null;
                            return false;
                        }

                    }

                    logger.Info(collector.GetSummary());

                    this.logger.Info($"Fast path cost limit {cost_limit.EquivalentInMinutes}");

                    collector.BuildPredictions(out var est_ratio);
                    this.logger.Info($"Using estimate ratio {est_ratio}");
                    if (this.sysConfig.UseEstimateRatio)
                        estimate_ratio = est_ratio;
                }

                using (var debugFinderHistory = this.sysConfig.DumpProgress
                           ? new DebugFinderHistory(this.logger, this.map, "normal-fwd", this.debugDirectory)
                           : null)
                using (var finderHistory = this.sysConfig.DumpProgress
                           ? new DebugFinderHistory(this.logger, this.map, "normal-bwd", this.debugDirectory)
                           : null)
                {
                    if (tryFindLegPath(collector: null,
                            debugFinderHistory,
                            finderHistory,
                            estimate_ratio, precise_comparer, cost_limit, buckets[pt_index - 1], buckets[pt_index],
                            "proper-find",
                            out CompStatistics cost_stats, out CostPath cost_path))
                    {
                        best_path = cost_path;
                        // 652_998
                        this.logger.Info(
                            $"Proper path cost {best_path.Value.RunCost.EquivalentInMinutes} with {cost_stats.ForwardUpdateCount + cost_stats.BackwardUpdateCount} updates, sub-success {cost_stats.SuccessExactTarget}, sub-fails {cost_stats.FailedExactTarget}");
                    }
                    else if (best_path == null)
                    {
                        pathLegs = null;
                        return false;
                    }
                }


                List<StepRun> path = best_path.Value.Steps.ToList();

                // remove user points
                if (!path[0].Place.IsUserPoint)
                    throw new Exception($"Path does not start with user point {path[0].Place}.");
                path.RemoveAt(0);
                path[0] = StepRun.RecreateAsInitial(path[0], path[1]);
                if (!path[0].Place.IsCross)
                    throw new Exception($"Path start does not follow with cross point {path[0].Place}.");
                if (!path[1].Place.IsNode)
                    throw new Exception($"Path start does not follow with node {path[1].Place}.");

                if (!path[^1].Place.IsUserPoint)
                    throw new Exception($"Path does not end with user point {path[^1].Place}.");
                path.RemoveAt(path.Count - 1);
                if (!path[^1].Place.IsCross)
                    throw new Exception($"Path end does not follow with cross point {path[^1].Place}.");
                if (!path[^2].Place.IsNode)
                    throw new Exception($"Path end does not follow with node {path[^2].Place}.");

                if (path.First().IncomingDistance != Length.Zero || path.First().IncomingTime != TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException($"Initial step should be zero, it is {path.First().IncomingDistance} in {path.First().IncomingTime}");
                pathLegs.Add(new LegRun( path));

                // todo: INCORRECT, it won't give optimal path, fix it 
                // to avoid gaps at the anchors, like this
                // X----xo x----X
                // where X -- user point and cross point at the same time, x -- crosspoint, o -- user point
                // this hack was added, we simply use single snap which was used when finding current path leg
                buckets[pt_index] = buckets[pt_index].RebuildWithSingleSnap(path[^1].Place.Point!.Value, path[^2].Place.NodeId!.Value);
            }

            // note: adjacent legs have shared place
            smoothLegs(buckets, pathLegs);

            dumpPostDebug();

            return true;
        }

        private void smoothLegs(IReadOnlyList<RoadBucket> buckets, List<LegRun> rawLegs)
        {
            int bucket_idx = -1;
            // NOTE: in case of single leg this loop won't run at all...
            foreach (var (prev, next) in rawLegs.Slide(wrapAround:true))
            {
                ++bucket_idx;
                smoothLegs(buckets[bucket_idx],prev,next);

                if (prev.Steps.Last().Place != next.Steps.First().Place)
                    throw new ArgumentException("Legs are not connected");
            }
            
            // ... so we have to have separate loop just for validation
            foreach (var raw_leg in rawLegs)
            {
                if (raw_leg.Steps.First().IncomingDistance != Length.Zero || raw_leg.Steps.First().IncomingTime != TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException($"Initial step should be zero, it is {raw_leg.Steps.First().IncomingDistance} in {raw_leg.Steps.First().IncomingTime}");

            }
        }

        private void smoothLegs(RoadBucket previousBucket, in LegRun previousLeg, in LegRun nextLeg)
        {
            if (!previousBucket.AllowSmoothing)
                return;

            // each legs is stripped from start/end user points already

            // smoothing connections between legs

            // the first and last points are crosspoint, the second to them are real nodes
            // if we share the same node skip both crosspoints and starting (!) shared nodes, to avoid diagrams like this
            //           | 
            //           |
            //  ---------o--* U
            // o node
            // * crosspoint
            // U user point
            // we don't check more shared nodes, because this removal is neglible, but in general user could go to some place and return
            // using partially of the same path

            // the first execution of this condition is when we still have crosspoints at the end/start
            while (previousLeg.Steps[^2].Place.NodeId == nextLeg.Steps[1].Place.NodeId)
            {
                previousLeg.Steps.RemoveLast();
                nextLeg.Steps.RemoveFirst();
                nextLeg.Steps[0] = StepRun.RecreateAsInitial(nextLeg.Steps[0], nextLeg.Steps[1]);

                // keep removing same nodes as long the nodes were in snap-range 
                if (previousLeg.Steps.Count < 2 || nextLeg.Steps.Count < 2
                                                // the current one has to be within snap
                                                || !previousBucket.ReachableNodes.Contains(previousLeg.Steps[^1].Place.NodeId!.Value))
                    break;
            }
        }

        private void dumpPostDebug()
        {
            if (sysConfig.EnableDebugDumping && this.sysConfig.DumpLowCost)
            {
                var input = new TrackWriterInput();
                foreach (var node in DEBUG_lowCostNodes)
                    input.AddPoint(map.Nodes[node], icon: PointIcon.DotIcon);

                string filename = Helper.GetUniqueFileName(this.navigator.GetDebug(), $"low-cost.kml");
                input.BuildDecoratedKml().Save(filename);
            }

            if (sysConfig.EnableDebugDumping && this.sysConfig.DumpTooFar)
            {
                var input = new TrackWriterInput();
                foreach (var entry in this.DEBUG_suppressToFar)
                {
                    if (!this.DEBUG_suppressInRange.ContainsKey(entry.Key))
                        input.AddPoint(map.Nodes[entry.Key], label: entry.Value, icon: PointIcon.CircleIcon);
                }

                string filename = Helper.GetUniqueFileName(this.debugDirectory!, $"too-far.kml");
                input.BuildDecoratedKml().Save(filename);

            }

            if (sysConfig.EnableDebugDumping && this.sysConfig.DumpInRange)
            {
                var input = new TrackWriterInput();
                foreach (var entry in this.DEBUG_suppressInRange)
                    input.AddPoint(map.Nodes[entry.Key], label: entry.Value, icon: PointIcon.DotIcon);

                string filename = Helper.GetUniqueFileName(this.debugDirectory!, $"in-range.kml");
                input.BuildDecoratedKml().Save(filename);

            }

            if (sysConfig.EnableDebugDumping && this.sysConfig.DumpDangerous)
            {
                var input = new TrackWriterInput();
                foreach (var entry in this.DEBUG_dangerousNodes)
                    input.AddPoint(map.Nodes[entry], label: "noname", icon: PointIcon.DotIcon);

                string filename = Helper.GetUniqueFileName(this.debugDirectory!, $"dangerous.kml");
                input.BuildDecoratedKml().Save(filename);

            }
        }

       
        private List<StepRun> reversePathDirection(IReadOnlyList<StepRun> path)
        {
            var result = new List<StepRun>(capacity: path.Count);

            Length incoming_distance = Length.Zero;
            TimeSpan incoming_time = TimeSpan.Zero;

            // the first point does not havy any incoming road so we copy data for this from the next point
            long incoming_road_map_index = path.Last().IncomingRoadMapIndex;
            RoadCondition incoming_condition = path.Last().IncomingCondition;

            for (int i = path.Count - 1; i >= 0; --i)
            {
                result.Add(new StepRun(path[i].Place, incoming_road_map_index, incoming_condition, incoming_distance, incoming_time));

                incoming_distance = path[i].IncomingDistance;
                incoming_time = path[i].IncomingTime;
                incoming_road_map_index = path[i].IncomingRoadMapIndex;
                incoming_condition = path[i].IncomingCondition;
            }

            return result;
        }

        private bool tryFindLegPath(
            DistanceCollector? collector,
            DebugFinderHistory? forwardDebugHistory,
            DebugFinderHistory? backwardDebugHistory,
            LinearCoefficients<Length> estimateRatio,
            WeightComparer weightComparer,
            TravelCost costLimit,
            RoadBucket stageStart, RoadBucket stageEnd,
            string modeLabel,
            out CompStatistics stats,
            out CostPath resultPath)
        {
            stats = new CompStatistics();
            logger.Info($"Start at legal {stageStart.Any(it => this.map.Roads[it.RoadIdx.RoadMapIndex].HasAccess)}, end at legal {stageEnd.Any(it => this.map.Roads[it.RoadIdx.RoadMapIndex].HasAccess)}");

            int rejected = 0;

            // node id -> source node id
            var forward_backtrack = new Dictionary<Placement, (BacktrackInfo info,Weight weight)>();
            var backward_backtrack = new Dictionary<Placement, (BacktrackInfo info,Weight weight)>();

            // node id -> ESTIMATE length (i.e. current length + direct distance), info
            var forward_heap = MappedPairingHeap.Create<Placement, Weight, BacktrackInfo>(keyComparer: null, weightComparer: weightComparer);
            var backward_heap = MappedPairingHeap.Create<Placement, Weight, BacktrackInfo>(keyComparer: null, weightComparer: weightComparer);

            {
                Length remaining_direct_distance = calc.GetDistance(stageStart.UserPoint, stageEnd.UserPoint);

                forward_heap.TryAddOrUpdate(Placement.UserPoint(stageStart),
                    new Weight(forbiddenDistance: Length.Zero,unstableDistance:Length.Zero, 
                        currentTravelCost: TravelCost.Zero,
                        rawRemainingDistance: remaining_direct_distance, estimateRatio),
                    new BacktrackInfo(source:Placement.Prestart(new GeoZPoint(), stageStart.IsFinal),
                            null,
                            null,
                            Length.Zero,
                            TimeSpan.Zero));

                backward_heap.TryAddOrUpdate(Placement.UserPoint(stageEnd),
                    new Weight(forbiddenDistance: Length.Zero, unstableDistance:Length.Zero,
                        currentTravelCost: TravelCost.Zero,
                        rawRemainingDistance: remaining_direct_distance, estimateRatio),
                    
                    new BacktrackInfo(source:Placement.Prestart(new GeoZPoint(), stageEnd.IsFinal),
                            null,
                            null,
                            Length.Zero,
                            TimeSpan.Zero));
            }

            stats.ForwardUpdateCount += 1;
            stats.BackwardUpdateCount += 1;
            
            bool is_forward_side = true;
            
            (Placement place,Weight weight)? joint = null;

            while (true)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                is_forward_side = !is_forward_side;

                var backtrack = is_forward_side ? forward_backtrack : backward_backtrack;
                var opposite_backtrack = is_forward_side ? backward_backtrack:forward_backtrack ;
                MappedPairingHeap<Placement, Weight, BacktrackInfo> heap = is_forward_side ? forward_heap : backward_heap;
                RoadBucket start = is_forward_side ? stageStart : stageEnd;
                RoadBucket end = is_forward_side ? stageEnd : stageStart;

                if (false && backtrack.Count % 1_000 == 0)
                {
                    this.logger.Verbose($"Routed through {backtrack.Count} places");
                }

                if (!heap.TryPop(out Placement current_place, out var current_weight, out var current_info))
                {
                    if (joint == null)
                    {
                        this.logger.Info($"Finding path in {modeLabel} failed , fwd: {stats.ForwardUpdateCount}, bwd {stats.BackwardUpdateCount}");
                        resultPath = default;
                        return false;
                    }
                    else
                    {
                        List<StepRun> forward_steps = recreatePath(collector, forward_backtrack, Placement.UserPoint(stageStart), joint.Value.place);
                        List<StepRun> backward_steps = recreatePath(collector, backward_backtrack, Placement.UserPoint(stageEnd), joint.Value.place);
                        // skip first place, because it is shared with the result
                        forward_steps.AddRange(reversePathDirection(backward_steps).Skip(1));

                        resultPath = new CostPath(forward_steps, runCost: joint.Value.weight.CurrentTravelCost);

                        this.logger.Info($"We have joint in {modeLabel}");
                        
                        return true;
                    }
                }

                /*{
                    var refreshed_cost = computePredictedTimeCost(current_info.DirectDistanceToEnd);
                    if (refreshed_cost != current_weight.PredictedRemainingTimeCost)
                    {
                        if (!heap.TryAddOrUpdate(current_place,
                                new Weight(current_weight.RunningTimeCost, refreshed_cost, current_weight.RunningForbidden), current_info))
                            throw new Exception("Unable to refresh heap");
                        continue;
                    }
                }*/

                if (is_forward_side)
                {
                    forwardDebugHistory?.Add(current_place, current_weight, current_info);
                }
                else
                {
                    backwardDebugHistory?.Add(current_place, current_weight, current_info);
                }

                backtrack.Add(current_place, (current_info, current_weight));

                if (current_place.NodeId.HasValue && map.Roads[current_info.IncomingRoadId!.Value].IsDangerous)
                {
                    this.DEBUG_dangerousNodes.Add(current_place.NodeId.Value);
                }

                {
                    if (current_place.NodeId.HasValue && DEBUG_hotNodes.TryGetValue(current_place.NodeId.Value, out string? comment))
                    {
                        logger.Info($"Coming to hot node {current_place.NodeId}/{comment} using road {current_info.IncomingRoadId}");
                    }
                }

                //if (backtrack.Count % 1_000 == 0)
                //  logger.Info($"Backtrack size {backtrack.Count}");

                // we add user point flag to be sure we have such sequence -- user point, cross point, nodes...., cross point, user point

                if (current_place.Point == end.UserPoint && current_place.IsUserPoint)
                {
                    // we add user point flag to be sure we have such sequence -- user point, cross point, nodes...., cross point, user point
                    List<StepRun> route_steps = recreatePath(collector, backtrack, Placement.UserPoint(start), current_place);

                    resultPath = new CostPath(route_steps, runCost: current_weight.CurrentTravelCost);

                    logger.Info($"BOOM, direct hit with weight {current_weight} in {modeLabel}");
                    forwardDebugHistory?.DumpLastData();
                    backwardDebugHistory?.DumpLastData();
                    stats.RejectedNodes = rejected;

                    return true;
                }
                else if (opposite_backtrack.TryGetValue(current_place, out var opposite_info))
                {
                    this.logger.Info("Joint point found");
                    var new_joined_weight = Weight.Join(opposite_info.weight, current_weight);
                    if (joint == null || weightComparer.Compare(new_joined_weight, joint.Value.weight) < 0)
                        joint = (current_place, new_joined_weight);
                }

                int adjacent_count = 0;

                foreach ((var adj_place, long incoming_road_map_index) in getAdjacent(current_place, start, end))
                {
                    ++adjacent_count;

                    if (backtrack.ContainsKey(adj_place))
                    {
                        if (current_place.NodeId.HasValue && DEBUG_hotNodes.TryGetValue(current_place.NodeId.Value, out string? comment))
                        {
                            logger.Info($"Adjacent to hot node {current_place.NodeId}/{comment} is already used by outgoing road {incoming_road_map_index}@{adj_place.NodeId}");
                        }

                        continue;
                    }

                    Length? remaining_direct_distance = null;
                    {
                        if (heap.TryGetData(adj_place, out var adj_weight, out var adj_info))
                        {
                            remaining_direct_distance = adj_weight.ScaledRemainingDistance;
                        }
                    }

                    // after initial join, we work only in join mode, meaning we accepts only updates on our side, and we
                    // need to hit the opposite already fixed places
                    if (joint != null && (remaining_direct_distance==null || !opposite_backtrack.ContainsKey(adj_place)))
                            continue;

                    if (remaining_direct_distance == null)
                    {
                        if (adj_place.NodeId != null && this.userPlannerConfig.HACK_ExactToTarget)
                        {
                            var adj_bucket = RoadBucket.GetRoadBuckets(new[] {adj_place.NodeId.Value}, map, grid.Calc, allowSmoothing:false).Single();

                            var sub_buckets = new List<RoadBucket>() {adj_bucket, end};
                            var worker = new RouteFinder(logger,this.navigator, map, grid, shortcuts, sysConfig with { DumpProgress = false}, new UserPlannerPreferences() {HACK_ExactToTarget = false}.SetUniformSpeeds(),
                                sub_buckets, constraints, cancellationToken);
                            if (worker.tryFindPath(sub_buckets, out List<LegRun>? remaining))
                            {
                                ++stats.SuccessExactTarget;
                                remaining_direct_distance = Length.FromMeters(remaining.SelectMany(x => x.Steps).Sum(it => it.IncomingDistance.Meters));
                            }
                            else
                            {
                                //throw new InvalidOperationException($"Sub-path failed from n#{adj_place.NodeId}@{this.map.Nodes[adj_place.NodeId.Value]} to {end}");
                                ++stats.FailedExactTarget;
                            }
                        }

                        if (remaining_direct_distance == null)
                            remaining_direct_distance = calc.GetDistance(adj_place.GetPoint(map), end.UserPoint);
                    }

                    var segment_info = this.logic.GetSegmentInfo(start, end, incoming_road_map_index, current_place, adj_place);

                    Length new_run_dist = current_info.RunningRouteDistance + segment_info.SegmentLength;
                    Length new_forbidden_dist = current_weight.CurrentForbiddenDistance + segment_info.ForbiddenLength;
                    Length new_unstable_dist = current_weight.UnstableDistance + segment_info.UnstableLength;

                    Weight outgoing_weight = new Weight(forbiddenDistance: new_forbidden_dist, 
                        unstableDistance: new_unstable_dist,
                        currentTravelCost: current_weight.CurrentTravelCost + segment_info.Cost,
                        rawRemainingDistance: remaining_direct_distance.Value, estimateRatio);

                    if (weightComparer.GetTotalTimeCost(outgoing_weight) >= costLimit
                        || (this.constraints.WeightLessThan.HasValue && weightComparer.GetTotalTimeCost(outgoing_weight) >= weightComparer.GetTotalTimeCost(this.constraints.WeightLessThan.Value)))
                    {
                        ++rejected;
                    }
                    else
                    {
                        var outgoing_info = new BacktrackInfo(current_place,
                                incoming_road_map_index,
                                new RoadCondition(segment_info.SpeedMode, segment_info.RiskInfo, segment_info.IsForbidden, isSnap: segment_info.IsSnap),
                                new_run_dist,
                                current_info.RunningTime + segment_info.Time);

                        bool updated = heap.TryAddOrUpdate(adj_place, outgoing_weight, outgoing_info);
                        if (is_forward_side)
                            ++stats.ForwardUpdateCount;
                        else
                            ++stats.BackwardUpdateCount;
                        
                        {
                            if (current_place.NodeId.HasValue && DEBUG_hotNodes.TryGetValue(current_place.NodeId.Value, out string? comment))
                            {
                                logger.Info($"Adjacent to hot node {current_place.NodeId}/{comment} is by outgoing road {incoming_road_map_index}@{adj_place.NodeId}, {(segment_info.IsForbidden ? "forbidden" : "")}, weight {outgoing_weight}, updated {updated}");
                            }
                        }
                    }
                }

                stats.AddNode(degree: adjacent_count);
            }
        }



        /*        private TimeCost computePredictedTimeCost(Length directDistance)
        {
            return new TimeCost(directDistance / this.userConfig.Fastest, costFactor: 1.0);
        }
*/
        private List<StepRun> recreatePath(DistanceCollector? collector, IReadOnlyDictionary<Placement, (BacktrackInfo info,Weight weight)> backtrack, 
            Placement startPlace, Placement endPlace)
        {
            if (endPlace == startPlace)
                throw new ArgumentException("Same place");

            var result_path = new List<StepRun>();

            Placement current_place = endPlace;
            var current_info = backtrack[current_place].info;

            long last_incoming_road_id = current_info.IncomingRoadId!.Value;
            RoadCondition last_incoming_road_condition = current_info.IncomingCondition!.Value;
            Length total_routing_distance = current_info.RunningRouteDistance;

            while (true)
            {
                var source_place = current_info.Source;
                BacktrackInfo source_info;

                if (source_place.IsPrestart)
                {
                    source_info = new BacktrackInfo(source_place, null, null, Length.Zero, TimeSpan.Zero);
                }
                else
                {
                    source_info = backtrack[source_place].info;
                }

                if (collector != null)
                {
                    Length remaining_distance = calc.GetDistance(current_place.GetPoint(map),endPlace.GetPoint(map));
                    this.logger.Verbose($" direct {remaining_distance} route {current_info.RunningRouteDistance}");
                    collector.Collect(directDistance: remaining_distance, routeDistance: total_routing_distance-current_info.RunningRouteDistance);
                }

                //if (!current_place.IsUserPoint) // do not add user start/end point
                {
                var step_distance = current_info.RunningRouteDistance - source_info.RunningRouteDistance;
                var step_time = current_info.RunningTime - source_info.RunningTime;
                result_path.Add(new StepRun(current_place,
                    current_info.IncomingRoadId ?? last_incoming_road_id, // null is only for segment from pre-start to starting point,
                    current_info.IncomingCondition ?? last_incoming_road_condition,
                    step_distance,
                    step_time));
                }

                if (current_place == startPlace)
                {
                    // remove user points, so the route is always bound to real roads
                    // NOTE: the starting/ending point is not added at all, so we don't have to remove it

                    result_path.Reverse();
                    var init_step_distance = result_path.First().IncomingDistance;
                    var init_step_time = result_path.First().IncomingTime;
                    if (init_step_distance != Length.Zero || init_step_time != TimeSpan.Zero)
                        throw new ArgumentOutOfRangeException($"Initial step {TrackPlanner.Data.DataFormat.Format(init_step_distance,true)} in {TrackPlanner.Data.DataFormat.Format(init_step_time)}, expected zero.");

                    return result_path;
                }

                last_incoming_road_id = current_info.IncomingRoadId!.Value;
                last_incoming_road_condition = current_info.IncomingCondition!.Value;

                current_place = source_place;
                current_info = source_info;
            }
        }

        private void addAdjacentToPoint(Dictionary<Placement, long> adjacent, Placement place, RoadBucket bucket)
        {
            if (place.IsUserPoint && place.Point == bucket.UserPoint)
            {
                foreach (var snap in bucket)
                    adjacent.TryAdd(Placement.Crosspoint(snap.TrackCrosspoint,snap.RoadIdx.RoadMapIndex, place.IsFinal), snap.RoadIdx.RoadMapIndex);
            }

            foreach (var snap in bucket)
                if (place.IsCross && place.Point == snap.TrackCrosspoint)
                {
                    var snap_node = this.map.GetNode(snap.RoadIdx);
                    if (this.constraints.IsAcceptable(snap_node))
                    adjacent.TryAdd(new Placement(snap_node, place.IsFinal, isSnapped: true), snap.RoadIdx.RoadMapIndex);
                    adjacent.TryAdd(Placement.UserPoint(bucket), snap.RoadIdx.RoadMapIndex);
                }
        }

        private IEnumerable<(Placement point, long roadId)> getAdjacent(Placement current, RoadBucket bucketA, RoadBucket bucketB)
        {
            // point -> incoming road id
            var adjacent = new Dictionary<Placement, long>();
            if (current.NodeId.HasValue)
            {
                addAdjacentCrosspointsToNode(adjacent, current, bucketA);
                addAdjacentCrosspointsToNode(adjacent, current, bucketB);

                bool has_adjacent_crosspoints = adjacent.Count > 0;
                
                foreach (var adj_road_idx in map.GetAdjacentRoads(current.NodeId.Value))
                {
                    var adj_node_id = this.map.GetNode(adj_road_idx);
                    if (!this.constraints.IsAcceptable(adj_node_id))
                        continue;

                    bool is_in_a = bucketA.Contains(adj_road_idx);
                    bool is_in_b = bucketB.Contains(adj_road_idx);
                    bool is_final = (bucketA.IsFinal && is_in_a) || (bucketB.IsFinal && is_in_b);
                    var is_snapped = is_in_a || is_in_b;
                    /*if (!has_adjacent_crosspoints && !is_snapped)
                    {
                        // todo: jak tylko bedziemy mieli pamiec, po prostu zwracac tu ireadonlylist i sprawdz count, chodzi o to, ze musimy miec wylacznie jedna droge
                        if (!this.map.GetRoads(this.map.GetNode(adj_road_idx)).Skip(1).Any())
                        {
                            var current_road_indices = this.map.GetRoads(current.NodeId.Value).Where(it => it.RoadMapIndex == adj_road_idx.RoadMapIndex).ToArray();
                            if (current_road_indices.Length == 1)
                            {
                                
                            }
                        }
                    }*/
                    adjacent.TryAdd(new Placement(adj_node_id, is_final, isSnapped: is_snapped), adj_road_idx.RoadMapIndex);
                }
            }
            else
            {
                addAdjacentToPoint(adjacent, current, bucketA);
                addAdjacentToPoint(adjacent, current, bucketB);
                
            }

            return adjacent.Select(it => (it.Key, it.Value));

        }


        private void addAdjacentCrosspointsToNode(Dictionary<Placement, long> adjacent, Placement place, RoadBucket bucket)
        {
            foreach (var snap in bucket)
            {
                if (place.NodeId == map.GetNode(snap.RoadIdx))
                {
                    adjacent.TryAdd(Placement.Crosspoint(snap.TrackCrosspoint, snap.RoadIdx.RoadMapIndex,place.IsFinal), snap.RoadIdx.RoadMapIndex);
                }
            }
        }

        private GeoZPoint getPoint(Placement place)
        {
            GeoZPoint a;
            if (place.NodeId.HasValue)
                a = map.Nodes[place.NodeId.Value];
            else
                a = place.Point!.Value;
            return a;
        }

        private IReadOnlySet<long> suppressTrafficCost(IEnumerable<RoadBucket> buckets, Length suppressionRange)
        {
            var suppressed = new HashSet<long>();

            if (suppressionRange != Length.Zero)
            {
                // assume that for all mid-points user selected, she/he is aware that she/he places point on a high-traffic road
                // in such case assume that for given distance user is OK to ride on such rode, so do not add any "penalties"
                // while searching for a route
                foreach (var bucket in buckets.Skip(1).SkipLast(1))
                {
                    foreach (RoadSnapInfo snap in bucket)
                    {
                        var road = map.Roads[snap.RoadIdx.RoadMapIndex];
                        if (road.IsDangerous || road.IsUncomfortable)
                        {
                            suppressed.AddRange(suppressTrafficCost(map.GetNode(snap.RoadIdx), suppressionRange));
                        }
                    }
                }
            }

            return suppressed;
        }

        private IEnumerable<long> suppressTrafficCost(long startNodeId, Length suppressionRange)
        {
            var suppressed = new HashSet<long>();
            // node id -> distance -> incoming road
            var heap = MappedPairingHeap.Create<long, Length, long>();
            
            heap.TryAddOrUpdate(startNodeId, Length.Zero, default);

            while (heap.TryPop(out long curr_node_id, out Length curr_dist, out long incoming_road_id))
            {
                // please note this collection is not "polluted" with data coming from other mid-points selected by user
                suppressed.Add(curr_node_id);
                this.DEBUG_suppressInRange.TryAdd(curr_node_id,$"#{incoming_road_id} {curr_dist}");

                GeoZPoint current_point = map.Nodes[curr_node_id];

                foreach (var adj_idx in map.GetAdjacentRoads(curr_node_id))
                {
                    var adj_node_id = map.GetNode(adj_idx);

                    if (suppressed.Contains(adj_node_id))
                        continue;

                    if (map.Roads[adj_idx.RoadMapIndex].IsDangerous || map.Roads[adj_idx.RoadMapIndex].IsUncomfortable)
                    {
                        Length adj_total_dist = curr_dist + calc.GetDistance(current_point, map.Nodes[adj_node_id]);
                        if (adj_total_dist <= suppressionRange)
                        {
                            heap.TryAddOrUpdate(adj_node_id, adj_total_dist, adj_idx.RoadMapIndex);
                        }
                        else
                            DEBUG_suppressToFar.TryAdd(adj_node_id, $"#{adj_idx.RoadMapIndex} {adj_total_dist}");
                    }
                }

            }
            return suppressed;
        }
    }
}
