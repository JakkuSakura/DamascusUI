using AppKit;
using Foundation;

namespace DamascusUI;

internal static class MacMenuBar
{
    public static bool IsSupported =>
        OperatingSystem.IsMacOS();

    public static void Apply(string appName, List<NativeMenuItemDef> items, Action<string> onClick)
    {
        if (!IsSupported)
        {
            return;
        }

        NSRunLoop.Main.BeginInvokeOnMainThread(() =>
        {
            var mainMenu = new NSMenu();
            mainMenu.AddItem(CreateApplicationRoot(appName, onClick));

            var fileMenu = BuildMenu("File", items, onClick, includeAbout: false);
            if (fileMenu is not null && fileMenu.Submenu is not null && fileMenu.Submenu.Count > 0)
            {
                mainMenu.AddItem(fileMenu);
            }

            NSApplication.SharedApplication.MainMenu = mainMenu;
        });
    }

    private static NSMenuItem CreateApplicationRoot(string appName, Action<string> onClick)
    {
        var appRoot = new NSMenuItem(appName)
        {
            Submenu = new NSMenu(appName),
        };
        appRoot.Submenu!.AddItem(new NSMenuItem("About Todos", (_, _) => onClick("about")));
        return appRoot;
    }

    private static NSMenuItem? BuildMenu(
        string label,
        List<NativeMenuItemDef> items,
        Action<string> onClick,
        bool includeAbout)
    {
        var submenu = new NSMenu(label);
        foreach (var item in items)
        {
            if (!includeAbout && item.Id == "about")
            {
                continue;
            }

            var nativeItem = BuildItem(item, onClick);
            if (nativeItem is not null)
            {
                submenu.AddItem(nativeItem);
            }
        }

        if (submenu.Count == 0)
        {
            return null;
        }

        return new NSMenuItem(label)
        {
            Submenu = submenu,
        };
    }

    private static NSMenuItem? BuildItem(NativeMenuItemDef item, Action<string> onClick)
    {
        return item.Kind switch
        {
            "separator" => NSMenuItem.SeparatorItem,
            "submenu" => BuildSubmenu(item, onClick),
            _ => BuildAction(item, onClick),
        };
    }

    private static NSMenuItem BuildAction(NativeMenuItemDef item, Action<string> onClick)
    {
        var id = item.Id ?? string.Empty;
        return new NSMenuItem(item.Label ?? string.Empty, (_, _) => onClick(id));
    }

    private static NSMenuItem? BuildSubmenu(NativeMenuItemDef item, Action<string> onClick)
    {
        if (item.Items is null || item.Items.Count == 0)
        {
            return null;
        }

        var submenu = new NSMenu(item.Label ?? string.Empty);
        foreach (var child in item.Items)
        {
            var nativeItem = BuildItem(child, onClick);
            if (nativeItem is not null)
            {
                submenu.AddItem(nativeItem);
            }
        }

        if (submenu.Count == 0)
        {
            return null;
        }

        return new NSMenuItem(item.Label ?? string.Empty)
        {
            Submenu = submenu,
        };
    }
}
