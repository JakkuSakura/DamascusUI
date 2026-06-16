using Avalonia.Controls;

namespace DamascusUI;

public sealed class MainWindow : Window
{
    public const int ViewerPort = 45769;

    public MainWindow()
    {
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
}
