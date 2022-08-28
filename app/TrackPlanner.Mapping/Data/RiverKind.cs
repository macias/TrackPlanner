namespace TrackPlanner.Mapping.Data
{
    public enum RiverKind
    {
        River,
        Stream,
    }

    public static class RiverKindExtension
    {
        public static int IndexOf(this RiverKind kind)
        {
            return (int)kind;
        }
    }

}