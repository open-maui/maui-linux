// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Numerics;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Skia;
using SkiaSharp;
using ControlsShape = Microsoft.Maui.Controls.Shapes.Shape;
using Stretch = Microsoft.Maui.Controls.Stretch;

namespace Microsoft.Maui.Platform;

/// <summary>
/// The platform view of MAUI's <c>ShapeViewHandler</c> on Linux: what MauiShapeView
/// (Android, iOS) and W2DGraphicsView (Windows) are. A graphics view whose
/// <see cref="Drawable"/> is a <see cref="ShapeDrawable"/> over the
/// <see cref="IShapeView"/>, so fill, stroke, dash pattern and offset, line cap and
/// join, miter limit and aspect are drawn by MAUI's own shape drawing, on a Skia
/// canvas (Microsoft.Maui.Graphics.Skia).
/// </summary>
/// <remarks>
/// The view's own coordinates start at (0,0) at its top-left, as on every MAUI
/// platform. A Controls <c>Shape</c> stretches its path into the view by its Aspect
/// in <c>Shape.PathForBounds</c>, which the platform-neutral Controls build OpenMaui
/// runs leaves out (it is compiled for platform builds only): the same transform is
/// applied here, ahead of the shape's own render transform (Path.RenderTransform).
/// </remarks>
public class SkiaShapeView : SkiaView
{
    private IDrawable? _drawable;

    /// <summary>
    /// What the view draws: a <see cref="ShapeDrawable"/> over the shape view when the
    /// view belongs to a shape handler.
    /// </summary>
    public IDrawable? Drawable
    {
        get => _drawable;
        set
        {
            _drawable = value;
            Invalidate();
        }
    }

    /// <summary>The shape view the handler set the drawable for (ShapeDrawable keeps its own internal).</summary>
    internal IShapeView? ShapeView { get; private set; }

    /// <summary>The shape's own render transform (Path.RenderTransform), applied after the aspect.</summary>
    internal Matrix3x2? ShapeRenderTransform { get; set; }

    /// <summary>The winding mode the shape fills with (Polygon/Polyline FillRule, a Path's geometry FillRule).</summary>
    internal WindingMode ShapeWindingMode { get; set; }

    /// <summary>MAUI's <c>UpdateShape</c>: a new <see cref="ShapeDrawable"/> over the shape view.</summary>
    internal void UpdateShape(IShapeView shapeView)
    {
        ShapeView = shapeView;
        Drawable = new ShapeDrawable(shapeView);
    }

    /// <summary>MAUI's <c>InvalidateShape</c>: the shape is drawn again.</summary>
    internal void InvalidateShape(IShapeView shapeView)
    {
        if (_drawable == null)
            UpdateShape(shapeView);
        else
            Invalidate();
    }

    /// <summary>Drops the shape view (the handler disconnected).</summary>
    internal void ClearShape()
    {
        ShapeView = null;
        _drawable = null;
        ShapeRenderTransform = null;
        ShapeWindingMode = WindingMode.NonZero;
    }

    /// <summary>
    /// A shape view paints its Background behind the shape only when it also has a
    /// Fill (MAUI's ShapeViewHandler.MapBackground); with no Fill the Background fills
    /// the shape itself (ShapeDrawable).
    /// </summary>
    protected override void DrawBackground(SKCanvas canvas, SKRect bounds)
    {
        var shapeView = ShapeView;
        if (shapeView == null || _drawable == null)
        {
            base.DrawBackground(canvas, bounds);
            return;
        }

        if (shapeView.Fill is null || shapeView.Background is not { } background)
            return;

        canvas.Save();
        canvas.Translate(bounds.Left, bounds.Top);
        using (var skiaCanvas = new SkiaCanvas { Canvas = canvas })
        {
            var rect = new RectF(0, 0, bounds.Width, bounds.Height);
            skiaCanvas.SetFillPaint(background, rect);
            skiaCanvas.FillRectangle(rect);
        }
        canvas.Restore();
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var drawable = _drawable;
        if (drawable == null)
            return;

        var rect = new RectF(0, 0, bounds.Width, bounds.Height);
        if (drawable is ShapeDrawable shapeDrawable && ShapeView is { } shapeView)
        {
            Matrix3x2? transform = AspectTransform(shapeView, rect);
            if (ShapeRenderTransform is { } render)
                transform = transform is { } aspect ? aspect * render : render;
            shapeDrawable.UpdateRenderTransform(transform);
            shapeDrawable.UpdateWindingMode(ShapeWindingMode);
        }

        canvas.Save();
        canvas.ClipRect(bounds);
        canvas.Translate(bounds.Left, bounds.Top);
        using (var skiaCanvas = new SkiaCanvas { Canvas = canvas })
            drawable.Draw(skiaCanvas, rect);
        canvas.Restore();
    }

