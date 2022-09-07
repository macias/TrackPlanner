using System;

namespace TrackPlanner.Data.Stored
{
    public sealed class TripEvent
    {
        public string Label { get; set; } = "";
        private string? category;
        public string Category
        {
            get { return this.category ?? Label; }
            set { this.category = value; }
        }
        public string ClassIcon { get; set; } = "";
        public int EveryDay { get; set; } = 1;
        public TimeSpan Duration { get; set; }
        public bool SkipBeforeHome { get; set; }
        public bool SkipAfterHome { get; set; }
        // only one can be set, if both are not set, the system assume the event is one time only (per day)
        // within the same category, interval-based events have lower priority
        public TimeSpan? Interval { get; set; }
        // double meaning of zero and null -- both mean start as soon as possible, but null should not start the day
        public TimeSpan? ClockTime { get; set; }


    }
}


