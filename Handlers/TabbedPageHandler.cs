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
public partial class TabbedPageHandler : LinuxViewHandler<ITabbedView, SkiaTabbedPage>
{
    private bool _isUpdatingSelection;

    public static IPropertyMapper<ITabbedView, TabbedPageHandler> Mapper = new PropertyMapper<ITabbedView, TabbedPageHandler>(ViewHandler.ViewMapper)
    {
        [nameof(TabbedPage.BarBackgroundColor)] = MapBarBackgroundColor,
        [nameof(TabbedPage.BarBackground)] = MapBarBackground,
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
        // The page's MAUI frame follows the platform view's arrange.
        platformView.HostedPage = VirtualView as Microsoft.Maui.Controls.Page;
        platformView.SelectedIndexChanged += OnSelectedIndexChanged;

        if (VirtualView is TabbedPage tabbedPage)
        {
            tabbedPage.PagesChanged += OnPagesChanged;
            tabbedPage.Appearing += OnTabbedPageAppearing;
            tabbedPage.Disappearing += OnTabbedPageDisappearing;
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
            tabbedPage.Appearing -= OnTabbedPageAppearing;
            tabbedPage.Disappearing -= OnTabbedPageDisappearing;
        }
        TrackBarBackground(null);
        UntrackPages();
        platformView.ClearTabs();
        base.DisconnectHandler(platformView);
    }

    // The gradient BarBackground is followed (a stop changing repaints the bar) while the page
    // is shown, as MAUI's TabbedPageManager does: a brush shared from the app's resources
    // outlives the page, and a subscription kept after the page left the screen (popped as a
    // modal, before its handler is disconnected) pinned the handler and page to it.
    private Microsoft.Maui.Controls.GradientBrush? _trackedGradient;

    private void TrackBarBackground(Brush? brush)
    {
        var gradient = brush as Microsoft.Maui.Controls.GradientBrush;
        if (ReferenceEquals(gradient, _trackedGradient))
            return;
        if (_trackedGradient != null)
            _trackedGradient.InvalidateGradientBrushRequested -= OnBarBackgroundInvalidated;
        _trackedGradient = gradient;
        if (gradient != null)
            gradient.InvalidateGradientBrushRequested += OnBarBackgroundInvalidated;
    }

    private void OnBarBackgroundInvalidated(object? sender, EventArgs e) => PlatformView?.Invalidate();

    private void OnTabbedPageAppearing(object? sender, EventArgs e) =>
        TrackBarBackground((VirtualView as TabbedPage)?.BarBackground);

    private void OnTabbedPageDisappearing(object? sender, EventArgs e) => TrackBarBackground(null);

    // Each tab follows its page's Title and IconImageSource, as the tabs of the other platforms do.
    private readonly List<Page> _trackedPages = new();

    private void TrackPage(Page page)
    {
        page.PropertyChanged += OnChildPagePropertyChanged;
        _trackedPages.Add(page);
    }

    private void UntrackPages()
    {
        foreach (var page in _trackedPages)
            page.PropertyChanged -= OnChildPagePropertyChanged;
        _trackedPages.Clear();
    }

    private void OnChildPagePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not Page page || PlatformView is null || VirtualView is not TabbedPage tabbedPage)
            return;
        if (e.PropertyName is not (nameof(Page.Title) or nameof(Page.IconImageSource)))
            return;
        int index = tabbedPage.Children.IndexOf(page);
        if (index < 0 || index >= PlatformView.Tabs.Count)
            return;
        var tab = PlatformView.Tabs[index];
        tab.Title = page.Title ?? "Tab";
        tab.IconPath = Microsoft.Maui.Platform.Linux.Services.ImageFileResolver.FileOf(page.IconImageSource);
        tab.IconSource = page.IconImageSource;
        PlatformView.Invalidate();
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
        UntrackPages();

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
                        PlatformView.AddTab(page.Title ?? "Tab", skiaContent, Microsoft.Maui.Platform.Linux.Services.ImageFileResolver.FileOf(page.IconImageSource));
                        PlatformView.Tabs[PlatformView.Tabs.Count - 1].IconSource = page.IconImageSource;
                        TrackPage(page);
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

    public static void MapBarBackground(TabbedPageHandler handler, ITabbedView tabbedView)
    {
        if (handler.PlatformView is null || tabbedView is not TabbedPage tabbedPage) return;
        handler.PlatformView.TabBarBackground = tabbedPage.BarBackground;
        // A new brush replaces the one followed; an unseen page follows none until it appears.
        if (handler._trackedGradient != null || tabbedPage.IsLoaded)
            handler.TrackBarBackground(tabbedPage.BarBackground);
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
