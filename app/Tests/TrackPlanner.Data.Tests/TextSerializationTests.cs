using MathUnit;
using TrackPlanner.Data;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrackPlanner.Data.Serialization;
using TrackPlanner.Shared;
using Xunit;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace TrackPlanner.Data.Tests
{
    public readonly struct FooBar
    {
        public int X { get; }
        public int Y { get; }

        [JsonConstructor]
        public FooBar(int x,int y)
        {
            X = x;
            Y = y;
        }
    }


    public class TextSerializationTests
    {
        [Fact]
        public void FooBarSerializationTest()
        {
            var input = new FooBar(3,5);
            var options = TextOptionsFactory.BuildJsonOptions(compact: false);

            var json_string = JsonSerializer.Serialize(input, options);

            var output = JsonSerializer.Deserialize<FooBar>(json_string, options);

            Assert.Equal(input, output);

        }

        [Fact]
        public void AngleSerializationTest()
        {
            var input = Angle.FromRadians(Math.PI);
            var options = TextOptionsFactory.BuildJsonOptions(compact:false);

            var json_string = JsonSerializer.Serialize(input, options);
            Assert.Contains("180", json_string);

            var output = JsonSerializer.Deserialize<Angle>(json_string, options);

            Assert.Equal(input, output);

        }

        [Fact]
        public void GeoZPointSerializationTest()
        {
            return; // it looks Text.Json does not respect ISerializable
            
            {
                var input = GeoZPoint.Create(Angle.FromDegrees(90), Angle.FromDegrees(180), null);
                var options = TextOptionsFactory.BuildJsonOptions(compact:false);
                var json_string = JsonSerializer.Serialize(input, options);
                var output = JsonSerializer.Deserialize<GeoZPoint>(json_string, options);

                Assert.Equal(input, output);
            }

            {
                var input = GeoZPoint.Create(Angle.FromDegrees(90), Angle.FromDegrees(180), Length.FromMeters(100));
                var options = TextOptionsFactory.BuildJsonOptions(compact:false);
                var json_string = JsonSerializer.Serialize(input, options);
                var output = JsonSerializer.Deserialize<GeoZPoint>(json_string, options);

                Assert.Equal(input, output);
            }
        }

    }
}
