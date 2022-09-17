using MathUnit;
using System;
using System.Collections.Generic;
using TrackPlanner.Shared;
using TrackPlanner.Mapping.Data;
using TrackPlanner.Structures;

namespace TrackPlanner.Mapping
{
    public sealed class TrueGridCell : RoadGridCell
    {
        private readonly CompactDictionaryFill<ushort,NodeInfo> nodes;

        public TrueGridCell(in CellIndex cellIndex, List<RoadIndexLong>? segmets = null) : base(cellIndex, segmets)
        {
            this.nodes = new CompactDictionaryFill<ushort, NodeInfo>();
        }


        public GeoZPoint GetPoint(ushort nodeIndex)
        {
            return nodes[nodeIndex].Point;
        }
        
        public IReadOnlyList<RoadIndexLong> GetRoads(ushort nodeIndex)
        {
            return nodes[nodeIndex].Roads;
        }

        public RoadInfo GetRoad(ushort roadIndex)
        {
            throw new NotImplementedException();
        }
    }
}