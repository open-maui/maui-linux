// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using Syncfusion.Maui.Core.Internals;
using static Microsoft.Maui.Platform.Linux.Syncfusion.SfDyn;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// SfPullToRefresh (also what SfDataGrid's <c>AllowPullToRefresh</c> uses) pulls with the mouse,
/// as on Windows. Every platform build drives the pull from native pointer events on the pulled
/// content; the platform-neutral build has none (<c>ConfigTouch</c> and the control's touch
/// listener are empty), so nothing pulled. The Windows handlers are ported onto OpenMaui's pointer
/// routing: a press on the content (not on a scroll bar) starts, unless the content under the
/// pointer is scrolled down; the drag pulls by its distance from the press; the release ends.
/// The two helpers that read native views are filled in too: the scroll offset of a scroll view
/// in the content (<c>GetChildScrollOffset</c>) and a child's position in the control
/// (<c>ChildLocationToScreen</c>).
/// </summary>
internal static class SfPullToRefreshPatches
{
    private const string Asm = "Syncfusion.Maui.PullToRefresh";
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private sealed class Press
    {
        public Point Start;
        public bool ChildScrolled;
    }

    private static readonly ConditionalWeakTable<View, Press> s_presses = new();
    private static Type? s_pullToRefresh;
    private static Type? s_helpers;
    private static int s_installed;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        s_pullToRefresh = Type("Syncfusion.Maui.PullToRefresh.SfPullToRefresh", Asm);
        if (s_pullToRefresh == null)
            return; // SfPullToRefresh is not part of the app
        // Only over the neutral build, whose ConfigTouch wires nothing.
        if (Il(s_pullToRefresh.GetMethod("ConfigTouch", Any)).Length > 2)
            return;
        s_helpers = Type("Syncfusion.Maui.PullToRefresh.PullToRefreshHelpers", Asm);
        var harmony = new Harmony("com.openmaui.syncfusion.pulltorefresh");
        Patch(harmony, "GetChildScrollOffset", nameof(GetChildScrollOffset_Prefix));
        Patch(harmony, "ChildLocationToScreen", nameof(ChildLocationToScreen_Prefix));
        SkiaView.PointerRoutedAny += OnPointerRouted;
    }

    private static void Patch(Harmony harmony, string method, string patch)
    {
        try
        {
            var target = s_pullToRefresh!.GetMethod(method, Any);
            if (target != null && Il(target).Length <= 16)
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(SfPullToRefreshPatches).GetMethod(patch, BindingFlags.Static | BindingFlags.NonPublic)));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Patching SfPullToRefresh.{method} failed", ex);
        }
    }

    // SfPullToRefresh.GetChildScrollOffset(object scrollView): the vertical offset of a scroll view.
    private static bool GetChildScrollOffset_Prefix(object scrollView, ref double __result)
    {
        __result = scrollView is SkiaScrollView scroller ? scroller.ScrollY : 0.0;
        return false;
    }

    // SfPullToRefresh.ChildLocationToScreen(object native): the child's offset in the control.
    private static bool ChildLocationToScreen_Prefix(object __instance, object native, ref Point __result)
    {
        __result = Point.Zero;
        if (native is SkiaView child && (__instance as VisualElement)?.Handler?.PlatformView is SkiaView control)
            __result = new Point(Math.Abs(child.ScreenBounds.X - control.ScreenBounds.X), Math.Abs(child.ScreenBounds.Y - control.ScreenBounds.Y));
        return false;
    }

    private static void OnPointerRouted(View view, SkiaView.RoutedPointerKind kind, PointerEventArgs e)
    {
        if (s_pullToRefresh == null || !s_pullToRefresh.IsInstanceOfType(view))
            return;
        try
        {
            switch (kind)
            {
                case SkiaView.RoutedPointerKind.Pressed when e.Button == PointerButton.Left:
                    Pressed(view, e);
                    break;
                case SkiaView.RoutedPointerKind.Moved when s_presses.TryGetValue(view, out var press):
                    Moved(view, press, e);
                    break;
                case SkiaView.RoutedPointerKind.Released when s_presses.TryGetValue(view, out _):
                    s_presses.Remove(view);
                    Handle(view, PointerActions.Released, Point.Zero);
                    break;
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "SfPullToRefresh pointer handling failed", ex);
        }
    }

    // PullableContentPointerPressed.
    private static void Pressed(View view, PointerEventArgs e)
    {
        if (view.Handler?.PlatformView is not SkiaView control || OnScrollBar(control, e.X, e.Y))
            return;
        var local = new Point(e.X - control.ScreenBounds.X, e.Y - control.ScreenBounds.Y);
        var content = Get(view, "PullableContent") as IVisualTreeElement;
        var first = content == null ? null : VisualTreeElementExtensions.GetVisualTreeDescendants(content).FirstOrDefault();
        Set(view, "childLoopCount", 0);
        bool scrolled = Call(view, "IsChildElementScrolled", first, local) is true;
        s_presses.AddOrUpdate(view, new Press { Start = new Point(e.X, e.Y), ChildScrolled = scrolled });
        Handle(view, PointerActions.Pressed, Point.Zero);
    }

    // PullableContentManipulationDelta: the cumulative translation since the press.
    private static void Moved(View view, Press press, PointerEventArgs e)
    {
        if (!Is(view, "IsIPullToRefresh") && s_helpers != null && Get(view, "PullableContent") is IView content)
        {
            var args = new object?[] { content, view, false, false };
            s_helpers.GetMethod("CheckChildren", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(null, args);
            Set(view, "IsIPullToRefresh", args[2]);
        }
        if (Is(view, "IsIPullToRefresh") || press.ChildScrolled)
            return;
        Handle(view, PointerActions.Moved, new Point(e.X - press.Start.X, e.Y - press.Start.Y));
    }

    private static void Handle(View view, PointerActions action, Point point) => Call(view, "HandleTouchInteraction", action, point);

    /// <summary>IsSourceScrollBar: the press is on a scroll bar of a scroll view in the content.</summary>
    private static bool OnScrollBar(SkiaView control, float x, float y)
    {
        var hit = control.HitTest(x, y);
        if (hit is not SkiaScrollView scroller)
            return false;
        var b = scroller.ScreenBounds;
        return x >= b.Right - scroller.ScrollBarWidth || y >= b.Bottom - scroller.ScrollBarWidth;
    }
}
