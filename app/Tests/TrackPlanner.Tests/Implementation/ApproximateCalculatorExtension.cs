using MathUnit;
using TrackPlanner.Shared;
using TrackPlanner.Turner.Implementation;
using TrackPlanner.Mapping;
using TrackPlanner.PathFinder;
using Xunit;

namespace TrackPlanner.Tests.Implementation
{
    public static class ApproximateCalculatorExtension
    {
        internal static bool IsCrossIntersectionOnlyBySides(this ApproximateCalculator calc, in GeoZPoint center, in GeoZPoint incomingTrack, 
            in GeoZPoint outgoingTrack, in GeoZPoint leftArmPoint, in GeoZPoint rightArmPoint)
        {
            return calc.IsCrossIntersection(center, incomingTrack, outgoingTrack, leftArmPoint, rightArmPoint, Angle.Zero, out Angle inLeftAngle, out Angle inRightAngle, out Angle outLeftAngle, out Angle outRightAngle);
        }
    }


}
