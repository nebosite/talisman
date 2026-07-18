using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// A tiny in-process MCP server over HTTP, bound to localhost. Speaks the
    /// request/response subset of the MCP "Streamable HTTP" transport: the client
    /// POSTs JSON-RPC to the single endpoint and gets a JSON-RPC reply. Kept
    /// dependency-free (HttpListener + Newtonsoft) and localhost-only.
    ///
    /// Protocol handling lives in <see cref="McpDispatcher"/>; this class is just
    /// the HTTP plumbing and the listener thread.
    /// </summary>
    // --------------------------------------------------------------------------
    public class McpServer
    {
        readonly int _port;
        readonly McpDispatcher _dispatcher;
        HttpListener _listener;
        Thread _thread;
        volatile bool _running;

        public string Url => $"http://127.0.0.1:{_port}/";

        public McpServer(McpDispatcher dispatcher, int port = 8765)
        {
            _dispatcher = dispatcher;
            _port = port;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Start listening on a background thread. Localhost binding does not need a
        /// URL ACL reservation. Never throws - logs and gives up on failure.
        /// </summary>
        // --------------------------------------------------------------------------
        public void Start()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(Url);
                _listener.Start();
                _running = true;

                _thread = new Thread(Listen) { IsBackground = true, Name = "McpServer" };
                _thread.Start();
                Log.Info("MCP server listening at " + Url);
            }
            catch (Exception ex)
            {
                Log.Error("MCP server failed to start on " + Url, ex);
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Stop listening.
        /// </summary>
        // --------------------------------------------------------------------------
        public void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
        }

        void Listen()
        {
            while (_running)
            {
                HttpListenerContext context;
                try
                {
                    context = _listener.GetContext();
                }
                catch (Exception)
                {
                    if (_running) continue; // transient; keep serving
                    return;                 // stopped
                }

                try { Handle(context); }
                catch (Exception ex) { Log.Warn("MCP request handling failed.", ex); }
            }
        }

        void Handle(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            response.Headers["Access-Control-Allow-Origin"] = "*";

            // Only POST carries JSON-RPC; GET (SSE stream) is not supported here.
            if (request.HttpMethod == "OPTIONS")
            {
                response.Headers["Access-Control-Allow-Methods"] = "POST, OPTIONS";
                response.Headers["Access-Control-Allow-Headers"] = "Content-Type, MCP-Protocol-Version, Mcp-Session-Id";
                WriteEmpty(response, 204);
                return;
            }
            if (request.HttpMethod != "POST")
            {
                WriteEmpty(response, 405);
                return;
            }

            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
                body = reader.ReadToEnd();

            JToken parsed;
            try { parsed = JToken.Parse(body); }
            catch (Exception)
            {
                WriteJson(response, 200, ParseError());
                return;
            }

            // A JSON-RPC batch is an array; a single call is an object.
            if (parsed is JArray batch)
            {
                var responses = new JArray();
                foreach (var item in batch)
                {
                    if (item is JObject obj)
                    {
                        var r = _dispatcher.Handle(obj);
                        if (r != null) responses.Add(r);
                    }
                }
                if (responses.Count == 0) WriteEmpty(response, 202);
                else WriteJson(response, 200, responses);
                return;
            }

            if (parsed is JObject single)
            {
                var r = _dispatcher.Handle(single);
                if (r == null) WriteEmpty(response, 202);
                else WriteJson(response, 200, r);
                return;
            }

            WriteJson(response, 200, ParseError());
        }

        static JObject ParseError() => new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = JValue.CreateNull(),
            ["error"] = new JObject { ["code"] = -32700, ["message"] = "Parse error" },
        };

        static void WriteJson(HttpListenerResponse response, int status, JToken payload)
        {
            var bytes = Encoding.UTF8.GetBytes(payload.ToString(Formatting.None));
            response.StatusCode = status;
            response.ContentType = "application/json";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }

        static void WriteEmpty(HttpListenerResponse response, int status)
        {
            response.StatusCode = status;
            response.ContentLength64 = 0;
            response.OutputStream.Close();
        }
    }
}
