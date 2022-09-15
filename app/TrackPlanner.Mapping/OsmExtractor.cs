using System;
using System.Collections.Generic;
using OsmSharp;
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
    public static class OsmExtractor
    {
        public enum Source
        {
            Historic,
            Buildings,
            Heritage,
            SiteType,
            Castles,
        }
    }

    public sealed class OsmExtractor<TRoad>
    {
        private readonly ILogger logger;
        private readonly IReadOnlyBasicMap<long, GeoZPoint> nodes;
        private readonly IReadOnlyBasicMap<long, TRoad> ways;
        private readonly Func<TRoad, long> roadNodeExtractor;
        private readonly HashSet<(OsmExtractor.Source,string)> unused;
        private readonly List<(TouristAttraction hist, long? nodeId, long? wayId)> attractions;

        public IEnumerable<(OsmExtractor.Source,string)> Unused => this.unused;

        public OsmExtractor(ILogger logger,IReadOnlyBasicMap<long, GeoZPoint> nodes,
            IReadOnlyBasicMap<long, TRoad> ways,
            // road data -> any node of the road
            Func<TRoad,long> roadNodeExtractor)
        {
            this.logger = logger;
            this.nodes = nodes;
            this.ways = ways;
            this.roadNodeExtractor = roadNodeExtractor;
            this.unused = new HashSet<(OsmExtractor.Source,string)>();
            this.attractions = new List<(TouristAttraction hist, long? nodeId, long? wayId)>();
        }

        public List<(TouristAttraction attraction, MapPoint location)> GetAttractions()
        {
            return attractions.Select(it =>
            {
                var effective_node_id = it.nodeId ?? this.roadNodeExtractor( ways[it.wayId!.Value]);
                return (it.hist, new MapPoint(nodes[effective_node_id], effective_node_id));
            }).ToList();
        }

        public void Extract(OsmGeo element)
        {
            if (!element.Id.HasValue)
            {
                this.logger.Warning($"No id for {element}");
                return;
            }

            TouristAttraction.Feature? features = null;

            void add_feature(TouristAttraction.Feature feat)
            {
                features = (features ?? TouristAttraction.Feature.None) | feat;
            }

            void add_feat(string entry, string phrase, TouristAttraction.Feature feat, ref bool found)
            {
                if (entry.Contains(phrase))
                {
                    found = true;
                    features = (features ?? TouristAttraction.Feature.None) | feat;
                }
            }

            if (element.Tags.TryGetValue("historic", out string hist_value))
            {
                void set_feature(string type, TouristAttraction.Feature feat)
                {
                    bool found = false;
                    add_feat(hist_value, type, feat, ref found);
                }

                set_feature("aqueduct", TouristAttraction.Feature.Aqueduct);
                set_feature("castle", TouristAttraction.Feature.Castle);
                set_feature("mansion", TouristAttraction.Feature.Mansion);
                set_feature("lighthouse", TouristAttraction.Feature.Lighthouse);
                set_feature("wreck", TouristAttraction.Feature.Ship | TouristAttraction.Feature.Ruins);
                set_feature("boat", TouristAttraction.Feature.Ship);
                set_feature("ship", TouristAttraction.Feature.Ship);
                set_feature("tank", TouristAttraction.Feature.Tank);
                set_feature("tomb", TouristAttraction.Feature.Grave);
                set_feature("manor", TouristAttraction.Feature.Manor);
                set_feature("bunker", TouristAttraction.Feature.Bunker);
                set_feature("church", TouristAttraction.Feature.Church);
                set_feature("monastery", TouristAttraction.Feature.Church);
                set_feature("synagogue", TouristAttraction.Feature.Church);
                set_feature("palace", TouristAttraction.Feature.Palace);
                set_feature("tower", TouristAttraction.Feature.Tower);
                set_feature("chapel", TouristAttraction.Feature.Chapel);
                set_feature("ruins", TouristAttraction.Feature.Ruins);
                set_feature("bridge", TouristAttraction.Feature.Bridge);
                set_feature("building", TouristAttraction.Feature.None);
                set_feature("city_gate", TouristAttraction.Feature.CityWalls);
                set_feature("citywalls", TouristAttraction.Feature.CityWalls);
                set_feature("earthworks", TouristAttraction.Feature.Fortification);
                set_feature("fort", TouristAttraction.Feature.Fortification);
                set_feature("fortification", TouristAttraction.Feature.Fortification);
                set_feature("watermill", TouristAttraction.Feature.Mill);
                set_feature("train_station", TouristAttraction.Feature.TrainStation);
                set_feature("pillory", TouristAttraction.Feature.Pillory);
                set_feature("quarry", TouristAttraction.Feature.Mine);
                set_feature("archaeological_site", TouristAttraction.Feature.ArchaeologicalSite);
                set_feature("aircraft", TouristAttraction.Feature.Aircraft);
                set_feature("helicopter", TouristAttraction.Feature.Aircraft);
                set_feature("locomotive", TouristAttraction.Feature.Train);
                set_feature("railway_car", TouristAttraction.Feature.Train);

                if (features == null)
                    this.unused.Add((OsmExtractor.Source.Historic, hist_value));
            }

            // https://wiki.openstreetmap.org/wiki/Key:heritage
            if (element.Tags.TryGetValue("heritage", out string heritage_value))
            {
                add_feature(TouristAttraction.Feature.None);
                this.unused.Add(( OsmExtractor.Source.Heritage, heritage_value));
            }

            if (element.Tags.TryGetValue("tourism", out string? tourism_value)
                && tourism_value.Contains("attraction")
                && element.Tags.TryGetValue("building", out string? building_value))
            {
                var found = false;
                add_feat(building_value, "ship", TouristAttraction.Feature.Ship, ref found);
                add_feat(building_value, "synagogue", TouristAttraction.Feature.Church, ref found);
                add_feat(building_value, "temple", TouristAttraction.Feature.Church, ref found);
                add_feat(building_value, "cathedral", TouristAttraction.Feature.Church, ref found);
                add_feat(building_value, "chapel", TouristAttraction.Feature.Church, ref found);
                add_feat(building_value, "church", TouristAttraction.Feature.Church, ref found);
                add_feat(building_value, "ruins", TouristAttraction.Feature.Ruins, ref found);
                add_feat(building_value, "historic", TouristAttraction.Feature.None, ref found);
                add_feat(building_value, "castle", TouristAttraction.Feature.Castle, ref found);
                add_feat(building_value, "bunker", TouristAttraction.Feature.Bunker, ref found);
                add_feat(building_value, "palace", TouristAttraction.Feature.Palace, ref found);
                add_feat(building_value, "train_station", TouristAttraction.Feature.TrainStation, ref found);

                if (!found)
                    this.unused.Add((OsmExtractor.Source.Buildings,building_value));
            }

            if (element.Tags.TryGetValue("ruins", out string ruins_val) && ruins_val == "yes")
                add_feature(TouristAttraction.Feature.Ruins);

            if (element.Tags.TryGetValue("site_type", out string? site_type_value))
            {
                bool found = false;
                if (site_type_value.Contains("tumulus"))
                {
                    // don't set this feature unless it has something already set
                    if (features != null)
                    {
                        add_feature(TouristAttraction.Feature.Grave);
                    }

                    found = true;
                }

                add_feat(site_type_value, "fortification", TouristAttraction.Feature.Fortification, ref found);
                add_feat(site_type_value, "earthworks", TouristAttraction.Feature.Fortification, ref found);

                if (!found)
                    this.unused.Add((OsmExtractor.Source.SiteType,site_type_value));
            }

            if (element.Tags.TryGetValue("castle_type", out string castle_type_val))
            {
                var found = false;
                add_feat(castle_type_val, "archaeological_site", TouristAttraction.Feature.ArchaeologicalSite, ref found);
                add_feat(castle_type_val, "manor", TouristAttraction.Feature.Manor, ref found);
                add_feat(castle_type_val, "palace", TouristAttraction.Feature.Palace, ref found);
                add_feat(castle_type_val, "fortress", TouristAttraction.Feature.Fortress, ref found);

                if (!found)
                    this.unused.Add((OsmExtractor.Source.Castles,castle_type_val));
            }

            element.Tags.TryGetValue("name", out var name_val);
            name_val ??= "";

            element.Tags.TryGetValue("url", out string? url_value);

            if (element is Way way)
            {
                if (features.HasValue)
                {
                    attractions.Add((new TouristAttraction( name_val, url_value, features.Value), way.Nodes.First(), null));
                }
            }
            else if (element is Node node)
            {
                long node_id = node.Id!.Value;

                if (features.HasValue)
                {
                    attractions.Add((new TouristAttraction( name_val, url_value, features.Value), node_id, null));
                }
            }
            else if (element is Relation relation)
            {
                if (features.HasValue)
                {
                    var way_id = relation.Members.Where(it => it.Type == OsmGeoType.Way).Select(it => it.Id).FirstOrNone();
                    if (way_id.HasValue)
                        attractions.Add((new TouristAttraction( name_val, url_value, features.Value), null, way_id.Value));
                    else
                    {
                        var node_id = relation.Members.Where(it => it.Type == OsmGeoType.Node).Select(it => it.Id).FirstOrNone();
                        if (node_id.HasValue)
                            attractions.Add((new TouristAttraction( name_val, url_value, features.Value), node_id.Value, null));
                        else
                            this.logger.Warning($"Relation {relation.Id} does not have any way or node.");
                    }
                }
            }
        }
    }
}