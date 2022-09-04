
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrackPlanner.Data;
using TrackPlanner.Settings;
using SharpKml.Base;
using SharpKml.Dom;
using SharpKml.Engine;
using TrackPlanner.Data.Stored;
using TrackPlanner.Shared;
using TimeSpan = System.TimeSpan;
using TrackPlanner.LinqExtensions;

namespace TrackPlanner.DataExchange
{
    public sealed class TrackWriter
    {
        public static LineDefinition segmentToKmlIput(UserVisualPreferences visualPrefs, LegFragment fragment)
        {
            string costFlags(Risk cost)
            {
                string s = "";
                if (cost.HasFlag(Risk.Dangerous))
                    s += "D";
                if (cost.HasFlag(Risk.Uncomfortable))
                    s += "U";
                if (cost.HasFlag(Risk.Suppressed))
                    s += "S";
                if (cost.HasFlag(Risk.HighTrafficBikeLane))
                    s += "B";
                return s;
            }

            var kml_lines = GetKmlSpeedLines(visualPrefs);
            var kml_forbidden = new KmlLineDecoration(new SharpKml.Base.Color32(visualPrefs.ForbiddenStyle.GetAbgrColor()), visualPrefs.ForbiddenStyle.Width);
            
                string format_ride_time(TimeSpan time)
                {
                    time = TimeSpan.FromMinutes(Math.Ceiling(time.TotalMinutes));
                    if (time.Days >= 1)
                        return time.ToString(@"d\.h\:m");
                    else
                        return time.ToString(@"h\:m");
                }

                string name = (fragment.IsForbidden? visualPrefs.ForbiddenStyle: visualPrefs.SpeedStyles[fragment.Mode]).Label;
                KmlLineDecoration style = fragment.IsForbidden? kml_forbidden : kml_lines[fragment.Mode];
                
                
                name += $" {costFlags(fragment.Risk)}";

                string? description = null;
                if (fragment.RoadIds.Count == 1)
                    name += $" #{fragment.RoadIds.Single()}";
                else
                    description = String.Join(", ", fragment.RoadIds.Select(it => $"#{it}"));
                
                return new LineDefinition(fragment.Places.Select(it => it.Point).ToArray(), name,description, style);
            
        }

        public static Dictionary<SpeedMode, KmlLineDecoration> GetKmlSpeedLines(UserVisualPreferences visualPrefs)
        {
            return visualPrefs.SpeedStyles.ToDictionary(it => it.Key, it => new KmlLineDecoration(new SharpKml.Base.Color32(it.Value.GetAbgrColor()), it.Value.Width));
        }

        private static void saveAsKml(UserVisualPreferences visualPrefs, Stream stream, string title, IEnumerable<LegPlan> legs,IEnumerable<TurnInfo>? turns)
        {
            var input=  new TrackWriterInput() { Title = title };
            input.Lines.AddRange(legs.SelectMany(it => it.Fragments).Select(seg => segmentToKmlIput(visualPrefs, seg)));
            input.AddTurns(turns);
            input.Waypoints.AddRange(legs.Select(it => it.Fragments.First().Places.First().Point)
                    .Concat(legs.Last().Fragments.Last().Places.Last().Point)
                    .Select(it => new WaypointDefinition(it,"-checkpoint", description:null, PointIcon.StarIcon)));

            var kml = input.BuildDecoratedKml();
            
            if ((kml.Root as Document)!.Features.Any())
            {
                kml.Save(stream);
            }
        }

        public static void SaveAsKml(UserVisualPreferences visualPrefs, Stream stream, string title, IEnumerable<LegPlan> legs,IEnumerable<TurnInfo>? turns)
        {
            saveAsKml(visualPrefs,stream,title, legs,turns);
        }

        public static void SaveAsKml(UserVisualPreferences visualPrefs, Stream stream, string title, TrackPlan plan)
        {
            SaveAsKml(visualPrefs, stream,title, plan.Legs, plan.DailyTurns?.SelectMany(x => x));
        }

        // https://github.com/samcragg/sharpkml/blob/main/docs/GettingStarted.md

        public static IEnumerable<(GeoZPoint point, string label)> LabelWaypoints(IEnumerable<GeoZPoint>? waypoints)
        {
            if (waypoints == null)
                yield break;

            int count = 0;
            foreach (GeoZPoint pt in waypoints)
                yield return (pt, $"Turn {count++}");
        }

        public static void Write(string filename, IReadOnlyList<GeoZPoint>? track, IEnumerable<GeoZPoint> waypoints, PointIcon? icon = null)
        {
            KmlFile kml = Build(track, waypoints, icon);
            kml.Save(filename);
        }

        public static KmlFile Build(IReadOnlyList<GeoZPoint>? track, IEnumerable<GeoZPoint>? waypoints, PointIcon? icon = null)
        {
            return BuildLabeled(track, LabelWaypoints(waypoints), icon);
        }

        public static void WriteLabeled(string filename, IReadOnlyList<GeoZPoint>? track, IEnumerable<(GeoZPoint point, string label)>? waypoints, PointIcon? icon = null, Color32? color = null)
        {
            KmlFile kml = BuildLabeled(track, waypoints, icon);
            kml.Save(filename);
        }

        public static KmlFile BuildLabeled(IReadOnlyList<GeoZPoint>? track, IEnumerable<(GeoZPoint point, string label)>? waypoints, PointIcon? icon = null)
        {
            IEnumerable<(GeoZPoint point, string label, PointIcon icon)>? rich_waypoints = null;
            if (waypoints != null)
                rich_waypoints = waypoints.Select(it =>
                {
                    PointIcon point_icon = icon ?? PointIcon.DotIcon;
                    //  if (color.HasValue)
                    //    point_icon = new PointIcon(point_icon.Id, color.Value, point_icon.ImageUrl);
                    return (it.point, it.label, point_icon);
                });

            var tracks = new List<(IReadOnlyList<GeoZPoint>, string)>();
            if (track!=null)
                tracks.Add((track,"Track"));;
            KmlFile kml = BuildKml(tracks, rich_waypoints);
            return kml;
        }

        public static KmlFile BuildMultiple(IEnumerable<(IReadOnlyList<GeoZPoint> track, string label)>? tracks,
            IEnumerable<(GeoZPoint point, string label, PointIcon icon)>? waypoints)
        {
            KmlFile kml = BuildKml(tracks, waypoints);
            return kml;
        }

        public static KmlFile BuildKml(IEnumerable<(IReadOnlyList<GeoZPoint> track, string label)>? tracks, IEnumerable<(GeoZPoint point, string label, PointIcon icon)>? waypoints)
        {
            return BuildDecoratedKml(tracks?.Select(trk => (trk.track, trk.label, (KmlLineDecoration?)null)), waypoints);
        }
        public static KmlFile BuildDecoratedKml(IEnumerable<(IReadOnlyList<GeoZPoint> track, string label, KmlLineDecoration? style)>? tracks, 
            IEnumerable<(GeoZPoint point, string label, PointIcon icon)>? waypoints)
        {
            var input = new TrackWriterInput();
            if (tracks!=null)
                input.Lines.AddRange(tracks.Select(it => new LineDefinition(it.track,it.label, description:null,it.style)));
            if (waypoints!=null)
                input.Waypoints.AddRange(waypoints.Select(it => new WaypointDefinition(it.point,it.label,description:null,it.icon)));
            
            return input.BuildDecoratedKml();
        }
    }
}
