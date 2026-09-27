// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Hosting;
using Syncfusion.Maui.Core.Internals;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Feeds Syncfusion's touch and gesture detectors from OpenMaui's pointer
/// routing. A pointer event reaches the view under the pointer and every
/// ancestor, which is how native touch reaches Syncfusion's listeners on the
/// other platforms; for each view that has a detector the bridge raises the
/// raw touch (pressed, moved, released, entered, exited) and synthesises tap,
/// double-tap, right-tap and long-press. Points are in the view's own
/// coordinates, as Syncfusion expects.
/// </summary>
internal static class SfInputBridge
{
    private const double TapSlop = 10;
    private static readonly TimeSpan LongPressDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan DoubleTapWindow = TimeSpan.FromMilliseconds(400);

    private sealed class Press
    {
        public Point Start;
        public DateTime Time;
        public bool Right;
        public bool Moved;
        public bool LongPressed;
        public int Generation;
    }

    private static readonly ConditionalWeakTable<View, Press> s_presses = new();
    private static readonly ConditionalWeakTable<View, StrongBox<(DateTime Time, Point Point)>> s_lastTaps = new();
    private static int s_generation;
    private static int s_installed;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        SfInternals.ReportMissingOnce();
        SkiaTextMeasurer.EnsureInstalled();
        SkiaView.PointerRouted += OnPointerRouted;
        LinuxTicker.Ticked += SfInvalidation.OnAnimationTick;
    }

    private static void OnPointerRouted(View view, SkiaView.RoutedPointerKind kind, PointerEventArgs e)
    {
        SkiaTextMeasurer.EnsureInstalled();
        var touch = SfInternals.TouchDetectorOf(view);
        var gesture = SfInternals.GestureDetectorOf(view);
        if (touch == null && gesture == null)
            return;

        var origin = OriginOf(view);
        var local = new Point(e.X - origin.X, e.Y - origin.Y);
        var window = new Point(e.X, e.Y);
        Func<IElement?, Point?> position = element => element == null
            ? window
            : (OriginOf(element) is var o ? new Point(window.X - o.X, window.Y - o.Y) : null);

        s_presses.TryGetValue(view, out var press);
        switch (kind)
        {
            case SkiaView.RoutedPointerKind.Pressed:
                press = new Press
                {
                    Start = local,
                    Time = DateTime.UtcNow,
                    Right = e.Button == PointerButton.Right,
                    Generation = ++s_generation,
                };
                s_presses.AddOrUpdate(view, press);
                if (touch != null)
                    SfInternals.Touch(touch, position, PointerActions.Pressed, local, !press.Right, press.Right);
                if (gesture != null && !press.Right)
                    ScheduleLongPress(view, gesture, press, position);
                break;

            case SkiaView.RoutedPointerKind.Moved:
                if (press != null && Distance(press.Start, local) > TapSlop)
                    press.Moved = true;
                if (touch != null)
                    SfInternals.Touch(touch, position, PointerActions.Moved, local, press is { Right: false }, press is { Right: true });
                break;

            case SkiaView.RoutedPointerKind.Released:
                s_presses.Remove(view);
                if (touch != null)
                    SfInternals.Touch(touch, position, PointerActions.Released, local, false, false);
                if (gesture != null && press != null)
                    Release(view, gesture, press, local);
                break;

            case SkiaView.RoutedPointerKind.Entered:
                if (touch != null)
                    SfInternals.Touch(touch, position, PointerActions.Entered, local, false, false);
                break;

            case SkiaView.RoutedPointerKind.Exited:
                if (touch != null)
                    SfInternals.Touch(touch, position, PointerActions.Exited, local, false, false);
                break;
        }

        // Syncfusion redraws through its own handler, which is never ours;
        // repaint after anything it may have reacted to.
        if (kind == SkiaView.RoutedPointerKind.Moved)
            (view.Handler?.PlatformView as SkiaView)?.Invalidate();
        else
            SfInvalidation.InvalidateAll(drawingOnly: false);
    }

    private static void Release(View view, GestureDetector gesture, Press press, Point local)
    {
        if (press.Right)
        {
            SfInternals.RightTap(gesture, local);
            return;
        }
        if (press.Moved || press.LongPressed || Distance(press.Start, local) > TapSlop)
            return;

        var now = DateTime.UtcNow;
        SfInternals.Tap(gesture, local, 1);
        if (s_lastTaps.TryGetValue(view, out var last)
            && now - last.Value.Time <= DoubleTapWindow
            && Distance(last.Value.Point, local) <= TapSlop)
        {
            SfInternals.Tap(gesture, local, 2);
            s_lastTaps.Remove(view);
        }
        else
        {
            s_lastTaps.AddOrUpdate(view, new StrongBox<(DateTime, Point)>((now, local)));
        }
    }

    private static void ScheduleLongPress(View view, GestureDetector gesture, Press press, Func<IElement?, Point?> position)
    {
        int generation = press.Generation;
        var dispatcher = view.Dispatcher;
        if (dispatcher == null)
            return;
        dispatcher.DispatchDelayed(LongPressDelay, () =>
        {
            if (!s_presses.TryGetValue(view, out var current) || current.Generation != generation || current.Moved)
                return;
            current.LongPressed = true;
            SfInternals.LongPress(gesture, position, current.Start);
            SfInvalidation.InvalidateAll(drawingOnly: false);
        });
    }

    private static Point OriginOf(IElement element) =>
        (element.Handler as IViewHandler)?.PlatformView is SkiaView skia
            ? new Point(skia.Bounds.X, skia.Bounds.Y)
            : Point.Zero;

    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
