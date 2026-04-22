using System;
using System.IO;

namespace PdfHighlighter
{
    internal static class AppLogger
    {
        private static readonly object Sync = new object();
        private static StreamWriter? writer;
        private static string logPath = string.Empty;

        public static void Initialize()
        {
            lock (Sync)
            {
                logPath = Path.Combine(AppContext.BaseDirectory, "debug.log");

                // Pri kazdem spusteni log vynulujeme, aby obsahoval jen aktualni beh.
                File.WriteAllText(logPath, string.Empty);

                writer = new StreamWriter(new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                    AutoFlush = true
                };

                Log("=== Application started ===");
            }
        }

        public static void Shutdown()
        {
            lock (Sync)
            {
                if (writer != null)
                {
                    Log("=== Application stopped ===");
                    writer.Dispose();
                    writer = null;
                }
            }
        }

        public static void Log(string message)
        {
            lock (Sync)
            {
                if (writer == null)
                    return;

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                writer.WriteLine($"[{timestamp}] {message}");
            }
        }
    }
}
