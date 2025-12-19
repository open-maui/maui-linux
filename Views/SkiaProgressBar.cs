// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered progress bar control.
/// </summary>
public class SkiaProgressBar : SkiaView
{
    private double _progress;

    public double Progress
    {
        get => _progress;
        set
        {
            var clamped = Math.Clamp(value, 0, 1);
            if (_progress != clamped)
            {
                _progress = clamped;
                ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(_progress));
                Invalidate();
            }
        }
    }

    public SKColor TrackColor { get; set; } = new SKColor(0xE0, 0xE0, 0xE0);
    public SKColor ProgressColor { get; set; } = new SKColor(0x21, 0x96, 0xF3);
    public SKColor DisabledColor { get; set; } = new SKColor(0xBD, 0xBD, 0xBD);
    public float Height { get; set; } = 4;
    public float CornerRadius { get; set; } = 2;

    public event EventHandler<ProgressChangedEventArgs>? ProgressChanged;

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var trackY = bounds.MidY;
        var trackTop = trackY - Height / 2;
        var trackBottom = trackY + Height / 2;

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
        return new SKSize(200, Height + 8);
    }
}

public class ProgressChangedEventArgs : EventArgs
{
    public double Progress { get; }
    public ProgressChangedEventArgs(double progress) => Progress = progress;
}
