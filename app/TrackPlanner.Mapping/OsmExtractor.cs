using MathUnit;
using OsmSharp;
using OsmSharp.Streams;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Geo;
using TrackPlanner.Shared;
using TrackPlanner.LinqExtensions;
using TrackPlanner.Storage.Data;

namespace TrackPlanner.Mapping
{
    public sealed class OsmExtractor
    {
        private readonly ILogger logger;
        private readonly IGeoCalculator calc;
        private readonly string? debugDirectory;
        private readonly HashSet<string> unusedHistoric;
        private readonly HashSet<string> unusedBuildings;
        private readonly HashSet<string> unusedHeritage;
        private readonly HashSet<string> unusedSiteType;

        public IEnumerable<string> Unused => this.unusedHistoric.Select(it => $"h:{this.unusedHistoric}")
            .Concat(this.unusedBuildings.Select(it => $"b:{it}"))
            .Concat(this.unusedHeritage.Select(it => $"r:{it}"))
            .Concat(this.unusedSiteType.Select(it => $"s:{it}"))
        ;

        public OsmExtractor(ILogger logger, IGeoCalculator calc, string? debugDirectory)
        {
            this.logger = logger;
            this.calc = calc;
            this.debugDirectory = debugDirectory;
            this.unusedHistoric = new HashSet<string>();
            this.unusedBuildings = new HashSet<string>();
            this.unusedHeritage = new HashSet<string>();
            this.unusedSiteType = new HashSet<string>();
        }

        public List<(TouristAttraction historicObject,GeoPoint location)> ReadHistoricObjects(string filePath)
        {
            var nodes = new CompactDictionaryShift<long, GeoPoint>();
            // way id -> first node id
            var ways = new CompactDictionaryShift<long, long>();
            var attractions = new List<(TouristAttraction hist, long? nodeId, long? wayId)>();

            double start = Stopwatch.GetTimestamp();
            {
                using (var stream = new MemoryStream(System.IO.File.ReadAllBytes(filePath)))
                    //using (var stream = new FileStream(map_path, FileMode.Open, FileAccess.Read))
                {
                    using (var source = new PBFOsmStreamSource(stream))
                    {
                        foreach (OsmGeo element in source)
                        {
                            if (!element.Id.HasValue)
                            {
                                logger.Warning($"No id for {element}");
                                continue;
                            }

                            TouristAttraction.Feature? features = null;
                            
                            if (element.Tags.TryGetValue("historic", out string hist_value))
                            {
                                void set_feature(string type, TouristAttraction.Feature feature)
                                {
                                    if (hist_value.Contains(type))
                                        features |= feature;
                                }

                                set_feature("castle", TouristAttraction.Feature.Castle);
                                set_feature("mansion", TouristAttraction.Feature.Mansion);
                                set_feature("lighthouse", TouristAttraction.Feature.Lighthouse);
                                set_feature("wreck", TouristAttraction.Feature.Wreck);
                                set_feature("tomb", TouristAttraction.Feature.Tomb);
                                set_feature("manor", TouristAttraction.Feature.Manor);
                                set_feature("bunker", TouristAttraction.Feature.Bunker);
                                set_feature("church", TouristAttraction.Feature.Church);
                                set_feature("palace", TouristAttraction.Feature.Palace);
                                set_feature("tower", TouristAttraction.Feature.Tower);
                                set_feature("chapel", TouristAttraction.Feature.Chapel);
                                set_feature("archaeological_site", TouristAttraction.Feature.ArchaeologicalSite);

                                if (features==null)
                                    this.unusedHistoric.Add(hist_value);
                            }

                            // https://wiki.openstreetmap.org/wiki/Key:heritage
                            if (element.Tags.TryGetValue("heritage", out string heritage_value))
                            {
                                features ??= TouristAttraction.Feature.None;
                                this.unusedHeritage.Add(heritage_value);
                            }

                            if (element.Tags.TryGetValue("tourism", out string? tourism_value) && tourism_value.Contains("attraction")
                                                                                               && element.Tags.TryGetValue("building", out string? building_value))
                            {
                                if (building_value.Contains("church"))
                                    features |= TouristAttraction.Feature.Church;
                                else
                                    this.unusedBuildings.Add(hist_value);
                            }

                            if ((element.Tags.TryGetValue("ruins", out string ruins_val) && ruins_val == "yes") || hist_value.Contains("ruins"))
                                features |= TouristAttraction.Feature.Ruins;

                            if (element.Tags.TryGetValue("site_type", out string? site_type_value))
                                this.unusedSiteType.Add(site_type_value);

                            if (element.Tags.TryGetValue("castle_type", out string type_val))
                            {
                                if (type_val == "manor")
                                    features |= TouristAttraction.Feature.Manor;
                                if (type_val == "palace")
                                    features |= TouristAttraction.Feature.Palace;
                            }

                             element.Tags.TryGetValue("name", out var name_val);
                                 name_val ??= "";

                                 element.Tags.TryGetValue("url", out string? url_value);

                            if (element is Way way)
                            {
                                ways.Add(way.Id!.Value, way.Nodes.First());

                                if (features.HasValue)
                                {
                                    attractions.Add((new TouristAttraction(default, name_val, url_value, features.Value), way.Nodes.First(), null));

                                }
                            }
                            else if (element is Node node)
                            {
                                long node_id = node.Id!.Value;

                                if (node.Latitude.HasValue && node.Longitude.HasValue)
                                {
                                    nodes.Add(node_id, new GeoPoint(latitude: Angle.FromDegrees(node.Latitude.Value), longitude: Angle.FromDegrees(node.Longitude.Value)));
                                }

                                if (features.HasValue)
                                {
                                    attractions.Add((new TouristAttraction(default, name_val, url_value, features.Value), node_id, null));
                                }
                            }
                            else if (element is Relation relation)
                            {
                                if (features.HasValue)
                                {
                                    var way_id = relation.Members.Where(it => it.Type == OsmGeoType.Way).Select(it => it.Id).FirstOrNone();
                                    if (way_id.HasValue)
                                        attractions.Add((new TouristAttraction(default, name_val, url_value, features.Value), null, way_id.Value));
                                    else
                                    {
                                        var node_id = relation.Members.Where(it => it.Type == OsmGeoType.Node).Select(it => it.Id).FirstOrNone();
                                        if (node_id.HasValue)
                                            attractions.Add((new TouristAttraction(default, name_val, url_value, features.Value), node_id.Value, null));
                                        else
                                            this.logger.Warning($"Relation {relation.Id} does not have any way or node.");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return attractions.Select(it =>
            {
                var effective_node_id = it.nodeId ?? ways[it.wayId!.Value];
                    return (new TouristAttraction(effective_node_id,  it.hist.Name, it.hist.Url, it.hist.Features),nodes[effective_node_id]);
                }).ToList();
        }
    }
}