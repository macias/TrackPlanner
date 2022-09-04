using System.Linq;
using TrackPlanner.Shared;
using Xunit;

namespace TrackPlanner.Tests
{
    public class BAD_MiniWorldPlanTest : MiniWorld
    {
        [Fact]
        public void A_FIX_BAD_PLANNING_ZakrzewkoNoDuplicatesTest()
        {
            // in first version program got to middle point, went back, then went again forward 

            var map_filename = "zakrzewko.kml";
            var places = ComputePlaces(map_filename,
                GeoZPoint.FromDegreesMeters(53.097324, 18.640022, 0),
                GeoZPoint.FromDegreesMeters(53.102116, 18.646202, 0),
            GeoZPoint.FromDegreesMeters(53.110565, 18.661394, 0)
            );

            //saveData(plan,turns,map_filename);

            Assert.Equal(places.Count, places.Select(it => it.NodeId!.Value).Distinct().Count());
        }
    }
}