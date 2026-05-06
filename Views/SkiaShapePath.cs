// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered view for MAUI Shapes.Path control.
/// Converts PathGeometry (PathFigure, LineSegment, BezierSegment, etc.) to SKPath for rendering.
/// </summary>
public class SkiaShapePath : SkiaView
{
    #region BindableProperties

    public static readonly BindableProperty FillColorProperty =
        BindableProperty.Create(nameof(FillColor), typeof(Color), typeof(SkiaShapePath), null,
            propertyChanged: (b, o, n) => ((SkiaShapePath)b).Invalidate());

    public static readonly BindableProperty StrokeColorProperty =
        BindableProperty.Create(nameof(StrokeColor), typeof(Color), typeof(SkiaShapePath), null,
            propertyChanged: (b, o, n) => ((SkiaShapePath)b).Invalidate());

    public static readonly BindableProperty StrokeThicknessProperty =
        BindableProperty.Create(nameof(StrokeThickness), typeof(double), typeof(SkiaShapePath), 1.0,
            propertyChanged: (b, o, n) => ((SkiaShapePath)b).Invalidate());

    public static readonly BindableProperty StrokeLineCapProperty =
        BindableProperty.Create(nameof(StrokeLineCap), typeof(PenLineCap), typeof(SkiaShapePath), PenLineCap.Flat,
            propertyChanged: (b, o, n) => ((SkiaShapePath)b).Invalidate());

    public static readonly BindableProperty StrokeLineJoinProperty =
        BindableProperty.Create(nameof(StrokeLineJoin), typeof(PenLineJoin), typeof(SkiaShapePath), PenLineJoin.Miter,
            propertyChanged: (b, o, n) => ((SkiaShapePath)b).Invalidate());

    public static readonly BindableProperty AspectProperty =
        BindableProperty.Create(nameof(Aspect), typeof(Stretch), typeof(SkiaShapePath), Stretch.None,
            propertyChanged: (b, o, n) => ((SkiaShapePath)b).Invalidate());

    #endregion

    #region Properties

    public Color? FillColor
    {
        get => (Color?)GetValue(FillColorProperty);
        set => SetValue(FillColorProperty, value);
    }

