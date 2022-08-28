using MathUnit;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using TrackPlanner.Data;
using TrackPlanner.Settings;
using TrackPlanner.Shared;
using TrackRadar.Collections;
using TrackPlanner.DataExchange;
using TrackPlanner.LinqExtensions;
using TrackPlanner.Mapping;

namespace TrackPlanner.PathFinder
{
    public readonly record struct NodePoint
    {
        public static NodePoint CreatePoint(GeoZPoint point)
        {
            return new NodePoint(null, point);
        }

        public static NodePoint CreateNode(long? nodeId)
        {
            return new NodePoint(nodeId, null);
        }

        public long? NodeId { get; }
        public GeoZPoint? Point { get; }

        private NodePoint(long? nodeId, GeoZPoint? point)
        {
            NodeId = nodeId;
            Point = point;
        }
    }
}