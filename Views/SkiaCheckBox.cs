// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered checkbox control with full MAUI compliance.
/// Implements ICheckBox interface requirements:
/// - IsChecked property with CheckedChanged event
/// - Color property (maps to BoxColor when checked)
/// - Foreground property (maps to CheckColor - the checkmark color)
/// </summary>
public class SkiaCheckBox : SkiaView
{
    #region SKColor Helper

    private static SKColor ToSKColor(Color? color)
    {
        if (color == null) return SKColors.Transparent;
        return new SKColor(
            (byte)(color.Red * 255),
            (byte)(color.Green * 255),
            (byte)(color.Blue * 255),
            (byte)(color.Alpha * 255));
    }

    #endregion

    #region BindableProperties

    public static readonly BindableProperty IsCheckedProperty =
        BindableProperty.Create(
            nameof(IsChecked),
            typeof(bool),
            typeof(SkiaCheckBox),
            false,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).OnIsCheckedChanged());

    public static readonly BindableProperty ColorProperty =
        BindableProperty.Create(
            nameof(Color),
            typeof(Color),
            typeof(SkiaCheckBox),
            Color.FromRgb(33, 150, 243), // Material Blue
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    public static readonly BindableProperty CheckColorProperty =
        BindableProperty.Create(
            nameof(CheckColor),
            typeof(Color),
            typeof(SkiaCheckBox),
            Colors.White,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    public static readonly BindableProperty UncheckedBoxColorProperty =
        BindableProperty.Create(
            nameof(UncheckedBoxColor),
            typeof(Color),
            typeof(SkiaCheckBox),
            Colors.White,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(
            nameof(BorderColor),
            typeof(Color),
            typeof(SkiaCheckBox),
            Color.FromRgb(117, 117, 117),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    public static readonly BindableProperty DisabledColorProperty =
        BindableProperty.Create(
            nameof(DisabledColor),
            typeof(Color),
            typeof(SkiaCheckBox),
            Color.FromRgb(189, 189, 189),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    public static readonly BindableProperty HoveredBorderColorProperty =
        BindableProperty.Create(
            nameof(HoveredBorderColor),
            typeof(Color),
            typeof(SkiaCheckBox),
            Color.FromRgb(33, 150, 243),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    public static readonly BindableProperty BoxSizeProperty =
        BindableProperty.Create(
            nameof(BoxSize),
            typeof(double),
            typeof(SkiaCheckBox),
            20.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).InvalidateMeasure());

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(
            nameof(CornerRadius),
            typeof(double),
            typeof(SkiaCheckBox),
            3.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    public static readonly BindableProperty BorderWidthProperty =
        BindableProperty.Create(
            nameof(BorderWidth),
            typeof(double),
            typeof(SkiaCheckBox),
            2.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    public static readonly BindableProperty CheckStrokeWidthProperty =
        BindableProperty.Create(
            nameof(CheckStrokeWidth),
            typeof(double),
            typeof(SkiaCheckBox),
            2.5,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets whether the checkbox is checked.
    /// </summary>
    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    /// <summary>
    /// Gets or sets the color of the checkbox box when checked.
    /// This is the primary MAUI CheckBox.Color property.
    /// </summary>
    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the color of the checkmark itself.
    /// Maps to ICheckBox.Foreground in MAUI.
    /// </summary>
    public Color CheckColor
    {
        get => (Color)GetValue(CheckColorProperty);
        set => SetValue(CheckColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the color of the checkbox box when unchecked.
    /// </summary>
    public Color UncheckedBoxColor
    {
        get => (Color)GetValue(UncheckedBoxColorProperty);
        set => SetValue(UncheckedBoxColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the border color when unchecked.
    /// </summary>
    public Color BorderColor
    {
        get => (Color)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the color used when the control is disabled.
    /// </summary>
    public Color DisabledColor
    {
        get => (Color)GetValue(DisabledColorProperty);
        set => SetValue(DisabledColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the border color when hovered.
    /// </summary>
    public Color HoveredBorderColor
    {
        get => (Color)GetValue(HoveredBorderColorProperty);
        set => SetValue(HoveredBorderColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the size of the checkbox box in device-independent units.
    /// </summary>
    public double BoxSize
    {
        get => (double)GetValue(BoxSizeProperty);
        set => SetValue(BoxSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the corner radius of the checkbox box.
    /// </summary>
    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>
    /// Gets or sets the border width.
    /// </summary>
    public double BorderWidth
    {
        get => (double)GetValue(BorderWidthProperty);
        set => SetValue(BorderWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the stroke width of the checkmark.
    /// </summary>
    public double CheckStrokeWidth
    {
        get => (double)GetValue(CheckStrokeWidthProperty);
        set => SetValue(CheckStrokeWidthProperty, value);
    }

    /// <summary>
    /// Gets whether the control is currently hovered.
    /// </summary>
    public bool IsHovered { get; private set; }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when the IsChecked property changes.
    /// </summary>
    public event EventHandler<CheckedChangedEventArgs>? CheckedChanged;

    #endregion

    #region Constructor

    public SkiaCheckBox()
    {
        IsFocusable = true;
    }

    #endregion

    #region Event Handlers

    private void OnIsCheckedChanged()
    {
        CheckedChanged?.Invoke(this, new CheckedChangedEventArgs(IsChecked));
        SkiaVisualStateManager.GoToState(this, IsChecked ? "Checked" : "Unchecked");
        Invalidate();
    }

    #endregion

    #region Rendering

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var boxSize = (float)BoxSize;
        var cornerRadius = (float)CornerRadius;
        var borderWidth = (float)BorderWidth;

        // Center the checkbox box in bounds
        var boxRect = new SKRect(
            bounds.Left + (bounds.Width - boxSize) / 2f,
            bounds.Top + (bounds.Height - boxSize) / 2f,
            bounds.Left + (bounds.Width - boxSize) / 2f + boxSize,
            bounds.Top + (bounds.Height - boxSize) / 2f + boxSize);

        var roundRect = new SKRoundRect(boxRect, cornerRadius);

        // Get colors as SKColor
        var colorSK = ToSKColor(Color);
        var checkColorSK = ToSKColor(CheckColor);
        var uncheckedBoxColorSK = ToSKColor(UncheckedBoxColor);
        var borderColorSK = ToSKColor(BorderColor);
        var disabledColorSK = ToSKColor(DisabledColor);
        var hoveredBorderColorSK = ToSKColor(HoveredBorderColor);

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = !IsEnabled ? disabledColorSK
                  : IsChecked ? colorSK
                  : uncheckedBoxColorSK,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRoundRect(roundRect, bgPaint);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = !IsEnabled ? disabledColorSK
                  : IsChecked ? colorSK
                  : IsHovered ? hoveredBorderColorSK
                  : borderColorSK,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = borderWidth
        };
        canvas.DrawRoundRect(roundRect, borderPaint);

        // Draw focus ring
        if (IsFocused)
        {
            using var focusPaint = new SKPaint
            {
                Color = colorSK.WithAlpha(80),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3f
            };
            var focusRect = new SKRoundRect(boxRect, cornerRadius);
            focusRect.Inflate(4f, 4f);
            canvas.DrawRoundRect(focusRect, focusPaint);
        }

        // Draw checkmark
        if (IsChecked)
        {
            DrawCheckmark(canvas, boxRect, checkColorSK);
        }
    }

    private void DrawCheckmark(SKCanvas canvas, SKRect boxRect, SKColor checkColor)
    {
        var checkStrokeWidth = (float)CheckStrokeWidth;
        var boxSize = (float)BoxSize;

        using var paint = new SKPaint
        {
            Color = checkColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = checkStrokeWidth,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        float padding = boxSize * 0.2f;
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

    #endregion

    #region Pointer Events

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
        // No action needed
    }

    #endregion

    #region Keyboard Events

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (IsEnabled && e.Key == Key.Space)
        {
            IsChecked = !IsChecked;
            e.Handled = true;
        }
    }

    #endregion

    #region Lifecycle

    protected override void OnEnabledChanged()
    {
        base.OnEnabledChanged();
        SkiaVisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled");
    }

    #endregion

    #region Layout

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        var boxSize = (float)BoxSize;
        return new SKSize(boxSize + 8f, boxSize + 8f);
    }

    #endregion
}
