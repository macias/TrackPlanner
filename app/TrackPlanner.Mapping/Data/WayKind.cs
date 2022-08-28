using System.Collections.Generic;

namespace TrackPlanner.Mapping.Data
{
    public enum WayKind : byte
    {
        Highway, // AKA motorway
        HighwayLink,
        Trunk,
        TrunkLink,
        Primary,
        PrimaryLink,
        Secondary,
        SecondaryLink,
        Tertiary,
        TertiaryLink,
        Cycleway,
        /// <summary>
        /// if surface is not given assume both paved and unpaved
        /// </summary>
        Unclassified,
        Footway,
        Steps,
        Ferry,
        /// <summary>
        /// if surface is not given assume unpaved
        /// </summary>
        Path,
        Crossing,
    }

    public static class WayKindExtension
    {
        public static int IndexOf(this WayKind kind)
        {
            return (int)kind;
        }

        public static bool IsStable(this WayKind kind)
        {
            return kind != WayKind.Path;
        }
    }

}