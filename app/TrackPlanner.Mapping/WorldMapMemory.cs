﻿using MathUnit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TrackPlanner.Shared;
using TrackPlanner.LinqExtensions;
using TrackPlanner.Mapping.Data;
using TrackPlanner.Mapping.Disk;
using TrackPlanner.Storage;
using TrackPlanner.Storage.Data;

namespace TrackPlanner.Mapping
{
    public sealed class WorldMapMemory : IWorldMap
    {
        public static WorldMapMemory CreateOnlyRoads(ILogger logger,
            IReadOnlyMap<long, GeoZPoint> nodes,
            IReadOnlyMap<long, RoadInfo> roads,
            INodeRoadsDictionary backReferences)
        {
            return new WorldMapMemory(logger, nodes, roads, backReferences,
                forests: null, rivers: null, cities: null, waters: null, protectedArea: null, noZone: null, railways: null, onlyRoads: true);
        }
        private readonly IEnumerable<IEnumerable<long>>? _protected;
        private readonly IEnumerable<CityInfo>? cities;
        private readonly IEnumerable<IEnumerable<long>>? forests;

        private readonly INodeRoadsDictionary nodeRoadReferences;
        private readonly IEnumerable<IEnumerable<long>>? noZone;
        private readonly ILogger logger;
        private readonly bool onlyRoads;

        private readonly IEnumerable<IEnumerable<long>>? railways;
        private readonly IEnumerable<(RiverKind kind, IEnumerable<long> indices)>? rivers;
        private readonly IEnumerable<IEnumerable<long>>? waters;

        private IReadOnlySet<long>? bikeFootDangerousNearbyNodes;

        IReadOnlyEnumerableDictionary<long, GeoZPoint> IWorldMap.Nodes => this.Nodes;
        IReadOnlyEnumerableDictionary<long, RoadInfo> IWorldMap.Roads => this.Roads;
        public IReadOnlyMap<long, GeoZPoint> Nodes { get; }
        public IReadOnlyMap<long, RoadInfo> Roads { get; }
        public IEnumerable<IEnumerable<long>> Railways => this.railways ?? throw new InvalidOperationException("Map was loaded only with roads info.");
        public IEnumerable<IEnumerable<long>> Forests => this.forests ?? throw new InvalidOperationException("Map was loaded only with roads info.");
        public IEnumerable<(RiverKind kind, IEnumerable<long> indices)> Rivers => this.rivers ?? throw new InvalidOperationException("Map was loaded only with roads info.");
        public IEnumerable<CityInfo> Cities => this.cities ?? throw new InvalidOperationException("Map was loaded only with roads info.");
        public IEnumerable<IEnumerable<long>> Waters => this.waters ?? throw new InvalidOperationException("Map was loaded only with roads info.");
        public IEnumerable<IEnumerable<long>> Protected => this._protected ?? throw new InvalidOperationException("Map was loaded only with roads info.");

        public IEnumerable<IEnumerable<long>> NoZone => this.noZone ?? throw new InvalidOperationException("Map was loaded only with roads info.");
        public Angle Southmost { get; }
        public Angle Northmost { get; }
        public Angle Eastmost { get; }
        public Angle Westmost { get; }

        public WorldMapMemory(ILogger logger,
            IReadOnlyMap<long, GeoZPoint> nodes,
            IReadOnlyMap<long, RoadInfo> roads,
            INodeRoadsDictionary backReferences,
            List<IEnumerable<long>>? forests,
            List<(RiverKind kind, IEnumerable<long> indices)>? rivers,
            List<CityInfo>? cities,
            List<IEnumerable<long>>? waters,
            List<IEnumerable<long>>? protectedArea,
            List<IEnumerable<long>>? noZone,
            List<List<long>>? railways,
            bool onlyRoads)
        {
            this.Nodes = nodes;
            this.logger = logger;
            this.onlyRoads = onlyRoads;

            logger.Verbose($"Creating map with {roads.Count} roads");

            this.Roads = roads;
            if (!onlyRoads)
            {
                this.forests = forests;
                this.rivers = rivers;
                this.cities = cities;
                this.waters = waters;
                this._protected = protectedArea;
                this.noZone = noZone;
                this.railways = railways;
            }

            validate(Roads.SelectMany(it => it.Value.Nodes));
            if (!onlyRoads)
            {
                validate(forests!.SelectMany(it => it));
                validate(rivers!.SelectMany(it => it.indices));
                validate(cities!.Select(it => it.Node));
                validate(waters!.SelectMany(it => it));
                validate(railways!.SelectMany(it => it));
                validate(protectedArea!.SelectMany(it => it));
                validate(noZone!.SelectMany(it => it));
            }

            Southmost = this.Nodes.Min(n => n.Value.Latitude);
            Northmost = this.Nodes.Max(n => n.Value.Latitude);
            Eastmost = this.Nodes.Max(n => n.Value.Longitude);
            Westmost = this.Nodes.Min(n => n.Value.Longitude);

            this.nodeRoadReferences = backReferences;

        }


