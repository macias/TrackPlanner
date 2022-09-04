using System;
using System.Collections.Generic;
using System.Linq;
using Geo;
using MathUnit;
using TrackPlanner.Data.Stored;
using TrackPlanner.Shared;
using TrackPlanner.LinqExtensions;

namespace TrackPlanner.Data
{
    public sealed class DraftHelper
    {
        private readonly IGeoCalculator calc;

        public DraftHelper(IGeoCalculator calc)
        {
            this.calc = calc;
        }
        private  void fillFragmentDistances(LegFragment fragment)
        {
            //for (int i = 1; i < segment.Points.Count; ++i)
            //  dist += calc.GetDistance(segment.Points[i-1],segment.Points[i]);
            foreach (var (prev, next) in fragment.Places.Slide())
            {
                var dist = calc.GetDistance(prev.Point, next.Point);
                fragment.StepDistances.Add(dist);
            }

            fragment.UnsimplifiedDistance = fragment.StepDistances.Sum();
        }

        
      /*  private void splitIntoPieces(LegFragment segment, int startIndex, UserPreferences userPrefs)
        {
            var dist = calc.GetDistance(segment.Places[startIndex].Point, segment.Places[startIndex+1].Point);
            var raw_time = dist / userPrefs.Speeds[segment.Mode];

            if (raw_time <= userPrefs.CheckpointIntervalLimit)
                return;

            var mid = this.calc.GetMidPoint(segment.Places[startIndex].Point, segment.Places[startIndex + 1].Point);
            segment.Places.Insert(startIndex+1,new MapPoint(mid,null));
            splitIntoPieces(segment,startIndex+1,userPrefs);
            splitIntoPieces(segment,startIndex,userPrefs);
        }*/
        
        private LegPlan buildDraftLeg(GeoPoint start, GeoPoint end, UserPlannerPreferences userPlannerPrefs)
        {
            var fragment = new LegFragment()
                {
                    Places = new List<MapPoint>()
                    {
                        new MapPoint(start.Convert(), null),
                        new MapPoint(end.Convert(), null),
                    },
                }
                .SetSpeedMode(SpeedMode.Ground);

           // splitIntoPieces(fragment, 0,userPrefs);

            fillFragmentDistances(fragment);
            
            fragment.RawTime = fragment.UnsimplifiedDistance / userPlannerPrefs.Speeds[fragment.Mode];

            var leg = new LegPlan()
            {
                IsDrafted = true,
                Fragments = new List<LegFragment>() {fragment},
            };
            
            leg.ComputeLegTotals();
            
            return leg;
        }


        public  TrackPlan BuildDraftPlan(PlanRequest request)
        {
            var result = new TrackPlan()
            {
                Legs = new List<LegPlan>(),
            };
            foreach (var (prev, next) in request.GetPointsSequence().Slide())
            {
                LegPlan leg = buildDraftLeg(prev.UserPoint, next.UserPoint, request.PlannerPreferences);
                result.Legs.Add(leg);
            }

            return result;
        }

        
    }
}
