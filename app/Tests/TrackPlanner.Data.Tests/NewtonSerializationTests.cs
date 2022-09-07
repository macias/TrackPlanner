using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Equivalency;
using Geo;
using MathUnit;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using TrackPlanner.Data.Serialization;
using TrackPlanner.Data.Stored;
using TrackPlanner.Shared;
using Xunit;
using TimeSpan = System.TimeSpan;

namespace TrackPlanner.Data.Tests
{
    public class NewtonSerializationTests
    {
        [Fact]
        public void UserTurnerPreferencesSerializationTest()
        {
            var input = new UserTurnerPreferences()
            {
                TurnArmLength = Length.FromKilometers(3),
            };

            var json_options = NewtonOptionsFactory.BuildJsonOptions(compact:false);

            var json_string = JsonConvert.SerializeObject(input, json_options);

            var output = JsonConvert.DeserializeObject<UserTurnerPreferences>(json_string, json_options);

            output.Should().BeEquivalentTo(input, setApproximateAngleOptions);
        }

        [Fact]
        public void UserRouterPreferencesSerializationTest()
        {
            var input = new UserRouterPreferences()
            {
                TrafficSuppression = Length.FromMeters(2),
                Speeds = new Dictionary<SpeedMode, Speed>()
                {
                    {SpeedMode.Asphalt, Speed.FromMetersPerSecond(50)},
                }
            };
            
            var options = NewtonOptionsFactory.BuildJsonOptions(compact:false);

            var json_string = JsonConvert.SerializeObject(input, options);

            var output = JsonConvert.DeserializeObject<UserPlannerPreferences>(json_string, options);

            output.Should().BeEquivalentTo(input, setApproximateAngleOptions);
        }

        [Fact]
        public void RequestPointSerializationTest()
        {
            var input = new RequestPoint(GeoPoint.FromDegrees(53.024, 18.60917), true);
            
            var options = NewtonOptionsFactory.BuildJsonOptions(compact:false);

            var json_string = JsonConvert.SerializeObject(input, options);

            var output = JsonConvert.DeserializeObject<RequestPoint>(json_string, options);

            output.Should().BeEquivalentTo(input, setApproximateAngleOptions);
        }

        [Fact]
        public void PlanRequestSerializationTest()
        {
            var user_points = new[] {GeoPoint.FromDegrees(53.024, 18.60917), GeoPoint.FromDegrees(53.15528, 18.61338),}
                .Select(it => new RequestPoint(it, true))
                .ToList();
            
            var input = new PlanRequest()
            {
                DailyPoints = new List<List<RequestPoint>>(){user_points},
                TurnerPreferences = new UserTurnerPreferences()
                {
                    TurnArmLength = Length.FromKilometers(3),
                },
                RouterPreferences = new UserRouterPreferences()
                {
                    TrafficSuppression = Length.FromMeters(2),
                    Speeds = new Dictionary<SpeedMode, Speed>()
                    {
                        {SpeedMode.Asphalt, Speed.FromMetersPerSecond(50)},
                    }
                }
            };

            var options = NewtonOptionsFactory.BuildJsonOptions(compact:false);

            var json_string = JsonConvert.SerializeObject(input, options);

            var output = JsonConvert.DeserializeObject<PlanRequest>(json_string, options);

            output.Should().BeEquivalentTo(input, setApproximateAngleOptions);
        }

        [Fact]
        public void TurnInfoSerializationTest()
        {
            var input = new TurnInfo(TurnInfo.EntityReference.Roundabout, 123,  GeoZPoint.FromDegreesMeters(20, 30, 1), 3, 14, true, true,reason:"hello");

            var options = NewtonOptionsFactory.BuildJsonOptions(compact:false);

            var json_string = JsonConvert.SerializeObject(input, options);

            var output = JsonConvert.DeserializeObject<TurnInfo>(json_string, options);

            input.Should().BeEquivalentTo(output);
        }
        
        [Fact]
        public void ScheduleAnchorTest()
        {
            var input = new ScheduleAnchor() {Label = "foo"};
            
            var options = NewtonOptionsFactory.BuildJsonOptions(compact:false);

            var json_string = JsonConvert.SerializeObject(input, options);

            var output = JsonConvert.DeserializeObject<ScheduleAnchor>(json_string, options);

            input.Should().BeEquivalentTo(output);
        }

