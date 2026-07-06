using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Thread-safe logger that appends timestamped lines to a daily rolling file.
    /// Defaults to %LOCALAPPDATA%\Talisman\logs so crashes in the wild leave a
    /// trail on disk that survives the process dying.
    /// </summary>
    // --------------------------------------------------------------------------
    public class FileLogger : ILogger
    {
        readonly object _writeLock = new object();
        readonly int _retentionDays;

        // --------------------------------------------------------------------------
        /// <summary>
        /// Folder that log files are written to.
        /// </summary>
        // --------------------------------------------------------------------------
        public string LogDirectory { get; }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Messages below this level are dropped. Defaults to Debug (log all).
        /// </summary>
        // --------------------------------------------------------------------------
        public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

        // --------------------------------------------------------------------------
        /// <summary>
        /// The default log folder: %LOCALAPPDATA%\Talisman\logs.
        /// </summary>
        // --------------------------------------------------------------------------
        public static string DefaultLogDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Talisman",
                "logs");

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor. Creates the log directory and prunes files older than the
        /// retention window.
        /// </summary>
        // --------------------------------------------------------------------------
        public FileLogger(string logDirectory = null, int retentionDays = 14)
        {
            LogDirectory = string.IsNullOrWhiteSpace(logDirectory) ? DefaultLogDirectory : logDirectory;
            _retentionDays = retentionDays;
            Directory.CreateDirectory(LogDirectory);
            PruneOldLogs();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Full path of the log file for the current day.
        /// </summary>
        // --------------------------------------------------------------------------
        public string CurrentLogFile =>
            Path.Combine(LogDirectory, $"talisman-{DateTime.Now:yyyy-MM-dd}.log");

        // --------------------------------------------------------------------------
        /// <summary>
        /// Format and append a log line. Never throws: logging must not be able to
        /// bring down the app, so I/O failures are swallowed to the debugger.
        /// </summary>
        // --------------------------------------------------------------------------
        public void Write(LogLevel level, string message, Exception ex = null)
        {
            if (level < MinimumLevel) return;

            var line = Format(level, message, ex);
            try
            {
                lock (_writeLock)
                {
                    File.AppendAllText(CurrentLogFile, line, Encoding.UTF8);
                }
            }
            catch (Exception writeError)
            {
                // Last resort - if we cannot write the log, there is nowhere good
                // left to report it. Surface to any attached debugger and move on.
                System.Diagnostics.Debug.WriteLine("FileLogger failed to write: " + writeError);
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Build one log line: "2026-07-06 14:30:00.123 [ERROR] message" followed
        /// by the exception detail (type, message, stack) on subsequent lines.
        /// </summary>
        // --------------------------------------------------------------------------
        internal static string Format(LogLevel level, string message, Exception ex)
        {
            var builder = new StringBuilder();
            builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            builder.Append(" [").Append(level.ToString().ToUpperInvariant().PadRight(5)).Append("] ");
            builder.Append(message);
            builder.AppendLine();

            if (ex != null)
            {
                builder.Append(ex.GetType().FullName).Append(": ").AppendLine(ex.Message);
                if (ex.StackTrace != null) builder.AppendLine(ex.StackTrace);

                var inner = ex.InnerException;
                while (inner != null)
                {
                    builder.Append("  ---> ").Append(inner.GetType().FullName).Append(": ").AppendLine(inner.Message);
                    if (inner.StackTrace != null) builder.AppendLine(inner.StackTrace);
                    inner = inner.InnerException;
                }
            }

            return builder.ToString();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Return the tail of the most recently written log file, for including in a
        /// crash report. Never throws - returns empty string on any problem.
        /// </summary>
        // --------------------------------------------------------------------------
        public string ReadRecentLines(int maxLines = 300)
        {
            try
            {
                var newest = Directory
                    .GetFiles(LogDirectory, "talisman-*.log")
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();
                if (newest == null) return "";

                var lines = File.ReadAllLines(newest);
                var start = Math.Max(0, lines.Length - maxLines);
                return string.Join(Environment.NewLine, lines.Skip(start));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("FileLogger ReadRecentLines failed: " + ex);
                return "";
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Delete log files older than the retention window so the folder does not
        /// grow without bound.
        /// </summary>
        // --------------------------------------------------------------------------
        void PruneOldLogs()
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-_retentionDays);
                var oldFiles = Directory
                    .GetFiles(LogDirectory, "talisman-*.log")
                    .Where(f => File.GetLastWriteTime(f) < cutoff);

                foreach (var file in oldFiles)
                {
                    try { File.Delete(file); }
                    catch { /* a locked/removed file should not stop startup */ }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("FileLogger prune failed: " + ex);
            }
        }
    }
}
