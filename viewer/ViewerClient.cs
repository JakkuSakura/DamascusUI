using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;

namespace DamascusUI;

public sealed class ViewerClient : IDisposable
{
    private readonly ClientWebSocket _ws = new();
    private readonly string _url;
    private readonly Action<string, string> _onOpenWindow;
    private readonly Action<List<NativeMenuItem>> _onSetMenu;
    private CancellationTokenSource? _cts;

    public ViewerClient(string baseUrl, Action<string, string> onOpenWindow, Action<List<NativeMenuItem>> onSetMenu)
    {
        _url = baseUrl.Replace("http://", "ws://").Replace("https://", "wss://") + "/ws";
        _onOpenWindow = onOpenWindow;
        _onSetMenu = onSetMenu;
    }

    public async Task ConnectAsync()
    {
        _cts = new CancellationTokenSource();
        await _ws.ConnectAsync(new Uri(_url), _cts.Token);
        _ = ReceiveLoop(_cts.Token);
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        var buffer = new byte[8192];
        while (_ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await _ws.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close) break;

            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            using var doc = JsonDocument.Parse(json);
            var type = doc.RootElement.GetProperty("type").GetString();

            switch (type)
            {
                case "viewer.open-window":
                    _onOpenWindow(
                        doc.RootElement.GetProperty("title").GetString() ?? "",
                        doc.RootElement.GetProperty("url").GetString() ?? "");
                    break;
                case "viewer.set-menu-bar":
                    _onSetMenu(ParseMenu(doc.RootElement.GetProperty("items")));
                    break;
            }
        }
    }

    private static List<NativeMenuItem> ParseMenu(JsonElement items)
    {
        var list = new List<NativeMenuItem>();
        foreach (var item in items.EnumerateArray())
        {
            var kind = item.GetProperty("kind").GetString();
            var label = item.TryGetProperty("label", out var l) ? l.GetString() : "";
            switch (kind)
            {
                case "action":
                    list.Add(new NativeMenuItem(label!));
                    break;
                case "separator":
                    list.Add(new NativeMenuItemSeparator());
                    break;
                case "submenu":
                    var sub = new NativeMenuItem(label!);
                    sub.Menu = new NativeMenu();
                    foreach (var child in ParseMenu(item.GetProperty("items")))
                        sub.Menu.Items.Add(child);
                    list.Add(sub);
                    break;
            }
        }
        return list;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _ws.Dispose();
    }
}
