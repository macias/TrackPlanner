using TrackPlanner.Data.Stored;
using System;
using System.Collections.Generic;
using System.Linq;
using MathUnit;
using TrackPlanner.Data;
using TrackPlanner.Shared;
using TrackPlanner.Mapping;

namespace TrackPlanner.PathFinder
{
    public class RouteCompactor
    {
        private readonly ILogger logger;
        private readonly IWorldMap map;
        private readonly UserPlannerPreferences userPlannerPrefs;
        private readonly bool compactPreservesRoads;
        private readonly ApproximateCalculator calc;

        public RouteCompactor(ILogger logger, IWorldMap map,UserPlannerPreferences userPlannerPrefs, bool compactPreservesRoads)
        {
            this.logger = logger;
            this.map = map;
            this.userPlannerPrefs = userPlannerPrefs;
            this.compactPreservesRoads = compactPreservesRoads;
            this.calc = new ApproximateCalculator();
        }
        
        public TrackPlan Compact(List<LegRun> legs)
        {
            foreach (var leg in legs)
                flattenRoundabouts(leg.Steps);
            var plan = splitLegs(legs);
            
            if (this.userPlannerPrefs.CompactingDistanceDeviation== 0 || this.userPlannerPrefs.CompactingAngleDeviation== 0)
                this.logger.Verbose("Skipping simplification");
            else
            {
                int removed = 0;
                foreach (var leg in plan.Legs)
                {
                    foreach (var fragment in leg.Fragments)
                       removed+= simplifySegment(fragment);
                }
                
                this.logger.Verbose($"{removed} nodes removed when simplifying route with {this.userPlannerPrefs.CompactingAngleDeviation}°, {this.userPlannerPrefs.CompactingDistanceDeviation}m.");
            }
            
            this.logger.Info($"{plan.Legs.SelectMany(l => l.Fragments).SelectMany(s => s.Places).Count()} points on the track.");
            
            return plan;
        }

        private int simplifySegment(LegFragment fragment)
        {
            int removed_count = 0;
            for (int start_idx = 0; start_idx < fragment.Places.Count - 2; ++start_idx)
            {
                int end_idx;
                // we cannot remove the ending point
                for (end_idx = start_idx + 2; end_idx < fragment.Places.Count-1; ++end_idx)
                {
                    Angle base_bearing = this.calc.GetBearing(fragment.Places[start_idx].Point, fragment.Places[end_idx].Point);

                    for (int i = start_idx; i < end_idx; ++i)
                    {
                        Angle curr_bearing = this.calc.GetBearing(fragment.Places[i].Point, fragment.Places[i + 1].Point);

                        if (this.calc.GetAbsoluteBearingDifference(curr_bearing, base_bearing) > Angle.FromDegrees(this.userPlannerPrefs.CompactingAngleDeviation))
                        {
                            goto END_IDX_LOOP;
                        }
                    }

                    for (int i = start_idx + 1; i < end_idx; ++i) // the loop ranges are different than above
                    {
                        (Length dist, _, _) = this.calc.GetDistanceToArcSegment(fragment.Places[i].Point, fragment.Places[end_idx].Point, fragment.Places[start_idx].Point);

                        if (dist > Length.FromMeters(this.userPlannerPrefs.CompactingDistanceDeviation))
                        {
                            goto END_IDX_LOOP;
                        }
                    }
                }

                END_IDX_LOOP: ;

                // in both cases we have to decrease end_idx -- either we broke through the Count limit or we failed
                // to stick within the limits for it (but previous value was OK)
                --end_idx;

                int count = end_idx - start_idx - 1;
                if (count > 0)
                {
                    fragment.Places.RemoveRange(start_idx + 1, count);
                    var removed_steps_sum = fragment.StepDistances.Skip(start_idx).Take(count).Sum();
                    fragment.StepDistances.RemoveRange(start_idx, count);
                    fragment.StepDistances[start_idx] += removed_steps_sum;
                    removed_count += count;
                }
            }

            return removed_count;
        }

        private void flattenRoundabouts(List<StepRun> pathSteps)
        {
            legCheck(pathSteps);
            
            for (int step_idx = pathSteps.Count - 1; step_idx >= 0; )
            {
                var path_step = pathSteps[step_idx];
                
                var incoming_road_map_index = path_step.IncomingRoadMapIndex;
                var roundabout_road = map.Roads[ incoming_road_map_index];
                if (!roundabout_road.IsRoundabout)
                {
                    --step_idx;
                    continue;
                }

                RoadCondition condition =  path_step.IncomingCondition;
                TimeSpan time = TimeSpan.Zero;
                Length distance = Length.Zero;
                
                int entry_idx;
                // take all steps covering roundabout
                for (entry_idx = step_idx; entry_idx >= 0 && map.Roads[ pathSteps[entry_idx].IncomingRoadMapIndex].IsRoundabout; --entry_idx)
                {
                    var curr_step = pathSteps[entry_idx];
                    
                    if (condition != curr_step.IncomingCondition)
                        throw new NotSupportedException();
                    
                    time+=curr_step.IncomingTime;
                    distance+=curr_step.IncomingDistance;
                    
                }

                if (entry_idx == -1) // it could be that our leg start right at roundabout
                    entry_idx = 0;
                
                // entry_idx is first point of roundabout and step_idx is the last
                
                
                Angle lat = Angle.Zero;
                Angle lon = Angle.Zero;
                Length? alt = null;
                var nodes = roundabout_road.Nodes;
                foreach (var pt in nodes.Select(id => map.Nodes[id]).Skip(1)) // skip first one, because it is looped
                {
                    lat += pt.Latitude;
                    lon += pt.Longitude;
                    if (pt.Altitude.HasValue)
                        alt = (alt ?? Length.Zero) + pt.Altitude.Value;
                }

                var road_nodes_count = nodes.Count-1;
                lat /= road_nodes_count;
                lon /= road_nodes_count;
                if (alt.HasValue)
                alt = alt.Value/ road_nodes_count;

                distance /= 2;
                time /= 2;
                pathSteps[step_idx] = new StepRun(pathSteps[step_idx].Place, incoming_road_map_index, condition, distance, time);
                    // remove entire roundabout trip, keep entrance and exit
                pathSteps.RemoveRange(entry_idx+1,step_idx-entry_idx-1);
                pathSteps.Insert(entry_idx+1,new StepRun(Placement.Aggregate(GeoZPoint.Create(lat,lon,alt),incoming_road_map_index) , incoming_road_map_index, condition, 
                    distance,time));
             //   Console.WriteLine($"Roundabout with new step at {entry_idx+1} and replacement at {step_idx}");
                step_idx=entry_idx-1;
            }
          
            legCheck(pathSteps);
        }

