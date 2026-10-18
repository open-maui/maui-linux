// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// The Shell's tab bar, as MAUI's Windows implementation shows it: the current ShellItem is a
/// navigation view whose pane is a row of tabs across the top of the content area (see
/// SkiaShell.TopNavigation.cs). This part holds the Shell's appearance for the presented page
/// (MAUI's IAppearanceObserver, the colours ShellItemHandler.UpdateAppearance applies) and the
/// public members the earlier bottom bar exposed, now mapped to that pane.
/// </summary>
public partial class SkiaShell
{
    private TabIconCache? _tabIconCache;
    private ShellAppearance? _appearance;
    private AppearanceObserver? _appearanceObserver;

    private TabIconCache TabIcons => _tabIconCache ??= new TabIconCache(this);

    /// <summary>Receives the Shell's appearance for the presented page (MAUI's IAppearanceObserver).</summary>
    private sealed class AppearanceObserver : IAppearanceObserver
    {
        private readonly SkiaShell _owner;

        public AppearanceObserver(SkiaShell owner) => _owner = owner;

        public void OnAppearanceChanged(ShellAppearance appearance)
        {
            _owner._appearance = appearance;
            // The pane's visibility follows the page (Shell.TabBarIsVisible) as well as its colours.
            _owner.InvalidateMeasure();
            _owner.Invalidate();
        }
    }

    private void ObserveAppearance(Shell shell)
    {
        if (shell is not IShellController controller)
            return;
        _appearanceObserver = new AppearanceObserver(this);
        try
        {
            controller.AddAppearanceObserver(_appearanceObserver, shell);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaShell", "Observing the Shell's appearance failed", ex);
        }
    }

    private void StopObservingAppearance(Shell shell)
    {
        if (_appearanceObserver != null && shell is IShellController controller)
            controller.RemoveAppearanceObserver(_appearanceObserver);
        _appearanceObserver = null;
        _appearance = null;
    }

    /// <summary>
    /// The sections the tab bar (the top navigation pane) lists: the current ShellItem's visible
    /// sections while the pane is shown, empty otherwise. A section MAUI made around a bare
    /// ShellContent is listed by its content, as Windows' ShellItemHandler.MapMenuItems does.
    /// </summary>
    public IReadOnlyList<Microsoft.Maui.Controls.ShellSection> TabBarSections =>
        IsTopPaneShown && _mauiShell?.CurrentItem is IShellItemController item
            ? item.GetItems()
            : Array.Empty<Microsoft.Maui.Controls.ShellSection>();

    /// <summary>
    /// Where the tab bar is, in the coordinates of <see cref="SkiaView.Bounds"/>: the top navigation
    /// pane across the top of the content area, under the navigation bar and beside a locked
    /// flyout, <see cref="TabBarHeight"/> tall; empty when none is shown.
    /// </summary>
    public Rect TabBarBounds
    {
        get
        {
            if (!IsTopPaneShown)
                return Rect.Zero;
            float flyoutOffset = FlyoutBehavior == ShellFlyoutBehavior.Locked ? FlyoutWidth : 0f;
            return new Rect(
                Bounds.Left + flyoutOffset,
                Bounds.Top + (IsNavBarShown ? NavBarHeight : 0f),
                Math.Max(0, Bounds.Width - flyoutOffset),
                TabBarHeight);
        }
    }

    /// <summary>
    /// Where the last layout put the icon and the title of the tab standing for
    /// <paramref name="section"/> in the top navigation pane (the section's own tab, or its
    /// content's for a section MAUI made implicitly); empty while the tab is in the overflow
    /// drop-down, and the icon's empty until it has loaded (tests, conformance helpers).
    /// </summary>
    internal (SKRect Icon, SKRect Title) TabBarItemBounds(Microsoft.Maui.Controls.ShellSection section) =>
        TopNavigation.ItemPartBounds(section);
}
