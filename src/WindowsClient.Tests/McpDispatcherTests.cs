using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for the MCP JSON-RPC dispatcher: the handshake, tool listing, tool
    /// invocation (success, error, unknown), and notification handling.
    /// </summary>
    // --------------------------------------------------------------------------
    public class McpDispatcherTests
    {
        static McpDispatcher MakeDispatcher(McpToolDefinition tool = null)
        {
            var tools = new List<McpToolDefinition>();
            if (tool != null) tools.Add(tool);
            return new McpDispatcher(tools, "talisman", "1.2.3");
        }

        static McpToolDefinition EchoTool(string name = "echo") =>
            new McpToolDefinition(name, "echoes", new JObject { ["type"] = "object" },
                args => McpToolResult.Ok("got:" + (string)args["value"]));

        static JObject Request(string method, object id = null, JObject parameters = null)
        {
            var r = new JObject { ["jsonrpc"] = "2.0", ["method"] = method };
            if (id != null) r["id"] = JToken.FromObject(id);
            if (parameters != null) r["params"] = parameters;
            return r;
        }

        [Fact]
        public void Initialize_ReturnsCapabilitiesAndEchoesProtocolVersion()
        {
            var d = MakeDispatcher();
            var resp = d.Handle(Request("initialize", id: 1,
                parameters: new JObject { ["protocolVersion"] = "2025-06-18" }));

            Assert.Equal("2.0", (string)resp["jsonrpc"]);
            Assert.Equal(1, (int)resp["id"]);
            Assert.Equal("2025-06-18", (string)resp["result"]["protocolVersion"]);
            Assert.NotNull(resp["result"]["capabilities"]["tools"]);
            Assert.Equal("talisman", (string)resp["result"]["serverInfo"]["name"]);
            Assert.Equal("1.2.3", (string)resp["result"]["serverInfo"]["version"]);
        }

        [Fact]
        public void InitializedNotification_ReturnsNull()
        {
            var d = MakeDispatcher();
            Assert.Null(d.Handle(Request("notifications/initialized")));
        }

        [Fact]
        public void Ping_ReturnsEmptyResult()
        {
            var d = MakeDispatcher();
            var resp = d.Handle(Request("ping", id: 5));
            Assert.Equal(5, (int)resp["id"]);
            Assert.NotNull(resp["result"]);
        }

        [Fact]
        public void ToolsList_ReturnsRegisteredTools()
        {
            var d = MakeDispatcher(EchoTool());
            var resp = d.Handle(Request("tools/list", id: 2));

            var tools = (JArray)resp["result"]["tools"];
            Assert.Single(tools);
            Assert.Equal("echo", (string)tools[0]["name"]);
            Assert.Equal("echoes", (string)tools[0]["description"]);
            Assert.NotNull(tools[0]["inputSchema"]);
        }

        [Fact]
        public void ToolsCall_InvokesTool_AndReturnsContent()
        {
            var d = MakeDispatcher(EchoTool());
            var resp = d.Handle(Request("tools/call", id: 3, parameters: new JObject
            {
                ["name"] = "echo",
                ["arguments"] = new JObject { ["value"] = "hi" },
            }));

            var content = (JArray)resp["result"]["content"];
            Assert.Equal("text", (string)content[0]["type"]);
            Assert.Equal("got:hi", (string)content[0]["text"]);
            Assert.False((bool)resp["result"]["isError"]);
        }

        [Fact]
        public void ToolsCall_UnknownTool_ReturnsInvalidParams()
        {
            var d = MakeDispatcher(EchoTool());
            var resp = d.Handle(Request("tools/call", id: 4, parameters: new JObject { ["name"] = "nope" }));

            Assert.Equal(-32602, (int)resp["error"]["code"]);
        }

        [Fact]
        public void ToolsCall_ToolThrows_ReturnsIsError()
        {
            var throwing = new McpToolDefinition("boom", "throws", new JObject(),
                args => throw new System.InvalidOperationException("kaboom"));
            var d = MakeDispatcher(throwing);

            var resp = d.Handle(Request("tools/call", id: 6, parameters: new JObject { ["name"] = "boom" }));

            Assert.True((bool)resp["result"]["isError"]);
            Assert.Contains("kaboom", (string)resp["result"]["content"][0]["text"]);
        }

        [Fact]
        public void UnknownMethod_WithId_ReturnsMethodNotFound()
        {
            var d = MakeDispatcher();
            var resp = d.Handle(Request("does/notExist", id: 7));
            Assert.Equal(-32601, (int)resp["error"]["code"]);
        }

        [Fact]
        public void UnknownNotification_WithoutId_ReturnsNull()
        {
            var d = MakeDispatcher();
            Assert.Null(d.Handle(Request("some/notification")));
        }
    }
}
