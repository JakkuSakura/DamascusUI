using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace DamascusUI;

public sealed class ViewerServer : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpListener _listener = new();
    private readonly int _port;
    private readonly Action<long, string, string> _onOpenWindow;
    private readonly Action<long, List<NativeMenuItemDef>> _onSetMenu;
    private readonly Action<string> _onSetDockIcon;
    private readonly Action<string> _onSetDockBadge;
    private readonly Action<long, string> _onSetWindowTitle;
    private readonly Func<long> _focusedWindowId;
    private readonly ConcurrentDictionary<long, ShellConnection> _connections = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<long, byte>> _topicSubscribers = new();

    public ViewerServer(
        int port,
        Action<long, string, string> onOpenWindow,
        Action<long, List<NativeMenuItemDef>> onSetMenu,
        Action<string> onSetDockIcon,
        Action<string> onSetDockBadge,
        Action<long, string> onSetWindowTitle,
        Func<long> focusedWindowId)
    {
        _port = port;
        _onOpenWindow = onOpenWindow;
        _onSetMenu = onSetMenu;
        _onSetDockIcon = onSetDockIcon;
        _onSetDockBadge = onSetDockBadge;
        _onSetWindowTitle = onSetWindowTitle;
        _focusedWindowId = focusedWindowId;
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    public void Start()
    {
        _listener.Start();
        Console.WriteLine($"[ViewerServer] listening on http://127.0.0.1:{_port}");
        _ = Task.Run(ListenLoop);
    }

    private async Task ListenLoop()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext? ctx = null;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (ctx is not null)
            {
                _ = HandleContext(ctx);
            }
        }
    }

    private async Task HandleContext(HttpListenerContext ctx)
    {
        try
        {
            if (ctx.Request.IsWebSocketRequest && ctx.Request.Url?.AbsolutePath == "/ws")
            {
                await AcceptWebSocket(ctx);
                return;
            }

            await RespondJson(ctx, 404, new { error = "not found" });
        }
        catch
        {
            if (ctx.Response.OutputStream.CanWrite)
            {
                await RespondJson(ctx, 500, new { error = "internal error" });
            }
        }
    }

    private async Task AcceptWebSocket(HttpListenerContext ctx)
    {
        var wsContext = await ctx.AcceptWebSocketAsync(null);
        var connection = new ShellConnection(wsContext.WebSocket);
        await ReceiveLoop(connection);
    }

    private async Task ReceiveLoop(ShellConnection connection)
    {
        var socket = connection.Socket;
        var buffer = new byte[16 * 1024];

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None);
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var json = Encoding.UTF8.GetString(ms.ToArray());
                using var doc = JsonDocument.Parse(json);
                await HandleMessage(connection, doc.RootElement);
            }
        }
        finally
        {
            Disconnect(connection);
            socket.Dispose();
        }
    }

    private async Task HandleMessage(ShellConnection connection, JsonElement message)
    {
        if (!message.TryGetProperty("type", out var typeValue))
        {
            await connection.SendAsync(new ErrorEnvelope("missing_type", "Message missing type"));
            return;
        }

        var type = typeValue.GetString();
        switch (type)
        {
            case "register":
                await HandleRegister(connection, message);
                break;
            case "set-title":
                await HandleSetTitle(connection, message);
                break;
            case "open-window":
                await HandleOpenWindow(connection, message);
                break;
            case "set-menu":
                await HandleSetMenu(connection, message);
                break;
            case "set-dock-badge":
                await HandleSetDockBadge(message);
                break;
            case "set-dock-icon":
                await HandleSetDockIcon(message);
                break;
            case "subscribe":
                await HandleSubscribe(connection, message);
                break;
            case "unsubscribe":
                await HandleUnsubscribe(connection, message);
                break;
            case "publish":
                await HandlePublish(connection, message);
                break;
            default:
                await connection.SendAsync(new ErrorEnvelope("unknown_type", $"Unknown type: {type}"));
                break;
        }
    }

    private async Task HandleRegister(ShellConnection connection, JsonElement message)
    {
        var payload = message.Deserialize<RegisterRequest>(JsonOptions);
        if (payload is null)
        {
            await connection.SendAsync(new ErrorEnvelope("invalid_register", "Invalid register payload"));
            return;
        }

        connection.WindowId = payload.WindowId;
        _connections[payload.WindowId] = connection;
        await connection.SendAsync(new RegisteredEnvelope(payload.WindowId));
    }

    private async Task HandleSetTitle(ShellConnection connection, JsonElement message)
    {
        var payload = message.Deserialize<SetTitleRequest>(JsonOptions);
        if (payload is null)
        {
            await connection.SendAsync(new ErrorEnvelope("invalid_set_title", "Invalid title payload"));
            return;
        }

        Dispatcher.UIThread.Post(() => _onSetWindowTitle(connection.WindowId, payload.Title));
        await connection.SendAsync(new AckEnvelope("set-title"));
    }

    private async Task HandleOpenWindow(ShellConnection connection, JsonElement message)
    {
        var payload = message.Deserialize<OpenWindowRequest>(JsonOptions);
        if (payload is null)
        {
            await connection.SendAsync(new ErrorEnvelope("invalid_open_window", "Invalid open window payload"));
            return;
        }

        Dispatcher.UIThread.Post(() =>
            _onOpenWindow(connection.WindowId, payload.Title, payload.Url ?? string.Empty));

        await connection.SendAsync(new AckEnvelope("open-window"));
    }

    private async Task HandleSetMenu(ShellConnection connection, JsonElement message)
    {
        var payload = message.Deserialize<SetMenuRequest>(JsonOptions);
        if (payload?.Items is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => _onSetMenu(connection.WindowId, payload.Items));
        await Task.CompletedTask;
    }

    private async Task HandleSetDockBadge(JsonElement message)
    {
        var payload = message.Deserialize<SetDockBadgeRequest>(JsonOptions);
        if (payload is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => _onSetDockBadge(payload.Text));
        await Task.CompletedTask;
    }

    private async Task HandleSetDockIcon(JsonElement message)
    {
        var payload = message.Deserialize<SetDockIconRequest>(JsonOptions);
        if (payload is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => _onSetDockIcon(payload.Icon));
        await Task.CompletedTask;
    }

    private async Task HandleSubscribe(ShellConnection connection, JsonElement message)
    {
        var payload = message.Deserialize<SubscribeRequest>(JsonOptions);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Topic))
        {
            await connection.SendAsync(new ErrorEnvelope("invalid_subscribe", "Invalid subscribe payload"));
            return;
        }

        var subscribers = _topicSubscribers.GetOrAdd(payload.Topic, _ => new ConcurrentDictionary<long, byte>());
        subscribers[connection.WindowId] = 0;
        connection.Topics.Add(payload.Topic);
        await connection.SendAsync(new AckEnvelope("subscribe"));
    }

    private async Task HandleUnsubscribe(ShellConnection connection, JsonElement message)
    {
        var payload = message.Deserialize<SubscribeRequest>(JsonOptions);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Topic))
        {
            await connection.SendAsync(new ErrorEnvelope("invalid_unsubscribe", "Invalid unsubscribe payload"));
            return;
        }

        RemoveSubscription(connection.WindowId, payload.Topic);
        connection.Topics.Remove(payload.Topic);
        await connection.SendAsync(new AckEnvelope("unsubscribe"));
    }

    private async Task HandlePublish(ShellConnection connection, JsonElement message)
    {
        var payload = message.Deserialize<PublishRequest>(JsonOptions);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Topic))
        {
            await connection.SendAsync(new ErrorEnvelope("invalid_publish", "Invalid publish payload"));
            return;
        }

        var scope = payload.Scope ?? "broadcast";
        await PublishEvent(payload.Topic, payload.Payload, connection.WindowId, scope, payload.TargetWindowId);

        await connection.SendAsync(new AckEnvelope("publish"));
    }

    public void PublishFromShell(string topic, JsonElement payload, string scope, long targetWindowId = 0)
    {
        _ = PublishEvent(topic, payload, 0, scope, targetWindowId);
    }

    private async Task PublishEvent(string topic, JsonElement? payload, long fromWindowId, string scope, long? targetWindowId)
    {
        var request = new PublishRequest(topic, payload, scope, targetWindowId);
        IEnumerable<long> targets = ResolveTargets(fromWindowId, request, scope);
        var envelope = new EventEnvelope(topic, payload, fromWindowId);

        foreach (var resolvedTargetWindowId in targets)
        {
            if (_connections.TryGetValue(resolvedTargetWindowId, out var target))
            {
                await target.SendAsync(envelope);
            }
        }
    }

    private IEnumerable<long> ResolveTargets(long sourceWindowId, PublishRequest payload, string scope)
    {
        if (_topicSubscribers.TryGetValue(payload.Topic, out var subscribers) is false)
        {
            return [];
        }

        var windowIds = subscribers.Keys;
        return scope switch
        {
            "self" => windowIds.Where(id => id == sourceWindowId),
            "except-self" => windowIds.Where(id => id != sourceWindowId),
            "window" when payload.TargetWindowId.HasValue => windowIds.Where(id => id == payload.TargetWindowId.Value),
            "focused" => windowIds.Where(id => id == _focusedWindowId()),
            _ => windowIds,
        };
    }

    private void Disconnect(ShellConnection connection)
    {
        if (connection.WindowId == 0)
        {
            return;
        }

        _connections.TryRemove(connection.WindowId, out _);
        foreach (var topic in connection.Topics)
        {
            RemoveSubscription(connection.WindowId, topic);
        }
    }

    private void RemoveSubscription(long windowId, string topic)
    {
        if (_topicSubscribers.TryGetValue(topic, out var subscribers))
        {
            subscribers.TryRemove(windowId, out _);
            if (subscribers.IsEmpty)
            {
                _topicSubscribers.TryRemove(topic, out _);
            }
        }
    }

    private static async Task RespondJson(HttpListenerContext ctx, int code, object body)
    {
        ctx.Response.StatusCode = code;
        ctx.Response.ContentType = "application/json";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(body, JsonOptions);
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }

    public void Dispose()
    {
        _listener.Stop();
        foreach (var connection in _connections.Values)
        {
            connection.Socket.Dispose();
        }
        _connections.Clear();
        _topicSubscribers.Clear();
    }

    private sealed class ShellConnection(WebSocket socket)
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public WebSocket Socket { get; } = socket;
        public long WindowId { get; set; }
        public HashSet<string> Topics { get; } = [];

        public async Task SendAsync<T>(T payload)
        {
            if (Socket.State != WebSocketState.Open)
            {
                return;
            }

            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
            await _sendLock.WaitAsync();
            try
            {
                await Socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            finally
            {
                _sendLock.Release();
            }
        }
    }
}

