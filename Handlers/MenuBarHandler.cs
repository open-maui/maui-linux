// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Hosting;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for MenuBar on Linux using Skia rendering.
/// Maps MenuBar to SkiaMenuBar platform view.
/// </summary>
public partial class MenuBarHandler : ElementHandler<IMenuBar, SkiaMenuBar>
{
    public static IPropertyMapper<IMenuBar, MenuBarHandler> Mapper =
        new PropertyMapper<IMenuBar, MenuBarHandler>()
        {
            [nameof(IMenuBar.IsEnabled)] = MapIsEnabled,
        };

    public static CommandMapper<IMenuBar, MenuBarHandler> CommandMapper =
        new()
        {
            ["Add"] = MapAdd,
            ["Remove"] = MapRemove,
            ["Clear"] = MapClear,
            ["Insert"] = MapInsert,
        };

    public MenuBarHandler() : base(Mapper, CommandMapper)
    {
    }

    public MenuBarHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaMenuBar CreatePlatformElement()
    {
        return new SkiaMenuBar();
    }

    protected override void ConnectHandler(SkiaMenuBar platformView)
    {
        base.ConnectHandler(platformView);
        SyncMenuBarItems();
    }

    protected override void DisconnectHandler(SkiaMenuBar platformView)
    {
        platformView.Items.Clear();
        base.DisconnectHandler(platformView);
    }

    private void SyncMenuBarItems()
    {
        if (PlatformView is null || VirtualView is null) return;

        PlatformView.Items.Clear();

        foreach (var menuBarItem in VirtualView)
        {
            if (menuBarItem is MenuBarItem mauiItem)
            {
                var platformItem = CreatePlatformMenuBarItem(mauiItem);
                PlatformView.Items.Add(platformItem);
            }
        }

        PlatformView.Invalidate();
    }

    private static Platform.MenuBarItem CreatePlatformMenuBarItem(MenuBarItem mauiItem)
    {
        var platformItem = new Platform.MenuBarItem
        {
            Text = mauiItem.Text ?? ""
        };

        // MenuBarItem inherits from BaseMenuItem which has a collection
        // Use cast to IEnumerable to iterate
        if (mauiItem is System.Collections.IEnumerable enumerable)
        {
            foreach (var child in enumerable)
            {
                if (child is MenuFlyoutItem flyoutItem)
                {
                    var menuItem = CreatePlatformMenuItem(flyoutItem);
                    platformItem.Items.Add(menuItem);
                }
                else if (child is MenuFlyoutSubItem subItem)
                {
                    var menuItem = CreatePlatformMenuItemWithSubs(subItem);
                    platformItem.Items.Add(menuItem);
                }
                else if (child is MenuFlyoutSeparator)
                {
                    platformItem.Items.Add(new Platform.MenuItem { IsSeparator = true });
                }
            }
        }

        return platformItem;
    }

    private static Platform.MenuItem CreatePlatformMenuItem(MenuFlyoutItem mauiItem)
    {
        var menuItem = new Platform.MenuItem
        {
            Text = mauiItem.Text ?? "",
            IsEnabled = mauiItem.IsEnabled,
            IconSource = mauiItem.IconImageSource?.ToString()
        };

        // Map keyboard accelerator
        if (mauiItem.KeyboardAccelerators.Count > 0)
        {
            var accel = mauiItem.KeyboardAccelerators[0];
            menuItem.Shortcut = FormatKeyboardAccelerator(accel);
        }

        // Connect click event
        menuItem.Clicked += (s, e) =>
        {
            if (mauiItem.Command?.CanExecute(mauiItem.CommandParameter) == true)
            {
                mauiItem.Command.Execute(mauiItem.CommandParameter);
            }
            (mauiItem as IMenuFlyoutItem)?.Clicked();
        };

        return menuItem;
    }

    private static Platform.MenuItem CreatePlatformMenuItemWithSubs(MenuFlyoutSubItem mauiSubItem)
    {
        var menuItem = new Platform.MenuItem
        {
            Text = mauiSubItem.Text ?? "",
            IsEnabled = mauiSubItem.IsEnabled,
            IconSource = mauiSubItem.IconImageSource?.ToString()
        };

        // MenuFlyoutSubItem is enumerable
        if (mauiSubItem is System.Collections.IEnumerable enumerable)
        {
            foreach (var child in enumerable)
            {
                if (child is MenuFlyoutItem flyoutItem)
                {
                    menuItem.SubItems.Add(CreatePlatformMenuItem(flyoutItem));
                }
                else if (child is MenuFlyoutSubItem nestedSubItem)
                {
                    menuItem.SubItems.Add(CreatePlatformMenuItemWithSubs(nestedSubItem));
                }
                else if (child is MenuFlyoutSeparator)
                {
                    menuItem.SubItems.Add(new Platform.MenuItem { IsSeparator = true });
                }
            }
        }

        return menuItem;
    }

    private static string FormatKeyboardAccelerator(KeyboardAccelerator accel)
    {
        var parts = new List<string>();

        if (accel.Modifiers.HasFlag(KeyboardAcceleratorModifiers.Ctrl))
            parts.Add("Ctrl");
        if (accel.Modifiers.HasFlag(KeyboardAcceleratorModifiers.Alt))
            parts.Add("Alt");
        if (accel.Modifiers.HasFlag(KeyboardAcceleratorModifiers.Shift))
            parts.Add("Shift");

        parts.Add(accel.Key ?? "");

        return string.Join("+", parts);
    }

