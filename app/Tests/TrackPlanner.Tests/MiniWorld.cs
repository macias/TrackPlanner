using TrackPlanner.Data.Stored;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using TrackPlanner.Data;
using TrackPlanner.Settings;
using SharpKml.Base;
using TrackPlanner.Shared;
using TrackPlanner.Turner;
using TrackPlanner.DataExchange;
using TrackPlanner.LinqExtensions;
using TrackPlanner.Mapping;
using TrackPlanner.Mapping.Data;
using TrackPlanner.Mapping.Disk;
using TrackPlanner.PathFinder;
using TrackPlanner.Tests.Implementation;
using Xunit;

namespace TrackPlanner.Tests
{
    public abstract class MiniWorld
    {
        private const string baseDirectory = "../../../../../..";
        protected const int Precision = 15;

        private static readonly Navigator navigator = new Navigator(baseDirectory);

        public static IEnumerable<object[]> TestParams => Enum.GetValues<MapMode>().Select(it => new object[]{it});

        protected static void SaveData(IEnumerable<Placement> plan, string mapFilename)
        {
            var input = new TrackWriterInput() {Title = null};
            int index = -1;
            foreach (var place in plan)
            {
                ++index;
                input.AddPoint(place.Point, $"[{index}] {place}", comment: null, PointIcon.DotIcon);
            }

            saveToKml(input,mapFilename);
        }

        private static void saveToKml(TrackWriterInput input,string mapFilename)
        {
            var kml = input.BuildDecoratedKml();

            using (var stream = new FileStream(Helper.GetUniqueFileName(navigator.GetOutput(), "test-" + System.IO.Path.GetFileName(mapFilename)),
                       FileMode.CreateNew, FileAccess.Write))
            {
                kml.Save(stream);
            }
        }

        protected static void SaveData(IEnumerable<Placement> plan, IEnumerable<TurnInfo> turns, string mapFilename)
        {
            var input = new TrackWriterInput() {Title = null};
            input.AddLine(plan.Select(it => it.Point).ToArray(), name: null, new KmlLineDecoration(new Color32(0, 0, 0, 255), 1));
            input.AddTurns(turns, PointIcon.CircleIcon);

            saveToKml(input,mapFilename);
        }

        private static WorldMapMemory loadMiniMap(ILogger logger, string filename)
        {
            var kml_track = TrackReader.ReadUnstyled(System.IO.Path.Combine(navigator.GetMiniMaps(), filename));

            var nodes = new HashMap<long, GeoZPoint>();
            var rev_nodes = new HashMap<GeoZPoint, long>();
            var roads = new HashMap<long, RoadInfo>();
            var dangerous = new HashSet<long>();

            foreach (var waypoint in kml_track.Waypoints)
            {
                var id = long.Parse(waypoint.Name!);
                nodes.Add(id, waypoint.Point);
                rev_nodes.Add(waypoint.Point, id);

                if (waypoint.Description == TrackPlanner.Mapping.WorldMapExtension.KmlDangerousTag)
                    dangerous.Add(id);
            }

            foreach (var line in kml_track.Lines)
            {
                var info = RoadInfo.Parse(long.Parse(line.Name!), line.Points.Select(it => rev_nodes[it]).ToList(), line.Description!);
                roads.Add(info.Identifier, info);
            }

            var world_map = WorldMapMemory.CreateOnlyRoads(logger, nodes, roads, new NodeRoadsAssocDictionary(nodes, roads));
            world_map.SetDangerous(dangerous);
            return world_map;
        }

