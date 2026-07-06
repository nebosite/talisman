using System;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// A logger that discards everything. Used as the default target before the
    /// real logger is initialized, and in tests that do not care about output.
    /// </summary>
    // --------------------------------------------------------------------------
    public class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new NullLogger();
        public void Write(LogLevel level, string message, Exception ex = null) { }
    }
}
