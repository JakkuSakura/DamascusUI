using System.Diagnostics;
using System.Text.Json;
using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace DamascusUI;

public sealed class App : Application
{
    public static string FrontendUrl { get; private set; } = "http://127.0.0.1:3000";
    public static string WindowTitle { get; private set; } = "DamascusUI";
    private Process? _coreProcess;
    private ViewerServer? _server;
    private string _lastDockBadge = string.Empty;
    private long _nextWindowId = 1;
    private readonly ConcurrentDictionary<long, Window> _windows = new();
    private long _focusedWindowId;

    public override void Initialize()
    {
        Name = WindowTitle;
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        LoadConfig();
        TryApplyBundledDockIcon();
        var args = Environment.GetCommandLineArgs();
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--url" when i + 1 < args.Length:
                    FrontendUrl = args[++i];
                    break;
                case "--title" when i + 1 < args.Length:
                    WindowTitle = args[++i];
                    break;
                case "--spawn" when i + 1 < args.Length:
                    var bin = args[++i];
                    _coreProcess = Process.Start(new ProcessStartInfo(bin)
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                    });
                    // Read the bound address from core's stdout
                    if (_coreProcess != null)
                    {
                        var line = _coreProcess.StandardOutput.ReadLine();
                        if (line?.StartsWith("DAMASCUS_ADDR=") == true)
                            FrontendUrl = line["DAMASCUS_ADDR=".Length..];
                    }
                    break;
            }
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindowId = NextWindowId();
            desktop.MainWindow = CreateWindow(mainWindowId, WindowTitle, FrontendUrl);
            desktop.Startup += (_, _) =>
            {
                _server = new ViewerServer(
                    MainWindow.ViewerPort,
                    OpenNewWindow,
                    SetMenuBar,
                    SetDockIcon,
                    SetDockBadge,
                    SetWindowTitle,
                    FocusedWindowId);
                _server.Start();
                _ = RefreshDockIconFromFrontendAsync();
            };
            desktop.Exit += (_, _) =>
            {
                _server?.Dispose();
                if (_coreProcess is { HasExited: false })
                {
                    _coreProcess.Kill();
                    _coreProcess.Dispose();
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void LoadConfig()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "damascus.json");
            if (!File.Exists(path)) return;
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("url", out var u)) FrontendUrl = u.GetString()!;
            if (root.TryGetProperty("title", out var t)) WindowTitle = t.GetString()!;
            if (root.TryGetProperty("spawn", out var s) && s.ValueKind == JsonValueKind.String)
                _coreProcess = Process.Start(new ProcessStartInfo(s.GetString()!)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                });
        }
        catch { }
    }

    private Window CreateWindow(long windowId, string title, string url)
    {
        var source = BuildWindowUri(windowId, NormalizeViewerUrl(url));
        var window = new MainWindow(windowId, source)
        {
            Title = title,
        };
        _windows[windowId] = window;
        window.Activated += (_, _) => _focusedWindowId = windowId;
        window.Closed += (_, _) => _windows.TryRemove(windowId, out _);
        ApplyDockBadge(window);
        return window;
    }

    private void OpenNewWindow(long sourceWindowId, string title, string url)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var win = CreateWindow(NextWindowId(), title, url);
            win.Show();
        });
    }

    public void SendOpenWindow(long sourceWindowId, string title, string url)
    {
        OpenNewWindow(sourceWindowId, title, NormalizeViewerUrl(url));
    }

    private void SetMenuBar(long windowId, List<NativeMenuItemDef> items)
    {
        if (_windows.TryGetValue(windowId, out var window) is false)
        {
            return;
        }

        var appMenu = new NativeMenu();
        var appRoot = new NativeMenuItem(WindowTitle);
        appRoot.Menu = new NativeMenu();
        appRoot.Menu.Items.Add(CreateMenuActionItem("About Todos", "about"));
        appMenu.Items.Add(appRoot);
        NativeMenu.SetMenu(this, appMenu);

        var windowMenu = new NativeMenu();
        var fileMenu = new NativeMenuItem("File");
        fileMenu.Menu = new NativeMenu();
        foreach (var item in items)
        {
            if (item.Id == "about")
            {
                continue;
            }

            fileMenu.Menu.Items.Add(ToNativeMenuItem(item));
        }
        windowMenu.Items.Add(fileMenu);
        NativeMenu.SetMenu(window, windowMenu);
    }

    private void SetDockBadge(string text)
    {
        _lastDockBadge = text;
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        if (desktop.MainWindow is not null)
        {
            ApplyDockBadge(desktop.MainWindow);
        }
    }

    private void SetDockIcon(string icon)
    {
        MacDock.TrySetDockIconFromBase64(icon);
    }

    private void TryApplyBundledDockIcon()
    {
        var favicon = Path.Combine(AppContext.BaseDirectory, "favicon.ico");
        if (File.Exists(favicon))
        {
            SetDockIcon(Convert.ToBase64String(File.ReadAllBytes(favicon)));
        }
    }

    private async Task RefreshDockIconFromFrontendAsync()
    {
        try
        {
            using var http = new HttpClient();
            using var response = await http.GetAsync(new Uri(new Uri(FrontendUrl), "/favicon.ico"));
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes.Length == 0)
            {
                return;
            }

            var base64 = Convert.ToBase64String(bytes);
            Dispatcher.UIThread.Post(() => SetDockIcon(base64));
        }
        catch
        {
        }
    }

    private void ApplyDockBadge(Window window)
    {
        window.Tag = _lastDockBadge;
    }

    private void SetWindowTitle(long windowId, string title)
    {
        if (_windows.TryGetValue(windowId, out var window))
        {
            window.Title = title;
        }
    }

    public string NormalizeViewerUrl(string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        return FrontendUrl;
    }

    private Uri BuildWindowUri(long windowId, string url)
    {
        var builder = new UriBuilder(url);
        var windowParam = $"shellWindowId={windowId}";
        if (string.IsNullOrEmpty(builder.Query))
        {
            builder.Query = windowParam;
        }
        else
        {
            builder.Query = $"{builder.Query.TrimStart('?')}&{windowParam}";
        }
        return builder.Uri;
    }

    private long NextWindowId()
    {
        return _nextWindowId++;
    }

    public long FocusedWindowId()
    {
        return _focusedWindowId;
    }

    public ViewerServer? GetViewerServer()
    {
        return _server;
    }

    public NativeMenuItem CreateActionMenuItem(string label, Action onClick)
    {
        var item = new NativeMenuItem(label);
        item.Click += (_, _) => onClick();
        return item;
    }

    public NativeMenuItemSeparator CreateSeparatorMenuItem()
    {
        return new NativeMenuItemSeparator();
    }

    public NativeMenuItem CreateAboutMenuItem()
    {
        return CreateActionMenuItem("About Todos", () =>
        {
            _server?.PublishFromShell("menu.about", JsonSerializer.SerializeToElement(new
            {
                title = "About Todos",
                message = "Todos\nDamascusUI example app",
            }), "window", FocusedWindowId());
        });
    }

    private NativeMenuItem CreateMenuActionItem(string label, string id)
    {
        return CreateActionMenuItem(label, () =>
        {
            if (id == "new")
            {
                SendOpenWindow(FocusedWindowId(), "Todos", FrontendUrl);
            }

            _server?.PublishFromShell(
                $"menu.{id}",
                JsonSerializer.SerializeToElement(new { id, label }),
                "window",
                FocusedWindowId());
        });
    }

    private NativeMenuItem ToNativeMenuItem(NativeMenuItemDef def)
    {
        return def.Kind switch
        {
            "separator" => CreateSeparatorMenuItem(),
            "submenu" => CreateSubmenuItem(def),
            _ => CreateMenuActionItem(def.Label ?? string.Empty, def.Id ?? string.Empty),
        };
    }

    private NativeMenuItem CreateSubmenuItem(NativeMenuItemDef def)
    {
        var item = new NativeMenuItem(def.Label ?? string.Empty)
        {
            Menu = new NativeMenu(),
        };
        foreach (var child in def.Items ?? [])
        {
            item.Menu.Items.Add(ToNativeMenuItem(child));
        }
        return item;
    }
}
