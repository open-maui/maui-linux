// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Hosting;
using SkiaSharp;
using System.Runtime.CompilerServices;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for MenuBar on Linux using Skia rendering.
/// Maps MenuBar to SkiaMenuBar platform view.
/// </summary>
public partial class MenuBarHandler : ElementHandler<IMenuBar, SkiaMenuBar>, IMenuBarHandler
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
            if (menuBarItem is Microsoft.Maui.Controls.MenuBarItem mauiItem)
            {
                var platformItem = CreatePlatformMenuBarItem(mauiItem);
                PlatformView.Items.Add(platformItem);
            }
        }

        PlatformView.Invalidate();
    }

    private static SkiaMenuBarItem CreatePlatformMenuBarItem(Microsoft.Maui.Controls.MenuBarItem mauiItem)
    {
        var platformItem = new SkiaMenuBarItem
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
                    platformItem.Items.Add(new SkiaMenuItem { IsSeparator = true });
                }
            }
        }

        return platformItem;
    }

    private static SkiaMenuItem CreatePlatformMenuItem(MenuFlyoutItem mauiItem)
    {
        var menuItem = new SkiaMenuItem
        {
            Text = mauiItem.Text ?? "",
            IsEnabled = mauiItem.IsEnabled,
            IconSource = Microsoft.Maui.Platform.Linux.Services.ImageFileResolver.FileOf(mauiItem.IconImageSource)
        };

        // Map keyboard accelerator
        if (mauiItem.KeyboardAccelerators.Count > 0)
        {
            var accel = mauiItem.KeyboardAccelerators[0];
            menuItem.Shortcut = FormatKeyboardAccelerator(accel);
        }

        // Connect click event
        // IMenuElement.Clicked runs the Command and raises Clicked once;
        // executing the command here as well fired it twice.
        menuItem.Clicked += (s, e) => ((IMenuElement)mauiItem).Clicked();

        return menuItem;
    }

    private static SkiaMenuItem CreatePlatformMenuItemWithSubs(MenuFlyoutSubItem mauiSubItem)
    {
        var menuItem = new SkiaMenuItem
        {
            Text = mauiSubItem.Text ?? "",
            IsEnabled = mauiSubItem.IsEnabled,
            IconSource = Microsoft.Maui.Platform.Linux.Services.ImageFileResolver.FileOf(mauiSubItem.IconImageSource)
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
                    menuItem.SubItems.Add(new SkiaMenuItem { IsSeparator = true });
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

    /// <summary>MAUI's <c>MapAdd</c> signature; runs the command on a Linux <see cref="MenuBarHandler"/>.</summary>
    public static void MapAdd(IMenuBarHandler handler, IMenuBar layout, object? arg)
    {
        if (handler is MenuBarHandler linuxHandler)
            MapAddCore(linuxHandler, layout, arg);
        else if (arg is MenuBarHandlerUpdate update)
            handler.Add(update.MenuBarItem);
    }

    private static void MapAddCore(MenuBarHandler handler, IMenuBar menuBar, object? args)
    {
        handler.SyncMenuBarItems();
    }

    /// <summary>MAUI's <c>MapRemove</c> signature; runs the command on a Linux <see cref="MenuBarHandler"/>.</summary>
    public static void MapRemove(IMenuBarHandler handler, IMenuBar layout, object? arg)
    {
        if (handler is MenuBarHandler linuxHandler)
            MapRemoveCore(linuxHandler, layout, arg);
        else if (arg is MenuBarHandlerUpdate update)
            handler.Remove(update.MenuBarItem);
    }

    private static void MapRemoveCore(MenuBarHandler handler, IMenuBar menuBar, object? args)
    {
        handler.SyncMenuBarItems();
    }

    /// <summary>MAUI's <c>MapClear</c> signature; runs the command on a Linux <see cref="MenuBarHandler"/>.</summary>
    public static void MapClear(IMenuBarHandler handler, IMenuBar layout, object? arg)
    {
        if (handler is MenuBarHandler linuxHandler)
            MapClearCore(linuxHandler, layout, arg);
        else
            handler.Clear();
    }

    private static void MapClearCore(MenuBarHandler handler, IMenuBar menuBar, object? args)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Items.Clear();
        handler.PlatformView.Invalidate();
    }

    /// <summary>MAUI's <c>MapInsert</c> signature; runs the command on a Linux <see cref="MenuBarHandler"/>.</summary>
    public static void MapInsert(IMenuBarHandler handler, IMenuBar layout, object? arg)
    {
        if (handler is MenuBarHandler linuxHandler)
            MapInsertCore(linuxHandler, layout, arg);
        else if (arg is MenuBarHandlerUpdate update)
            handler.Insert(update.Index, update.MenuBarItem);
    }

    private static void MapInsertCore(MenuBarHandler handler, IMenuBar menuBar, object? args)
    {
        handler.SyncMenuBarItems();
    }

    // IMenuBarHandler: each change rebuilds the platform menu bar from the MenuBar's items.

    /// <summary>An item was added to the menu bar.</summary>
    public void Add(IMenuBarItem view) => SyncMenuBarItems();

    /// <summary>An item was removed from the menu bar.</summary>
    public void Remove(IMenuBarItem view) => SyncMenuBarItems();

    /// <summary>The menu bar was cleared.</summary>
    public void Clear()
    {
        if (PlatformView is null) return;
        PlatformView.Items.Clear();
        PlatformView.Invalidate();
    }

    /// <summary>An item was inserted into the menu bar.</summary>
    public void Insert(int index, IMenuBarItem view) => SyncMenuBarItems();

    IMenuBar IMenuBarHandler.VirtualView => VirtualView;

    object IMenuBarHandler.PlatformView => PlatformView;
}

