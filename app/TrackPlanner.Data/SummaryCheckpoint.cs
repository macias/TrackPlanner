using System;
using System.Collections.Generic;
using System.Linq;
using MathUnit;
using TrackPlanner.Shared;
using TrackPlanner.LinqExtensions;

namespace TrackPlanner.Data
{
    public enum TripEvent
    {
        SnackTime,
        Resupply,
        Lunch,
        Laundry
    }
    public sealed class SummaryCheckpoint
    {
        public TimeSpan Arrival { get; set; }
        public TimeSpan Departure { get; set; }
        public Length IncomingDistance { get; set; }
        public TimeSpan Break { get; set; }
        public TimeSpan RollingTime { get; set; }
        public int? IncomingLegIndex { get; set; }
        public bool IsLooped { get; set; }
        public TimeSpan? LaundryAt { get; set; }
        public TimeSpan? LunchAt { get; set; }
        public TimeSpan? CampRessuplyAt { get; set; }
        public List<TimeSpan> SnackTimesAt { get; set; }
        public string Label { get; set; }

        public IEnumerable<(TimeSpan time, TripEvent kind)> Events => this.SnackTimesAt.Select(it => ( (TimeSpan?) it,  Resupply: TripEvent.SnackTime))
            .Concat((this.LaundryAt, TripEvent.Laundry), (this.LunchAt,  TripEvent.Lunch),( this.CampRessuplyAt,  TripEvent.Resupply))
            .Where(it => it.Item1.HasValue)
            .Select(it => (time: it.Item1!.Value, it.Item2))
            .OrderBy(it => it.time);
        
        public SummaryCheckpoint()
        {
            Label = "";
            SnackTimesAt = new List<TimeSpan>();
        }

        public TimeSpan GetSnackTimeDuration(SummaryJourney summary)
        {
            return summary.PlannerPreferences.SnackTimeDuration * this.SnackTimesAt.Count;
        }
    }
}
