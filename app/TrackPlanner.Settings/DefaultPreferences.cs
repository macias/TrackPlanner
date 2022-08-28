using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TrackPlanner.Data;
using TrackPlanner.Data.Serialization;

namespace TrackPlanner.Settings
{
    public sealed class DefaultPreferences
    {
        public bool AutoBuild { get; set; }
        public bool CalcReal { get; set; }
        public bool LoopRoute { get; set; }
        public bool StartsAtHome { get; set; }
        public bool EndsAtHome { get; set; }

        public DefaultPreferences()
        {
            StartsAtHome = true;
            EndsAtHome = true;
        }
    }

}

/*
Access to fetch at 'http://localhost:8700/planner/plan-route' from origin 'http://localhost:5000' has been blocked by CORS policy: Response to preflight request doesn't pass
access control check: No 'Access-Control-Allow-Origin' header is present on the requested resource. If an opaque response serves your needs, set the request's mode to 'no-cors' 
to fetch the resource with CORS disabled.
*/
