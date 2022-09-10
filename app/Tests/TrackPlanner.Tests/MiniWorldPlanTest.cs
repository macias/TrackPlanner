using System.Linq;
using TrackPlanner.Mapping;
using TrackPlanner.Shared;
using Xunit;

namespace TrackPlanner.Tests
{
    public class MiniWorldPlanTest : MiniWorld
    {
        
        [Theory]
        [MemberData(nameof(TestParams))]
        public void ZakrzewkoNoGoingBackTest(MapMode mapMode)
        {
            // in first version program got to middle point, went back, then went again forward 

            var map_filename = "zakrzewko.kml";

            var places = ComputePlaces(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(53.097324, 18.640022, 0),
                GeoZPoint.FromDegreesMeters(53.102116, 18.646202, 0),
                GeoZPoint.FromDegreesMeters(53.110565, 18.661394, 0)
            );

            Assert.Equal(places.Count-1,// there is one joint point (duplicated) here 
                places.Select(it => it.Point).Distinct().Count());
        }
    }
}