        private void legCheck(IReadOnlyList<StepRun> pathSteps)
        {
                var init_step = pathSteps.First();
                if (init_step.IncomingDistance != Length.Zero)
                    throw new ArgumentOutOfRangeException($"Initial step is expected to be zero length, it is {init_step.IncomingDistance}."); }

        private IEnumerable<LegPlan> splitSteps(IReadOnlyList<StepRun> pathSteps)
        {
            legCheck(pathSteps);

            TimeSpan leg_time_limit;
            int expected_leg_pieces;
            {
                var time_total = pathSteps.Select(it => it.IncomingTime).Sum();
                expected_leg_pieces = (int) Math.Ceiling(time_total / this.userPlannerPrefs.CheckpointIntervalLimit);
                leg_time_limit = time_total / expected_leg_pieces;
                
                this.logger.Info($"Leg total time {time_total} with limit {this.userPlannerPrefs.CheckpointIntervalLimit} split into {expected_leg_pieces} pieces, each {leg_time_limit}");
            }

            var leg = new LegPlan() {AutoAnchored = false};
            TimeSpan running_time = TimeSpan.Zero;

            MapPoint? last_point = null;
            
            foreach (StepRun step in pathSteps)
            {
                GeoZPoint current_point = step.Place.Point;

                if (leg.Fragments.Any())
                {
                    if (leg.Fragments.Last().Places.Last().Point == current_point) // skip repeated points
                        continue;

                    // if the current leg part is too long, we have to return it and create another leg
                    if (expected_leg_pieces > 1 // remember, we will return tail leg at the end (out of the loop)
                        && running_time + step.IncomingTime > leg_time_limit)
                    {
                        --expected_leg_pieces;
                        leg.ComputeLegTotals();
                        logger.Info($"Returning split leg {leg.RawTime}");
                        yield return leg;

                        leg = new LegPlan() {AutoAnchored = true};
                        running_time = TimeSpan.Zero;
                    }
                }

                var speed_mode = step.IncomingCondition.Mode;

                if (!leg.Fragments.Any()
                    || (this.compactPreservesRoads && leg.Fragments.Last().RoadIds.Single() != step.IncomingRoadMapIndex)
                    || (!this.compactPreservesRoads && leg.Fragments.Last().Mode != speed_mode))
                {
                    var new_fragment = new LegFragment()
                    {
                        Risk = step.IncomingCondition.Risk,
                        IsForbidden = step.IncomingCondition.IsForbidden,
                    }
                        .SetSpeedMode(speed_mode)
                        ;

                    if (last_point is {} last_pt)
                    {
                        // we need to draw it, so the beginning of the new segment has to start where the last one ended
                        new_fragment.Places.Add(last_pt);
                    }

                    leg.Fragments.Add(new_fragment);
                }

                var fragment = leg.Fragments.Last();
                fragment.Places.Add(new MapPoint(current_point, step.Place.NodeId));
                fragment.RoadIds.Add(step.IncomingRoadMapIndex);
                fragment.RawTime += step.IncomingTime;
                fragment.UnsimplifiedDistance += step.IncomingDistance;
                // do not add initial distance (it is zero), we need to keep number of distances equal to
                // number of segments (point A - point B), not the points themselves (N points = N-1 segments)
                if (last_point!=null)  
                    fragment.StepDistances.Add(step.IncomingDistance);

                running_time += step.IncomingTime;

                last_point = fragment.Places.Last();
            }


            if (leg.Fragments.Any())
            {
                leg.ComputeLegTotals();
                logger.Info($"Returning last leg, auto = {leg.AutoAnchored}, time {leg.RawTime}");
                yield return leg;
            }
        }

        private TrackPlan splitLegs(IReadOnlyList<LegRun> pathLegs)
        {
            var route = new TrackPlan() {Legs = new List<LegPlan>()};

            foreach (var leg_run in pathLegs)
            {
                var plan_legs = splitSteps(leg_run.Steps);
                route.Legs.AddRange(plan_legs);
            }

            return route;
        }

    }
}