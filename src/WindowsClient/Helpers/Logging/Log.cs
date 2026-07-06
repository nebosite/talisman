using System;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Ambient access point for logging so any class can log without threading a
    /// logger reference through every constructor. Call <see cref="Initialize"/>
    /// once at startup; until then (and in tests) writes go to a NullLogger.
    /// </summary>
    // --------------------------------------------------------------------------
    public static class Log
    {
        static ILogger _current = NullLogger.Instance;

        // --------------------------------------------------------------------------
        /// <summary>
        /// The active logger. Never null.
        /// </summary>
        // --------------------------------------------------------------------------
        public static ILogger Current => _current;

        // --------------------------------------------------------------------------
        /// <summary>
        /// Point the ambient logger at a real implementation. Passing null resets
        /// to the NullLogger.
        /// </summary>
        // --------------------------------------------------------------------------
        public static void Initialize(ILogger logger)
        {
            _current = logger ?? NullLogger.Instance;
        }

        public static void Debug(string message) => _current.Write(LogLevel.Debug, message);
        public static void Info(string message) => _current.Write(LogLevel.Info, message);
        public static void Warn(string message, Exception ex = null) => _current.Write(LogLevel.Warn, message, ex);
        public static void Error(string message, Exception ex = null) => _current.Write(LogLevel.Error, message, ex);
        public static void Fatal(string message, Exception ex = null) => _current.Write(LogLevel.Fatal, message, ex);
    }
}
