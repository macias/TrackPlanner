using MathUnit;
using OsmSharp;
using OsmSharp.Streams;
using OsmSharp.Tags;
using SharpKml.Engine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TrackPlanner.Shared;
using TrackPlanner.DataExchange;


#nullable enable

namespace TrackPlanner.Mapping
{
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct Slicer
    {
        // the point of this type is to make area slices small in order to easy visualize it, being correct
        // (not end at north pole or take opposite point of the globe; in the latter case it would lead to ambigupus slices, because there are infinie number of great circles)
        private readonly Angle minLatitude;
        private readonly Angle maxLatitude;

        public Slicer(Angle minLatitude, Angle maxLatitude)
        {
            this.minLatitude = minLatitude;
            this.maxLatitude = maxLatitude;
        }

        public GeoZPoint GetSlicePoint(GeoZPoint point)
        {
            Angle latitude = maxLatitude + (point.Latitude - minLatitude);
            if (latitude <= Angle.PI / 2)
                return GeoZPoint.Create(latitude, point.Longitude, point.Altitude);

            // we went through the north pole to the other side of the globe
            latitude = Angle.PI - latitude;
            return GeoZPoint.Create(latitude, point.Longitude + Angle.PI, point.Altitude);
        }
    }
}