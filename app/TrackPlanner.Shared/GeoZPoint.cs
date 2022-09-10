using MathUnit;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

#nullable enable

namespace TrackPlanner.Shared
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack=2)]
    public readonly struct GeoZPoint : IEquatable<GeoZPoint>, ISerializable
    {
        public static GeoZPoint Invalid { get; } = FromDegreesMeters(100,0,short.MaxValue);
        
        public static GeoZPoint FromDegreesMeters(double latitude, double longitude, double? altitude)
        {
            return new GeoZPoint(latitude, longitude, altitude);
        }
        
        public static GeoZPoint Create(Angle latitude, Angle longitude, Length? altitude)
        {
            return new GeoZPoint(latitude.Degrees, longitude.Degrees, altitude?.Meters);
        }

        private const short nullAltitude = short.MinValue;
        
        private readonly float latitudeDegrees;
        private readonly float longitudeDegrees;
        // this is temporary ugly hack to save memory
        private short altitudeMeters => nullAltitude;

        public Angle Latitude => Angle.FromDegrees(latitudeDegrees);
        public Angle Longitude => Angle.FromDegrees(longitudeDegrees);
        public Length? Altitude => altitudeMeters == nullAltitude ? null : Length.FromMeters(altitudeMeters);

        private GeoZPoint(double latitude,double longitude, double? altitude)
        {
            this.latitudeDegrees = (float)latitude;
            this.longitudeDegrees = (float)longitude;
            //this.altitudeMeters = (short)(altitude ?? nullAltitude);
        }

        public GeoZPoint(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            this.latitudeDegrees = info.GetSingle(nameof(Latitude));
            this.longitudeDegrees = info.GetSingle(nameof(Longitude));
            var alt = (short?)info.GetValue(nameof(Altitude), typeof(short?));
            //this.altitudeMeters = alt ??  nullAltitude;
        }

        public void Deconstruct(out Angle latitude, out Angle longitude, out Length? altitude)
        {
            latitude = this.Latitude;
            longitude = this.Longitude;
            altitude = this.Altitude;
        }

        public override string ToString()
        {
            string result = $"{Latitude}, {Longitude}";
            if (this.Altitude.HasValue)
                result += $", {Altitude}";
            return result;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            info.AddValue(nameof(Latitude), latitudeDegrees);
            info.AddValue(nameof(Longitude), longitudeDegrees);
            short? alt = Altitude.HasValue ? altitudeMeters : null;
            info.AddValue(nameof(Altitude), alt);
        }

        public override bool Equals(object? obj)
        {
            if (obj is GeoZPoint pt)
                return Equals(pt);
            else
                return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine( this.latitudeDegrees, this.longitudeDegrees, this.altitudeMeters);
        }

        public bool Equals(GeoZPoint obj)
        {
            return this.latitudeDegrees == obj.latitudeDegrees && this.longitudeDegrees == obj.longitudeDegrees && this.altitudeMeters == obj.altitudeMeters;
        }

        public static bool operator==(in GeoZPoint a,in GeoZPoint b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(in GeoZPoint a, in GeoZPoint b)
        {
            return !(a==b);
        }
        
        public static void WriteFloatAngle(BinaryWriter writer, Angle angle)
        {
            writer.Write((float)angle.Degrees);
        }

        public static Angle ReadFloatAngle(BinaryReader reader)
        {
            return Angle.FromDegrees(reader.ReadSingle());
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(latitudeDegrees);
            writer.Write(longitudeDegrees);
            writer.Write(altitudeMeters);
        }

        public static GeoZPoint Read(BinaryReader reader)
        {
            var lat = reader.ReadSingle();
            var lon = reader.ReadSingle();
            var alt = reader.ReadInt16();

            return new  GeoZPoint(lat, lon, alt);
        }

    }
}