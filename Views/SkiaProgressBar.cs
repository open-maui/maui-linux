// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered progress bar control with full XAML styling support.
/// </summary>
public class SkiaProgressBar : SkiaView
{
    #region BindableProperties

    /// <summary>
    /// Bindable property for Progress.
    /// </summary>
    public static readonly BindableProperty ProgressProperty =
        BindableProperty.Create(
            nameof(Progress),
            typeof(double),
            typeof(SkiaProgressBar),
            0.0,
            BindingMode.TwoWay,
            coerceValue: (b, v) => Math.Clamp((double)v, 0, 1),
            propertyChanged: (b, o, n) => ((SkiaProgressBar)b).OnProgressChanged());

    /// <summary>
    /// Bindable property for TrackColor.
    /// </summary>
    public static readonly BindableProperty TrackColorProperty =
        BindableProperty.Create(
            nameof(TrackColor),
            typeof(SKColor),
            typeof(SkiaProgressBar),
            new SKColor(0xE0, 0xE0, 0xE0),
            propertyChanged: (b, o, n) => ((SkiaProgressBar)b).Invalidate());

    /// <summary>
    /// Bindable property for ProgressColor.
    /// </summary>
    public static readonly BindableProperty ProgressColorProperty =
        BindableProperty.Create(
            nameof(ProgressColor),
            typeof(SKColor),
            typeof(SkiaProgressBar),
            new SKColor(0x21, 0x96, 0xF3),
            propertyChanged: (b, o, n) => ((SkiaProgressBar)b).Invalidate());

    /// <summary>
    /// Bindable property for DisabledColor.
    /// </summary>
    public static readonly BindableProperty DisabledColorProperty =
        BindableProperty.Create(
            nameof(DisabledColor),
            typeof(SKColor),
            typeof(SkiaProgressBar),
            new SKColor(0xBD, 0xBD, 0xBD),
            propertyChanged: (b, o, n) => ((SkiaProgressBar)b).Invalidate());

    /// <summary>
    /// Bindable property for BarHeight.
    /// </summary>
    public static readonly BindableProperty BarHeightProperty =
        BindableProperty.Create(
            nameof(BarHeight),
            typeof(float),
            typeof(SkiaProgressBar),
            4f,
            propertyChanged: (b, o, n) => ((SkiaProgressBar)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for CornerRadius.
    /// </summary>
    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(
            nameof(CornerRadius),
            typeof(float),
            typeof(SkiaProgressBar),
            2f,
            propertyChanged: (b, o, n) => ((SkiaProgressBar)b).Invalidate());

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the progress value (0.0 to 1.0).
    /// </summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
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
    /// Gets or sets the progress color.
    /// </summary>
    public SKColor ProgressColor
    {
        get => (SKColor)GetValue(ProgressColorProperty);
        set => SetValue(ProgressColorProperty, value);
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
    /// Gets or sets the bar height.
    /// </summary>
    public float BarHeight
    {
        get => (float)GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the corner radius.
    /// </summary>
    public float CornerRadius
    {
        get => (float)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    #endregion

    /// <summary>
    /// Event raised when progress changes.
    /// </summary>
    public event EventHandler<ProgressChangedEventArgs>? ProgressChanged;

    private void OnProgressChanged()
    {
        ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(Progress));
        Invalidate();
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var trackY = bounds.MidY;
        var trackTop = trackY - BarHeight / 2;
        var trackBottom = trackY + BarHeight / 2;

        // Draw track
        using var trackPaint = new SKPaint
        {
            Color = IsEnabled ? TrackColor : DisabledColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        var trackRect = new SKRoundRect(
            new SKRect(bounds.Left, trackTop, bounds.Right, trackBottom),
            CornerRadius);
        canvas.DrawRoundRect(trackRect, trackPaint);

        // Draw progress
        if (Progress > 0)
        {
            var progressWidth = bounds.Width * (float)Progress;

            using var progressPaint = new SKPaint
            {
                Color = IsEnabled ? ProgressColor : DisabledColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            var progressRect = new SKRoundRect(
                new SKRect(bounds.Left, trackTop, bounds.Left + progressWidth, trackBottom),
                CornerRadius);
            canvas.DrawRoundRect(progressRect, progressPaint);
        }
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return new SKSize(200, BarHeight + 8);
    }
}

/// <summary>
/// Event args for progress changed events.
/// </summary>
public class ProgressChangedEventArgs : EventArgs
{
    public double Progress { get; }
    public ProgressChangedEventArgs(double progress) => Progress = progress;
}
