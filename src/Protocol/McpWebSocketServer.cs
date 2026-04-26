using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ClaudeCodeVS.Protocol
{
    internal sealed class McpWebSocketServer
    {
        private const string AuthHeader = "x-claude-code-ide-authorization";
        private const int MinPort = 10000;
        private const int MaxPort = 65535;

        private readonly string _authToken;
        private readonly JsonRpcDispatcher _dispatcher;
        private readonly ConcurrentDictionary<Guid, ClientSession> _clients = new ConcurrentDictionary<Guid, ClientSession>();

        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private Task _acceptLoop;
        private int _port;

        public int Port => _port;
        public int ClientCount => _clients.Count;

        public event EventHandler ClientCountChanged;

        public McpWebSocketServer(string authToken, JsonRpcDispatcher dispatcher)
        {
            _authToken = authToken;
            _dispatcher = dispatcher;
        }

        public Task<int> StartAsync(CancellationToken ct)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _port = BindOnFreePort();
            _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
            return Task.FromResult(_port);
        }

        private int BindOnFreePort()
        {
            var rng = new Random();
            for (int attempt = 0; attempt < 50; attempt++)
            {
                int port = rng.Next(MinPort, MaxPort + 1);
                try
                {
                    var listener = new HttpListener();
                    listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                    listener.Start();
                    _listener = listener;
                    return port;
                }
                catch (HttpListenerException)
                {
                }
                catch (SocketException)
                {
                }
            }
            throw new InvalidOperationException("Could not bind to a free port in range " + MinPort + "-" + MaxPort);
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException) { break; }
                catch (HttpListenerException) { break; }

                _ = Task.Run(() => HandleRequestAsync(ctx, ct));
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext ctx, CancellationToken ct)
        {
            try
            {
                if (!ctx.Request.IsWebSocketRequest)
                {
                    ctx.Response.StatusCode = 400;
                    ctx.Response.Close();
                    return;
                }

                string provided = ctx.Request.Headers[AuthHeader];
                if (string.IsNullOrEmpty(provided) || !AuthToken.ConstantTimeEquals(provided, _authToken))
                {
                    ctx.Response.StatusCode = 401;
                    ctx.Response.Close();
                    return;
                }

                var wsCtx = await ctx.AcceptWebSocketAsync(null).ConfigureAwait(false);
                var session = new ClientSession(wsCtx.WebSocket);
                _clients[session.Id] = session;
                RaiseClientCountChanged();

                try
                {
                    await RunSessionAsync(session, ct).ConfigureAwait(false);
                }
                finally
                {
                    _clients.TryRemove(session.Id, out _);
                    RaiseClientCountChanged();
                    try { session.Socket.Dispose(); } catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ClaudeCodeVS] request error: " + ex);
            }
        }

        private async Task RunSessionAsync(ClientSession session, CancellationToken ct)
        {
            var buffer = new byte[16 * 1024];
            var builder = new StringBuilder();

            while (session.Socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await session.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                }
                catch (WebSocketException) { break; }
                catch (OperationCanceledException) { break; }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    try { await session.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); } catch { }
                    break;
                }

                builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (!result.EndOfMessage) continue;

                string message = builder.ToString();
                builder.Clear();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        string response = await _dispatcher.HandleAsync(message, session).ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(response))
                        {
                            await session.SendAsync(response, CancellationToken.None).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[ClaudeCodeVS] dispatch error: " + ex);
                    }
                });
            }
        }

        public void BroadcastNotification(string method, JObject paramsObj)
        {
            var frame = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = paramsObj ?? new JObject(),
            };
            string text = frame.ToString(Newtonsoft.Json.Formatting.None);

            foreach (var kv in _clients)
            {
                _ = kv.Value.SendAsync(text, CancellationToken.None);
            }
        }

        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            foreach (var kv in _clients)
            {
                try { kv.Value.Socket.Abort(); } catch { }
            }
            _clients.Clear();
        }

        private void RaiseClientCountChanged()
        {
            try { ClientCountChanged?.Invoke(this, EventArgs.Empty); } catch { }
        }
    }

    internal sealed class ClientSession
    {
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        public Guid Id { get; } = Guid.NewGuid();
        public WebSocket Socket { get; }

        public ClientSession(WebSocket socket)
        {
            Socket = socket;
        }

        public async Task SendAsync(string text, CancellationToken ct)
        {
            if (Socket.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(text);
            await _sendLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
            }
            catch (WebSocketException) { }
            catch (ObjectDisposedException) { }
            finally
            {
                _sendLock.Release();
            }
        }
    }
}
