using System.Diagnostics;
using System.Text.Json;
using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace DamascusUI;

public sealed class App : Application
{
    public static string FrontendUrl { get; private set; } = "http://127.0.0.1:3000";
    public static string WindowTitle { get; private set; } = "DamascusUI";
    public static bool ConfigTransparent { get; private set; } = false;
    public static bool ConfigDecorations { get; private set; } = true;
    private const int PreferredViewerPort = 45769;
    private static string LaunchWorkingDirectory { get; } = Environment.CurrentDirectory;
    private Process? _coreProcess;
    private ViewerServer? _server;
    private string _lastDockBadge = string.Empty;
    private long _nextWindowId = 1;
    private readonly ConcurrentDictionary<long, Window> _windows = new();
    private readonly ConcurrentDictionary<long, List<NativeMenuItemDef>> _windowMenuDefinitions = new();
    private long _focusedWindowId;
    private string? _activeMenuSignature;

    public override void Initialize()
    {
        Name = WindowTitle;
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        LoadConfig();
        TryApplyBundledDockIcon();
        var args = Environment.GetCommandLineArgs();
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--url" when i + 1 < args.Length:
                    FrontendUrl = args[++i];
                    break;
                case "--title" when i + 1 < args.Length:
                    WindowTitle = args[++i];
                    break;
                case "--spawn" when i + 1 < args.Length:
                    StartCoreProcess(args[++i]);
                    break;
            }
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Startup += (_, _) =>
            {
                _server = new ViewerServer(
                    PreferredViewerPort,
                    OpenNewWindow,
                    CloseWindow,
                    MinimizeWindow,
                    ToggleMaximize,
                    MoveWindow,
                    SetMenuBar,
                    SetDockIcon,
                    SetDockBadge,
                    SetWindowTitle,
                    SetWindowProps,
                    ShowSystemNotification,
                    FocusedWindowId);
                _server.Start();

                var mainWindowId = NextWindowId();
                desktop.MainWindow = CreateWindow(mainWindowId, WindowTitle, FrontendUrl, ConfigTransparent, ConfigDecorations);
                _ = RefreshDockIconFromFrontendAsync();
            };
            desktop.Exit += (_, _) =>
            {
                _server?.Dispose();
                if (_coreProcess is { HasExited: false })
                {
                    _coreProcess.Kill();
                    _coreProcess.Dispose();
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void LoadConfig()
    {
        try
        {
            var path = ResolveBundledFile("damascus.json");
            if (!File.Exists(path)) return;
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("url", out var u)) FrontendUrl = u.GetString()!;
            if (root.TryGetProperty("title", out var t)) WindowTitle = t.GetString()!;
            if (root.TryGetProperty("transparent", out var tr) && tr.ValueKind == JsonValueKind.True || tr.ValueKind == JsonValueKind.False)
                ConfigTransparent = tr.GetBoolean();
            if (root.TryGetProperty("decorations", out var dc) && dc.ValueKind == JsonValueKind.True || dc.ValueKind == JsonValueKind.False)
                ConfigDecorations = dc.GetBoolean();
            if (root.TryGetProperty("spawn", out var s) && s.ValueKind == JsonValueKind.String)
            {
                StartCoreProcess(s.GetString()!);
            }
        }
        catch { }
    }

    private void StartCoreProcess(string path)
    {
        var resolvedPath = ResolveLaunchPath(path);
        _coreProcess = Process.Start(new ProcessStartInfo(resolvedPath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(resolvedPath) ?? LaunchWorkingDirectory,
        });

        if (_coreProcess is null)
        {
            return;
        }
    }

    private Window CreateWindow(long windowId, string title, string url, bool transparent = false, bool decorations = true)
    {
        var source = BuildWindowUri(windowId, NormalizeViewerUrl(url));
        var window = new MainWindow(windowId, source, transparent, decorations)
        {
            Title = title,
        };
        _windows[windowId] = window;
        window.Activated += (_, _) =>
        {
            _focusedWindowId = windowId;
            RefreshApplicationMenuForWindow(windowId);
        };
        window.Closed += (_, _) =>
        {
            _windows.TryRemove(windowId, out _);
            _windowMenuDefinitions.TryRemove(windowId, out _);
            if (_focusedWindowId == windowId)
            {
                _focusedWindowId = _windows.Keys.OrderBy(id => id).FirstOrDefault();
                RefreshApplicationMenuForWindow(_focusedWindowId);
            }
        };
        ApplyDockBadge(window);
        return window;
    }

    private void OpenNewWindow(long sourceWindowId, string title, string url, bool transparent = false, bool decorations = true)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var win = CreateWindow(NextWindowId(), title, url, transparent, decorations);
            win.Show();
        });
    }

    public void SendOpenWindow(long sourceWindowId, string title, string url, bool transparent = false, bool decorations = true)
    {
        OpenNewWindow(sourceWindowId, title, NormalizeViewerUrl(url), transparent, decorations);
    }

    private void SetMenuBar(long windowId, List<NativeMenuItemDef> items)
    {
        if (_windows.ContainsKey(windowId) is false)
        {
            return;
        }

        _windowMenuDefinitions[windowId] = [.. items];
        if (_focusedWindowId == 0)
        {
            _focusedWindowId = windowId;
        }

        RefreshApplicationMenuForWindow(_focusedWindowId == windowId ? windowId : _focusedWindowId);
    }

    private void RefreshApplicationMenuForWindow(long windowId)
    {
        var items = _windowMenuDefinitions.TryGetValue(windowId, out var definitions)
            ? definitions
            : [];
        var signature = BuildMenuSignature(items);
        if (_activeMenuSignature == signature)
        {
            return;
        }

        if (MacMenuBar.IsSupported)
        {
            MacMenuBar.Apply(WindowTitle, items, HandleMenuAction);
        }
        _activeMenuSignature = signature;
    }

    private static string BuildMenuSignature(List<NativeMenuItemDef> items)
    {
        return JsonSerializer.Serialize(items, ShellJsonContext.Default.ListNativeMenuItemDef);
    }

    private void SetDockBadge(string text)
    {
        _lastDockBadge = text;
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        if (desktop.MainWindow is not null)
        {
            ApplyDockBadge(desktop.MainWindow);
        }
    }

    private void SetDockIcon(string icon)
    {
        MacDock.TrySetDockIconFromBase64(icon);
    }

    private void ShowSystemNotification(long windowId, NotificationRequest request)
    {
        // Run the blocking `defaults read` check off the UI thread so it can never stall
        // window move/resize (which also run on the UI thread).
        var server = _server;
        _ = Task.Run(() =>
        {
            if (MacFocus.TryIsFocusLikelyActive() == true)
            {
                var warningPayload = JsonSerializer.SerializeToElement(
                    new NotificationWarningPayload(
                        request.Title,
                        request.Body,
                        "focus-active",
                        "Focus/Do Not Disturb appears active; macOS may mute or delay the notification banner."),
                    ShellJsonContext.Default.NotificationWarningPayload);
                server?.PublishFromShell("notification.warning", warningPayload, "window", windowId);
            }
        });

        MacNotification.TryShow(
            request.Title,
            request.Body,
            () =>
            {
                var topic = string.IsNullOrWhiteSpace(request.Topic)
                    ? "notification.clicked"
                    : request.Topic!;
                _server?.PublishFromShell(
                    topic,
                    request.Payload ?? JsonSerializer.SerializeToElement(
                        new NotificationDefaultPayload(request.Title, request.Body),
                        ShellJsonContext.Default.NotificationDefaultPayload),
                    "window",
                    windowId);
            });
    }

    private void TryApplyBundledDockIcon()
    {
        var favicon = ResolveBundledFile("favicon.ico");
        if (File.Exists(favicon))
        {
            SetDockIcon(Convert.ToBase64String(File.ReadAllBytes(favicon)));
        }
    }

    private static string ResolveBundledFile(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, fileName),
            Path.Combine(AppContext.BaseDirectory, "..", "Resources", fileName),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", fileName),
            Path.Combine(LaunchWorkingDirectory, "shell", fileName),
            Path.Combine(LaunchWorkingDirectory, fileName),
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return Path.GetFullPath(candidates[0]);
    }

    private static string ResolveLaunchPath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        var candidates = new[]
        {
            Path.Combine(LaunchWorkingDirectory, path),
            Path.Combine(AppContext.BaseDirectory, path),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", path),
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return Path.GetFullPath(Path.Combine(LaunchWorkingDirectory, path));
    }

    private async Task RefreshDockIconFromFrontendAsync()
    {
        try
        {
            using var http = new HttpClient();
            using var response = await http.GetAsync(new Uri(new Uri(FrontendUrl), "/favicon.ico"));
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes.Length == 0)
            {
                return;
            }

            var base64 = Convert.ToBase64String(bytes);
            Dispatcher.UIThread.Post(() => SetDockIcon(base64));
        }
        catch
        {
        }
    }

    private void ApplyDockBadge(Window window)
    {
        window.Tag = _lastDockBadge;
    }

    private void CloseWindow(long windowId)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_windows.TryGetValue(windowId, out var window))
            {
                window.Close();
            }
        });
    }

    private void MinimizeWindow(long windowId)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_windows.TryGetValue(windowId, out var window))
            {
                window.WindowState = WindowState.Minimized;
            }
        });
    }

    private void ToggleMaximize(long windowId)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_windows.TryGetValue(windowId, out var window))
            {
                window.WindowState = window.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
        });
    }

    private void MoveWindow(long windowId, double dx, double dy)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_windows.TryGetValue(windowId, out var window))
            {
                var pos = window.Position;
                window.Position = new PixelPoint(
                    (int)(pos.X + dx),
                    (int)(pos.Y + dy));
            }
        });
    }

    private void SetWindowTitle(long windowId, string title)
    {
        if (_windows.TryGetValue(windowId, out var window))
        {
            window.Title = title;
        }
    }

    private void SetWindowProps(long windowId, bool? transparent, bool? decorations)
    {
        if (!_windows.TryGetValue(windowId, out var window))
        {
            return;
        }

        if (transparent.HasValue)
        {
            window.TransparencyLevelHint = transparent.Value
                ? [WindowTransparencyLevel.Transparent]
                : [];
            window.Background = transparent.Value ? Brushes.Transparent : null;
        }

        if (decorations.HasValue)
        {
            window.WindowDecorations = decorations.Value
                ? Avalonia.Controls.WindowDecorations.Full
                : Avalonia.Controls.WindowDecorations.None;
            if (!decorations.Value)
            {
                window.ExtendClientAreaToDecorationsHint = true;
                window.ExtendClientAreaTitleBarHeightHint = 0;
            }
        }
    }

    public string NormalizeViewerUrl(string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        return FrontendUrl;
    }

    private Uri BuildWindowUri(long windowId, string url)
    {
        var builder = new UriBuilder(url);
        var windowParam = $"shellWindowId={windowId}&shellPort={_server!.ActualPort}";
        if (string.IsNullOrEmpty(builder.Query))
        {
            builder.Query = windowParam;
        }
        else
        {
            builder.Query = $"{builder.Query.TrimStart('?')}&{windowParam}";
        }
        return builder.Uri;
    }

    private long NextWindowId()
    {
        return _nextWindowId++;
    }

    public long FocusedWindowId()
    {
        return _focusedWindowId;
    }

    public ViewerServer? GetViewerServer()
    {
        return _server;
    }

    public NativeMenuItem CreateActionMenuItem(string label, Action onClick)
    {
        var item = new NativeMenuItem(label);
        item.Click += (_, _) => onClick();
        return item;
    }

    public NativeMenuItemSeparator CreateSeparatorMenuItem()
    {
        return new NativeMenuItemSeparator();
    }

    public NativeMenuItem CreateAboutMenuItem()
    {
        return CreateActionMenuItem("About Todos", () =>
        {
            _server?.PublishFromShell(
                "menu.about",
                JsonSerializer.SerializeToElement(
                    new AboutEventPayload("About Todos", "Todos\nDamascusUI example app"),
                    ShellJsonContext.Default.AboutEventPayload),
                "window",
                FocusedWindowId());
        });
    }

    private NativeMenuItem CreateMenuActionItem(string label, string id)
    {
        return CreateActionMenuItem(label, () => HandleMenuAction(id, label));
    }

    private void HandleMenuAction(string id)
    {
        HandleMenuAction(id, id);
    }

    private void HandleMenuAction(string id, string fallbackLabel)
    {
        var label = FindMenuLabel(_focusedWindowId, id) ?? fallbackLabel;
        if (id == "new")
        {
            SendOpenWindow(FocusedWindowId(), "Todos", FrontendUrl, ConfigTransparent, ConfigDecorations);
        }

        _server?.PublishFromShell(
            $"menu.{id}",
            JsonSerializer.SerializeToElement(
                new MenuItemClickedPayload(id, label),
                ShellJsonContext.Default.MenuItemClickedPayload),
            "window",
            FocusedWindowId());
    }

    private NativeMenuItem ToNativeMenuItem(NativeMenuItemDef def)
    {
        return def.Kind switch
        {
            "separator" => CreateSeparatorMenuItem(),
            "submenu" => CreateSubmenuItem(def),
            _ => CreateMenuActionItem(def.Label ?? string.Empty, def.Id ?? string.Empty),
        };
    }

    private NativeMenuItem CreateSubmenuItem(NativeMenuItemDef def)
    {
        var item = new NativeMenuItem(def.Label ?? string.Empty)
        {
            Menu = new NativeMenu(),
        };
        foreach (var child in def.Items ?? [])
        {
            item.Menu.Items.Add(ToNativeMenuItem(child));
        }
        return item;
    }

    private string? FindMenuLabel(long windowId, string id)
    {
        if (!_windowMenuDefinitions.TryGetValue(windowId, out var items))
        {
            return id == "about" ? "About Todos" : null;
        }

        return FindMenuLabel(items, id) ?? (id == "about" ? "About Todos" : null);
    }

    private static string? FindMenuLabel(IEnumerable<NativeMenuItemDef> items, string id)
    {
        foreach (var item in items)
        {
            if (item.Id == id && !string.IsNullOrWhiteSpace(item.Label))
            {
                return item.Label;
            }

            if (item.Items is not null)
            {
                var nested = FindMenuLabel(item.Items, id);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }

        return null;
    }
}
