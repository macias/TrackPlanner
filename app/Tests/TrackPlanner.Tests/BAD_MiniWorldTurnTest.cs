using TrackPlanner.Mapping;
using TrackPlanner.Shared;
using Xunit;

namespace TrackPlanner.Tests
{
    // tests based on legacy data come from times when the track plan came from the external planner/router and turner
    // had to add turn points, in other words track was given "in advance" 
    public class BAD_MiniWorldTurnTest : MiniWorld
    {
        [Theory]
        [MemberData(nameof(TestParams))]
        public void A_FIX_BUG_TorunSouthRangeTest(MapMode mapMode)
        {
            var map_filename = "legacy/torun_south_range.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                52.96484, 18.53726, 0),
                GeoZPoint.FromDegreesMeters(                52.9352, 18.51589, 0),
                GeoZPoint.FromDegreesMeters(                52.87777, 18.63722, 0)
            );

            //saveData(plan,turns,map_filename);
            
            Assert.Equal(1, turns.Count);

            Assert.Equal(52.935208700000004, turns[0].Point.Latitude.Degrees, Precision);
            Assert.Equal(18.515891200000002, turns[0].Point.Longitude.Degrees, Precision);
            Assert.True(turns[0].Forward);
            Assert.True(turns[0].Backward);
            Assert.Equal(45, turns[0].TrackIndex);
        }

                [Theory]
        [MemberData(nameof(TestParams))]

        public void A_FIX_RETHINK_KaszczorekRoundaboutCyclewayShortcutTest(MapMode mapMode)
        {
            // rethink if the planner does the right job going through roundabout instead of skipping it
            var map_filename = "legacy/kaszczorek_roundabout_cycleway_shortcut.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                53.01299, 18.68607, 0),
                GeoZPoint.FromDegreesMeters(                53.01034, 18.69025, 0)
            );
            //saveData(plan, turns, map_filename);

            Assert.Equal(2, turns.Count);

            int index = 0;

            Assert.Equal(53.011958400000005, turns[index].Point.Latitude.Degrees, Precision);
            Assert.Equal(18.687863100000001, turns[index].Point.Longitude.Degrees, Precision);
            Assert.True(turns[index].Forward);
            Assert.True(turns[index].Backward);
            Assert.Equal(7, turns[index].TrackIndex);

