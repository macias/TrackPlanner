using TrackPlanner.Data.Stored;
using System.Linq;
using MathUnit;
using TrackPlanner.Shared;
using TrackPlanner.DataExchange;
using TrackPlanner.Mapping;
using TrackPlanner.PathFinder;

namespace TrackPlanner.TestRunner
{
    class Program
    {
        private static readonly Length extractionRange = Length.FromMeters(500);
        
        private const string baseDir = "../../../../../..";
        private static readonly string outputDir = $"{baseDir}/output";

        static void Main(string[] args)
        {
            //var data = new TurnTestData();
            //var test = new TurnTest(data);
            //test.BiskupiceSwitchFromCyclewayTest();
            //var test = new PackedMapTest();
            //test.AdditionsTest();
            //new CompactDictionaryTest().LongResizingTest();

         //   new Program().ExtractMiniMapFromFile(args, "gaski.kml");
            new Program().extractMiniMapFromPoints("zakrzewko.kml",
                GeoZPoint.FromDegreesMeters(53.097324, 18.640022, 0),
                GeoZPoint.FromDegreesMeters(53.102116, 18.646202, 0),
                GeoZPoint.FromDegreesMeters(53.110565, 18.661394, 0)
            );
            //new MiniWorldTurnTest().LipionkaTest();
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
            var app_path = System.IO.Path.Combine(baseDir, "app");
            using (Logger.Create(System.IO.Path.Combine(outputDir, "log.txt"), out ILogger logger))
            {
                using (RouteManager.Create(logger, new Navigator( baseDir),  "kujawsko-pomorskie", 
                           new SystemConfiguration(){ CompactPreservesRoads = true}, out var manager))
                {
                    foreach (var filename in planFilenames)
                    {
                        GeoZPoint[] raw_track_plan = TrackReader.LEGACY_Read(System.IO.Path.Combine(app_path, "tracks", filename)).ToArray();

                        var mini_map = manager.Map.ExtractMiniMap(logger, manager.Calculator, extractionRange, raw_track_plan);
                        mini_map.SaveAsKml(visual_prefs, System.IO.Path.Combine(app_path, "mini-maps", filename));
                    }
                }
            }
        }

        private void extractMiniMapFromPoints(string filename, params GeoZPoint[] points)
        {
            var visual_prefs = new UserVisualPreferences();

            var app_path = System.IO.Path.Combine(baseDir, "app");

            using (Logger.Create(System.IO.Path.Combine(outputDir, "log.txt"), out ILogger logger))
            {
                using (RouteManager.Create(logger, new Navigator( baseDir),  "kujawsko-pomorskie", 
                           new SystemConfiguration(){ CompactPreservesRoads = true}, out var manager))
                {
                    {
                        var mini_map = manager.Map.ExtractMiniMap(logger, manager.Calculator, extractionRange, points);
                        mini_map.SaveAsKml(visual_prefs, System.IO.Path.Combine(app_path, "mini-maps", filename));
                    }
                }
            }
        }
    }
}


 
