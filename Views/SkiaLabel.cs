// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Rendering;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered label control for displaying text.
/// </summary>
public class SkiaLabel : SkiaView
{
    public string Text { get; set; } = "";
    public SKColor TextColor { get; set; } = SKColors.Black;
    public string FontFamily { get; set; } = "Sans";
    public float FontSize { get; set; } = 14;
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public bool IsUnderline { get; set; }
    public bool IsStrikethrough { get; set; }
    public TextAlignment HorizontalTextAlignment { get; set; } = TextAlignment.Start;
    public TextAlignment VerticalTextAlignment { get; set; } = TextAlignment.Center;
    public LineBreakMode LineBreakMode { get; set; } = LineBreakMode.TailTruncation;
    public int MaxLines { get; set; } = 0; // 0 = unlimited
    public float LineHeight { get; set; } = 1.2f;
    public float CharacterSpacing { get; set; }
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
    public SKRect Padding { get; set; } = new SKRect(0, 0, 0, 0);

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        if (string.IsNullOrEmpty(Text))
            return;

        var fontStyle = new SKFontStyle(
            IsBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            IsItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

        var typeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, fontStyle)
                      ?? SKTypeface.Default;

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
        if (MaxLines == 1 || !Text.Contains('\n'))
        {
            DrawSingleLine(canvas, paint, font, contentBounds);
        }
        else
        {
            DrawMultiLine(canvas, paint, font, contentBounds);
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

    private void DrawMultiLine(SKCanvas canvas, SKPaint paint, SKFont font, SKRect bounds)
    {
        var lines = Text.Split('\n');
        var lineSpacing = FontSize * LineHeight;
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

        var typeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, fontStyle)
                      ?? SKTypeface.Default;

        using var font = new SKFont(typeface, FontSize);
        using var paint = new SKPaint(font);

        if (MaxLines == 1 || !Text.Contains('\n'))
        {
            var textBounds = new SKRect();
            paint.MeasureText(Text, ref textBounds);

            return new SKSize(
                textBounds.Width + Padding.Left + Padding.Right,
                textBounds.Height + Padding.Top + Padding.Bottom);
        }
        else
        {
            var lines = Text.Split('\n');
            var maxLinesToMeasure = MaxLines > 0 ? Math.Min(MaxLines, lines.Length) : lines.Length;

            float maxWidth = 0;
            foreach (var line in lines.Take(maxLinesToMeasure))
            {
                maxWidth = Math.Max(maxWidth, paint.MeasureText(line));
            }

            var totalHeight = maxLinesToMeasure * FontSize * LineHeight;

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
