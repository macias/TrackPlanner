using System;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Turner.Implementation
{
    internal readonly struct RoadRank
    {
        enum Perception
        {
            Highway,
            Trunk,
            Primary,
            Secondary,
            Tertiary,
            Cycleway,
            Ferry,
            Path,
        }

        public static RoadRank CyclewayLink(in RoadInfo info) => new RoadRank(info.Kind, Perception.Cycleway, isSolid(info), forced: true);
        public RoadRank CyclewayLink() => new RoadRank(original, Perception.Cycleway, IsSolid, forced: true);

        public bool IsCycleway => this.simplified == Perception.Cycleway;
        public bool IsMapPath => this.original == WayKind.Path;
        public bool IsSolid { get; }

        private readonly WayKind original;
        private readonly Perception simplified;
        private readonly bool forced;

        public RoadRank(in RoadInfo info) : this(info.Kind, simplifyRoadImportance(info), isSolid(info), false)
        {
        }
        private RoadRank(WayKind original, Perception kind, bool isSolid, bool forced)
        {
            this.original = original;
            this.simplified = kind;
            this.forced = forced;
            this.IsSolid = isSolid;
        }

        private static bool isSolid(in RoadInfo info)
        {
            if (info.Kind <= WayKind.TertiaryLink)
                return true;

            switch (info.Kind)
            {
                case WayKind.Unclassified:
                    if (info.Surface.IsLikelyPaved() || info.HasName)
                        return true;
                    break;

                case WayKind.Path:
                    if (info.Surface <= RoadSurface.Paved)
                        return true;
                    break;
            }

            return false;
        }

        private static Perception simplifyRoadImportance(in RoadInfo info)
        {
            // here we convert the kinds of the roads to more likely how the cyclist will "feel" the road and most importantly the changes between roads

            switch (info.Kind)
            {
                // for turns treat links and given road the same
                case WayKind.Highway:
                case WayKind.HighwayLink: return Perception.Highway;
                case WayKind.Primary:
                case WayKind.PrimaryLink: return Perception.Primary;
                case WayKind.Secondary:
                case WayKind.SecondaryLink: return Perception.Secondary;
                case WayKind.Trunk:
                case WayKind.TrunkLink: return Perception.Trunk;
                case WayKind.Tertiary:
                case WayKind.TertiaryLink: return Perception.Tertiary;

                case WayKind.Ferry: return Perception.Ferry;
                case WayKind.Cycleway: return Perception.Cycleway;

                case WayKind.Footway: return Perception.Path;
                case WayKind.Steps: return Perception.Path;

                // it is unlikely secondary road will remain unclassified, but residential or tertiary yes -- so, same bucket
                // we can upgrade this type of road only if we don't know the surface or if we know it well -- because it is better to add turn-notification than not
                case WayKind.Unclassified:
                    if (info.Surface.IsLikelyPaved() || info.HasName)
                        return Perception.Tertiary;
                    else
                        return Perception.Path;

                case WayKind.Path:
                    if (info.Surface == RoadSurface.AsphaltLike || info.HasName)
                        return Perception.Tertiary;
                    else
                        return Perception.Path;

            }

            throw new NotImplementedException($"Add case for {info.Kind}");
        }

        public bool IsMoreImportantThan(in RoadRank other)
        {
            if (this.simplified == Perception.Tertiary && other.simplified == Perception.Cycleway)
                return false;
            else
                return this.simplified < other.simplified;
        }

        public int DifferenceLevel(in RoadRank other)
        {
            if (this.simplified < other.simplified)
                return other.DifferenceLevel(this);

            int diff = this.simplified - other.simplified;
            // switching from (let's say) cycleway to path or to ferry is the the same difference, so we have to correct our enum "gaps"
            if (this.simplified == Perception.Path && other.simplified < Perception.Ferry)
                --diff;
            return diff;
        }


        public override string ToString()
        {
            return $"{(forced ? "^^" : "")}{simplified}";
        }
    }
}
