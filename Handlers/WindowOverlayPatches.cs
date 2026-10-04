// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Numerics;
using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// MAUI's window overlays (<c>Window.AddOverlay</c>, <see cref="IWindowOverlay"/>, and the
/// window's <see cref="VisualDiagnosticsOverlay"/> with its adorners) on Linux. MAUI's
/// <see cref="WindowOverlay"/> is an <see cref="IDrawable"/> whose platform part, on Windows
/// a graphics view added on top of the window's root, draws it and redraws on
/// <c>Invalidate</c>; the platform-neutral build has no platform part, so nothing drew.
/// Here the window's renderer draws every initialized, visible overlay over the page (see
/// <c>WindowContext.ActiveWindowOverlays</c>), and these patches give the overlay the rest of
/// its platform part:
/// <list type="bullet">
/// <item><c>Invalidate</c> (and the element and visibility changes that call it) redraws the
/// window;</item>
/// <item>the diagnostics overlay redraws when a scroll view it tracks scrolls
/// (<c>AddScrollableElementHandler</c>) and on a UI change (<c>HandleUIChange</c>), as its
/// Windows build does;</item>
/// <item>MAUI's view-geometry helpers the adorners use (<c>GetBoundingBox</c>,
/// <c>GetViewTransform</c>, <c>GetPlatformViewBounds</c>) and <c>GetHostedWindow</c> read the
/// Skia view, where the neutral build returned the view's Frame (relative to its parent), a
/// zero matrix and null. <see cref="RectangleAdorner"/>'s model update is replaced as a whole,
/// since a one-line helper may have been inlined into it.</item>
/// </list>
/// Taps reach the overlay through the window's input routing (<c>WindowContext</c>).
/// </summary>
internal static class WindowOverlayPatches
{
    private static int s_installed;
    private static MethodInfo? s_onTappedInternal;
    private static FieldInfo? s_scrollViews;
    private static FieldInfo? s_adornerModel;
    private static MethodInfo? s_modelUpdate;

    // Scroll views a diagnostics overlay follows, with the handler it put on each.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<VisualDiagnosticsOverlay, List<(SkiaScrollView View, EventHandler<ScrolledEventArgs> Handler)>> s_scrollSubscriptions = new();

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        try
        {
            const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var harmony = new Harmony("com.openmaui.windowoverlay");
            var self = typeof(WindowOverlayPatches);
            HarmonyMethod Method(string name) => new(self.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic));

            var overlay = typeof(WindowOverlay);
            s_onTappedInternal = overlay.GetMethod("OnTappedInternal", Any, null, new[] { typeof(Point) }, null);

            // Every way the overlay's drawing changes: MAUI calls Invalidate from each of
            // these, but a one-line Invalidate can be inlined into them, so they are patched too.
            Postfix(harmony, overlay.GetMethod(nameof(WindowOverlay.Invalidate), Type.EmptyTypes), Method(nameof(Redraw_Postfix)));
            Postfix(harmony, overlay.GetMethod(nameof(WindowOverlay.AddWindowElement)), Method(nameof(Redraw_Postfix)));
            Postfix(harmony, overlay.GetMethod(nameof(WindowOverlay.RemoveWindowElement)), Method(nameof(Redraw_Postfix)));
            Postfix(harmony, overlay.GetMethod(nameof(WindowOverlay.RemoveWindowElements)), Method(nameof(Redraw_Postfix)));
            Postfix(harmony, overlay.GetProperty(nameof(WindowOverlay.IsVisible))?.SetMethod, Method(nameof(Redraw_Postfix)));
            Postfix(harmony, overlay.GetMethod(nameof(WindowOverlay.Initialize)), Method(nameof(Redraw_Postfix)));
            Postfix(harmony, overlay.GetMethod(nameof(WindowOverlay.Deinitialize)), Method(nameof(Redraw_Postfix)));
            // VisualDiagnosticsOverlay.HandleUIChange (Windows) redraws; the neutral build has no override.
            Postfix(harmony, overlay.GetMethod(nameof(WindowOverlay.HandleUIChange)), Method(nameof(HandleUIChange_Postfix)));

