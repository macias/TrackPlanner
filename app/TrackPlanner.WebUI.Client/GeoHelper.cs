using System;
using System.Linq;
using Geo;
using MathUnit;
using TrackPlanner.Data;
using TrackPlanner.Shared;
using TrackPlanner.LinqExtensions;

namespace TrackPlanner.WebUI.Client
{
    public static class GeoHelper
    {
        private static readonly IGeoCalculator calc = new ApproximateCalculator();
        
        public static void SnapToFragment(GeoPoint point, LegFragment fragment, out Length alongFragmentDistance)
        {
            var z_point = point.Convert();

            alongFragmentDistance = Length.Zero;

            var min_distance = Length.MaxValue;
            GeoZPoint DEBUG_segment_start = default;
            Length DEBUG_along_segment = default;
            var running_distance = Length.Zero;
            int idx = 0;
            foreach (var (prev, next) in fragment.Places.Slide())
            {
                (Length snap_dist, _, Length along) = calc.GetDistanceToArcSegment(z_point, prev.Point, next.Point);
                if (min_distance > snap_dist)
                {
                    min_distance = snap_dist;
                    alongFragmentDistance = along + running_distance;
                    DEBUG_along_segment = along;
                    DEBUG_segment_start = prev.Point;
                }

                running_distance += fragment.StepDistances[idx];
                ++idx;
                

            }
            Console.WriteLine($"SnapToFragment segment start {DEBUG_segment_start}, from fragment {fragment.Places.First().Point} to {fragment.Places.Last().Point}; {TrackPlanner.Data.DataFormat.Format(DEBUG_along_segment,false)} / {TrackPlanner.Data.DataFormat.Format(alongFragmentDistance,false)} / {TrackPlanner.Data.DataFormat.Format(running_distance,false)} / {TrackPlanner.Data.DataFormat.Format(fragment.UnsimplifiedDistance,false)}");
        }

       
    }
}
