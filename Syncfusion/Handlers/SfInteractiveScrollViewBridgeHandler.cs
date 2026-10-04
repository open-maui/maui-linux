// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Syncfusion.Maui.Core.Internals;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Handler for Syncfusion's <see cref="SfInteractiveScrollView"/> (the pan and
/// zoom scroller in Syncfusion.Maui.Core, used by SfToolbar and the viewers
/// built on it), replacing the internal <c>SfInteractiveScrollViewHandler</c>
/// whose platform-neutral <c>CreatePlatformView</c> throws. As on Windows, where
/// the platform view is a plain ScrollViewer, the control's own content holder
/// (<c>PresentedContent</c>, which Syncfusion sizes to the extent and zooms)
/// scrolls in OpenMaui's scroll view; the scroll position, viewport size and
/// scroll-finished signal are reported back through the control's internal
/// members, as the Windows handler reports them, and <c>ScrollToAsync</c>
/// requests made before the content is laid out wait for it.
/// </summary>
public class SfInteractiveScrollViewBridgeHandler : LinuxViewHandler<SfInteractiveScrollView, SkiaSfInteractiveScrollView>
{
    public static IPropertyMapper<SfInteractiveScrollView, SfInteractiveScrollViewBridgeHandler> Mapper =
        new PropertyMapper<SfInteractiveScrollView, SfInteractiveScrollViewBridgeHandler>(ViewHandler.ViewMapper)
        {
            ["PresentedContent"] = MapContent,
            [nameof(SfInteractiveScrollView.Content)] = MapContent,
            [nameof(SfInteractiveScrollView.Orientation)] = MapScrollBars,
            [nameof(SfInteractiveScrollView.HorizontalScrollBarVisibility)] = MapScrollBars,
            [nameof(SfInteractiveScrollView.VerticalScrollBarVisibility)] = MapScrollBars,
        };

    public static CommandMapper<SfInteractiveScrollView, SfInteractiveScrollViewBridgeHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
            ["ScrollTo"] = MapScrollTo,
        };

    private double _reportedX, _reportedY;

    public SfInteractiveScrollViewBridgeHandler() : base(Mapper, CommandMapper)
    {
    }

    protected override SkiaSfInteractiveScrollView CreatePlatformView() => new();

    protected override void ConnectHandler(SkiaSfInteractiveScrollView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.MauiView = VirtualView;
        platformView.Scrolled += OnPlatformScrolled;
        platformView.LaidOut += OnLaidOut;
        VirtualView.SizeChanged += OnSizeChanged;
    }

    protected override void DisconnectHandler(SkiaSfInteractiveScrollView platformView)
    {
        VirtualView.SizeChanged -= OnSizeChanged;
        platformView.Scrolled -= OnPlatformScrolled;
        platformView.LaidOut -= OnLaidOut;
        platformView.Content = null;
        platformView.MauiView = null;
        base.DisconnectHandler(platformView);
    }

    public static void MapContent(SfInteractiveScrollViewBridgeHandler handler, SfInteractiveScrollView scrollView)
    {
        if (handler.PlatformView is not { } view || handler.MauiContext is not { } context)
            return;
        if (SfReflect.Get(scrollView, "PresentedContent") is not View presented)
        {
            view.Content = null;
            return;
        }
        view.Content = SfItemViews.PlatformOf(presented, context);
    }

    public static void MapScrollBars(SfInteractiveScrollViewBridgeHandler handler, SfInteractiveScrollView scrollView)
    {
        if (handler.PlatformView is not { } view)
            return;
        view.Orientation = scrollView.Orientation switch
        {
            Microsoft.Maui.ScrollOrientation.Horizontal => ScrollOrientation.Horizontal,
            Microsoft.Maui.ScrollOrientation.Both => ScrollOrientation.Both,
            Microsoft.Maui.ScrollOrientation.Neither => ScrollOrientation.Neither,
            _ => ScrollOrientation.Vertical,
        };
        view.HorizontalScrollBarVisibility = Visibility(scrollView.HorizontalScrollBarVisibility);
        view.VerticalScrollBarVisibility = Visibility(scrollView.VerticalScrollBarVisibility);
    }

    private static ScrollBarVisibility Visibility(Microsoft.Maui.ScrollBarVisibility visibility) => visibility switch
    {
        Microsoft.Maui.ScrollBarVisibility.Always => ScrollBarVisibility.Always,
        Microsoft.Maui.ScrollBarVisibility.Never => ScrollBarVisibility.Never,
        _ => ScrollBarVisibility.Default,
    };

    /// <summary>
    /// ScrollTo (Syncfusion's internal ScrollToParameters). Done now when the
    /// content can scroll that far, else once it has been laid out, as the
    /// Windows handler defers it while a content layout is pending.
    /// </summary>
    public static void MapScrollTo(SfInteractiveScrollViewBridgeHandler handler, SfInteractiveScrollView scrollView, object? args)
    {
        if (args == null || handler.PlatformView is not { } view)
            return;
        double x = SfReflect.Get(args, "ScrollX") is double sx ? sx : scrollView.ScrollX;
        double y = SfReflect.Get(args, "ScrollY") is double sy ? sy : scrollView.ScrollY;
        bool animated = SfReflect.Get(args, "Animated") is true;
        if (!view.CanScrollTo((float)x, (float)y))
        {
            view.PendingScroll = new SKPoint((float)x, (float)y);
            return;
        }
        handler.ScrollTo((float)x, (float)y, animated);
    }

    private void ScrollTo(float x, float y, bool animated)
    {
        if (PlatformView is not { } view || VirtualView is not { } scrollView)
            return;
        view.PendingScroll = null;
        view.ScrollTo(x, y, animated);
        // A scroll to where the view already is raises no Scrolled; report it
        // anyway so ScrollToAsync completes, as the Windows handler does.
        Report(view.ScrollX, view.ScrollY);
        SfReflect.Call(scrollView, "SendScrollFinished");
    }

    private void OnLaidOut(object? sender, EventArgs e)
    {
        if (PlatformView is { PendingScroll: { } pending } view && view.CanScrollTo(pending.X, pending.Y))
            ScrollTo(pending.X, pending.Y, animated: false);
    }

    private void OnPlatformScrolled(object? sender, ScrolledEventArgs e)
    {
        Report(e.ScrollX, e.ScrollY);
        if (VirtualView is { } scrollView)
            SfReflect.Call(scrollView, "SendScrollFinished");
    }

    private void Report(double x, double y)
    {
        if (VirtualView is not { } scrollView)
            return;
        if (SfReflect.Create(typeof(ScrollChangedEventArgs), x, y, _reportedX, _reportedY) is ScrollChangedEventArgs args)
            SfReflect.Call(scrollView, "OnScrollChanged", args);
        _reportedX = x;
        _reportedY = y;
    }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        if (VirtualView is not { } scrollView)
            return;
        SfReflect.Set(scrollView, nameof(SfInteractiveScrollView.ViewportWidth), scrollView.Width);
        SfReflect.Set(scrollView, nameof(SfInteractiveScrollView.ViewportHeight), scrollView.Height);
    }
}

