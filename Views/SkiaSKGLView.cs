// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace Microsoft.Maui.Platform;

/// <summary>
/// SkiaView that hosts an SKGLView by providing a software-rendered fallback surface.
/// On platforms with GPU-accelerated SkiaSharp, SKGLView uses an OpenGL backend.
/// On Linux via OpenMaui, we render to a CPU-backed SKSurface and fire the
/// PaintSurface event with a synthetic GRContext, allowing GL-targeting controls
/// to fall back gracefully to software rendering.
/// </summary>
public class SkiaSKGLView : SkiaView
{
    private ISKGLView? _glView;
    private SKSurface? _cachedSurface;
    private SKImageInfo _cachedInfo;
    private GRContext? _grContext;

    /// <summary>
    /// Sets the ISKGLView virtual view that will receive PaintSurface callbacks.
    /// </summary>
    public ISKGLView? GLView
    {
        get => _glView;
        set
        {
            _glView = value;
            InvalidateCachedSurface();
            Invalidate();
        }
    }

    /// <summary>
    /// Whether the view should continuously redraw (render loop mode).
    /// </summary>
    public bool HasRenderLoop { get; set; }

    /// <summary>
    /// Called by the handler when InvalidateSurface() is requested.
    /// </summary>
    public void InvalidateCanvas()
    {
        InvalidateCachedSurface();
        Invalidate();
    }

    private void InvalidateCachedSurface()
    {
        _cachedSurface?.Dispose();
        _cachedSurface = null;
    }

    private GRContext GetOrCreateGRContext()
    {
        if (_grContext == null)
        {
            // Create a CPU-backed GR context as a fallback
            // This allows GL-targeting code to function without actual GPU
            var glInterface = GRGlInterface.Create();
            if (glInterface != null)
            {
                _grContext = GRContext.CreateGl(glInterface);
            }
        }
        return _grContext!;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = WidthRequest >= 0 ? WidthRequest : availableSize.Width;
        var h = HeightRequest >= 0 ? HeightRequest : availableSize.Height;

        if (double.IsInfinity(w) || double.IsNaN(w)) w = 300;
        if (double.IsInfinity(h) || double.IsNaN(h)) h = 200;

        return new Size(w, h);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        if (_glView == null) return;

        int width = Math.Max(1, (int)bounds.Width);
        int height = Math.Max(1, (int)bounds.Height);

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

        // Reuse surface if dimensions match
        if (_cachedSurface == null || _cachedInfo.Width != width || _cachedInfo.Height != height)
        {
            _cachedSurface?.Dispose();
            _cachedSurface = SKSurface.Create(info);
            _cachedInfo = info;

            if (_cachedSurface == null)
            {
                DiagnosticLog.Error("SkiaSKGLView", $"Failed to create SKSurface ({width}x{height})");
                return;
            }

            _glView.OnCanvasSizeChanged(new SKSizeI(width, height));
        }

        _cachedSurface.Canvas.Clear(SKColors.Transparent);

        // Fire PaintSurface with a software-backed event args
        try
        {
            var args = new SKPaintGLSurfaceEventArgs(_cachedSurface, new GRBackendRenderTarget(width, height, 0, 0, new GRGlFramebufferInfo(0, 0)));
            _glView.OnPaintSurface(args);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaSKGLView", $"PaintSurface failed: {ex.Message}");
            return;
        }

        _cachedSurface.Canvas.Flush();
        canvas.DrawSurface(_cachedSurface, bounds.Left, bounds.Top);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            InvalidateCachedSurface();
            _grContext?.Dispose();
            _grContext = null;
        }
        base.Dispose(disposing);
    }
}
