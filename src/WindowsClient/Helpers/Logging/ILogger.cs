using System;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Minimal logging abstraction. Kept small so it is trivial to implement,
    /// fake in tests, and call from anywhere via the static <see cref="Log"/>.
    /// </summary>
    // --------------------------------------------------------------------------
    public interface ILogger
    {
        // --------------------------------------------------------------------------
        /// <summary>
        /// Write a single message at the given level. An optional exception is
        /// appended with its type, message, and full stack trace.
        /// </summary>
        // --------------------------------------------------------------------------
        void Write(LogLevel level, string message, Exception ex = null);
    }

    // --------------------------------------------------------------------------
    /// <summary>
    /// Convenience helpers so callers can write log.Info("...") instead of
    /// spelling out the level every time.
    /// </summary>
    // --------------------------------------------------------------------------
    public static class LoggerExtensions
    {
        public static void Debug(this ILogger logger, string message) => logger?.Write(LogLevel.Debug, message);
        public static void Info(this ILogger logger, string message) => logger?.Write(LogLevel.Info, message);
        public static void Warn(this ILogger logger, string message, Exception ex = null) => logger?.Write(LogLevel.Warn, message, ex);
        public static void Error(this ILogger logger, string message, Exception ex = null) => logger?.Write(LogLevel.Error, message, ex);
        public static void Fatal(this ILogger logger, string message, Exception ex = null) => logger?.Write(LogLevel.Fatal, message, ex);
    }
}
