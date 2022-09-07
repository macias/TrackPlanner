using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TrackPlanner.LinqExtensions;

namespace TrackPlanner.Data.Stored
{
    public sealed class ScheduleJourney :ISchedule<ScheduleDay,ScheduleAnchor>
    {
        public string Comment { get; set; } = "";
        public TrackPlan TrackPlan { get; set; } = default!;
        [Obsolete("2022-07-24: use Days")]
        public ScheduleDay[] Schedule {
            set
            {
                if (value != null)
                    Days = value.ToList();
            }
        }
        public bool IsLooped { get; set; }
        public bool StartsAtHome { get; set;  }
        public bool EndsAtHome { get; set;  }
        IReadOnlyList<IReadOnlyDay> IReadOnlySchedule.Days => this.Days;
        public List<ScheduleDay> Days { get; set; } = default!;
        public UserPlannerPreferences PlannerPreferences { get; set; } = default!;
        public UserTurnerPreferences TurnerPreferences { get; set; } = default!;
        public UserVisualPreferences VisualPreferences { get; set; } = default!;
        public UserRouterPreferences RouterPreferences { get; set; } = default!;
        
      //  public IScheduleLike Interface => this;

        public ScheduleJourney()
        {
            this.Days = new List<ScheduleDay>();
        }
        
        public PlanRequest BuildPlanRequest()
        {
            var daily_points = new List<List< RequestPoint>>();
            for (int day_idx = 0; day_idx < Days.Count; ++day_idx)
            {
                var points = new List<RequestPoint>();
                for (int anchor_idx = 0; anchor_idx < Days[day_idx].Anchors.Count; ++anchor_idx)
                {
                    this.GetAnchorDetails(day_idx,anchor_idx,out _,out var is_last_of_day);
                    bool allow_smoothing = !is_last_of_day && !(day_idx == 0 && anchor_idx == 0);
                    points.Add(new RequestPoint( this.Days[day_idx].Anchors[anchor_idx].UserPoint, allow_smoothing));
                }
                
                daily_points.Add(points);
            }
            if (this.IsLooped && daily_points.SelectMany(x => x).Any())
                daily_points[^1].Add(daily_points[0].First());

            var plan_request = new PlanRequest()
            {
                DailyPoints = daily_points,
                TurnerPreferences = TurnerPreferences,
                RouterPreferences = RouterPreferences,
                
            };
            
            return plan_request;
        }
        
    }
}