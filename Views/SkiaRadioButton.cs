// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered radio button control with full MAUI compliance.
/// Implements IRadioButton interface requirements:
/// - IsChecked property with CheckedChanged event
/// - GroupName for mutual exclusion
/// - Value property for binding
/// - Content property for label text
/// </summary>
public class SkiaRadioButton : SkiaView
{
    #region SKColor Helper

    private static SKColor ToSKColor(Color? color)
    {
        if (color == null) return SKColors.Transparent;
        return color.ToSKColor();
    }

    #endregion

    #region BindableProperties

    public static readonly BindableProperty IsCheckedProperty =
        BindableProperty.Create(
            nameof(IsChecked),
            typeof(bool),
            typeof(SkiaRadioButton),
            false,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).OnIsCheckedChanged());

    public static readonly BindableProperty ContentProperty =
        BindableProperty.Create(
            nameof(Content),
            typeof(string),
            typeof(SkiaRadioButton),
            "",
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).InvalidateMeasure());

    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(
            nameof(Value),
            typeof(object),
            typeof(SkiaRadioButton),
            null,
            BindingMode.TwoWay);

    public static readonly BindableProperty GroupNameProperty =
        BindableProperty.Create(
            nameof(GroupName),
            typeof(string),
            typeof(SkiaRadioButton),
            null,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).OnGroupNameChanged((string?)o, (string?)n));

    public static readonly BindableProperty RadioColorProperty =
        BindableProperty.Create(
            nameof(RadioColor),
            typeof(Color),
            typeof(SkiaRadioButton),
            Color.FromRgb(0x21, 0x96, 0xF3), // Material Blue
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).Invalidate());

    public static readonly BindableProperty UncheckedColorProperty =
        BindableProperty.Create(
            nameof(UncheckedColor),
            typeof(Color),
            typeof(SkiaRadioButton),
            Color.FromRgb(0x75, 0x75, 0x75),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).Invalidate());

    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(
            nameof(TextColor),
            typeof(Color),
            typeof(SkiaRadioButton),
            Colors.Black,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).Invalidate());

    public static readonly BindableProperty DisabledColorProperty =
        BindableProperty.Create(
            nameof(DisabledColor),
            typeof(Color),
            typeof(SkiaRadioButton),
            Color.FromRgb(0xBD, 0xBD, 0xBD),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).Invalidate());

    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(
            nameof(FontSize),
            typeof(double),
            typeof(SkiaRadioButton),
            14.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).InvalidateMeasure());

    public static readonly BindableProperty RadioSizeProperty =
        BindableProperty.Create(
            nameof(RadioSize),
            typeof(double),
            typeof(SkiaRadioButton),
            20.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).InvalidateMeasure());

    public static readonly BindableProperty SpacingProperty =
        BindableProperty.Create(
            nameof(Spacing),
            typeof(double),
            typeof(SkiaRadioButton),
            8.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).InvalidateMeasure());

    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(
            nameof(BorderColor),
            typeof(Color),
            typeof(SkiaRadioButton),
            Color.FromRgb(0x75, 0x75, 0x75),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).Invalidate());

    public static readonly BindableProperty BorderWidthProperty =
        BindableProperty.Create(
            nameof(BorderWidth),
            typeof(double),
            typeof(SkiaRadioButton),
            2.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).Invalidate());

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets whether the radio button is checked.
    /// </summary>
    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    /// <summary>
    /// Gets or sets the text content displayed next to the radio button.
    /// </summary>
    public string Content
    {
        get => (string)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Gets or sets the value associated with this radio button.
    /// </summary>
    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Gets or sets the group name for mutual exclusion.
    /// </summary>
    public string? GroupName
    {
        get => (string?)GetValue(GroupNameProperty);
        set => SetValue(GroupNameProperty, value);
    }

    /// <summary>
    /// Gets or sets the color of the radio circle when checked.
    /// </summary>
    public Color RadioColor
    {
        get => (Color)GetValue(RadioColorProperty);
        set => SetValue(RadioColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the color of the radio circle when unchecked.
    /// </summary>
    public Color UncheckedColor
    {
        get => (Color)GetValue(UncheckedColorProperty);
        set => SetValue(UncheckedColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the text color.
    /// </summary>
    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the color used when disabled.
    /// </summary>
    public Color DisabledColor
    {
        get => (Color)GetValue(DisabledColorProperty);
        set => SetValue(DisabledColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the font size for the content text.
    /// </summary>
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the size of the radio circle.
    /// </summary>
    public double RadioSize
    {
        get => (double)GetValue(RadioSizeProperty);
        set => SetValue(RadioSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the spacing between the radio circle and content.
    /// </summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
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
    /// Gets or sets the border width.
    /// </summary>
    public double BorderWidth
    {
        get => (double)GetValue(BorderWidthProperty);
        set => SetValue(BorderWidthProperty, value);
    }

    /// <summary>
    /// Gets whether the control is currently hovered.
    /// </summary>
    public bool IsHovered { get; private set; }

    #endregion

    #region Group Management

    private static readonly Dictionary<string, List<WeakReference<SkiaRadioButton>>> _groups = new();

    #endregion

    #region Events

    /// <summary>
    /// Occurs when IsChecked changes.
    /// </summary>
    public event EventHandler<CheckedChangedEventArgs>? CheckedChanged;

    #endregion

    #region Constructor

    public SkiaRadioButton()
    {
        IsFocusable = true;
    }

    #endregion

    #region Event Handlers

    private void OnIsCheckedChanged()
    {
        if (IsChecked && !string.IsNullOrEmpty(GroupName))
        {
            UncheckOthersInGroup();
        }
        CheckedChanged?.Invoke(this, new CheckedChangedEventArgs(IsChecked));
        SkiaVisualStateManager.GoToState(this, IsChecked
            ? SkiaVisualStateManager.CommonStates.Checked
            : SkiaVisualStateManager.CommonStates.Unchecked);
        Invalidate();
    }

    private void OnGroupNameChanged(string? oldValue, string? newValue)
    {
        RemoveFromGroup(oldValue);
        AddToGroup(newValue);
    }

    #endregion

    #region Group Management Methods

    private void AddToGroup(string? groupName)
    {
        if (string.IsNullOrEmpty(groupName)) return;

        if (!_groups.TryGetValue(groupName, out var group))
        {
            group = new List<WeakReference<SkiaRadioButton>>();
            _groups[groupName] = group;
        }

        // Clean up dead references
        group.RemoveAll(wr => !wr.TryGetTarget(out _));
        group.Add(new WeakReference<SkiaRadioButton>(this));
    }

    private void RemoveFromGroup(string? groupName)
    {
        if (string.IsNullOrEmpty(groupName)) return;

        if (_groups.TryGetValue(groupName, out var group))
        {
            group.RemoveAll(wr => !wr.TryGetTarget(out var target) || target == this);
            if (group.Count == 0) _groups.Remove(groupName);
        }
    }

    private void UncheckOthersInGroup()
    {
        if (string.IsNullOrEmpty(GroupName)) return;

        if (_groups.TryGetValue(GroupName, out var group))
        {
            foreach (var weakRef in group)
            {
                if (weakRef.TryGetTarget(out var radioButton) && radioButton != this && radioButton.IsChecked)
                {
                    radioButton.SetValue(IsCheckedProperty, false);
                }
            }
        }
    }

    #endregion

    #region Rendering

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var radioSize = (float)RadioSize;
        var fontSize = (float)FontSize;
        var spacing = (float)Spacing;
        var borderWidth = (float)BorderWidth;

        var radioRadius = radioSize / 2;
        var radioCenterX = bounds.Left + radioRadius;
        var radioCenterY = bounds.MidY;

        // Get colors
        var radioColorSK = ToSKColor(RadioColor);
        var uncheckedColorSK = ToSKColor(UncheckedColor);
        var textColorSK = ToSKColor(TextColor);
        var disabledColorSK = ToSKColor(DisabledColor);
        var borderColorSK = ToSKColor(BorderColor);

        // Draw focus ring behind radio circle
        if (IsFocused)
        {
            using var focusPaint = new SKPaint
            {
                Color = radioColorSK.WithAlpha(80),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawCircle(radioCenterX, radioCenterY, radioRadius + 4, focusPaint);
        }

        // Draw outer circle (border)
        using var outerPaint = new SKPaint
        {
            Color = IsEnabled
                ? (IsChecked ? radioColorSK : (IsHovered ? radioColorSK : uncheckedColorSK))
                : disabledColorSK,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = borderWidth,
            IsAntialias = true
        };
        canvas.DrawCircle(radioCenterX, radioCenterY, radioRadius - 1, outerPaint);

        // Draw inner filled circle when checked
        if (IsChecked)
        {
            using var innerPaint = new SKPaint
            {
                Color = IsEnabled ? radioColorSK : disabledColorSK,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawCircle(radioCenterX, radioCenterY, radioRadius - 5, innerPaint);
        }

        // Draw content text
        if (!string.IsNullOrEmpty(Content))
        {
            using var font = new SKFont(SKTypeface.Default, fontSize);
            using var textPaint = new SKPaint(font)
            {
                Color = IsEnabled ? textColorSK : disabledColorSK,
                IsAntialias = true
            };

            var textX = bounds.Left + radioSize + spacing;
            var textBounds = new SKRect();
            textPaint.MeasureText(Content, ref textBounds);
            canvas.DrawText(Content, textX, bounds.MidY - textBounds.MidY, textPaint);
        }
    }

    #endregion

    #region Pointer Events

    public override void OnPointerEntered(PointerEventArgs e)
    {
        if (IsEnabled)
        {
            IsHovered = true;
            SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.PointerOver);
            Invalidate();
        }
    }

    public override void OnPointerExited(PointerEventArgs e)
    {
        IsHovered = false;
        SkiaVisualStateManager.GoToState(this, IsEnabled
            ? SkiaVisualStateManager.CommonStates.Normal
            : SkiaVisualStateManager.CommonStates.Disabled);
        Invalidate();
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;
        if (!IsChecked) IsChecked = true;
        e.Handled = true;
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        // No action needed
    }

    #endregion

    #region Keyboard Events

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        if (e.Key == Key.Space || e.Key == Key.Enter)
        {
            if (!IsChecked) IsChecked = true;
            e.Handled = true;
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

    protected override Size MeasureOverride(Size availableSize)
    {
        var radioSize = (float)RadioSize;
        var fontSize = (float)FontSize;
        var spacing = (float)Spacing;

        var textWidth = 0f;
        if (!string.IsNullOrEmpty(Content))
        {
            using var font = new SKFont(SKTypeface.Default, fontSize);
            using var paint = new SKPaint(font);
            textWidth = paint.MeasureText(Content) + spacing;
        }
        return new Size(radioSize + textWidth, Math.Max(radioSize, fontSize * 1.5f));
    }

    #endregion
}
