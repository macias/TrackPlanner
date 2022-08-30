using Geo;
using MathUnit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Configuration;
using OsmSharp.IO.PBF;
using TrackPlanner.Data;
using TrackPlanner.Settings;
using SharpKml.Dom;
using TrackPlanner.Shared;
using TrackPlanner.Turner;
using TrackPlanner.DataExchange;
using TrackPlanner.LinqExtensions;
using TrackPlanner.Mapping;
using TrackPlanner.Mapping.Data;
using TrackPlanner.PathFinder;
using TimeSpan = System.TimeSpan;

namespace TrackPlanner.Runner
{
    
    /*
    https://scihub.copernicus.eu/dhus/#/home



    super sprawa, po zarejestrowaniu się można przeglądać dane z sentineli dla wybranych obszarów

    Request Done: ( footprint:"Intersects(POLYGON((21.906846278110624 49.97692768443906,21.92511191789837 49.97692768443906,21.92511191789837 49.9830247493573,21.906846278110624 49.9830247493573,21.906846278110624 49.97692768443906)))" ) AND ( beginPosition:[2021-01-01T00:00:00.000Z TO 2021-02-15T23:59:59.999Z] AND endPosition:[2021-01-01T00:00:00.000Z TO 2021-02-15T23:59:59.999Z] ) 

    */
    // todo: byc moze uzupelniac trakc o node'y z zadanej drogi
    // todo: wczytywac mapy, i tworzyc (JSON) slownik ich parametrow, tak aby pozniej doczytywac mape on-demand w zaleznosci od trasy


    // testowac pigze, dlaczego na zakrecie nie mamy takze mapowania na crossing
    // doprowadzic do tego, zeby punkty ktore sie nakladaly z crossing, zeby je usuwac

    class Program
    {
        private const string baseDir = "../../../../..";
        private static readonly string outputDir = $"{baseDir}/output";

        static void Main(string[] args)
        {
            using (Logger.Create(System.IO.Path.Combine(outputDir, "log.txt"), out ILogger logger))
            {
                //logger.Info($"{nameof(NodeRoadsDictionary.SliceIndex)}: {System.Runtime.InteropServices.Marshal.SizeOf(typeof(NodeRoadsDictionary.SliceIndex))}");
                logger.Info($"{nameof(RoadIndexLong)}: {System.Runtime.InteropServices.Marshal.SizeOf(typeof(RoadIndexLong))}");
                logger.Info($"{nameof(GeoZPoint)}: {System.Runtime.InteropServices.Marshal.SizeOf(typeof(GeoZPoint))}");
                //logger.Info($"{nameof(RoadInfo)}: {System.Runtime.InteropServices.Marshal.SizeOf(typeof(RoadInfo))}");
                //RunTurner(logger);
                RunFinder(logger);
                //RunCalc();
                //DictionaryEval();
                ExtracMapData(logger);
                
            }

            Console.WriteLine("Done!");
            //Console.ReadLine();
        }

        private static void ExtracMapData(ILogger logger)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("clientsettings.json", optional: false);

            IConfiguration config = builder.Build();

            var env_config = new EnvironmentConfiguration();
            config.GetSection(EnvironmentConfiguration.SectionName).Bind(env_config);
            env_config.Check();

            var baseDirectory = System.IO.Path.GetFullPath(baseDir);

            var navigator = new Navigator(baseDirectory);

            var sysConfig = new SystemConfiguration() { EnableDebugDumping = true };

