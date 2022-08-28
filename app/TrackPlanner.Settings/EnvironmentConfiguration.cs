using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TrackPlanner.Data;
using TrackPlanner.Data.Serialization;

namespace TrackPlanner.Settings
{
  
    public sealed class EnvironmentConfiguration
    {
        public static string SectionName => "EnvironmentConfiguration";
        public static string Filename => "clientsettings.json";

        public string TileServer { get; set; }
        public string PlannerServer { get; set; }
        public TimeSpan PopupTimeout { get; set; }

        public Dictionary<SpeedMode, LineDecoration> SpeedStyles { get; set; }
        public LineDecoration ForbiddenStyle { get; set; } = default!;
        public UserPlannerPreferences PlannerPreferences { get; set; }
        public UserTurnerPreferences TurnerPreferences { get; set; }
        public DefaultPreferences Defaults { get; set; } 
        

        public EnvironmentConfiguration()
        {
            this.Defaults = new DefaultPreferences();
            PopupTimeout = TimeSpan.FromSeconds(10);
            TileServer = "";
            PlannerServer = "";
            SpeedStyles = new Dictionary<SpeedMode, LineDecoration>();
            this.PlannerPreferences = UserPlannerPreferencesHelper.CreateBikeOriented().SetCustomSpeeds();
            this.TurnerPreferences = new UserTurnerPreferences();
        }

        public void Check()
        {
            if (SpeedStyles == null)
                throw new ArgumentNullException(nameof(SpeedStyles));

            string missing = String.Join(", ", Enum.GetValues<SpeedMode>().Where(it => !SpeedStyles.ContainsKey(it)));
            if (missing != "")
                throw new ArgumentOutOfRangeException($"Missing styles for: {missing}.");
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
