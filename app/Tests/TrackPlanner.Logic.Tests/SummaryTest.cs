using System.Collections.Generic;
using System.Linq;
using MathUnit;
using Microsoft.VisualBasic;
using TrackPlanner.Data;
using TrackPlanner.Logic.Tests.Data;

namespace TrackPlanner.Logic.Tests;

public class SummaryTest
{
    private static DummyReadOnlySchedule createDummySchedule()
    {
        var schedule = new DummyReadOnlySchedule()
        {
            IsLooped = true,
            Days = new List<DummyDay>()
            {
                new DummyDay() {Anchors = new List<IAnchor>() {new DummyAnchor(), new DummyAnchor(), new DummyAnchor()}},
                new DummyDay() {Anchors = new List<IAnchor>() {new DummyAnchor(), new DummyAnchor()}},
            },
            TrackPlan = new TrackPlan()
            {
                Legs = new List<LegPlan>()
                {
                    new LegPlan() {UnsimplifiedDistance = Length.FromMeters(100)},
                    new LegPlan() {UnsimplifiedDistance = Length.FromMeters(200)},

                    new LegPlan() {UnsimplifiedDistance = Length.FromMeters(400)},
                    new LegPlan() {UnsimplifiedDistance = Length.FromMeters(800)},
                    new LegPlan() {UnsimplifiedDistance = Length.FromMeters(20)},

                }
            }
        };

        return schedule;
    }

    [Fact]
    public void SingleAnchorSummaryTest()
    {
        var schedule = new DummyReadOnlySchedule()
        {
            IsLooped = true,
            Days = new List<DummyDay>()
            {
                new DummyDay() {Anchors = new List<IAnchor>() {new DummyAnchor()}},
            },
            TrackPlan = new TrackPlan(),
        };

        var summary = schedule.GetSummary();

        // nothing crashed = success
    }

    [Fact]
    public void SummaryDayDistancesTest()
    {
        var schedule = createDummySchedule();

        var summary = schedule.GetSummary();
        
        Assert.Equal(Length.FromMeters(300), summary.Days[0].Distance );
        Assert.Equal(Length.FromMeters(1220), summary.Days[1].Distance );

        Assert.Equal(Length.FromMeters(1520), summary.Distance );
    }
    
    [Fact]
    public void DayDistancesByLegsTest()
    {
        var schedule = createDummySchedule();

        var legs_0 = schedule.GetDayLegs(0);
        var legs_1 = schedule.GetDayLegs(1);
        
        Assert.Equal(Length.FromMeters(300), legs_0.Select(it => it.UnsimplifiedDistance).Sum() );
        Assert.Equal(Length.FromMeters(1220), legs_1.Select(it => it.UnsimplifiedDistance).Sum());
    }

    [Fact]
    public void LastCheckpointPerDayTest()
    {
        var schedule = createDummySchedule();

        var summary = schedule.GetSummary();

        Assert.False(summary.Days[0].Checkpoints.Last().GetAtomicEvents(summary).Any());
        Assert.False(summary.Days[1].Checkpoints.Last().GetAtomicEvents(summary).Any());
    }

}