using MathUnit;
using OsmSharp;
using OsmSharp.Streams;
using OsmSharp.Tags;
using SharpKml.Engine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Geo;
using TrackPlanner.Shared;
using TrackPlanner.DataExchange;
using TrackPlanner.LinqExtensions;
using TrackPlanner.Mapping.Data;


#nullable enable

namespace TrackPlanner.Mapping
{
    public sealed class OsmExtractor
    {
        private readonly ILogger logger;
        private readonly IGeoCalculator calc;
        private readonly string? debugDirectory;
        private readonly HashSet<string> unusedHistoric;
        public IEnumerable<string> UnusedHistoric => this.unusedHistoric;

        public OsmExtractor(ILogger logger, IGeoCalculator calc, string? debugDirectory)
        {
            this.logger = logger;
            this.calc = calc;
            this.debugDirectory = debugDirectory;
            this.unusedHistoric = new HashSet<string>();
        }

        public List<HistoricObject> ReadHistoricObjects(string filePath)
        {
            var nodes = new CompactDictionary<long, GeoPoint>();
            // way id -> first node id
            var ways = new CompactDictionary<long, long>();
            var historic_objects = new List<(HistoricObject hist, long? nodeId, long? wayId)>();

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

                            bool is_castle = false;
                            bool is_target = false;
                            if (element.Tags.TryGetValue("historic", out string hist_value))
                            {

                                is_castle = hist_value.Contains("castle");
                                is_target = hist_value.Contains("mansion")
                                            || hist_value.Contains("lighthouse")
                                            || hist_value.Contains("wreck")
                                            || hist_value.Contains("tomb")
                                            || hist_value.Contains("manor")
                                            || hist_value.Contains("bunker")
                                            || hist_value.Contains("church")
                                            || hist_value.Contains("palace")
                                            || hist_value.Contains("tower")
                                            || hist_value.Contains("chapel")
                                    ;
                                if (!is_castle && !is_target)
                                    this.unusedHistoric.Add(hist_value);
                            }

                            // https://wiki.openstreetmap.org/wiki/Key:heritage
                            bool is_heritage = element.Tags.TryGetValue("heritage", out string heritage_value);
                            bool is_church_attraction = element.Tags.TryGetValue("tourism", out string? tourism_value) && tourism_value.Contains("attraction")
                                && element.Tags.TryGetValue("building",out string? building_value) && building_value.Contains("church");
                            bool ruins = is_castle && ((element.Tags.TryGetValue("ruins", out string ruins_val) && ruins_val == "yes") || hist_value.Contains("ruins"));
                            element.Tags.TryGetValue("site_type", out string? site_type_value);
                            string description = "";
                            if (element.Tags.TryGetValue("castle_type", out string type_val))
                            {
                                if (type_val == "manor" || type_val == "palace")
                                    description= $" ({type_val})";
                            }
                            string name = element.Tags.TryGetValue("name", out var name_val) 
                                ? name_val : $"{hist_value} {site_type_value}{description} {element.GetType().Name[0]}{element.Id}";
                            element.Tags.TryGetValue("url", out string? url_value);

                            if (is_castle || is_church_attraction || is_heritage)
                                is_target = true;
                            
                            if (element is Way way)
                            {
                                ways.Add(way.Id!.Value,way.Nodes.First());

                                if (is_target)
                                {
                                    historic_objects.Add((new HistoricObject(default, name, url_value, ruins), way.Nodes.First(), null));

                                }
                            }
                            else if (element is Node node)
                            {
                                long node_id = node.Id!.Value;

                                if (node.Latitude.HasValue && node.Longitude.HasValue)
                                {
                                    nodes.Add(node_id, new GeoPoint(latitude: Angle.FromDegrees(node.Latitude.Value), longitude: Angle.FromDegrees(node.Longitude.Value)));
                                }

                                if (is_target)
                                {
                                    historic_objects.Add((new HistoricObject(default, name,url_value, ruins), node_id,null));
                                }
                            }
                            else if (element is Relation relation)
                            {
                                if (is_target)
                                { 
                                    var way_id = relation.Members.Where(it => it.Type == OsmGeoType.Way).Select(it => it.Id).FirstOrNone();
                                    if (way_id.HasValue)
                                        historic_objects.Add((new HistoricObject(default, name,url_value, ruins), null, way_id.Value));
                                    else
                                    {
                                        var node_id = relation.Members.Where(it => it.Type == OsmGeoType.Node).Select(it => it.Id).FirstOrNone();
                                        if (node_id.HasValue)
                                            historic_objects.Add((new HistoricObject(default, name,url_value, ruins), node_id.Value, null));
                                        else
                                            this.logger.Warning($"Relation {relation.Id} does not have any way or node.");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return historic_objects.Select(it => new HistoricObject(it.nodeId.HasValue? nodes[it.nodeId.Value] : nodes[ways[it.wayId!.Value]], it.hist.Name,it.hist.Url, it.hist.Ruins)).ToList();
        }
    }
}