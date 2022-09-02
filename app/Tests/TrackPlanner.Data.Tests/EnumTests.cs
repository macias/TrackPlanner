using System;
using Xunit;

namespace TrackPlanner.Data.Tests
{

    public class EnumTests
    {
        [Fact]
        public void TripEventTest()
        {
            foreach (var elem in Enum.GetValues<TripEvent>())
            {
                elem.GetClassIcon();
                elem.GetLabel();
            }
       }
    }
}