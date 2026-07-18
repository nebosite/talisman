using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// The protocol brain of the MCP server: turns a single JSON-RPC 2.0 request
    /// into its response, implementing the minimal MCP method set needed for a
    /// request/response tool server (initialize, tools/list, tools/call, ping).
    ///
    /// Pure and transport-agnostic - unit-tested without any HTTP. Returns null
    /// for notifications (requests without an id), which the transport answers
    /// with an empty acknowledgement.
    /// </summary>
    // --------------------------------------------------------------------------
    public class McpDispatcher
    {
        // The newest protocol version we understand; we echo the client's requested
        // version when it sends one, for maximum compatibility.
        const string DefaultProtocolVersion = "2024-11-05";

        readonly IReadOnlyList<McpToolDefinition> _tools;
        readonly string _serverName;
        readonly string _serverVersion;

        public McpDispatcher(IReadOnlyList<McpToolDefinition> tools, string serverName, string serverVersion)
        {
            _tools = tools ?? new List<McpToolDefinition>();
            _serverName = serverName;
            _serverVersion = serverVersion;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Handle one JSON-RPC request object. Returns the response object, or null
        /// if the request was a notification (no id) that needs no reply.
        /// </summary>
        // --------------------------------------------------------------------------
        public JObject Handle(JObject request)
        {
            var method = (string)request["method"];
            var id = request["id"];
            var isNotification = id == null || id.Type == JTokenType.Null;

            switch (method)
            {
                case "initialize":
                    return Result(id, InitializeResult(request["params"] as JObject));

                case "initialized":
                case "notifications/initialized":
                    return null; // client handshake ack - nothing to return

                case "ping":
                    return Result(id, new JObject());

                case "tools/list":
                    return Result(id, ToolsListResult());

                case "tools/call":
                    return ToolsCall(id, request["params"] as JObject);

                default:
                    if (isNotification) return null; // ignore unknown notifications
                    return Error(id, -32601, "Method not found: " + method);
            }
        }

        JObject InitializeResult(JObject parameters)
        {
            var protocolVersion = (string)parameters?["protocolVersion"] ?? DefaultProtocolVersion;
            return new JObject
            {
                ["protocolVersion"] = protocolVersion,
                ["capabilities"] = new JObject { ["tools"] = new JObject() },
                ["serverInfo"] = new JObject { ["name"] = _serverName, ["version"] = _serverVersion },
            };
        }

        JObject ToolsListResult()
        {
            var tools = new JArray();
            foreach (var tool in _tools)
            {
                tools.Add(new JObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["inputSchema"] = tool.InputSchema,
                });
            }
            return new JObject { ["tools"] = tools };
        }

        JObject ToolsCall(JToken id, JObject parameters)
        {
            var name = (string)parameters?["name"];
            var tool = _tools.FirstOrDefault(t => t.Name == name);
            if (tool == null) return Error(id, -32602, "Unknown tool: " + name);

            var arguments = parameters?["arguments"] as JObject ?? new JObject();

            McpToolResult result;
            try
            {
                result = tool.Invoke(arguments);
            }
            catch (Exception ex)
            {
                result = McpToolResult.Error("Tool threw: " + ex.Message);
            }

            return Result(id, new JObject
            {
                ["content"] = new JArray { new JObject { ["type"] = "text", ["text"] = result.Text } },
                ["isError"] = result.IsError,
            });
        }

        static JObject Result(JToken id, JObject result) => new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id ?? JValue.CreateNull(),
            ["result"] = result,
        };

        static JObject Error(JToken id, int code, string message) => new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id ?? JValue.CreateNull(),
            ["error"] = new JObject { ["code"] = code, ["message"] = message },
        };
    }
}
