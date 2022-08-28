using System.Collections.Generic;
using System.Linq;

namespace TrackPlanner.Turner.Implementation

{
    internal sealed class GraphSegmentUsed
    {
        private readonly Dictionary<GraphBubble, Dictionary<GraphBubble, int>> data;
        public int Count { get; private set; }
        public IEnumerable<KeyValuePair< GraphFutureSegment,int>> Segments => this.data.SelectMany(it => it.Value.Select(sub => KeyValuePair.Create( new GraphFutureSegment(it.Key, sub.Key),sub.Value)));

        public int this[in GraphFutureSegment segment] => this.data[segment.Current][segment.Target];

        public GraphSegmentUsed()
        {
            this.data = new Dictionary<GraphBubble, Dictionary<GraphBubble, int>>();
        }

        internal void Add(in GraphFutureSegment segment)
        {
            if (!this.data.TryGetValue(segment.Current, out var sub))
            {
                sub = new Dictionary<GraphBubble, int>();
                this.data.Add(segment.Current, sub);
            }

            sub.Add(segment.Target, Count);
            ++Count;
        }

        internal bool ContainsKey(in GraphFutureSegment segment)
        {
            if (!this.data.TryGetValue(segment.Current, out var sub))
            {
                return false;
            }

            return sub.ContainsKey(segment.Target);
        }
    }


}