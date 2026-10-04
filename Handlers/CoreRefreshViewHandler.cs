// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// MAUI's RefreshViewHandler on Linux: the handler of a core <see cref="IRefreshView"/>
/// (a library's own refresh view, or any view that implements the interface without
/// deriving from Controls' RefreshView). Its platform view is the same
/// <see cref="SkiaRefreshView"/> OpenMaui's <see cref="RefreshViewHandler"/> uses for a
/// Controls RefreshView, and it maps MAUI's IRefreshView properties: IsRefreshing (both
/// ways: a pull that starts a refresh sets <see cref="IRefreshView.IsRefreshing"/>),
/// Content, RefreshColor, IsRefreshEnabled, Background and IsEnabled.
/// </summary>
public partial class CoreRefreshViewHandler : LinuxViewHandler<IRefreshView, SkiaRefreshView>, IRefreshViewHandler
{
    private bool _isUpdatingRefreshing;

    public static IPropertyMapper<IRefreshView, IRefreshViewHandler> Mapper = new PropertyMapper<IRefreshView, IRefreshViewHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IRefreshView.IsRefreshing)] = MapIsRefreshing,
        [nameof(IRefreshView.Content)] = MapContent,
        [nameof(IRefreshView.RefreshColor)] = MapRefreshColor,
        [nameof(IRefreshView.IsRefreshEnabled)] = MapIsRefreshEnabled,
        [nameof(IView.Background)] = MapBackground,
        [nameof(IView.IsEnabled)] = MapIsEnabled,
    };

    public static CommandMapper<IRefreshView, IRefreshViewHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public CoreRefreshViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public CoreRefreshViewHandler(IPropertyMapper? mapper)
        : base(mapper ?? Mapper, CommandMapper)
    {
    }

    public CoreRefreshViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    IRefreshView IRefreshViewHandler.VirtualView => VirtualView;

    object IRefreshViewHandler.PlatformView => PlatformView;

    protected override SkiaRefreshView CreatePlatformView() => new();

    protected override void ConnectHandler(SkiaRefreshView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Refreshing += OnRefreshing;
    }

    protected override void DisconnectHandler(SkiaRefreshView platformView)
    {
        platformView.Refreshing -= OnRefreshing;
        base.DisconnectHandler(platformView);
    }

    // A pull past the threshold started a refresh on the platform view: the virtual view
    // learns it, as MAUI's platform handlers report the platform's refresh state.
    private void OnRefreshing(object? sender, EventArgs e)
    {
        if (VirtualView is null || _isUpdatingRefreshing)
            return;
        try
        {
            _isUpdatingRefreshing = true;
            VirtualView.IsRefreshing = true;
        }
        finally
        {
            _isUpdatingRefreshing = false;
        }
    }

    static SkiaRefreshView? RefreshPlatformView(IRefreshViewHandler handler) => handler.PlatformView as SkiaRefreshView;

    public static void MapIsRefreshing(IRefreshViewHandler handler, IRefreshView refreshView)
    {
        if (RefreshPlatformView(handler) is not { } platform)
            return;
        if (handler is CoreRefreshViewHandler own)
        {
            if (own._isUpdatingRefreshing)
                return;
            own._isUpdatingRefreshing = true;
            try { platform.IsRefreshing = refreshView.IsRefreshing; }
            finally { own._isUpdatingRefreshing = false; }
            return;
        }
        platform.IsRefreshing = refreshView.IsRefreshing;
    }

    public static void MapContent(IRefreshViewHandler handler, IRefreshView refreshView)
    {
        if (RefreshPlatformView(handler) is not { } platform || handler.MauiContext is not { } context)
            return;

        var content = refreshView.Content;
        if (content is null)
        {
            platform.Content = null;
            return;
        }

        try
        {
            if (content.Handler is null)
                content.ToViewHandler(context);
            platform.Content = content.Handler?.PlatformView as SkiaView;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("CoreRefreshViewHandler", $"Failed to render content ({content.GetType().Name}): {ex.Message}", ex);
        }
    }

    public static void MapRefreshColor(IRefreshViewHandler handler, IRefreshView refreshView)
    {
        if (RefreshPlatformView(handler) is { } platform && refreshView.RefreshColor is SolidPaint { Color: { } color })
            platform.RefreshColor = color;
    }

    /// <summary>
    /// A refresh view whose refresh is disabled keeps its content interactive but does not
    /// start a refresh on a pull (MAUI's IsRefreshEnabled); a disabled view does neither.
    /// </summary>
    public static void MapIsRefreshEnabled(IRefreshViewHandler handler, IRefreshView refreshView)
    {
        if (RefreshPlatformView(handler) is { } platform)
            platform.IsPullEnabled = refreshView.IsRefreshEnabled;
    }

    public static void MapBackground(IRefreshViewHandler handler, IRefreshView refreshView)
    {
        if (RefreshPlatformView(handler) is { } platform && refreshView.Background is SolidPaint { Color: { } color })
            platform.RefreshBackgroundColor = color;
    }

    public static void MapIsEnabled(IRefreshViewHandler handler, IRefreshView refreshView)
    {
        if (RefreshPlatformView(handler) is { } platform)
            platform.IsEnabled = refreshView.IsEnabled;
    }
}
