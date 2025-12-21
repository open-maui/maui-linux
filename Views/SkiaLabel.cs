// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Rendering;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered label control for displaying text with full XAML styling support.
/// </summary>
public class SkiaLabel : SkiaView
{
    #region BindableProperties

    /// <summary>
    /// Bindable property for Text.
    /// </summary>
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(
            nameof(Text),
            typeof(string),
            typeof(SkiaLabel),
            "",
            propertyChanged: (b, o, n) => ((SkiaLabel)b).OnTextChanged());

    /// <summary>
    /// Bindable property for TextColor.
    /// </summary>
    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(
            nameof(TextColor),
            typeof(SKColor),
            typeof(SkiaLabel),
            SKColors.Black,
            propertyChanged: (b, o, n) => ((SkiaLabel)b).Invalidate());

    /// <summary>
    /// Bindable property for FontFamily.
    /// </summary>
    public static readonly BindableProperty FontFamilyProperty =
        BindableProperty.Create(
            nameof(FontFamily),
            typeof(string),
            typeof(SkiaLabel),
            "Sans",
            propertyChanged: (b, o, n) => ((SkiaLabel)b).OnFontChanged());

    /// <summary>
    /// Bindable property for FontSize.
    /// </summary>
    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(
            nameof(FontSize),
            typeof(float),
            typeof(SkiaLabel),
            14f,
            propertyChanged: (b, o, n) => ((SkiaLabel)b).OnFontChanged());

    /// <summary>
    /// Bindable property for IsBold.
    /// </summary>
    public static readonly BindableProperty IsBoldProperty =
        BindableProperty.Create(
            nameof(IsBold),
            typeof(bool),
            typeof(SkiaLabel),
            false,
            propertyChanged: (b, o, n) => ((SkiaLabel)b).OnFontChanged());

    /// <summary>
    /// Bindable property for IsItalic.
    /// </summary>
    public static readonly BindableProperty IsItalicProperty =
        BindableProperty.Create(
            nameof(IsItalic),
            typeof(bool),
            typeof(SkiaLabel),
            false,
            propertyChanged: (b, o, n) => ((SkiaLabel)b).OnFontChanged());

    /// <summary>
    /// Bindable property for IsUnderline.
    /// </summary>
    public static readonly BindableProperty IsUnderlineProperty =
        BindableProperty.Create(
            nameof(IsUnderline),
            typeof(bool),
            typeof(SkiaLabel),
            false,
            propertyChanged: (b, o, n) => ((SkiaLabel)b).Invalidate());

    /// <summary>
    /// Bindable property for IsStrikethrough.
    /// </summary>
    public static readonly BindableProperty IsStrikethroughProperty =
        BindableProperty.Create(
            nameof(IsStrikethrough),
            typeof(bool),
            typeof(SkiaLabel),
            false,
            propertyChanged: (b, o, n) => ((SkiaLabel)b).Invalidate());

    /// <summary>
    /// Bindable property for HorizontalTextAlignment.
    /// </summary>
    public static readonly BindableProperty HorizontalTextAlignmentProperty =
        BindableProperty.Create(
            nameof(HorizontalTextAlignment),
            typeof(TextAlignment),
            typeof(SkiaLabel),
            TextAlignment.Start,
            propertyChanged: (b, o, n) => ((SkiaLabel)b).Invalidate());

    /// <summary>
    /// Bindable property for VerticalTextAlignment.
    /// </summary>
    public static readonly BindableProperty VerticalTextAlignmentProperty =
        BindableProperty.Create(
            nameof(VerticalTextAlignment),
            typeof(TextAlignment),
            typeof(SkiaLabel),
            TextAlignment.Center,
            propertyChanged: (b, o, n) => ((SkiaLabel)b).Invalidate());

    /// <summary>
    /// Bindable property for LineBreakMode.
    /// </summary>
    public static readonly BindableProperty LineBreakModeProperty =
        BindableProperty.Create(
            nameof(LineBreakMode),
            typeof(LineBreakMode),
            typeof(SkiaLabel),
            LineBreakMode.TailTruncation,
            propertyChanged: (b, o, n) => ((SkiaLabel)b).Invalidate());

    /// <summary>
    /// Bindable property for MaxLines.
    /// </summary>
    public static readonly BindableProperty MaxLinesProperty =
        BindableProperty.Create(
            nameof(MaxLines),
            typeof(int),
            typeof(SkiaLabel),
            0,
            propertyChanged: (b, o, n) => ((SkiaLabel)b).OnTextChanged());

