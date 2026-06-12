// Protocol types mirroring wit/ — used by the Avalonia desktop viewer.
// Communication is via WebSocket with JSON messages carrying a "type" discriminator.

using System.Text.Json.Serialization;

namespace DamascusUI.Protocol;

// ── types ────────────────────────────────────────────────────────────────────

public record PointI32(int X, int Y);
public record PointF32(float X, float Y);
public record SizeI32(int Width, int Height);
public record SizeF32(float Width, float Height);
public record RectI32(PointI32 Origin, SizeI32 Size);
public record Color(byte R, byte G, byte B, byte A = 255);

// ── window ────────────────────────────────────────────────────────────────────

public record OpenRequest(
    string Title,
    uint Width,
    uint Height,
    SizeI32? MinSize,
    SizeI32? MaxSize,
    bool Resizable,
    bool Maximized,
    uint? ParentId
) : IProtocolMessage
{
    public string Type => "window.open";
}

public record OpenResponse(uint SurfaceId) : IProtocolMessage
{
    public string Type => "window.open-response";
}

public record CloseRequest(uint SurfaceId) : IProtocolMessage
{
    public string Type => "window.close";
}

public record SetProperties(
    uint SurfaceId,
    string? Title,
    SizeI32? Size,
    bool? Resizable,
    bool? Minimized,
    bool? Maximized,
    bool? Fullscreen
) : IProtocolMessage
{
    public string Type => "window.set-properties";
}

[JsonDerivedType(typeof(WindowResized), "window.resized")]
[JsonDerivedType(typeof(WindowClosed), "window.closed")]
[JsonDerivedType(typeof(FocusChanged), "window.focus-changed")]
[JsonDerivedType(typeof(FullscreenChanged), "window.fullscreen-changed")]
[JsonDerivedType(typeof(ScaleChanged), "window.scale-changed")]
public abstract record WindowEvent : IProtocolMessage;

public record WindowResized(uint SurfaceId, uint Width, uint Height) : WindowEvent;
public record WindowClosed(uint SurfaceId) : WindowEvent;
public record FocusChanged(uint SurfaceId, bool Focused) : WindowEvent;
public record FullscreenChanged(uint SurfaceId, bool Fullscreen) : WindowEvent;
public record ScaleChanged(uint SurfaceId, float Scale) : WindowEvent;

// ── render ────────────────────────────────────────────────────────────────────

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImageFormat { Rgba8, Jpeg, Png }

public record DirtyRect(RectI32 Rect);

public record Frame(
    uint SurfaceId,
    ulong Sequence,
    SizeI32 Size,
    ImageFormat Format,
    byte[] Data,
    DirtyRect[]? Dirty
) : IProtocolMessage
{
    public string Type => "render.frame";
}

// ── input ─────────────────────────────────────────────────────────────────────

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MouseButton { Left, Right, Middle, Back, Forward }

public record Modifiers(bool Shift, bool Control, bool Alt, bool Meta);

public record PointerMoved(uint SurfaceId, PointF32 Position) : IProtocolMessage
{
    public string Type => "input.pointer-moved";
}

public record PointerPressed(
    uint SurfaceId, MouseButton Button, PointF32 Position, Modifiers Modifiers
) : IProtocolMessage
{
    public string Type => "input.pointer-pressed";
}

public record PointerReleased(
    uint SurfaceId, MouseButton Button, PointF32 Position, Modifiers Modifiers
) : IProtocolMessage
{
    public string Type => "input.pointer-released";
}

public record PointerWheel(
    uint SurfaceId, float DeltaX, float DeltaY, Modifiers Modifiers
) : IProtocolMessage
{
    public string Type => "input.pointer-wheel";
}

public record PointerEnter(uint SurfaceId, bool Entered, PointF32 Position) : IProtocolMessage
{
    public string Type => "input.pointer-enter";
}

public record KeyEvent(
    uint SurfaceId, string Key, bool Pressed, Modifiers Modifiers
) : IProtocolMessage
{
    public string Type => "input.key";
}

