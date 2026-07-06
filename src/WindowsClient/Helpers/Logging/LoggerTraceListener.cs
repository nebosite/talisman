using System.Diagnostics;
using System.Text;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Bridges System.Diagnostics Trace/Debug output into the file logger, so the
    /// app's many existing Debug.WriteLine calls land in the log with no rewrite.
    ///
    /// Note: Debug.WriteLine is compiled out of Release builds, so in Release this
    /// only forwards Trace.WriteLine. New code that must be visible in production
    /// should call Log.* directly rather than relying on this bridge.
    /// </summary>
    // --------------------------------------------------------------------------
    public class LoggerTraceListener : TraceListener
    {
        readonly ILogger _logger;
        readonly StringBuilder _partial = new StringBuilder();

        public LoggerTraceListener(ILogger logger)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Trace.Write does not imply a line break, so buffer until WriteLine.
        /// </summary>
        // --------------------------------------------------------------------------
        public override void Write(string message)
        {
            _partial.Append(message);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Flush any buffered Write() text plus this message as one Trace line.
        /// </summary>
        // --------------------------------------------------------------------------
        public override void WriteLine(string message)
        {
            if (_partial.Length > 0)
            {
                message = _partial.ToString() + message;
                _partial.Clear();
            }
            _logger.Write(LogLevel.Debug, "[trace] " + message);
        }
    }
}
