using System;
using System.Collections.Generic;
using System.Linq;

namespace TrackPlanner.Turner.Implementation
{
    internal sealed class GraphFutureSegment
    {
        private List<GraphBubble>? sources;

        public GraphBubble Current { get; }
        public GraphBubble Target { get; }

        public GraphFutureSegment(GraphBubble current, GraphBubble target)
        {
            Current = current;
            Target = target;
        }

        internal void Deconstruct(out GraphBubble current, out GraphBubble target)
        {
            current = this.Current;
            target = this.Target;
        }

        public override string ToString()
        {
            return $"{Current} -> {Target}";
        }

        internal void DEBUG_SetSources(List<GraphBubble> sources)
        {
            this.sources = sources;
        }

        internal void DEBUG_ValidateSources(List<GraphBubble> backtraceSources)
        {
            if (this.sources == null)
                throw new NullReferenceException("We don't have any sources set");

            if (!Enumerable.SequenceEqual(this.sources, backtraceSources))
                throw new Exception();
        }

        internal void DEBUG_ValidateSourcesAlongPath(Dictionary<GraphFutureSegment, (GraphPathWeight currentWeight, GraphPathWeight futureWeight, List<GraphBubble> source, long? incomingRoadId)> backtrace)
        {
            if (this.sources == null)
                throw new NullReferenceException("We don't have any sources set");

            var curr_sources = this.sources.ToList();
            var curr = this;
            while (curr_sources.Any())
            {
                var back_sources = backtrace[curr].source;
                if (!Enumerable.SequenceEqual(curr_sources, back_sources))
                    throw new Exception();
                curr = new GraphFutureSegment(curr_sources.Last(), curr.Current);
                curr_sources = curr_sources.SkipLast(1).ToList();
            }
            if (curr.Current.BubbleKind != GraphBubble.Kind.Final)
                throw new Exception();
        }
    }

}