record RegisterRequest([property: JsonPropertyName("windowId")] long WindowId);

record SetTitleRequest([property: JsonPropertyName("title")] string Title);

record SetDockBadgeRequest([property: JsonPropertyName("text")] string Text);

record SetDockIconRequest([property: JsonPropertyName("icon")] string Icon);

record SetMenuRequest([property: JsonPropertyName("items")] List<NativeMenuItemDef> Items);

record SubscribeRequest([property: JsonPropertyName("topic")] string Topic);

record PublishRequest(
    [property: JsonPropertyName("topic")] string Topic,
    [property: JsonPropertyName("payload")] JsonElement? Payload,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("targetWindowId")] long? TargetWindowId
);

record OpenWindowRequest(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("url")] string Url
);

record AckEnvelope([property: JsonPropertyName("action")] string Action)
{
    [JsonPropertyName("type")]
    public string Type => "ack";
}

record RegisteredEnvelope([property: JsonPropertyName("windowId")] long WindowId)
{
    [JsonPropertyName("type")]
    public string Type => "registered";
}

record EventEnvelope(
    [property: JsonPropertyName("topic")] string Topic,
    [property: JsonPropertyName("payload")] JsonElement? Payload,
    [property: JsonPropertyName("fromWindowId")] long FromWindowId)
{
    [JsonPropertyName("type")]
    public string Type => "event";
}

record ErrorEnvelope(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message)
{
    [JsonPropertyName("type")]
    public string Type => "error";
}