        // note we can get even for the same road multiple indices, example case: roundabouts -- "start" and "end" are at the same point
        public IEnumerable<RoadIndexLong> GetRoads(long nodeId)
        {
            return this.nodeRoadReferences[nodeId];
        }

        public int LEGACY_RoadSegmentsDistanceCount(long roadId, int sourceIndex, int destIndex)
        {
            int min_idx = Math.Min(sourceIndex, destIndex);
            int max_idx = Math.Max(sourceIndex, destIndex);

            int count = max_idx - min_idx;

            {
                // when end is looped  (EXCLUDING start-end)
                int conn_idx = this.Roads[roadId].Nodes.IndexOf(this.Roads[roadId].Nodes.Last());
                if (conn_idx != 0 && conn_idx != this.Roads[roadId].Nodes.Count - 1)
                {
                    count = Math.Min(count, this.Roads[roadId].Nodes.Count - 1 - max_idx + Math.Abs(conn_idx - min_idx));
                }
            }
            {
                // when start is looped (including start-end)
                int conn_idx = this.Roads[roadId].Nodes.Skip(1).IndexOf(this.Roads[roadId].Nodes.First());
                if (conn_idx != -1)
                {
                    ++conn_idx;

                    count = Math.Min(count, min_idx + Math.Abs(conn_idx - max_idx));
                }
            }

            // todo: add handling other forms of loops, like knots

            return count;
        }

        public string GetStats()
        {
            return "no stats so far";
        }

        public void SetDangerous(IReadOnlySet<long> dangerousNearbyNodes)
        {
            this.bikeFootDangerousNearbyNodes = dangerousNearbyNodes;
        }

        public bool IsBikeFootRoadDangerousNearby( long nodeId)
        {
            //return this.bikeFootDangerousNearbyNodes.TryGetValue(roadId, out var indices) && indices.Contains(nodeId);
            return this.bikeFootDangerousNearbyNodes!.Contains(nodeId);
        }

        private void validate(IEnumerable<long> nodeReferences)
        {
            foreach (var node_id in nodeReferences)
                if (!Nodes.ContainsKey(node_id))
                    throw new KeyNotFoundException($"Cannot find reference node {node_id}");
        }

