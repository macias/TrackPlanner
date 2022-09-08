using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MathUnit;
using TrackPlanner.Data;
using TrackPlanner.Settings;
using SharpKml.Dom;
using TrackPlanner.Shared;
using TrackPlanner.DataExchange;
using TrackPlanner.Mapping.Data;
using TrackPlanner.Data.Stored;

namespace TrackPlanner.Mapping
{
    public static class WorldMapExtension
    {
        public static string KmlDangerousTag => "dangerous";
        
        public static void SaveAsKml(this IWorldMap map, UserVisualPreferences visualPrefs, string path)
        {
            var speed_lines = TrackWriter.GetKmlSpeedLines(visualPrefs);
            var title = System.IO.Path.GetFileNameWithoutExtension(path);

            var input=  new TrackWriterInput() { Title = title };
            input.Lines = map.Roads.Select(it =>
            {
                var road = map.Roads[it.Key];
                return new LineDefinition(road.Nodes.Select(node_id => map.Nodes[node_id]).ToArray(), name: it.Key.ToString(),
                    description:road.DetailsToString(), speed_lines[ it.Value.GetRoadSpeedMode()]);
            }).ToList();
            input.Waypoints = map.Nodes.Select(it => new WaypointDefinition (it.Value,it.Key.ToString(),
                    description:(map.IsBikeFootRoadDangerousNearby(it.Key)?KmlDangerousTag:""), PointIcon.DotIcon))
                .ToList();

            var kml = input.BuildDecoratedKml();
            
            if ((kml.Root as Document)!.Features.Any())
            {
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                {
                    kml.Save(stream);
                }
            }
        }
        
        public static IWorldMap ExtractMiniMap(this IWorldMap sourceMap, ILogger logger, IGeoCalculator calc, Length distance,params GeoZPoint[] focusPoints)
        {
            var nodes_extract = new HashMap<long, GeoZPoint>();

            foreach ((long node_id, GeoZPoint coords) in sourceMap.Nodes)
            {
                if (focusPoints.Any(it => calc.GetDistance(it, coords) <= distance))
                    nodes_extract.TryAdd(node_id, coords);
            }

            var roads_extract = new HashMap<long, RoadInfo>();

            foreach ((long road_id, RoadInfo road_info) in sourceMap.Roads)
            {
                if (road_info.Nodes.Any(it => nodes_extract.ContainsKey(it)))
                {
                    roads_extract.TryAdd(road_id, road_info);
                }
            }

            foreach (var node_id in roads_extract.Values.SelectMany(it => it.Nodes))
            {
                nodes_extract.TryAdd(node_id, sourceMap.Nodes[node_id]);
            }

            var dangerous = new HashSet<long>();
            foreach (var node_id in nodes_extract.Keys)
                if (sourceMap.IsBikeFootRoadDangerousNearby(node_id))
                    dangerous.Add(node_id);

            var map_extract = WorldMapMemory.CreateOnlyRoads(logger, nodes_extract, roads_extract, new NodeRoadsAssocDictionary(nodes_extract, roads_extract));
            map_extract.SetDangerous(dangerous);
            return map_extract;
        }

        public static IEnumerable<RoadIndexLong> GetAdjacentRoads(this IWorldMap map, long nodeId)
        {
            foreach (var road_idx in map.GetRoads(nodeId))
            {
                if (map.TryGetSibling(road_idx, -1, out RoadIndexLong prev))
                    yield return prev;
                if (map.TryGetSibling(road_idx, +1, out RoadIndexLong next))
                    yield return next;
            }
        }

        public static bool TryGetSibling(this IWorldMap map, RoadIndexLong current, int direction, out RoadIndexLong sibling)
        {
            if (direction != 1 && direction != -1)
                throw new ArgumentOutOfRangeException($"{nameof(direction)} = {direction}");

            var target_index = current.IndexAlongRoad + direction;

            if (target_index >= 0 && target_index < map.Roads[current.RoadMapIndex].Nodes.Count)
            {
                sibling = new RoadIndexLong(current.RoadMapIndex, target_index);
                return true;
            }

            sibling = default;
            return false;
        }

        public static bool TryGetPrevious(this IWorldMap map, RoadIndexLong current, out RoadIndexLong previous)
        {
            return TryGetSibling(map, current, -1, out previous);
        }
        
        public static bool TryGetNext(this IWorldMap map, RoadIndexLong current, out RoadIndexLong next)
        {
            return TryGetSibling(map, current, +1, out next);
        }

        public static long GetNode(this IWorldMap map, in RoadIndexLong idx)
        {
            return map.Roads[idx.RoadMapIndex].Nodes[idx.IndexAlongRoad];
        }

        public static GeoZPoint GetPoint(this IWorldMap map, in RoadIndexLong idx)
        {
            return map.Nodes[map.GetNode(idx)];
        }

        public static bool IsCycleWay(this IWorldMap map, long roadId) => map.Roads[roadId].Kind == WayKind.Cycleway;

public static bool IsMotorRoad(this IWorldMap map, long roadId)
            => map.Roads[roadId].Kind != WayKind.Cycleway && map.Roads[roadId].Kind != WayKind.Footway && map.Roads[roadId].Kind != WayKind.Steps;

public static bool IsSignificantMotorRoad(this IWorldMap map, long roadId) => map.IsMotorRoad(roadId) && map.Roads[roadId].Kind <= WayKind.Unclassified;

        internal static int CycleWeight(this IWorldMap map, in RoadIndexLong idx) => map.IsCycleWay(idx.RoadMapIndex) ? 1 : 0;

        public static bool IsRoadContinuation(this IWorldMap map, long currentRoadId, long nextRoadId)
        {
            return currentRoadId == nextRoadId
                || (map.IsCycleWay(currentRoadId) && map.IsCycleWay(nextRoadId))
                || (map.Roads[currentRoadId].HasName && map.Roads[nextRoadId].HasName 
                                                     && map.Roads[currentRoadId].NameIdentifier == map.Roads[nextRoadId].NameIdentifier);
        }

        internal static bool IsRoadLooped(this IWorldMap map, long roadId)
        {
            return map.Roads[roadId].Nodes.Count != map.Roads[roadId].Nodes.Distinct().Count();
        }

        public static bool IsDirectionAllowed(this IWorldMap map, in RoadIndexLong from, in RoadIndexLong dest)
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

        public static Length GetRoadDistance(this IWorldMap map, IGeoCalculator calc, RoadIndexLong start, RoadIndexLong dest)
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

        public static IEnumerable<RoadIndexLong> GetRoadIndices(this IWorldMap map, RoadIndexLong start, RoadIndexLong dest)
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
