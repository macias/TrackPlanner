using TrackPlanner.Data.Stored;
using MathUnit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using TrackPlanner.Data;
using TrackPlanner.LinqExtensions;
using TrackPlanner.Settings;
using TrackPlanner.Shared;
using TrackPlanner.Mapping;
using TrackPlanner.Mapping.Data;


namespace TrackPlanner.PathFinder
{
    public sealed class RouteManager
    {
        public static IDisposable Create  (ILogger? logger, Navigator navigator,  
            string mapSubdirectory, SystemConfiguration systemConfiguration, 
            out RouteManager manager)
        {
            manager = new RouteManager(logger, navigator, worldMap:null, mapSubdirectory, systemConfiguration, out IDisposable disp);
            return disp;
        }

        public static IDisposable Create  (ILogger? logger, Navigator navigator,  
            IWorldMap worldMap, SystemConfiguration systemConfiguration, 
            out RouteManager manager)
        {
            manager = new RouteManager(logger, navigator, worldMap, mapSubdirectory:null, 
                systemConfiguration, out IDisposable disp);
            return disp;
        }

        private readonly ILogger logger;
        public IWorldMap Map { get;  }
        private readonly Navigator navigator;
        public SystemConfiguration SysConfig { get; }
        public IGeoCalculator Calculator { get; set; }

        public string? DebugDirectory { get; }

        private RouteManager(ILogger? logger,Navigator navigator, 
            IWorldMap? worldMap,
            string? mapSubdirectory,
            SystemConfiguration systemConfiguration,
            out IDisposable disposable)
        {
            if (logger == null) 
                throw new ArgumentNullException(nameof(logger));
            this.logger = logger;

            this.navigator = navigator;
            logger.Info($"{this} with baseDirectory: {navigator.BaseDirectory}");

            this.SysConfig = systemConfiguration;

            this.Calculator = new ApproximateCalculator();

            this.DebugDirectory = navigator.GetDebug(this.SysConfig.EnableDebugDumping);
            
            if (worldMap != null)
            {
                this.Map = worldMap;
                disposable = CompositeDisposable.None;
            }
            else
            {
                var osm_reader = new OsmReader(logger, Calculator, this.SysConfig.MemoryParams,
                    highTrafficProximity: this.SysConfig.HighTrafficProximity, DebugDirectory);
                {
                    double start = Stopwatch.GetTimestamp();

                    var map_paths = System.IO.Directory.GetFiles(System.IO.Path.Combine(navigator.GetWorldMaps(), mapSubdirectory!), "*.osm.pbf");
                    if (map_paths.Length == 0)
                        throw new ArgumentException($"No maps found at {map_paths}");
                    else if (map_paths.Length > 1)
                        throw new NotSupportedException($"Currently only single map file is supported.");

                    disposable = osm_reader.ReadOsmMap(map_paths.Single(), onlyRoads: true, out var out_map);
                    this.Map = out_map;
                    disposable = CompositeDisposable.Stack(disposable,
                        () => { logger.Info($"STATS {nameof(this.Map)} {this.Map.GetStats()}"); });
                    Console.WriteLine($"Loading map in {(Stopwatch.GetTimestamp() - start) / Stopwatch.Frequency} s");
                }
            }

            //TrackReader.Write(Helper.GetUniqueFileName(outputDir, "special.kml"), null, new[] { map.Nodes[6635814192] });

            //DoTest();


//logger.Verbose("Building grid");
  //          this.grid = new RoadGridMemory(logger, map, new ApproximateCalculator(), this.sysConfig.GridCellSize, this.sysConfig.DebugDirectory, legacyGetNodeAllRoads: false);
    //        logger.Verbose("Grid built");

        }

        public bool TryFindFlattenRoute(UserRouterPreferences userConfig, IReadOnlyList<RequestPoint> userPoints, 
            CancellationToken token, [MaybeNullWhen(false)] out List<LegRun> route,out string? problem)
        {
            // the last point of given leg is repeated as the first point of the following leg
            var result = RouteFinder.TryFindPath(this.logger, this.navigator, this.Map, this.SysConfig, userConfig, 
                userPoints, token, out route,out problem);
            if (result)
            {
                var compactor = new RouteCompactor(this.logger, this.Map, userConfig, this.SysConfig.CompactPreservesRoads);

                compactor.FlattenRoundabouts(route!);
            }

            return result;
        }

        public bool TryFindCompactRoute(UserRouterPreferences userConfig, 
            IReadOnlyList<RequestPoint> userPoints, 
            CancellationToken token, [MaybeNullWhen(false)] out TrackPlan track)
        {
            if (!TryFindFlattenRoute(userConfig, userPoints, token, out var legs, out var problem))
            {
                track = default;
                return false;
            }

            track = CompactFlattenRoute(userConfig, legs);
            if (problem != null)
                track.ProblemMessage = problem;

            return true;
        }

        public TrackPlan CompactFlattenRoute(UserRouterPreferences userConfig, List<LegRun> legs)
        {
            var compactor = new RouteCompactor(this.logger, this.Map, userConfig, this.SysConfig.CompactPreservesRoads);

            return compactor.Compact(legs);
        }

        public bool TryFindRoute(UserRouterPreferences userConfig,IReadOnlyList<NodePoint>  userPlaces,bool allowSmoothing,
            CancellationToken token, [MaybeNullWhen(false)] out TrackPlan track)
        {
            if (!RouteFinder.TryFindPath(logger,this.navigator, this.Map, this.SysConfig,userConfig, userPlaces,allowSmoothing:allowSmoothing, token, out var legs))
            {
                track = default;
                return false;
            }

            var compactor = new RouteCompactor(logger, this.Map, userConfig, this.SysConfig.CompactPreservesRoads);
            compactor.FlattenRoundabouts(legs);
            track = compactor.Compact(legs);
            return true;
        }

        public bool TryFindRoute(UserRouterPreferences userConfig,IReadOnlyList<long>  mapNodes,
            in PathConstraints constraints,
            bool allowSmoothing,
            CancellationToken token, [MaybeNullWhen(false)] out TrackPlan track)
        {
            bool DUMMY_ALLOW_SMOOTHING = false;
            if (!RouteFinder.TryFindPath(logger, this.navigator, this.Map,  new Shortcuts(), this.SysConfig,userConfig, mapNodes,constraints,allowSmoothing, token, out var legs))
            {
                track = default;
                return false;
            }

            var compactor = new RouteCompactor(logger, this.Map, userConfig, this.SysConfig.CompactPreservesRoads);
            compactor.FlattenRoundabouts(legs);
            track = compactor.Compact(legs);
            return true;
        }
        
        /*private void DoTest()
        {
            //  2.509714238s dla dictionary
            var start = Stopwatch.GetTimestamp();
            GeoZPoint pt;
            foreach (var node_id in this.map.Nodes.Keys)
            {
                pt = this.map.Nodes[node_id];
            }

            throw new Exception($"Iterated in {(Stopwatch.GetTimestamp()-start-0.0)/Stopwatch.Frequency}s");
        }*/
        
    }
}