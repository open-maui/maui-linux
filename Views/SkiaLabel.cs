// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered label control matching the .NET MAUI Label API.
/// </summary>
public class SkiaLabel : SkiaView
{
    #region BindableProperties

    /// <summary>
    /// Bindable property for Text.
    /// </summary>
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(SkiaLabel),
        string.Empty,
        propertyChanged: (b, o, n) => ((SkiaLabel)b).OnTextChanged());

    /// <summary>
    /// Bindable property for TextColor.
    /// Default is null to match MAUI Label.TextColor (falls back to platform default).
    /// </summary>
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor),
        typeof(Color),
        typeof(SkiaLabel),
        null,
        propertyChanged: (b, o, n) => ((SkiaLabel)b).Invalidate());

    /// <summary>
    /// Bindable property for FontFamily.
    /// </summary>
    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(
        nameof(FontFamily),
        typeof(string),
        typeof(SkiaLabel),
        string.Empty,
        propertyChanged: (b, o, n) => ((SkiaLabel)b).OnFontChanged());

    /// <summary>
    /// Bindable property for FontSize.
    /// </summary>
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize),
        typeof(double),
        typeof(SkiaLabel),
        14.0,
        propertyChanged: (b, o, n) => ((SkiaLabel)b).OnFontChanged());

    /// <summary>
    /// Bindable property for FontAttributes.
    /// </summary>
    public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(
        nameof(FontAttributes),
        typeof(FontAttributes),
        typeof(SkiaLabel),
        FontAttributes.None,
        propertyChanged: (b, o, n) => ((SkiaLabel)b).OnFontChanged());

    /// <summary>
    /// Bindable property for FontAutoScalingEnabled.
    /// </summary>
    public static readonly BindableProperty FontAutoScalingEnabledProperty = BindableProperty.Create(
        nameof(FontAutoScalingEnabled),
        typeof(bool),
        typeof(SkiaLabel),
        true,
        propertyChanged: (b, o, n) => ((SkiaLabel)b).OnFontChanged());

    /// <summary>
    /// Bindable property for CharacterSpacing.
    /// </summary>
    public static readonly BindableProperty CharacterSpacingProperty = BindableProperty.Create(
        nameof(CharacterSpacing),
        typeof(double),
        typeof(SkiaLabel),
        0.0,
        propertyChanged: (b, o, n) => ((SkiaLabel)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for TextDecorations.
    /// </summary>
    public static readonly BindableProperty TextDecorationsProperty = BindableProperty.Create(
        nameof(TextDecorations),
        typeof(TextDecorations),
        typeof(SkiaLabel),
        TextDecorations.None,
        propertyChanged: (b, o, n) => ((SkiaLabel)b).Invalidate());

    /// <summary>
    /// Bindable property for HorizontalTextAlignment.
    /// </summary>
    public static readonly BindableProperty HorizontalTextAlignmentProperty = BindableProperty.Create(
        nameof(HorizontalTextAlignment),
        typeof(TextAlignment),
        typeof(SkiaLabel),
        TextAlignment.Start,
        propertyChanged: (b, o, n) => ((SkiaLabel)b).Invalidate());

    /// <summary>
    /// Bindable property for VerticalTextAlignment.
    /// Default is Start to match MAUI Label.VerticalTextAlignment.
    /// </summary>
    public static readonly BindableProperty VerticalTextAlignmentProperty = BindableProperty.Create(
        nameof(VerticalTextAlignment),
        typeof(TextAlignment),
        typeof(SkiaLabel),
        TextAlignment.Start,
        propertyChanged: (b, o, n) => ((SkiaLabel)b).Invalidate());

    /// <summary>
    /// Bindable property for LineBreakMode.
    /// </summary>
    public static readonly BindableProperty LineBreakModeProperty = BindableProperty.Create(
        nameof(LineBreakMode),
        typeof(LineBreakMode),
        typeof(SkiaLabel),
        LineBreakMode.TailTruncation,
        propertyChanged: (b, o, n) => ((SkiaLabel)b).Invalidate());

    /// <summary>
    /// Bindable property for MaxLines.
    /// </summary>
    public static readonly BindableProperty MaxLinesProperty = BindableProperty.Create(
        nameof(MaxLines),
        typeof(int),
        typeof(SkiaLabel),
        0,
        propertyChanged: (b, o, n) => ((SkiaLabel)b).OnTextChanged());

    /// <summary>
    /// Bindable property for LineHeight.
    /// Default is -1 to match MAUI Label.LineHeight (platform default).
    /// </summary>
    public static readonly BindableProperty LineHeightProperty = BindableProperty.Create(
        nameof(LineHeight),
        typeof(double),
        typeof(SkiaLabel),
        -1.0,
        propertyChanged: (b, o, n) => ((SkiaLabel)b).OnTextChanged());

    /// <summary>
    /// Bindable property for TextTransform.
    /// </summary>
    public static readonly BindableProperty TextTransformProperty = BindableProperty.Create(
        nameof(TextTransform),
        typeof(TextTransform),
        typeof(SkiaLabel),
        TextTransform.Default,
        propertyChanged: (b, o, n) => ((SkiaLabel)b).Invalidate());

    /// <summary>
    /// Bindable property for TextType.
    /// </summary>
    public static readonly BindableProperty TextTypeProperty = BindableProperty.Create(
        nameof(TextType),
        typeof(TextType),
        typeof(SkiaLabel),
        TextType.Text,
        propertyChanged: (b, o, n) => ((SkiaLabel)b).OnTextChanged());

    /// <summary>
    /// Bindable property for Padding.
    /// </summary>
    public static new readonly BindableProperty PaddingProperty = BindableProperty.Create(
        nameof(Padding),
        typeof(Thickness),
        typeof(SkiaLabel),
        new Thickness(0),
        propertyChanged: (b, o, n) => ((SkiaLabel)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for FormattedText.
    /// </summary>
    public static readonly BindableProperty FormattedTextProperty = BindableProperty.Create(
        nameof(FormattedText),
        typeof(FormattedString),
        typeof(SkiaLabel),
        null,
        propertyChanged: (b, o, n) => ((SkiaLabel)b).OnFormattedTextChanged((FormattedString?)o, (FormattedString?)n));

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
    /// Gets or sets the text color.
    /// Null means use platform default (black on Linux).
    /// </summary>
    public Color? TextColor
    {
        get => (Color?)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
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
    /// Gets or sets the font attributes.
    /// </summary>
    public FontAttributes FontAttributes
    {
        get => (FontAttributes)GetValue(FontAttributesProperty);
        set => SetValue(FontAttributesProperty, value);
    }

    /// <summary>
    /// Gets or sets whether font auto-scaling is enabled.
    /// </summary>
    public bool FontAutoScalingEnabled
    {
        get => (bool)GetValue(FontAutoScalingEnabledProperty);
        set => SetValue(FontAutoScalingEnabledProperty, value);
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
    /// Gets or sets the text decorations.
    /// </summary>
    public TextDecorations TextDecorations
    {
        get => (TextDecorations)GetValue(TextDecorationsProperty);
        set => SetValue(TextDecorationsProperty, value);
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
    /// Gets or sets the line break mode.
    /// </summary>
    public LineBreakMode LineBreakMode
    {
        get => (LineBreakMode)GetValue(LineBreakModeProperty);
        set => SetValue(LineBreakModeProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum number of lines.
    /// </summary>
    public int MaxLines
    {
        get => (int)GetValue(MaxLinesProperty);
        set => SetValue(MaxLinesProperty, value);
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
    /// Gets or sets the text transform.
    /// </summary>
    public TextTransform TextTransform
    {
        get => (TextTransform)GetValue(TextTransformProperty);
        set => SetValue(TextTransformProperty, value);
    }

    /// <summary>
    /// Gets or sets the text type.
    /// </summary>
    public TextType TextType
    {
        get => (TextType)GetValue(TextTypeProperty);
        set => SetValue(TextTypeProperty, value);
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
    /// Gets or sets the formatted text.
    /// </summary>
    public FormattedString? FormattedText
    {
        get => (FormattedString?)GetValue(FormattedTextProperty);
        set => SetValue(FormattedTextProperty, value);
    }

    #endregion

    #region Selection State

    private int _selectionStart = -1;
    private int _selectionLength = 0;
    private bool _isSelecting = false;
    private DateTime _lastClickTime = DateTime.MinValue;
    private float _lastClickX;
    private const double DoubleClickThresholdMs = 400;

    /// <summary>
    /// Gets or sets whether text selection is enabled.
    /// </summary>
    public bool IsTextSelectionEnabled { get; set; } = true;

    /// <summary>
    /// Gets the currently selected text.
    /// </summary>
    public string SelectedText
    {
        get
        {
            if (_selectionStart < 0 || _selectionLength == 0) return string.Empty;
            var text = GetDisplayText();
            var start = Math.Min(_selectionStart, _selectionStart + _selectionLength);
            var length = Math.Abs(_selectionLength);
            if (start < 0 || start >= text.Length) return string.Empty;
            return text.Substring(start, Math.Min(length, text.Length - start));
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when the label is tapped.
    /// </summary>
    public event EventHandler? Tapped;

    /// <summary>
    /// Raises the Tapped event.
    /// </summary>
    protected virtual void OnTapped()
    {
        Tapped?.Invoke(this, EventArgs.Empty);
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!IsTextSelectionEnabled || string.IsNullOrEmpty(Text)) return;

        var text = GetDisplayText();
        if (string.IsNullOrEmpty(text)) return;

        // Calculate character position from click
        var screenBounds = ScreenBounds;
        var clickX = e.X - (float)screenBounds.Left - (float)Padding.Left;
        var charIndex = GetCharacterIndexAtX(clickX);

        // Check for double-click (select word)
        var now = DateTime.UtcNow;
        var timeSinceLastClick = (now - _lastClickTime).TotalMilliseconds;
        var distanceFromLastClick = Math.Abs(e.X - _lastClickX);

        if (timeSinceLastClick < DoubleClickThresholdMs && distanceFromLastClick < 10)
        {
            // Double-click: select word
            SelectWordAt(charIndex);
            _lastClickTime = DateTime.MinValue;
            _isSelecting = false;
        }
        else
        {
            // Single click: start selection
            _selectionStart = charIndex;
            _selectionLength = 0;
            _isSelecting = true;
            _lastClickTime = now;
            _lastClickX = e.X;
        }

        Invalidate();
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!IsTextSelectionEnabled || !_isSelecting) return;

        var text = GetDisplayText();
        if (string.IsNullOrEmpty(text)) return;

        var screenBounds = ScreenBounds;
        var clickX = e.X - (float)screenBounds.Left - (float)Padding.Left;
        var charIndex = GetCharacterIndexAtX(clickX);

        _selectionLength = charIndex - _selectionStart;
        Invalidate();
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isSelecting && _selectionLength == 0)
        {
            // No drag happened, it's a tap
            OnTapped();
        }

        _isSelecting = false;
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!IsTextSelectionEnabled) return;

        // Ctrl+A: Select All
        if (e.Key == Key.A && e.Modifiers.HasFlag(KeyModifiers.Control))
        {
            SelectAll();
            e.Handled = true;
        }
        // Ctrl+C: Copy
        else if (e.Key == Key.C && e.Modifiers.HasFlag(KeyModifiers.Control))
        {
            CopyToClipboard();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Selects all text in the label.
    /// </summary>
    public void SelectAll()
    {
        var text = GetDisplayText();
        _selectionStart = 0;
        _selectionLength = text.Length;
        Invalidate();
    }

    /// <summary>
    /// Clears the current selection.
    /// </summary>
    public void ClearSelection()
    {
        _selectionStart = -1;
        _selectionLength = 0;
        Invalidate();
    }

    private void SelectWordAt(int charIndex)
    {
        var text = GetDisplayText();
        if (string.IsNullOrEmpty(text) || charIndex < 0 || charIndex >= text.Length) return;

        int start = charIndex;
        int end = charIndex;

        // Move start backwards to beginning of word
        while (start > 0 && IsWordChar(text[start - 1]))
            start--;

        // Move end forwards to end of word
        while (end < text.Length && IsWordChar(text[end]))
            end++;

        _selectionStart = start;
        _selectionLength = end - start;
    }

    private static bool IsWordChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }

    private int GetCharacterIndexAtX(float x)
    {
        var text = GetDisplayText();
        if (string.IsNullOrEmpty(text)) return 0;

        float fontSize = FontSize > 0 ? (float)FontSize : 14f;
        var fontFamily = string.IsNullOrEmpty(FontFamily) ? "Sans" : FontFamily;

        using var font = new SKFont(
            SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(fontFamily, GetFontStyle()) ?? SKTypeface.Default,
            fontSize);
        using var paint = new SKPaint(font);

        for (int i = 0; i <= text.Length; i++)
        {
            var substring = text.Substring(0, i);
            var width = paint.MeasureText(substring);
            if (CharacterSpacing != 0 && i > 0)
            {
                width += (float)(CharacterSpacing * i);
            }
            if (width > x)
            {
                return i > 0 ? i - 1 : 0;
            }
        }
        return text.Length;
    }

    private void CopyToClipboard()
    {
        var selectedText = SelectedText;
        if (!string.IsNullOrEmpty(selectedText))
        {
            SystemClipboard.SetText(selectedText);
        }
    }

    #endregion

    #region Private Methods

    private void OnTextChanged()
    {
        InvalidateMeasure();
        Invalidate();
    }

    private void OnFontChanged()
    {
        InvalidateMeasure();
        Invalidate();
    }

    private void OnFormattedTextChanged(FormattedString? oldValue, FormattedString? newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnFormattedTextPropertyChanged;
        }
        if (newValue != null)
        {
            newValue.PropertyChanged += OnFormattedTextPropertyChanged;
        }
        OnTextChanged();
    }

    private void OnFormattedTextPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnTextChanged();
    }

    private SKColor ToSKColor(Color? color)
    {
        if (color == null) return SkiaTheme.TextPrimarySK;
        return color.ToSKColor();
    }

    private string GetDisplayText()
    {
        var text = Text ?? string.Empty;

        // Handle TextType.Html by stripping tags (basic implementation)
        if (TextType == TextType.Html)
        {
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", "");
        }

        // Apply text transform
        return TextTransform switch
        {
            TextTransform.Uppercase => text.ToUpperInvariant(),
            TextTransform.Lowercase => text.ToLowerInvariant(),
            _ => text
        };
    }

    private SKFontStyle GetFontStyle()
    {
        bool isBold = FontAttributes.HasFlag(FontAttributes.Bold);
        bool isItalic = FontAttributes.HasFlag(FontAttributes.Italic);

        return new SKFontStyle(
            isBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            isItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
    }

    /// <summary>
    /// Determines if text should be rendered right-to-left based on FlowDirection.
    /// </summary>
    private bool IsRightToLeft()
    {
        return FlowDirection == FlowDirection.RightToLeft;
    }

    /// <summary>
    /// Gets the effective horizontal alignment for the given alignment,
    /// accounting for FlowDirection (RTL flips Start/End).
    /// </summary>
    private float GetHorizontalPosition(TextAlignment alignment, float boundsLeft, float boundsRight, float textWidth)
    {
        bool isRtl = IsRightToLeft();

        return alignment switch
        {
            TextAlignment.Start => isRtl ? boundsRight - textWidth : boundsLeft,
            TextAlignment.Center => (boundsLeft + boundsRight) / 2 - textWidth / 2,
            TextAlignment.End => isRtl ? boundsLeft : boundsRight - textWidth,
            _ => isRtl ? boundsRight - textWidth : boundsLeft
        };
    }

    #endregion

    #region Drawing

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var padding = Padding;
        var contentBounds = new SKRect(
            bounds.Left + (float)padding.Left,
            bounds.Top + (float)padding.Top,
            bounds.Right - (float)padding.Right,
            bounds.Bottom - (float)padding.Bottom);

        // If we have FormattedText, draw that instead
        if (FormattedText != null && FormattedText.Spans.Count > 0)
        {
            DrawFormattedText(canvas, contentBounds);
            return;
        }

        string displayText = GetDisplayText();
        if (string.IsNullOrEmpty(displayText)) return;

        float fontSize = FontSize > 0 ? (float)FontSize : 14f;
        var fontFamily = string.IsNullOrEmpty(FontFamily) ? "Sans" : FontFamily;

        using var font = new SKFont(
            SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(fontFamily, GetFontStyle()) ?? SKTypeface.Default,
            fontSize);

        using var paint = new SKPaint(font)
        {
            Color = ToSKColor(TextColor),
            IsAntialias = true
        };

        // Check if we need multi-line rendering
        bool needsMultiLine = LineBreakMode == LineBreakMode.WordWrap ||
                             LineBreakMode == LineBreakMode.CharacterWrap ||
                             MaxLines > 1 ||
                             displayText.Contains('\n');

        if (needsMultiLine)
        {
            DrawMultiLineText(canvas, paint, font, contentBounds, displayText);
        }
        else
        {
            DrawSingleLineText(canvas, paint, contentBounds, displayText);
        }
    }

    private void DrawSingleLineText(SKCanvas canvas, SKPaint paint, SKRect bounds, string text)
    {
        var textBounds = new SKRect();
        paint.MeasureText(text, ref textBounds);

        // Apply truncation if needed
        string displayText = text;
        float availableWidth = bounds.Width;

        if (textBounds.Width > availableWidth && LineBreakMode != LineBreakMode.NoWrap)
        {
            displayText = TruncateText(text, paint, availableWidth, LineBreakMode);
            paint.MeasureText(displayText, ref textBounds);
        }

        // Account for character spacing in measurement
        float textWidth = textBounds.Width;
        if (CharacterSpacing != 0 && displayText.Length > 1)
        {
            textWidth += (float)(CharacterSpacing * (displayText.Length - 1));
        }

        // Calculate position based on alignment and FlowDirection
        float x = GetHorizontalPosition(HorizontalTextAlignment, bounds.Left, bounds.Right, textWidth);

        float y = VerticalTextAlignment switch
        {
            TextAlignment.Start => bounds.Top - textBounds.Top,
            TextAlignment.Center => bounds.MidY - textBounds.MidY,
            TextAlignment.End => bounds.Bottom - textBounds.Bottom,
            _ => bounds.MidY - textBounds.MidY
        };

        // Draw selection highlight if applicable
        if (_selectionStart >= 0 && _selectionLength != 0)
        {
            DrawSelectionHighlight(canvas, paint, x, y, displayText, textBounds);
        }

        DrawTextWithSpacing(canvas, displayText, x, y, paint);
        DrawTextDecorations(canvas, paint, x, y, textBounds);
    }

    private void DrawSelectionHighlight(SKCanvas canvas, SKPaint paint, float x, float y, string text, SKRect textBounds)
    {
        var selStart = Math.Min(_selectionStart, _selectionStart + _selectionLength);
        var selEnd = Math.Max(_selectionStart, _selectionStart + _selectionLength);

        // Clamp to text length
        selStart = Math.Max(0, Math.Min(selStart, text.Length));
        selEnd = Math.Max(0, Math.Min(selEnd, text.Length));

        if (selStart >= selEnd) return;

        var textToStart = text.Substring(0, selStart);
        var textToEnd = text.Substring(0, selEnd);

        float startX = x + paint.MeasureText(textToStart);
        float endX = x + paint.MeasureText(textToEnd);

        if (CharacterSpacing != 0)
        {
            startX += (float)(CharacterSpacing * selStart);
            endX += (float)(CharacterSpacing * selEnd);
        }

        using var selectionPaint = new SKPaint
        {
            Color = SkiaTheme.PrimaryLightSK,
            Style = SKPaintStyle.Fill
        };

        float selectionTop = y + textBounds.Top;
        float selectionBottom = y + textBounds.Bottom;
        canvas.DrawRect(new SKRect(startX, selectionTop, endX, selectionBottom), selectionPaint);
    }

    private void DrawMultiLineText(SKCanvas canvas, SKPaint paint, SKFont font, SKRect bounds, string text)
    {
        // LineHeight -1 means platform default (use 1.0 multiplier)
        double effectiveLineHeight = LineHeight < 0 ? 1.0 : LineHeight;
        float lineHeight = (float)(FontSize * effectiveLineHeight);
        float y = bounds.Top;
        int lineCount = 0;

        var lines = WrapText(text, paint, bounds.Width);

        foreach (var line in lines)
        {
            if (MaxLines > 0 && lineCount >= MaxLines) break;
            if (y + lineHeight > bounds.Bottom && MaxLines == 0) break;

            var textBounds = new SKRect();
            paint.MeasureText(line, ref textBounds);

            float textWidth = textBounds.Width;
            if (CharacterSpacing != 0 && line.Length > 1)
            {
                textWidth += (float)(CharacterSpacing * (line.Length - 1));
            }

            // Use FlowDirection-aware positioning
            float x = GetHorizontalPosition(HorizontalTextAlignment, bounds.Left, bounds.Right, textWidth);

            float textY = y - textBounds.Top;
            DrawTextWithSpacing(canvas, line, x, textY, paint);
            DrawTextDecorations(canvas, paint, x, textY, textBounds);

            y += lineHeight;
            lineCount++;
        }
    }

    private void DrawTextWithSpacing(SKCanvas canvas, string text, float x, float y, SKPaint paint)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Get the preferred typeface from the current paint
        var fontFamily = string.IsNullOrEmpty(FontFamily) ? "Sans" : FontFamily;
        var preferredTypeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(fontFamily, GetFontStyle())
                               ?? SKTypeface.Default;

        if (CharacterSpacing == 0 || text.Length <= 1)
        {
            // No character spacing - use font fallback for the whole string
            DrawTextWithFallback(canvas, text, x, y, paint, preferredTypeface);
            return;
        }

        // With character spacing, we need to draw character by character with fallback
        float currentX = x;
        float fontSize = FontSize > 0 ? (float)FontSize : 14f;

        // Use font fallback to get runs for proper glyph coverage
        var runs = FontFallbackManager.Instance.ShapeTextWithFallback(text, preferredTypeface);

        foreach (var run in runs)
        {
            // Draw each character in the run with spacing
            foreach (char c in run.Text)
            {
                string charStr = c.ToString();
                using var charFont = new SKFont(run.Typeface, fontSize);
                using var charPaint = new SKPaint(charFont)
                {
                    Color = paint.Color,
                    IsAntialias = true
                };

                canvas.DrawText(charStr, currentX, y, charPaint);
                currentX += charPaint.MeasureText(charStr) + (float)CharacterSpacing;
            }
        }
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
        float fontSize = FontSize > 0 ? (float)FontSize : 14f;
        float currentX = x;

        foreach (var run in runs)
        {
            using var runFont = new SKFont(run.Typeface, fontSize);
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
    /// Draws formatted span text with font fallback for emoji, CJK, and other scripts.
    /// </summary>
    private void DrawFormattedSpanWithFallback(SKCanvas canvas, string text, float x, float y, SKPaint paint, SKTypeface preferredTypeface, float fontSize)
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
            using var runFont = new SKFont(run.Typeface, fontSize);
            using var runPaint = new SKPaint(runFont)
            {
                Color = paint.Color,
                IsAntialias = true
            };

            canvas.DrawText(run.Text, currentX, y, runPaint);
            currentX += runPaint.MeasureText(run.Text);
        }
    }

    private void DrawTextDecorations(SKCanvas canvas, SKPaint paint, float x, float y, SKRect textBounds)
    {
        if (TextDecorations == TextDecorations.None) return;

        using var linePaint = new SKPaint
        {
            Color = paint.Color,
            StrokeWidth = 1,
            IsAntialias = true
        };

        float textWidth = textBounds.Width;
        if (CharacterSpacing != 0)
        {
            // Approximate width adjustment for decorations
            textWidth += (float)(CharacterSpacing * Math.Max(0, Text?.Length - 1 ?? 0));
        }

        if (TextDecorations.HasFlag(TextDecorations.Underline))
        {
            float underlineY = y + 2;
            canvas.DrawLine(x, underlineY, x + textWidth, underlineY, linePaint);
        }

        if (TextDecorations.HasFlag(TextDecorations.Strikethrough))
        {
            float strikeY = y - textBounds.Height / 3;
            canvas.DrawLine(x, strikeY, x + textWidth, strikeY, linePaint);
        }
    }

    private void DrawFormattedText(SKCanvas canvas, SKRect bounds)
    {
        if (FormattedText == null) return;

        float x = bounds.Left;
        float y = bounds.Top;
        // LineHeight -1 means platform default (use 1.0 multiplier)
        double effectiveLineHeight = LineHeight < 0 ? 1.0 : LineHeight;
        float lineHeight = (float)(FontSize * effectiveLineHeight);
        float fontSize = FontSize > 0 ? (float)FontSize : 14f;

        // Calculate baseline for first line
        using var measureFont = new SKFont(SKTypeface.Default, fontSize);
        using var measurePaint = new SKPaint(measureFont);
        var metrics = measurePaint.FontMetrics;
        y -= metrics.Ascent;

        foreach (var span in FormattedText.Spans)
        {
            if (string.IsNullOrEmpty(span.Text)) continue;

            // Get span-specific styling
            var spanFontSize = span.FontSize > 0 ? (float)span.FontSize : fontSize;
            var spanFontFamily = !string.IsNullOrEmpty(span.FontFamily) ? span.FontFamily :
                                 (!string.IsNullOrEmpty(FontFamily) ? FontFamily : "Sans");

            bool isBold = span.FontAttributes.HasFlag(FontAttributes.Bold) ||
                         FontAttributes.HasFlag(FontAttributes.Bold);
            bool isItalic = span.FontAttributes.HasFlag(FontAttributes.Italic) ||
                           FontAttributes.HasFlag(FontAttributes.Italic);

            var fontStyle = new SKFontStyle(
                isBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                isItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

            using var font = new SKFont(
                SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(spanFontFamily, fontStyle) ?? SKTypeface.Default,
                spanFontSize);

            var spanColor = span.TextColor ?? TextColor;
            using var paint = new SKPaint(font)
            {
                Color = ToSKColor(spanColor),
                IsAntialias = true
            };

            var textBounds = new SKRect();
            paint.MeasureText(span.Text, ref textBounds);

            // Check if we need to wrap to next line
            if (x + textBounds.Width > bounds.Right && x > bounds.Left)
            {
                x = bounds.Left;
                y += lineHeight;
            }

            // Use font fallback for this span
            var preferredTypeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(spanFontFamily, fontStyle)
                                   ?? SKTypeface.Default;
            DrawFormattedSpanWithFallback(canvas, span.Text, x, y, paint, preferredTypeface, spanFontSize);

            // Draw span decorations
            if (span.TextDecorations != TextDecorations.None)
            {
                using var linePaint = new SKPaint { Color = paint.Color, StrokeWidth = 1, IsAntialias = true };

                if (span.TextDecorations.HasFlag(TextDecorations.Underline))
                {
                    canvas.DrawLine(x, y + 2, x + textBounds.Width, y + 2, linePaint);
                }
                if (span.TextDecorations.HasFlag(TextDecorations.Strikethrough))
                {
                    float strikeY = y - textBounds.Height / 3;
                    canvas.DrawLine(x, strikeY, x + textBounds.Width, strikeY, linePaint);
                }
            }

            x += textBounds.Width;
        }
    }

    private string TruncateText(string text, SKPaint paint, float maxWidth, LineBreakMode mode)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var bounds = new SKRect();
        paint.MeasureText(text, ref bounds);
        if (bounds.Width <= maxWidth) return text;

        string ellipsis = "...";
        float ellipsisWidth = paint.MeasureText(ellipsis);

        switch (mode)
        {
            case LineBreakMode.HeadTruncation:
                for (int i = 1; i < text.Length; i++)
                {
                    string truncated = ellipsis + text.Substring(i);
                    if (paint.MeasureText(truncated) <= maxWidth)
                        return truncated;
                }
                return ellipsis;

            case LineBreakMode.MiddleTruncation:
                int half = text.Length / 2;
                for (int i = 0; i < half; i++)
                {
                    string truncated = text.Substring(0, half - i) + ellipsis + text.Substring(half + i);
                    if (paint.MeasureText(truncated) <= maxWidth)
                        return truncated;
                }
                return ellipsis;

            case LineBreakMode.TailTruncation:
            default:
                for (int i = text.Length - 1; i > 0; i--)
                {
                    string truncated = text.Substring(0, i) + ellipsis;
                    if (paint.MeasureText(truncated) <= maxWidth)
                        return truncated;
                }
                return ellipsis;
        }
    }

    private List<string> WrapText(string text, SKPaint paint, float maxWidth)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text)) return lines;

        // Split by existing newlines first
        var paragraphs = text.Split('\n');

        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrEmpty(paragraph))
            {
                lines.Add(string.Empty);
                continue;
            }

            // Check if the entire paragraph fits on one line - no need to wrap
            // Use small tolerance to account for floating point precision
            float paragraphWidth = paint.MeasureText(paragraph);
            if (paragraphWidth <= maxWidth + 1.0f)
            {
                lines.Add(paragraph);
                continue;
            }

            if (LineBreakMode == LineBreakMode.CharacterWrap)
            {
                WrapByCharacter(paragraph, paint, maxWidth, lines);
            }
            else
            {
                WrapByWord(paragraph, paint, maxWidth, lines);
            }
        }

        return lines;
    }

    private void WrapByWord(string text, SKPaint paint, float maxWidth, List<string> lines)
    {
        var words = text.Split(' ');
        string currentLine = "";

        foreach (var word in words)
        {
            string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
            float width = paint.MeasureText(testLine);

            if (width > maxWidth && !string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
        {
            lines.Add(currentLine);
        }
    }

    private void WrapByCharacter(string text, SKPaint paint, float maxWidth, List<string> lines)
    {
        string currentLine = "";

        foreach (char c in text)
        {
            string testLine = currentLine + c;
            float width = paint.MeasureText(testLine);

            if (width > maxWidth && !string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
                currentLine = c.ToString();
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
        {
            lines.Add(currentLine);
        }
    }

    #endregion

    #region Measurement

    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        double paddingH = padding.Left + padding.Right;
        double paddingV = padding.Top + padding.Bottom;

        string displayText = GetDisplayText();
        if (string.IsNullOrEmpty(displayText) && (FormattedText == null || FormattedText.Spans.Count == 0))
        {
            return new Size(paddingH, paddingV + FontSize);
        }

        float fontSize = FontSize > 0 ? (float)FontSize : 14f;
        var fontFamily = string.IsNullOrEmpty(FontFamily) ? "Sans" : FontFamily;

        using var font = new SKFont(
            SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(fontFamily, GetFontStyle()) ?? SKTypeface.Default,
            fontSize);

        using var paint = new SKPaint(font);

        double width, height;
        // LineHeight -1 means platform default (use 1.0 multiplier)
        double effectiveLineHeight = LineHeight < 0 ? 1.0 : LineHeight;

        if (FormattedText != null && FormattedText.Spans.Count > 0)
        {
            // Measure formatted text
            width = 0;
            height = fontSize * effectiveLineHeight;
            foreach (var span in FormattedText.Spans)
            {
                if (!string.IsNullOrEmpty(span.Text))
                {
                    width += paint.MeasureText(span.Text);
                }
            }
        }
        else
        {
            // Use advance width (paint.MeasureText return value) not bounding box width
            // This must match what WrapText uses for consistency
            var textBounds = new SKRect();
            paint.MeasureText(displayText, ref textBounds);
            width = paint.MeasureText(displayText);  // Advance width, not textBounds.Width
            height = textBounds.Height;

            // Account for character spacing
            if (CharacterSpacing != 0 && displayText.Length > 1)
            {
                width += CharacterSpacing * (displayText.Length - 1);
            }

            // Account for multi-line
            if (displayText.Contains('\n') || MaxLines > 1)
            {
                var lines = displayText.Split('\n');
                int lineCount = MaxLines > 0 ? Math.Min(lines.Length, MaxLines) : lines.Length;
                height = lineCount * fontSize * effectiveLineHeight;
            }
        }

        width += paddingH;
        height += paddingV;

        // Respect explicit size requests
        if (WidthRequest >= 0)
        {
            width = WidthRequest;
        }
        if (HeightRequest >= 0)
        {
            height = HeightRequest;
        }

        return new Size(Math.Max(width, 1.0), Math.Max(height, 1.0));
    }

    #endregion
}

