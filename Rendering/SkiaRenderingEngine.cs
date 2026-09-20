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
/// Manages Skia rendering for one window with dirty region optimization. What
/// to draw (layout, regions, overlays, dialogs) is decided here; where the
/// pixels live and how a frame reaches the display is the <see cref="IRenderTarget"/>'s
/// business (CPU raster via the window's Present, or GPU via EGL).
/// </summary>
public class SkiaRenderingEngine : IDisposable, IRenderContext
{
    // IRenderContext.Resources alias: existing internals keep using ResourceCache
    // by name; new code (and views post-Stage-5) prefer the interface's Resources.
    ResourceCache IRenderContext.Resources => ResourceCache;
    void IRenderContext.Invalidate() => InvalidateAll();

    private readonly IDisplayWindow _window;
    private readonly IRenderTarget _target;
    private readonly FrameStatistics? _stats;
    private bool _disposed;
    private bool _fullRedrawNeeded = true;

    // Dirty region tracking for optimized rendering
    private readonly List<SKRect> _dirtyRegions = new();
    private readonly Lock _dirtyLock = new();
    /// <summary>
    /// Maximum number of dirty regions to track before falling back to a full redraw.
    /// </summary>
    public static int MaxDirtyRegions { get; set; } = 32;

    /// <summary>
    /// Overlap ratio threshold (0.0-1.0) at which adjacent dirty regions are merged.
    /// </summary>
    public static float RegionMergeThreshold { get; set; } = 0.3f;

    public ResourceCache ResourceCache { get; }
    public int Width => _target.Width;
    public int Height => _target.Height;

    /// <summary>The target this engine presents through (raster or GPU).</summary>
    public IRenderTarget RenderTarget => _target;

    /// <summary>
    /// DPI scale factor for HiDPI displays. Layout is performed at logical pixels
    /// (Width/DpiScale × Height/DpiScale) and the canvas is scaled up for rendering.
    /// </summary>
    public float DpiScale { get; set; } = 1.0f;

    /// <summary>
    /// Gets the logical width (physical width divided by DPI scale).
    /// </summary>
    public float LogicalWidth => Width / DpiScale;

    /// <summary>
    /// Gets the logical height (physical height divided by DPI scale).
    /// </summary>
    public float LogicalHeight => Height / DpiScale;

    /// <summary>
    /// Gets or sets whether dirty region optimization is enabled.
    /// When disabled, full redraws occur (useful for debugging).
    /// </summary>
    public bool EnableDirtyRegionOptimization { get; set; } = true;

    /// <summary>
    /// Multi-window: whether this engine draws the (app-modal) dialog and
    /// context-menu overlays. True by default so the single-window path is
    /// unchanged; with several windows live, LinuxApplication sets this true
    /// only on the dialog-host window's engine so a dialog appears once.
    /// </summary>
    public bool RendersDialogs { get; set; } = true;

    /// <summary>
    /// Multi-window: when set, only popup overlays owned by views under this
    /// root are drawn by this engine (a dropdown opened in window A must not
    /// paint into window B). Null (default, single-window) draws all popups —
    /// the historical behavior. Set per-frame by WindowContext.Render.
    /// </summary>
    public SkiaView? PopupFilterRoot { get; set; }

    /// <summary>
    /// Gets the number of dirty regions in the current frame.
    /// </summary>
    public int DirtyRegionCount
    {
        get { lock (_dirtyLock) return _dirtyRegions.Count; }
    }

    /// <summary>
    /// Creates an engine over the CPU raster target (the window's own Present
    /// path). Used by tests and by callers that do not go through
    /// <see cref="RenderTargetFactory"/>.
    /// </summary>
    public SkiaRenderingEngine(IDisplayWindow window)
        : this(window, new RasterRenderTarget(window))
    {
    }

    /// <summary>
    /// Creates an engine over an explicit render target. The engine owns the
    /// target and disposes it.
    /// </summary>
    public SkiaRenderingEngine(IDisplayWindow window, IRenderTarget target)
    {
        _window = window;
        _target = target;
        ResourceCache = new ResourceCache();
        if (FrameStatistics.Enabled)
            _stats = new FrameStatistics(target.Name);

        _target.Resize(window.Width, window.Height);
        _fullRedrawNeeded = true;

        _window.Resized += OnWindowResized;
        _window.Exposed += OnWindowExposed;
    }

