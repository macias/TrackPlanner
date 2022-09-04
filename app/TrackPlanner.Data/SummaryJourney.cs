using System;
using System.Collections.Generic;
using System.Linq;
using MathUnit;
using TrackPlanner.Data.Stored;
using TrackPlanner.Shared;

namespace TrackPlanner.Data
{
    public sealed class SummaryJourney
    {
        private readonly Lazy<Length> distance;
        public Length Distance => this.distance.Value;
        public List<SummaryDay> Days { get; set; }
        public UserPlannerPreferences PlannerPreferences { get; init; } = default!;

        public SummaryJourney()
        {
            this.Days = new List<SummaryDay>();
            this.distance = new Lazy<Length>(() => this.Days.Select(it => it.Distance).Sum());
        }
    }
}