    public static void MapIsEnabled(MenuBarHandler handler, IMenuBar menuBar)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsEnabled = menuBar.IsEnabled;
    }

    public static void MapAdd(MenuBarHandler handler, IMenuBar menuBar, object? args)
    {
        handler.SyncMenuBarItems();
    }

    public static void MapRemove(MenuBarHandler handler, IMenuBar menuBar, object? args)
    {
        handler.SyncMenuBarItems();
    }

    public static void MapClear(MenuBarHandler handler, IMenuBar menuBar, object? args)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Items.Clear();
        handler.PlatformView.Invalidate();
    }

    public static void MapInsert(MenuBarHandler handler, IMenuBar menuBar, object? args)
    {
        handler.SyncMenuBarItems();
    }
}

/// <summary>
/// Handler for MenuFlyout (context menu) on Linux using Skia rendering.
/// Maps IMenuFlyout to SkiaMenuFlyout platform view.
/// </summary>
public partial class MenuFlyoutHandler : ElementHandler<IMenuFlyout, SkiaMenuFlyout>
{
    public static IPropertyMapper<IMenuFlyout, MenuFlyoutHandler> Mapper =
        new PropertyMapper<IMenuFlyout, MenuFlyoutHandler>()
        {
        };

    public static CommandMapper<IMenuFlyout, MenuFlyoutHandler> CommandMapper =
        new()
        {
            ["Add"] = MapAdd,
            ["Remove"] = MapRemove,
            ["Clear"] = MapClear,
        };

    public MenuFlyoutHandler() : base(Mapper, CommandMapper)
    {
    }

    public MenuFlyoutHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaMenuFlyout CreatePlatformElement()
    {
        return new SkiaMenuFlyout();
    }

    protected override void ConnectHandler(SkiaMenuFlyout platformView)
    {
        base.ConnectHandler(platformView);
        SyncMenuItems();
    }

    protected override void DisconnectHandler(SkiaMenuFlyout platformView)
    {
        platformView.Items.Clear();
        base.DisconnectHandler(platformView);
    }

    private void SyncMenuItems()
    {
        if (PlatformView is null || VirtualView is null) return;

        PlatformView.Items.Clear();

        foreach (var item in VirtualView)
        {
            if (item is MenuFlyoutItem flyoutItem)
            {
                PlatformView.Items.Add(CreatePlatformMenuItem(flyoutItem));
            }
            else if (item is MenuFlyoutSubItem subItem)
            {
                PlatformView.Items.Add(CreatePlatformMenuItemWithSubs(subItem));
            }
            else if (item is MenuFlyoutSeparator)
            {
                PlatformView.Items.Add(new Platform.MenuItem { IsSeparator = true });
            }
        }
    }

    private static Platform.MenuItem CreatePlatformMenuItem(MenuFlyoutItem mauiItem)
    {
        var menuItem = new Platform.MenuItem
        {
            Text = mauiItem.Text ?? "",
            IsEnabled = mauiItem.IsEnabled,
            IconSource = mauiItem.IconImageSource?.ToString()
        };

        // Map keyboard accelerator
        if (mauiItem.KeyboardAccelerators.Count > 0)
        {
            var accel = mauiItem.KeyboardAccelerators[0];
            menuItem.Shortcut = FormatKeyboardAccelerator(accel);
        }

        // Connect click event
        menuItem.Clicked += (s, e) =>
        {
            if (mauiItem.Command?.CanExecute(mauiItem.CommandParameter) == true)
            {
                mauiItem.Command.Execute(mauiItem.CommandParameter);
            }
            (mauiItem as IMenuFlyoutItem)?.Clicked();
        };

        return menuItem;
    }

    private static Platform.MenuItem CreatePlatformMenuItemWithSubs(MenuFlyoutSubItem mauiSubItem)
    {
        var menuItem = new Platform.MenuItem
        {
            Text = mauiSubItem.Text ?? "",
            IsEnabled = mauiSubItem.IsEnabled,
            IconSource = mauiSubItem.IconImageSource?.ToString()
        };

        // MenuFlyoutSubItem is enumerable
        if (mauiSubItem is System.Collections.IEnumerable enumerable)
        {
            foreach (var child in enumerable)
            {
                if (child is MenuFlyoutItem flyoutItem)
                {
                    menuItem.SubItems.Add(CreatePlatformMenuItem(flyoutItem));
                }
                else if (child is MenuFlyoutSubItem nestedSubItem)
                {
                    menuItem.SubItems.Add(CreatePlatformMenuItemWithSubs(nestedSubItem));
                }
                else if (child is MenuFlyoutSeparator)
                {
                    menuItem.SubItems.Add(new Platform.MenuItem { IsSeparator = true });
                }
            }
        }

        return menuItem;
    }

    private static string FormatKeyboardAccelerator(KeyboardAccelerator accel)
    {
        var parts = new List<string>();

        if (accel.Modifiers.HasFlag(KeyboardAcceleratorModifiers.Ctrl))
            parts.Add("Ctrl");
        if (accel.Modifiers.HasFlag(KeyboardAcceleratorModifiers.Alt))
            parts.Add("Alt");
        if (accel.Modifiers.HasFlag(KeyboardAcceleratorModifiers.Shift))
            parts.Add("Shift");

        parts.Add(accel.Key ?? "");

        return string.Join("+", parts);
    }

    public static void MapAdd(MenuFlyoutHandler handler, IMenuFlyout menuFlyout, object? args)
    {
        handler.SyncMenuItems();
    }

    public static void MapRemove(MenuFlyoutHandler handler, IMenuFlyout menuFlyout, object? args)
    {
        handler.SyncMenuItems();
    }

    public static void MapClear(MenuFlyoutHandler handler, IMenuFlyout menuFlyout, object? args)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Items.Clear();
    }
}
