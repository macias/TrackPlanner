using MathUnit;
using TrackPlanner.Shared;
using TrackPlanner.Turner.Implementation;
using TrackPlanner.Mapping;
using TrackPlanner.PathFinder;
using TrackPlanner.Tests.Implementation;
using Xunit;

namespace TrackPlanner.Tests
{
    public class CalculatorTest
    {
        const int angleOrecision = 7;

        [Fact]
        public void AngleDistanceTest()
        {
            var calc = new ApproximateCalculator();

            var center = new GeoZPoint();

            {
                var a = GeoZPoint.FromDegreesMeters(1, 0, null);
                var b = GeoZPoint.FromDegreesMeters(0, 1, null);

                Assert.Equal(270, calc.AngleDistance(center, a, b).Degrees, angleOrecision);
                Assert.Equal(90, calc.AngleDistance(center, b, a).Degrees, angleOrecision);
            }
            {
                var a = GeoZPoint.FromDegreesMeters( -180, 0,null);
                var b = GeoZPoint.FromDegreesMeters( 180,0, null);

                Assert.Equal(180, calc.AngleDistance(center, a, b).Degrees, angleOrecision);
                Assert.Equal(180, calc.AngleDistance(center, b, a).Degrees, angleOrecision);
            }
            {
                var a = GeoZPoint.FromDegreesMeters(1, 0.1, null);
                var b = GeoZPoint.FromDegreesMeters(0.1, 1, null);

//                Assert.Equal(281.4217738, calc.AngleDistance(center, a, b).Degrees, angle_precision); // using double internally
                Assert.Equal(281.4209013144078, calc.AngleDistance(center, a, b).Degrees, angleOrecision); // using floats internally
                //Assert.Equal(78.5799715, calc.AngleDistance(center, b, a).Degrees, angle_precision);
                Assert.Equal(78.57909868559221, calc.AngleDistance(center, b, a).Degrees, angleOrecision);
            }
            {
                var a = GeoZPoint.FromDegreesMeters(1, 0.1, null);
                var b = GeoZPoint.FromDegreesMeters(-1, 0, null);

//                Assert.Equal(185.7108869, calc.AngleDistance(center, a, b).Degrees, angle_precision);
                Assert.Equal(185.7100143136971, calc.AngleDistance(center, a, b).Degrees, angleOrecision);
//                Assert.Equal(174.2899858, calc.AngleDistance(center, b, a).Degrees, angle_precision);
                Assert.Equal(174.2899856863029, calc.AngleDistance(center, b, a).Degrees, angleOrecision);
            }
            {
                var a = GeoZPoint.FromDegreesMeters(1, 0.1, null);
                var b = GeoZPoint.FromDegreesMeters(0, -1, null);

//                Assert.Equal(95.7108869, calc.AngleDistance(center, a, b).Degrees, angle_precision);
                Assert.Equal( 95.71001431369709, calc.AngleDistance(center, a, b).Degrees, angleOrecision);
//                Assert.Equal(264.2899858, calc.AngleDistance(center, b, a).Degrees, angle_precision);
                Assert.Equal(264.2899856863029, calc.AngleDistance(center, b, a).Degrees, angleOrecision);
            }
            {
                var a = GeoZPoint.FromDegreesMeters(1, 0.1, null);
                var b = GeoZPoint.FromDegreesMeters(1, 0, null);

  //              Assert.Equal(5.7108869, calc.AngleDistance(center, a, b).Degrees, angle_precision);
                Assert.Equal(5.710014313697095, calc.AngleDistance(center, a, b).Degrees, angleOrecision);
//                Assert.Equal(354.2899858, calc.AngleDistance(center, b, a).Degrees, angle_precision);
                Assert.Equal(354.28998568630294, calc.AngleDistance(center, b, a).Degrees, angleOrecision);
            }
        }

        [Fact]
        public void PositiveCrossIntersectionTest()
        {
            var calc = new ApproximateCalculator();

            var center = new GeoZPoint();

            var n = GeoZPoint.FromDegreesMeters(1, 0.1, null);
            var e = GeoZPoint.FromDegreesMeters(-0.1, 1, null);
            var s = GeoZPoint.FromDegreesMeters(-1, -0.1, null);
            var w = GeoZPoint.FromDegreesMeters(0.1, -1, null);

            Assert.True(calc.IsCrossIntersectionOnlyBySides(center, n, s, e, w));
            Assert.True(calc.IsCrossIntersectionOnlyBySides(center, n, s, w, e));
            Assert.True(calc.IsCrossIntersectionOnlyBySides(center, s, n, w, e));
            Assert.True(calc.IsCrossIntersectionOnlyBySides(center, s, n, e, w));
        }

