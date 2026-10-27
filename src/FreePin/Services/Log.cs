using System;
using System.IO;
using System.Text;

namespace FreePin.Services
{
    public static class Log
    {
        private static readonly object Sync = new object();
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FreePin", "Logs");
        private static readonly string LogPath = Path.Combine(LogDirectory, "freepin.log");

        public static string FilePath
        {
            get { return LogPath; }
        }

        public static void Info(string message)
        {
            Write("INFO", message, null);
        }

        public static void Warn(string message)
        {
            Write("WARN", message, null);
        }

        public static void Error(string message, Exception exception)
        {
            Write("ERROR", message, exception);
        }

        private static void Write(string level, string message, Exception exception)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(LogDirectory);
                    var builder = new StringBuilder();
                    builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                        .Append(" [").Append(level).Append("] ")
                        .Append(message);
                    if (exception != null)
                    {
                        builder.AppendLine();
                        builder.Append(exception);
                    }
                    builder.AppendLine();
                    File.AppendAllText(LogPath, builder.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // Logging must never take down the application.
            }
        }
    }
}
