using System.Collections.Generic;
using System.Linq;
using TrackPlanner.Data;
using TrackPlanner.Data.Stored;

namespace TrackPlanner.WebUI.Client.Data
{
    public class VisualSchedule<TAnchorVisual,TDayVisual> 
    {
        public UserPlannerPreferences PlannerPreferences { get; }
        public UserTurnerPreferences TurnerPreferences { get; }
        public UserVisualPreferences VisualPreferences { get; }
        public List<VisualDay<TAnchorVisual,TDayVisual>> Days { get;  }
        public bool StartsAtHome { get; set; }
        public bool EndsAtHome { get; set; }

        private bool isModified;
        public bool IsModified {
            get
            {
                if (!this.isModified && this.Days.Any(it => it.IsModified))
                    this.isModified = true;
                return this.isModified;
            }
            set
            {
                this.isModified = value;
                if (!value)
                    foreach (var day in Days)
                        day.IsModified = false;
                    {
                        
                    }
            }
        }

        public VisualSchedule(UserPlannerPreferences plannerPreferences,UserTurnerPreferences turnerPreferences,UserVisualPreferences visualPreferences)
        {
            PlannerPreferences = plannerPreferences;
            TurnerPreferences = turnerPreferences;
            VisualPreferences = visualPreferences;
            this.Days = new List<VisualDay<TAnchorVisual, TDayVisual>>();
        }

    }
}