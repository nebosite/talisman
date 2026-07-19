namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// The outcome of running an MCP tool: the text to return to the model and
    /// whether it represents an error (surfaced as the MCP result's isError flag,
    /// not a JSON-RPC protocol error).
    /// </summary>
    // --------------------------------------------------------------------------
    public class McpToolResult
    {
        public string Text { get; }
        public bool IsError { get; }

        public McpToolResult(string text, bool isError = false)
        {
            Text = text ?? "";
            IsError = isError;
        }

        public static McpToolResult Ok(string text) => new McpToolResult(text, false);
        public static McpToolResult Error(string text) => new McpToolResult(text, true);
    }
}
