// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered text entry control with full XAML styling and data binding support.
/// Implements IInputContext for IME (Input Method Editor) support.
/// </summary>
public class SkiaEntry : SkiaView, IInputContext
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

    #region IInputContext Implementation

    /// <summary>
    /// Gets or sets the text for IME context.
    /// </summary>
    string IInputContext.Text
    {
        get => Text;
        set => Text = value;
    }

    /// <summary>
    /// Gets or sets the cursor position for IME context.
    /// </summary>
    int IInputContext.CursorPosition
    {
        get => _cursorPosition;
        set => CursorPosition = value;
    }

    /// <summary>
    /// Gets the selection start for IME context.
    /// </summary>
    int IInputContext.SelectionStart => _selectionStart;

    /// <summary>
    /// Gets the selection length for IME context.
    /// </summary>
    int IInputContext.SelectionLength => _selectionLength;

    /// <summary>
    /// Called when IME commits text.
    /// </summary>
    public void OnTextCommitted(string text)
    {
        if (IsReadOnly) return;

        // Delete selection if any
        if (_selectionLength != 0)
        {
            DeleteSelection();
        }

        // Clear pre-edit text
        _preEditText = string.Empty;
        _preEditCursorPosition = 0;

        // Check max length
        if (MaxLength > 0 && Text.Length + text.Length > MaxLength)
        {
            text = text.Substring(0, MaxLength - Text.Length);
        }

        // Insert committed text at cursor
        var newText = Text.Insert(_cursorPosition, text);
        var newPos = _cursorPosition + text.Length;
        Text = newText;
        _cursorPosition = newPos;

        ResetCursorBlink();
        Invalidate();
    }

    /// <summary>
    /// Called when IME pre-edit (composition) text changes.
    /// </summary>
    public void OnPreEditChanged(string preEditText, int cursorPosition)
    {
        _preEditText = preEditText ?? string.Empty;
        _preEditCursorPosition = cursorPosition;
        ResetCursorBlink();
        Invalidate();
    }

    /// <summary>
    /// Called when IME pre-edit ends (cancelled or committed).
    /// </summary>
    public void OnPreEditEnded()
    {
        _preEditText = string.Empty;
        _preEditCursorPosition = 0;
        Invalidate();
    }

    #endregion

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

    protected override void DrawBackground(SKCanvas canvas, SKRect bounds)
    {
        // Skip base background drawing if Entry is transparent
        // (transparent Entry is likely inside a Border that handles appearance)
        var bgColor = ToSKColor(EntryBackgroundColor);
        var baseBgColor = GetEffectiveBackgroundColor();
        if (bgColor.Alpha < 10 && baseBgColor.Alpha < 10)
            return;

        // Otherwise let base class draw
        base.DrawBackground(canvas, bounds);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var bgColor = ToSKColor(EntryBackgroundColor);
        var isTransparent = bgColor.Alpha < 10; // Consider nearly transparent as transparent

        // Only draw background and border if not transparent
        // (transparent means the Entry is likely inside a Border that handles appearance)
        if (!isTransparent)
        {
            // Draw background
            using var bgPaint = new SKPaint
            {
                Color = bgColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            var rect = new SKRoundRect(bounds, (float)CornerRadius);
            canvas.DrawRoundRect(rect, bgPaint);

            // Draw border
            var borderColor = IsFocused ? ToSKColor(FocusedBorderColor) : ToSKColor(BorderColor);
            var borderWidth = IsFocused ? (float)BorderWidth + 1 : (float)BorderWidth;

            using var borderPaint = new SKPaint
            {
                Color = borderColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = borderWidth
            };
            canvas.DrawRoundRect(rect, borderPaint);
        }

        // Calculate content bounds
        var contentBounds = new SKRect(
            bounds.Left + (float)Padding.Left,
            bounds.Top + (float)Padding.Top,
            bounds.Right - (float)Padding.Right,
            bounds.Bottom - (float)Padding.Bottom);

        // Reserve space for clear button if shown
        var clearButtonSize = 20f;
        var clearButtonMargin = 8f;
        var showClear = ShouldShowClearButton();
        if (showClear)
        {
            contentBounds.Right -= clearButtonSize + clearButtonMargin;
        }

        // Set up clipping for text area
        canvas.Save();
        canvas.ClipRect(contentBounds);

        var fontStyle = GetFontStyle();
        var typeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(GetEffectiveFontFamily(), fontStyle)
                      ?? SKTypeface.Default;

        using var font = new SKFont(typeface, (float)FontSize);
        using var paint = new SKPaint(font) { IsAntialias = true };

        var displayText = GetDisplayText();
        // Append pre-edit text at cursor position for IME composition display
        var preEditInsertPos = Math.Min(_cursorPosition, displayText.Length);
        var displayTextWithPreEdit = string.IsNullOrEmpty(_preEditText)
            ? displayText
            : displayText.Insert(preEditInsertPos, _preEditText);
        var hasText = !string.IsNullOrEmpty(displayTextWithPreEdit);

        if (hasText)
        {
            paint.Color = GetEffectiveTextColor();

            // Measure text to cursor position for scrolling
            var textToCursor = displayText.Substring(0, Math.Min(_cursorPosition, displayText.Length));
            var cursorX = paint.MeasureText(textToCursor);

            // Auto-scroll to keep cursor visible
            if (cursorX - _scrollOffset > contentBounds.Width - 10)
            {
                _scrollOffset = cursorX - contentBounds.Width + 10;
            }
            else if (cursorX - _scrollOffset < 0)
            {
                _scrollOffset = cursorX;
            }

            // Draw selection (check != 0 to handle both forward and backward selection)
            if (IsFocused && _selectionLength != 0)
            {
                DrawSelection(canvas, paint, displayText, contentBounds);
            }

            // Calculate text position based on vertical alignment
            var textBounds = new SKRect();
            paint.MeasureText(displayText, ref textBounds);

            float x = contentBounds.Left - _scrollOffset;
            float y = VerticalTextAlignment switch
            {
                TextAlignment.Start => contentBounds.Top - textBounds.Top,
                TextAlignment.End => contentBounds.Bottom - textBounds.Bottom,
                _ => contentBounds.MidY - textBounds.MidY // Center
            };

            // Draw the text with font fallback for emoji/CJK support
            DrawTextWithFallback(canvas, displayTextWithPreEdit, x, y, paint, typeface);

            // Draw underline for pre-edit (composition) text
            if (!string.IsNullOrEmpty(_preEditText))
            {
                DrawPreEditUnderline(canvas, paint, displayText, x, y, contentBounds);
            }

            // Draw cursor
            if (IsFocused && !IsReadOnly && _cursorVisible)
            {
                DrawCursor(canvas, paint, displayText, contentBounds);
            }
        }
        else if (!string.IsNullOrEmpty(Placeholder))
        {
            // Draw placeholder
            paint.Color = GetEffectivePlaceholderColor();

            var textBounds = new SKRect();
            paint.MeasureText(Placeholder, ref textBounds);

            float x = contentBounds.Left;
            float y = contentBounds.MidY - textBounds.MidY;

            canvas.DrawText(Placeholder, x, y, paint);
        }
        else if (IsFocused && !IsReadOnly && _cursorVisible)
        {
            // Draw cursor even with no text
            DrawCursor(canvas, paint, "", contentBounds);
        }

        canvas.Restore();

        // Draw clear button if applicable
        if (showClear)
        {
            DrawClearButton(canvas, bounds, clearButtonSize, clearButtonMargin);
        }
    }

    private bool ShouldShowClearButton()
    {
        if (string.IsNullOrEmpty(Text)) return false;

        // Check both legacy ShowClearButton and MAUI ClearButtonVisibility
        if (ShowClearButton && IsFocused) return true;

        return ClearButtonVisibility switch
        {
            ClearButtonVisibility.WhileEditing => IsFocused,
            ClearButtonVisibility.Never => false,
            _ => false
        };
    }

    private SKFontStyle GetFontStyle()
    {
        bool isBold = FontAttributes.HasFlag(FontAttributes.Bold);
        bool isItalic = FontAttributes.HasFlag(FontAttributes.Italic);

        if (isBold && isItalic)
            return SKFontStyle.BoldItalic;
        if (isBold)
            return SKFontStyle.Bold;
        if (isItalic)
            return SKFontStyle.Italic;
        return SKFontStyle.Normal;
    }

    private void DrawClearButton(SKCanvas canvas, SKRect bounds, float size, float margin)
    {
        var centerX = bounds.Right - margin - size / 2;
        var centerY = bounds.MidY;

        // Draw circle background
        using var circlePaint = new SKPaint
        {
            Color = SkiaTheme.Gray400SK,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawCircle(centerX, centerY, size / 2 - 2, circlePaint);

        // Draw X
        using var xPaint = new SKPaint
        {
            Color = SkiaTheme.BackgroundWhiteSK,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            StrokeCap = SKStrokeCap.Round
        };

        var offset = size / 4 - 1;
        canvas.DrawLine(centerX - offset, centerY - offset, centerX + offset, centerY + offset, xPaint);
        canvas.DrawLine(centerX - offset, centerY + offset, centerX + offset, centerY - offset, xPaint);
    }

    private string GetDisplayText()
    {
        if (IsPassword && !string.IsNullOrEmpty(Text))
        {
            return new string(PasswordChar, Text.Length);
        }
        return Text;
    }

    private void DrawSelection(SKCanvas canvas, SKPaint paint, string displayText, SKRect bounds)
    {
        var selStart = Math.Min(_selectionStart, _selectionStart + _selectionLength);
        var selEnd = Math.Max(_selectionStart, _selectionStart + _selectionLength);

        var textToStart = displayText.Substring(0, selStart);
        var textToEnd = displayText.Substring(0, selEnd);

        var startX = bounds.Left - _scrollOffset + paint.MeasureText(textToStart);
        var endX = bounds.Left - _scrollOffset + paint.MeasureText(textToEnd);

        using var selPaint = new SKPaint
        {
            Color = ToSKColor(SelectionColor),
            Style = SKPaintStyle.Fill
        };

        canvas.DrawRect(startX, bounds.Top, endX - startX, bounds.Height, selPaint);
    }

    private void DrawCursor(SKCanvas canvas, SKPaint paint, string displayText, SKRect bounds)
    {
        var textToCursor = displayText.Substring(0, Math.Min(_cursorPosition, displayText.Length));
        var cursorX = bounds.Left - _scrollOffset + paint.MeasureText(textToCursor);

        using var cursorPaint = new SKPaint
        {
            Color = ToSKColor(CursorColor),
            StrokeWidth = 2,
            IsAntialias = true
        };

        canvas.DrawLine(cursorX, bounds.Top + 2, cursorX, bounds.Bottom - 2, cursorPaint);
    }

    /// <summary>
    /// Draws text with font fallback for emoji, CJK, and other scripts.
    /// </summary>
    private void DrawTextWithFallback(SKCanvas canvas, string text, float x, float y, SKPaint paint, SKTypeface preferredTypeface)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // Use FontFallbackManager for mixed-script text
        var runs = FontFallbackManager.Instance.ShapeTextWithFallback(text, preferredTypeface);

        if (runs.Count <= 1)
        {
            // Single run or no fallback needed - draw directly
            canvas.DrawText(text, x, y, paint);
            return;
        }

        // Multiple runs with different fonts
        float currentX = x;
        foreach (var run in runs)
        {
            using var runFont = new SKFont(run.Typeface, (float)FontSize);
            using var runPaint = new SKPaint(runFont)
            {
                Color = paint.Color,
                IsAntialias = true
            };

            canvas.DrawText(run.Text, currentX, y, runPaint);
            currentX += runPaint.MeasureText(run.Text);
        }
    }

    /// <summary>
    /// Draws underline for IME pre-edit (composition) text.
    /// </summary>
    private void DrawPreEditUnderline(SKCanvas canvas, SKPaint paint, string displayText, float x, float y, SKRect bounds)
    {
        // Calculate pre-edit text position
        var textToCursor = displayText.Substring(0, Math.Min(_cursorPosition, displayText.Length));
        var preEditStartX = x + paint.MeasureText(textToCursor);
        var preEditEndX = preEditStartX + paint.MeasureText(_preEditText);

        // Draw dotted underline to indicate composition
        using var underlinePaint = new SKPaint
        {
            Color = paint.Color,
            StrokeWidth = 1,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash(new float[] { 3, 2 }, 0)
        };

        var underlineY = y + 2;
        canvas.DrawLine(preEditStartX, underlineY, preEditEndX, underlineY, underlinePaint);
    }

    private void ResetCursorBlink()
    {
        _cursorBlinkTime = DateTime.UtcNow;
        _cursorVisible = true;
    }

    /// <summary>
    /// Updates cursor blink animation.
    /// </summary>
    public void UpdateCursorBlink()
    {
        if (!IsFocused) return;

        var elapsed = (DateTime.UtcNow - _cursorBlinkTime).TotalMilliseconds;
        var newVisible = ((int)(elapsed / 500) % 2) == 0;

        if (newVisible != _cursorVisible)
        {
            _cursorVisible = newVisible;
            Invalidate();
        }
    }

    public override void OnTextInput(TextInputEventArgs e)
    {
        if (!IsEnabled || IsReadOnly) return;

        // Ignore control characters (Ctrl+key combinations send ASCII control codes)
        if (!string.IsNullOrEmpty(e.Text) && e.Text.Length == 1 && e.Text[0] < 32)
            return;

        // Delete selection if any
        if (_selectionLength != 0)
        {
            DeleteSelection();
        }

        // Check max length
        if (MaxLength > 0 && Text.Length >= MaxLength)
            return;

        // Insert text at cursor
        var insertText = e.Text;
        if (MaxLength > 0)
        {
            var remaining = MaxLength - Text.Length;
            insertText = insertText.Substring(0, Math.Min(insertText.Length, remaining));
        }

        var newText = Text.Insert(_cursorPosition, insertText);
        var oldPos = _cursorPosition;
        Text = newText;
        _cursorPosition = oldPos + insertText.Length;

        ResetCursorBlink();
        Invalidate();
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        switch (e.Key)
        {
            case Key.Backspace:
                if (!IsReadOnly)
                {
                    if (_selectionLength > 0)
                    {
                        DeleteSelection();
                    }
                    else if (_cursorPosition > 0)
                    {
                        var newText = Text.Remove(_cursorPosition - 1, 1);
                        var newPos = _cursorPosition - 1;
                        Text = newText;
                        _cursorPosition = newPos;
                    }
                    ResetCursorBlink();
                    Invalidate();
                }
                e.Handled = true;
                break;

            case Key.Delete:
                if (!IsReadOnly)
                {
                    if (_selectionLength > 0)
                    {
                        DeleteSelection();
                    }
                    else if (_cursorPosition < Text.Length)
                    {
                        Text = Text.Remove(_cursorPosition, 1);
                    }
                    ResetCursorBlink();
                    Invalidate();
                }
                e.Handled = true;
                break;

            case Key.Left:
                if (_cursorPosition > 0)
                {
                    if (e.Modifiers.HasFlag(KeyModifiers.Shift))
                    {
                        ExtendSelection(-1);
                    }
                    else
                    {
                        ClearSelection();
                        _cursorPosition--;
                    }
                    ResetCursorBlink();
                    Invalidate();
                }
                e.Handled = true;
                break;

            case Key.Right:
                if (_cursorPosition < Text.Length)
                {
                    if (e.Modifiers.HasFlag(KeyModifiers.Shift))
                    {
                        ExtendSelection(1);
                    }
                    else
                    {
                        ClearSelection();
                        _cursorPosition++;
                    }
                    ResetCursorBlink();
                    Invalidate();
                }
                e.Handled = true;
                break;

            case Key.Home:
                if (e.Modifiers.HasFlag(KeyModifiers.Shift))
                {
                    ExtendSelectionTo(0);
                }
                else
                {
                    ClearSelection();
                    _cursorPosition = 0;
                }
                ResetCursorBlink();
                Invalidate();
                e.Handled = true;
                break;

            case Key.End:
                if (e.Modifiers.HasFlag(KeyModifiers.Shift))
                {
                    ExtendSelectionTo(Text.Length);
                }
                else
                {
                    ClearSelection();
                    _cursorPosition = Text.Length;
                }
                ResetCursorBlink();
                Invalidate();
                e.Handled = true;
                break;

            case Key.A:
                if (e.Modifiers.HasFlag(KeyModifiers.Control))
                {
                    SelectAll();
                    e.Handled = true;
                }
                break;

            case Key.C:
                if (e.Modifiers.HasFlag(KeyModifiers.Control))
                {
                    CopyToClipboard();
                    e.Handled = true;
                }
                break;

            case Key.V:
                if (e.Modifiers.HasFlag(KeyModifiers.Control) && !IsReadOnly)
                {
                    PasteFromClipboard();
                    e.Handled = true;
                }
                break;

            case Key.X:
                if (e.Modifiers.HasFlag(KeyModifiers.Control) && !IsReadOnly)
                {
                    CutToClipboard();
                    e.Handled = true;
                }
                break;

            case Key.Enter:
                Completed?.Invoke(this, EventArgs.Empty);
                // Execute ReturnCommand if set and can execute
                if (ReturnCommand?.CanExecute(ReturnCommandParameter) == true)
                {
                    ReturnCommand.Execute(ReturnCommandParameter);
                }
                e.Handled = true;
                break;
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        // Handle right-click context menu
        if (e.Button == PointerButton.Right)
        {
            ShowContextMenu(e.X, e.Y);
            return;
        }

        // Check if clicked on clear button
        if (ShouldShowClearButton())
        {
            var clearButtonSize = 20f;
            var clearButtonMargin = 8f;
            var clearCenterX = (float)(Bounds.Left + Bounds.Width) - clearButtonMargin - clearButtonSize / 2;
            var clearCenterY = (float)(Bounds.Top + Bounds.Height / 2);

            var dx = e.X - clearCenterX;
            var dy = e.Y - clearCenterY;
            if (dx * dx + dy * dy < (clearButtonSize / 2) * (clearButtonSize / 2))
            {
                // Clear button clicked
                Text = "";
                _cursorPosition = 0;
                _selectionLength = 0;
                Invalidate();
                return;
            }
        }

        // Calculate cursor position from click using screen coordinates
        var screenBounds = ScreenBounds;
        var clickX = e.X - (float)screenBounds.Left - (float)Padding.Left + _scrollOffset;
        _cursorPosition = GetCharacterIndexAtX(clickX);

        // Check for double-click (select word or select all)
        var now = DateTime.UtcNow;
        var timeSinceLastClick = (now - _lastClickTime).TotalMilliseconds;
        var distanceFromLastClick = Math.Abs(e.X - _lastClickX);

        if (timeSinceLastClick < DoubleClickThresholdMs && distanceFromLastClick < 10)
        {
            // Double-click: select all or select word based on property
            if (SelectAllOnDoubleClick)
            {
                SelectAll();
            }
            else
            {
                SelectWordAtCursor();
            }
            _lastClickTime = DateTime.MinValue; // Reset to prevent triple-click issues
            _isSelecting = false;
        }
        else
        {
            // Single click: start selection
            _selectionStart = _cursorPosition;
            _selectionLength = 0;
            _isSelecting = true;
            _lastClickTime = now;
            _lastClickX = e.X;
        }

        ResetCursorBlink();
        Invalidate();
    }

    private void SelectWordAtCursor()
    {
        if (string.IsNullOrEmpty(Text)) return;

        // Find word boundaries
        int start = _cursorPosition;
        int end = _cursorPosition;

        // Move start backwards to beginning of word
        while (start > 0 && IsWordChar(Text[start - 1]))
            start--;

        // Move end forwards to end of word
        while (end < Text.Length && IsWordChar(Text[end]))
            end++;

        _selectionStart = start;
        _cursorPosition = end;
        _selectionLength = end - start;
    }

    private static bool IsWordChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!IsEnabled || !_isSelecting) return;

        // Extend selection to current mouse position
        var screenBounds = ScreenBounds;
        var clickX = e.X - (float)screenBounds.Left - (float)Padding.Left + _scrollOffset;
        var newPosition = GetCharacterIndexAtX(clickX);

        if (newPosition != _cursorPosition)
        {
            _cursorPosition = newPosition;
            _selectionLength = _cursorPosition - _selectionStart;
            ResetCursorBlink();
            Invalidate();
        }
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        _isSelecting = false;
    }

    private int GetCharacterIndexAtX(float x)
    {
        if (string.IsNullOrEmpty(Text)) return 0;

        var fontStyle = GetFontStyle();
        var typeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(GetEffectiveFontFamily(), fontStyle)
                      ?? SKTypeface.Default;

        using var font = new SKFont(typeface, (float)FontSize);
        using var paint = new SKPaint(font);

        var displayText = GetDisplayText();

        for (int i = 0; i <= displayText.Length; i++)
        {
            var substring = displayText.Substring(0, i);
            var width = paint.MeasureText(substring);

            if (width >= x)
            {
                // Check if closer to current or previous character
                if (i > 0)
                {
                    var prevWidth = paint.MeasureText(displayText.Substring(0, i - 1));
                    if (x - prevWidth < width - x)
                        return i - 1;
                }
                return i;
            }
        }

        return displayText.Length;
    }

    private void DeleteSelection()
    {
        var start = Math.Min(_selectionStart, _selectionStart + _selectionLength);
        var length = Math.Abs(_selectionLength);

        Text = Text.Remove(start, length);
        _cursorPosition = start;
        _selectionLength = 0;
    }

    private void ClearSelection()
    {
        _selectionLength = 0;
    }

    private void ExtendSelection(int delta)
    {
        if (_selectionLength == 0)
        {
            _selectionStart = _cursorPosition;
        }

        _cursorPosition += delta;
        _selectionLength = _cursorPosition - _selectionStart;
    }

    private void ExtendSelectionTo(int position)
    {
        if (_selectionLength == 0)
        {
            _selectionStart = _cursorPosition;
        }

        _cursorPosition = position;
        _selectionLength = _cursorPosition - _selectionStart;
    }

    /// <summary>
    /// Selects all text.
    /// </summary>
    public void SelectAll()
    {
        _selectionStart = 0;
        _cursorPosition = Text.Length;
        _selectionLength = Text.Length;
        Invalidate();
    }

    private void CopyToClipboard()
    {
        // Password fields should not allow copying
        if (IsPassword) return;
        if (_selectionLength == 0) return;

        var start = Math.Min(_selectionStart, _selectionStart + _selectionLength);
        var length = Math.Abs(_selectionLength);
        var selectedText = Text.Substring(start, length);

        // Use system clipboard via xclip/xsel
        SystemClipboard.SetText(selectedText);
    }

    private void CutToClipboard()
    {
        // Password fields should not allow cutting
        if (IsPassword) return;

        CopyToClipboard();
        DeleteSelection();
        Invalidate();
    }

    private void PasteFromClipboard()
    {
        // Get from system clipboard
        var text = SystemClipboard.GetText();
        if (string.IsNullOrEmpty(text)) return;

        if (_selectionLength != 0)
        {
            DeleteSelection();
        }

        // Check max length
        if (MaxLength > 0)
        {
            var remaining = MaxLength - Text.Length;
            text = text.Substring(0, Math.Min(text.Length, remaining));
        }

        var newText = Text.Insert(_cursorPosition, text);
        var newPos = _cursorPosition + text.Length;
        Text = newText;
        _cursorPosition = newPos;
        Invalidate();
    }

    private void ShowContextMenu(float x, float y)
    {
        Console.WriteLine($"[SkiaEntry] ShowContextMenu at ({x}, {y})");
        bool hasSelection = _selectionLength != 0;
        bool hasText = !string.IsNullOrEmpty(Text);
        bool hasClipboard = !string.IsNullOrEmpty(SystemClipboard.GetText());

        GtkContextMenuService.ShowContextMenu(new List<GtkMenuItem>
        {
            new GtkMenuItem("Cut", () =>
            {
                CutToClipboard();
                Invalidate();
            }, hasSelection),
            new GtkMenuItem("Copy", () =>
            {
                CopyToClipboard();
            }, hasSelection),
            new GtkMenuItem("Paste", () =>
            {
                PasteFromClipboard();
                Invalidate();
            }, hasClipboard),
            GtkMenuItem.Separator,
            new GtkMenuItem("Select All", () =>
            {
                SelectAll();
                Invalidate();
            }, hasText)
        });
    }

    public override void OnFocusGained()
    {
        base.OnFocusGained();
        SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.Focused);

        // Connect to IME service
        _inputMethodService?.SetFocus(this);

        // Update cursor location for IME candidate window positioning
        UpdateImeCursorLocation();
    }

    public override void OnFocusLost()
    {
        base.OnFocusLost();
        SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.Normal);

        // Disconnect from IME service and reset any composition
        _inputMethodService?.SetFocus(null);
        _preEditText = string.Empty;
        _preEditCursorPosition = 0;
    }

    /// <summary>
    /// Updates the IME cursor location for candidate window positioning.
    /// </summary>
    private void UpdateImeCursorLocation()
    {
        if (_inputMethodService == null) return;

        var screenBounds = ScreenBounds;
        var fontStyle = GetFontStyle();
        var typeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(GetEffectiveFontFamily(), fontStyle)
                      ?? SKTypeface.Default;

        using var font = new SKFont(typeface, (float)FontSize);
        using var paint = new SKPaint(font);

        var displayText = GetDisplayText();
        var textToCursor = displayText.Substring(0, Math.Min(_cursorPosition, displayText.Length));
        var cursorX = paint.MeasureText(textToCursor);

        int x = (int)(screenBounds.Left + Padding.Left - _scrollOffset + cursorX);
        int y = (int)(screenBounds.Top + Padding.Top);
        int height = (int)FontSize;

        _inputMethodService.SetCursorLocation(x, y, 2, height);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var fontStyle = GetFontStyle();
        var typeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(GetEffectiveFontFamily(), fontStyle)
                      ?? SKTypeface.Default;

        using var font = new SKFont(typeface, (float)FontSize);

        // Use font metrics for consistent height regardless of text content
        // This prevents size changes when placeholder disappears or text changes
        var metrics = font.Metrics;
        var textHeight = metrics.Descent - metrics.Ascent + metrics.Leading;

        return new Size(
            200, // Default width, will be overridden by layout
            textHeight + Padding.Top + Padding.Bottom + BorderWidth * 2);
    }
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
