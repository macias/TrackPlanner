using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace TrackPlanner.Mapping.Data
{
    // https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.structlayoutattribute.pack?view=net-6.0
    // https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.layoutkind?view=net-6.0
    
    [StructLayout(LayoutKind.Sequential, Pack=2)]
    public readonly struct RoadIndexFlat
    {
        private static readonly NumberFormatInfo nfi;

        static RoadIndexFlat()
        {
            nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
            nfi.NumberGroupSeparator = "_";
        }

        public static RoadIndexFlat InvalidIndex(long roadId)
        {
            return new RoadIndexFlat(roadId, ushort.MaxValue);
        }

        public int RoadMapIndex { get; }
        public ushort IndexAlongRoad { get; }

        public RoadIndexFlat(long roadMapIndex, int indexAlongRoad)
        {
            RoadMapIndex = (int)roadMapIndex;
            IndexAlongRoad = (ushort)indexAlongRoad;
        }

        public RoadIndexFlat(KeyValuePair< long, ushort> pair) : this(pair.Key,pair.Value)
        {
        }

        public void Deconstruct(out int roadMapIndex,out ushort indexAlongRoad)
        {
            roadMapIndex = this.RoadMapIndex;
            indexAlongRoad = this.IndexAlongRoad;
        }

        public override string ToString()
        {
//            return $"{RoadId.ToString("#,0",nfi)} [{IndexAlongRoad}]";
            return $"{RoadMapIndex} [{IndexAlongRoad}]";
        }

        public override bool Equals(object? obj)
        {
            if (obj is RoadIndexFlat idx)
                return this.Equals(idx);
            else
                return false;
        }

        public  bool Equals(RoadIndexFlat obj)
        {
            return this.RoadMapIndex == obj.RoadMapIndex && this.IndexAlongRoad == obj.IndexAlongRoad;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.RoadMapIndex, this.IndexAlongRoad);
        }

        public RoadIndexFlat Next()
        {
            return new RoadIndexFlat(RoadMapIndex, IndexAlongRoad + 1);
        }

        public static bool operator==(in RoadIndexFlat a,in RoadIndexFlat b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(in RoadIndexFlat a, in RoadIndexFlat b)
        {
            return !a.Equals(b);
        }
    }

}
