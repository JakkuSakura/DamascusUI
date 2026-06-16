using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DamascusUI;

public sealed class MainWindow : Window
{
    public const int ViewerPort = 45769;
    public long WindowId { get; }

    public MainWindow(long windowId, Uri source, bool transparent = false, bool decorations = true)
    {
        WindowId = windowId;
        Title = App.WindowTitle;
        Width = 1024;
        Height = 768;

        if (!decorations)
        {
            WindowDecorations = Avalonia.Controls.WindowDecorations.None;
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaTitleBarHeightHint = 0;
        }

        if (transparent)
        {
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
            Background = Brushes.Transparent;
            TransparencyBackgroundFallback = Brushes.Transparent;
        }

        var webview = new NativeWebView
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            Background = transparent ? Brushes.Transparent : null,
        };

        Content = webview;
        webview.Source = source;
    }
}
