using System;
using System.Collections.Generic;
using MathUnit;
using TrackPlanner.Mapping.Data;
using TrackPlanner.Shared;
using TrackPlanner.Storage;

namespace TrackPlanner.Mapping.Disk
{
    internal sealed class TrueGridWorldMap : IWorldMap
    {
        private readonly ILogger logger;
        private readonly IdTranslationTable nodesTranslation;
        private readonly IdTranslationTable roadsTranslation;
        private readonly DiskDictionary<CellIndex, TrueGridCell> cells;
        private readonly IGrid grid;

        public Angle Eastmost { get;  }
        public Angle Northmost { get;  }
        public Angle Southmost{ get;  }
        public Angle Westmost{ get;  }
        public IGrid Grid => grid;

        public TrueGridWorldMap(ILogger logger,
            Angle northmost,
            Angle eastmost,
            Angle southmost,
            Angle westmost,
            IdTranslationTable nodesTranslation,
            IdTranslationTable roadsTranslation,
            DiskDictionary<CellIndex, TrueGridCell> cells,
            int gridCellSize, string? debugDirectory)
        {
            this.logger = logger;
            this.nodesTranslation = nodesTranslation;
            this.roadsTranslation = roadsTranslation;
            this.cells = cells;
            Southmost = southmost;
            Northmost = northmost;
            Eastmost = eastmost;
            Westmost = westmost;


            var calc = new ApproximateCalculator();

            this.grid = new RoadGridDisk<TrueGridCell>(logger, cells, this, calc,
                gridCellSize, debugDirectory, legacyGetNodeAllRoads: false);
        }
        

        public GeoZPoint GetPoint(long nodeId)
        {
            var world_id = this.nodesTranslation.Get(nodeId);
            return GetPoint(world_id);
        }

        private GeoZPoint GetPoint(WorldIdentifier nodeWorldId)
        {
            var cell = this.cells[nodeWorldId.CellIndex];
           return cell.GetPoint(nodeWorldId.EntityIndex);
        }

        public RoadInfo GetRoad(long roadId)
        {
            var world_id = this.roadsTranslation.Get(roadId);
            return GetRoad(world_id);
        }

        private RoadInfo GetRoad(WorldIdentifier roadWorldId)
        {
            var cell = this.cells[roadWorldId.CellIndex];
            return cell.GetRoad(roadWorldId.EntityIndex);
        }

        public IEnumerable<KeyValuePair<long, GeoZPoint>> GetAllNodes()
        {
            throw new System.NotImplementedException();
        }

        public IEnumerable<RoadIndexLong> GetRoadsAtNode(long nodeId)
        {
            var world_id = this.nodesTranslation.Get(nodeId);
            return GetRoadsAtNode(world_id);
        }

        private IEnumerable<RoadIndexLong> GetRoadsAtNode(WorldIdentifier nodeWorldId)
        {
            var cell = this.cells[nodeWorldId.CellIndex];
            return cell.GetRoads(nodeWorldId.EntityIndex);
        }

        public bool IsBikeFootRoadDangerousNearby(long nodeId)
        {
            throw new System.NotImplementedException();
        }

        public string GetStats()
        {
            return "stats not implemented";
        }


        public IEnumerable<KeyValuePair<long, RoadInfo>> GetAllRoads()
        {
            throw new System.NotImplementedException();
        }

    }
}
