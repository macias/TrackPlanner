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
using TrackPlanner.Shared;
using TrackPlanner.DataExchange;
using TrackPlanner.LinqExtensions;
using TrackPlanner.Mapping.Data;
using TrackPlanner.Mapping.Disk;
using TrackPlanner.Storage;


#nullable enable

namespace TrackPlanner.Mapping
{
    // elevation
    // https://en.wikipedia.org/wiki/Shuttle_Radar_Topography_Mission

    // https://wiki.openstreetmap.org/wiki/Map_Features
    public sealed class OsmReader
    {
        private static readonly IReadOnlySet<string> knownOtherRoads = new HashSet<string>() { "speed_camera", "motorway_junction", "traffic_signals", "milestone", "give_way", "turning_circle",
            "stop", "stop_line", "mini_roundabout", "traffic_signals;crossing", "passing_place", "street_lamp", "turning_loop", "elevator",
            "traffic_mirror", "services", "traffic_sign", "give_way;crossing",
            "toll_gantry", "speed_display", "emergency_access_point", "crossing;street_lamp", "priority", "crossing;traffic_signals", "trailhead", 
            "emergency_bay", "crossing;bus_stop", "barrier", "disused:bus_stop", "yes", "roundabout", "u", "parking", "tertiary + cycleway:right=lane", "planned", "disused", "razed", "2", "busway", "closed", "fire_road", "inline_skates"            
        };

        private static readonly IReadOnlySet<string> knownMaxSpeedErrors = new HashSet<string>() {"PL:rural","PL:urban","implicit","none"};

        private readonly ILogger logger;
        private readonly IGeoCalculator calc;
        private readonly MemorySettings memSettings;
        private readonly Length highTrafficProximity;
        private readonly string? debugDirectory;

        public OsmReader(ILogger logger, IGeoCalculator calc, MemorySettings memSettings, 
            Length highTrafficProximity, string? debugDirectory)
        {
            this.logger = logger;
            this.calc = calc;
            this.memSettings = memSettings;
            this.highTrafficProximity = highTrafficProximity;
            this.debugDirectory = debugDirectory;
        }

        public IDisposable ReadOsmMap(string mapPathOrDirectory, bool onlyRoads, out IWorldMap map)
        {
            string[] osm_files;
            if (System.IO.File.Exists(mapPathOrDirectory))
                osm_files = new[] {mapPathOrDirectory};
            else
               osm_files = System.IO.Directory.GetFiles(mapPathOrDirectory, "*.osm.pbf");

            var road_names = new Dictionary<string, int>();

            if (!onlyRoads)
            {
                if (this.memSettings.MapMode != MapMode.MemoryOnly)
                    throw new ArgumentException();

                var temp_map = readActualOsmMap(osm_files, road_names, onlyRoads);
                map = temp_map;
                var temp_grid = new RoadGridMemory(logger,
                    new RoadGridMemoryBuilder(logger, temp_map, new ApproximateCalculator(), this.memSettings.GridCellSize, debugDirectory).BuildCells(),
                    map, new ApproximateCalculator(), this.memSettings.GridCellSize, debugDirectory, legacyGetNodeAllRoads: false);
                temp_map.AttachDangerInNonMotorNodes(temp_grid,this.highTrafficProximity);
                return CompositeDisposable.None;
            }

            var extracts = new List<string>(capacity: osm_files.Length);

            long timestamp = DateTimeOffset.UtcNow.Ticks;

            foreach (string file_path in osm_files)
            {
                // removing double extension
                string xtr_path = System.IO.Path.ChangeExtension(System.IO.Path.ChangeExtension(file_path, null), "xtr");
                extracts.Add(xtr_path);

                if (!System.IO.File.Exists(xtr_path))
                {
                    var temp_map = readActualOsmMap(new[] {file_path}, road_names, onlyRoads);

                    var temp_grid = new RoadGridMemory(logger,
                        new RoadGridMemoryBuilder(logger, temp_map, new ApproximateCalculator(), this.memSettings.GridCellSize, debugDirectory).BuildCells(),
                        temp_map, new ApproximateCalculator(), this.memSettings.GridCellSize, debugDirectory, legacyGetNodeAllRoads: false);
                    temp_map.AttachDangerInNonMotorNodes(temp_grid,this.highTrafficProximity);

                    using (var mem = new MemoryStream())
                    {

                        temp_map.Write(timestamp, mem);
                        mem.Position = 0;
                        System.IO.File.WriteAllBytes(xtr_path, mem.ToArray());
                    }
                }
            }

            if (this.memSettings.MapMode == MapMode.MemoryOnly)
            {
                double start = Stopwatch.GetTimestamp();
                //var temp_map = OsmMapMemory.ReadMappedArray(logger, extracts);
                var temp_map = WorldMapMemory.ReadRawArray(logger, extracts,
                    this.memSettings.GridCellSize, debugDirectory!,
                    out var invalid_files);
                if (invalid_files.Any())
                    throw new NotSupportedException();
                map = temp_map;
                logger.Info($"Loaded MEM in {(Stopwatch.GetTimestamp() - start) / Stopwatch.Frequency} s");

                return CompositeDisposable.None;
            }
            else if (this.memSettings.MapMode == MapMode.HybridDisk)
            {
                double start = Stopwatch.GetTimestamp();
                var disp = WorldMapDisk.Read(logger, extracts, this.memSettings, debugDirectory,
                    out var temp_map,out var invalid_files);
                if (invalid_files.Any())
                    throw new NotSupportedException();
                map = temp_map;
                logger.Info($"Loaded HYBRID in {(Stopwatch.GetTimestamp() - start) / Stopwatch.Frequency} s");

                return disp;
            }
            else
                throw new NotImplementedException($"{nameof(this.memSettings.MapMode)} {this.memSettings.MapMode}");
        }


