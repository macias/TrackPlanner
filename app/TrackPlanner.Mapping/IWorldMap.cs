using MathUnit;
using System.Collections.Generic;
using TrackPlanner.Shared;
using TrackPlanner.Mapping.Data;
using TrackPlanner.Storage.Data;

namespace TrackPlanner.Mapping
{
    public interface IWorldMap
    {
        IReadOnlyEnumerableDictionary<long, GeoZPoint> Nodes { get; }
        IReadOnlyEnumerableDictionary<long,RoadInfo> Roads { get; }

        IEnumerable<CityInfo> Cities { get; }
        Angle Eastmost { get; }
        IEnumerable<IEnumerable<long>> Forests { get; }
        Angle Northmost { get; }
        IEnumerable<IEnumerable<long>> NoZone { get; }
        IEnumerable<IEnumerable<long>> Protected { get; }
        IEnumerable<IEnumerable<long>> Railways { get; }
        IEnumerable<(RiverKind kind, IEnumerable<long> indices)> Rivers { get; }
        Angle Southmost { get; }
        IEnumerable<IEnumerable<long>> Waters { get; }
        Angle Westmost { get; }

        IEnumerable<RoadIndexLong> GetRoads(long nodeId);
        bool IsBikeFootRoadDangerousNearby(long nodeId);
        int LEGACY_RoadSegmentsDistanceCount(long roadId, int sourceIndex, int destIndex);
        RoadGrid CreateRoadGrid(int gridCellSize, string? debugDirectory);

        string GetStats();
    }
}