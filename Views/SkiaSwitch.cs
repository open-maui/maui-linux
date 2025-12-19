// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered toggle switch control.
/// </summary>
public class SkiaSwitch : SkiaView
{
    private bool _isOn;
    private float _animationProgress; // 0 = off, 1 = on

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isOn != value)
            {
                _isOn = value;
                _animationProgress = value ? 1f : 0f;
                Toggled?.Invoke(this, new ToggledEventArgs(value));
                Invalidate();
            }
        }
    }

    public SKColor OnTrackColor { get; set; } = new SKColor(0x21, 0x96, 0xF3);
    public SKColor OffTrackColor { get; set; } = new SKColor(0x9E, 0x9E, 0x9E);
    public SKColor ThumbColor { get; set; } = SKColors.White;
    public SKColor DisabledColor { get; set; } = new SKColor(0xBD, 0xBD, 0xBD);
    public float TrackWidth { get; set; } = 52;
    public float TrackHeight { get; set; } = 32;
    public float ThumbRadius { get; set; } = 12;
    public float ThumbPadding { get; set; } = 4;

    public event EventHandler<ToggledEventArgs>? Toggled;

    public SkiaSwitch()
    {
        IsFocusable = true;
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

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return new SKSize(TrackWidth + 8, TrackHeight + 8);
    }
}

public class ToggledEventArgs : EventArgs
{
    public bool Value { get; }
    public ToggledEventArgs(bool value) => Value = value;
}
