#nullable enable

namespace TrackPlanner.Mapping.Data
{
    public enum RoadSurface : byte
    {
        AsphaltLike,
        HardBlocks,
        Paved,
        DirtLike,
        GrassLike,
        SandLike,
        Unpaved,
        Ice,
        Wood, // making it separate because I don't ride on wood surfaces (SPLINTERS and NAILS)
        Unknown, // separate value (instead of unpaved) is handy for computing turn-notifications and it is better than having nullable surface (for painting)
    }

    public static class RoadSurfaceExtension
    {
        public static int IndexOf(this RoadSurface kind)
        {
            return (int)kind;
        }
    }


}