using MathUnit;
using TrackPlanner.Mapping;

namespace TrackPlanner.PathFinder
{
    public sealed record class SystemConfiguration
    {
        public LinearCoefficients<Length> DefaultEstimateRatio { get;  }
        public Length InitSnapProximityLimit { get; set; }
        public Length FinalSnapProximityLimit { get; set; }
        public bool EnableDebugDumping { get; set; }
        public Length HighTrafficProximity { get; set; } // if cycleway is closer than given limit to the road we consider the cycleway as high traffic
        public bool DumpProgress { get; set; }
        public bool DoublePass { get; set; }
        public bool UseEstimateRatio{ get; set; }
        public bool CompactPreservesRoads { get; set; }
        public MemorySettings MemoryParams { get; set; }
        public bool DumpLowCost { get; set; }
        public bool DumpTooFar { get; set; }
        public bool DumpInRange { get; set; }
        public bool DumpDangerous { get; set; }

        public SystemConfiguration()
        {
            this.DefaultEstimateRatio = LinearCoefficients<Length>.Constant(1);
            
            InitSnapProximityLimit = Length.FromMeters(25);
            FinalSnapProximityLimit = Length.FromKilometers(10);
            HighTrafficProximity = Length.FromMeters(20);
            DumpProgress = false;
            DoublePass = false;
            UseEstimateRatio = false;
            MemoryParams = new MemorySettings();
        }

    }
}