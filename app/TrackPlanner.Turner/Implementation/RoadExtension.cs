using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Turner.Implementation
{
    internal static class RoadExtension
    {
        public static bool IsLikelyPaved(this RoadSurface surface)
        {
            return surface <= RoadSurface.Paved || surface == RoadSurface.Unknown;
        }
    }
}
