using System;
using System.Collections.Generic;
using System.Linq;
using MathUnit;

namespace TrackPlanner.Data.Stored
{
    public sealed class ScheduleDay : IDay<ScheduleAnchor>
    {
        [Obsolete("2022-07-24 use Anchors")]
        public ScheduleAnchor[] Checkpoints {
            set
            {
                if (value != null)
                    Anchors = value.ToList();
            }
        } 
        public TimeSpan Start { get; set; }

        public List<ScheduleAnchor> Anchors { get; set; } = default!;
        IReadOnlyList<IReadOnlyAnchor> IReadOnlyDay.Anchors => this.Anchors;
        
        public ScheduleDay()
        {
            this.Anchors = new List<ScheduleAnchor>();
        }

    }
}