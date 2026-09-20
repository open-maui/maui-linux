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
    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(
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

        using var font = SkiaFontFactory.Create(
            RenderContext?.Resources.GetTypeface(fontFamily, GetFontStyle()) ?? SKTypeface.Default,
            fontSize);

        for (int i = 0; i <= text.Length; i++)
        {
            var substring = text.Substring(0, i);
            var width = font.MeasureText(substring);
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

    private SKColor ToSKColor(Color? color) => TextRenderingHelper.ToSKColor(color, SkiaTheme.TextPrimarySK);

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

    private SKFontStyle GetFontStyle() => TextRenderingHelper.GetFontStyle(FontAttributes);

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

        using var font = SkiaFontFactory.Create(
            RenderContext?.Resources.GetTypeface(fontFamily, GetFontStyle()) ?? SKTypeface.Default,
            fontSize);

        using var paint = new SKPaint
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
            DrawSingleLineText(canvas, paint, font, contentBounds, displayText);
        }
    }

    private void DrawSingleLineText(SKCanvas canvas, SKPaint paint, SKFont font, SKRect bounds, string text)
    {
        font.MeasureText(text, out var textBounds);

        // Apply truncation if needed
        string displayText = text;
        float availableWidth = bounds.Width;

        if (textBounds.Width > availableWidth && LineBreakMode != LineBreakMode.NoWrap)
        {
            displayText = TruncateText(text, font, availableWidth, LineBreakMode);
            font.MeasureText(displayText, out textBounds);
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
            TextAlignment.Center => TextRenderingHelper.BaselineForVerticalCenter(font, bounds.MidY),
            TextAlignment.End => bounds.Bottom - textBounds.Bottom,
            _ => TextRenderingHelper.BaselineForVerticalCenter(font, bounds.MidY)
        };

        // Draw selection highlight if applicable
        if (_selectionStart >= 0 && _selectionLength != 0)
        {
            DrawSelectionHighlight(canvas, font, x, y, displayText, textBounds);
        }

        DrawTextWithSpacing(canvas, displayText, x, y, font, paint);
        DrawTextDecorations(canvas, paint, x, y, textBounds);
    }

    private void DrawSelectionHighlight(SKCanvas canvas, SKFont font, float x, float y, string text, SKRect textBounds)
    {
        var selStart = Math.Min(_selectionStart, _selectionStart + _selectionLength);
        var selEnd = Math.Max(_selectionStart, _selectionStart + _selectionLength);

        // Clamp to text length
        selStart = Math.Max(0, Math.Min(selStart, text.Length));
        selEnd = Math.Max(0, Math.Min(selEnd, text.Length));

        if (selStart >= selEnd) return;

        var textToStart = text.Substring(0, selStart);
        var textToEnd = text.Substring(0, selEnd);

        float startX = x + font.MeasureText(textToStart);
        float endX = x + font.MeasureText(textToEnd);

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
        // LineHeight -1 means platform default (use 1.2 multiplier for readable line spacing)
        double effectiveLineHeight = LineHeight < 0 ? 1.2 : LineHeight;
        float lineHeight = (float)(FontSize * effectiveLineHeight);
        float y = bounds.Top;
        int lineCount = 0;

        var lines = WrapText(text, font, bounds.Width);

        foreach (var line in lines)
        {
            if (MaxLines > 0 && lineCount >= MaxLines) break;
            if (y + lineHeight > bounds.Bottom && MaxLines == 0) break;

            font.MeasureText(line, out var textBounds);

            float textWidth = textBounds.Width;
            if (CharacterSpacing != 0 && line.Length > 1)
            {
                textWidth += (float)(CharacterSpacing * (line.Length - 1));
            }

            // Use FlowDirection-aware positioning
            float x = GetHorizontalPosition(HorizontalTextAlignment, bounds.Left, bounds.Right, textWidth);

            float textY = y - textBounds.Top;
            DrawTextWithSpacing(canvas, line, x, textY, font, paint);
            DrawTextDecorations(canvas, paint, x, textY, textBounds);

            y += lineHeight;
            lineCount++;
        }
    }

    private void DrawTextWithSpacing(SKCanvas canvas, string text, float x, float y, SKFont font, SKPaint paint)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Get the preferred typeface from the current paint
        var fontFamily = string.IsNullOrEmpty(FontFamily) ? "Sans" : FontFamily;
        var preferredTypeface = RenderContext?.Resources.GetTypeface(fontFamily, GetFontStyle())
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
                using var charFont = SkiaFontFactory.Create(run.Typeface, fontSize);
                using var charPaint = new SKPaint
                {
                    Color = paint.Color,
                    IsAntialias = true
                };

                canvas.DrawText(charStr, currentX, y, charFont, charPaint);
                currentX += charFont.MeasureText(charStr) + (float)CharacterSpacing;
            }
        }
    }

    /// <summary>
    /// Draws text with font fallback for emoji, CJK, and other scripts.
    /// </summary>
    private void DrawTextWithFallback(SKCanvas canvas, string text, float x, float y, SKPaint paint, SKTypeface preferredTypeface)
        => TextRenderingHelper.DrawTextWithFallback(canvas, text, x, y, paint, preferredTypeface, FontSize > 0 ? (float)FontSize : 14f);

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
            using var directFont = SkiaFontFactory.Create(preferredTypeface, fontSize);
            canvas.DrawText(text, x, y, directFont, paint);
            return;
        }

        // Multiple runs with different fonts
        float currentX = x;

        foreach (var run in runs)
        {
            using var runFont = SkiaFontFactory.Create(run.Typeface, fontSize);
            using var runPaint = new SKPaint
            {
                Color = paint.Color,
                IsAntialias = true
            };

            canvas.DrawText(run.Text, currentX, y, runFont, runPaint);
            currentX += runFont.MeasureText(run.Text);
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
        var formatted = FormattedText;
        if (formatted == null) return;

        var layout = LayoutFormattedText(formatted, bounds.Width);

        // Vertical alignment of the whole block inside the content bounds.
        float offsetY = VerticalTextAlignment switch
        {
            TextAlignment.Center => (bounds.Height - layout.Height) / 2f,
            TextAlignment.End => bounds.Height - layout.Height,
            _ => 0f,
        };
        if (offsetY < 0) offsetY = 0;

        // Per-span hit rectangles, in label-local coordinates (relative to the
        // view's own top-left, so GestureManager can test a view-relative
        // point against them through Label.GetChildElements).
        var viewBounds = BoundsSK;
        var spanRects = new Dictionary<Span, List<Rect>>();

        foreach (var run in layout.Runs)
        {
            var line = layout.Lines[run.Line];
            float lineX = GetHorizontalPosition(HorizontalTextAlignment, bounds.Left, bounds.Right, line.Width);
            float x = lineX + run.X;
            float lineTop = bounds.Top + offsetY + line.Top;
            float baseline = lineTop + line.Ascent;

            using var paint = new SKPaint
            {
                Color = ToSKColor(run.Span.TextColor ?? TextColor),
                IsAntialias = true,
            };

            if (run.Span.BackgroundColor != null)
            {
                using var bgPaint = new SKPaint { Color = run.Span.BackgroundColor.ToSKColor(), Style = SKPaintStyle.Fill };
                canvas.DrawRect(new SKRect(x, lineTop, x + run.Width, lineTop + line.Height), bgPaint);
            }

            DrawFormattedSpanWithFallback(canvas, run.Text, x, baseline, paint, run.Typeface, run.FontSize);

            var decorations = run.Span.TextDecorations != TextDecorations.None ? run.Span.TextDecorations : TextDecorations;
            if (decorations != TextDecorations.None)
            {
                using var linePaint = new SKPaint { Color = paint.Color, StrokeWidth = Math.Max(1f, run.FontSize / 14f), IsAntialias = true };
                if (decorations.HasFlag(TextDecorations.Underline))
                {
                    float underlineY = baseline + Math.Max(2f, run.FontSize * 0.12f);
                    canvas.DrawLine(x, underlineY, x + run.Width, underlineY, linePaint);
                }
                if (decorations.HasFlag(TextDecorations.Strikethrough))
                {
                    float strikeY = baseline - run.FontSize * 0.28f;
                    canvas.DrawLine(x, strikeY, x + run.Width, strikeY, linePaint);
                }
            }

            if (!spanRects.TryGetValue(run.Span, out var rects))
                spanRects[run.Span] = rects = new List<Rect>();
            rects.Add(new Rect(x - viewBounds.Left, lineTop - viewBounds.Top, run.Width, line.Height));
        }

        PublishSpanRegions(formatted, spanRects);
    }

    #region Formatted text layout

    /// <summary>One contiguous piece of a span on one line.</summary>
    private readonly record struct FormattedRun(Span Span, string Text, int Line, float X, float Width, float FontSize, SKTypeface Typeface);

    private sealed class FormattedLine
    {
        public float Top;
        public float Height;
        public float Ascent;
        public float Width;
    }

    private sealed class FormattedLayout
    {
        public readonly List<FormattedRun> Runs = new();
        public readonly List<FormattedLine> Lines = new();
        public float Width;
        public float Height;
    }

    /// <summary>
    /// Lays FormattedText out into runs and lines for a content width. Shared
    /// by measure and draw so the two agree: each span resolves its own
    /// typeface and size, text wraps at word boundaries (or on a newline) when it
    /// overflows, and a line is as tall as its largest span (font size times
    /// the line-height multiplier) with the baseline at the largest ascent.
    /// </summary>
    private FormattedLayout LayoutFormattedText(FormattedString formatted, float maxWidth)
    {
        var layout = new FormattedLayout();
        double effectiveLineHeight = LineHeight < 0 ? 1.2 : LineHeight;
        float baseFontSize = FontSize > 0 ? (float)FontSize : 14f;
        bool canWrap = maxWidth > 0 && !float.IsInfinity(maxWidth) && !float.IsNaN(maxWidth)
                       && LineBreakMode != LineBreakMode.NoWrap;

        var line = NewLine(layout, 0);
        float x = 0;

        foreach (var span in formatted.Spans)
        {
            var spanText = span.Text;
            if (string.IsNullOrEmpty(spanText)) continue;
            spanText = span.TextTransform switch
            {
                TextTransform.Uppercase => spanText.ToUpperInvariant(),
                TextTransform.Lowercase => spanText.ToLowerInvariant(),
                _ => spanText,
            };

            float spanFontSize = span.FontSize > 0 ? (float)span.FontSize : baseFontSize;
            var typeface = ResolveSpanTypeface(span);
            using var font = SkiaFontFactory.Create(typeface, spanFontSize);
            var metrics = font.Metrics;
            float ascent = -metrics.Ascent;
            float spanLineHeight = (float)(spanFontSize * effectiveLineHeight);

            var paragraphs = spanText.Split('\n');
            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0)
                {
                    // Forced break.
                    line = NewLine(layout, line.Top + line.Height);
                    x = 0;
                }

                foreach (var token in Tokenize(paragraphs[p]))
                {
                    float width = font.MeasureText(token);
                    if (CharacterSpacing != 0 && token.Length > 1)
                        width += (float)(CharacterSpacing * (token.Length - 1));

                    if (canWrap && x > 0 && x + width > maxWidth && !string.IsNullOrWhiteSpace(token))
                    {
                        line = NewLine(layout, line.Top + line.Height);
                        x = 0;
                    }

                    // Grow the line to this span's metrics.
                    line.Height = Math.Max(line.Height, spanLineHeight);
                    line.Ascent = Math.Max(line.Ascent, ascent);

                    // Merge with the previous run of the same span on this line.
                    int last = layout.Runs.Count - 1;
                    if (last >= 0 && layout.Runs[last].Span == span && layout.Runs[last].Line == layout.Lines.Count - 1)
                    {
                        var prev = layout.Runs[last];
                        layout.Runs[last] = prev with { Text = prev.Text + token, Width = prev.Width + width };
                    }
                    else
                    {
                        layout.Runs.Add(new FormattedRun(span, token, layout.Lines.Count - 1, x, width, spanFontSize, typeface));
                    }

                    x += width;
                    line.Width = Math.Max(line.Width, x);
                }
            }
        }

        // An empty trailing line (e.g. text ending in a newline) still takes space.
        foreach (var l in layout.Lines)
        {
            if (l.Height <= 0) l.Height = (float)(baseFontSize * effectiveLineHeight);
        }

        var lastLine = layout.Lines[^1];
        layout.Height = lastLine.Top + lastLine.Height;
        layout.Width = layout.Lines.Max(l => l.Width);
        return layout;
    }

    private static FormattedLine NewLine(FormattedLayout layout, float top)
    {
        var line = new FormattedLine { Top = top };
        layout.Lines.Add(line);
        return line;
    }

    /// <summary>Words with their trailing whitespace attached, so wrapping keeps spaces at line ends.</summary>
    private static IEnumerable<string> Tokenize(string text)
    {
        int i = 0;
        while (i < text.Length)
        {
            int start = i;
            if (char.IsWhiteSpace(text[i]))
            {
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            }
            else
            {
                while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            }
            yield return text.Substring(start, i - start);
        }
    }

    private SKTypeface ResolveSpanTypeface(Span span)
    {
        var family = !string.IsNullOrEmpty(span.FontFamily) ? span.FontFamily
                   : (!string.IsNullOrEmpty(FontFamily) ? FontFamily : "Sans");
        bool isBold = span.FontAttributes.HasFlag(FontAttributes.Bold) || FontAttributes.HasFlag(FontAttributes.Bold);
        bool isItalic = span.FontAttributes.HasFlag(FontAttributes.Italic) || FontAttributes.HasFlag(FontAttributes.Italic);
        var style = new SKFontStyle(
            isBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            isItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        return RenderContext?.Resources.GetTypeface(family, style) ?? SKTypeface.Default;
    }

    /// <summary>
    /// Publishes each span's drawn rectangles as its MAUI hit region so
    /// <c>Label.GetChildElements(point)</c> (used by GestureManager for span
    /// TapGestureRecognizers) finds the span under a view-local point.
    /// Spans that drew nothing get an empty region.
    /// </summary>
    private static void PublishSpanRegions(FormattedString formatted, Dictionary<Span, List<Rect>> spanRects)
    {
        foreach (var span in formatted.Spans)
        {
            var spatial = (Microsoft.Maui.Controls.Internals.ISpatialElement)span;
            spatial.Region = spanRects.TryGetValue(span, out var rects)
                ? Region.FromRectangles(rects)
                : default;
        }
    }

    /// <summary>
    /// The rectangles (label-local) the span was last drawn into; empty until
    /// the label has been drawn. Exposed for tests and tooling.
    /// </summary>
    public IReadOnlyList<Rect> GetSpanRects(Span span)
    {
        var spatial = (Microsoft.Maui.Controls.Internals.ISpatialElement)span;
        var region = spatial.Region;
        return RegionRectangles(region);
    }

    private static IReadOnlyList<Rect> RegionRectangles(Region region)
    {
        // Region keeps its rectangles behind an internal property; read them
        // through the public Contains test would be lossy, so use reflection.
        var prop = typeof(Region).GetProperty("Regions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        if (prop?.GetValue(region) is IReadOnlyList<Rect> rects) return rects;
        return Array.Empty<Rect>();
    }

    #endregion

    private string TruncateText(string text, SKFont font, float maxWidth, LineBreakMode mode)
    {
        if (string.IsNullOrEmpty(text)) return text;

        font.MeasureText(text, out var bounds);
        if (bounds.Width <= maxWidth) return text;

        string ellipsis = "...";
        float ellipsisWidth = font.MeasureText(ellipsis);

        switch (mode)
        {
            case LineBreakMode.HeadTruncation:
                for (int i = 1; i < text.Length; i++)
                {
                    string truncated = ellipsis + text.Substring(i);
                    if (font.MeasureText(truncated) <= maxWidth)
                        return truncated;
                }
                return ellipsis;

            case LineBreakMode.MiddleTruncation:
                int half = text.Length / 2;
                for (int i = 0; i < half; i++)
                {
                    string truncated = text.Substring(0, half - i) + ellipsis + text.Substring(half + i);
                    if (font.MeasureText(truncated) <= maxWidth)
                        return truncated;
                }
                return ellipsis;

            case LineBreakMode.TailTruncation:
            default:
                for (int i = text.Length - 1; i > 0; i--)
                {
                    string truncated = text.Substring(0, i) + ellipsis;
                    if (font.MeasureText(truncated) <= maxWidth)
                        return truncated;
                }
                return ellipsis;
        }
    }

    private List<string> WrapText(string text, SKFont font, float maxWidth)
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
            float paragraphWidth = font.MeasureText(paragraph);
            if (paragraphWidth <= maxWidth + 1.0f)
            {
                lines.Add(paragraph);
                continue;
            }

            if (LineBreakMode == LineBreakMode.CharacterWrap)
            {
                WrapByCharacter(paragraph, font, maxWidth, lines);
            }
            else
            {
                WrapByWord(paragraph, font, maxWidth, lines);
            }
        }

        return lines;
    }

    private void WrapByWord(string text, SKFont font, float maxWidth, List<string> lines)
    {
        var words = text.Split(' ');
        string currentLine = "";

        foreach (var word in words)
        {
            string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
            float width = font.MeasureText(testLine);

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

    private void WrapByCharacter(string text, SKFont font, float maxWidth, List<string> lines)
    {
        string currentLine = "";

        foreach (char c in text)
        {
            string testLine = currentLine + c;
            float width = font.MeasureText(testLine);

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

        using var font = SkiaFontFactory.Create(
            RenderContext?.Resources.GetTypeface(fontFamily, GetFontStyle()) ?? SKTypeface.Default,
            fontSize);

        double width, height;
        // LineHeight -1 means platform default (use 1.2 multiplier for readable line spacing)
        double effectiveLineHeight = LineHeight < 0 ? 1.2 : LineHeight;

        if (FormattedText != null && FormattedText.Spans.Count > 0)
        {
            // Same layout the draw pass uses: per-span fonts, word wrapping at
            // the available content width, line height from the largest span.
            float maxWidth = double.IsInfinity(availableSize.Width) || double.IsNaN(availableSize.Width)
                ? float.PositiveInfinity
                : (float)Math.Max(1.0, availableSize.Width - paddingH);
            var layout = LayoutFormattedText(FormattedText, maxWidth);
            width = layout.Width;
            height = layout.Height;
        }
        else
        {
            // Use advance width (font.MeasureText return value) not bounding box width
            // This must match what WrapText uses for consistency
            width = font.MeasureText(displayText);  // Advance width, not textBounds.Width
            // Height comes from the line height, NOT the ink bounds: ink-bounds
            // height varies with the glyphs present ("Input" with its descender
            // measures taller than "Buttons"), which makes sibling spacing
            // depend on the letters in the text. Line-height measurement is
            // glyph-independent and matches the multi-line branch below.
            height = fontSize * effectiveLineHeight;

            // Account for character spacing
            if (CharacterSpacing != 0 && displayText.Length > 1)
            {
                width += CharacterSpacing * (displayText.Length - 1);
            }

            // Account for multi-line. This must mirror OnDraw's needsMultiLine
            // condition and DrawMultiLineText's math exactly: a wrapping label
            // that reports single-line height gets under-allocated by its
            // layout and its extra lines overdraw the next sibling.
            bool wraps = LineBreakMode == LineBreakMode.WordWrap ||
                         LineBreakMode == LineBreakMode.CharacterWrap;
            if (wraps && !double.IsInfinity(availableSize.Width) && width > availableSize.Width)
            {
                // Wrap with the same helper and width the draw pass will use.
                float contentWidth = (float)Math.Max(1.0, availableSize.Width - paddingH);
                var wrapped = WrapText(displayText, font, contentWidth);
                int lineCount = MaxLines > 0 ? Math.Min(wrapped.Count, MaxLines) : wrapped.Count;
                lineCount = Math.Max(1, lineCount);
                height = lineCount * fontSize * effectiveLineHeight;
                width = Math.Min(width, availableSize.Width);
            }
            else if (displayText.Contains('\n') || MaxLines > 1)
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

