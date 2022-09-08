using Geo;

#nullable enable

namespace TrackPlanner.Mapping
{
    public readonly struct HistoricObject
    {
        public long NodeId { get; }
        public string Name { get; }
        public string? Url { get; }
        public bool Ruins { get; }

        public HistoricObject(long nodeId, string name, string? url, bool ruins)
        {
            NodeId = nodeId;
            Name = name;
            Url = url;
            Ruins = ruins;
        }
    }
}