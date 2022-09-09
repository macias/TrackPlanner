using System.IO;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TrackPlanner.Tests")]

namespace TrackPlanner.Mapping
{
    public readonly record struct CellCoord
    {
        public int LatitudeGridIndex { get; init; }
        public int LongitudeGridIndex { get; init; }

        public CellCoord(int latitudeGridIndex, int longitudeGridIndex)
        {
            this.LatitudeGridIndex = latitudeGridIndex;
            this.LongitudeGridIndex = longitudeGridIndex;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(LatitudeGridIndex);
            writer.Write(LongitudeGridIndex);
        }
        public static CellCoord Read(BinaryReader reader)
        {
            var latitudeGrid = reader.ReadInt32();
            var longitudeGrid = reader.ReadInt32();

            return new CellCoord() {LatitudeGridIndex = latitudeGrid, LongitudeGridIndex = longitudeGrid};
        }

        public void Deconstruct(out int latitudeGrid, out int longitudeGrid)
        {
            latitudeGrid = this.LatitudeGridIndex;
            longitudeGrid = this.LongitudeGridIndex;
        }

    }
    
    
}