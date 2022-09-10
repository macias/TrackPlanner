using System.IO;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TrackPlanner.Tests")]

namespace TrackPlanner.Mapping
{
    public readonly record struct CellIndex
    {
        public int LatitudeGridIndex { get; init; }
        public int LongitudeGridIndex { get; init; }

        public CellIndex(int latitudeGridIndex, int longitudeGridIndex)
        {
            this.LatitudeGridIndex = latitudeGridIndex;
            this.LongitudeGridIndex = longitudeGridIndex;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(LatitudeGridIndex);
            writer.Write(LongitudeGridIndex);
        }
        public static CellIndex Read(BinaryReader reader)
        {
            var latitudeGrid = reader.ReadInt32();
            var longitudeGrid = reader.ReadInt32();

            return new CellIndex() {LatitudeGridIndex = latitudeGrid, LongitudeGridIndex = longitudeGrid};
        }

        public void Deconstruct(out int latitudeGrid, out int longitudeGrid)
        {
            latitudeGrid = this.LatitudeGridIndex;
            longitudeGrid = this.LongitudeGridIndex;
        }

    }
    
    
}