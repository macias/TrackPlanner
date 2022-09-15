using System.Collections.Generic;
using TrackPlanner.Data;
using TrackPlanner.Data.Stored;
using TrackPlanner.Settings;

namespace TrackPlanner.Logic.Tests.Data
{
    public class DummyReadOnlySchedule : IReadOnlySchedule
    {
        public bool IsLooped { get; set; }
        public bool StartsAtHome { get; set; }
        public bool EndsAtHome { get; set; }
        IReadOnlyList<IReadOnlyDay> IReadOnlySchedule.Days => this.Days;
        public List<DummyDay> Days { get; set; }
        public TrackPlan TrackPlan { get; set; }
        public UserPlannerPreferences PlannerPreferences { get; set; }
        public UserTurnerPreferences TurnerPreferences { get; set; }
        public UserRouterPreferences RouterPreferences { get; set; }

        public DummyReadOnlySchedule()
        {
            this.Days = new List<DummyDay>();
            this.TrackPlan = new TrackPlan();
            this.RouterPreferences = new UserRouterPreferences().SetCustomSpeeds();
            this.TurnerPreferences = new UserTurnerPreferences();
            this.PlannerPreferences = new UserPlannerPreferences();
        }
    }
}