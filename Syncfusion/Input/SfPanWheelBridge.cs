// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using Syncfusion.Maui.Core.Internals;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Mouse drags and the wheel for Syncfusion's detectors, as the Windows build
/// raises them: a drag with the left button held is a pan (Started on the
/// first move, then Running with the movement since the previous move,
/// Completed on release), for views with a pan listener (a chart's
/// ChartZoomPanBehavior pans, SfCartesianChart's selection zoom); the wheel
/// goes to the view's touch detector as a scroll with Windows' wheel delta
/// (120 per notch, positive away from the user), which zooms a chart with
/// ChartZoomPanBehavior. Ctrl with the wheel is a pinch for views with a
/// pinch listener, which is how a precision touchpad's pinch reaches Windows
/// apps: one step per notch (a scale of 1.2 per notch, finer for touchpad
/// fractions), centred on the pointer. Points are in the view's own
/// coordinates.
/// </summary>
internal static class SfPanWheelBridge
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const double WheelDeltaPerNotch = 120;
    private const double PinchScalePerNotch = 1.2;

    private static readonly FieldInfo? s_panListeners = typeof(GestureDetector).GetField("panGestureListeners", Any);
    private static readonly FieldInfo? s_pinchListeners = typeof(GestureDetector).GetField("pinchGestureListeners", Any);

    private static readonly MethodInfo? s_onPinch = typeof(GestureDetector).GetMethod("OnPinch", Any, null,
        new[] { typeof(Func<IElement?, Point?>), typeof(GestureStatus), typeof(Point), typeof(double), typeof(float) }, null);

    private static readonly MethodInfo? s_onScroll = typeof(GestureDetector).GetMethod("OnScroll", Any, null,
        new[] { typeof(Func<IElement?, Point?>), typeof(GestureStatus), typeof(Point), typeof(Point), typeof(Point) }, null);

    private static readonly MethodInfo? s_onScrollAction = typeof(TouchDetector).GetMethod("OnScrollAction", Any, null,
        new[] { typeof(long), typeof(Point), typeof(double), typeof(bool?) }, null);

    private sealed class Drag
    {
        public Point Last;
        public bool Started;
    }

    private static readonly ConditionalWeakTable<View, Drag> s_drags = new();
    private static int s_installed;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        if (s_onScroll == null || s_panListeners == null || s_onScrollAction == null)
            DiagnosticLog.Warn("Syncfusion", "This Syncfusion.Maui.Core release lacks the pan or wheel entry points; dragging or wheel zoom is disabled.");
        if (s_onPinch == null || s_pinchListeners == null)
            DiagnosticLog.Warn("Syncfusion", "This Syncfusion.Maui.Core release lacks the pinch entry point; ctrl+wheel zoom is disabled.");
        SkiaView.PointerRoutedAny += OnPointerRouted;
        SkiaView.ScrollRouted += OnScrollRouted;
    }

    private static void OnPointerRouted(View view, SkiaView.RoutedPointerKind kind, PointerEventArgs e)
    {
        if (s_onScroll == null)
            return;
        var gesture = SfInternals.GestureDetectorOf(view);
        if (gesture == null || s_panListeners?.GetValue(gesture) is not ICollection { Count: > 0 })
            return;

        var origin = OriginOf(view);
        var local = new Point(e.X - origin.X, e.Y - origin.Y);
        var window = new Point(e.X, e.Y);
        Func<IElement?, Point?> position = element => element == null
            ? window
            : (OriginOf(element) is var o ? new Point(window.X - o.X, window.Y - o.Y) : null);

        switch (kind)
        {
            case SkiaView.RoutedPointerKind.Pressed when e.Button == PointerButton.Left:
                s_drags.AddOrUpdate(view, new Drag { Last = local });
                break;

            case SkiaView.RoutedPointerKind.Moved when s_drags.TryGetValue(view, out var drag):
                var translate = new Point(local.X - drag.Last.X, local.Y - drag.Last.Y);
                if (translate.X == 0 && translate.Y == 0)
                    break;
                drag.Last = local;
                Pan(gesture, position, drag.Started ? GestureStatus.Running : GestureStatus.Started, local, translate);
                drag.Started = true;
                break;

            case SkiaView.RoutedPointerKind.Released when s_drags.TryGetValue(view, out var ended):
                s_drags.Remove(view);
                if (ended.Started)
                    Pan(gesture, position, GestureStatus.Completed, local, Point.Zero);
                SfInvalidation.InvalidateAll(drawingOnly: false);
                break;
        }
    }

    private static void Pan(GestureDetector gesture, Func<IElement?, Point?> position, GestureStatus status, Point point, Point translate)
    {
        try
        {
            s_onScroll!.Invoke(gesture, new object[] { position, status, point, translate, Point.Zero });
        }
        catch (TargetInvocationException ex)
        {
            DiagnosticLog.Error("Syncfusion", "A pan listener failed", ex.InnerException ?? ex);
        }
    }

    private static void OnScrollRouted(View view, ScrollEventArgs e)
    {
        if (e.DeltaY == 0)
            return;
        var origin = OriginOf(view);
        var local = new Point(e.X - origin.X, e.Y - origin.Y);
        if (e.IsControlPressed && TryPinch(view, e, local))
            return;
        if (s_onScrollAction == null)
            return;
        var touch = SfInternals.TouchDetectorOf(view);
        if (touch == null)
            return;
        try
        {
            // OpenMaui's DeltaY is positive towards the user (scroll down);
            // Windows' wheel delta is positive away from the user.
            var handled = s_onScrollAction.Invoke(touch, new object?[] { 1L, local, -e.DeltaY * WheelDeltaPerNotch, (bool?)e.Handled });
            if (handled is true)
                e.Handled = true;
        }
        catch (TargetInvocationException ex)
        {
            DiagnosticLog.Error("Syncfusion", "A wheel listener failed", ex.InnerException ?? ex);
        }
        SfInvalidation.InvalidateAll(drawingOnly: false);
    }

    /// <summary>
    /// One ctrl+wheel step as a complete pinch (Started, Running with the
    /// step's scale, Completed), so each notch zooms by the same factor about
    /// the pointer and a following drag pans again. The angle is NaN: a wheel
    /// has no direction, so directional zooming leaves both axes free.
    /// </summary>
    private static bool TryPinch(View view, ScrollEventArgs e, Point local)
    {
        if (s_onPinch == null || SfInternals.GestureDetectorOf(view) is not { } gesture
            || s_pinchListeners?.GetValue(gesture) is not ICollection { Count: > 0 })
            return false;
        var window = new Point(e.X, e.Y);
        Func<IElement?, Point?> position = element => element == null
            ? window
            : (OriginOf(element) is var o ? new Point(window.X - o.X, window.Y - o.Y) : null);
        var scale = (float)Math.Pow(PinchScalePerNotch, -e.DeltaY);
        try
        {
            s_onPinch.Invoke(gesture, new object[] { position, GestureStatus.Started, local, double.NaN, 1f });
            s_onPinch.Invoke(gesture, new object[] { position, GestureStatus.Running, local, double.NaN, scale });
            s_onPinch.Invoke(gesture, new object[] { position, GestureStatus.Completed, local, double.NaN, 1f });
        }
        catch (TargetInvocationException ex)
        {
            DiagnosticLog.Error("Syncfusion", "A pinch listener failed", ex.InnerException ?? ex);
        }
        // The pinch is the ctrl+wheel's whole meaning here: a scroll view
        // further up must not scroll as well.
        e.Handled = true;
        SfInvalidation.InvalidateAll(drawingOnly: false);
        return true;
    }

    /// <summary>
    /// The element's window position. ScreenBounds, not Bounds: content inside
    /// a scroll view (SfListView's rows) keeps unscrolled bounds.
    /// </summary>
    private static Point OriginOf(IElement element) =>
        (element.Handler as IViewHandler)?.PlatformView is SkiaView skia
            ? new Point(skia.ScreenBounds.X, skia.ScreenBounds.Y)
            : Point.Zero;
}
