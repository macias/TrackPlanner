using MathUnit;

namespace TrackPlanner.Turner
{
   
    public sealed class SystemTurnerConfig
    {
        public string? DebugDirectory { get; set; }
        public int GridCellSize { get; set; }

        public SystemTurnerConfig()
        {
            this.GridCellSize = 100;
        }
    }

}