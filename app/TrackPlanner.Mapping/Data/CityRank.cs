namespace TrackPlanner.Mapping.Data
{
    public enum CityRank
    {
        Capital,
        Important1,
        Important2,
        Important3,
        Important4,
        City,
        Town,
        Village,
        Hamlet,
        Other
    }

    public static class CityRankExtension
    {
        public static int IndexOf(this CityRank kind)
        {
            return (int) kind;
        }
    }
}