// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Window;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Services;
using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>
/// Manages Skia rendering to an X11 window with dirty region optimization.
/// </summary>
public class SkiaRenderingEngine : IDisposable
{
    private readonly X11Window _window;
    private SKBitmap? _bitmap;
    private SKBitmap? _backBuffer;
    private SKCanvas? _canvas;
    private SKImageInfo _imageInfo;
    private bool _disposed;
    private bool _fullRedrawNeeded = true;

    // Dirty region tracking for optimized rendering
    private readonly List<SKRect> _dirtyRegions = new();
    private readonly object _dirtyLock = new();
    /// <summary>
    /// Maximum number of dirty regions to track before falling back to a full redraw.
    /// </summary>
    public static int MaxDirtyRegions { get; set; } = 32;

    /// <summary>
    /// Overlap ratio threshold (0.0-1.0) at which adjacent dirty regions are merged.
    /// </summary>
    public static float RegionMergeThreshold { get; set; } = 0.3f;

    public static SkiaRenderingEngine? Current { get; private set; }
    public ResourceCache ResourceCache { get; }
    public int Width => _imageInfo.Width;
    public int Height => _imageInfo.Height;

    /// <summary>
    /// Gets or sets whether dirty region optimization is enabled.
    /// When disabled, full redraws occur (useful for debugging).
    /// </summary>
    public bool EnableDirtyRegionOptimization { get; set; } = true;

    /// <summary>
    /// Gets the number of dirty regions in the current frame.
    /// </summary>
    public int DirtyRegionCount
    {
        get { lock (_dirtyLock) return _dirtyRegions.Count; }
    }

    public SkiaRenderingEngine(X11Window window)
    {
        _window = window;
        ResourceCache = new ResourceCache();
        Current = this;

        CreateSurface(window.Width, window.Height);

        _window.Resized += OnWindowResized;
        _window.Exposed += OnWindowExposed;
    }

    private void CreateSurface(int width, int height)
    {
        _bitmap?.Dispose();
        _backBuffer?.Dispose();
        _canvas?.Dispose();

        _imageInfo = new SKImageInfo(
            Math.Max(1, width),
            Math.Max(1, height),
            SKColorType.Bgra8888,
            SKAlphaType.Premul);

        _bitmap = new SKBitmap(_imageInfo);
        _backBuffer = new SKBitmap(_imageInfo);
        _canvas = new SKCanvas(_bitmap);
        _fullRedrawNeeded = true;

        lock (_dirtyLock)
        {
            _dirtyRegions.Clear();
        }
    }

    private void OnWindowResized(object? sender, (int Width, int Height) size)
    {
        CreateSurface(size.Width, size.Height);
    }

    private void OnWindowExposed(object? sender, EventArgs e)
    {
        _fullRedrawNeeded = true;
    }

    /// <summary>
    /// Marks the entire surface as needing redraw.
    /// </summary>
    public void InvalidateAll()
    {
        _fullRedrawNeeded = true;
    }

    /// <summary>
    /// Marks a specific region as needing redraw.
    /// Multiple regions are tracked and merged for efficiency.
    /// </summary>
    public void InvalidateRegion(SKRect region)
    {
        if (region.IsEmpty || region.Width <= 0 || region.Height <= 0)
            return;

        // Clamp to surface bounds
        region = SKRect.Intersect(region, new SKRect(0, 0, Width, Height));
        if (region.IsEmpty)
            return;

        lock (_dirtyLock)
        {
            // If we have too many regions, just do a full redraw
            if (_dirtyRegions.Count >= MaxDirtyRegions)
            {
                _fullRedrawNeeded = true;
                _dirtyRegions.Clear();
                return;
            }

            // Try to merge with existing regions
            for (int i = 0; i < _dirtyRegions.Count; i++)
            {
                var existing = _dirtyRegions[i];
                if (ShouldMergeRegions(existing, region))
                {
                    _dirtyRegions[i] = SKRect.Union(existing, region);
                    return;
                }
            }

            _dirtyRegions.Add(region);
        }
    }

    private bool ShouldMergeRegions(SKRect a, SKRect b)
    {
        // Check if regions overlap
        var intersection = SKRect.Intersect(a, b);
        if (intersection.IsEmpty)
        {
            // Check if they're adjacent (within a few pixels)
            var expanded = new SKRect(a.Left - 4, a.Top - 4, a.Right + 4, a.Bottom + 4);
            return expanded.IntersectsWith(b);
        }

        // Merge if intersection is significant relative to either region
        var intersectionArea = intersection.Width * intersection.Height;
        var aArea = a.Width * a.Height;
        var bArea = b.Width * b.Height;
        var minArea = Math.Min(aArea, bArea);

        return intersectionArea / minArea >= RegionMergeThreshold;
    }