            var diagnostics = typeof(VisualDiagnosticsOverlay);
            s_scrollViews = diagnostics.GetField("_scrollViews", Any);
            Prefix(harmony, diagnostics.GetMethod(nameof(VisualDiagnosticsOverlay.AddScrollableElementHandler), new[] { typeof(IScrollView) }), Method(nameof(AddScrollableElementHandler_Prefix)));
            Prefix(harmony, diagnostics.GetMethod(nameof(VisualDiagnosticsOverlay.RemoveScrollableElementHandler), Type.EmptyTypes), Method(nameof(RemoveScrollableElementHandler_Prefix)));

            var extensions = typeof(IView).Assembly.GetType("Microsoft.Maui.Platform.ViewExtensions");
            if (extensions != null)
            {
                Prefix(harmony, extensions.GetMethod("GetViewTransform", Any, null, new[] { typeof(IView) }, null), Method(nameof(GetViewTransform_Prefix)));
                Prefix(harmony, extensions.GetMethod("GetBoundingBox", Any, null, new[] { typeof(IView) }, null), Method(nameof(GetBoundingBox_Prefix)));
                Prefix(harmony, extensions.GetMethod("GetPlatformViewBounds", Any, null, new[] { typeof(IView) }, null), Method(nameof(GetBoundingBox_Prefix)));
                Prefix(harmony, extensions.GetMethod("GetHostedWindow", Any, null, new[] { typeof(IView) }, null), Method(nameof(GetHostedWindow_Prefix)));
            }
            else
            {
                DiagnosticLog.Warn("WindowOverlayPatches", "Microsoft.Maui.Platform.ViewExtensions not found; adorners use the views' Frames");
            }

