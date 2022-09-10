using MathUnit;

namespace TrackPlanner.Mapping
{
    public sealed class MemorySettings
    {
        public int CacheNodesLimit { get; set; }
        public int CacheRoadsLimit { get; set; }
        public int CacheCellsLimit { get; set; }
        public int CacheNodeToRoadsLimit { get; set; }
        public int GridCellSize { get; set; }
        public MapMode MapMode { get; set; }

        public MemorySettings()
        {
            this.MapMode = MapMode.HybridDisk;

            GridCellSize = 100;

            CacheCellsLimit = 1;
            CacheNodesLimit = 10_000;
            CacheRoadsLimit = 5_000;
            
            CacheNodeToRoadsLimit = CacheNodesLimit;
        }

    }

 
}