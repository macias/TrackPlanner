using SharpKml.Dom;
using SharpKml.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TrackPlanner.Shared;
using TrackPlanner.LinqExtensions;

namespace TrackPlanner.DataExchange
{
    public sealed class TrackWriterInput : TrackDefinition
    {
        public string? Title { get; set; }

        public TrackWriterInput()
        {
        }

        private static SharpKml.Base.Vector toVector(GeoZPoint pt)
        {
            var result = new SharpKml.Base.Vector(latitude: pt.Latitude.Degrees, longitude: pt.Longitude.Degrees);
            if (pt.Altitude.HasValue)
                result.Altitude = pt.Altitude.Value.Meters;
            return result;
        }

        private static CoordinateCollection toCollection(IEnumerable<GeoZPoint> points)
        {
            return new CoordinateCollection(points.Select(it => toVector(it)));
        }

        private static Placemark createWaypoint(GeoZPoint wpt, string? name, string? description,PointIcon? icon)
        {
            var feature = new Placemark()
            {
                Geometry = new Point() { Coordinate = toVector(wpt) },
                Name = name,
            };

            if (description != null)
                feature.Description = new Description() {Text = description};

            if (icon != null)
                feature.StyleUrl = new Uri($"#{icon.Id}", UriKind.Relative);

            return feature;
        }

        public IEnumerable<KmlFile> BuildGoogleKml()
        {
            const int featureLimit = 2_000;

            Document? root = null;

            {
                int count = 0;
                foreach ((GeoZPoint wpt, string? name, string? description,PointIcon? icon) in Waypoints)
                {
                    if (root==null)
                    {
                        root = new Document();
                        addWaypointStyles(root);
                    }

                    Placemark feature = createWaypoint(wpt, name, description, icon);

                    root.AddFeature(feature);
                    ++count;

                    if (root.Features.Count % featureLimit==0)
                    {
                        yield return KmlFile.Create(root, duplicates: false);
                        root = null;
                    }
                }
            }

            {
                foreach (var track in Lines)
                {
                    if (root == null)
                    {
                        root = new Document();
                        addTrackStyles(root);
                    }

                    addTrackToDocument(root,track);

                    if (root.Features.Count % featureLimit == 0)
                    {
                        yield return KmlFile.Create(root, duplicates: false);
                        root = null;
                    }


                }
            }

            if (root!=null)
            {
                yield return KmlFile.Create(root, duplicates: false);
                root = null;
            }
        }

        private void addTrackStyles(Document root)
        {
            foreach (var style in Lines.Select(it => it.Style).Where(it => it != null).DistinctBy(it => it!.Id))
            {
                var line_style = new LineStyle();
                line_style.Color = style!.Color;
                line_style.ColorMode = ColorMode.Normal;
                line_style.Width = style.Width;
                root.AddStyle(new Style()
                {
                    Line = line_style,
                    Id = style.Id
                });
            }
        }

        private void addWaypointStyles(Document root)
        {
            foreach (var icon in Waypoints.Select(it => it.Icon).Where(it => it != null).Distinct())
            {
                var dot_icon_style = new Style();
                dot_icon_style.Id = icon!.Id;
                dot_icon_style.Icon = new IconStyle();
                dot_icon_style.Icon.Color = icon.Color;
                dot_icon_style.Icon.ColorMode = ColorMode.Normal;
                dot_icon_style.Icon.Icon = new IconStyle.IconLink(new Uri(icon.ImageUrl));
                dot_icon_style.Icon.Scale = 1;
                root.AddStyle(dot_icon_style);
            }
        }

        public KmlFile BuildDecoratedKml()
        {
            var root = new Document();
            root.Name = Title;

            {
                addWaypointStyles(root);

                int count = 0;
                foreach ((GeoZPoint wpt, string? label,string? comment, PointIcon? icon) in Waypoints)
                {
                    Placemark feature = createWaypoint(wpt, label, comment, icon);

                    root.AddFeature(feature);
                    ++count;
                }
            }

            {
                addTrackStyles(root);

                foreach (var track in Lines)
                {
                    addTrackToDocument(root,track);
                }
            }

            return KmlFile.Create(root, duplicates: false);
        }

        private static void addTrackToDocument(Document root, LineDefinition line)
        {
            Placemark feature = new Placemark()
            {
                Geometry = new LineString() {Coordinates = toCollection(line.Points)},
                Name = line.Name,
                //Description = new Description(){ Text = track.description },
            };

            if (line.Description != null)
                feature.Description = new Description() {Text = line.Description};

            if (line.Style != null)
                feature.StyleUrl = new Uri($"#{line.Style.Id}", UriKind.Relative);

            root.AddFeature(feature);
        }
    }

    
}