            var extractor = new OsmExtractor(logger, new ApproximateCalculator(), navigator.GetDebug());
                var osm_files = System.IO.Directory.GetFiles(System.IO.Path.Combine( baseDirectory, "maps/"), "*.osm.pbf");
                foreach (var file in osm_files)
                {
                    logger.Info($"Reading {file}");
                    var hist_objects = extractor.ReadHistoricObjects(file);
                    // 1.0 -- castles only
                    // 2.0 -- added historic objects
                    // 2.1 -- added new historic objects
                    var castles_fileName = Helper.GetUniqueFileName(outputDir, "2.1-"+System.IO.Path.GetFileName((file).Replace(".osm.pbf","")+"-historic.kml"));
                    using (FileStream stream = new FileStream(castles_fileName, FileMode.CreateNew))
                    {
                        var input = new TrackWriterInput();
                        foreach (var hist in hist_objects)
                            input.AddPoint(hist.Location.Convert(), hist.Name,hist.Url, hist.Ruins ? PointIcon.CircleIcon : PointIcon.StarIcon);
                        var kml = input.BuildDecoratedKml();
                        kml.Save(stream);
                    }
                    logger.Info($"Data saved to {castles_fileName}");
                }
                
                logger.Info($"Unused historic values: {(String.Join(Environment.NewLine,extractor.UnusedHistoric.OrderBy(x => x)))}");
            
        }

