using MathUnit;
using System.Collections.Generic;
using TrackPlanner.Shared;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Mapping
{
    public interface IWorldMap
    {
        IGrid Grid { get; }
        
        GeoZPoint GetPoint(long nodeId);
        IEnumerable<KeyValuePair<long, GeoZPoint>> GetAllNodes();

        Angle Eastmost { get; }
        Angle Northmost { get; }
        Angle Southmost { get; }
        Angle Westmost { get; }

        IEnumerable<RoadIndexLong> GetRoadsAtNode(long nodeId);
        bool IsBikeFootRoadDangerousNearby(long nodeId);
        int LEGACY_RoadSegmentsDistanceCount(long roadId, int sourceIndex, int destIndex);
       // RoadGrid createRoadGrid(int gridCellSize, string? debugDirectory);

        string GetStats();
        RoadInfo GetRoad(long roadMapIndex);
        IEnumerable<KeyValuePair<long, RoadInfo>> GetAllRoads();
    }
}