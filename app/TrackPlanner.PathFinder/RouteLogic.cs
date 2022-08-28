using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using MathUnit;
using TrackPlanner.Data;
using TrackPlanner.Settings;
using TrackPlanner.Shared;
using TrackPlanner.Mapping;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.PathFinder
{
    public class RouteLogic
    {
        private readonly UserPlannerPreferences userPlannerConfig;
        private readonly Speed fastest;
        private readonly IReadOnlySet<long> suppressedTraffic;
        private readonly HashSet<long> DEBUG_lowCostNodes;
        private readonly IWorldMap map;
        private readonly IGeoCalculator calc;

        public RouteLogic(IWorldMap map,IGeoCalculator calc,UserPlannerPreferences userPlannerConfig,Speed fastest, IReadOnlySet<long> suppressedTraffic)
        {
            this.map = map;
            this.calc = calc;
            this.userPlannerConfig = userPlannerConfig;
            this.fastest = fastest;
            this.suppressedTraffic = suppressedTraffic;
            this.DEBUG_lowCostNodes = new HashSet<long>();
        }
        
        
        private bool tryGetIcomingRoadIds(Placement current, RoadBucket bucketA, RoadBucket bucketB, [MaybeNullWhen(false)] out IEnumerable<long> roadIds)
        {
            if (current.IsUserPoint || current.IsPrestart)
            {
                roadIds = default;
                return false;
            }
            else if (current.NodeId.HasValue)
            {
                roadIds = map.GetRoads(current.NodeId.Value).Select(it => it.RoadMapIndex);
                return true;
            }
            else if (current.IsCross)
            {
                IEnumerable<RoadSnapInfo> snaps;

                snaps = bucketA.Where(it => current.Point == it.TrackCrosspoint);
                if (snaps.Any())
                {
                    roadIds = snaps.Select(it => it.RoadIdx.RoadMapIndex);
                    return true;
                }

                snaps = bucketB.Where(it => current.Point == it.TrackCrosspoint);
                {
                    roadIds = snaps.Select(it => it.RoadIdx.RoadMapIndex);
                    return true;
                }
            }

            throw new InvalidOperationException("Not possible");
        }

        internal SegmentInfo GetSegmentInfo(RoadBucket start, RoadBucket end, long incomingRoadMapIndex, Placement currentPlace, Placement targetPlace)
        {
            var target_point = targetPlace.GetPoint(map);

            Length segment_length = calc.GetDistance(currentPlace.GetPoint(map), target_point);

            var incoming_road = this.map.Roads[incomingRoadMapIndex];

            bool is_forbidden = incoming_road.Kind <= WayKind.HighwayLink || !incoming_road.HasAccess;
            SpeedMode incoming_speed_mode = incoming_road.GetRoadSpeedMode();

            double cost_factor = 1.0;
            Risk risk_info = Risk.None;

            {
                bool is_suppressed(Placement pl) => (pl.IsSnapped && !pl.IsFinal)
                                                    || (pl.NodeId.HasValue && suppressedTraffic.Contains(pl.NodeId.Value));

                if (incoming_road.IsDangerous)
                {
                    risk_info |= Risk.Dangerous;


                    if (is_suppressed(currentPlace) && is_suppressed(targetPlace))
                    {
                        ; // default cost
                        if (currentPlace.NodeId.HasValue)
                            this.DEBUG_lowCostNodes.Add(currentPlace.NodeId.Value);
                        if (targetPlace.NodeId.HasValue)
                            this.DEBUG_lowCostNodes.Add(targetPlace.NodeId.Value);

                        risk_info |= Risk.Suppressed;
                    }
                    else
                    {
                        cost_factor = 1.0 + this.userPlannerConfig.AddedMotorDangerousTrafficFactor;
                    }
                }
                else if (incoming_road.IsUncomfortable)
                {
                    risk_info |= Risk.Uncomfortable;

                    if (is_suppressed(currentPlace) && is_suppressed(targetPlace))
                    {
                        ; // default cost
                        risk_info |= Risk.Suppressed;
                    }
                    else
                    {
                        cost_factor = 1.0 + this.userPlannerConfig.AddedMotorUncomfortableTrafficFactor;
                    }
                }
                else if (currentPlace.NodeId.HasValue && targetPlace.NodeId.HasValue
                                                      && this.map.IsBikeFootRoadDangerousNearby(/*roadId: incomingRoadMapIndex, */nodeId: currentPlace.NodeId.Value)
                                                      && this.map.IsBikeFootRoadDangerousNearby(/*roadId: incomingRoadMapIndex, */nodeId: targetPlace.NodeId.Value))
                {
                    risk_info |= Risk.HighTrafficBikeLane;
                    cost_factor = 1.0 + this.userPlannerConfig.AddedBikeFootHighTrafficFactor;
                    //logger.Info($"Higher cost {cost_factor} for way {incoming_road_id}");
                }
            }

            Speed curr_adj_speed;

            // segment between user point and cross point
            bool is_snap = currentPlace.Point.HasValue && targetPlace.Point.HasValue;

            if (is_snap)
            {
                // do not mark snap as forbidden because it would start counting its length
                //    is_forbidden = false;
                // use top speed to prefer slightly longer snap to some node, instead snapping right  to the closest road and then moving by it 1-2 meters, which is absurd
                curr_adj_speed = this.fastest;
                cost_factor = 1.0;
            }
            else
            {
                curr_adj_speed = this.userPlannerConfig.Speeds[incoming_speed_mode];
            }

            TimeSpan added_run_time = segment_length / curr_adj_speed;
            TravelCost added_run_cost = new TravelCost(added_run_time, cost_factor);
            // crossing or joining high-traffic road
            if (this.userPlannerConfig.JoiningHighTraffic != TimeSpan.Zero && !incoming_road.IsMassiveTraffic && tryGetIcomingRoadIds(targetPlace, start, end, out IEnumerable<long>? road_ids))
            {
                if (road_ids.Any(it => this.map.Roads[it].IsMassiveTraffic)) // we are hitting high-traffic road
                {
                    TimeSpan join_traffic = this.userPlannerConfig.JoiningHighTraffic;
                    added_run_time += join_traffic;
                    added_run_cost += new TravelCost(join_traffic, 1.0); // we add it in separate step, because cost factor of crossing is constant
                }
            }

            return (SegmentInfo.Create(segment_length, isForbidden: is_forbidden, isStable: incoming_road.Kind.IsStable(), isSnap: is_snap) with
                {
                    RiskInfo = risk_info,
                    Cost = added_run_cost,
                    Time = added_run_time,
                })
                .WithSpeedMode(incoming_speed_mode)
                ;
        }

    }
}