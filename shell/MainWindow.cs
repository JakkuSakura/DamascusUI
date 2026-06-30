using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DamascusUI;

public sealed class MainWindow : Window
{
    private const int ResizeBorder = 4;
    public long WindowId { get; }

    public MainWindow(long windowId, Uri source, bool transparent = false, bool decorations = true)
    {
        WindowId = windowId;
        Title = App.WindowTitle;
        Width = 1024;
        Height = 768;
        var useMacFallbackChrome = OperatingSystem.IsMacOS() && !decorations;

        if (!decorations)
        {
            WindowDecorations = useMacFallbackChrome
                ? Avalonia.Controls.WindowDecorations.BorderOnly
                : Avalonia.Controls.WindowDecorations.None;
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaTitleBarHeightHint = 0;
            CanResize = true;
            if (!useMacFallbackChrome)
            {
                // WindowDecorations.None may strip NSResizableWindowMask on macOS.
                // Restore it via native call once the window handle is available.
                Opened += (_, _) => MacWindowHelper.RestoreResizeMask(this);
            }
        }

        if (transparent)
        {
            TransparencyLevelHint = OperatingSystem.IsMacOS()
                ? [WindowTransparencyLevel.Mica, WindowTransparencyLevel.Blur]
                : [WindowTransparencyLevel.Transparent];
            Background = Brushes.Transparent;
            if (!OperatingSystem.IsMacOS())
            {
                TransparencyBackgroundFallback = Brushes.Transparent;
            }
            if (!useMacFallbackChrome)
            {
                Opened += (_, _) => MacWindowHelper.DisableWebViewBackground(this);
            }
        }

        var webview = new NativeWebView
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            Background = transparent ? Brushes.Transparent : null,
        };

        // Wrap in a container with a small transparent border for native resize handles
        Content = new Border
        {
            Child = webview,
            Margin = decorations || useMacFallbackChrome ? new Thickness(0) : new Thickness(ResizeBorder),
            Background = transparent ? Brushes.Transparent : null,
        };

        webview.Source = source;
    }
}
