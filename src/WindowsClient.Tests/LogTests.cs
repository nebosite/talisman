using System;
using System.Collections.Generic;
using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for the static Log accessor and the LoggerTraceListener bridge.
    /// </summary>
    // --------------------------------------------------------------------------
    public class LogTests : IDisposable
    {
        class RecordingLogger : ILogger
        {
            public readonly List<(LogLevel Level, string Message, Exception Ex)> Entries
                = new List<(LogLevel, string, Exception)>();

            public void Write(LogLevel level, string message, Exception ex = null)
                => Entries.Add((level, message, ex));
        }

        public void Dispose()
        {
            // Leave the ambient logger in a clean state for other tests.
            Log.Initialize(null);
        }

        [Fact]
        public void Current_DefaultsToNonNullLogger()
        {
            Log.Initialize(null);
            Assert.NotNull(Log.Current);
        }

        [Fact]
        public void Initialize_RoutesCallsToProvidedLogger()
        {
            var recorder = new RecordingLogger();
            Log.Initialize(recorder);

            Log.Info("info message");
            Log.Error("error message", new Exception("nope"));

            Assert.Equal(2, recorder.Entries.Count);
            Assert.Equal(LogLevel.Info, recorder.Entries[0].Level);
            Assert.Equal("info message", recorder.Entries[0].Message);
            Assert.Equal(LogLevel.Error, recorder.Entries[1].Level);
            Assert.NotNull(recorder.Entries[1].Ex);
        }

        [Fact]
        public void Initialize_Null_ResetsToNullLoggerWithoutThrowing()
        {
            Log.Initialize(null);
            var ex = Record.Exception(() => Log.Info("goes nowhere"));
            Assert.Null(ex);
        }

        [Fact]
        public void TraceListener_ForwardsWriteLineToLogger()
        {
            var recorder = new RecordingLogger();
            var listener = new LoggerTraceListener(recorder);

            listener.WriteLine("traced message");

            Assert.Single(recorder.Entries);
            Assert.Contains("traced message", recorder.Entries[0].Message);
        }

        [Fact]
        public void TraceListener_BuffersWriteUntilWriteLine()
        {
            var recorder = new RecordingLogger();
            var listener = new LoggerTraceListener(recorder);

            listener.Write("part1 ");
            listener.Write("part2");
            Assert.Empty(recorder.Entries); // nothing flushed yet

            listener.WriteLine("!");
            Assert.Single(recorder.Entries);
            Assert.Contains("part1 part2!", recorder.Entries[0].Message);
        }
    }
}
