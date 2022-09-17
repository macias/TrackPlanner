using System;
using System.IO;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TrackPlanner.Tests")]

namespace TrackPlanner.Mapping
{
    public readonly record struct CellIndex
    {
        public short LatitudeGridIndex { get;  }
        public short LongitudeGridIndex { get; }

        public CellIndex(int latitudeGridIndex, int longitudeGridIndex)
        {
            if (latitudeGridIndex<short.MinValue || latitudeGridIndex > short.MaxValue)
                throw new ArgumentOutOfRangeException($"{nameof(latitudeGridIndex)} = {latitudeGridIndex}");
            if (longitudeGridIndex<short.MinValue || longitudeGridIndex > short.MaxValue)
                throw new ArgumentOutOfRangeException($"{nameof(longitudeGridIndex)} = {longitudeGridIndex}");

            this.LatitudeGridIndex = (short)latitudeGridIndex;
            this.LongitudeGridIndex = (short)longitudeGridIndex;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(LatitudeGridIndex);
            writer.Write(LongitudeGridIndex);
        }
        public static CellIndex Read(BinaryReader reader)
        {
            var latitudeGrid = reader.ReadInt16();
            var longitudeGrid = reader.ReadInt16();

            return new CellIndex(latitudeGridIndex: latitudeGrid, longitudeGridIndex:longitudeGrid};
        }

        public void Deconstruct(out short latitudeGrid, out short longitudeGrid)
        {
            latitudeGrid = this.LatitudeGridIndex;
            longitudeGrid = this.LongitudeGridIndex;
        }

    }
    
    
}