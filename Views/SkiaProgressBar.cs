// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered progress bar control with full MAUI compliance.
/// Implements IProgress interface requirements:
/// - Progress property (0.0 to 1.0)
/// - ProgressColor for the filled portion
/// </summary>
public class SkiaProgressBar : SkiaView
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
    /// Bindable property for Progress.
    /// </summary>
    public static readonly BindableProperty ProgressProperty =
        BindableProperty.Create(
            nameof(Progress),
            typeof(double),
            typeof(SkiaProgressBar),
            0.0,
            BindingMode.TwoWay,
            coerceValue: (b, v) => Math.Clamp((double)v, 0.0, 1.0),
            propertyChanged: (b, o, n) => ((SkiaProgressBar)b).OnProgressChanged((double)o, (double)n));

    /// <summary>
    /// Bindable property for TrackColor (background track).
    /// </summary>
    public static readonly BindableProperty TrackColorProperty =
        BindableProperty.Create(
            nameof(TrackColor),
            typeof(Color),
            typeof(SkiaProgressBar),
            Color.FromRgb(0xE0, 0xE0, 0xE0),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaProgressBar)b).Invalidate());

    /// <summary>
    /// Bindable property for ProgressColor (filled portion).
    /// </summary>
    public static readonly BindableProperty ProgressColorProperty =
        BindableProperty.Create(
            nameof(ProgressColor),
            typeof(Color),
            typeof(SkiaProgressBar),
            Color.FromRgb(0x21, 0x96, 0xF3), // Material Blue
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaProgressBar)b).Invalidate());

    /// <summary>
    /// Bindable property for DisabledColor.
    /// </summary>
    public static readonly BindableProperty DisabledColorProperty =
        BindableProperty.Create(
            nameof(DisabledColor),
            typeof(Color),
            typeof(SkiaProgressBar),
            Color.FromRgb(0xBD, 0xBD, 0xBD),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaProgressBar)b).Invalidate());

    /// <summary>
    /// Bindable property for BarHeight.
    /// </summary>
    public static readonly BindableProperty BarHeightProperty =
        BindableProperty.Create(
            nameof(BarHeight),
            typeof(double),
            typeof(SkiaProgressBar),
            4.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaProgressBar)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for CornerRadius.
    /// </summary>
    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(
            nameof(CornerRadius),
            typeof(double),
            typeof(SkiaProgressBar),
            2.0,
            BindingMode.TwoWay,
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
    /// Gets or sets the track color (background).
    /// </summary>
    public Color TrackColor
    {
        get => (Color)GetValue(TrackColorProperty);
        set => SetValue(TrackColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the progress color (filled portion).
    /// </summary>
    public Color ProgressColor
    {
        get => (Color)GetValue(ProgressColorProperty);
        set => SetValue(ProgressColorProperty, value);
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
    /// Gets or sets the bar height.
    /// </summary>
    public double BarHeight
    {
        get => (double)GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the corner radius.
    /// </summary>
    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when progress changes.
    /// </summary>
    public event EventHandler<ProgressChangedEventArgs>? ProgressChanged;

    #endregion

    #region Event Handlers

    private void OnProgressChanged(double oldValue, double newValue)
    {
        ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(oldValue, newValue));
        Invalidate();
    }

    #endregion

    #region Rendering

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var barHeight = (float)BarHeight;
        var cornerRadius = (float)CornerRadius;

        float midY = bounds.MidY;
        float trackTop = midY - barHeight / 2f;
        float trackBottom = midY + barHeight / 2f;

        // Get colors
        var trackColorSK = ToSKColor(TrackColor);
        var progressColorSK = ToSKColor(ProgressColor);
        var disabledColorSK = ToSKColor(DisabledColor);

        // Draw track
        using var trackPaint = new SKPaint
        {
            Color = IsEnabled ? trackColorSK : disabledColorSK,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        var trackRect = new SKRoundRect(
            new SKRect(bounds.Left, trackTop, bounds.Right, trackBottom),
            cornerRadius);
        canvas.DrawRoundRect(trackRect, trackPaint);

        // Draw progress
        if (Progress > 0.0)
        {
            float progressWidth = bounds.Width * (float)Progress;

            using var progressPaint = new SKPaint
            {
                Color = IsEnabled ? progressColorSK : disabledColorSK,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            var progressRect = new SKRoundRect(
                new SKRect(bounds.Left, trackTop, bounds.Left + progressWidth, trackBottom),
                cornerRadius);
            canvas.DrawRoundRect(progressRect, progressPaint);
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
        var barHeight = BarHeight;
        return new Size(200, barHeight + 8);
    }

    #endregion
}

/// <summary>
/// Event args for progress changed events.
/// </summary>
public class ProgressChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the old progress value.
    /// </summary>
    public double OldProgress { get; }

    /// <summary>
    /// Gets the new progress value.
    /// </summary>
    public double NewProgress { get; }

    /// <summary>
    /// Gets the current progress value (same as NewProgress).
    /// </summary>
    public double Progress => NewProgress;

    public ProgressChangedEventArgs(double oldProgress, double newProgress)
    {
        OldProgress = oldProgress;
        NewProgress = newProgress;
    }
}
