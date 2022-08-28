namespace TrackPlanner.Mapping
{
    public static class LoggerExtension
    {
        public static void Info(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Info, message);
        }
        public static void Error(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Error, message);
        }
        public static void Verbose(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Verbose, message);
        }
        public static void Warning(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Warning, message);
        }
    }

}