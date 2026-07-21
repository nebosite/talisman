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

        /// <summary>
        /// How long to wait before re-trying a failed bind. The usual cause is a
        /// previous instance whose http.sys registration hasn't been released yet
        /// (e.g. after a forced close), which clears on its own within seconds.
        /// </summary>
        static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(15);

        // --------------------------------------------------------------------------
        /// <summary>
        /// Start serving on a background thread. Binding is supervised: if the port
        /// is temporarily unavailable, or the listener later dies, we keep retrying
        /// instead of leaving the server dead for the rest of the session.
        /// Never throws.
        /// </summary>
        // --------------------------------------------------------------------------
        public void Start()
        {
            if (_running) return;
            _running = true;

            // Release the http.sys registration on any managed process exit, so the
            // next launch doesn't collide with our own leftover registration.
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            _thread = new Thread(RunSupervised) { IsBackground = true, Name = "McpServer" };
            _thread.Start();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Stop listening and stop retrying.
        /// </summary>
        // --------------------------------------------------------------------------
        public void Stop()
        {
            _running = false;
            try { AppDomain.CurrentDomain.ProcessExit -= OnProcessExit; } catch { }

            var listener = _listener;
            _listener = null;
            try { listener?.Stop(); } catch { }
            try { listener?.Close(); } catch { }
        }

        void OnProcessExit(object sender, EventArgs e) => Stop();

        // --------------------------------------------------------------------------
        /// <summary>
        /// Bind, serve, and re-bind. A transient bind conflict or a listener that
        /// dies mid-session must not take the tool offline permanently.
        /// </summary>
        // --------------------------------------------------------------------------
        void RunSupervised()
        {
            var consecutiveFailures = 0;
            while (_running)
            {
                if (TryBind(ref consecutiveFailures))
                {
                    Listen(); // returns when the listener is stopped or breaks
                    if (_running) Log.Warn("MCP listener stopped unexpectedly; will rebind.");
                }

                if (!_running) break;
                Thread.Sleep(RetryDelay);
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Try to claim the port. Logs the first failure and then only occasionally,
        /// so a port permanently held by something else can't spam the log.
        /// </summary>
        // --------------------------------------------------------------------------
        bool TryBind(ref int consecutiveFailures)
        {
            try
            {
                var listener = new HttpListener();
                listener.Prefixes.Add(Url);
                listener.Start();
                _listener = listener;

                Log.Info(consecutiveFailures > 0
                    ? $"MCP server listening at {Url} (recovered after {consecutiveFailures} failed attempt(s))."
                    : $"MCP server listening at {Url}");
                consecutiveFailures = 0;
                return true;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
                if (consecutiveFailures == 1 || consecutiveFailures % 20 == 0)
                {
                    Log.Warn($"MCP server could not bind {Url} (attempt {consecutiveFailures}); " +
                             $"retrying every {RetryDelay.TotalSeconds:0}s.", ex);
                }
                return false;
            }
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
                    if (!_running) return;
                    // If the listener is gone/broken, hand back to the supervisor to
                    // rebind. Otherwise it was transient - pause briefly so a
                    // repeated failure can never become a tight spin.
                    var listener = _listener;
                    if (listener == null || !listener.IsListening) return;
                    Thread.Sleep(100);
                    continue;
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
