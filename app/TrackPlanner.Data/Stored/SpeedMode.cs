using MathUnit;

namespace TrackPlanner.Data.Stored
{
    public enum SpeedMode // NOTE, it is used as indexer!
    {
        Asphalt,
        HardBlocks, // concrete plates or cobblestones
        Ground,
        Sand,

        Paved,
        Unknown,
        
        UrbanSidewalk, // it is either "pure" sidewalk (bicycles are forbidden) or it is mixed (pedestrians and cyclists are not separated)
        
        Walk, // walking on unrideable surface (like wood)

        CarryBike, // steps, or walking over some objects

        CableFerry
    }
}