using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TrackPlanner.Data;

namespace TrackPlanner.Mapping.Data
{
    [StructLayout(LayoutKind.Auto)]
    public readonly struct RoadInfo : IEquatable<RoadInfo>
    {
        [Flags]
        private enum RoadFeatures : byte
        {
            None = 0,

            OneWay = 1,
            BikeLine = 2,
            UrbanSidewalk = 4,
            Roundabout = 8,
            Dismount = 16,
            HasAccess = 32,
            Singletrack = 64,
            SpeedLimit50 = 128,
        }

        // explicit field for checking usage of identifier (we should use it only as label/debug, on every other occassion there should be dictionary index used)
        private  readonly  long identifier ;
        public long Identifier => this.identifier;
        private readonly RoadFeatures features;
        public WayKind Kind { get; }
        private readonly byte surface_smoothness;
        public RoadSurface Surface => (RoadSurface)((this.surface_smoothness >> 4) & 0x0f);
        public RoadSmoothness Smoothness => (RoadSmoothness)(this.surface_smoothness & 0x0f);
        public IReadOnlyList<long> Nodes { get; }

        private const int nullNameIdentifier = -1; // road does not have name set
        private readonly int nameIdentifier;
        
        
        public bool BikeLane => this.features.HasFlag(RoadFeatures.BikeLine);
        public bool OneWay => this.features.HasFlag(RoadFeatures.OneWay);
        public bool UrbanSidewalk => this.features.HasFlag(RoadFeatures.UrbanSidewalk);
        public bool IsRoundabout => this.features.HasFlag(RoadFeatures.Roundabout);
        public bool IsSingletrack => this.features.HasFlag(RoadFeatures.Singletrack);
        public bool Dismount => this.features.HasFlag(RoadFeatures.Dismount);
        public bool HasAccess => this.features.HasFlag(RoadFeatures.HasAccess);
        public bool HasSpeedLimit50 => this.features.HasFlag(RoadFeatures.SpeedLimit50);


        public int NameIdentifier
        {
            get
            {
                if (this.nameIdentifier == nullNameIdentifier)
                    throw new NullReferenceException($"Road {this.identifier} does not have name.");

                return this.nameIdentifier;
            }
        }

        public sbyte Layer { get; }

        public bool IsMassiveTraffic => this.Kind <= WayKind.PrimaryLink;
        public bool IsSignificantTraffic => !this.IsMassiveTraffic && this.Kind <= WayKind.SecondaryLink;
        public bool IsDangerous => this.IsMassiveTraffic && !this.HasSpeedLimit50;
        public bool IsUncomfortable => this.IsSignificantTraffic && !this.HasSpeedLimit50;

        public bool HasName => this.nameIdentifier >= 0;

        private RoadInfo(long identifier, WayKind kind, int nameIdentifier, RoadFeatures roadFeatures, RoadSurface surface, RoadSmoothness smoothness,
            sbyte layer, IReadOnlyList<long> nodes)
        {
            this.features = roadFeatures;
            this.identifier = identifier;
            this.nameIdentifier = nameIdentifier;
            Kind = kind;
            this.surface_smoothness = (byte) ((((byte) (surface)) << 4) | ((byte) smoothness));
            Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            Layer = layer;
        }

        public RoadInfo(long identifier, WayKind kind, int? nameIdentifier, bool oneWay, bool roundabout, RoadSurface surface, RoadSmoothness smoothness,
            bool hasAccess, bool speedLimit50, bool hasBikeLane, bool isSingletrack, bool urbanSidewalk, bool dismount, sbyte layer, IReadOnlyList<long> nodes)

            : this(identifier, kind, nameIdentifier ?? nullNameIdentifier, (oneWay ? RoadFeatures.OneWay : RoadFeatures.None)
                                                                   | (hasBikeLane ? RoadFeatures.BikeLine : RoadFeatures.None)
                                                                   | (speedLimit50 ? RoadFeatures.SpeedLimit50 : RoadFeatures.None)
                                                                   | (roundabout ? RoadFeatures.Roundabout : RoadFeatures.None)
                                                                   | (dismount ? RoadFeatures.Dismount : RoadFeatures.None)
                                                                   | (hasAccess ? RoadFeatures.HasAccess : RoadFeatures.None)
                                                                   | (isSingletrack ? RoadFeatures.Singletrack : RoadFeatures.None)
                                                                   | (urbanSidewalk ? RoadFeatures.UrbanSidewalk : RoadFeatures.None), surface, smoothness, layer, nodes)
        {
        }

        internal string DetailsToString()
        {
            return $"{Layer}; {Kind}; {Surface}; {Smoothness}; {features}; {this.nameIdentifier}";
        }

        internal static RoadInfo Parse(long roadId, IReadOnlyList<long> nodes, string details)
        {
            var parts = details.Split("; ");

            int index = 0;
            var layer = sbyte.Parse(parts[index++]);
            var kind = Enum.Parse<WayKind>(parts[index++]);
            var surface = Enum.Parse<RoadSurface>(parts[index++]);
            var smoothness = Enum.Parse<RoadSmoothness>(parts[index++]);
            var features = Enum.Parse<RoadFeatures>(parts[index++]);
            var name_identifier = int.Parse(parts[index++]);
            
            return new RoadInfo(roadId, kind, name_identifier, features, surface, smoothness, layer, nodes);
        }
        
        internal void Write(BinaryWriter writer)
        {
            writer.Write(this.identifier);
            writer.Write(Layer);
            writer.Write((byte) Kind);
            writer.Write((byte) Surface);
            writer.Write((byte) Smoothness);
            writer.Write(this.nameIdentifier);
            // squash any name identifier as invalid, because we don't store road names (so far)
            writer.Write((byte) features);

            writer.Write(Nodes.Count);
            foreach (var node in Nodes)
                writer.Write(node);
        }

        public static RoadInfo Read(BinaryReader reader, IReadOnlyDictionary<long, long> nodeMapping)
        {
            var id = reader.ReadInt64();
            var layer = reader.ReadSByte();
            var kind = (WayKind) reader.ReadByte();
            var surface = (RoadSurface) reader.ReadByte();
            var smoothness = (RoadSmoothness) reader.ReadByte();
            var name_identifier = reader.ReadInt32();
            var features = (RoadFeatures) (reader.ReadByte());

            var nodes_count = reader.ReadInt32();

            var nodes = new long[nodes_count];
            for (int i = 0; i < nodes_count; ++i)
                nodes[i] = nodeMapping[reader.ReadInt64()];

            return new RoadInfo(id, kind, name_identifier, features, surface, smoothness, layer, nodes);
        }

        private static RoadInfo Read(BinaryReader reader, bool probing, out long id, out int nodesCount)
        {
             id = reader.ReadInt64();
            var layer = reader.ReadSByte();
            var kind = (WayKind) reader.ReadByte();
            var surface = (RoadSurface) reader.ReadByte();
            var smoothness = (RoadSmoothness) reader.ReadByte();
            var name_identifier = reader.ReadInt32();
            var features = (RoadFeatures) (reader.ReadByte());

            nodesCount = reader.ReadInt32();

            if (probing)
            {
                for (int i = 0; i < nodesCount; ++i)
                    reader.ReadInt64();

                return default;
            }
            else
            {
                var nodes = new long[nodesCount];
                for (int i = 0; i < nodesCount; ++i)
                    nodes[i] = reader.ReadInt64();

                return new RoadInfo(id, kind, name_identifier, features, surface, smoothness, layer, nodes);
            }
        }

        internal static void ProbeRead(BinaryReader reader,out long id,out int nodesCount)
        {
            Read(reader, probing:true,out  id, out nodesCount);
        }

        internal static RoadInfo Read(BinaryReader reader)
        {
            return Read(reader, probing:false, out _,out _);
        }

        internal RoadInfo BuildWithDenyAccess()
        {
            var feat = this.features;
            if (HasAccess)
                feat ^= RoadFeatures.HasAccess;
            return new RoadInfo(this.identifier, Kind, nameIdentifier, feat, Surface, Smoothness, Layer, Nodes);
        }

        internal RoadInfo BuildWithSpeedLimit()
        {
            var feat = this.features;
            if (!HasSpeedLimit50)
                feat ^= RoadFeatures.SpeedLimit50;
            return new RoadInfo(this.identifier, Kind, nameIdentifier, feat, Surface, Smoothness, Layer, Nodes);
        }

        public override bool Equals(object? obj)
        {
            return obj is RoadInfo info && Equals(info);
        }

        public bool Equals(RoadInfo other)
        {
            return this.identifier == other.identifier &&
                   Kind == other.Kind &&
                   features == other.features &&
                   Surface == other.Surface &&
                   Smoothness == other.Smoothness &&
                   nameIdentifier == other.nameIdentifier &&
                   Layer == other.Layer
                   && Enumerable.SequenceEqual(Nodes, other.Nodes);
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(this.identifier);
            hash.Add(Kind);
            hash.Add(features);
            hash.Add(Surface);
            hash.Add(Smoothness);
            hash.Add(nameIdentifier);
            hash.Add(Layer);
            foreach (var n in Nodes)
                hash.Add(n);
            return hash.ToHashCode();
        }

        public override string ToString()
        {
            return $"{nameof(identifier)}: {this.identifier}, {nameof(Kind)}: {Kind}, {nameof(features)}: {this.features}, {nameof(Surface)}: {Surface},{nameof(Smoothness)}: {Smoothness},{nameof(nameIdentifier)}: {nameIdentifier},{nameof(Layer)}: {Layer}, #{nameof(Nodes)}: {Nodes.Count}";
        }

        public static bool operator ==(RoadInfo left, RoadInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RoadInfo left, RoadInfo right)
        {
            return !(left == right);
        }

        public bool TryMergeWith(in RoadInfo other,out RoadInfo merged)
        {
            if (this.HasSpeedLimit50 || other.HasSpeedLimit50)
            {
                // if one piece of map managed to compute the limit, we trust such computation
                merged = this.BuildWithSpeedLimit();
                var tmp = this.BuildWithSpeedLimit();
                return (merged == tmp);
            }
            else
            {
                merged = default;
                return false;
            }
        }
        
        private static SpeedMode? tryGetSurfaceSpeed(RoadSurface surface)
        {
            switch (surface)
            {
                case RoadSurface.Wood: return SpeedMode.Walk;
                case RoadSurface.AsphaltLike: return SpeedMode.Asphalt;
                case RoadSurface.DirtLike: return SpeedMode.Ground;
                case RoadSurface.Ice:
                case RoadSurface.GrassLike: return SpeedMode.Ground;
                case RoadSurface.HardBlocks: return SpeedMode.HardBlocks;
                case RoadSurface.SandLike: return SpeedMode.Sand;

                case RoadSurface.Paved:
                case RoadSurface.Unpaved:
                case RoadSurface.Unknown: return null;
                default: throw new NotImplementedException($"{surface}");
            }
        }

        
        public SpeedMode GetRoadSpeedMode()
        {
            {
                // special cases

                if (this.Kind == WayKind.Ferry) // has to be placed before anything else, because we don't ride on the ferry, but the ferry travels with its own speed
                    return SpeedMode.CableFerry;
                if (this.Kind == WayKind.Steps)
                    return SpeedMode.CarryBike;
                if (this.Dismount || this.IsSingletrack)
                    return SpeedMode.Walk;
            }

            if (this.Kind <= WayKind.SecondaryLink)
                return SpeedMode.Asphalt;


            var surface_speed = tryGetSurfaceSpeed(this.Surface);

            if (this.Kind == WayKind.Footway)
            {
                if (this.BikeLane) // footway with separated bike line has to be asphalt or paving stones
                    return surface_speed ?? SpeedMode.Asphalt;
                else if (this.UrbanSidewalk) // sidewalks should be rather well done, but there are pedestrians so we cannot go full speed
                    return SpeedMode.UrbanSidewalk;

                // if this is any other footway, it rather means we are outside urban area and we have to rely on surface reading,
                // and probably noboy will care about riding it
            }


            if (surface_speed.HasValue)
                return surface_speed.Value;

            if (this.Kind <= WayKind.TertiaryLink)
                return SpeedMode.Asphalt;

            if (this.Surface == RoadSurface.Paved)
                return SpeedMode.Paved;
            
            return SpeedMode.Unknown;
        }
        
    }
}