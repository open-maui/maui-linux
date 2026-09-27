// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// The live Syncfusion platform views, repainted after input in case the
/// control reacted to it. Everything else repaints on the control's own
/// request (InvalidateDrawable, see SfInvalidationPatches).
/// </summary>
internal static class SfInvalidation
{
    private static readonly List<WeakReference<SkiaView>> s_views = new();

    internal static int Count
    {
        get
        {
            Prune();
            return s_views.Count;
        }
    }

    internal static void Track(SkiaView view)
    {
        if (s_views.Count % 256 == 255)
            Prune();
        s_views.Add(new WeakReference<SkiaView>(view));
    }

    internal static void Untrack(SkiaView view) =>
        s_views.RemoveAll(w => !w.TryGetTarget(out var v) || ReferenceEquals(v, view));

    /// <summary>
    /// Repaints the tracked views; with <paramref name="drawingOnly"/> only
    /// those whose control paints something itself (layout-only SfViews
    /// repaint through their children).
    /// </summary>
    internal static void InvalidateAll(bool drawingOnly)
    {
        foreach (var weak in s_views.ToArray())
        {
            if (!weak.TryGetTarget(out var view) || !view.IsVisible)
                continue;
            if (drawingOnly && view is SkiaSfLayout { Draws: false })
                continue;
            view.Invalidate();
        }
    }

    private static void Prune() => s_views.RemoveAll(w => !w.TryGetTarget(out _));
}