/// <summary>
/// Handler for MenuFlyout (context menu) on Linux using Skia rendering.
/// Maps IMenuFlyout to SkiaMenuFlyout platform view.
/// </summary>
public partial class MenuFlyoutHandler : ElementHandler<IMenuFlyout, SkiaMenuFlyout>, IMenuFlyoutHandler
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
            ["Insert"] = MapInsert,
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
                PlatformView.Items.Add(new SkiaMenuItem { IsSeparator = true });
            }
        }
    }

    private static SkiaMenuItem CreatePlatformMenuItem(MenuFlyoutItem mauiItem)
    {
        var menuItem = new SkiaMenuItem
        {
            Text = mauiItem.Text ?? "",
            IsEnabled = mauiItem.IsEnabled,
            IconSource = Microsoft.Maui.Platform.Linux.Services.ImageFileResolver.FileOf(mauiItem.IconImageSource)
        };

        // Map keyboard accelerator
        if (mauiItem.KeyboardAccelerators.Count > 0)
        {
            var accel = mauiItem.KeyboardAccelerators[0];
            menuItem.Shortcut = FormatKeyboardAccelerator(accel);
        }

        // Connect click event
        // IMenuElement.Clicked runs the Command and raises Clicked once;
        // executing the command here as well fired it twice.
        menuItem.Clicked += (s, e) => ((IMenuElement)mauiItem).Clicked();

        return menuItem;
    }

    private static SkiaMenuItem CreatePlatformMenuItemWithSubs(MenuFlyoutSubItem mauiSubItem)
    {
        var menuItem = new SkiaMenuItem
        {
            Text = mauiSubItem.Text ?? "",
            IsEnabled = mauiSubItem.IsEnabled,
            IconSource = Microsoft.Maui.Platform.Linux.Services.ImageFileResolver.FileOf(mauiSubItem.IconImageSource)
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
                    menuItem.SubItems.Add(new SkiaMenuItem { IsSeparator = true });
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

    /// <summary>MAUI's <c>MapAdd</c> signature; runs the command on a Linux <see cref="MenuFlyoutHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapAdd(IMenuFlyoutHandler handler, IMenuFlyout menuElement, object? arg)
    {
        if (handler is MenuFlyoutHandler linuxHandler)
            linuxHandler.SyncMenuItems();
        else if (arg is ContextFlyoutItemHandlerUpdate update)
            handler.Add(update.MenuElement);
    }

    /// <summary>MAUI's <c>MapRemove</c> signature; runs the command on a Linux <see cref="MenuFlyoutHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapRemove(IMenuFlyoutHandler handler, IMenuFlyout menuElement, object? arg)
    {
        if (handler is MenuFlyoutHandler linuxHandler)
            linuxHandler.SyncMenuItems();
        else if (arg is ContextFlyoutItemHandlerUpdate update)
            handler.Remove(update.MenuElement);
    }

    /// <summary>MAUI's <c>MapClear</c> signature; runs the command on a Linux <see cref="MenuFlyoutHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapClear(IMenuFlyoutHandler handler, IMenuFlyout menuElement, object? arg) => handler.Clear();

    /// <summary>MAUI's <c>MapInsert</c>: an element was inserted; the menu is rebuilt from the flyout.</summary>
    public static void MapInsert(IMenuFlyoutHandler handler, IMenuFlyout menuElement, object? arg)
    {
        if (handler is MenuFlyoutHandler linuxHandler)
            linuxHandler.SyncMenuItems();
        else if (arg is ContextFlyoutItemHandlerUpdate update)
            handler.Insert(update.Index, update.MenuElement);
    }

    // IMenuFlyoutHandler: each change rebuilds the platform menu from the flyout's elements.

    /// <summary>An element was added to the flyout.</summary>
    public void Add(IMenuElement view) => SyncMenuItems();

    /// <summary>An element was removed from the flyout.</summary>
    public void Remove(IMenuElement view) => SyncMenuItems();

    /// <summary>The flyout was cleared.</summary>
    public void Clear() => PlatformView?.Items.Clear();

    /// <summary>An element was inserted into the flyout.</summary>
    public void Insert(int index, IMenuElement view) => SyncMenuItems();

    IMenuFlyout IMenuFlyoutHandler.VirtualView => VirtualView;

    object IMenuFlyoutHandler.PlatformView => PlatformView;
}
