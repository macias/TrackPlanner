using System;
using SharpKml.Dom;
using SharpKml.Engine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrackPlanner.Shared;

namespace TrackPlanner.DataExchange
{
    public sealed class TrackReader
    {
        // https://github.com/samcragg/sharpkml/blob/main/docs/GettingStarted.md

        public static IEnumerable<GeoZPoint> LEGACY_Read(string filename)
        {
            filename = System.IO.Path.GetFullPath(filename);

            KmlFile file;
            using (var stream = new MemoryStream(System.IO.File.ReadAllBytes(filename)))
                file = KmlFile.Load(stream);

            if (file.Root is Kml kml)
            {
                /*foreach (var placemark in kml.Flatten().OfType<Placemark>())
                {
                    Console.WriteLine(placemark.StyleUrl);
                }*/
                foreach (var line in kml.Flatten().OfType<LineString>())
                {
                    foreach (var coords in line.Coordinates)
                    {
                        yield return GeoZPoint.FromDegreesMeters(coords.Latitude, coords.Longitude, coords.Altitude);
                    }
                }
            }
        }

        public static TrackDefinition ReadUnstyled(string filename)
        {
            filename = System.IO.Path.GetFullPath(filename);

            KmlFile file;
            using (var stream = new MemoryStream(System.IO.File.ReadAllBytes(filename)))
                file = KmlFile.Load(stream);

            TrackDefinition track = new TrackDefinition();

            foreach (var placemark in file.Root.Flatten().OfType<Placemark>())
            {
                if (placemark.Geometry is Point kml_point)
                {
                    var coords = kml_point.Coordinate;
                    track.Waypoints.Add(new WaypointDefinition(GeoZPoint.FromDegreesMeters(coords.Latitude, coords.Longitude, coords.Altitude),
                        placemark.Name, placemark.Description?.Text, icon: null));
                }
                else if (placemark.Geometry is LineString kml_line)
                {
                    track.Lines.Add(new LineDefinition(kml_line.Coordinates.Select(it => GeoZPoint.FromDegreesMeters(it.Latitude, it.Longitude, it.Altitude)).ToList(),
                        placemark.Name, placemark.Description?.Text, style: null));
                }
                else
                    throw new NotSupportedException();
            }

            return track;
        }

    }
}