            var adorner = typeof(RectangleAdorner);
            s_adornerModel = adorner.GetField("model", Any);
            s_modelUpdate = s_adornerModel?.FieldType.GetMethod("Update", Any);
            if (s_adornerModel != null && s_modelUpdate != null)
                Prefix(harmony, adorner.GetMethod("UpdateModel", Any, null, Type.EmptyTypes, null), Method(nameof(UpdateModel_Prefix)));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WindowOverlayPatches", "Patching MAUI's window overlays failed", ex);
        }
    }

    private static void Postfix(Harmony harmony, MethodBase? target, HarmonyMethod patch)
    {
        if (target == null)
        {
            DiagnosticLog.Warn("WindowOverlayPatches", $"A WindowOverlay member to patch ({patch.method?.Name}) was not found");
            return;
        }
        harmony.Patch(target, postfix: patch);
    }

    private static void Prefix(Harmony harmony, MethodBase? target, HarmonyMethod patch)
    {
        if (target == null)
        {
            DiagnosticLog.Warn("WindowOverlayPatches", $"A member to patch for {patch.method?.Name} was not found");
            return;
        }
        harmony.Patch(target, prefix: patch);
    }

    private static void Redraw_Postfix(WindowOverlay __instance) => RequestRedraw(__instance.Window);

    private static void HandleUIChange_Postfix(WindowOverlay __instance)
    {
        if (__instance is VisualDiagnosticsOverlay)
            __instance.Invalidate();
    }

    /// <summary>Redraws the window an overlay belongs to.</summary>
    internal static void RequestRedraw(IWindow? window)
    {
        var app = LinuxApplication.Current;
        if (app == null)
            return;
        foreach (var context in app.WindowContexts)
        {
            if (window == null || ReferenceEquals(context.MauiWindow, window))
                context.RenderingEngine?.InvalidateAll();
        }
        LinuxApplication.RequestRedraw();
    }

    // Parameter names match VisualDiagnosticsOverlay.AddScrollableElementHandler(IScrollView scrollBar).
    private static bool AddScrollableElementHandler_Prefix(VisualDiagnosticsOverlay __instance, IScrollView scrollBar)
    {
        if (scrollBar?.Handler?.PlatformView is not SkiaScrollView view)
            return false;
        if (s_scrollViews?.GetValue(__instance) is not Dictionary<IScrollView, object> scrollViews || !scrollViews.TryAdd(scrollBar, view))
            return false;
        var overlay = __instance;
        EventHandler<ScrolledEventArgs> handler = (_, _) => overlay.Invalidate();
        view.Scrolled += handler;
        s_scrollSubscriptions.GetOrCreateValue(__instance).Add((view, handler));
        return false;
    }

    private static bool RemoveScrollableElementHandler_Prefix(VisualDiagnosticsOverlay __instance)
    {
        if (s_scrollSubscriptions.TryGetValue(__instance, out var subscriptions))
        {
            foreach (var (view, handler) in subscriptions)
                view.Scrolled -= handler;
            subscriptions.Clear();
        }
        (s_scrollViews?.GetValue(__instance) as Dictionary<IScrollView, object>)?.Clear();
        return false;
    }

    private static bool GetViewTransform_Prefix(IView view, ref Matrix4x4 __result)
    {
        __result = GetViewTransform(view);
        return false;
    }

    private static bool GetBoundingBox_Prefix(IView view, ref Rect __result)
    {
        __result = GetBoundingBox(view);
        return false;
    }

    private static bool GetHostedWindow_Prefix(IView? view, ref IWindow? __result)
    {
        __result = GetHostedWindow(view);
        return false;
    }

    // Replaces RectangleAdorner.UpdateModel: MAUI's body with the Skia view's geometry.
    private static bool UpdateModel_Prefix(RectangleAdorner __instance)
    {
        var box = GetBoundingBox(__instance.VisualView);
        box = new Rect(box.X + __instance.Offset.X, box.Y + __instance.Offset.Y, box.Width, box.Height);
        var margin = __instance.VisualView?.Margin ?? default;
        var model = s_adornerModel!.GetValue(__instance);
        s_modelUpdate!.Invoke(model, new object[] { box, margin, GetViewTransform(__instance.VisualView!), (double)__instance.Density });
        return false;
    }

    /// <summary>
    /// A view's bounds in its window (the coordinates overlays draw in): its Skia view's bounds
    /// through every render transform and scroll offset above it, as Windows maps an element's
    /// bounds to its root; the view's Frame when it has no Skia view.
    /// </summary>
    internal static Rect GetBoundingBox(IView? view)
    {
        if (view?.Handler?.PlatformView is not SkiaView skia)
            return view?.Frame ?? Rect.Zero;
        var b = skia.Bounds;
        var mapped = skia.ToWindowMatrix().MapRect(new SKRect((float)b.Left, (float)b.Top, (float)b.Right, (float)b.Bottom));
        return new Rect(mapped.Left, mapped.Top, mapped.Width, mapped.Height);
    }

    /// <summary>
    /// The transform from a view's own space (its top-left at the origin) to its window, as
    /// Windows' TransformToVisual(root): identity scale and rotation for an untransformed view.
    /// </summary>
    internal static Matrix4x4 GetViewTransform(IView? view)
    {
        if (view?.Handler?.PlatformView is not SkiaView skia)
            return Matrix4x4.Identity;
        var m = skia.ToWindowMatrix().PreConcat(SKMatrix.CreateTranslation((float)skia.Bounds.Left, (float)skia.Bounds.Top));
        return new Matrix4x4(
            m.ScaleX, m.SkewY, 0, 0,
            m.SkewX, m.ScaleY, 0, 0,
            0, 0, 1, 0,
            m.TransX, m.TransY, 0, 1);
    }

    /// <summary>The MAUI window whose tree shows <paramref name="view"/>, null when none does.</summary>
    internal static IWindow? GetHostedWindow(IView? view) =>
        view?.Handler?.PlatformView is SkiaView skia ? WindowContext.ForView(skia)?.MauiWindow : null;

    /// <summary>
    /// Raises <paramref name="overlay"/>'s Tapped for a tap at <paramref name="point"/>, with the
    /// elements MAUI's OnTappedInternal finds there.
    /// </summary>
    internal static void RaiseTapped(WindowOverlay overlay, Point point)
    {
        try
        {
            s_onTappedInternal ??= typeof(WindowOverlay).GetMethod("OnTappedInternal",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(Point) }, null);
            s_onTappedInternal?.Invoke(overlay, new object[] { point });
        }
        catch (TargetInvocationException ex)
        {
            DiagnosticLog.Error("WindowOverlayPatches", "A window overlay's Tapped handler threw", ex.InnerException ?? ex);
        }
    }
}
