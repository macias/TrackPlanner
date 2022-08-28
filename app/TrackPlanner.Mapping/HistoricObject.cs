using Geo;

#nullable enable

namespace TrackPlanner.Mapping
{
    public readonly struct HistoricObject
    {
        public GeoPoint Location { get; }
        public string Name { get; }
        public string Url { get; }
        public bool Ruins { get; }

        public HistoricObject(GeoPoint location, string name, string url, bool ruins)
        {
            Location = location;
            Name = name;
            Url = url;
            Ruins = ruins;
        }
    }
}