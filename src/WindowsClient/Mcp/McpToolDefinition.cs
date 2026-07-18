using System;
using Newtonsoft.Json.Linq;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// One MCP tool: its name, description, JSON-Schema for arguments, and the
    /// handler that runs it. Bundling the handler with the definition keeps the
    /// dispatcher tool-agnostic.
    /// </summary>
    // --------------------------------------------------------------------------
    public class McpToolDefinition
    {
        public string Name { get; }
        public string Description { get; }
        public JObject InputSchema { get; }
        public Func<JObject, McpToolResult> Invoke { get; }

        public McpToolDefinition(string name, string description, JObject inputSchema, Func<JObject, McpToolResult> invoke)
        {
            Name = name;
            Description = description;
            InputSchema = inputSchema;
            Invoke = invoke;
        }
    }
}
