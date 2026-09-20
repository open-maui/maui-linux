// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Hosting;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for TabbedPage on Linux using Skia rendering.
/// Maps ITabbedView interface to SkiaTabbedPage platform view.
/// </summary>
public partial class TabbedPageHandler : ViewHandler<ITabbedView, SkiaTabbedPage>
{
    private bool _isUpdatingSelection;

    public static IPropertyMapper<ITabbedView, TabbedPageHandler> Mapper = new PropertyMapper<ITabbedView, TabbedPageHandler>(ViewHandler.ViewMapper)
    {
        [nameof(TabbedPage.BarBackgroundColor)] = MapBarBackgroundColor,
        [nameof(TabbedPage.BarTextColor)] = MapBarTextColor,
        [nameof(TabbedPage.SelectedTabColor)] = MapSelectedTabColor,
        [nameof(TabbedPage.UnselectedTabColor)] = MapUnselectedTabColor,
        [nameof(TabbedPage.CurrentPage)] = MapCurrentPage,
        // Tab bar placement is expressed through MAUI's platform-specific
        // attached property (TabbedPage.ToolbarPlacement, AndroidSpecific);
        // Linux honours it the same way: Bottom puts the tab strip below the content.
        [ToolbarPlacementPropertyName] = MapToolbarPlacement,
    };

    private const string ToolbarPlacementPropertyName = "ToolbarPlacement";

    public static CommandMapper<ITabbedView, TabbedPageHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public TabbedPageHandler() : base(Mapper, CommandMapper)
    {
    }

    public TabbedPageHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaTabbedPage CreatePlatformView()
    {
        return new SkiaTabbedPage();
    }

    protected override void ConnectHandler(SkiaTabbedPage platformView)
    {
        base.ConnectHandler(platformView);
        platformView.SelectedIndexChanged += OnSelectedIndexChanged;

        if (VirtualView is TabbedPage tabbedPage)
        {
            tabbedPage.PagesChanged += OnPagesChanged;
        }

        // Sync initial tabs
        SyncTabs();
    }

    protected override void DisconnectHandler(SkiaTabbedPage platformView)
    {
        platformView.SelectedIndexChanged -= OnSelectedIndexChanged;
        if (VirtualView is TabbedPage tabbedPage)
        {
            tabbedPage.PagesChanged -= OnPagesChanged;
        }
        platformView.ClearTabs();
        base.DisconnectHandler(platformView);
    }

    private void OnPagesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        SyncTabs();
    }

    private void OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (VirtualView is null || PlatformView is null || _isUpdatingSelection) return;

        try
        {
            _isUpdatingSelection = true;

            // Sync selected page back to virtual view
            if (VirtualView is TabbedPage tabbedPage && PlatformView.SelectedIndex >= 0)
            {
                var selectedIndex = PlatformView.SelectedIndex;
                if (selectedIndex < tabbedPage.Children.Count)
                {
                    tabbedPage.CurrentPage = tabbedPage.Children[selectedIndex] as Page;
                }
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    private void SyncTabs()
    {
        if (PlatformView is null || VirtualView is null || MauiContext is null) return;

        PlatformView.ClearTabs();

        if (VirtualView is TabbedPage tabbedPage)
        {
            foreach (var child in tabbedPage.Children)
            {
                if (child is Page page)
                {
                    // Create handler for page content
                    if (page.Handler == null)
                    {
                        page.Handler = page.ToViewHandler(MauiContext);
                    }

                    if (page.Handler?.PlatformView is SkiaView skiaContent)
                    {
                        PlatformView.AddTab(page.Title ?? "Tab", skiaContent, page.IconImageSource?.ToString());
                    }
                }
            }

            // Sync selected tab (MAUI's CurrentPage is authoritative here)
            if (tabbedPage.CurrentPage != null)
            {
                var index = tabbedPage.Children.IndexOf(tabbedPage.CurrentPage);
                if (index >= 0)
                {
                    try
                    {
                        _isUpdatingSelection = true;
                        PlatformView.SelectedIndex = index;
                    }
                    finally
                    {
                        _isUpdatingSelection = false;
                    }
                }
            }
        }
    }

    public static void MapBarBackgroundColor(TabbedPageHandler handler, ITabbedView tabbedView)
    {
        if (handler.PlatformView is null) return;

        if (tabbedView is TabbedPage tabbedPage && tabbedPage.BarBackgroundColor is Color color)
        {
            handler.PlatformView.TabBarBackgroundColor = color;
        }
    }

    public static void MapBarTextColor(TabbedPageHandler handler, ITabbedView tabbedView)
    {
        if (handler.PlatformView is null) return;

        if (tabbedView is TabbedPage tabbedPage && tabbedPage.BarTextColor is Color color)
        {
            // BarTextColor is the tab text color; the Selected/Unselected
            // colors refine it when set.
            if (tabbedPage.UnselectedTabColor is null)
                handler.PlatformView.UnselectedTabColor = color;
            if (tabbedPage.SelectedTabColor is null)
            {
                handler.PlatformView.SelectedTabColor = color;
                handler.PlatformView.IndicatorColor = color;
            }
        }
    }

    public static void MapCurrentPage(TabbedPageHandler handler, ITabbedView tabbedView)
    {
        if (handler.PlatformView is null || handler._isUpdatingSelection) return;

        if (tabbedView is TabbedPage tabbedPage && tabbedPage.CurrentPage != null)
        {
            var index = tabbedPage.Children.IndexOf(tabbedPage.CurrentPage);
            if (index >= 0)
            {
                try
                {
                    handler._isUpdatingSelection = true;
                    handler.PlatformView.SelectedIndex = index;
                }
                finally
                {
                    handler._isUpdatingSelection = false;
                }
            }
        }
    }

    public static void MapToolbarPlacement(TabbedPageHandler handler, ITabbedView tabbedView)
    {
        if (handler.PlatformView is null) return;

        if (tabbedView is TabbedPage tabbedPage)
        {
            var placement = Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.TabbedPage.GetToolbarPlacement(tabbedPage);
            handler.PlatformView.TabBarOnBottom =
                placement == Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.ToolbarPlacement.Bottom;
        }
    }

    public static void MapSelectedTabColor(TabbedPageHandler handler, ITabbedView tabbedView)
    {
        if (handler.PlatformView is null) return;

        if (tabbedView is TabbedPage tabbedPage && tabbedPage.SelectedTabColor is Color color)
        {
            handler.PlatformView.SelectedTabColor = color;
            handler.PlatformView.IndicatorColor = color;
        }
    }

    public static void MapUnselectedTabColor(TabbedPageHandler handler, ITabbedView tabbedView)
    {
        if (handler.PlatformView is null) return;

        if (tabbedView is TabbedPage tabbedPage && tabbedPage.UnselectedTabColor is Color color)
        {
            handler.PlatformView.UnselectedTabColor = color;
        }
    }
}
