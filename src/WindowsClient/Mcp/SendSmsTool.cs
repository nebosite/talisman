using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// The "sendsms" MCP tool: sends a text message through Microsoft Phone Link
    /// (relayed via the user's connected phone). The recipient number can be passed
    /// per call, or fall back to a default saved in Talisman Settings. Argument
    /// parsing / number normalization is separated from the send side effect (the
    /// actual Phone Link automation) so it can be unit-tested without any UI.
    /// </summary>
    // --------------------------------------------------------------------------
    public static class SendSmsTool
    {
        public const string Name = "sendsms";

        // --------------------------------------------------------------------------
        /// <summary>
        /// Build the tool definition. The default number is read lazily (so a
        /// Settings change takes effect without restarting the server);
        /// <paramref name="send"/> is the side effect (number, message).
        /// </summary>
        // --------------------------------------------------------------------------
        public static McpToolDefinition Create(Func<string> getDefaultNumber, Action<string, string> send)
        {
            var schema = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["message"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "The text message body to send.",
                    },
                    ["to"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Recipient US mobile number (10 digits; punctuation and a leading 1 are ignored). Omit to use the number saved in Talisman Settings.",
                    },
                },
                ["required"] = new JArray { "message" },
            };

            return new McpToolDefinition(
                Name,
                "Send an SMS text message through Phone Link (via your connected phone).",
                schema,
                args => Run(args, getDefaultNumber?.Invoke(), send));
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Validate/resolve arguments and, if good, send the text.
        /// </summary>
        // --------------------------------------------------------------------------
        public static McpToolResult Run(JObject args, string defaultNumber, Action<string, string> send)
        {
            if (!TryResolve(args, defaultNumber, out var number, out var message, out var error))
                return McpToolResult.Error(error);

            try
            {
                send(number, message);
            }
            catch (Exception ex)
            {
                return McpToolResult.Error("Failed to send text: " + ex.Message);
            }

            return McpToolResult.Ok($"Text sent to {number} via Phone Link.");
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Resolve the tool arguments (with the Settings fallback) into a normalized
        /// recipient number and the message body. message is required; the number
        /// comes from the call or, when omitted, the saved default.
        /// </summary>
        // --------------------------------------------------------------------------
        public static bool TryResolve(
            JObject args, string defaultNumber, out string number, out string message, out string error)
        {
            number = null;
            message = null;
            error = null;

            message = (string)args?["message"];
            if (string.IsNullOrWhiteSpace(message)) { error = "message is required."; return false; }

            var raw = FirstNonBlank((string)args?["to"], defaultNumber);
            if (string.IsNullOrWhiteSpace(raw))
            {
                error = "No recipient number: pass 'to', or set a default number in Talisman Settings.";
                return false;
            }

            return TryNormalizeNumber(raw, out number, out error);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Reduce a phone number to 10 US digits, ignoring punctuation and a leading
        /// country code (+1 / 1). Fails if it isn't 10 digits.
        /// </summary>
        // --------------------------------------------------------------------------
        public static bool TryNormalizeNumber(string raw, out string tenDigits, out string error)
        {
            tenDigits = null;
            error = null;

            var digits = new string((raw ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length == 11 && digits[0] == '1') digits = digits.Substring(1);

            if (digits.Length != 10)
            {
                error = $"'{raw}' is not a valid 10-digit US mobile number.";
                return false;
            }

            tenDigits = digits;
            return true;
        }

        static string FirstNonBlank(string a, string b) =>
            !string.IsNullOrWhiteSpace(a) ? a : b;
    }
}
