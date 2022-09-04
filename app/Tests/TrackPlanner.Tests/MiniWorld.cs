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
using TrackPlanner.Mapping;
using TrackPlanner.Mapping.Data;
using TrackPlanner.PathFinder;
using TrackPlanner.Tests.Implementation;
using Xunit;

namespace TrackPlanner.Tests
{
    public abstract class MiniWorld
    {
        private const string baseDirectory = "../../../../../..";
        protected const int Precision = 15;

        protected const bool CheckReversal = true;

        private static readonly Navigator navigator = new Navigator(baseDirectory);

        protected static void SaveData(IEnumerable<GeoZPoint> plan, IEnumerable<TurnInfo> turns, string mapFilename)
        {
            var input = new TrackWriterInput() {Title = null};
            input.AddLine(plan, name: null, new KmlLineDecoration(new Color32(0, 0, 0, 255), 1));
            input.AddTurns(turns, PointIcon.CircleIcon);

            var kml = input.BuildDecoratedKml();

            using (var stream = new FileStream(Helper.GetUniqueFileName(navigator.GetOutput(), "test-" + System.IO.Path.GetFileName(mapFilename)),
                       FileMode.CreateNew, FileAccess.Write))
            {
                kml.Save(stream);
            }
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

        protected IDisposable ComputePlaces(string filename, out RouteManager manager, out IReadOnlyList<Placement> placements, 
            params GeoZPoint[] userPoints)
        {
            if (userPoints.Length < 2)
                throw new ArgumentOutOfRangeException();

            var logger = new NoLogger();
            var mini_map = loadMiniMap(logger, filename);
            var user_configuration = UserPlannerPreferencesHelper.CreateBikeOriented().SetCustomSpeeds();

            var result = RouteManager.Create(logger, new Navigator(baseDirectory), mini_map,
                new SystemConfiguration() {CompactPreservesRoads = true}, out manager);

            RequestPoint[] req_points = userPoints.Select(it => new RequestPoint(it.Convert(), false)).ToArray();
            for (int i = 1; i < req_points.Length - 1; ++i)
            {
                req_points[i] = req_points[i] with {AllowSmoothing = true};
            }

            List<LegRun>? plan;
            if (!manager.TryFindRawRoute(user_configuration, req_points, CancellationToken.None, out plan,out var problem))
                throw new Exception("Route not found");
            if (problem != null)
                throw new Exception(problem);

            placements = plan.SelectMany(leg => leg.Steps.Select(it => it.Place)).ToList();

            return result;
        }

        protected IReadOnlyList<Placement> ComputePlaces(string filename, params GeoZPoint[] userPoints)
        {
            using (ComputePlaces(filename, out _, out var placements, userPoints))
            {
                return placements;
            }
        }

        protected (IReadOnlyList<GeoZPoint> plan, IReadOnlyList<TurnInfo> turns) ComputeTurns(string filename, params GeoZPoint[] userPoints)
        {
            var logger = new NoLogger();

            using (ComputePlaces(filename, out var manager, out var plan_nodes, userPoints))
            {
                var turner = new NodeTrackTurner(logger, manager.Map, manager.DebugDirectory!);

                var turner_preferences = new UserTurnerPreferences();
                var regular = turner.ComputeTurnPoints(plan_nodes, turner_preferences);
                if (CheckReversal)
                {
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

                    return (plan_nodes.Select(it => it.GetPoint(manager.Map)).ToList(), regular);
                }
            }
        }
    }
}