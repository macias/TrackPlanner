using System.Collections.Generic;
using OsmSharp;
using OsmSharp.Streams;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Geo;
using MathUnit;
using TrackPlanner.Data;
using TrackPlanner.LinqExtensions;
using TrackPlanner.Mapping.Data;
using TrackPlanner.Shared;
using TrackPlanner.Structures;

namespace TrackPlanner.Mapping
{
    public sealed class OsmCollector
    {
        private readonly ILogger logger;
        private readonly HashSet<(OsmExtractor.Source,string)> unused;
        private CompactDictionaryShift<long, GeoZPoint> nodes = default!;
        private CompactDictionaryShift<long, long> ways = default!;

        public IEnumerable<string> Unused => this.unused.Select(it => $"{it.Item1}: {it.Item2}");

        public OsmCollector(ILogger logger)
        {
            this.logger = logger;
            this.unused = new HashSet<(OsmExtractor.Source, string)>();

            Clear();
        }

        private void Clear()
        {
            this.nodes = new CompactDictionaryShift<long, GeoZPoint>();
            this.ways = new CompactDictionaryShift<long, long>();
        }
        
        public List<(TouristAttraction attraction, MapPoint location)> ReadOsm(string filePath)
        {
            Clear();
            var extractor = new OsmExtractor<long>(this.logger, this.nodes, this.ways,x => x);
            
            double start = Stopwatch.GetTimestamp();
            {
                using (var stream = new MemoryStream(System.IO.File.ReadAllBytes(filePath)))
                {
                    using (var source = new PBFOsmStreamSource(stream))
                    {
                        foreach (OsmGeo element in source)
                        {
                            if (!element.Id.HasValue)
                            {
                                this.logger.Warning($"No id for {element}");
                                continue;
                            }
                            
                            if (element is Way way)
                            {
                                ways.Add(way.Id!.Value, way.Nodes.First());
                            }
                            else if (element is Node node)
                            {
                                long node_id = node.Id!.Value;

                                if (node.Latitude.HasValue && node.Longitude.HasValue)
                                {
                                    nodes.Add(node_id, GeoZPoint.FromDegreesMeters(latitude: node.Latitude.Value, 
                                        longitude: node.Longitude.Value,
                                        altitude:null));
                                }
                            }
                            
                            extractor.Extract(element);
                        }
                    }
                }
            }

            this.unused.AddRange(extractor.Unused);
            
            return extractor.GetAttractions();
        }
    }
}