using MathUnit;
using System;
using System.Collections.Generic;
using System.Linq;
using TrackPlanner.Data;
using TrackPlanner.Shared;
using TrackPlanner.LinqExtensions;
using TrackPlanner.Mapping.Data;
using TrackPlanner.Storage.Data;

namespace TrackPlanner.Mapping
{
    public abstract class RoadGrid
    {
        public int CellSize { get; }

        private readonly IWorldMap map;
        public IGeoCalculator Calc { get; }
        private readonly ILogger logger;
        private readonly string? debugDirectory;
        private readonly bool legacyGetNodeAllRoads;
        private readonly IReadOnlyBasicMap<CellIndex, RoadGridCell> cells;

        public abstract string GetStats();
        
        protected RoadGrid(ILogger logger, IReadOnlyBasicMap<CellIndex, RoadGridCell> cells, IWorldMap map, IGeoCalculator calc,
            int gridCellSize, string? debugDirectory, bool legacyGetNodeAllRoads)
        {
            this.CellSize = gridCellSize;
            this.logger = logger;
            this.map = map;
            this.cells = cells;
            this.Calc = calc;
            this.debugDirectory = debugDirectory;
            this.legacyGetNodeAllRoads = legacyGetNodeAllRoads;
        }

        private CellIndex getCellIndex(Angle latitude, Angle longitude)
        {
            return new CellIndex()
            {
                LatitudeGridIndex = (int) (latitude.Degrees * CellSize),
                LongitudeGridIndex = (int) (longitude.Degrees * CellSize)
            };
        }



        public List<RoadBucket> GetRoadBuckets(IReadOnlyList<RequestPoint> userTrack, Length proximityLimit,Length upperProximityLimit, 
            bool requireAllHits, bool singleMiddleSnaps)
        {
            var buckets = new List<RoadBucket>();
            for (int i = 0; i < userTrack.Count; ++i)
            {
                var is_final = i == 0 || i == userTrack.Count - 1;
                var bucket = GetRoadBucket(i, userTrack[i].UserPoint.Convert(), proximityLimit,upperProximityLimit, requireAllHits,singleMiddleSnaps && !is_final, 
                    isFinal: is_final,allowSmoothing:userTrack[i].AllowSmoothing);
                if (bucket!=null)
                    buckets.Add(bucket);
            }

            return buckets;
        }

        public RoadBucket? GetRoadBucket(int index, GeoZPoint userPoint, Length initProximityLimit,
            Length finalProximityLimit, bool requireAllHits,bool singleSnap, bool isFinal,bool allowSmoothing)
        {
            Length proximity = initProximityLimit;

            // gather all nodes within given proximity, if no nodes cannot be found, simply skip given point
            RoadBucket bucket = CreateBucket(index, userPoint, initProximityLimit, finalProximityLimit, automatic: false,
                // for ends pick up only closest nodes of given roads to avoid using some distant ones and then relying on some path-weight magic to jump from the distant node right to the end
                // (path-weight magic could be for example not counting snap distance)
                singleSnap:  singleSnap,
                isFinal: isFinal,
                allowSmoothing:allowSmoothing
            );

            if (bucket.Count == 0)
            {
                if (requireAllHits || isFinal)
                    throw new Exception($"No nodes found for user final point {userPoint} within {proximity}");

                return null;
            }
            else
            {
                return bucket;
            }
        }

        public RoadBucket CreateBucket(int debugIndex, in GeoZPoint point, Length initProximityLimit,
            Length finalProximityLimit,
            bool automatic, bool singleSnap, bool isFinal,bool allowSmoothing)
        {
            var dict = new Dictionary<RoadIndexLong, RoadSnapInfo>();
            // nodes which are within snap reach but were rejected because of winner-takes-all mode
            var reachable_nodes = new HashSet<long>();
            Length effective_proximity;
            for (effective_proximity = initProximityLimit;  effective_proximity <= finalProximityLimit; effective_proximity *= 2)
            {
                dict = getPointNode(point, effective_proximity, singlePerRoad: false);
                if (dict.Count > 0)
                    break;
            }

            if (dict.Any())
            {
                (RoadIndexLong closest_road_idx, RoadSnapInfo closest_snap_info) = dict.OrderBy(it => it.Value.TrackSnapDistance).First();

                Length closest_snap = closest_snap_info.TrackSnapDistance;

                if (effective_proximity != initProximityLimit)
                {
                    // we increase the limit of snap aggressively but once we have some data we try to limit the range of the snaps
                    // in linear fashion
                    effective_proximity = Math.Max(1, Math.Ceiling(closest_snap / initProximityLimit)) * initProximityLimit;

                    dict = dict.Where(it => it.Value.TrackSnapDistance <= effective_proximity)
                        .ToDictionary(it => it.Key, it => it.Value);

                    //.ToDictionary(it => it.Key, it => (it.Value.snapDistance,it.Value.crosspoint));
                }

                reachable_nodes.AddRange(dict.Keys.Select(it => this.map.GetNode(it)));

                if (singleSnap)
                {
                    dict.Clear();
                    dict.Add(closest_road_idx, closest_snap_info);
                }

                // when are just filling the gaps we cannot add penalties on those extra points
                if (automatic)
                    dict = dict.ToDictionary(it => it.Key,
                        it => new RoadSnapInfo(it.Value.RoadIdx, trackSnapDistance: Length.Zero, it.Value.TrackCrosspoint,
                            it.Value.DistanceAlongRoad, it.Value.LEGACY_ShortestNextDistance));

            }

            return new RoadBucket(debugIndex, map, nodeId: null, point, Calc, dict,reachable_nodes, effective_proximity, isFinal,allowSmoothing);
        }

