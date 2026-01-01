// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Skia;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered graphics view that supports IDrawable for custom drawing.
/// </summary>
public class SkiaGraphicsView : SkiaView
{
    private IDrawable? _drawable;

    public IDrawable? Drawable
    {
        get => _drawable;
        set
        {
            _drawable = value;
            Invalidate();
        }
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Draw background
        if (BackgroundColor != SKColors.Transparent)
        {
            using var bgPaint = new SKPaint
            {
                Color = BackgroundColor,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(bounds, bgPaint);
        }

        // Draw using IDrawable
        if (_drawable != null)
        {
            var dirtyRect = new RectF(bounds.Left, bounds.Top, bounds.Width, bounds.Height);

            using var skiaCanvas = new SkiaCanvas();
            skiaCanvas.Canvas = canvas;

            _drawable.Draw(skiaCanvas, dirtyRect);
        }
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        // Graphics view takes all available space by default
        if (availableSize.Width < float.MaxValue && availableSize.Height < float.MaxValue)
        {
            return availableSize;
        }

        // Return a reasonable default size
        return new SKSize(
            availableSize.Width < float.MaxValue ? availableSize.Width : 100,
            availableSize.Height < float.MaxValue ? availableSize.Height : 100);
    }
}
