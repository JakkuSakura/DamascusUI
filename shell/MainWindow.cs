using Avalonia.Controls;

namespace DamascusUI;

public sealed class MainWindow : Window
{
    public const int ViewerPort = 45769;
    public long WindowId { get; }

    public MainWindow(long windowId, Uri source)
    {
        WindowId = windowId;
        Title = App.WindowTitle;
        Width = 1024;
        Height = 768;

        var webview = new NativeWebView
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };

        Content = webview;
        webview.Source = source;
    }
}
