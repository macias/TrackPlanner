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
    }
}