        internal void Write(long timestamp, Stream stream, RoadGridMemory grid)
        {
            if (!onlyRoads)
                throw new Exception("Can write only roads map");

            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                //writer.Write(fileFormatVersion);
                writer.Write(timestamp);
                writer.Write(grid.CellSize);

                GeoZPoint.WriteFloatAngle(writer, this.Northmost);
                GeoZPoint.WriteFloatAngle(writer, this.Eastmost);
                GeoZPoint.WriteFloatAngle(writer, this.Southmost);
                GeoZPoint.WriteFloatAngle(writer, this.Westmost);

                writer.Write(Nodes.Count);
                writer.Write(Roads.Count);
                writer.Write(grid.Count);

                var roads_offset = writer.BaseStream.Position;
                writer.Write(-1L);
                var cells_offset = writer.BaseStream.Position;
                writer.Write(-1L);

                // ---- writing nodes -----------------------------
                {
                    // we create array to make sure we have the same order while iterating in two loops (C# framework does not guarantee this) 
                    var map_indices = Nodes.Keys.OrderBy(x =>x).ToArray();

                    var offsets = new WriterOffsets<long>(writer);

                    foreach (var map_idx in map_indices)
                    {
                        writer.Write(map_idx);
                        offsets.Register(map_idx);
                    }

                    foreach (var map_idx in map_indices)
                    {
                       var debug_pos = offsets.AddOffset(map_idx);
                        writer.Write(this.bikeFootDangerousNearbyNodes!.Contains(map_idx));
                        Nodes[map_idx].Write(writer);
                        if (writer.BaseStream.Position - debug_pos != NodeRoadsDiskDictionary.NodeDataDiskSize)
                            throw new NotSupportedException("Incorrect size of the node data.");
                        this.nodeRoadReferences.Write(writer, map_idx);
                    }

                    offsets.WriteBackOffsets();
                }

                {
                    var curr_pos = writer.BaseStream.Position;
                    writer.BaseStream.Seek(roads_offset, SeekOrigin.Begin);
                    writer.Write(curr_pos);
                    writer.BaseStream.Seek(curr_pos, SeekOrigin.Begin);
                }
                // ---- writing roads -----------------------------------
                {
                    var map_indices = Roads.Keys.OrderBy(x => x).ToArray();

                    var offsets = new WriterOffsets<long>(writer);

                    foreach (var map_idx in map_indices)
                    {
                        writer.Write(map_idx);
                        offsets.Register(map_idx);
                    }

                    foreach (var map_idx in map_indices)
                    {
                        offsets.AddOffset(map_idx);
                        Roads[map_idx].Write(writer);
                    }

                    offsets.WriteBackOffsets();
                }

                {
                    var curr_pos = writer.BaseStream.Position;
                    writer.BaseStream.Seek(cells_offset, SeekOrigin.Begin);
                    writer.Write(curr_pos);
                    writer.BaseStream.Seek(curr_pos, SeekOrigin.Begin);
                }

                // ---- writing grid ---------------------------
                
                grid.Write(writer);

            }
        }

        private static WorldMapMemory ReadDictionary_OBSOLETE(ILogger logger, IEnumerable<string> fileNames,
            out List<string> invalidFiles)
        {
            long? timestamp = null;
            var total_nodes_count = 0;
            var total_roads_count = 0;

            //Console.WriteLine("PRESS KEY BEFORE STREAMS");
            //Console.ReadLine();

            invalidFiles = new List<string>();
            
            foreach (var fn in fileNames)
            {
                using (var reader = new BinaryReader(new FileStream(fn, FileMode.Open, FileAccess.Read), Encoding.UTF8, leaveOpen: false))
                {
                    /*var curr_version = reader.ReadInt64();
                    if ( curr_version != fileFormatVersion)
                    {
                        invalidFiles.Add(fn);
                        logger.Warning($"File {fn} uses format {curr_version}, supported {fileFormatVersion}");
                        continue;
                    }*/
                    var ts = reader.ReadInt64();
                    if (!timestamp.HasValue)
                        timestamp = ts;
                    else if (timestamp != ts)
                        throw new ArgumentException($"Maps are not sync, road names use different identifiers {fn}.");

                    total_roads_count += reader.ReadInt32();
                    total_nodes_count += reader.ReadInt32();
                }
            }

            // here: 3 GB taken 

            //Console.WriteLine("PRESS KEY FOR REAL READ");
            //Console.ReadLine();

            var nodes = MapFactory.CreateFast<long, GeoZPoint>(total_nodes_count);
            var roads = MapFactory.CreateFast<long, RoadInfo>(total_roads_count);
            //var road_lengths = MapFactory.CreateFast<long, int>(total_roads_count);

            //var nodes = MapFactory.CreatePacked<long, GeoZPoint>(total_nodes_count);
            //var roads = MapFactory.CreatePacked<long, RoadInfo>(total_roads_count);

            /*if (false)
            {
                foreach (var fn in fileNames)
                {
                    logger.Verbose($"Loading raw map from {fn}");

                    using (var reader = new BinaryReader(new FileStream(fn, FileMode.Open, FileAccess.Read), Encoding.UTF8, leaveOpen: false))
                    {
                        reader.ReadInt64();

                        var roads_count = reader.ReadInt32();
                        reader.ReadInt32();

                        {
                            for (int i = 0; i < roads_count; ++i)
                            {
                                RoadInfo.ProbeRead(reader, out long id, out int length);
                                if (!road_lengths.TryAdd(id, length, out var existing) && length != existing)
                                    throw new Exception($"Road {id} with {existing} nodes already exists, while trying to add with {length}");
                            }
                        }
                    }
                }
            }*/

            foreach (var fn in fileNames)
            {
                logger.Verbose($"Loading raw map from {fn}");

                using (var reader = new BinaryReader(new FileStream(fn, FileMode.Open, FileAccess.Read), Encoding.UTF8, leaveOpen: false))
                {
                    reader.ReadInt64();

                    var roads_count = reader.ReadInt32();
                    var nodes_count = reader.ReadInt32();


                    {

                        for (int i = 0; i < roads_count; ++i)
                        {
                            var road = RoadInfo.Read(reader);
                            if (!roads.TryAdd(road.Identifier, road, out RoadInfo existing) && road != existing)
                            {
                                if (road.TryMergeWith(existing, out RoadInfo merged))
                                    roads[road.Identifier] = merged;
                                else
                                    throw new ArgumentException($"Road {road.Identifier} already exists with {road}, while we have {existing}");
                            }
                        }
                    }

                    {
                        for (int i = 0; i < nodes_count; ++i)
                        {
                            var id = reader.ReadInt64();
                            var pt = GeoZPoint.Read(reader);
                            if (!nodes.TryAdd(id, pt, out GeoZPoint existing) && pt != existing)
                                throw new Exception($"Node {id} already exists at {existing}, while trying to add at {pt}");
                        }
                    }

                }
            }

            // here: 4.7 GB (+1.7GB) taken 
            // 28_137_923 nodes, 4_185_103 roads, 33_486_629 road points
            logger.Verbose($"All maps loaded {total_nodes_count} nodes, {total_roads_count} roads, {roads.Sum(it => it.Value.Nodes.Count)} road points");
            //Console.WriteLine("PRESS KEY FOR BACK REFS");
            //Console.ReadLine();
            var start = Stopwatch.GetTimestamp();
            var nodes_to_roads = new NodeRoadsAssocDictionary(nodes, roads);

            // here: 8.3 GB (+3.6GB) taken 

            logger.Verbose($"Nodes to roads references created in {(Stopwatch.GetTimestamp() - start - 0.0) / Stopwatch.Frequency}s");
            //Console.WriteLine("PRESS KEY STOP");
            //Console.ReadLine();

            return WorldMapMemory.CreateOnlyRoads(logger, nodes, roads, nodes_to_roads);
        }

