using TrackPlanner.Data.Stored;
using System.Collections.Generic;
using TrackPlanner.Data;
using TrackPlanner.Turner.Implementation;
using TrackPlanner.Mapping;
using TrackPlanner.PathFinder;

namespace TrackPlanner.Turner
{
    public sealed class NodeTrackTurner
    {
        private readonly ILogger logger;
        private readonly IWorldMap map;
        private readonly SystemTurnerConfig sysConfig;

        public NodeTrackTurner(ILogger logger, IWorldMap map, string debugDirectory)
        {
            this.logger = logger;
            this.map = map;
            this.sysConfig = new SystemTurnerConfig() {DebugDirectory = debugDirectory};
        }

        public List<TurnInfo>  ComputeTurnPoints(IEnumerable<Placement> track, UserTurnerPreferences userPreferences)
        {
            return new NodeTurnWorker(logger, map, this.sysConfig, userPreferences)
                .ComputeTurnPoints(track);
        }
    }

}
