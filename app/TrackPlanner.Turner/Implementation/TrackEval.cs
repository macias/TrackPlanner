using MathUnit;
using TrackPlanner.Shared;
using TrackPlanner.Mapping;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Turner.Implementation

{

 internal readonly struct TrackEval
    {
        public int BucketIndex { get; }
        public RoadIndexLong? IncomingIndex { get; }

        private readonly WorldMapMemory mapMemory;
        
        public RoadIndexLong RoadIndexLong { get; }
        public Length TotalLength { get; }
        public Length TotalError { get; }
        public int Switches { get; }
        public int Cycleways { get; }
        public GeoZPoint Point { get; }

        public long Node => this.mapMemory.GetNode(this.RoadIndexLong);

        public TrackEval(int bucketIndex, WorldMapMemory mapMemory, in RoadIndexLong? incomingIndex, in RoadIndexLong roadIndexLong, Length totalLength, 
            Length totalError, int switches, int cycleways,in GeoZPoint? point = null)
        {
            this.BucketIndex = bucketIndex;
            this.mapMemory = mapMemory;
            IncomingIndex = incomingIndex;
            this.RoadIndexLong = roadIndexLong;
            this.TotalLength = totalLength;
            TotalError = totalError;
            Switches = switches;
            Cycleways = cycleways;
            Point = point ?? mapMemory.GetPoint(roadIndexLong);
        }

        /*public void Deconstruct(out RoadIndex roadIndex, out Length totalLength, out Length totalError, out int switches, out int cycleways)
        {
            roadIndex = this.RoadIndex;
            totalLength = this.TotalLength;
            totalError = this.TotalError;
            switches = this.Switches;
            cycleways = this.Cycleways;
        }*/

        public string Digest()
        {
            return $"{this.TotalLength.Meters}_{this.TotalError.Meters}_{this.Switches}_{this.Cycleways}";
        }
        public override string ToString()
        {
            return $"b {BucketIndex}:{RoadIndexLong}({this.mapMemory.GetNode(RoadIndexLong)})";
        }
    }

}