        /*

Designated path keys 24279991 : bicycle foot wheelchair, 26116023 : bicycle:forward foot, 28356417 : ski, 50893492 : horse, 110020350 : access, 168001056 : bicycle foot inline_skates, 173308948 : foot ski, 241801214 : golf_cart, 257367436 : bicycle foot motor_vehicle, 367271198 : bicycle foot horse, 385269448 : motorcar, 462740019 : wheelchair, 518835706 : foot horse, 524775730 : bicycle cycleway:left:bicycle cycleway:right:foot foot, 715901073 : agricultural horse, 715904453 : agricultural, 792452093 : agricultural foot goods, 828042090 : bicycle foot footway, 846766431 : bicycle mtb
Loaded 178_093_027 nodes, 50_177 road names, 4_051_113 roads, 1_503 longest in 508.1771256 s
        */
        private WorldMapMemory readActualOsmMap(IEnumerable<string> filePaths,Dictionary<string, int> roadNames ,
            bool onlyRoads)
        {
            IMap<long,RoadInfo> roads = MapFactory.CreateFast<long, RoadInfo>(); // road id -> road info
            var forests = new List<IEnumerable<long>>();
            var rivers = new List<(RiverKind kind, IEnumerable<long> indices)>();
            var railways = new List<List<long>>();
            var cities = new List<CityInfo>();
            var waters = new List<IEnumerable<long>>();
            var protected_area = new List<IEnumerable<long>>();
            var nozone = new List<NamedPolygon>();

            var forest_relations = new List<RelationInfo>();
            var water_relations = new List<RelationInfo>();
            var protected_relations = new List<RelationInfo>();
            var nozone_relations = new List<RelationInfo>();

            var roads_other_values = new HashSet<string>();
            // designated tags -> road id
            var designated_path_keys = new Dictionary<string, long>();
            
            // high traffic road ids with no speed limit given 
            var undefined_speed = new HashSet<long>();

            int? getRoadIdentifier(OsmGeo element, out string? name)
            {
                name = element.Tags.TryGetValue("name", out string name_value) ? name_value : "";
                if (string.IsNullOrEmpty(name))
                {
                    name = null;
                    return null;
                }

                if (!roadNames.TryGetValue(name, out int index))
                {
                    index = roadNames.Count;
                    roadNames.Add(name, index);
                }

                return index;
            }

            bool try_add_road(OsmGeo element, bool parseOnly, params long[] nodes)
            {
                bool bike_lane = hasBikeLine(element);
                bool has_urban_sidewalk;

                if (!tryParseWayKind(element, roads_other_values!, designated_path_keys, ref bike_lane, out has_urban_sidewalk, out WayKind road_kind))
                    return false;

                RoadSurface surface = parseSurface(element);
                bool has_access = parseAccess(element);
                var layer = readLayer(element);
                int? max_speed = readSpeedLimit(element) ;
                var is_roundabout = isRoundabout(element);
                if (!max_speed.HasValue && is_roundabout) // it is super unlikely that the speed limit on the roundabout will be above 50km/h
                    max_speed = 0;
                int? name_identifier = getRoadIdentifier(element, out string? road_name);
                bool is_singletrack = road_name?.ToLowerInvariant().Contains("singletrack") ?? false;
                if (is_singletrack)
                    logger.Verbose($"Way {element.Id} marked as singletrack: {road_name}");

                var road = new RoadInfo(element.Id!.Value, road_kind,
                    nameIdentifier: name_identifier, 
                    parseOneWay(element), is_roundabout, surface, parseSmoothness(element),
                    hasAccess: has_access,
                    speedLimit50:(max_speed?? int.MaxValue)<=50,
                    hasBikeLane: bike_lane,
                    isSingletrack: is_singletrack,
                    has_urban_sidewalk,
                    dismount: bikeDismount(element),
                    layer,
                    nodes);

                if (road.IsMassiveTraffic && !max_speed.HasValue)
                    undefined_speed.Add(road.Identifier);

                if (!parseOnly)
                {
                    if (!roads!.TryAdd(road.Identifier, road, out var existing) && road != existing)
                        throw new ArgumentException($"Road {road.Identifier} already exists with {road}, while we have {existing}");
                }

                return true;
            }

            double start = Stopwatch.GetTimestamp();
            foreach (string map_path in filePaths)
            {
                using (var stream = new MemoryStream(System.IO.File.ReadAllBytes(map_path)))
                //using (var stream = new FileStream(map_path, FileMode.Open, FileAccess.Read))
                {
                    logger.Verbose($"Stream {map_path} built in {(Stopwatch.GetTimestamp() - start) / Stopwatch.Frequency} s");

                    using (var source = new PBFOsmStreamSource(stream))
                    {
                        foreach (OsmGeo element in source)
                        {
                            if (!element.Id.HasValue)
                            {
                                logger.Warning($"No id for {element}");
                                continue;
                            }

                            if (element is Way way)
                            {
                                if (try_add_road(element, parseOnly: false, way.Nodes))
                                {
                                    ;
                                }
                                else if (way.Tags.TryGetValue("route", out string route_value) && route_value == "ferry")
                                {
                                    bool has_access = parseAccess(element);
                                    var layer = readLayer(element);
                                    RoadInfo roadInfo = new RoadInfo(element.Id.Value, WayKind.Ferry, 
                                        nameIdentifier: getRoadIdentifier(element, out _), 
                                        parseOneWay(element), roundabout: false, RoadSurface.Unknown,
                                                                        RoadSmoothness.Bad, has_access,
                                                                        speedLimit50:true,
                                                                        hasBikeLane: false, isSingletrack: false,
                                                                        urbanSidewalk: false, dismount: bikeDismount(element), layer, way.Nodes);
                                    roads.Add(roadInfo.Identifier, roadInfo);
                                }
                                else if (isNoZone(element))
                                {
                                    string name = getString(element, "name") ?? "unknown";

                                    nozone.Add(new NamedPolygon(element.Id.Value, name, way.Nodes));
                                }
                                else if (!onlyRoads)
                                {
                                    if (isForest(element))
                                    {
                                        forests.Add(way.Nodes);
                                    }
                                    else if (isRailway(element))
                                    {
                                        railways.Add(way.Nodes.ToList());
                                    }
                                    else if (isRiver(element, out RiverKind river_kind))
                                    {
                                        rivers.Add((river_kind, way.Nodes));
                                    }
                                    else if (isWater(element))
                                    {
                                        waters.Add(way.Nodes);
                                    }
                                    else if (isProtectedArea(element))
                                    {
                                        protected_area.Add(way.Nodes);
                                    }

                                }

                            }
                            else if (element is Node node && node.Latitude.HasValue && node.Longitude.HasValue)
                            {
                                long node_id = node.Id!.Value;

                                // https://wiki.openstreetmap.org/wiki/Tag:highway%3Dcrossing
                                // currently we are not interested in point-roads (like crossings)
                                if (try_add_road(element, parseOnly: true, node_id)) // todo: point-ways have separate id numbering, currently reader is not prepared for this
                                {
                                    ;
                                }
                                else if (!onlyRoads)
                                {
                                    if (element.Tags.TryGetValue("place", out string place_value) && (place_value == "city" || place_value == "town" || place_value == "village" || place_value == "hamlet"))
                                    {
                                        string name = getString(element, "name") ?? "unknown";

                                        CityRank rank = parseCityRank(element, place_value);

                                        cities.Add(new CityInfo(rank: rank, name: name, node: node_id));
                                    }
                                }
                            }
                            else if (element is Relation relation)
                            {
                                List<long> extract_outline(Relation rel) => rel.Members.Where(it => it.Type == OsmGeoType.Way && it.Role == "outer").Select(it => it.Id).ToList();

                                string name = getString(relation, "mame") ?? "anon";

                                if (isNoZone(element))
                                    nozone_relations.Add(new RelationInfo(name, element.Id.Value, extract_outline(relation)));
                                if (!onlyRoads)
                                {
                                    if (isForest(element))
                                        forest_relations.Add(new RelationInfo(name, element.Id.Value, extract_outline(relation)));
                                    else if (isWater(element))
                                        water_relations.Add(new RelationInfo(name, element.Id.Value, extract_outline(relation)));
                                    else if (isProtectedArea(element))
                                        protected_relations.Add(new RelationInfo(name, element.Id.Value, extract_outline(relation)));
                                }
                            }
                            else
                                throw new NotImplementedException($"Type {element.GetType().Name} is not supported");
                        }
                    }
                }
            }

            if (roads_other_values.Any())
                logger.Verbose($"Roads other values {(String.Join(", ", roads_other_values))}");
            if (designated_path_keys.Any())
                logger.Verbose($"Designated path keys {(String.Join(", ", designated_path_keys.Select(it => $"{it.Value} : {it.Key}")))}");
            logger.Verbose($"Loaded {roadNames.Count} road names, {roads.Count} roads, {roads.Values.Select(it => it.Nodes.Count).Max()} longest in {(Stopwatch.GetTimestamp() - start) / Stopwatch.Frequency} s");
            logger.Flush();

            {
                long current_size = 0;
                long compacted = 0;
                long biggest_range = 0;
                foreach (var road in roads.Values)
                {
                    current_size += sizeof(long) * road.Nodes.Count;
                    compacted += sizeof(long) + sizeof(uint) * road.Nodes.Count;
                    road.Nodes.MinMax(out var min,out var max);
                    biggest_range = Math.Max(max - min, biggest_range);
                }

                logger.Info($"Current size {current_size}B, compacted {compacted}B, range {biggest_range}");
            }
            
            nozone.AddRange(mutableConvertRelationsToPolygons(logger, roads, nozone_relations, "no zone"));

            if (!onlyRoads)
            {
                forests.AddRange(mutableConvertRelationsToPolygons(logger, roads, forest_relations, "forests").Select(it => it.Nodes));
                waters.AddRange(mutableConvertRelationsToPolygons(logger, roads, water_relations, "waters").Select(it => it.Nodes));
                protected_area.AddRange(mutableConvertRelationsToPolygons(logger, roads, protected_relations, "protected").Select(it => it.Nodes));
            }

            //mutableMergeRailways(railways);
            
            var nodes = MapFactory.CreateFast<long, GeoZPoint>();

            foreach (var node_id in roads.SelectMany(it => it.Value.Nodes)
                .Concat(nozone.SelectMany(it => it.Nodes)))
            {
                nodes.TryAdd(node_id, GeoZPoint.Invalid,out _);
            }

            if (!onlyRoads)
            {
                foreach (var node_id in forests.SelectMany(it => it)
                    .Concat(rivers.SelectMany(it => it.indices))
                    .Concat(waters.SelectMany(it => it))
                    .Concat(railways.SelectMany(it => it))
                    .Concat(protected_area.SelectMany(it => it))
                    .Concat(cities.Select(it => it.Node)))
                {
                    nodes.TryAdd(node_id, GeoZPoint.Invalid,out _);
                }
            }

            foreach (string map_path in filePaths)
            {
                //using (var stream = new MemoryStream(System.IO.File.ReadAllBytes(map_path)))
                using (var stream = new FileStream(map_path, FileMode.Open, FileAccess.Read))
                {
                    logger.Verbose($"Stream {map_path} built in {(Stopwatch.GetTimestamp() - start) / Stopwatch.Frequency} s");

                    using (var source = new PBFOsmStreamSource(stream))
                    {
                        foreach (OsmGeo element in source)
                        {
                            if (element.Id.HasValue && element is Node node 
                                                    && node.Latitude.HasValue && node.Longitude.HasValue)
                            {
                                long node_id = node.Id!.Value;
                                GeoZPoint pt = GeoZPoint.FromDegreesMeters(node.Latitude.Value, node.Longitude.Value, altitude: null);
                                if (nodes.TryGetValue(node_id, out GeoZPoint existing))
                                {
                                    if( existing != GeoZPoint.Invalid)
                                        throw new ArgumentException($"Node {node_id} already exists at {existing}, while we have {pt}");
                                    nodes[node_id] = pt;
                                }
                            }
                        }
                    }
                }
            }
            
            logger.Verbose($"Loaded {nodes.Count} nodes in {(Stopwatch.GetTimestamp() - start) / Stopwatch.Frequency} s");
            logger.Flush();

            computeRoadAccess(nodes, nozone, roads);

            if (onlyRoads)
            {
                var for_removal = nozone.SelectMany(it => it.Nodes).ToHashSet();
                for_removal.ExceptWith(roads.Values.SelectMany(it => it.Nodes));
                nodes.ExceptWith(for_removal);
                nozone = null;
            }
            
            var node_to_roads_dict = new NodeRoadsAssocDictionary(nodes,roads);

            improveSpeedLimits(nodes,node_to_roads_dict, roads, undefined_speed);

            return new WorldMapMemory(logger, nodes, roads,
                node_to_roads_dict,
                forests, rivers, cities,
                waters,
                protected_area,
                noZone: nozone?.Select(it => it.Nodes.AsEnumerable()).ToList(),
                railways,
               this.memSettings.GridCellSize,debugDirectory,
                onlyRoads);
        }
        
        

