// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>
/// CPU raster target: draws into an <see cref="SKBitmap"/> and hands the pixels
/// to <see cref="IDisplayWindow.Present"/>, which does the backend-specific
/// submission (wl_shm copy + commit on Wayland, XPutImage on X11). This is the
/// historical pipeline and the fallback whenever a GPU target cannot be created.
/// </summary>
public sealed class RasterRenderTarget : IRenderTarget
{
    private readonly IDisplayWindow _window;
    private SKBitmap? _bitmap;
    private SKCanvas? _canvas;
    private SKImageInfo _imageInfo;
    private bool _disposed;

    public string Name => "raster";
    public bool IsGpuAccelerated => false;
    public bool PreservesContents => true;
    public int Width => _imageInfo.Width;
    public int Height => _imageInfo.Height;

    public RasterRenderTarget(IDisplayWindow window)
    {
        _window = window;
        Resize(window.Width, window.Height);
    }

    public void Resize(int width, int height)
    {
        var oldBitmap = _bitmap;
        _canvas?.Dispose();

        _imageInfo = new SKImageInfo(
            Math.Max(1, width),
            Math.Max(1, height),
            SKColorType.Bgra8888,
            SKAlphaType.Premul);

        _bitmap = new SKBitmap(_imageInfo);
        _canvas = new SKCanvas(_bitmap);

        // Copy old content to the new bitmap to avoid a blank flash during resize
        if (oldBitmap != null)
        {
            _canvas.DrawBitmap(oldBitmap, 0, 0);
            oldBitmap.Dispose();
        }
    }

    public SKCanvas? BeginFrame() => _canvas;

    public void EndFrame()
    {
        if (_bitmap == null || _canvas == null) return;

        _canvas.Flush();
        var pixels = _bitmap.GetPixels();
        if (pixels == IntPtr.Zero) return;

        _window.Present(pixels, _imageInfo.Width, _imageInfo.Height, _imageInfo.RowBytes);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _canvas?.Dispose();
        _bitmap?.Dispose();
        _canvas = null;
        _bitmap = null;
    }
}
