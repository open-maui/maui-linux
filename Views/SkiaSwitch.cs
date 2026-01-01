// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSwitch)b).OnIsOnChanged());

    /// <summary>
    /// Bindable property for OnTrackColor.
    /// </summary>
    public static readonly BindableProperty OnTrackColorProperty =
        BindableProperty.Create(
            nameof(OnTrackColor),
            typeof(SKColor),
            typeof(SkiaSwitch),
            new SKColor(0x21, 0x96, 0xF3),
            propertyChanged: (b, o, n) => ((SkiaSwitch)b).Invalidate());

    /// <summary>
    /// Bindable property for OffTrackColor.
    /// </summary>
    public static readonly BindableProperty OffTrackColorProperty =
        BindableProperty.Create(
            nameof(OffTrackColor),
            typeof(SKColor),
            typeof(SkiaSwitch),
            new SKColor(0x9E, 0x9E, 0x9E),
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
            propertyChanged: (b, o, n) => ((SkiaSwitch)b).Invalidate());

    /// <summary>
    /// Bindable property for DisabledColor.
    /// </summary>
    public static readonly BindableProperty DisabledColorProperty =
        BindableProperty.Create(
            nameof(DisabledColor),
            typeof(SKColor),
            typeof(SkiaSwitch),
            new SKColor(0xBD, 0xBD, 0xBD),
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

    private float _animationProgress; // 0 = off, 1 = on

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
        SkiaVisualStateManager.GoToState(this, IsOn ? SkiaVisualStateManager.CommonStates.On : SkiaVisualStateManager.CommonStates.Off);
        Invalidate();
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var centerY = bounds.MidY;
        var trackLeft = bounds.MidX - TrackWidth / 2;
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
            new SKRect(trackLeft, centerY - TrackHeight / 2, trackRight, centerY + TrackHeight / 2),
            TrackHeight / 2);
        canvas.DrawRoundRect(trackRect, trackPaint);

        // Draw thumb shadow
        if (IsEnabled)
        {
            using var shadowPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 40),
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2)
            };
            canvas.DrawCircle(thumbX + 1, centerY + 1, ThumbRadius, shadowPaint);
        }

        // Draw thumb
        using var thumbPaint = new SKPaint
        {
            Color = IsEnabled ? ThumbColor : new SKColor(0xF5, 0xF5, 0xF5),
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
                StrokeWidth = 3
            };
            var focusRect = new SKRoundRect(trackRect.Rect, TrackHeight / 2);
            focusRect.Inflate(3, 3);
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
        if (!IsEnabled) return;
        IsOn = !IsOn;
        e.Handled = true;
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        // Toggle handled in OnPointerPressed
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        if (e.Key == Key.Space || e.Key == Key.Enter)
        {
            IsOn = !IsOn;
            e.Handled = true;
        }
    }

    protected override void OnEnabledChanged()
    {
        base.OnEnabledChanged();
        SkiaVisualStateManager.GoToState(this, IsEnabled ? SkiaVisualStateManager.CommonStates.Normal : SkiaVisualStateManager.CommonStates.Disabled);
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return new SKSize(TrackWidth + 8, TrackHeight + 8);
    }
}

/// <summary>
/// Event args for toggled events.
/// </summary>
public class ToggledEventArgs : EventArgs
{
    public bool Value { get; }
    public ToggledEventArgs(bool value) => Value = value;
}
