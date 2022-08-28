using System.IO;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TrackPlanner.Tests")]

namespace TrackPlanner.Mapping
{
    public readonly record struct CellCoord
    {
        public int latitudeGrid { get; init; }
        public int longitudeGrid { get; init; }

        public CellCoord(int latitudeGrid, int longitudeGrid)
        {
            this.latitudeGrid = latitudeGrid;
            this.longitudeGrid = longitudeGrid;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(latitudeGrid);
            writer.Write(longitudeGrid);
        }
        public static CellCoord Read(BinaryReader reader)
        {
            var latitudeGrid = reader.ReadInt32();
            var longitudeGrid = reader.ReadInt32();

            return new CellCoord() {latitudeGrid = latitudeGrid, longitudeGrid = longitudeGrid};
        }

        public void Deconstruct(out int latitudeGrid, out int longitudeGrid)
        {
            latitudeGrid = this.latitudeGrid;
            longitudeGrid = this.longitudeGrid;
        }

    }
    
    
}