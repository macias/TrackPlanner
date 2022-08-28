using MathUnit;
using System;
using System.Collections.Generic;
using System.Linq;
using TrackPlanner.Shared;
using TrackPlanner.Mapping;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Turner.Implementation
{
    internal sealed class GraphBubble
    {
        public enum Kind
        {
            Final,
            Crosspoint,
            Internal
        }
        private const ushort crosspointIndicator = ushort.MaxValue;

        private static int DEBUG_COUNTER;
        internal int DEBUG_ID { get; } = DEBUG_COUNTER++;

        public readonly int debugBucketIndex;
        private readonly string? info;

        public GeoZPoint Point { get; }
        public Length TrackSnapDistance { get; }
        public Kind BubbleKind { get; }

        private readonly Dictionary<GraphBubble, GraphBubbleConnection> targets;
        // here we can have index equal -1 indicating it is a crosspoint on the road
        // road id -> index along road (because roads have loops and knots we can several indices of the same road)
        private readonly Dictionary<long, HashSet<int>> roadIndices;

        public IEnumerable<(GraphBubble bubble, long? roadId, Length onRoadTravelDistance)> Targets => this.targets.SelectMany(it => it.Value.GetEntries().Select(v => (it.Key, v.roadId, v.onRoadTravelDistance)));

        public IEnumerable<RoadIndexLong> RoadIndices => enumerateIndices().Where(it => it.IndexAlongRoad != crosspointIndicator);

        public string KindLabel
        {
            get
            {
                switch (BubbleKind)
                {
                    case Kind.Crosspoint: return "x";
                    case Kind.Final: return "e";
                    case Kind.Internal: return "";
                    default: throw new NotSupportedException();
                }
            }
        }

        private IEnumerable<RoadIndexLong> enumerateIndices()
        {
            return this.roadIndices.SelectMany(it => it.Value.Select(v => new RoadIndexLong(it.Key, v)));
        }

        public GraphBubble(int debugBucketIndex, in GeoZPoint point, Length trackSnapDistance, Kind kind, string? info = null)
        {
            this.debugBucketIndex = debugBucketIndex;
            Point = point;
            TrackSnapDistance = trackSnapDistance;
            BubbleKind = kind;
            this.info = info;
            this.targets = new Dictionary<GraphBubble, GraphBubbleConnection>();
            this.roadIndices = new Dictionary<long, HashSet<int>>();
        }

        public void AddRoadIndex(in RoadIndexLong idx)
        {
            if (!this.roadIndices.TryGetValue(idx.RoadMapIndex, out var road_indices))
            {
                road_indices = new HashSet<int>();
                this.roadIndices.Add(idx.RoadMapIndex, road_indices);
            }

            road_indices.Add(idx.IndexAlongRoad);
        }

        public IEnumerable<RoadIndexLong> GetRoadIndices(long roadId)
        {
            return this.roadIndices[roadId].Select(it => new RoadIndexLong(roadId, it));
        }

        public void AddRoad(long roadId)
        {
            AddRoadIndex(new RoadIndexLong(roadId, crosspointIndicator));
        }

        internal void AddTarget(GraphBubble dest, long? roadId, Length onRoadTravelDistance)
        {
            if (this == dest)
                throw new ArgumentException();

            if (!this.targets.TryGetValue(dest, out GraphBubbleConnection? connection))
            {
                connection = new GraphBubbleConnection();
                this.targets.Add(dest, connection);
            }

            connection.Add(roadId, onRoadTravelDistance);
        }

        public override string ToString()
        {
            return $"bucket {debugBucketIndex}:{DEBUG_ID} {KindLabel}, {info} " + (this.RoadIndices.Any() ? this.RoadIndices.First().ToString() : "");
        }
#if ZOMBIE
        internal IEnumerable< long> GetConnectingRoads(GraphBubble target)
        {
            if (!this.targets.TryGetValue(target, out var conn))
                throw new ArgumentException();

            var result =  conn.GetEntries().Select(it => it.roadId).ToArray();
            if (!result.Any())
                throw new InvalidOperationException();

            return result;
/*            if (this.indices.Count == 0)
            {
                if (target.indices.Count == 0)
                    throw new InvalidOperationException();

                return target.GetConnectingRoads(this);
            }

            if (target.indices.Count == 0) // start or end of the path
            {
                RoadIndex cx_index = this.enumerateIndices().Single();
                if (cx_index.IndexAlongRoad != crosspointIndicator)
                    throw new InvalidOperationException();

                return cx_index.RoadId;
            }

            long[] common_roads = this.indices.Keys.Intersect(target.indices.Keys).ToArray();
            return common_roads.Single();*/
        }
#endif
    }
}