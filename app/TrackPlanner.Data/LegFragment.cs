using MathUnit;
using System;
using System.Collections.Generic;
using System.Linq;
using TrackPlanner.Shared;
using TrackPlanner.LinqExtensions;

namespace TrackPlanner.Data
{
    public sealed class LegFragment
    {
        public Risk Risk { get; set; }
        public SpeedMode Mode { get; set; }
        public bool IsForbidden { get; set; }
        public List<MapPoint> Places { get; set; }
        // count(steps) is equal to count(places) - 1
        public List<Length> StepDistances { get; set; }
        public Length UnsimplifiedDistance { get; set; } // distance before points removal (simplification)
        public TimeSpan RawTime { get; set; }
        public HashSet<long> RoadIds { get; set; }

        public LegFragment()
        {
            this.Places = new List<MapPoint>();
            this.RoadIds = new HashSet<long>();
            this.StepDistances = new List<Length>();
        }

        public LegFragment SetSpeedMode(SpeedMode mode)
        {
            this.Mode = mode;
            return this;
        }

        public string? DEBUG_Validate(int legIndex,int fragmentIndex)
        {
            var distances = Length.Zero;
            foreach (var step_dist in StepDistances)
            {
                distances += step_dist;
            }

           // if ((distances - UnsimplifiedDistance).Abs() > TrackPlan.LengthErrorLimit)
             //   throw new InvalidOperationException($"{this}.{nameof(UnsimplifiedDistance)}={UnsimplifiedDistance} when sum of elements = {distances}");

            var segment_count = this.Places.Slide().Count();
            
            if (segment_count!= StepDistances.Count)
            return $"Number of step-segments {segment_count} is not equal to number of step distances {this.StepDistances.Count}";

            if (Places.Count <= 1)
                return $"Fragment {fragmentIndex} at {legIndex} leg contains less than 2 points.";
            
            return null;
        }
    }
}