        private Dictionary<RoadIndexLong, RoadSnapInfo> getPointNode(GeoZPoint point, Length limit,
            // curved roads can give several hits, this flag limits it to give only the closest hit
            bool singlePerRoad)
        {
            // key: road id -> index along road -> value
            var result = new Dictionary<long, Dictionary<int, RoadSnapInfo>>();

            foreach (RoadSnapInfo snap in GetSnaps(point, limit, predicate: null))
            {
                if (!result.TryGetValue(snap.RoadIdx.RoadMapIndex, out var road_dict))
                {
                    road_dict = new Dictionary<int, RoadSnapInfo>();
                    result.Add(snap.RoadIdx.RoadMapIndex, road_dict);
                }

                if (singlePerRoad)
                {
                    if (road_dict.Count > 0 && road_dict.Values.First().TrackSnapDistance > snap.TrackSnapDistance)
                        road_dict.Clear();
                    if (road_dict.Count == 0 || road_dict.Values.First().TrackSnapDistance == snap.TrackSnapDistance)
                        road_dict[snap.RoadIdx.IndexAlongRoad] = snap;
                }
                else
                {
                    if (!road_dict.TryGetValue(snap.RoadIdx.IndexAlongRoad, out var existing) || existing.TrackSnapDistance > snap.TrackSnapDistance)
                        road_dict[snap.RoadIdx.IndexAlongRoad] = snap;
                }
            }

            /*            Calc.GetAngularDistances(point, limit, out Angle lat_limit, out Angle lon_limit);

                        (int min_lat_grid, int min_lon_grid) = getCellIndex(point.Latitude - lat_limit, point.Longitude - lon_limit);
                        (int max_lat_grid, int max_lon_grid) = getCellIndex(point.Latitude + lat_limit, point.Longitude + lon_limit);


                        for (int lat_idx = min_lat_grid; lat_idx <= max_lat_grid; ++lat_idx)
                            for (int lon_idx = min_lon_grid; lon_idx <= max_lon_grid; ++lon_idx)
                            {
                                if (this.cells.TryGetValue((lat_idx, lon_idx), out RoadNodesGridCell? cell))
                                {
                                }
                            }
            */

            if (legacyGetNodeAllRoads)
                LEGACY_addNodeAllRoads(point, singlePerRoad, result);

            return result
                .SelectMany(it => it.Value.Select(v => v.Value))
                .ToDictionary(it => it.RoadIdx, it => it);
        }

       
        public IEnumerable<RoadSnapInfo> GetSnaps(GeoZPoint point, Length limit, Func<RoadInfo, bool>? predicate)
        {
            Calc.GetAngularDistances(point, limit, out Angle lat_limit, out Angle lon_limit);

            (int min_lat_grid, int min_lon_grid) = getCellIndex(point.Latitude - lat_limit, point.Longitude - lon_limit);
            (int max_lat_grid, int max_lon_grid) = getCellIndex(point.Latitude + lat_limit, point.Longitude + lon_limit);

            for (int lat_idx = min_lat_grid; lat_idx <= max_lat_grid; ++lat_idx)
                for (int lon_idx = min_lon_grid; lon_idx <= max_lon_grid; ++lon_idx)
                {
                    if (cells.TryGetValue(new CellIndex(){ LatitudeGridIndex = lat_idx, LongitudeGridIndex = lon_idx}, 
                            out RoadGridCell? cell))
                    {
                        foreach (RoadSnapInfo snap in cell.GetSnaps(this.map,Calc,point, limit, predicate))
                            yield return snap;
                    }
                }

        }
        
        private void LEGACY_addNodeAllRoads(GeoZPoint point, bool closestPerRoad, Dictionary<long, Dictionary<int, RoadSnapInfo>> snaps)
        {
            // if we once hit given node, in such case add all roads from that node computing snap distance as direct distance to such node

            foreach (long node in snaps.SelectMany(entry => entry.Value.Select(it => map.GetNode(new RoadIndexLong(entry.Key, it.Key)))).Distinct().ToArray())
            {
                GeoZPoint node_point = map.GetPoint(node);
                var dist = Calc.GetDistance(point, node_point);
                foreach (var idx in map.GetRoads(node))
                {
                    if (map.Roads[idx.RoadMapIndex].Kind == WayKind.Crossing)
                        continue;

                    if (!snaps.TryGetValue(idx.RoadMapIndex, out var entry))
                    {
                        entry = new Dictionary<int, RoadSnapInfo>();
                        snaps.Add(idx.RoadMapIndex, entry);
                    }
                    else if (closestPerRoad)
                        continue;

                    if (!entry.TryGetValue(idx.IndexAlongRoad, out var data))
                    {
                        entry.Add(idx.IndexAlongRoad, new RoadSnapInfo(idx, dist, node_point, Length.Zero, Length.Zero));
                    }
                }
            }
        }


    }
}