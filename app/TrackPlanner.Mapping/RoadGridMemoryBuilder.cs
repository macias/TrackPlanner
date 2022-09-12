using MathUnit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using TrackPlanner.Shared;
using TrackPlanner.Mapping;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Mapping
{
    public sealed class RoadGridMemoryBuilder
    {
        public int CellSize { get; }

        private readonly WorldMapMemory mapMemory;
        private readonly IGeoCalculator calc;
        private readonly ILogger logger;
        private readonly string? debugDirectory;

        public RoadGridMemoryBuilder(ILogger logger, WorldMapMemory mapMemory, IGeoCalculator calc, int gridCellSize, string? debugDirectory)
        {
            this.CellSize = gridCellSize;
            this.logger = logger;
            this.mapMemory = mapMemory;
            this.calc = calc;
            this.debugDirectory = debugDirectory;
        }
        
        public HashMap<CellIndex, RoadGridCell> BuildCells()
        {
            var cells = new HashMap<CellIndex, RoadGridCell>();

            RoadGridCell select_cell(in GeoZPoint current, out CellIndex cellIndex)
            {
                cellIndex = getCellIndex(current.Latitude, current.Longitude);

                if (!cells.TryGetValue(cellIndex, out RoadGridCell? cell))
                {
                    cell = new RoadGridCell(cellIndex);
                    cells.Add(cellIndex, cell);
                }

                return cell;
            }

            foreach ((long road_map_index, RoadInfo road) in this.mapMemory.GetAllRoads())
            {
                for (int i = 0; i < road.Nodes.Count - 1; ++i)
                {
                    var curr_idx = new RoadIndexLong(road_map_index, i);
                    GeoZPoint curr_point = this.mapMemory.GetPoint(curr_idx);
                    GeoZPoint next_point = this.mapMemory.GetPoint(curr_idx.Next());

                    RoadGridCell curr_cell = select_cell(curr_point, out var curr_cell_idx);
                    curr_cell.Add(curr_idx);
                    RoadGridCell next_cell = select_cell(next_point, out var next_cell_idx);
                    if (curr_cell != next_cell)
                    {
                        next_cell.Add(curr_idx);
                        if (!isAdjacentOrSame(curr_cell_idx, next_cell_idx))
                        {
                            // fill_mid_cells(curr_idx, dist, curr_cell_idx, next_cell_idx, curr_point, next_point);

                            var occupied = new HashSet<RoadGridCell>();
                            occupied.Add(curr_cell);
                            occupied.Add(next_cell);

                            // far from optimal -- from each corner of the cells calculate projection on the segment, compute in which
                            // cell crosspoint falls and register segment there
                            var corners = new HashSet<GeoZPoint>();

                            for (int lati=Math.Min( curr_cell_idx.LatitudeGridIndex, next_cell_idx.LatitudeGridIndex);lati<=Math.Max( curr_cell_idx.LatitudeGridIndex, next_cell_idx.LatitudeGridIndex);++lati)
                            for (int loni = Math.Min(curr_cell_idx.LongitudeGridIndex, next_cell_idx.LongitudeGridIndex); lati <= Math.Max(curr_cell_idx.LongitudeGridIndex, next_cell_idx.LongitudeGridIndex); ++loni)
                            {
                                foreach ((Angle lat, Angle lon) in getCellCorners(lati, loni))
                                {
                                    if (lat<curr_point.Latitude.Min(next_point.Latitude) 
                                        || lat>curr_point.Latitude.Max(next_point.Latitude)
                                        || lon<curr_point.Longitude.Min(next_point.Longitude) 
                                        || lat>curr_point.Longitude.Max(next_point.Longitude))
                                        continue;

                                    corners.Add(GeoZPoint.Create(lat, lon, null));
                                }
                            }

                            foreach (var pt in corners)
                            {
                                (_, var cx, _) = calc.GetDistanceToArcSegment( pt, curr_point, next_point );
                                RoadGridCell cx_cell = select_cell(cx, out _);
                                if (occupied.Add(cx_cell))
                                {
                                    cx_cell.Add(curr_idx);
                                }
                            }
                            
                        }
                    }
                }
            }

            {
                var debug_stats = cells.Values.Select(it => it.Count).OrderBy(x => x).ToArray();
                logger.Info($"Grid cells fill stats: count = {debug_stats.Length}, min = {debug_stats.First()}, median = {debug_stats[debug_stats.Length/2]}, max: {debug_stats.Last()}");
            }
            
            return cells;
        }

        private static bool isAdjacentOrSame(CellIndex indexA, CellIndex indexB)
        {
            return Math.Abs(indexA.LatitudeGridIndex - indexB.LatitudeGridIndex) + Math.Abs(indexA.LongitudeGridIndex- indexB.LongitudeGridIndex) <= 1;
        }

        private IEnumerable< (Angle lat, Angle lon)> getCellCorners(int latIndex, int lonIndex)
        {
            int lat_dir = Math.Sign(latIndex);
            int lon_dir = Math.Sign(lonIndex);
            
            yield return (Angle.FromDegrees(latIndex * 1.0 / this.CellSize), Angle.FromDegrees(lonIndex * 1.0 / this.CellSize));
            yield return (Angle.FromDegrees((latIndex+lat_dir) * 1.0 / this.CellSize), Angle.FromDegrees((lonIndex+lon_dir) * 1.0 / this.CellSize));
            yield return (Angle.FromDegrees((latIndex+lat_dir) * 1.0 / this.CellSize), Angle.FromDegrees(lonIndex * 1.0 / this.CellSize));
            yield return (Angle.FromDegrees(latIndex * 1.0 / this.CellSize), Angle.FromDegrees((lonIndex+lon_dir) * 1.0 / this.CellSize));
        }

        private CellIndex getCellIndex(Angle latitude, Angle longitude)
        {
            return new CellIndex()
            {
                LatitudeGridIndex = (int) (latitude.Degrees * this.CellSize),
                LongitudeGridIndex = (int) (longitude.Degrees * this.CellSize)
            };
        }



    }
}