
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
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
            Grave = 1 << 4,
            Church = 1 << 5,
            Mansion = 1 << 6,
            Lighthouse = 1 << 7,
            Ship = 1 << 8,
            Bunker = 1 << 9,
            Palace = 1 << 10,
            Tower = 1 << 11,
            Chapel = 1 << 12,
            Fortress = 1 << 13,
            Fortification = 1 << 14,
            Bridge = 1 << 15,
            CityWalls = 1 << 16,
            TrainStation = 1 << 17,
            Mill = 1 << 18,
            Pillory = 1 << 19,
            Mine = 1 << 20,
            Aircraft = 1 << 21,
            Aqueduct = 1 << 22,
            Train = 1 << 23,
            Tank = 1 << 24,
        }

        public string? Name { get; }
        public string? Url { get; }
        public Feature Features { get; }

        public TouristAttraction(string? name, string? url, Feature features)
        {
            Name = name;
            Url = url;
            Features = features;
        }

        [Pure]
        public IEnumerable<string> GetFeatures()
        {
            var this_features = this.Features;
            return Enum.GetValues<Feature>()
                .Where(it => it != Feature.None && this_features.HasFlag(it))
                .Select(it => it.ToString());
        }
    }
}