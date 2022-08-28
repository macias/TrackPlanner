using MathUnit;
using System;
using System.Collections.Generic;
using System.Linq;

using TrackPlanner.Turner.Implementation;
using TrackPlanner.Mapping;
using TrackPlanner.LinqExtensions;
using TrackPlanner.DataExchange;
using TrackPlanner.PathFinder;
using TrackPlanner.Shared;
using TrackPlanner.Data;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Turner.Implementation

{
    internal record struct TurnNotification
    {
        public static TurnNotification None => new TurnNotification(false, "");

        public bool Enable { get; }
        public string Reason { get; }
        
        public TurnNotification(bool enable, string reason)
        {
            Enable = enable;
            Reason = reason;
        }
    }
    
}
