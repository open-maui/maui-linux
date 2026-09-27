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
        if (BackgroundColor != null && BackgroundColor != Colors.Transparent)
        {
            using var bgPaint = new SKPaint
            {
                Color = GetEffectiveBackgroundColor(),
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(bounds, bgPaint);
        }

        // Draw using IDrawable
        if (_drawable != null)
        {
            // Drawables work in the view's own coordinates, (0,0) at its
            // top-left, as on every MAUI platform; Skia bounds are window-absolute.
            canvas.Save();
            canvas.ClipRect(bounds);
            canvas.Translate(bounds.Left, bounds.Top);
            using (var skiaCanvas = new SkiaCanvas { Canvas = canvas })
                _drawable.Draw(skiaCanvas, new RectF(0, 0, bounds.Width, bounds.Height));
            canvas.Restore();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Graphics view takes all available space by default
        if (availableSize.Width < double.MaxValue && availableSize.Height < double.MaxValue)
        {
            return availableSize;
        }

        // Return a reasonable default size
        return new Size(
            availableSize.Width < double.MaxValue ? availableSize.Width : 100,
            availableSize.Height < double.MaxValue ? availableSize.Height : 100);
    }
}
