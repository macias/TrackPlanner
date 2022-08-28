

using TrackPlanner.Mapping;

namespace TrackPlanner.Tests.Implementation
{
    internal sealed class NoLogger : ILogger
    {
        public void Flush()
        {
        }

        public void Log(LogLevel level,string message)
        {
        }

    }
}