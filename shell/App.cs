using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace DamascusUI;

public sealed class App : Application
{
    public static string FrontendUrl { get; private set; } = "http://127.0.0.1:3000";
    private Process? _coreProcess;
    private ViewerServer? _server;

    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--url" when i + 1 < args.Length:
                    FrontendUrl = args[++i];
                    break;
                case "--spawn" when i + 1 < args.Length:
                    var bin = args[++i];
                    _coreProcess = Process.Start(new ProcessStartInfo(bin)
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                    });
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
