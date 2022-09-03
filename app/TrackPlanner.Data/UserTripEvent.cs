using MathUnit;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace TrackPlanner.Data
{
    public sealed class UserTripEvent
    {
        public string Label { get; set; } = "";
        public string ClassIcon { get; set; } = "";
        public int EveryDay { get; set; } = 1;
        public TimeSpan Duration { get; set; }
        public TimeSpan? Opportunity { get; set; }
        public TimeSpan? Interval { get; set; }
        public bool NearHomeEnabled { get; set; }
    }
}


