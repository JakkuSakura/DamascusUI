using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace DamascusUI;

public sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = "DamascusUI";
        Width = 1024;
        Height = 768;

        var webview = new NativeWebView
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        Content = webview;
        webview.Source = new Uri(App.FrontendUrl);
    }
}
