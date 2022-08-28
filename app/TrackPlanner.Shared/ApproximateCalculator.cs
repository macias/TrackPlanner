using Geo;
using MathUnit;
using System;
using System.Runtime.CompilerServices;
using TrackPlanner.Shared;

[assembly: InternalsVisibleTo("TrackTurner.Tests")] 

namespace TrackPlanner.Shared
{
    public sealed class ApproximateCalculator : IGeoCalculator
    {

        public Length GetDistance(in GeoZPoint a, in GeoZPoint b)
        {
            return GeoCalculator.GetDistance(a.Convert(), b.Convert());
        }

        public GeoZPoint OppositePoint(in GeoZPoint p)
        {
            return GeoCalculator.OppositePoint(p.Convert()).Convert();
        }

        public (Length distance, GeoZPoint crosspoint, Length distanceAlongSegment) GetDistanceToArcSegment(in GeoZPoint point, in GeoZPoint segmentStart, in GeoZPoint segmentEnd)
        {
            Length dist = GeoCalculator.GetDistanceToArcSegment(point.Convert(), segmentStart.Convert(), segmentEnd.Convert(),
                out GeoPoint cx, out Length distanceAlongSegment);
            return (dist, cx.Convert(), distanceAlongSegment);
        }

        public bool CheckArcSegmentIntersection(in GeoZPoint startA, in GeoZPoint endA, in GeoZPoint startB, in GeoZPoint endB, out GeoZPoint crosspoint)
        {
            GeoCalculator.GetArcSegmentIntersection(startA.Convert(), endA.Convert(), startB.Convert(), endB.Convert(),
                       out GeoPoint? cx1, out GeoPoint? cx2);
            crosspoint = cx1?.Convert() ?? default;
            return cx1.HasValue;
        }

        /*public double SquareDistance(in GeoZPoint a, in GeoZPoint b)
        {
            Angle shortest_distance(Angle a,Angle b)
            {
                Angle diff = (a - b).Normalize();
                return diff <= Angle.PI ? diff : Angle.FullCircle - diff;
            }
            return Math.Pow(shortest_distance(a.Latitude, b.Latitude).Radians, 2) + Math.Pow(shortest_distance(a.Longitude, b.Longitude).Radians, 2);
        }*/

        /// <summary>
        /// 
        /// </summary>
        /// <param name="center"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns>angle in (0,360) range. Note angles are computed counterclockwise</returns>
        public Angle AngleDistance(in GeoZPoint center, in GeoZPoint a, in GeoZPoint b)
        {
            // https://stackoverflow.com/questions/1211212/how-to-calculate-an-angle-from-three-points
            // https://math.stackexchange.com/questions/878785/how-to-find-an-angle-in-range0-360-between-2-vectors

            /*            var x1 = (a.Longitude - center.Longitude).Radians;
                        var y1 = (a.Latitude - center.Latitude).Radians;
                        var x2 = (b.Longitude - center.Longitude).Radians;
                        var y2 = (b.Latitude - center.Latitude).Radians;

                        // this is buggy, because of the around 0 angle issue (e.g. a is 359, b is 1 -- you either get too negative values, or too big)
                        var dot = x1 * x2 + y1 * y2;     // dot product
                        var det = x1 * y2 - y1 * x2;    // determinant
                        var angle = Math.Atan2(det, dot);  // atan2(y, x) or atan2(sin, cos)

                        return Angle.FromRadians(angle).Normalize();
            */

            var center_conv = center.Convert();
            Angle bearing_a = GeoCalculator.GetBearing(center_conv,a.Convert());
            Angle bearing_b = GeoCalculator.GetBearing(center_conv, b.Convert());

            return (bearing_a- bearing_b).Normalize();
        }

        public Angle GetNormalizedBearingDistance(Angle startAngle, Angle endAngle)
        {
            return (endAngle.Normalize()-startAngle.Normalize()).Normalize();
        }

