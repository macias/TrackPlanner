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

        public DummyReadOnlySchedule()
        {
            this.PlannerPreferences = new UserPlannerPreferences().SetCustomSpeeds();
            this.Days = new List<DummyDay>();
            this.TrackPlan = new TrackPlan();
            this.TurnerPreferences = new UserTurnerPreferences();
        }
    }
}