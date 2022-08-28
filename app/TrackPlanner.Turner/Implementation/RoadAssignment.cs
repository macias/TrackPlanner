using MathUnit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using TrackPlanner.Mapping;
using TrackPlanner.LinqExtensions;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Turner.Implementation

{
    internal sealed class RoadAssignment : IEnumerable<KeyValuePair<long, (RoadIndexLong idx, Length distance)>>
    {
        private readonly WorldMapMemory mapMemory;
        // key: road id
        private readonly Dictionary<long, (RoadIndexLong idx, Length distance)> roads;
        public bool Computed { get; set; }

        public IEnumerable<(RoadIndexLong idx, Length distance)> Values => this.roads.Values;
        public IEnumerable<long> Keys => this.roads.Keys;
        public int Count => this.roads.Count;

        public RoadAssignment(WorldMapMemory mapMemory, Dictionary<long, (RoadIndexLong idx, Length distance)> dict)
        {
            this.mapMemory = mapMemory;
            this.roads = dict;
        }

        internal void RemoveExcept(Length distance)
        {
            foreach (var entry in roads.ToArray())
                if (entry.Value.distance != distance)
                    roads.Remove(entry.Key);
        }

        public IEnumerator<KeyValuePair<long, (RoadIndexLong idx, Length distance)>> GetEnumerator()
        {
            return roads.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        internal void RemoveExcept(long roadId)
        {
            this.roads.RemoveExcept(roadId);
        }

        internal void RemoveExcept(IReadOnlySet< long> roadIds)
        {
            foreach (var road_id in this.roads.Keys.ToArray())
                if (!roadIds.Contains(road_id))
                    this.roads.Remove(road_id);
        }

        public bool TryGetRoad(long roadId,out RoadIndexLong indexLong,out Length distance)
        {
            var result = this.roads.TryGetValue(roadId, out var entry);
            (indexLong, distance) = entry;
            return result;
        }

        internal IEnumerable< RoadIndexLong> GetAtNode(long nodeId)
        {
            foreach (var entry in this.roads.Values)
                if (this.mapMemory.GetNode(entry.idx) == nodeId)
                    yield return entry.idx;
        }

        /*        internal bool Remove(WayKind wayKind)
                {
                    bool changed = false;
                    foreach (var entry in roads.ToArray())
                        if (map.Roads[entry.Key].Kind == wayKind)
                            if (roads.Remove(entry.Key))
                                changed = true;

                    return changed;
                }
          */
    }
}
