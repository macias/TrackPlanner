using System.Collections.Generic;
using MathUnit;

namespace TrackPlanner.PathFinder
{
    public sealed class WeightComparer : IComparer<Weight>
    {
        private readonly double runningScale;
        private readonly double estimatedScale;
        private readonly Speed estimatedSpeed;
        private readonly bool useStableRoads;

        public WeightComparer(double runningScale, double estimatedScale,Speed estimatedSpeed,bool useStableRoads)
        {
            this.runningScale = runningScale;
            this.estimatedScale = estimatedScale;
            this.estimatedSpeed = estimatedSpeed;
            this.useStableRoads = useStableRoads;
        }

        public int Compare(Weight x, Weight y)
        {
            int comp;
            comp = x.CurrentForbiddenDistance.CompareTo(y.CurrentForbiddenDistance);
            if (comp != 0)
                return comp;
            if (this.useStableRoads)
            {
                comp = x.UnstableDistance.CompareTo(y.UnstableDistance);
                if (comp != 0)
                    return comp;
            }

            comp = GetTotalTimeCost(x).CompareTo(GetTotalTimeCost(y));
            if (comp != 0)
                return comp;

            return 0;
        }

        public TravelCost GetTotalTimeCost(Weight w)
        {
            return w.GetTotalTimeCost(this.runningScale,this.estimatedScale,this.estimatedSpeed);
        }
    }
}