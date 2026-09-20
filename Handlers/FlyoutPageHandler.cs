// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
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
        // The Controls-level property raises "FlyoutLayoutBehavior"; the
        // IFlyoutView projection above only covers the initial mapping.
        [nameof(FlyoutPage.FlyoutLayoutBehavior)] = MapFlyoutBehavior,
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
            // Sync back to the virtual view. MAUI throws when IsPresented is
            // cleared while it expects split mode; the platform never clears
            // a split flyout, but stay defensive.
            if (VirtualView is FlyoutPage flyoutPage && flyoutPage.IsPresented != PlatformView.IsPresented)
            {
                try
                {
                    flyoutPage.IsPresented = PlatformView.IsPresented;
                }
                catch (InvalidOperationException ex)
                {
                    DiagnosticLog.Debug("FlyoutPageHandler", "IsPresented sync refused by MAUI", ex);
                }
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

        if (flyoutView is FlyoutPage flyoutPage)
        {
            // Use the Controls value directly: it distinguishes the Split
            // variants that IFlyoutView collapses into Locked.
            // Default follows MAUI's own decision (IFlyoutPageController.
            // ShouldShowSplitMode): split on desktop/tablet idioms, popover on
            // phones — MAUI refuses IsPresented=false while it expects split.
            var controller = (IFlyoutPageController)flyoutPage;
            handler.PlatformView.FlyoutLayoutBehavior = flyoutPage.FlyoutLayoutBehavior switch
            {
                Microsoft.Maui.Controls.FlyoutLayoutBehavior.Popover => FlyoutLayoutBehavior.Popover,
                Microsoft.Maui.Controls.FlyoutLayoutBehavior.Split => FlyoutLayoutBehavior.Split,
                Microsoft.Maui.Controls.FlyoutLayoutBehavior.SplitOnLandscape => FlyoutLayoutBehavior.SplitOnLandscape,
                Microsoft.Maui.Controls.FlyoutLayoutBehavior.SplitOnPortrait => FlyoutLayoutBehavior.SplitOnPortrait,
                _ => controller.ShouldShowSplitMode ? FlyoutLayoutBehavior.Split : FlyoutLayoutBehavior.Popover
            };
        }
        else
        {
            handler.PlatformView.FlyoutLayoutBehavior = flyoutView.FlyoutBehavior switch
            {
                Microsoft.Maui.FlyoutBehavior.Locked => FlyoutLayoutBehavior.Split,
                Microsoft.Maui.FlyoutBehavior.Flyout => FlyoutLayoutBehavior.Popover,
                _ => FlyoutLayoutBehavior.Default
            };
        }

        // Split keeps the flyout presented; reflect that on the virtual view
        // so IsPresented reads true for the app as well.
        if (handler.PlatformView.IsSplit && flyoutView is FlyoutPage page && !page.IsPresented && !handler._isUpdatingPresented)
        {
            try
            {
                handler._isUpdatingPresented = true;
                page.IsPresented = true;
            }
            catch (InvalidOperationException)
            {
                // MAUI refuses IsPresented changes for some behaviors; the
                // platform still shows the split flyout.
            }
            finally
            {
                handler._isUpdatingPresented = false;
            }
        }
    }

    public static void MapBackground(FlyoutPageHandler handler, IFlyoutView flyoutView)
    {
        if (handler.PlatformView is null) return;

        // Brush.Default is a SolidColorBrush with a null Color.
        if (flyoutView is FlyoutPage flyoutPage && flyoutPage.Background is SolidColorBrush { Color: Color color })
        {
            handler.PlatformView.ScrimColor = color.WithAlpha(100f / 255f);
        }
    }
}
