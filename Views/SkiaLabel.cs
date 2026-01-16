// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Rendering;
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
    /// </summary>
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor),
        typeof(Color),
        typeof(SkiaLabel),
        Colors.Black,
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
    /// </summary>
    public static readonly BindableProperty VerticalTextAlignmentProperty = BindableProperty.Create(
        nameof(VerticalTextAlignment),
        typeof(TextAlignment),
        typeof(SkiaLabel),
        TextAlignment.Center,
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
    /// </summary>
    public static readonly BindableProperty LineHeightProperty = BindableProperty.Create(
        nameof(LineHeight),
        typeof(double),
        typeof(SkiaLabel),
        1.2,
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
    /// </summary>
    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
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

    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        OnTapped();
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
        if (color == null) return SKColors.Black;
        return new SKColor(
            (byte)(color.Red * 255),
            (byte)(color.Green * 255),
            (byte)(color.Blue * 255),
            (byte)(color.Alpha * 255));
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

        // Calculate position based on alignment
        float x = HorizontalTextAlignment switch
        {
            TextAlignment.Start => bounds.Left,
            TextAlignment.Center => bounds.MidX - textWidth / 2,
            TextAlignment.End => bounds.Right - textWidth,
            _ => bounds.Left
        };

        float y = VerticalTextAlignment switch
        {
            TextAlignment.Start => bounds.Top - textBounds.Top,
            TextAlignment.Center => bounds.MidY - textBounds.MidY,
            TextAlignment.End => bounds.Bottom - textBounds.Bottom,
            _ => bounds.MidY - textBounds.MidY
        };

        DrawTextWithSpacing(canvas, displayText, x, y, paint);
        DrawTextDecorations(canvas, paint, x, y, textBounds);
    }

    private void DrawMultiLineText(SKCanvas canvas, SKPaint paint, SKFont font, SKRect bounds, string text)
    {
        float lineHeight = (float)(FontSize * LineHeight);
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

            float x = HorizontalTextAlignment switch
            {
                TextAlignment.Start => bounds.Left,
                TextAlignment.Center => bounds.MidX - textWidth / 2,
                TextAlignment.End => bounds.Right - textWidth,
                _ => bounds.Left
            };

            float textY = y - textBounds.Top;
            DrawTextWithSpacing(canvas, line, x, textY, paint);
            DrawTextDecorations(canvas, paint, x, textY, textBounds);

            y += lineHeight;
            lineCount++;
        }
    }

    private void DrawTextWithSpacing(SKCanvas canvas, string text, float x, float y, SKPaint paint)
    {
        if (CharacterSpacing == 0 || string.IsNullOrEmpty(text) || text.Length <= 1)
        {
            canvas.DrawText(text, x, y, paint);
            return;
        }

        float currentX = x;
        foreach (char c in text)
        {
            string charStr = c.ToString();
            canvas.DrawText(charStr, currentX, y, paint);
            currentX += paint.MeasureText(charStr) + (float)CharacterSpacing;
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
        float lineHeight = (float)(FontSize * LineHeight);
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

            canvas.DrawText(span.Text, x, y, paint);

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

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        var padding = Padding;
        float paddingH = (float)(padding.Left + padding.Right);
        float paddingV = (float)(padding.Top + padding.Bottom);

        string displayText = GetDisplayText();
        if (string.IsNullOrEmpty(displayText) && (FormattedText == null || FormattedText.Spans.Count == 0))
        {
            return new SKSize(paddingH, paddingV + (float)FontSize);
        }

        float fontSize = FontSize > 0 ? (float)FontSize : 14f;
        var fontFamily = string.IsNullOrEmpty(FontFamily) ? "Sans" : FontFamily;

        using var font = new SKFont(
            SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(fontFamily, GetFontStyle()) ?? SKTypeface.Default,
            fontSize);

        using var paint = new SKPaint(font);

        float width, height;

        if (FormattedText != null && FormattedText.Spans.Count > 0)
        {
            // Measure formatted text
            width = 0;
            height = (float)(fontSize * LineHeight);
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
            var textBounds = new SKRect();
            paint.MeasureText(displayText, ref textBounds);
            width = textBounds.Width;
            height = textBounds.Height;

            // Account for character spacing
            if (CharacterSpacing != 0 && displayText.Length > 1)
            {
                width += (float)(CharacterSpacing * (displayText.Length - 1));
            }

            // Account for multi-line
            if (displayText.Contains('\n') || MaxLines > 1)
            {
                var lines = displayText.Split('\n');
                int lineCount = MaxLines > 0 ? Math.Min(lines.Length, MaxLines) : lines.Length;
                height = (float)(lineCount * fontSize * LineHeight);
            }
        }

        width += paddingH;
        height += paddingV;

        // Respect explicit size requests
        if (WidthRequest >= 0)
        {
            width = (float)WidthRequest;
        }
        if (HeightRequest >= 0)
        {
            height = (float)HeightRequest;
        }

        return new SKSize(Math.Max(width, 1f), Math.Max(height, 1f));
    }

    #endregion
}

