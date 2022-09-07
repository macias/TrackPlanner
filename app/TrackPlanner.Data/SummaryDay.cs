using System;
using System.Collections.Generic;
using System.Linq;
using MathUnit;

namespace TrackPlanner.Data
{
    public sealed class SummaryDay
    {
        // we cannot use TimeOnly type because it is limited to 24 hours, and it is pretty valid case to
        // ride more in one take
        public TimeSpan Start { get; set; } // explicit data, because even without checkpoints we should know the start of the day 
        
        // for looped route we will get more checkpoints than anchors (by one)
        public List<SummaryCheckpoint> Checkpoints { get; set; }
        public TimeSpan TrueDuration => Checkpoints.Count==0? TimeSpan.Zero : this.Checkpoints[^1].Arrival - this.Checkpoints[0].Arrival;
        public Length Distance { get; set; }
        public TimeSpan? LateCampingBy { get; set; }
        public string? Problem { get; set; }

        public SummaryDay()
        {
            this.Checkpoints = new List<SummaryCheckpoint>();
        }

        public int[] GetEventCounters()
        {
            if (Checkpoints.Count == 0)
                return Array.Empty<int>();
            
            var buffer = Checkpoints[0].EventCounters.ToArray();
            foreach (var pt in Checkpoints.Skip(1))
            {
                for (int i = 0; i < buffer.Length; ++i)
                    buffer[i] += pt.EventCounters[i];
            }

            return buffer;
        }
    }
}