        private void improveSpeedLimits(IReadOnlyMap<long,GeoZPoint> nodes,
            NodeRoadsAssocDictionary backReferences,
            IMap<long, RoadInfo> roads, HashSet<long> undefinedSpeed)
        {
            var DEBUG_low_speed = new HashSet<long>();
            var DEBUG_undecided = new HashSet<long>();
            var DEBUG_high_speed = new HashSet<long>();
            
            // do not compute secondary roads, because they too ofen miss any speed limit info
            while (undefinedSpeed.Any())
            {
                var used = new HashSet<long>();
                bool outcome = tryFindSpeedLimit(backReferences, roads, undefinedSpeed, used, out bool limit,
                    RoadIndexLong.InvalidIndex(undefinedSpeed.First()));
                undefinedSpeed.ExceptWith(used);
                
                if (outcome && limit) // we could compute the limits and the limits are present (well, it is our guess)
                {
                    foreach (var road_id in used)
                    {
                        roads[road_id] = roads[road_id].BuildWithSpeedLimit();
                    }
                }

                if (debugDirectory != null)
                {
                    if (!outcome)
                        DEBUG_undecided.AddRange(used);
                    else if (limit)
                        DEBUG_low_speed.AddRange(used);
                    else
                        DEBUG_high_speed.AddRange(used);
                }
            }

            if (debugDirectory != null)
            {
                void dump_roads(IEnumerable<long> ids,string label)
                {
                    var input = new TrackWriterInput();
                    foreach (var road_id in ids)
                    {
                        input.AddLine(roads[road_id].Nodes.Select(n => nodes[n]),$"#{road_id}");

                    }
                    
                    string filename = Helper.GetUniqueFileName(debugDirectory, $"speed-improve-{label}.kml");
                    input.BuildDecoratedKml().Save(filename);
                    
                }
                
                dump_roads(DEBUG_undecided,"undecided");
                dump_roads(DEBUG_low_speed,"low");
                dump_roads(DEBUG_high_speed,"high");
            }
        }
        
