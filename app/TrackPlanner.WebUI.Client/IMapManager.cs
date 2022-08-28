using System.Threading.Tasks;
using BlazorLeaflet.Models;
using TrackPlanner.Data;

namespace TrackPlanner.WebUI.Client
{
    public interface IMapManager
    {
        TrackPlan Plan { get; }
        
        ValueTask RebuildNeededAsync();
        void MarkerAdded(Marker marker);
        void MarkerRemoved(Marker marker);
        void BeforeMarkersRemoved();
        void RemoveLeg(int legIndex);
    }
}