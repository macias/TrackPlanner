
using TrackPlanner.Shared;

namespace TrackPlanner.Data
{
    public sealed class TurnInfo
    {
        public enum EntityReference
        {
            Node,
            // in case of turn on roundabout the point should be its center (so it will not have underlying OSM node) 
            Roundabout
        }
        public static TurnInfo CreateRegular(long nodeId, GeoZPoint point, int trackIndex, bool forward, bool backward,string? reason)
        {
            return new TurnInfo(EntityReference.Node, nodeId, point, trackIndex, null, forward, backward,reason);
        }
        public static TurnInfo CreateRoundabout(long roadId, GeoZPoint point, int trackIndex, int? roundaboutGroup)
        {
            return new TurnInfo(EntityReference.Roundabout, roadId, point, trackIndex, roundaboutGroup, 
                true, true,reason:"roundabout");
        }

        public GeoZPoint Point { get; }
        public int TrackIndex { get; }
        public int? RoundaboutGroup { get; }
        public bool Forward { get; }
        public bool Backward { get; }
        public string? Reason { get; }
        public EntityReference Entity { get; }
        public long EntityId { get; }

        public TurnInfo(EntityReference entity,long entityId, GeoZPoint point, int trackIndex, int? roundaboutGroup, bool forward, bool backward,string? reason)
        {
            Entity = entity;
            EntityId = entityId;
            Point = point;
            TrackIndex = trackIndex;
            RoundaboutGroup = roundaboutGroup;
            Forward = forward;
            Backward = backward;
            Reason = reason;
        }

        public string GetLabel()
        {
            string label = $"{(Entity== EntityReference.Node?"N":"R")}-{EntityId} ";
            if (this.RoundaboutGroup.HasValue)
                label += $"(G{this.RoundaboutGroup}) ";
            else if (this.Backward != this.Forward)
            {
                label += $"({(this.Forward ? "F" : "B")}) ";
            }

            return label + $"track index {TrackIndex}";
        }

        public override bool Equals(object? obj)
        {
            if (obj is TurnInfo info)
                return Equals(info);
            else
                return false;
        }

        public bool Equals(TurnInfo obj)
        {
            if (object.ReferenceEquals(this, obj))
                return true;

            // track index is not included so we can easily compare track with its reversed counterpart
            return Point.Equals(obj.Point) && RoundaboutGroup == obj.RoundaboutGroup && Forward == obj.Forward && Backward == obj.Backward;
        }

        public override int GetHashCode()
        {
            return Point.GetHashCode()^ RoundaboutGroup.GetHashCode() ^ Forward.GetHashCode() ^ Backward.GetHashCode();
        }
    }
}
