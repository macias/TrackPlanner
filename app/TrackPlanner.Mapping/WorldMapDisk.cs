using MathUnit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using OsmSharp;
using OsmSharp.IO.PBF;
using TrackPlanner.Shared;
using TrackPlanner.LinqExtensions;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Mapping
{
    public sealed class WorldMapDisk : IWorldMap
    {
        private static GeoZPoint loadNode(IReadOnlyList<BinaryReader> readers)
        {
            if (!readers.Any())
                throw new ArgumentException("No readers are given");
            
            GeoZPoint? result = null;
            foreach (var reader in readers)
            {
                var pt = GeoZPoint.Read(reader);
                if (result == null)
                    result = pt;
                else if (result != pt)
                    throw new Exception($"Nodes mismatch {result} vs {pt}");
            }
            
            return result!.Value;
        }

        private static RoadInfo loadRoad(IReadOnlyList<BinaryReader> readers)
        {
            RoadInfo? result = null;

            foreach (var reader in readers)
            {
                var road = RoadInfo.Read(reader);
                if (result == null)
                    result = road;
                else if (result != road)
                {
                    if (road.TryMergeWith(result.Value, out RoadInfo merged))
                        result = merged;
                    else
                        throw new ArgumentException($"Road {road.Identifier} mismatch, {road} vs {result}");
                }
            }

            return result!.Value;
        }

        private readonly NodeRoadsDiskDictionary nodeRoadReferences;


        private readonly IReadOnlySet<long> bikeFootDangerousNearbyNodes;
        private readonly DiskDictionary<long, RoadInfo> roads;
        private readonly DiskDictionary<long,GeoZPoint> nodes;

        public IReadOnlyEnumerableDictionary<long, GeoZPoint> Nodes => this.nodes;
        public IReadOnlyEnumerableDictionary<long, RoadInfo> Roads => this.roads;
        public IEnumerable<IEnumerable<long>> Railways => throw new InvalidOperationException("Map was loaded only with roads info.");
        public IEnumerable<IEnumerable<long>> Forests => throw new InvalidOperationException("Map was loaded only with roads info.");
        public IEnumerable<(RiverKind kind, IEnumerable<long> indices)> Rivers => throw new InvalidOperationException("Map was loaded only with roads info.");
        public IEnumerable<CityInfo> Cities => throw new InvalidOperationException("Map was loaded only with roads info.");
        public IEnumerable<IEnumerable<long>> Waters => throw new InvalidOperationException("Map was loaded only with roads info.");
        public IEnumerable<IEnumerable<long>> Protected => throw new InvalidOperationException("Map was loaded only with roads info.");

        public IEnumerable<IEnumerable<long>> NoZone => throw new InvalidOperationException("Map was loaded only with roads info.");
        public Angle Southmost { get; }
        public Angle Northmost { get; }
        public Angle Eastmost { get; }
        public Angle Westmost { get; }

        public WorldMapDisk(ILogger logger,
            Angle northmost,
            Angle eastmost ,
        Angle southmost ,
        Angle westmost ,
            DiskDictionary<long, GeoZPoint> nodes,
        DiskDictionary<long, RoadInfo> roads,
            NodeRoadsDiskDictionary backReferences,
            HashSet<long> dangerousNearbyNodes)
        {
            this.nodes = nodes;
            Southmost = southmost;
            Northmost = northmost;
            Eastmost = eastmost;
            Westmost = westmost;

            this.roads = roads;

            this.nodeRoadReferences = backReferences;
            this.bikeFootDangerousNearbyNodes = dangerousNearbyNodes;
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
            return $"{nameof(nodes)} = {this.nodes.GetStats()}; {nameof(this.roads)} = {this.roads.GetStats()}; {nameof(this.nodeRoadReferences)} = {this.nodeRoadReferences.GetStats()}";
        }

        public bool IsBikeFootRoadDangerousNearby(long nodeId)
        {
            //return this.bikeFootDangerousNearbyNodes.TryGetValue(roadId, out var indices) && indices.Contains(nodeId);
            return this.bikeFootDangerousNearbyNodes.Contains(nodeId);
        }
        
        internal static IDisposable Read(ILogger logger, IReadOnlyList<string> fileNames,MemorySettings memSettings,
            out IWorldMap map,out DiskDictionary<CellCoord, RoadGridCell> cells)
        {
            // Loaded HYBRID in 98.074381506 s
            
            
// this way of reading, i.e. with mapping sparse identifiers into array indices
// is not flexible but it allowed to use arrays instead of dictionaries and 
// it saved around 3GB for node to roads references

            {
                long? timestamp = null;

                // 3190 MB here
                Console.WriteLine("PRESS KEY BEFORE INIT STREAMS");
                Console.ReadLine();

                foreach (var fn in fileNames)
                {
                    using (var reader = new BinaryReader(new FileStream(fn, FileMode.Open, FileAccess.Read), Encoding.UTF8, leaveOpen: false))
                    {
                        var ts = reader.ReadInt64();
                        if (!timestamp.HasValue)
                            timestamp = ts;
                        else if (timestamp != ts)
                            throw new ArgumentException($"Maps are not sync, road names use different identifiers {fn}.");

                        var cell_size = reader.ReadInt32();
                        if (cell_size != memSettings.GridCellSize)
                            throw new ArgumentException($"Cell grid size mismatch in {fn}, expected {memSettings.GridCellSize}, actual {cell_size}");
                    }
                }
            }


            var node_sources = new List<ReaderOffsets<long>>(capacity: fileNames.Count);
            var road_sources = new List<ReaderOffsets<long>>(capacity: fileNames.Count);
            var cell_sources = new List<ReaderOffsets<CellCoord>>(capacity: fileNames.Count);

            // 3190 MB here
            Console.WriteLine("PRESS KEY FOR REAL READ");
            Console.ReadLine();

            Angle? total_north_most = null;
            Angle? total_east_most = null;
            Angle? total_south_most = null;
            Angle? total_west_most = null;
            
            var dangerous_nearby_nodes = new HashSet<long>();
            
            foreach (var fn in fileNames)
            {
                logger.Verbose($"Loading raw map from {fn}");

                var reader = new BinaryReader(new FileStream(fn, FileMode.Open, FileAccess.Read), Encoding.UTF8, leaveOpen: false);

                reader.ReadInt64(); // timestamp
                reader.ReadInt32(); // cell size

                Angle max_angle(Angle? a, Angle b) => a?.Max(b) ?? b;
                Angle min_angle(Angle? a, Angle b) => a?.Min(b) ?? b;
                
                var north_most = GeoZPoint.ReadFloatAngle(reader);
                var east_most = GeoZPoint.ReadFloatAngle(reader);
                var south_most = GeoZPoint.ReadFloatAngle(reader);
                var west_most = GeoZPoint.ReadFloatAngle(reader);

                total_north_most = max_angle(total_north_most, north_most);
                total_east_most = max_angle(total_east_most, east_most);
                total_south_most = min_angle(total_south_most, south_most);
                total_west_most = min_angle(total_west_most, west_most);
                
                var nodes_count = reader.ReadInt32();
                var roads_count = reader.ReadInt32();
                var cells_count = reader.ReadInt32();

                var roads_offset = reader.ReadInt64();
                var grid_offset = reader.ReadInt64();

                var node_offsets = new ReaderOffsets<long>(reader, nodes_count);

                for (int i = 0; i < nodes_count; ++i)
                {
                    var node_id = reader.ReadInt64();
                    node_offsets.AddReaderOffset(node_id);
                }

                foreach (var (id, offset) in node_offsets.OrderBy(it => it.Value)) // sort in order to get sequential read from disk
                {
                    reader.BaseStream.Seek(offset, SeekOrigin.Begin);

                    bool is_dangerous_nearby = reader.ReadBoolean();
                    if (is_dangerous_nearby)
                        dangerous_nearby_nodes.Add(id);
                }
                
                reader.BaseStream.Seek(roads_offset, SeekOrigin.Begin);

                var road_offsets = new ReaderOffsets<long>(reader, roads_count);

                for (int i = 0; i < roads_count; ++i)
                {
                    var road_id = reader.ReadInt64();
                    road_offsets.AddReaderOffset(road_id);
                }

                reader.BaseStream.Seek(grid_offset, SeekOrigin.Begin);

                var cell_offsets = new ReaderOffsets<CellCoord>(reader, cells_count);
                for (int i = 0; i < cells_count; ++i)
                {
                    var cell_coord = CellCoord.Read(reader);
                    cell_offsets.AddReaderOffset(cell_coord);
                }

                if (false)
                {
                    BinaryReader[] readers_arr = new[] { reader };

                    foreach (var (coords, cell_offset) in cell_offsets)
                    {
                        reader.BaseStream.Seek(cell_offset, SeekOrigin.Begin);
                        var cell = RoadGridCell.Load(readers_arr);
                        var road_ids = cell.Segments.Select(it => it.RoadMapIndex).Distinct().ToArray();
                        foreach (var rd_id in road_ids)
                        {
                            reader.BaseStream.Seek(road_offsets[rd_id], SeekOrigin.Begin);
                            var info = loadRoad(readers_arr);

                            foreach (var nd_id in info.Nodes)
                            {
                                reader.BaseStream.Seek(node_offsets[nd_id], SeekOrigin.Begin);
                                loadNode(readers_arr);
                            }
                        }
                    }
                }

                node_sources.Add(node_offsets);
                road_sources.Add(road_offsets);
                cell_sources.Add(cell_offsets);
            }

            // 27_309_382 nodes (exp: 28_137_923), 4_109_763 roads (exp: 4_185_103), 33_486_629 road points

            // 4323 MB --> +1133 MB (total) 
            Console.WriteLine("PRESS KEY FOR BACK REFS");
            Console.ReadLine();
            
            var start = Stopwatch.GetTimestamp();
            
            var nodes_to_roads = new NodeRoadsDiskDictionary (new DiskDictionary<long,List<RoadIndexLong>>(node_sources, GeoZPoint.DiskSizeOf, 
                NodeRoadsDiskDictionary.Load,memSettings.CacheNodeToRoadsLimit));

            logger.Verbose($"Nodes to roads references created in {(Stopwatch.GetTimestamp() - start - 0.0) / Stopwatch.Frequency}s");
            
            // 4323 MB --> +1133 MB (total) 
            Console.WriteLine("PRESS KEY BEFORE SETUP");
            Console.ReadLine();

            var nodes = new DiskDictionary<long, GeoZPoint>(node_sources, 1, loadNode, memSettings.CacheNodesLimit);
            var roads = new DiskDictionary<long, RoadInfo>(road_sources, 0, loadRoad, memSettings.CacheRoadsLimit);
            cells = new DiskDictionary<CellCoord, RoadGridCell>(cell_sources, 0, RoadGridCell.Load, memSettings.CacheCellsLimit);

            // 4323 MB --> +1133 MB (total) 
            Console.WriteLine("PRESS KEY BEFORE MAP");
            Console.ReadLine();

            var disk_map = new WorldMapDisk(logger,
                northmost:total_north_most!.Value,
                eastmost :total_east_most!.Value,
                southmost :total_south_most!.Value,
                westmost :total_west_most!.Value,
                nodes, roads, nodes_to_roads,
                dangerous_nearby_nodes);

            // 4323 MB --> +1133 MB (total) 
            Console.WriteLine("PRESS KEY BEFORE ALL DONE");
            Console.ReadLine();

            map = disk_map;
            return new CompositeDisposable(node_sources.Select(it => 
            {
                var (r, _) = it;
                return r;
            }));
        }


    }
}