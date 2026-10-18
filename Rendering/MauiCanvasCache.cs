// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics.Skia;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>
/// One MAUI <see cref="SkiaCanvas"/> per thread, pointed at the canvas of the frame being drawn.
/// Building a SkiaCanvas builds its state service, whose default paints and font are made
/// on first use (SkiaCanvasStateService.EnsureDefaults); doing that for every shape, BoxView,
/// GraphicsView or DrawingView on every frame made a page with many of them redraw (and
/// resize) slowly. Pointing the existing canvas at a new SKCanvas only resets its state.
/// A drawable that draws while another is drawing on this thread gets a canvas of its own.
/// </summary>
internal static class MauiCanvasCache
{
    [ThreadStatic]
    private static SkiaCanvas? t_canvas;

    [ThreadStatic]
    private static bool t_inUse;

    /// <summary>A MAUI canvas drawing on <paramref name="canvas"/>, in its default state, until the lease is disposed.</summary>
    internal static Lease Rent(SKCanvas canvas)
    {
        if (t_inUse)
            return new Lease(new SkiaCanvas { Canvas = canvas }, owned: true);
        t_inUse = true;
        var mauiCanvas = t_canvas ??= new SkiaCanvas();
        mauiCanvas.Canvas = canvas; // resets the canvas state
        return new Lease(mauiCanvas, owned: false);
    }

    internal readonly struct Lease : IDisposable
    {
        private readonly bool _owned;

        internal Lease(SkiaCanvas canvas, bool owned)
        {
            Canvas = canvas;
            _owned = owned;
        }

        internal SkiaCanvas Canvas { get; }

        public void Dispose()
        {
            if (_owned)
            {
                Canvas.Dispose();
                return;
            }
            // Drop the frame's SKCanvas so the cached wrapper never outlives it.
            Canvas.Canvas = null!;
            t_inUse = false;
        }
    }
}
