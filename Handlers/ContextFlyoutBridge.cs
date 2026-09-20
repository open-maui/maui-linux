// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Shows a view's <c>FlyoutBase.ContextFlyout</c> (a <see cref="MenuFlyout"/>)
/// as a <see cref="SkiaContextMenu"/> on secondary click. The window's input
/// path calls <see cref="TryShow"/> before dispatching the press, so an explicit
/// flyout replaces a control's built-in context menu the way it does on the
/// other platforms.
/// </summary>
public static class ContextFlyoutBridge
{
    /// <summary>
    /// Walks from the hit view up the platform tree and returns the first
    /// MAUI view that declares a <see cref="MenuFlyout"/> context flyout.
    /// </summary>
    public static MenuFlyout? Find(SkiaView? hit)
    {
        for (var view = hit; view != null; view = view.Parent)
        {
            if (view.MauiView is BindableObject bindable && FlyoutBase.GetContextFlyout(bindable) is MenuFlyout flyout)
                return flyout;
        }
        return null;
    }

    /// <summary>
    /// Opens the flyout at window coordinates when one applies to the hit
    /// view; returns false (and shows nothing) otherwise.
    /// </summary>
    public static bool TryShow(SkiaView? hit, float x, float y)
    {
        var flyout = Find(hit);
        if (flyout == null)
            return false;

        var items = BuildItems(flyout);
        if (items.Count == 0)
            return false;

        LinuxDialogService.ShowContextMenu(new SkiaContextMenu(x, y, items));
        return true;
    }

    /// <summary>
    /// Flattens the flyout into context-menu entries. Sub-items become a
    /// disabled heading followed by their indented children and a separator,
    /// since the Skia context menu has no cascading submenus.
    /// </summary>
    public static List<ContextMenuItem> BuildItems(IMenuFlyout flyout)
    {
        var items = new List<ContextMenuItem>();
        foreach (var element in flyout)
            Append(items, element, indent: 0);

        // No leading/trailing/double separators after flattening.
        for (int i = items.Count - 1; i >= 0; i--)
        {
            bool edge = i == 0 || i == items.Count - 1;
            bool doubled = i > 0 && items[i].IsSeparator && items[i - 1].IsSeparator;
            if (items[i].IsSeparator && (edge || doubled))
                items.RemoveAt(i);
        }
        return items;
    }

    private static void Append(List<ContextMenuItem> items, IMenuElement element, int indent)
    {
        string pad = new(' ', indent * 2);
        switch (element)
        {
            case IMenuFlyoutSeparator:
                items.Add(ContextMenuItem.Separator);
                break;

            case IMenuFlyoutSubItem sub:
                if (items.Count > 0 && !items[^1].IsSeparator)
                    items.Add(ContextMenuItem.Separator);
                items.Add(new ContextMenuItem(pad + (sub.Text ?? string.Empty), null, isEnabled: false));
                foreach (var child in sub)
                    Append(items, child, indent + 1);
                items.Add(ContextMenuItem.Separator);
                break;

            case IMenuFlyoutItem item:
                // IMenuElement.Clicked runs the Command and raises Clicked
                // exactly once, the same entry point the other platforms use.
                items.Add(new ContextMenuItem(pad + (item.Text ?? string.Empty), item.Clicked, item.IsEnabled));
                break;
        }
    }
}
