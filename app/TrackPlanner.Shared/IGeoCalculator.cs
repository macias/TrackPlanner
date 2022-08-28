using MathUnit;
using TrackPlanner.Shared;

namespace TrackPlanner.Shared
{
    public interface IGeoCalculator
    {
        Angle AngleDistance(in GeoZPoint center, in GeoZPoint a, in GeoZPoint b);
        /// <summary>
        /// gets distance (difference) between two bearings
        /// </summary>
        /// <param name="fromAngle"></param>
        /// <param name="endAngle"></param>
        /// <returns></returns>
        Angle GetNormalizedBearingDistance(Angle fromAngle, Angle endAngle);
        Length GetDistance(in GeoZPoint a, in GeoZPoint b);
        GeoZPoint GetMidPoint(in GeoZPoint a, in GeoZPoint b);
        (Length distance, GeoZPoint crosspoint, Length distanceAlongSegment) GetDistanceToArcSegment(in GeoZPoint point, in GeoZPoint segmentStart, in GeoZPoint segmentEnd);
        //GeoZPoint GetDestination(GeoZPoint start, Angle bearing, Length distance);
        /// <summary>
        /// how much angular difference/distance does it make at given point travelling by given distance
        /// </summary>
        /// <param name="point"></param>
        /// <param name="distance"></param>
        /// <param name="latitudeDistance"></param>
        /// <param name="longitudeDistance"></param>
        void GetAngularDistances(GeoZPoint point, Length distance, out Angle latitudeDistance, out Angle longitudeDistance);
        bool CheckArcSegmentIntersection(in GeoZPoint startA, in GeoZPoint endA, in GeoZPoint startB, in GeoZPoint endB, out GeoZPoint crosspoint);
        GeoZPoint OppositePoint(in GeoZPoint p);
            }
}