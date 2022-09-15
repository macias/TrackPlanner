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
using TrackPlanner.Shared;
using TrackPlanner.DataExchange;
using TrackPlanner.Mapping.Data;
using TrackPlanner.Structures;


#nullable enable

namespace TrackPlanner.Mapping
{
    internal sealed class DEBUG_NoZone
    {
        private bool zoneEnabled;
        private bool fullyEnabled;

        public long ZoneId { get; }

        private readonly Dictionary<GeoZPoint, List<(GeoZPoint pt, bool on_edge)>> crosspoints;
        private readonly HashSet<GeoZPoint> tainted;
        private readonly List<long> zoneForbiddenRoads;
        private readonly List<long> allForbiddenRoads;
        private readonly ILogger logger;
        private readonly IGeoCalculator calc;
        private readonly long roadId;

        public NamedPolygon? Zone { get; private set; }

        private Slicer slicer;

        public DEBUG_NoZone(ILogger logger, IGeoCalculator calc, long zoneId, long roadId)
        {
            this.logger = logger;
            this.calc = calc;
            ZoneId = zoneId;
            this.roadId = roadId;
            this.crosspoints = new Dictionary<GeoZPoint, List<(GeoZPoint pt, bool on_edge)>>();
            this.tainted = new HashSet<GeoZPoint>();

            this.zoneForbiddenRoads = new List<long>();
            this.allForbiddenRoads = new List<long>();
        }

        internal void Activate(NamedPolygon zone, long roadId, Slicer slicer)
        {
            this.zoneEnabled = zone.Id == ZoneId;
            this.fullyEnabled = zoneEnabled && this.roadId == roadId;
            if (!fullyEnabled)
                return;

            this.Zone = zone;
            this.slicer = slicer;

            logger.Info($"Watching road {roadId}");
        }

        public void Forbidden(long roadId)
        {
            this.allForbiddenRoads.Add(roadId);

            if (!this.zoneEnabled)
                return;

            this.zoneForbiddenRoads.Add(roadId);
        }

        internal void AddCrossPoint(GeoZPoint roadPoint, GeoZPoint crosspoint, bool onEdge)
        {
            if (!this.fullyEnabled)
                return;

            var list = this.crosspoints[roadPoint];
            list.Add((crosspoint, onEdge));
        }

        internal void MarkTaintedPoint(GeoZPoint roadPoint)
        {
            if (!this.fullyEnabled)
                return;

            this.tainted.Add(roadPoint);
        }

        internal void RegisterPoint(GeoZPoint roadPoint)
        {
            if (!this.fullyEnabled)
                return;

            // points can have same coordinates
            this.crosspoints.TryAdd(roadPoint, new List<(GeoZPoint pt, bool on_edge)>());
        }

        public KmlFile? BuildZonePointsKml(IReadOnlyMap<long, GeoZPoint> nodes)
        {
            if (Zone == null)
                return null;

            var input = new TrackWriterInput();

            foreach (var pt in Zone.Value.Nodes.Select(id => nodes[id]).Skip(1))
            {
                input.AddPoint(pt, null,null, PointIcon.DotIcon);
            }

            return input.BuildDecoratedKml();
        }

        public KmlFile? BuildZoneLineKml(IReadOnlyMap<long, GeoZPoint> nodes)
        {
            if (Zone == null)
                return null;
            
            var input = new TrackWriterInput();
            input.AddLine(Zone.Value.Nodes.Select(id => nodes[id]), $"Zone #{ZoneId}");

            return input.BuildDecoratedKml();
        }

        public KmlFile BuildKml(IReadOnlyMap<long, GeoZPoint> nodes)
        {
            var input = new TrackWriterInput();

            if (this.crosspoints.Count == 0)
                logger.Verbose($"Road {this.roadId} does not have any point within area");
            else
            {
                int idx = 0;
                foreach (var node_entry in this.crosspoints)
                {
                    GeoZPoint slice_pt = slicer.GetSlicePoint(node_entry.Key);
                    input.AddLine(new[] { node_entry.Key, slice_pt }, $"{node_entry.Key.Latitude}, {node_entry.Key.Longitude} -> {slice_pt.Latitude}");

                    input.AddPoint(node_entry.Key, $"PT {idx}, count {node_entry.Value.Count}", null, tainted.Contains(node_entry.Key) ? PointIcon.ParkingIcon : PointIcon.CircleIcon);
                    foreach (var cx in node_entry.Value)
                        input.AddPoint(cx.pt, $"{idx} CX {(cx.on_edge ? "on-edge" : "")}");

                    ++idx;
                }
            }

            return input.BuildDecoratedKml();
        }

        internal KmlFile BuildZoneForbiddenKml(IMap<long, GeoZPoint> nodes, IReadOnlyMap<long, RoadInfo> roads)
        {
            return BuildForbiddenKml(zoneForbiddenRoads, nodes, roads);
        }

        internal KmlFile BuildAllForbiddenKml(IMap<long, GeoZPoint> nodes, IReadOnlyMap<long, RoadInfo> roads)
        {
            return BuildForbiddenKml(allForbiddenRoads, nodes, roads);
        }

        private static KmlFile BuildForbiddenKml(List<long> forbidden, IMap<long, GeoZPoint> nodes, IReadOnlyMap<long, RoadInfo> roads)
        {
            var input = new TrackWriterInput();
            foreach (var road_info in forbidden.Select(it => roads[it]))
            {
                input.AddLine(road_info.Nodes.Select(it => nodes[it]).ToArray(), $"Rd #{road_info.Identifier}");
            }

            return input.BuildDecoratedKml();
        }
    }
}