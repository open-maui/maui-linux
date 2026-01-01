// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Rendering;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered checkbox control with full XAML styling support.
/// </summary>
public class SkiaCheckBox : SkiaView
{
    #region BindableProperties

    /// <summary>
    /// Bindable property for IsChecked.
    /// </summary>
    public static readonly BindableProperty IsCheckedProperty =
        BindableProperty.Create(
            nameof(IsChecked),
            typeof(bool),
            typeof(SkiaCheckBox),
            false,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).OnIsCheckedChanged());

    /// <summary>
    /// Bindable property for CheckColor.
    /// </summary>
    public static readonly BindableProperty CheckColorProperty =
        BindableProperty.Create(
            nameof(CheckColor),
            typeof(SKColor),
            typeof(SkiaCheckBox),
            SKColors.White,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    /// <summary>
    /// Bindable property for BoxColor.
    /// </summary>
    public static readonly BindableProperty BoxColorProperty =
        BindableProperty.Create(
            nameof(BoxColor),
            typeof(SKColor),
            typeof(SkiaCheckBox),
            new SKColor(0x21, 0x96, 0xF3),
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    /// <summary>
    /// Bindable property for UncheckedBoxColor.
    /// </summary>
    public static readonly BindableProperty UncheckedBoxColorProperty =
        BindableProperty.Create(
            nameof(UncheckedBoxColor),
            typeof(SKColor),
            typeof(SkiaCheckBox),
            SKColors.White,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    /// <summary>
    /// Bindable property for BorderColor.
    /// </summary>
    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(
            nameof(BorderColor),
            typeof(SKColor),
            typeof(SkiaCheckBox),
            new SKColor(0x75, 0x75, 0x75),
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    /// <summary>
    /// Bindable property for DisabledColor.
    /// </summary>
    public static readonly BindableProperty DisabledColorProperty =
        BindableProperty.Create(
            nameof(DisabledColor),
            typeof(SKColor),
            typeof(SkiaCheckBox),
            new SKColor(0xBD, 0xBD, 0xBD),
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    /// <summary>
    /// Bindable property for HoveredBorderColor.
    /// </summary>
    public static readonly BindableProperty HoveredBorderColorProperty =
        BindableProperty.Create(
            nameof(HoveredBorderColor),
            typeof(SKColor),
            typeof(SkiaCheckBox),
            new SKColor(0x21, 0x96, 0xF3),
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    /// <summary>
    /// Bindable property for BoxSize.
    /// </summary>
    public static readonly BindableProperty BoxSizeProperty =
        BindableProperty.Create(
            nameof(BoxSize),
            typeof(float),
            typeof(SkiaCheckBox),
            20f,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for CornerRadius.
    /// </summary>
    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(
            nameof(CornerRadius),
            typeof(float),
            typeof(SkiaCheckBox),
            3f,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    /// <summary>
    /// Bindable property for BorderWidth.
    /// </summary>
    public static readonly BindableProperty BorderWidthProperty =
        BindableProperty.Create(
            nameof(BorderWidth),
            typeof(float),
            typeof(SkiaCheckBox),
            2f,
            propertyChanged: (b, o, n) => ((SkiaCheckBox)b).Invalidate());

    /// <summary>
    /// Bindable property for CheckStrokeWidth.
    /// </summary>
    public static readonly BindableProperty CheckStrokeWidthProperty =
        BindableProperty.Create(
            nameof(CheckStrokeWidth),
            typeof(float),
            typeof(SkiaCheckBox),
            2.5f,
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
    /// Gets or sets the check color.
    /// </summary>
    public SKColor CheckColor
    {
        get => (SKColor)GetValue(CheckColorProperty);
        set => SetValue(CheckColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the box color when checked.
    /// </summary>
    public SKColor BoxColor
    {
        get => (SKColor)GetValue(BoxColorProperty);
        set => SetValue(BoxColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the box color when unchecked.
    /// </summary>
    public SKColor UncheckedBoxColor
    {
        get => (SKColor)GetValue(UncheckedBoxColorProperty);
        set => SetValue(UncheckedBoxColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the border color.
    /// </summary>
    public SKColor BorderColor
    {
        get => (SKColor)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the disabled color.
    /// </summary>
    public SKColor DisabledColor
    {
        get => (SKColor)GetValue(DisabledColorProperty);
        set => SetValue(DisabledColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the hovered border color.
    /// </summary>
    public SKColor HoveredBorderColor
    {
        get => (SKColor)GetValue(HoveredBorderColorProperty);
        set => SetValue(HoveredBorderColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the box size.
    /// </summary>
    public float BoxSize
    {
        get => (float)GetValue(BoxSizeProperty);
        set => SetValue(BoxSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the corner radius.
    /// </summary>
    public float CornerRadius
    {
        get => (float)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>
    /// Gets or sets the border width.
    /// </summary>
    public float BorderWidth
    {
        get => (float)GetValue(BorderWidthProperty);
        set => SetValue(BorderWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the check stroke width.
    /// </summary>
    public float CheckStrokeWidth
    {
        get => (float)GetValue(CheckStrokeWidthProperty);
        set => SetValue(CheckStrokeWidthProperty, value);
    }

    /// <summary>
    /// Gets whether the pointer is over the checkbox.
    /// </summary>
    public bool IsHovered { get; private set; }

    #endregion

    /// <summary>
    /// Event raised when checked state changes.
    /// </summary>
    public event EventHandler<CheckedChangedEventArgs>? CheckedChanged;

    public SkiaCheckBox()
    {
        IsFocusable = true;
    }

    private void OnIsCheckedChanged()
    {
        CheckedChanged?.Invoke(this, new CheckedChangedEventArgs(IsChecked));
        SkiaVisualStateManager.GoToState(this, IsChecked ? SkiaVisualStateManager.CommonStates.Checked : SkiaVisualStateManager.CommonStates.Unchecked);
        Invalidate();
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
        SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.PointerOver);
        Invalidate();
    }

    public override void OnPointerExited(PointerEventArgs e)
    {
        IsHovered = false;
        SkiaVisualStateManager.GoToState(this, IsEnabled ? SkiaVisualStateManager.CommonStates.Normal : SkiaVisualStateManager.CommonStates.Disabled);
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

    protected override void OnEnabledChanged()
    {
        base.OnEnabledChanged();
        SkiaVisualStateManager.GoToState(this, IsEnabled ? SkiaVisualStateManager.CommonStates.Normal : SkiaVisualStateManager.CommonStates.Disabled);
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
