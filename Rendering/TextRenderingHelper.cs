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

    /// <summary>
    /// Draws underline for IME pre-edit (composition) text.
    /// Renders a dashed underline beneath the pre-edit text region.
    /// </summary>
    public static void DrawPreEditUnderline(SKCanvas canvas, SKPaint paint, string displayText, int cursorPosition, string preEditText, float x, float y)
    {
        // Calculate pre-edit text position
        var textToCursor = displayText.Substring(0, Math.Min(cursorPosition, displayText.Length));
        var preEditStartX = x + paint.MeasureText(textToCursor);
        var preEditEndX = preEditStartX + paint.MeasureText(preEditText);

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
