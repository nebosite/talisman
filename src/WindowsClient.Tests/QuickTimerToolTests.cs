using System;
using Newtonsoft.Json.Linq;
using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for the quicktimer MCP tool: argument validation and the happy path
    /// that fires the timer callback.
    /// </summary>
    // --------------------------------------------------------------------------
    public class QuickTimerToolTests
    {
        [Fact]
        public void TryParse_ValidArgs_ParsesEndTimeTitleAndBody()
        {
            var args = new JObject
            {
                ["endTime"] = "2026-07-18T15:30:00",
                ["title"] = "  Stand up  ",
                ["body"] = "join https://example.com/meet",
            };

            var ok = QuickTimerTool.TryParse(args, out var endTime, out var title, out var body, out var error);

            Assert.True(ok);
            Assert.Null(error);
            Assert.Equal(new DateTime(2026, 7, 18, 15, 30, 0), endTime);
            Assert.Equal("Stand up", title);
            Assert.Equal("join https://example.com/meet", body);
        }

        [Fact]
        public void TryParse_MissingEndTime_Fails()
        {
            var args = new JObject { ["title"] = "x" };
            var ok = QuickTimerTool.TryParse(args, out _, out _, out _, out var error);
            Assert.False(ok);
            Assert.Contains("endTime", error);
        }

        [Fact]
        public void TryParse_BadEndTime_Fails()
        {
            var args = new JObject { ["endTime"] = "not-a-date", ["title"] = "x" };
            var ok = QuickTimerTool.TryParse(args, out _, out _, out _, out var error);
            Assert.False(ok);
            Assert.Contains("not a valid date-time", error);
        }

        [Fact]
        public void TryParse_MissingTitle_Fails()
        {
            var args = new JObject { ["endTime"] = "2026-07-18T15:30:00" };
            var ok = QuickTimerTool.TryParse(args, out _, out _, out _, out var error);
            Assert.False(ok);
            Assert.Contains("title", error);
        }

        [Fact]
        public void TryParse_BodyOptional_DefaultsNull()
        {
            var args = new JObject { ["endTime"] = "2026-07-18T15:30:00", ["title"] = "x" };
            var ok = QuickTimerTool.TryParse(args, out _, out _, out var body, out _);
            Assert.True(ok);
            Assert.Null(body);
        }

        [Fact]
        public void Run_ValidArgs_FiresCallback_AndReturnsOk()
        {
            DateTime? gotEnd = null;
            string gotTitle = null, gotBody = null;
            var args = new JObject
            {
                ["endTime"] = "2026-07-18T15:30:00",
                ["title"] = "Call Bob",
                ["body"] = "notes",
            };

            var result = QuickTimerTool.Run(args, (e, t, b) => { gotEnd = e; gotTitle = t; gotBody = b; });

            Assert.False(result.IsError);
            Assert.Contains("Call Bob", result.Text);
            Assert.Equal(new DateTime(2026, 7, 18, 15, 30, 0), gotEnd);
            Assert.Equal("Call Bob", gotTitle);
            Assert.Equal("notes", gotBody);
        }

        [Fact]
        public void Run_InvalidArgs_DoesNotFireCallback_AndReturnsError()
        {
            var fired = false;
            var args = new JObject { ["title"] = "no end time" };

            var result = QuickTimerTool.Run(args, (e, t, b) => fired = true);

            Assert.True(result.IsError);
            Assert.False(fired);
        }

        [Fact]
        public void Create_BuildsDefinitionWithNameAndRequiredFields()
        {
            var tool = QuickTimerTool.Create((e, t, b) => { });

            Assert.Equal("quicktimer", tool.Name);
            var required = (JArray)tool.InputSchema["required"];
            Assert.Contains("endTime", required.ToObject<string[]>());
            Assert.Contains("title", required.ToObject<string[]>());
        }
    }
}