        private IDisposable computePlaces(MapMode mapMode, string filename, out RouteManager manager, out IReadOnlyList<Placement> placements,
            params GeoZPoint[] userPoints)
        {
            if (userPoints.Length < 2)
                throw new ArgumentOutOfRangeException();

            var sys_config = new SystemConfiguration() {CompactPreservesRoads = true, 
                MemoryParams = new MemorySettings() { MapMode = mapMode}};
            var logger = new NoLogger();
            var result = CompositeDisposable.None;

            IWorldMap mini_map;
            {
                var mem_map = loadMiniMap(logger, filename);
                mini_map = mem_map;
                if (mapMode== MapMode.HybridDisk)
                {
                    result = CompositeDisposable.Stack(result, createDiskMap(logger, mem_map, filename,
                        sys_config.MemoryParams, out var disk_map));
                    mini_map = disk_map;
                }
                else if (mapMode != MapMode.MemoryOnly)
                    throw new NotImplementedException($"{mapMode}");
            }

            var user_configuration = UserRouterPreferencesHelper.CreateBikeOriented().SetCustomSpeeds();

            result = CompositeDisposable.Stack(result, RouteManager.Create(logger, new Navigator(baseDirectory),
                 mini_map,
                sys_config, out manager));
            
            RequestPoint[] req_points = userPoints.Select(it => new RequestPoint(it.Convert(), false)).ToArray();
            for (int i = 1; i < req_points.Length - 1; ++i)
            {
                req_points[i] = req_points[i] with {AllowSmoothing = true};
            }

            List<LegRun>? plan;
            if (!manager.TryFindFlattenRoute(user_configuration, req_points, 
                    CancellationToken.None, out plan,out var problem))
                throw new Exception("Route not found");
            if (problem != null)
                throw new Exception(problem);

            placements = plan.SelectMany(leg => leg.Steps.Select(it => it.Place)).ToList();

            return result;
        }

        private IDisposable createDiskMap(ILogger logger, WorldMapMemory memMap, string fileName,
            MemorySettings memorySettings,  out WorldMapDisk diskMap)
        {
            Stream stream = new MemoryStream();
            var result = new CompositeDisposable(stream);
            memMap.Write(timestamp:0,stream,memMap.CreateRoadGrid(memorySettings.GridCellSize,navigator.GetDebug()));
            stream.Position = 0;
            result = result.Stack(WorldMapDisk.Read(logger, new[] {(stream, fileName)}.ToArray(), memorySettings, 
                out diskMap));
            return result;
        }

        protected IReadOnlyList<Placement> ComputePlaces(MapMode mapMode,string filename, params GeoZPoint[] userPoints)
        {
            using (computePlaces(mapMode, filename, out _, out var placements, userPoints))
            {
                return placements;
            }
        }

        protected (IReadOnlyList<Placement> plan, IReadOnlyList<TurnInfo> turns) ComputeTurns(MapMode mapMode,string filename, params GeoZPoint[] userPoints)
        {
            var logger = new NoLogger();

            using (computePlaces(mapMode, filename, out var manager, out var plan_nodes, userPoints))
            {
                var turner = new NodeTrackTurner(logger, manager.Map, manager.DebugDirectory!);

                var turner_preferences = new UserTurnerPreferences();
                var regular = turner.ComputeTurnPoints(plan_nodes, turner_preferences);
                
                { // checking reversal as well
                    IReadOnlyList<TurnInfo> reversed = turner.ComputeTurnPoints(plan_nodes.Reverse().ToList(), turner_preferences)
                        .AsEnumerable()
                        .Reverse()
                        // reversing internal data
                        // quality ignores  track index and we couldn't simply mirror it because internally some of the points can be initially removed
                        .Select(it => new TurnInfo(it.Entity, it.EntityId, it.Point, trackIndex: -1, it.RoundaboutGroup, it.Backward, it.Forward, reason: it.Reason))
                        .ToList();

                    Assert.Equal(expected: regular.Count, actual: reversed.Count);
                    foreach (var (reg, rev) in regular.Zip(reversed, (reg, rev) => (reg, rev)))
                    {
                        Assert.Equal(reg.Point.Latitude.Degrees, rev.Point.Latitude.Degrees, Precision);
                        Assert.Equal(reg.Point.Longitude.Degrees, rev.Point.Longitude.Degrees, Precision);
                        Assert.Equal(reg.RoundaboutGroup, rev.RoundaboutGroup);
                        Assert.Equal(reg.Forward, rev.Forward);
                        Assert.Equal(reg.Backward, rev.Backward);
                    }

                    return (plan_nodes, regular);
                }
            }
        }
    }
}