        public Angle GetAbsoluteBearingDifference(Angle bearingA, Angle bearingB)
        {
            var bearing_dist = GetNormalizedBearingDistance(bearingA, bearingB);
            return bearing_dist <= Angle.PI ? bearing_dist : Angle.FullCircle - bearing_dist;

        }

        /*  public GeoZPoint GetDestination(GeoZPoint start, Angle bearing, Length distance)
          {
              return GeoCalculator.GetDestination(start.Convert(), bearing, distance).Convert();
          }*/

        public void GetAngularDistances(GeoZPoint point, Length distance, out Angle latitudeDistance, out Angle longitudeDistance)
        {
            latitudeDistance = Angle.FullCircle * (distance / GeoCalculator.EarthCircumference);
            // with increasing latitude (Y) the radius of the earth cicle is smaller, thus we have to compute it
            longitudeDistance = GeoCalculator.GetLongitudeDifference(point.Latitude, distance);
        }

        public GeoZPoint GetMidPoint(in GeoZPoint a, in GeoZPoint b)
        {
            GeoPoint pt = GeoCalculator.GetMidPoint(a.Convert(), b.Convert());
            return GeoZPoint.Create(pt.Latitude, pt.Longitude, a.Altitude == null || b.Altitude == null ? null : (a.Altitude + b.Altitude) / 2);
        }

        // do our track crosses other road in shape of (more or less) like "-|-"
        // note, it is not about cross geometry
        // two roads
        // 
        // _|
        // and
        //     _
        //   |
        // would form a cross too but no of those roads CROSSES the others

        public bool IsCrossIntersection(in GeoZPoint center, in GeoZPoint incomingTrack, in GeoZPoint outgoingTrack, in GeoZPoint leftArmPoint, in GeoZPoint rightArmPoint, Angle angleSeparation,
            out Angle inLeftAngle, out Angle inRightAngle, out Angle outLeftAngle, out Angle outRightAngle)
        {
            int angle_side(Angle angle) => (angle.Normalize() - Angle.PI).Sign();

            // basically we compute direction, the arms should go -- track, other, track, other (clockwise, or counterclockwise) -- then we have crossing
            Angle outgoing_bearing = GeoCalculator.GetBearing(center.Convert(), outgoingTrack.Convert());
            Angle incoming_bearing = GeoCalculator.GetBearing(center.Convert(), incomingTrack.Convert());

            Angle right_bearing = GeoCalculator.GetBearing(center.Convert(), rightArmPoint.Convert());
            Angle left_bearing = GeoCalculator.GetBearing(center.Convert(), leftArmPoint.Convert());

            Angle bearing_diff(Angle bearing1, Angle bearing2) { var dist = (bearing1 - bearing2).Normalize(); return dist <= Angle.PI ? dist : Angle.FullCircle - dist; };

            inLeftAngle = bearing_diff(incoming_bearing, left_bearing);
            inRightAngle = bearing_diff(incoming_bearing, right_bearing);
            outLeftAngle = bearing_diff(outgoing_bearing, left_bearing);
            outRightAngle = bearing_diff(outgoing_bearing, right_bearing);
            if (inLeftAngle < angleSeparation || inRightAngle < angleSeparation || outLeftAngle < angleSeparation || outRightAngle < angleSeparation)
                return false;

            int angle_dir = angle_side(right_bearing - outgoing_bearing);

            if (angle_side(incoming_bearing - right_bearing) != angle_dir)
                return false;

            if (angle_side(left_bearing - incoming_bearing) != angle_dir)
                return false;

            if (angle_side(outgoing_bearing - left_bearing) != angle_dir)
                return false;


            return true;
        }

        public Angle GetBearing(in GeoZPoint start, in GeoZPoint dest)
        {
            return GeoCalculator.GetBearing(start.Convert(), dest.Convert());
        }

        public GeoZPoint PointAlongSegment(in GeoZPoint start, in GeoZPoint dest, Length length)
        {
            Angle bearing = this.GetBearing(start, dest);
            return GeoCalculator.GetDestination(start.Convert(), bearing, length).Convert();
        }
    }

}
