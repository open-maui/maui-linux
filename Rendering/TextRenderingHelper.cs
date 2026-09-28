// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>
/// Shared text rendering utilities extracted from SkiaEntry, SkiaEditor, and SkiaLabel
/// to eliminate code duplication for common text rendering operations.
/// </summary>
public static class TextRenderingHelper
{
    /// <summary>
    /// Returns the baseline Y coordinate that vertically centers text on <paramref name="centerY"/>
    /// using glyph-independent font metrics.
    /// </summary>
    /// <remarks>
    /// Do NOT center text vertically via its measured ink bounds
    /// (<c>bounds.MidY - textBounds.MidY</c>): the ink box depends on the glyphs present, so the
    /// computed baseline drifts from string to string. For example, on MediaDemo's buttons,
    /// "Play"/"Stop" (descenders in y, p) sat visibly higher than "Pause"/"Mute" (no descenders),
    /// because a descender extends the ink box downward and shifts the ink-centered baseline up.
    /// Centering on font metrics (ascent/descent) places every string of the same font on the same
    /// baseline regardless of which glyphs it contains. Note SKFontMetrics.Ascent is negative.
    /// Ink-bounds centering remains appropriate only for standalone symbols drawn as text
    /// (e.g. "+"/"−" stepper glyphs, icon-font glyphs), where optical centering of the ink is wanted.
    /// </remarks>
    public static float BaselineForVerticalCenter(SKFont font, float centerY)
    {
        var metrics = font.Metrics;
        return centerY - (metrics.Ascent + metrics.Descent) / 2f;
    }

    /// <summary>
    /// Advance width of <paramref name="text"/> as <see cref="DrawTextWithFallback"/>
    /// draws it: characters the font lacks (emoji, symbols, CJK) are measured in
    /// the fallback face that draws them. Equal to <c>font.MeasureText</c> when no
    /// fallback is needed.
    /// </summary>
    public static float MeasureWidth(SKFont font, string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        if (font.Typeface == null)
            return font.MeasureText(text);

        // A string's advance width depends only on the font and the text, never on the space it
        // is laid out in, but measuring it shapes it with font fallback: re-measuring every label
        // on each step of a window resize made a complex page (Strikeline's) resize at a few
        // frames a second. Keyed by the typeface object, so a disposed typeface's reused handle
        // cannot return another font's width; cleared when it grows past a bound.
        var key = new WidthKey(font.Typeface, font.Size, font.Embolden, font.SkewX, text);
        if (s_widths.TryGetValue(key, out var cached))
            return cached;
        var width = MeasureWidthUncached(font, text);
        if (s_widths.Count >= MaxCachedWidths)
            s_widths.Clear();
        s_widths[key] = width;
        return width;
    }

    private readonly record struct WidthKey(SKTypeface Typeface, float Size, bool Embolden, float SkewX, string Text);

    private const int MaxCachedWidths = 20000;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<WidthKey, float> s_widths = new();

    private static float MeasureWidthUncached(SKFont font, string text)
    {
        var runs = FontFallbackManager.Instance.ShapeTextWithFallback(text, font.Typeface);
        if (runs.Count == 0 || (runs.Count == 1 && ReferenceEquals(runs[0].Typeface, font.Typeface)))
            return font.MeasureText(text);

        float width = 0;
        foreach (var run in runs)
        {
            using var runFont = SkiaFontFactory.Create(run.Typeface, font.Size);
            runFont.Embolden = font.Embolden;
            runFont.SkewX = font.SkewX;
            width += runFont.MeasureText(run.Text);
        }
        return width;
    }

    /// <summary>
    /// <paramref name="text"/> cut to <paramref name="maxWidth"/> with a trailing
    /// ellipsis (by advance width, with font fallback), or whole when it fits.
    /// </summary>
    internal static string Ellipsize(SKFont font, string? text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || MeasureWidth(font, text) <= maxWidth + 0.5f)
            return text ?? string.Empty;
        const string ellipsis = "\u2026";
        int lo = 0, hi = text.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (MeasureWidth(font, text[..mid].TrimEnd() + ellipsis) <= maxWidth)
                lo = mid;
            else
                hi = mid - 1;
        }
        return lo == 0 ? ellipsis : text[..lo].TrimEnd() + ellipsis;
    }

    /// <summary>
    /// Draws text with font fallback for emoji, CJK, and other scripts.
    /// Uses FontFallbackManager to shape text across multiple typefaces when needed.
    /// </summary>
    public static void DrawTextWithFallback(SKCanvas canvas, string text, float x, float y, SKPaint paint, SKTypeface preferredTypeface, float fontSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // Use FontFallbackManager for mixed-script text
        var runs = FontFallbackManager.Instance.ShapeTextWithFallback(text, preferredTypeface);

        if (runs.Count <= 1)
        {
            // One run: the preferred face, or a fallback when the text is made
            // only of characters it lacks (a lone "⋮" in a UI font).
            using var font = SkiaFontFactory.Create(runs.Count == 1 ? runs[0].Typeface : preferredTypeface, fontSize);
            canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);
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

            canvas.DrawText(run.Text, currentX, y, SKTextAlign.Left, runFont, runPaint);
            currentX += runFont.MeasureText(run.Text, runPaint);
        }
    }

    /// <summary>
    /// Draws underline for IME pre-edit (composition) text.
    /// Renders a dashed underline beneath the pre-edit text region.
    /// </summary>
    public static void DrawPreEditUnderline(SKCanvas canvas, SKPaint paint, SKFont font, string displayText, int cursorPosition, string preEditText, float x, float y)
    {
        // Calculate pre-edit text position
        var textToCursor = displayText.Substring(0, Math.Min(cursorPosition, displayText.Length));
        var preEditStartX = x + font.MeasureText(textToCursor, paint);
        var preEditEndX = preEditStartX + font.MeasureText(preEditText, paint);

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
    /// Converts a MAUI Color to SkiaSharp SKColor for rendering.
    /// Returns the specified default color when the input color is null.
    /// </summary>
    public static SKColor ToSKColor(Color? color, SKColor defaultColor = default)
    {
        if (color == null) return defaultColor;
        return color.ToSKColor();
    }

    /// <summary>
    /// Converts FontAttributes to the corresponding SKFontStyle.
    /// </summary>
    public static SKFontStyle GetFontStyle(FontAttributes attributes)
    {
        bool isBold = attributes.HasFlag(FontAttributes.Bold);
        bool isItalic = attributes.HasFlag(FontAttributes.Italic);

        if (isBold && isItalic)
            return SKFontStyle.BoldItalic;
        if (isBold)
            return SKFontStyle.Bold;
        if (isItalic)
            return SKFontStyle.Italic;
        return SKFontStyle.Normal;
    }

    /// <summary>
    /// Gets the effective font family, returning "Sans" as the platform default when empty.
    /// </summary>
    public static string GetEffectiveFontFamily(string? fontFamily)
    {
        return string.IsNullOrEmpty(fontFamily) ? "Sans" : fontFamily;
    }
}