            ++index;
            // this point PROBABLY can be moved a bit towards main road
            Assert.Equal(53.011309300000001, turns[index].Point.Latitude.Degrees, Precision);
            Assert.Equal(18.688785800000002, turns[index].Point.Longitude.Degrees, Precision);
            Assert.False(turns[index].Forward);
            Assert.True(turns[index].Backward);
            Assert.Equal(14, turns[index].TrackIndex);
        }

                [Theory]
        [MemberData(nameof(TestParams))]

        public void A_FIX_INVESTIGATE_KaszczorekBridgeMinorPassTest(MapMode mapMode)
        {
            var map_filename = "legacy/kaszczorek_bridge_minor_pass.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                53.0023, 18.69977, 0),
                GeoZPoint.FromDegreesMeters(                53.00024, 18.7026, 0)
            );

            //saveData(plan, turns, map_filename);
            Assert.Equal(3, turns.Count);

            int index = 0;

            // major road split
            Assert.Equal(53.002014700000004, turns[index].Point.Latitude.Degrees, Precision);
            Assert.Equal(18.700387999999997, turns[index].Point.Longitude.Degrees, Precision);
            Assert.True(turns[index].Forward);
            Assert.True(turns[index].Backward);
            Assert.Equal(2, turns[index].TrackIndex);

            ++index;

            // turn to minor road
            Assert.Equal(53.001069000000001, turns[index].Point.Latitude.Degrees, Precision);
            Assert.Equal(18.7013751, turns[index].Point.Longitude.Degrees, Precision);
            Assert.True(turns[index].Forward);
            Assert.False(turns[index].Backward);
            Assert.Equal(12, turns[index].TrackIndex);

            ++index;

            // road is splitting into cycleway
            Assert.Equal(53.000867099999994, turns[index].Point.Latitude.Degrees, Precision);
            Assert.Equal(18.701641900000002, turns[index].Point.Longitude.Degrees, Precision);
            Assert.False(turns[index].Forward);
            Assert.True(turns[index].Backward);
            Assert.Equal(13, turns[index].TrackIndex);
        }

                [Theory]
        [MemberData(nameof(TestParams))]

        public void A_FIX_USE_ROAD_BiskupiceSwitchToCyclewayTest(MapMode mapMode)
        {
            var map_filename = "legacy/biskupice_switch_to_cycleway.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                53.13679, 18.51126, 0),
                GeoZPoint.FromDegreesMeters(                53.14268, 18.50394, 0)
            );

            //saveData(plan, turns, map_filename);
            Assert.Equal(1, turns.Count);

            Assert.Equal(53.143534600000002, turns[0].Point.Latitude.Degrees, Precision);
            Assert.Equal(18.506028300000001, turns[0].Point.Longitude.Degrees, Precision);
            Assert.True(turns[0].Forward);
            Assert.True(turns[0].Backward);
            Assert.Equal(4, turns[0].TrackIndex);
        }

                [Theory]
        [MemberData(nameof(TestParams))]

        public void A_FIX_BAD_PLANNING_BiskupiceTurnOnCyclewayTest(MapMode mapMode)
        {
            var map_filename = "legacy/biskupice_turn_on_cycleway.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                53.13756, 18.51066, 0),
                GeoZPoint.FromDegreesMeters(                53.14437, 18.50728, 0)
            );

            SaveData(plan, turns, map_filename);
            
            Assert.Equal(1, turns.Count);

            Assert.Equal(53.143534600000002, turns[0].Point.Latitude.Degrees, Precision);
            Assert.Equal(18.506028300000001, turns[0].Point.Longitude.Degrees, Precision);
            Assert.True(turns[0].Forward);
            Assert.True(turns[0].Backward);
            Assert.Equal(3, turns[0].TrackIndex);
        }

                [Theory]
        [MemberData(nameof(TestParams))]

        public void A_FIX_PLANNING_PROBLEM_DorposzSzlachecki_YJunctionTest(MapMode mapMode)
        {
            var map_filename = "legacy/dorposz_szlachecki_y_junction.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                53.14437, 18.50728, 0),
                GeoZPoint.FromDegreesMeters(                53.29396, 18.42947, 0)
            );

            //saveData(plan, turns, map_filename);
            Assert.Equal(1, turns.Count);

            Assert.Equal(53.290602100000001, turns[0].Point.Latitude.Degrees, Precision);
            Assert.Equal(18.428604400000001, turns[0].Point.Longitude.Degrees, Precision);
            // it would be great if we could add some extra logic to remove the need of forward turn-notification, currently the angle is around 136 so it seen as turn
            Assert.True(turns[0].Forward);
            Assert.True(turns[0].Backward);
            Assert.Equal(6, turns[0].TrackIndex);
        }

                [Theory]
        [MemberData(nameof(TestParams))]

        public void A_FIX_RETHINK_PLANNING_TorunSkarpaIgnoringCyclewayTest(MapMode mapMode)
        {
            // at current stage of planner, the cycleway is only partialy ignored
            
            var map_filename = "legacy/torun_skarpa_ignoring_cycleway.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                53.02259, 18.66845, 0),
                GeoZPoint.FromDegreesMeters(                53.01858, 18.67595, 0)
            );

            //saveData(plan,turns,map_filename);
            
            Assert.Equal(1, turns.Count);
            
            // this entire turn-notification is because we snapped path to one-direction road (partially) so when we go along no problem, but when we go back
            // turn calculator see we go against current thus it gives us notification
            Assert.Equal(53.022024400000006, turns[0].Point.Latitude.Degrees, Precision);
            Assert.Equal(18.669373999999998, turns[0].Point.Longitude.Degrees, Precision);
            Assert.False(turns[0].Forward);
            Assert.True(turns[0].Backward);
            Assert.Equal(4, turns[0].TrackIndex);
        }
                [Theory]
        [MemberData(nameof(TestParams))]

        public void FIX_ChelmnoRoundaboutLTurnTest(MapMode mapMode)
        {
            // todo: we need to flatten entry+exit roads as well
            // O>----
            // O roundabout
            // > split to entry+exit road
            // - regular road
            var map_filename = "legacy/chelmno-roundabout_Lturn.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                53.32023, 18.42174, 0),
                GeoZPoint.FromDegreesMeters(                53.32692, 18.4115, 0)
            );
            
            SaveData(plan, turns, map_filename);

            Assert.Equal(2, turns.Count);

            Assert.Equal(53.328389600000001, turns[0].Point.Latitude.Degrees, Precision);
            Assert.Equal(18.420067500000002, turns[0].Point.Longitude.Degrees, Precision);
            Assert.Equal(0, turns[0].RoundaboutGroup);
            Assert.Equal(7, turns[0].TrackIndex);

            Assert.Equal(53.328644800000006, turns[1].Point.Latitude.Degrees, Precision);
            Assert.Equal(18.4194174, turns[1].Point.Longitude.Degrees, Precision);
            Assert.Equal(0, turns[1].RoundaboutGroup);
            Assert.Equal(22, turns[1].TrackIndex);
        }

        
    }
}