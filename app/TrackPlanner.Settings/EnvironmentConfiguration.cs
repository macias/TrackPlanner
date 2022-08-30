using System;
using TrackPlanner.Data;
using TrackPlanner.Data.Serialization;

namespace TrackPlanner.Settings
{
  
    public sealed class EnvironmentConfiguration
    {
        public static string SectionName => "EnvironmentConfiguration";

        public string TileServer { get; set; }
        public string PlannerServer { get; set; }
        public TimeSpan PopupTimeout { get; set; }

        public UserPlannerPreferences PlannerPreferences { get; set; }
        public UserTurnerPreferences TurnerPreferences { get; set; }
        public UserVisualPreferences VisualPreferences { get; set; }
        public DefaultPreferences Defaults { get; set; } 
        

        public EnvironmentConfiguration()
        {
            this.Defaults = new DefaultPreferences();
            PopupTimeout = TimeSpan.FromSeconds(10);
            TileServer = "http://localhost:8600/tile/";
            PlannerServer = "http://localhost:8700/";
            this.PlannerPreferences = UserPlannerPreferencesHelper.CreateBikeOriented().SetCustomSpeeds();
            this.TurnerPreferences = new UserTurnerPreferences();
            this.VisualPreferences = new UserVisualPreferences();
        }

        public void Check()
        {
            if (VisualPreferences == null)
                throw new ArgumentNullException(nameof(VisualPreferences));

            VisualPreferences.Check();
        }

        public override string ToString()
        {
            return new ProxySerializer().Serialize(this);
        }
    }
}

/*
Access to fetch at 'http://localhost:8700/planner/plan-route' from origin 'http://localhost:5000' has been blocked by CORS policy: Response to preflight request doesn't pass
access control check: No 'Access-Control-Allow-Origin' header is present on the requested resource. If an opaque response serves your needs, set the request's mode to 'no-cors' 
to fetch the resource with CORS disabled.
*/
