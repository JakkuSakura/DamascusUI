using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Avalonia.Threading;

namespace DamascusUI;

public sealed class ViewerServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly int _preferredPort;
    public int ActualPort { get; private set; }
    private readonly Action<long, string, string, bool, bool> _onOpenWindow;
    private readonly Action<long> _onCloseWindow;
    private readonly Action<long> _onMinimizeWindow;
    private readonly Action<long> _onToggleMaximize;
    private readonly Action<long, double, double> _onMoveWindow;
    private readonly Action<long, List<NativeMenuItemDef>> _onSetMenu;
    private readonly Action<string> _onSetDockIcon;
    private readonly Action<string> _onSetDockBadge;
    private readonly Action<long, string> _onSetWindowTitle;
    private readonly Action<long, bool?, bool?> _onSetWindowProps;
    private readonly Action<long, NotificationRequest> _onShowNotification;
    private readonly Func<long> _focusedWindowId;
    private readonly ConcurrentDictionary<long, ShellConnection> _connections = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<long, byte>> _topicSubscribers = new();

    public ViewerServer(
        int preferredPort,
        Action<long, string, string, bool, bool> onOpenWindow,
        Action<long> onCloseWindow,
        Action<long> onMinimizeWindow,
        Action<long> onToggleMaximize,
        Action<long, double, double> onMoveWindow,
        Action<long, List<NativeMenuItemDef>> onSetMenu,
        Action<string> onSetDockIcon,
        Action<string> onSetDockBadge,
        Action<long, string> onSetWindowTitle,
        Action<long, bool?, bool?> onSetWindowProps,
        Action<long, NotificationRequest> onShowNotification,
        Func<long> focusedWindowId)
    {
        _preferredPort = preferredPort;
        _onOpenWindow = onOpenWindow;
        _onCloseWindow = onCloseWindow;
        _onMinimizeWindow = onMinimizeWindow;
        _onToggleMaximize = onToggleMaximize;
        _onMoveWindow = onMoveWindow;
        _onSetMenu = onSetMenu;
        _onSetDockIcon = onSetDockIcon;
        _onSetDockBadge = onSetDockBadge;
        _onSetWindowTitle = onSetWindowTitle;
        _onSetWindowProps = onSetWindowProps;
        _onShowNotification = onShowNotification;
        _focusedWindowId = focusedWindowId;
    }

    public void Start()
    {
        for (int port = _preferredPort; port < _preferredPort + 100; port++)
        {
            try
            {
                _listener.Prefixes.Clear();
                _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                _listener.Start();
                ActualPort = port;
                Console.WriteLine($"[ViewerServer] listening on http://127.0.0.1:{port}");
                _ = Task.Run(ListenLoop);
                return;
            }
            catch (HttpListenerException)
            {
            }
        }

        throw new InvalidOperationException("ViewerServer: no available port");
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

            await RespondJson(ctx, 404, "not found");
        }
        catch
        {
            if (ctx.Response.OutputStream.CanWrite)
            {
                await RespondJson(ctx, 500, "internal error");
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
            case "set-window-props":
                await HandleSetWindowProps(connection, message);
                break;
            case "close-window":
                await HandleCloseWindow(connection);
                break;
            case "minimize-window":
                Dispatcher.UIThread.Post(() => _onMinimizeWindow(connection.WindowId));
                break;
            case "toggle-maximize":
                Dispatcher.UIThread.Post(() => _onToggleMaximize(connection.WindowId));
                break;
            case "move-window":
                await HandleMoveWindow(connection, message);
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
            case "show-notification":
                await HandleShowNotification(connection, message);
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
        var payload = message.Deserialize(ShellJsonContext.Default.RegisterRequest);
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
        var payload = message.Deserialize(ShellJsonContext.Default.SetTitleRequest);
        if (payload is null)
        {
            await connection.SendAsync(new ErrorEnvelope("invalid_set_title", "Invalid title payload"));
            return;
        }

        Dispatcher.UIThread.Post(() => _onSetWindowTitle(connection.WindowId, payload.Title));
        await connection.SendAsync(new AckEnvelope("set-title"));
    }

    private async Task HandleSetWindowProps(ShellConnection connection, JsonElement message)
    {
        var payload = message.Deserialize(ShellJsonContext.Default.SetWindowPropsRequest);
        if (payload is null)
        {
            await connection.SendAsync(new ErrorEnvelope("invalid_set_window_props", "Invalid set-window-props payload"));
            return;
        }

        if (payload.Transparent is bool || payload.Decorations is bool)
        {
            Dispatcher.UIThread.Post(() => _onSetWindowProps(connection.WindowId, payload.Transparent, payload.Decorations));
        }
    }

    private async Task HandleCloseWindow(ShellConnection connection)
    {
        Dispatcher.UIThread.Post(() => _onCloseWindow(connection.WindowId));
    }

    private async Task HandleMoveWindow(ShellConnection connection, JsonElement message)
    {
        if (message.TryGetProperty("dx", out var dxEl) && message.TryGetProperty("dy", out var dyEl))
        {
            var dx = dxEl.GetDouble();
            var dy = dyEl.GetDouble();
            Dispatcher.UIThread.Post(() => _onMoveWindow(connection.WindowId, dx, dy));
        }
    }

    private async Task HandleOpenWindow(ShellConnection connection, JsonElement message)
    {
        var payload = message.Deserialize(ShellJsonContext.Default.OpenWindowRequest);
        if (payload is null)
        {
            await connection.SendAsync(new ErrorEnvelope("invalid_open_window", "Invalid open window payload"));
            return;
        }

        Dispatcher.UIThread.Post(() =>
            _onOpenWindow(connection.WindowId, payload.Title, payload.Url ?? string.Empty, payload.Transparent, payload.Decorations));

        await connection.SendAsync(new AckEnvelope("open-window"));
    }

    private async Task HandleSetMenu(ShellConnection connection, JsonElement message)
    {
        var payload = message.Deserialize(ShellJsonContext.Default.SetMenuRequest);
        if (payload?.Items is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => _onSetMenu(connection.WindowId, payload.Items));
        await Task.CompletedTask;
    }

    private async Task HandleSetDockBadge(JsonElement message)
    {
        var payload = message.Deserialize(ShellJsonContext.Default.SetDockBadgeRequest);
        if (payload is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => _onSetDockBadge(payload.Text));
        await Task.CompletedTask;
    }

    private async Task HandleSetDockIcon(JsonElement message)
    {
        var payload = message.Deserialize(ShellJsonContext.Default.SetDockIconRequest);
        if (payload is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => _onSetDockIcon(payload.Icon));
        await Task.CompletedTask;
    }

    private async Task HandleShowNotification(ShellConnection connection, JsonElement message)
    {
        var payload = message.Deserialize(ShellJsonContext.Default.NotificationRequest);
        if (payload is null)
        {
            await connection.SendAsync(new ErrorEnvelope("invalid_notification", "Invalid notification payload"));
            return;
        }

        Dispatcher.UIThread.Post(() => _onShowNotification(connection.WindowId, payload));
        await connection.SendAsync(new AckEnvelope("show-notification"));
    }

    private async Task HandleSubscribe(ShellConnection connection, JsonElement message)
    {
        var payload = message.Deserialize(ShellJsonContext.Default.SubscribeRequest);
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
        var payload = message.Deserialize(ShellJsonContext.Default.SubscribeRequest);
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
        var payload = message.Deserialize(ShellJsonContext.Default.PublishRequest);
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

    private static async Task RespondJson(HttpListenerContext ctx, int code, string error)
    {
        ctx.Response.StatusCode = code;
        ctx.Response.ContentType = "application/json";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            new HttpErrorBody(error), ShellJsonContext.Default.HttpErrorBody);
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

        // Typed overloads: each resolves to the source-generated JsonTypeInfo — no reflection.
        public Task SendAsync(AckEnvelope payload) =>
            SendBytesAsync(JsonSerializer.SerializeToUtf8Bytes(payload, ShellJsonContext.Default.AckEnvelope));

        public Task SendAsync(ErrorEnvelope payload) =>
            SendBytesAsync(JsonSerializer.SerializeToUtf8Bytes(payload, ShellJsonContext.Default.ErrorEnvelope));

        public Task SendAsync(RegisteredEnvelope payload) =>
            SendBytesAsync(JsonSerializer.SerializeToUtf8Bytes(payload, ShellJsonContext.Default.RegisteredEnvelope));

        public Task SendAsync(EventEnvelope payload) =>
            SendBytesAsync(JsonSerializer.SerializeToUtf8Bytes(payload, ShellJsonContext.Default.EventEnvelope));

        private async Task SendBytesAsync(byte[] bytes)
        {
            if (Socket.State != WebSocketState.Open)
            {
                return;
            }

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

record RegisterRequest([property: System.Text.Json.Serialization.JsonPropertyName("windowId")] long WindowId);

record SetTitleRequest([property: System.Text.Json.Serialization.JsonPropertyName("title")] string Title);

record SetDockBadgeRequest([property: System.Text.Json.Serialization.JsonPropertyName("text")] string Text);

record SetDockIconRequest([property: System.Text.Json.Serialization.JsonPropertyName("icon")] string Icon);

record SetMenuRequest([property: System.Text.Json.Serialization.JsonPropertyName("items")] List<NativeMenuItemDef> Items);

record SubscribeRequest([property: System.Text.Json.Serialization.JsonPropertyName("topic")] string Topic);

record PublishRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("topic")] string Topic,
    [property: System.Text.Json.Serialization.JsonPropertyName("payload")] JsonElement? Payload,
    [property: System.Text.Json.Serialization.JsonPropertyName("scope")] string? Scope,
    [property: System.Text.Json.Serialization.JsonPropertyName("targetWindowId")] long? TargetWindowId
);

record OpenWindowRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("title")] string Title,
    [property: System.Text.Json.Serialization.JsonPropertyName("url")] string Url,
    [property: System.Text.Json.Serialization.JsonPropertyName("transparent")] bool Transparent,
    [property: System.Text.Json.Serialization.JsonPropertyName("decorations")] bool Decorations
);

record SetWindowPropsRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("surfaceId")] uint SurfaceId,
    [property: System.Text.Json.Serialization.JsonPropertyName("transparent")] bool? Transparent,
    [property: System.Text.Json.Serialization.JsonPropertyName("decorations")] bool? Decorations
);

record MoveWindowRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("dx")] double Dx,
    [property: System.Text.Json.Serialization.JsonPropertyName("dy")] double Dy
);

record AckEnvelope([property: System.Text.Json.Serialization.JsonPropertyName("action")] string Action)
{
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string Type => "ack";
}

record RegisteredEnvelope([property: System.Text.Json.Serialization.JsonPropertyName("windowId")] long WindowId)
{
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string Type => "registered";
}

record EventEnvelope(
    [property: System.Text.Json.Serialization.JsonPropertyName("topic")] string Topic,
    [property: System.Text.Json.Serialization.JsonPropertyName("payload")] JsonElement? Payload,
    [property: System.Text.Json.Serialization.JsonPropertyName("fromWindowId")] long FromWindowId)
{
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string Type => "event";
}

record ErrorEnvelope(
    [property: System.Text.Json.Serialization.JsonPropertyName("code")] string Code,
    [property: System.Text.Json.Serialization.JsonPropertyName("message")] string Message)
{
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string Type => "error";
}
