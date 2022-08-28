using System;
using System.Collections.Generic;
using MathUnit;
using TrackPlanner.Shared;

namespace TrackPlanner.Data
{
    public sealed class SummaryDay
    {
        public TimeSpan Start { get; set; } // explicit data, because even without checkpoints we should know the start of the day 
        // for looped route we will get more checkpoints than anchors (by one)
        public List<SummaryCheckpoint> Checkpoints { get; set; }
        public TimeSpan TrueTimeDuration => Checkpoints.Count==0? TimeSpan.Zero : this.Checkpoints[^1].Arrival - this.Checkpoints[0].Arrival;
        public Length Distance { get; set; }
        public TimeSpan? LateCampingBy { get; set; }
        public TimeSpan CampResupplyDuration { get; set; }
        public TimeSpan SnackTimesDuration { get; set; }
        public TimeSpan LaundryDuration { get; set; }
        public TimeSpan LunchDuration { get; set; }

        public SummaryDay()
        {
            this.Checkpoints = new List<SummaryCheckpoint>();
        }

    }
}