        internal static WorldMapMemory ReadMappedArray(ILogger logger, IEnumerable<string> fileNames,
            out List<string> invalidFiles)
        {
            // Loaded MEM in 131.860504244 s

// this way of reading, i.e. with mapping sparse identifiers into array indices
// is not flexible but it allowed to use arrays instead of dictionaries and 
// it saved around 3GB for node to roads references

            long? timestamp = null;
            var total_nodes_count = 0;
            var total_roads_count = 0;

            //Console.WriteLine("PRESS KEY BEFORE STREAMS");
            //Console.ReadLine();
            invalidFiles = new List<string>();
            foreach (var fn in fileNames)
            {
                using (var reader = new BinaryReader(new FileStream(fn, FileMode.Open, FileAccess.Read), Encoding.UTF8, leaveOpen: false))
                {
                    /*var curr_version = reader.ReadInt64();
                    if (curr_version != fileFormatVersion)
                    {
                        invalidFiles.Add(fn);
                        logger.Warning($"File {fn} uses format {curr_version}, supported {fileFormatVersion}");
                        continue;
                    }*/
                    var ts = reader.ReadInt64();
                    if (!timestamp.HasValue)
                        timestamp = ts;
                    else if (timestamp != ts)
                        throw new ArgumentException($"Maps are not sync, road names use different identifiers {fn}.");

                    reader.ReadInt32(); // cell size

                    GeoZPoint.ReadFloatAngle(reader);
                    GeoZPoint.ReadFloatAngle(reader);
                    GeoZPoint.ReadFloatAngle(reader);
                    GeoZPoint.ReadFloatAngle(reader);


                    var nodes_count = reader.ReadInt32();
                    var roads_count = reader.ReadInt32();

                    total_nodes_count += nodes_count;
                    total_roads_count += roads_count;
                    
                    logger.Info($"{fn} stats: {nameof(nodes_count)} {nodes_count}, {nameof(roads_count)} {roads_count}.");
                }
            }

            // here: 3 GB taken 

            //Console.WriteLine("PRESS KEY FOR REAL READ");
            //Console.ReadLine();

            var nodes_mapping = new Dictionary<long, long>(capacity: total_nodes_count);
            var roads_mapping = new Dictionary<long, long>(capacity: total_roads_count);

            var nodes = new ArrayLong<GeoZPoint>(capacity: total_nodes_count);
            var roads = new ArrayLong<RoadInfo>(total_roads_count);

            var dangerous_nearby_nodes = new HashSet<long>();
            
            foreach (var fn in fileNames)
            {
                logger.Verbose($"Loading raw map from {fn}");

                using (var reader = new BinaryReader(new FileStream(fn, FileMode.Open, FileAccess.Read), Encoding.UTF8, leaveOpen: false))
                {
                    reader.ReadInt64(); // timestamp
                    reader.ReadInt32(); // cell size

                    GeoZPoint.ReadFloatAngle(reader);
                    GeoZPoint.ReadFloatAngle(reader);
                    GeoZPoint.ReadFloatAngle(reader);
                    GeoZPoint.ReadFloatAngle(reader);

                    var nodes_count = reader.ReadInt32();
                    var roads_count = reader.ReadInt32();
                    reader.ReadInt32(); // cells count

                    var roads_offset = reader.ReadInt64();
                    reader.ReadInt64(); // grid offset

                    {
                        var offsets = new ReaderOffsets<long>(reader, nodes_count);
                        for (int i = 0; i < nodes_count; ++i)
                        {
                            var node_id = reader.ReadInt64();
                            //node_ids.Add(node_id);
                            offsets.AddReaderOffset(node_id);
                        }

                        foreach (var (id, offset) in offsets.Offsets.OrderBy(it => it.Value)) // sort in order to get sequential read from disk
                        {
                            reader.BaseStream.Seek(offset, SeekOrigin.Begin);

                            bool is_dangerous_nearby = reader.ReadBoolean();
                            var pt = GeoZPoint.Read(reader);
                            if (!nodes_mapping.TryGetValue(id, out var node_index))
                            {
                                node_index = nodes_mapping.Count;
                                nodes_mapping.Add(id, node_index);
                                nodes.Add(pt);
                            }
                            else
                            {
                                var existing = nodes[node_index];
                                if (pt != existing)
                                    throw new Exception($"Node {id} already exists at {existing}, while trying to add at {pt}");
                            }
                            
                            if (is_dangerous_nearby)
                                dangerous_nearby_nodes.Add(node_index);
                        }
                    }

                    reader.BaseStream.Seek(roads_offset, SeekOrigin.Begin);

                    {
                        for (int i = 0; i < roads_count; ++i)
                        {
                            reader.ReadInt64(); // road id
                            reader.ReadInt64(); // road offsets
                        }

                        for (int i = 0; i < roads_count; ++i)
                        {
                            var road = RoadInfo.Read(reader, nodes_mapping);
                            if (!roads_mapping.TryGetValue(road.Identifier, out var road_index))
                            {
                                road_index = roads_mapping.Count;
                                roads_mapping.Add(road.Identifier, road_index);
                                roads.Add(road);
                            }
                            else
                            {
                                var existing = roads[road_index];
                                if (road != existing)
                                {
                                    if (road.TryMergeWith(existing, out RoadInfo merged))
                                        roads[road_index] = merged;
                                    else
                                        throw new ArgumentException($"Road {road.Identifier} already exists with {road}, while we have {existing}");
                                }
                            }
                        }
                    }


                }
            }

            nodes.TrimExcess();
            roads.TrimExcess();

            // here: 4.7 GB (+1.7GB) taken 
            // 27_309_382 nodes (exp: 28_137_923), 4_109_763 roads (exp: 4_185_103), 33_486_629 road points
            logger.Verbose($"All maps loaded {nodes.Count} nodes (exp: {total_nodes_count}), {roads.Count} roads (exp: {total_roads_count}), {roads.Sum(it => it.Value.Nodes.Count)} road points");
            //Console.WriteLine("PRESS KEY FOR BACK REFS");
            //Console.ReadLine();
            var start = Stopwatch.GetTimestamp();
            NodeRoadsLongDictionary nodes_to_roads = new NodeRoadsLongDictionary(roads);

            // here: 5.7 GB (+1GB) taken 

            logger.Verbose($"Nodes to roads references created in {(Stopwatch.GetTimestamp() - start - 0.0) / Stopwatch.Frequency}s");
            //Console.WriteLine("PRESS KEY STOP");
            //Console.ReadLine();

            var map = WorldMapMemory.CreateOnlyRoads(logger, nodes, roads, nodes_to_roads);
            map.bikeFootDangerousNearbyNodes = dangerous_nearby_nodes;
            
            return map;
        }

