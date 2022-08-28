using System.Collections.Generic;

#nullable enable

namespace TrackPlanner.Mapping.Data
{
    public readonly struct RelationInfo
    {
        public string Name { get; }
        public long Id { get; }
        public List<long> WayNodes { get; }

        public RelationInfo(string name, long id, List<long> wayNodes)
        {
            Name = name;
            Id = id;
            this.WayNodes = wayNodes;
        }
    }
}