public record CharEvent(uint SurfaceId, uint Char) : IProtocolMessage
{
    public string Type => "input.char";
}

public record TouchPoint(ulong Id, PointF32 Position, float Pressure);

public record TouchStarted(uint SurfaceId, TouchPoint[] Points) : IProtocolMessage
{
    public string Type => "input.touch-started";
}

public record TouchMoved(uint SurfaceId, TouchPoint[] Points) : IProtocolMessage
{
    public string Type => "input.touch-moved";
}

public record TouchEnded(uint SurfaceId, TouchPoint[] Points) : IProtocolMessage
{
    public string Type => "input.touch-ended";
}

public record SetCursor(uint SurfaceId, byte Cursor) : IProtocolMessage
{
    public string Type => "input.set-cursor";
}

// ── menu ──────────────────────────────────────────────────────────────────────

[JsonDerivedType(typeof(MenuAction), "action")]
[JsonDerivedType(typeof(MenuCheck), "check")]
[JsonDerivedType(typeof(MenuRadio), "radio")]
[JsonDerivedType(typeof(MenuSeparator), "separator")]
[JsonDerivedType(typeof(MenuSubmenu), "submenu")]
public abstract record MenuItem;

public record MenuAction(string Id, string Label) : MenuItem;
public record MenuCheck(string Id, string Label, bool Checked) : MenuItem;
public record MenuRadio(string Id, string Label, string Group, bool Selected) : MenuItem;
public record MenuSeparator : MenuItem;
public record MenuSubmenu(string Label, MenuItem[] Items) : MenuItem;

public record SetMenuBar(uint SurfaceId, MenuItem[] Items) : IProtocolMessage
{
    public string Type => "menu.set-bar";
}

public record ShowContextMenu(uint SurfaceId, MenuItem[] Items) : IProtocolMessage
{
    public string Type => "menu.show-context";
}

public record MenuActivated(uint SurfaceId, string Path) : IProtocolMessage
{
    public string Type => "menu.activated";
}

// ── dialog ────────────────────────────────────────────────────────────────────

public record FileFilter(string Name, string[] Extensions);

public record OpenFileDialog(
    uint SurfaceId, string Title, FileFilter[] Filters, bool MultiSelect
) : IProtocolMessage
{
    public string Type => "dialog.open-file";
}

public record SaveFileDialog(
    uint SurfaceId, string Title, FileFilter[] Filters, string? DefaultName
) : IProtocolMessage
{
    public string Type => "dialog.save-file";
}

public record FileResult(string[] Paths, bool Cancelled) : IProtocolMessage
{
    public string Type => "dialog.file-result";
}

public record MessageBox(
    uint SurfaceId, string Title, string Message, string Kind, string[] Buttons
) : IProtocolMessage
{
    public string Type => "dialog.message-box";
}

public record MessageBoxResult(uint Button) : IProtocolMessage
{
    public string Type => "dialog.message-result";
}

// ── tray ──────────────────────────────────────────────────────────────────────

[JsonDerivedType(typeof(TrayMenuAction), "action")]
[JsonDerivedType(typeof(TrayMenuSeparator), "separator")]
public abstract record TrayMenuItem;

public record TrayMenuAction(string Id, string Label) : TrayMenuItem;
public record TrayMenuSeparator : TrayMenuItem;

public record SetTrayIcon(byte[] Icon, string Tooltip, TrayMenuItem[] Menu) : IProtocolMessage
{
    public string Type => "tray.set-icon";
}

public record TrayActivated(bool LeftClick) : IProtocolMessage
{
    public string Type => "tray.activated";
}

public record TrayMenuSelected(string Id) : IProtocolMessage
{
    public string Type => "tray.menu-selected";
}

public record ShowNotification(string Title, string Body, byte[]? Icon) : IProtocolMessage
{
    public string Type => "tray.notification";
}

public record NotificationClicked(uint Id) : IProtocolMessage
{
    public string Type => "tray.notification-clicked";
}

// ── base interface ────────────────────────────────────────────────────────────

public interface IProtocolMessage
{
    string Type { get; }
}
