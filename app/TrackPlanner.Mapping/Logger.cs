using System;
using System.IO;

namespace TrackPlanner.Mapping
{
    public static class Logger
    {
        public static IDisposable Create(string path, out ILogger logger)
        {
            {
                var log_dir = System.IO.Path.GetDirectoryName(path);
                if (log_dir != null)
                    System.IO.Directory.CreateDirectory(log_dir);
            }

            var stream = new StreamWriter(path, append: true);
            logger = new LoggerImpl(stream);
            return stream;
        }

        public static ILogger Create()
        {
            return new LoggerImpl(null);
        }


        private sealed class LoggerImpl : ILogger
        {
            private readonly object threadLock = new object();

            private readonly TextWriter? writer;

            public LoggerImpl(TextWriter? writer)
            {
                this.writer = writer;
            }

            public void Log(LogLevel level, string message)
            {
                lock (this.threadLock)
                {
                    message = $"[{level.ToString().ToUpperInvariant()}] {message}";
                    writer?.WriteLine(message);
                    Console.WriteLine(message);
                }
            }

            public void Flush()
            {
                lock (this.threadLock)
                {
                    writer?.Flush();
                }
            }
        }
    }
}