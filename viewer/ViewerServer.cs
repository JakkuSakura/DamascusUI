using System.Net;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;

namespace DamascusUI;

/// <summary>
/// Local HTTP server that the frontend calls to control the native viewer.
/// Runs on a fixed port so the frontend knows where to reach it.
/// </summary>
public sealed class ViewerServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly int _port;
    private readonly Action<string, string> _onOpenWindow;
    private readonly Action<List<NativeMenuItem>> _onSetMenu;

    public int Port => _port;

    public ViewerServer(int port, Action<string, string> onOpenWindow, Action<List<NativeMenuItem>> onSetMenu)
    {
        _port = port;
        _onOpenWindow = onOpenWindow;
        _onSetMenu = onSetMenu;
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
            var ctx = await _listener.GetContextAsync();
            _ = HandleRequest(ctx);
        }
    }

    private async Task HandleRequest(HttpListenerContext ctx)
    {
        try
        {
            var path = ctx.Request.Url!.AbsolutePath.TrimStart('/');
            var reader = new StreamReader(ctx.Request.InputStream);
            var body = await reader.ReadToEndAsync();

            switch (path)
            {
                case "set-title":
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (Application.Current?.ApplicationLifetime is
                            IClassicDesktopStyleApplicationLifetime desktop
                            && desktop.MainWindow is not null)
                            desktop.MainWindow.Title = body;
                    });
                    await Respond(ctx, 200, "ok");
                    break;

                case "open-window":
                    var req = JsonSerializer.Deserialize<OpenWindowRequest>(body);
                    if (req != null)
                        _onOpenWindow(req.Title, req.Url);
                    await Respond(ctx, 200, "ok");
                    break;

                case "set-menu":
                    var items = JsonSerializer.Deserialize<List<NativeMenuItemDef>>(body);
                    if (items != null)
                        _onSetMenu(items.Select(ToNativeMenuItem).ToList());
                    await Respond(ctx, 200, "ok");
                    break;

                default:
                    await Respond(ctx, 404, "not found");
                    break;
            }
        }
        catch
        {
            await Respond(ctx, 500, "error");
        }
    }

    private static async Task Respond(HttpListenerContext ctx, int code, string body)
    {
        ctx.Response.StatusCode = code;
        var bytes = Encoding.UTF8.GetBytes(body);
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }

    private static NativeMenuItem ToNativeMenuItem(NativeMenuItemDef def)
    {
        switch (def.Kind)
        {
            case "separator":
                return new NativeMenuItemSeparator();
            case "submenu":
                var sub = new NativeMenuItem(def.Label ?? "");
                sub.Menu = new NativeMenu();
                foreach (var child in def.Items ?? [])
                    sub.Menu.Items.Add(ToNativeMenuItem(child));
                return sub;
            default:
                return new NativeMenuItem(def.Label ?? "");
        }
    }

    public void Dispose() => _listener.Stop();
}

// JSON DTOs
record OpenWindowRequest(string Title, string Url);
record NativeMenuItemDef(string Kind, string? Label, List<NativeMenuItemDef>? Items);