/// <summary>
/// OpenMaui's scroll view with the one hook <see cref="SfInteractiveScrollViewBridgeHandler"/>
/// needs: a signal after each pass that measures the content, so a scroll
/// requested before the content had its size can run.
/// </summary>
public class SkiaSfInteractiveScrollView : SkiaScrollView
{
    /// <summary>A scroll waiting for the content to be large enough.</summary>
    public SKPoint? PendingScroll { get; set; }

    /// <summary>Raised after the content has been measured and arranged.</summary>
    public event EventHandler? LaidOut;

    /// <summary>
    /// The wheel. With Shift held the Windows handler turns vertical
    /// scrolling off (<c>OnKeyDown</c>, restored by <c>OnKeyUp</c>), so the
    /// ScrollViewer scrolls the wheel horizontally; the same happens here.
    /// </summary>
    public override void OnScroll(ScrollEventArgs e)
    {
        if ((e.Modifiers & KeyModifiers.Shift) == 0 || e.DeltaY == 0)
        {
            base.OnScroll(e);
            return;
        }
        var horizontal = new ScrollEventArgs(e.X, e.Y, e.DeltaX + e.DeltaY, 0, e.Modifiers);
        base.OnScroll(horizontal);
        if (horizontal.Handled)
            e.Handled = true;
    }

    /// <summary>True when the content extends far enough for (x, y).</summary>
    public bool CanScrollTo(float x, float y)
        => Bounds.Width > 0 && x <= ScrollableWidth + 0.5f && y <= ScrollableHeight + 0.5f;

    protected override Rect ArrangeOverride(Rect bounds)
    {
        var result = base.ArrangeOverride(bounds);
        if (PendingScroll != null)
            RaiseLaidOutSoon();
        return result;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        base.OnDraw(canvas, bounds);
        if (PendingScroll != null)
            RaiseLaidOutSoon();
    }

    private bool _raising;

    private void RaiseLaidOutSoon()
    {
        if (_raising)
            return;
        _raising = true;
        try
        {
            LaidOut?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Applying a pending scroll failed", ex);
        }
        finally
        {
            _raising = false;
        }
    }
}
