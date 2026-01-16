// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Controls;
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
        return new SKColor(
            (byte)(color.Red * 255),
            (byte)(color.Green * 255),
            (byte)(color.Blue * 255),
            (byte)(color.Alpha * 255));
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

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        float strokeThickness = (float)StrokeThickness;
        float cornerRadius = (float)CornerRadius;

        var borderRect = new SKRect(
            bounds.Left + strokeThickness / 2f,
            bounds.Top + strokeThickness / 2f,
            bounds.Right - strokeThickness / 2f,
            bounds.Bottom - strokeThickness / 2f);

        // Draw shadow if enabled
        if (HasShadow)
        {
            using var shadowPaint = new SKPaint
            {
                Color = ToSKColor(ShadowColor),
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, (float)ShadowBlurRadius),
                Style = SKPaintStyle.Fill
            };
            var shadowRect = new SKRect(
                borderRect.Left + (float)ShadowOffsetX,
                borderRect.Top + (float)ShadowOffsetY,
                borderRect.Right + (float)ShadowOffsetX,
                borderRect.Bottom + (float)ShadowOffsetY);
            canvas.DrawRoundRect(new SKRoundRect(shadowRect, cornerRadius), shadowPaint);
        }

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = BackgroundColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(borderRect, cornerRadius), bgPaint);

        // Draw border
        if (strokeThickness > 0f)
        {
            using var borderPaint = new SKPaint
            {
                Color = ToSKColor(Stroke),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = strokeThickness,
                IsAntialias = true
            };
            canvas.DrawRoundRect(new SKRoundRect(borderRect, cornerRadius), borderPaint);
        }

        // Draw children
        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                child.Draw(canvas);
            }
        }
    }

    #endregion

    #region Layout

    protected override SKRect GetContentBounds()
    {
        return GetContentBounds(Bounds);
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

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        float strokeThickness = (float)StrokeThickness;
        var padding = BorderPadding;
        float paddingWidth = (float)(padding.Left + padding.Right) + strokeThickness * 2f;
        float paddingHeight = (float)(padding.Top + padding.Bottom) + strokeThickness * 2f;

        // Respect explicit size requests
        var requestedWidth = WidthRequest >= 0.0 ? (float)WidthRequest : availableSize.Width;
        var requestedHeight = HeightRequest >= 0.0 ? (float)HeightRequest : availableSize.Height;

        var childAvailable = new SKSize(
            Math.Max(0f, requestedWidth - paddingWidth),
            Math.Max(0f, requestedHeight - paddingHeight));

        var maxChildSize = SKSize.Empty;

        foreach (var child in Children)
        {
            var childSize = child.Measure(childAvailable);
            maxChildSize = new SKSize(
                Math.Max(maxChildSize.Width, childSize.Width),
                Math.Max(maxChildSize.Height, childSize.Height));
        }

        // Use requested size if set, otherwise use child size + padding
        var width = WidthRequest >= 0.0 ? (float)WidthRequest : maxChildSize.Width + paddingWidth;
        var height = HeightRequest >= 0.0 ? (float)HeightRequest : maxChildSize.Height + paddingHeight;

        return new SKSize(width, height);
    }

    protected override SKRect ArrangeOverride(SKRect bounds)
    {
        var contentBounds = GetContentBounds(bounds);

        foreach (var child in Children)
        {
            // Apply child's margin
            var margin = child.Margin;
            var marginedBounds = new SKRect(
                contentBounds.Left + (float)margin.Left,
                contentBounds.Top + (float)margin.Top,
                contentBounds.Right - (float)margin.Right,
                contentBounds.Bottom - (float)margin.Bottom);
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
            if (bounds.Contains(new SKPoint(x, y)))
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
