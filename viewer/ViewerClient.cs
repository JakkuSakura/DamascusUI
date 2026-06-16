using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace DamascusUI;

/// <summary>
/// Connects to the backend WebSocket and handles viewer commands.
/// </summary>
public sealed class ViewerClient : IDisposable
{
    private readonly ClientWebSocket _ws = new();
    private readonly string _url;
    private readonly Action<string, string> _onOpenWindow;
    private CancellationTokenSource? _cts;

    public ViewerClient(string baseUrl, Action<string, string> onOpenWindow)
    {
        _url = baseUrl.Replace("http://", "ws://").Replace("https://", "wss://") + "/ws";
        _onOpenWindow = onOpenWindow;
    }

    public async Task ConnectAsync()
    {
        _cts = new CancellationTokenSource();
        await _ws.ConnectAsync(new Uri(_url), _cts.Token);
        Console.WriteLine($"[ViewerClient] connected to {_url}");

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
                    var title = doc.RootElement.GetProperty("title").GetString() ?? "";
                    var url = doc.RootElement.GetProperty("url").GetString() ?? "";
                    _onOpenWindow(title, url);
                    break;
            }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _ws.Dispose();
    }
}
