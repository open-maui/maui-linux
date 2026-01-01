// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Window;
using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>
/// GPU-accelerated rendering engine using OpenGL.
/// Falls back to software rendering if GPU initialization fails.
/// </summary>
public class GpuRenderingEngine : IDisposable
{
    private readonly X11Window _window;
    private GRContext? _grContext;
    private GRBackendRenderTarget? _renderTarget;
    private SKSurface? _surface;
    private SKCanvas? _canvas;
    private bool _disposed;
    private bool _gpuAvailable;
    private int _width;
    private int _height;

    // Fallback to software rendering
    private SKBitmap? _softwareBitmap;
    private SKCanvas? _softwareCanvas;

    // Dirty region tracking
    private readonly List<SKRect> _dirtyRegions = new();
    private readonly object _dirtyLock = new();
    private bool _fullRedrawNeeded = true;
    private const int MaxDirtyRegions = 32;

    /// <summary>
    /// Gets whether GPU acceleration is available and active.
    /// </summary>
    public bool IsGpuAccelerated => _gpuAvailable && _grContext != null;

    /// <summary>
    /// Gets the current rendering backend name.
    /// </summary>
    public string BackendName => IsGpuAccelerated ? "OpenGL" : "Software";

    public int Width => _width;
    public int Height => _height;

    public GpuRenderingEngine(X11Window window)
    {
        _window = window;
        _width = window.Width;
        _height = window.Height;

        // Try to initialize GPU rendering
        _gpuAvailable = TryInitializeGpu();

        if (!_gpuAvailable)
        {
            Console.WriteLine("[GpuRenderingEngine] GPU not available, using software rendering");
            InitializeSoftwareRendering();
        }

        _window.Resized += OnWindowResized;
        _window.Exposed += OnWindowExposed;
    }

