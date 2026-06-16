using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace DamascusUI;

public sealed class MainWindow : Window
{
    private readonly Action<string, string> _openWindow;
    private ViewerClient? _client;

    public MainWindow(Action<string, string> openWindow)
    {
        _openWindow = openWindow;
        Title = App.WindowTitle;
        Width = 1024;
        Height = 768;

        var webview = new NativeWebView
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };

        Content = webview;
        webview.Source = new Uri(App.FrontendUrl);
    }

    public async Task ConnectViewerAsync()
    {
        _client = new ViewerClient(App.FrontendUrl, _openWindow);
        await _client.ConnectAsync();
    }
}
