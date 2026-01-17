// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered stepper control with increment/decrement buttons.
/// Implements IStepper interface requirements:
/// - Minimum, Maximum, Value, Increment properties
/// - ValueChanged event with old/new values
/// </summary>
public class SkiaStepper : SkiaView
{
    #region SKColor Helper

    private static SKColor ToSKColor(Color? color)
    {
        if (color == null) return SKColors.Transparent;
        return color.ToSKColor();
    }

    #endregion

    #region BindableProperties

    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(
            nameof(Value),
            typeof(double),
            typeof(SkiaStepper),
            0.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaStepper)b).OnValuePropertyChanged((double)o, (double)n));

    public static readonly BindableProperty MinimumProperty =
        BindableProperty.Create(
            nameof(Minimum),
            typeof(double),
            typeof(SkiaStepper),
            0.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaStepper)b).OnRangeChanged());

    public static readonly BindableProperty MaximumProperty =
        BindableProperty.Create(
            nameof(Maximum),
            typeof(double),
            typeof(SkiaStepper),
            100.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaStepper)b).OnRangeChanged());

    public static readonly BindableProperty IncrementProperty =
        BindableProperty.Create(
            nameof(Increment),
            typeof(double),
            typeof(SkiaStepper),
            1.0,
            BindingMode.TwoWay);

    public static readonly BindableProperty ButtonBackgroundColorProperty =
        BindableProperty.Create(
            nameof(ButtonBackgroundColor),
            typeof(Color),
            typeof(SkiaStepper),
            Color.FromRgb(0xE0, 0xE0, 0xE0),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaStepper)b).Invalidate());

    public static readonly BindableProperty ButtonPressedColorProperty =
        BindableProperty.Create(
            nameof(ButtonPressedColor),
            typeof(Color),
            typeof(SkiaStepper),
            Color.FromRgb(0xBD, 0xBD, 0xBD),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaStepper)b).Invalidate());

    public static readonly BindableProperty ButtonDisabledColorProperty =
        BindableProperty.Create(
            nameof(ButtonDisabledColor),
            typeof(Color),
            typeof(SkiaStepper),
            Color.FromRgb(0xF5, 0xF5, 0xF5),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaStepper)b).Invalidate());

    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(
            nameof(BorderColor),
            typeof(Color),
            typeof(SkiaStepper),
            Color.FromRgb(0xBD, 0xBD, 0xBD),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaStepper)b).Invalidate());

    public static readonly BindableProperty SymbolColorProperty =
        BindableProperty.Create(
            nameof(SymbolColor),
            typeof(Color),
            typeof(SkiaStepper),
            Colors.Black,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaStepper)b).Invalidate());

    public static readonly BindableProperty SymbolDisabledColorProperty =
        BindableProperty.Create(
            nameof(SymbolDisabledColor),
            typeof(Color),
            typeof(SkiaStepper),
            Color.FromRgb(0xBD, 0xBD, 0xBD),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaStepper)b).Invalidate());

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(
            nameof(CornerRadius),
            typeof(double),
            typeof(SkiaStepper),
            4.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaStepper)b).Invalidate());

    public static readonly BindableProperty ButtonWidthProperty =
        BindableProperty.Create(
            nameof(ButtonWidth),
            typeof(double),
            typeof(SkiaStepper),
            40.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaStepper)b).InvalidateMeasure());

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the current value.
    /// </summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, Math.Clamp(value, Minimum, Maximum));
    }

    /// <summary>
    /// Gets or sets the minimum value.
    /// </summary>
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum value.
    /// </summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>
    /// Gets or sets the increment amount.
    /// </summary>
    public double Increment
    {
        get => (double)GetValue(IncrementProperty);
        set => SetValue(IncrementProperty, Math.Max(0.001, value));
    }

    /// <summary>
    /// Gets or sets the button background color.
    /// </summary>
    public Color ButtonBackgroundColor
    {
        get => (Color)GetValue(ButtonBackgroundColorProperty);
        set => SetValue(ButtonBackgroundColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the button pressed color.
    /// </summary>
    public Color ButtonPressedColor
    {
        get => (Color)GetValue(ButtonPressedColorProperty);
        set => SetValue(ButtonPressedColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the button disabled color.
    /// </summary>
    public Color ButtonDisabledColor
    {
        get => (Color)GetValue(ButtonDisabledColorProperty);
        set => SetValue(ButtonDisabledColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the border color.
    /// </summary>
    public Color BorderColor
    {
        get => (Color)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the symbol color.
    /// </summary>
    public Color SymbolColor
    {
        get => (Color)GetValue(SymbolColorProperty);
        set => SetValue(SymbolColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the symbol disabled color.
    /// </summary>
    public Color SymbolDisabledColor
    {
        get => (Color)GetValue(SymbolDisabledColorProperty);
        set => SetValue(SymbolDisabledColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the corner radius.
    /// </summary>
    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>
    /// Gets or sets the button width.
    /// </summary>
    public double ButtonWidth
    {
        get => (double)GetValue(ButtonWidthProperty);
        set => SetValue(ButtonWidthProperty, value);
    }

    /// <summary>
    /// Gets whether the minus button is currently pressed.
    /// </summary>
    public bool IsMinusPressed { get; private set; }

    /// <summary>
    /// Gets whether the plus button is currently pressed.
    /// </summary>
    public bool IsPlusPressed { get; private set; }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the value changes.
    /// </summary>
    public event EventHandler<ValueChangedEventArgs>? ValueChanged;

    #endregion

    #region Constructor

    public SkiaStepper()
    {
        IsFocusable = true;
    }

    #endregion

    #region Event Handlers

    private void OnValuePropertyChanged(double oldValue, double newValue)
    {
        ValueChanged?.Invoke(this, new ValueChangedEventArgs(oldValue, newValue));
        Invalidate();
    }

    private void OnRangeChanged()
    {
        var clamped = Math.Clamp(Value, Minimum, Maximum);
        if (Math.Abs(Value - clamped) > double.Epsilon)
        {
            Value = clamped;
        }
        Invalidate();
    }

    #endregion

    #region Rendering

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var buttonWidth = (float)ButtonWidth;
        var cornerRadius = (float)CornerRadius;

        var minusRect = new SKRect(bounds.Left, bounds.Top, bounds.Left + buttonWidth, bounds.Bottom);
        var plusRect = new SKRect(bounds.Right - buttonWidth, bounds.Top, bounds.Right, bounds.Bottom);

        // Get colors
        var buttonBgColorSK = ToSKColor(ButtonBackgroundColor);
        var buttonPressedColorSK = ToSKColor(ButtonPressedColor);
        var buttonDisabledColorSK = ToSKColor(ButtonDisabledColor);
        var borderColorSK = ToSKColor(BorderColor);
        var symbolColorSK = ToSKColor(SymbolColor);
        var symbolDisabledColorSK = ToSKColor(SymbolDisabledColor);

        // Draw focus ring
        if (IsFocused)
        {
            using var focusPaint = new SKPaint
            {
                Color = ToSKColor(Color.FromRgb(0x21, 0x96, 0xF3)).WithAlpha(60),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3,
                IsAntialias = true
            };
            var focusRect = new SKRect(bounds.Left - 2, bounds.Top - 2, bounds.Right + 2, bounds.Bottom + 2);
            canvas.DrawRoundRect(new SKRoundRect(focusRect, cornerRadius + 2), focusPaint);
        }

        DrawButton(canvas, minusRect, "-", IsMinusPressed, !CanDecrement(),
            buttonBgColorSK, buttonPressedColorSK, buttonDisabledColorSK,
            symbolColorSK, symbolDisabledColorSK, cornerRadius, true);

        DrawButton(canvas, plusRect, "+", IsPlusPressed, !CanIncrement(),
            buttonBgColorSK, buttonPressedColorSK, buttonDisabledColorSK,
            symbolColorSK, symbolDisabledColorSK, cornerRadius, false);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = borderColorSK,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true
        };

        var totalRect = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        canvas.DrawRoundRect(new SKRoundRect(totalRect, cornerRadius), borderPaint);

        // Draw center divider
        var centerX = bounds.MidX;
        canvas.DrawLine(centerX, bounds.Top, centerX, bounds.Bottom, borderPaint);
    }

    private void DrawButton(SKCanvas canvas, SKRect rect, string symbol, bool isPressed, bool isDisabled,
        SKColor bgColor, SKColor pressedColor, SKColor disabledColor,
        SKColor symbolColor, SKColor symbolDisabledColor, float cornerRadius, bool isLeft)
    {
        // Draw background with rounded corners on the appropriate side
        using var bgPaint = new SKPaint
        {
            Color = isDisabled ? disabledColor : (isPressed ? pressedColor : bgColor),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        // Create path for rounded corners on one side only
        using var path = new SKPath();
        if (isLeft)
        {
            path.MoveTo(rect.Left + cornerRadius, rect.Top);
            path.LineTo(rect.Right, rect.Top);
            path.LineTo(rect.Right, rect.Bottom);
            path.LineTo(rect.Left + cornerRadius, rect.Bottom);
            path.ArcTo(new SKRect(rect.Left, rect.Bottom - cornerRadius * 2, rect.Left + cornerRadius * 2, rect.Bottom), 90, 90, false);
            path.LineTo(rect.Left, rect.Top + cornerRadius);
            path.ArcTo(new SKRect(rect.Left, rect.Top, rect.Left + cornerRadius * 2, rect.Top + cornerRadius * 2), 180, 90, false);
        }
        else
        {
            path.MoveTo(rect.Left, rect.Top);
            path.LineTo(rect.Right - cornerRadius, rect.Top);
            path.ArcTo(new SKRect(rect.Right - cornerRadius * 2, rect.Top, rect.Right, rect.Top + cornerRadius * 2), 270, 90, false);
            path.LineTo(rect.Right, rect.Bottom - cornerRadius);
            path.ArcTo(new SKRect(rect.Right - cornerRadius * 2, rect.Bottom - cornerRadius * 2, rect.Right, rect.Bottom), 0, 90, false);
            path.LineTo(rect.Left, rect.Bottom);
        }
        path.Close();
        canvas.DrawPath(path, bgPaint);

        // Draw symbol
        using var font = new SKFont(SKTypeface.Default, 20);
        using var textPaint = new SKPaint(font)
        {
            Color = isDisabled ? symbolDisabledColor : symbolColor,
            IsAntialias = true
        };

        var textBounds = new SKRect();
        textPaint.MeasureText(symbol, ref textBounds);
        canvas.DrawText(symbol, rect.MidX - textBounds.MidX, rect.MidY - textBounds.MidY, textPaint);
    }

    #endregion

    #region Helper Methods

    private bool CanIncrement() => IsEnabled && Value < Maximum;
    private bool CanDecrement() => IsEnabled && Value > Minimum;

    #endregion

    #region Pointer Events

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        var buttonWidth = (float)ButtonWidth;

        if (e.X < buttonWidth)
        {
            IsMinusPressed = true;
            if (CanDecrement()) Value -= Increment;
            SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.Pressed);
        }
        else if (e.X > Bounds.Width - buttonWidth)
        {
            IsPlusPressed = true;
            if (CanIncrement()) Value += Increment;
            SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.Pressed);
        }

        e.Handled = true;
        Invalidate();
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (IsMinusPressed || IsPlusPressed)
        {
            IsMinusPressed = false;
            IsPlusPressed = false;
            SkiaVisualStateManager.GoToState(this, IsEnabled
                ? SkiaVisualStateManager.CommonStates.Normal
                : SkiaVisualStateManager.CommonStates.Disabled);
            Invalidate();
        }
    }

    public override void OnPointerEntered(PointerEventArgs e)
    {
        if (IsEnabled)
        {
            SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.PointerOver);
        }
    }

    public override void OnPointerExited(PointerEventArgs e)
    {
        SkiaVisualStateManager.GoToState(this, IsEnabled
            ? SkiaVisualStateManager.CommonStates.Normal
            : SkiaVisualStateManager.CommonStates.Disabled);
    }

    #endregion

    #region Keyboard Events

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
            case Key.PageUp:
                if (CanIncrement()) Value += Increment * 10;
                e.Handled = true;
                break;
            case Key.PageDown:
                if (CanDecrement()) Value -= Increment * 10;
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

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        var buttonWidth = (float)ButtonWidth;
        return new SKSize(buttonWidth * 2 + 1, 32);
    }

    #endregion
}
