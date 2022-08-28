using System;
using System.Collections;
using System.Collections.Generic;

namespace TrackPlanner.PathFinder
{
    public readonly record struct PathConstraints
    {
        public Weight? WeightLessThan { get; init; }
        public IReadOnlySet<long>? ExcludedNodes { get; init; }

        public PathConstraints()
        {
            this.WeightLessThan = null;
            ExcludedNodes = null;
        }

        public bool IsAcceptable(long nodeId)
        {
            return this.ExcludedNodes == null || !this.ExcludedNodes.Contains(nodeId);
        }
    }
}