        private bool tryFindSpeedLimit(NodeRoadsAssocDictionary nodeToRoadsDictionary,
            IReadOnlyMap<long, RoadInfo> roads,
            HashSet<long> undefinedSpeed,
            long nodeId, HashSet<long> usedRoads, out bool speedLimit50)
        {
            var outgoing = nodeToRoadsDictionary[nodeId]
                .Where(idx => !usedRoads.Contains(idx.RoadMapIndex) && roads[idx.RoadMapIndex].IsMassiveTraffic).ToArray();

            return tryFindSpeedLimit(nodeToRoadsDictionary, roads, undefinedSpeed, usedRoads, out speedLimit50, outgoing);
        }

        private bool tryFindSpeedLimit(NodeRoadsAssocDictionary nodeToRoadsDictionary, 
            IReadOnlyMap<long, RoadInfo> roads, 
            HashSet<long> undefinedSpeed, 
            HashSet<long> usedRoads, out bool speedLimit50,
            params RoadIndexLong[] outgoingRoads)
        {
            if (outgoingRoads.Length == 0) 
            {
                // we hit the end of the road here -- maybe because it is cut of the map, or it is transition to other
                // kind of the road, in any case, we cannot deduce speed limit
                speedLimit50 = default;
                return false;
            }

            foreach (RoadIndexLong idx in outgoingRoads)
            {
                if (!undefinedSpeed.Contains(idx.RoadMapIndex)) // when the road has given speed limit
                {
                    if (!roads[idx.RoadMapIndex].HasSpeedLimit50) // and it is above 50, we conclude the selected road could have speed limit 
                    {
                        speedLimit50 = false;
                        return true;
                    }
                }
                else
                {
                    usedRoads.Add(idx.RoadMapIndex);

                    if (idx.IndexAlongRoad != 0)
                    {
                        if (!tryFindSpeedLimit(nodeToRoadsDictionary, roads, undefinedSpeed, roads[idx.RoadMapIndex].Nodes.First(), usedRoads, out bool limit))
                        {
                            speedLimit50 = default;
                            return false;
                        }

                        if (!limit)
                        {
                            speedLimit50 = false;
                            return true;
                        }
                    }

                    if (idx.IndexAlongRoad != roads[idx.RoadMapIndex].Nodes.Count - 1)
                    {
                        if (!tryFindSpeedLimit(nodeToRoadsDictionary, roads, undefinedSpeed, roads[idx.RoadMapIndex].Nodes.Last(), usedRoads, out bool limit))
                        {
                            speedLimit50 = default;
                            return false;
                        }

                        if (!limit)
                        {
                            speedLimit50 = false;
                            return true;
                        }
                    }
                }
            }

            speedLimit50 = true;
            return true;
        }

