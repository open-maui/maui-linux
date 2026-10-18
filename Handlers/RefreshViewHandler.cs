// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Hosting;
using SkiaSharp;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for RefreshView on Linux using Skia rendering.
/// Maps RefreshView to SkiaRefreshView platform view. A core <see cref="IRefreshView"/>
/// that is not a Controls RefreshView gets <see cref="CoreRefreshViewHandler"/>.
/// </summary>
public partial class RefreshViewHandler : LinuxViewHandler<RefreshView, SkiaRefreshView>, IRefreshViewHandler
{
    private bool _isUpdatingRefreshing;

    public static IPropertyMapper<RefreshView, RefreshViewHandler> Mapper =
        new PropertyMapper<RefreshView, RefreshViewHandler>(ViewHandler.ViewMapper)
        {
            [nameof(RefreshView.Content)] = MapContent,
            [nameof(RefreshView.IsRefreshing)] = MapIsRefreshing,
            [nameof(RefreshView.RefreshColor)] = MapRefreshColor,
            [nameof(RefreshView.Command)] = MapCommand,
            [nameof(RefreshView.CommandParameter)] = MapCommandParameter,
            [nameof(IView.Background)] = MapBackground,
            [nameof(IRefreshView.IsRefreshEnabled)] = MapIsRefreshEnabled,
        };

    public static CommandMapper<RefreshView, RefreshViewHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
        };

    public RefreshViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public RefreshViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    IRefreshView IRefreshViewHandler.VirtualView => VirtualView;

    object IRefreshViewHandler.PlatformView => PlatformView;

    protected override SkiaRefreshView CreatePlatformView()
    {
        return new SkiaRefreshView();
    }

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

    private void OnRefreshing(object? sender, EventArgs e)
    {
        if (VirtualView is null || _isUpdatingRefreshing) return;

        try
        {
            _isUpdatingRefreshing = true;

            // Notify the virtual view that refreshing has started
            VirtualView.IsRefreshing = true;

            // The command will be executed by the platform view
        }
        finally
        {
            _isUpdatingRefreshing = false;
        }
    }

    /// <summary>MAUI's <c>MapContent</c> signature; maps the property on a Linux <see cref="RefreshViewHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapContent(IRefreshViewHandler handler, IRefreshView refreshView)
    {
        if (handler is RefreshViewHandler linuxHandler && refreshView is RefreshView linuxView)
            MapContentCore(linuxHandler, linuxView);
    }

    [Obsolete("Use the IRefreshViewHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapContent(RefreshViewHandler handler, RefreshView refreshView) => MapContentCore(handler, refreshView);

    private static void MapContentCore(RefreshViewHandler handler, RefreshView refreshView)
    {
        if (handler.PlatformView is null || handler.MauiContext is null) return;

        var content = refreshView.Content;
        if (content == null)
        {
            handler.PlatformView.Content = null;
            return;
        }

        // Create handler for content
        if (content.Handler == null)
        {
            content.Handler = content.ToViewHandler(handler.MauiContext);
        }

        if (content.Handler?.PlatformView is SkiaView skiaContent)
        {
            handler.PlatformView.Content = skiaContent;
        }
    }

    /// <summary>MAUI's <c>MapIsRefreshing</c> signature; maps the property on a Linux <see cref="RefreshViewHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapIsRefreshing(IRefreshViewHandler handler, IRefreshView refreshView)
    {
        if (handler is RefreshViewHandler linuxHandler && refreshView is RefreshView linuxView)
            MapIsRefreshingCore(linuxHandler, linuxView);
    }

    [Obsolete("Use the IRefreshViewHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapIsRefreshing(RefreshViewHandler handler, RefreshView refreshView) => MapIsRefreshingCore(handler, refreshView);

    private static void MapIsRefreshingCore(RefreshViewHandler handler, RefreshView refreshView)
    {
        if (handler.PlatformView is null || handler._isUpdatingRefreshing) return;

        try
        {
            handler._isUpdatingRefreshing = true;
            handler.PlatformView.IsRefreshing = refreshView.IsRefreshing;
        }
        finally
        {
            handler._isUpdatingRefreshing = false;
        }
    }

    /// <summary>MAUI's <c>MapRefreshColor</c> signature; maps the property on a Linux <see cref="RefreshViewHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapRefreshColor(IRefreshViewHandler handler, IRefreshView refreshView)
    {
        if (handler is RefreshViewHandler linuxHandler && refreshView is RefreshView linuxView)
            MapRefreshColorCore(linuxHandler, linuxView);
    }

    [Obsolete("Use the IRefreshViewHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapRefreshColor(RefreshViewHandler handler, RefreshView refreshView) => MapRefreshColorCore(handler, refreshView);

    private static void MapRefreshColorCore(RefreshViewHandler handler, RefreshView refreshView)
    {
        if (handler.PlatformView is null) return;

        if (refreshView.RefreshColor is not null)
        {
            handler.PlatformView.RefreshColor = refreshView.RefreshColor;
        }
    }

    public static void MapCommand(RefreshViewHandler handler, RefreshView refreshView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Command = refreshView.Command;
    }

    public static void MapCommandParameter(RefreshViewHandler handler, RefreshView refreshView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.CommandParameter = refreshView.CommandParameter;
    }

    public static void MapBackground(RefreshViewHandler handler, RefreshView refreshView)
    {
        if (handler.PlatformView is null) return;

        if (refreshView.Background is SolidColorBrush solidBrush)
        {
            handler.PlatformView.RefreshBackgroundColor = solidBrush.Color;
        }
    }

    /// <summary>A pull starts a refresh only while <c>RefreshView.IsRefreshEnabled</c> is true.</summary>
    public static void MapIsRefreshEnabled(RefreshViewHandler handler, RefreshView refreshView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsPullEnabled = ((IRefreshView)refreshView).IsRefreshEnabled;
    }

    /// <summary>MAUI's <c>MapRefreshViewBackground</c>: the refresh indicator's background.</summary>
    public static void MapRefreshViewBackground(IRefreshViewHandler handler, IView view)
    {
        if (handler is RefreshViewHandler linuxHandler && view is RefreshView refreshView)
            MapBackground(linuxHandler, refreshView);
    }
}
