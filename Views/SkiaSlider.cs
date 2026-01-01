// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered slider control with full XAML styling support.
/// </summary>
public class SkiaSlider : SkiaView
{
    #region BindableProperties

    /// <summary>
    /// Bindable property for Minimum.
    /// </summary>
    public static readonly BindableProperty MinimumProperty =
        BindableProperty.Create(
            nameof(Minimum),
            typeof(double),
            typeof(SkiaSlider),
            0.0,
            propertyChanged: (b, o, n) => ((SkiaSlider)b).OnRangeChanged());

    /// <summary>
    /// Bindable property for Maximum.
    /// </summary>
    public static readonly BindableProperty MaximumProperty =
        BindableProperty.Create(
            nameof(Maximum),
            typeof(double),
            typeof(SkiaSlider),
            100.0,
            propertyChanged: (b, o, n) => ((SkiaSlider)b).OnRangeChanged());

    /// <summary>
    /// Bindable property for Value.
    /// </summary>
    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(
            nameof(Value),
            typeof(double),
            typeof(SkiaSlider),
            0.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSlider)b).OnValuePropertyChanged((double)o, (double)n));

    /// <summary>
    /// Bindable property for TrackColor.
    /// </summary>
    public static readonly BindableProperty TrackColorProperty =
        BindableProperty.Create(
            nameof(TrackColor),
            typeof(SKColor),
            typeof(SkiaSlider),
            new SKColor(0xE0, 0xE0, 0xE0),
            propertyChanged: (b, o, n) => ((SkiaSlider)b).Invalidate());

    /// <summary>
    /// Bindable property for ActiveTrackColor.
    /// </summary>
    public static readonly BindableProperty ActiveTrackColorProperty =
        BindableProperty.Create(
            nameof(ActiveTrackColor),
            typeof(SKColor),
            typeof(SkiaSlider),
            new SKColor(0x21, 0x96, 0xF3),
            propertyChanged: (b, o, n) => ((SkiaSlider)b).Invalidate());

    /// <summary>
    /// Bindable property for ThumbColor.
    /// </summary>
    public static readonly BindableProperty ThumbColorProperty =
        BindableProperty.Create(
            nameof(ThumbColor),
            typeof(SKColor),
            typeof(SkiaSlider),
            new SKColor(0x21, 0x96, 0xF3),
            propertyChanged: (b, o, n) => ((SkiaSlider)b).Invalidate());

    /// <summary>
    /// Bindable property for DisabledColor.
    /// </summary>
    public static readonly BindableProperty DisabledColorProperty =
        BindableProperty.Create(
            nameof(DisabledColor),
            typeof(SKColor),
            typeof(SkiaSlider),
            new SKColor(0xBD, 0xBD, 0xBD),
            propertyChanged: (b, o, n) => ((SkiaSlider)b).Invalidate());

    /// <summary>
    /// Bindable property for TrackHeight.
    /// </summary>
    public static readonly BindableProperty TrackHeightProperty =
        BindableProperty.Create(
            nameof(TrackHeight),
            typeof(float),
            typeof(SkiaSlider),
            4f,
            propertyChanged: (b, o, n) => ((SkiaSlider)b).Invalidate());

    /// <summary>
    /// Bindable property for ThumbRadius.
    /// </summary>
    public static readonly BindableProperty ThumbRadiusProperty =
        BindableProperty.Create(
            nameof(ThumbRadius),
            typeof(float),
            typeof(SkiaSlider),
            10f,
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
    /// Gets or sets the track color.
    /// </summary>
    public SKColor TrackColor
    {
        get => (SKColor)GetValue(TrackColorProperty);
        set => SetValue(TrackColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the active track color.
    /// </summary>
    public SKColor ActiveTrackColor
    {
        get => (SKColor)GetValue(ActiveTrackColorProperty);
        set => SetValue(ActiveTrackColorProperty, value);
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

    #endregion

    private bool _isDragging;

    /// <summary>
    /// Event raised when the value changes.
    /// </summary>
    public event EventHandler<SliderValueChangedEventArgs>? ValueChanged;

    /// <summary>
    /// Event raised when drag starts.
    /// </summary>
    public event EventHandler? DragStarted;

    /// <summary>
    /// Event raised when drag completes.
    /// </summary>
    public event EventHandler? DragCompleted;

    public SkiaSlider()
    {
        IsFocusable = true;
    }

    private void OnRangeChanged()
    {
        // Clamp value to new range
        var clamped = Math.Clamp(Value, Minimum, Maximum);
        if (Value != clamped)
        {
            Value = clamped;
        }
        Invalidate();
    }

    private void OnValuePropertyChanged(double oldValue, double newValue)
    {
        ValueChanged?.Invoke(this, new SliderValueChangedEventArgs(newValue));
        Invalidate();
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var trackY = bounds.MidY;
        var trackLeft = bounds.Left + ThumbRadius;
        var trackRight = bounds.Right - ThumbRadius;
        var trackWidth = trackRight - trackLeft;

        var percentage = Maximum > Minimum ? (Value - Minimum) / (Maximum - Minimum) : 0;
        var thumbX = trackLeft + (float)(percentage * trackWidth);

        // Draw inactive track
        using var inactiveTrackPaint = new SKPaint
        {
            Color = IsEnabled ? TrackColor : DisabledColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        var inactiveRect = new SKRoundRect(
            new SKRect(trackLeft, trackY - TrackHeight / 2, trackRight, trackY + TrackHeight / 2),
            TrackHeight / 2);
        canvas.DrawRoundRect(inactiveRect, inactiveTrackPaint);

        // Draw active track
        if (percentage > 0)
        {
            using var activeTrackPaint = new SKPaint
            {
                Color = IsEnabled ? ActiveTrackColor : DisabledColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            var activeRect = new SKRoundRect(
                new SKRect(trackLeft, trackY - TrackHeight / 2, thumbX, trackY + TrackHeight / 2),
                TrackHeight / 2);
            canvas.DrawRoundRect(activeRect, activeTrackPaint);
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
            canvas.DrawCircle(thumbX + 1, trackY + 2, ThumbRadius, shadowPaint);
        }

        // Draw thumb
        using var thumbPaint = new SKPaint
        {
            Color = IsEnabled ? ThumbColor : DisabledColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawCircle(thumbX, trackY, ThumbRadius, thumbPaint);

        // Draw focus ring
        if (IsFocused)
        {
            using var focusPaint = new SKPaint
            {
                Color = ThumbColor.WithAlpha(60),
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawCircle(thumbX, trackY, ThumbRadius + 8, focusPaint);
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        _isDragging = true;
        UpdateValueFromPosition(e.X);
        DragStarted?.Invoke(this, EventArgs.Empty);
        SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.Pressed);
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!IsEnabled || !_isDragging) return;
        UpdateValueFromPosition(e.X);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            DragCompleted?.Invoke(this, EventArgs.Empty);
            SkiaVisualStateManager.GoToState(this, IsEnabled ? SkiaVisualStateManager.CommonStates.Normal : SkiaVisualStateManager.CommonStates.Disabled);
        }
    }

    private void UpdateValueFromPosition(float x)
    {
        var trackLeft = Bounds.Left + ThumbRadius;
        var trackRight = Bounds.Right - ThumbRadius;
        var trackWidth = trackRight - trackLeft;

        var percentage = Math.Clamp((x - trackLeft) / trackWidth, 0, 1);
        Value = Minimum + percentage * (Maximum - Minimum);
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        var step = (Maximum - Minimum) / 100; // 1% steps

        switch (e.Key)
        {
            case Key.Left:
            case Key.Down:
                Value -= step * 10;
                e.Handled = true;
                break;
            case Key.Right:
            case Key.Up:
                Value += step * 10;
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
        }
    }

    protected override void OnEnabledChanged()
    {
        base.OnEnabledChanged();
        SkiaVisualStateManager.GoToState(this, IsEnabled ? SkiaVisualStateManager.CommonStates.Normal : SkiaVisualStateManager.CommonStates.Disabled);
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return new SKSize(200, ThumbRadius * 2 + 16);
    }
}

/// <summary>
/// Event args for slider value changed events.
/// </summary>
public class SliderValueChangedEventArgs : EventArgs
{
    public double NewValue { get; }
    public SliderValueChangedEventArgs(double newValue) => NewValue = newValue;
}
