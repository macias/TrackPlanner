using TrackPlanner.Shared;

namespace TrackPlanner.Data
{
    public  readonly record struct MapPoint(GeoZPoint Point,long? NodeId)
    {
        
    }
    
}