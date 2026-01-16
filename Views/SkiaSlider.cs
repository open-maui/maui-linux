// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered slider control with full MAUI compliance.
/// Implements ISlider interface requirements:
/// - Minimum, Maximum, Value properties
/// - MinimumTrackColor, MaximumTrackColor, ThumbColor
/// - ValueChanged, DragStarted, DragCompleted events
/// </summary>
public class SkiaSlider : SkiaView
{
    #region SKColor Helper

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

    #region BindableProperties

    public static readonly BindableProperty MinimumProperty =
        BindableProperty.Create(
            nameof(Minimum),
            typeof(double),
            typeof(SkiaSlider),
            0.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSlider)b).OnRangeChanged());

    public static readonly BindableProperty MaximumProperty =
        BindableProperty.Create(
            nameof(Maximum),
            typeof(double),
            typeof(SkiaSlider),
            100.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSlider)b).OnRangeChanged());

    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(
            nameof(Value),
            typeof(double),
            typeof(SkiaSlider),
            0.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSlider)b).OnValuePropertyChanged((double)o, (double)n));

    public static readonly BindableProperty MinimumTrackColorProperty =
        BindableProperty.Create(
            nameof(MinimumTrackColor),
            typeof(Color),
            typeof(SkiaSlider),
            Color.FromRgb(0x21, 0x96, 0xF3), // Material Blue - active track
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSlider)b).Invalidate());

    public static readonly BindableProperty MaximumTrackColorProperty =
        BindableProperty.Create(
            nameof(MaximumTrackColor),
            typeof(Color),
            typeof(SkiaSlider),
            Color.FromRgb(0xE0, 0xE0, 0xE0), // Gray - inactive track
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSlider)b).Invalidate());

    public static readonly BindableProperty ThumbColorProperty =
        BindableProperty.Create(
            nameof(ThumbColor),
            typeof(Color),
            typeof(SkiaSlider),
            Color.FromRgb(0x21, 0x96, 0xF3), // Material Blue
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSlider)b).Invalidate());

    public static readonly BindableProperty DisabledColorProperty =
        BindableProperty.Create(
            nameof(DisabledColor),
            typeof(Color),
            typeof(SkiaSlider),
            Color.FromRgb(0xBD, 0xBD, 0xBD),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSlider)b).Invalidate());

    public static readonly BindableProperty TrackHeightProperty =
        BindableProperty.Create(
            nameof(TrackHeight),
            typeof(double),
            typeof(SkiaSlider),
            4.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSlider)b).Invalidate());

    public static readonly BindableProperty ThumbRadiusProperty =
        BindableProperty.Create(
            nameof(ThumbRadius),
            typeof(double),
            typeof(SkiaSlider),
            10.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSlider)b).InvalidateMeasure());

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the minimum value.
    /// </summary>
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum value.
    /// </summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>
    /// Gets or sets the current value.
    /// </summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, Math.Clamp(value, Minimum, Maximum));
    }

    /// <summary>
    /// Gets or sets the color of the track from minimum to current value.
    /// This is the "active" or "filled" portion of the track.
    /// </summary>
    public Color MinimumTrackColor
    {
        get => (Color)GetValue(MinimumTrackColorProperty);
        set => SetValue(MinimumTrackColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the color of the track from current value to maximum.
    /// This is the "inactive" or "unfilled" portion of the track.
    /// </summary>
    public Color MaximumTrackColor
    {
        get => (Color)GetValue(MaximumTrackColorProperty);
        set => SetValue(MaximumTrackColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the thumb color.
    /// </summary>
    public Color ThumbColor
    {
        get => (Color)GetValue(ThumbColorProperty);
        set => SetValue(ThumbColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the color used when disabled.
    /// </summary>
    public Color DisabledColor
    {
        get => (Color)GetValue(DisabledColorProperty);
        set => SetValue(DisabledColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the track height in device-independent units.
    /// </summary>
    public double TrackHeight
    {
        get => (double)GetValue(TrackHeightProperty);
        set => SetValue(TrackHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the thumb radius in device-independent units.
    /// </summary>
    public double ThumbRadius
    {
        get => (double)GetValue(ThumbRadiusProperty);
        set => SetValue(ThumbRadiusProperty, value);
    }

    /// <summary>
    /// Gets whether the slider is currently being dragged.
    /// </summary>
    public bool IsDragging { get; private set; }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the value changes.
    /// </summary>
    public event EventHandler<ValueChangedEventArgs>? ValueChanged;

    /// <summary>
    /// Event raised when drag starts.
    /// </summary>
    public event EventHandler? DragStarted;

    /// <summary>
    /// Event raised when drag completes.
    /// </summary>
    public event EventHandler? DragCompleted;

    #endregion

    #region Constructor

    public SkiaSlider()
    {
        IsFocusable = true;
    }

    #endregion

    #region Event Handlers

    private void OnRangeChanged()
    {
        // Clamp value to new range
        var clamped = Math.Clamp(Value, Minimum, Maximum);
        if (Math.Abs(Value - clamped) > double.Epsilon)
        {
            Value = clamped;
        }
        Invalidate();
    }

    private void OnValuePropertyChanged(double oldValue, double newValue)
    {
        ValueChanged?.Invoke(this, new ValueChangedEventArgs(oldValue, newValue));
        Invalidate();
    }

    #endregion

    #region Rendering

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var trackHeight = (float)TrackHeight;
        var thumbRadius = (float)ThumbRadius;

        var trackY = bounds.MidY;
        var trackLeft = bounds.Left + thumbRadius;
        var trackRight = bounds.Right - thumbRadius;
        var trackWidth = trackRight - trackLeft;

        var percentage = Maximum > Minimum ? (Value - Minimum) / (Maximum - Minimum) : 0;
        var thumbX = trackLeft + (float)(percentage * trackWidth);

        // Get colors
        var minTrackColorSK = ToSKColor(MinimumTrackColor);
        var maxTrackColorSK = ToSKColor(MaximumTrackColor);
        var thumbColorSK = ToSKColor(ThumbColor);
        var disabledColorSK = ToSKColor(DisabledColor);

        // Draw inactive (maximum) track
        using var inactiveTrackPaint = new SKPaint
        {
            Color = IsEnabled ? maxTrackColorSK : disabledColorSK,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        var inactiveRect = new SKRoundRect(
            new SKRect(trackLeft, trackY - trackHeight / 2, trackRight, trackY + trackHeight / 2),
            trackHeight / 2);
        canvas.DrawRoundRect(inactiveRect, inactiveTrackPaint);

        // Draw active (minimum) track
        if (percentage > 0)
        {
            using var activeTrackPaint = new SKPaint
            {
                Color = IsEnabled ? minTrackColorSK : disabledColorSK,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            var activeRect = new SKRoundRect(
                new SKRect(trackLeft, trackY - trackHeight / 2, thumbX, trackY + trackHeight / 2),
                trackHeight / 2);
            canvas.DrawRoundRect(activeRect, activeTrackPaint);
        }

        // Draw focus ring behind thumb
        if (IsFocused)
        {
            using var focusPaint = new SKPaint
            {
                Color = thumbColorSK.WithAlpha(60),
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawCircle(thumbX, trackY, thumbRadius + 8, focusPaint);
        }

        // Draw thumb shadow
        if (IsEnabled)
        {
            using var shadowPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 30),
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3)
            };
            canvas.DrawCircle(thumbX + 1, trackY + 2, thumbRadius, shadowPaint);
        }

        // Draw thumb
        using var thumbPaint = new SKPaint
        {
            Color = IsEnabled ? thumbColorSK : disabledColorSK,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawCircle(thumbX, trackY, thumbRadius, thumbPaint);

        // Draw pressed state (larger thumb when dragging)
        if (IsDragging)
        {
            using var pressedPaint = new SKPaint
            {
                Color = thumbColorSK.WithAlpha(40),
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawCircle(thumbX, trackY, thumbRadius + 4, pressedPaint);
        }
    }

    #endregion

    #region Pointer Events

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        IsDragging = true;
        UpdateValueFromPosition(e.X);
        DragStarted?.Invoke(this, EventArgs.Empty);
        SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.Pressed);
        e.Handled = true;
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!IsEnabled || !IsDragging) return;
        UpdateValueFromPosition(e.X);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (IsDragging)
        {
            IsDragging = false;
            DragCompleted?.Invoke(this, EventArgs.Empty);
            SkiaVisualStateManager.GoToState(this, IsEnabled
                ? SkiaVisualStateManager.CommonStates.Normal
                : SkiaVisualStateManager.CommonStates.Disabled);
            Invalidate();
        }
    }

    private void UpdateValueFromPosition(float x)
    {
        var thumbRadius = (float)ThumbRadius;
        var trackLeft = Bounds.Left + thumbRadius;
        var trackRight = Bounds.Right - thumbRadius;
        var trackWidth = trackRight - trackLeft;

        if (trackWidth <= 0) return;

        var percentage = Math.Clamp((x - trackLeft) / trackWidth, 0, 1);
        Value = Minimum + percentage * (Maximum - Minimum);
    }

    #endregion

    #region Keyboard Events

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        var step = (Maximum - Minimum) / 100; // 1% steps
        var largeStep = step * 10; // 10% for arrow keys

        switch (e.Key)
        {
            case Key.Left:
            case Key.Down:
                Value -= largeStep;
                e.Handled = true;
                break;
            case Key.Right:
            case Key.Up:
                Value += largeStep;
                e.Handled = true;
                break;
            case Key.Home:
                Value = Minimum;
                e.Handled = true;
                break;
            case Key.End:
                Value = Maximum;
                e.Handled = true;
                break;
            case Key.PageDown:
                Value -= (Maximum - Minimum) * 0.1; // 10%
                e.Handled = true;
                break;
            case Key.PageUp:
                Value += (Maximum - Minimum) * 0.1; // 10%
                e.Handled = true;
                break;
        }
    }

    #endregion

    #region Lifecycle

    protected override void OnEnabledChanged()
    {
        base.OnEnabledChanged();
        SkiaVisualStateManager.GoToState(this, IsEnabled
            ? SkiaVisualStateManager.CommonStates.Normal
            : SkiaVisualStateManager.CommonStates.Disabled);
    }

    #endregion

    #region Layout

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        var thumbRadius = (float)ThumbRadius;
        return new SKSize(200, thumbRadius * 2 + 16);
    }

    #endregion
}
