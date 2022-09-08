using System.Linq;
using TrackPlanner.Shared;
using Xunit;

namespace TrackPlanner.Tests
{
    public class MiniWorldPlanTest : MiniWorld
    {
        [Fact]
        public void FijewoShortcutTest()
        {
            // initially program went through a "shortcut" -- road within gas station
            // first fix: add penalty when changing roads 
            
            // another possible approach would be decreasing penalty for riding high-speed roads (this case)
            // so it would not matter much if the ride is 10.1 km or 10 km

            var map_filename = "fijewo-shortcut.kml";

            var plan = ComputePlaces(map_filename,
                GeoZPoint.FromDegreesMeters(53.392567, 18.934572, 0),
                GeoZPoint.FromDegreesMeters(53.401714, 18.9353, 0)
            );

            Assert.True(plan.Where(it => it.IsNode)
                // program should compute route through this intersection (no shortcut)
                .Any(it => it.NodeId == 587587510));
        }

        
        [Fact]
        public void ZakrzewkoNoGoingBackTest()
        {
            // in first version program got to middle point, went back, then went again forward 

            var map_filename = "zakrzewko.kml";

            var places = ComputePlaces(map_filename,
                GeoZPoint.FromDegreesMeters(53.097324, 18.640022, 0),
                GeoZPoint.FromDegreesMeters(53.102116, 18.646202, 0),
                GeoZPoint.FromDegreesMeters(53.110565, 18.661394, 0)
            );

            Assert.Equal(places.Count-1,// there is one joint point (duplicated) here 
                places.Select(it => it.Point).Distinct().Count());
        }
    }
}