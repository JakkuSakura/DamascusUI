using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

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

        try
        {
            webview.Source = new Uri(App.FrontendUrl);
            Content = webview;
        }
        catch
        {
            var panel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 16,
            };
            panel.Children.Add(new TextBlock
            {
                Text = "DamascusUI",
                FontSize = 28,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            panel.Children.Add(new TextBlock
            {
                Text = App.FrontendUrl,
                FontSize = 14,
                Foreground = Brushes.DodgerBlue,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            var btn = new Button { Content = "Open", HorizontalAlignment = HorizontalAlignment.Center };
            btn.Click += (_, _) => Process.Start(new ProcessStartInfo(App.FrontendUrl) { UseShellExecute = true });
            panel.Children.Add(btn);
            Content = panel;
        }
    }
}
