using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using MathUnit;
using TrackPlanner.Shared;
using TrackPlanner.Mapping.Data;
using TrackPlanner.Structures;

namespace TrackPlanner.Mapping
{
    // 8GB robocza, 10B peak, 19 sekund wczytanie
    public sealed class NodeRoadsAssocDictionary : INodeRoadsDictionary
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public readonly struct SliceIndex
        {
            public int Offset { get; }
            public byte Length { get; }

            public SliceIndex(int offset, byte length)
            {
                Offset = offset;
                Length = length;
            }
        }

        private readonly IReadOnlyDictionary<long, SliceIndex> coordinates;
        private readonly RoadIndexLong[] buffer;

        public IEnumerable<RoadIndexLong> this[long nodeId]
        {

            get
            {
                var coords = this.coordinates[nodeId];
                for (int i = coords.Length - 1; i >= 0; --i)
                {
                    yield return this.buffer[coords.Offset + i];
                }
            }
        }

        public void Write(BinaryWriter writer,long nodeId)
        {
            writer.Write(this.coordinates[nodeId].Length);
            
            foreach (var index in this[nodeId])
            {
                index.Write(writer);
            }
        }

        public NodeRoadsAssocDictionary(IReadOnlyMap<long, GeoZPoint> nodes,
            IReadOnlyMap<long, RoadInfo> roads)
        {
            // roads can form strange loops like "q" shape (example: https://www.openstreetmap.org/way/23005989 )
            // or can have knots, example: https://www.openstreetmap.org/way/88373084

            var back_refs = nodes.ToDictionary(it => it.Key, _ => default(SliceIndex));
            var buffer = new RoadIndexLong[roads.Sum(it => it.Value.Nodes.Count)];

            foreach ((_, RoadInfo info) in roads)
            {  
                for (int i = 0; i < info.Nodes.Count; ++i)
                {
                    var coords = back_refs[info.Nodes[i]];
                    back_refs[info.Nodes[i]] = new SliceIndex(coords.Offset,(byte)( coords.Length + 1));
                }
            }

            {
                int offset = 0;

                foreach (var node_id in back_refs.Keys.ToArray())
                {
                    var coords = back_refs[node_id];
                    offset += coords.Length;
                    back_refs[node_id] = new SliceIndex(offset, coords.Length);
                }
            }

            foreach ((long road_map_index, RoadInfo info) in roads)
            {
                for (int i = 0; i < info.Nodes.Count; ++i)
                {
                    var coords = back_refs[info.Nodes[i]];
                    coords = new SliceIndex (coords.Offset - 1, coords.Length);
                    back_refs[info.Nodes[i]] = coords;
                    buffer[coords.Offset] = new RoadIndexLong(road_map_index, i);
                }
            }

            this.coordinates = back_refs;
            this.buffer = buffer;
        }

    }

}