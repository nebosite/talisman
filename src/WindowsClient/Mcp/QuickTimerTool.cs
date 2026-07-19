using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// The "quicktimer" MCP tool: sets a Talisman timer that floats a
    /// hard-to-ignore reminder at a given time. Arg parsing is separated from the
    /// side effect so it can be unit-tested.
    /// </summary>
    // --------------------------------------------------------------------------
    public static class QuickTimerTool
    {
        public const string Name = "quicktimer";

        // --------------------------------------------------------------------------
        /// <summary>
        /// Build the tool definition. <paramref name="setTimer"/> is the side effect
        /// (endTime, title, body) - usually a Dispatcher-marshaled call into AppModel.
        /// </summary>
        // --------------------------------------------------------------------------
        public static McpToolDefinition Create(Action<DateTime, string, string> setTimer)
        {
            var schema = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["endTime"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "When the timer should go off, as an ISO-8601 date-time (e.g. 2026-07-18T15:30:00).",
                    },
                    ["title"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Short title shown on the reminder.",
                    },
                    ["body"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Optional details. Any URL in here becomes a clickable link on the reminder.",
                    },
                },
                ["required"] = new JArray { "endTime", "title" },
            };

            return new McpToolDefinition(
                Name,
                "Set a Talisman timer that floats a hard-to-ignore reminder at a given time.",
                schema,
                args => Run(args, setTimer));
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Validate arguments and, if good, fire the timer.
        /// </summary>
        // --------------------------------------------------------------------------
        public static McpToolResult Run(JObject args, Action<DateTime, string, string> setTimer)
        {
            if (!TryParse(args, out var endTime, out var title, out var body, out var error))
                return McpToolResult.Error(error);

            setTimer(endTime, title, body);
            return McpToolResult.Ok($"Timer set for {endTime:yyyy-MM-dd HH:mm}: {title}");
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Parse and validate the tool arguments. endTime and title are required.
        /// </summary>
        // --------------------------------------------------------------------------
        public static bool TryParse(JObject args, out DateTime endTime, out string title, out string body, out string error)
        {
            endTime = default;
            title = null;
            body = null;
            error = null;

            var endTimeRaw = (string)args?["endTime"];
            if (string.IsNullOrWhiteSpace(endTimeRaw)) { error = "endTime is required."; return false; }
            if (!DateTime.TryParse(endTimeRaw, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces, out endTime))
            {
                error = $"endTime '{endTimeRaw}' is not a valid date-time.";
                return false;
            }

            title = (string)args?["title"];
            if (string.IsNullOrWhiteSpace(title)) { error = "title is required."; return false; }
            title = title.Trim();

            body = (string)args?["body"];
            return true;
        }
    }
}