    /// <summary>
    /// Bindable property for LineHeight.
    /// </summary>
    public static readonly BindableProperty LineHeightProperty =
        BindableProperty.Create(
            nameof(LineHeight),
            typeof(float),
            typeof(SkiaLabel),
            1.2f,
            propertyChanged: (b, o, n) => ((SkiaLabel)b).OnTextChanged());

    /// <summary>
    /// Bindable property for CharacterSpacing.
    /// </summary>
    public static readonly BindableProperty CharacterSpacingProperty =
        BindableProperty.Create(
            nameof(CharacterSpacing),
            typeof(float),
            typeof(SkiaLabel),
            0f,
            propertyChanged: (b, o, n) => ((SkiaLabel)b).Invalidate());

    /// <summary>
    /// Bindable property for Padding.
    /// </summary>
    public static readonly BindableProperty PaddingProperty =
        BindableProperty.Create(
            nameof(Padding),
            typeof(SKRect),
            typeof(SkiaLabel),
            SKRect.Empty,
            propertyChanged: (b, o, n) => ((SkiaLabel)b).OnTextChanged());

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
    public SKColor TextColor
    {
        get => (SKColor)GetValue(TextColorProperty);
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
    public float FontSize
    {
        get => (float)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the text is bold.
    /// </summary>
    public bool IsBold
    {
        get => (bool)GetValue(IsBoldProperty);
        set => SetValue(IsBoldProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the text is italic.
    /// </summary>
    public bool IsItalic
    {
        get => (bool)GetValue(IsItalicProperty);
        set => SetValue(IsItalicProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the text has underline.
    /// </summary>
    public bool IsUnderline
    {
        get => (bool)GetValue(IsUnderlineProperty);
        set => SetValue(IsUnderlineProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the text has strikethrough.
    /// </summary>
    public bool IsStrikethrough
    {
        get => (bool)GetValue(IsStrikethroughProperty);
        set => SetValue(IsStrikethroughProperty, value);
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
    /// Gets or sets the maximum number of lines. 0 = unlimited.
    /// </summary>
    public int MaxLines
    {
        get => (int)GetValue(MaxLinesProperty);
        set => SetValue(MaxLinesProperty, value);
    }

    /// <summary>
    /// Gets or sets the line height multiplier.
    /// </summary>
    public float LineHeight
    {
        get => (float)GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the character spacing.
    /// </summary>
    public float CharacterSpacing
    {
        get => (float)GetValue(CharacterSpacingProperty);
        set => SetValue(CharacterSpacingProperty, value);
    }

    /// <summary>
    /// Gets or sets the padding.
    /// </summary>
    public SKRect Padding
    {
        get => (SKRect)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal alignment (compatibility property).
    /// </summary>
    public SkiaTextAlignment HorizontalAlignment
    {
        get => HorizontalTextAlignment switch
        {
            TextAlignment.Start => SkiaTextAlignment.Left,
            TextAlignment.Center => SkiaTextAlignment.Center,
            TextAlignment.End => SkiaTextAlignment.Right,
            _ => SkiaTextAlignment.Left
        };
        set => HorizontalTextAlignment = value switch
        {
            SkiaTextAlignment.Left => TextAlignment.Start,
            SkiaTextAlignment.Center => TextAlignment.Center,
            SkiaTextAlignment.Right => TextAlignment.End,
            _ => TextAlignment.Start
        };
    }

    /// <summary>
    /// Gets or sets the vertical alignment (compatibility property).
    /// </summary>
    public SkiaVerticalAlignment VerticalAlignment
    {
        get => VerticalTextAlignment switch
        {
            TextAlignment.Start => SkiaVerticalAlignment.Top,
            TextAlignment.Center => SkiaVerticalAlignment.Center,
            TextAlignment.End => SkiaVerticalAlignment.Bottom,
            _ => SkiaVerticalAlignment.Top
        };
        set => VerticalTextAlignment = value switch
        {
            SkiaVerticalAlignment.Top => TextAlignment.Start,
            SkiaVerticalAlignment.Center => TextAlignment.Center,
            SkiaVerticalAlignment.Bottom => TextAlignment.End,
            _ => TextAlignment.Start
        };
    }

    #endregion

    private static SKTypeface? _cachedTypeface;

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

    private static SKTypeface GetLinuxTypeface()
    {
        if (_cachedTypeface != null) return _cachedTypeface;

        // Try common Linux font paths
        string[] fontPaths = {
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/ubuntu/Ubuntu-R.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf"
        };

        foreach (var path in fontPaths)
        {
            if (System.IO.File.Exists(path))
            {
                _cachedTypeface = SKTypeface.FromFile(path);
                if (_cachedTypeface != null) return _cachedTypeface;
            }
        }
        return SKTypeface.Default;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        if (string.IsNullOrEmpty(Text))
            return;

        var fontStyle = new SKFontStyle(
            IsBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            IsItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

        var typeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, fontStyle);
        if (typeface == null || typeface == SKTypeface.Default)
        {
            typeface = GetLinuxTypeface();
        }

        using var font = new SKFont(typeface, FontSize);
        using var paint = new SKPaint(font)
        {
            Color = IsEnabled ? TextColor : TextColor.WithAlpha(128),
            IsAntialias = true
        };

        // Calculate content bounds with padding
        var contentBounds = new SKRect(
            bounds.Left + Padding.Left,
            bounds.Top + Padding.Top,
            bounds.Right - Padding.Right,
            bounds.Bottom - Padding.Bottom);

        // Handle single line vs multiline
        // Use DrawSingleLine for normal labels (MaxLines <= 1 or unlimited) without newlines
        // Use DrawMultiLineWithWrapping only when MaxLines > 1 (word wrap needed) or text has newlines
        bool needsMultiLine = MaxLines > 1 || Text.Contains('\n');
        if (needsMultiLine)
        {
            DrawMultiLineWithWrapping(canvas, paint, font, contentBounds);
        }
        else
        {
            DrawSingleLine(canvas, paint, font, contentBounds);
        }
    }

    private void DrawSingleLine(SKCanvas canvas, SKPaint paint, SKFont font, SKRect bounds)
    {
        var displayText = Text;

        // Measure text
        var textBounds = new SKRect();
        paint.MeasureText(displayText, ref textBounds);

        // Apply truncation if needed
        if (textBounds.Width > bounds.Width && LineBreakMode == LineBreakMode.TailTruncation)
        {
            displayText = TruncateText(paint, displayText, bounds.Width);
            paint.MeasureText(displayText, ref textBounds);
        }

        // Calculate position based on alignment
        float x = HorizontalTextAlignment switch
        {
            TextAlignment.Start => bounds.Left,
            TextAlignment.Center => bounds.MidX - textBounds.Width / 2,
            TextAlignment.End => bounds.Right - textBounds.Width,
            _ => bounds.Left
        };

        float y = VerticalTextAlignment switch
        {
            TextAlignment.Start => bounds.Top - textBounds.Top,
            TextAlignment.Center => bounds.MidY - textBounds.MidY,
            TextAlignment.End => bounds.Bottom - textBounds.Bottom,
            _ => bounds.MidY - textBounds.MidY
        };

        canvas.DrawText(displayText, x, y, paint);

        // Draw underline if needed
        if (IsUnderline)
        {
            using var linePaint = new SKPaint
            {
                Color = paint.Color,
                StrokeWidth = 1,
                IsAntialias = true
            };
            var underlineY = y + 2;
            canvas.DrawLine(x, underlineY, x + textBounds.Width, underlineY, linePaint);
        }

        // Draw strikethrough if needed
        if (IsStrikethrough)
        {
            using var linePaint = new SKPaint
            {
                Color = paint.Color,
                StrokeWidth = 1,
                IsAntialias = true
            };
            var strikeY = y - textBounds.Height / 3;
            canvas.DrawLine(x, strikeY, x + textBounds.Width, strikeY, linePaint);
        }
    }

    private void DrawMultiLineWithWrapping(SKCanvas canvas, SKPaint paint, SKFont font, SKRect bounds)
    {
        // Handle inverted or zero-height/width bounds
        var effectiveBounds = bounds;

        // Fix invalid height
        if (bounds.Height <= 0)
        {
            var effectiveLH = LineHeight <= 0 ? 1.2f : LineHeight;
            var estimatedHeight = MaxLines > 0 ? MaxLines * FontSize * effectiveLH : FontSize * effectiveLH * 10;
            effectiveBounds = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + estimatedHeight);
        }

        // Fix invalid width - use a reasonable default if width is invalid or extremely large
        float effectiveWidth = effectiveBounds.Width;
        if (effectiveWidth <= 0)
        {
            // Use a default width based on canvas
            effectiveWidth = 400; // Reasonable default
        }

        // Note: Previously had width capping logic here that reduced effective width
        // to 60% for multiline labels. Removed - the layout system should now provide
        // correct widths, and artificially capping causes text to wrap too early.

        // First, word-wrap the text to fit within bounds
        var wrappedLines = WrapText(paint, Text, effectiveWidth);

        // LineHeight of -1 or <= 0 means "use default" - use 1.2 as default multiplier
        var effectiveLineHeight = LineHeight <= 0 ? 1.2f : LineHeight;
        var lineSpacing = FontSize * effectiveLineHeight;
        var maxLinesToDraw = MaxLines > 0 ? Math.Min(MaxLines, wrappedLines.Count) : wrappedLines.Count;

        // Calculate total height
        var totalHeight = maxLinesToDraw * lineSpacing;

        // Calculate starting Y based on vertical alignment
        float startY = VerticalTextAlignment switch
        {
            TextAlignment.Start => effectiveBounds.Top + FontSize,
            TextAlignment.Center => effectiveBounds.MidY - totalHeight / 2 + FontSize,
            TextAlignment.End => effectiveBounds.Bottom - totalHeight + FontSize,
            _ => effectiveBounds.Top + FontSize
        };

        for (int i = 0; i < maxLinesToDraw; i++)
        {
            var line = wrappedLines[i];

            // Add ellipsis if this is the last line and there are more lines
            bool isLastLine = i == maxLinesToDraw - 1;
            bool hasMoreContent = maxLinesToDraw < wrappedLines.Count;
            if (isLastLine && hasMoreContent && LineBreakMode == LineBreakMode.TailTruncation)
            {
                line = TruncateTextWithEllipsis(paint, line, effectiveWidth);
            }

            var textBounds = new SKRect();
            paint.MeasureText(line, ref textBounds);

            float x = HorizontalTextAlignment switch
            {
                TextAlignment.Start => effectiveBounds.Left,
                TextAlignment.Center => effectiveBounds.MidX - textBounds.Width / 2,
                TextAlignment.End => effectiveBounds.Right - textBounds.Width,
                _ => effectiveBounds.Left
            };

            float y = startY + i * lineSpacing;

            // Don't break early for inverted bounds - just draw
            if (effectiveBounds.Height > 0 && y > effectiveBounds.Bottom)
                break;

            canvas.DrawText(line, x, y, paint);
        }
    }

    private List<string> WrapText(SKPaint paint, string text, float maxWidth)
    {
        var result = new List<string>();

        // Split by newlines first
        var paragraphs = text.Split('\n');

        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrEmpty(paragraph))
            {
                result.Add("");
                continue;
            }

            // Check if paragraph fits in one line
            if (paint.MeasureText(paragraph) <= maxWidth)
            {
                result.Add(paragraph);
                continue;
            }

            // Word wrap this paragraph
            var words = paragraph.Split(' ');
            var currentLine = "";

            foreach (var word in words)
            {
                var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                var lineWidth = paint.MeasureText(testLine);

                if (lineWidth > maxWidth && !string.IsNullOrEmpty(currentLine))
                {
                    result.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                result.Add(currentLine);
            }
        }

        return result;
    }

    private void DrawMultiLine(SKCanvas canvas, SKPaint paint, SKFont font, SKRect bounds)
    {
        var lines = Text.Split('\n');
        var effectiveLineHeight = LineHeight <= 0 ? 1.2f : LineHeight;
        var lineSpacing = FontSize * effectiveLineHeight;
        var maxLinesToDraw = MaxLines > 0 ? Math.Min(MaxLines, lines.Length) : lines.Length;

        // Calculate total height
        var totalHeight = maxLinesToDraw * lineSpacing;

        // Calculate starting Y based on vertical alignment
        float startY = VerticalTextAlignment switch
        {
            TextAlignment.Start => bounds.Top + FontSize,
            TextAlignment.Center => bounds.MidY - totalHeight / 2 + FontSize,
            TextAlignment.End => bounds.Bottom - totalHeight + FontSize,
            _ => bounds.Top + FontSize
        };

        for (int i = 0; i < maxLinesToDraw; i++)
        {
            var line = lines[i];

            // Add ellipsis if this is the last line and there are more
            if (i == maxLinesToDraw - 1 && i < lines.Length - 1 && LineBreakMode == LineBreakMode.TailTruncation)
            {
                line = TruncateText(paint, line, bounds.Width);
            }

            var textBounds = new SKRect();
            paint.MeasureText(line, ref textBounds);

            float x = HorizontalTextAlignment switch
            {
                TextAlignment.Start => bounds.Left,
                TextAlignment.Center => bounds.MidX - textBounds.Width / 2,
                TextAlignment.End => bounds.Right - textBounds.Width,
                _ => bounds.Left
            };

            float y = startY + i * lineSpacing;

            if (y > bounds.Bottom)
                break;

            canvas.DrawText(line, x, y, paint);
        }
    }

    /// <summary>
    /// Truncates text and ALWAYS adds ellipsis (used when there's more content to indicate).
    /// </summary>
    private string TruncateTextWithEllipsis(SKPaint paint, string text, float maxWidth)
    {
        const string ellipsis = "...";
        var ellipsisWidth = paint.MeasureText(ellipsis);
        var textWidth = paint.MeasureText(text);

        // If text + ellipsis fits, just append ellipsis
        if (textWidth + ellipsisWidth <= maxWidth)
            return text + ellipsis;

        // Otherwise, truncate to make room for ellipsis
        var availableWidth = maxWidth - ellipsisWidth;
        if (availableWidth <= 0)
            return ellipsis;

        // Binary search for the right length
        int low = 0;
        int high = text.Length;

        while (low < high)
        {
            int mid = (low + high + 1) / 2;
            var substring = text.Substring(0, mid);

            if (paint.MeasureText(substring) <= availableWidth)
                low = mid;
            else
                high = mid - 1;
        }

        return text.Substring(0, low) + ellipsis;
    }

    private string TruncateText(SKPaint paint, string text, float maxWidth)
    {
        const string ellipsis = "...";
        var ellipsisWidth = paint.MeasureText(ellipsis);

        if (paint.MeasureText(text) <= maxWidth)
            return text;

        var availableWidth = maxWidth - ellipsisWidth;
        if (availableWidth <= 0)
            return ellipsis;

        // Binary search for the right length
        int low = 0;
        int high = text.Length;

        while (low < high)
        {
            int mid = (low + high + 1) / 2;
            var substring = text.Substring(0, mid);

            if (paint.MeasureText(substring) <= availableWidth)
                low = mid;
            else
                high = mid - 1;
        }

        return text.Substring(0, low) + ellipsis;
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return new SKSize(
                Padding.Left + Padding.Right,
                FontSize + Padding.Top + Padding.Bottom);
        }

        var fontStyle = new SKFontStyle(
            IsBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            IsItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

        // Use same typeface logic as OnDraw to ensure consistent measurement
        var typeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, fontStyle);
        if (typeface == null || typeface == SKTypeface.Default)
        {
            typeface = GetLinuxTypeface();
        }

        using var font = new SKFont(typeface, FontSize);
        using var paint = new SKPaint(font);

        // Use same logic as OnDraw: multiline only when MaxLines > 1 or text has newlines
        bool needsMultiLine = MaxLines > 1 || Text.Contains('\n');
        if (!needsMultiLine)
        {
            var textBounds = new SKRect();
            paint.MeasureText(Text, ref textBounds);

            // Add small buffer for font rendering tolerance
            const float widthBuffer = 4f;

            return new SKSize(
                textBounds.Width + Padding.Left + Padding.Right + widthBuffer,
                textBounds.Height + Padding.Top + Padding.Bottom);
        }
        else
        {
            // Use available width for word wrapping measurement
            var wrapWidth = availableSize.Width - Padding.Left - Padding.Right;
            if (wrapWidth <= 0)
            {
                wrapWidth = float.MaxValue; // No wrapping if no width constraint
            }

            // Wrap text to get actual line count
            var wrappedLines = WrapText(paint, Text, wrapWidth);
            var maxLinesToMeasure = MaxLines > 0 ? Math.Min(MaxLines, wrappedLines.Count) : wrappedLines.Count;

            float maxWidth = 0;
            foreach (var line in wrappedLines.Take(maxLinesToMeasure))
            {
                maxWidth = Math.Max(maxWidth, paint.MeasureText(line));
            }

            var effectiveLineHeight = LineHeight <= 0 ? 1.2f : LineHeight;
            var totalHeight = maxLinesToMeasure * FontSize * effectiveLineHeight;

            return new SKSize(
                maxWidth + Padding.Left + Padding.Right,
                totalHeight + Padding.Top + Padding.Bottom);
        }
    }
}

/// <summary>
/// Text alignment options.
/// </summary>
public enum TextAlignment
{
    Start,
    Center,
    End
}

/// <summary>
/// Line break mode options.
/// </summary>
public enum LineBreakMode
{
    NoWrap,
    WordWrap,
    CharacterWrap,
    HeadTruncation,
    TailTruncation,
    MiddleTruncation
}

/// <summary>
/// Horizontal text alignment for Skia label.
/// </summary>
public enum SkiaTextAlignment
{
    Left,
    Center,
    Right
}

/// <summary>
/// Vertical text alignment for Skia label.
/// </summary>
public enum SkiaVerticalAlignment
{
    Top,
    Center,
    Bottom
}
