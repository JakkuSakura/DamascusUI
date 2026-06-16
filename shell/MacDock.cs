using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DamascusUI;

internal static class MacDock
{
    public static void TrySetDockIconFromBase64(string base64Image)
    {
        if (!OperatingSystem.IsMacOS() || string.IsNullOrWhiteSpace(base64Image))
        {
            return;
        }

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(base64Image);
        }
        catch
        {
            return;
        }

        TrySetDockIconFromBytes(imageBytes);
    }

    [SupportedOSPlatform("macos")]
    private static void TrySetDockIconFromBytes(byte[] imageBytes)
    {
        var dataHandle = GCHandle.Alloc(imageBytes, GCHandleType.Pinned);
        try
        {
            IntPtr nsApplicationClass = objc_getClass("NSApplication");
            IntPtr sharedApplicationSelector = sel_registerName("sharedApplication");
            IntPtr app = IntPtr_objc_msgSend(nsApplicationClass, sharedApplicationSelector);
            if (app == IntPtr.Zero)
            {
                return;
            }

            IntPtr nsDataClass = objc_getClass("NSData");
            IntPtr dataWithBytesSelector = sel_registerName("dataWithBytes:length:");
            IntPtr data = IntPtr_objc_msgSend_IntPtr_nuint(
                nsDataClass,
                dataWithBytesSelector,
                dataHandle.AddrOfPinnedObject(),
                (nuint)imageBytes.Length);
            if (data == IntPtr.Zero)
            {
                return;
            }

            IntPtr nsImageClass = objc_getClass("NSImage");
            IntPtr allocSelector = sel_registerName("alloc");
            IntPtr initWithDataSelector = sel_registerName("initWithData:");
            IntPtr imageAlloc = IntPtr_objc_msgSend(nsImageClass, allocSelector);
            if (imageAlloc == IntPtr.Zero)
            {
                return;
            }

            IntPtr image = IntPtr_objc_msgSend_IntPtr(imageAlloc, initWithDataSelector, data);
            if (image == IntPtr.Zero)
            {
                return;
            }

            IntPtr setApplicationIconImageSelector = sel_registerName("setApplicationIconImage:");
            Void_objc_msgSend_IntPtr(app, setApplicationIconImageSelector, image);
        }
        finally
        {
            dataHandle.Free();
        }
    }

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend_IntPtr_nuint(IntPtr receiver, IntPtr selector, IntPtr arg1, nuint arg2);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void Void_objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);
}
