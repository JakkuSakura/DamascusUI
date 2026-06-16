
using Avalonia.Controls;

namespace DamascusUI;

public sealed class MainWindow : Window
{
    private readonly Action<string, string> _openWindow;
    private readonly Action<List<NativeMenuItem>> _setMenu;
    private ViewerClient? _client;

    public MainWindow(Action<string, string> openWindow, Action<List<NativeMenuItem>> setMenu)
    {
        _openWindow = openWindow;
        _setMenu = setMenu;
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
        _client = new ViewerClient(App.FrontendUrl, _openWindow, _setMenu);
        await _client.ConnectAsync();
    }
}
