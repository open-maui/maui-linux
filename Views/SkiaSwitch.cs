// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered toggle switch control with full MAUI compliance.
/// Implements ISwitch interface requirements:
/// - IsOn property with Toggled event
/// - OnColor (TrackColor when on)
/// - ThumbColor
/// - Smooth animation on toggle
/// </summary>
public class SkiaSwitch : SkiaView
{
    #region SKColor Helper

    private static SKColor ToSKColor(Color? color)
    {
        if (color == null) return SKColors.Transparent;
        return color.ToSKColor();
    }

    #endregion

    #region BindableProperties

    public static readonly BindableProperty IsOnProperty =
        BindableProperty.Create(
            nameof(IsOn),
            typeof(bool),
            typeof(SkiaSwitch),
            false,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSwitch)b).OnIsOnChanged((bool)o, (bool)n));

    public static readonly BindableProperty OnTrackColorProperty =
        BindableProperty.Create(
            nameof(OnTrackColor),
            typeof(Color),
            typeof(SkiaSwitch),
            Color.FromRgb(33, 150, 243), // Material Blue
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSwitch)b).Invalidate());

    public static readonly BindableProperty OffTrackColorProperty =
        BindableProperty.Create(
            nameof(OffTrackColor),
            typeof(Color),
            typeof(SkiaSwitch),
            Color.FromRgb(158, 158, 158),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSwitch)b).Invalidate());

    public static readonly BindableProperty ThumbColorProperty =
        BindableProperty.Create(
            nameof(ThumbColor),
            typeof(Color),
            typeof(SkiaSwitch),
            Colors.White,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSwitch)b).Invalidate());

    public static readonly BindableProperty DisabledColorProperty =
        BindableProperty.Create(
            nameof(DisabledColor),
            typeof(Color),
            typeof(SkiaSwitch),
            Color.FromRgb(189, 189, 189),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSwitch)b).Invalidate());

    public static readonly BindableProperty TrackWidthProperty =
        BindableProperty.Create(
            nameof(TrackWidth),
            typeof(double),
            typeof(SkiaSwitch),
            52.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSwitch)b).InvalidateMeasure());

    public static readonly BindableProperty TrackHeightProperty =
        BindableProperty.Create(
            nameof(TrackHeight),
            typeof(double),
            typeof(SkiaSwitch),
            32.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSwitch)b).InvalidateMeasure());

    public static readonly BindableProperty ThumbRadiusProperty =
        BindableProperty.Create(
            nameof(ThumbRadius),
            typeof(double),
            typeof(SkiaSwitch),
            12.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaSwitch)b).Invalidate());

    public static readonly BindableProperty ThumbPaddingProperty =
        BindableProperty.Create(
            nameof(ThumbPadding),
            typeof(double),
            typeof(SkiaSwitch),
            4.0,
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
    /// Gets or sets the track color when the switch is on.
    /// This is the primary MAUI Switch.OnColor property.
    /// </summary>
    public Color OnTrackColor
    {
        get => (Color)GetValue(OnTrackColorProperty);
        set => SetValue(OnTrackColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the track color when the switch is off.
    /// </summary>
    public Color OffTrackColor
    {
        get => (Color)GetValue(OffTrackColorProperty);
        set => SetValue(OffTrackColorProperty, value);
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
    /// Gets or sets the color used when the control is disabled.
    /// </summary>
    public Color DisabledColor
    {
        get => (Color)GetValue(DisabledColorProperty);
        set => SetValue(DisabledColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the track width in device-independent units.
    /// </summary>
    public double TrackWidth
    {
        get => (double)GetValue(TrackWidthProperty);
        set => SetValue(TrackWidthProperty, value);
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
    /// Gets or sets the thumb padding in device-independent units.
    /// </summary>
    public double ThumbPadding
    {
        get => (double)GetValue(ThumbPaddingProperty);
        set => SetValue(ThumbPaddingProperty, value);
    }

    #endregion

    #region Animation Fields

    private float _animationProgress;
    private System.Timers.Timer? _animationTimer;
    private bool _animatingToOn;
    private const int AnimationDurationMs = 200;
    private const int AnimationFrameMs = 16; // ~60fps

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the switch is toggled.
    /// </summary>
    public event EventHandler<ToggledEventArgs>? Toggled;

    #endregion

    #region Constructor

    public SkiaSwitch()
    {
        IsFocusable = true;
        _animationProgress = 0f;
    }

    #endregion

    #region Event Handlers

    private void OnIsOnChanged(bool oldValue, bool newValue)
    {
        // Start animation
        StartAnimation(newValue);

        Toggled?.Invoke(this, new ToggledEventArgs(newValue));
        SkiaVisualStateManager.GoToState(this, newValue ? "On" : "Off");
    }

    #endregion

    #region Animation

    private void StartAnimation(bool toOn)
    {
        _animatingToOn = toOn;

        // Stop existing animation
        _animationTimer?.Stop();
        _animationTimer?.Dispose();

        // Create new animation timer
        _animationTimer = new System.Timers.Timer(AnimationFrameMs);
        _animationTimer.Elapsed += OnAnimationFrame;
        _animationTimer.AutoReset = true;
        _animationTimer.Start();
    }

    private void OnAnimationFrame(object? sender, System.Timers.ElapsedEventArgs e)
    {
        float step = AnimationFrameMs / (float)AnimationDurationMs;

        if (_animatingToOn)
        {
            _animationProgress += step;
            if (_animationProgress >= 1f)
            {
                _animationProgress = 1f;
                StopAnimation();
            }
        }
        else
        {
            _animationProgress -= step;
            if (_animationProgress <= 0f)
            {
                _animationProgress = 0f;
                StopAnimation();
            }
        }

        // Request redraw on UI thread
        Invalidate();
    }

    private void StopAnimation()
    {
        _animationTimer?.Stop();
        _animationTimer?.Dispose();
        _animationTimer = null;
    }

    #endregion

    #region Rendering

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var trackWidth = (float)TrackWidth;
        var trackHeight = (float)TrackHeight;
        var thumbRadius = (float)ThumbRadius;
        var thumbPadding = (float)ThumbPadding;

        var centerY = bounds.MidY;
        var trackLeft = bounds.MidX - trackWidth / 2f;
        var trackRight = trackLeft + trackWidth;

        // Calculate thumb position based on animation progress
        var thumbMinX = trackLeft + thumbPadding + thumbRadius;
        var thumbMaxX = trackRight - thumbPadding - thumbRadius;
        var thumbX = thumbMinX + _animationProgress * (thumbMaxX - thumbMinX);

        // Get colors
        var onColorSK = ToSKColor(OnTrackColor);
        var offColorSK = ToSKColor(OffTrackColor);
        var thumbColorSK = ToSKColor(ThumbColor);
        var disabledColorSK = ToSKColor(DisabledColor);

        // Interpolate track color based on animation progress
        var trackColor = IsEnabled
            ? InterpolateColor(offColorSK, onColorSK, _animationProgress)
            : disabledColorSK;

        // Draw track
        using var trackPaint = new SKPaint
        {
            Color = trackColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        var trackRect = new SKRoundRect(
            new SKRect(trackLeft, centerY - trackHeight / 2f, trackRight, centerY + trackHeight / 2f),
            trackHeight / 2f);
        canvas.DrawRoundRect(trackRect, trackPaint);

        // Draw thumb shadow (only when enabled)
        if (IsEnabled)
        {
            using var shadowPaint = new SKPaint
            {
                Color = SkiaTheme.Shadow25SK,
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2f)
            };
            canvas.DrawCircle(thumbX + 1f, centerY + 1f, thumbRadius, shadowPaint);
        }

        // Draw thumb
        using var thumbPaint = new SKPaint
        {
            Color = IsEnabled ? thumbColorSK : SkiaTheme.Gray100SK,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawCircle(thumbX, centerY, thumbRadius, thumbPaint);

        // Draw focus ring
        if (IsFocused)
        {
            using var focusPaint = new SKPaint
            {
                Color = onColorSK.WithAlpha(60),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3f
            };
            var focusRect = new SKRoundRect(trackRect.Rect, trackHeight / 2f);
            focusRect.Inflate(3f, 3f);
            canvas.DrawRoundRect(focusRect, focusPaint);
        }
    }

    private static SKColor InterpolateColor(SKColor from, SKColor to, float t)
    {
        // Clamp t to [0, 1]
        t = Math.Max(0f, Math.Min(1f, t));

        return new SKColor(
            (byte)(from.Red + (to.Red - from.Red) * t),
            (byte)(from.Green + (to.Green - from.Green) * t),
            (byte)(from.Blue + (to.Blue - from.Blue) * t),
            (byte)(from.Alpha + (to.Alpha - from.Alpha) * t));
    }

    #endregion

    #region Pointer Events

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
        // No action needed
    }

    #endregion

    #region Keyboard Events

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (IsEnabled && (e.Key == Key.Space || e.Key == Key.Enter))
        {
            IsOn = !IsOn;
            e.Handled = true;
        }
    }

    #endregion

    #region Lifecycle

    protected override void OnEnabledChanged()
    {
        base.OnEnabledChanged();
        SkiaVisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopAnimation();
        }
        base.Dispose(disposing);
    }

    #endregion

    #region Layout

    protected override Size MeasureOverride(Size availableSize)
    {
        var trackWidth = TrackWidth;
        var trackHeight = TrackHeight;
        return new Size(trackWidth + 8, trackHeight + 8);
    }

    #endregion
}
