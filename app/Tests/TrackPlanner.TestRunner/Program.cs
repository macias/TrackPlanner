using System;
using System.Configuration;
using System.Linq;
using MathUnit;
using Microsoft.Extensions.Configuration;
using TrackPlanner.Data;
using TrackPlanner.Settings;
using TrackPlanner.Shared;
using TrackPlanner.DataExchange;
using TrackPlanner.Mapping;
using TrackPlanner.PathFinder;
using TrackPlanner.Tests;

namespace TrackPlanner.TestRunner
{
    class Program
    {
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

            new Program().ExtractMiniMap(args,
                "torun_south_range.kml",
                "biskupice_switch_from_cycleway.kml",
                "torun_unislaw_dedicated_cycleway.kml",
                "torun_chelminska_smoothing_cycleway.kml",
                "torun_chelminska_cycleway_snap_with_turn.kml",
                "silno_cycleway_bump.kml",
                "kaszczorek_roundabout_cycleway_shortcut.kml",
                "kaszczorek_bridge_minor_pass.kml",
                "kaszczorek_bridge_minor_pass_exit.kml",
                "biskupice_switch_to_cycleway.kml",
                "rusinowo_easy_overrides_sharp.kml",
                "biskupice_turn_on_cycleway.kml",
                "dorposz_szlachecki_y_junction.kml",
                "pigza_switch_to_cycleway.kml",
                "torun_skarpa_ignoring_cycleway.kml",
                "radzyn_chelminski_crossed_loop.kml",
                "pigza_switch_from_cycleway_with_turn.kml",
                "pigza_turn_on_named_path.kml",
                "pigza_going_straight_into_path.kml",
                "suchatowka_turn_railway.kml",
                "debowo_straight_into_minor.kml",
                "gaski_y-turn_unclassified.kml",
                "nawra_almost_straight_Y_junction.kml",
                "chelmno-roundabout_Lturn.kml",
                "gaski.kml"
            );
            //new MiniWorldTurnTest().LipionkaTest();
        }

        public void ExtractMiniMap(string[] args, params string[] planFilenames)
        {
            var extraction_range = Length.FromMeters(500);
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
                using (RouteManager.Create(logger, new Navigator( baseDir),  "kujawsko-pomorskie", new SystemConfiguration(){ CompactPreservesRoads = true}, out var manager))
                {
                    foreach (var filename in planFilenames)
                    {
                        GeoZPoint[] raw_track_plan = TrackReader.LEGACY_Read(System.IO.Path.Combine(app_path, "tracks", filename)).ToArray();

                        var mini_map = manager.Map.ExtractMiniMap(logger, manager.Calculator, extraction_range, raw_track_plan);
                        mini_map.SaveAsKml(visual_prefs, System.IO.Path.Combine(app_path, "mini-maps", filename));
                    }
                }
            }
        }
    }
}


 
