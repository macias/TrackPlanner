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

    public class DataFormatTests
    {
        [Fact]
        public void AdjustingNumbersTest()
        {
            int x;
            x = 0;
            Assert.Equal("0", DataFormat.Adjust(x,0));
            Assert.Equal("0", DataFormat.Adjust(x,1));
            Assert.Equal("0", DataFormat.Adjust(x,9));
            Assert.Equal("00", DataFormat.Adjust(x,10));
            Assert.Equal("00", DataFormat.Adjust(x,99));
            Assert.Equal("000", DataFormat.Adjust(x,100));
            Assert.Equal("000", DataFormat.Adjust(x,999));

            x = 1;
            Assert.Equal("1", DataFormat.Adjust(x,1));
            Assert.Equal("1", DataFormat.Adjust(x,9));
            Assert.Equal("01", DataFormat.Adjust(x,10));
            Assert.Equal("01", DataFormat.Adjust(x,99));
            Assert.Equal("001", DataFormat.Adjust(x,100));
            Assert.Equal("001", DataFormat.Adjust(x,999));

            x = 9;
            Assert.Equal("9", DataFormat.Adjust(x,9));
            Assert.Equal("09", DataFormat.Adjust(x,10));
            Assert.Equal("09", DataFormat.Adjust(x,99));
            Assert.Equal("009", DataFormat.Adjust(x,100));
            Assert.Equal("009", DataFormat.Adjust(x,999));
            
            x = 12;
            Assert.Throws<ArgumentOutOfRangeException>(() => DataFormat.Adjust(x, 10));
            Assert.Equal("12", DataFormat.Adjust(x,99));
            Assert.Equal("012", DataFormat.Adjust(x,100));
            Assert.Equal("012", DataFormat.Adjust(x,999));
        }
    }
}