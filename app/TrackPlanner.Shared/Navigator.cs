namespace TrackPlanner.Shared
{
    public readonly struct Navigator
    {
        public string BaseDirectory { get; }

        public Navigator(string baseDirectory)
        {
            this.BaseDirectory = System.IO.Path.GetFullPath(baseDirectory);
        }

        public string GetMiniMaps()
        {
            return System.IO.Path.Combine(this.BaseDirectory, "app/mini-maps");
        }

        public string GetWorldMaps()
        {
            return System.IO.Path.Combine(this.BaseDirectory, "maps");
        }

        public string GetOutput()
        {
            return System.IO.Path.Combine(this.BaseDirectory, "output");
        }

        public string GetDebug()
        {
            return System.IO.Path.Combine(this.BaseDirectory, "debug");
        }
        public string? GetDebug(bool enabled)
        {
            return enabled?GetDebug():null;
        }
    }
}