using System;
using System.Collections.Generic;
using System.Linq;
using MathUnit;
using TrackPlanner.Shared;
using TrackPlanner.LinqExtensions;

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
        public EnumArray<TripEvent,int> EventCount { get; set; }
        public string Label { get; set; }

        public IEnumerable<(TripEvent kind,TimeSpan duration)> GetEvents(SummaryJourney summary) =>
            Enum.GetValues<TripEvent>().SelectMany(it => Enumerable.Range(0,this.EventCount[ it])
                    .Select(_ => (  it,summary.PlannerPreferences.EventDuration[it])))
            .OrderBy(it => it.Item1);
        
        public SummaryCheckpoint()
        {
            Label = "";
            this.EventCount = new EnumArray<TripEvent, int>();
        }

        public TimeSpan GetSnackTimeDuration(SummaryJourney summary)
        {
            return summary.PlannerPreferences.EventDuration[TripEvent.SnackTime]*this.EventCount[TripEvent.SnackTime];
        }
    }
}
