// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered activity indicator (spinner) control with full MAUI compliance.
/// Implements IActivityIndicator interface requirements:
/// - IsRunning property to start/stop animation
/// - Color property for the indicator color
/// </summary>
public class SkiaActivityIndicator : SkiaView
{
    #region SKColor Helper

    private static SKColor ToSKColor(Color? color)
    {
        if (color == null) return SKColors.Transparent;
        return color.ToSKColor();
    }

    #endregion

    #region BindableProperties

    /// <summary>
    /// Bindable property for IsRunning.
    /// </summary>
    public static readonly BindableProperty IsRunningProperty =
        BindableProperty.Create(
            nameof(IsRunning),
            typeof(bool),
            typeof(SkiaActivityIndicator),
            false,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaActivityIndicator)b).OnIsRunningChanged());

    /// <summary>
    /// Bindable property for Color.
    /// </summary>
    public static readonly BindableProperty ColorProperty =
        BindableProperty.Create(
            nameof(Color),
            typeof(Color),
            typeof(SkiaActivityIndicator),
            Color.FromRgb(0x21, 0x96, 0xF3), // Material Blue
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaActivityIndicator)b).Invalidate());

    /// <summary>
    /// Bindable property for DisabledColor.
    /// </summary>
    public static readonly BindableProperty DisabledColorProperty =
        BindableProperty.Create(
            nameof(DisabledColor),
            typeof(Color),
            typeof(SkiaActivityIndicator),
            Color.FromRgb(0xBD, 0xBD, 0xBD),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaActivityIndicator)b).Invalidate());

    /// <summary>
    /// Bindable property for Size.
    /// </summary>
    public static readonly BindableProperty SizeProperty =
        BindableProperty.Create(
            nameof(Size),
            typeof(double),
            typeof(SkiaActivityIndicator),
            32.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaActivityIndicator)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for StrokeWidth.
    /// </summary>
    public static readonly BindableProperty StrokeWidthProperty =
        BindableProperty.Create(
            nameof(StrokeWidth),
            typeof(double),
            typeof(SkiaActivityIndicator),
            3.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaActivityIndicator)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for RotationSpeed.
    /// </summary>
    public static readonly BindableProperty RotationSpeedProperty =
        BindableProperty.Create(
            nameof(RotationSpeed),
            typeof(double),
            typeof(SkiaActivityIndicator),
            360.0,
            BindingMode.TwoWay);

    /// <summary>
    /// Bindable property for ArcCount.
    /// </summary>
    public static readonly BindableProperty ArcCountProperty =
        BindableProperty.Create(
            nameof(ArcCount),
            typeof(int),
            typeof(SkiaActivityIndicator),
            12,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaActivityIndicator)b).Invalidate());

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets whether the indicator is running.
    /// </summary>
    public bool IsRunning
    {
        get => (bool)GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    /// <summary>
    /// Gets or sets the indicator color.
    /// </summary>
    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the disabled color.
    /// </summary>
    public Color DisabledColor
    {
        get => (Color)GetValue(DisabledColorProperty);
        set => SetValue(DisabledColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the indicator size.
    /// </summary>
    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the stroke width.
    /// </summary>
    public double StrokeWidth
    {
        get => (double)GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the rotation speed in degrees per second.
    /// </summary>
    public double RotationSpeed
    {
        get => (double)GetValue(RotationSpeedProperty);
        set => SetValue(RotationSpeedProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of arcs.
    /// </summary>
    public int ArcCount
    {
        get => (int)GetValue(ArcCountProperty);
        set => SetValue(ArcCountProperty, value);
    }

    #endregion

    #region Private Fields

    private float _rotationAngle;
    private DateTime _lastUpdateTime = DateTime.UtcNow;

    #endregion

    #region Event Handlers

    private void OnIsRunningChanged()
    {
        if (IsRunning)
        {
            _lastUpdateTime = DateTime.UtcNow;
        }
        Invalidate();
    }

    #endregion

    #region Rendering

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        if (!IsRunning && !IsEnabled)
        {
            return;
        }

        var size = (float)Size;
        var strokeWidth = (float)StrokeWidth;
        var rotationSpeed = (float)RotationSpeed;

        var centerX = bounds.MidX;
        var centerY = bounds.MidY;
        var radius = Math.Min(size / 2, Math.Min(bounds.Width, bounds.Height) / 2) - strokeWidth;

        // Update rotation
        if (IsRunning)
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastUpdateTime).TotalSeconds;
            _lastUpdateTime = now;
            _rotationAngle = (_rotationAngle + (float)(rotationSpeed * elapsed)) % 360;
        }

        canvas.Save();
        canvas.Translate(centerX, centerY);
        canvas.RotateDegrees(_rotationAngle);

        var colorSK = ToSKColor(IsEnabled ? Color : DisabledColor);

        // Draw arcs with varying opacity
        for (int i = 0; i < ArcCount; i++)
        {
            var alpha = (byte)(255 * (1 - (float)i / ArcCount));
            var arcColor = colorSK.WithAlpha(alpha);

            using var paint = new SKPaint
            {
                Color = arcColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = strokeWidth,
                StrokeCap = SKStrokeCap.Round
            };

            var startAngle = (360f / ArcCount) * i;
            var sweepAngle = 360f / ArcCount / 2;

            using var path = new SKPath();
            path.AddArc(
                new SKRect(-radius, -radius, radius, radius),
                startAngle,
                sweepAngle);

            canvas.DrawPath(path, paint);
        }

        canvas.Restore();

        // Request redraw for animation
        if (IsRunning)
        {
            Invalidate();
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

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = (float)Size;
        var strokeWidth = (float)StrokeWidth;
        return new Size(size + strokeWidth * 2, size + strokeWidth * 2);
    }

    #endregion
}
