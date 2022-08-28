using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TrackPlanner.Shared;
using TrackPlanner.Mapping;

namespace TrackPlanner.PathFinder
{
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Placement : IEquatable<Placement>
    {
        public static Placement Prestart(GeoZPoint point, bool isFinal)
        {
            return new Placement(point,associatedRoadId:null, PlaceKind.Prestart | PlaceKind.Snapped, isFinal);
        }
        public static Placement UserPoint(RoadBucket bucket)
        {
            return new Placement(bucket.UserPoint,associatedRoadId:null, PlaceKind.UserPoint | PlaceKind.Snapped, bucket.IsFinal);
        }
        public static Placement Crosspoint(GeoZPoint point, long roadId, bool isFinal)
        {
            return new Placement(point,roadId, PlaceKind.Cross  | PlaceKind.Snapped, isFinal);
        }

        public static Placement Aggregate(GeoZPoint point,long roadId)
        {
            return new Placement(point,roadId, PlaceKind.Aggregate, isFinal:false);
        }

        public GeoZPoint? Point { get; }
        private readonly long? associatedRoadId;
        private readonly PlaceKind kind;
        public bool IsUserPoint => kind.HasFlag(PlaceKind.UserPoint);
        public bool IsCross => kind.HasFlag(PlaceKind.Cross);
        public bool IsNode => kind.HasFlag(PlaceKind.Node);
        public bool IsPrestart => kind.HasFlag(PlaceKind.Prestart);
        public bool IsFinal => kind.HasFlag(PlaceKind.FinalBlob);
        public bool IsSnapped => kind.HasFlag(PlaceKind.Snapped);
        public long? NodeId { get; }

        public Placement()
        {
            throw new InvalidOperationException();
        }
        private Placement(GeoZPoint point,long? associatedRoadId, PlaceKind kind, bool isFinal)
        {
            if (kind == PlaceKind.Node)
                throw new ArgumentException();
            if (isFinal)
                kind |= PlaceKind.FinalBlob;
            this.associatedRoadId = associatedRoadId;
            this.kind = kind;
            this.Point = point;
            this.NodeId = null;
        }

        public Placement(long nodeId, bool isFinal,bool isSnapped)
        {
            if (isFinal && !isSnapped)
                throw new ArgumentException();
            
            this.kind = PlaceKind.Node;
            if (isFinal)
                this.kind |= PlaceKind.FinalBlob;
            if (isSnapped)
                this.kind |= PlaceKind.Snapped;

            this.associatedRoadId = null;
            this.Point = null;
            NodeId = nodeId;
        }

        public GeoZPoint GetPoint(IWorldMap map)
        {
            return Point ?? map.Nodes[this.NodeId!.Value];
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Point, kind, NodeId);
        }

        public override bool Equals(object? obj)
        {
            return obj is Placement node && Equals(node);
        }

        public bool Equals(Placement other)
        {
            return Point == other.Point &&
                   kind == other.kind &&
                   NodeId == other.NodeId;
        }

        public static bool operator ==(Placement left, Placement right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Placement left, Placement right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            string result;
            if (this.Point.HasValue)
                result = $"pt:{this.Point}";
            else
                result = $"nd:{this.NodeId}";

            return $"{result} k:{this.kind}";
        }
    }


}
