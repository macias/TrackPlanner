using System;
using System.Collections.Generic;
using MathUnit;
using TrackPlanner.Data.Stored;
using TrackPlanner.Shared;

namespace TrackPlanner.Data
{
    public sealed class SummaryDay
    {
        // we cannot use TimeOnly type because it is limited to 24 hours, and it is pretty valid case to
        // ride more in one take
        public TimeSpan Start { get; set; } // explicit data, because even without checkpoints we should know the start of the day 
        
        // for looped route we will get more checkpoints than anchors (by one)
        public List<SummaryCheckpoint> Checkpoints { get; set; }
        public TimeSpan TrueDuration => Checkpoints.Count==0? TimeSpan.Zero : this.Checkpoints[^1].Arrival - this.Checkpoints[0].Arrival;
        public Length Distance { get; set; }
        public TimeSpan? LateCampingBy { get; set; }
        public EnumArray<TripEvent,TimeSpan> EventDuration { get; set; }
        public TimeSpan[] UserEventDuration { get; set; }

        public SummaryDay(int eventsCount)
        {
            this.Checkpoints = new List<SummaryCheckpoint>();
            this.EventDuration = new EnumArray<TripEvent, TimeSpan>();
            this.UserEventDuration = new TimeSpan[eventsCount];
        }

    }
}
