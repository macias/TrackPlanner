using System;
using System.Collections.Generic;
using System.Linq;
using MathUnit;

namespace TrackPlanner.Data
{
    public sealed class TrackPlan
    {
        internal static Length ValidationLengthErrorLimit => Length.FromCentimeters(1);
        
        public List<LegPlan> Legs { get; set; }
        public List<List<TurnInfo>> DailyTurns { get; set; } // turns within days
        public bool IsEmpty => this.Legs.Count == 0;
        
        public TrackPlan()
        {
            this.Legs = new List<LegPlan>();
            this.DailyTurns = new List<List<TurnInfo>>();
        }

        public string? DEBUG_Validate()
        {
            for (int i=0;i<this.Legs.Count;++i)
            {
              var failure =  Legs[i].DEBUG_Validate(legIndex:i);
              if (failure != null)
                  return failure;
              
              if (i > 0 && this.Legs[i - 1].Fragments.Last().Places.Last().Point != this.Legs[i].Fragments.First().Places.First().Point)
                  return $"We have gap between leg {i-1} and {i}.";
            }

            return null;
        }

        public LegPlan GetLeg(int legIndex,string? DEBUG_context = null)
        {
            if (legIndex < 0 || legIndex >= this.Legs.Count)
                throw new ArgumentOutOfRangeException($"{nameof(legIndex)}={legIndex} when having {this.Legs.Count} legs ({DEBUG_context}).");

            return this.Legs[legIndex];
        }

    }
}
