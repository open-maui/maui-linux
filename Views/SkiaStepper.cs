// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered stepper control with increment/decrement buttons.
/// </summary>
public class SkiaStepper : SkiaView
{
    #region BindableProperties

    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(double), typeof(SkiaStepper), 0.0, BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaStepper)b).OnValuePropertyChanged((double)o, (double)n));

    public static readonly BindableProperty MinimumProperty =
        BindableProperty.Create(nameof(Minimum), typeof(double), typeof(SkiaStepper), 0.0,
            propertyChanged: (b, o, n) => ((SkiaStepper)b).OnRangeChanged());

    public static readonly BindableProperty MaximumProperty =
        BindableProperty.Create(nameof(Maximum), typeof(double), typeof(SkiaStepper), 100.0,
            propertyChanged: (b, o, n) => ((SkiaStepper)b).OnRangeChanged());

    public static readonly BindableProperty IncrementProperty =
        BindableProperty.Create(nameof(Increment), typeof(double), typeof(SkiaStepper), 1.0);

    public static readonly BindableProperty ButtonBackgroundColorProperty =
        BindableProperty.Create(nameof(ButtonBackgroundColor), typeof(SKColor), typeof(SkiaStepper), new SKColor(0xE0, 0xE0, 0xE0),
            propertyChanged: (b, o, n) => ((SkiaStepper)b).Invalidate());

    public static readonly BindableProperty ButtonPressedColorProperty =
        BindableProperty.Create(nameof(ButtonPressedColor), typeof(SKColor), typeof(SkiaStepper), new SKColor(0xBD, 0xBD, 0xBD),
            propertyChanged: (b, o, n) => ((SkiaStepper)b).Invalidate());

    public static readonly BindableProperty ButtonDisabledColorProperty =
        BindableProperty.Create(nameof(ButtonDisabledColor), typeof(SKColor), typeof(SkiaStepper), new SKColor(0xF5, 0xF5, 0xF5),
            propertyChanged: (b, o, n) => ((SkiaStepper)b).Invalidate());

    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(nameof(BorderColor), typeof(SKColor), typeof(SkiaStepper), new SKColor(0xBD, 0xBD, 0xBD),
            propertyChanged: (b, o, n) => ((SkiaStepper)b).Invalidate());

    public static readonly BindableProperty SymbolColorProperty =
        BindableProperty.Create(nameof(SymbolColor), typeof(SKColor), typeof(SkiaStepper), SKColors.Black,
            propertyChanged: (b, o, n) => ((SkiaStepper)b).Invalidate());

    public static readonly BindableProperty SymbolDisabledColorProperty =
        BindableProperty.Create(nameof(SymbolDisabledColor), typeof(SKColor), typeof(SkiaStepper), new SKColor(0xBD, 0xBD, 0xBD),
            propertyChanged: (b, o, n) => ((SkiaStepper)b).Invalidate());

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(float), typeof(SkiaStepper), 4f,
            propertyChanged: (b, o, n) => ((SkiaStepper)b).Invalidate());

    public static readonly BindableProperty ButtonWidthProperty =
        BindableProperty.Create(nameof(ButtonWidth), typeof(float), typeof(SkiaStepper), 40f,
            propertyChanged: (b, o, n) => ((SkiaStepper)b).InvalidateMeasure());

    #endregion

    #region Properties

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, Math.Clamp(value, Minimum, Maximum));
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Increment
    {
        get => (double)GetValue(IncrementProperty);
        set => SetValue(IncrementProperty, Math.Max(0.001, value));
    }

    public SKColor ButtonBackgroundColor
    {
        get => (SKColor)GetValue(ButtonBackgroundColorProperty);
        set => SetValue(ButtonBackgroundColorProperty, value);
    }

    public SKColor ButtonPressedColor
    {
        get => (SKColor)GetValue(ButtonPressedColorProperty);
        set => SetValue(ButtonPressedColorProperty, value);
    }

    public SKColor ButtonDisabledColor
    {
        get => (SKColor)GetValue(ButtonDisabledColorProperty);
        set => SetValue(ButtonDisabledColorProperty, value);
    }

    public SKColor BorderColor
    {
        get => (SKColor)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    public SKColor SymbolColor
    {
        get => (SKColor)GetValue(SymbolColorProperty);
        set => SetValue(SymbolColorProperty, value);
    }

    public SKColor SymbolDisabledColor
    {
        get => (SKColor)GetValue(SymbolDisabledColorProperty);
        set => SetValue(SymbolDisabledColorProperty, value);
    }

    public float CornerRadius
    {
        get => (float)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public float ButtonWidth
    {
        get => (float)GetValue(ButtonWidthProperty);
        set => SetValue(ButtonWidthProperty, value);
    }

    #endregion

    private bool _isMinusPressed;
    private bool _isPlusPressed;

    public event EventHandler? ValueChanged;

    public SkiaStepper()
    {
        IsFocusable = true;
    }

    private void OnValuePropertyChanged(double oldValue, double newValue)
    {
        ValueChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private void OnRangeChanged()
    {
        var clamped = Math.Clamp(Value, Minimum, Maximum);
        if (Value != clamped)
        {
            Value = clamped;
        }
        Invalidate();
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var minusRect = new SKRect(bounds.Left, bounds.Top, bounds.Left + ButtonWidth, bounds.Bottom);
        var plusRect = new SKRect(bounds.Right - ButtonWidth, bounds.Top, bounds.Right, bounds.Bottom);

        DrawButton(canvas, minusRect, "-", _isMinusPressed, !CanDecrement());
        DrawButton(canvas, plusRect, "+", _isPlusPressed, !CanIncrement());

        using var borderPaint = new SKPaint
        {
            Color = BorderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true
        };

        var totalRect = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        canvas.DrawRoundRect(new SKRoundRect(totalRect, CornerRadius), borderPaint);

        var centerX = bounds.MidX;
        canvas.DrawLine(centerX, bounds.Top, centerX, bounds.Bottom, borderPaint);
    }

    private void DrawButton(SKCanvas canvas, SKRect rect, string symbol, bool isPressed, bool isDisabled)
    {
        using var bgPaint = new SKPaint
        {
            Color = isDisabled ? ButtonDisabledColor : (isPressed ? ButtonPressedColor : ButtonBackgroundColor),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRect(rect, bgPaint);

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

    private bool CanIncrement() => IsEnabled && Value < Maximum;
    private bool CanDecrement() => IsEnabled && Value > Minimum;

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        if (e.X < ButtonWidth)
        {
            _isMinusPressed = true;
            if (CanDecrement()) Value -= Increment;
        }
        else if (e.X > Bounds.Width - ButtonWidth)
        {
            _isPlusPressed = true;
            if (CanIncrement()) Value += Increment;
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
                if (CanIncrement()) Value += Increment;
                e.Handled = true;
                break;
            case Key.Down:
            case Key.Left:
                if (CanDecrement()) Value -= Increment;
                e.Handled = true;
                break;
        }
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return new SKSize(ButtonWidth * 2 + 1, 32);
    }
}
