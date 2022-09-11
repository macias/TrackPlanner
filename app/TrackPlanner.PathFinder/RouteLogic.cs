using System;
using TrackPlanner.Data.Stored;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using MathUnit;
using TrackPlanner.Data;
using TrackPlanner.Shared;
using TrackPlanner.Mapping;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.PathFinder
{
    public class RouteLogic
    {
        private readonly UserRouterPreferences userConfig;
        private readonly Speed fastest;
        private readonly IReadOnlySet<long> suppressedTraffic;
        private readonly HashSet<long> DEBUG_lowCostNodes;
        private readonly IWorldMap map;
        private readonly IGeoCalculator calc;

        public RouteLogic(IWorldMap map,IGeoCalculator calc,UserRouterPreferences userConfig,Speed fastest, IReadOnlySet<long> suppressedTraffic)
        {
            this.map = map;
            this.calc = calc;
            this.userConfig = userConfig;
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
            else if (current.IsNode)
            {
                roadIds = map.GetRoads(current.NodeId).Select(it => it.RoadMapIndex);
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

        internal SegmentInfo GetSegmentInfo(RoadBucket start, RoadBucket end,
            long? incomingRoadMapIndex, 
            long connectingRoadMapIndex, 
            Placement currentPlace, Placement targetPlace)
        {
            var target_point = targetPlace.Point;

            Length segment_length = calc.GetDistance(currentPlace.Point, target_point);

            RoadInfo connecting_road = this.map.Roads[connectingRoadMapIndex];

            bool is_forbidden = connecting_road.Kind <= WayKind.HighwayLink || !connecting_road.HasAccess;
            SpeedMode connecting_speed_mode = connecting_road.GetRoadSpeedMode();

            double cost_scale_factor = 1.0;
            
            Risk risk_info = Risk.None;

            if (connecting_road.Kind != WayKind.Cycleway)
                cost_scale_factor += this.userConfig.AddedNonCyclewayCostFactor;
            
            {
                bool is_suppressed(Placement pl) => (pl.IsSnapped && !pl.IsFinal)
                                                    || (pl.IsNode && suppressedTraffic.Contains(pl.NodeId));

                if (connecting_road.IsDangerous)
                {
                    risk_info |= Risk.Dangerous;


                    if (is_suppressed(currentPlace) && is_suppressed(targetPlace))
                    {
                        ; // default cost
                        if (currentPlace.IsNode)
                            this.DEBUG_lowCostNodes.Add(currentPlace.NodeId);
                        if (targetPlace.IsNode)
                            this.DEBUG_lowCostNodes.Add(targetPlace.NodeId);

                        risk_info |= Risk.Suppressed;
                    }
                    else
                    {
                        cost_scale_factor += this.userConfig.AddedMotorDangerousTrafficFactor;
                    }
                }
                else if (connecting_road.IsUncomfortable)
                {
                    risk_info |= Risk.Uncomfortable;

                    if (is_suppressed(currentPlace) && is_suppressed(targetPlace))
                    {
                        ; // default cost
                        risk_info |= Risk.Suppressed;
                    }
                    else
                    {
                        cost_scale_factor += this.userConfig.AddedMotorUncomfortableTrafficFactor;
                    }
                }
                else if (currentPlace.IsNode && targetPlace.IsNode
                                             && this.map.IsBikeFootRoadDangerousNearby( /*roadId: incomingRoadMapIndex, */nodeId: currentPlace.NodeId)
                                             && this.map.IsBikeFootRoadDangerousNearby( /*roadId: incomingRoadMapIndex, */nodeId: targetPlace.NodeId))
                {
                    risk_info |= Risk.HighTrafficBikeLane;
                    cost_scale_factor += this.userConfig.AddedBikeFootHighTrafficFactor;
                    //logger.Info($"Higher cost {cost_factor} for way {incoming_road_id}");
                }
            }

            Speed curr_adj_speed;

            // segment between user point and cross point
            bool is_snap = !currentPlace.IsNode && !targetPlace.IsNode;

            if (is_snap)
            {
                // do not mark snap as forbidden because it would start counting its length
                //    is_forbidden = false;
                // use top speed to prefer slightly longer snap to some node, instead snapping right  to the closest road and then moving by it 1-2 meters, which is absurd
                curr_adj_speed = this.fastest;
                cost_scale_factor = 1.0;
            }
            else
            {
                curr_adj_speed = this.userConfig.Speeds[connecting_speed_mode];
            }

            TimeSpan added_run_time = segment_length / curr_adj_speed;
            TravelCost added_run_cost = TravelCost.Create(added_run_time, cost_scale_factor);

            if (!is_snap)
            {
                // crossing or joining high-traffic road
                if (this.userConfig.JoiningHighTraffic != TimeSpan.Zero
                    // todo: this is odd, we should check the road we came, and the connecting road (as future one)
                    && !connecting_road.IsMassiveTraffic
                    && tryGetIcomingRoadIds(targetPlace, start, end, out IEnumerable<long>? road_ids)
                    && road_ids.Any(it => this.map.Roads[it].IsMassiveTraffic)) // we are hitting high-traffic road
                {
                    TimeSpan join_traffic = this.userConfig.JoiningHighTraffic;
                    added_run_time += join_traffic;
                    // we add it in separate step, because cost factor of crossing is constant
                    added_run_cost += TravelCost.Create(join_traffic, 1.0);
                }
                else if (incomingRoadMapIndex != null
                         && !this.map.IsRoadContinuation(incomingRoadMapIndex.Value, connectingRoadMapIndex))
                {
                    added_run_cost += TravelCost.Create(this.userConfig.AddedRoadSwitchingCostValue, 1.0);
                }
            }



            return (SegmentInfo.Create(segment_length, isForbidden: is_forbidden, isStable: connecting_road.Kind.IsStable(), isSnap: is_snap) with
                {
                    RiskInfo = risk_info,
                    Cost = added_run_cost,
                    Time = added_run_time,
                })
                .WithSpeedMode(connecting_speed_mode)
                ;
        }

    }
}