        private static void DictionaryEval()
        {
            //Console.WriteLine(new Dictionary<int,int>(capacity:2_014_641).GetCapacity()); // 2_411_033

            string path = System.IO.Path.Combine(baseDir, "maps", "dolnoslaskie-2022-01-10.xtr");
            var res_dict = new Dictionary<long, long>();
            Dictionary<long, long> dict;
            SeededDictionary<long, long> seed_dict;
            CompactDictionaryFirst<long, long> compact;
            CompactDictionary<long, long> compact0;
            var res_compact = new CompactDictionaryFirst<long, long>();
            var res_compact0 = new CompactDictionary<long, long>();

            using (var mem = new MemoryStream(System.IO.File.ReadAllBytes(path)))
            {
                using (var reader = new BinaryReader(mem))
                {
                    reader.ReadInt64(); // timestamp
                    reader.ReadInt32(); // cell size

                    Angle max_angle(Angle? a, Angle b) => a?.Max(b) ?? b;
                    Angle min_angle(Angle? a, Angle b) => a?.Min(b) ?? b;

                    var north_most = GeoZPoint.ReadFloatAngle(reader);
                    var east_most = GeoZPoint.ReadFloatAngle(reader);
                    var south_most = GeoZPoint.ReadFloatAngle(reader);
                    var west_most = GeoZPoint.ReadFloatAngle(reader);

                    var nodes_count = reader.ReadInt32();
                    var roads_count = reader.ReadInt32();
                    var cells_count = reader.ReadInt32();

                    seed_dict = new SeededDictionary<long, long>(EqualityComparer<long>.Default, nodes_count);
                    dict = new Dictionary<long, long>(capacity: nodes_count);
                    compact = new CompactDictionaryFirst<long, long>(nodes_count);
                    compact0 = new CompactDictionary<long, long>(nodes_count);
                    var roads_offset = reader.ReadInt64();
                    var grid_offset = reader.ReadInt64();

                    var pos = reader.BaseStream.Position;

                    var start = Stopwatch.GetTimestamp();
                    for (int i = 0; i < nodes_count; ++i)
                    {
                        var node_id = reader.ReadInt64();
                        var off = reader.ReadInt64();

                        res_dict.Add(node_id, off);
                    }

                    Console.WriteLine($"RES Dict filled in {(Stopwatch.GetTimestamp() - start - 0.0) / Stopwatch.Frequency}s");

                    reader.BaseStream.Seek(pos, SeekOrigin.Begin);
                    start = Stopwatch.GetTimestamp();
                    for (int i = 0; i < nodes_count; ++i)
                    {
                        var node_id = reader.ReadInt64();
                        var off = reader.ReadInt64();

                        dict.Add(node_id, off);
                    }

                    Console.WriteLine($"Dict filled in {(Stopwatch.GetTimestamp() - start - 0.0) / Stopwatch.Frequency}s");

                    reader.BaseStream.Seek(pos, SeekOrigin.Begin);
                    start = Stopwatch.GetTimestamp();
                    for (int i = 0; i < nodes_count; ++i)
                    {
                        var node_id = reader.ReadInt64();
                        var off = reader.ReadInt64();

                        compact.Add(node_id, off);
                    }

                    Console.WriteLine($"Compact filled in {(Stopwatch.GetTimestamp() - start - 0.0) / Stopwatch.Frequency}s");

                    reader.BaseStream.Seek(pos, SeekOrigin.Begin);
                    start = Stopwatch.GetTimestamp();
                    for (int i = 0; i < nodes_count; ++i)
                    {
                        var node_id = reader.ReadInt64();
                        var off = reader.ReadInt64();

                        compact0.Add(node_id, off);
                    }

                    Console.WriteLine($"Compact0 filled in {(Stopwatch.GetTimestamp() - start - 0.0) / Stopwatch.Frequency}s");

                    
                    
                    
                    
                    
                    
                    reader.BaseStream.Seek(pos, SeekOrigin.Begin);
                    start = Stopwatch.GetTimestamp();
                    for (int i = 0; i < nodes_count; ++i)
                    {
                        var node_id = reader.ReadInt64();
                        var off = reader.ReadInt64();

                        res_compact.Add(node_id, off);
                    }

                    Console.WriteLine($"RES Compact filled in {(Stopwatch.GetTimestamp() - start - 0.0) / Stopwatch.Frequency}s");

                    reader.BaseStream.Seek(pos, SeekOrigin.Begin);
                    start = Stopwatch.GetTimestamp();
                    for (int i = 0; i < nodes_count; ++i)
                    {
                        var node_id = reader.ReadInt64();
                        var off = reader.ReadInt64();

                        res_compact0.Add(node_id, off);
                    }

                    Console.WriteLine($"RES Compact0 filled in {(Stopwatch.GetTimestamp() - start - 0.0) / Stopwatch.Frequency}s");
                    
                    
                    
                    
                    
                    
                    reader.BaseStream.Seek(pos, SeekOrigin.Begin);
                    start = Stopwatch.GetTimestamp();

                    for (int i = 0; i < nodes_count; ++i)
                    {
                        var node_id = reader.ReadInt64();
                        var off = reader.ReadInt64();

                        seed_dict.AddSeed(node_id, off);
                    }

                    reader.BaseStream.Seek(pos, SeekOrigin.Begin);

                    for (int i = 0; i < nodes_count; ++i)
                    {
                        var node_id = reader.ReadInt64();
                        var off = reader.ReadInt64();

                        seed_dict.TryAdd(node_id, off);
                    }

                    Console.WriteLine($"Seed filled in {(Stopwatch.GetTimestamp() - start - 0.0) / Stopwatch.Frequency}s");

                }
            }

            Console.WriteLine("Map loaded");

            var copied = res_dict.ToList();

            {
                var start = Stopwatch.GetTimestamp();
                for (int i = copied.Count - 1; i >= 0; --i)
                {
                    if (!res_dict.TryGetValue(copied[i].Key, out var existing))
                        throw new ArgumentException();
                    if (copied[i].Value != existing)
                        throw new ArgumentException();
                }

                Console.WriteLine($"RES Dict queried in {(Stopwatch.GetTimestamp() - start - 0.0) / Stopwatch.Frequency}s");
            }
            {
                var start = Stopwatch.GetTimestamp();
                for (int i = copied.Count - 1; i >= 0; --i)
                {
                    if (!dict.TryGetValue(copied[i].Key, out var existing))
                        throw new ArgumentException();
                    if (copied[i].Value != existing)
                        throw new ArgumentException();
                }

                Console.WriteLine($"Dict queried in {(Stopwatch.GetTimestamp() - start - 0.0) / Stopwatch.Frequency}s");
            }
            {
                var start = Stopwatch.GetTimestamp();
                for (int i = copied.Count - 1; i >= 0; --i)
                {
                    if (!seed_dict.TryGetValue(copied[i].Key, out var existing))
                        throw new ArgumentException();
                    if (copied[i].Value != existing)
                        throw new ArgumentException();
                }

                Console.WriteLine($"Seed queried in {(Stopwatch.GetTimestamp() - start - 0.0) / Stopwatch.Frequency}s");
            }
            {
                var start = Stopwatch.GetTimestamp();
                for (int i = copied.Count - 1; i >= 0; --i)
                {
                    if (!compact.TryGetValue(copied[i].Key, out var existing))
                        throw new ArgumentException();
                    if (copied[i].Value != existing)
                        throw new ArgumentException();
                }

                Console.WriteLine($"Compact queried in {(Stopwatch.GetTimestamp() - start - 0.0) / Stopwatch.Frequency}s");
            }
            {
                var start = Stopwatch.GetTimestamp();
                for (int i = copied.Count - 1; i >= 0; --i)
                {
                    if (!compact0.TryGetValue(copied[i].Key, out var existing))
                        throw new ArgumentException();
                    if (copied[i].Value != existing)
                        throw new ArgumentException();
                }

                Console.WriteLine($"Compact0 queried in {(Stopwatch.GetTimestamp() - start - 0.0) / Stopwatch.Frequency}s");
            }
            {
                var start = Stopwatch.GetTimestamp();
                for (int i = copied.Count - 1; i >= 0; --i)
                {
                    if (!res_compact.TryGetValue(copied[i].Key, out var existing))
                        throw new ArgumentException("RES Compact failed "+res_compact.Keys.Contains(copied[i].Key).ToString());
                    if (copied[i].Value != existing)
                        throw new ArgumentException();
                }

                Console.WriteLine($"RES Compact queried in {(Stopwatch.GetTimestamp() - start - 0.0) / Stopwatch.Frequency}s");
            }
            {
                var start = Stopwatch.GetTimestamp();
                for (int i = copied.Count - 1; i >= 0; --i)
                {
                    if (!res_compact0.TryGetValue(copied[i].Key, out var existing))
                        throw new ArgumentException();
                    if (copied[i].Value != existing)
                        throw new ArgumentException();
                }

                Console.WriteLine($"RES Compact0 queried in {(Stopwatch.GetTimestamp() - start - 0.0) / Stopwatch.Frequency}s");
            }
        }