    public Color? StrokeColor
    {
        get => (Color?)GetValue(StrokeColorProperty);
        set => SetValue(StrokeColorProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public PenLineCap StrokeLineCap
    {
        get => (PenLineCap)GetValue(StrokeLineCapProperty);
        set => SetValue(StrokeLineCapProperty, value);
    }

    public PenLineJoin StrokeLineJoin
    {
        get => (PenLineJoin)GetValue(StrokeLineJoinProperty);
        set => SetValue(StrokeLineJoinProperty, value);
    }

    public Stretch Aspect
    {
        get => (Stretch)GetValue(AspectProperty);
        set => SetValue(AspectProperty, value);
    }

    /// <summary>
    /// The MAUI Geometry to render (PathGeometry, EllipseGeometry, etc.).
    /// </summary>
    public Geometry? Data { get; set; }

    #endregion

    private SKPath? _cachedPath;
    private SKRect _pathBounds;

    /// <summary>
    /// Builds an SKPath from the MAUI Geometry.
    /// </summary>
    private SKPath? BuildPath()
    {
        if (Data == null) return null;

        var path = new SKPath();

        if (Data is PathGeometry pathGeometry)
        {
            foreach (var figure in pathGeometry.Figures)
            {
                path.MoveTo((float)figure.StartPoint.X, (float)figure.StartPoint.Y);

                foreach (var segment in figure.Segments)
                {
                    switch (segment)
                    {
                        case LineSegment line:
                            path.LineTo((float)line.Point.X, (float)line.Point.Y);
                            break;

                        case BezierSegment bezier:
                            path.CubicTo(
                                (float)bezier.Point1.X, (float)bezier.Point1.Y,
                                (float)bezier.Point2.X, (float)bezier.Point2.Y,
                                (float)bezier.Point3.X, (float)bezier.Point3.Y);
                            break;

                        case QuadraticBezierSegment quadBezier:
                            path.QuadTo(
                                (float)quadBezier.Point1.X, (float)quadBezier.Point1.Y,
                                (float)quadBezier.Point2.X, (float)quadBezier.Point2.Y);
                            break;

                        case ArcSegment arc:
                            path.ArcTo(
                                (float)arc.Size.Width, (float)arc.Size.Height,
                                (float)arc.RotationAngle,
                                arc.IsLargeArc ? SKPathArcSize.Large : SKPathArcSize.Small,
                                arc.SweepDirection == SweepDirection.Clockwise ? SKPathDirection.Clockwise : SKPathDirection.CounterClockwise,
                                (float)arc.Point.X, (float)arc.Point.Y);
                            break;

                        case PolyLineSegment polyLine:
                            foreach (var point in polyLine.Points)
                            {
                                path.LineTo((float)point.X, (float)point.Y);
                            }
                            break;

                        case PolyBezierSegment polyBezier:
                            for (int i = 0; i + 2 < polyBezier.Points.Count; i += 3)
                            {
                                path.CubicTo(
                                    (float)polyBezier.Points[i].X, (float)polyBezier.Points[i].Y,
                                    (float)polyBezier.Points[i + 1].X, (float)polyBezier.Points[i + 1].Y,
                                    (float)polyBezier.Points[i + 2].X, (float)polyBezier.Points[i + 2].Y);
                            }
                            break;

                        case PolyQuadraticBezierSegment polyQuad:
                            for (int i = 0; i + 1 < polyQuad.Points.Count; i += 2)
                            {
                                path.QuadTo(
                                    (float)polyQuad.Points[i].X, (float)polyQuad.Points[i].Y,
                                    (float)polyQuad.Points[i + 1].X, (float)polyQuad.Points[i + 1].Y);
                            }
                            break;
                    }
                }

                if (figure.IsClosed)
                {
                    path.Close();
                }
            }

            // Apply fill rule
            path.FillType = pathGeometry.FillRule == FillRule.EvenOdd
                ? SKPathFillType.EvenOdd
                : SKPathFillType.Winding;
        }
        else if (Data is EllipseGeometry ellipse)
        {
            path.AddOval(new SKRect(
                (float)(ellipse.Center.X - ellipse.RadiusX),
                (float)(ellipse.Center.Y - ellipse.RadiusY),
                (float)(ellipse.Center.X + ellipse.RadiusX),
                (float)(ellipse.Center.Y + ellipse.RadiusY)));
        }
        else if (Data is RectangleGeometry rect)
        {
            path.AddRect(new SKRect(
                (float)rect.Rect.X, (float)rect.Rect.Y,
                (float)(rect.Rect.X + rect.Rect.Width),
                (float)(rect.Rect.Y + rect.Rect.Height)));
        }
        else if (Data is LineGeometry line)
        {
            path.MoveTo((float)line.StartPoint.X, (float)line.StartPoint.Y);
            path.LineTo((float)line.EndPoint.X, (float)line.EndPoint.Y);
        }

        return path;
    }

    /// <summary>
    /// Invalidates the cached path when geometry changes.
    /// </summary>
    public void InvalidatePath()
    {
        _cachedPath?.Dispose();
        _cachedPath = null;
        InvalidateMeasure();
        Invalidate();
    }

    private SKPath? GetOrBuildPath()
    {
        if (_cachedPath == null)
        {
            _cachedPath = BuildPath();
            if (_cachedPath != null)
            {
                _pathBounds = _cachedPath.TightBounds;
            }
        }
        return _cachedPath;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var path = GetOrBuildPath();
        if (path == null)
            return new Size(
                WidthRequest >= 0 ? WidthRequest : 0,
                HeightRequest >= 0 ? HeightRequest : 0);

        var w = WidthRequest >= 0 ? WidthRequest : _pathBounds.Right;
        var h = HeightRequest >= 0 ? HeightRequest : _pathBounds.Bottom;

        return new Size(w, h);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var path = GetOrBuildPath();
        if (path == null) return;

        canvas.Save();

        // Apply stretch/aspect scaling
        if (Aspect != Stretch.None && _pathBounds.Width > 0 && _pathBounds.Height > 0)
        {
            float scaleX = bounds.Width / _pathBounds.Width;
            float scaleY = bounds.Height / _pathBounds.Height;

            switch (Aspect)
            {
                case Stretch.Fill:
                    canvas.Translate(bounds.Left - _pathBounds.Left * scaleX, bounds.Top - _pathBounds.Top * scaleY);
                    canvas.Scale(scaleX, scaleY);
                    break;
                case Stretch.Uniform:
                    float uniformScale = Math.Min(scaleX, scaleY);
                    float offsetX = (bounds.Width - _pathBounds.Width * uniformScale) / 2f;
                    float offsetY = (bounds.Height - _pathBounds.Height * uniformScale) / 2f;
                    canvas.Translate(bounds.Left + offsetX - _pathBounds.Left * uniformScale,
                                     bounds.Top + offsetY - _pathBounds.Top * uniformScale);
                    canvas.Scale(uniformScale, uniformScale);
                    break;
                case Stretch.UniformToFill:
                    float fillScale = Math.Max(scaleX, scaleY);
                    float fOffsetX = (bounds.Width - _pathBounds.Width * fillScale) / 2f;
                    float fOffsetY = (bounds.Height - _pathBounds.Height * fillScale) / 2f;
                    canvas.Translate(bounds.Left + fOffsetX - _pathBounds.Left * fillScale,
                                     bounds.Top + fOffsetY - _pathBounds.Top * fillScale);
                    canvas.Scale(fillScale, fillScale);
                    break;
            }
        }
        else
        {
            // No stretch - just translate to bounds origin
            canvas.Translate(bounds.Left, bounds.Top);
        }

        // Draw fill
        if (FillColor != null)
        {
            using var fillPaint = new SKPaint
            {
                Color = FillColor.ToSKColor(),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawPath(path, fillPaint);
        }

        // Draw stroke
        if (StrokeColor != null && StrokeThickness > 0)
        {
            using var strokePaint = new SKPaint
            {
                Color = StrokeColor.ToSKColor(),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = (float)StrokeThickness,
                IsAntialias = true,
                StrokeCap = StrokeLineCap switch
                {
                    PenLineCap.Round => SKStrokeCap.Round,
                    PenLineCap.Square => SKStrokeCap.Square,
                    _ => SKStrokeCap.Butt
                },
                StrokeJoin = StrokeLineJoin switch
                {
                    PenLineJoin.Round => SKStrokeJoin.Round,
                    PenLineJoin.Bevel => SKStrokeJoin.Bevel,
                    _ => SKStrokeJoin.Miter
                }
            };
            canvas.DrawPath(path, strokePaint);
        }

        canvas.Restore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cachedPath?.Dispose();
            _cachedPath = null;
        }
        base.Dispose(disposing);
    }
}
