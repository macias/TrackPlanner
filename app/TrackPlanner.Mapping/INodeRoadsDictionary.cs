using System.Collections.Generic;
using System.IO;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Mapping
{
    public interface INodeRoadsDictionary
    {
        IEnumerable<RoadIndexLong> this[long nodeIndex] { get; }
        void Write(BinaryWriter writer,long nodeId);
    }

}