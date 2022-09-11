using System.Linq;
using TrackPlanner.Data;
using TrackPlanner.Mapping;
using TrackPlanner.Shared;
using Xunit;

namespace TrackPlanner.Tests
{
    // tests based on legacy data come from times when the track plan came from the external planner/router and turner
    // had to add turn points, in other words track was given "in advance" 
    public class MiniWorldTurnTest : MiniWorld
    {
        [Theory]
        [MemberData(nameof(TestParams))]
        public void ChelmnoRoundaboutLTurnTest(MapMode mapMode)
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
            
            //SaveData(plan, turns, map_filename);

            Assert.Equal(1, turns.Count);

            int index = 0;
            
            Assert.Equal( TurnInfo.EntityReference.Roundabout, turns[index].Entity);
            Assert.Equal(235135545, turns[index].EntityId);
            Assert.Equal(0, turns[index].RoundaboutGroup);
            Assert.True(turns[index].Forward);
            Assert.True(turns[index].Backward);
            Assert.Equal(7, turns[index].TrackIndex);
        }


        
        [Theory]
        [MemberData(nameof(TestParams))]
        public void BiskupiceSwitchingCyclewaySidesTest(MapMode mapMode)
        {
            // road should be used
            
            var map_filename = "legacy/biskupice_switching_cycleway_sides.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(    53.14337, 18.50604, 0),
                GeoZPoint.FromDegreesMeters(    53.14226, 18.50179, 0)
            );

            Assert.Equal(0, turns.Count);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void GrabowiecFlatRunTest(MapMode mapMode)
        {
            // road should be used
            
            var map_filename = "legacy/grabowiec_flat_run.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(    52.99471, 18.7021, 0),
                GeoZPoint.FromDegreesMeters(    52.95359, 18.72525, 0)
            );

            Assert.Equal(0, turns.Count);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void StareRoznoTest(MapMode mapMode)
        {
            var map_filename = "legacy/stare_rozno.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(    52.88891, 18.6217, 0),
                GeoZPoint.FromDegreesMeters(    52.87858, 18.63708, 0)
                );

            Assert.Equal(0, turns.Count);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void LipieSidewalkTest(MapMode mapMode)
        {
            var map_filename = "legacy/lipie_sidewalk.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(    52.87693, 18.44306, 0),
                GeoZPoint.FromDegreesMeters(    52.87349, 18.43837, 0)
                );

            Assert.Equal(0, turns.Count);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void PerkowoTest(MapMode mapMode)
        {
            var map_filename = "legacy/perkowo.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(    52.90463, 18.47046, 0),
                GeoZPoint.FromDegreesMeters(    52.90219, 18.47163, 0)
                );

            Assert.Equal(0, turns.Count);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void MarcinkowoGravelTest(MapMode mapMode)
        {
            var map_filename = "legacy/marcinkowo_gravel.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(    52.7958, 18.35691, 0),
                GeoZPoint.FromDegreesMeters(    52.79661, 18.35044, 0)
                );