        private void computeRoadAccess(IMap<long, GeoZPoint> nodes, List<NamedPolygon> nozones, IMap<long, RoadInfo> roads)
        {
            // NOTE: this algorithm is currently not accurate
            // a) it does not split road which are partially inside and outside no-zone
            // b) it assumes the roads is outside the no-zone if all points are outside (in fact such road can still intersect with no-zone)

            var debug = new DEBUG_NoZone(logger, calc, zoneId: 419051605L, roadId: 704473434L);

            foreach (var zone in nozones)
            {
                logger.Info($"Processing #{zone.Id} {zone.Name}");


                Angle zone_min_lat = Angle.FullCircle;
                Angle zone_max_lat = -Angle.FullCircle;
                Angle zone_min_lon = Angle.FullCircle;
                Angle zone_max_lon = -Angle.FullCircle;

                foreach (var zone_node_id in zone.Nodes)
                {
                    GeoZPoint zone_pt = nodes[zone_node_id];
                    zone_min_lat = zone_min_lat.Min(zone_pt.Latitude);
                    zone_max_lat = zone_max_lat.Max(zone_pt.Latitude);
                    zone_min_lon = zone_min_lon.Min(zone_pt.Longitude);
                    zone_max_lon = zone_max_lon.Max(zone_pt.Longitude);
                }

                var slicer = new Slicer(zone_min_lat, zone_max_lat);

                foreach ((long road_map_index,var road_info) in roads.ToArray())
                {
                    debug.Activate(zone, road_info.Identifier, slicer);

                    // roads so important are unlikely implicitly forbidden 
                    if (road_info.Kind <= WayKind.SecondaryLink || !road_info.HasAccess)
                    {
                        continue;
                    }

                    var insides = new List<bool>(capacity: road_info.Nodes.Count);
                    foreach (var road_node_id in road_info.Nodes)
                    {
                        insides.Add(isNodeInside(debug, zone_min_lat, zone_max_lat, zone_min_lon, zone_max_lon, slicer, nodes[road_node_id], zone, nodes));
                    }

                    if (insides.Any(x => x))
                    {
                        // this is inaccurate but for example Torun range has incorrect border overlapping legal roads, so we have somehow to fix
                        // such errors

                        // if the road in majority of its length is outside of the no-zone, count is as available

                        Length outside_length = Length.Zero;
                        Length inside_length = Length.Zero;
                        for (int i = 1; i < insides.Count; ++i)
                        {
                            if (insides[i - 1] == insides[i])
                            {
                                var dist = calc.GetDistance(nodes[road_info.Nodes[i - 1]], nodes[road_info.Nodes[i]]);
                                if (insides[i])
                                    inside_length += dist;
                                else
                                    outside_length += dist;
                            }
                        }

                        if (inside_length > outside_length)
                        {
                            logger.Verbose($"Setting road {road_info.Identifier} as forbidden");
                            debug.Forbidden(road_info.Identifier);
                            // it is over-simplification, we should split such road, but for now it will suffice
                            roads[road_map_index] = road_info.BuildWithDenyAccess();
                        }
                    }
                }
            }

            if (debugDirectory != null)
            {
                {
                    var kml = debug.BuildKml(nodes);
                    kml.Save(Helper.GetUniqueFileName(debugDirectory, "nozone-debug.kml"));
                }
                {
                    var kml = debug.BuildZonePointsKml(nodes);
                    kml?.Save(Helper.GetUniqueFileName(debugDirectory, "nozone-area-points.kml"));
                }
                {
                    var kml = debug.BuildZoneLineKml(nodes);
                    kml?.Save(Helper.GetUniqueFileName(debugDirectory, "nozone-area-line.kml"));
                }
                {
                    var kml = debug.BuildZoneForbiddenKml(nodes, roads);
                    kml.Save(Helper.GetUniqueFileName(debugDirectory, "nozone-forbidden.kml"));
                }
                {
                    var kml = debug.BuildAllForbiddenKml(nodes, roads);
                    kml.Save(Helper.GetUniqueFileName(debugDirectory, "nozone-all-forbidden.kml"));
                }
            }
        }


        private bool isNodeInside(DEBUG_NoZone debug,
                Angle zone_min_lat,
        Angle zone_max_lat,
        Angle zone_min_lon,
        Angle zone_max_lon,
        Slicer slicer,
        GeoZPoint road_pt,
        NamedPolygon zone,
        IMap<long, GeoZPoint> nodes)
        {
            /*if (zone.Id == 419051605)
            {
                logger.Info($"Taking road {road_info.Id}");
            }*/

            if (road_pt.Latitude < zone_min_lat || road_pt.Latitude > zone_max_lat || road_pt.Longitude < zone_min_lon || road_pt.Longitude > zone_max_lon)
                return false;

            debug.RegisterPoint(road_pt);
            /* if (zone.Id== 419051605)
             {
                 logger.Info($"Checking inside road {road_info.Id}");
             }*/
            // we will count polygon crosses on top of the point (moving vertically, along longitude is safe, becase is also along greate circle)
            // https://en.wikipedia.org/wiki/Point_in_polygon
            int cross_count = 0;

            for (int z = 1; z < zone.Nodes.Count; ++z)
            {
                GeoZPoint zone_seg_a = nodes[zone.Nodes[z - 1]];
                GeoZPoint zone_seg_b = nodes[zone.Nodes[z]];

                if (road_pt.Longitude == zone_seg_b.Longitude && zone_seg_b.Latitude >= road_pt.Latitude)
                {
                    // end of the segment is right above us
                    if (zone_seg_b.Latitude == road_pt.Latitude)
                    {
                        cross_count = 1;
                        debug.AddCrossPoint(road_pt, zone_seg_b, onEdge: true);
                        break; // the road point lies exectly on polygon, we can stop checking right now
                    }
                    else
                    {
                        // do not change the cross count because it is hard case (but we can hope other points from the road will suffice)
                        //  ^
                        // / \
                        //  .
                        // such point looks like being inside (it hits the vertex)
                        // but this one also hits the vertex
                        //  /
                        // <
                        // .\
                        // but it is outside
                        debug.MarkTaintedPoint(road_pt);
                        cross_count = 0;
                        break;
                    }
                }

                if (zone_seg_a.Longitude < road_pt.Longitude && zone_seg_b.Longitude < road_pt.Longitude)
                    continue;
                if (zone_seg_a.Longitude > road_pt.Longitude && zone_seg_b.Longitude > road_pt.Longitude)
                    continue;
                if (zone_seg_a.Latitude < road_pt.Latitude && zone_seg_b.Latitude < road_pt.Latitude)
                    continue;

                if (calc.CheckArcSegmentIntersection(zone_seg_a, zone_seg_b, road_pt,
                    // north pole, buggy, but it will work for now,
                    // we cannot take the opposite point of the globe, because those two points make ambigous segment (infitite number of great circles)
                    slicer.GetSlicePoint(road_pt),
                    out GeoZPoint cx))
                {
                    ++cross_count;
                    debug.AddCrossPoint(road_pt, cx, onEdge: false);
                }
            }

            return cross_count % 2 == 1;
        }
        private static bool isRiver(OsmGeo element, out RiverKind riverKind)
        {
            if (element.Tags.TryGetValue("waterway", out string river_value))
            {
                if (river_value == "river")
                {
                    riverKind = RiverKind.River;
                    return true;
                }
                else if (river_value == "stream")
                {
                    riverKind = RiverKind.Stream;
                    return true;
                }

            }

            riverKind = default;
            return false;

        }

