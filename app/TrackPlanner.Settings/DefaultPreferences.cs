namespace TrackPlanner.Settings
{
    public sealed class DefaultPreferences
    {
        public bool AutoBuild { get; set; }
        public bool CalcReal { get; set; }
        public bool LoopRoute { get; set; }
        public bool StartsAtHome { get; set; }
        public bool EndsAtHome { get; set; }

        public DefaultPreferences()
        {
            StartsAtHome = true;
            EndsAtHome = true;
            CalcReal = true;
            LoopRoute = true;
        }
    }

}

