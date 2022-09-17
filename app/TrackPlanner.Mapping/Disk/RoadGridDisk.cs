using TrackPlanner.Shared;
using TrackPlanner.Storage;

namespace TrackPlanner.Mapping.Disk
{
    public sealed class RoadGridDisk<TCell> : RoadGrid<TCell>
    where TCell:RoadGridCell
    {
        private readonly DiskDictionary<CellIndex, TCell> cells;

        public RoadGridDisk(ILogger logger, DiskDictionary<CellIndex, TCell> cells, 
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