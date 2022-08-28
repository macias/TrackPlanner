using System;
using System.Collections.Generic;
using System.Linq;
using Geo;
using MathUnit;
using TrackPlanner.Shared;

namespace TrackPlanner.Data
{
    // leg are the segments from anchor to anchor
    public sealed class LegPlan
    {
        public static LegPlan Missing { get; } = new() {IsDrafted = true};

        public List<LegFragment> Fragments { get; set; }

        public Length UnsimplifiedDistance { get; set; }
        public TimeSpan RawTime { get; set; }
        public bool IsDrafted { get; set; }
        public bool AutoAnchored { get; set; }

        public LegPlan()
        {
            this.Fragments = new List<LegFragment>();
        }

      /*  public LegPlan CreateStub()
        {
            var stub = new LegPlan() {IsStub = true};
            var fragment = new LegFragment();
            fragment.Places.Add(this.Fragments.First().Places.First());
            fragment.Places.Add(this.Fragments.Last().Places.Last());
            stub.Fragments.Add(fragment);
            return stub;
        }*/

        public string? DEBUG_Validate(int legIndex)
        {
            var distances = Length.Zero;
            for (int i=0;i<Fragments.Count;++i)
            {
                var fragment = this.Fragments[i];
                
                distances += fragment.UnsimplifiedDistance;
                var failure = fragment.DEBUG_Validate(legIndex,i);
                if (failure != null)
                    return failure;

                if (i > 0 && Fragments[i - 1].Places.Last().Point != fragment.Places.First().Point)
                    return $"We have gap between fragments {i-1} and {i} at leg {legIndex}.";
            }

            if ((distances - UnsimplifiedDistance).Abs() > TrackPlan.ValidationLengthErrorLimit)
                return ($"{this}.{nameof(UnsimplifiedDistance)}={UnsimplifiedDistance} when sum of elements = {distances}");

            return null;
        }

        public void ComputeLegTotals()
        {
            var distance = Length.Zero;
            var time = TimeSpan.Zero;
            
            foreach (var fragment in Fragments)
            {
                distance += fragment.UnsimplifiedDistance;
                time += fragment.RawTime;
            }

            this.UnsimplifiedDistance = distance;
            this.RawTime = time;
        }

        public bool NeedsRebuild(bool calcReal)
        {
            return this== Missing || (this.IsDrafted && calcReal);
        }
    }
}
