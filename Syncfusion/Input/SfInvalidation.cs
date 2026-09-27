// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// The live Syncfusion platform views, repainted when the control may have
/// changed what it draws: after input, and on every animation tick while
/// MAUI animations run (ripples, tab indicators, busy spinners). Syncfusion's
/// own invalidation goes to its handler type and never reaches these views.
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

    internal static void OnAnimationTick() => InvalidateAll(drawingOnly: true);

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
