using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for FileLogger: line formatting, file creation, level filtering,
    /// retention pruning, and concurrent-write safety. Each test uses its own
    /// throwaway directory so runs don't collide.
    /// </summary>
    // --------------------------------------------------------------------------
    public class FileLoggerTests : IDisposable
    {
        readonly string _dir;

        public FileLoggerTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "TalismanLogTests", Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
            catch { /* best effort cleanup */ }
        }

        string ReadAllLogText()
        {
            return Directory.GetFiles(_dir, "talisman-*.log")
                .Select(File.ReadAllText)
                .Aggregate("", (a, b) => a + b);
        }

        [Fact]
        public void Constructor_CreatesLogDirectory()
        {
            var logger = new FileLogger(_dir);
            Assert.True(Directory.Exists(logger.LogDirectory));
            Assert.Equal(_dir, logger.LogDirectory);
        }

        [Fact]
        public void Write_CreatesFileAndRecordsMessage()
        {
            var logger = new FileLogger(_dir);
            logger.Write(LogLevel.Info, "hello world");

            var text = ReadAllLogText();
            Assert.Contains("hello world", text);
            Assert.Contains("[INFO ]", text);
        }

        [Fact]
        public void Write_IncludesExceptionTypeMessageAndStack()
        {
            var logger = new FileLogger(_dir);
            Exception captured;
            try { throw new InvalidOperationException("boom"); }
            catch (Exception ex) { captured = ex; }

            logger.Write(LogLevel.Error, "it failed", captured);

            var text = ReadAllLogText();
            Assert.Contains("it failed", text);
            Assert.Contains("System.InvalidOperationException", text);
            Assert.Contains("boom", text);
            Assert.Contains(nameof(Write_IncludesExceptionTypeMessageAndStack), text); // stack frame
        }

        [Fact]
        public void Format_IncludesInnerExceptionChain()
        {
            var inner = new ArgumentNullException("param");
            var outer = new InvalidOperationException("outer failed", inner);

            var line = FileLogger.Format(LogLevel.Fatal, "wrapped", outer);

            Assert.Contains("[FATAL]", line);
            Assert.Contains("outer failed", line);
            Assert.Contains("--->", line);
            Assert.Contains("System.ArgumentNullException", line);
        }

        [Fact]
        public void Write_RespectsMinimumLevel()
        {
            var logger = new FileLogger(_dir) { MinimumLevel = LogLevel.Warn };
            logger.Write(LogLevel.Debug, "should be dropped");
            logger.Write(LogLevel.Info, "also dropped");
            logger.Write(LogLevel.Error, "should be kept");

            var text = ReadAllLogText();
            Assert.DoesNotContain("should be dropped", text);
            Assert.DoesNotContain("also dropped", text);
            Assert.Contains("should be kept", text);
        }

        [Fact]
        public void Constructor_PrunesLogsOlderThanRetention()
        {
            // Seed an old log file and a recent one, then construct with a 14 day window.
            Directory.CreateDirectory(_dir);
            var oldFile = Path.Combine(_dir, "talisman-2000-01-01.log");
            var recentFile = Path.Combine(_dir, "talisman-recent.log");
            File.WriteAllText(oldFile, "old");
            File.WriteAllText(recentFile, "recent");
            File.SetLastWriteTime(oldFile, DateTime.Now.AddDays(-30));

            var unused = new FileLogger(_dir, retentionDays: 14);

            Assert.False(File.Exists(oldFile));
            Assert.True(File.Exists(recentFile));
        }

        [Fact]
        public void Write_IsThreadSafeUnderConcurrency()
        {
            var logger = new FileLogger(_dir);

            Parallel.For(0, 200, i => logger.Write(LogLevel.Info, "line " + i));

            var lineCount = ReadAllLogText()
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Count(l => l.Contains("line "));
            Assert.Equal(200, lineCount);
        }
    }
}
