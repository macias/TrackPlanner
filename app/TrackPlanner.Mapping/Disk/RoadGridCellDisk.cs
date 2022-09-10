﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Mapping.Disk
{
    public static class RoadGridCellDisk
    {
     
        public static void Write(this RoadGridCell cell, BinaryWriter writer)
        {
            writer.Write(cell.Count);
            foreach (var elem in cell.Segments)
                elem.Write(writer);
        }
        
        public static RoadGridCell Read(BinaryReader reader)
        {
            var count = reader.ReadInt32();
            var segments = new HashSet<RoadIndexLong>(capacity: count);
            for (int i = 0; i < count; ++i)
                segments.Add(RoadIndexLong.Read(reader));

            return new RoadGridCell(segments.ToList());
        }

        public static unsafe RoadGridCell Load(IReadOnlyList<BinaryReader> readers)
        {
            var counts = stackalloc int[readers.Count];
            int total_count = 0;
            for (int r=0;r<readers.Count;++r)
            {
                var c = readers[r].ReadInt32();
                counts[r] = c;
                total_count += c;
            }

            var segments = new HashSet<RoadIndexLong>(capacity: total_count);

            for (int r = 0; r < readers.Count; ++r)
            {
                for (int i = 0; i < counts[r]; ++i)
                    segments.Add(RoadIndexLong.Read(readers[r]));
            }

            return new RoadGridCell(segments.ToList());
        }
    }

   
}
