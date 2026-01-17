// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Hosting;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for FlyoutPage on Linux using Skia rendering.
/// Maps IFlyoutView interface to SkiaFlyoutPage platform view.
/// </summary>
public partial class FlyoutPageHandler : ViewHandler<IFlyoutView, SkiaFlyoutPage>
{
    private bool _isUpdatingPresented;

    public static IPropertyMapper<IFlyoutView, FlyoutPageHandler> Mapper = new PropertyMapper<IFlyoutView, FlyoutPageHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IFlyoutView.Flyout)] = MapFlyout,
        [nameof(IFlyoutView.Detail)] = MapDetail,
        [nameof(IFlyoutView.IsPresented)] = MapIsPresented,
        [nameof(IFlyoutView.FlyoutWidth)] = MapFlyoutWidth,
        [nameof(IFlyoutView.IsGestureEnabled)] = MapIsGestureEnabled,
        [nameof(IFlyoutView.FlyoutBehavior)] = MapFlyoutBehavior,
        [nameof(IView.Background)] = MapBackground,
    };

    public static CommandMapper<IFlyoutView, FlyoutPageHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public FlyoutPageHandler() : base(Mapper, CommandMapper)
    {
    }

    public FlyoutPageHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaFlyoutPage CreatePlatformView()
    {
        return new SkiaFlyoutPage();
    }

    protected override void ConnectHandler(SkiaFlyoutPage platformView)
    {
        base.ConnectHandler(platformView);
        platformView.IsPresentedChanged += OnIsPresentedChanged;
    }

    protected override void DisconnectHandler(SkiaFlyoutPage platformView)
    {
        platformView.IsPresentedChanged -= OnIsPresentedChanged;
        platformView.Flyout = null;
        platformView.Detail = null;
        base.DisconnectHandler(platformView);
    }

    private void OnIsPresentedChanged(object? sender, EventArgs e)
    {
        if (VirtualView is null || PlatformView is null || _isUpdatingPresented) return;

        try
        {
            _isUpdatingPresented = true;
            // Sync back to the virtual view
            if (VirtualView is FlyoutPage flyoutPage)
            {
                flyoutPage.IsPresented = PlatformView.IsPresented;
            }
        }
        finally
        {
            _isUpdatingPresented = false;
        }
    }

    public static void MapFlyout(FlyoutPageHandler handler, IFlyoutView flyoutView)
    {
        if (handler.PlatformView is null || handler.MauiContext is null) return;

        var flyout = flyoutView.Flyout;
        if (flyout == null)
        {
            handler.PlatformView.Flyout = null;
            return;
        }

        // Create handler for flyout content
        if (flyout.Handler == null)
        {
            flyout.Handler = flyout.ToViewHandler(handler.MauiContext);
        }

        if (flyout.Handler?.PlatformView is SkiaView skiaFlyout)
        {
            handler.PlatformView.Flyout = skiaFlyout;
        }
    }

    public static void MapDetail(FlyoutPageHandler handler, IFlyoutView flyoutView)
    {
        if (handler.PlatformView is null || handler.MauiContext is null) return;

        var detail = flyoutView.Detail;
        if (detail == null)
        {
            handler.PlatformView.Detail = null;
            return;
        }

        // Create handler for detail content
        if (detail.Handler == null)
        {
            detail.Handler = detail.ToViewHandler(handler.MauiContext);
        }

        if (detail.Handler?.PlatformView is SkiaView skiaDetail)
        {
            handler.PlatformView.Detail = skiaDetail;
        }
    }

    public static void MapIsPresented(FlyoutPageHandler handler, IFlyoutView flyoutView)
    {
        if (handler.PlatformView is null || handler._isUpdatingPresented) return;

        try
        {
            handler._isUpdatingPresented = true;
            handler.PlatformView.IsPresented = flyoutView.IsPresented;
        }
        finally
        {
            handler._isUpdatingPresented = false;
        }
    }

    public static void MapFlyoutWidth(FlyoutPageHandler handler, IFlyoutView flyoutView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.FlyoutWidth = (float)flyoutView.FlyoutWidth;
    }

    public static void MapIsGestureEnabled(FlyoutPageHandler handler, IFlyoutView flyoutView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.GestureEnabled = flyoutView.IsGestureEnabled;
    }

    public static void MapFlyoutBehavior(FlyoutPageHandler handler, IFlyoutView flyoutView)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.FlyoutLayoutBehavior = flyoutView.FlyoutBehavior switch
        {
            Microsoft.Maui.FlyoutBehavior.Disabled => FlyoutLayoutBehavior.Default,
            Microsoft.Maui.FlyoutBehavior.Flyout => FlyoutLayoutBehavior.Popover,
            Microsoft.Maui.FlyoutBehavior.Locked => FlyoutLayoutBehavior.Split,
            _ => FlyoutLayoutBehavior.Default
        };
    }

    public static void MapBackground(FlyoutPageHandler handler, IFlyoutView flyoutView)
    {
        if (handler.PlatformView is null) return;

        if (flyoutView is FlyoutPage flyoutPage && flyoutPage.Background is SolidColorBrush solidBrush)
        {
            handler.PlatformView.ScrimColor = solidBrush.Color.ToSKColor().WithAlpha(100);
        }
    }
}
