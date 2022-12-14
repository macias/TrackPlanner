using TrackPlanner.Data.Stored;
using System.Linq;
using MathUnit;
using TrackPlanner.Shared;
using TrackPlanner.DataExchange;
using TrackPlanner.Mapping;
using TrackPlanner.PathFinder;
using TrackPlanner.Structures;
using TrackPlanner.Tests;

namespace TrackPlanner.TestRunner
{
    class Program
    {
        private static readonly Length extractionRange = Length.FromMeters(500);
        
        private const string baseDirectory = "../../../../../..";
        private static readonly Navigator navigator = new Navigator(baseDirectory);

        static void Main(string[] args)
        {
            //var data = new TurnTestData();
            //var test = new TurnTest(data);
            //test.BiskupiceSwitchFromCyclewayTest();
            //var test = new PackedMapTest();
            //test.AdditionsTest();
            //new CompactDictionaryTest().LongResizingTest();

         //   new Program().ExtractMiniMapFromFile(args, "gaski.kml");
         if (false)
            new Program().extractMiniMapFromPoints("cierpice-crossing_road.kml",
                GeoZPoint.FromDegreesMeters(52.983727, 18.485634, 0),
                GeoZPoint.FromDegreesMeters(52.987045, 18.49471, 0)
            );
            //new MiniWorldTurnTest().LipionkaTest();
            
            new CompactDictionaryTest().RemovalTest(new CompactDictionaryFill<long, string>());
        }

        public void ExtractMiniMapFromFile(string[] args, params string[] planFilenames)
        {
            var snap_limit = Length.FromMeters(5);
/*
            var config_builder = new ConfigurationBuilder()
                .AddJsonFile(EnvironmentConfiguration.Filename, optional: false)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();
            var env_config = new EnvironmentConfiguration();
            config_builder.GetSection(EnvironmentConfiguration.SectionName).Bind(env_config);
            //logger.Info($"{nameof(env_config)} {env_config}");
            env_config.Check();
*/
            var visual_prefs = new UserVisualPreferences();
            var app_path = System.IO.Path.Combine(baseDirectory, "app");
            using (Logger.Create(System.IO.Path.Combine(navigator.GetOutput(), "log.txt"), out ILogger logger))
            {
                var sys_config = new SystemConfiguration(){ CompactPreservesRoads = true};
                using (RouteManager.Create(logger, new Navigator( baseDirectory),  "kujawsko-pomorskie", 
                           sys_config, out var manager))
                {
                    foreach (var filename in planFilenames)
                    {
                        GeoZPoint[] raw_track_plan = TrackReader.LEGACY_Read(System.IO.Path.Combine(app_path, "tracks", filename)).ToArray();

                        var mini_map = manager.Map.ExtractMiniMap(logger, manager.Calculator, extractionRange,
                          sys_config.MemoryParams.GridCellSize,  navigator.GetDebug(),
                            raw_track_plan);
                        mini_map.SaveAsKml(visual_prefs, System.IO.Path.Combine(app_path, "mini-maps", filename));
                    }
                }
            }
        }

        private void extractMiniMapFromPoints(string filename, params GeoZPoint[] points)
        {
            var visual_prefs = new UserVisualPreferences();

            var app_path = System.IO.Path.Combine(baseDirectory, "app");

            using (Logger.Create(System.IO.Path.Combine(navigator.GetOutput(), "log.txt"), out ILogger logger))
            {
                var sys_config = new SystemConfiguration(){ CompactPreservesRoads = true};
                using (RouteManager.Create(logger, new Navigator( baseDirectory),  "kujawsko-pomorskie", 
                           sys_config, out var manager))
                {
                    {
                        var mini_map = manager.Map.ExtractMiniMap(logger, manager.Calculator, extractionRange,
                            sys_config.MemoryParams.GridCellSize,navigator.GetDebug(),
                            points);
                        mini_map.SaveAsKml(visual_prefs, System.IO.Path.Combine(app_path, "mini-maps", filename));
                    }
                }
            }
        }
    }
}


 
