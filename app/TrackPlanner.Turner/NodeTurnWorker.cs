using TrackPlanner.Data.Stored;
using MathUnit;
using System;
using System.Collections.Generic;
using System.Linq;

using TrackPlanner.Turner.Implementation;
using TrackPlanner.Mapping;
using TrackPlanner.LinqExtensions;
using TrackPlanner.DataExchange;
using TrackPlanner.PathFinder;
using TrackPlanner.Shared;
using TrackPlanner.Data;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Turner

{
    public sealed class NodeTurnWorker
    {
        private readonly ILogger logger;
        private readonly SystemTurnerConfig sysConfig;
        private readonly IWorldMap map;
        private readonly UserTurnerPreferences userPreferences;
        private readonly ApproximateCalculator calc;
        private readonly List<(GeoZPoint, string)> DEBUG_points;

        public NodeTurnWorker(ILogger logger, IWorldMap map,
            SystemTurnerConfig sysConfig, UserTurnerPreferences userPreferences)
        {
            this.logger = logger;
            this.map = map;
            this.sysConfig = sysConfig;
            this.userPreferences = userPreferences;
            this.calc = new ApproximateCalculator();
            this.DEBUG_points = new List<(GeoZPoint, string)>();
        }

        private string stringify(in RoadInfo info)
        {
            return $"{info.Kind}:{info.Identifier}";
        }

        private static readonly IReadOnlySet<int> emptyIntSet = new HashSet<int>();

        public List<TurnInfo> ComputeTurnPoints(IEnumerable<Placement> trackPlaces,ref string? problem)
        {
            List<TrackNode> track = trackPlaces
                .Where(it => it.IsNode)
                .ConsecutiveDistinct()
                .Select(it => TrackNode.Create(this.map,it.NodeId)).ToList();

            if (false && this.sysConfig.DebugDirectory != null)
            {
                TrackWriter.Build(null, track.Select(it => it.Point)).Save(Helper.GetUniqueFileName(this.sysConfig.DebugDirectory, "orig-points.kml"));
                TrackWriter.Build(null, new[] { track.First().Point, track.Last().Point }, PointIcon.ParkingIcon)
                    .Save(Helper.GetUniqueFileName(this.sysConfig.DebugDirectory, "orig-anchors.kml") );
            }
            
            // remove same CONSECUTIVE (because the track could be in shape of "8") points
            //  track = track.ConsecutiveDistinctBy(dict => dict.NodeId).ToList();

            logger.Verbose($"Recreated track has {track.Count()} points");

            dumpRecreatedTrack(track, "dups");

            var turns = new List<TurnInfo>();

            for (int i = 0; i < track.Count - 1; ++i)
                track[i].Segment.Forward = computeTrackSegmentRoadId(track, i, direction: +1);
            for (int i = 1; i < track.Count; ++i)
                track[i].Segment.Backward = computeTrackSegmentRoadId(track, i, direction: -1);

            markCyclewayExits(track);

            IReadOnlySet<int> potential_turn_indices = computeCrossroadsIndices(track).ToHashSet();
            if (this.sysConfig.DebugDirectory != null)
                TrackWriter.WriteLabeled(Helper.GetUniqueFileName(this.sysConfig.DebugDirectory, "possible-turns.kml"), null, potential_turn_indices.OrderBy(x => x).Select(x => (track[x].Point, $"{x}")));

            foreach (int turn_pt_idx in potential_turn_indices.OrderBy(it => it)) // ordering makes debugging easier
            {
                // compute road kinds only at given points, this way we avoid problems with computing along entire track (at some point computing can be shaky)
                RoadIndexLong incoming_road = track[turn_pt_idx - 1].Segment.Forward;
                RoadIndexLong outgoing_road = track[turn_pt_idx + 1].Segment.Backward;

                RoadRank incoming_kind = new RoadRank(map.Roads[incoming_road.RoadMapIndex]);
                RoadRank outgoing_kind = new RoadRank(map.Roads[outgoing_road.RoadMapIndex]);

                long incoming_arm_node = track[turn_pt_idx - 1].NodeId;
                GeoZPoint incoming_arm_pt = track[turn_pt_idx - 1].Point;
                long outgoing_arm_node = track[turn_pt_idx + 1].NodeId;
                GeoZPoint outgoing_arm_pt = track[turn_pt_idx + 1].Point;

                var track_node = track[turn_pt_idx];
                GeoZPoint turn_point = track_node.Point;

                IReadOnlyList<(RoadIndexLong turn, RoadIndexLong sibling)> alt_arms = getAlternateArms(track_node, incoming_road, outgoing_road).ToList();
                logger.Verbose($"Track index {turn_pt_idx}, arms {alt_arms.Count}, {(String.Join(", ", alt_arms.Select(it => stringify(map.Roads[it.turn.RoadMapIndex]))))}");

                bool is_cross_intersection = false;
                if (alt_arms.Count == 2)
                {
                    // we calculate more distant points from the track to avoid reporting cross intersection for figures like
                    //   |  / track here
                    //  -+-/
                    //   |
                    //  with - segment being very short. For the rider in real life if will be angled interesection
                    if (map.Roads[alt_arms[0].turn.RoadMapIndex].Kind == map.Roads[alt_arms[1].turn.RoadMapIndex].Kind
                        // go over other turns to get proper length of the arm
                        && tryGetPointAlongTrack(track, emptyIntSet, turn_pt_idx, -1, this.userPreferences.MinimalCrossIntersection, out GeoZPoint distant_incoming_pt)
                        && tryGetPointAlongTrack(track, emptyIntSet, turn_pt_idx, +1, this.userPreferences.MinimalCrossIntersection, out GeoZPoint distant_outgoing_pt)
                            // for detecting true cross intersection we don't simplify arm kinds, because the arms have to be identical
                            //&& incoming_kind == outgoing_kind
                            )
                    {
                        GeoZPoint left_arm_pt = map.GetPoint(alt_arms[0].sibling);
                        GeoZPoint right_arm_pt = map.GetPoint(alt_arms[1].sibling);
                        if (calc.IsCrossIntersection(turn_point, distant_incoming_pt, distant_outgoing_pt, left_arm_pt, right_arm_pt, this.userPreferences.CrossIntersectionAngleSeparation,
                            out Angle in_left_angle, out Angle in_right_angle, out Angle out_left_angle, out Angle out_right_angle))
                        {
                            is_cross_intersection = true;
                            if (this.sysConfig.DebugDirectory != null)
                            {
                                TrackWriter.WriteLabeled(Helper.GetUniqueFileName(this.sysConfig.DebugDirectory, $"cross_interesection-{turn_pt_idx}.kml"),
                                    new[] { distant_incoming_pt, turn_point, distant_outgoing_pt },
                                    new[] { (left_arm_pt,$"{(in_left_angle.Degrees.ToString("0.#"))} {(out_left_angle.Degrees.ToString("0.#"))} "),
                                        (turn_point,$"{turn_pt_idx}"),
                                        (right_arm_pt,$"{(in_right_angle.Degrees.ToString("0.#"))} {(out_right_angle.Degrees.ToString("0.#"))} ") });
                            }
                        }
                    }
                }

                if (is_cross_intersection)
                {
                    logger.Verbose($"Skipping cross intersection at track node {turn_pt_idx}: {incoming_road}-{outgoing_road} vs {alt_arms[0].sibling}-{alt_arms[1].sibling}");
                }
                else
                {
                    TurnNotification forward = TurnNotification.None;
                    TurnNotification backward = TurnNotification.None;
                    
                    foreach ((RoadIndexLong alt_road_idx, RoadIndexLong alt_sibling) in alt_arms)
                    {
                        RoadInfo altInfo = map.Roads[alt_road_idx.RoadMapIndex];
                        //RoadRank alt_importance = simplifyRoadImportance(alt_info);
                        long alt_sibling_node = altInfo.Nodes[alt_sibling.IndexAlongRoad];

                        (forward, backward) = isTurnNeeded(track, potential_turn_indices, turn_pt_idx,
                            //track[trk_idx].GetIndex(incoming_road.RoadId), incoming_road,
                            //track[trk_idx].GetIndex(outgoing_road.RoadId), outgoing_road,
                            track_node.IsDirectionAllowed(incoming_road.RoadMapIndex, incoming_road),
                            track_node.IsDirectionAllowed(outgoing_road.RoadMapIndex, outgoing_road),
                            alt_road_idx, alt_sibling,
                            turn_point,
                            map.Roads[incoming_road.RoadMapIndex], // road kind "to" turn point
                            incoming_arm_node,
                            map.Roads[outgoing_road.RoadMapIndex], // road kind "from" turn point
                            outgoing_arm_node,
                            altInfo, alt_sibling_node);

                        if (forward.Enable || backward.Enable)
                        {
                            logger.Verbose($"Turn {turn_pt_idx} on roads {incoming_road.RoadMapIndex} {outgoing_road.RoadMapIndex} {altInfo.Identifier}, all present {(String.Join(", ", alt_arms.Select(it => stringify(map.Roads[it.turn.RoadMapIndex]))))}");
                            string reason;
                            if (forward.Reason == backward.Reason)
                                reason = "* "+forward.Reason;
                            else if (forward.Enable && backward.Enable)
                                reason = $"F:{forward.Reason}; B:{backward.Reason}";
                            else
                                reason = forward.Enable ? forward.Reason : backward.Reason;
                            
                            turns.Add(TurnInfo.CreateRegular(track_node.NodeId, turn_point, turn_pt_idx, forward.Enable, backward.Enable, reason));

                            break;
                        }
                    }

                    if (!forward.Enable && !backward.Enable)
                    {
                        logger.Verbose($"NO turn {turn_pt_idx} on roads {incoming_road.RoadMapIndex} {outgoing_road.RoadMapIndex}, all present {(String.Join(", ", alt_arms.Select(it => stringify(map.Roads[it.turn.RoadMapIndex]))))}");
                    }

                }
            }

            // add pairs enter-exit turns (if needed) for passed roundabouts

            computeRoundaboutTurnNotifications(track, turns,ref problem);

            if (this.sysConfig.DebugDirectory != null)
            {
                TrackWriter.WriteLabeled(Helper.GetUniqueFileName(this.sysConfig.DebugDirectory, "debug.kml"), null, DEBUG_points);
                TrackWriter.WriteLabeled(Helper.GetUniqueFileName(this.sysConfig.DebugDirectory, "turn-points.kml"), null, turns.Select(it => (it.Point, it.GetLabel())));
            }

            return turns;
        }

        private void computeRoundaboutTurnNotifications(List<TrackNode> track, List<TurnInfo> turns,
            ref string? problem)
        {
            int group = 0;

            for (int node_index = 1; node_index < track.Count; ++node_index)
            {
                TrackNode track_node = track[node_index];
                if (track[node_index - 1].RoundaboutId.HasValue || !track_node.RoundaboutId.HasValue)
                    continue;

                GeoZPoint center = this.map.GetRoundaboutCenter(this.calc, track_node.RoundaboutId.Value);
                IReadOnlySet<long> exit_nodes = getRoundaboutExitNodes(track_node.RoundaboutId.Value);

                var track_exits = track.Select(it => it.NodeId).ZipIndex()
                    .Where(it => exit_nodes.Contains(it.item)).ToList();
                if (track_exits.Count != 2)
                {
                    string message = $"We cannot compute for track with {track_exits.Count} exits at roundabout #{track_node.RoundaboutId}.";
                    problem ??= message;
                    this.logger.Warning(message);
                    continue;
                }

                (var incoming_node, var outgoing_node) = (track_exits[0], track_exits[1]);
                GeoZPoint incoming_pt = map.GetPoint(incoming_node.item);
                GeoZPoint outgoing_pt = map.GetPoint(outgoing_node.item);
                foreach (long exit in exit_nodes)
                {
                    if (exit != incoming_node.item && exit != outgoing_node.item)
                    {
                        (TurnNotification forward, TurnNotification backward) = (TurnNotification.None, TurnNotification.None);
                        TurnNotification forced = isTurnNeededOnCurvedTrack(node_index, center, incoming_pt, outgoing_pt, 
                            map.GetPoint(exit),
                            // we could the exit kind into account, but well -- since it works...
                            isAltMinor: false, ref forward, ref backward);
                        if (forced.Enable || forward.Enable || backward.Enable)
                        {
                            // todo: once we have better TrackRadar then keep the geometry info like
                            // the turn would be in the center, but warn user about it earlier -- at the entry
                            // points
                            turns.Add(TurnInfo.CreateRoundabout(track_node.RoundaboutId.Value,
                                center, incoming_node.index, group));
                            /*turns.Add(TurnInfo.CreateRoundabout(track_node.RoundaboutId.Value,
                                incoming_pt, incoming_node.index, group));
                            turns.Add(TurnInfo.CreateRoundabout(track_node.RoundaboutId.Value,
                                outgoing_pt, outgoing_node.index, group));
                                */
                            ++group;
                            break;
                        }
                    }
                }
            }
        }

        private void markCyclewayExits(List<TrackNode> track)
        {
            IReadOnlySet<int> potential_turn_indices = computeCrossroadsIndices(track).ToHashSet();
            foreach (int i in potential_turn_indices.OrderBy(x => x))
            {
                RoadIndexLong incoming_road = track[i - 1].Segment.Forward;
                RoadIndexLong outgoing_road = track[i + 1].Segment.Backward;

                RoadRank incoming_kind = new RoadRank(map.Roads[incoming_road.RoadMapIndex]);
                RoadRank outgoing_kind = new RoadRank(map.Roads[outgoing_road.RoadMapIndex]);

                bool incoming_fixed = fixPathCycleWayLink(track, i - 1, +1, ref incoming_kind);
                bool outgoing_fixed = fixPathCycleWayLink(track, i + 1, -1, ref outgoing_kind);
                // if we detected a path, which can be treated as cycleway it is better to keep this info, because further turn-nofication depends on such shaky situation
                if (incoming_fixed)
                {
                    track[i - 1].ForwardCyclewayUncertain = true;
                }
                if (outgoing_fixed)
                    track[i + 1].BackwardCyclewayUncertain = true;

                GeoZPoint incoming_arm_pt = track[i - 1].Point;
                GeoZPoint outgoing_arm_pt = track[i + 1].Point;

                GeoZPoint turn_point = track[i].Point;

                // if we go via road and we simply "turn" into parallel cycleway then ignore such turn, because road signs will tell all the instructions

                // we try to detect here if our track looks like this
                // |_
                //   |
                // in theory there are two turns, in fact it can be road-to-cycleway switch and in such case there should be no turn notifications

                bool from_cycleway = incoming_kind.IsCycleway;
                bool to_cycleway = outgoing_kind.IsCycleway;
                if ((from_cycleway && outgoing_kind.IsSolid) || (to_cycleway && incoming_kind.IsSolid))
                {
                    // ok, so we know we have change from/to cycleway, this coresponds to the short "link" segment
                    // so now we have to check if our track "behind" the cycle-link is also cycleway
                    if (from_cycleway && (i < 2 || map.Roads[track[i - 2].Segment.Forward.RoadMapIndex].Kind != WayKind.Cycleway))
                    {
                        logger.Verbose($"Giving up on cycle way exit at track index {i}, no further cycleway");
                        continue;
                    }
                    else if (to_cycleway && (i >= track.Count - 2 || map.Roads[track[i + 2].Segment.Backward.RoadMapIndex].Kind != WayKind.Cycleway))
                    {
                        logger.Verbose($"Giving up on cycle way exit at track index {i}, no further cycleway");
                        continue;
                    }

                    GeoZPoint cycleway_point = from_cycleway ? incoming_arm_pt : outgoing_arm_pt;
                    // first segment of the cycleway (counting from road usually is orthogonal and very short
                    Length cycle_next_dist = calc.GetDistance(turn_point, cycleway_point);

                    logger.Verbose($"Potential cycle way exit at track index {i}, dist {cycle_next_dist}");
                    DEBUG_points.Add((turn_point, $"Cycle way exit {i} turn"));
                    DEBUG_points.Add((cycleway_point, $"Cycle way point {i} exit"));

                    if (cycle_next_dist <= this.userPreferences.CyclewayExitDistanceLimit)
                    {
                        /*int regular_cycle_track_idx = i + (from_cycleway ? -2 : +2);
                        if (regular_cycle_track_idx < 0 || regular_cycle_track_idx >= track.Count)
                        {
                            logger.Warning($"Cannot decide whether we have cycleway exit, because index is off the track {regular_cycle_track_idx}/{track.Count}");
                            continue;
                        }
                        */

                        Angle road_bearing = calc.GetBearing(from_cycleway ? outgoing_arm_pt : incoming_arm_pt, turn_point);
                        int parallel_index = i + (from_cycleway ? -1 : +1);
                        if (!tryGetPointAlongTrack(track, potential_turn_indices, parallel_index, from_cycleway ? -1 : +1, this.userPreferences.CyclewayRoadParallelLength, out GeoZPoint parallel_cycle_point))
                        {
                            logger.Warning($"Cannot get enough parallel segment {this.userPreferences.CyclewayRoadParallelLength} to the road starting from {parallel_index}");
                            continue;
                        }

                        Angle cycle_bearing = calc.GetBearing(from_cycleway ? incoming_arm_pt : outgoing_arm_pt, parallel_cycle_point);
                        //                        Angle cycle_bearing = calc.GetBearing(from_cycleway ? incoming_arm_pt : outgoing_arm_pt, track[regular_cycle_track_idx].Point);
                        // compute absolute value in range (0,180) with 180 meaning we go straight ahead, 0 we going back
                        Angle bearing_diff = calc.GetAbsoluteBearingDifference(road_bearing, cycle_bearing);
                        bool marking_exit = bearing_diff >= this.userPreferences.CyclewayExitAngleLimit;
                        logger.Verbose($"Exit cycleway {i} = {marking_exit}. At angle {bearing_diff}, limit {this.userPreferences.CyclewayExitAngleLimit}");
                        if (marking_exit)
                        {
                            track[i].CycleWayExit = true;
                            if (from_cycleway)
                            {
                                track[i - 1].CyclewaySwitch = true;
                                logger.Verbose($"Marking track index {i - 1} as cycleway switch");
                            }
                            if (to_cycleway)
                            {
                                track[i + 1].CyclewaySwitch = true;
                                logger.Verbose($"Marking track index {i + 1} as cycleway switch");
                            }
                            track[i - 1].ForwardCycleWayCorrected = incoming_fixed;
                            track[i + 1].BackwardCycleWayCorrected = outgoing_fixed;
                        }

                    }
                }

            }

            foreach (var node in track)
            {
                // if we know for sure we need to correct cycleway exit link part then this segment is no longer uncertain
                if (node.ForwardCycleWayCorrected)
                    node.ForwardCyclewayUncertain = false;
                if (node.BackwardCycleWayCorrected)
                    node.BackwardCyclewayUncertain = false;
            }

        }

        private bool tryGetPointAlongTrack(IReadOnlyList<TrackNode> track, IReadOnlySet<int> turnIndices, int startIndex, int direction, Length length, out GeoZPoint point)
        {
            for (int curr = startIndex; ; curr += direction)
            {
                int next = curr + direction;
                if (next < 0 || next >= track.Count)
                {
                    if (curr == startIndex)
                    {
                        point = default;
                        return false;
                    }
                    else
                    {
                        point = track[curr].Point;
                        return true;
                    }
                }

                if (length == Length.Zero)
                {
                    point = track[curr].Point;
                    return true;
                }

                Length dist = calc.GetDistance(track[curr].Point, track[next].Point);
                if (length >= dist)
                    length -= dist;
                else
                {
                    point = calc.PointAlongSegment(track[curr].Point, track[next].Point, length);
                    return true;
                }

                if (turnIndices.Contains(next))
                {
                    point = track[next].Point;
                    return true;
                }
            }


        }

        private bool OLD_fixPathCycleWayLink_ByTrack(List<TrackNode> track, int i, int direction, ref WayKind segmentKind)
        {
            // there some cases when cycleways don't end up with regular roads, but in pathes, which then go into regular roads, 
            // we would like to treat such short path-cycleway exits like regular cycleways

            if (segmentKind != WayKind.Path)
                return false;
            if (Math.Min(i - 1, i - 1 + direction) < 0 || Math.Max(i + 1, i + 1 + direction) >= track.Count) // not enough context
                return false;

            // please note we deal here with segments, and we can have two cases
            // o----O====o-----o
            // or
            // o----o====O-----o
            // = segment we would like to fix, O active track node
            // in first case left is next-left, while in the second it is next-next-left
            // thanks to "direction" passed we can select appropriate segments with the same code

            RoadIndexLong incoming_road = track[i - 1].Segment[direction];
            RoadIndexLong outgoing_road = track[i + 1].Segment[direction]; //computeTrackSegmentRoadId(track, i + 1, direction);

            RoadRank incoming_kind = new RoadRank(map.Roads[incoming_road.RoadMapIndex]);
            RoadRank outgoing_kind = new RoadRank(map.Roads[outgoing_road.RoadMapIndex]);

            bool is_cycleway_link(in RoadRank a, in RoadRank b) => a.IsCycleway && !b.IsCycleway && !b.IsMapPath;

            if (is_cycleway_link(incoming_kind, outgoing_kind) || is_cycleway_link(outgoing_kind, incoming_kind))
            {
                logger.Verbose($"Fixing path at track {i} as cycleway link");
                segmentKind = WayKind.Cycleway;
                return true;
            }

            return false;
        }

        private bool fixPathCycleWayLink(List<TrackNode> track, int i, int direction, ref RoadRank segmentKind)
        {
            // there some cases when cycleways don't end up with regular roads, but in pathes, which then go into regular roads, 
            // we would like to treat such short path-cycleway exits like regular cycleways

            // please note we deal here with segments, and we can have two cases
            // o----O====o-----o
            // or
            // o----o====O-----o
            // = segment we would like to fix, O active track node
            // in first case left is next-left, while in the second it is next-next-left
            // thanks to "direction" passed we can select appropriate segments with the same code

            if (!segmentKind.IsMapPath)
            {
                logger.Verbose($"Skipping cycleway link fix for {i}/{direction}, not map path");
                return false;
            }

            // we could rely only on track segments which would be maybe more accurate, but until it is not needed let's base just on map info

            bool? is_cycleway_category(TrackNode node)
            {
                bool has_cycleway = node.Any(it => map.Roads[it.RoadMapIndex].Kind == WayKind.Cycleway);
                bool has_road = node.Any(it => new RoadRank(map.Roads[it.RoadMapIndex]).IsSolid);

                if (has_cycleway && !has_road)
                    return true;
                else if (has_road && !has_cycleway)
                    return false;
                else
                    return null; // hard to tell
            }

            if (!(is_cycleway_category(track[i]) is bool this_cycle))
                return false;
            if (!(is_cycleway_category(track[i + direction]) is bool other_cycle))
                return false;

            bool making_link = this_cycle != other_cycle;
            logger.Verbose($"Cycle way link fixed = {making_link} track {i}/{direction} with this cycle {this_cycle} and other cycle {other_cycle}");
            if (making_link) // to have LINK one has to be cycle way, while the other cannot be cycle way
            {
                segmentKind = segmentKind.CyclewayLink();
                return true;
            }

            return false;
        }

        private void dumpRecreatedTrack(IReadOnlyList<TrackNode> road_assignments, string label)
        {
            if (this.sysConfig.DebugDirectory != null)
            {
                var points = Enumerable.Range(0, road_assignments.Count).Select((Func<int, (GeoZPoint, string label)>)(i =>
                {
                    var ass = road_assignments[i];
                    (long road_id, ushort idx) = ass.First();
                    long node_id = this.map.Roads[road_id].Nodes[idx];
                    string label = ass.Count == 1 ? $"{i}={road_id}" : $"{i} : {ass.Count}";
                    return (this.map.GetPoint(node_id), label);
                }));
                TrackWriter.WriteLabeled(Helper.GetUniqueFileName(this.sysConfig.DebugDirectory, $"recreated-line-{label}.kml"), points.Select(it => it.Item1).ToList(), null);
                TrackWriter.WriteLabeled(Helper.GetUniqueFileName(this.sysConfig.DebugDirectory, $"recreated-points-{label}.kml"), null, points);
            }
        }

        private IEnumerable<(RoadIndexLong turn, RoadIndexLong sibling)> getAlternateArms(TrackNode trackNode, RoadIndexLong incoming, RoadIndexLong outgoing)
        {
            foreach (RoadIndexLong road_idx in trackNode)
            {
                bool is_same_arm(in RoadIndexLong a, in RoadIndexLong b) => a.RoadMapIndex == road_idx.RoadMapIndex && a.RoadMapIndex == b.RoadMapIndex  // same road
                                                                                                                         // we cannot compare direct indices or nodes, because our track does not have to contain all the nodes from the given map road
                    && Math.Sign(road_idx.IndexAlongRoad - a.IndexAlongRoad) == Math.Sign(road_idx.IndexAlongRoad - b.IndexAlongRoad); // same side of the turn point

                // alternate road can have two arms
                if (map.TryGetPrevious(road_idx, out RoadIndexLong alt_prev_sibling))
                {
                    if (!is_same_arm(alt_prev_sibling, incoming) && !is_same_arm(alt_prev_sibling, outgoing))
                        yield return (road_idx, alt_prev_sibling);
                }

                if (map.TryGetNext(road_idx, out RoadIndexLong alt_next_sibling))
                {
                    if (!is_same_arm(alt_next_sibling, incoming) && !is_same_arm(alt_next_sibling, outgoing))
                        yield return (road_idx, alt_next_sibling);
                }
            }
        }


        private HashSet<long> getRoundaboutExitNodes(long roundaboutId)
        {
            var result = new HashSet<long>();
            foreach (long node_id in map.Roads[roundaboutId].Nodes)
            {
                foreach (RoadIndexLong road_idx in this.map.GetRoads(node_id).Where(it => it.RoadMapIndex != roundaboutId))
                {
                    RoadInfo roadInfo = map.Roads[road_idx.RoadMapIndex];
                    if (roadInfo.OneWay)
                    {
                        // if this is oneway exit road, it means the exit is on the end of this "link"-road actually, case like this
                        // O>
                        if (road_idx.IndexAlongRoad == 0)
                            result.Add(map.Roads[road_idx.RoadMapIndex].Nodes.Last());
                        else if (road_idx.IndexAlongRoad == roadInfo.Nodes.Count - 1)
                            result.Add(map.Roads[road_idx.RoadMapIndex].Nodes.First());
                        else
                            throw new NotImplementedException();
                    }
                    else
                        // if this is bi-directional road, it means it is direct exit from roundabout
                        result.Add(map.GetNode(road_idx));
                }
            }

            return result;
        }

        private IEnumerable<int> computeCrossroadsIndices(IReadOnlyList<TrackNode> roadAssignments)
        {
            for (int i = 1; i < roadAssignments.Count - 1; ++i)
            {
                TrackNode track_node = roadAssignments[i];

                if (track_node.CycleWayExit || track_node.CyclewaySwitch)
                {
                    ;
                }
                else if (track_node.RoundaboutId.HasValue)
                {
                    ; // for roundabouts we have special treatment
                }
                else if (track_node.Count == 1)
                {
                    ;
                }
                else if (track_node.Count > 2)
                {
                    yield return i;
                }
                else if (isEndRoad(track_node.First()) && isEndRoad(track_node.Last()))
                {
                    // just an extension of the roads
                    ;
                }
                else
                {
                    // T-juction of two roads, thus it is a turn here
                    yield return i;
                }
            }
        }

        private bool isEndRoad(in RoadIndexLong roadIdx)
        {
            RoadInfo info = this.map.Roads[roadIdx.RoadMapIndex];
            return roadIdx.IndexAlongRoad == 0 || roadIdx.IndexAlongRoad == info.Nodes.Count - 1;
        }

        private RoadIndexLong computeTrackSegmentRoadId(IReadOnlyList<TrackNode> roadAssignments, int idx, int direction)
        {
            if (direction != -1 && direction != 1)
                throw new ArgumentOutOfRangeException($"{nameof(direction)} {direction}");

            // such cases are valid scenarios
            // main road
            // ----x---------x----
            //      \-------/
            //    truck control service road
            // as the effect two points from the track will have two, not one, roads shared for them
            // we need to take this into account

            // road id -> node index along the road
            TrackNode current = roadAssignments[idx];
            TrackNode next = roadAssignments[idx + direction];

            // road id -> node indices along the road
            IReadOnlyDictionary<long, (ushort currentIndex, ushort nextIndex)> intersection = current.ShortestSegmentsIntersection(next);

            if (intersection.Count == 0)
            {
                logger.Verbose($"CURRENT {idx}");
                foreach (var entry in current)
                    logger.Verbose($"{entry.RoadMapIndex} [{entry.IndexAlongRoad}]");
                logger.Verbose($"NEXT {idx + direction}");
                foreach (var entry in next)
                    logger.Verbose($"{entry.RoadMapIndex} [{entry.IndexAlongRoad}]");
                if (this.sysConfig.DebugDirectory != null)
                {
                    TrackWriter.WriteLabeled(Helper.GetUniqueFileName(this.sysConfig.DebugDirectory, "segment-kinds.kml"), null,
                        new[] { (current.Point, $"{idx}"), (next.Point, $"{idx + direction}") }, PointIcon.CircleIcon);
                }
                throw new ArgumentException($"Unable to compute segment {intersection.Count}");
            }
            else if (intersection.Count == 1)
            {
                (long road_id, (int current_index, _)) = intersection.Single();
                return new RoadIndexLong(road_id, current_index);
            }
            else
            {
                // we could check if we have consecutive nodes from given road, but depending how we assigned track to road this may fail

                // ok, we have multiple roads coming through two consecutive points of our track, so we need to select the road for which those points are closest to each other
                // instead of computing actual distance, we take the index-length (how many road nodes are involved)
                (long road_id, (int current_index, _)) = intersection.OrderBy(it => map.LEGACY_RoadSegmentsDistanceCount(it.Key, it.Value.currentIndex, it.Value.nextIndex)).First();

                return new RoadIndexLong(road_id, current_index);
            }
        }

        /*private bool directionAllowed(in RoadIndex from, in RoadIndex dest)
        {
            if (from.RoadId != dest.RoadId)
                throw new ArgumentException($"Cannot compute direction for two different roads {from.RoadId} {dest.RoadId}");
            if (map.GetNode( from )== map.GetNode( dest))
                throw new ArgumentException($"Cannot compute direction for the same spot {from.IndexAlongRoad}");

            if (!map.Roads[from.RoadId].OneWay)
                return true;

            if (map.IsRoadLooped(from.RoadId))
                throw new ArgumentException($"Cannot tell direction of looped road {from.RoadId}");

            return dest.IndexAlongRoad > from.IndexAlongRoad;
        }*/

        private bool tryGetRoundabout(IReadOnlyList<TrackNode> track, int trackIndex, int direction, out long roundaboutId)
        {
            if (direction != -1 && direction != 1)
                throw new ArgumentOutOfRangeException($"Invalid direction {direction}");

            for (; trackIndex >= 0 && trackIndex < track.Count; trackIndex += direction)
                if (track[trackIndex].RoundaboutId is long road_id)
                {
                    roundaboutId = road_id;
                    return true;
                }

            roundaboutId = -1;
            return false;
        }

        private bool reachesRoundabout(RoadIndexLong roadIndexLong, int direction, long roundaboutId)
        {
            if (direction != -1 && direction != 1)
                throw new ArgumentOutOfRangeException($"Invalid direction {direction}");

            IReadOnlySet<long> roundabout_nodes = map.Roads[roundaboutId].Nodes.ToHashSet();

            RoadInfo roadInfo = map.Roads[roadIndexLong.RoadMapIndex];
            for (int idx = roadIndexLong.IndexAlongRoad; idx >= 0 && idx < roadInfo.Nodes.Count; idx += direction)
                if (roundabout_nodes.Contains(roadInfo.Nodes[idx]))
                {
                    return true;
                }

            return false;
        }

        private (TurnNotification forward, TurnNotification backward) isTurnNeeded(IReadOnlyList<TrackNode> track, IReadOnlySet<int> turnIndices, 
            int nodeIndex,
            //in RoadIndex incomingTurn, in RoadIndex incoming, 
            //in RoadIndex outgoingTurn, in RoadIndex outgoing,
            bool isIncomingDirectionAllowed,
            bool isOutgoingDirectionAllowed,

            in RoadIndexLong altTurn, in RoadIndexLong altSibling,
            in GeoZPoint turnPoint,
            in RoadInfo incomingSegmentInfo, long incomingNode,
            in RoadInfo outgoingSegmentInfo, long outgoingNode,
            in RoadInfo altInfo, long altSiblingNode)
        {
            // general assumption is we can go both ways along the track no matter if this is one way or not
            // but we have to ride according to the rules when considering alternative
            if (!map.IsDirectionAllowed(altTurn, altSibling))
            {
                logger.Verbose($"One direction, rejecting");
                return (TurnNotification.None, TurnNotification.None);
            }

            // if we are on cycle way and we continue to ride on cycleway, then we don't need any turn notification
            if (incomingSegmentInfo.Kind == WayKind.Cycleway
                && outgoingSegmentInfo.Kind == WayKind.Cycleway
                // surface continuation is important visual cue
                && incomingSegmentInfo.Surface == outgoingSegmentInfo.Surface
                && altInfo.Kind != WayKind.Cycleway)
            {
                return (TurnNotification.None, TurnNotification.None);
            }

            RoadRank alt_kind = new RoadRank(altInfo);

            logger.Verbose($"Incoming corrected = {track[nodeIndex].BackwardCycleWayCorrected}, outgoing corrected = {track[nodeIndex].ForwardCycleWayCorrected}");

            RoadRank incoming_rank = track[nodeIndex].BackwardCycleWayCorrected ? RoadRank.CyclewayLink(incomingSegmentInfo) : new RoadRank(incomingSegmentInfo);
            RoadRank outgoing_rank = track[nodeIndex].ForwardCycleWayCorrected ? RoadRank.CyclewayLink(outgoingSegmentInfo) : new RoadRank(outgoingSegmentInfo);

            GeoZPoint current_point = map.GetPoint(incomingNode);
            GeoZPoint next_point = map.GetPoint(outgoingNode);
            GeoZPoint alt_sibling_point = map.GetPoint(altSiblingNode);

            // consider we are coming from right
            // _L 
            // if L is primary road and we ride along it -- no turn is need
            // if we go in horizontal line -- turn is needed
            if (incoming_rank.IsMoreImportantThan(alt_kind) && outgoing_rank.IsMoreImportantThan(alt_kind))
            {
                logger.Verbose($"Alt arm has lower priority {alt_kind} than both arms {incoming_rank}, {outgoing_rank} of our track");
                return (TurnNotification.None, TurnNotification.None);
            }

            // exceptions:
            // _|_                
            // let's say | is primary but we go in horizontal line, despite priorities of the road there should be no turn (because we maintain course)

            {
                int debug_id = DEBUG_points.Count;
                DEBUG_points.Add((current_point, $"c {nodeIndex - 1} {debug_id} {incoming_rank} {incomingNode}"));
                DEBUG_points.Add((next_point, $"n {nodeIndex + 1} {debug_id} {outgoing_rank} {outgoingNode}"));
                DEBUG_points.Add((turnPoint, $"t {nodeIndex} {debug_id}"));
                DEBUG_points.Add((alt_sibling_point, $"a {debug_id} {alt_kind} {altSiblingNode}"));
            }

            bool incoming_uncertain = track[nodeIndex - 1].ForwardCyclewayUncertain;
            bool outgoing_uncertain = track[nodeIndex + 1].BackwardCyclewayUncertain;

            logger.Verbose($"Checking transition {(incoming_uncertain ? "?" : "")}{incoming_rank} -> {(outgoing_uncertain ? "?" : "")}{outgoing_rank}");

            if (incoming_uncertain || outgoing_uncertain)
            {
                var uncertain_notification = new TurnNotification(true, $"Uncertain roads incoming:{incoming_uncertain}, outgoing:{outgoing_uncertain}");
                return (uncertain_notification, uncertain_notification);
            }

            // we can go without turn notification (for example) from path to highway, but not from highway to path
            //                bool forward = incoming_segment_kind < outgoing_segment_kind;
            //              bool backward = incoming_segment_kind > outgoing_segment_kind;
            TurnNotification forward = TurnNotification.None;
            TurnNotification backward = TurnNotification.None;
            if (incoming_rank.IsMoreImportantThan(outgoing_rank))
                forward = new TurnNotification(true, "Coming into lesser road");
            if (outgoing_rank.IsMoreImportantThan(incoming_rank))
                backward = new TurnNotification(true, "Returning into lesser road");

            if (forward.Enable && backward.Enable) // this case should not ever happen
                return (forward, backward);

            logger.Verbose($"Initial turn notifications forward {forward}, backward {backward}");

            // if any priority difference suggests no need for notification, let's check if our track is pretty straight at this point

            GeoZPoint incoming_point;
            if (!tryGetPointAlongTrack(track, turnIndices, nodeIndex, -1, this.userPreferences.TurnArmLength, out incoming_point))
                incoming_point = current_point;
            GeoZPoint outgoing_point;
            if (!tryGetPointAlongTrack(track, turnIndices, nodeIndex, +1, this.userPreferences.TurnArmLength, out outgoing_point))
                outgoing_point = next_point;

            TurnNotification forced = isTurnNeededOnCurvedTrack(nodeIndex, turnPoint, incoming_point, outgoing_point,
                alt_sibling_point,
                isAltMinor: altInfo.Kind > incomingSegmentInfo.Kind && altInfo.Kind > outgoingSegmentInfo.Kind, ref forward, ref backward);

            if (forced.Enable)
            {
                return (forced, forced);
            }

            if (forward.Enable)
            {
                // consider road around roundabout like this
                // O>
                // so if we detect our track is against the traffic and our track leads to roundabout and the alternate road leads to the same roundabout
                // are not at the "real" crossroad but at the split roads leading to the same roundabout, so there should be no turn-notification, because
                // you cannot choose the road, one way is FROM roundabout, the other is TO roundabout, you ride the one you has to ride
                if (!isOutgoingDirectionAllowed //directionAllowed(outgoingTurn, outgoing)
                    && tryGetRoundabout(track, nodeIndex, +1, out long roundabout_id)
                    && reachesRoundabout(altTurn, Math.Sign(altSibling.IndexAlongRoad - altTurn.IndexAlongRoad), roundabout_id))
                {
                    forward = TurnNotification.None;
                    logger.Verbose("Rejecting alternate road because both track and alt are links to roundabout");
                }
            }

            if (backward.Enable)
            {
                if (!isIncomingDirectionAllowed //directionAllowed(incomingTurn, incoming)
                    && tryGetRoundabout(track, nodeIndex, -1, out long roundabout_id)
                    && reachesRoundabout(altTurn, Math.Sign(altSibling.IndexAlongRoad - altTurn.IndexAlongRoad), roundabout_id))
                {
                    backward = TurnNotification.None;
                    logger.Verbose("Rejecting alternate road because both track and alt are links to roundabout");
                }
            }

            return (forward, backward);
        }

        // this recomputes angle distance from range (0,360) to (-180,+180) which is easier to say what side it is, -180/+180 angle means dead ahead
        private Angle signedAngleDistance(in GeoZPoint center, in GeoZPoint start, in GeoZPoint end)
        {
            Angle dist = calc.AngleDistance(center, start, end);
            return dist <= Angle.PI ? dist : dist - Angle.FullCircle;
        }

        private TurnNotification isTurnNeededOnCurvedTrack(int turnPointIndex, in GeoZPoint turnPoint, in GeoZPoint currentPoint,
            in GeoZPoint nextPoint, in GeoZPoint altSiblingPoint, bool isAltMinor, ref TurnNotification forward, ref TurnNotification backward)
        {
            var is_track_curved = isTrackCurved(turnPoint, currentPoint, nextPoint, out var track_angle);
            logger.Verbose($"Angle: {track_angle} below limit {this.userPreferences.StraigtLineAngleLimit}, curved = {is_track_curved}");
            if (is_track_curved)
            {
                if (this.sysConfig.DebugDirectory != null)
                {
                    TrackWriter.Build(new[] { currentPoint, turnPoint, nextPoint }, new[] { turnPoint })
                        .Save(Helper.GetUniqueFileName(this.sysConfig.DebugDirectory, $"turn-{turnPointIndex}-angled.kml"));
                }
                return new TurnNotification(true,$"Curved track {TrackPlanner.Data.DataFormat.Format(track_angle)} below limit {TrackPlanner.Data.DataFormat.Format(this.userPreferences.StraigtLineAngleLimit)}");
            }

            bool alt_angle_makes_turn(Angle track, Angle alt)
            {
                if (track.Sign() == alt.Sign()) // same side
                {
                    logger.Verbose($"Same sides, alt angle {alt}");
                    return alt.Abs() > track.Abs();
                }
                else // opposite sides
                {
                    Angle diff = track.Abs() - alt.Abs();
                    Angle diff_limit = this.userPreferences.GetAltAngleDifferenceLimit(track.Abs());//.AltAngleDifferenceLowLimit;
                    if (isAltMinor)
                        diff_limit -= this.userPreferences.AltMinorAngleSlack;
                    bool turn_needed = diff <= diff_limit;
                    logger.Verbose($"Opposite sides, alt angle {alt}, diff {diff}, limit {diff_limit}, turn {turn_needed}");
                    return turn_needed;
                }
            }
            // ok, our track looks flat at this turn, so we could skip turn-notification, but now the question is whether alternate road is not straight line as well

            if (!forward.Enable && alt_angle_makes_turn(track_angle, signedAngleDistance(turnPoint, currentPoint, altSiblingPoint)))
                forward = new TurnNotification(true,"Alternative road makes turn");


            // we checked riding forward/along the track, now we have to check riding backwards (we don't know which direction we will be riding)

            // watch the changed signed, now we are riding back, from next to current
            if (!backward.Enable && alt_angle_makes_turn(-track_angle, signedAngleDistance(turnPoint, nextPoint, altSiblingPoint)))
                backward = new TurnNotification(true,"Alternative road makes turn");

            return  TurnNotification.None;
        }

        private bool isTrackCurved(GeoZPoint turnPoint, GeoZPoint currentPoint, GeoZPoint nextPoint, 
            out Angle trackAngle)
        {
            trackAngle = signedAngleDistance(turnPoint, currentPoint, nextPoint);
            return trackAngle.Abs() <= this.userPreferences.StraigtLineAngleLimit;
        }
    }
}