            Assert.Equal(0, turns.Count);
        }
        
                [Theory]
        [MemberData(nameof(TestParams))]
        public void LipionkaTest(MapMode mapMode)
        {
            var map_filename = "legacy/lipionka.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(52.85411, 18.43042, 0),
                GeoZPoint.FromDegreesMeters(52.86473, 18.43156, 0)
                );

            Assert.Equal(0, turns.Count);
        }
        
                [Theory]
        [MemberData(nameof(TestParams))]
        public void LeszczAngledCrossingTest(MapMode mapMode)
        {
            var map_filename = "legacy/leszcz_angled_crossing.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(    53.10594, 18.51779, 0),
                GeoZPoint.FromDegreesMeters(    53.10766, 18.52053, 0)
            );

            Assert.Equal(0, turns.Count);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void WrzosyCyclepathTest(MapMode mapMode)
        {
            var map_filename = "legacy/wrzosy_cyclepath.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(    53.05293, 18.56809, 0),
                GeoZPoint.FromDegreesMeters(    53.05573, 18.5632, 0)
            );

            Assert.Equal(0, turns.Count);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void ChelmnoRoundaboutTest(MapMode mapMode)
        {
            var map_filename = "legacy/chelmno-roundabout.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(    53.32023, 18.42174, 0),
                GeoZPoint.FromDegreesMeters(    53.33369, 18.4192, 0)
            );

            Assert.Equal(0, turns.Count);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void WymyslowoStraightTest(MapMode mapMode)
        {
            var map_filename = "legacy/wymyslowo_straight.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(    53.163, 18.50388, 0),
                GeoZPoint.FromDegreesMeters(    53.17132, 18.50534, 0)
            );

            // initial turn is not reported, because we cut "tails" of the track, so basically the turn point becomes starting point
            Assert.Equal(0, turns.Count);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void PigzaSwitchFromCyclewayStraightTest(MapMode mapMode)
        {
            var map_filename = "legacy/pigza_switch_from_cycleway_straight.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(    53.11649, 18.52708, 0),
                GeoZPoint.FromDegreesMeters(    53.12156, 18.5282, 0)
            );

            Assert.Equal(0, turns.Count);
        }
        

                [Theory]
        [MemberData(nameof(TestParams))]
        public void SiemonNotACrossJunctionTest(MapMode mapMode)
        {
            // currently there is problem because OSM shows it as a cross junction, while in reality it is NOT cross junction
            // so we should get turn point in the middle (in ideal world)
            var map_filename = "legacy/siemon_not_a_cross_junction.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(    53.16501, 18.39586, 0),
                GeoZPoint.FromDegreesMeters(    53.16677, 18.38891, 0)
            );

            Assert.Equal(0, turns.Count); // it is impossible to get turn point with current OSM data
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void LipieStraightTrackTest(MapMode mapMode)
        {
            var map_filename = "legacy/lipie_straight_track.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(        52.87949, 18.44857, 0),
                GeoZPoint.FromDegreesMeters(        52.87648, 18.44262, 0)
            );
            
            Assert.Equal(2, turns.Count);

            int index = 0;
            
            Assert.Equal(746296255 , turns[index].EntityId);
            Assert.True(turns[index].Forward);
            Assert.True(turns[index].Backward);
            Assert.Equal(8, turns[index].TrackIndex);

            ++index;
            
            Assert.Equal(1575195605, turns[index].EntityId);
            Assert.True(turns[index].Forward);
            Assert.True(turns[index].Backward);
            Assert.Equal(9, turns[index].TrackIndex);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void GaskiTest(MapMode mapMode)
        {
            var map_filename = "legacy/gaski.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(            52.83209, 18.42885, 0),
                GeoZPoint.FromDegreesMeters(            52.83003, 18.425, 0)
            );
            
            Assert.Equal(2, turns.Count);

            Assert.Equal(1238387822, turns[0].EntityId);
            Assert.True(turns[0].Forward);
            Assert.True(turns[0].Backward);
            Assert.Equal(2, turns[0].TrackIndex);

            Assert.Equal(4463230352, turns[1].EntityId);
            Assert.False(turns[1].Forward);
            Assert.True(turns[1].Backward);
            Assert.Equal(4, turns[1].TrackIndex);
        }

        [Theory]
        [MemberData(nameof(TestParams))]
        public void RadzynChelminskiCrossedLoopTest(MapMode mapMode)
        {
            // the track looks like this
            // ><>
            // the purpose of this test is to check if program correctly handle the entire track and it won't shorten it to
            // >
            // because it detects there is "shorter" path
            var map_filename = "legacy/radzyn_chelminski_crossed_loop.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                53.3689, 18.97037, 0),
                GeoZPoint.FromDegreesMeters(                53.35659, 18.99001, 0),
                GeoZPoint.FromDegreesMeters(                53.37222, 18.99997, 0),
                GeoZPoint.FromDegreesMeters(                53.35734, 18.96729, 0)
            );

            //SaveData(plan,turns,map_filename);

            Assert.Equal(2, turns.Count);

            int index = 0;
            
            Assert.Equal(2118304203, turns[index].EntityId);
            Assert.True(turns[index].Forward);
            Assert.True(turns[index].Backward);
            Assert.Equal(66, turns[index].TrackIndex);

            ++index;
            
            Assert.Equal(1846783922, turns[index].EntityId);
            // not ideal here, but the angle at the the Y-junction point so sharp (it is twised junction),
            // that triggers need for notification
            // maybe if we could measure the point father apart from turn-point?
            Assert.True(turns[index].Forward);
            Assert.True(turns[index].Backward);
            Assert.Equal(98, turns[index].TrackIndex);
        }



                [Theory]
        [MemberData(nameof(TestParams))]
        public void BiskupiceSwitchFromCyclewayTest(MapMode mapMode)
        {
            var map_filename = "legacy/biskupice_switch_from_cycleway.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                53.14388, 18.50628, 0),
                GeoZPoint.FromDegreesMeters(                53.14635, 18.50787, 0)
            );

        //saveTurns(turns,map_filename);

            Assert.Equal(1, turns.Count);
            
            Assert.Equal(982422243, turns[0].EntityId);
            Assert.True(turns[0].Forward);
            Assert.True(turns[0].Backward);
            Assert.Equal(3, turns[0].TrackIndex);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void TorunUnislawDedicatedCyclewayTest(MapMode mapMode)
        {
            var map_filename = "legacy/torun_unislaw_dedicated_cycleway.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                    53.0601, 18.55826, 0),
                GeoZPoint.FromDegreesMeters(                    53.11972, 18.46862, 0)
            );

            Assert.Equal(1, turns.Count);

            Assert.Equal(1737021333, turns[0].EntityId);
            Assert.True(turns[0].Forward);
            Assert.False(turns[0].Backward);
            Assert.Equal(1, turns[0].TrackIndex);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void TorunChelminskaSmoothingCyclewayTest(MapMode mapMode)
        {
            // there is a subtle Y junction on the cycleway
            
            var map_filename = "legacy/torun_chelminska_smoothing_cycleway.kml";
            var (plan, turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(53.06009, 18.55827, 0),
                GeoZPoint.FromDegreesMeters(53.06251, 18.5549, 0)
            );

            Assert.Equal(1, turns.Count);

            Assert.Equal(1737021333, turns[0].EntityId);
            Assert.True(turns[0].Forward);
            Assert.False(turns[0].Backward);
            Assert.Equal(1, turns[0].TrackIndex);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void TorunChelminskaCyclewaySnapWithTurnTest(MapMode mapMode)
        {
            var map_filename = "legacy/torun_chelminska_cycleway_snap_with_turn.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                53.05968, 18.55871, 0),
                GeoZPoint.FromDegreesMeters(                53.05981, 18.55302, 0)
            );

            if (true)
            {
                // this version goes at first by cycleway and then makes two gentle turns
                // this is preferred way, despite the fact it has two turns 
                
                Assert.Equal(2, turns.Count);

                Assert.Equal(1737021333, turns[0].EntityId);
                Assert.True(turns[0].Forward);
                Assert.False(turns[0].Backward);
                Assert.Equal(1, turns[0].TrackIndex);

                Assert.Equal(3417741714, turns[1].EntityId);
                Assert.True(turns[1].Forward);
                Assert.True(turns[1].Backward);
                Assert.Equal(6, turns[1].TrackIndex);
            }
            else
            {
                // this one goes first by major road and then makes rapid turn, it is not bad, but the above one is better choice

                Assert.Equal(1, turns.Count);

                Assert.Equal(53.061941700000006, turns[0].Point.Latitude.Degrees, Precision);
                Assert.Equal(18.556223800000001, turns[0].Point.Longitude.Degrees, Precision);
                Assert.True(turns[0].Forward);
                Assert.True(turns[0].Backward);
                Assert.Equal(3, turns[0].TrackIndex);
            }
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void SilnoCyclewayBumpTest(MapMode mapMode)
        {
            var map_filename = "legacy/silno_cycleway_bump.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                52.9426, 18.73246, 0),
                GeoZPoint.FromDegreesMeters(                52.93953, 18.73554, 0)
            );

            //SaveData(plan,turns,map_filename);
            
            // turner depends on routing so to avoid constant changes due to router
            // we added conditional checks
            
            int index = 0;
            int second_turn_track_index;
            // if the route starts with road part then we have two turns
            if (plan.Any(it => it.IsNode && it.NodeId == 3610427916))
            {
                Assert.Equal(2, turns.Count);

                second_turn_track_index = 10;
                    
                Assert.Equal(3610427916, turns[index].EntityId);
                Assert.True(turns[index].Forward);
                Assert.False(turns[index].Backward);
                Assert.Equal(5, turns[index].TrackIndex);

                ++index;
            }
            else // if it starts from parallel cycleway we will have only one turn
            {
                Assert.Equal(1, turns.Count);

                second_turn_track_index = 8;
            }

            // this turn is common in both cases
            
            Assert.Equal(6384120377, turns[index].EntityId);
            Assert.True(turns[index].Forward);
            Assert.True(turns[index].Backward);
            Assert.Equal(second_turn_track_index, turns[index].TrackIndex);
        }



                [Theory]
        [MemberData(nameof(TestParams))]
        public void KaszczorekBridgeMinorPassExitTest(MapMode mapMode)
        {
            var map_filename = "legacy/kaszczorek_bridge_minor_pass_exit.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                52.99863, 18.70227, 0),
                GeoZPoint.FromDegreesMeters(                52.99735, 18.70238, 0)
            );

            Assert.Equal(1, turns.Count);

            int index = 0;

            Assert.Equal(773474234, turns[index].EntityId);
            Assert.True(turns[index].Forward);
            Assert.True(turns[index].Backward);
            Assert.Equal(4, turns[index].TrackIndex);
        }



                [Theory]
        [MemberData(nameof(TestParams))]
        public void RusinowoEasyOverridesSharpTest(MapMode mapMode)
        {
            // the track has easy turn, the alternate road has sharp angle, so when going forward easy turn (track) should override sharp one and we should not get turn point
            var map_filename = "legacy/rusinowo_easy_overrides_sharp.kml";

            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                53.04441, 19.32008, 0),
                GeoZPoint.FromDegreesMeters(                53.05342, 19.33372, 0)
            );

            Assert.Equal(1, turns.Count);

            Assert.Equal(1819846285, turns[0].EntityId);
            Assert.False(turns[0].Forward);
            Assert.True(turns[0].Backward);
            Assert.Equal(5, turns[0].TrackIndex);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void PigzaSwitchToCyclewayTest(MapMode mapMode)
        {
            var map_filename = "legacy/pigza_switch_to_cycleway.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                53.11469, 18.5243, 0),
                GeoZPoint.FromDegreesMeters(                53.11667, 18.5273, 0)
            );

            // for now we will live with it, but ideally there should be no notification in this case
            Assert.Equal(1, turns.Count);

            Assert.Equal(1437255316, turns[0].EntityId);
            Assert.True(turns[0].Forward);
            Assert.True(turns[0].Backward);
            Assert.Equal(3, turns[0].TrackIndex);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void PigzaSwitchFromCyclewayWithTurnTest(MapMode mapMode)
        {
            var map_filename = "legacy/pigza_switch_from_cycleway_with_turn.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                53.11639, 18.52682, 0),
                GeoZPoint.FromDegreesMeters(                53.11876, 18.52663, 0)
            );

            Assert.Equal(1, turns.Count);

            Assert.Equal(1615430797, turns[0].EntityId);
            Assert.True(turns[0].Forward);
            Assert.True(turns[0].Backward);
            Assert.Equal(4, turns[0].TrackIndex);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void PigzaTurnOnNamedPathTest(MapMode mapMode)
        {
            var map_filename = "legacy/pigza_turn_on_named_path.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                53.12008, 18.52574, 0),
                GeoZPoint.FromDegreesMeters(                53.12848, 18.51757, 0)
            );

            //SaveData(plan, turns, map_filename);

            Assert.Equal(1, turns.Count);

            Assert.Equal(1437255487, turns[0].EntityId);
            Assert.True(turns[0].Forward);
            Assert.False(turns[0].Backward);
            Assert.Equal(6, turns[0].TrackIndex);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void PigzaGoingStraightIntoPathTest(MapMode mapMode)
        {
            var map_filename = "legacy/pigza_going_straight_into_path.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                53.11898, 18.52568, 0),
                GeoZPoint.FromDegreesMeters(                53.11742, 18.53591, 0)
            );

            // here we have T-junction  (on the left is regular road)
            // #____
            // #  |
            // with horizontal part of the samek kind (path) while vertical is cycleway, we go along path
            // in maybe theory we would not have turn notification (because we don't change road kind) but we have to
            // take into consideration two factors
            // (a) OSM can have errors
            // (b) the short link between road and T-junction can be in practive (while riding) treated more like part of cycleway

            // but overall, it would be better to get rid of this notification

            Assert.Equal(1, turns.Count);

            int index = 0;

            Assert.Equal(6635814210, turns[index].EntityId);
            Assert.True(turns[index].Forward);
            Assert.True(turns[index].Backward);
            Assert.Equal(2, turns[index].TrackIndex);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void SuchatowkaTurnRailwayTest(MapMode mapMode)
        {
            var map_filename = "legacy/suchatowka_turn_railway.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                52.91112, 18.47965, 0),
                GeoZPoint.FromDegreesMeters(                52.90999, 18.47969, 0)
            );

            Assert.Equal(1, turns.Count);

            Assert.Equal(4630332360, turns[0].EntityId);
            Assert.True(turns[0].Forward);
            Assert.True(turns[0].Backward);
            Assert.Equal(5, turns[0].TrackIndex);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void DebowoStraightIntoMinorTest(MapMode mapMode)
        {
            var map_filename = "legacy/debowo_straight_into_minor.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                52.68843, 18.0593, 0),
                GeoZPoint.FromDegreesMeters(                52.68376, 18.03473, 0)
            );

            // the idea of this test is to check if we get alert on L-shaped major road, with extension of the minor one
            //  ::
            //  ::
            //  ++###
            //  ||
            //  ||
            //
            // :: here is minor one, when we go major->minor we need to get alert despite we are going in straight line, otherwise if we take turn
            // (which does not have alert) we could tell the difference between turning and going straight, because both cases would be alert-free.
            // Only when going back minor->major the alert can be skipped because it is obvious where to go (without alert)

            Assert.Equal(1, turns.Count);

            Assert.Equal(1116766709, turns[0].EntityId);
            Assert.True(turns[0].Forward);
            Assert.False(turns[0].Backward);
            Assert.Equal(8, turns[0].TrackIndex);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void GaskiYTurnUnclassifiedTest(MapMode mapMode)
        {
            var map_filename = "legacy/gaski_y-turn_unclassified.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                52.83042, 18.4256, 0),
                GeoZPoint.FromDegreesMeters(                52.82918, 18.42151, 0)
            );

            // there should be no turn notification here because we suspect the turn-alternative road is a minor one,
            // but OSM at the moment has almost no data about the alternate road
            Assert.Equal(1, turns.Count);

            Assert.Equal(1238387747, turns[0].EntityId);
            Assert.True(turns[0].Forward);
            Assert.False(turns[0].Backward);
            Assert.Equal(5, turns[0].TrackIndex);
        }

                [Theory]
        [MemberData(nameof(TestParams))]
        public void NawraAlmostStraightYJunctionTest(MapMode mapMode)
        {
            var map_filename = "legacy/nawra_almost_straight_Y_junction.kml";
            var (plan,turns) = ComputeTurns(mapMode,map_filename,
                GeoZPoint.FromDegreesMeters(                53.18625, 18.49384, 0),
                GeoZPoint.FromDegreesMeters(                53.19082, 18.49915, 0)
            );

            Assert.Equal(2, turns.Count);

            Assert.Equal(587567345, turns[0].EntityId);
            Assert.False(turns[0].Forward);
            Assert.True(turns[0].Backward);
            Assert.Equal(2, turns[0].TrackIndex);

            Assert.Equal(587567344, turns[1].EntityId);
            // todo: not ideal here, but the angle at the the Y-junction point so sharp (it is twisted junction), that triggers need for notification
            // maybe if we could measure the point father apart from turn-point?
            Assert.True(turns[1].Forward);
            Assert.True(turns[1].Backward);
            Assert.Equal(5, turns[1].TrackIndex);
        }
       
    }
}