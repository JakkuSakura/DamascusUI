using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace DamascusUI;

public sealed class App : Application
{
    public static string FrontendUrl { get; private set; } = "http://127.0.0.1:3000";
    public static string WindowTitle { get; private set; } = "DamascusUI";
    private Process? _coreProcess;
    private ViewerServer? _server;

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
                _server = new ViewerServer(MainWindow.ViewerPort, OpenNewWindow, SetMenuBar);
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
        var win = new Window
        {
            Title = title,
            Width = 1024,
            Height = 768,
            Content = new NativeWebView { Source = new Uri(url) },
        };
        win.Show();
    }

    private void SetMenuBar(List<NativeMenuItem> items)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is not null)
        {
            var menu = new NativeMenu();
            foreach (var item in items) menu.Items.Add(item);
            NativeMenu.SetMenu(desktop.MainWindow, menu);
        }
    }
}
