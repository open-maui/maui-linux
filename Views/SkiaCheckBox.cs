// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Rendering;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered checkbox control.
/// </summary>
public class SkiaCheckBox : SkiaView
{
    private bool _isChecked;

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked != value)
            {
                _isChecked = value;
                CheckedChanged?.Invoke(this, new CheckedChangedEventArgs(value));
                Invalidate();
            }
        }
    }

    public SKColor CheckColor { get; set; } = SKColors.White;
    public SKColor BoxColor { get; set; } = new SKColor(0x21, 0x96, 0xF3); // Material Blue
    public SKColor UncheckedBoxColor { get; set; } = SKColors.White;
    public SKColor BorderColor { get; set; } = new SKColor(0x75, 0x75, 0x75);
    public SKColor DisabledColor { get; set; } = new SKColor(0xBD, 0xBD, 0xBD);
    public SKColor HoveredBorderColor { get; set; } = new SKColor(0x21, 0x96, 0xF3);
    public float BoxSize { get; set; } = 20;
    public float CornerRadius { get; set; } = 3;
    public float BorderWidth { get; set; } = 2;
    public float CheckStrokeWidth { get; set; } = 2.5f;

    public bool IsHovered { get; private set; }

    public event EventHandler<CheckedChangedEventArgs>? CheckedChanged;

    public SkiaCheckBox()
    {
        IsFocusable = true;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Center the checkbox box in bounds
        var boxRect = new SKRect(
            bounds.Left + (bounds.Width - BoxSize) / 2,
            bounds.Top + (bounds.Height - BoxSize) / 2,
            bounds.Left + (bounds.Width - BoxSize) / 2 + BoxSize,
            bounds.Top + (bounds.Height - BoxSize) / 2 + BoxSize);

        var roundRect = new SKRoundRect(boxRect, CornerRadius);

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = !IsEnabled ? DisabledColor
                  : IsChecked ? BoxColor
                  : UncheckedBoxColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRoundRect(roundRect, bgPaint);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = !IsEnabled ? DisabledColor
                  : IsChecked ? BoxColor
                  : IsHovered ? HoveredBorderColor
                  : BorderColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = BorderWidth
        };
        canvas.DrawRoundRect(roundRect, borderPaint);

        // Draw focus ring
        if (IsFocused)
        {
            using var focusPaint = new SKPaint
            {
                Color = BoxColor.WithAlpha(80),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3
            };
            var focusRect = new SKRoundRect(boxRect, CornerRadius);
            focusRect.Inflate(4, 4);
            canvas.DrawRoundRect(focusRect, focusPaint);
        }

        // Draw checkmark
        if (IsChecked)
        {
            DrawCheckmark(canvas, boxRect);
        }
    }

    private void DrawCheckmark(SKCanvas canvas, SKRect boxRect)
    {
        using var paint = new SKPaint
        {
            Color = CheckColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = CheckStrokeWidth,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        // Checkmark path - a simple check
        var padding = BoxSize * 0.2f;
        var left = boxRect.Left + padding;
        var right = boxRect.Right - padding;
        var top = boxRect.Top + padding;
        var bottom = boxRect.Bottom - padding;

        // Check starts from bottom-left, goes to middle-bottom, then to top-right
        using var path = new SKPath();
        path.MoveTo(left, boxRect.MidY);
        path.LineTo(boxRect.MidX - padding * 0.3f, bottom - padding * 0.5f);
        path.LineTo(right, top + padding * 0.3f);

        canvas.DrawPath(path, paint);
    }

    public override void OnPointerEntered(PointerEventArgs e)
    {
        if (!IsEnabled) return;
        IsHovered = true;
        Invalidate();
    }

    public override void OnPointerExited(PointerEventArgs e)
    {
        IsHovered = false;
        Invalidate();
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;
        IsChecked = !IsChecked;
        e.Handled = true;
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        // Toggle handled in OnPointerPressed
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        // Toggle on Space
        if (e.Key == Key.Space)
        {
            IsChecked = !IsChecked;
            e.Handled = true;
        }
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        // Add some padding around the box for touch targets
        return new SKSize(BoxSize + 8, BoxSize + 8);
    }
}

/// <summary>
/// Event args for checked changed events.
/// </summary>
public class CheckedChangedEventArgs : EventArgs
{
    public bool IsChecked { get; }

    public CheckedChangedEventArgs(bool isChecked)
    {
        IsChecked = isChecked;
    }
}
