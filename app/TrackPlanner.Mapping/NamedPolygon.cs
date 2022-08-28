using System;
using System.Collections.Generic;

namespace TrackPlanner.Mapping
{
    internal readonly struct NamedPolygon
    {
        public IReadOnlyList<long> Nodes { get; }
        public long Id { get; }
        public string Name { get; }

        public NamedPolygon(long id, string name, IReadOnlyList<long> nodes)
        {
            Id = id;
            Name = name;
            this.Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        }
    }
}