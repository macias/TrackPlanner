
using System;
using System.Collections.Generic;
using System.Linq;

namespace TrackPlanner.Mapping
{
    public record struct TouristAttraction
    {
        [Flags]
        public enum Feature
        {
            None = 0,
            Ruins = 1 << 0,
            ArchaeologicalSite = 1 << 1,
            Manor = 1 << 2,
            Castle = 1 << 3,
            Tomb = 1 << 4,
            Church = 1 << 5,
            Mansion = 1 << 6,
            Lighthouse= 1 << 7,
            Wreck= 1 << 8,
            Bunker= 1 << 9,
            Palace= 1 << 10,
            Tower= 1 << 11,
            Chapel= 1 << 12,
        }
        
        public long NodeId { get; }
        public string? Name { get; }
        public string? Url { get; }
        public Feature Features{ get; }

        public TouristAttraction(long nodeId, string? name, string? url, Feature features)
        {
            NodeId = nodeId;
            Name = name;
            Url = url;
            Features = features;
        }

        public IEnumerable<string> GetFeatures()
        {
            var this_features = this.Features;
            return Enum.GetValues<Feature>()
                .Where(it => it != Feature.None && this_features.HasFlag(it))
                .Select(it => it.ToString());
        }
    }
}