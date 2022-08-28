using MathUnit;
using System;
using System.Collections.Generic;
using System.Linq;
using TrackPlanner.Shared;
using TrackPlanner.Turner.Implementation;
using TrackPlanner.Mapping;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Turner
{
    public static class WorldMapExtension
    {
        internal static bool IsCycleWay(this IWorldMap map, long roadId) => map.Roads[roadId].Kind == WayKind.Cycleway;

        internal static bool IsMotorRoad(this IWorldMap map, long roadId)
            => map.Roads[roadId].Kind != WayKind.Cycleway && map.Roads[roadId].Kind != WayKind.Footway && map.Roads[roadId].Kind != WayKind.Steps;

        internal static bool IsSignificantMotorRoad(this IWorldMap map, long roadId) => map.IsMotorRoad(roadId) && map.Roads[roadId].Kind <= WayKind.Unclassified;

        internal static int CycleWeight(this IWorldMap map, in RoadIndexLong idx) => map.IsCycleWay(idx.RoadMapIndex) ? 1 : 0;

        internal static bool IsRoadContinuation(this IWorldMap map, long currentRoadId, long nextRoadId)
        {
            return currentRoadId == nextRoadId
                || (map.IsCycleWay(currentRoadId) && map.IsCycleWay(nextRoadId))
                || (map.Roads[currentRoadId].HasName && map.Roads[currentRoadId].NameIdentifier == map.Roads[nextRoadId].NameIdentifier);
        }

        internal static bool IsRoadLooped(this IWorldMap map, long roadId)
        {
            return map.Roads[roadId].Nodes.Count != map.Roads[roadId].Nodes.Distinct().Count();
        }

        internal static bool IsDirectionAllowed(this IWorldMap map, in RoadIndexLong from, in RoadIndexLong dest)
        {
            if (from.RoadMapIndex != dest.RoadMapIndex)
                throw new ArgumentException($"Cannot compute direction for two different roads {from.RoadMapIndex} {dest.RoadMapIndex}");
            if (map.GetNode(from) == map.GetNode(dest))
                throw new ArgumentException($"Cannot compute direction for the same spot {from.IndexAlongRoad}");

            if (!map.Roads[from.RoadMapIndex].OneWay)
                return true;

            if (map.IsRoadLooped(from.RoadMapIndex))
                throw new ArgumentException($"Cannot tell direction of looped road {from.RoadMapIndex}");

            return dest.IndexAlongRoad > from.IndexAlongRoad;
        }

        internal static Length GetRoadDistance(this IWorldMap map, IGeoCalculator calc, RoadIndexLong start, RoadIndexLong dest)
        {
            if (map.GetNode(start) == map.GetNode(dest))
                return Length.Zero;

            if (start.RoadMapIndex != dest.RoadMapIndex)
                throw new ArgumentException();

            if (start.IndexAlongRoad > dest.IndexAlongRoad)
                return GetRoadDistance(map, calc, dest, start);

            var length = Length.Zero;

            RoadIndexLong curr = start;
            foreach (var next in GetRoadIndices(map, start, dest).Skip(1))
            {
                length += calc.GetDistance(map.GetPoint(curr), map.GetPoint(next));
                curr = next;
            }

            return length;
        }

        internal static IEnumerable<RoadIndexLong> GetRoadIndices(this IWorldMap map, RoadIndexLong start, RoadIndexLong dest)
        {
            if (start.RoadMapIndex != dest.RoadMapIndex)
                throw new ArgumentException();

            // todo: add road loop handling

            yield return start;

            int dir = dest.IndexAlongRoad.CompareTo(start.IndexAlongRoad);
            for (RoadIndexLong curr = start; curr != dest;)
            {
                RoadIndexLong next = new RoadIndexLong(curr.RoadMapIndex, curr.IndexAlongRoad + dir);
                yield return next;
                curr = next;
            }
        }


    }

}
