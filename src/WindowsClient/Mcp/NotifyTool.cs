using System;
using Newtonsoft.Json.Linq;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// The "notify" MCP tool: sends a push notification to the user's phone via
    /// Pushover's email gateway - an ordinary email (through Outlook) to the user's
    /// Pushover address, where the subject becomes the notification title and the
    /// body the message. Silent and reliable (no SMS, no stealing focus). The
    /// recipient can be passed per call, or fall back to the address saved in
    /// Talisman Settings. Arg parsing is separated from the send side effect so it
    /// can be unit-tested without Outlook.
    /// </summary>
    // --------------------------------------------------------------------------
    public static class NotifyTool
    {
        public const string Name = "notify";

        // --------------------------------------------------------------------------
        /// <summary>
        /// Build the tool definition. The default recipient email is read lazily (so
        /// a Settings change takes effect without restarting the server);
        /// <paramref name="send"/> is the side effect (to, title, message).
        /// </summary>
        // --------------------------------------------------------------------------
        public static McpToolDefinition Create(Func<string> getDefaultEmail, Action<string, string, string> send)
        {
            var schema = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["message"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "The notification body.",
                    },
                    ["title"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Optional short title, shown as the Pushover notification title. Defaults to \"Talisman\".",
                    },
                    ["to"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Optional recipient email (e.g. a Pushover email-gateway address). Omit to use the address saved in Talisman Settings.",
                    },
                },
                ["required"] = new JArray { "message" },
            };

            return new McpToolDefinition(
                Name,
                "Send a push notification to your phone via Pushover (email gateway through Outlook).",
                schema,
                args => Run(args, getDefaultEmail?.Invoke(), send));
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Validate/resolve arguments and, if good, send the notification.
        /// </summary>
        // --------------------------------------------------------------------------
        public static McpToolResult Run(JObject args, string defaultEmail, Action<string, string, string> send)
        {
            if (!TryResolve(args, defaultEmail, out var to, out var title, out var message, out var error))
                return McpToolResult.Error(error);

            try
            {
                send(to, title, message);
            }
            catch (Exception ex)
            {
                return McpToolResult.Error("Failed to send notification: " + ex.Message);
            }

            return McpToolResult.Ok($"Notification sent to {to}: {title}");
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Resolve the tool arguments (with the Settings fallback) into a recipient,
        /// title, and body. message is required; the recipient comes from the call
        /// or, when omitted, the saved default; the title defaults to "Talisman".
        /// </summary>
        // --------------------------------------------------------------------------
        public static bool TryResolve(
            JObject args, string defaultEmail, out string to, out string title, out string message, out string error)
        {
            to = null;
            title = null;
            message = null;
            error = null;

            message = (string)args?["message"];
            if (string.IsNullOrWhiteSpace(message)) { error = "message is required."; return false; }

            to = FirstNonBlank((string)args?["to"], defaultEmail);
            if (string.IsNullOrWhiteSpace(to))
            {
                error = "No recipient: pass 'to', or set a Pushover email in Talisman Settings.";
                return false;
            }
            to = to.Trim();

            var t = (string)args?["title"];
            title = string.IsNullOrWhiteSpace(t) ? "Talisman" : t.Trim();
            return true;
        }

        static string FirstNonBlank(string a, string b) =>
            !string.IsNullOrWhiteSpace(a) ? a : b;
    }
}
