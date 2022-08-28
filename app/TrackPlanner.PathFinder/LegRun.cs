using MathUnit;
using System;
using System.Collections.Generic;

namespace TrackPlanner.PathFinder
{
    public record  struct LegRun
    {
        public List<StepRun> Steps { get; }

        public LegRun(List<StepRun> steps)
        {
            Steps = steps;
        }
    }
    
}