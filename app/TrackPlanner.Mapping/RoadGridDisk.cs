using TrackPlanner.Shared;

namespace TrackPlanner.Mapping
{
    public sealed class RoadGridDisk : RoadGrid
    {
        private readonly DiskDictionary<CellCoord, RoadGridCell> cells;

        public RoadGridDisk(ILogger logger, DiskDictionary<CellCoord, RoadGridCell> cells, IWorldMap map, IGeoCalculator calc,
            int gridCellSize, string? debugDirectory, bool legacyGetNodeAllRoads) : base(logger, cells, map, calc,
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