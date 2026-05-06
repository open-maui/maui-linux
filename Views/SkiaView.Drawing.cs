// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public abstract partial class SkiaView
{
    /// <summary>
    /// Draws this view and its children to the canvas.
    /// </summary>
    public virtual void Draw(SKCanvas canvas)
    {
        if (!IsVisible || Opacity <= 0)
        {
            return;
        }

        canvas.Save();

        // Get SKRect for internal rendering
        var skBounds = BoundsSK;

        // Apply transforms if any are set
        if (Scale != 1.0 || ScaleX != 1.0 || ScaleY != 1.0 ||
            Rotation != 0.0 || RotationX != 0.0 || RotationY != 0.0 ||
            TranslationX != 0.0 || TranslationY != 0.0)
        {
            // Calculate anchor point in absolute coordinates
            float anchorAbsX = skBounds.Left + (float)(Bounds.Width * AnchorX);
            float anchorAbsY = skBounds.Top + (float)(Bounds.Height * AnchorY);

            // Move origin to anchor point
            canvas.Translate(anchorAbsX, anchorAbsY);

            // Apply translation
            if (TranslationX != 0.0 || TranslationY != 0.0)
            {
                canvas.Translate((float)TranslationX, (float)TranslationY);
            }

            // Apply rotation
            if (Rotation != 0.0)
            {
                canvas.RotateDegrees((float)Rotation);
            }

            // Apply scale
            float scaleX = (float)(Scale * ScaleX);
            float scaleY = (float)(Scale * ScaleY);
            if (scaleX != 1f || scaleY != 1f)
            {
                canvas.Scale(scaleX, scaleY);
            }

            // Move origin back
            canvas.Translate(-anchorAbsX, -anchorAbsY);
        }

        // Apply opacity
        if (Opacity < 1.0f)
        {
            canvas.SaveLayer(new SKPaint { Color = SKColors.White.WithAlpha((byte)(Opacity * 255)) });
        }

        // Draw shadow if set
        if (Shadow != null)
        {
            DrawShadow(canvas, skBounds);
        }

        // Apply clip geometry if set
        if (Clip != null)
        {
            ApplyClip(canvas, skBounds);
        }

        // Draw background at absolute bounds
        DrawBackground(canvas, skBounds);

        // Draw content at absolute bounds
        OnDraw(canvas, skBounds);

        // Draw children - they draw at their own absolute bounds
        foreach (var child in _children)
        {
            child.Draw(canvas);
        }

        if (Opacity < 1.0f)
        {
            canvas.Restore();
        }

        canvas.Restore();
    }

    /// <summary>
    /// Override to draw custom content.
    /// </summary>
    protected virtual void OnDraw(SKCanvas canvas, SKRect bounds)
    {
    }

    /// <summary>
    /// Draws the shadow for this view.
    /// </summary>
    protected virtual void DrawShadow(SKCanvas canvas, SKRect bounds)
    {
        if (Shadow == null) return;

        var shadowColor = Shadow.Brush is SolidColorBrush scb
            ? scb.Color.ToSKColor().WithAlpha((byte)(scb.Color.Alpha * 255 * Shadow.Opacity))
            : SKColors.Black.WithAlpha((byte)(255 * Shadow.Opacity));

        using var shadowPaint = new SKPaint
        {
            Color = shadowColor,
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, (float)Shadow.Radius / 2)
        };

        var shadowBounds = new SKRect(
            bounds.Left + (float)Shadow.Offset.X,
            bounds.Top + (float)Shadow.Offset.Y,
            bounds.Right + (float)Shadow.Offset.X,
            bounds.Bottom + (float)Shadow.Offset.Y);

        canvas.DrawRect(shadowBounds, shadowPaint);
    }

    /// <summary>
    /// Applies the clip geometry to the canvas.
    /// </summary>
    protected virtual void ApplyClip(SKCanvas canvas, SKRect bounds)
    {
        if (Clip == null) return;

        // Convert MAUI Geometry to SkiaSharp path
        var path = ConvertGeometryToPath(Clip, bounds);
        if (path != null)
        {
            canvas.ClipPath(path);
        }
    }

    /// <summary>
    /// Converts a MAUI Geometry to a SkiaSharp path.
    /// </summary>
    private SKPath? ConvertGeometryToPath(Geometry geometry, SKRect bounds)
    {
        var path = new SKPath();

        if (geometry is RectangleGeometry rect)
        {
            var r = rect.Rect;
            path.AddRect(new SKRect(
                bounds.Left + (float)r.Left,
                bounds.Top + (float)r.Top,
                bounds.Left + (float)r.Right,
                bounds.Top + (float)r.Bottom));
        }
        else if (geometry is EllipseGeometry ellipse)
        {
            path.AddOval(new SKRect(
                bounds.Left + (float)(ellipse.Center.X - ellipse.RadiusX),
                bounds.Top + (float)(ellipse.Center.Y - ellipse.RadiusY),
                bounds.Left + (float)(ellipse.Center.X + ellipse.RadiusX),
                bounds.Top + (float)(ellipse.Center.Y + ellipse.RadiusY)));
        }
        else if (geometry is RoundRectangleGeometry roundRect)
        {
            var r = roundRect.Rect;
            var cr = roundRect.CornerRadius;
            var skRect = new SKRect(
                bounds.Left + (float)r.Left,
                bounds.Top + (float)r.Top,
                bounds.Left + (float)r.Right,
                bounds.Top + (float)r.Bottom);
            var skRoundRect = new SKRoundRect();
            skRoundRect.SetRectRadii(skRect, new[]
            {
                new SKPoint((float)cr.TopLeft, (float)cr.TopLeft),
                new SKPoint((float)cr.TopRight, (float)cr.TopRight),
                new SKPoint((float)cr.BottomRight, (float)cr.BottomRight),
                new SKPoint((float)cr.BottomLeft, (float)cr.BottomLeft)
            });
            path.AddRoundRect(skRoundRect);
        }
        // Add more geometry types as needed

        return path;
    }

    /// <summary>
    /// Draws the background (color or brush) for this view.
    /// </summary>
    protected virtual void DrawBackground(SKCanvas canvas, SKRect bounds)
    {
        // First try to use Background brush
        if (Background != null)
        {
            using var paint = new SKPaint { IsAntialias = true };

            if (Background is SolidColorBrush scb)
            {
                paint.Color = scb.Color.ToSKColor();
                canvas.DrawRect(bounds, paint);
            }
            else if (Background is LinearGradientBrush lgb)
            {
                var start = new SKPoint(
                    bounds.Left + (float)(lgb.StartPoint.X * bounds.Width),
                    bounds.Top + (float)(lgb.StartPoint.Y * bounds.Height));
                var end = new SKPoint(
                    bounds.Left + (float)(lgb.EndPoint.X * bounds.Width),
                    bounds.Top + (float)(lgb.EndPoint.Y * bounds.Height));

                var colors = lgb.GradientStops.Select(s => s.Color.ToSKColor()).ToArray();
                var positions = lgb.GradientStops.Select(s => s.Offset).ToArray();

                paint.Shader = SKShader.CreateLinearGradient(start, end, colors, positions, SKShaderTileMode.Clamp);
                canvas.DrawRect(bounds, paint);
            }
            else if (Background is RadialGradientBrush rgb)
            {
                var center = new SKPoint(
                    bounds.Left + (float)(rgb.Center.X * bounds.Width),
                    bounds.Top + (float)(rgb.Center.Y * bounds.Height));
                var radius = (float)(rgb.Radius * Math.Max(bounds.Width, bounds.Height));

                var colors = rgb.GradientStops.Select(s => s.Color.ToSKColor()).ToArray();
                var positions = rgb.GradientStops.Select(s => s.Offset).ToArray();

                paint.Shader = SKShader.CreateRadialGradient(center, radius, colors, positions, SKShaderTileMode.Clamp);
                canvas.DrawRect(bounds, paint);
            }
        }
        // Fall back to BackgroundColor (skip if transparent)
        else if (_backgroundColorSK.Alpha > 0)
        {
            using var paint = new SKPaint { Color = _backgroundColorSK };
            canvas.DrawRect(bounds, paint);
        }
    }
}
