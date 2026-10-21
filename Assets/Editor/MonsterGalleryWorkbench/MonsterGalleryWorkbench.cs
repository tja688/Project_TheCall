#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace TheCall.Editor
{
    [InitializeOnLoad]
    public static partial class MonsterGalleryWorkbench
    {
        const string TokenSessionKey = "TheCall.MonsterGallery.Token.v1";
        const string PortSessionKey = "TheCall.MonsterGallery.Port.v1";
        const string RunningSessionKey = "TheCall.MonsterGallery.Running.v1";
        public const int ProtocolVersion = 1;
        public const int MaxPayloadBytes = 1024 * 1024;
        const int MinPort = 7940;
        const int MaxPort = 7969;
        const int MaxJobsPerTick = 32;

        static readonly int MainThreadId = Thread.CurrentThread.ManagedThreadId;
        static readonly ConcurrentQueue<Action> MainThreadJobs = new ConcurrentQueue<Action>();
        static readonly object Gate = new object();
        static readonly List<PollWait> Polls = new List<PollWait>();
        static readonly List<WebSocket> Sockets = new List<WebSocket>();

        static HttpListener _listener;
        static CancellationTokenSource _cts;
        static string _token;
        static int _port;
        static bool _sockets;
        static bool _updateHooked;
        static long _revision;
        static string _webRoot;

        static MonsterGalleryWorkbench()
        {
            EditorApplication.quitting += Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.delayCall += ResumeIfNeeded;
        }

        public static bool SupportsWebSockets => _sockets;

        public static string LaunchUrl
        {
            get
            {
                EnsureStarted();
                return "http://127.0.0.1:" + _port + "/#token=" + Uri.EscapeDataString(_token);
            }
        }

        [MenuItem("TheCall/Monster/打开立绘抽查台")]
        public static void Open()
        {
            SessionState.SetBool(RunningSessionKey, true);
            EnsureStarted();
            MonsterGallerySession.EnsureSeeded();
            Application.OpenURL(LaunchUrl);
            Debug.Log("立绘抽查台：" + LaunchUrl);
        }

        [MenuItem("TheCall/Monster/关闭立绘抽查台")]
        public static void Close()
        {
            SessionState.SetBool(RunningSessionKey, false);
            Shutdown();
        }

        public static string Probe()
        {
            SessionState.SetBool(RunningSessionKey, true);
            EnsureStarted();
            MonsterGallerySession.EnsureSeeded();
            var done = new TaskCompletionSource<List<string>>();
            Task.Run(() =>
            {
                try { done.SetResult(ProbeHttp()); }
                catch (Exception exception) { done.SetException(exception); }
            });
            var until = DateTime.UtcNow.AddSeconds(90);
            while (!done.Task.IsCompleted && DateTime.UtcNow < until)
            {
                PumpMainThread();
                Thread.Sleep(5);
            }

            if (!done.Task.IsCompleted) return "FAIL probe timed out " + LaunchUrl;
            var failures = done.Task.GetAwaiter().GetResult();
            var shot = MonsterPortraitCapture.Render(
                MonsterVisualTraits.From(MonsterAppearance.FromSeed(48291)), 0.4f);
            var path = Path.Combine(Path.GetTempPath(), "monster-gallery-probe.png");
            File.WriteAllBytes(path, shot);
            var dark = CountDark(shot);
            if (shot == null || shot.Length < 1000 || shot[0] != 0x89) failures.Add("portrait png is empty");
            if (dark < 200) failures.Add("portrait has too little ink (" + dark + ")");
            return (failures.Count == 0 ? "PASS" : "FAIL " + string.Join("; ", failures)) + " dark=" + dark + " " + path + " " + LaunchUrl;
        }

        public static void EnsureStarted()
        {
            lock (Gate)
            {
                if (_listener != null && _listener.IsListening) return;
                EnsureTokenAndPort();
                _webRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Assets/Editor/MonsterGalleryWorkbench/Web"));
                _listener = new HttpListener { IgnoreWriteExceptions = true };
                _listener.Prefixes.Add("http://127.0.0.1:" + _port + "/");
                _listener.Prefixes.Add("http://localhost:" + _port + "/");
                _listener.Start();
                _cts = new CancellationTokenSource();
                _sockets = Array.Exists(typeof(HttpListenerContext).GetMethods(), method => method.Name == "AcceptWebSocketAsync");
                if (!_updateHooked)
                {
                    EditorApplication.update += PumpMainThread;
                    _updateHooked = true;
                }

                var cancel = _cts.Token;
                Task.Run(() => AcceptLoop(cancel), cancel);
            }
        }

        public static void Shutdown()
        {
            lock (Gate)
            {
                try { _cts?.Cancel(); } catch (Exception) { /* closing */ }
                _cts = null;
                try { _listener?.Stop(); _listener?.Close(); } catch (Exception) { /* closing */ }
                _listener = null;
            }

            if (IsMainThread()) ReleaseMainThreadResources();
            else RunOnMainThread(ReleaseMainThreadResources);
            lock (Sockets)
            {
                foreach (var socket in Sockets)
                    try { socket.Abort(); } catch (Exception) { /* closing */ }
                Sockets.Clear();
            }
        }

        static void ReleaseMainThreadResources()
        {
            foreach (var poll in Polls) poll.Done.TrySetCanceled();
            Polls.Clear();
            MonsterPortraitCapture.Shutdown();
        }

        static void ResumeIfNeeded()
        {
            if (SessionState.GetBool(RunningSessionKey, false)) EnsureStarted();
        }

        static void EnsureTokenAndPort()
        {
            _token = SessionState.GetString(TokenSessionKey, "");
            if (string.IsNullOrEmpty(_token))
            {
                var bytes = new byte[32];
                RandomNumberGenerator.Fill(bytes);
                _token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                SessionState.SetString(TokenSessionKey, _token);
            }

            var preferred = SessionState.GetInt(PortSessionKey, MinPort);
            if (preferred < MinPort || preferred > MaxPort || !CanBind(preferred)) preferred = FindFreePort();
            _port = preferred;
            SessionState.SetInt(PortSessionKey, _port);
        }

        static int FindFreePort()
        {
            for (var port = MinPort; port <= MaxPort; port++)
                if (CanBind(port)) return port;
            throw new InvalidOperationException("立绘抽查台没有可用端口。");
        }

        static bool CanBind(int port)
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            try { listener.Start(); return true; }
            catch (SocketException) { return false; }
            finally { try { listener.Stop(); } catch (Exception) { /* ignore */ } }
        }

        static void PumpMainThread()
        {
            var count = 0;
            while (count < MaxJobsPerTick && MainThreadJobs.TryDequeue(out var job))
            {
                try { job(); }
                catch (Exception exception) { Debug.LogException(exception); }
                count++;
            }
        }

        static void RunOnMainThread(Action action) => RunOnMainThread(() =>
        {
            action();
            return 0;
        });

        static T RunOnMainThread<T>(Func<T> function)
        {
            if (IsMainThread()) return function();
            T result = default;
            Exception error = null;
            var done = new ManualResetEventSlim(false);
            MainThreadJobs.Enqueue(() =>
            {
                try { result = function(); }
                catch (Exception exception) { error = exception; }
                finally { done.Set(); }
            });
            if (!done.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("编辑器主线程没有执行抽查台任务。");
            if (error != null) throw error;
            return result;
        }

        static bool IsMainThread() => Thread.CurrentThread.ManagedThreadId == MainThreadId;

        static string Dispatch(string body)
        {
            JObject message;
            try { message = JObject.Parse(string.IsNullOrEmpty(body) ? "{}" : body); }
            catch (JsonException) { return CommandResult("", false, "命令不是 JSON。"); }

            var requestId = message.Value<string>("requestId") ?? "";
            var command = message.Value<string>("command") ?? "";
            if (!MonsterGallerySession.TryCommand(command, message["payload"] as JObject, out var error))
                return CommandResult(requestId, false, error);
            _revision++;
            Publish(SnapshotJson());
            return CommandResult(requestId, true, "");
        }

        static string CommandResult(string requestId, bool ok, string error) => JsonConvert.SerializeObject(new
        {
            type = "commandResult",
            requestId,
            ok,
            revision = _revision,
            payload = ok ? MonsterGallerySession.SnapshotPayload() : null,
            error = error ?? "",
        });

        static string SnapshotJson() => JsonConvert.SerializeObject(new
        {
            type = "snapshot",
            protocolVersion = ProtocolVersion,
            revision = _revision,
            payload = MonsterGallerySession.SnapshotPayload(),
        });

        static void Publish(string snapshot)
        {
            List<PollWait> waiting;
            lock (Polls)
            {
                waiting = new List<PollWait>(Polls);
                Polls.Clear();
            }

            foreach (var poll in waiting) poll.Done.TrySetResult(snapshot);
            List<WebSocket> sockets;
            lock (Sockets) sockets = new List<WebSocket>(Sockets);
            var bytes = Encoding.UTF8.GetBytes(snapshot);
            foreach (var socket in sockets) Task.Run(() => SendBytes(socket, bytes));
        }

        static async Task<string> WaitForRevision(long after, int timeoutMs)
        {
            var existing = RunOnMainThread(() => _revision > after ? SnapshotJson() : null);
            if (existing != null) return existing;
            var wait = new PollWait { Done = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously) };
            RunOnMainThread(() =>
            {
                if (_revision > after) wait.Done.TrySetResult(SnapshotJson());
                else lock (Polls) Polls.Add(wait);
            });
            var finished = await Task.WhenAny(wait.Done.Task, Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (finished != wait.Done.Task)
            {
                RunOnMainThread(() => { lock (Polls) Polls.Remove(wait); });
                return JsonConvert.SerializeObject(new { type = "idle", protocolVersion = ProtocolVersion, revision = _revision });
            }

            return await wait.Done.Task.ConfigureAwait(false);
        }

        static List<string> ProbeHttp()
        {
            var failures = new List<string>();
            Expect(Http("GET", "/api/snapshot", null, null, null), 401, "missing token", failures);
            Expect(Http("GET", "/api/snapshot", null, "wrong-token", null), 401, "wrong token", failures);
            Expect(Raw("GET /../secret HTTP/1.1\r\nHost: 127.0.0.1:" + _port + "\r\nConnection: close\r\n\r\n"), 400, "traversal", failures);
            Expect(Http("GET", "/api/snapshot", null, _token, "http://evil.example"), 403, "origin", failures);
            Expect(Http("POST", "/api/command", new string('x', MaxPayloadBytes + 8), _token, null), 413, "oversize", failures);
            var denied = Http("POST", "/api/command", "{\"requestId\":\"p\",\"command\":\"nope\",\"payload\":{}}", _token, null);
            if (denied.status != 200 || denied.body.IndexOf("\"ok\":false", StringComparison.Ordinal) < 0)
                failures.Add("unknown command was not rejected");
            var rolled = Http("POST", "/api/command", "{\"requestId\":\"r\",\"command\":\"roll\",\"payload\":{\"source\":\"shop\"}}", _token, null);
            if (rolled.status != 200 || rolled.body.IndexOf("\"ok\":true", StringComparison.Ordinal) < 0)
                failures.Add("roll failed: " + rolled.status + " " + Trim(rolled.body));
            else if (rolled.body.IndexOf("\"png\":\"", StringComparison.Ordinal) < 0)
                failures.Add("roll returned no portrait");
            return failures;
        }

        static int CountDark(byte[] png)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                texture.LoadImage(png);
                var pixels = texture.GetPixels32();
                var dark = 0;
                for (var i = 0; i < pixels.Length; i++)
                    if (pixels[i].r < 70 && pixels[i].g < 70 && pixels[i].b < 70) dark++;
                return dark;
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        sealed class PollWait
        {
            public TaskCompletionSource<string> Done;
        }
    }
}
#endif
