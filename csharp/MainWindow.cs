using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.WebView;

namespace DamascusUI;

public sealed class MainWindow : Window
{
    private readonly WebView _webview;

    public MainWindow()
    {
        Title = "DamascusUI";
        Width = 1024;
        Height = 768;

        _webview = new WebView
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        Content = _webview;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _webview.Url = new Uri(App.FrontendUrl);
    }
}