        [Fact]
        public void SegmentDataSerializationTest()
        {
            var input = new LegFragment()
            {
                IsForbidden = true,
                Places = new List<MapPoint>() {new MapPoint( GeoZPoint.FromDegreesMeters(12, 34, 56),null)},
                UnsimplifiedDistance = Length.FromMeters(67),
                RawTime = TimeSpan.FromSeconds(90),
                RoadIds = new HashSet<long>() {13},
            }
                    .SetSpeedMode(SpeedMode.Paved)
                ;
            var options = NewtonOptionsFactory.BuildJsonOptions(compact:false);

            var json_string = JsonConvert.SerializeObject(input, options);

            var output = JsonConvert.DeserializeObject<LegFragment>(json_string, options);

            input.Should().BeEquivalentTo(output);
        }

        [Fact]
        public void RoutePlanSerializationTest()
        {

            var input = new TrackPlan()
            {
                DailyTurns = new List<List<TurnInfo>>()
                {
                   new List<TurnInfo>(){ new TurnInfo( TurnInfo.EntityReference.Roundabout, 580, GeoZPoint.FromDegreesMeters(20, 30, 1), 3, 14, true, true,reason:"world")},
                },
                Legs = new List<LegPlan>()
                {
                    new LegPlan()
                    {
                        Fragments = new List<LegFragment>()
                        {
                            new LegFragment()
                            {
                                IsForbidden = true,
                                Places = new List<MapPoint>() {new MapPoint( GeoZPoint.FromDegreesMeters(12, 34, 56),null)},
                                UnsimplifiedDistance = Length.FromMeters(67),
                                RawTime = TimeSpan.FromSeconds(90),
                                RoadIds = new HashSet<long>() {13},
                            }
                                .SetSpeedMode(SpeedMode.Paved)
                        },


                    }

                },

            };

            var options = NewtonOptionsFactory.BuildJsonOptions(compact:false);

            var json_string = JsonConvert.SerializeObject(input, options);

            var output = JsonConvert.DeserializeObject<TrackPlan>(json_string, options);

            input.Should().BeEquivalentTo(output);
        }

        [Fact]
        public void AngleSerializationTest()
        {
            var input = Angle.FromDegrees(150);
            var options = NewtonOptionsFactory.BuildJsonOptions(compact:false);

            var json_string = JsonConvert.SerializeObject(input, options);

            var output = JsonConvert.DeserializeObject<Angle>(json_string, options);

            input.Should().BeEquivalentTo( output, setApproximateAngleOptions);
        }

        private static EquivalencyAssertionOptions<TExpectation> setApproximateAngleOptions<TExpectation>(EquivalencyAssertionOptions<TExpectation> options)
        {
            const int precision = 10;

            // https://stackoverflow.com/questions/36782975/fluent-assertions-approximately-compare-a-classes-properties
            options
                .Using<Angle>(ctx => ctx.Subject.Degrees.Should().BeApproximately(ctx.Expectation.Degrees, precision))
                .When(info => info.Type == typeof(Angle));

            return options;
        }


        [Fact]
        public void LengthSerializationTest()
        {
            var input = Length.FromMeters(120);
            var options = NewtonOptionsFactory.BuildJsonOptions(compact:false);

            var json_string = JsonConvert.SerializeObject(input, options);

            var output = JsonConvert.DeserializeObject<Length>(json_string, options);

            Assert.Equal(input, output);

        }


        [Fact]
        public void GeoZPointSerializationTest()
        {
            {
                var input = GeoZPoint.Create(Angle.FromDegrees(90), Angle.FromDegrees(180), null);
                var options = NewtonOptionsFactory.BuildJsonOptions(compact:false);
                var json_string = JsonConvert.SerializeObject(input, options);
                var output = JsonConvert.DeserializeObject<GeoZPoint>(json_string, options);

                Assert.Equal(input, output);
            }

            {
                var input = GeoZPoint.Create(Angle.FromDegrees(90), Angle.FromDegrees(180), Length.FromMeters(100));
                var options = NewtonOptionsFactory.BuildJsonOptions(compact:false);
                var json_string = JsonConvert.SerializeObject(input, options);
                var output = JsonConvert.DeserializeObject<GeoZPoint>(json_string, options);

                Assert.Equal(input, output);
            }
        }

    }
}
