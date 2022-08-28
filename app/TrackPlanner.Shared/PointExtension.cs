using Geo;
using MathUnit;
using System;
using System.Runtime.CompilerServices;
using TrackPlanner.Shared;


namespace TrackPlanner.Shared
{
    public static class PointExtension
    {
        public static Geo.GeoPoint Convert(this in GeoZPoint pt) => new Geo.GeoPoint(pt.Latitude, pt.Longitude);
        public static GeoZPoint Convert(this in GeoPoint pt) =>  GeoZPoint.Create(pt.Latitude, pt.Longitude, altitude: null);
    }

}