        private static CityRank parseCityRank(OsmGeo element, string place_value)
        {
            // https://wiki.openstreetmap.org/wiki/Key:capital
            string? capital = getString(element, "capital");
            if (capital == "yes")
                return CityRank.Capital;

            if (int.TryParse(capital, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level))
            {
                switch (level)
                {
                    case 0: throw new NotImplementedException($"{element}");
                    case 1: return CityRank.Important1;
                    case 2: return CityRank.Important2;
                    case 3: return CityRank.Important3;
                    case 4: return CityRank.Important4;
                }
            }

            if (place_value == "city")
                return CityRank.City;
            else if (place_value == "town")
                return CityRank.Town;
            else if (place_value == "village")
                return CityRank.Village;
            else if (place_value == "hamlet")
                return CityRank.Hamlet;
            else
                return CityRank.Other;
        }

        private static bool hasBikeLine(OsmGeo element)
        {
            // https://wiki.openstreetmap.org/wiki/Key:cycleway
            return (element.Tags.TryGetValue("cycleway", out string cycleway_value) && cycleway_value != "no")
                || (element.Tags.TryGetValue("bicycle", out string bicycle_value) && bicycle_value == "yes");
        }

        private static RoadSmoothness parseSmoothness(OsmGeo element)
        {
            // https://wiki.openstreetmap.org/wiki/Key:smoothness
            if (!element.Tags.TryGetValue("smoothness", out string smoothness_value))
                return RoadSmoothness.Bad;

            switch (smoothness_value)
            {
                case "excellent": return RoadSmoothness.Excellent;
                case "good": return RoadSmoothness.Good;
                case "intermediate": return RoadSmoothness.Intermediate;
                case "bad": return RoadSmoothness.Bad;
                case "very_bad": return RoadSmoothness.VeryBad;
                case "horrible": return RoadSmoothness.Horrible;
                case "very_horrible": return RoadSmoothness.VeryHorrible;
                case "impassable": return RoadSmoothness.Impassable;
            }

            return RoadSmoothness.Bad;
        }

        private static bool isRoundabout(OsmGeo element)
        {
            if (!element.Tags.TryGetValue("junction", out string junction_value))
                return false;

            if (junction_value == "roundabout")
                return true;

            return false;
        }

