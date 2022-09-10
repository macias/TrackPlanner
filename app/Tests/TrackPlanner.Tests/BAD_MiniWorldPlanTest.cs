using System.Linq;
using TrackPlanner.Mapping;
using TrackPlanner.Shared;
using Xunit;

namespace TrackPlanner.Tests
{
    public class BAD_MiniWorldPlanTest : MiniWorld
    {
        [Theory]
        [MemberData(nameof(TestParams))]
        public void FijewoShortcutTest(MapMode mapMode)
        {
            // initially program went through a "shortcut" -- road within gas station
            // first fix: add penalty when changing roads 
            
            // another possible approach would be decreasing penalty for riding high-speed roads (this case)
            // so it would not matter much if the ride is 10.1 km or 10 km

            var map_filename = "fijewo-shortcut.kml";

            var plan = ComputePlaces(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(53.392567, 18.934572, 0),
                GeoZPoint.FromDegreesMeters(53.401714, 18.9353, 0)
            );

            Assert.True(plan.Where(it => it.IsNode)
                // program should compute route through this intersection (no shortcut)
                .Any(it => it.NodeId == 587587510));
        }

        
        [Theory]
        [MemberData(nameof(TestParams))]
        public void CierpiceCrossingRoadTest(MapMode mapMode)
        {
            // there is an error in OSM data, it does not have common node on one lane at the road intersection
            // we should fix the map on the fly

            // in "buggy" version program relies on OSM data, goes as the data dictates, finds the first common node
            // and goes back on the target lane
            
            var map_filename = "cierpice-crossing_road.kml";

            var plan = ComputePlaces(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(52.983727, 18.485634, 0),
                GeoZPoint.FromDegreesMeters(52.987045, 18.49471, 0)
            );

            SaveData(plan,map_filename);
            
            Assert.False(plan.Where(it => it.IsNode)
                // pivot point, in OSM-corrected version we won't go there
                .Any(it => it.NodeId == 4332109258));
        }

    }
}