    private bool TryInitializeGpu()
    {
        try
        {
            // Check if we can create an OpenGL context
            var glInterface = GRGlInterface.Create();
            if (glInterface == null)
            {
                Console.WriteLine("[GpuRenderingEngine] Failed to create GL interface");
                return false;
            }

            _grContext = GRContext.CreateGl(glInterface);
            if (_grContext == null)
            {
                Console.WriteLine("[GpuRenderingEngine] Failed to create GR context");
                glInterface.Dispose();
                return false;
            }

            CreateGpuSurface();
            Console.WriteLine("[GpuRenderingEngine] GPU acceleration enabled");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GpuRenderingEngine] GPU initialization failed: {ex.Message}");
            return false;
        }
    }

    private void CreateGpuSurface()
    {
        if (_grContext == null) return;

        _renderTarget?.Dispose();
        _surface?.Dispose();

        var width = Math.Max(1, _width);
        var height = Math.Max(1, _height);

        // Create framebuffer info (assuming default framebuffer 0)
        var framebufferInfo = new GRGlFramebufferInfo(0, SKColorType.Rgba8888.ToGlSizedFormat());

        _renderTarget = new GRBackendRenderTarget(
            width, height,
            0,  // sample count
            8,  // stencil bits
            framebufferInfo);

        _surface = SKSurface.Create(
            _grContext,
            _renderTarget,
            GRSurfaceOrigin.BottomLeft,
            SKColorType.Rgba8888);

        if (_surface == null)
        {
            Console.WriteLine("[GpuRenderingEngine] Failed to create GPU surface, falling back to software");
            _gpuAvailable = false;
            InitializeSoftwareRendering();
            return;
        }

        _canvas = _surface.Canvas;
    }

    private void InitializeSoftwareRendering()
    {
        var width = Math.Max(1, _width);
        var height = Math.Max(1, _height);

        _softwareBitmap?.Dispose();
        _softwareCanvas?.Dispose();

        var imageInfo = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        _softwareBitmap = new SKBitmap(imageInfo);
        _softwareCanvas = new SKCanvas(_softwareBitmap);
        _canvas = _softwareCanvas;
    }

    private void OnWindowResized(object? sender, (int Width, int Height) size)
    {
        _width = size.Width;
        _height = size.Height;

        if (_gpuAvailable && _grContext != null)
        {
            CreateGpuSurface();
        }
        else
        {
            InitializeSoftwareRendering();
        }

        _fullRedrawNeeded = true;
    }

    private void OnWindowExposed(object? sender, EventArgs e)
    {
        _fullRedrawNeeded = true;
    }

    /// <summary>
    /// Marks a region as needing redraw.
    /// </summary>
    public void InvalidateRegion(SKRect region)
    {
        if (region.IsEmpty || region.Width <= 0 || region.Height <= 0)
            return;

        region = SKRect.Intersect(region, new SKRect(0, 0, Width, Height));
        if (region.IsEmpty) return;

        lock (_dirtyLock)
        {
            if (_dirtyRegions.Count >= MaxDirtyRegions)
            {
                _fullRedrawNeeded = true;
                _dirtyRegions.Clear();
                return;
            }
            _dirtyRegions.Add(region);
        }
    }

    /// <summary>
    /// Marks the entire surface as needing redraw.
    /// </summary>
    public void InvalidateAll()
    {
        _fullRedrawNeeded = true;
    }

    /// <summary>
    /// Renders the view tree with dirty region optimization.
    /// </summary>
    public void Render(SkiaView rootView)
    {
        if (_canvas == null) return;

        // Measure and arrange
        var availableSize = new SKSize(Width, Height);
        rootView.Measure(availableSize);
        rootView.Arrange(new SKRect(0, 0, Width, Height));

        // Determine regions to redraw
        List<SKRect> regionsToRedraw;
        bool isFullRedraw;

        lock (_dirtyLock)
        {
            isFullRedraw = _fullRedrawNeeded || _dirtyRegions.Count == 0;
            if (isFullRedraw)
            {
                regionsToRedraw = new List<SKRect> { new SKRect(0, 0, Width, Height) };
                _dirtyRegions.Clear();
                _fullRedrawNeeded = false;
            }
            else
            {
                regionsToRedraw = new List<SKRect>(_dirtyRegions);
                _dirtyRegions.Clear();
            }
        }

        // Render each dirty region
        foreach (var region in regionsToRedraw)
        {
            _canvas.Save();
            if (!isFullRedraw)
            {
                _canvas.ClipRect(region);
            }

            // Clear region
            _canvas.Clear(SKColors.White);

            // Draw view tree
            rootView.Draw(_canvas);

            _canvas.Restore();
        }

        // Draw popup overlays
        SkiaView.DrawPopupOverlays(_canvas);

        // Draw modal dialogs
        if (LinuxDialogService.HasActiveDialog)
        {
            LinuxDialogService.DrawDialogs(_canvas, new SKRect(0, 0, Width, Height));
        }

        _canvas.Flush();

        // Present to window
        if (_gpuAvailable && _grContext != null)
        {
            _grContext.Submit();
            // Swap buffers would happen here via GLX/EGL
        }
        else if (_softwareBitmap != null)
        {
            var pixels = _softwareBitmap.GetPixels();
            if (pixels != IntPtr.Zero)
            {
                _window.DrawPixels(pixels, Width, Height, _softwareBitmap.RowBytes);
            }
        }
    }

    /// <summary>
    /// Gets performance statistics for the GPU context.
    /// </summary>
    public GpuStats GetStats()
    {
        if (_grContext == null)
        {
            return new GpuStats { IsGpuAccelerated = false };
        }

        // Get resource cache limits from GRContext
        _grContext.GetResourceCacheLimits(out var maxResources, out var maxBytes);

        return new GpuStats
        {
            IsGpuAccelerated = true,
            MaxTextureSize = 4096, // Common default, SkiaSharp doesn't expose this directly
            ResourceCacheUsedBytes = 0, // Would need to track manually
            ResourceCacheLimitBytes = maxBytes
        };
    }

    /// <summary>
    /// Purges unused GPU resources to free memory.
    /// </summary>
    public void PurgeResources()
    {
        _grContext?.PurgeResources();
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

                _surface?.Dispose();
                _renderTarget?.Dispose();
                _grContext?.Dispose();
                _softwareBitmap?.Dispose();
                _softwareCanvas?.Dispose();
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
