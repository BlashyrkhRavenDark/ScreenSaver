using System;
using System.IO;
using System.Text;
using System.Threading;

namespace ScreenSaver
{
    /// <summary>
    /// Append-only log to %LOCALAPPDATA%\ScreenSaver\screensaver.log so we can
    /// post-mortem screensaver failures. The .scr has no console, no stderr,
    /// and Windows takes over the desktop before any error dialog could be seen.
    /// </summary>
    internal static class DiagLog
    {
        private static string s_path;
        private static readonly object s_lock = new object();

        public static void Init()
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ScreenSaver");
                Directory.CreateDirectory(dir);
                s_path = Path.Combine(dir, "screensaver.log");
                // Truncate at boot - we only care about the most recent run.
                // (Use FileStream with FileMode.Create to overwrite cleanly.)
                using (var fs = new FileStream(s_path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                using (var w = new StreamWriter(fs, Encoding.UTF8))
                {
                    w.WriteLine($"=== ScreenSaver log opened {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} (PID {Environment.ProcessId}) ===");
                }
            }
            catch
            {
                s_path = null;
            }
        }

        public static void Write(string message)
        {
            if (s_path == null) return;
            try
            {
                lock (s_lock)
                {
                    using (var fs = new FileStream(s_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    using (var w = new StreamWriter(fs, Encoding.UTF8))
                    {
                        w.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Thread.CurrentThread.ManagedThreadId}] {message}");
                    }
                }
            }
            catch { }
        }

        public static void WriteError(string label, Exception ex)
        {
            if (ex == null) { Write(label + ": (null exception)"); return; }
            var sb = new StringBuilder();
            sb.Append(label).Append(": ").Append(ex.GetType().FullName).Append(": ").Append(ex.Message);
            if (ex.StackTrace != null) sb.Append("\n").Append(ex.StackTrace);
            Write(sb.ToString());
        }
    }
}
