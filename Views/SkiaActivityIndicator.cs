// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered activity indicator (spinner) control with full XAML styling support.
/// </summary>
public class SkiaActivityIndicator : SkiaView
{
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
            propertyChanged: (b, o, n) => ((SkiaActivityIndicator)b).OnIsRunningChanged());

    /// <summary>
    /// Bindable property for Color.
    /// </summary>
    public static readonly BindableProperty ColorProperty =
        BindableProperty.Create(
            nameof(Color),
            typeof(SKColor),
            typeof(SkiaActivityIndicator),
            new SKColor(0x21, 0x96, 0xF3),
            propertyChanged: (b, o, n) => ((SkiaActivityIndicator)b).Invalidate());

    /// <summary>
    /// Bindable property for DisabledColor.
    /// </summary>
    public static readonly BindableProperty DisabledColorProperty =
        BindableProperty.Create(
            nameof(DisabledColor),
            typeof(SKColor),
            typeof(SkiaActivityIndicator),
            new SKColor(0xBD, 0xBD, 0xBD),
            propertyChanged: (b, o, n) => ((SkiaActivityIndicator)b).Invalidate());

    /// <summary>
    /// Bindable property for Size.
    /// </summary>
    public static readonly BindableProperty SizeProperty =
        BindableProperty.Create(
            nameof(Size),
            typeof(float),
            typeof(SkiaActivityIndicator),
            32f,
            propertyChanged: (b, o, n) => ((SkiaActivityIndicator)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for StrokeWidth.
    /// </summary>
    public static readonly BindableProperty StrokeWidthProperty =
        BindableProperty.Create(
            nameof(StrokeWidth),
            typeof(float),
            typeof(SkiaActivityIndicator),
            3f,
            propertyChanged: (b, o, n) => ((SkiaActivityIndicator)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for RotationSpeed.
    /// </summary>
    public static readonly BindableProperty RotationSpeedProperty =
        BindableProperty.Create(
            nameof(RotationSpeed),
            typeof(float),
            typeof(SkiaActivityIndicator),
            360f);

    /// <summary>
    /// Bindable property for ArcCount.
    /// </summary>
    public static readonly BindableProperty ArcCountProperty =
        BindableProperty.Create(
            nameof(ArcCount),
            typeof(int),
            typeof(SkiaActivityIndicator),
            12,
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
    public SKColor Color
    {
        get => (SKColor)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
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
    /// Gets or sets the indicator size.
    /// </summary>
    public float Size
    {
        get => (float)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the stroke width.
    /// </summary>
    public float StrokeWidth
    {
        get => (float)GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the rotation speed in degrees per second.
    /// </summary>
    public float RotationSpeed
    {
        get => (float)GetValue(RotationSpeedProperty);
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

    private float _rotationAngle;
    private DateTime _lastUpdateTime = DateTime.UtcNow;

    private void OnIsRunningChanged()
    {
        if (IsRunning)
        {
            _lastUpdateTime = DateTime.UtcNow;
        }
        Invalidate();
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        if (!IsRunning && !IsEnabled)
        {
            return;
        }

        var centerX = bounds.MidX;
        var centerY = bounds.MidY;
        var radius = Math.Min(Size / 2, Math.Min(bounds.Width, bounds.Height) / 2) - StrokeWidth;

        // Update rotation
        if (IsRunning)
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastUpdateTime).TotalSeconds;
            _lastUpdateTime = now;
            _rotationAngle = (_rotationAngle + (float)(RotationSpeed * elapsed)) % 360;
        }

        canvas.Save();
        canvas.Translate(centerX, centerY);
        canvas.RotateDegrees(_rotationAngle);

        var color = IsEnabled ? Color : DisabledColor;

        // Draw arcs with varying opacity
        for (int i = 0; i < ArcCount; i++)
        {
            var alpha = (byte)(255 * (1 - (float)i / ArcCount));
            var arcColor = color.WithAlpha(alpha);

            using var paint = new SKPaint
            {
                Color = arcColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = StrokeWidth,
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

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return new SKSize(Size + StrokeWidth * 2, Size + StrokeWidth * 2);
    }
}
