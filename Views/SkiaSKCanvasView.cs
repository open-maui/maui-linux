// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace Microsoft.Maui.Platform;

/// <summary>
/// SkiaView that hosts an SKCanvasView by providing the native rendering surface.
/// When OnDraw is called, creates an offscreen SKSurface and fires the SKCanvasView's
/// PaintSurface event, then composites the result onto our canvas.
/// This enables all SkiaSharp-based MAUI controls (LiveCharts, custom drawings, etc.)
/// to work on Linux without platform-specific native hosts.
/// </summary>
public class SkiaSKCanvasView : SkiaView
{
    private ISKCanvasView? _canvasView;
    private SKSurface? _cachedSurface;
    private SKImageInfo _cachedInfo;

    /// <summary>
    /// Sets the ISKCanvasView virtual view that will receive PaintSurface callbacks.
    /// </summary>
    public ISKCanvasView? CanvasView
    {
        get => _canvasView;
        set
        {
            _canvasView = value;
            InvalidateCachedSurface();
            Invalidate();
        }
    }

    private bool _invalidateLogged;

    /// <summary>
    /// Called by the handler when InvalidateSurface() is requested.
    /// </summary>
    public void InvalidateCanvas()
    {
        if (!_invalidateLogged)
        {
            _invalidateLogged = true;
            DiagnosticLog.Error("SkiaSKCanvasView", $"InvalidateCanvas called, hasCanvasView={_canvasView != null}");
        }
        InvalidateCachedSurface();
        Invalidate();
    }

    private void InvalidateCachedSurface()
    {
        _cachedSurface?.Dispose();
        _cachedSurface = null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = WidthRequest >= 0 ? WidthRequest : availableSize.Width;
        var h = HeightRequest >= 0 ? HeightRequest : availableSize.Height;

        // Don't return infinity
        if (double.IsInfinity(w) || double.IsNaN(w)) w = 300;
        if (double.IsInfinity(h) || double.IsNaN(h)) h = 200;

        return new Size(w, h);
    }

    private int _drawCount;

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        if (_canvasView == null)
        {
            return;
        }

        int width = Math.Max(1, (int)bounds.Width);
        int height = Math.Max(1, (int)bounds.Height);

        _drawCount++;
        if (_drawCount <= 5 || _drawCount % 100 == 0)
        {
            DiagnosticLog.Error("SkiaSKCanvasView", $"OnDraw #{_drawCount}: bounds={bounds.Width}x{bounds.Height}, canvasView={_canvasView.GetType().Name}");
        }

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

        // Reuse surface if dimensions match
        if (_cachedSurface == null || _cachedInfo.Width != width || _cachedInfo.Height != height)
        {
            _cachedSurface?.Dispose();
            _cachedSurface = SKSurface.Create(info);
            _cachedInfo = info;

            if (_cachedSurface == null)
            {
                DiagnosticLog.Error("SkiaSKCanvasView", $"Failed to create SKSurface ({width}x{height})");
                return;
            }

            // Notify the canvas view of the size change
            _canvasView.OnCanvasSizeChanged(new SKSizeI(width, height));
        }

        // Clear the offscreen surface
        _cachedSurface.Canvas.Clear(SKColors.Transparent);

        // Fire the PaintSurface event on the SKCanvasView
        try
        {
            var args = new SKPaintSurfaceEventArgs(_cachedSurface, info);

            if (_drawCount <= 3)
            {
                // Check if PaintSurface event has subscribers
                var canvasViewType = _canvasView.GetType();
                var paintField = canvasViewType.GetField("PaintSurface", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var paintEvent = paintField?.GetValue(_canvasView);
                DiagnosticLog.Error("SkiaSKCanvasView", $"OnDraw #{_drawCount}: PaintSurface field={paintField?.Name ?? "null"}, delegate={paintEvent?.GetType().Name ?? "null"}, IsLoaded={(_canvasView as Microsoft.Maui.Controls.VisualElement)?.IsLoaded}");

                // List all event-like fields
                foreach (var f in canvasViewType.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                {
                    if (f.FieldType.Name.Contains("EventHandler") || f.FieldType.Name.Contains("Action") || f.Name.Contains("Paint"))
                    {
                        var val = f.GetValue(_canvasView);
                        DiagnosticLog.Error("SkiaSKCanvasView", $"  Field: {f.Name} ({f.FieldType.Name}) = {(val != null ? "has value" : "null")}");
                    }
                }
            }

            _canvasView.OnPaintSurface(args);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaSKCanvasView", $"PaintSurface failed: {ex.Message}");
            return;
        }

        // Check if LiveCharts actually drew something
        _cachedSurface.Canvas.Flush();
        if (_drawCount <= 5)
        {
            using var snapshot = _cachedSurface.Snapshot();
            using var pixmap = snapshot.PeekPixels();
            if (pixmap != null)
            {
                var pixels = pixmap.GetPixelSpan();
                int nonTransparent = 0;
                for (int i = 3; i < Math.Min(pixels.Length, 4000); i += 4)
                {
                    if (pixels[i] > 0) nonTransparent++;
                }
                DiagnosticLog.Error("SkiaSKCanvasView", $"OnDraw #{_drawCount}: first 1000 pixels: {nonTransparent} non-transparent");
            }
        }

        // Composite the offscreen surface onto our canvas
        canvas.DrawSurface(_cachedSurface, bounds.Left, bounds.Top);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            InvalidateCachedSurface();
        }
        base.Dispose(disposing);
    }
}
