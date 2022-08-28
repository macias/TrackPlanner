using System;
using System.Collections.Generic;
using System.Linq;
using TrackPlanner.LinqExtensions;

namespace TrackPlanner.PathFinder
{
    internal sealed class CompStatistics
    {
        public int RejectedNodes { get; set; }

        // how many nodes we have per given degree
        // degree 0 -- it is isolated node
        // degree 1 -- it is end of the road 
        private readonly List<int> nodeDegreeCounts;
        public int ForwardUpdateCount { get; set; }
        public int BackwardUpdateCount { get; set; }
        public int SuccessExactTarget { get; set; }
        public int FailedExactTarget { get; set; }

        public CompStatistics()
        {
            this.nodeDegreeCounts = new List<int>();
        }

        public void AddNode(int degree)
        {
            this.nodeDegreeCounts.Expand(degree + 1);
            ++this.nodeDegreeCounts[degree];
        }

        public override string ToString()
        {
            return $"{nameof(RejectedNodes)} = {RejectedNodes}, {nameof(nodeDegreeCounts)} = {String.Join(", ", this.nodeDegreeCounts.ZipIndex().Where(it => it.item > 0).Select(it => $"{it.index}: {it.item}"))}";
        }
    }

}