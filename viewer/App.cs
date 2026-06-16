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

    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1) FrontendUrl = args[1];
        if (args.Length > 2) WindowTitle = args[2];

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(OpenNewWindow, SetMenuBar);
            desktop.Startup += async (_, _) =>
            {
                if (desktop.MainWindow is MainWindow mw)
                    await mw.ConnectViewerAsync();
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
