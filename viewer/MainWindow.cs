using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace DamascusUI;

public sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = App.WindowTitle;
        Width = 1024;
        Height = 768;

        var webview = new NativeWebView
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        webview.NavigationStarting += (_, e) =>
        {
            if (e.Url?.StartsWith("damascus://") == true)
            {
                e.Cancel = true;
                HandleProtocol(e.Url);
            }
        };

        Content = webview;
        webview.Source = new Uri(App.FrontendUrl);
    }

    private void HandleProtocol(string url)
    {
        // damascus://set-title/Hello%20World
        var uri = new Uri(url);
        var parts = uri.AbsolutePath.TrimStart('/').Split('/');

        Dispatcher.UIThread.Post(() =>
        {
            switch (parts[0])
            {
                case "set-title":
                    if (parts.Length > 1)
                        Title = Uri.UnescapeDataString(parts[1]);
                    break;
                case "set-icon":
                    // TODO: set window icon from base64 data
                    break;
            }
        });
    }
}
