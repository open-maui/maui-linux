// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered slider control.
/// </summary>
public class SkiaSlider : SkiaView
{
    private bool _isDragging;
    private double _value;

    public double Minimum { get; set; } = 0;
    public double Maximum { get; set; } = 100;

    public double Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, Minimum, Maximum);
            if (_value != clamped)
            {
                _value = clamped;
                ValueChanged?.Invoke(this, new SliderValueChangedEventArgs(_value));
                Invalidate();
            }
        }
    }

    public SKColor TrackColor { get; set; } = new SKColor(0xE0, 0xE0, 0xE0);
    public SKColor ActiveTrackColor { get; set; } = new SKColor(0x21, 0x96, 0xF3);
    public SKColor ThumbColor { get; set; } = new SKColor(0x21, 0x96, 0xF3);
    public SKColor DisabledColor { get; set; } = new SKColor(0xBD, 0xBD, 0xBD);
    public float TrackHeight { get; set; } = 4;
    public float ThumbRadius { get; set; } = 10;

    public event EventHandler<SliderValueChangedEventArgs>? ValueChanged;
    public event EventHandler? DragStarted;
    public event EventHandler? DragCompleted;

    public SkiaSlider()
    {
        IsFocusable = true;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var trackY = bounds.MidY;
        var trackLeft = bounds.Left + ThumbRadius;
        var trackRight = bounds.Right - ThumbRadius;
        var trackWidth = trackRight - trackLeft;

        var percentage = (Value - Minimum) / (Maximum - Minimum);
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

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return new SKSize(200, ThumbRadius * 2 + 16);
    }
}

public class SliderValueChangedEventArgs : EventArgs
{
    public double NewValue { get; }
    public SliderValueChangedEventArgs(double newValue) => NewValue = newValue;
}
