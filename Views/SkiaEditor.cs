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
public class SkiaEditor : SkiaView, IInputContext
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
        return PlaceholderColor != null ? ToSKColor(PlaceholderColor) : SkiaTheme.TextPlaceholderSK;
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

        EnsureCursorVisible();
        Invalidate();
    }

    /// <summary>
    /// Called when IME pre-edit (composition) text changes.
    /// </summary>
    public void OnPreEditChanged(string preEditText, int cursorPosition)
    {
        _preEditText = preEditText ?? string.Empty;
        _preEditCursorPosition = cursorPosition;
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

    private void UpdateLines()
    {
        _lines.Clear();
        var text = Text ?? "";
        if (string.IsNullOrEmpty(text))
        {
            _lines.Add("");
            return;
        }

        using var font = new SKFont(SKTypeface.Default, (float)FontSize);

        // Split by actual newlines first
        var paragraphs = text.Split('\n');

        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrEmpty(paragraph))
            {
                _lines.Add("");
                continue;
            }

            // Word wrap this paragraph if we have a known width
            if (_wrapWidth > 0)
            {
                WrapParagraph(paragraph, font, _wrapWidth);
            }
            else
            {
                _lines.Add(paragraph);
            }
        }

        if (_lines.Count == 0)
        {
            _lines.Add("");
        }
    }

    private void WrapParagraph(string paragraph, SKFont font, float maxWidth)
    {
        var words = paragraph.Split(' ');
        var currentLine = "";

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
            var lineWidth = MeasureText(testLine, font);

            if (lineWidth > maxWidth && !string.IsNullOrEmpty(currentLine))
            {
                // Line too long, save current and start new
                _lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }

        // Add remaining text
        if (!string.IsNullOrEmpty(currentLine))
        {
            _lines.Add(currentLine);
        }
    }

    private (int line, int column) GetLineColumn(int position)
    {
        var pos = 0;
        for (int i = 0; i < _lines.Count; i++)
        {
            var lineLength = _lines[i].Length;
            if (pos + lineLength >= position || i == _lines.Count - 1)
            {
                return (i, position - pos);
            }
            pos += lineLength + 1;
        }
        return (_lines.Count - 1, _lines[^1].Length);
    }

    private int GetPosition(int line, int column)
    {
        var pos = 0;
        for (int i = 0; i < line && i < _lines.Count; i++)
        {
            pos += _lines[i].Length + 1;
        }
        if (line < _lines.Count)
        {
            pos += Math.Min(column, _lines[line].Length);
        }
        return Math.Min(pos, Text.Length);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var paddingLeft = (float)Padding.Left;
        var paddingTop = (float)Padding.Top;
        var paddingRight = (float)Padding.Right;
        var paddingBottom = (float)Padding.Bottom;
        var fontSize = (float)FontSize;
        var lineHeight = (float)LineHeight;
        var cornerRadius = (float)CornerRadius;

        // Update wrap width if bounds changed and re-wrap text
        var newWrapWidth = bounds.Width - paddingLeft - paddingRight;
        if (Math.Abs(newWrapWidth - _wrapWidth) > 1)
        {
            _wrapWidth = newWrapWidth;
            UpdateLines();
        }

        // Draw background
        var bgColor = EditorBackgroundColor != null ? ToSKColor(EditorBackgroundColor) :
            (IsEnabled ? SkiaTheme.BackgroundWhiteSK : SkiaTheme.Gray100SK);
        using var bgPaint = new SKPaint
        {
            Color = bgColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(bounds, cornerRadius), bgPaint);

        // Draw border only if BorderColor is not transparent
        if (BorderColor != null && BorderColor != Colors.Transparent && BorderColor.Alpha > 0)
        {
            using var borderPaint = new SKPaint
            {
                Color = IsFocused ? ToSKColor(CursorColor) : ToSKColor(BorderColor),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = IsFocused ? 2 : 1,
                IsAntialias = true
            };
            canvas.DrawRoundRect(new SKRoundRect(bounds, cornerRadius), borderPaint);
        }

        // Setup text rendering
        using var font = new SKFont(SKTypeface.Default, fontSize);
        var lineSpacing = fontSize * lineHeight;

        // Clip to content area
        var contentRect = new SKRect(
            bounds.Left + paddingLeft,
            bounds.Top + paddingTop,
            bounds.Right - paddingRight,
            bounds.Bottom - paddingBottom);

        canvas.Save();
        canvas.ClipRect(contentRect);
        // Don't translate - let the text draw at absolute positions
        // canvas.Translate(0, -_scrollOffsetY);

        if (string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(Placeholder))
        {
            using var placeholderPaint = new SKPaint(font)
            {
                Color = GetEffectivePlaceholderColor(),
                IsAntialias = true
            };
            // Handle multiline placeholder text by splitting on newlines
            var placeholderLines = Placeholder.Split('\n');
            var y = contentRect.Top + fontSize;
            foreach (var line in placeholderLines)
            {
                canvas.DrawText(line, contentRect.Left, y, placeholderPaint);
                y += lineSpacing;
            }
        }
        else
        {
            var textColor = GetEffectiveTextColor();
            using var textPaint = new SKPaint(font)
            {
                Color = IsEnabled ? textColor : textColor.WithAlpha(128),
                IsAntialias = true
            };
            using var selectionPaint = new SKPaint
            {
                Color = ToSKColor(SelectionColor),
                Style = SKPaintStyle.Fill
            };

            var y = contentRect.Top + fontSize;
            var charIndex = 0;

            for (int lineIndex = 0; lineIndex < _lines.Count; lineIndex++)
            {
                var line = _lines[lineIndex];
                var x = contentRect.Left;

                // Draw selection for this line if applicable
                if (_selectionStart >= 0 && _selectionLength != 0)
                {
                    // Handle both positive and negative selection lengths
                    var selStart = _selectionLength > 0 ? _selectionStart : _selectionStart + _selectionLength;
                    var selEnd = _selectionLength > 0 ? _selectionStart + _selectionLength : _selectionStart;
                    var lineStart = charIndex;
                    var lineEnd = charIndex + line.Length;

                    if (selEnd > lineStart && selStart < lineEnd)
                    {
                        var selStartInLine = Math.Max(0, selStart - lineStart);
                        var selEndInLine = Math.Min(line.Length, selEnd - lineStart);

                        var startX = x + MeasureText(line.Substring(0, selStartInLine), font);
                        var endX = x + MeasureText(line.Substring(0, selEndInLine), font);

                        canvas.DrawRect(new SKRect(startX, y - fontSize, endX, y + lineSpacing - fontSize), selectionPaint);
                    }
                }

                // Determine if pre-edit text should be displayed on this line
                var (cursorLine, cursorCol) = GetLineColumn(_cursorPosition);
                var displayLine = line;
                var hasPreEditOnThisLine = !string.IsNullOrEmpty(_preEditText) && cursorLine == lineIndex;

                if (hasPreEditOnThisLine)
                {
                    // Insert pre-edit text at cursor position within this line
                    var insertPos = Math.Min(cursorCol, line.Length);
                    displayLine = line.Insert(insertPos, _preEditText);
                }

                // Draw the text with font fallback for emoji/CJK support
                DrawTextWithFallback(canvas, displayLine, x, y, textPaint, SKTypeface.Default);

                // Draw underline for pre-edit (composition) text
                if (hasPreEditOnThisLine)
                {
                    DrawPreEditUnderline(canvas, textPaint, line, x, y, contentRect);
                }

                // Draw cursor if on this line
                if (IsFocused && _cursorVisible)
                {
                    if (cursorLine == lineIndex)
                    {
                        // Account for pre-edit text when calculating cursor position
                        var textToCursor = line.Substring(0, Math.Min(cursorCol, line.Length));
                        var cursorX = x + MeasureText(textToCursor, font);

                        // If there's pre-edit text, cursor goes after it
                        if (hasPreEditOnThisLine && _preEditText.Length > 0)
                        {
                            cursorX += MeasureText(_preEditText, font);
                        }

                        using var cursorPaint = new SKPaint
                        {
                            Color = ToSKColor(CursorColor),
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = 2,
                            IsAntialias = true
                        };
                        canvas.DrawLine(cursorX, y - fontSize + 2, cursorX, y + 2, cursorPaint);
                    }
                }

                y += lineSpacing;
                charIndex += line.Length + 1;
            }
        }

        canvas.Restore();

        // Draw scrollbar if needed
        var totalHeight = _lines.Count * fontSize * lineHeight;
        if (totalHeight > contentRect.Height)
        {
            DrawScrollbar(canvas, bounds, contentRect.Height, totalHeight);
        }
    }

    private float MeasureText(string text, SKFont font)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        using var paint = new SKPaint(font);
        return paint.MeasureText(text);
    }

    private void DrawScrollbar(SKCanvas canvas, SKRect bounds, float viewHeight, float contentHeight)
    {
        var scrollbarWidth = 6f;
        var scrollbarMargin = 2f;
        var paddingTop = (float)Padding.Top;
        var scrollbarHeight = Math.Max(20, viewHeight * (viewHeight / contentHeight));
        var scrollbarY = bounds.Top + paddingTop + (_scrollOffsetY / contentHeight) * (viewHeight - scrollbarHeight);

        using var paint = new SKPaint
        {
            Color = SkiaTheme.Shadow25SK,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(
                bounds.Right - scrollbarWidth - scrollbarMargin,
                scrollbarY,
                bounds.Right - scrollbarMargin,
                scrollbarY + scrollbarHeight),
            scrollbarWidth / 2), paint);
    }

    private void EnsureCursorVisible()
    {
        var (line, col) = GetLineColumn(_cursorPosition);
        var fontSize = (float)FontSize;
        var lineHeight = (float)LineHeight;
        var lineSpacing = fontSize * lineHeight;
        var cursorY = line * lineSpacing;
        var viewHeight = Bounds.Height - (float)(Padding.Top + Padding.Bottom);

        if (cursorY < _scrollOffsetY)
        {
            _scrollOffsetY = cursorY;
        }
        else if (cursorY + lineSpacing > _scrollOffsetY + (float)viewHeight)
        {
            _scrollOffsetY = cursorY + lineSpacing - (float)viewHeight;
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        DiagnosticLog.Debug("SkiaEditor", $"OnPointerPressed: Button={e.Button}, IsEnabled={IsEnabled}");
        if (!IsEnabled) return;

        // Handle right-click context menu
        if (e.Button == PointerButton.Right)
        {
            DiagnosticLog.Debug("SkiaEditor", "Right-click detected, showing context menu");
            ShowContextMenu(e.X, e.Y);
            return;
        }

        IsFocused = true;

        // Use screen coordinates for proper hit detection
        var screenBounds = ScreenBounds;
        var paddingLeft = (float)Padding.Left;
        var paddingTop = (float)Padding.Top;
        var contentX = e.X - screenBounds.Left - paddingLeft;
        var contentY = e.Y - screenBounds.Top - paddingTop + _scrollOffsetY;

        var fontSize = (float)FontSize;
        var lineSpacing = fontSize * (float)LineHeight;
        var clickedLine = Math.Clamp((int)(contentY / lineSpacing), 0, _lines.Count - 1);

        using var font = new SKFont(SKTypeface.Default, fontSize);
        var line = _lines[clickedLine];
        var clickedCol = 0;

        for (int i = 0; i <= line.Length; i++)
        {
            var charX = MeasureText(line.Substring(0, i), font);
            if (charX > contentX)
            {
                clickedCol = i > 0 ? i - 1 : 0;
                break;
            }
            clickedCol = i;
        }

        _cursorPosition = GetPosition(clickedLine, clickedCol);

        // Check for double-click (select word)
        var now = DateTime.UtcNow;
        var timeSinceLastClick = (now - _lastClickTime).TotalMilliseconds;
        var distanceFromLastClick = Math.Sqrt(Math.Pow(e.X - _lastClickX, 2) + Math.Pow(e.Y - _lastClickY, 2));

        if (timeSinceLastClick < DoubleClickThresholdMs && distanceFromLastClick < 10)
        {
            // Double-click: select the word at cursor
            SelectWordAtCursor();
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
            _lastClickY = e.Y;
        }

        _cursorVisible = true;
        _lastCursorBlink = DateTime.Now;

        Invalidate();
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!IsEnabled || !_isSelecting) return;

        // Calculate position from mouse coordinates
        var screenBounds = ScreenBounds;
        var paddingLeft = (float)Padding.Left;
        var paddingTop = (float)Padding.Top;
        var contentX = e.X - screenBounds.Left - paddingLeft;
        var contentY = e.Y - screenBounds.Top - paddingTop + _scrollOffsetY;

        var fontSize = (float)FontSize;
        var lineSpacing = fontSize * (float)LineHeight;
        var clickedLine = Math.Clamp((int)(contentY / lineSpacing), 0, _lines.Count - 1);

        using var font = new SKFont(SKTypeface.Default, fontSize);
        var line = _lines[clickedLine];
        var clickedCol = 0;

        for (int i = 0; i <= line.Length; i++)
        {
            var charX = MeasureText(line.Substring(0, i), font);
            if (charX > contentX)
            {
                clickedCol = i > 0 ? i - 1 : 0;
                break;
            }
            clickedCol = i;
        }

        var newPosition = GetPosition(clickedLine, clickedCol);
        if (newPosition != _cursorPosition)
        {
            _cursorPosition = newPosition;
            _selectionLength = _cursorPosition - _selectionStart;
            _cursorVisible = true;
            _lastCursorBlink = DateTime.Now;
            Invalidate();
        }
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        _isSelecting = false;
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        var (line, col) = GetLineColumn(_cursorPosition);
        _cursorVisible = true;
        _lastCursorBlink = DateTime.Now;

        switch (e.Key)
        {
            case Key.Left:
                if (_cursorPosition > 0)
                {
                    _cursorPosition--;
                    EnsureCursorVisible();
                }
                e.Handled = true;
                break;

            case Key.Right:
                if (_cursorPosition < Text.Length)
                {
                    _cursorPosition++;
                    EnsureCursorVisible();
                }
                e.Handled = true;
                break;

            case Key.Up:
                if (line > 0)
                {
                    _cursorPosition = GetPosition(line - 1, col);
                    EnsureCursorVisible();
                }
                e.Handled = true;
                break;

            case Key.Down:
                if (line < _lines.Count - 1)
                {
                    _cursorPosition = GetPosition(line + 1, col);
                    EnsureCursorVisible();
                }
                e.Handled = true;
                break;

            case Key.Home:
                _cursorPosition = GetPosition(line, 0);
                EnsureCursorVisible();
                e.Handled = true;
                break;

            case Key.End:
                _cursorPosition = GetPosition(line, _lines[line].Length);
                EnsureCursorVisible();
                e.Handled = true;
                break;

            case Key.Enter:
                if (!IsReadOnly)
                {
                    InsertText("\n");
                }
                e.Handled = true;
                break;

            case Key.Backspace:
                if (!IsReadOnly)
                {
                    if (_selectionLength != 0)
                    {
                        DeleteSelection();
                    }
                    else if (_cursorPosition > 0)
                    {
                        Text = Text.Remove(_cursorPosition - 1, 1);
                        _cursorPosition--;
                    }
                    EnsureCursorVisible();
                }
                e.Handled = true;
                break;

            case Key.Delete:
                if (!IsReadOnly)
                {
                    if (_selectionLength != 0)
                    {
                        DeleteSelection();
                    }
                    else if (_cursorPosition < Text.Length)
                    {
                        Text = Text.Remove(_cursorPosition, 1);
                    }
                }
                e.Handled = true;
                break;

            case Key.Tab:
                if (!IsReadOnly)
                {
                    InsertText("    ");
                }
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
        }

        Invalidate();
    }

    public override void OnTextInput(TextInputEventArgs e)
    {
        if (!IsEnabled || IsReadOnly) return;

        // Ignore control characters (Ctrl+key combinations send ASCII control codes)
        if (!string.IsNullOrEmpty(e.Text) && e.Text.Length == 1 && e.Text[0] < 32)
            return;

        if (!string.IsNullOrEmpty(e.Text))
        {
            InsertText(e.Text);
            e.Handled = true;
        }
    }

    private void InsertText(string text)
    {
        if (_selectionLength > 0)
        {
            var currentText = Text;
            Text = currentText.Remove(_selectionStart, _selectionLength);
            _cursorPosition = _selectionStart;
            _selectionStart = -1;
            _selectionLength = 0;
        }

        if (MaxLength > 0 && Text.Length + text.Length > MaxLength)
        {
            text = text.Substring(0, Math.Max(0, MaxLength - Text.Length));
        }

        if (!string.IsNullOrEmpty(text))
        {
            Text = Text.Insert(_cursorPosition, text);
            _cursorPosition += text.Length;
            EnsureCursorVisible();
        }
    }

    public override void OnScroll(ScrollEventArgs e)
    {
        var fontSize = (float)FontSize;
        var lineHeight = (float)LineHeight;
        var lineSpacing = fontSize * lineHeight;
        var totalHeight = _lines.Count * lineSpacing;
        var viewHeight = (float)Bounds.Height - (float)(Padding.Top + Padding.Bottom);
        var maxScroll = Math.Max(0, totalHeight - viewHeight);

        _scrollOffsetY = Math.Clamp(_scrollOffsetY - e.DeltaY * 3, 0, maxScroll);
        Invalidate();
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

        Completed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Resets the cursor blink timer (shows cursor immediately).
    /// </summary>
    private void ResetCursorBlink()
    {
        _lastCursorBlink = DateTime.Now;
        _cursorVisible = true;
    }

    /// <summary>
    /// Updates cursor blink animation. Called by the application's animation loop.
    /// </summary>
    public void UpdateCursorBlink()
    {
        if (!IsFocused) return;

        var elapsed = (DateTime.Now - _lastCursorBlink).TotalMilliseconds;
        var newVisible = ((int)(elapsed / 500) % 2) == 0;

        if (newVisible != _cursorVisible)
        {
            _cursorVisible = newVisible;
            Invalidate();
        }
    }

    #region Selection and Clipboard

    public void SelectAll()
    {
        _selectionStart = 0;
        _cursorPosition = Text.Length;
        _selectionLength = Text.Length;
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

    private void CopyToClipboard()
    {
        if (_selectionLength == 0) return;

        var start = Math.Min(_selectionStart, _selectionStart + _selectionLength);
        var length = Math.Abs(_selectionLength);
        var selectedText = Text.Substring(start, length);

        // Use system clipboard via xclip/xsel
        SystemClipboard.SetText(selectedText);
    }

    private void CutToClipboard()
    {
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

        InsertText(text);
    }

    private void DeleteSelection()
    {
        if (_selectionLength == 0) return;

        var start = Math.Min(_selectionStart, _selectionStart + _selectionLength);
        var length = Math.Abs(_selectionLength);

        Text = Text.Remove(start, length);
        _cursorPosition = start;
        _selectionStart = -1;
        _selectionLength = 0;
    }

    private void ShowContextMenu(float x, float y)
    {
        DiagnosticLog.Debug("SkiaEditor", $"ShowContextMenu at ({x}, {y}), IsGtkMode={LinuxApplication.IsGtkMode}");
        bool hasSelection = _selectionLength != 0;
        bool hasText = !string.IsNullOrEmpty(Text);
        bool hasClipboard = !string.IsNullOrEmpty(SystemClipboard.GetText());
        bool isEditable = !IsReadOnly;

        if (LinuxApplication.IsGtkMode)
        {
            // Use GTK context menu when running in GTK mode (e.g., with WebView)
            GtkContextMenuService.ShowContextMenu(new List<GtkMenuItem>
            {
                new GtkMenuItem("Cut", () =>
                {
                    CutToClipboard();
                    Invalidate();
                }, hasSelection && isEditable),
                new GtkMenuItem("Copy", () =>
                {
                    CopyToClipboard();
                }, hasSelection),
                new GtkMenuItem("Paste", () =>
                {
                    PasteFromClipboard();
                    Invalidate();
                }, hasClipboard && isEditable),
                GtkMenuItem.Separator,
                new GtkMenuItem("Select All", () =>
                {
                    SelectAll();
                    Invalidate();
                }, hasText)
            });
        }
        else
        {
            // Use Skia-rendered context menu for pure Skia mode (Wayland/X11)
            bool isDarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
            var items = new List<ContextMenuItem>
            {
                new ContextMenuItem("Cut", () =>
                {
                    CutToClipboard();
                    Invalidate();
                }, hasSelection && isEditable),
                new ContextMenuItem("Copy", () =>
                {
                    CopyToClipboard();
                }, hasSelection),
                new ContextMenuItem("Paste", () =>
                {
                    PasteFromClipboard();
                    Invalidate();
                }, hasClipboard && isEditable),
                ContextMenuItem.Separator,
                new ContextMenuItem("Select All", () =>
                {
                    SelectAll();
                    Invalidate();
                }, hasText)
            };
            var menu = new SkiaContextMenu(x, y, items, isDarkTheme);
            LinuxDialogService.ShowContextMenu(menu);
        }
    }

    #endregion

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

    /// <summary>
    /// Updates the IME cursor location for candidate window positioning.
    /// </summary>
    private void UpdateImeCursorLocation()
    {
        if (_inputMethodService == null) return;

        var screenBounds = ScreenBounds;
        var (line, col) = GetLineColumn(_cursorPosition);
        var fontSize = (float)FontSize;
        var lineSpacing = fontSize * (float)LineHeight;

        using var font = new SKFont(SKTypeface.Default, fontSize);
        using var paint = new SKPaint(font);

        var lineText = line < _lines.Count ? _lines[line] : "";
        var textToCursor = lineText.Substring(0, Math.Min(col, lineText.Length));
        var cursorX = paint.MeasureText(textToCursor);

        int x = (int)(screenBounds.Left + Padding.Left + cursorX);
        int y = (int)(screenBounds.Top + Padding.Top + line * lineSpacing - _scrollOffsetY);
        int height = (int)fontSize;

        _inputMethodService.SetCursorLocation(x, y, 2, height);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (AutoSize)
        {
            var fontSize = (float)FontSize;
            var lineHeight = (float)LineHeight;
            var lineSpacing = fontSize * lineHeight;
            var verticalPadding = Padding.Top + Padding.Bottom;
            var height = Math.Max(lineSpacing + verticalPadding, _lines.Count * lineSpacing + verticalPadding);
            return new Size(
                availableSize.Width < double.MaxValue ? availableSize.Width : 200,
                Math.Min(height, availableSize.Height < double.MaxValue ? availableSize.Height : 200));
        }

        return new Size(
            availableSize.Width < double.MaxValue ? Math.Min(availableSize.Width, 200) : 200,
            availableSize.Height < double.MaxValue ? Math.Min(availableSize.Height, 150) : 150);
    }
}
