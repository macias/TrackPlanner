using System;
using System.Collections.Generic;
using System.Linq;
using MathUnit;
using TrackPlanner.Data.Stored;

namespace TrackPlanner.Data
{
    public sealed class SummaryCheckpoint
    {
        public TimeSpan Arrival { get; set; }
        public TimeSpan Departure { get; set; }
        public Length IncomingDistance { get; set; }
        public TimeSpan Break { get; set; }
        public TimeSpan RollingTime { get; set; }
        public int? IncomingLegIndex { get; set; }
        public bool IsLooped { get; set; }
        public int[] EventCounters { get; set; }
        public string Label { get; set; }

        public IEnumerable<(string label, string iconClass, TimeSpan duration)> GetAtomicEvents(SummaryJourney summary) =>
            Enumerable.Range(0,EventCounters.Length).SelectMany(it => Enumerable.Range(0,this.EventCounters[ it])
                    .Select(_ => (  summary.PlannerPreferences.TripEvents[it].Label,IconClass: summary.PlannerPreferences.TripEvents[it].ClassIcon, 
                        summary.PlannerPreferences.TripEvents[it].Duration)))
            .OrderBy(it => it.Item1);
        
        public SummaryCheckpoint(int eventsCount)
        {
            Label = "";
            this.EventCounters = new int[eventsCount];
        }
    }
}