        private int? readSpeedLimit(OsmGeo element)
        {
            const string maxspeed_key = "maxspeed";

            if (!element.Tags.TryGetValue(maxspeed_key, out string value))
                return null;

            if (knownMaxSpeedErrors.Contains(value))
                return null;

            bool mph = value.EndsWith("mph");
            value = value.Replace("kph", "").Replace("mph", "").Trim();

            return value.Split(';')
                .Select(it =>
                {
                    if (!int.TryParse(it, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                        throw new ArgumentException($"Unable to parse {maxspeed_key} = {it}.");

                    if (mph)
                        result = (int) Math.Round(result * 1.60934);

                    return result;
                }).Max();
        }

        private sbyte readLayer(OsmGeo element)
        {
            if (!element.Tags.TryGetValue("layer", out string layer_value))
                return 0;

            if (layer_value.Contains(';'))
            {
                logger.Warning($"Way {element.Id} contains incorrect layer value {layer_value}");
                layer_value = layer_value.Split(";").First();
            }

            var direct = double.Parse(layer_value, CultureInfo.InvariantCulture); // AFAIK it should be integer...

            var simplified = (sbyte)Math.Round(direct * 10);

            if (simplified/10!=direct)
            {
                logger.Warning($"Way {element.Id} contains fraction layer value {layer_value}");
            }

            return simplified;
        }

        private static bool parseOneWay(OsmGeo element)
        {
            return element.Tags.TryGetValue("oneway", out string oneway_value) && oneway_value == "yes";
        }

        private static bool parseAccess(OsmGeo element)
        {
            {
                if (element.Tags.TryGetValue("access", out string value) && (value == "no" || value == "none" || value == "private"))
                    return false;
            }

            int count = 2;
            {
                if (element.Tags.TryGetValue("bicycle", out string value) && (value == "no" || value == "none"))
                    --count;
            }
            {
                if (element.Tags.TryGetValue("foot", out string value) && (value == "no" || value == "none"))
                    --count;
            }

            return count > 0;
        }

        public static bool bikeDismount(OsmGeo element)
        {
            return element.Tags.TryGetValue("bicycle", out string value) && value == "dismount";
        }

        private static RoadSurface parseSurface(OsmGeo element)
        {
            // https://wiki.openstreetmap.org/wiki/Key:surface
            if (!element.Tags.TryGetValue("surface", out string surface_value))
                return RoadSurface.Unknown;

            switch (surface_value)
            {
                case "paved": return RoadSurface.Paved;

                case "asphalt":
                case "concrete": return RoadSurface.AsphaltLike;

                case "concrete:lanes":
                case "concrete:plates": return RoadSurface.HardBlocks;

                case "paving_stones":
                case "sett": return RoadSurface.AsphaltLike;

                case "unhewn_cobblestone":
                case "cobblestone":
                case "cobblestone:flattened": return RoadSurface.HardBlocks;

                case "metal": return RoadSurface.AsphaltLike;

                case "wood": return RoadSurface.Wood;

                case "unpaved": return RoadSurface.Unpaved;

                case "compacted":
                case "fine_gravel":
                case "gravel":
                case "pebblestone":
                case "ground":
                case "dirt":
                case "earth": return RoadSurface.DirtLike;

                case "grass": return RoadSurface.GrassLike;
                case "grass_paver": return RoadSurface.HardBlocks;

                case "mud":
                case "sand": return RoadSurface.SandLike;
                case "woodchips": return RoadSurface.GrassLike;
                case "snow": return RoadSurface.SandLike;
                case "ice": return RoadSurface.Ice;
                case "salt": return RoadSurface.DirtLike;
            }

            return RoadSurface.Unpaved;
        }

        private static bool isProtectedArea(OsmGeo element)
        {
            if (element.Tags.TryGetValue("boundary", out string boundary_value) && boundary_value == "national_park")
                return true;

            // we should have here such criteria that "Rezerwat Dolina Rzeki Brdy" https://www.openstreetmap.org/way/202500797#map=12/53.5927/17.9139
            // and "Rezerwat Nadgoplański Park Tysiąclecia" https://www.openstreetmap.org/way/202758615#map=12/52.5964/18.3571
            // are both positive matches, but "Zespół Parków Krajobrazowych Chełmińskiego i Nadwiślańskiego" https://www.openstreetmap.org/relation/2627097
            // and "Nadwiślański Park Krajobrazowy" https://www.openstreetmap.org/relation/2552829 
            // are negative matches
            // https://wiki.openstreetmap.org/wiki/Key:protect_class
            if (element.Tags.TryGetValue("leisure", out string leisure_value) && leisure_value == "nature_reserve"
                && element.Tags.TryGetValue("protect_class", out string protect_class_value))
            {
                int digits = protect_class_value.TakeWhile(x => char.IsDigit(x)).Count();
                if (int.TryParse(protect_class_value.AsSpan(0, digits), NumberStyles.Integer, CultureInfo.InvariantCulture, out int protect_class_int)
                    && protect_class_int <= 4)
                    return true;
            }

            return false;
        }

        private static bool isWater(OsmGeo element)
        {
            return (element.Tags.TryGetValue("natural", out string natural_value) && (natural_value == "water" || natural_value == "bay" || natural_value == "riverbank"))
                || (element.Tags.TryGetValue("place", out string place_value) && place_value == "sea");
        }

        private static List<List<long>> mutableMergeRailways(List<List<long>> railways)
        {
            for (int debug_iter = 0; true; ++debug_iter)
            {
                int starting_count = railways.Count;

                for (int i = railways.Count - 1; i >= 0; --i)
                {
                    var way = railways[i];
                    // we are going from the last to the first, so at given point we don't have to match current with last, because it was already matched as last to current
                    var all_else = railways.Take(i);

                    List<long> path;
                    if (matchesLine(way.First(), head: false, all_else, out path))
                    {
                        railways.RemoveAt(i);
                        path.AddRange(way.Skip(1));
                    }
                    else if (matchesLine(way.Last(), head: true, all_else, out path))
                    {
                        railways.RemoveAt(i);
                        path.InsertRange(0, way.SkipLast(1));
                    }
                    else if (matchesLine(way.First(), head: true, all_else, out path))
                    {
                        railways.RemoveAt(i);
                        path.InsertRange(0, way.Skip(1).Reverse());
                    }
                    else if (matchesLine(way.Last(), head: false, all_else, out path))
                    {
                        railways.RemoveAt(i);
                        path.AddRange(way.AsEnumerable().Reverse().Skip(1));
                    }
                }

                if (starting_count == railways.Count)
                    break;
            }

            return railways;
        }

        private static IEnumerable<NamedPolygon> mutableConvertRelationsToPolygons(ILogger logger,
            // road id -> node ids list
            IReadOnlyMap<long, RoadInfo> roads,
            IEnumerable<RelationInfo> relations,
            // forests, lakes, etc.
            string typeName)
        {
            var closed = new List<NamedPolygon>();

            foreach (RelationInfo rel_info in relations)
            {
                if (!rel_info.WayNodes.All(it => roads.ContainsKey(it)))
                {
                    logger.Warning($"{typeName} relation {rel_info.Name}/{rel_info.Id} with missing reference");
                    continue;
                }

                var outlines = new List<List<long>>();
                outlines.Add(roads[rel_info.WayNodes.First()].Nodes.ToList());
                moveToClosed(rel_info.Id, rel_info.Name, outlines, closed);
                rel_info.WayNodes.RemoveAt(0);

                for (int debug_iter = 0; rel_info.WayNodes.Any(); ++debug_iter)
                {
                    for (int i = 0; i < rel_info.WayNodes.Count; ++i)
                    {
                        var way = roads[rel_info.WayNodes[i]].Nodes;
                        List<long> path;
                        if (matchesLine(way.First(), head: false, outlines, out path))
                        {
                            rel_info.WayNodes.RemoveAt(i);
                            path.AddRange(way.Skip(1));
                            moveToClosed(rel_info.Id, rel_info.Name, outlines, closed);
                            goto rel_main_loop;
                        }
                        else if (matchesLine(way.Last(), head: true, outlines, out path))
                        {
                            rel_info.WayNodes.RemoveAt(i);
                            path.InsertRange(0, way.SkipLast(1));
                            moveToClosed(rel_info.Id, rel_info.Name, outlines, closed);
                            goto rel_main_loop;
                        }
                        else if (matchesLine(way.First(), head: true, outlines, out path))
                        {
                            rel_info.WayNodes.RemoveAt(i);
                            path.InsertRange(0, way.Skip(1).Reverse());
                            moveToClosed(rel_info.Id, rel_info.Name, outlines, closed);
                            goto rel_main_loop;
                        }
                        else if (matchesLine(way.Last(), head: false, outlines, out path))
                        {
                            rel_info.WayNodes.RemoveAt(i);
                            path.AddRange(way.Reverse().Skip(1));
                            moveToClosed(rel_info.Id, rel_info.Name, outlines, closed);
                            goto rel_main_loop;
                        }
                    }

                    // if we didn't match anything, simply add new polygon seed
                    outlines.Insert(0, roads[rel_info.WayNodes.First()].Nodes.ToList());
                    moveToClosed(rel_info.Id, rel_info.Name, outlines, closed);
                    rel_info.WayNodes.RemoveAt(0);

                rel_main_loop:;

                }

                if (outlines.Any())
                    throw new ArgumentException("We have open outline");
            }

            return closed;
        }

        private static void moveToClosed(long id, string name, List<List<long>> outlines, List<NamedPolygon> closed)
        {
            for (int i = outlines.Count - 1; i >= 0; --i)
                if (outlines[i].First() == outlines[i].Last())
                {
                    closed.Add(new NamedPolygon(id, name, outlines[i]));
                    outlines.RemoveAt(i);
                }
        }

        private static readonly List<long> emptyIndicesList = new List<long>();
        private static bool matchesLine(long nodeIndex, bool head, IEnumerable<List<long>> lines, out List<long> matchingLine)
        {
            foreach (var entry in lines)
                if (nodeIndex == (head ? entry.First() : entry.Last()))
                {
                    matchingLine = entry;
                    return true;
                }

            matchingLine = emptyIndicesList;
            return false;
        }


        private static bool isForest(OsmGeo element)
        {
            return (element.Tags.TryGetValue("natural", out string natural_value) && natural_value == "wood")
                                            || (element.Tags.TryGetValue("landuse", out string landuse_value) && landuse_value == "forest");
        }

        private static bool isRailway(OsmGeo element)
        {
            // we are looking for heavyweight railway system which can take a bike and it is useful on long trips, so tram or subway does not qualify -- it is only local

            // https://wiki.openstreetmap.org/wiki/Key:railway
            if (!element.Tags.TryGetValue("railway", out string railway_value))
                return false;

            if (railway_value == "abandoned" || railway_value == "construction" || railway_value == "disused" || railway_value == "miniature" || railway_value == "subway" || railway_value == "tram")
                return false;

            return true;
        }

        private static bool isNoZone(OsmGeo element)
        {
            return element.Tags.TryGetValue("landuse", out string landuse_value) && landuse_value == "military";
        }

        private static int? getInt(OsmGeo element, string key)
        {
            if (!element.Tags.TryGetValue(key, out string value))
                return null;

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                return result;
            else
                return null;
        }

        private static string? getString(OsmGeo element, string key)
        {
            if (!element.Tags.TryGetValue(key, out string value))
                return null;
            else
                return value;
        }

        private static bool tryParseWayKind(OsmGeo element, HashSet<string> otherValues, Dictionary<string, long> pathDesignatedKeys, ref bool hasBikeLane, out bool urbanSidewalk, out WayKind kind)
        {
            // https://wiki.openstreetmap.org/wiki/Key:highway

            if (element.Tags.TryGetValue("area", out string area_val) && area_val == "yes")
            {
                // this is not road per se, only adjacent place, see:
                // https://wiki.openstreetmap.org/wiki/Tag:highway%3Drest_area
                kind = default;
                urbanSidewalk = default;
                return false;
            }

            if (!element.Tags.TryGetValue("highway", out string way_value)
                || way_value == "proposed" || way_value == "construction" || way_value == "rest_area"
                || way_value == "raceway" || way_value == "bus_stop" || way_value == "platform"
                || way_value == "corridor" || way_value == "abandoned" || way_value == "traffic_island")
            {
                kind = default;
                urbanSidewalk = default;
                return false;
            }

            var way_kind = getWayKind(element, element.Id!.Value, element.Tags, way_value, pathDesignatedKeys, ref hasBikeLane, out urbanSidewalk);

            if (!way_kind.HasValue && !knownOtherRoads.Contains(way_value))
                otherValues.Add(way_value);

            kind = way_kind ?? WayKind.Unclassified;


            return true;
        }

        private static WayKind? getWayKind(OsmGeo element, long id, IEnumerable<Tag> tags, string wayValue, Dictionary<string, long> pathDesignatedKeys,
            ref bool hasBikeLane,
            out bool urbanSidewalk)
        {
            urbanSidewalk = default;

            switch (wayValue)
            {
                case "motorway": return WayKind.Highway;
                case "motorway_link": return WayKind.HighwayLink;
                case "trunk": return WayKind.Trunk;
                case "trunk_link": return WayKind.TrunkLink;
                case "primary": return WayKind.Primary;
                case "primary_link": return WayKind.PrimaryLink;
                case "secondary": return WayKind.Secondary;
                case "secondary_link": return WayKind.SecondaryLink;
                case "tertiary": return WayKind.Tertiary;
                case "tertiary_link": return WayKind.TertiaryLink;


                case "cycleway": return WayKind.Cycleway;

                case "steps": return WayKind.Steps;

                case "pedestrian":
                case "sidewalk": return WayKind.Footway;

                // https://wiki.openstreetmap.org/wiki/Tag:highway%3Dfootway
                case "footway":
                    {
                        if (element.Tags.TryGetValue("footway", out string footway_value) && footway_value == "sidewalk")
                        {
                            urbanSidewalk = true;
                        }

                        return WayKind.Footway;
                    }

                case "crossing": return WayKind.Crossing;

                case "residential":
                case "living_street":
                case "road":
                case "service":
                case "unclassified": return WayKind.Unclassified;

                case "path": // fixing somewhat vague path tagging
                    {
                        var designated = tags.Where(it => it.Value == "designated").Select(it => it.Key).ToHashSet();
                        if (designated.Count == 1 && designated.Contains("motor_vehicle"))
                        {
                            ; // just a path
                        }
                        else if (designated.Count > 0 && designated.Any(it => it != "bicycle" && it != "foot"))
                        {
                            pathDesignatedKeys.TryAdd(string.Join(" ", designated.OrderBy(it => it)), id);
                        }
                        else if (designated.Contains("foot"))
                        {
                            // if it is a path with only foot it is either pedestrain footway (with maybe bikelane along)
                            if (designated.Contains("bicycle"))
                                hasBikeLane = true;
                            return WayKind.Footway;
                        }
                        else if (designated.Contains("bicycle"))
                            return WayKind.Cycleway;

                        return WayKind.Path;
                    }

                // those tracks/paths are unstable (they don't have signs for example)
                case "track":
                case "bridleway": return WayKind.Path;
            }

            return null;
        }
    }
}