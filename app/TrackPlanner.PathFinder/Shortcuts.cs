using MathUnit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TrackPlanner.Mapping;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.PathFinder
{
    
    public sealed class Shortcuts
    {
        /*private readonly Dictionary<long, Dictionary<RoadIndexLong, Shortcut>> arcs;

        public Shortcuts()
        {
            this.arcs = new Dictionary<long, Dictionary<RoadIndexLong, Shortcut>>();
        }

        public void Add(long sourceNodeId, RoadIndexLong target, Shortcut shortcut)
        {
            if (!this.arcs.TryGetValue(sourceNodeId, out var targets))
            {
                targets = new Dictionary<RoadIndexLong, Shortcut>();
                this.arcs.Add(sourceNodeId, targets);
            }

            targets.Add(target, shortcut);
        }

        public IEnumerable<(RoadIndexLong target, Shortcut shortcut)> GetShortcuts(long nodeId)
        {
            if (!this.arcs.TryGetValue(nodeId, out var targets))
                return Enumerable.Empty<(RoadIndexLong, Shortcut )>();

            return targets.Select(it => (it.Key, it.Value));
        }*/
    }

}