        [Fact]
        public void NegativeCrossIntersectionTest()
        {
            var calc = new ApproximateCalculator();

            var center = new GeoZPoint();

            var n = GeoZPoint.FromDegreesMeters(1, 0.1, null);
            var e = GeoZPoint.FromDegreesMeters(-0.1, 1, null);
            var s = GeoZPoint.FromDegreesMeters(-1, -0.1, null);
            var w = GeoZPoint.FromDegreesMeters(0.1, -1, null);

            Assert.False(calc.IsCrossIntersectionOnlyBySides(center, n, e, s, w));
            Assert.False(calc.IsCrossIntersectionOnlyBySides(center, n, e, w, s));
            Assert.False(calc.IsCrossIntersectionOnlyBySides(center, e, n, s, w));
            Assert.False(calc.IsCrossIntersectionOnlyBySides(center, e, n, w, s));
            Assert.False(calc.IsCrossIntersectionOnlyBySides(center, s, e, n, w));
            Assert.False(calc.IsCrossIntersectionOnlyBySides(center, s, e, w, n));
            Assert.False(calc.IsCrossIntersectionOnlyBySides(center, e, s, n, w));
            Assert.False(calc.IsCrossIntersectionOnlyBySides(center, e, s, w, n));
        }
        
        [Fact]
        public void AbsoluteBearingDifferenceTest()
        {
            var calc = new ApproximateCalculator();

            Assert.Equal(0,calc.GetAbsoluteBearingDifference(Angle.FromDegrees(90),Angle.FromDegrees(90)).Degrees,angleOrecision);
            
            Assert.Equal(2,calc.GetAbsoluteBearingDifference(Angle.FromDegrees(-1),Angle.FromDegrees(+1)).Degrees,angleOrecision);
            Assert.Equal(2,calc.GetAbsoluteBearingDifference(Angle.FromDegrees(+1),Angle.FromDegrees(-1)).Degrees,angleOrecision);
            
            Assert.Equal(2,calc.GetAbsoluteBearingDifference(Angle.FromDegrees(359),Angle.FromDegrees(+1)).Degrees,angleOrecision);
            Assert.Equal(2,calc.GetAbsoluteBearingDifference(Angle.FromDegrees(+1),Angle.FromDegrees(359)).Degrees,angleOrecision);
            
            Assert.Equal(179,calc.GetAbsoluteBearingDifference(Angle.FromDegrees(181),Angle.FromDegrees(+2)).Degrees,angleOrecision);
            Assert.Equal(179,calc.GetAbsoluteBearingDifference(Angle.FromDegrees(2),Angle.FromDegrees(181)).Degrees,angleOrecision);

            Assert.Equal(179,calc.GetAbsoluteBearingDifference(Angle.FromDegrees(-179),Angle.FromDegrees(+2)).Degrees,angleOrecision);
            Assert.Equal(179,calc.GetAbsoluteBearingDifference(Angle.FromDegrees(2),Angle.FromDegrees(-179)).Degrees,angleOrecision);
        }

        [Fact]
        public void NormalizedBearingDistanceTest()
        {
            var calc = new ApproximateCalculator();

            Assert.Equal(0,calc.GetNormalizedBearingDistance(Angle.FromDegrees(90),Angle.FromDegrees(90)).Degrees,angleOrecision);

            Assert.Equal(2,calc.GetNormalizedBearingDistance(Angle.FromDegrees(-1),Angle.FromDegrees(+1)).Degrees,angleOrecision);
            Assert.Equal(358,calc.GetNormalizedBearingDistance(Angle.FromDegrees(+1),Angle.FromDegrees(-1)).Degrees,angleOrecision);
            
            Assert.Equal(2,calc.GetNormalizedBearingDistance(Angle.FromDegrees(359),Angle.FromDegrees(+1)).Degrees,angleOrecision);
            Assert.Equal(358,calc.GetNormalizedBearingDistance(Angle.FromDegrees(+1),Angle.FromDegrees(359)).Degrees,angleOrecision);
            
            Assert.Equal(181,calc.GetNormalizedBearingDistance(Angle.FromDegrees(181),Angle.FromDegrees(+2)).Degrees,angleOrecision);
            Assert.Equal(179,calc.GetNormalizedBearingDistance(Angle.FromDegrees(2),Angle.FromDegrees(181)).Degrees,angleOrecision);

            Assert.Equal(181,calc.GetNormalizedBearingDistance(Angle.FromDegrees(-179),Angle.FromDegrees(+2)).Degrees,angleOrecision);
            Assert.Equal(179,calc.GetNormalizedBearingDistance(Angle.FromDegrees(2),Angle.FromDegrees(-179)).Degrees,angleOrecision);
        }

    }
}
