namespace TrackPlanner.Turner.Implementation
{
    internal record struct TurnNotification
    {
        public static TurnNotification None => new TurnNotification(false, "");

        public bool Enable { get; }
        public string Reason { get; }
        
        public TurnNotification(bool enable, string reason)
        {
            Enable = enable;
            Reason = reason;
        }
    }
    
}
