using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TrackPlanner.PathFinder
{
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct CostPath
    {
        public IReadOnlyList<StepRun> Steps { get; }
        public TravelCost RunCost { get; }

        public CostPath(IReadOnlyList<StepRun> steps, TravelCost runCost)
        {
            Steps = steps;
            RunCost = runCost;
        }
    }
}