        private static void RunCalc()
        {
            Geo.GeoCalculator.GetArcSegmentIntersection(GeoPoint.FromDegrees(52.8970606, 18.657783399999996), GeoPoint.FromDegrees(53.02517820000001, 18.657783399999996),
                GeoPoint.FromDegrees(52.97015, 18.65988),
                GeoPoint.FromDegrees(52.97147, 18.655),
                       out GeoPoint? cx1, out GeoPoint? cx2);
            Console.WriteLine("Crosspoints");
            Console.WriteLine(cx1);
            Console.WriteLine(cx2);
        }

        private static void RunFinder(ILogger logger)
        {
            /*
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(EnvironmentConfiguration.Filename, optional: false);

            IConfiguration config = builder.Build();

            var env_config = new EnvironmentConfiguration();
            config.GetSection(EnvironmentConfiguration.SectionName).Bind(env_config);
            env_config.Check();*/
            
            // going around highway
            //GeoZPoint[] user_points = new[] { GeoZPoint.FromDegreesMeters(53.07388, 18.75152, null), GeoZPoint.FromDegreesMeters(53.08105, 18.75141, null), };
            // going through range
            GeoZPoint[]? user_points = null;
            NodePoint[]? user_places = null;
            var user_configuration = UserPlannerPreferencesHelper.CreateBikeOriented().SetCustomSpeeds();

            if (false)
            {
                GeoZPoint south_legal_point = GeoZPoint.FromDegreesMeters(52.90048, 18.57341, null);
                GeoZPoint north_nozone_point = GeoZPoint.FromDegreesMeters(52.98162, 18.60957, null);

                GeoZPoint south_deep_nozone_point = GeoZPoint.FromDegreesMeters(52.9265, 18.58358, null);
                GeoZPoint north_deep_nozone_point = GeoZPoint.FromDegreesMeters(52.96128, 18.59625, null);
                //user_points = new[] { south_legal_point, north_nozone_point, };
                user_points = new[] {
                    north_nozone_point,
                    south_legal_point,
                };
            }

            {

                GeoZPoint torun_bridge = GeoZPoint.FromDegreesMeters(53.00061, 18.60424, null); // most w toruniu
                GeoZPoint inowroclaw_tupadly = GeoZPoint.FromDegreesMeters(52.75295, 18.25615, null);

                if (false)
                {
                    // wersja memory (wszystko jest wrzucane w oszczedny pamieciowo sposob do pamieci, zero operacji na dysku podczas liczenia) Time route: 3.008533185 s, 2.607847712 s


                    // top -o %MEM
                    // dla tej trasy szczytowa pamiec to ok. 9GB 
                    // ale po wczytaniu map, zrobieniu grida spada do 8GB
                    // podczas liczenia trasy w ogole nie widac wzrostu pamieci
                    // glowny czynnik zajetosci RAM, to same mapy
                    
                    // przy korzystaniu z double-passa i same funkcji odciecia zbyt dalekich punktow, liczba analizowanych
                    // punktow ZWIEKSZYLA SIE, z 84095 do 84630 (nie rozumiem tego zupelnie); obliczona trasa
                    // na szczescie jest identyczna (to jest troche bez sensu, bo rejected nodes = 0, wiec z czego
                    // wynika wieksza liczba na wejsciu?)
                    user_points = new[] {
                        torun_bridge,
                        inowroclaw_tupadly,
                    };
                    // exact distance to target: 48_304 updates
                    // approx distance to target: 70_922 updates
                }

                if (false)
                {
                    user_points = new[] {
                        torun_bridge,
                        GeoZPoint.FromDegreesMeters(52.935, 18.5158,null), // in the middle of high-traffic road
                        inowroclaw_tupadly,
                    };
                }
            }

            if (false)
            {
                user_points = new[] {
                    // sprawdzenie wagi, zeby program odbijal z chodnika przy szosie
                    //GeoZPoint.FromDegreesMeters(52.91243, 18.48199, null), // nieco wczesniej przed Suchatowka                   
                    GeoZPoint.FromDegreesMeters(52.91158, 18.47988, null), // przed Suchatowka
                    GeoZPoint.FromDegreesMeters(52.89978, 18.47233, null), // za Suchatowka
                };
            }

            if (false)
            {
                user_points = new[] {
                    // Cierpice. Mapa zawiera blad, bo sciezka ktora wybiera nie przecina sie niby z torami kolejowymi
                    GeoZPoint.FromDegreesMeters(    52.98716, 18.49451, null), 
                    GeoZPoint.FromDegreesMeters(    52.98662, 18.46635, null), 
                };
            }

            if (false)
            {
                // test dlugiej trasy, Torun-Osie
                user_points = new[] {
                    GeoZPoint.FromDegreesMeters(        53.024, 18.60917, null), // Torun 
                    GeoZPoint.FromDegreesMeters(        53.61702, 18.27056, null),  // Osie
                };
            }

//            if (false)
            {
                // something really short but with 3 points to check out smoothing middle points
                user_points = new[] {
                    GeoZPoint.FromDegreesMeters(            53.38503, 18.93153, null),  
                    GeoZPoint.FromDegreesMeters(            53.38563, 18.93642, null),  
                    GeoZPoint.FromDegreesMeters(            53.38943, 18.93627, null), 
                };
            }

            if (false)
            {
                // m n#9223673711@52.40488815307617°, 19.027236938476562° to 53.59299850463868°, 17.86724090576172°

                user_places = new[] {
                    NodePoint.CreateNode(9223673711) ,  
                    NodePoint.CreatePoint(  GeoZPoint.FromDegreesMeters(            53.59299850463868, 17.86724090576172, null)),  
                };
                user_configuration = new UserPlannerPreferences() {HACK_ExactToTarget = false}.SetUniformSpeeds();
            }
            if (false)
            {
                // testing for computation speed
                // mapy.cz -- 4.92 seconds
                // TR -- 19.81293994 s (z cala mapa polski 25s)
                // NUC z cala Polska -- 9 s
                // NUC z cala Polska bidir -- 7.1 s
                // NUC z cala Polska bidir singlepass -- 6.9 s (krotko mowiac, pozytku z fast-pass nie ma)
                user_points = new[] {
                    GeoZPoint.FromDegreesMeters(            52.4049, 19.02756, null), // Chodecz 
                    GeoZPoint.FromDegreesMeters(            53.593, 17.86724, null),  // Tuchola
                };
            }

            if (false)
            {
                // testing if Poland-long route can be computed
                user_points = new[] {
                    GeoZPoint.FromDegreesMeters(                49.05505, 22.70635, null), // Bieszczady  
                    GeoZPoint.FromDegreesMeters(                53.91467, 14.23544, null),  // Swinoujscie 
                };
            }

            if (false)
            {
                // just testing node routing
                user_places = new[] {
                    // https://www.openstreetmap.org/node/1545695728
                    NodePoint.CreateNode( 1545695728L),
                    // https://www.openstreetmap.org/node/3661740250
                    NodePoint.CreateNode(3661740250L),
                };
            }

            if (false)
            {
                user_points = new[] {
                    // prostowanie ronda 
                    GeoZPoint.FromDegreesMeters(53.27968, 18.93167, null), // rondo w Wabrzeznie 
                    GeoZPoint.FromDegreesMeters(53.27856, 18.93023, null), 
                };
            }

            //track = track.Reverse().ToArray();

            using (RouteManager.Create(logger,new Navigator(baseDir), "kujawsko-pomorskie", 
                       new SystemConfiguration(){ EnableDebugDumping = true, CompactPreservesRoads = true}, out var manager))
            {

                if (false)
                {
                    double start = Stopwatch.GetTimestamp();

                    if (!manager.TryFindRoute(UserPlannerPreferencesHelper.CreateBikeOriented().SetUniformSpeeds(), user_points.Select(it => new RequestPoint(it.Convert(),false)).ToArray(), 
                            CancellationToken.None, out var computed_track))
                        throw new Exception();

                    Console.WriteLine($"Distance route in {(Stopwatch.GetTimestamp() - start) / Stopwatch.Frequency} s");

                    using (FileStream stream = new FileStream(Helper.GetUniqueFileName(outputDir, "distance-track-with-turns.kml"), FileMode.CreateNew))
                    {
                        TrackWriter.SaveAsKml(new UserVisualPreferences(), stream, "something meaningful",computed_track);
                    }
                }

                //if (false)
                {
                    double start = Stopwatch.GetTimestamp();

                
                    TrackPlan? computed_track;
                    if (user_points != null)
                    {
                        if (!manager.TryFindRoute(user_configuration, user_points.Select(it => new RequestPoint(it.Convert(),false)).ToArray(), 
                                CancellationToken.None, out computed_track))
                            throw new Exception("Route not found");
                    }
                    else if (user_places!=null)
                    {
                        if (!manager.TryFindRoute(user_configuration, user_places, allowSmoothing:false, CancellationToken.None, out computed_track))
                            throw new Exception("Route not found");
                    }
                    else
                    throw new Exception("No input given");

                    Console.WriteLine($"Time route in {(Stopwatch.GetTimestamp() - start) / Stopwatch.Frequency} s");


                    using (FileStream stream = new FileStream(Helper.GetUniqueFileName(outputDir, "time-track-with-turns.kml"), FileMode.CreateNew))
                    {
                        TrackWriter.SaveAsKml(new UserVisualPreferences(), stream, "something meaningful",computed_track);
                    }
                }
            }

        }
        
    }
}
