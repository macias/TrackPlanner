using TrackPlanner.Shared;
using TrackPlanner.Storage;

namespace TrackPlanner.Mapping.Disk
{
    public sealed class RoadGridDisk : RoadGrid
    {
        private readonly DiskDictionary<CellIndex, RoadGridCell> cells;

        public RoadGridDisk(ILogger logger, DiskDictionary<CellIndex, RoadGridCell> cells, 
            IWorldMap map, IGeoCalculator calc,
            int gridCellSize, string? debugDirectory, bool legacyGetNodeAllRoads) 
            : base(logger, cells, map, calc,
            gridCellSize, debugDirectory, legacyGetNodeAllRoads)
        {
            this.cells = cells;
        }

        public override string GetStats()
        {
            return this.cells.GetStats();
        }
    }
}