    /// <summary>
    /// A shape view measures to its explicit size, else nothing: the shape sizes itself
    /// from its path (Shape.MeasureOverride), as with MAUI's ShapeViewHandler.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = WidthRequest >= 0 ? WidthRequest : 0;
        var height = HeightRequest >= 0 ? HeightRequest : 0;
        return new Size(width, height);
    }

    /// <summary>
    /// The transform a Controls shape's path gets in the view's bounds: MAUI's
    /// Shape.TransformPathForBounds (platform builds only) for its Aspect, the bounds
    /// deflated by half the stroke. Null for a shape that sizes its path itself.
    /// </summary>
    internal static Matrix3x2? AspectTransform(IShapeView shapeView, RectF viewRect)
    {
        if (shapeView.Shape is not ControlsShape shape)
            return null;

        RectF pathBounds = shape.GetPath().GetBoundsByFlattening(1);

        double strokeThickness = shape.StrokeThickness;
        var viewBounds = new Rect(
            viewRect.X + strokeThickness / 2,
            viewRect.Y + strokeThickness / 2,
            viewRect.Width - strokeThickness,
            viewRect.Height - strokeThickness);

        Matrix3x2 transform;
        if (shape.Aspect == Stretch.None)
        {
            float translateX = 0;
            float translateY = 0;

            if (viewBounds.Left > pathBounds.Left)
                translateX = (float)(viewBounds.Left - pathBounds.Left);
            else if (pathBounds.Right > viewBounds.Right)
                translateX = (float)(viewBounds.Right - pathBounds.Right);

            if (viewBounds.Top > pathBounds.Top)
                translateY = (float)(viewBounds.Top - pathBounds.Top);
            else if (pathBounds.Bottom > viewBounds.Bottom)
                translateY = (float)(viewBounds.Bottom - pathBounds.Bottom);

            transform = translateX != 0 || translateY != 0
                ? Matrix3x2.CreateTranslation(translateX, translateY)
                : Matrix3x2.Identity;
        }
        else
        {
            transform = Matrix3x2.Identity;

            float calculatedWidth = (float)(viewBounds.Width / pathBounds.Width);
            float calculatedHeight = (float)(viewBounds.Height / pathBounds.Height);

            float widthScale = float.IsNaN(calculatedWidth) || float.IsInfinity(calculatedWidth) ? 0 : calculatedWidth;
            float heightScale = float.IsNaN(calculatedHeight) || float.IsInfinity(calculatedHeight) ? 0 : calculatedHeight;

            switch (shape.Aspect)
            {
                case Stretch.Fill:
                    transform *= Matrix3x2.CreateScale(widthScale, heightScale);
                    transform *= Matrix3x2.CreateTranslation(
                        (float)(viewBounds.Left - widthScale * pathBounds.Left),
                        (float)(viewBounds.Top - heightScale * pathBounds.Top));
                    break;

                case Stretch.Uniform:
                    float minScale = Math.Min(widthScale, heightScale);
                    transform *= Matrix3x2.CreateScale(minScale, minScale);
                    transform *= Matrix3x2.CreateTranslation(
                        (float)(viewBounds.Left - minScale * pathBounds.Left +
                            (viewBounds.Width - minScale * pathBounds.Width) / 2),
                        (float)(viewBounds.Top - minScale * pathBounds.Top +
                            (viewBounds.Height - minScale * pathBounds.Height) / 2));
                    break;

                case Stretch.UniformToFill:
                    float maxScale = Math.Max(widthScale, heightScale);
                    transform *= Matrix3x2.CreateScale(maxScale, maxScale);
                    transform *= Matrix3x2.CreateTranslation(
                        (float)(viewBounds.Left - maxScale * pathBounds.Left),
                        (float)(viewBounds.Top - maxScale * pathBounds.Top));
                    break;
            }
        }

        return transform.IsIdentity ? null : transform;
    }
}