        // private const long fileFormatVersion = 111;
        
                internal static WorldMapMemory ReadRawArray(ILogger logger, IEnumerable<string> fileNames,
                    out List<string> invalidFiles)
        {
            // Loaded MEM in 131.860504244 s

// this way of reading, i.e. with mapping sparse identifiers into array indices
// is not flexible but it allowed to use arrays instead of dictionaries and 
// it saved around 3GB for node to roads references

            long? timestamp = null;
            var total_nodes_count = 0;
            var total_roads_count = 0;

            //Console.WriteLine("PRESS KEY BEFORE STREAMS");
            //Console.ReadLine();

            invalidFiles = new List<string>();
            
            foreach (var fn in fileNames)
            {
                using (var reader = new BinaryReader(new FileStream(fn, FileMode.Open, FileAccess.Read), Encoding.UTF8, leaveOpen: false))
                {
                    /*var curr_version = reader.ReadInt64();
                    if (curr_version != fileFormatVersion)
                    {
                        invalidFiles.Add(fn);
                        logger.Warning($"File {fn} uses format {curr_version}, supported {fileFormatVersion}");
                        continue;
                    }*/

                    var ts = reader.ReadInt64();
                    if (!timestamp.HasValue)
                        timestamp = ts;
                    else if (timestamp != ts)
                        throw new ArgumentException($"Maps are not sync, road names use different identifiers {fn}.");

                    reader.ReadInt32(); // cell size

                    GeoZPoint.ReadFloatAngle(reader);
                    GeoZPoint.ReadFloatAngle(reader);
                    GeoZPoint.ReadFloatAngle(reader);
                    GeoZPoint.ReadFloatAngle(reader);


                    var nodes_count = reader.ReadInt32();
                    var roads_count = reader.ReadInt32();

                    total_nodes_count += nodes_count;
                    total_roads_count += roads_count;
                    
                    logger.Info($"{fn} stats: {nameof(nodes_count)} {nodes_count}, {nameof(roads_count)} {roads_count}.");
                }
            }

            // here: 3 GB taken 

            //Console.WriteLine("PRESS KEY FOR REAL READ");
            //Console.ReadLine();

            var nodes = new HashMap<long, GeoZPoint>(capacity: total_nodes_count);
            var roads = new HashMap<long, RoadInfo>(total_roads_count);

            var dangerous_nearby_nodes = new HashSet<long>();
            
            foreach (var fn in fileNames)
            {
                logger.Verbose($"Loading raw map from {fn}");

                using (var reader = new BinaryReader(new FileStream(fn, FileMode.Open, FileAccess.Read), Encoding.UTF8, leaveOpen: false))
                {
                    reader.ReadInt64(); // timestamp
                    reader.ReadInt32(); // cell size

                    GeoZPoint.ReadFloatAngle(reader);
                    GeoZPoint.ReadFloatAngle(reader);
                    GeoZPoint.ReadFloatAngle(reader);
                    GeoZPoint.ReadFloatAngle(reader);

                    var nodes_count = reader.ReadInt32();
                    var roads_count = reader.ReadInt32();
                    reader.ReadInt32(); // cells count

                    var roads_offset = reader.ReadInt64();
                    reader.ReadInt64(); // grid offset

                    {
                        var offsets = new ReaderOffsets<long>(reader, nodes_count);
                        for (int i = 0; i < nodes_count; ++i)
                        {
                            var node_id = reader.ReadInt64();
                            //node_ids.Add(node_id);
                            offsets.AddReaderOffset(node_id);
                        }

                        foreach (var (id, offset) in offsets.Offsets.OrderBy(it => it.Value)) // sort in order to get sequential read from disk
                        {
                            reader.BaseStream.Seek(offset, SeekOrigin.Begin);

                            bool is_dangerous_nearby = reader.ReadBoolean();
                            var pt = GeoZPoint.Read(reader);
                            if (!nodes.TryGetValue(id, out var existing))
                            {
                                nodes.Add(id,pt);
                            }
                            else
                            {
                                if (pt != existing)
                                    throw new Exception($"Node {id} already exists at {existing}, while trying to add at {pt}");
                            }
                            
                            if (is_dangerous_nearby)
                                dangerous_nearby_nodes.Add(id);
                        }
                    }

                    reader.BaseStream.Seek(roads_offset, SeekOrigin.Begin);

                    {
                        for (int i = 0; i < roads_count; ++i)
                        {
                            reader.ReadInt64(); // road id
                            reader.ReadInt64(); // road offsets
                        }

                        for (int i = 0; i < roads_count; ++i)
                        {
                            var road = RoadInfo.Read(reader);
                            if (!roads.TryGetValue(road.Identifier, out var existing))
                            {
                                roads.Add(road.Identifier, road);
                            }
                            else
                            {
                                if (road != existing)
                                {
                                    if (road.TryMergeWith(existing, out RoadInfo merged))
                                        roads[road.Identifier] = merged;
                                    else
                                        throw new ArgumentException($"Road {road.Identifier} already exists with {road}, while we have {existing}");
                                }
                            }
                        }
                    }


                }
            }

            nodes.TrimExcess();
            roads.TrimExcess();

            // here: 4.7 GB (+1.7GB) taken 
            // 27_309_382 nodes (exp: 28_137_923), 4_109_763 roads (exp: 4_185_103), 33_486_629 road points
            logger.Verbose($"All maps loaded {nodes.Count} nodes (exp: {total_nodes_count}), {roads.Count} roads (exp: {total_roads_count}), {roads.Sum(it => it.Value.Nodes.Count)} road points");
            //Console.WriteLine("PRESS KEY FOR BACK REFS");
            //Console.ReadLine();
            var start = Stopwatch.GetTimestamp();
            var nodes_to_roads = new NodeRoadsAssocDictionary(nodes,roads);

            // here: 5.7 GB (+1GB) taken 

            logger.Verbose($"Nodes to roads references created in {(Stopwatch.GetTimestamp() - start - 0.0) / Stopwatch.Frequency}s");
            //Console.WriteLine("PRESS KEY STOP");
            //Console.ReadLine();

            var map = WorldMapMemory.CreateOnlyRoads(logger, nodes, roads, nodes_to_roads);
            map.bikeFootDangerousNearbyNodes = dangerous_nearby_nodes;
            
            return map;
        }