    private void OnWindowResized(object? sender, (int Width, int Height) size)
    {
        _target.Resize(size.Width, size.Height);
        _fullRedrawNeeded = true;

        lock (_dirtyLock)
        {
            _dirtyRegions.Clear();
        }
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
        if (_disposed)
            return;

        // CSD reserves a titlebar strip at the top of the surface. Views see a
        // smaller window (height minus titlebar) and are drawn translated down
        // by the titlebar height inside RenderRegion. The titlebar itself is
        // drawn after the view passes, in the strip the view tree doesn't cover.
        bool csdActive = _window is Window.WaylandWindow waylandCsd && waylandCsd.UseCsd;
        float csdInsetLogical = csdActive ? Window.WaylandWindow.CsdTitlebarHeightLogical : 0f;

        // Measure and arrange at logical pixel dimensions
        var logicalWidth = (double)LogicalWidth;
        var logicalHeight = (double)LogicalHeight - csdInsetLogical;
        var availableSize = new Size(logicalWidth, logicalHeight);
        try
        {
            rootView.Measure(availableSize);
            rootView.Arrange(new Rect(0, 0, logicalWidth, logicalHeight));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaRenderingEngine", "Exception during Measure/Arrange", ex);
            return;
        }

        // Determine what to redraw. Nothing dirty means no frame at all, on
        // every target (a GPU swapchain simply keeps showing its last buffer).
        // When something is dirty, a target that does not keep the previous
        // frame (GPU) must be repainted fully; a raster target repaints only
        // the merged dirty regions.
        List<SKRect> regionsToRedraw;
        bool isFullRedraw;

        lock (_dirtyLock)
        {
            bool anythingDirty = _fullRedrawNeeded || !EnableDirtyRegionOptimization || _dirtyRegions.Count > 0;
            if (!anythingDirty)
                return;

            isFullRedraw = _fullRedrawNeeded || !EnableDirtyRegionOptimization || !_target.PreservesContents;
            if (isFullRedraw)
            {
                regionsToRedraw = new List<SKRect> { new SKRect(0, 0, Width, Height) };
                _dirtyRegions.Clear();
                _fullRedrawNeeded = false;
            }
            else
            {
                regionsToRedraw = MergeOverlappingRegions(_dirtyRegions.ToList());
                _dirtyRegions.Clear();
            }
        }

        _stats?.BeginFrame();
        var canvas = _target.BeginFrame();
        if (canvas == null)
            return;

        // Render dirty regions
        foreach (var region in regionsToRedraw)
        {
            try
            {
                RenderRegion(canvas, rootView, region, isFullRedraw, csdInsetLogical);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("SkiaRenderingEngine", $"Exception rendering region {region}", ex);
            }
        }

        // CSD titlebar: draw after view tree so it sits on top of any pixels
        // the view tree (or its background fill) may have leaked into the
        // titlebar strip. Drawn in logical coords matching the DpiScale matrix
        // used everywhere else in this method.
        if (csdActive && _window is Window.WaylandWindow waylandCsdDraw)
        {
            try
            {
                canvas.Save();
                if (DpiScale > 1.0f)
                    canvas.Scale(DpiScale);
                Window.WaylandCsdRenderer.DrawTitlebar(
                    canvas,
                    waylandCsdDraw,
                    LogicalWidth,
                    waylandCsdDraw.Title ?? string.Empty);
                canvas.Restore();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("SkiaRenderingEngine", "Exception drawing CSD titlebar", ex);
            }
        }

        // Draw popup overlays (always on top, full redraw). PopupFilterRoot is
        // null for single-window apps (draw everything, historical behavior);
        // in multi-window it restricts to popups owned by this window's tree.
        try
        {
            SkiaView.PopupDpiScale = DpiScale;
            SkiaView.DrawPopupOverlays(canvas, PopupFilterRoot);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaRenderingEngine", "Exception drawing popup overlays", ex);
        }

        // Draw modal dialogs and context menus on top of everything. The view
        // tree is laid out and drawn in *logical* coordinates with the canvas
        // matrix scaled to DpiScale; dialogs need the same treatment so their
        // button bounds (cached during Draw and consulted during click hit-test)
        // are in the same coordinate space as pointer events (which are
        // divided by DpiScale by ScalePointerArgs).
        try
        {
            if (RendersDialogs && (LinuxDialogService.HasActiveDialog || LinuxDialogService.HasContextMenu))
            {
                canvas.Save();
                if (DpiScale > 1.0f)
                    canvas.Scale(DpiScale);
                LinuxDialogService.DrawDialogs(canvas, new SKRect(0, 0, LogicalWidth, LogicalHeight));
                canvas.Restore();
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaRenderingEngine", "Exception drawing dialogs", ex);
        }

        // Flush and submit the frame (raster: copy to the window; GPU: swap).
        _target.EndFrame();
        _stats?.EndFrame();
    }

    private void RenderRegion(SKCanvas canvas, SkiaView rootView, SKRect region, bool isFullRedraw, float csdInsetLogical = 0f)
    {
        canvas.Save();

        if (!isFullRedraw)
        {
            // Clip to dirty region for partial updates
            canvas.ClipRect(region);
        }

        // Clear the region with transparent so PNG alpha and view backgrounds are preserved
        canvas.Save();
        canvas.ClipRect(region);
        canvas.Clear(SKColors.Transparent);
        canvas.Restore();

        // Apply DPI scaling so all drawing is proportionally larger on HiDPI displays
        if (DpiScale > 1.0f)
        {
            canvas.Scale(DpiScale);
        }

        // CSD: shift the view tree down by the titlebar inset (in logical px,
        // because we just scaled the canvas to DpiScale above so 1 unit == 1
        // logical pixel from here on). Done as a translate rather than a
        // resized clip so the views' own bounds math stays unchanged.
        if (csdInsetLogical > 0f)
            canvas.Translate(0f, csdInsetLogical);

        // Draw the view tree (views will naturally clip to their bounds)
        try
        {
            rootView.Draw(canvas);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaRenderingEngine", "Exception during view Draw", ex);
        }

        canvas.Restore();
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

    /// <summary>
    /// The target's current canvas, if a frame is in progress or the target is
    /// a raster one that keeps its canvas between frames. Diagnostic use only.
    /// </summary>
    public SKCanvas? GetCanvas() => _target is RasterRenderTarget ? _target.BeginFrame() : null;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _window.Resized -= OnWindowResized;
                _window.Exposed -= OnWindowExposed;
                _target.Dispose();
                ResourceCache.Dispose();
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
