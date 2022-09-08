using System.Linq;
using TrackPlanner.Shared;
using Xunit;

namespace TrackPlanner.Tests
{
    public class BAD_MiniWorldPlanTest : MiniWorld
    {
        [Fact]
        public void FijewoShortcutTest()
        {
            // in first version program went through a "shortcut" -- road within gas station 

            var map_filename = "fijewo-shortcut.kml";

            var places = ComputePlaces(map_filename,
                GeoZPoint.FromDegreesMeters(53.392567, 18.934572, 0),
                GeoZPoint.FromDegreesMeters(53.401714, 18.9353, 0)
            );

            Assert.True(places.Where(it => it.IsNode)
                // program should compute route through this intersection (no shortcut)
                .Any(it => it.NodeId == 587587510));
        }
        
        [Fact]
        public void CierpiceCrossingRoadTest()
        {
            // there is an error in OSM data, it does not have common node on one lane at the road intersection
            // we should fix the map on the fly

            // in "buggy" version program relies on OSM data, goes as the data dictates, finds the first common node
            // and goes back on the target lane
            
            var map_filename = "cierpice-crossing_road.kml";

            var places = ComputePlaces(map_filename,
                GeoZPoint.FromDegreesMeters(52.983727, 18.485634, 0),
                GeoZPoint.FromDegreesMeters(52.987045, 18.49471, 0)
            );

            Assert.True(places.Where(it => it.IsNode)
                // pivot point, in OSM-corrected version we won't go there
                .Any(it => it.NodeId == 4332109258));
        }

    }
}