    /// <summary>
    /// Renders the view tree, optionally using dirty region optimization.
    /// </summary>
    public void Render(SkiaView rootView)
    {
        if (_canvas == null || _bitmap == null)
            return;

        // Measure and arrange
        var availableSize = new Size(Width, Height);
        try
        {
            rootView.Measure(availableSize);
            rootView.Arrange(new Rect(0, 0, Width, Height));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaRenderingEngine", "Exception during Measure/Arrange", ex);
            return;
        }

        // Determine what to redraw
        List<SKRect> regionsToRedraw;
        bool isFullRedraw = _fullRedrawNeeded || !EnableDirtyRegionOptimization;

        lock (_dirtyLock)
        {
            if (isFullRedraw)
            {
                regionsToRedraw = new List<SKRect> { new SKRect(0, 0, Width, Height) };
                _dirtyRegions.Clear();
                _fullRedrawNeeded = false;
            }
            else if (_dirtyRegions.Count == 0)
            {
                // Nothing to redraw
                return;
            }
            else
            {
                regionsToRedraw = MergeOverlappingRegions(_dirtyRegions.ToList());
                _dirtyRegions.Clear();
            }
        }

        // Render dirty regions
        foreach (var region in regionsToRedraw)
        {
            try
            {
                RenderRegion(rootView, region, isFullRedraw);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("SkiaRenderingEngine", $"Exception rendering region {region}", ex);
            }
        }

        // Draw popup overlays (always on top, full redraw)
        try
        {
            SkiaView.DrawPopupOverlays(_canvas);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaRenderingEngine", "Exception drawing popup overlays", ex);
        }

        // Draw modal dialogs and context menus on top of everything
        try
        {
            if (LinuxDialogService.HasActiveDialog || LinuxDialogService.HasContextMenu)
            {
                LinuxDialogService.DrawDialogs(_canvas, new SKRect(0, 0, Width, Height));
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaRenderingEngine", "Exception drawing dialogs", ex);
        }

        _canvas.Flush();

        // Present to X11 window
        PresentToWindow();
    }

    private void RenderRegion(SkiaView rootView, SKRect region, bool isFullRedraw)
    {
        if (_canvas == null) return;

        _canvas.Save();

        if (!isFullRedraw)
        {
            // Clip to dirty region for partial updates
            _canvas.ClipRect(region);
        }

        // Clear the region
        using var clearPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
        _canvas.DrawRect(region, clearPaint);

        // Draw the view tree (views will naturally clip to their bounds)
        try
        {
            rootView.Draw(_canvas);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaRenderingEngine", "Exception during view Draw", ex);
        }

        _canvas.Restore();
    }

    private List<SKRect> MergeOverlappingRegions(List<SKRect> regions)
    {
        if (regions.Count <= 1)
            return regions;

        var merged = new List<SKRect>();
        var used = new bool[regions.Count];

        for (int i = 0; i < regions.Count; i++)
        {
            if (used[i]) continue;

            var current = regions[i];
            used[i] = true;

            // Keep merging until no more merges possible
            bool didMerge;
            do
            {
                didMerge = false;
                for (int j = i + 1; j < regions.Count; j++)
                {
                    if (used[j]) continue;

                    if (ShouldMergeRegions(current, regions[j]))
                    {
                        current = SKRect.Union(current, regions[j]);
                        used[j] = true;
                        didMerge = true;
                    }
                }
            } while (didMerge);

            merged.Add(current);
        }

        return merged;
    }

    private void PresentToWindow()
    {
        if (_bitmap == null) return;

        var pixels = _bitmap.GetPixels();
        if (pixels == IntPtr.Zero) return;

        _window.DrawPixels(pixels, _imageInfo.Width, _imageInfo.Height, _imageInfo.RowBytes);
    }

    public SKCanvas? GetCanvas() => _canvas;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _window.Resized -= OnWindowResized;
                _window.Exposed -= OnWindowExposed;
                _canvas?.Dispose();
                _bitmap?.Dispose();
                _backBuffer?.Dispose();
                ResourceCache.Dispose();
                if (Current == this) Current = null;
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
