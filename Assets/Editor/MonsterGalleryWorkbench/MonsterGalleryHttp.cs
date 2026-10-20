#if UNITY_EDITOR
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TheCall.Editor
{
    public static partial class MonsterGalleryWorkbench
    {
        static async Task AcceptLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync().ConfigureAwait(false); }
                catch (Exception) { break; }
                _ = Task.Run(() => HandleContext(context), cancellationToken);
            }
        }

        static void HandleContext(HttpListenerContext context)
        {
            try
            {
                if (!IPAddress.IsLoopback(context.Request.RemoteEndPoint.Address))
                {
                    WriteText(context, 403, "forbidden: non-loopback");
                    return;
                }

                var path = context.Request.Url?.AbsolutePath ?? "/";
                var raw = context.Request.RawUrl ?? "";
                if (path.IndexOf("..", StringComparison.Ordinal) >= 0 || path.IndexOf('\\') >= 0 ||
                    raw.IndexOf("..", StringComparison.Ordinal) >= 0 || raw.IndexOf('\\') >= 0)
                {
                    WriteText(context, 400, "forbidden: path traversal");
                    return;
                }

                if (!IsAllowedHost(context.Request))
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

                if (path.Equals("/reference.jpg", StringComparison.Ordinal))
                {
                    var bytes=File.ReadAllBytes(Path.Combine(_webRoot,"../../../示意图/怪物实例.jpg"));
                    context.Response.ContentType="image/jpeg";context.Response.Headers["Cache-Control"]="no-store";
                    context.Response.Headers["X-Content-Type-Options"]="nosniff";context.Response.ContentLength64=bytes.Length;
                    context.Response.OutputStream.Write(bytes,0,bytes.Length);context.Response.OutputStream.Close();return;
                }

                if (path.Equals("/stream", StringComparison.OrdinalIgnoreCase))
                {
                    if (!_sockets || !context.Request.IsWebSocketRequest)
                    {
                        WriteText(context, 501, "websocket unavailable; use /api/events");
                        return;
                    }

                    HandleWebSocket(context).GetAwaiter().GetResult();
                    return;
                }

                if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsAuthorized(context.Request))
                    {
                        WriteText(context, 401, "unauthorized");
                        return;
                    }

                    HandleApi(context);
                    return;
                }

                WriteText(context, 404, "not found");
            }
            catch (Exception exception)
            {
                try { WriteText(context, 500, exception.Message); } catch (Exception) { /* closed */ }
            }
        }

        static void HandleApi(HttpListenerContext context)
        {
            var path = context.Request.Url.AbsolutePath;
            var method = context.Request.HttpMethod ?? "GET";
            if (path.Equals("/api/snapshot", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                WriteText(context, 200, RunOnMainThread(SnapshotJson), "application/json; charset=utf-8");
                return;
            }

            if (path.Equals("/api/events", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                var after = QueryLong(context.Request, "afterRevision");
                var timeout = (int)Math.Min(30000, Math.Max(0, QueryLong(context.Request, "timeoutMs", 25000)));
                WriteText(context, 200, WaitForRevision(after, timeout).GetAwaiter().GetResult(), "application/json; charset=utf-8");
                return;
            }

            if (path.Equals("/api/command", StringComparison.OrdinalIgnoreCase) && method == "POST")
            {
                if (!TryReadBody(context, out var body, out var error))
                {
                    WriteText(context, 413, CommandResult("", false, error), "application/json; charset=utf-8");
                    return;
                }

                WriteText(context, 200, RunOnMainThread(() => Dispatch(body)), "application/json; charset=utf-8");
                return;
            }

            WriteText(context, 404, "not found");
        }

        static async Task HandleWebSocket(HttpListenerContext context)
        {
            var accepted = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
            var socket = accepted.WebSocket;
            var hello = await ReadMessage(socket, 5000).ConfigureAwait(false);
            string helloToken = null;
            string helloType = null;
            if (!string.IsNullOrEmpty(hello))
            {
                try
                {
                    var message = Newtonsoft.Json.Linq.JObject.Parse(hello);
                    helloType = message.Value<string>("type");
                    helloToken = message.Value<string>("token");
                }
                catch (Newtonsoft.Json.JsonException) { /* rejected below */ }
            }

            if (helloType != "hello" || helloToken != _token)
            {
                await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "unauthorized", CancellationToken.None).ConfigureAwait(false);
                return;
            }

            lock (Sockets) Sockets.Add(socket);
            try
            {
                await SendText(socket, RunOnMainThread(SnapshotJson)).ConfigureAwait(false);
                while (socket.State == WebSocketState.Open)
                {
                    var text = await ReadMessage(socket, 0).ConfigureAwait(false);
                    if (text == null) break;
                    if (text.Length == 0) continue;
                    await SendText(socket, RunOnMainThread(() => Dispatch(text))).ConfigureAwait(false);
                }
            }
            catch (Exception) { /* client left */ }
            finally
            {
                lock (Sockets) Sockets.Remove(socket);
                try { socket.Abort(); } catch (Exception) { /* closing */ }
            }
        }

        static bool IsAllowedHost(HttpListenerRequest request)
        {
            var host = request.UserHostName ?? "";
            if (!host.Equals("127.0.0.1:" + _port, StringComparison.OrdinalIgnoreCase) &&
                !host.Equals("localhost:" + _port, StringComparison.OrdinalIgnoreCase)) return false;
            var origin = request.Headers["Origin"];
            if (string.IsNullOrEmpty(origin)) return true;
            return origin.Equals("http://127.0.0.1:" + _port, StringComparison.OrdinalIgnoreCase)
                || origin.Equals("http://localhost:" + _port, StringComparison.OrdinalIgnoreCase);
        }

        static bool IsAuthorized(HttpListenerRequest request)
        {
            var header = request.Headers["Authorization"] ?? "";
            const string prefix = "Bearer ";
            return header.StartsWith(prefix, StringComparison.Ordinal) && header.Substring(prefix.Length) == _token;
        }

        static bool TryReadBody(HttpListenerContext context, out string body, out string error)
        {
            body = null;
            error = null;
            if (context.Request.ContentLength64 > MaxPayloadBytes)
            {
                error = "payload too large";
                return false;
            }

            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                var buffer = new char[4096];
                var builder = new StringBuilder();
                int read;
                while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    builder.Append(buffer, 0, read);
                    if (builder.Length > MaxPayloadBytes)
                    {
                        error = "payload too large";
                        return false;
                    }
                }

                body = builder.ToString();
            }

            return true;
        }

        static string ReadWeb(string name) => File.ReadAllText(Path.Combine(_webRoot, name), Encoding.UTF8);

        static void WriteStatic(HttpListenerContext context, string contentType, string body)
        {
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; connect-src 'self'; img-src 'self' data:; script-src 'self'; style-src 'self'; object-src 'none'; base-uri 'none'";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Cache-Control"] = "no-store";
            WriteText(context, 200, body, contentType);
        }

        static void WriteText(HttpListenerContext context, int status, string body, string contentType = "text/plain; charset=utf-8")
        {
            var bytes = Encoding.UTF8.GetBytes(body ?? "");
            context.Response.StatusCode = status;
            context.Response.ContentType = contentType;
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.OutputStream.Close();
        }

        static async Task<string> ReadMessage(WebSocket socket, int timeoutMs)
        {
            var buffer = new byte[8192];
            var builder = new StringBuilder();
            using (var cancel = timeoutMs > 0 ? new CancellationTokenSource(timeoutMs) : new CancellationTokenSource())
            {
                var token = timeoutMs > 0 ? cancel.Token : CancellationToken.None;
                while (socket.State == WebSocketState.Open)
                {
                    var read = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                    if (read.MessageType == WebSocketMessageType.Close) return null;
                    builder.Append(Encoding.UTF8.GetString(buffer, 0, read.Count));
                    if (read.EndOfMessage) return builder.ToString();
                    if (builder.Length > MaxPayloadBytes) return null;
                }
            }

            return null;
        }

        static Task SendText(WebSocket socket, string text) => SendBytes(socket, Encoding.UTF8.GetBytes(text ?? ""));

        static async Task SendBytes(WebSocket socket, byte[] bytes)
        {
            if (socket.State != WebSocketState.Open) return;
            try
            {
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception) { /* client left */ }
        }

        static long QueryLong(HttpListenerRequest request, string key, long fallback = 0)
        {
            var value = request.QueryString[key];
            return long.TryParse(value, out var parsed) ? parsed : fallback;
        }

        static (int status, string body) Http(string method, string path, string body, string token, string origin)
        {
            var request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + _port + path);
            request.Method = method;
            request.Timeout = 20000;
            if (!string.IsNullOrEmpty(token)) request.Headers["Authorization"] = "Bearer " + token;
            if (!string.IsNullOrEmpty(origin)) request.Headers["Origin"] = origin;
            if (body != null)
            {
                var bytes = Encoding.UTF8.GetBytes(body);
                request.ContentType = "application/json";
                request.ContentLength = bytes.Length;
                using (var stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
            }

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                    return ((int)response.StatusCode, reader.ReadToEnd());
            }
            catch (WebException exception) when (exception.Response is HttpWebResponse response)
            {
                using (response)
                using (var reader = new StreamReader(response.GetResponseStream()))
                    return ((int)response.StatusCode, reader.ReadToEnd());
            }
        }

        static (int status, string body) Raw(string head)
        {
            using (var client = new TcpClient("127.0.0.1", _port))
            using (var stream = client.GetStream())
            {
                var bytes = Encoding.ASCII.GetBytes(head);
                stream.Write(bytes, 0, bytes.Length);
                stream.ReadTimeout = 5000;
                var buffer = new byte[1024];
                var read = stream.Read(buffer, 0, buffer.Length);
                var text = Encoding.UTF8.GetString(buffer, 0, read);
                var status = text.StartsWith("HTTP/", StringComparison.Ordinal) ? int.Parse(text.Split(' ')[1]) : 0;
                return (status, text);
            }
        }

        static void Expect((int status, string body) response, int status, string name, System.Collections.Generic.List<string> failures)
        {
            if (response.status != status) failures.Add(name + " -> " + response.status + " " + Trim(response.body));
        }

        static string Trim(string value) => string.IsNullOrEmpty(value) ? "" : value.Substring(0, Math.Min(160, value.Length));
    }
}
#endif
