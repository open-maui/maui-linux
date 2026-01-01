// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered border/frame container control with full XAML styling support.
/// </summary>
public class SkiaBorder : SkiaLayoutView
{
    #region BindableProperties

    public static readonly BindableProperty StrokeThicknessProperty =
        BindableProperty.Create(nameof(StrokeThickness), typeof(float), typeof(SkiaBorder), 1f,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(float), typeof(SkiaBorder), 0f,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    public static readonly BindableProperty StrokeProperty =
        BindableProperty.Create(nameof(Stroke), typeof(SKColor), typeof(SkiaBorder), SKColors.Black,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    public static readonly BindableProperty PaddingLeftProperty =
        BindableProperty.Create(nameof(PaddingLeft), typeof(float), typeof(SkiaBorder), 0f,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).InvalidateMeasure());

    public static readonly BindableProperty PaddingTopProperty =
        BindableProperty.Create(nameof(PaddingTop), typeof(float), typeof(SkiaBorder), 0f,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).InvalidateMeasure());

    public static readonly BindableProperty PaddingRightProperty =
        BindableProperty.Create(nameof(PaddingRight), typeof(float), typeof(SkiaBorder), 0f,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).InvalidateMeasure());

    public static readonly BindableProperty PaddingBottomProperty =
        BindableProperty.Create(nameof(PaddingBottom), typeof(float), typeof(SkiaBorder), 0f,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).InvalidateMeasure());

    public static readonly BindableProperty HasShadowProperty =
        BindableProperty.Create(nameof(HasShadow), typeof(bool), typeof(SkiaBorder), false,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    public static readonly BindableProperty ShadowColorProperty =
        BindableProperty.Create(nameof(ShadowColor), typeof(SKColor), typeof(SkiaBorder), new SKColor(0, 0, 0, 40),
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    public static readonly BindableProperty ShadowBlurRadiusProperty =
        BindableProperty.Create(nameof(ShadowBlurRadius), typeof(float), typeof(SkiaBorder), 4f,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    public static readonly BindableProperty ShadowOffsetXProperty =
        BindableProperty.Create(nameof(ShadowOffsetX), typeof(float), typeof(SkiaBorder), 2f,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    public static readonly BindableProperty ShadowOffsetYProperty =
        BindableProperty.Create(nameof(ShadowOffsetY), typeof(float), typeof(SkiaBorder), 2f,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBorder)b).Invalidate());

    #endregion

    private bool _isPressed;

    #region Properties

    public float StrokeThickness
    {
        get => (float)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public float CornerRadius
    {
        get => (float)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public SKColor Stroke
    {
        get => (SKColor)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public float PaddingLeft
    {
        get => (float)GetValue(PaddingLeftProperty);
        set => SetValue(PaddingLeftProperty, value);
    }

    public float PaddingTop
    {
        get => (float)GetValue(PaddingTopProperty);
        set => SetValue(PaddingTopProperty, value);
    }

    public float PaddingRight
    {
        get => (float)GetValue(PaddingRightProperty);
        set => SetValue(PaddingRightProperty, value);
    }

    public float PaddingBottom
    {
        get => (float)GetValue(PaddingBottomProperty);
        set => SetValue(PaddingBottomProperty, value);
    }

    public bool HasShadow
    {
        get => (bool)GetValue(HasShadowProperty);
        set => SetValue(HasShadowProperty, value);
    }

    public SKColor ShadowColor
    {
        get => (SKColor)GetValue(ShadowColorProperty);
        set => SetValue(ShadowColorProperty, value);
    }

    public float ShadowBlurRadius
    {
        get => (float)GetValue(ShadowBlurRadiusProperty);
        set => SetValue(ShadowBlurRadiusProperty, value);
    }

    public float ShadowOffsetX
    {
        get => (float)GetValue(ShadowOffsetXProperty);
        set => SetValue(ShadowOffsetXProperty, value);
    }

    public float ShadowOffsetY
    {
        get => (float)GetValue(ShadowOffsetYProperty);
        set => SetValue(ShadowOffsetYProperty, value);
    }

    #endregion

    #region Events

    public event EventHandler? Tapped;

    #endregion

    #region SetPadding Methods

    /// <summary>
    /// Sets uniform padding on all sides.
    /// </summary>
    public void SetPadding(float all)
    {
        PaddingLeft = PaddingTop = PaddingRight = PaddingBottom = all;
    }

    /// <summary>
    /// Sets padding with horizontal and vertical values.
    /// </summary>
    public void SetPadding(float horizontal, float vertical)
    {
        PaddingLeft = PaddingRight = horizontal;
        PaddingTop = PaddingBottom = vertical;
    }

    /// <summary>
    /// Sets padding with individual values for each side.
    /// </summary>
    public void SetPadding(float left, float top, float right, float bottom)
    {
        PaddingLeft = left;
        PaddingTop = top;
        PaddingRight = right;
        PaddingBottom = bottom;
    }

    #endregion

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var strokeThickness = StrokeThickness;
        var cornerRadius = CornerRadius;

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
                Color = ShadowColor,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, ShadowBlurRadius),
                Style = SKPaintStyle.Fill
            };
            var shadowRect = new SKRect(
                borderRect.Left + ShadowOffsetX,
                borderRect.Top + ShadowOffsetY,
                borderRect.Right + ShadowOffsetX,
                borderRect.Bottom + ShadowOffsetY);
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
                Color = Stroke,
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

    protected override SKRect GetContentBounds()
    {
        return GetContentBounds(Bounds);
    }

    protected new SKRect GetContentBounds(SKRect bounds)
    {
        var strokeThickness = StrokeThickness;
        return new SKRect(
            bounds.Left + PaddingLeft + strokeThickness,
            bounds.Top + PaddingTop + strokeThickness,
            bounds.Right - PaddingRight - strokeThickness,
            bounds.Bottom - PaddingBottom - strokeThickness);
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        var strokeThickness = StrokeThickness;
        var paddingWidth = PaddingLeft + PaddingRight + strokeThickness * 2f;
        var paddingHeight = PaddingTop + PaddingBottom + strokeThickness * 2f;

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
                    Console.WriteLine("[SkiaBorder.HitTest] Intercepting for gesture - returning self");
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
            Console.WriteLine("[SkiaBorder] OnPointerPressed INTERCEPTED for gesture, MauiView=" + MauiView?.GetType().Name);
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
            Console.WriteLine("[SkiaBorder] OnPointerReleased - processing gesture recognizers, MauiView=" + MauiView?.GetType().Name);
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
}
