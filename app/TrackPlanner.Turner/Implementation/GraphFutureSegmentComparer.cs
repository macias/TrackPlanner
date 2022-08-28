using System;
using System.Collections.Generic;
using System.Linq;

namespace TrackPlanner.Turner.Implementation
{
    internal sealed class GraphFutureSegmentComparer : IEqualityComparer<GraphFutureSegment>
    {
        public bool Equals(GraphFutureSegment? a, GraphFutureSegment? b)
        {
            bool result = a!.Target == b!.Target && a.Current == b.Current;
            return result;
        }

        public int GetHashCode(GraphFutureSegment a)
        {
            return a.Current.GetHashCode() ^ a.Target.GetHashCode();
        }
    }
}