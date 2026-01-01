// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered checkbox control with full XAML styling support.
/// </summary>
public class SkiaCheckBox : SkiaView
{
    #region BindableProperties

    public static readonly BindableProperty IsCheckedProperty =
        BindableProperty.Create(
            nameof(IsChecked),
            typeof(bool),
            typeof(SkiaCheckBox),
            false,
            BindingMode.OneWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).OnIsCheckedChanged());

    public static readonly BindableProperty CheckColorProperty =
        BindableProperty.Create(
            nameof(CheckColor),
            typeof(SKColor),
            typeof(SkiaCheckBox),
            SKColors.White,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    public static readonly BindableProperty BoxColorProperty =
        BindableProperty.Create(
            nameof(BoxColor),
            typeof(SKColor),
            typeof(SkiaCheckBox),
            new SKColor(33, 150, 243),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    public static readonly BindableProperty UncheckedBoxColorProperty =
        BindableProperty.Create(
            nameof(UncheckedBoxColor),
            typeof(SKColor),
            typeof(SkiaCheckBox),
            SKColors.White,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(
            nameof(BorderColor),
            typeof(SKColor),
            typeof(SkiaCheckBox),
            new SKColor(117, 117, 117),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    public static readonly BindableProperty DisabledColorProperty =
        BindableProperty.Create(
            nameof(DisabledColor),
            typeof(SKColor),
            typeof(SkiaCheckBox),
            new SKColor(189, 189, 189),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    public static readonly BindableProperty HoveredBorderColorProperty =
        BindableProperty.Create(
            nameof(HoveredBorderColor),
            typeof(SKColor),
            typeof(SkiaCheckBox),
            new SKColor(33, 150, 243),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    public static readonly BindableProperty BoxSizeProperty =
        BindableProperty.Create(
            nameof(BoxSize),
            typeof(float),
            typeof(SkiaCheckBox),
            20f,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).InvalidateMeasure());

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(
            nameof(CornerRadius),
            typeof(float),
            typeof(SkiaCheckBox),
            3f,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    public static readonly BindableProperty BorderWidthProperty =
        BindableProperty.Create(
            nameof(BorderWidth),
            typeof(float),
            typeof(SkiaCheckBox),
            2f,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    public static readonly BindableProperty CheckStrokeWidthProperty =
        BindableProperty.Create(
            nameof(CheckStrokeWidth),
            typeof(float),
            typeof(SkiaCheckBox),
            2.5f,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    #endregion

    #region Properties

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public SKColor CheckColor
    {
        get => (SKColor)GetValue(CheckColorProperty);
        set => SetValue(CheckColorProperty, value);
    }

    public SKColor BoxColor
    {
        get => (SKColor)GetValue(BoxColorProperty);
        set => SetValue(BoxColorProperty, value);
    }

    public SKColor UncheckedBoxColor
    {
        get => (SKColor)GetValue(UncheckedBoxColorProperty);
        set => SetValue(UncheckedBoxColorProperty, value);
    }

    public SKColor BorderColor
    {
        get => (SKColor)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    public SKColor DisabledColor
    {
        get => (SKColor)GetValue(DisabledColorProperty);
        set => SetValue(DisabledColorProperty, value);
    }

    public SKColor HoveredBorderColor
    {
        get => (SKColor)GetValue(HoveredBorderColorProperty);
        set => SetValue(HoveredBorderColorProperty, value);
    }

    public float BoxSize
    {
        get => (float)GetValue(BoxSizeProperty);
        set => SetValue(BoxSizeProperty, value);
    }

    public float CornerRadius
    {
        get => (float)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public float BorderWidth
    {
        get => (float)GetValue(BorderWidthProperty);
        set => SetValue(BorderWidthProperty, value);
    }

    public float CheckStrokeWidth
    {
        get => (float)GetValue(CheckStrokeWidthProperty);
        set => SetValue(CheckStrokeWidthProperty, value);
    }

    public bool IsHovered { get; private set; }

    #endregion

    public event EventHandler<CheckedChangedEventArgs>? CheckedChanged;

    public SkiaCheckBox()
    {
        IsFocusable = true;
    }

    private void OnIsCheckedChanged()
    {
        CheckedChanged?.Invoke(this, new CheckedChangedEventArgs(IsChecked));
        SkiaVisualStateManager.GoToState(this, IsChecked ? "Checked" : "Unchecked");
        Invalidate();
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Center the checkbox box in bounds
        var boxRect = new SKRect(
            bounds.Left + (bounds.Width - BoxSize) / 2f,
            bounds.Top + (bounds.Height - BoxSize) / 2f,
            bounds.Left + (bounds.Width - BoxSize) / 2f + BoxSize,
            bounds.Top + (bounds.Height - BoxSize) / 2f + BoxSize);

        var roundRect = new SKRoundRect(boxRect, CornerRadius);

        // Debug logging when checked
        if (IsChecked)
        {
            Console.WriteLine($"[SkiaCheckBox] OnDraw CHECKED - BoxColor=({BoxColor.Red},{BoxColor.Green},{BoxColor.Blue}), UncheckedBoxColor=({UncheckedBoxColor.Red},{UncheckedBoxColor.Green},{UncheckedBoxColor.Blue})");
        }

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
                StrokeWidth = 3f
            };
            var focusRect = new SKRoundRect(boxRect, CornerRadius);
            focusRect.Inflate(4f, 4f);
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
            Color = SKColors.White,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = CheckStrokeWidth,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        float padding = BoxSize * 0.2f;
        float left = boxRect.Left + padding;
        float right = boxRect.Right - padding;
        float top = boxRect.Top + padding;
        float bottom = boxRect.Bottom - padding;

        using var path = new SKPath();
        path.MoveTo(left, boxRect.MidY);
        path.LineTo(boxRect.MidX - padding * 0.3f, bottom - padding * 0.5f);
        path.LineTo(right, top + padding * 0.3f);

        canvas.DrawPath(path, paint);
    }

    public override void OnPointerEntered(PointerEventArgs e)
    {
        if (IsEnabled)
        {
            IsHovered = true;
            SkiaVisualStateManager.GoToState(this, "PointerOver");
            Invalidate();
        }
    }

    public override void OnPointerExited(PointerEventArgs e)
    {
        IsHovered = false;
        SkiaVisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled");
        Invalidate();
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (IsEnabled)
        {
            IsChecked = !IsChecked;
            e.Handled = true;
        }
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (IsEnabled && e.Key == Key.Space)
        {
            IsChecked = !IsChecked;
            e.Handled = true;
        }
    }

    protected override void OnEnabledChanged()
    {
        base.OnEnabledChanged();
        SkiaVisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled");
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return new SKSize(BoxSize + 8f, BoxSize + 8f);
    }
}
