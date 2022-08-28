using System;
using System.Collections.Generic;
using MathUnit;

namespace TrackPlanner.Data
{
    public interface IReadOnlySchedule
    {
        bool IsLooped { get; }
        bool StartsAtHome { get; } // camp/tent related things are not an issue
        bool EndsAtHome { get; } 
        IReadOnlyList<IReadOnlyDay> Days { get; }
        TrackPlan TrackPlan { get; }
        UserPlannerPreferences PlannerPreferences { get; }
        UserTurnerPreferences TurnerPreferences { get; }
    }

    public interface ISchedule<TDay,TAnchor> : IReadOnlySchedule
    where TDay:IDay<TAnchor>
    where TAnchor:IAnchor
    {
        new List<TDay> Days { get; }
    }
}