using System;
using Xunit;

namespace TrackPlanner.PathFinder.Tests;

public class TravelCostTest
{
    [Fact]
    public void ComparisonTest()
    {
        var a = new TravelCost(TimeSpan.FromMinutes(2), 2);
        var b = new TravelCost(TimeSpan.FromMinutes(3), 1);
        Assert.True(a > b);
        Assert.True(a >= b);
        Assert.True(b < a);
        Assert.True(b <= a);
        Assert.True(b != a);
        Assert.True(a != b);
        Assert.False(b == a);
    }
}