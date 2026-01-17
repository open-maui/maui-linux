// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered border/frame container control with full XAML styling support.
/// Implements MAUI IBorderView interface patterns.
/// </summary>
public class SkiaBorder : SkiaLayoutView
{
    #region BindableProperties

    public static readonly BindableProperty StrokeThicknessProperty =
        BindableProperty.Create(nameof(StrokeThickness), typeof(double), typeof(SkiaBorder), 1.0,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(SkiaBorder), 0.0,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    public static readonly BindableProperty StrokeProperty =
        BindableProperty.Create(nameof(Stroke), typeof(Color), typeof(SkiaBorder), Colors.Black,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    public static readonly BindableProperty BorderPaddingProperty =
        BindableProperty.Create(nameof(BorderPadding), typeof(Thickness), typeof(SkiaBorder), new Thickness(0),
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).InvalidateMeasure());

    public static readonly BindableProperty HasShadowProperty =
        BindableProperty.Create(nameof(HasShadow), typeof(bool), typeof(SkiaBorder), false,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    public static readonly BindableProperty ShadowColorProperty =
        BindableProperty.Create(nameof(ShadowColor), typeof(Color), typeof(SkiaBorder), Color.FromRgba(0, 0, 0, 40),
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    public static readonly BindableProperty ShadowBlurRadiusProperty =
        BindableProperty.Create(nameof(ShadowBlurRadius), typeof(double), typeof(SkiaBorder), 4.0,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    public static readonly BindableProperty ShadowOffsetXProperty =
        BindableProperty.Create(nameof(ShadowOffsetX), typeof(double), typeof(SkiaBorder), 2.0,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    public static readonly BindableProperty ShadowOffsetYProperty =
        BindableProperty.Create(nameof(ShadowOffsetY), typeof(double), typeof(SkiaBorder), 2.0,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    public static readonly BindableProperty StrokeShapeProperty =
        BindableProperty.Create(nameof(StrokeShape), typeof(IShape), typeof(SkiaBorder), null,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    public static readonly BindableProperty StrokeDashArrayProperty =
        BindableProperty.Create(nameof(StrokeDashArray), typeof(DoubleCollection), typeof(SkiaBorder), null,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    public static readonly BindableProperty StrokeDashOffsetProperty =
        BindableProperty.Create(nameof(StrokeDashOffset), typeof(double), typeof(SkiaBorder), 0.0,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    public static readonly BindableProperty StrokeLineCapProperty =
        BindableProperty.Create(nameof(StrokeLineCap), typeof(LineCap), typeof(SkiaBorder), LineCap.Butt,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    public static readonly BindableProperty StrokeLineJoinProperty =
        BindableProperty.Create(nameof(StrokeLineJoin), typeof(LineJoin), typeof(SkiaBorder), LineJoin.Miter,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    public static readonly BindableProperty StrokeMiterLimitProperty =
        BindableProperty.Create(nameof(StrokeMiterLimit), typeof(double), typeof(SkiaBorder), 10.0,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    #endregion

    private bool _isPressed;

    #region Properties

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public Color Stroke
    {
        get => (Color)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Thickness BorderPadding
    {
        get => (Thickness)GetValue(BorderPaddingProperty);
        set => SetValue(BorderPaddingProperty, value);
    }

    // Convenience properties for backward compatibility
    public double PaddingLeft
    {
        get => BorderPadding.Left;
        set => BorderPadding = new Thickness(value, BorderPadding.Top, BorderPadding.Right, BorderPadding.Bottom);
    }

    public double PaddingTop
    {
        get => BorderPadding.Top;
        set => BorderPadding = new Thickness(BorderPadding.Left, value, BorderPadding.Right, BorderPadding.Bottom);
    }

    public double PaddingRight
    {
        get => BorderPadding.Right;
        set => BorderPadding = new Thickness(BorderPadding.Left, BorderPadding.Top, value, BorderPadding.Bottom);
    }

    public double PaddingBottom
    {
        get => BorderPadding.Bottom;
        set => BorderPadding = new Thickness(BorderPadding.Left, BorderPadding.Top, BorderPadding.Right, value);
    }

    public bool HasShadow
    {
        get => (bool)GetValue(HasShadowProperty);
        set => SetValue(HasShadowProperty, value);
    }

    public Color ShadowColor
    {
        get => (Color)GetValue(ShadowColorProperty);
        set => SetValue(ShadowColorProperty, value);
    }

    public double ShadowBlurRadius
    {
        get => (double)GetValue(ShadowBlurRadiusProperty);
        set => SetValue(ShadowBlurRadiusProperty, value);
    }

    public double ShadowOffsetX
    {
        get => (double)GetValue(ShadowOffsetXProperty);
        set => SetValue(ShadowOffsetXProperty, value);
    }

    public double ShadowOffsetY
    {
        get => (double)GetValue(ShadowOffsetYProperty);
        set => SetValue(ShadowOffsetYProperty, value);
    }

    /// <summary>
    /// Gets or sets the shape of the border stroke (Rectangle, RoundRectangle, Ellipse, etc.).
    /// </summary>
    public IShape? StrokeShape
    {
        get => (IShape?)GetValue(StrokeShapeProperty);
        set => SetValue(StrokeShapeProperty, value);
    }

    /// <summary>
    /// Gets or sets the dash pattern for the stroke.
    /// </summary>
    public DoubleCollection? StrokeDashArray
    {
        get => (DoubleCollection?)GetValue(StrokeDashArrayProperty);
        set => SetValue(StrokeDashArrayProperty, value);
    }

    /// <summary>
    /// Gets or sets the offset into the dash pattern.
    /// </summary>
    public double StrokeDashOffset
    {
        get => (double)GetValue(StrokeDashOffsetProperty);
        set => SetValue(StrokeDashOffsetProperty, value);
    }

    /// <summary>
    /// Gets or sets the cap style for the stroke line ends.
    /// </summary>
    public LineCap StrokeLineCap
    {
        get => (LineCap)GetValue(StrokeLineCapProperty);
        set => SetValue(StrokeLineCapProperty, value);
    }

    /// <summary>
    /// Gets or sets the join style for stroke corners.
    /// </summary>
    public LineJoin StrokeLineJoin
    {
        get => (LineJoin)GetValue(StrokeLineJoinProperty);
        set => SetValue(StrokeLineJoinProperty, value);
    }

    /// <summary>
    /// Gets or sets the miter limit for stroke joins.
    /// </summary>
    public double StrokeMiterLimit
    {
        get => (double)GetValue(StrokeMiterLimitProperty);
        set => SetValue(StrokeMiterLimitProperty, value);
    }

    #endregion

    #region Events

    public event EventHandler? Tapped;

    #endregion

    #region Helper Methods

    /// <summary>
    /// Converts a MAUI Color to SkiaSharp SKColor.
    /// </summary>
    private static SKColor ToSKColor(Color? color)
    {
        if (color == null) return SKColors.Transparent;
        return color.ToSKColor();
    }

    #endregion

    #region SetPadding Methods

    /// <summary>
    /// Sets uniform padding on all sides.
    /// </summary>
    public void SetPadding(double all)
    {
        BorderPadding = new Thickness(all);
    }

    /// <summary>
    /// Sets padding with horizontal and vertical values.
    /// </summary>
    public void SetPadding(double horizontal, double vertical)
    {
        BorderPadding = new Thickness(horizontal, vertical);
    }

    /// <summary>
    /// Sets padding with individual values for each side.
    /// </summary>
    public void SetPadding(double left, double top, double right, double bottom)
    {
        BorderPadding = new Thickness(left, top, right, bottom);
    }

    #endregion

    #region Drawing

    /// <summary>
    /// Converts LineCap to SKStrokeCap.
    /// </summary>
    private static SKStrokeCap ToSKStrokeCap(LineCap lineCap)
    {
        return lineCap switch
        {
            LineCap.Round => SKStrokeCap.Round,
            LineCap.Square => SKStrokeCap.Square,
            _ => SKStrokeCap.Butt
        };
    }

    /// <summary>
    /// Converts LineJoin to SKStrokeJoin.
    /// </summary>
    private static SKStrokeJoin ToSKStrokeJoin(LineJoin lineJoin)
    {
        return lineJoin switch
        {
            LineJoin.Round => SKStrokeJoin.Round,
            LineJoin.Bevel => SKStrokeJoin.Bevel,
            _ => SKStrokeJoin.Miter
        };
    }

    /// <summary>
    /// Creates an SKPath for the border based on StrokeShape.
    /// </summary>
    private SKPath CreateShapePath(SKRect rect, float defaultCornerRadius)
    {
        var path = new SKPath();

        if (StrokeShape is RoundRectangle roundRect)
        {
            // Use RoundRectangle's corner radii
            var cr = roundRect.CornerRadius;
            var radii = new SKPoint[]
            {
                new SKPoint((float)cr.TopLeft, (float)cr.TopLeft),
                new SKPoint((float)cr.TopRight, (float)cr.TopRight),
                new SKPoint((float)cr.BottomRight, (float)cr.BottomRight),
                new SKPoint((float)cr.BottomLeft, (float)cr.BottomLeft)
            };
            var skRoundRect = new SKRoundRect();
            skRoundRect.SetRectRadii(rect, radii);
            path.AddRoundRect(skRoundRect);
        }
        else if (StrokeShape is Ellipse)
        {
            path.AddOval(rect);
        }
        else if (StrokeShape is Rectangle)
        {
            path.AddRect(rect);
        }
        else
        {
            // Default: use CornerRadius property
            if (defaultCornerRadius > 0)
            {
                path.AddRoundRect(rect, defaultCornerRadius, defaultCornerRadius);
            }
            else
            {
                path.AddRect(rect);
            }
        }

        return path;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        float strokeThickness = (float)StrokeThickness;
        float cornerRadius = (float)CornerRadius;

        var borderRect = new SKRect(
            bounds.Left + strokeThickness / 2f,
            bounds.Top + strokeThickness / 2f,
            bounds.Right - strokeThickness / 2f,
            bounds.Bottom - strokeThickness / 2f);

        // Create the shape path
        using var shapePath = CreateShapePath(borderRect, cornerRadius);

        // Draw shadow if enabled
        if (HasShadow)
        {
            using var shadowPaint = new SKPaint
            {
                Color = ToSKColor(ShadowColor),
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, (float)ShadowBlurRadius),
                Style = SKPaintStyle.Fill
            };
            canvas.Save();
            canvas.Translate((float)ShadowOffsetX, (float)ShadowOffsetY);
            canvas.DrawPath(shapePath, shadowPaint);
            canvas.Restore();
        }

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = GetEffectiveBackgroundColor(),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawPath(shapePath, bgPaint);

        // Draw border
        if (strokeThickness > 0f)
        {
            using var borderPaint = new SKPaint
            {
                Color = ToSKColor(Stroke),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = strokeThickness,
                IsAntialias = true,
                StrokeCap = ToSKStrokeCap(StrokeLineCap),
                StrokeJoin = ToSKStrokeJoin(StrokeLineJoin),
                StrokeMiter = (float)StrokeMiterLimit
            };

            // Apply dash pattern if specified
            if (StrokeDashArray != null && StrokeDashArray.Count > 0)
            {
                var dashArray = new float[StrokeDashArray.Count];
                for (int i = 0; i < StrokeDashArray.Count; i++)
                {
                    dashArray[i] = (float)(StrokeDashArray[i] * strokeThickness);
                }
                borderPaint.PathEffect = SKPathEffect.CreateDash(dashArray, (float)(StrokeDashOffset * strokeThickness));
            }

            canvas.DrawPath(shapePath, borderPaint);
        }

        // Clip to shape and draw children
        canvas.Save();
        canvas.ClipPath(shapePath);
        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                child.Draw(canvas);
            }
        }
        canvas.Restore();
    }

    #endregion

    #region Layout

    protected override SKRect GetContentBounds()
    {
        return GetContentBounds(new SKRect((float)Bounds.Left, (float)Bounds.Top, (float)Bounds.Right, (float)Bounds.Bottom));
    }

    protected new SKRect GetContentBounds(SKRect bounds)
    {
        float strokeThickness = (float)StrokeThickness;
        var padding = BorderPadding;
        return new SKRect(
            bounds.Left + (float)padding.Left + strokeThickness,
            bounds.Top + (float)padding.Top + strokeThickness,
            bounds.Right - (float)padding.Right - strokeThickness,
            bounds.Bottom - (float)padding.Bottom - strokeThickness);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        float strokeThickness = (float)StrokeThickness;
        var padding = BorderPadding;
        float paddingWidth = (float)(padding.Left + padding.Right) + strokeThickness * 2f;
        float paddingHeight = (float)(padding.Top + padding.Bottom) + strokeThickness * 2f;

        // Respect explicit size requests
        var requestedWidth = WidthRequest >= 0.0 ? (float)WidthRequest : (float)availableSize.Width;
        var requestedHeight = HeightRequest >= 0.0 ? (float)HeightRequest : (float)availableSize.Height;

        var childAvailable = new Size(
            Math.Max(0f, requestedWidth - paddingWidth),
            Math.Max(0f, requestedHeight - paddingHeight));

        var maxChildSize = Size.Zero;

        foreach (var child in Children)
        {
            var childSize = child.Measure(childAvailable);
            maxChildSize = new Size(
                Math.Max(maxChildSize.Width, childSize.Width),
                Math.Max(maxChildSize.Height, childSize.Height));
        }

        // Use requested size if set, otherwise use child size + padding
        var width = WidthRequest >= 0.0 ? (float)WidthRequest : (float)maxChildSize.Width + paddingWidth;
        var height = HeightRequest >= 0.0 ? (float)HeightRequest : (float)maxChildSize.Height + paddingHeight;

        return new Size(width, height);
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        var contentBounds = GetContentBounds(new SKRect((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom));

        foreach (var child in Children)
        {
            // Apply child's margin
            var margin = child.Margin;
            var marginedBounds = new Rect(
                contentBounds.Left + margin.Left,
                contentBounds.Top + margin.Top,
                contentBounds.Width - margin.Left - margin.Right,
                contentBounds.Height - margin.Top - margin.Bottom);
            child.Arrange(marginedBounds);
        }

        return bounds;
    }

    #endregion

    #region Input Handling

    private bool HasTapGestureRecognizers()
    {
        if (MauiView?.GestureRecognizers == null)
        {
            return false;
        }

        foreach (var gestureRecognizer in MauiView.GestureRecognizers)
        {
            if (gestureRecognizer is TapGestureRecognizer)
            {
                return true;
            }
        }

        return false;
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (IsVisible && IsEnabled)
        {
            var bounds = Bounds;
            if (bounds.Contains(x, y))
            {
                if (HasTapGestureRecognizers())
                {
                    return this;
                }
                return base.HitTest(x, y);
            }
        }
        return null;
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (HasTapGestureRecognizers())
        {
            _isPressed = true;
            e.Handled = true;
            if (MauiView != null)
            {
                GestureManager.ProcessPointerDown(MauiView, e.X, e.Y);
            }
        }
        else
        {
            base.OnPointerPressed(e);
        }
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (_isPressed)
        {
            _isPressed = false;
            e.Handled = true;
            if (MauiView != null)
            {
                GestureManager.ProcessPointerUp(MauiView, e.X, e.Y);
            }
            Tapped?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            base.OnPointerReleased(e);
        }
    }

    public override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isPressed = false;
    }

    #endregion
}
