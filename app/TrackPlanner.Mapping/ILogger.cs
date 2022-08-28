namespace TrackPlanner.Mapping
{
    public interface ILogger
    {
        void Log(LogLevel level, string message);

        void Flush();
    }
}