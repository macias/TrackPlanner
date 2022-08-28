using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace TrackPlanner.Mapping.Data
{
    // https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.structlayoutattribute.pack?view=net-6.0
    // https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.layoutkind?view=net-6.0
    
    [StructLayout(LayoutKind.Sequential, Pack=2)]
    public readonly struct RoadIndexLong : IEquatable<RoadIndexLong>
    {
        private static readonly NumberFormatInfo nfi;

        static RoadIndexLong()
        {
            nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
            nfi.NumberGroupSeparator = "_";
        }

        public static RoadIndexLong InvalidIndex(long roadId)
        {
            return new RoadIndexLong(roadId, ushort.MaxValue);
        }

        public long RoadMapIndex { get; }
        public ushort IndexAlongRoad { get; }

        public RoadIndexLong(long roadMapIndex, int indexAlongRoad)
        {
            RoadMapIndex = roadMapIndex;
            IndexAlongRoad = (ushort)indexAlongRoad;
        }

        public RoadIndexLong(KeyValuePair< long, ushort> pair) : this(pair.Key,pair.Value)
        {
        }

        public RoadIndexLong(RoadIndexFlat flat) : this(flat.RoadMapIndex,flat.IndexAlongRoad)
        {
        }
        
        public void Write(BinaryWriter writer)
        {
            writer.Write(RoadMapIndex);
            writer.Write(IndexAlongRoad);
        }
        
        public static RoadIndexLong Read(BinaryReader reader)
        {
            var id = reader.ReadInt64();
            var along = reader.ReadUInt16();

            return new RoadIndexLong(id, along);
        }

        public void Deconstruct(out long roadId,out ushort indexAlongRoad)
        {
            roadId = this.RoadMapIndex;
            indexAlongRoad = this.IndexAlongRoad;
        }

        public override string ToString()
        {
//            return $"{RoadId.ToString("#,0",nfi)} [{IndexAlongRoad}]";
            return $"{RoadMapIndex} [{IndexAlongRoad}]";
        }

        public override bool Equals(object? obj)
        {
            if (obj is RoadIndexLong idx)
                return this.Equals(idx);
            else
                return false;
        }

        public  bool Equals(RoadIndexLong obj)
        {
            return this.RoadMapIndex == obj.RoadMapIndex && this.IndexAlongRoad == obj.IndexAlongRoad;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.RoadMapIndex, this.IndexAlongRoad);
        }

        public RoadIndexLong Next()
        {
            return new RoadIndexLong(RoadMapIndex, IndexAlongRoad + 1);
        }

        public static bool operator==(in RoadIndexLong a,in RoadIndexLong b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(in RoadIndexLong a, in RoadIndexLong b)
        {
            return !a.Equals(b);
        }
    }

}
