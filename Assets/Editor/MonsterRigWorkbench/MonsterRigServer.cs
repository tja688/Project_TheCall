#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace TheCall.Editor
{
    [InitializeOnLoad]
    public static class MonsterRigServer
    {
        public const int ProtocolVersion = 1;
        public const int MaxPayloadBytes = 1024 * 1024;
        const int MinPort = 7961;
        const int MaxPort = 7970;
        const string TokenKey = "TheCall.MonsterRig.Token";
        const string PortKey = "TheCall.MonsterRig.Port";

        static readonly ConcurrentQueue<Action> Jobs = new ConcurrentQueue<Action>();
        static readonly object Gate = new object();
        static HttpListener listener;
        static CancellationTokenSource cts;
        static string token;
        static int port;
        static bool updateHooked;
        static int mainThreadId;

        static MonsterRigServer()
        {
            EditorApplication.quitting += Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
        }

        public static string LaunchUrl
        {
            get
            {
                EnsureStarted();
                return "http://127.0.0.1:" + port + "/#token=" + Uri.EscapeDataString(token);
            }
        }

        [MenuItem("The Call/怪物拼装工作区")]
        public static void Open()
        {
            Application.OpenURL(LaunchUrl);
        }

        public static void EnsureStarted()
        {
            lock (Gate)
            {
                if (listener != null && listener.IsListening)
                    return;

                EnsureTokenAndPort();
                listener = new HttpListener();
                listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
                listener.Prefixes.Add("http://localhost:" + port + "/");
                listener.Start();
                cts = new CancellationTokenSource();
                if (!updateHooked)
                {
                    EditorApplication.update += Pump;
                    updateHooked = true;
                }

                var cancel = cts.Token;
                Task.Run(() => AcceptLoop(cancel), cancel);
            }
        }

        public static void Shutdown()
        {
            lock (Gate)
            {
                try { cts?.Cancel(); } catch (Exception) { }
                cts = null;
                try
                {
                    listener?.Stop();
                    listener?.Close();
                }
                catch (Exception) { }
                listener = null;
            }
        }

        static void EnsureTokenAndPort()
        {
            token = SessionState.GetString(TokenKey, "");
            if (string.IsNullOrEmpty(token))
            {
                var bytes = new byte[32];
                using (var rng = RandomNumberGenerator.Create())
                    rng.GetBytes(bytes);
                token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                SessionState.SetString(TokenKey, token);
            }

            port = SessionState.GetInt(PortKey, 0);
            if (port < MinPort || port > MaxPort || !CanListen(port))
            {
                port = MinPort;
                while (port <= MaxPort && !CanListen(port))
                    port += 1;
                if (port > MaxPort)
                    port = MinPort;
            }

            SessionState.SetInt(PortKey, port);
        }

        static bool CanListen(int candidate)
        {
            try
            {
                var probe = new HttpListener();
                probe.Prefixes.Add("http://127.0.0.1:" + candidate + "/");
                probe.Start();
                probe.Stop();
                probe.Close();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static void Pump()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            var count = 0;
            while (count < 32 && Jobs.TryDequeue(out var job))
            {
                try { job(); }
                catch (Exception error) { Debug.LogException(error); }
                count += 1;
            }
        }

        static T Run<T>(Func<T> function)
        {
            if (mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId == mainThreadId)
                return function();

            T result = default;
            Exception error = null;
            var done = new ManualResetEventSlim(false);
            Jobs.Enqueue(() =>
            {
                try { result = function(); }
                catch (Exception thrown) { error = thrown; }
                finally { done.Set(); }
            });
            if (!done.Wait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("主线程没有处理命令");

            if (error != null)
                throw error;

            return result;
        }

        static async Task AcceptLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    break;
                }

                _ = Task.Run(() => Handle(context), cancellationToken);
            }
        }

        static void Handle(HttpListenerContext context)
        {
            try
            {
                if (context.Request.RemoteEndPoint == null || !IPAddress.IsLoopback(context.Request.RemoteEndPoint.Address))
                {
                    WriteText(context, 403, "forbidden: non-loopback");
                    return;
                }

                var path = context.Request.Url == null ? "/" : context.Request.Url.AbsolutePath;
                if (path.IndexOf("..", StringComparison.Ordinal) >= 0 || path.IndexOf('\\') >= 0)
                {
                    WriteText(context, 400, "forbidden: path traversal");
                    return;
                }

                if (!AllowedHost(context.Request))
                {
                    WriteText(context, 403, "forbidden: host/origin");
                    return;
                }

                if (path == "/" || path.Equals("/index.html", StringComparison.OrdinalIgnoreCase))
                {
                    WriteStatic(context, "text/html; charset=utf-8", ReadWeb("index.html"));
                    return;
                }

                if (path.Equals("/styles.css", StringComparison.OrdinalIgnoreCase))
                {
                    WriteStatic(context, "text/css; charset=utf-8", ReadWeb("styles.css"));
                    return;
                }

                if (path.Equals("/app.js", StringComparison.OrdinalIgnoreCase))
                {
                    WriteStatic(context, "application/javascript; charset=utf-8", ReadWeb("app.js"));
                    return;
                }

                if (path.Equals("/stream", StringComparison.OrdinalIgnoreCase))
                {
                    if (!context.Request.IsWebSocketRequest)
                    {
                        WriteText(context, 501, "websocket unavailable; use /api/events");
                        return;
                    }

                    HandleSocket(context).GetAwaiter().GetResult();
                    return;
                }

                if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                {
                    WriteText(context, 404, "not found");
                    return;
                }

                if (!Authorized(context.Request))
                {
                    WriteText(context, 401, "unauthorized");
                    return;
                }

                if (path.Equals("/api/snapshot", StringComparison.OrdinalIgnoreCase))
                {
                    WriteJson(context, 200, Run(MonsterRigSession.SnapshotEnvelope));
                    return;
                }

                if (path.Equals("/api/events", StringComparison.OrdinalIgnoreCase))
                {
                    var after = 0L;
                    long.TryParse(context.Request.QueryString["afterRevision"], out after);
                    var timeout = 25000;
                    int.TryParse(context.Request.QueryString["timeoutMs"], out timeout);
                    timeout = Mathf.Clamp(timeout, 0, 25000);
                    var started = DateTime.UtcNow;
                    while ((DateTime.UtcNow - started).TotalMilliseconds < timeout && MonsterRigSession.Revision <= after)
                        Thread.Sleep(150);

                    WriteJson(context, 200, Run(MonsterRigSession.SnapshotEnvelope));
                    return;
                }

                if (path.Equals("/api/command", StringComparison.OrdinalIgnoreCase) && context.Request.HttpMethod == "POST")
                {
                    if (!TryBody(context, out var body, out var error))
                    {
                        WriteJson(context, 413, new { type = "commandResult", ok = false, error });
                        return;
                    }

                    WriteJson(context, 200, Run(() => MonsterRigSession.Dispatch(body)));
                    return;
                }

                if (path.Equals("/api/art", StringComparison.OrdinalIgnoreCase))
                {
                    var id = context.Request.QueryString["id"] ?? "";
                    var bytes = Run(() =>
                    {
                        var data = MonsterRigSession.ArtBytes(id, out var artError);
                        return new ArtResponse { Bytes = data, Error = artError };
                    });
                    if (bytes.Bytes == null)
                    {
                        WriteText(context, 404, bytes.Error ?? "missing");
                        return;
                    }

                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "image/png";
                    context.Response.Headers["Cache-Control"] = "no-store";
                    context.Response.ContentLength64 = bytes.Bytes.Length;
                    context.Response.OutputStream.Write(bytes.Bytes, 0, bytes.Bytes.Length);
                    context.Response.OutputStream.Close();
                    return;
                }

                WriteText(context, 404, "not found");
            }
            catch (Exception error)
            {
                try { WriteText(context, 500, error.Message); } catch (Exception) { }
            }
        }

        static async Task HandleSocket(HttpListenerContext context)
        {
            var socketContext = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
            var socket = socketContext.WebSocket;
            var buffer = new byte[8192];
            var message = await ReadMessage(socket, buffer).ConfigureAwait(false);
            if (message == null || message.IndexOf(token, StringComparison.Ordinal) < 0)
            {
                await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "unauthorized", CancellationToken.None).ConfigureAwait(false);
                return;
            }

            var seen = 0L;
            while (socket.State == WebSocketState.Open)
            {
                var envelope = Run(MonsterRigSession.SnapshotEnvelope);
                var revision = MonsterRigSession.Revision;
                if (revision != seen)
                {
                    var json = JsonConvert.SerializeObject(envelope);
                    var outgoing = Encoding.UTF8.GetBytes(json);
                    await socket.SendAsync(new ArraySegment<byte>(outgoing), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                    seen = revision;
                }

                await Task.Delay(200).ConfigureAwait(false);
            }
        }

        static async Task<string> ReadMessage(WebSocket socket, byte[] buffer)
        {
            var builder = new StringBuilder();
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                    return null;

                builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (result.EndOfMessage)
                    return builder.ToString();
            }

            return null;
        }

        static bool AllowedHost(HttpListenerRequest request)
        {
            var host = request.UserHostName ?? "";
            var ok = host.Equals("127.0.0.1:" + port, StringComparison.OrdinalIgnoreCase)
                || host.Equals("localhost:" + port, StringComparison.OrdinalIgnoreCase);
            if (!ok)
                return false;

            var origin = request.Headers["Origin"];
            if (string.IsNullOrEmpty(origin))
                return true;

            return origin.Equals("http://127.0.0.1:" + port, StringComparison.OrdinalIgnoreCase)
                || origin.Equals("http://localhost:" + port, StringComparison.OrdinalIgnoreCase);
        }

        static bool Authorized(HttpListenerRequest request)
        {
            var header = request.Headers["Authorization"] ?? "";
            const string prefix = "Bearer ";
            return header.StartsWith(prefix, StringComparison.Ordinal) && header.Substring(prefix.Length) == token;
        }

        static bool TryBody(HttpListenerContext context, out string body, out string error)
        {
            body = null;
            error = null;
            if (context.Request.ContentLength64 > MaxPayloadBytes)
            {
                error = "payload too large";
                return false;
            }

            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                body = reader.ReadToEnd();

            if (body != null && Encoding.UTF8.GetByteCount(body) > MaxPayloadBytes)
            {
                error = "payload too large";
                return false;
            }

            return true;
        }

        static string ReadWeb(string name)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "Assets/Editor/MonsterRigWorkbench/Web", name);
            return File.ReadAllText(path, Encoding.UTF8);
        }

        static void WriteStatic(HttpListenerContext context, string contentType, string body)
        {
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; connect-src 'self'; img-src 'self' blob:; script-src 'self'; style-src 'self'; object-src 'none'; base-uri 'none'";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Cache-Control"] = "no-store";
            WriteText(context, 200, body, contentType);
        }

        static void WriteJson(HttpListenerContext context, int status, object value)
        {
            WriteText(context, status, JsonConvert.SerializeObject(value), "application/json; charset=utf-8");
        }

        static void WriteText(HttpListenerContext context, int status, string body, string contentType = "text/plain; charset=utf-8")
        {
            var bytes = Encoding.UTF8.GetBytes(body ?? "");
            context.Response.StatusCode = status;
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.Close();
        }

        sealed class ArtResponse
        {
            public byte[] Bytes;
            public string Error;
        }
    }
}
#endif
