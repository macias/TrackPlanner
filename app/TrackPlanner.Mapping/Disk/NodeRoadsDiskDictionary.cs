using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrackPlanner.Mapping.Data;
using TrackPlanner.Storage;

namespace TrackPlanner.Mapping.Disk
{
    public sealed class NodeRoadsDiskDictionary : INodeRoadsDictionary
    {
        public static int NodeDataDiskSize => sizeof(System.Single) + sizeof(System.Single) // lat + lon 
                                                                    + sizeof(System.Int16) // altitude
                                                                    +sizeof(bool); // dangerous flag
        
        private readonly DiskDictionary<long, List<RoadIndexLong>> diskDictionary;

        public IEnumerable<RoadIndexLong> this[long nodeId] => this.diskDictionary[nodeId];

        public NodeRoadsDiskDictionary(DiskDictionary<long,List<RoadIndexLong>> diskDictionary)
        {
            this.diskDictionary = diskDictionary;
        }
        
        public void Write(BinaryWriter writer, long nodeId)
        {
            throw new NotImplementedException();
        }

        public static unsafe List<RoadIndexLong> Load(long _, IReadOnlyList<BinaryReader> readers)
        {
            int total_count = 0;
            var counts = stackalloc int[readers.Count];

            for (int r = 0; r < readers.Count; ++r)
            {
                int curr_count = readers[r].ReadByte();
                counts[r] = curr_count;
                total_count += curr_count;
            }

            var result = new HashSet<RoadIndexLong>(capacity: total_count);
            
            for (int r = 0; r < readers.Count; ++r)
            {
                for (int i = 0; i < counts[r]; ++i)
                {
                    result.Add(RoadIndexLong.Read(readers[r]));
                }
            }

            return result.ToList();
        }

        public string GetStats()
        {
            return this.diskDictionary.GetStats();
        }
    }

}