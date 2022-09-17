using System;
using System.IO;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TrackPlanner.Tests")]

namespace TrackPlanner.Mapping
{
    // separate for nodes and roads
    public readonly record struct WorldIdentifier
    {
        public CellIndex CellIndex { get;  }
        public ushort EntityIndex { get;  }

        public WorldIdentifier(CellIndex cellIndex, int entityIndex)
        {
            if (entityIndex<ushort.MinValue || entityIndex > ushort.MaxValue)
                throw new ArgumentOutOfRangeException($"{nameof(entityIndex)} = {entityIndex}");
            CellIndex = cellIndex;
            EntityIndex = (ushort)entityIndex;
        }
        
        public void Write(BinaryWriter writer)
        {
            this.CellIndex.Write(writer);
            writer.Write(EntityIndex);
        }
        public static WorldIdentifier Read(BinaryReader reader)
        {
            var cell_index = global::CellIndex.Read(reader);
            var entity_index = reader.ReadUInt16();

            return new WorldIdentifier(cell_index, entity_index);
    }

    }
    
  
    
}