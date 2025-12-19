// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered stepper control with increment/decrement buttons.
/// </summary>
public class SkiaStepper : SkiaView
{
    private double _value;
    private double _minimum;
    private double _maximum = 100;
    private double _increment = 1;
    private bool _isMinusPressed;
    private bool _isPlusPressed;

    // Styling
    public SKColor ButtonBackgroundColor { get; set; } = new SKColor(0xE0, 0xE0, 0xE0);
    public SKColor ButtonPressedColor { get; set; } = new SKColor(0xBD, 0xBD, 0xBD);
    public SKColor ButtonDisabledColor { get; set; } = new SKColor(0xF5, 0xF5, 0xF5);
    public SKColor BorderColor { get; set; } = new SKColor(0xBD, 0xBD, 0xBD);
    public SKColor SymbolColor { get; set; } = SKColors.Black;
    public SKColor SymbolDisabledColor { get; set; } = new SKColor(0xBD, 0xBD, 0xBD);
    public float CornerRadius { get; set; } = 4;
    public float ButtonWidth { get; set; } = 40;

    public double Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, _minimum, _maximum);
            if (_value != clamped)
            {
                _value = clamped;
                ValueChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }
    }

    public double Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            if (_value < _minimum) Value = _minimum;
            Invalidate();
        }
    }

    public double Maximum
    {
        get => _maximum;
        set
        {
            _maximum = value;
            if (_value > _maximum) Value = _maximum;
            Invalidate();
        }
    }

    public double Increment
    {
        get => _increment;
        set { _increment = Math.Max(0.001, value); Invalidate(); }
    }

    public event EventHandler? ValueChanged;

    public SkiaStepper()
    {
        IsFocusable = true;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var buttonHeight = bounds.Height;
        var minusRect = new SKRect(bounds.Left, bounds.Top, bounds.Left + ButtonWidth, bounds.Bottom);
        var plusRect = new SKRect(bounds.Right - ButtonWidth, bounds.Top, bounds.Right, bounds.Bottom);

        // Draw minus button
        DrawButton(canvas, minusRect, "-", _isMinusPressed, !CanDecrement());

        // Draw plus button
        DrawButton(canvas, plusRect, "+", _isPlusPressed, !CanIncrement());

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = BorderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true
        };

        // Overall border with rounded corners
        var totalRect = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        canvas.DrawRoundRect(new SKRoundRect(totalRect, CornerRadius), borderPaint);

        // Center divider
        var centerX = bounds.MidX;
        canvas.DrawLine(centerX, bounds.Top, centerX, bounds.Bottom, borderPaint);
    }

    private void DrawButton(SKCanvas canvas, SKRect rect, string symbol, bool isPressed, bool isDisabled)
    {
        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = isDisabled ? ButtonDisabledColor : (isPressed ? ButtonPressedColor : ButtonBackgroundColor),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        // Draw button background (clipped by overall border)
        canvas.DrawRect(rect, bgPaint);

        // Draw symbol
        using var font = new SKFont(SKTypeface.Default, 20);
        using var textPaint = new SKPaint(font)
        {
            Color = isDisabled ? SymbolDisabledColor : SymbolColor,
            IsAntialias = true
        };

        var textBounds = new SKRect();
        textPaint.MeasureText(symbol, ref textBounds);
        canvas.DrawText(symbol, rect.MidX - textBounds.MidX, rect.MidY - textBounds.MidY, textPaint);
    }

    private bool CanIncrement() => IsEnabled && _value < _maximum;
    private bool CanDecrement() => IsEnabled && _value > _minimum;

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        var x = e.X;
        if (x < ButtonWidth)
        {
            _isMinusPressed = true;
            if (CanDecrement()) Value -= _increment;
        }
        else if (x > Bounds.Width - ButtonWidth)
        {
            _isPlusPressed = true;
            if (CanIncrement()) Value += _increment;
        }
        Invalidate();
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        _isMinusPressed = false;
        _isPlusPressed = false;
        Invalidate();
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        switch (e.Key)
        {
            case Key.Up:
            case Key.Right:
                if (CanIncrement()) Value += _increment;
                e.Handled = true;
                break;
            case Key.Down:
            case Key.Left:
                if (CanDecrement()) Value -= _increment;
                e.Handled = true;
                break;
        }
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return new SKSize(ButtonWidth * 2 + 1, 32);
    }
}
