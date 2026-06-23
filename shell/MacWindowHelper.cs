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
            var nsWindow = GetNSWindow(window);
            if (nsWindow == IntPtr.Zero) return;

            var selStyleMask = sel_registerName("styleMask");
            var selSetStyleMask = sel_registerName("setStyleMask:");
            var currentMask = nint_objc_msgSend(nsWindow, selStyleMask);

            if ((currentMask & NSResizableWindowMask) != 0) return;

            nint_void_objc_msgSend(nsWindow, selSetStyleMask, currentMask | NSResizableWindowMask);
        }
        catch { }
    }

    /// <summary>
    /// Disables the WKWebView opaque background so per-pixel transparency works.
    /// Must be called after the window is shown (Opened event).
    /// </summary>
    public static void DisableWebViewBackground(Window window)
    {
        if (!OperatingSystem.IsMacOS()) return;
        TryDisableWebViewBackground(window);
    }

    [SupportedOSPlatform("macos")]
    private static void TryDisableWebViewBackground(Window window)
    {
        try
        {
            var nsWindow = GetNSWindow(window);
            if (nsWindow == IntPtr.Zero) return;

            var selContentView = sel_registerName("contentView");
            var contentView = IntPtr_objc_msgSend(nsWindow, selContentView);
            if (contentView == IntPtr.Zero) return;

            FindAndDisableWKWebViewBackground(contentView);
        }
        catch { }
    }

    [SupportedOSPlatform("macos")]
    private static void FindAndDisableWKWebViewBackground(IntPtr nsView)
    {
        var wkWebViewClass = objc_getClass("WKWebView");
        if (wkWebViewClass == IntPtr.Zero) return;

        var selIsKindOfClass = sel_registerName("isKindOfClass:");
        var result = bool_objc_msgSend(nsView, selIsKindOfClass, wkWebViewClass);
        if (result)
        {
            var selSetValue = sel_registerName("setValue:forKey:");
            var nsFalse = objc_getClass("NSNumber");
            var selNumberWithBool = sel_registerName("numberWithBool:");
            var falseNum = IntPtr_objc_msgSend_int(nsFalse, selNumberWithBool, 0);
            IntPtr_void_objc_msgSend_IntPtr(nsView, selSetValue, falseNum, sel_registerName("drawsBackground"));
            return;
        }

        var selSubviews = sel_registerName("subviews");
        var subviews = IntPtr_objc_msgSend(nsView, selSubviews);
        if (subviews == IntPtr.Zero) return;

        var selCount = sel_registerName("count");
        var count = nint_objc_msgSend(subviews, selCount);
        var selObjectAtIndex = sel_registerName("objectAtIndex:");

        for (nint i = 0; i < count; i++)
        {
            var subview = IntPtr_objc_msgSend_nint(subviews, selObjectAtIndex, i);
            if (subview != IntPtr.Zero)
            {
                FindAndDisableWKWebViewBackground(subview);
            }
        }
    }

    [SupportedOSPlatform("macos")]
    private static IntPtr GetNSWindow(Window window)
    {
        var viewHandle = window.TryGetPlatformHandle();
        if (viewHandle is null) return IntPtr.Zero;

        var nsView = viewHandle.Handle;
        var selWindow = sel_registerName("window");
        return IntPtr_objc_msgSend(nsView, selWindow);
    }

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint nint_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern void nint_void_objc_msgSend(IntPtr receiver, IntPtr selector, nint arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern bool bool_objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend_int(IntPtr receiver, IntPtr selector, int arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void IntPtr_void_objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend_nint(IntPtr receiver, IntPtr selector, nint arg);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr objc_getClass(string name);
}
