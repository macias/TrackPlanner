using MathUnit;
using System.Collections.Generic;
using TrackPlanner.Data;
using TrackPlanner.Shared;

namespace TrackPlanner.Mapping
{
    public interface IGrid
    {
        int CellSize { get; }
        IGeoCalculator Calc { get; }

        List<RoadBucket> GetRoadBuckets(IReadOnlyList<RequestPoint> userTrack, Length proximityLimit, 
            Length upperProximityLimit, bool requireAllHits, bool singleMiddleSnaps);
        RoadBucket? GetRoadBucket(int index, GeoZPoint userPoint, Length initProximityLimit,
            Length finalProximityLimit, bool requireAllHits, bool singleSnap, bool isFinal, 
            bool allowSmoothing);
        
        string GetStats();
    }
}