                RoadGrid IWorldMap.CreateRoadGrid(int gridCellSize, string? debugDirectory)
                {
                    return this.CreateRoadGrid(gridCellSize, debugDirectory);
                }
                
        public RoadGridMemory CreateRoadGrid(int gridCellSize,string? debugDirectory)
        {
            var calc = new ApproximateCalculator();
            return new RoadGridMemory(logger,
                new RoadGridMemoryBuilder(logger, this, calc, gridCellSize, debugDirectory).BuildCells(),
                this, calc, gridCellSize, debugDirectory, legacyGetNodeAllRoads: false);
        }
        
        public void AttachDangerInNonMotorNodes( RoadGrid grid, Length highTrafficProximity)
        {
            long start = Stopwatch.GetTimestamp();

            // cycle/foot-way id -> node id (it is strange mapping, but retrieving indices during route searching is indirect, currently)
            var dangerous_nearby = new Dictionary<long, HashSet<long>>();
            // we no longer use this layout, but lets keep it here for the sake of history (maybe we will get back to this)

            foreach (var road in this.Roads.Values)
            {
                if (!road.IsDangerous)
                    continue;

                foreach (var node_id in road.Nodes)
                {
                    foreach (var snap in grid.GetSnaps(this.Nodes[node_id], highTrafficProximity,
                                 info => info.Layer == road.Layer && (info.Kind == WayKind.Cycleway || info.Kind == WayKind.Footway)))
                    {
                        var snapped_road = this.Roads[snap.RoadIdx.RoadMapIndex];

                        if (!dangerous_nearby.TryGetValue(snap.RoadIdx.RoadMapIndex, out HashSet<long>? node_indices))
                        {
                            node_indices = new HashSet<long>();
                            dangerous_nearby.Add(snap.RoadIdx.RoadMapIndex, node_indices);
                        }

                        node_indices.Add(snapped_road.Nodes[snap.RoadIdx.IndexAlongRoad]);
                    }
                }
            }

            logger.Info($"High traffic nodes {dangerous_nearby.Sum(it => it.Value.Count)} computed in {((Stopwatch.GetTimestamp() - start + 0.0) / Stopwatch.Frequency)}s");

            this.SetDangerous(dangerous_nearby.Values.SelectMany(x => x).ToHashSet());
        }



    }
}