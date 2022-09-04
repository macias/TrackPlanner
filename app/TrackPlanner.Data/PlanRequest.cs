using System.Collections.Generic;
using System.Linq;
using Geo;
using TrackPlanner.Data.Stored;
using TrackPlanner.LinqExtensions;

namespace TrackPlanner.Data
{
    public sealed class PlanRequest
    {
        // the end of the given list is NOT repeated as the start of the following list
        public List<List<RequestPoint>> DailyPoints { get; set; } = default!; 
        public UserPlannerPreferences PlannerPreferences { get; set; } = default!;
        public UserTurnerPreferences TurnerPreferences { get; set; } = default!;

        public PlanRequest()
        {
        }

        public IEnumerable<RequestPoint> GetPointsSequence()
        {
            return DailyPoints.SelectMany(x => x);
        }
    }
}
