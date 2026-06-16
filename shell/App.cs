using System.Diagnostics;
using System.Text.Json;
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

    public override void Initialize()
    {
        Name = WindowTitle;
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        LoadConfig();
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
            desktop.MainWindow = new MainWindow();
            desktop.Startup += (_, _) =>
            {
                _server = new ViewerServer(MainWindow.ViewerPort, OpenNewWindow, SetMenuBar, SetDockBadge);
                _server.Start();
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

    private void OpenNewWindow(string title, string url)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var win = new Window
            {
                Title = title,
                Width = 1024,
                Height = 768,
                Content = new NativeWebView { Source = new Uri(url) },
            };
            ApplyDockBadge(win);
            win.Show();
        });
    }

    public void SendOpenWindow(string title, string url)
    {
        OpenNewWindow(title, NormalizeViewerUrl(url));
    }

    private void SetMenuBar(List<NativeMenuItem> items)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is not null)
        {
            var menu = new NativeMenu();
            foreach (var item in items) menu.Items.Add(item);
            NativeMenu.SetMenu(this, menu);
        }
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

    private void ApplyDockBadge(Window window)
    {
        window.Tag = _lastDockBadge;
    }

    public string NormalizeViewerUrl(string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        return FrontendUrl;
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
            var owner = (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            var dialog = new Window
            {
                Title = "About Todos",
                Width = 360,
                Height = 180,
                CanResize = false,
                Content = new TextBlock
                {
                    Text = "Todos\nDamascusUI example app",
                    TextAlignment = Avalonia.Media.TextAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                },
            };

            if (owner is null)
            {
                dialog.Show();
                return;
            }

            _ = dialog.ShowDialog(owner);
        });
    }
}
