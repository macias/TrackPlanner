using System.IO;
using System.Linq;
using TrackPlanner.Shared;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Mapping
{
    public sealed class RoadGridMemory : RoadGrid
    {
        private readonly HashMap<CellCoord, RoadGridCell> cells;

        public int Count => this.cells.Count;
        
        public RoadGridMemory(ILogger logger, HashMap<CellCoord, RoadGridCell> cells, IWorldMap map, IGeoCalculator calc, int gridCellSize, string? debugDirectory, bool legacyGetNodeAllRoads)
        : base(logger, cells, map, calc, gridCellSize, debugDirectory, legacyGetNodeAllRoads)
        {
            this.cells = cells;
        }

        internal void Write(BinaryWriter writer)
        {
            var offsets = new WriterOffsets<CellCoord>(writer);

            // creating an array guarantees the same order of iteration in two loops
            var coords_array = this.cells.Keys.ToArray();
            foreach (var coords in coords_array)
            {
                coords.Write(writer);
                offsets.Register(coords);
            }

            foreach (var coords in coords_array)
            {
                offsets.AddOffset(coords);
                this.cells[coords].Write(writer);
            }

            offsets.WriteBackOffsets();
        }


        public override string GetStats()
        {
            return "no stats so far";
        }
    }
}