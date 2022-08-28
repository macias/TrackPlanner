using System;
using MathUnit;

namespace TrackPlanner.Data
{
    public sealed class SaveRequest
    {
        public ScheduleJourney Schedule { get; set; } = default!;
        public string Path { get; set; } = default!;

        public SaveRequest()
        {
            
        }
    }

}