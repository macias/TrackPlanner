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
    }
}