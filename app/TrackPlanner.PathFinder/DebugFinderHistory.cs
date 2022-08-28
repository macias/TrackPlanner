using MathUnit;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TrackPlanner.Shared;
using TrackRadar.Collections;
using TrackPlanner.DataExchange;
using TrackPlanner.Mapping;

namespace TrackPlanner.PathFinder
{
    // A* algorithm with pairing heap
    // https://en.wikipedia.org/wiki/A*_search_algorithm
    // https://brilliant.org/wiki/pairing-heap/
    // https://en.wikipedia.org/wiki/Pairing_heap

    internal sealed class DebugFinderHistory : IDisposable
    {
        private readonly ILogger logger;
        private IWorldMap map;
        private readonly string label;
        private int lastIndexSaved;
        private readonly string? debugDirectory;
        private readonly Dictionary<Placement, (int index, Weight weight, BacktrackInfo info)> histData;

        public DebugFinderHistory(ILogger logger, IWorldMap map,string label, string? debugDirectory)
        {
            this.logger = logger;
            this.map = map;
            this.label = label;
            this.debugDirectory = debugDirectory;
            this.histData = new Dictionary<Placement, (int index, Weight weight, BacktrackInfo info)>();
            this.lastIndexSaved = -1;
        }

        public void Dispose()
        {
            DumpLastData();
        }
        
        internal void Add(Placement place, Weight weight, BacktrackInfo info)
        {
            this.histData.Add(place, (this.histData.Count, weight, info));

            if (this.histData.Count % 1000 == 0)
                DumpLastData();
        }

        public void DumpLastData()
        {
            if (debugDirectory == null)
                return;

            var input = new TrackWriterInput();
            var last = lastIndexSaved;
            foreach (var entry in this.histData.Where(it => it.Value.index > last).OrderBy(it => it.Value.index))
            {
                string source = entry.Value.index == 0 ? "@" : this.histData[entry.Value.info.Source].index.ToString();

                input.AddPoint(entry.Key.GetPoint(map), $"{entry.Value.index}{(entry.Key.NodeId.HasValue?$"#{entry.Key.NodeId}":"")} {entry.Value.weight} from {source}",comment:null, entry.Value.index == 0 ? PointIcon.StarIcon : PointIcon.DotIcon);

                this.lastIndexSaved = entry.Value.index;
            }

            string filename = Helper.GetUniqueFileName(debugDirectory, $"trace-{label}-{histData.Count:D10}.kml");
            input.BuildDecoratedKml().Save(filename);
        }
    }

  
}
