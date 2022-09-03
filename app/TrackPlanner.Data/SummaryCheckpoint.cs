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
        public int[] UserEventsCounter { get; set; }
        public string Label { get; set; }

        public IEnumerable<(string label, string iconClass, TimeSpan duration)> GetEvents(SummaryJourney summary) =>
            Enum.GetValues<TripEvent>().SelectMany(it => Enumerable.Range(0,this.EventCount[ it])
                    .Select(_ => (  it.GetLabel(),it.GetClassIcon(), summary.PlannerPreferences.EventDuration[it])))
                .Concat(Enumerable.Range(0,UserEventsCounter.Length).SelectMany(it => Enumerable.Range(0,this.UserEventsCounter[ it])
                    .Select(_ => (  summary.PlannerPreferences.UserEvents[it].Label,IconClass: summary.PlannerPreferences.UserEvents[it].ClassIcon, 
                        summary.PlannerPreferences.UserEvents[it].Duration))))
            .OrderBy(it => it.Item1);
        
        public SummaryCheckpoint(int eventsCount)
        {
            Label = "";
            this.EventCount = new EnumArray<TripEvent, int>();
            this.UserEventsCounter = new int[eventsCount];
        }

        public TimeSpan GetSnackTimeDuration(SummaryJourney summary)
        {
            return summary.PlannerPreferences.EventDuration[TripEvent.SnackTime]*this.EventCount[TripEvent.SnackTime];
        }
    }
}
