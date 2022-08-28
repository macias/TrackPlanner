using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrackPlanner.Shared;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Mapping
{

    public sealed class NodeRoadsLongDictionary : INodeRoadsDictionary
    {
        private readonly IReadOnlyList<int> offsets;
        private readonly IReadOnlyList<RoadIndexFlat> buffer;

        public IEnumerable<RoadIndexLong> this[long nodeIndex]
        {

            get
            {
                var offset = this.offsets[(int) nodeIndex];
                var length = nodeIndex == this.offsets.Count - 1 ? (this.buffer.Count - offset) : (this.offsets[(int) (nodeIndex + 1)] - offset);
                for (int i = length - 1; i >= 0; --i)
                {
                    yield return new RoadIndexLong(this.buffer[offset + i]);
                }
            }
        }

        public void Write(BinaryWriter writer,long nodeId)
        {
            throw new System.NotImplementedException();
        }

        public NodeRoadsLongDictionary(IReadOnlyArrayLong<RoadInfo> roads)
        {
            // roads can form strange loops like "q" shape (example: https://www.openstreetmap.org/way/23005989 )
            // or can have knots, example: https://www.openstreetmap.org/way/88373084

            var node_refs_count = roads.Sum(it => it.Value.Nodes.Count);
            var offsets = new int[node_refs_count];
            var lengths = new int[node_refs_count];
            var buffer = new RoadIndexFlat[node_refs_count];

            // computing how much roads fall use given node
            foreach ((_, RoadInfo info) in roads)
            {
                for (int i = 0; i < info.Nodes.Count; ++i)
                {
                    ++lengths[info.Nodes[i]];
                }
            }

            // computing offsets in buffer for every road
            {
                int offset = 0;

                for (int node_index = 0; node_index < lengths.Length; ++node_index)
                {
                    offsets[node_index] = offset;
                    offset += lengths[node_index];
                }
            }

            // fill the references 
            for (int road_index = 0; road_index < roads.Count; ++road_index)
            {
                RoadInfo info = roads[road_index];
                for (int i = 0; i < info.Nodes.Count; ++i)
                {
                    var node_index = info.Nodes[i];
                    --lengths[node_index];
                    buffer[offsets[node_index] + lengths[node_index]] = new RoadIndexFlat(road_index, i);
                }
            }

            this.offsets = offsets;
            this.buffer = buffer;
        }

    }

}