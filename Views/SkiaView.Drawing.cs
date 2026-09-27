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
            // Nothing painted this frame; the rect of the last paint was
            // already reported by the invalidation that hid the view.
            _hasPaintedRect = false;
            return;
        }

        canvas.Save();

        // Get SKRect for internal rendering
        var skBounds = BoundsSK;

        // Device matrix of the parent's coordinate space (DPI scale, CSD
        // inset, scroll offsets and ancestor transforms included): where a
        // later invalidation of this view will land is predicted from it.
        _parentDeviceMatrix = canvas.TotalMatrix;
        _hasParentDeviceMatrix = true;

        // Apply transforms if any are set
        if (HasRenderTransform)
            canvas.Concat(LocalRenderTransform(skBounds));

        // Physical pixels this paint covers: partial-damage invalidation
        // repaints them when the view changes or moves away.
        _contentDeviceMatrix = canvas.TotalMatrix;
        _paintedDeviceRect = _contentDeviceMatrix.MapRect(VisualOverflow(skBounds));
        _hasPaintedRect = true;

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
        foreach (var child in _children.ToArray())
        {
            child.Draw(canvas);
        }

        if (Opacity < 1.0f)
        {
            canvas.Restore();
        }

        canvas.Restore();
    }

    private SKMatrix _parentDeviceMatrix;
    private bool _hasParentDeviceMatrix;
    private SKMatrix _contentDeviceMatrix;
    private SKRect _paintedDeviceRect;
    private bool _hasPaintedRect;

    /// <summary>
    /// Hit-tests in the view's own space: a point in its parent's (window) space is mapped
    /// through the inverse of the view's render transform first, as the view is drawn
    /// through it. Parents call this for their children. A tab's content slid into view
    /// by TranslationX (SfTabView) took no input: its bounds are untranslated.
    /// </summary>
    internal SkiaView? HitTestAt(float x, float y)
    {
        if (HasRenderTransform && LocalRenderTransform(ToSKRect(Bounds)).TryInvert(out var inverse))
        {
            var p = inverse.MapPoint(x, y);
            return HitTest(p.X, p.Y);
        }
        return HitTest(x, y);
    }

    /// <summary>A point in the parent's space mapped through the inverse of this view's own render transform.</summary>
    internal SKPoint ToOwnSpace(float x, float y) =>
        HasRenderTransform && LocalRenderTransform(ToSKRect(Bounds)).TryInvert(out var inverse)
            ? inverse.MapPoint(x, y)
            : new SKPoint(x, y);

    /// <summary>
    /// A window point in this view's untransformed space (where its Bounds are): the
    /// inverse of every render transform from the root down to and including this view.
    /// </summary>
    internal SKPoint FromWindow(float x, float y)
    {
        SkiaView? transformed = null;
        for (var v = this; v != null; v = v.Parent)
            if (v.HasRenderTransform) { transformed = v; break; }
        if (transformed == null)
            return new SKPoint(x, y);

        var chain = new List<SkiaView>();
        for (var v = this; v != null; v = v.Parent)
            chain.Add(v);
        var p = new SKPoint(x, y);
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            var v = chain[i];
            if (v.HasRenderTransform && v.LocalRenderTransform(ToSKRect(v.Bounds)).TryInvert(out var inverse))
                p = inverse.MapPoint(p);
        }
        return p;
    }

    private static SKRect ToSKRect(Rect r) => new((float)r.Left, (float)r.Top, (float)r.Right, (float)r.Bottom);

    private bool HasRenderTransform =>
        Scale != 1.0 || ScaleX != 1.0 || ScaleY != 1.0 ||
        Rotation != 0.0 || RotationX != 0.0 || RotationY != 0.0 ||
        TranslationX != 0.0 || TranslationY != 0.0;

    /// <summary>
    /// The view's own transform (translation, rotation, scale about the
    /// anchor), in its parent's coordinate space.
    /// </summary>
    private SKMatrix LocalRenderTransform(SKRect skBounds)
    {
        float anchorAbsX = skBounds.Left + (float)(Bounds.Width * AnchorX);
        float anchorAbsY = skBounds.Top + (float)(Bounds.Height * AnchorY);
        var m = SKMatrix.CreateTranslation(anchorAbsX, anchorAbsY);
        if (TranslationX != 0.0 || TranslationY != 0.0)
            m = m.PreConcat(SKMatrix.CreateTranslation((float)TranslationX, (float)TranslationY));
        if (Rotation != 0.0)
            m = m.PreConcat(SKMatrix.CreateRotationDegrees((float)Rotation));
        float scaleX = (float)(Scale * ScaleX);
        float scaleY = (float)(Scale * ScaleY);
        if (scaleX != 1f || scaleY != 1f)
            m = m.PreConcat(SKMatrix.CreateScale(scaleX, scaleY));
        return m.PreConcat(SKMatrix.CreateTranslation(-anchorAbsX, -anchorAbsY));
    }

    /// <summary>
    /// Bounds grown by what may be painted outside them: the shadow's offset
    /// and blur, plus a small margin for antialiasing, strokes centred on the
    /// edge and focus rings.
    /// </summary>
    private SKRect VisualOverflow(SKRect skBounds)
    {
        const float margin = 4f;
        var r = skBounds;
        r.Inflate(margin, margin);
        if (Shadow != null)
        {
            float blur = (float)Shadow.Radius * 1.5f + margin;
            var s = skBounds;
            s.Offset((float)Shadow.Offset.X, (float)Shadow.Offset.Y);
            s.Inflate(blur, blur);
            r = SKRect.Union(r, s);
        }
        return r;
    }

    /// <summary>
    /// Physical-pixel rects to repaint when this view changes: where it was
    /// last painted and where it will be painted now (its current bounds and
    /// transform through the parent's last device matrix; for a view never
    /// painted yet, through the nearest painted ancestor's content matrix).
    /// Returns false when no prediction is possible (repaint everything).
    /// </summary>
    internal bool TryGetDamageRects(out SKRect previous, out bool hasPrevious, out SKRect next)
    {
        previous = _paintedDeviceRect;
        hasPrevious = _hasPaintedRect;
        next = default;

        SKMatrix parentMatrix;
        if (_hasParentDeviceMatrix)
        {
            parentMatrix = _parentDeviceMatrix;
        }
        else
        {
            var ancestor = _parent;
            while (ancestor != null && !ancestor._hasPaintedRect) ancestor = ancestor._parent;
            if (ancestor == null) return false;
            parentMatrix = ancestor._contentDeviceMatrix;
        }

        var skBounds = BoundsSK;
        var matrix = HasRenderTransform ? parentMatrix.PreConcat(LocalRenderTransform(skBounds)) : parentMatrix;
        next = matrix.MapRect(VisualOverflow(skBounds));
        return true;
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
        using var paint = CreateBackgroundPaint(bounds);
        if (paint != null)
            canvas.DrawRect(bounds, paint);
    }

    /// <summary>
    /// The paint for this view's background over <paramref name="bounds"/>: its
    /// Background brush (solid or gradient) when set, else BackgroundColor;
    /// null when there is nothing to paint. MAUI's default Background is
    /// Brush.Default, a non-null empty brush, and counts as unset. Views that
    /// fill a shape (Border) use this so both properties paint, as in MAUI.
    /// </summary>
    protected SKPaint? CreateBackgroundPaint(SKRect bounds)
    {
        var background = Background;
        if (!Brush.IsNullOrEmpty(background))
        {
            var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
            switch (background)
            {
                case SolidColorBrush scb:
                    paint.Color = scb.Color.ToSKColor();
                    return paint;
                case LinearGradientBrush lgb:
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
                    return paint;
                }
                case RadialGradientBrush rgb:
                {
                    var center = new SKPoint(
                        bounds.Left + (float)(rgb.Center.X * bounds.Width),
                        bounds.Top + (float)(rgb.Center.Y * bounds.Height));
                    var radius = (float)(rgb.Radius * Math.Max(bounds.Width, bounds.Height));
                    var colors = rgb.GradientStops.Select(s => s.Color.ToSKColor()).ToArray();
                    var positions = rgb.GradientStops.Select(s => s.Offset).ToArray();
                    paint.Shader = SKShader.CreateRadialGradient(center, radius, colors, positions, SKShaderTileMode.Clamp);
                    return paint;
                }
            }
            paint.Dispose();
        }

        if (_backgroundColorSK.Alpha > 0)
            return new SKPaint { Color = _backgroundColorSK, Style = SKPaintStyle.Fill, IsAntialias = true };
        return null;
    }
}
