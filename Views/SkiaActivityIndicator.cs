// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered activity indicator (spinner) control.
/// </summary>
public class SkiaActivityIndicator : SkiaView
{
    private bool _isRunning;
    private float _rotationAngle;
    private DateTime _lastUpdateTime = DateTime.UtcNow;

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (_isRunning != value)
            {
                _isRunning = value;
                if (value)
                {
                    _lastUpdateTime = DateTime.UtcNow;
                }
                Invalidate();
            }
        }
    }

    public SKColor Color { get; set; } = new SKColor(0x21, 0x96, 0xF3);
    public SKColor DisabledColor { get; set; } = new SKColor(0xBD, 0xBD, 0xBD);
    public float Size { get; set; } = 32;
    public float StrokeWidth { get; set; } = 3;
    public float RotationSpeed { get; set; } = 360; // Degrees per second
    public int ArcCount { get; set; } = 12;

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
