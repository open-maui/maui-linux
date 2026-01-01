// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered toggle switch control with full XAML styling support.
/// </summary>
public class SkiaSwitch : SkiaView
{
    #region BindableProperties

    /// <summary>
    /// Bindable property for IsOn.
    /// </summary>
    public static readonly BindableProperty IsOnProperty =
        BindableProperty.Create(
            nameof(IsOn),
            typeof(bool),
            typeof(SkiaSwitch),
            false,
            BindingMode.OneWay,
            propertyChanged: (b, o, n) => ((SkiaSwitch)b).OnIsOnChanged());

    /// <summary>
    /// Bindable property for OnTrackColor.
    /// </summary>
    public static readonly BindableProperty OnTrackColorProperty =
        BindableProperty.Create(
            nameof(OnTrackColor),
            typeof(SKColor),
            typeof(SkiaSwitch),
            new SKColor(33, 150, 243),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSwitch)b).Invalidate());

    /// <summary>
    /// Bindable property for OffTrackColor.
    /// </summary>
    public static readonly BindableProperty OffTrackColorProperty =
        BindableProperty.Create(
            nameof(OffTrackColor),
            typeof(SKColor),
            typeof(SkiaSwitch),
            new SKColor(158, 158, 158),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSwitch)b).Invalidate());

    /// <summary>
    /// Bindable property for ThumbColor.
    /// </summary>
    public static readonly BindableProperty ThumbColorProperty =
        BindableProperty.Create(
            nameof(ThumbColor),
            typeof(SKColor),
            typeof(SkiaSwitch),
            SKColors.White,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSwitch)b).Invalidate());

    /// <summary>
    /// Bindable property for DisabledColor.
    /// </summary>
    public static readonly BindableProperty DisabledColorProperty =
        BindableProperty.Create(
            nameof(DisabledColor),
            typeof(SKColor),
            typeof(SkiaSwitch),
            new SKColor(189, 189, 189),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSwitch)b).Invalidate());

    /// <summary>
    /// Bindable property for TrackWidth.
    /// </summary>
    public static readonly BindableProperty TrackWidthProperty =
        BindableProperty.Create(
            nameof(TrackWidth),
            typeof(float),
            typeof(SkiaSwitch),
            52f,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSwitch)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for TrackHeight.
    /// </summary>
    public static readonly BindableProperty TrackHeightProperty =
        BindableProperty.Create(
            nameof(TrackHeight),
            typeof(float),
            typeof(SkiaSwitch),
            32f,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSwitch)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for ThumbRadius.
    /// </summary>
    public static readonly BindableProperty ThumbRadiusProperty =
        BindableProperty.Create(
            nameof(ThumbRadius),
            typeof(float),
            typeof(SkiaSwitch),
            12f,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSwitch)b).Invalidate());

    /// <summary>
    /// Bindable property for ThumbPadding.
    /// </summary>
    public static readonly BindableProperty ThumbPaddingProperty =
        BindableProperty.Create(
            nameof(ThumbPadding),
            typeof(float),
            typeof(SkiaSwitch),
            4f,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSwitch)b).Invalidate());

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets whether the switch is on.
    /// </summary>
    public bool IsOn
    {
        get => (bool)GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    /// <summary>
    /// Gets or sets the on track color.
    /// </summary>
    public SKColor OnTrackColor
    {
        get => (SKColor)GetValue(OnTrackColorProperty);
        set => SetValue(OnTrackColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the off track color.
    /// </summary>
    public SKColor OffTrackColor
    {
        get => (SKColor)GetValue(OffTrackColorProperty);
        set => SetValue(OffTrackColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the thumb color.
    /// </summary>
    public SKColor ThumbColor
    {
        get => (SKColor)GetValue(ThumbColorProperty);
        set => SetValue(ThumbColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the disabled color.
    /// </summary>
    public SKColor DisabledColor
    {
        get => (SKColor)GetValue(DisabledColorProperty);
        set => SetValue(DisabledColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the track width.
    /// </summary>
    public float TrackWidth
    {
        get => (float)GetValue(TrackWidthProperty);
        set => SetValue(TrackWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the track height.
    /// </summary>
    public float TrackHeight
    {
        get => (float)GetValue(TrackHeightProperty);
        set => SetValue(TrackHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the thumb radius.
    /// </summary>
    public float ThumbRadius
    {
        get => (float)GetValue(ThumbRadiusProperty);
        set => SetValue(ThumbRadiusProperty, value);
    }

    /// <summary>
    /// Gets or sets the thumb padding.
    /// </summary>
    public float ThumbPadding
    {
        get => (float)GetValue(ThumbPaddingProperty);
        set => SetValue(ThumbPaddingProperty, value);
    }

    #endregion

    private float _animationProgress;

    /// <summary>
    /// Event raised when the switch is toggled.
    /// </summary>
    public event EventHandler<ToggledEventArgs>? Toggled;

    public SkiaSwitch()
    {
        IsFocusable = true;
    }

    private void OnIsOnChanged()
    {
        _animationProgress = IsOn ? 1f : 0f;
        Toggled?.Invoke(this, new ToggledEventArgs(IsOn));
        SkiaVisualStateManager.GoToState(this, IsOn ? "On" : "Off");
        Invalidate();
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var centerY = bounds.MidY;
        var trackLeft = bounds.MidX - TrackWidth / 2f;
        var trackRight = trackLeft + TrackWidth;

        // Calculate thumb position
        var thumbMinX = trackLeft + ThumbPadding + ThumbRadius;
        var thumbMaxX = trackRight - ThumbPadding - ThumbRadius;
        var thumbX = thumbMinX + _animationProgress * (thumbMaxX - thumbMinX);

        // Interpolate track color
        var trackColor = IsEnabled
            ? InterpolateColor(OffTrackColor, OnTrackColor, _animationProgress)
            : DisabledColor;

        // Draw track
        using var trackPaint = new SKPaint
        {
            Color = trackColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        var trackRect = new SKRoundRect(
            new SKRect(trackLeft, centerY - TrackHeight / 2f, trackRight, centerY + TrackHeight / 2f),
            TrackHeight / 2f);
        canvas.DrawRoundRect(trackRect, trackPaint);

        // Draw thumb shadow
        if (IsEnabled)
        {
            using var shadowPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 40),
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2f)
            };
            canvas.DrawCircle(thumbX + 1f, centerY + 1f, ThumbRadius, shadowPaint);
        }

        // Draw thumb
        using var thumbPaint = new SKPaint
        {
            Color = IsEnabled ? ThumbColor : new SKColor(245, 245, 245),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawCircle(thumbX, centerY, ThumbRadius, thumbPaint);

        // Draw focus ring
        if (IsFocused)
        {
            using var focusPaint = new SKPaint
            {
                Color = OnTrackColor.WithAlpha(60),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3f
            };
            var focusRect = new SKRoundRect(trackRect.Rect, TrackHeight / 2f);
            focusRect.Inflate(3f, 3f);
            canvas.DrawRoundRect(focusRect, focusPaint);
        }
    }

    private static SKColor InterpolateColor(SKColor from, SKColor to, float t)
    {
        return new SKColor(
            (byte)(from.Red + (to.Red - from.Red) * t),
            (byte)(from.Green + (to.Green - from.Green) * t),
            (byte)(from.Blue + (to.Blue - from.Blue) * t),
            (byte)(from.Alpha + (to.Alpha - from.Alpha) * t));
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (IsEnabled)
        {
            IsOn = !IsOn;
            e.Handled = true;
        }
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (IsEnabled && (e.Key == Key.Space || e.Key == Key.Enter))
        {
            IsOn = !IsOn;
            e.Handled = true;
        }
    }

    protected override void OnEnabledChanged()
    {
        base.OnEnabledChanged();
        SkiaVisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled");
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return new SKSize(TrackWidth + 8f, TrackHeight + 8f);
    }
}
