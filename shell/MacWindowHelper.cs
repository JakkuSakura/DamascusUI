using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;

namespace DamascusUI;

internal static class MacWindowHelper
{
    // NSWindowStyleMask.Resizable = 8
    private const nint NSResizableWindowMask = 8;

    /// <summary>
    /// Re-adds NSResizableWindowMask after Avalonia's WindowDecorations.None removes it.
    /// Must be called after the window is shown (Opened event).
    /// </summary>
    public static void RestoreResizeMask(Window window)
    {
        if (!OperatingSystem.IsMacOS()) return;
        TryRestoreResizeMask(window);
    }

    [SupportedOSPlatform("macos")]
    private static void TryRestoreResizeMask(Window window)
    {
        try
        {
            var viewHandle = window.TryGetPlatformHandle();
            if (viewHandle is null) return;

            var nsView = viewHandle.Handle;
            var selWindow = sel_registerName("window");
            var nsWindow = IntPtr_objc_msgSend(nsView, selWindow);
            if (nsWindow == IntPtr.Zero) return;

            var selStyleMask = sel_registerName("styleMask");
            var selSetStyleMask = sel_registerName("setStyleMask:");
            var currentMask = nint_objc_msgSend(nsWindow, selStyleMask);

            if ((currentMask & NSResizableWindowMask) != 0) return;

            nint_void_objc_msgSend(nsWindow, selSetStyleMask, currentMask | NSResizableWindowMask);
        }
        catch { }
    }

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint nint_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void nint_void_objc_msgSend(IntPtr receiver, IntPtr selector, nint arg);
}
