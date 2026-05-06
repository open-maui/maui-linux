// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered multiline text editor control with full XAML styling support.
/// Implements IInputContext for IME (Input Method Editor) support.
/// </summary>
public partial class SkiaEditor : SkiaView, IInputContext
{
    #region BindableProperties

    /// <summary>
    /// Bindable property for Text.
    /// </summary>
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(
            nameof(Text),
            typeof(string),
            typeof(SkiaEditor),
            "",
            BindingMode.OneWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).OnTextPropertyChanged((string)o, (string)n));

    /// <summary>
    /// Bindable property for Placeholder.
    /// </summary>
    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(
            nameof(Placeholder),
            typeof(string),
            typeof(SkiaEditor),
            "",
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    /// <summary>
    /// Bindable property for TextColor.
    /// Default is null to match MAUI Editor.TextColor (falls back to platform default).
    /// </summary>
    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(
            nameof(TextColor),
            typeof(Color),
            typeof(SkiaEditor),
            null,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    /// <summary>
    /// Bindable property for PlaceholderColor.
    /// Default is null to match MAUI Editor.PlaceholderColor (falls back to platform default).
    /// </summary>
    public static readonly BindableProperty PlaceholderColorProperty =
        BindableProperty.Create(
            nameof(PlaceholderColor),
            typeof(Color),
            typeof(SkiaEditor),
            null,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    /// <summary>
    /// Bindable property for BorderColor.
    /// </summary>
    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(
            nameof(BorderColor),
            typeof(Color),
            typeof(SkiaEditor),
            Colors.Transparent,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    /// <summary>
    /// Bindable property for SelectionColor.
    /// </summary>
    public static readonly BindableProperty SelectionColorProperty =
        BindableProperty.Create(
            nameof(SelectionColor),
            typeof(Color),
            typeof(SkiaEditor),
            Color.FromRgba(0x21, 0x96, 0xF3, 0x60),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    /// <summary>
    /// Bindable property for CursorColor.
    /// </summary>
    public static readonly BindableProperty CursorColorProperty =
        BindableProperty.Create(
            nameof(CursorColor),
            typeof(Color),
            typeof(SkiaEditor),
            Color.FromRgb(0x21, 0x96, 0xF3),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    /// <summary>
    /// Bindable property for FontFamily.
    /// Default is empty string to match MAUI Editor.FontFamily (falls back to platform default).
    /// </summary>
    public static readonly BindableProperty FontFamilyProperty =
        BindableProperty.Create(
            nameof(FontFamily),
            typeof(string),
            typeof(SkiaEditor),
            string.Empty,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for FontSize.
    /// </summary>
    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(
            nameof(FontSize),
            typeof(double),
            typeof(SkiaEditor),
            14.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for LineHeight.
    /// </summary>
    public static readonly BindableProperty LineHeightProperty =
        BindableProperty.Create(
            nameof(LineHeight),
            typeof(double),
            typeof(SkiaEditor),
            1.4,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for CornerRadius.
    /// </summary>
    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(
            nameof(CornerRadius),
            typeof(double),
            typeof(SkiaEditor),
            4.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    /// <summary>
    /// Bindable property for Padding.
    /// </summary>
    public static new readonly BindableProperty PaddingProperty =
        BindableProperty.Create(
            nameof(Padding),
            typeof(Thickness),
            typeof(SkiaEditor),
            new Thickness(12),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for IsReadOnly.
    /// </summary>
    public static readonly BindableProperty IsReadOnlyProperty =
        BindableProperty.Create(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(SkiaEditor),
            false,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    /// <summary>
    /// Bindable property for MaxLength.
    /// </summary>
    public static readonly BindableProperty MaxLengthProperty =
        BindableProperty.Create(
            nameof(MaxLength),
            typeof(int),
            typeof(SkiaEditor),
            -1,
            BindingMode.TwoWay);

    /// <summary>
    /// Bindable property for AutoSize.
    /// </summary>
    public static readonly BindableProperty AutoSizeProperty =
        BindableProperty.Create(
            nameof(AutoSize),
            typeof(bool),
            typeof(SkiaEditor),
            false,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for FontAttributes.
    /// </summary>
    public static readonly BindableProperty FontAttributesProperty =
        BindableProperty.Create(
            nameof(FontAttributes),
            typeof(FontAttributes),
            typeof(SkiaEditor),
            FontAttributes.None,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for CharacterSpacing.
    /// </summary>
    public static readonly BindableProperty CharacterSpacingProperty =
        BindableProperty.Create(
            nameof(CharacterSpacing),
            typeof(double),
            typeof(SkiaEditor),
            0.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    /// <summary>
    /// Bindable property for IsTextPredictionEnabled.
    /// </summary>
    public static readonly BindableProperty IsTextPredictionEnabledProperty =
        BindableProperty.Create(
            nameof(IsTextPredictionEnabled),
            typeof(bool),
            typeof(SkiaEditor),
            true);

    /// <summary>
    /// Bindable property for IsSpellCheckEnabled.
    /// </summary>
    public static readonly BindableProperty IsSpellCheckEnabledProperty =
        BindableProperty.Create(
            nameof(IsSpellCheckEnabled),
            typeof(bool),
            typeof(SkiaEditor),
            true);

    /// <summary>
    /// Bindable property for SelectionLength.
    /// </summary>
    public static readonly BindableProperty SelectionLengthProperty =
        BindableProperty.Create(
            nameof(SelectionLength),
            typeof(int),
            typeof(SkiaEditor),
            0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    /// <summary>
    /// Bindable property for CursorPosition.
    /// </summary>
    public static readonly BindableProperty CursorPositionProperty =
        BindableProperty.Create(
            nameof(CursorPosition),
            typeof(int),
            typeof(SkiaEditor),
            0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).OnCursorPositionPropertyChanged((int)n));

    /// <summary>
    /// Bindable property for HorizontalTextAlignment.
    /// </summary>
    public static readonly BindableProperty HorizontalTextAlignmentProperty =
        BindableProperty.Create(
            nameof(HorizontalTextAlignment),
            typeof(TextAlignment),
            typeof(SkiaEditor),
            TextAlignment.Start,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    /// <summary>
    /// Bindable property for VerticalTextAlignment.
    /// </summary>
    public static readonly BindableProperty VerticalTextAlignmentProperty =
        BindableProperty.Create(
            nameof(VerticalTextAlignment),
            typeof(TextAlignment),
            typeof(SkiaEditor),
            TextAlignment.Start,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    /// <summary>
    /// Bindable property for background color exposed for MAUI binding.
    /// </summary>
    public static readonly BindableProperty EditorBackgroundColorProperty =
        BindableProperty.Create(
            nameof(EditorBackgroundColor),
            typeof(Color),
            typeof(SkiaEditor),
            Colors.Transparent,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    #endregion

    #region Color Conversion Helper

    /// <summary>
    /// Converts a MAUI Color to SkiaSharp SKColor.
    /// </summary>
    private static SKColor ToSKColor(Color? color) => TextRenderingHelper.ToSKColor(color, SKColors.Transparent);

    /// <summary>
    /// Gets the effective text color (platform default black if null).
    /// </summary>
    private SKColor GetEffectiveTextColor()
    {
        return TextColor != null ? ToSKColor(TextColor) : SkiaTheme.TextPrimarySK;
    }

    /// <summary>
    /// Gets the effective placeholder color (platform default gray if null).
    /// </summary>
    private SKColor GetEffectivePlaceholderColor()
    {
        return PlaceholderColor != null ? ToSKColor(PlaceholderColor) : SkiaTheme.TextPlaceholderSK;
    }

    /// <summary>
    /// Gets the effective font family (platform default "Sans" if empty).
    /// </summary>
    private string GetEffectiveFontFamily() => TextRenderingHelper.GetEffectiveFontFamily(FontFamily);

    /// <summary>
    /// Determines if text should be rendered right-to-left based on FlowDirection.
    /// </summary>
    private bool IsRightToLeft()
    {
        return FlowDirection == FlowDirection.RightToLeft;
    }

    /// <summary>
    /// Gets the horizontal alignment accounting for FlowDirection.
    /// </summary>
    private float GetEffectiveTextX(SKRect contentBounds, float textWidth)
    {
        bool isRtl = IsRightToLeft();

        return HorizontalTextAlignment switch
        {
            TextAlignment.Start => isRtl ? contentBounds.Right - textWidth : contentBounds.Left,
            TextAlignment.Center => contentBounds.MidX - textWidth / 2,
            TextAlignment.End => isRtl ? contentBounds.Left : contentBounds.Right - textWidth,
            _ => isRtl ? contentBounds.Right - textWidth : contentBounds.Left
        };
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the text content.
    /// </summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// Gets or sets the placeholder text.
    /// </summary>
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>
    /// Gets or sets the text color. Null means platform default (black).
    /// </summary>
    public Color? TextColor
    {
        get => (Color?)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the placeholder color. Null means platform default (gray).
    /// </summary>
    public Color? PlaceholderColor
    {
        get => (Color?)GetValue(PlaceholderColorProperty);
        set => SetValue(PlaceholderColorProperty, value);
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
    /// Gets or sets the selection color.
    /// </summary>
    public Color SelectionColor
    {
        get => (Color)GetValue(SelectionColorProperty);
        set => SetValue(SelectionColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the cursor color.
    /// </summary>
    public Color CursorColor
    {
        get => (Color)GetValue(CursorColorProperty);
        set => SetValue(CursorColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the font family.
    /// </summary>
    public string FontFamily
    {
        get => (string)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>
    /// Gets or sets the font size.
    /// </summary>
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the line height multiplier.
    /// </summary>
    public double LineHeight
    {
        get => (double)GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
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
    /// Gets or sets the padding.
    /// </summary>
    public new Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the editor is read-only.
    /// </summary>
    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum length. -1 for unlimited.
    /// </summary>
    public int MaxLength
    {
        get => (int)GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the editor auto-sizes to content.
    /// </summary>
    public bool AutoSize
    {
        get => (bool)GetValue(AutoSizeProperty);
        set => SetValue(AutoSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the font attributes (Bold, Italic, etc.).
    /// </summary>
    public FontAttributes FontAttributes
    {
        get => (FontAttributes)GetValue(FontAttributesProperty);
        set => SetValue(FontAttributesProperty, value);
    }

    /// <summary>
    /// Gets or sets the character spacing.
    /// </summary>
    public double CharacterSpacing
    {
        get => (double)GetValue(CharacterSpacingProperty);
        set => SetValue(CharacterSpacingProperty, value);
    }

    /// <summary>
    /// Gets or sets whether text prediction is enabled.
    /// </summary>
    public bool IsTextPredictionEnabled
    {
        get => (bool)GetValue(IsTextPredictionEnabledProperty);
        set => SetValue(IsTextPredictionEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets whether spell check is enabled.
    /// </summary>
    public bool IsSpellCheckEnabled
    {
        get => (bool)GetValue(IsSpellCheckEnabledProperty);
        set => SetValue(IsSpellCheckEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the cursor position.
    /// </summary>
    public int CursorPosition
    {
        get => _cursorPosition;
        set
        {
            var newValue = Math.Clamp(value, 0, (Text ?? "").Length);
            if (_cursorPosition != newValue)
            {
                _cursorPosition = newValue;
                SetValue(CursorPositionProperty, newValue);
                EnsureCursorVisible();
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the selection length.
    /// </summary>
    public int SelectionLength
    {
        get => _selectionLength;
        set
        {
            if (_selectionLength != value)
            {
                _selectionLength = value;
                SetValue(SelectionLengthProperty, value);
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the horizontal text alignment.
    /// </summary>
    public TextAlignment HorizontalTextAlignment
    {
        get => (TextAlignment)GetValue(HorizontalTextAlignmentProperty);
        set => SetValue(HorizontalTextAlignmentProperty, value);
    }

    /// <summary>
    /// Gets or sets the vertical text alignment.
    /// </summary>
    public TextAlignment VerticalTextAlignment
    {
        get => (TextAlignment)GetValue(VerticalTextAlignmentProperty);
        set => SetValue(VerticalTextAlignmentProperty, value);
    }

    /// <summary>
    /// Gets or sets the editor background color (MAUI-exposed property).
    /// </summary>
    public Color EditorBackgroundColor
    {
        get => (Color)GetValue(EditorBackgroundColorProperty);
        set => SetValue(EditorBackgroundColorProperty, value);
    }

    #endregion

    private void OnCursorPositionPropertyChanged(int newValue)
    {
        var clampedValue = Math.Clamp(newValue, 0, (Text ?? "").Length);
        if (_cursorPosition != clampedValue)
        {
            _cursorPosition = clampedValue;
            EnsureCursorVisible();
            Invalidate();
        }
    }

    private int _cursorPosition;
    private int _selectionStart = -1;
    private int _selectionLength;
    private float _scrollOffsetY;
    private bool _cursorVisible = true;
    private DateTime _lastCursorBlink = DateTime.Now;
    private List<string> _lines = new() { "" };
    private float _wrapWidth = 0; // Available width for word wrapping
    private bool _isSelecting; // For mouse-based text selection
    private DateTime _lastClickTime = DateTime.MinValue;
    private float _lastClickX;
    private float _lastClickY;
    private const double DoubleClickThresholdMs = 400;

    // IME (Input Method Editor) support
    private string _preEditText = string.Empty;
    private int _preEditCursorPosition;
    private IInputMethodService? _inputMethodService;

    /// <summary>
    /// Event raised when text changes.
    /// </summary>
    public event EventHandler? TextChanged;

    /// <summary>
    /// Event raised when editing is completed.
    /// </summary>
    public event EventHandler? Completed;

    public SkiaEditor()
    {
        IsFocusable = true;
        // Get IME service from factory
        _inputMethodService = InputMethodServiceFactory.Instance;
    }

    private void OnTextPropertyChanged(string oldText, string newText)
    {
        var text = newText ?? "";

        if (MaxLength > 0 && text.Length > MaxLength)
        {
            text = text.Substring(0, MaxLength);
            SetValue(TextProperty, text);
            return;
        }

        UpdateLines();
        _cursorPosition = Math.Min(_cursorPosition, text.Length);
        _scrollOffsetY = 0; // Reset scroll when text changes externally
        _selectionLength = 0;
        TextChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }
}
