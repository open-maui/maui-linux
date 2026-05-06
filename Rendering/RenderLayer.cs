// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

public class RenderLayer : IDisposable
{
    private SKBitmap? _bitmap;
    private SKCanvas? _canvas;
    private bool _isDirty = true;
    private SKRect _bounds;
    private bool _disposed;

    public int ZIndex { get; }

    public bool IsDirty => _isDirty;

    public bool IsVisible { get; set; } = true;

    public float Opacity { get; set; } = 1f;

    public RenderLayer(int zIndex)
    {
        ZIndex = zIndex;
    }

    public SKCanvas BeginDraw(SKRect bounds)
    {
        if (_bitmap == null || _bounds != bounds)
        {
            _bitmap?.Dispose();
            _canvas?.Dispose();

            var width = Math.Max(1, (int)bounds.Width);
            var height = Math.Max(1, (int)bounds.Height);
            _bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            _canvas = new SKCanvas(_bitmap);
            _bounds = bounds;
        }

        _canvas!.Clear(SKColors.Transparent);
        _isDirty = false;
        return _canvas;
    }

    public void Invalidate()
    {
        _isDirty = true;
    }

    public void DrawTo(SKCanvas canvas, SKRect bounds)
    {
        if (!IsVisible || _bitmap == null) return;

        using var paint = new SKPaint
        {
            Color = SKColors.White.WithAlpha((byte)(Opacity * 255f))
        };
        canvas.DrawBitmap(_bitmap, bounds.Left, bounds.Top, paint);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _canvas?.Dispose();
        _bitmap?.Dispose();
    }
}
