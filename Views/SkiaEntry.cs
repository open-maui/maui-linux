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
/// Skia-rendered text entry control with full XAML styling and data binding support.
/// Implements IInputContext for IME (Input Method Editor) support.
/// </summary>
public partial class SkiaEntry : SkiaView, IInputContext
{
    #region BindableProperties

    /// <summary>
    /// Bindable property for Text.
    /// </summary>
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(
            nameof(Text),
            typeof(string),
            typeof(SkiaEntry),
            "",
            BindingMode.OneWay,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).OnTextPropertyChanged((string)o, (string)n));

    /// <summary>
    /// Bindable property for Placeholder.
    /// </summary>
    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(
            nameof(Placeholder),
            typeof(string),
            typeof(SkiaEntry),
            "",
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for PlaceholderColor.
    /// Default is null to match MAUI Entry.PlaceholderColor (falls back to platform default).
    /// </summary>
    public static readonly BindableProperty PlaceholderColorProperty =
        BindableProperty.Create(
            nameof(PlaceholderColor),
            typeof(Color),
            typeof(SkiaEntry),
            null,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for TextColor.
    /// Default is null to match MAUI Entry.TextColor (falls back to platform default).
    /// </summary>
    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(
            nameof(TextColor),
            typeof(Color),
            typeof(SkiaEntry),
            null,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for EntryBackgroundColor (specific to entry, separate from base BackgroundColor).
    /// </summary>
    public static readonly BindableProperty EntryBackgroundColorProperty =
        BindableProperty.Create(
            nameof(EntryBackgroundColor),
            typeof(Color),
            typeof(SkiaEntry),
            Colors.Transparent,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for BorderColor.
    /// </summary>
    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(
            nameof(BorderColor),
            typeof(Color),
            typeof(SkiaEntry),
            Color.FromRgb(0xBD, 0xBD, 0xBD),
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for FocusedBorderColor.
    /// </summary>
    public static readonly BindableProperty FocusedBorderColorProperty =
        BindableProperty.Create(
            nameof(FocusedBorderColor),
            typeof(Color),
            typeof(SkiaEntry),
            Color.FromRgb(0x21, 0x96, 0xF3),
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for SelectionColor.
    /// </summary>
    public static readonly BindableProperty SelectionColorProperty =
        BindableProperty.Create(
            nameof(SelectionColor),
            typeof(Color),
            typeof(SkiaEntry),
            Color.FromRgba(0x21, 0x96, 0xF3, 0x80),
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for CursorColor.
    /// </summary>
    public static readonly BindableProperty CursorColorProperty =
        BindableProperty.Create(
            nameof(CursorColor),
            typeof(Color),
            typeof(SkiaEntry),
            Color.FromRgb(0x21, 0x96, 0xF3),
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for FontFamily.
    /// Default is empty string to match MAUI Entry.FontFamily (falls back to platform default).
    /// </summary>
    public static readonly BindableProperty FontFamilyProperty =
        BindableProperty.Create(
            nameof(FontFamily),
            typeof(string),
            typeof(SkiaEntry),
            string.Empty,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for FontSize.
    /// </summary>
    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(
            nameof(FontSize),
            typeof(double),
            typeof(SkiaEntry),
            14.0,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for CornerRadius.
    /// </summary>
    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(
            nameof(CornerRadius),
            typeof(double),
            typeof(SkiaEntry),
            4.0,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for BorderWidth.
    /// </summary>
    public static readonly BindableProperty BorderWidthProperty =
        BindableProperty.Create(
            nameof(BorderWidth),
            typeof(double),
            typeof(SkiaEntry),
            1.0,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for Padding.
    /// </summary>
    public static readonly BindableProperty PaddingProperty =
        BindableProperty.Create(
            nameof(Padding),
            typeof(Thickness),
            typeof(SkiaEntry),
            new Thickness(12, 8),
            propertyChanged: (b, o, n) => ((SkiaEntry)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for IsPassword.
    /// </summary>
    public static readonly BindableProperty IsPasswordProperty =
        BindableProperty.Create(
            nameof(IsPassword),
            typeof(bool),
            typeof(SkiaEntry),
            false,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for PasswordChar.
    /// </summary>
    public static readonly BindableProperty PasswordCharProperty =
        BindableProperty.Create(
            nameof(PasswordChar),
            typeof(char),
            typeof(SkiaEntry),
            '*', // Use asterisk for universal font compatibility
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for MaxLength.
    /// </summary>
    public static readonly BindableProperty MaxLengthProperty =
        BindableProperty.Create(
            nameof(MaxLength),
            typeof(int),
            typeof(SkiaEntry),
            0);

    /// <summary>
    /// Bindable property for SelectAllOnDoubleClick.
    /// When true, double-clicking selects all text instead of just the word.
    /// </summary>
    public static readonly BindableProperty SelectAllOnDoubleClickProperty =
        BindableProperty.Create(
            nameof(SelectAllOnDoubleClick),
            typeof(bool),
            typeof(SkiaEntry),
            false);

    /// <summary>
    /// Bindable property for IsReadOnly.
    /// </summary>
    public static readonly BindableProperty IsReadOnlyProperty =
        BindableProperty.Create(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(SkiaEntry),
            false,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for HorizontalTextAlignment.
    /// </summary>
    public static readonly BindableProperty HorizontalTextAlignmentProperty =
        BindableProperty.Create(
            nameof(HorizontalTextAlignment),
            typeof(TextAlignment),
            typeof(SkiaEntry),
            TextAlignment.Start,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for VerticalTextAlignment.
    /// Default is Start to match MAUI Entry.VerticalTextAlignment.
    /// </summary>
    public static readonly BindableProperty VerticalTextAlignmentProperty =
        BindableProperty.Create(
            nameof(VerticalTextAlignment),
            typeof(TextAlignment),
            typeof(SkiaEntry),
            TextAlignment.Start,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for ShowClearButton.
    /// </summary>
    public static readonly BindableProperty ShowClearButtonProperty =
        BindableProperty.Create(
            nameof(ShowClearButton),
            typeof(bool),
            typeof(SkiaEntry),
            false,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for CharacterSpacing.
    /// </summary>
    public static readonly BindableProperty CharacterSpacingProperty =
        BindableProperty.Create(
            nameof(CharacterSpacing),
            typeof(double),
            typeof(SkiaEntry),
            0.0,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for FontAttributes.
    /// </summary>
    public static readonly BindableProperty FontAttributesProperty =
        BindableProperty.Create(
            nameof(FontAttributes),
            typeof(FontAttributes),
            typeof(SkiaEntry),
            FontAttributes.None,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for ReturnType.
    /// </summary>
    public static readonly BindableProperty ReturnTypeProperty =
        BindableProperty.Create(
            nameof(ReturnType),
            typeof(ReturnType),
            typeof(SkiaEntry),
            ReturnType.Default);

    /// <summary>
    /// Bindable property for ReturnCommand.
    /// </summary>
    public static readonly BindableProperty ReturnCommandProperty =
        BindableProperty.Create(
            nameof(ReturnCommand),
            typeof(System.Windows.Input.ICommand),
            typeof(SkiaEntry),
            null);

    /// <summary>
    /// Bindable property for ReturnCommandParameter.
    /// </summary>
    public static readonly BindableProperty ReturnCommandParameterProperty =
        BindableProperty.Create(
            nameof(ReturnCommandParameter),
            typeof(object),
            typeof(SkiaEntry),
            null);

    /// <summary>
    /// Bindable property for Keyboard.
    /// </summary>
    public static readonly BindableProperty KeyboardProperty =
        BindableProperty.Create(
            nameof(Keyboard),
            typeof(Keyboard),
            typeof(SkiaEntry),
            Keyboard.Default);

    /// <summary>
    /// Bindable property for ClearButtonVisibility.
    /// </summary>
    public static readonly BindableProperty ClearButtonVisibilityProperty =
        BindableProperty.Create(
            nameof(ClearButtonVisibility),
            typeof(ClearButtonVisibility),
            typeof(SkiaEntry),
            ClearButtonVisibility.Never,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for IsTextPredictionEnabled.
    /// </summary>
    public static readonly BindableProperty IsTextPredictionEnabledProperty =
        BindableProperty.Create(
            nameof(IsTextPredictionEnabled),
            typeof(bool),
            typeof(SkiaEntry),
            true);

    /// <summary>
    /// Bindable property for IsSpellCheckEnabled.
    /// </summary>
    public static readonly BindableProperty IsSpellCheckEnabledProperty =
        BindableProperty.Create(
            nameof(IsSpellCheckEnabled),
            typeof(bool),
            typeof(SkiaEntry),
            true);

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
    /// Gets or sets the placeholder color. Null means platform default (gray).
    /// </summary>
    public Color? PlaceholderColor
    {
        get => (Color?)GetValue(PlaceholderColorProperty);
        set => SetValue(PlaceholderColorProperty, value);
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
    /// Gets or sets the entry background color.
    /// </summary>
    public Color EntryBackgroundColor
    {
        get => (Color)GetValue(EntryBackgroundColorProperty);
        set => SetValue(EntryBackgroundColorProperty, value);
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
    /// Gets or sets the focused border color.
    /// </summary>
    public Color FocusedBorderColor
    {
        get => (Color)GetValue(FocusedBorderColorProperty);
        set => SetValue(FocusedBorderColorProperty, value);
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
    /// Gets or sets the corner radius.
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
    /// Gets or sets the padding.
    /// </summary>
    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>
    /// Gets or sets whether this is a password field.
    /// </summary>
    public bool IsPassword
    {
        get => (bool)GetValue(IsPasswordProperty);
        set => SetValue(IsPasswordProperty, value);
    }

    /// <summary>
    /// Gets or sets the password masking character.
    /// </summary>
    public char PasswordChar
    {
        get => (char)GetValue(PasswordCharProperty);
        set => SetValue(PasswordCharProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum text length. 0 = unlimited.
    /// </summary>
    public int MaxLength
    {
        get => (int)GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    /// <summary>
    /// Gets or sets whether double-clicking selects all text instead of just the word.
    /// Useful for URL bars and similar inputs.
    /// </summary>
    public bool SelectAllOnDoubleClick
    {
        get => (bool)GetValue(SelectAllOnDoubleClickProperty);
        set => SetValue(SelectAllOnDoubleClickProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the entry is read-only.
    /// </summary>
    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
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
    /// Gets or sets whether to show the clear button.
    /// </summary>
    public bool ShowClearButton
    {
        get => (bool)GetValue(ShowClearButtonProperty);
        set => SetValue(ShowClearButtonProperty, value);
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
    /// Gets or sets the font attributes (bold, italic).
    /// </summary>
    public FontAttributes FontAttributes
    {
        get => (FontAttributes)GetValue(FontAttributesProperty);
        set => SetValue(FontAttributesProperty, value);
    }

    /// <summary>
    /// Gets or sets the return key type for the soft keyboard.
    /// </summary>
    public ReturnType ReturnType
    {
        get => (ReturnType)GetValue(ReturnTypeProperty);
        set => SetValue(ReturnTypeProperty, value);
    }

    /// <summary>
    /// Gets or sets the command to execute when the return key is pressed.
    /// </summary>
    public System.Windows.Input.ICommand? ReturnCommand
    {
        get => (System.Windows.Input.ICommand?)GetValue(ReturnCommandProperty);
        set => SetValue(ReturnCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the parameter for the return command.
    /// </summary>
    public object? ReturnCommandParameter
    {
        get => GetValue(ReturnCommandParameterProperty);
        set => SetValue(ReturnCommandParameterProperty, value);
    }

    /// <summary>
    /// Gets or sets the keyboard type for this entry.
    /// </summary>
    public Keyboard Keyboard
    {
        get => (Keyboard)GetValue(KeyboardProperty);
        set => SetValue(KeyboardProperty, value);
    }

    /// <summary>
    /// Gets or sets when the clear button is visible.
    /// </summary>
    public ClearButtonVisibility ClearButtonVisibility
    {
        get => (ClearButtonVisibility)GetValue(ClearButtonVisibilityProperty);
        set => SetValue(ClearButtonVisibilityProperty, value);
    }

    /// <summary>
    /// Gets or sets the cursor position.
    /// </summary>
    public int CursorPosition
    {
        get => _cursorPosition;
        set
        {
            _cursorPosition = Math.Clamp(value, 0, Text.Length);
            ResetCursorBlink();
            Invalidate();
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
            _selectionLength = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets whether text prediction is enabled.
    /// Note: This is a hint to the input system; actual behavior depends on platform support.
    /// </summary>
    public bool IsTextPredictionEnabled
    {
        get => (bool)GetValue(IsTextPredictionEnabledProperty);
        set => SetValue(IsTextPredictionEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets whether spell checking is enabled.
    /// Note: This is a hint to the input system; actual behavior depends on platform support.
    /// </summary>
    public bool IsSpellCheckEnabled
    {
        get => (bool)GetValue(IsSpellCheckEnabledProperty);
        set => SetValue(IsSpellCheckEnabledProperty, value);
    }

    #endregion

    private int _cursorPosition;
    private int _selectionStart;
    private int _selectionLength;
    private float _scrollOffset;
    private DateTime _cursorBlinkTime = DateTime.UtcNow;
    private bool _cursorVisible = true;
    private bool _isSelecting; // For mouse-based text selection
    private DateTime _lastClickTime = DateTime.MinValue;
    private float _lastClickX;
    private const double DoubleClickThresholdMs = 400;

    // IME (Input Method Editor) support
    private string _preEditText = string.Empty;
    private int _preEditCursorPosition;
    private IInputMethodService? _inputMethodService;

    /// <summary>
    /// Event raised when text changes.
    /// </summary>
    public event EventHandler<TextChangedEventArgs>? TextChanged;

    /// <summary>
    /// Event raised when Enter is pressed.
    /// </summary>
    public event EventHandler? Completed;

    public SkiaEntry()
    {
        IsFocusable = true;
        // Get IME service from factory
        _inputMethodService = InputMethodServiceFactory.Instance;
    }

    /// <summary>
    /// Converts a MAUI Color to SkiaSharp SKColor for rendering.
    /// </summary>
    private static SKColor ToSKColor(Color? color)
    {
        if (color == null) return SKColors.Transparent;
        return color.ToSKColor();
    }

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
        return PlaceholderColor != null ? ToSKColor(PlaceholderColor) : SkiaTheme.TextDisabledSK;
    }

    /// <summary>
    /// Gets the effective font family (platform default "Sans" if empty).
    /// </summary>
    private string GetEffectiveFontFamily()
    {
        return string.IsNullOrEmpty(FontFamily) ? "Sans" : FontFamily;
    }

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
            TextAlignment.Start => isRtl ? contentBounds.Right - textWidth - _scrollOffset : contentBounds.Left - _scrollOffset,
            TextAlignment.Center => contentBounds.MidX - textWidth / 2,
            TextAlignment.End => isRtl ? contentBounds.Left - _scrollOffset : contentBounds.Right - textWidth - _scrollOffset,
            _ => isRtl ? contentBounds.Right - textWidth - _scrollOffset : contentBounds.Left - _scrollOffset
        };
    }

    private void OnTextPropertyChanged(string oldText, string newText)
    {
        _cursorPosition = Math.Min(_cursorPosition, (newText ?? "").Length);
        _scrollOffset = 0; // Reset scroll when text changes externally
        _selectionLength = 0;
        TextChanged?.Invoke(this, new TextChangedEventArgs(oldText, newText ?? ""));
        Invalidate();
    }

    private string GetDisplayText()
    {
        if (IsPassword && !string.IsNullOrEmpty(Text))
        {
            return new string(PasswordChar, Text.Length);
        }
        return Text;
    }

    private SKFontStyle GetFontStyle() => TextRenderingHelper.GetFontStyle(FontAttributes);
}

/// <summary>
/// Event args for text changed events.
/// </summary>
public class TextChangedEventArgs : EventArgs
{
    public string OldTextValue { get; }
    public string NewTextValue { get; }

    public TextChangedEventArgs(string oldText, string newText)
    {
        OldTextValue = oldText;
        NewTextValue = newText;
    }
}
