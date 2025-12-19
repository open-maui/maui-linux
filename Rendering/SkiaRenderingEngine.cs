// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Window;
using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>
/// Manages Skia rendering to an X11 window.
/// </summary>
public class SkiaRenderingEngine : IDisposable
{
    private readonly X11Window _window;
    private SKBitmap? _bitmap;
    private SKCanvas? _canvas;
    private SKImageInfo _imageInfo;
    private bool _disposed;
    private bool _fullRedrawNeeded = true;

    public static SkiaRenderingEngine? Current { get; private set; }
    public ResourceCache ResourceCache { get; }
    public int Width => _imageInfo.Width;
    public int Height => _imageInfo.Height;

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
        _canvas?.Dispose();

        _imageInfo = new SKImageInfo(
            Math.Max(1, width),
            Math.Max(1, height),
            SKColorType.Bgra8888,
            SKAlphaType.Premul);

        _bitmap = new SKBitmap(_imageInfo);
        _canvas = new SKCanvas(_bitmap);
        _fullRedrawNeeded = true;
        
    }

    private void OnWindowResized(object? sender, (int Width, int Height) size)
    {
        CreateSurface(size.Width, size.Height);
    }

    private void OnWindowExposed(object? sender, EventArgs e)
    {
        _fullRedrawNeeded = true;
    }

    public void InvalidateAll()
    {
        _fullRedrawNeeded = true;
    }

    public void Render(SkiaView rootView)
    {
        if (_canvas == null || _bitmap == null)
            return;

        _canvas.Clear(SKColors.White);
        
        // Measure first, then arrange
        var availableSize = new SKSize(Width, Height);
        rootView.Measure(availableSize);
        
        rootView.Arrange(new SKRect(0, 0, Width, Height));
        
        // Draw the view tree
        rootView.Draw(_canvas);
        
        // Draw popup overlays (dropdowns, calendars, etc.) on top
        SkiaView.DrawPopupOverlays(_canvas);
        
        _canvas.Flush();

        // Present to X11 window
        PresentToWindow();
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

public class ResourceCache : IDisposable
{
    private readonly Dictionary<string, SKTypeface> _typefaces = new();
    private bool _disposed;

    public SKTypeface GetTypeface(string fontFamily, SKFontStyle style)
    {
        var key = $"{fontFamily}_{style.Weight}_{style.Width}_{style.Slant}";
        if (!_typefaces.TryGetValue(key, out var typeface))
        {
            typeface = SKTypeface.FromFamilyName(fontFamily, style) ?? SKTypeface.Default;
            _typefaces[key] = typeface;
        }
        return typeface;
    }

    public void Clear()
    {
        foreach (var tf in _typefaces.Values) tf.Dispose();
        _typefaces.Clear();
    }

    public void Dispose()
    {
        if (!_disposed) { Clear(); _disposed = true; }
    }
}
